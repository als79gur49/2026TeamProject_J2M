using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal enum ImpactGeometryRejectReason
    {
        None = 0,
        CrossFaceUnsupported = 1,
    }

    internal readonly struct ImpactGeometry
    {
        public ImpactGeometry(
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            Direction moveFacing,
            bool isFlipImpact)
        {
            SourceCell = sourceCell;
            ImpactCell = impactCell;
            MoveFacing = moveFacing;
            IsFlipImpact = isFlipImpact;
        }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ImpactCell { get; }

        public Direction MoveFacing { get; }

        public bool IsFlipImpact { get; }

        public bool HasSourceFacing => IsFlipImpact;

        public Direction SourceFacing => HasSourceFacing
            ? MoveFacing switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            }
            : Direction.None;
    }

    internal static class ImpactGeometryResolver
    {
        public static bool TryResolve(
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out ImpactGeometry geometry)
        {
            return TryResolve(sourceCell, impactCell, out geometry, out _);
        }

        public static bool TryResolve(
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out ImpactGeometry geometry,
            out ImpactGeometryRejectReason rejectReason)
        {
            if (sourceCell.face != impactCell.face)
            {
                geometry = default;
                rejectReason = ImpactGeometryRejectReason.CrossFaceUnsupported;
                return false;
            }

            var delta = impactCell - sourceCell;
            var moveFacing = ResolveMoveFacing(delta, sourceCell, impactCell);
            geometry = new ImpactGeometry(
                sourceCell,
                impactCell,
                moveFacing,
                isFlipImpact: Math.Abs(delta.x) + Math.Abs(delta.y) > 1);
            rejectReason = ImpactGeometryRejectReason.None;
            return true;
        }

        private static Direction ResolveMoveFacing(
            Vector2Int delta,
            SurfaceCell sourceCell,
            SurfaceCell impactCell)
        {
            if (delta.x > 0)
            {
                return Direction.Right;
            }

            if (delta.x < 0)
            {
                return Direction.Left;
            }

            if (delta.y > 0)
            {
                return Direction.Up;
            }

            if (delta.y < 0)
            {
                return Direction.Down;
            }

            throw new InvalidOperationException(
                $"Impact move requires a distinct destination. Source={sourceCell}|ImpactAt={impactCell}");
        }
    }
}
