using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            entity = default;

            return _unitOccupancy.TryGetValue(cell, out var entityId) &&
                   _entitiesById.TryGetValue(entityId, out entity);
        }

        public bool TryGetProjectileAt(Vector2Int cell, out EntityState entity)
        {
            entity = default;

            return _projectileOccupancy.TryGetValue(cell, out var entityId) &&
                   _entitiesById.TryGetValue(entityId, out entity);
        }

        public bool IsBlockedForUnit(Vector2Int cell)
        {
            return _unitOccupancy.ContainsKey(cell);
        }

        public bool BlocksMovement(int entityId)
        {
            return _entitiesById.ContainsKey(entityId);
        }

        public bool CanBeTargetedForNewSelection(int entityId)
        {
            return _entitiesById.TryGetValue(entityId, out var entity) && !entity.markedForDeath;
        }

        public void EnumerateEntitiesOrdered(List<EntityState> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var entity in _entitiesById.Values)
            {
                buffer.Add(entity);
            }

            buffer.Sort(EntityIdComparer.Instance);
        }

        private sealed class EntityIdComparer : IComparer<EntityState>
        {
            internal static readonly EntityIdComparer Instance = new();

            public int Compare(EntityState left, EntityState right)
            {
                return left.entityId.CompareTo(right.entityId);
            }
        }
    }
}
