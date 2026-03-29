using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class WorldPlacementPolicy
    {
        private static readonly IReadOnlyDictionary<SurfaceCell, int> EmptyOccupancy = new Dictionary<SurfaceCell, int>();

        public static bool IsBlockedForUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell cell)
        {
            return TryGetUnitBlocker(entitiesById, unitOccupancy, topology, boardBounds, terrainData, cell, out _);
        }

        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                unitOccupancy,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                default,
                PlacementQueryMode.Authoritative,
                out blocker);
        }

        public static bool TryGetGameplayPlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlockerCore(
                entitiesById,
                unitOccupancy,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                topology,
                PlacementQueryMode.Gameplay,
                out blocker);
        }

        public static bool TryGetUnitBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            if (!topology.IsFaceActive(cell.face))
            {
                blocker = default;
                return false;
            }

            return TryGetGameplayPlacementBlocker(
                entitiesById,
                unitOccupancy,
                EmptyOccupancy,
                topology,
                boardBounds,
                terrainData,
                EntityType.Unit,
                cell,
                ignoredEntityId: 0,
                out blocker);
        }

        private static bool TryGetPlacementBlockerCore(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            out SlideStopper blocker)
        {
            ValidateQueryDictionaries(entitiesById, unitOccupancy);

            if (projectileOccupancy == null)
            {
                throw new ArgumentNullException(nameof(projectileOccupancy));
            }

            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            if (queryMode == PlacementQueryMode.Gameplay && !topology.IsFaceActive(cell.face))
            {
                blocker = default;
                return false;
            }

            if (!boardBounds.Contains(cell.PlanarPosition))
            {
                blocker = SlideStopper.CreateBoardEdge(cell);
                return true;
            }

            if (TerrainBlocksPlacement(entityType, terrainData, cell.PlanarPosition))
            {
                blocker = SlideStopper.CreateTerrain(cell);
                return true;
            }

            if (TryGetBlockingPlacementEntity(
                    entitiesById,
                    unitOccupancy,
                    projectileOccupancy,
                    topology,
                    queryMode,
                    entityType,
                    cell,
                    ignoredEntityId,
                    out var blockingEntity))
            {
                blocker = SlideStopper.CreateEntity(blockingEntity);
                return true;
            }

            blocker = default;
            return false;
        }

        private static bool TryGetBlockingPlacementEntity(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out EntityState entity)
        {
            if (entityType == EntityType.Projectile)
            {
                if (TryGetPlacementOccupant(
                        entitiesById,
                        projectileOccupancy,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        out entity))
                {
                    return true;
                }

                if (TryGetPlacementOccupant(
                        entitiesById,
                        unitOccupancy,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        out entity))
                {
                    return true;
                }
            }
            else if (TryGetPlacementOccupant(
                         entitiesById,
                         unitOccupancy,
                         topology,
                         queryMode,
                         cell,
                         ignoredEntityId,
                         out entity))
            {
                return true;
            }

            entity = default;
            return false;
        }

        private static bool TryGetPlacementOccupant(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            SurfaceCell cell,
            int ignoredEntityId,
            out EntityState entity)
        {
            if (TryGetStoredOccupant(entitiesById, occupancyByCell, cell, out entity) &&
                entity.entityId != ignoredEntityId &&
                GameplayEntityQueryPolicy.IsBlockingPlacementEntity(
                    entity,
                    topology,
                    requireGameplayVisibility: queryMode == PlacementQueryMode.Gameplay))
            {
                return true;
            }

            entity = default;
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

        private static bool TerrainBlocksPlacement(
            EntityType entityType,
            TerrainData terrainData,
            Vector2Int cell)
        {
            switch (entityType)
            {
                case EntityType.None:
                case EntityType.Unit:
                case EntityType.Projectile:
                case EntityType.Box:
                    return terrainData.BlocksUnitMovement(cell);

                default:
                    throw new InvalidOperationException($"Unsupported placement entity type: {entityType}");
            }
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

        private enum PlacementQueryMode
        {
            Authoritative = 0,
            Gameplay = 1,
        }
    }
}
