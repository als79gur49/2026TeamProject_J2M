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
            var resolvedDamageResolutions = ResolveDamageResolutions(snapshot, selectedGroups, tickIndex);
            for (var i = 0; i < resolvedDamageResolutions.Count; i++)
            {
                damageResolutions.Add(resolvedDamageResolutions[i]);
            }

            var destroyResolutions = ResolveDestroyResolutions(snapshot, selectedGroups, damageResolutions);
            var delayedAttackEffects = ResolveDelayedAttackEffects(selectedGroups, tickIndex);
            CommitResolved(
                writeContext,
                delayedAttackEffectSink,
                selectedGroups,
                damageResolutions,
                destroyResolutions,
                delayedAttackEffects,
                commitEvents,
                delayedAttackEnqueueEvents);
        }

        internal void CommitResolved(
            IAttackCommitContext writeContext,
            IDelayedAttackEffectSink delayedAttackEffectSink,
            IReadOnlyList<ActionGroup> selectedGroups,
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            IReadOnlyList<DelayedAttackEffectRecord> delayedAttackEffects,
            List<string> commitEvents,
            List<string> delayedAttackEnqueueEvents)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
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

            if (destroyResolutions == null)
            {
                throw new ArgumentNullException(nameof(destroyResolutions));
            }

            if (delayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(delayedAttackEffects));
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

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    var resolution = FindDamageResolution(damageResolutions, group.GroupId, damage.TargetId, damageIndex);
                    if (!resolution.Accepted)
                    {
                        var sourceKindSuffix = BuildSourceKindSuffix(group.AttackSourceKind);
                        commitEvents.Add(
                            $"DamageRejected|G={group.GroupId}|I={group.IntentId}|Source={group.SourceId}{sourceKindSuffix}|Target={damage.TargetId}|Amount={damage.Amount}|Reason={resolution.RejectReason}");
                        continue;
                    }

                    if (resolution.HasPlayerDamageState)
                    {
                        writeContext.SetPlayerDamageState(damage.TargetId, resolution.PlayerDamageState);
                    }

                    writeContext.ApplyDamage(damage.TargetId, damage.Amount);
                    var committedSourceKindSuffix = BuildSourceKindSuffix(group.AttackSourceKind);
                    commitEvents.Add(
                        $"DamageCommitted|G={group.GroupId}|I={group.IntentId}{committedSourceKindSuffix}|Target={damage.TargetId}|Amount={damage.Amount}");
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

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroyResolution = FindDestroyResolution(destroyResolutions, group.GroupId, group.Destroys[destroyIndex].TargetId, destroyIndex);
                    if (!destroyResolution.Accepted)
                    {
                        continue;
                    }

                    writeContext.MarkDestroy(destroyResolution.TargetId);
                    commitEvents.Add(
                        $"DestroyMarked|G={group.GroupId}|I={group.IntentId}|Target={destroyResolution.TargetId}|FinalHp={destroyResolution.FinalHp}|Condition={destroyResolution.Condition}");
                }
            }

            var groupsById = new Dictionary<int, ActionGroup>(selectedGroups.Count);
            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                groupsById[selectedGroups[groupIndex].GroupId] = selectedGroups[groupIndex];
            }

            for (var delayedIndex = 0; delayedIndex < delayedAttackEffects.Count; delayedIndex++)
            {
                var effectRecord = delayedAttackEffects[delayedIndex];
                delayedAttackEffectSink.Enqueue(effectRecord);
                if (!groupsById.TryGetValue(effectRecord.SourceActionPlanId, out var sourceGroup))
                {
                    throw new InvalidOperationException($"Missing source action plan {effectRecord.SourceActionPlanId} for delayed attack effect.");
                }

                delayedAttackEnqueueEvents.Add(
                    $"DelayedAttackEnqueued|G={effectRecord.SourceActionPlanId}|I={sourceGroup.IntentId}|Source={effectRecord.SourceId}|Target={effectRecord.TargetId}|Damage={effectRecord.Damage}|ExecuteTick={effectRecord.ExecuteAtTick}|Sequence={effectRecord.EffectSequence}");
            }
        }

        private List<DamageResolutionRecord> ResolveDamageResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> selectedGroups,
            int tickIndex)
        {
            var damageResolutions = new List<DamageResolutionRecord>();
            var playerDamageStatesByEntityId = new Dictionary<int, PlayerDamageState>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    if (!snapshot.TryGetEntity(damage.TargetId, out var target) ||
                        !EntityRolePolicy.IsPlayerUnit(target))
                    {
                        damageResolutions.Add(
                            new DamageResolutionRecord(
                                group.GroupId,
                                group.IntentId,
                                group.SourceId,
                                group.AttackSourceKind,
                                damage.TargetId,
                                damage.Amount,
                                accepted: true,
                                DamageRejectReason.None,
                                damageIndex));
                        continue;
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
                        damageResolutions.Add(
                            new DamageResolutionRecord(
                                group.GroupId,
                                group.IntentId,
                                group.SourceId,
                                group.AttackSourceKind,
                                damage.TargetId,
                                damage.Amount,
                                accepted: false,
                                DamageRejectReason.ReceiverCooldown,
                                damageIndex));
                        continue;
                    }

                    var updatedState = PlayerDamageQueries.AcceptDamage(
                        damageState,
                        tickIndex,
                        _playerDamageCooldownTicks);
                    playerDamageStatesByEntityId[damage.TargetId] = updatedState;
                    damageResolutions.Add(
                        new DamageResolutionRecord(
                            group.GroupId,
                            group.IntentId,
                            group.SourceId,
                            group.AttackSourceKind,
                            damage.TargetId,
                            damage.Amount,
                            accepted: true,
                            DamageRejectReason.None,
                            damageIndex,
                            hasPlayerDamageState: true,
                            playerDamageState: updatedState));
                }
            }

            return damageResolutions;
        }

        private static List<DestroyResolutionRecord> ResolveDestroyResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> selectedGroups,
            IReadOnlyList<DamageResolutionRecord> damageResolutions)
        {
            var accumulatedDamageByTarget = new Dictionary<int, int>();
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                if (!damageResolutions[i].Accepted)
                {
                    continue;
                }

                if (accumulatedDamageByTarget.TryGetValue(damageResolutions[i].TargetId, out var existingDamage))
                {
                    accumulatedDamageByTarget[damageResolutions[i].TargetId] = existingDamage + damageResolutions[i].Amount;
                    continue;
                }

                accumulatedDamageByTarget[damageResolutions[i].TargetId] = damageResolutions[i].Amount;
            }

            var destroyResolutions = new List<DestroyResolutionRecord>();
            var destroyMarkedTargets = new HashSet<int>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    var accepted = false;
                    var finalHp = 0;

                    if (!destroyMarkedTargets.Contains(destroy.TargetId) &&
                        snapshot.TryGetEntity(destroy.TargetId, out var target) &&
                        !target.markedForDeath)
                    {
                        var accumulatedDamage = accumulatedDamageByTarget.TryGetValue(destroy.TargetId, out var damage)
                            ? damage
                            : 0;
                        finalHp = target.hp - accumulatedDamage;
                        if (destroy.Condition != DestroyCondition.WhenHpDepleted || finalHp <= 0)
                        {
                            destroyMarkedTargets.Add(destroy.TargetId);
                            accepted = true;
                        }
                    }

                    destroyResolutions.Add(
                        new DestroyResolutionRecord(
                            group.GroupId,
                            group.IntentId,
                            group.SourceId,
                            destroy.TargetId,
                            destroy.Condition,
                            finalHp,
                            accepted,
                            destroyIndex));
                }
            }

            return destroyResolutions;
        }

        private static List<DelayedAttackEffectRecord> ResolveDelayedAttackEffects(
            IReadOnlyList<ActionGroup> selectedGroups,
            int tickIndex)
        {
            var delayedAttackEffects = new List<DelayedAttackEffectRecord>();
            var delayedAttackSequence = 1;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                for (var delayedIndex = 0; delayedIndex < group.DelayedAttacks.Count; delayedIndex++)
                {
                    var delayedAttack = group.DelayedAttacks[delayedIndex];
                    delayedAttackEffects.Add(
                        new DelayedAttackEffectRecord(
                            group.SourceId,
                            delayedAttack.TargetId,
                            delayedAttack.Damage,
                            group.Priority,
                            tickIndex,
                            tickIndex + 1,
                            group.GroupId,
                            delayedAttackSequence));
                    delayedAttackSequence++;
                }
            }

            return delayedAttackEffects;
        }

        private static string BuildSourceKindSuffix(AttackSourceKind sourceKind)
        {
            return sourceKind == AttackSourceKind.PassiveContact
                ? $"|SourceKind={sourceKind}"
                : string.Empty;
        }

        private static DamageResolutionRecord FindDamageResolution(
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            int actionPlanId,
            int targetId,
            int localActionIndex)
        {
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                if (damageResolutions[i].ActionPlanId == actionPlanId &&
                    damageResolutions[i].TargetId == targetId &&
                    damageResolutions[i].LocalActionIndex == localActionIndex)
                {
                    return damageResolutions[i];
                }
            }

            throw new InvalidOperationException(
                $"Missing damage resolution for action plan {actionPlanId}, target {targetId}, action {localActionIndex}.");
        }

        private static DestroyResolutionRecord FindDestroyResolution(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int actionPlanId,
            int targetId,
            int localActionIndex)
        {
            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                if (destroyResolutions[i].ActionPlanId == actionPlanId &&
                    destroyResolutions[i].TargetId == targetId &&
                    destroyResolutions[i].LocalActionIndex == localActionIndex)
                {
                    return destroyResolutions[i];
                }
            }

            throw new InvalidOperationException(
                $"Missing destroy resolution for action plan {actionPlanId}, target {targetId}, action {localActionIndex}.");
        }
    }
}
