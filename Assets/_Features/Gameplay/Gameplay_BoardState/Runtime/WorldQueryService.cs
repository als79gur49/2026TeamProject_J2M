using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class WorldQueryService
    {
        // Service wrappers preserve authoritative snapshot semantics; callers must not substitute render positions.
        public static bool TryGetEntityAt(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryGetEntityAt(entitiesById, occupancyByCell, topology, cell, out entity);
        }

        public static bool IsInsideBoard(BoardBounds boardBounds, SurfaceCell cell)
        {
            return boardBounds.Contains(cell.PlanarPosition);
        }

        public static bool IsInsideBoard(BoardBounds boardBounds, Vector2Int cell)
        {
            return boardBounds.Contains(cell);
        }

        public static bool IsTerrainBlockedForUnit(
            CubeTopologyState topology,
            TerrainData terrainData,
            SurfaceCell cell)
        {
            return SnapshotReadQueries.IsTerrainBlockedForUnit(topology, terrainData, cell);
        }

        public static bool IsBlockedForUnit(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> unitOccupancy,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData,
            SurfaceCell cell)
        {
            return WorldPlacementPolicy.IsBlockedForUnit(entitiesById, unitOccupancy, topology, boardBounds, terrainData, cell);
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
            return WorldPlacementPolicy.TryGetAuthoritativePlacementBlocker(
                entitiesById,
                unitOccupancy,
                projectileOccupancy,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
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
            return WorldPlacementPolicy.TryGetGameplayPlacementBlocker(
                entitiesById,
                unitOccupancy,
                projectileOccupancy,
                topology,
                boardBounds,
                terrainData,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public static bool BlocksMovement(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            CubeTopologyState topology,
            int entityId)
        {
            return SnapshotReadQueries.BlocksMovement(entitiesById, topology, entityId);
        }

        public static bool CanBeTargetedForNewSelection(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            CubeTopologyState topology,
            int entityId)
        {
            return SnapshotReadQueries.CanBeTargetedForNewSelection(entitiesById, topology, entityId);
        }

        public static void EnumerateEntitiesOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateEntitiesOrdered(entitiesById, buffer);
        }

        public static void EnumerateOccupancyOrdered(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, int> occupancyByCell,
            CubeTopologyState topology,
            List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(entitiesById, occupancyByCell, topology, buffer);
        }

        public static void EnumerateTerrainBlockedCellsOrdered(
            TerrainData terrainData,
            List<Vector2Int> buffer)
        {
            SnapshotReadQueries.EnumerateTerrainBlockedCellsOrdered(terrainData, buffer);
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
            return WorldPlacementPolicy.TryGetUnitBlocker(
                entitiesById,
                unitOccupancy,
                topology,
                boardBounds,
                terrainData,
                cell,
                out blocker);
        }

        public static bool TryResolvePlayerStep(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell origin,
            Direction direction,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return SurfaceTraversalQueries.TryResolvePlayerStep(
                topology,
                boardBounds,
                origin,
                direction,
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
            return SurfaceTraversalQueries.TryResolvePlayerStep(
                topology,
                boardBounds,
                origin,
                delta,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public static bool TryResolveLocalFlipCells(
            CubeTopologyState topology,
            BoardBounds boardBounds,
            SurfaceCell actorCell,
            Vector2Int delta,
            out SurfaceCell targetCell,
            out SurfaceCell landingCell)
        {
            return SurfaceTraversalQueries.TryResolveLocalFlipCells(
                topology,
                boardBounds,
                actorCell,
                delta,
                out targetCell,
                out landingCell);
        }

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
            return SurfaceSlideQueries.TryResolveNextSurfaceBoxSlideStep(
                entitiesById,
                unitOccupancy,
                topology,
                boardBounds,
                terrainData,
                origin,
                delta,
                out destination,
                out stopper);
        }
    }
}
