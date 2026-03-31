using System;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class SurfaceTraversalQueries
    {
        public static bool TryResolvePlayerStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Direction direction,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return TryResolvePlayerStep(
                topology,
                boardBounds,
                origin,
                DirectionToDelta(direction),
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public static bool TryResolvePlayerStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            if (!TryBeginStepResolution(topology, origin, delta, out destination, out rotationKind, out updatedTopology))
            {
                return false;
            }

            if (TryResolveBottomFaceRotation(topology, boardBounds, origin, delta, out destination, out rotationKind, out updatedTopology))
            {
                return true;
            }

            return TryResolveOrdinaryStep(boardBounds, origin, delta, ref destination);
        }

        public static bool TryResolveUnitStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Direction direction,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return TryResolveUnitStep(
                topology,
                boardBounds,
                origin,
                DirectionToDelta(direction),
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public static bool TryResolveUnitStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            if (!TryBeginStepResolution(topology, origin, delta, out destination, out rotationKind, out updatedTopology))
            {
                return false;
            }

            return TryResolveOrdinaryStep(boardBounds, origin, delta, ref destination);
        }

        public static bool TryResolveLocalFlipCells(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell actorCell,
            Vector2Int delta,
            out SurfaceCell targetCell,
            out SurfaceCell landingCell)
        {
            ValidateSlideDelta(delta);

            targetCell = default;
            landingCell = default;

            if (!topology.IsFaceActive(actorCell.face))
            {
                return false;
            }

            var adjacentTarget = actorCell + delta;
            var oppositeLanding = actorCell - delta;
            if (adjacentTarget.face != actorCell.face ||
                oppositeLanding.face != actorCell.face ||
                !boardBounds.Contains(adjacentTarget.PlanarPosition) ||
                !boardBounds.Contains(oppositeLanding.PlanarPosition))
            {
                return false;
            }

            targetCell = adjacentTarget;
            landingCell = oppositeLanding;
            return true;
        }

        internal static void ValidateSlideDelta(Vector2Int delta)
        {
            if ((delta.x == 0 && delta.y == 0) || Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            {
                throw new InvalidOperationException("Slide queries require an orthogonal single-cell direction.");
            }
        }

        private static bool TryBeginStepResolution(
            CubeTopologyState topology,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            ValidateSlideDelta(delta);

            destination = default;
            rotationKind = CubeRotationKind.None;
            updatedTopology = topology;

            return topology.IsFaceActive(origin.face);
        }

        private static bool TryResolveOrdinaryStep(
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            ref SurfaceCell destination)
        {
            var candidate = origin + delta;
            if (!boardBounds.Contains(candidate.PlanarPosition))
            {
                return false;
            }

            destination = candidate;
            return true;
        }

        private static bool TryResolveBottomFaceRotation(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            destination = default;
            rotationKind = CubeRotationKind.None;
            updatedTopology = topology;

            if (!boardBounds.IsBounded || origin.face != topology.BottomFace)
            {
                return false;
            }

            if (delta == Vector2Int.up && origin.y == boardBounds.MaxInclusive.y)
            {
                rotationKind = CubeRotationKind.Forward;
                updatedTopology = topology.Rotate(rotationKind);
                destination = new SurfaceCell(updatedTopology.BottomFace, origin.x, boardBounds.MinInclusive.y);
                return true;
            }

            if (delta == Vector2Int.down && origin.y == boardBounds.MinInclusive.y)
            {
                rotationKind = CubeRotationKind.Backward;
                updatedTopology = topology.Rotate(rotationKind);
                destination = new SurfaceCell(updatedTopology.BottomFace, origin.x, boardBounds.MaxInclusive.y);
                return true;
            }

            return false;
        }

        private static Vector2Int DirectionToDelta(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2Int.up,
                Direction.Right => Vector2Int.right,
                Direction.Down => Vector2Int.down,
                Direction.Left => Vector2Int.left,
                _ => throw new InvalidOperationException("Surface step queries require a cardinal direction."),
            };
        }
    }
}
