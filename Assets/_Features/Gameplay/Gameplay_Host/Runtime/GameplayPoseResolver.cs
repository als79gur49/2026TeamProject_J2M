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
        private readonly Func<CubeRotationKind, Quaternion> _resolveTopologyRotationOffset;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        public GameplayPoseResolver(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            Func<CubeRotationKind, Quaternion> resolveTopologyRotationOffset)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _resolveTopologyRotationOffset = resolveTopologyRotationOffset ?? throw new ArgumentNullException(nameof(resolveTopologyRotationOffset));
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
            if (!TryResolveTransitionStartRotation(sourceTopology, destinationTopology, out var transitionStartRotation))
            {
                return TryResolveLegacyTransitionLocalPose(
                    projector,
                    cell,
                    sourceTopology,
                    destinationTopology,
                    entityType,
                    facing,
                    out pose);
            }

            if (!projector.TryProjectTransitionEntityCell(
                    cell,
                    destinationTopology,
                    sourceTopology,
                    entityType,
                    out var projectedPose))
            {
                return false;
            }

            var localRotation = projector.TryResolveTransitionEntityRotation(
                cell,
                destinationTopology,
                sourceTopology,
                facing,
                out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;
            var inverseTransitionStartRotation = Quaternion.Inverse(transitionStartRotation);

            pose = new GameplayEntityPose(
                inverseTransitionStartRotation * projectedPose.LocalPosition,
                inverseTransitionStartRotation * localRotation);
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

            var localRotation = projector.TryResolveTransitionEntityRotation(
                cell,
                sourceTopology,
                destinationTopology,
                facing,
                out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;

            pose = new GameplayEntityPose(
                projectedPose.LocalPosition,
                localRotation);
            return true;
        }

        public bool TryResolveTransitionStartRotation(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            out Quaternion transitionStartRotation)
        {
            transitionStartRotation = Quaternion.identity;
            if (sourceTopology.Equals(destinationTopology))
            {
                return true;
            }

            if (destinationTopology.Equals(sourceTopology.Rotate(CubeRotationKind.Forward)))
            {
                transitionStartRotation = _resolveTopologyRotationOffset(CubeRotationKind.Forward);
                return true;
            }

            if (destinationTopology.Equals(sourceTopology.Rotate(CubeRotationKind.Backward)))
            {
                transitionStartRotation = _resolveTopologyRotationOffset(CubeRotationKind.Backward);
                return true;
            }

            return false;
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
    }
}
