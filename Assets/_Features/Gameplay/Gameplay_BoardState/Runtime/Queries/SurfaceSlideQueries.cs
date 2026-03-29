using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class SurfaceSlideQueries
    {
        public static bool TryResolveNextSurfaceBoxSlideStep(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            ValidateQueryDictionaries(entitiesById, unitOccupancy);

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
                if (TryGetSurfaceBoxSlideBlocker(
                        entitiesById,
                        unitOccupancy,
                        topology,
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

            if (TryGetSurfaceBoxSlideBlocker(
                    entitiesById,
                    unitOccupancy,
                    topology,
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

        private static bool TryGetSurfaceBoxSlideBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            TerrainData terrainData,
            SurfaceCell cell,
            out SlideStopper stopper)
        {
            if (terrainData.BlocksUnitMovement(cell.PlanarPosition))
            {
                stopper = SlideStopper.CreateTerrain(cell);
                return true;
            }

            if (TryGetStoredOccupant(entitiesById, unitOccupancy, cell, out var entity) &&
                GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, topology) &&
                entity.type != EntityType.Projectile)
            {
                stopper = SlideStopper.CreateEntity(entity);
                return true;
            }

            stopper = default;
            return false;
        }

        private static bool TryGetStoredOccupant(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            SurfaceCell cell,
            out EntityState entity)
        {
            entity = default;

            return occupancyByCell.TryGetValue(cell, out var entityId) &&
                   entitiesById.TryGetValue(entityId, out entity);
        }

        private static void ValidateQueryDictionaries(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            if (occupancyByCell == null)
            {
                throw new ArgumentNullException(nameof(occupancyByCell));
            }
        }
    }
}
