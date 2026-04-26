using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class SurfaceSlideQueries
    {
        private static readonly IReadOnlyDictionary<SurfaceCell, int> EmptyProjectileOccupancy = new Dictionary<SurfaceCell, int>();

        public static bool TryResolveNextSurfaceBoxSlideStep(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            ValidateQueryDictionaries(entitiesById, stackedUnitsByCell, solidOccupancyByCell);

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            SurfaceTraversalQueries.ValidateSlideDelta(delta);

            if (!topology.IsFaceActive(origin.face))
            {
                destination = default;
                stopper = default;
                return false;
            }

            if (!boardBounds.IsBounded)
            {
                destination = origin + delta;
                if (TryGetBoxSlideBlocker(
                        entitiesById,
                        stackedUnitsByCell,
                        enemyJumpStatesByEntityId,
                        enemyGlideStatesByEntityId,
                        phasedStatesByEntityId,
                        solidOccupancyByCell,
                        topology,
                        boardBounds,
                        terrainData,
                        destination,
                        out stopper))
                {
                    destination = default;
                    return false;
                }

                stopper = default;
                return true;
            }

            if (!boardBounds.Contains(origin.PlanarPosition))
            {
                throw new InvalidOperationException(
                    $"Slide origin {origin} must be inside the configured board bounds.");
            }

            if (!TryGetNextSurfaceBoxSlideCell(topology, boardBounds, origin, delta, out destination, out stopper))
            {
                destination = default;
                return false;
            }

            if (TryGetBoxSlideBlocker(
                    entitiesById,
                    stackedUnitsByCell,
                    enemyJumpStatesByEntityId,
                    enemyGlideStatesByEntityId,
                    phasedStatesByEntityId,
                    solidOccupancyByCell,
                    topology,
                    boardBounds,
                    terrainData,
                    destination,
                    out stopper))
            {
                destination = default;
                return false;
            }

            return true;
        }

        private static bool TryGetNextSurfaceBoxSlideCell(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell current,
            Vector2Int delta,
            out SurfaceCell next,
            out SlideStopper stopper)
        {
            if (current.face == topology.BottomFace &&
                delta == Vector2Int.up &&
                current.y == boardBounds.MaxInclusive.y)
            {
                next = new SurfaceCell(topology.FrontFace, current.x, boardBounds.MinInclusive.y);
                stopper = default;
                return true;
            }

            if (current.face == topology.FrontFace &&
                delta == Vector2Int.down &&
                current.y == boardBounds.MinInclusive.y)
            {
                next = new SurfaceCell(topology.BottomFace, current.x, boardBounds.MaxInclusive.y);
                stopper = default;
                return true;
            }

            next = current + delta;
            if (boardBounds.Contains(next.PlanarPosition))
            {
                stopper = default;
                return true;
            }

            stopper = SlideStopper.CreateBoardEdge(next);
            next = default;
            return false;
        }
        private static bool TryGetBoxSlideBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            IReadOnlyDictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell cell,
            out SlideStopper stopper)
        {
            return WorldPlacementPolicy.TryGetBoxSlidePlacementBlocker(
                entitiesById,
                stackedUnitsByCell,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                phasedStatesByEntityId,
                solidOccupancyByCell,
                EmptyProjectileOccupancy,
                topology,
                boardBounds,
                terrainData,
                cell,
                out stopper);
        }

        private static void ValidateQueryDictionaries(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (stackedUnitsByCell == null)
            {
                throw new ArgumentNullException(nameof(stackedUnitsByCell));
            }

            if (solidOccupancyByCell == null)
            {
                throw new ArgumentNullException(nameof(solidOccupancyByCell));
            }
        }
    }
}
