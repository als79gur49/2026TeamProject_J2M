using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class WorldState : IWorldStateMutationPort
    {
        private readonly Dictionary<int, EntityState> _entitiesById = new();
        private readonly Dictionary<SurfaceCell, int> _projectileOccupancy = new();
        private readonly Dictionary<SurfaceCell, int> _solidOccupancy = new();
        private readonly BoardBounds _boardBounds;
        private readonly Dictionary<int, EnemyActionRuntimeState> _enemyActionStatesByEntityId = new();
        private readonly Dictionary<int, EntityExecutionLockState> _executionLockStatesByEntityId = new();
        private readonly Dictionary<int, EnemyJumpRuntimeState> _enemyJumpStatesByEntityId = new();
        private readonly Dictionary<int, PlayerControlState> _playerControlStatesByEntityId = new();
        private readonly Dictionary<SurfaceCell, SortedSet<int>> _stackedUnitsByCell = new();
        private readonly TerrainData _terrainData;
        private CubeTopologyState _topology;

        internal WorldState()
            : this(Array.Empty<EntityState>(), BoardBounds.Unbounded, TerrainData.Empty, new CubeTopologyState(FaceId.Floor))
        {
        }

        internal WorldState(IEnumerable<EntityState> initialEntities)
            : this(initialEntities, BoardBounds.Unbounded, TerrainData.Empty, new CubeTopologyState(FaceId.Floor))
        {
        }

        internal WorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData)
            : this(initialEntities, boardBounds, terrainData, new CubeTopologyState(FaceId.Floor))
        {
        }

        internal WorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
            CubeTopologyState topology)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            _boardBounds = boardBounds;
            _terrainData = terrainData ?? throw new ArgumentNullException(nameof(terrainData));
            _topology = topology;
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
                CloneStackedUnitsByCell(),
                new Dictionary<SurfaceCell, int>(_solidOccupancy),
                new Dictionary<SurfaceCell, int>(_projectileOccupancy),
                new Dictionary<int, EnemyActionRuntimeState>(_enemyActionStatesByEntityId),
                new Dictionary<int, EntityExecutionLockState>(_executionLockStatesByEntityId),
                new Dictionary<int, EnemyJumpRuntimeState>(_enemyJumpStatesByEntityId),
                new Dictionary<int, PlayerControlState>(_playerControlStatesByEntityId),
                _topology,
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

            entity.enemyLocomotionCooldownTicks = Mathf.Max(0, entity.enemyLocomotionCooldownTicks);
            EnsurePlacementIsLegal(entity, entity.position, ignoredEntityId: 0);
            _entitiesById.Add(entity.entityId, entity);
            SetOccupancyForEntity(entity);
        }

        private void MoveEntityTo(int entityId, SurfaceCell destination)
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
            _enemyActionStatesByEntityId.Remove(entityId);
            _executionLockStatesByEntityId.Remove(entityId);
            _enemyJumpStatesByEntityId.Remove(entityId);
            _playerControlStatesByEntityId.Remove(entityId);
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

        private void ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.aiMode = aiMode;
            entity.aiStateTimer = aiStateTimer;
            UpdateStoredEntity(entity);
        }

        private void SetEnemyLocomotionCooldown(int entityId, int cooldownTicks)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.enemyLocomotionCooldownTicks = Mathf.Max(0, cooldownTicks);
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

        private void SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            if (entity.boardPresence == boardPresence)
            {
                return;
            }

            ClearOccupancyForEntity(entity);

            entity.boardPresence = boardPresence;
            if (boardPresence == EntityBoardPresence.Occupying)
            {
                EnsurePlacementIsLegal(entity, entity.position, entityId);
            }

            UpdateStoredEntity(entity);
            SetOccupancyForEntity(entity);
        }

        private void SetTopology(CubeTopologyState topology)
        {
            _topology = topology;
        }

        private void SetPlayerControlState(int entityId, PlayerControlState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _playerControlStatesByEntityId[entityId] = state;
        }

        private void SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _enemyActionStatesByEntityId[entityId] = state;
        }

        private void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _enemyJumpStatesByEntityId[entityId] = state;
        }

        private void SetEntityExecutionLockState(int entityId, EntityExecutionLockState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _executionLockStatesByEntityId[entityId] = state;
        }

        private void ClearOccupancyForEntity(EntityState entity)
        {
            if (!ShouldStoreEntityInOccupancy(entity))
            {
                return;
            }

            switch (entity.type)
            {
                case EntityType.Projectile:
                    ClearLayerOccupancy(_projectileOccupancy, entity.position, entity.entityId);
                    break;

                case EntityType.Unit:
                    ClearStackedUnitOccupancy(entity.position, entity.entityId);
                    break;

                default:
                    ClearLayerOccupancy(_solidOccupancy, entity.position, entity.entityId);
                    break;
            }
        }

        private void SetOccupancyForEntity(EntityState entity)
        {
            if (!ShouldStoreEntityInOccupancy(entity))
            {
                return;
            }

            switch (entity.type)
            {
                case EntityType.Projectile:
                    SetLayerOccupancy(_projectileOccupancy, entity.position, entity.entityId);
                    break;

                case EntityType.Unit:
                    SetStackedUnitOccupancy(entity.position, entity.entityId);
                    break;

                default:
                    SetLayerOccupancy(_solidOccupancy, entity.position, entity.entityId);
                    break;
            }
        }

        private bool TryGetEntity(int entityId, out EntityState entity)
        {
            return _entitiesById.TryGetValue(entityId, out entity);
        }

        private void EnsurePlacementIsLegal(EntityState entity, SurfaceCell cell, int ignoredEntityId)
        {
            if (!WorldPlacementPolicy.TryGetAuthoritativePlacementBlocker(
                    _entitiesById,
                    _stackedUnitsByCell,
                    _solidOccupancy,
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
            SurfaceCell cell,
            SlideStopper blocker)
        {
            return new InvalidOperationException(
                $"Entity {entity.entityId} cannot occupy {cell}. {FormatPlacementBlocker(blocker)}");
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

        private Dictionary<SurfaceCell, SortedSet<int>> CloneStackedUnitsByCell()
        {
            var clone = new Dictionary<SurfaceCell, SortedSet<int>>(_stackedUnitsByCell.Count);

            foreach (var pair in _stackedUnitsByCell)
            {
                clone.Add(pair.Key, new SortedSet<int>(pair.Value));
            }

            return clone;
        }

        private void ClearStackedUnitOccupancy(SurfaceCell position, int entityId)
        {
            if (!_stackedUnitsByCell.TryGetValue(position, out var entityIds))
            {
                return;
            }

            if (!entityIds.Remove(entityId))
            {
                throw new InvalidOperationException(
                    $"Conflicting stacked-unit occupancy detected while updating world state at {position}.");
            }

            if (entityIds.Count == 0)
            {
                _stackedUnitsByCell.Remove(position);
            }
        }

        private void SetStackedUnitOccupancy(SurfaceCell position, int entityId)
        {
            if (!_stackedUnitsByCell.TryGetValue(position, out var entityIds))
            {
                entityIds = new SortedSet<int>();
                _stackedUnitsByCell.Add(position, entityIds);
            }

            entityIds.Add(entityId);
        }

        private static bool ShouldStoreEntityInOccupancy(EntityState entity)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying;
        }

        private static string FormatPlacementBlocker(SlideStopper blocker)
        {
            switch (blocker.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return $"Board bounds reject the cell at {blocker.Cell}.";

                case SlideStopperKind.Terrain:
                    return $"Terrain blocks the cell at {blocker.Cell}.";

                case SlideStopperKind.Entity:
                    return $"Entity {blocker.EntityId} ({blocker.EntityType}) already occupies {blocker.Cell}.";

                default:
                    return "The placement is blocked by an unknown world-state invariant.";
            }
        }

        private static void SetLayerOccupancy(
            Dictionary<SurfaceCell, int> occupancyByCell,
            SurfaceCell position,
            int entityId)
        {
            if (occupancyByCell.TryGetValue(position, out var occupantId) && occupantId != entityId)
            {
                throw new InvalidOperationException(
                    $"Conflicting occupancy detected while updating world state at {position}.");
            }

            occupancyByCell[position] = entityId;
        }

        private static void ClearLayerOccupancy(
            Dictionary<SurfaceCell, int> occupancyByCell,
            SurfaceCell position,
            int entityId)
        {
            if (!occupancyByCell.TryGetValue(position, out var occupantId))
            {
                return;
            }

            if (occupantId != entityId)
            {
                throw new InvalidOperationException(
                    $"Conflicting occupancy detected while updating world state at {position}.");
            }

            occupancyByCell.Remove(position);
        }

        bool IWorldStateMutationPort.TryGetEntity(int entityId, out EntityState entity)
        {
            return TryGetEntity(entityId, out entity);
        }

        void IWorldStateMutationPort.MoveEntityTo(int entityId, SurfaceCell destination)
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

        void IWorldStateMutationPort.ApplyEnemyAiState(int entityId, EnemyAiMode aiMode, int aiStateTimer)
        {
            ApplyEnemyAiState(entityId, aiMode, aiStateTimer);
        }

        void IWorldStateMutationPort.SetEnemyLocomotionCooldown(int entityId, int cooldownTicks)
        {
            SetEnemyLocomotionCooldown(entityId, cooldownTicks);
        }

        void IWorldStateMutationPort.SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            SetEnemyActionState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            SetEnemyJumpState(entityId, state);
        }

        void IWorldStateMutationPort.SetEntityExecutionLockState(int entityId, EntityExecutionLockState state)
        {
            SetEntityExecutionLockState(entityId, state);
        }

        void IWorldStateMutationPort.MarkDestroy(int entityId)
        {
            MarkDestroy(entityId);
        }

        void IWorldStateMutationPort.SetFacing(int entityId, Direction facing)
        {
            SetFacing(entityId, facing);
        }

        void IWorldStateMutationPort.SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            SetBoardPresence(entityId, boardPresence);
        }

        void IWorldStateMutationPort.SetPlayerControlState(int entityId, PlayerControlState state)
        {
            SetPlayerControlState(entityId, state);
        }

        void IWorldStateMutationPort.SetTopology(CubeTopologyState topology)
        {
            SetTopology(topology);
        }
    }
}
