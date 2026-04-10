using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Attack.Commit
{
    internal sealed class AttackCommitter
    {
        private readonly int _playerDamageCooldownTicks;

        public AttackCommitter()
            : this(
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds))
        {
        }

        public AttackCommitter(PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            _playerDamageCooldownTicks = Math.Max(0, playerControlTiming.DamageCooldownTicks);
        }

        public void Commit(
            WorldSnapshot snapshot,
            IAttackCommitContext writeContext,
            int tickIndex,
            IDelayedAttackEffectSink delayedAttackEffectSink,
            IReadOnlyList<ActionGroup> selectedGroups,
            List<DamageResolutionRecord> damageResolutions,
            List<string> commitEvents,
            List<string> delayedAttackEnqueueEvents)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (tickIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickIndex), "Tick index cannot be negative.");
            }

            if (delayedAttackEffectSink == null)
            {
                throw new ArgumentNullException(nameof(delayedAttackEffectSink));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            if (damageResolutions == null)
            {
                throw new ArgumentNullException(nameof(damageResolutions));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            if (delayedAttackEnqueueEvents == null)
            {
                throw new ArgumentNullException(nameof(delayedAttackEnqueueEvents));
            }

            commitEvents.Clear();
            delayedAttackEnqueueEvents.Clear();
            damageResolutions.Clear();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var stateChangeIndex = 0; stateChangeIndex < group.StateChanges.Count; stateChangeIndex++)
                {
                    var stateChange = group.StateChanges[stateChangeIndex];
                    writeContext.ApplyStateChange(stateChange.EntityId, stateChange.State, stateChange.StateTimer);
                    commitEvents.Add(
                        $"StateChanged|G={group.GroupId}|I={group.IntentId}|E={stateChange.EntityId}|State={stateChange.State}|Timer={stateChange.StateTimer}");
                }
            }

            var accumulatedDamageByTarget = new Dictionary<int, int>();
            var playerDamageStatesByEntityId = new Dictionary<int, PlayerDamageState>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    var resolution = ResolveDamage(
                        snapshot,
                        writeContext,
                        tickIndex,
                        group,
                        damage,
                        playerDamageStatesByEntityId);
                    damageResolutions.Add(resolution);

                    if (!resolution.Accepted)
                    {
                        var sourceKindSuffix = BuildSourceKindSuffix(group.AttackSourceKind);
                        commitEvents.Add(
                            $"DamageRejected|G={group.GroupId}|I={group.IntentId}|Source={group.SourceId}{sourceKindSuffix}|Target={damage.TargetId}|Amount={damage.Amount}|Reason={resolution.RejectReason}");
                        continue;
                    }

                    writeContext.ApplyDamage(damage.TargetId, damage.Amount);
                    var committedSourceKindSuffix = BuildSourceKindSuffix(group.AttackSourceKind);
                    commitEvents.Add(
                        $"DamageCommitted|G={group.GroupId}|I={group.IntentId}{committedSourceKindSuffix}|Target={damage.TargetId}|Amount={damage.Amount}");

                    var accumulatedDamage = damage.Amount;
                    if (accumulatedDamageByTarget.TryGetValue(damage.TargetId, out var existingDamage))
                    {
                        accumulatedDamage += existingDamage;
                    }

                    accumulatedDamageByTarget[damage.TargetId] = accumulatedDamage;
                }
            }

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var spawnIndex = 0; spawnIndex < group.Spawns.Count; spawnIndex++)
                {
                    var spawn = group.Spawns[spawnIndex];
                    writeContext.SpawnEntity(spawn.Entity);
                    commitEvents.Add(
                        $"SpawnCommitted|G={group.GroupId}|I={group.IntentId}|SpawnId={spawn.SpawnId}|E={spawn.Entity.entityId}|Pos=({spawn.Entity.position.x},{spawn.Entity.position.y})|Type={spawn.Entity.type}|SpawnTick={spawn.Entity.spawnTick}");
                }
            }

            var destroyMarkedTargets = new HashSet<int>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    if (destroyMarkedTargets.Contains(destroy.TargetId))
                    {
                        continue;
                    }

                    if (!snapshot.TryGetEntity(destroy.TargetId, out var target) || target.markedForDeath)
                    {
                        continue;
                    }

                    var accumulatedDamage = accumulatedDamageByTarget.TryGetValue(destroy.TargetId, out var damage)
                        ? damage
                        : 0;
                    var finalHp = target.hp - accumulatedDamage;
                    if (destroy.Condition == DestroyCondition.WhenHpDepleted && finalHp > 0)
                    {
                        continue;
                    }

                    destroyMarkedTargets.Add(destroy.TargetId);

                    writeContext.MarkDestroy(destroy.TargetId);
                    commitEvents.Add(
                        $"DestroyMarked|G={group.GroupId}|I={group.IntentId}|Target={destroy.TargetId}|FinalHp={finalHp}|Condition={destroy.Condition}");
                }
            }

            var delayedAttackSequence = 1;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var delayedIndex = 0; delayedIndex < group.DelayedAttacks.Count; delayedIndex++)
                {
                    var delayedAttack = group.DelayedAttacks[delayedIndex];
                    var effectRecord = new DelayedAttackEffectRecord(
                        group.SourceId,
                        delayedAttack.TargetId,
                        delayedAttack.Damage,
                        group.Priority,
                        tickIndex,
                        tickIndex + 1,
                        group.GroupId,
                        delayedAttackSequence);
                    delayedAttackEffectSink.Enqueue(effectRecord);
                    delayedAttackEnqueueEvents.Add(
                        $"DelayedAttackEnqueued|G={group.GroupId}|I={group.IntentId}|Source={effectRecord.SourceId}|Target={effectRecord.TargetId}|Damage={effectRecord.Damage}|ExecuteTick={effectRecord.ExecuteAtTick}|Sequence={effectRecord.EffectSequence}");
                    delayedAttackSequence++;
                }
            }
        }

        private DamageResolutionRecord ResolveDamage(
            WorldSnapshot snapshot,
            IAttackCommitContext writeContext,
            int tickIndex,
            ActionGroup group,
            in DamageAction damage,
            Dictionary<int, PlayerDamageState> playerDamageStatesByEntityId)
        {
            if (!snapshot.TryGetEntity(damage.TargetId, out var target) ||
                !EntityRolePolicy.IsPlayerUnit(target))
            {
                return new DamageResolutionRecord(
                    group.GroupId,
                    group.IntentId,
                    group.SourceId,
                    group.AttackSourceKind,
                    damage.TargetId,
                    damage.Amount,
                    accepted: true,
                    DamageRejectReason.None);
            }

            if (!playerDamageStatesByEntityId.TryGetValue(damage.TargetId, out var damageState))
            {
                damageState = snapshot.TryGetPlayerDamageState(damage.TargetId, out var storedState)
                    ? storedState
                    : default;
            }

            if (!PlayerDamageQueries.CanAcceptDamage(damageState, tickIndex))
            {
                playerDamageStatesByEntityId[damage.TargetId] = damageState;
                return new DamageResolutionRecord(
                    group.GroupId,
                    group.IntentId,
                    group.SourceId,
                    group.AttackSourceKind,
                    damage.TargetId,
                    damage.Amount,
                    accepted: false,
                    DamageRejectReason.ReceiverCooldown);
            }

            var updatedState = PlayerDamageQueries.AcceptDamage(
                damageState,
                tickIndex,
                _playerDamageCooldownTicks);
            playerDamageStatesByEntityId[damage.TargetId] = updatedState;
            writeContext.SetPlayerDamageState(damage.TargetId, updatedState);

            return new DamageResolutionRecord(
                group.GroupId,
                group.IntentId,
                group.SourceId,
                group.AttackSourceKind,
                damage.TargetId,
                damage.Amount,
                accepted: true,
                DamageRejectReason.None);
        }

        private static string BuildSourceKindSuffix(AttackSourceKind sourceKind)
        {
            return sourceKind == AttackSourceKind.PassiveContact
                ? $"|SourceKind={sourceKind}"
                : string.Empty;
        }
    }
}
