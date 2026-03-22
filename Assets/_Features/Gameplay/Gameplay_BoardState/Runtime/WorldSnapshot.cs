using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class WorldSnapshot
    {
        private readonly IReadOnlyDictionary<int, EntityState> _entitiesById;
        private readonly IReadOnlyDictionary<Vector2Int, int> _projectileOccupancy;
        private readonly IReadOnlyDictionary<Vector2Int, int> _unitOccupancy;

        internal WorldSnapshot(
            Dictionary<int, EntityState> entitiesById,
            Dictionary<Vector2Int, int> unitOccupancy,
            Dictionary<Vector2Int, int> projectileOccupancy)
        {
            _entitiesById = new ReadOnlyDictionary<int, EntityState>(entitiesById ?? throw new ArgumentNullException(nameof(entitiesById)));
            _unitOccupancy = new ReadOnlyDictionary<Vector2Int, int>(unitOccupancy ?? throw new ArgumentNullException(nameof(unitOccupancy)));
            _projectileOccupancy = new ReadOnlyDictionary<Vector2Int, int>(projectileOccupancy ?? throw new ArgumentNullException(nameof(projectileOccupancy)));
        }

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

        public bool IsBlockedForUnit(Vector2Int cell)
        {
            return WorldQueryService.IsBlockedForUnit(_unitOccupancy, cell);
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
    }
}
