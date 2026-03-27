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
                    case MovementCommandKind.Interact:
                        TryExpandInteract(snapshot, entity, intent, buffer, rejectedReasons);
                        break;

                    case MovementCommandKind.Move:
                        ExpandMove(snapshot, entity, intent, buffer, rejectedReasons);
                        break;

                    case MovementCommandKind.Throw:
                        TryExpandThrow(snapshot, entity, intent, buffer, rejectedReasons);
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
                    // Move never upgrades into unit push. Occupied unit cells remain blocked.
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

        private static void TryExpandInteract(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!snapshot.TryGetUnitAt(intent.Destination, out var target) || target.type != EntityType.Box)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=InteractTargetNotBox|Cell=({intent.Destination.x},{intent.Destination.y})|Target={target.entityId}|Type={target.type}");
                return;
            }

            if (HasBoxCapability(target, BoxCapabilities.LootOnInteractDestroy))
            {
                return;
            }

            if (!HasBoxCapability(target, BoxCapabilities.Pushable))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=InteractTargetNotPushableBox|Cell=({target.position.x},{target.position.y})|Target={target.entityId}|Capabilities={target.boxCapabilities}");
                return;
            }

            var slideDelta = intent.Destination - source.position;
            if (!snapshot.TryGetBoxSlideDestination(target.position, slideDelta, out var destination, out var stopper))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideRayHasNoStopper|Cell=({target.position.x},{target.position.y})|Direction={ResolveFacing(source.position, intent.Destination)}");
                return;
            }

            if (destination == target.position)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideStopperAdjacent|Target={target.entityId}|{FormatStopper(stopper)}");
                return;
            }

            var slideFacing = ResolveFacing(source.position, intent.Destination);
            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.BoxSlide);

            var currentCell = target.position;
            while (currentCell != destination)
            {
                var nextCell = currentCell + slideDelta;
                actionGroup.Moves.Add(
                    new MoveAction(
                        target.entityId,
                        currentCell,
                        nextCell,
                        slideFacing));
                currentCell = nextCell;
            }

            buffer.Add(actionGroup);
        }

        private static void TryExpandThrow(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!snapshot.TryGetUnitAt(intent.Destination, out var target) ||
                target.type != EntityType.Box ||
                !HasBoxCapability(target, BoxCapabilities.Throwable))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ThrowTargetNotThrowableBox|Cell=({intent.Destination.x},{intent.Destination.y})|Target={target.entityId}|Type={target.type}|Capabilities={target.boxCapabilities}");
                return;
            }

            var interactionDelta = intent.Destination - source.position;
            var landing = source.position - interactionDelta;
            if (snapshot.TryGetUnitBlocker(landing, out var landingBlocker))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ThrowLandingBlocked|{FormatStopper(landingBlocker)}");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Throw);
            actionGroup.Moves.Add(
                new MoveAction(
                    target.entityId,
                    target.position,
                    landing,
                    ResolveCardinalFacing(-interactionDelta, "Throw requires an orthogonal adjacent interaction direction.")));
            buffer.Add(actionGroup);
        }

        private static bool HasBoxCapability(EntityState entity, BoxCapabilities capability)
        {
            return entity.type == EntityType.Box && (entity.boxCapabilities & capability) == capability;
        }

        private static string FormatStopper(SlideStopper stopper)
        {
            switch (stopper.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return $"StopperKind=BoardEdge|Cell=({stopper.Cell.x},{stopper.Cell.y})";

                case SlideStopperKind.Terrain:
                    return $"StopperKind=Terrain|Cell=({stopper.Cell.x},{stopper.Cell.y})";

                case SlideStopperKind.Entity:
                    return $"StopperKind=Entity|Stopper={stopper.EntityId}|StopperType={stopper.EntityType}|Cell=({stopper.Cell.x},{stopper.Cell.y})";

                default:
                    return $"StopperKind=None|Cell=({stopper.Cell.x},{stopper.Cell.y})";
            }
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
