using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class WorldPlacementPolicy
    {
        private static readonly IReadOnlyDictionary<SurfaceCell, int> EmptyOccupancy = new Dictionary<SurfaceCell, int>();

        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetAuthoritativePlacementBlocker(
                entitiesById,
                CreateStackedUnitQueryView(stackedUnitsByCell),
                solidOccupancyByCell,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetAuthoritativePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
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
                stackedUnitsByCell,
                solidOccupancyByCell,
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
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
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
                stackedUnitsByCell,
                solidOccupancyByCell,
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

        public static bool TryGetRepresentablePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetRepresentablePlacementBlocker(
                entitiesById,
                CreateStackedUnitQueryView(stackedUnitsByCell),
                solidOccupancyByCell,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool TryGetRepresentablePlacementBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
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
                stackedUnitsByCell,
                solidOccupancyByCell,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                default,
                PlacementQueryMode.Representable,
                out blocker);
        }

        public static bool TryGetUnitBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
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
                stackedUnitsByCell,
                solidOccupancyByCell,
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
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
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
            ValidateQueryDictionaries(entitiesById, solidOccupancyByCell);

            if (stackedUnitsByCell == null)
            {
                throw new ArgumentNullException(nameof(stackedUnitsByCell));
            }

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

            if (queryMode != PlacementQueryMode.Representable &&
                TerrainBlocksPlacement(entityType, terrainData, cell))
            {
                blocker = SlideStopper.CreateTerrain(cell);
                return true;
            }

            if (TryGetBlockingPlacementEntity(
                    entitiesById,
                    stackedUnitsByCell,
                    solidOccupancyByCell,
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
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            IReadOnlyDictionary<SurfaceCell, int> solidOccupancyByCell,
            IReadOnlyDictionary<SurfaceCell, int> projectileOccupancy,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out EntityState entity)
        {
            switch (entityType)
            {
                case EntityType.Unit:
                    return TryGetPlacementOccupant(
                        entitiesById,
                        solidOccupancyByCell,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        out entity);

                case EntityType.Projectile:
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

                    return TryGetPlacementOccupant(
                        entitiesById,
                        solidOccupancyByCell,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        out entity);

                case EntityType.Box:
                case EntityType.None:
                    if (TryGetPlacementOccupant(
                            entitiesById,
                            solidOccupancyByCell,
                            topology,
                            queryMode,
                            cell,
                            ignoredEntityId,
                            out entity))
                    {
                        return true;
                    }

                    return TryGetPlacementStackedUnit(
                        entitiesById,
                        stackedUnitsByCell,
                        topology,
                        queryMode,
                        cell,
                        ignoredEntityId,
                        out entity);

                default:
                    throw new InvalidOperationException($"Unsupported placement entity type: {entityType}");
            }
        }

        private static IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> CreateStackedUnitQueryView(
            IReadOnlyDictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell)
        {
            if (stackedUnitsByCell == null)
            {
                throw new ArgumentNullException(nameof(stackedUnitsByCell));
            }

            var view = new Dictionary<SurfaceCell, IReadOnlyCollection<int>>(stackedUnitsByCell.Count);
            foreach (var pair in stackedUnitsByCell)
            {
                view.Add(pair.Key, pair.Value);
            }

            return view;
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

        private static bool TryGetPlacementStackedUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            CubeTopologyState topology,
            PlacementQueryMode queryMode,
            SurfaceCell cell,
            int ignoredEntityId,
            out EntityState entity)
        {
            if (SnapshotReadQueries.TryGetStoredStackedUnit(
                    entitiesById,
                    stackedUnitsByCell,
                    topology,
                    cell,
                    requireGameplayVisibility: queryMode == PlacementQueryMode.Gameplay,
                    ignoredEntityId,
                    out entity) &&
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
            SurfaceCell cell)
        {
            switch (entityType)
            {
                case EntityType.None:
                case EntityType.Unit:
                case EntityType.Projectile:
                case EntityType.Box:
                    return terrainData.TryGetTerrain(cell, out var terrainCell) &&
                           (terrainCell.Flags & TerrainFlags.BlocksGroundTraversal) != 0;

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
            Representable = 2,
        }
    }
}
