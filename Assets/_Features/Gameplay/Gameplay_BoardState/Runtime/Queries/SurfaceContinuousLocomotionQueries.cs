using System;
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
            out ContinuousLocomotionSweepResult result)
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
            if (candidateAnchor.face != pose.AnchorCell.face)
            {
                result = CreateClamped(entityId, pose, anchorDelta, ContinuousLocomotionRejectionReason.TopologySeam);
                return true;
            }

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                candidateAnchor,
                entityId,
                snapshot.Topology,
                CubeRotationKind.None,
                snapshot.Topology);
            if (legality.Verdict != LegalityVerdict.Allowed)
            {
                result = CreateClamped(entityId, pose, anchorDelta, ContinuousLocomotionRejectionReason.TraversalBlocked);
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
            ContinuousLocomotionRejectionReason reason)
        {
            var clampX = pose.LocalOffset.X.RawValue;
            var clampY = pose.LocalOffset.Y.RawValue;
            if (anchorDelta.x > 0)
            {
                clampX = KinematicFixed.MaxPositiveLocalOffset;
            }
            else if (anchorDelta.x < 0)
            {
                clampX = KinematicFixed.MinLocalOffset;
            }
            else if (anchorDelta.y > 0)
            {
                clampY = KinematicFixed.MaxPositiveLocalOffset;
            }
            else if (anchorDelta.y < 0)
            {
                clampY = KinematicFixed.MinLocalOffset;
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
