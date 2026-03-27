using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class WorldState : IWorldStateMutationPort
    {
        private readonly Dictionary<int, EntityState> _entitiesById = new();
        private readonly Dictionary<Vector2Int, int> _projectileOccupancy = new();
        private readonly BoardBounds _boardBounds;
        private readonly TerrainData _terrainData;
        private readonly Dictionary<Vector2Int, int> _unitOccupancy = new();

        internal WorldState()
            : this(Array.Empty<EntityState>(), BoardBounds.Unbounded, TerrainData.Empty)
        {
        }

        internal WorldState(IEnumerable<EntityState> initialEntities)
            : this(initialEntities, BoardBounds.Unbounded, TerrainData.Empty)
        {
        }

        internal WorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            _boardBounds = boardBounds;
            _terrainData = terrainData ?? throw new ArgumentNullException(nameof(terrainData));
            ValidateTerrainBounds();

            foreach (var entity in initialEntities)
            {
                SpawnEntity(entity);
            }
        }

        internal WorldSnapshot CreateSnapshot()
        {
            return new WorldSnapshot(
                new Dictionary<int, EntityState>(_entitiesById),
                new Dictionary<Vector2Int, int>(_unitOccupancy),
                new Dictionary<Vector2Int, int>(_projectileOccupancy),
                _boardBounds,
                _terrainData);
        }

        internal IWorldWriteContext CreateWriteContext()
        {
            return new WorldStateWriteContext((IWorldStateMutationPort)this);
        }

        private void SpawnEntity(EntityState entity)
        {
            if (_entitiesById.ContainsKey(entity.entityId))
            {
                throw new InvalidOperationException("Duplicate entity id detected while adding entity.");
            }

            EnsurePlacementIsLegal(entity, entity.position, ignoredEntityId: 0);
            _entitiesById.Add(entity.entityId, entity);
            SetOccupancyForEntity(entity);
        }

        private void MoveEntityTo(int entityId, Vector2Int destination)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            var updatedEntity = entity;
            updatedEntity.position = destination;

            EnsurePlacementIsLegal(updatedEntity, destination, entityId);

            ClearOccupancyForEntity(entity);
            UpdateStoredEntity(updatedEntity);
            SetOccupancyForEntity(updatedEntity);
        }

        private void RemoveEntity(int entityId)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            ClearOccupancyForEntity(entity);
            _entitiesById.Remove(entityId);
        }

        private void ApplyDamage(int entityId, int amount)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.hp -= amount;
            UpdateStoredEntity(entity);
        }

        private void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.state = state;
            entity.stateTimer = stateTimer;
            UpdateStoredEntity(entity);
        }

        private void MarkDestroy(int entityId)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.markedForDeath = true;
            UpdateStoredEntity(entity);
        }

        private void SetFacing(int entityId, Direction facing)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.facing = facing;
            UpdateStoredEntity(entity);
        }

        private void ClearOccupancyForEntity(EntityState entity)
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

        private void SetOccupancyForEntity(EntityState entity)
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

        private void EnsurePlacementIsLegal(EntityState entity, Vector2Int cell, int ignoredEntityId)
        {
            if (!WorldQueryService.TryGetPlacementBlocker(
                    _entitiesById,
                    _unitOccupancy,
                    _projectileOccupancy,
                    _boardBounds,
                    _terrainData,
                    entity.type,
                    cell,
                    ignoredEntityId,
                    out var blocker))
            {
                return;
            }

            throw CreatePlacementViolationException(entity, cell, blocker);
        }

        private static InvalidOperationException CreatePlacementViolationException(
            EntityState entity,
            Vector2Int cell,
            SlideStopper blocker)
        {
            return new InvalidOperationException(
                $"Entity {entity.entityId} cannot occupy ({cell.x},{cell.y}). {FormatPlacementBlocker(blocker)}");
        }

        private void ValidateTerrainBounds()
        {
            if (!_boardBounds.IsBounded)
            {
                return;
            }

            var blockingCells = _terrainData.OrderedUnitBlockingCells;
            for (var i = 0; i < blockingCells.Count; i++)
            {
                if (_boardBounds.Contains(blockingCells[i]))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Terrain cell ({blockingCells[i].x},{blockingCells[i].y}) is outside the configured board bounds.");
            }
        }

        private void UpdateStoredEntity(EntityState entity)
        {
            _entitiesById[entity.entityId] = entity;
        }

        private static string FormatPlacementBlocker(SlideStopper blocker)
        {
            switch (blocker.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return $"Board bounds reject the cell at ({blocker.Cell.x},{blocker.Cell.y}).";

                case SlideStopperKind.Terrain:
                    return $"Terrain blocks the cell at ({blocker.Cell.x},{blocker.Cell.y}).";

                case SlideStopperKind.Entity:
                    return $"Entity {blocker.EntityId} ({blocker.EntityType}) already occupies ({blocker.Cell.x},{blocker.Cell.y}).";

                default:
                    return "The placement is blocked by an unknown world-state invariant.";
            }
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

        void IWorldStateMutationPort.MoveEntityTo(int entityId, Vector2Int destination)
        {
            MoveEntityTo(entityId, destination);
        }

        void IWorldStateMutationPort.SpawnEntity(EntityState entity)
        {
            SpawnEntity(entity);
        }

        void IWorldStateMutationPort.RemoveEntity(int entityId)
        {
            RemoveEntity(entityId);
        }

        void IWorldStateMutationPort.ApplyDamage(int entityId, int amount)
        {
            ApplyDamage(entityId, amount);
        }

        void IWorldStateMutationPort.ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            ApplyStateChange(entityId, state, stateTimer);
        }

        void IWorldStateMutationPort.MarkDestroy(int entityId)
        {
            MarkDestroy(entityId);
        }

        void IWorldStateMutationPort.SetFacing(int entityId, Direction facing)
        {
            SetFacing(entityId, facing);
        }
    }
}
