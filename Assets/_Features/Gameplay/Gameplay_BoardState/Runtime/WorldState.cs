using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class WorldState
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
            return new WriteContext(this);
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

        private sealed class WriteContext : IWorldWriteContext
        {
            private readonly WorldState _worldState;

            public WriteContext(WorldState worldState)
            {
                _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
            }

            public void MoveEntity(int entityId, Vector2Int destination)
            {
                if (!_worldState.TryGetEntity(entityId, out var entity))
                {
                    return;
                }

                _worldState.ClearOccupancy(entity);
                entity.position = destination;
                _worldState.UpdateEntity(entity);
                _worldState.SetOccupancy(entity);
            }

            public void ApplyDamage(int entityId, int amount)
            {
                if (!_worldState.TryGetEntity(entityId, out var entity))
                {
                    return;
                }

                entity.hp -= amount;
                _worldState.UpdateEntity(entity);
            }

            public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
            {
                if (!_worldState.TryGetEntity(entityId, out var entity))
                {
                    return;
                }

                entity.state = state;
                entity.stateTimer = stateTimer;
                _worldState.UpdateEntity(entity);
            }

            public void MarkDestroy(int entityId)
            {
                if (!_worldState.TryGetEntity(entityId, out var entity))
                {
                    return;
                }

                entity.markedForDeath = true;
                _worldState.UpdateEntity(entity);
            }

            public void SpawnEntity(EntityState entity)
            {
                _worldState.AddNewEntity(entity);
            }

            public void RemoveEntity(int entityId)
            {
                if (!_worldState.TryGetEntity(entityId, out var entity))
                {
                    return;
                }

                _worldState.ClearOccupancy(entity);
                _worldState.RemoveEntity(entityId);
            }

            public void SetFacing(int entityId, Direction facing)
            {
                if (!_worldState.TryGetEntity(entityId, out var entity))
                {
                    return;
                }

                entity.facing = facing;
                _worldState.UpdateEntity(entity);
            }
        }
    }
}
