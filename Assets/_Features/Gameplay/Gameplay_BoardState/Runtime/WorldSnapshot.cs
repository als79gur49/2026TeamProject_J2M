using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class WorldSnapshot
    {
        private readonly BoardBounds _boardBounds;
        private readonly IReadOnlyDictionary<int, EntityState> _entitiesById;
        private readonly IReadOnlyDictionary<Vector2Int, int> _projectileOccupancy;
        private readonly TerrainData _terrainData;
        private readonly IReadOnlyDictionary<Vector2Int, int> _unitOccupancy;

        internal WorldSnapshot(
            Dictionary<int, EntityState> entitiesById,
            Dictionary<Vector2Int, int> unitOccupancy,
            Dictionary<Vector2Int, int> projectileOccupancy,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            _entitiesById = new ReadOnlyDictionary<int, EntityState>(entitiesById ?? throw new ArgumentNullException(nameof(entitiesById)));
            _unitOccupancy = new ReadOnlyDictionary<Vector2Int, int>(unitOccupancy ?? throw new ArgumentNullException(nameof(unitOccupancy)));
            _projectileOccupancy = new ReadOnlyDictionary<Vector2Int, int>(projectileOccupancy ?? throw new ArgumentNullException(nameof(projectileOccupancy)));
            _boardBounds = boardBounds;
            _terrainData = terrainData ?? throw new ArgumentNullException(nameof(terrainData));
        }

        public BoardBounds BoardBounds => _boardBounds;

        public bool TryGetEntity(int entityId, out EntityState entity)
        {
            return _entitiesById.TryGetValue(entityId, out entity);
        }

        public bool TryGetUnitAt(Vector2Int cell, out EntityState entity)
        {
            return WorldQueryService.TryGetEntityAt(_entitiesById, _unitOccupancy, cell, out entity);
        }

        public bool TryGetProjectileAt(Vector2Int cell, out EntityState entity)
        {
            return WorldQueryService.TryGetEntityAt(_entitiesById, _projectileOccupancy, cell, out entity);
        }

        public bool IsInsideBoard(Vector2Int cell)
        {
            return WorldQueryService.IsInsideBoard(_boardBounds, cell);
        }

        public bool IsTerrainBlockedForUnit(Vector2Int cell)
        {
            return WorldQueryService.IsTerrainBlockedForUnit(_terrainData, cell);
        }

        public bool IsBlockedForUnit(Vector2Int cell)
        {
            return WorldQueryService.IsBlockedForUnit(_entitiesById, _unitOccupancy, _boardBounds, _terrainData, cell);
        }

        internal bool TryGetPlacementBlocker(
            EntityType entityType,
            Vector2Int cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return WorldQueryService.TryGetPlacementBlocker(
                _entitiesById,
                _unitOccupancy,
                _projectileOccupancy,
                _boardBounds,
                _terrainData,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        public bool TryGetBoxSlideDestination(
            Vector2Int origin,
            Vector2Int delta,
            out Vector2Int destination,
            out SlideStopper stopper)
        {
            return WorldQueryService.TryGetBoxSlideDestination(
                _entitiesById,
                _unitOccupancy,
                _boardBounds,
                _terrainData,
                origin,
                delta,
                out destination,
                out stopper);
        }

        public bool BlocksMovement(int entityId)
        {
            return WorldQueryService.BlocksMovement(_entitiesById, entityId);
        }

        public bool CanBeTargetedForNewSelection(int entityId)
        {
            return WorldQueryService.CanBeTargetedForNewSelection(_entitiesById, entityId);
        }

        public void EnumerateEntitiesOrdered(List<EntityState> buffer)
        {
            WorldQueryService.EnumerateEntitiesOrdered(_entitiesById, buffer);
        }

        internal bool TryGetUnitBlocker(Vector2Int cell, out SlideStopper blocker)
        {
            return WorldQueryService.TryGetUnitBlocker(_entitiesById, _unitOccupancy, _boardBounds, _terrainData, cell, out blocker);
        }

        internal void EnumerateTerrainBlockedCellsOrdered(List<Vector2Int> buffer)
        {
            WorldQueryService.EnumerateTerrainBlockedCellsOrdered(_terrainData, buffer);
        }

        internal void EnumerateUnitOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            WorldQueryService.EnumerateOccupancyOrdered(_unitOccupancy, buffer);
        }

        internal void EnumerateProjectileOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            WorldQueryService.EnumerateOccupancyOrdered(_projectileOccupancy, buffer);
        }
    }
}
