using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public enum ContinuousLocomotionRejectionReason
    {
        None = 0,
        MissingEntity = 1,
        NonUnit = 2,
        NonCardinalDelta = 3,
        TopologySeam = 4,
        TraversalBlocked = 5,
    }

    public readonly struct ContinuousLocomotionSweepResult
    {
        public ContinuousLocomotionSweepResult(
            int entityId,
            UnitContinuousLocomotionPose sourcePose,
            SurfaceCell resolvedAnchorCell,
            KinematicOffset2 resolvedLocalOffset,
            KinematicVelocity2 resolvedVelocity,
            bool anchorChanged,
            bool blocked,
            ContinuousLocomotionRejectionReason rejectedBy)
        {
            EntityId = entityId;
            SourcePose = sourcePose;
            ResolvedAnchorCell = resolvedAnchorCell;
            ResolvedLocalOffset = resolvedLocalOffset;
            ResolvedVelocity = resolvedVelocity;
            AnchorChanged = anchorChanged;
            Blocked = blocked;
            RejectedBy = rejectedBy;
        }

        public int EntityId { get; }

        public UnitContinuousLocomotionPose SourcePose { get; }

        public SurfaceCell ResolvedAnchorCell { get; }

        public KinematicOffset2 ResolvedLocalOffset { get; }

        public KinematicVelocity2 ResolvedVelocity { get; }

        public bool AnchorChanged { get; }

        public bool Blocked { get; }

        public ContinuousLocomotionRejectionReason RejectedBy { get; }
    }

    internal static class SurfaceContinuousLocomotionQueries
    {
        public static bool TryResolveSameFaceAxisMove(
            WorldSnapshot snapshot,
            int entityId,
            KinematicVelocity2 delta,
            int collisionRadiusUnits,
            out ContinuousLocomotionSweepResult result,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.TryGetEntity(entityId, out var entity))
            {
                result = CreateRejected(entityId, default, ContinuousLocomotionRejectionReason.MissingEntity);
                return false;
            }

            if (entity.type != EntityType.Unit ||
                !snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var pose))
            {
                result = CreateRejected(entityId, default, ContinuousLocomotionRejectionReason.NonUnit);
                return false;
            }

            if (delta.X.RawValue != 0 && delta.Y.RawValue != 0)
            {
                result = CreateRejected(entityId, pose, ContinuousLocomotionRejectionReason.NonCardinalDelta);
                return false;
            }

            var targetX = checked(pose.LocalOffset.X.RawValue + delta.X.RawValue);
            var targetY = checked(pose.LocalOffset.Y.RawValue + delta.Y.RawValue);
            if (TryResolveApproachAnchorDelta(
                    targetX,
                    targetY,
                    delta,
                    collisionRadiusUnits,
                    out var approachAnchorDelta))
            {
                if (!TryResolveCandidateAnchor(
                        snapshot,
                        entity,
                        pose,
                        approachAnchorDelta,
                        out var approachCandidateAnchor,
                        out var rejectionReason,
                        tileFeatureDefinitions))
                {
                    result = CreateClamped(
                        entityId,
                        pose,
                        approachAnchorDelta,
                        collisionRadiusUnits,
                        rejectionReason);
                    return true;
                }

                if (!TryResolveAnchorDelta(targetX, targetY, out var crossedAnchorDelta))
                {
                    result = new ContinuousLocomotionSweepResult(
                        entityId,
                        pose,
                        pose.AnchorCell,
                        new KinematicOffset2(KinematicFixed.FromRaw(targetX), KinematicFixed.FromRaw(targetY)),
                        delta,
                        anchorChanged: false,
                        blocked: false,
                        rejectedBy: ContinuousLocomotionRejectionReason.None);
                    return true;
                }

                result = new ContinuousLocomotionSweepResult(
                    entityId,
                    pose,
                    approachCandidateAnchor,
                    NormalizeCrossedOffset(targetX, targetY, crossedAnchorDelta),
                    delta,
                    anchorChanged: true,
                    blocked: false,
                    rejectedBy: ContinuousLocomotionRejectionReason.None);
                return true;
            }

            if (!TryResolveAnchorDelta(targetX, targetY, out var anchorDelta))
            {
                result = new ContinuousLocomotionSweepResult(
                    entityId,
                    pose,
                    pose.AnchorCell,
                    new KinematicOffset2(KinematicFixed.FromRaw(targetX), KinematicFixed.FromRaw(targetY)),
                    delta,
                    anchorChanged: false,
                    blocked: false,
                    rejectedBy: ContinuousLocomotionRejectionReason.None);
                return true;
            }

            if (Math.Abs(anchorDelta.x) + Math.Abs(anchorDelta.y) != 1)
            {
                result = CreateRejected(entityId, pose, ContinuousLocomotionRejectionReason.NonCardinalDelta);
                return false;
            }

            var candidateAnchor = pose.AnchorCell + anchorDelta;
            if (!TryResolveCandidateAnchor(
                    snapshot,
                    entity,
                    pose,
                    anchorDelta,
                    out candidateAnchor,
                    out var rejectedBy,
                    tileFeatureDefinitions))
            {
                result = CreateClamped(entityId, pose, anchorDelta, collisionRadiusUnits, rejectedBy);
                return true;
            }

            result = new ContinuousLocomotionSweepResult(
                entityId,
                pose,
                candidateAnchor,
                NormalizeCrossedOffset(targetX, targetY, anchorDelta),
                delta,
                anchorChanged: true,
                blocked: false,
                rejectedBy: ContinuousLocomotionRejectionReason.None);
            return true;
        }

        private static bool TryResolveCandidateAnchor(
            WorldSnapshot snapshot,
            EntityState entity,
            UnitContinuousLocomotionPose pose,
            Vector2Int anchorDelta,
            out SurfaceCell candidateAnchor,
            out ContinuousLocomotionRejectionReason rejectedBy,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            candidateAnchor = pose.AnchorCell + anchorDelta;
            if (candidateAnchor.face != pose.AnchorCell.face)
            {
                rejectedBy = ContinuousLocomotionRejectionReason.TopologySeam;
                return false;
            }

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                candidateAnchor,
                entity.entityId,
                snapshot.Topology,
                CubeRotationKind.None,
                snapshot.Topology,
                tileFeatureDefinitions: tileFeatureDefinitions);
            if (legality.Verdict != LegalityVerdict.Allowed)
            {
                rejectedBy = ContinuousLocomotionRejectionReason.TraversalBlocked;
                return false;
            }

            rejectedBy = ContinuousLocomotionRejectionReason.None;
            return true;
        }

        private static bool TryResolveApproachAnchorDelta(
            int targetX,
            int targetY,
            KinematicVelocity2 delta,
            int collisionRadiusUnits,
            out Vector2Int anchorDelta)
        {
            var radiusUnits = NormalizeCollisionRadiusUnits(collisionRadiusUnits);
            if (radiusUnits <= 0)
            {
                anchorDelta = default;
                return false;
            }

            if (delta.X.RawValue > 0 && targetX >= GetPositiveBlockedClamp(radiusUnits))
            {
                anchorDelta = Vector2Int.right;
                return true;
            }

            if (delta.X.RawValue < 0 && targetX <= GetNegativeBlockedClamp(radiusUnits))
            {
                anchorDelta = Vector2Int.left;
                return true;
            }

            if (delta.Y.RawValue > 0 && targetY >= GetPositiveBlockedClamp(radiusUnits))
            {
                anchorDelta = Vector2Int.up;
                return true;
            }

            if (delta.Y.RawValue < 0 && targetY <= GetNegativeBlockedClamp(radiusUnits))
            {
                anchorDelta = Vector2Int.down;
                return true;
            }

            anchorDelta = default;
            return false;
        }

        private static bool TryResolveAnchorDelta(int targetX, int targetY, out Vector2Int anchorDelta)
        {
            if (targetX >= KinematicFixed.HalfCellUnits)
            {
                anchorDelta = Vector2Int.right;
                return true;
            }

            if (targetX < KinematicFixed.MinLocalOffset)
            {
                anchorDelta = Vector2Int.left;
                return true;
            }

            if (targetY >= KinematicFixed.HalfCellUnits)
            {
                anchorDelta = Vector2Int.up;
                return true;
            }

            if (targetY < KinematicFixed.MinLocalOffset)
            {
                anchorDelta = Vector2Int.down;
                return true;
            }

            anchorDelta = default;
            return false;
        }

        private static ContinuousLocomotionSweepResult CreateRejected(
            int entityId,
            UnitContinuousLocomotionPose pose,
            ContinuousLocomotionRejectionReason reason)
        {
            return new ContinuousLocomotionSweepResult(
                entityId,
                pose,
                pose.AnchorCell,
                pose.LocalOffset,
                KinematicVelocity2.Zero,
                anchorChanged: false,
                blocked: true,
                rejectedBy: reason);
        }

        private static ContinuousLocomotionSweepResult CreateClamped(
            int entityId,
            UnitContinuousLocomotionPose pose,
            Vector2Int anchorDelta,
            int collisionRadiusUnits,
            ContinuousLocomotionRejectionReason reason)
        {
            var radiusUnits = NormalizeCollisionRadiusUnits(collisionRadiusUnits);
            var clampX = pose.LocalOffset.X.RawValue;
            var clampY = pose.LocalOffset.Y.RawValue;
            if (anchorDelta.x > 0)
            {
                clampX = GetPositiveBlockedClamp(radiusUnits);
            }
            else if (anchorDelta.x < 0)
            {
                clampX = GetNegativeBlockedClamp(radiusUnits);
            }
            else if (anchorDelta.y > 0)
            {
                clampY = GetPositiveBlockedClamp(radiusUnits);
            }
            else if (anchorDelta.y < 0)
            {
                clampY = GetNegativeBlockedClamp(radiusUnits);
            }

            return new ContinuousLocomotionSweepResult(
                entityId,
                pose,
                pose.AnchorCell,
                new KinematicOffset2(KinematicFixed.FromRaw(clampX), KinematicFixed.FromRaw(clampY)),
                KinematicVelocity2.Zero,
                anchorChanged: false,
                blocked: true,
                rejectedBy: reason);
        }

        private static int NormalizeCollisionRadiusUnits(int collisionRadiusUnits)
        {
            return Math.Max(0, Math.Min(KinematicFixed.HalfCellUnits - 1, collisionRadiusUnits));
        }

        private static int GetPositiveBlockedClamp(int collisionRadiusUnits)
        {
            return collisionRadiusUnits <= 0
                ? KinematicFixed.MaxPositiveLocalOffset
                : Math.Min(
                    KinematicFixed.MaxPositiveLocalOffset,
                    KinematicFixed.HalfCellUnits - collisionRadiusUnits);
        }

        private static int GetNegativeBlockedClamp(int collisionRadiusUnits)
        {
            return collisionRadiusUnits <= 0
                ? KinematicFixed.MinLocalOffset
                : KinematicFixed.MinLocalOffset + collisionRadiusUnits;
        }

        private static KinematicOffset2 NormalizeCrossedOffset(int targetX, int targetY, Vector2Int anchorDelta)
        {
            if (anchorDelta.x > 0)
            {
                targetX -= KinematicFixed.UnitsPerCell;
            }
            else if (anchorDelta.x < 0)
            {
                targetX += KinematicFixed.UnitsPerCell;
            }
            else if (anchorDelta.y > 0)
            {
                targetY -= KinematicFixed.UnitsPerCell;
            }
            else if (anchorDelta.y < 0)
            {
                targetY += KinematicFixed.UnitsPerCell;
            }

            return new KinematicOffset2(KinematicFixed.FromRaw(targetX), KinematicFixed.FromRaw(targetY));
        }
    }
}
