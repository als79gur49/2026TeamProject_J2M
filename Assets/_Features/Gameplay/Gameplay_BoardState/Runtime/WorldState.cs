using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class WorldState : IWorldStateMutationPort
    {
        private readonly Dictionary<int, EntityState> _entitiesById = new();
        private readonly Dictionary<Vector2Int, int> _projectileOccupancy = new();
        private readonly Dictionary<Vector2Int, int> _unitOccupancy = new();

        public WorldState()
        {
        }

        internal WorldState(IEnumerable<EntityState> initialEntities)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            foreach (var entity in initialEntities)
            {
                AddNewEntity(entity);
            }
        }

        internal WorldSnapshot CreateSnapshot()
        {
            return new WorldSnapshot(
                new Dictionary<int, EntityState>(_entitiesById),
                new Dictionary<Vector2Int, int>(_unitOccupancy),
                new Dictionary<Vector2Int, int>(_projectileOccupancy));
        }

        internal IWorldWriteContext CreateWriteContext()
        {
            return new WorldStateWriteContext((IWorldStateMutationPort)this);
        }

        private void AddNewEntity(EntityState entity)
        {
            if (_entitiesById.ContainsKey(entity.entityId))
            {
                throw new InvalidOperationException("Duplicate entity id detected while adding entity.");
            }

            _entitiesById.Add(entity.entityId, entity);
            SetOccupancy(entity);
        }

        private void ClearOccupancy(EntityState entity)
        {
            switch (entity.type)
            {
                case EntityType.Projectile:
                    _projectileOccupancy.Remove(entity.position);
                    break;

                default:
                    _unitOccupancy.Remove(entity.position);
                    break;
            }
        }

        private void RemoveEntity(int entityId)
        {
            _entitiesById.Remove(entityId);
        }

        private void SetOccupancy(EntityState entity)
        {
            switch (entity.type)
            {
                case EntityType.Projectile:
                    SetLayerOccupancy(_projectileOccupancy, entity.position, entity.entityId);
                    break;

                default:
                    SetLayerOccupancy(_unitOccupancy, entity.position, entity.entityId);
                    break;
            }
        }

        private bool TryGetEntity(int entityId, out EntityState entity)
        {
            return _entitiesById.TryGetValue(entityId, out entity);
        }

        private void UpdateEntity(EntityState entity)
        {
            _entitiesById[entity.entityId] = entity;
        }

        private static void SetLayerOccupancy(
            Dictionary<Vector2Int, int> occupancyByCell,
            Vector2Int position,
            int entityId)
        {
            if (occupancyByCell.TryGetValue(position, out var occupantId) && occupantId != entityId)
            {
                throw new InvalidOperationException("Conflicting occupancy detected while updating world state.");
            }

            occupancyByCell[position] = entityId;
        }

        bool IWorldStateMutationPort.TryGetEntity(int entityId, out EntityState entity)
        {
            return TryGetEntity(entityId, out entity);
        }

        void IWorldStateMutationPort.AddNewEntity(EntityState entity)
        {
            AddNewEntity(entity);
        }

        void IWorldStateMutationPort.UpdateEntity(EntityState entity)
        {
            UpdateEntity(entity);
        }

        void IWorldStateMutationPort.ClearOccupancy(EntityState entity)
        {
            ClearOccupancy(entity);
        }

        void IWorldStateMutationPort.SetOccupancy(EntityState entity)
        {
            SetOccupancy(entity);
        }

        void IWorldStateMutationPort.RemoveEntityRecord(int entityId)
        {
            RemoveEntity(entityId);
        }
    }
}
