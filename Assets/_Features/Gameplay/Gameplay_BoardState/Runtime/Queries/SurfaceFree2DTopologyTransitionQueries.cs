using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal enum Free2DTopologyTransitionRejectReason
    {
        None = 0,
        MissingEntity = 1,
        NonUnit = 2,
        MissingContinuousPose = 3,
        NonCardinalDelta = 4,
        UnsupportedSeam = 5,
        TopologyTransitionUnavailable = 6,
        TargetFaceOutOfBounds = 7,
        TargetFaceBlockedByTerrain = 8,
        TargetFaceBlockedBySolid = 9,
        TargetFaceBlockedByUnit = 10,
        TargetFaceBlockedByReservation = 11,
        TargetFaceFootprintBlocked = 12,
        RemapInvalid = 13,
        CrossingAxisDidNotReachSeam = 14,
        TargetFaceBlockedByTileFeature = 15,
    }

    internal readonly struct Free2DTopologyTransitionResult
    {
        public Free2DTopologyTransitionResult(
            bool success,
            int entityId,
            SurfaceCell sourceAnchor,
            KinematicOffset2 sourceLocalOffset,
            KinematicVelocity2 sourceVelocity,
            SurfaceCell targetAnchor,
            CubeTopologyState updatedTopology,
            CubeRotationKind rotationKind,
            KinematicOffset2 targetLocalOffset,
            KinematicVelocity2 targetVelocity,
            int targetResidualX,
            int targetResidualY,
            Free2DTopologyTransitionRejectReason rejectReason,
            LegalityResult targetLegality = default)
        {
            Success = success;
            EntityId = entityId;
            SourceAnchor = sourceAnchor;
            SourceLocalOffset = sourceLocalOffset;
            SourceVelocity = sourceVelocity;
            TargetAnchor = targetAnchor;
            UpdatedTopology = updatedTopology;
            RotationKind = rotationKind;
            TargetLocalOffset = targetLocalOffset;
            TargetVelocity = targetVelocity;
            TargetResidualX = targetResidualX;
            TargetResidualY = targetResidualY;
            RejectReason = rejectReason;
            TargetLegality = targetLegality;
        }

        public bool Success { get; }

        public int EntityId { get; }

        public SurfaceCell SourceAnchor { get; }

        public KinematicOffset2 SourceLocalOffset { get; }

        public KinematicVelocity2 SourceVelocity { get; }

        public SurfaceCell TargetAnchor { get; }

        public CubeTopologyState UpdatedTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public KinematicOffset2 TargetLocalOffset { get; }

        public KinematicVelocity2 TargetVelocity { get; }

        public int TargetResidualX { get; }

        public int TargetResidualY { get; }

        public Free2DTopologyTransitionRejectReason RejectReason { get; }

        public LegalityResult TargetLegality { get; }
    }

    internal static class SurfaceFree2DTopologyTransitionQueries
    {
        public static bool TryResolveFree2DTopologyTransition(
            WorldSnapshot snapshot,
            int entityId,
            Vector2Int directionDelta,
            KinematicVelocity2 velocityDelta,
            int collisionRadiusUnits,
            out Free2DTopologyTransitionResult result,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (Math.Abs(directionDelta.x) + Math.Abs(directionDelta.y) != 1)
            {
                result = CreateRejected(entityId, default, KinematicOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.NonCardinalDelta);
                return false;
            }

            if (!snapshot.TryGetEntity(entityId, out var entity))
            {
                result = CreateRejected(entityId, default, KinematicOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.MissingEntity);
                return false;
            }

            if (entity.type != EntityType.Unit)
            {
                result = CreateRejected(entityId, entity.position, KinematicOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.NonUnit);
                return false;
            }

            if (!snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var pose))
            {
                result = CreateRejected(entityId, entity.position, KinematicOffset2.Zero, velocityDelta, Free2DTopologyTransitionRejectReason.MissingContinuousPose);
                return false;
            }

            if (pose.AnchorCell != entity.position)
            {
                result = CreateRejected(entityId, pose.AnchorCell, pose.LocalOffset, velocityDelta, Free2DTopologyTransitionRejectReason.RemapInvalid);
                return false;
            }

            if (!SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                    snapshot.Topology,
                    snapshot.BoardBounds,
                    entity.position,
                    directionDelta,
                    pose.LocalOffset,
                    velocityDelta,
                    collisionRadiusUnits,
                    out var remapRejectReason,
                    out var remap))
            {
                result = CreateRejected(entityId, entity.position, pose.LocalOffset, velocityDelta, remapRejectReason);
                return false;
            }

            if (TryResolveDirectTargetOccupancyBlocker(
                    snapshot,
                    entityId,
                    remap.UpdatedTopology,
                    remap.TargetAnchor,
                    out var directOccupancyRejectReason))
            {
                result = new Free2DTopologyTransitionResult(
                    false,
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remap.TargetAnchor,
                    remap.UpdatedTopology,
                    remap.RotationKind,
                    remap.TargetLocalOffset,
                    remap.TargetVelocity,
                    0,
                    0,
                    directOccupancyRejectReason);
                return false;
            }

            var targetLegality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                remap.TargetAnchor,
                entityId,
                remap.UpdatedTopology,
                remap.RotationKind,
                remap.UpdatedTopology,
                tileFeatureDefinitions: tileFeatureDefinitions);
            if (targetLegality.Verdict != LegalityVerdict.Allowed)
            {
                result = new Free2DTopologyTransitionResult(
                    false,
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remap.TargetAnchor,
                    remap.UpdatedTopology,
                    remap.RotationKind,
                    remap.TargetLocalOffset,
                    remap.TargetVelocity,
                    0,
                    0,
                    ResolveRejectReason(targetLegality),
                    targetLegality);
                return false;
            }

            if (!snapshot.TryResolvePlayerStep(
                    entity.position,
                    directionDelta,
                    out var traversalTarget,
                    out var rotationKind,
                    out var updatedTopology) ||
                rotationKind == CubeRotationKind.None ||
                traversalTarget.face == entity.position.face ||
                remap.TargetAnchor != traversalTarget ||
                remap.RotationKind != rotationKind ||
                !remap.UpdatedTopology.Equals(updatedTopology))
            {
                result = CreateRejected(entityId, entity.position, pose.LocalOffset, velocityDelta, Free2DTopologyTransitionRejectReason.TopologyTransitionUnavailable);
                return false;
            }

            if (SurfaceContinuousFootprintQueries.IsTargetLocalPoseBlocked(
                    snapshot,
                    entityId,
                    remap.TargetAnchor,
                    remap.TargetLocalOffset,
                    collisionRadiusUnits,
                    directionDelta,
                    updatedTopology,
                    out var footprintRejectReason,
                    out targetLegality,
                    tileFeatureDefinitions))
            {
                result = new Free2DTopologyTransitionResult(
                    false,
                    entityId,
                    entity.position,
                    pose.LocalOffset,
                    velocityDelta,
                    remap.TargetAnchor,
                    updatedTopology,
                    rotationKind,
                    remap.TargetLocalOffset,
                    remap.TargetVelocity,
                    0,
                    0,
                    footprintRejectReason,
                    targetLegality);
                return false;
            }

            result = new Free2DTopologyTransitionResult(
                true,
                entityId,
                entity.position,
                pose.LocalOffset,
                velocityDelta,
                remap.TargetAnchor,
                updatedTopology,
                rotationKind,
                remap.TargetLocalOffset,
                remap.TargetVelocity,
                0,
                0,
                Free2DTopologyTransitionRejectReason.None);
            return true;
        }

        private static Free2DTopologyTransitionResult CreateRejected(
            int entityId,
            SurfaceCell sourceAnchor,
            KinematicOffset2 sourceLocalOffset,
            KinematicVelocity2 sourceVelocity,
            Free2DTopologyTransitionRejectReason reason)
        {
            return new Free2DTopologyTransitionResult(
                false,
                entityId,
                sourceAnchor,
                sourceLocalOffset,
                sourceVelocity,
                default,
                default,
                CubeRotationKind.None,
                KinematicOffset2.Zero,
                KinematicVelocity2.Zero,
                0,
                0,
                reason);
        }

        private static bool TryResolveDirectTargetOccupancyBlocker(
            WorldSnapshot snapshot,
            int movingEntityId,
            CubeTopologyState updatedTopology,
            SurfaceCell targetAnchor,
            out Free2DTopologyTransitionRejectReason rejectReason)
        {
            if (snapshot.TryGetSolidOccupantAt(updatedTopology, targetAnchor, out var solidOccupant) &&
                solidOccupant.entityId != movingEntityId)
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid;
                return true;
            }

            rejectReason = Free2DTopologyTransitionRejectReason.None;
            return false;
        }

        internal static Free2DTopologyTransitionRejectReason ResolveRejectReason(LegalityResult legality)
        {
            if (legality.Blockers.Count == 0)
            {
                return Free2DTopologyTransitionRejectReason.TargetFaceOutOfBounds;
            }

            return legality.Blockers[0].Kind switch
            {
                LegalityBlockerKind.BoardEdge => Free2DTopologyTransitionRejectReason.TargetFaceOutOfBounds,
                LegalityBlockerKind.Terrain => Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTerrain,
                LegalityBlockerKind.Unit => Free2DTopologyTransitionRejectReason.TargetFaceBlockedByUnit,
                LegalityBlockerKind.Solid => Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid,
                LegalityBlockerKind.Reservation => Free2DTopologyTransitionRejectReason.TargetFaceBlockedByReservation,
                LegalityBlockerKind.TileFeature => Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTileFeature,
                _ => Free2DTopologyTransitionRejectReason.TargetFaceOutOfBounds,
            };
        }
    }

    internal static class SurfaceContinuousFootprintQueries
    {
        public static bool IsTargetLocalPoseBlocked(
            WorldSnapshot snapshot,
            int entityId,
            SurfaceCell targetAnchor,
            KinematicOffset2 targetLocalOffset,
            int collisionRadiusUnits,
            Vector2Int entryDirectionDelta,
            CubeTopologyState targetTopology,
            out Free2DTopologyTransitionRejectReason rejectReason,
            out LegalityResult legality,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            rejectReason = Free2DTopologyTransitionRejectReason.None;
            legality = default;
            var radius = Math.Max(0, Math.Min(KinematicFixed.HalfCellUnits - 1, collisionRadiusUnits));
            if (radius <= 0)
            {
                return false;
            }

            if (targetLocalOffset.X.RawValue + radius >= KinematicFixed.HalfCellUnits &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.right, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            if (targetLocalOffset.X.RawValue - radius < KinematicFixed.MinLocalOffset &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.left, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            var movingForward = entryDirectionDelta.y > 0;
            if (movingForward &&
                targetLocalOffset.Y.RawValue + radius >= KinematicFixed.HalfCellUnits &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.up, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            if (!movingForward &&
                targetLocalOffset.Y.RawValue - radius < KinematicFixed.MinLocalOffset &&
                IsBlocked(snapshot, entityId, targetAnchor + Vector2Int.down, targetTopology, out rejectReason, out legality, tileFeatureDefinitions))
            {
                rejectReason = Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked;
                return true;
            }

            return false;
        }

        private static bool IsBlocked(
            WorldSnapshot snapshot,
            int entityId,
            SurfaceCell cell,
            CubeTopologyState targetTopology,
            out Free2DTopologyTransitionRejectReason rejectReason,
            out LegalityResult legality,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                cell,
                entityId,
                targetTopology,
                CubeRotationKind.None,
                targetTopology,
                tileFeatureDefinitions: tileFeatureDefinitions);
            if (legality.Verdict == LegalityVerdict.Allowed)
            {
                rejectReason = Free2DTopologyTransitionRejectReason.None;
                return false;
            }

            rejectReason = SurfaceFree2DTopologyTransitionQueries.ResolveRejectReason(legality);
            return true;
        }
    }
}
