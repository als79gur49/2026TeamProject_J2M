using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using UnityEngine;

namespace Game.Feature.Gameplay.Attack.Expansion
{
    internal sealed class AttackExpander
    {
        private const int StageThreeDamageAmount = 1;
        private const int SpawnedProjectileHp = 1;

        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<AttackIntent> sortedInputs,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedInputs == null)
            {
                throw new ArgumentNullException(nameof(sortedInputs));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (rejectedReasons == null)
            {
                throw new ArgumentNullException(nameof(rejectedReasons));
            }

            buffer.Clear();
            rejectedReasons.Clear();

            for (var i = 0; i < sortedInputs.Count; i++)
            {
                var intent = sortedInputs[i];
                if (intent.CommandKind == AttackCommandKind.ImpactReservation)
                {
                    ExpandSyntheticImpact(snapshot, intent, buffer, rejectedReasons);
                    continue;
                }

                if (intent.CommandKind == AttackCommandKind.DelayedEffect)
                {
                    ExpandDelayedEffect(snapshot, intent, buffer, rejectedReasons);
                    continue;
                }

                if (!snapshot.TryGetEntity(intent.SourceId, out var source))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingSource");
                    continue;
                }

                if (source.hp <= 0 || source.markedForDeath)
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=SourceNotAttackCapable|Hp={source.hp}|Marked={source.markedForDeath}");
                    continue;
                }

                switch (intent.CommandKind)
                {
                    case AttackCommandKind.FireProjectile:
                        ExpandFireProjectile(snapshot, source, intent, buffer, rejectedReasons);
                        continue;

                    case AttackCommandKind.Attack:
                        break;

                    default:
                        rejectedReasons.Add(
                            $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=UnsupportedCommand|Command={intent.CommandKind}");
                        continue;
                }

                if (!snapshot.TryGetEntity(intent.TargetId, out var target))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingTarget");
                    continue;
                }

                if (!IsOrthogonallyAdjacent(source.position, target.position))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=NotAdjacent|SourceCell=({source.position.x},{source.position.y})|TargetCell=({target.position.x},{target.position.y})");
                    continue;
                }

                if (!snapshot.CanBeTargetedForNewSelection(intent.TargetId))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=TargetNotSelectable");
                    continue;
                }

                var actionGroup = new ActionGroup(
                    intent.IntentId,
                    intent.SourceId,
                    intent.Priority,
                    ActionGroupKind.Attack);
                actionGroup.StateChanges.Add(
                    new StateChangeAction(
                        intent.SourceId,
                        EntityPhaseState.Acting,
                        0));
                actionGroup.Damages.Add(new DamageAction(intent.TargetId, StageThreeDamageAmount));
                // Stage3 keeps DestroyMark in the selected group chain; Commit decides whether it applies after accumulated damage.
                actionGroup.Destroys.Add(new DestroyAction(intent.TargetId));
                buffer.Add(actionGroup);
            }
        }

        private static void ExpandSyntheticImpact(
            WorldSnapshot snapshot,
            AttackIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!intent.ImpactReservation.HasValue)
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Reason=MissingImpactReservation|Kind={intent.InputKind}|LocalSequence={intent.LocalSequence}");
                return;
            }

            var reservation = intent.ImpactReservation.Value;

            if (!snapshot.TryGetEntity(intent.SourceId, out var source))
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingSource");
                return;
            }

            if (source.hp <= 0 || source.markedForDeath)
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=SourceNotAttackCapable|Hp={source.hp}|Marked={source.markedForDeath}");
                return;
            }

            if (!snapshot.TryGetEntity(intent.TargetId, out var target))
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingTarget");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Attack);
            actionGroup.Damages.Add(new DamageAction(target.entityId, reservation.Damage));
            actionGroup.Destroys.Add(new DestroyAction(target.entityId));

            if (source.type == EntityType.Projectile)
            {
                actionGroup.Damages.Add(new DamageAction(source.entityId, source.hp));
                actionGroup.Destroys.Add(new DestroyAction(source.entityId));
            }

            buffer.Add(actionGroup);
        }

        private static void ExpandDelayedEffect(
            WorldSnapshot snapshot,
            AttackIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!intent.DelayedAttackEffect.HasValue)
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Reason=MissingDelayedEffect|Kind={intent.InputKind}|LocalSequence={intent.LocalSequence}");
                return;
            }

            var effectRecord = intent.DelayedAttackEffect.Value;

            if (!snapshot.TryGetEntity(intent.SourceId, out var source))
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingSource");
                return;
            }

            if (source.hp <= 0 || source.markedForDeath)
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=SourceNotAttackCapable|Hp={source.hp}|Marked={source.markedForDeath}");
                return;
            }

            if (!snapshot.TryGetEntity(intent.TargetId, out var target))
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingTarget");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Attack);
            actionGroup.Damages.Add(new DamageAction(target.entityId, effectRecord.Damage));
            actionGroup.Destroys.Add(new DestroyAction(target.entityId));
            buffer.Add(actionGroup);
        }

        private static void ExpandFireProjectile(
            WorldSnapshot snapshot,
            EntityState source,
            AttackIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (source.type != EntityType.Unit)
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=SourceCannotFireProjectile|Type={source.type}");
                return;
            }

            var spawnDelta = ResolveDelta(source.facing);
            if (!spawnDelta.HasValue)
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=InvalidSpawnFacing|Facing={source.facing}");
                return;
            }

            var spawnPosition = source.position + spawnDelta.Value;
            if (snapshot.TryGetProjectileAt(spawnPosition, out var existingProjectile))
            {
                rejectedReasons.Add(
                    $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=SpawnDestinationBlockedByProjectile|Cell=({spawnPosition.x},{spawnPosition.y})|Occupant={existingProjectile.entityId}");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Attack);
            actionGroup.StateChanges.Add(
                new StateChangeAction(
                    intent.SourceId,
                    EntityPhaseState.Acting,
                    0));
            actionGroup.Spawns.Add(new SpawnAction(0, CreateProjectileTemplate(source, spawnPosition)));
            buffer.Add(actionGroup);
        }

        private static EntityState CreateProjectileTemplate(EntityState source, Vector2Int spawnPosition)
        {
            return new EntityState
            {
                entityId = 0,
                position = spawnPosition,
                hp = SpawnedProjectileHp,
                maxHp = SpawnedProjectileHp,
                teamId = source.teamId,
                type = EntityType.Projectile,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = source.facing,
                markedForDeath = false,
                spawnTick = 0,
            };
        }

        private static Vector2Int? ResolveDelta(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Vector2Int.up;

                case Direction.Right:
                    return Vector2Int.right;

                case Direction.Down:
                    return Vector2Int.down;

                case Direction.Left:
                    return Vector2Int.left;

                default:
                    return null;
            }
        }

        private static bool IsOrthogonallyAdjacent(Vector2Int source, Vector2Int target)
        {
            var delta = source - target;
            return Math.Abs(delta.x) + Math.Abs(delta.y) == 1;
        }
    }
}
