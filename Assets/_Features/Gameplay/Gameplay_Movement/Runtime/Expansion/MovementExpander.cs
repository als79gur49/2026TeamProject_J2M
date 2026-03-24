using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement;
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

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

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

                switch (intent.CommandKind)
                {
                    case MovementCommandKind.Move:
                        ExpandMove(snapshot, entity, intent, buffer, rejectedReasons);
                        break;

                    case MovementCommandKind.InteractSlide:
                        TryExpandInteractSlide(snapshot, entity, intent, orderedEntities, buffer, rejectedReasons);
                        break;

                    case MovementCommandKind.InteractFlip:
                        TryExpandInteractFlip(snapshot, entity, intent, buffer, rejectedReasons);
                        break;

                    default:
                        rejectedReasons.Add(
                            $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=UnsupportedCommand|Command={intent.CommandKind}");
                        break;
                }
            }
        }

        private static void ExpandMove(
            WorldSnapshot snapshot,
            EntityState entity,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (snapshot.TryGetUnitAt(intent.Destination, out var destinationEntity))
            {
                if (entity.type == EntityType.Projectile)
                {
                    if (TryExpandProjectileImpact(snapshot, intent, buffer, rejectedReasons))
                    {
                        return;
                    }
                }
                else
                {
                    TryExpandPushChain(snapshot, entity, intent, destinationEntity, buffer, rejectedReasons);
                    return;
                }
            }

            if (entity.type == EntityType.Projectile && snapshot.TryGetProjectileAt(intent.Destination, out _))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ProjectileDestinationBlocked|Cell=({intent.Destination.x},{intent.Destination.y})");
                return;
            }

            if (snapshot.IsBlockedForUnit(intent.Destination))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=BlockedDestination|Cell=({intent.Destination.x},{intent.Destination.y})");
                return;
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

        private static void TryExpandInteractSlide(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            IReadOnlyList<EntityState> orderedEntities,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!snapshot.TryGetUnitAt(intent.Destination, out var target) || target.type != EntityType.Box)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideTargetNotBox|Cell=({intent.Destination.x},{intent.Destination.y})|Target={target.entityId}|Type={target.type}");
                return;
            }

            var delta = intent.Destination - source.position;
            if (!TryFindNearestSlideStopper(target.position, delta, orderedEntities, out var stopper))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideRayHasNoStopper|Cell=({target.position.x},{target.position.y})|Direction={ResolveFacing(source.position, intent.Destination)}");
                return;
            }

            var destination = stopper.position - delta;
            if (destination == target.position)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideStopperAdjacent|Target={target.entityId}|Stopper={stopper.entityId}|Cell=({stopper.position.x},{stopper.position.y})");
                return;
            }

            var slideFacing = ResolveFacing(source.position, intent.Destination);
            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Slide);

            var currentCell = target.position;
            while (currentCell != destination)
            {
                var nextCell = currentCell + delta;
                actionGroup.Moves.Add(new MoveAction(target.entityId, currentCell, nextCell, slideFacing));
                currentCell = nextCell;
            }

            buffer.Add(actionGroup);
        }

        private static void TryExpandInteractFlip(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!snapshot.TryGetUnitAt(intent.Destination, out var target) || target.type != EntityType.Box)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipTargetNotBox|Cell=({intent.Destination.x},{intent.Destination.y})|Target={target.entityId}|Type={target.type}");
                return;
            }

            var interactionDelta = intent.Destination - source.position;
            var landing = source.position - interactionDelta;
            if (snapshot.TryGetUnitAt(landing, out var landingOccupant))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipLandingBlocked|Cell=({landing.x},{landing.y})|Occupant={landingOccupant.entityId}|Type={landingOccupant.type}");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Flip);
            actionGroup.Moves.Add(
                new MoveAction(
                    target.entityId,
                    target.position,
                    landing,
                    ResolveCardinalFacing(-interactionDelta, "Flip requires an orthogonal adjacent interaction direction.")));
            buffer.Add(actionGroup);
        }

        private static bool TryFindNearestSlideStopper(
            Vector2Int origin,
            Vector2Int delta,
            IReadOnlyList<EntityState> orderedEntities,
            out EntityState stopper)
        {
            stopper = default;
            var bestDistance = int.MaxValue;

            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var candidate = orderedEntities[i];
                if (candidate.type == EntityType.Projectile)
                {
                    continue;
                }

                if (!IsOnPositiveRay(candidate.position - origin, delta, out var distance))
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    stopper = candidate;
                }
            }

            return bestDistance != int.MaxValue;
        }

        private static bool IsOnPositiveRay(Vector2Int offset, Vector2Int delta, out int distance)
        {
            distance = 0;

            if (delta.x != 0)
            {
                if (offset.y != 0 || offset.x == 0 || Math.Sign(offset.x) != Math.Sign(delta.x))
                {
                    return false;
                }

                distance = Math.Abs(offset.x);
                return true;
            }

            if (offset.x != 0 || offset.y == 0 || Math.Sign(offset.y) != Math.Sign(delta.y))
            {
                return false;
            }

            distance = Math.Abs(offset.y);
            return true;
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
            return ResolveCardinalFacing(
                destination - source,
                "Stage2 movement only supports orthogonal single-cell moves.");
        }

        private static Direction ResolveCardinalFacing(Vector2Int delta, string errorMessage)
        {
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

            throw new InvalidOperationException(errorMessage);
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
