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
        private const int SlidingStateTimerTicks = 2;

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

                ValidateSingleStepMove(entity.position.PlanarPosition, intent.Destination, intent.SourceId);

                if (entity.type == EntityType.Projectile)
                {
                    ExpandProjectileMove(snapshot, entity, intent, buffer, rejectedReasons);
                    continue;
                }

                switch (intent.CommandKind)
                {
                    case MovementCommandKind.Push:
                    case MovementCommandKind.Move:
                        ExpandMoveLike(snapshot, entity, intent, buffer, rejectedReasons);
                        break;

                    case MovementCommandKind.Flip:
                        ExpandFlip(snapshot, entity, intent, buffer, rejectedReasons);
                        break;

                    default:
                        rejectedReasons.Add(
                            $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=UnsupportedCommand|Command={intent.CommandKind}");
                        break;
                }
            }
        }

        private static void ExpandMoveLike(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (IsSlidingPushBox(source))
            {
                ExpandSlidingPushBoxMove(snapshot, source, intent, buffer, rejectedReasons);
                return;
            }

            var delta = ResolveIntentDelta(source.position, intent.Destination);
            var stepFacing = ResolveCardinalFacing(
                delta,
                "Movement intents must remain orthogonal single-step commands.");
            var hasResolvedStep = snapshot.TryResolvePlayerStep(
                source.position,
                delta,
                out var destinationCell,
                out var rotationKind,
                out var updatedTopology);
            if (!hasResolvedStep)
            {
                destinationCell = source.position + delta;
                rotationKind = CubeRotationKind.None;
                updatedTopology = snapshot.Topology;
            }

            var movementTopology = rotationKind == CubeRotationKind.None
                ? snapshot.Topology
                : updatedTopology;

            if (snapshot.TryGetUnitAt(movementTopology, destinationCell, out var target))
            {
                if (target.type == EntityType.Box)
                {
                    if (HasBoxCapability(target, BoxCapabilities.Item))
                    {
                        ExpandItem(source, target, intent, destinationCell, stepFacing, rotationKind, updatedTopology, buffer);
                        return;
                    }

                    if (intent.CommandKind == MovementCommandKind.Push &&
                        snapshot.Topology.IsFaceActive(target.position.face) &&
                        HasBoxCapability(target, BoxCapabilities.Push))
                    {
                        TryExpandPush(snapshot, source, target, intent, delta, stepFacing, buffer, rejectedReasons);
                        return;
                    }

                    if (intent.CommandKind == MovementCommandKind.Push)
                    {
                        rejectedReasons.Add(
                            $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetNotPushBox|Cell={FormatCell(target.position)}|Target={target.entityId}|Capabilities={target.boxCapabilities}");
                        return;
                    }
                }
                else if (intent.CommandKind == MovementCommandKind.Push)
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetNotBox|Cell={FormatCell(destinationCell)}|Target={target.entityId}|Type={target.type}");
                    return;
                }
            }
            else if (intent.CommandKind == MovementCommandKind.Push)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetNotBox|Cell={FormatCell(destinationCell)}|Target=0|Type=None");
                return;
            }

            if (snapshot.TryGetPlacementBlocker(movementTopology, source.type, destinationCell, source.entityId, out var blocker))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=BlockedDestination|Cell={FormatCell(blocker.Cell)}");
                return;
            }

            ExpandMove(source, intent, destinationCell, stepFacing, rotationKind, updatedTopology, buffer);
        }

        private static void ExpandFlip(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            var delta = ResolveIntentDelta(source.position, intent.Destination);
            var interactionFacing = ResolveCardinalFacing(
                delta,
                "Flip commands require an orthogonal adjacent direction.");
            if (!snapshot.TryResolveLocalFlipCells(source.position, delta, out var targetCell, out var landingCell))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipCrossesBoundary|Origin={FormatCell(source.position)}|Direction={interactionFacing}");
                return;
            }

            if (!snapshot.TryGetUnitAt(targetCell, out var target) ||
                target.type != EntityType.Box ||
                !HasBoxCapability(target, BoxCapabilities.Flip))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipTargetNotFlippableBox|Cell={FormatCell(targetCell)}|Target={target.entityId}|Type={target.type}|Capabilities={target.boxCapabilities}");
                return;
            }

            if (snapshot.TryGetPlacementBlocker(snapshot.Topology, target.type, landingCell, target.entityId, out var landingBlocker))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipLandingBlocked|{FormatStopper(landingBlocker)}");
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
                    landingCell,
                    ResolveCardinalFacing(-delta, "Flip landing requires an orthogonal adjacent interaction direction.")));
            buffer.Add(actionGroup);
        }

        private static void ExpandProjectileMove(
            WorldSnapshot snapshot,
            EntityState entity,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (intent.CommandKind != MovementCommandKind.Move)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=UnsupportedCommand|Command={intent.CommandKind}");
                return;
            }

            var delta = ResolveIntentDelta(entity.position, intent.Destination);
            var destinationCell = entity.position + delta;

            if (snapshot.TryGetUnitAt(destinationCell, out _))
            {
                if (TryExpandProjectileImpact(snapshot, intent, destinationCell, buffer, rejectedReasons))
                {
                    return;
                }
            }

            if (snapshot.TryGetProjectileAt(destinationCell, out _))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ProjectileDestinationBlocked|Cell={FormatCell(destinationCell)}");
                return;
            }

            if (snapshot.TryGetPlacementBlocker(snapshot.Topology, entity.type, destinationCell, entity.entityId, out var blocker))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=BlockedDestination|Cell={FormatCell(blocker.Cell)}");
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
                    destinationCell,
                    ResolveCardinalFacing(delta, "Projectile movement requires an orthogonal single-cell direction.")));
            buffer.Add(actionGroup);
        }

        private static void ExpandMove(
            EntityState source,
            MoveIntent intent,
            SurfaceCell destinationCell,
            Direction facing,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology,
            List<ActionGroup> buffer)
        {
            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Move);
            actionGroup.Moves.Add(
                new MoveAction(
                    intent.SourceId,
                    source.position,
                    destinationCell,
                    facing));

            if (rotationKind != CubeRotationKind.None)
            {
                actionGroup.TopologyChanges.Add(new TopologyChangeAction(rotationKind, updatedTopology));
            }

            buffer.Add(actionGroup);
        }

        private static void ExpandItem(
            EntityState source,
            EntityState target,
            MoveIntent intent,
            SurfaceCell destinationCell,
            Direction facing,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology,
            List<ActionGroup> buffer)
        {
            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Item);
            AddDetachAndMarkForDestroy(actionGroup, target);
            actionGroup.Moves.Add(
                new MoveAction(
                    source.entityId,
                    source.position,
                    destinationCell,
                    facing));

            if (rotationKind != CubeRotationKind.None)
            {
                actionGroup.TopologyChanges.Add(new TopologyChangeAction(rotationKind, updatedTopology));
            }

            buffer.Add(actionGroup);
        }

        private static void TryExpandPush(
            WorldSnapshot snapshot,
            EntityState source,
            EntityState target,
            MoveIntent intent,
            Vector2Int delta,
            Direction stepFacing,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            var destinationResolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                snapshot.Topology,
                target.position,
                delta,
                out var destination,
                out var stopper);
            if (!destinationResolved)
            {
                if (HasBoxCapability(target, BoxCapabilities.Destroy))
                {
                    var destroyGroup = new ActionGroup(
                        intent.IntentId,
                        intent.SourceId,
                        intent.Priority,
                        ActionGroupKind.Push);
                    AddDetachAndMarkForDestroy(destroyGroup, target);
                    buffer.Add(destroyGroup);
                    return;
                }

                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideStopperAdjacent|Target={target.entityId}|{FormatStopper(stopper)}");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Push);
            actionGroup.StateChanges.Add(
                new StateChangeAction(
                    target.entityId,
                    EntityPhaseState.Sliding,
                    SlidingStateTimerTicks));
            actionGroup.Moves.Add(
                new MoveAction(
                    target.entityId,
                    target.position,
                    destination,
                    stepFacing));
            buffer.Add(actionGroup);
        }

        private static void ExpandSlidingPushBoxMove(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            var delta = ResolveIntentDelta(source.position, intent.Destination);
            var stepFacing = ResolveCardinalFacing(
                delta,
                "Sliding push boxes require an orthogonal single-step direction.");

            if (!snapshot.TryResolveNextSurfaceBoxSlideStep(
                    snapshot.Topology,
                    source.position,
                    delta,
                    out var destination,
                    out var stopper))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideStopped|Target={source.entityId}|{FormatStopper(stopper)}");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Push);
            actionGroup.StateChanges.Add(
                new StateChangeAction(
                    source.entityId,
                    EntityPhaseState.Sliding,
                    SlidingStateTimerTicks));
            actionGroup.Moves.Add(
                new MoveAction(
                    source.entityId,
                    source.position,
                    destination,
                    stepFacing));
            buffer.Add(actionGroup);
        }

        private static void AddDetachAndMarkForDestroy(ActionGroup actionGroup, EntityState target)
        {
            actionGroup.BoardPresenceChanges.Add(
                new BoardPresenceChangeAction(target.entityId, EntityBoardPresence.Detached));
            actionGroup.Destroys.Add(new DestroyAction(target.entityId, DestroyCondition.AlwaysMark));
        }

        private static bool HasBoxCapability(EntityState entity, BoxCapabilities capability)
        {
            return entity.type == EntityType.Box && (entity.boxCapabilities & capability) == capability;
        }

        private static bool IsSlidingPushBox(EntityState entity)
        {
            return entity.type == EntityType.Box &&
                   entity.state == EntityPhaseState.Sliding &&
                   HasBoxCapability(entity, BoxCapabilities.Push);
        }

        private static string FormatStopper(SlideStopper stopper)
        {
            switch (stopper.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return $"StopperKind=BoardEdge|Cell={FormatCell(stopper.Cell)}";

                case SlideStopperKind.Terrain:
                    return $"StopperKind=Terrain|Cell={FormatCell(stopper.Cell)}";

                case SlideStopperKind.Entity:
                    return $"StopperKind=Entity|Stopper={stopper.EntityId}|StopperType={stopper.EntityType}|Cell={FormatCell(stopper.Cell)}";

                default:
                    return $"StopperKind=None|Cell={FormatCell(stopper.Cell)}";
            }
        }

        private static bool TryExpandProjectileImpact(
            WorldSnapshot snapshot,
            MoveIntent intent,
            SurfaceCell destinationCell,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!snapshot.TryGetUnitAt(destinationCell, out var target))
            {
                return false;
            }

            if (!snapshot.BlocksMovement(target.entityId))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ImpactTargetNotBlocking|Target={target.entityId}|Cell={FormatCell(destinationCell)}");
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

        private static Vector2Int ResolveIntentDelta(SurfaceCell source, Vector2Int destination)
        {
            return destination - source.PlanarPosition;
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

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }
    }
}
