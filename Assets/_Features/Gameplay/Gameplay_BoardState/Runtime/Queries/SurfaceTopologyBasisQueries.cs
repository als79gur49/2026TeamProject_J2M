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
            out Free2DTopologyRemapResult result)
        {
            result = default;
            if (!boardBounds.IsBounded ||
                sourceAnchor.face != topology.BottomFace ||
                directionDelta.x != 0 ||
                Math.Abs(directionDelta.y) != 1)
            {
                return false;
            }

            var projectedY = checked(sourceLocalOffset.Y.RawValue + sourceVelocity.Y.RawValue);
            SurfaceCell targetAnchor;
            CubeRotationKind rotationKind;
            CubeTopologyState updatedTopology;
            int targetY;
            if (directionDelta.y > 0 &&
                sourceAnchor.y == boardBounds.MaxInclusive.y &&
                projectedY >= KinematicFixed.HalfCellUnits)
            {
                rotationKind = CubeRotationKind.Forward;
                updatedTopology = topology.Rotate(rotationKind);
                targetAnchor = new SurfaceCell(updatedTopology.BottomFace, sourceAnchor.x, boardBounds.MinInclusive.y);
                targetY = checked(projectedY - KinematicFixed.UnitsPerCell);
            }
            else if (directionDelta.y < 0 &&
                     sourceAnchor.y == boardBounds.MinInclusive.y &&
                     projectedY < KinematicFixed.MinLocalOffset)
            {
                rotationKind = CubeRotationKind.Backward;
                updatedTopology = topology.Rotate(rotationKind);
                targetAnchor = new SurfaceCell(updatedTopology.BottomFace, sourceAnchor.x, boardBounds.MaxInclusive.y);
                targetY = checked(projectedY + KinematicFixed.UnitsPerCell);
            }
            else
            {
                return false;
            }

            var targetLocalOffset = new KinematicOffset2(
                sourceLocalOffset.X,
                KinematicFixed.FromRaw(targetY));
            if (!targetLocalOffset.IsRepresentableLocalOffset)
            {
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
