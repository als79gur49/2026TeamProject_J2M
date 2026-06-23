using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayPoseResolver
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        public GameplayPoseResolver(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
        }

        public GameplayEntityPose CreateEntityPose(
            GameplayCubeProjector projector,
            SurfaceCell cell,
            CubeTopologyState topology,
            ProjectedCellPose projectedPose,
            Direction facing,
            Vector2 presentationPlaneOffset = default)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            var localRotation = projector.TryResolveEntityRotation(cell, topology, facing, out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;

            return new GameplayEntityPose(
                ResolvePresentationLocalPosition(projectedPose, presentationPlaneOffset),
                localRotation);
        }

        public Vector3 ResolvePresentationLocalPosition(
            ProjectedCellPose projectedPose,
            Vector2 presentationPlaneOffset)
        {
            if (presentationPlaneOffset.sqrMagnitude <= 0.000001f)
            {
                return projectedPose.LocalPosition;
            }

            return projectedPose.LocalPosition +
                   (projectedPose.LocalRotation * new Vector3(
                       presentationPlaneOffset.x,
                       presentationPlaneOffset.y,
                       0f));
        }

        public bool TryResolveLocalPose(
            GameplayCubeProjector projector,
            int entityId,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing,
            out GameplayEntityPose pose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            pose = default;
            var entityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var knownEntityType)
                ? knownEntityType
                : EntityType.Unit;
            if (!projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose))
            {
                return false;
            }

            pose = CreateEntityPose(projector, cell, topology, projectedPose, facing);
            return true;
        }

        public bool TryResolveKinematicLocalPose(
            GameplayCubeProjector projector,
            TickKinematicMotionTrack track,
            bool useDestination,
            out GameplayEntityPose pose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            var anchorCell = useDestination
                ? track.DestinationAnchorCell
                : track.SourceAnchorCell;
            var localOffset = useDestination
                ? track.DestinationLocalOffset
                : track.SourceLocalOffset;
            var topology = useDestination
                ? track.DestinationTopology ?? track.SourceTopology ?? _stateStore.CommittedTopology
                : track.SourceTopology ?? track.DestinationTopology ?? _stateStore.CommittedTopology;
            var facing = useDestination
                ? track.DestinationFacing ?? track.SourceFacing ?? Direction.Up
                : track.SourceFacing ?? track.DestinationFacing ?? Direction.Up;

            pose = default;
            if (!projector.TryProjectEntityCell(anchorCell, topology, track.EntityType, out var projectedPose))
            {
                return false;
            }

            pose = CreateEntityPose(
                projector,
                anchorCell,
                topology,
                projectedPose,
                facing,
                projector.ResolveKinematicPresentationPlaneOffset(localOffset));
            return true;
        }

        public bool TryResolveContinuousLocomotionPose(
            GameplayCubeProjector projector,
            TickContinuousLocomotionTrack track,
            bool useDestination,
            out GameplayEntityPose pose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            var anchorCell = useDestination
                ? track.DestinationAnchorCell
                : track.SourceAnchorCell;
            var localOffset = useDestination
                ? track.DestinationLocalOffset
                : track.SourceLocalOffset;
            var facing = useDestination
                ? track.DestinationFacing
                : track.SourceFacing;
            var topology = useDestination
                ? track.DestinationTopology ?? _stateStore.CommittedTopology
                : track.SourceTopology ?? _stateStore.CommittedTopology;

            pose = default;
            if (!projector.TryProjectEntityCell(anchorCell, topology, EntityType.Unit, out var projectedPose))
            {
                return false;
            }

            pose = CreateEntityPose(
                projector,
                anchorCell,
                topology,
                projectedPose,
                facing,
                projector.ResolveKinematicPresentationPlaneOffset(localOffset));
            return true;
        }

        public bool TryResolveGlidePresentationOffset(
            GameplayCubeProjector projector,
            TickEnemyGlidePresentationSignal signal,
            CubeTopologyState topology,
            out Vector3 offset)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            offset = default;
            var entityType = _stateStore.EntityTypesByEntityId.TryGetValue(signal.EntityId, out var knownEntityType)
                ? knownEntityType
                : EntityType.Unit;
            if (!projector.TryResolveEntitySurfaceNormal(signal.AnchorCell, topology, entityType, out var normal))
            {
                return false;
            }

            var heightWorld = signal.CurrentHeightUnits * projector.CellSize / SimulationFixed.UnitsPerCell;
            offset = -normal * heightWorld;
            return true;
        }

        public bool TryResolveTransitionLocalPose(
            GameplayCubeProjector projector,
            int entityId,
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            Direction facing,
            out GameplayEntityPose pose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            pose = default;
            var entityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var knownEntityType)
                ? knownEntityType
                : EntityType.Unit;
            if (!projector.TryProjectTransitionEntityCell(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    entityType,
                    out var projectedPose))
            {
                return false;
            }

            pose = CreateTransitionEntityPose(
                projector,
                cell,
                sourceTopology,
                destinationTopology,
                projectedPose,
                facing);
            return true;
        }

        public bool TryResolveLegacyTransitionLocalPose(
            GameplayCubeProjector projector,
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            EntityType entityType,
            Direction facing,
            out GameplayEntityPose pose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            pose = default;
            if (!projector.TryProjectTransitionEntityCell(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    entityType,
                    out var projectedPose))
            {
                return false;
            }

            pose = CreateTransitionEntityPose(
                projector,
                cell,
                sourceTopology,
                destinationTopology,
                projectedPose,
                facing);
            return true;
        }

        public bool TryResolveEntityExitSignalLocalPose(
            GameplayCubeProjector projector,
            TickEntityExitPresentationSignal signal,
            out GameplayEntityPose pose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            pose = default;
            if (!projector.TryProjectEntityCell(signal.SourceCell, signal.Topology, signal.EntityType, out var projectedPose))
            {
                return false;
            }

            pose = CreateEntityPose(projector, signal.SourceCell, signal.Topology, projectedPose, signal.Facing);
            return true;
        }

        public bool TryResolveContactDelayedEntityExitSignalLocalPose(
            GameplayCubeProjector projector,
            TickEntityExitPresentationSignal signal,
            out GameplayEntityPose pose)
        {
            if (_stateStore.ViewsByEntityId.TryGetValue(signal.ExitedEntityId, out var view) &&
                view != null)
            {
                pose = new GameplayEntityPose(view.transform.localPosition, view.transform.localRotation);
                return true;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(signal.ExitedEntityId, out pose))
            {
                return true;
            }

            return TryResolveEntityExitSignalLocalPose(projector, signal, out pose);
        }

        public bool TryResolveImpactTransientSignalLocalPoses(
            GameplayCubeProjector projector,
            TickImpactTransientPresentationSignal signal,
            out GameplayEntityPose sourcePose,
            out GameplayEntityPose impactPose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            sourcePose = default;
            impactPose = default;

            if (!projector.TryProjectEntityCell(signal.SourceCell, signal.Topology, signal.EntityType, out var sourceProjectedPose) ||
                !projector.TryProjectEntityCell(signal.ImpactCell, signal.Topology, signal.EntityType, out var impactProjectedPose))
            {
                return false;
            }

            sourcePose = CreateEntityPose(projector, signal.SourceCell, signal.Topology, sourceProjectedPose, signal.Facing);
            impactPose = CreateEntityPose(projector, signal.ImpactCell, signal.Topology, impactProjectedPose, signal.Facing);
            return true;
        }

        public bool TryResolveFlipImpactSignalLocalPoses(
            GameplayCubeProjector projector,
            FlipImpactPresentationSignal signal,
            out GameplayEntityPose sourcePose,
            out GameplayEntityPose impactPose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            sourcePose = default;
            impactPose = default;

            var entityType = _stateStore.EntityTypesByEntityId.TryGetValue(signal.BoxEntityId, out var resolvedEntityType)
                ? resolvedEntityType
                : EntityType.Unit;
            if (!projector.TryProjectEntityCell(signal.SourceCell, signal.Topology, entityType, out var sourceProjectedPose) ||
                !projector.TryProjectEntityCell(signal.ImpactCell, signal.Topology, entityType, out var impactProjectedPose))
            {
                return false;
            }

            sourcePose = CreateEntityPose(projector, signal.SourceCell, signal.Topology, sourceProjectedPose, signal.SourceFacing);
            impactPose = CreateEntityPose(projector, signal.ImpactCell, signal.Topology, impactProjectedPose, signal.ImpactFacing);
            return true;
        }

        public bool TryResolveVisibilityLocalPose(
            GameplayCubeProjector projector,
            TickVisibilityChange change,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickTopologyMotion? topologyMotion,
            out GameplayEntityPose localPose)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            if (previousCommittedLocalTargetPoses == null)
            {
                throw new ArgumentNullException(nameof(previousCommittedLocalTargetPoses));
            }

            if (_trackState.LocalMotionTracks.TryGetValue(change.EntityId, out var motionTrack) &&
                motionTrack.HasClips)
            {
                localPose = motionTrack.TailEndPose;
                return true;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(change.EntityId, out localPose))
            {
                return true;
            }

            if (topologyMotion.HasValue &&
                topologyMotion.Value.RotationKind != CubeRotationKind.None &&
                TryResolveTransitionLocalPose(
                    projector,
                    change.EntityId,
                    change.Cell,
                    change.Topology,
                    topologyMotion.Value.DestinationTopology,
                    change.Facing,
                    out localPose))
            {
                return true;
            }

            if (previousCommittedLocalTargetPoses.TryGetValue(change.EntityId, out localPose))
            {
                return true;
            }

            return TryResolveLocalPose(projector, change.EntityId, change.Cell, change.Topology, change.Facing, out localPose);
        }

        public bool TryResolveFallbackLocalPose(int entityId, out GameplayEntityPose localPose)
        {
            if (_stateStore.CommittedLocalTargetPoses.TryGetValue(entityId, out localPose))
            {
                return true;
            }

            if (_stateStore.TransitionVisibilityStates.TryGetValue(entityId, out var transitionVisibilityState))
            {
                localPose = transitionVisibilityState.LocalPose;
                return true;
            }

            if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedVisibilityState))
            {
                localPose = jumpDetachedVisibilityState.LocalPose;
                return true;
            }

            return _stateStore.RetainedLocalTargetPoses.TryGetValue(entityId, out localPose);
        }

        private static GameplayEntityPose CreateTransitionEntityPose(
            GameplayCubeProjector projector,
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            ProjectedCellPose projectedPose,
            Direction facing)
        {
            var localRotation = projector.TryResolveTransitionEntityRotation(
                cell,
                sourceTopology,
                destinationTopology,
                facing,
                out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;

            return new GameplayEntityPose(
                projectedPose.LocalPosition,
                localRotation);
        }
    }
}
