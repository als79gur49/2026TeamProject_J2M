using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement.Intents;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Expansion
{
    internal sealed class MovementExpander
    {
        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
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

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];

                if (!snapshot.TryGetEntity(intent.SourceId, out var entity))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=MissingSource");
                    continue;
                }

                ValidateSingleStepMove(entity.position, intent.Destination, intent.SourceId);

                if (snapshot.TryGetUnitAt(intent.Destination, out var destinationEntity))
                {
                    if (entity.type == EntityType.Projectile)
                    {
                        if (TryExpandProjectileImpact(snapshot, intent, buffer, rejectedReasons))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        TryExpandPushChain(snapshot, entity, intent, destinationEntity, buffer, rejectedReasons);
                        continue;
                    }
                }

                if (entity.type == EntityType.Projectile)
                {
                    if (snapshot.TryGetProjectileAt(intent.Destination, out _))
                    {
                        rejectedReasons.Add(
                            $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ProjectileDestinationBlocked|Cell=({intent.Destination.x},{intent.Destination.y})");
                        continue;
                    }
                }

                if (snapshot.IsBlockedForUnit(intent.Destination))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=BlockedDestination|Cell=({intent.Destination.x},{intent.Destination.y})");
                    continue;
                }

                var actionGroup = new ActionGroup(
                    intent.IntentId,
                    intent.SourceId,
                    intent.Priority,
                    ActionGroupKind.Move);
                actionGroup.Moves.Add(
                    new MoveAction(
                        intent.SourceId,
                        entity.position,
                        intent.Destination,
                        ResolveFacing(entity.position, intent.Destination)));
                buffer.Add(actionGroup);
            }
        }

        private static void TryExpandPushChain(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            EntityState firstDestinationOccupant,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (firstDestinationOccupant.type != EntityType.Unit)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushChainBlockedByNonUnit|Cell=({firstDestinationOccupant.position.x},{firstDestinationOccupant.position.y})|Target={firstDestinationOccupant.entityId}|Type={firstDestinationOccupant.type}");
                return;
            }

            var delta = intent.Destination - source.position;
            var chain = new List<EntityState>();
            var currentCell = intent.Destination;

            while (snapshot.TryGetUnitAt(currentCell, out var occupant))
            {
                if (occupant.type != EntityType.Unit)
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushChainBlockedByNonUnit|Cell=({occupant.position.x},{occupant.position.y})|Target={occupant.entityId}|Type={occupant.type}");
                    return;
                }

                chain.Add(occupant);
                currentCell += delta;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.PushChain);

            for (var chainIndex = chain.Count - 1; chainIndex >= 0; chainIndex--)
            {
                var pushedEntity = chain[chainIndex];
                actionGroup.Moves.Add(
                    new MoveAction(
                        pushedEntity.entityId,
                        pushedEntity.position,
                        pushedEntity.position + delta,
                        pushedEntity.facing));
            }

            actionGroup.Moves.Add(
                new MoveAction(
                    source.entityId,
                    source.position,
                    intent.Destination,
                    ResolveFacing(source.position, intent.Destination)));
            buffer.Add(actionGroup);
        }

        private static bool TryExpandProjectileImpact(
            WorldSnapshot snapshot,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!snapshot.TryGetUnitAt(intent.Destination, out var target))
            {
                return false;
            }

            if (!snapshot.BlocksMovement(target.entityId))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ImpactTargetNotBlocking|Target={target.entityId}|Cell=({intent.Destination.x},{intent.Destination.y})");
                return true;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.ProjectileImpact);
            buffer.Add(actionGroup);
            return true;
        }

        private static Direction ResolveFacing(Vector2Int source, Vector2Int destination)
        {
            var delta = destination - source;

            if (delta == Vector2Int.up)
            {
                return Direction.Up;
            }

            if (delta == Vector2Int.right)
            {
                return Direction.Right;
            }

            if (delta == Vector2Int.down)
            {
                return Direction.Down;
            }

            if (delta == Vector2Int.left)
            {
                return Direction.Left;
            }

            throw new InvalidOperationException("Stage2 movement only supports orthogonal single-cell moves.");
        }

        private static void ValidateSingleStepMove(Vector2Int source, Vector2Int destination, int sourceId)
        {
            var delta = destination - source;
            if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            {
                throw new InvalidOperationException(
                    $"Entity {sourceId} emitted an invalid Stage2 MoveIntent. Only orthogonal single-cell moves are allowed.");
            }
        }
    }
}
