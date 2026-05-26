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
        private readonly Dictionary<int, TileFeatureState> _tileFeaturesById = new();
        private readonly Dictionary<SurfaceCell, SortedSet<int>> _tileFeatureIdsByCell = new();
        private readonly BoardBounds _boardBounds;
        private readonly Dictionary<int, EnemyActionRuntimeState> _enemyActionStatesByEntityId = new();
        private readonly Dictionary<int, PendingCellImpact> _pendingCellImpactsById = new();
        private readonly Dictionary<int, ScheduledFlipContact> _scheduledFlipContactsByActionId = new();
        private readonly Dictionary<int, EnemyPatrolRuntimeState> _enemyPatrolStatesByEntityId = new();
        private readonly Dictionary<int, EnemyChargeRuntimeState> _enemyChargeStatesByEntityId = new();
        private readonly Dictionary<int, EntityExecutionLockState> _executionLockStatesByEntityId = new();
        private readonly Dictionary<int, EnemyJumpRuntimeState> _enemyJumpStatesByEntityId = new();
        private readonly Dictionary<int, EnemyGlideRuntimeState> _enemyGlideStatesByEntityId = new();
        private readonly Dictionary<int, EnemyUtilityRuntimeState> _enemyUtilityStatesByEntityId = new();
        private readonly Dictionary<int, EnemyFrontFaceSupportRuntimeState> _enemyFrontFaceSupportStatesByEntityId = new();
        private readonly Dictionary<int, BoxInteractionLockState> _boxInteractionLockStatesByEntityId = new();
        private readonly Dictionary<int, EnemyGravityFieldAuraFieldState> _enemyGravityFieldAuraFieldsById = new();
        private readonly Dictionary<int, PhasedRuntimeState> _phasedStatesByEntityId = new();
        private readonly Dictionary<int, PlayerDamageState> _playerDamageStatesByEntityId = new();
        private readonly Dictionary<int, PlayerControlState> _playerControlStatesByEntityId = new();
        private readonly Dictionary<int, SummonedEntityState> _summonedEntitiesByEntityId = new();
        private readonly Dictionary<int, EnemyDefinitionBindingState> _enemyDefinitionBindingsByEntityId = new();
        private readonly Dictionary<int, UnitKinematicRuntimeState> _unitKinematicStatesByEntityId = new();
        private readonly Dictionary<int, UnitContinuousLocomotionState> _unitContinuousLocomotionStatesByEntityId = new();
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
            : this(initialEntities, boardBounds, terrainData, topology, initialTileFeatures: null, initialEnemyGlideStatesByEntityId: null)
        {
        }

        internal WorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
            CubeTopologyState topology,
            IEnumerable<TileFeatureState> initialTileFeatures)
            : this(initialEntities, boardBounds, terrainData, topology, initialTileFeatures, initialEnemyGlideStatesByEntityId: null)
        {
        }

        internal WorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
            CubeTopologyState topology,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> initialEnemyGlideStatesByEntityId)
            : this(initialEntities, boardBounds, terrainData, topology, initialTileFeatures: null, initialEnemyGlideStatesByEntityId: initialEnemyGlideStatesByEntityId)
        {
        }

        internal WorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            TerrainData terrainData,
            CubeTopologyState topology,
            IEnumerable<TileFeatureState> initialTileFeatures,
            IReadOnlyDictionary<int, EnemyGlideRuntimeState> initialEnemyGlideStatesByEntityId)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            _boardBounds = boardBounds;
            _terrainData = terrainData ?? throw new ArgumentNullException(nameof(terrainData));
            _topology = topology;
            ValidateTerrainBounds();
            AddInitialTileFeatures(initialTileFeatures);

            if (initialEnemyGlideStatesByEntityId != null)
            {
                foreach (var pair in initialEnemyGlideStatesByEntityId)
                {
                    if (pair.Value.HasAuthoritativeRecord)
                    {
                        _enemyGlideStatesByEntityId[pair.Key] = pair.Value;
                    }
                }
            }

            foreach (var entity in initialEntities)
            {
                SpawnEntity(entity);
            }
        }

        internal WorldSnapshot CreateSnapshot()
        {
            SnapshotMaterializationDiagnostics.RecordWorldStateCreateSnapshot();
            return new WorldSnapshot(
                new Dictionary<int, EntityState>(_entitiesById),
                CloneStackedUnitsByCell(),
                new Dictionary<SurfaceCell, int>(_solidOccupancy),
                new Dictionary<SurfaceCell, int>(_projectileOccupancy),
                new Dictionary<int, TileFeatureState>(_tileFeaturesById),
                CloneTileFeatureIdsByCell(),
                new Dictionary<int, EnemyActionRuntimeState>(_enemyActionStatesByEntityId),
                new Dictionary<int, PendingCellImpact>(_pendingCellImpactsById),
                new Dictionary<int, ScheduledFlipContact>(_scheduledFlipContactsByActionId),
                new Dictionary<int, EnemyPatrolRuntimeState>(_enemyPatrolStatesByEntityId),
                new Dictionary<int, EnemyChargeRuntimeState>(_enemyChargeStatesByEntityId),
                new Dictionary<int, EntityExecutionLockState>(_executionLockStatesByEntityId),
                new Dictionary<int, EnemyJumpRuntimeState>(_enemyJumpStatesByEntityId),
                new Dictionary<int, EnemyGlideRuntimeState>(_enemyGlideStatesByEntityId),
                new Dictionary<int, EnemyUtilityRuntimeState>(_enemyUtilityStatesByEntityId),
                new Dictionary<int, EnemyFrontFaceSupportRuntimeState>(_enemyFrontFaceSupportStatesByEntityId),
                new Dictionary<int, BoxInteractionLockState>(_boxInteractionLockStatesByEntityId),
                new Dictionary<int, EnemyGravityFieldAuraFieldState>(_enemyGravityFieldAuraFieldsById),
                new Dictionary<int, PhasedRuntimeState>(_phasedStatesByEntityId),
                new Dictionary<int, PlayerDamageState>(_playerDamageStatesByEntityId),
                new Dictionary<int, PlayerControlState>(_playerControlStatesByEntityId),
                new Dictionary<int, SummonedEntityState>(_summonedEntitiesByEntityId),
                new Dictionary<int, EnemyDefinitionBindingState>(_enemyDefinitionBindingsByEntityId),
                new Dictionary<int, UnitKinematicRuntimeState>(_unitKinematicStatesByEntityId),
                new Dictionary<int, UnitContinuousLocomotionState>(_unitContinuousLocomotionStatesByEntityId),
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
            entity.enemyAttackCooldownTicks = Mathf.Max(0, entity.enemyAttackCooldownTicks);
            entity.enemyAttackCooldownTotalTicks = Mathf.Max(
                entity.enemyAttackCooldownTicks,
                entity.enemyAttackCooldownTotalTicks);
            if (ShouldStoreEntityInOccupancy(entity))
            {
                EnsurePlacementIsRepresentable(entity, entity.position, entity.entityId);
            }

            _entitiesById.Add(entity.entityId, entity);
            if (EntityRolePolicy.IsPlayerUnit(entity))
            {
                _playerDamageStatesByEntityId[entity.entityId] = default;
            }
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

            EnsurePlacementIsRepresentable(updatedEntity, destination, entityId);

            ClearOccupancyForEntity(entity);
            UpdateStoredEntity(updatedEntity);
            _unitKinematicStatesByEntityId.Remove(entityId);
            _unitContinuousLocomotionStatesByEntityId.Remove(entityId);
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
            _enemyPatrolStatesByEntityId.Remove(entityId);
            _enemyChargeStatesByEntityId.Remove(entityId);
            _executionLockStatesByEntityId.Remove(entityId);
            _enemyJumpStatesByEntityId.Remove(entityId);
            _enemyGlideStatesByEntityId.Remove(entityId);
            _enemyUtilityStatesByEntityId.Remove(entityId);
            _enemyFrontFaceSupportStatesByEntityId.Remove(entityId);
            _boxInteractionLockStatesByEntityId.Remove(entityId);
            _phasedStatesByEntityId.Remove(entityId);
            _playerDamageStatesByEntityId.Remove(entityId);
            _playerControlStatesByEntityId.Remove(entityId);
            _summonedEntitiesByEntityId.Remove(entityId);
            _enemyDefinitionBindingsByEntityId.Remove(entityId);
            _unitKinematicStatesByEntityId.Remove(entityId);
            _unitContinuousLocomotionStatesByEntityId.Remove(entityId);
        }

        private void ApplyDamage(int entityId, int amount)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.hp -= amount;
            UpdateStoredEntity(entity);
            if (entity.hp <= 0)
            {
                ClearChargeState(entityId);
                _phasedStatesByEntityId.Remove(entityId);
                _enemyGlideStatesByEntityId.Remove(entityId);
                _enemyFrontFaceSupportStatesByEntityId.Remove(entityId);
            }
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

        private void SetEnemyAttackCooldown(int entityId, int cooldownTicks, int totalTicks)
        {
            if (!TryGetEntity(entityId, out var entity))
            {
                return;
            }

            entity.enemyAttackCooldownTicks = Mathf.Max(0, cooldownTicks);
            entity.enemyAttackCooldownTotalTicks = entity.enemyAttackCooldownTicks > 0
                ? Mathf.Max(entity.enemyAttackCooldownTicks, totalTicks)
                : 0;
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
            ClearChargeState(entityId);
            _phasedStatesByEntityId.Remove(entityId);
            _enemyGlideStatesByEntityId.Remove(entityId);
            _enemyFrontFaceSupportStatesByEntityId.Remove(entityId);
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

        private void SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            if (!TryGetEntity(entityId, out var entity) ||
                entity.type != EntityType.Box)
            {
                return;
            }

            entity.kineticInstigatorEntityId = instigatorEntityId;
            entity.kineticInstigatorTeamId = instigatorTeamId;
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
            if (boardPresence != EntityBoardPresence.Occupying)
            {
                ClearChargeState(entityId);
                _phasedStatesByEntityId.Remove(entityId);
                _enemyGlideStatesByEntityId.Remove(entityId);
                ClearNonAirborneEnemyJumpStateForNonOccupyingEntity(entityId);
            }

            if (boardPresence == EntityBoardPresence.Occupying)
            {
                EnsurePlacementIsRepresentable(entity, entity.position, entityId);
            }

            UpdateStoredEntity(entity);
            SetOccupancyForEntity(entity);
        }

        private void ClearNonAirborneEnemyJumpStateForNonOccupyingEntity(int entityId)
        {
            if (!_enemyJumpStatesByEntityId.TryGetValue(entityId, out var jumpState) ||
                jumpState.phase == EnemyJumpPhase.Airborne)
            {
                return;
            }

            _enemyJumpStatesByEntityId[entityId] = EnemyJumpQueries.Clear(jumpState);
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

        private void SetPlayerDamageState(int entityId, PlayerDamageState state)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity) ||
                !EntityRolePolicy.IsPlayerUnit(entity))
            {
                return;
            }

            _playerDamageStatesByEntityId[entityId] = state;
        }

        private void SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _enemyActionStatesByEntityId[entityId] = state;
        }

        private void AddPendingCellImpact(PendingCellImpact impact)
        {
            _pendingCellImpactsById[impact.ImpactId] = impact;
        }

        private void RemovePendingCellImpact(int impactId)
        {
            _pendingCellImpactsById.Remove(impactId);
        }

        private void AddScheduledFlipContact(ScheduledFlipContact contact)
        {
            _scheduledFlipContactsByActionId[contact.ActionId] = contact;
        }

        private void RemoveScheduledFlipContact(int actionId)
        {
            _scheduledFlipContactsByActionId.Remove(actionId);
        }

        private void RemoveScheduledFlipContactsForEntity(int entityId)
        {
            if (entityId <= 0 || _scheduledFlipContactsByActionId.Count == 0)
            {
                return;
            }

            var actionIdsToRemove = new List<int>();
            foreach (var pair in _scheduledFlipContactsByActionId)
            {
                var contact = pair.Value;
                if (contact.ActorEntityId == entityId ||
                    contact.SourceBoxEntityId == entityId ||
                    contact.KineticInstigatorEntityId == entityId)
                {
                    actionIdsToRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < actionIdsToRemove.Count; i++)
            {
                _scheduledFlipContactsByActionId.Remove(actionIdsToRemove[i]);
            }
        }

        private void SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _enemyPatrolStatesByEntityId[entityId] = state;
        }

        private void SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _enemyChargeStatesByEntityId[entityId] = state;
        }

        private void SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            if (state.phase != EnemyJumpPhase.None &&
                _phasedStatesByEntityId.TryGetValue(entityId, out var phasedState) &&
                phasedState.IsActive)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot hold active jump and phased runtime states simultaneously.");
            }

            _enemyJumpStatesByEntityId[entityId] = state;
        }

        private void SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity))
            {
                return;
            }

            if (!state.HasAuthoritativeRecord)
            {
                _enemyGlideStatesByEntityId.Remove(entityId);
                return;
            }

            if (entity.type != EntityType.Unit)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot hold enemy glide runtime state because only unit entities are supported.");
            }

            if (!state.IsActive &&
                _solidOccupancy.TryGetValue(entity.position, out var solidEntityId) &&
                solidEntityId != entityId)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot leave active glide while occupying solid cell {entity.position}.");
            }

            _enemyGlideStatesByEntityId[entityId] = state;
        }

        internal void SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId) ||
                state == null)
            {
                return;
            }

            _enemyUtilityStatesByEntityId[entityId] = state;
        }

        internal void SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state)
        {
            if (!_entitiesById.ContainsKey(entityId) ||
                state == null)
            {
                return;
            }

            _enemyFrontFaceSupportStatesByEntityId[entityId] = state;
        }

        internal void SetSummonedEntityState(int entityId, SummonedEntityState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            _summonedEntitiesByEntityId[entityId] = state;
        }

        internal void SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state)
        {
            if (!_entitiesById.ContainsKey(entityId))
            {
                return;
            }

            state.Validate(nameof(state));
            _enemyDefinitionBindingsByEntityId[entityId] = state;
        }

        internal void SetBoxInteractionLockState(int entityId, BoxInteractionLockState state)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity) ||
                entity.type != EntityType.Box)
            {
                return;
            }

            _boxInteractionLockStatesByEntityId[entityId] = state;
        }

        internal void SetEnemyGravityFieldAuraFieldState(int fieldId, EnemyGravityFieldAuraFieldState state)
        {
            if (state.Radius <= 0 ||
                state.ExpiresTickExclusive <= state.StartedTick)
            {
                return;
            }

            _enemyGravityFieldAuraFieldsById[fieldId] = state;
        }

        internal void SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity) ||
                entity.type != EntityType.Box)
            {
                return;
            }

            entity.gravityFieldPhase = phase;
            entity.gravityFieldTimerTicks = timerTicks;
            _entitiesById[entityId] = entity;
        }

        internal void SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity))
            {
                return;
            }

            if (entity.type != EntityType.Unit)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot hold unit kinematic runtime state because only unit entities are supported.");
            }

            var normalizedState = state.NormalizedForStorage();
            if (normalizedState.IsSettledZero)
            {
                _unitKinematicStatesByEntityId.Remove(entityId);
                return;
            }

            if (_unitContinuousLocomotionStatesByEntityId.ContainsKey(entityId))
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot hold active unit kinematic and continuous locomotion states at the same time.");
            }

            _unitKinematicStatesByEntityId[entityId] = normalizedState;
        }

        internal void SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity))
            {
                return;
            }

            if (entity.type != EntityType.Unit)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot hold unit continuous locomotion state because only unit entities are supported.");
            }

            var normalizedState = state.NormalizedForStorage();
            if (normalizedState.IsOmittableIdleZero)
            {
                _unitContinuousLocomotionStatesByEntityId.Remove(entityId);
                return;
            }

            if (_unitKinematicStatesByEntityId.ContainsKey(entityId))
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot hold active unit continuous locomotion and kinematic states at the same time.");
            }

            _unitContinuousLocomotionStatesByEntityId[entityId] = normalizedState;
        }

        internal void RemoveBoxInteractionLockState(int entityId)
        {
            _boxInteractionLockStatesByEntityId.Remove(entityId);
        }

        internal void RemoveEnemyGravityFieldAuraFieldState(int fieldId)
        {
            _enemyGravityFieldAuraFieldsById.Remove(fieldId);
        }

        private void ClearChargeState(int entityId)
        {
            if (_enemyChargeStatesByEntityId.TryGetValue(entityId, out var currentState))
            {
                _enemyChargeStatesByEntityId[entityId] = EnemyChargeQueries.Clear(currentState);
            }
        }

        private void SetPhasedState(int entityId, PhasedRuntimeState state)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity))
            {
                return;
            }

            if (!state.IsActive)
            {
                _phasedStatesByEntityId.Remove(entityId);
                return;
            }

            if (entity.type != EntityType.Unit)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot enter phased runtime state because only unit entities are supported.");
            }

            if (entity.boardPresence != EntityBoardPresence.Occupying)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot enter phased runtime state while board presence is {entity.boardPresence}.");
            }

            if (entity.hp <= 0 || entity.markedForDeath)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot enter phased runtime state after death or destroy marking.");
            }

            if (_enemyJumpStatesByEntityId.TryGetValue(entityId, out var jumpState) &&
                jumpState.phase != EnemyJumpPhase.None)
            {
                throw new InvalidOperationException(
                    $"Entity {entityId} cannot enter phased runtime state while jump phase is {jumpState.phase}.");
            }

            _phasedStatesByEntityId[entityId] = state;
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

        private void EnsurePlacementIsRepresentable(EntityState entity, SurfaceCell cell, int ignoredEntityId)
        {
            if (!WorldPlacementPolicy.TryGetRepresentablePlacementBlocker(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyGlideStatesByEntityId,
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

            var terrainCells = _terrainData.OrderedTerrainCells;
            for (var i = 0; i < terrainCells.Count; i++)
            {
                if (_boardBounds.Contains(terrainCells[i].Cell.PlanarPosition))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Terrain cell {terrainCells[i].Cell} is outside the configured board bounds.");
            }
        }

        private void AddInitialTileFeatures(IEnumerable<TileFeatureState> initialTileFeatures)
        {
            if (initialTileFeatures == null)
            {
                return;
            }

            foreach (var tileFeature in initialTileFeatures)
            {
                AddTileFeature(tileFeature);
            }
        }

        private void AddTileFeature(TileFeatureState state)
        {
            ValidateTileFeatureState(state);

            if (_tileFeaturesById.ContainsKey(state.TileId))
            {
                throw new InvalidOperationException(
                    $"Duplicate TileFeature id {state.TileId} detected while adding tile feature.");
            }

            _tileFeaturesById.Add(state.TileId, state);
            AddTileFeatureCellIndex(state.TileId, state.Cell);
        }

        private void UpdateTileFeature(TileFeatureState state)
        {
            ValidateTileFeatureState(state);

            if (!_tileFeaturesById.TryGetValue(state.TileId, out var previous))
            {
                throw new InvalidOperationException(
                    $"Cannot update missing TileFeature id {state.TileId}.");
            }

            _tileFeaturesById[state.TileId] = state;
            if (!previous.Cell.Equals(state.Cell))
            {
                RemoveTileFeatureCellIndex(state.TileId, previous.Cell);
                AddTileFeatureCellIndex(state.TileId, state.Cell);
            }
        }

        private void RemoveTileFeature(int tileId)
        {
            if (tileId <= 0)
            {
                throw new InvalidOperationException(
                    $"TileFeature id {tileId} must be positive.");
            }

            if (!_tileFeaturesById.TryGetValue(tileId, out var previous))
            {
                throw new InvalidOperationException(
                    $"Cannot remove missing TileFeature id {tileId}.");
            }

            _tileFeaturesById.Remove(tileId);
            RemoveTileFeatureCellIndex(tileId, previous.Cell);
        }

        private void ValidateTileFeatureState(TileFeatureState state)
        {
            if (state.TileId <= 0)
            {
                throw new InvalidOperationException(
                    $"TileFeature id {state.TileId} must be positive.");
            }

            if (_boardBounds.IsBounded && !_boardBounds.Contains(state.Cell.PlanarPosition))
            {
                throw new InvalidOperationException(
                    $"TileFeature {state.TileId} at {state.Cell} is outside the configured board bounds.");
            }
        }

        private void AddTileFeatureCellIndex(int tileId, SurfaceCell cell)
        {
            if (!_tileFeatureIdsByCell.TryGetValue(cell, out var tileIds))
            {
                tileIds = new SortedSet<int>();
                _tileFeatureIdsByCell.Add(cell, tileIds);
            }

            tileIds.Add(tileId);
        }

        private void RemoveTileFeatureCellIndex(int tileId, SurfaceCell cell)
        {
            if (!_tileFeatureIdsByCell.TryGetValue(cell, out var tileIds))
            {
                return;
            }

            tileIds.Remove(tileId);
            if (tileIds.Count == 0)
            {
                _tileFeatureIdsByCell.Remove(cell);
            }
        }

        private void UpdateStoredEntity(EntityState entity)
        {
            _entitiesById[entity.entityId] = entity;
        }

        internal bool TryGetEnemyUtilityState(int entityId, out EnemyUtilityRuntimeState state)
        {
            return _enemyUtilityStatesByEntityId.TryGetValue(entityId, out state);
        }

        internal void RemoveEnemyUtilityState(int entityId)
        {
            _enemyUtilityStatesByEntityId.Remove(entityId);
        }

        internal void EnumerateEnemyUtilityStatesOrdered(List<EnemyUtilitySnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _enemyUtilityStatesByEntityId)
            {
                buffer.Add(new EnemyUtilitySnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal bool TryGetEnemyFrontFaceSupportState(int entityId, out EnemyFrontFaceSupportRuntimeState state)
        {
            return _enemyFrontFaceSupportStatesByEntityId.TryGetValue(entityId, out state);
        }

        internal void RemoveEnemyFrontFaceSupportState(int entityId)
        {
            _enemyFrontFaceSupportStatesByEntityId.Remove(entityId);
        }

        internal void EnumerateEnemyFrontFaceSupportStatesOrdered(List<EnemyFrontFaceSupportSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _enemyFrontFaceSupportStatesByEntityId)
            {
                buffer.Add(new EnemyFrontFaceSupportSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal bool TryGetBoxInteractionLockState(int entityId, out BoxInteractionLockState state)
        {
            return _boxInteractionLockStatesByEntityId.TryGetValue(entityId, out state);
        }

        internal bool TryGetEnemyGravityFieldAuraFieldState(
            int fieldId,
            out EnemyGravityFieldAuraFieldState state)
        {
            return _enemyGravityFieldAuraFieldsById.TryGetValue(fieldId, out state);
        }

        internal bool TryGetUnitKinematicState(int entityId, out UnitKinematicRuntimeState state)
        {
            return _unitKinematicStatesByEntityId.TryGetValue(entityId, out state);
        }

        internal bool TryGetUnitContinuousLocomotionState(int entityId, out UnitContinuousLocomotionState state)
        {
            return _unitContinuousLocomotionStatesByEntityId.TryGetValue(entityId, out state);
        }

        internal void EnumerateUnitKinematicStatesOrdered(List<UnitKinematicSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _unitKinematicStatesByEntityId)
            {
                buffer.Add(new UnitKinematicSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateUnitContinuousLocomotionStatesOrdered(List<UnitContinuousLocomotionSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _unitContinuousLocomotionStatesByEntityId)
            {
                buffer.Add(new UnitContinuousLocomotionSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateBoxInteractionLockStatesOrdered(List<BoxInteractionLockSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _boxInteractionLockStatesByEntityId)
            {
                buffer.Add(new BoxInteractionLockSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyGravityFieldAuraFieldStatesOrdered(
            List<EnemyGravityFieldAuraFieldSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _enemyGravityFieldAuraFieldsById)
            {
                buffer.Add(new EnemyGravityFieldAuraFieldSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.FieldId.CompareTo(right.FieldId));
        }

        internal bool TryGetSummonedEntityState(int entityId, out SummonedEntityState state)
        {
            return _summonedEntitiesByEntityId.TryGetValue(entityId, out state);
        }

        internal void RemoveSummonedEntityState(int entityId)
        {
            _summonedEntitiesByEntityId.Remove(entityId);
        }

        internal bool TryGetEnemyDefinitionBindingState(int entityId, out EnemyDefinitionBindingState state)
        {
            return _enemyDefinitionBindingsByEntityId.TryGetValue(entityId, out state);
        }

        internal void RemoveEnemyDefinitionBindingState(int entityId)
        {
            _enemyDefinitionBindingsByEntityId.Remove(entityId);
        }

        internal void EnumerateSummonedEntityStatesOrdered(List<SummonedEntitySnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _summonedEntitiesByEntityId)
            {
                buffer.Add(new SummonedEntitySnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyDefinitionBindingStatesOrdered(List<EnemyDefinitionBindingSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _enemyDefinitionBindingsByEntityId)
            {
                buffer.Add(new EnemyDefinitionBindingSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
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

        private Dictionary<SurfaceCell, SortedSet<int>> CloneTileFeatureIdsByCell()
        {
            var clone = new Dictionary<SurfaceCell, SortedSet<int>>(_tileFeatureIdsByCell.Count);

            foreach (var pair in _tileFeatureIdsByCell)
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

        void IWorldStateMutationPort.SetEnemyAttackCooldown(int entityId, int cooldownTicks, int totalTicks)
        {
            SetEnemyAttackCooldown(entityId, cooldownTicks, totalTicks);
        }

        void IWorldStateMutationPort.SetEnemyActionState(int entityId, EnemyActionRuntimeState state)
        {
            SetEnemyActionState(entityId, state);
        }

        void IWorldStateMutationPort.AddPendingCellImpact(PendingCellImpact impact)
        {
            AddPendingCellImpact(impact);
        }

        void IWorldStateMutationPort.RemovePendingCellImpact(int impactId)
        {
            RemovePendingCellImpact(impactId);
        }

        void IWorldStateMutationPort.AddScheduledFlipContact(ScheduledFlipContact contact)
        {
            AddScheduledFlipContact(contact);
        }

        void IWorldStateMutationPort.RemoveScheduledFlipContact(int actionId)
        {
            RemoveScheduledFlipContact(actionId);
        }

        void IWorldStateMutationPort.RemoveScheduledFlipContactsForEntity(int entityId)
        {
            RemoveScheduledFlipContactsForEntity(entityId);
        }

        void IWorldStateMutationPort.SetEnemyPatrolState(int entityId, EnemyPatrolRuntimeState state)
        {
            SetEnemyPatrolState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyChargeState(int entityId, EnemyChargeRuntimeState state)
        {
            SetEnemyChargeState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyJumpState(int entityId, EnemyJumpRuntimeState state)
        {
            SetEnemyJumpState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyGlideState(int entityId, EnemyGlideRuntimeState state)
        {
            SetEnemyGlideState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyUtilityState(int entityId, EnemyUtilityRuntimeState state)
        {
            SetEnemyUtilityState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyFrontFaceSupportState(int entityId, EnemyFrontFaceSupportRuntimeState state)
        {
            SetEnemyFrontFaceSupportState(entityId, state);
        }

        void IWorldStateMutationPort.SetSummonedEntityState(int entityId, SummonedEntityState state)
        {
            SetSummonedEntityState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyDefinitionBindingState(int entityId, EnemyDefinitionBindingState state)
        {
            SetEnemyDefinitionBindingState(entityId, state);
        }

        void IWorldStateMutationPort.SetBoxInteractionLockState(int entityId, BoxInteractionLockState state)
        {
            SetBoxInteractionLockState(entityId, state);
        }

        void IWorldStateMutationPort.SetEnemyGravityFieldAuraFieldState(int fieldId, EnemyGravityFieldAuraFieldState state)
        {
            SetEnemyGravityFieldAuraFieldState(fieldId, state);
        }

        void IWorldStateMutationPort.SetGravityFieldState(int entityId, GravityFieldPhase phase, int timerTicks)
        {
            SetGravityFieldState(entityId, phase, timerTicks);
        }

        void IWorldStateMutationPort.SetUnitKinematicState(int entityId, UnitKinematicRuntimeState state)
        {
            SetUnitKinematicState(entityId, state);
        }

        void IWorldStateMutationPort.SetUnitContinuousLocomotionState(int entityId, UnitContinuousLocomotionState state)
        {
            SetUnitContinuousLocomotionState(entityId, state);
        }

        void IWorldStateMutationPort.RemoveBoxInteractionLockState(int entityId)
        {
            RemoveBoxInteractionLockState(entityId);
        }

        void IWorldStateMutationPort.RemoveEnemyGravityFieldAuraFieldState(int fieldId)
        {
            RemoveEnemyGravityFieldAuraFieldState(fieldId);
        }

        void IWorldStateMutationPort.SetPhasedState(int entityId, PhasedRuntimeState state)
        {
            SetPhasedState(entityId, state);
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

        void IWorldStateMutationPort.SetBoxKineticOwner(int entityId, int instigatorEntityId, int instigatorTeamId)
        {
            SetBoxKineticOwner(entityId, instigatorEntityId, instigatorTeamId);
        }

        void IWorldStateMutationPort.SetBoardPresence(int entityId, EntityBoardPresence boardPresence)
        {
            SetBoardPresence(entityId, boardPresence);
        }

        void IWorldStateMutationPort.SetPlayerControlState(int entityId, PlayerControlState state)
        {
            SetPlayerControlState(entityId, state);
        }

        void IWorldStateMutationPort.SetPlayerDamageState(int entityId, PlayerDamageState state)
        {
            SetPlayerDamageState(entityId, state);
        }

        void IWorldStateMutationPort.SetTopology(CubeTopologyState topology)
        {
            SetTopology(topology);
        }

        void IWorldStateMutationPort.AddTileFeature(TileFeatureState state)
        {
            AddTileFeature(state);
        }

        void IWorldStateMutationPort.UpdateTileFeature(TileFeatureState state)
        {
            UpdateTileFeature(state);
        }

        void IWorldStateMutationPort.RemoveTileFeature(int tileId)
        {
            RemoveTileFeature(tileId);
        }
    }
}
