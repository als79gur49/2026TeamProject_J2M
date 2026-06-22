using System;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct Free2DTopologyRemapResult
    {
        public Free2DTopologyRemapResult(
            SurfaceCell targetAnchor,
            CubeTopologyState updatedTopology,
            CubeRotationKind rotationKind,
            KinematicOffset2 targetLocalOffset,
            KinematicVelocity2 targetVelocity)
        {
            TargetAnchor = targetAnchor;
            UpdatedTopology = updatedTopology;
            RotationKind = rotationKind;
            TargetLocalOffset = targetLocalOffset;
            TargetVelocity = targetVelocity;
        }

        public SurfaceCell TargetAnchor { get; }

        public CubeTopologyState UpdatedTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public KinematicOffset2 TargetLocalOffset { get; }

        public KinematicVelocity2 TargetVelocity { get; }
    }

    internal static class SurfaceTopologyBasisQueries
    {
        public static bool TryRemapBottomFaceYEdgeCrossing(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell sourceAnchor,
            Vector2Int directionDelta,
            KinematicOffset2 sourceLocalOffset,
            KinematicVelocity2 sourceVelocity,
            int collisionRadiusUnits,
            out Free2DTopologyTransitionRejectReason rejectReason,
            out Free2DTopologyRemapResult result)
        {
            result = default;
            rejectReason = Free2DTopologyTransitionRejectReason.None;
            if (!boardBounds.IsBounded ||
                sourceAnchor.face != topology.BottomFace ||
                directionDelta.x != 0 ||
                Math.Abs(directionDelta.y) != 1)
            {
                rejectReason = Free2DTopologyTransitionRejectReason.UnsupportedSeam;
                return false;
            }

            var projectedY = checked(sourceLocalOffset.Y.RawValue + sourceVelocity.Y.RawValue);
            var radius = Math.Max(0, Math.Min(KinematicFixed.HalfCellUnits - 1, collisionRadiusUnits));
            SurfaceCell targetAnchor;
            CubeRotationKind rotationKind;
            CubeTopologyState updatedTopology;
            int targetY;
            if (directionDelta.y > 0 && sourceAnchor.y == boardBounds.MaxInclusive.y)
            {
                var thresholdY = radius > 0
                    ? KinematicFixed.HalfCellUnits - radius
                    : KinematicFixed.HalfCellUnits;
                if (projectedY <= thresholdY)
                {
                    rejectReason = Free2DTopologyTransitionRejectReason.CrossingAxisDidNotReachSeam;
                    return false;
                }

                rotationKind = CubeRotationKind.Forward;
                updatedTopology = topology.Rotate(rotationKind);
                targetAnchor = new SurfaceCell(updatedTopology.BottomFace, sourceAnchor.x, boardBounds.MinInclusive.y);
                targetY = radius > 0
                    ? checked(KinematicFixed.MinLocalOffset + radius + Math.Max(0, projectedY - thresholdY))
                    : checked(projectedY - KinematicFixed.UnitsPerCell);
            }
            else if (directionDelta.y < 0 && sourceAnchor.y == boardBounds.MinInclusive.y)
            {
                var thresholdY = radius > 0
                    ? KinematicFixed.MinLocalOffset + radius
                    : KinematicFixed.MinLocalOffset;
                if (projectedY >= thresholdY)
                {
                    rejectReason = Free2DTopologyTransitionRejectReason.CrossingAxisDidNotReachSeam;
                    return false;
                }

                rotationKind = CubeRotationKind.Backward;
                updatedTopology = topology.Rotate(rotationKind);
                targetAnchor = new SurfaceCell(updatedTopology.BottomFace, sourceAnchor.x, boardBounds.MaxInclusive.y);
                targetY = radius > 0
                    ? checked(KinematicFixed.MaxPositiveLocalOffset - radius - Math.Max(0, thresholdY - projectedY))
                    : checked(projectedY + KinematicFixed.UnitsPerCell);
            }
            else
            {
                rejectReason = Free2DTopologyTransitionRejectReason.UnsupportedSeam;
                return false;
            }

            var targetLocalOffset = new KinematicOffset2(
                sourceLocalOffset.X,
                KinematicFixed.FromRaw(targetY));
            if (!targetLocalOffset.IsRepresentableLocalOffset)
            {
                rejectReason = Free2DTopologyTransitionRejectReason.RemapInvalid;
                return false;
            }

            var targetVelocity = new KinematicVelocity2(
                KinematicFixed.Zero,
                sourceVelocity.Y);
            result = new Free2DTopologyRemapResult(
                targetAnchor,
                updatedTopology,
                rotationKind,
                targetLocalOffset,
                targetVelocity);
            return true;
        }
    }
}
