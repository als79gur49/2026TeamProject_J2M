using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Attack.Commit
{
    internal sealed class AttackCommitter
    {
        public void Commit(
            WorldSnapshot snapshot,
            IWorldWriteContext writeContext,
            IReadOnlyList<ActionGroup> selectedGroups,
            List<string> commitEvents)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            commitEvents.Clear();

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

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    writeContext.ApplyDamage(damage.TargetId, damage.Amount);
                    commitEvents.Add(
                        $"DamageCommitted|G={group.GroupId}|I={group.IntentId}|Target={damage.TargetId}|Amount={damage.Amount}");

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
                    if (finalHp > 0)
                    {
                        continue;
                    }

                    destroyMarkedTargets.Add(destroy.TargetId);
                    writeContext.MarkDestroy(destroy.TargetId);
                    commitEvents.Add(
                        $"DestroyMarked|G={group.GroupId}|I={group.IntentId}|Target={destroy.TargetId}|FinalHp={finalHp}");
                }
            }
        }
    }
}
