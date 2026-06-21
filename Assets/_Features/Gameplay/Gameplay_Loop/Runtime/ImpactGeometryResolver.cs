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

    internal static class ImpactGeometryResolver
    {
        public static bool TryResolve(
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out ImpactTravelGeometry geometry)
        {
            return TryResolve(sourceCell, impactCell, out geometry, out _);
        }

        public static bool TryResolve(
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out ImpactTravelGeometry geometry,
            out ImpactGeometryRejectReason rejectReason)
        {
            if (sourceCell.face != impactCell.face)
            {
                geometry = default;
                rejectReason = ImpactGeometryRejectReason.CrossFaceUnsupported;
                return false;
            }

            return TryResolveSameFace(sourceCell, impactCell, out geometry, out rejectReason);
        }

        public static bool TryResolve(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out ImpactTravelGeometry geometry)
        {
            return TryResolve(topology, boardBounds, sourceCell, impactCell, out geometry, out _);
        }

        public static bool TryResolve(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out ImpactTravelGeometry geometry,
            out ImpactGeometryRejectReason rejectReason)
        {
            if (sourceCell.face == impactCell.face)
            {
                return TryResolveSameFace(sourceCell, impactCell, out geometry, out rejectReason);
            }

            if (TryResolveBottomFrontSeam(topology, boardBounds, sourceCell, impactCell, out var moveFacing))
            {
                geometry = new ImpactTravelGeometry(
                    sourceCell,
                    impactCell,
                    impactCell,
                    moveFacing);
                rejectReason = ImpactGeometryRejectReason.None;
                return true;
            }

            geometry = default;
            rejectReason = ImpactGeometryRejectReason.CrossFaceUnsupported;
            return false;
        }

        private static bool TryResolveSameFace(
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out ImpactTravelGeometry geometry,
            out ImpactGeometryRejectReason rejectReason)
        {
            var delta = impactCell - sourceCell;
            var moveFacing = ResolveMoveFacing(delta, sourceCell, impactCell);
            geometry = new ImpactTravelGeometry(
                sourceCell,
                impactCell,
                impactCell,
                moveFacing);
            rejectReason = ImpactGeometryRejectReason.None;
            return true;
        }

        private static bool TryResolveBottomFrontSeam(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            out Direction moveFacing)
        {
            if (!boardBounds.IsBounded ||
                sourceCell.x != impactCell.x)
            {
                moveFacing = Direction.None;
                return false;
            }

            if (sourceCell.face == topology.BottomFace &&
                impactCell.face == topology.FrontFace &&
                sourceCell.y == boardBounds.MaxInclusive.y &&
                impactCell.y == boardBounds.MinInclusive.y)
            {
                moveFacing = Direction.Up;
                return true;
            }

            if (sourceCell.face == topology.FrontFace &&
                impactCell.face == topology.BottomFace &&
                sourceCell.y == boardBounds.MinInclusive.y &&
                impactCell.y == boardBounds.MaxInclusive.y)
            {
                moveFacing = Direction.Down;
                return true;
            }

            moveFacing = Direction.None;
            return false;
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
