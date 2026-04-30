using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public readonly struct BoxInteractionLockState
    {
        public BoxInteractionLockState(
            int sourceEntityId,
            int sourceEffectIndex,
            int expiresTickExclusive,
            bool blocksPush,
            bool blocksFlip)
        {
            SourceEntityId = sourceEntityId;
            SourceEffectIndex = sourceEffectIndex;
            ExpiresTickExclusive = expiresTickExclusive;
            BlocksPush = blocksPush;
            BlocksFlip = blocksFlip;
        }

        public int SourceEntityId { get; }

        public int SourceEffectIndex { get; }

        public int ExpiresTickExclusive { get; }

        public bool BlocksPush { get; }

        public bool BlocksFlip { get; }
    }

    internal readonly struct BoxInteractionLockSnapshotEntry
    {
        public BoxInteractionLockSnapshotEntry(int entityId, BoxInteractionLockState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public BoxInteractionLockState State { get; }
    }

    internal static class BoxInteractionLockQueries
    {
        public static bool IsActive(in BoxInteractionLockState state, int tickIndex)
        {
            return tickIndex < state.ExpiresTickExclusive;
        }
    }

    // Read-only view over committed gameplay state. Entity positions and occupancy are the only gameplay coordinates.
    public sealed class WorldSnapshot
    {
        private readonly BoardBounds _boardBounds;
        private readonly IReadOnlyDictionary<int, EnemyActionRuntimeState> _enemyActionStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyPatrolRuntimeState> _enemyPatrolStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyChargeRuntimeState> _enemyChargeStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EntityExecutionLockState> _executionLockStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyJumpRuntimeState> _enemyJumpStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyGlideRuntimeState> _enemyGlideStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyUtilityRuntimeState> _enemyUtilityStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyFrontFaceSupportRuntimeState> _enemyFrontFaceSupportStatesByEntityId;
        private readonly IReadOnlyDictionary<int, BoxInteractionLockState> _boxInteractionLockStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EntityState> _entitiesById;
        private readonly IReadOnlyDictionary<int, PhasedRuntimeState> _phasedStatesByEntityId;
        private readonly IReadOnlyDictionary<int, PlayerDamageState> _playerDamageStatesByEntityId;
        private readonly IReadOnlyDictionary<int, PlayerControlState> _playerControlStatesByEntityId;
        private readonly IReadOnlyDictionary<int, SummonedEntityState> _summonedEntitiesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyDefinitionBindingState> _enemyDefinitionBindingsByEntityId;
        private readonly IReadOnlyDictionary<int, UnitKinematicRuntimeState> _unitKinematicStatesByEntityId;
        private readonly IReadOnlyDictionary<int, UnitContinuousLocomotionState> _unitContinuousLocomotionStatesByEntityId;
        private readonly IReadOnlyDictionary<SurfaceCell, int> _projectileOccupancy;
        private readonly IReadOnlyDictionary<SurfaceCell, int> _solidOccupancy;
        private readonly IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> _stackedUnitsByCell;
        private readonly TerrainData _terrainData;
        private readonly CubeTopologyState _topology;

        internal WorldSnapshot(
            Dictionary<int, EntityState> entitiesById,
            Dictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            Dictionary<SurfaceCell, int> solidOccupancy,
            Dictionary<SurfaceCell, int> projectileOccupancy,
            Dictionary<int, EnemyActionRuntimeState> enemyActionStatesByEntityId,
            Dictionary<int, EnemyPatrolRuntimeState> enemyPatrolStatesByEntityId,
            Dictionary<int, EnemyChargeRuntimeState> enemyChargeStatesByEntityId,
            Dictionary<int, EntityExecutionLockState> executionLockStatesByEntityId,
            Dictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            Dictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            Dictionary<int, EnemyUtilityRuntimeState> enemyUtilityStatesByEntityId,
            Dictionary<int, EnemyFrontFaceSupportRuntimeState> enemyFrontFaceSupportStatesByEntityId,
            Dictionary<int, BoxInteractionLockState> boxInteractionLockStatesByEntityId,
            Dictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            Dictionary<int, PlayerDamageState> playerDamageStatesByEntityId,
            Dictionary<int, PlayerControlState> playerControlStatesByEntityId,
            Dictionary<int, SummonedEntityState> summonedEntitiesByEntityId,
            Dictionary<int, EnemyDefinitionBindingState> enemyDefinitionBindingsByEntityId,
            Dictionary<int, UnitKinematicRuntimeState> unitKinematicStatesByEntityId,
            Dictionary<int, UnitContinuousLocomotionState> unitContinuousLocomotionStatesByEntityId,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            _entitiesById = new ReadOnlyDictionary<int, EntityState>(entitiesById ?? throw new ArgumentNullException(nameof(entitiesById)));
            _stackedUnitsByCell = CreateReadonlyStackedUnitsByCell(stackedUnitsByCell ?? throw new ArgumentNullException(nameof(stackedUnitsByCell)));
            _solidOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(solidOccupancy ?? throw new ArgumentNullException(nameof(solidOccupancy)));
            _projectileOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(projectileOccupancy ?? throw new ArgumentNullException(nameof(projectileOccupancy)));
            _enemyActionStatesByEntityId = new ReadOnlyDictionary<int, EnemyActionRuntimeState>(enemyActionStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyActionStatesByEntityId)));
            _enemyPatrolStatesByEntityId = new ReadOnlyDictionary<int, EnemyPatrolRuntimeState>(enemyPatrolStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyPatrolStatesByEntityId)));
            _enemyChargeStatesByEntityId = new ReadOnlyDictionary<int, EnemyChargeRuntimeState>(enemyChargeStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyChargeStatesByEntityId)));
            _executionLockStatesByEntityId = new ReadOnlyDictionary<int, EntityExecutionLockState>(executionLockStatesByEntityId ?? throw new ArgumentNullException(nameof(executionLockStatesByEntityId)));
            _enemyJumpStatesByEntityId = new ReadOnlyDictionary<int, EnemyJumpRuntimeState>(enemyJumpStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyJumpStatesByEntityId)));
            _enemyGlideStatesByEntityId = new ReadOnlyDictionary<int, EnemyGlideRuntimeState>(enemyGlideStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyGlideStatesByEntityId)));
            _enemyUtilityStatesByEntityId = new ReadOnlyDictionary<int, EnemyUtilityRuntimeState>(enemyUtilityStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyUtilityStatesByEntityId)));
            _enemyFrontFaceSupportStatesByEntityId = new ReadOnlyDictionary<int, EnemyFrontFaceSupportRuntimeState>(enemyFrontFaceSupportStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyFrontFaceSupportStatesByEntityId)));
            _boxInteractionLockStatesByEntityId = new ReadOnlyDictionary<int, BoxInteractionLockState>(boxInteractionLockStatesByEntityId ?? throw new ArgumentNullException(nameof(boxInteractionLockStatesByEntityId)));
            _phasedStatesByEntityId = new ReadOnlyDictionary<int, PhasedRuntimeState>(phasedStatesByEntityId ?? throw new ArgumentNullException(nameof(phasedStatesByEntityId)));
            _playerDamageStatesByEntityId = new ReadOnlyDictionary<int, PlayerDamageState>(playerDamageStatesByEntityId ?? throw new ArgumentNullException(nameof(playerDamageStatesByEntityId)));
            _playerControlStatesByEntityId = new ReadOnlyDictionary<int, PlayerControlState>(playerControlStatesByEntityId ?? throw new ArgumentNullException(nameof(playerControlStatesByEntityId)));
            _summonedEntitiesByEntityId = new ReadOnlyDictionary<int, SummonedEntityState>(summonedEntitiesByEntityId ?? throw new ArgumentNullException(nameof(summonedEntitiesByEntityId)));
            _enemyDefinitionBindingsByEntityId = new ReadOnlyDictionary<int, EnemyDefinitionBindingState>(enemyDefinitionBindingsByEntityId ?? throw new ArgumentNullException(nameof(enemyDefinitionBindingsByEntityId)));
            _unitKinematicStatesByEntityId = new ReadOnlyDictionary<int, UnitKinematicRuntimeState>(unitKinematicStatesByEntityId ?? throw new ArgumentNullException(nameof(unitKinematicStatesByEntityId)));
            _unitContinuousLocomotionStatesByEntityId = new ReadOnlyDictionary<int, UnitContinuousLocomotionState>(unitContinuousLocomotionStatesByEntityId ?? throw new ArgumentNullException(nameof(unitContinuousLocomotionStatesByEntityId)));
            _topology = topology;
            _boardBounds = boardBounds;
            _terrainData = terrainData ?? throw new ArgumentNullException(nameof(terrainData));
        }

        public BoardBounds BoardBounds => _boardBounds;

        public CubeTopologyState Topology => _topology;

        internal TerrainData TerrainData => _terrainData;

        internal IReadOnlyDictionary<int, EntityState> EntitiesById => _entitiesById;

        public bool TryGetEntity(int entityId, out EntityState entity)
        {
            return _entitiesById.TryGetValue(entityId, out entity);
        }

        public bool TryGetPlayerControlState(int entityId, out PlayerControlState state)
        {
            return _playerControlStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetPlayerDamageState(int entityId, out PlayerDamageState state)
        {
            return _playerDamageStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetUnitKinematicPose(int entityId, out UnitKinematicPose pose)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity) ||
                entity.type != EntityType.Unit)
            {
                pose = default;
                return false;
            }

            var hasAuthoritativeState = _unitKinematicStatesByEntityId.TryGetValue(entityId, out var state);
            pose = new UnitKinematicPose(
                entity.position,
                hasAuthoritativeState ? state : UnitKinematicRuntimeState.SettledZero,
                hasAuthoritativeState);
            return true;
        }

        internal bool TryGetUnitKinematicState(int entityId, out UnitKinematicRuntimeState state)
        {
            return _unitKinematicStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetUnitContinuousLocomotionPose(int entityId, out UnitContinuousLocomotionPose pose)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity) ||
                entity.type != EntityType.Unit)
            {
                pose = default;
                return false;
            }

            var hasAuthoritativeState = _unitContinuousLocomotionStatesByEntityId.TryGetValue(entityId, out var state);
            pose = new UnitContinuousLocomotionPose(
                entity.position,
                hasAuthoritativeState ? state : UnitContinuousLocomotionState.SettledZero,
                hasAuthoritativeState);
            return true;
        }

        internal bool TryGetUnitContinuousLocomotionState(int entityId, out UnitContinuousLocomotionState state)
        {
            return _unitContinuousLocomotionStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyActionState(int entityId, out EnemyActionRuntimeState state)
        {
            return _enemyActionStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyPatrolState(int entityId, out EnemyPatrolRuntimeState state)
        {
            return _enemyPatrolStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyChargeState(int entityId, out EnemyChargeRuntimeState state)
        {
            return _enemyChargeStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEntityExecutionLockState(int entityId, out EntityExecutionLockState state)
        {
            return _executionLockStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyJumpState(int entityId, out EnemyJumpRuntimeState state)
        {
            return _enemyJumpStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyGlideState(int entityId, out EnemyGlideRuntimeState state)
        {
            return _enemyGlideStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetActiveEnemyGlideState(int entityId, out EnemyGlideRuntimeState state)
        {
            if (_enemyGlideStatesByEntityId.TryGetValue(entityId, out state) &&
                state.IsActive)
            {
                return true;
            }

            state = default;
            return false;
        }

        public bool TryGetEnemyUtilityState(int entityId, out EnemyUtilityRuntimeState state)
        {
            return _enemyUtilityStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyFrontFaceSupportState(int entityId, out EnemyFrontFaceSupportRuntimeState state)
        {
            return _enemyFrontFaceSupportStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetBoxInteractionLockState(int entityId, out BoxInteractionLockState state)
        {
            return _boxInteractionLockStatesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetActiveBoxInteractionLockState(int entityId, int tickIndex, out BoxInteractionLockState state)
        {
            if (!_boxInteractionLockStatesByEntityId.TryGetValue(entityId, out state) ||
                !BoxInteractionLockQueries.IsActive(state, tickIndex))
            {
                state = default;
                return false;
            }

            return true;
        }

        public bool TryGetSummonedEntityState(int entityId, out SummonedEntityState state)
        {
            return _summonedEntitiesByEntityId.TryGetValue(entityId, out state);
        }

        public bool TryGetEnemyDefinitionBindingState(int entityId, out EnemyDefinitionBindingState state)
        {
            return _enemyDefinitionBindingsByEntityId.TryGetValue(entityId, out state);
        }

        internal bool TryGetPhasedState(int entityId, out PhasedRuntimeState state)
        {
            return _phasedStatesByEntityId.TryGetValue(entityId, out state);
        }

        internal bool TryGetResolvedSpatialState(int entityId, out ResolvedSpatialState spatialState)
        {
            if (!_entitiesById.TryGetValue(entityId, out var entity))
            {
                spatialState = default;
                return false;
            }

            var hasJumpState = _enemyJumpStatesByEntityId.TryGetValue(entityId, out var jumpState);
            var hasPhasedState = _phasedStatesByEntityId.TryGetValue(entityId, out var phasedState);
            spatialState = SpatialStateResolver.Resolve(
                entity,
                _topology,
                hasJumpState ? jumpState : (EnemyJumpRuntimeState?)null,
                hasPhasedState ? phasedState : (PhasedRuntimeState?)null);
            return true;
        }

        public bool CanStartAction(int entityId, int tickIndex)
        {
            return !TryGetEntityExecutionLockState(entityId, out var state) ||
                   EntityExecutionLockQueries.CanStartAction(state, tickIndex);
        }

        public bool CanExecuteIntent(int entityId, int tickIndex)
        {
            return !TryGetEntityExecutionLockState(entityId, out var state) ||
                   EntityExecutionLockQueries.CanExecuteIntent(state, tickIndex);
        }

        public bool CanExecuteMovementIntent(int entityId, int tickIndex)
        {
            return !TryGetEntityExecutionLockState(entityId, out var state) ||
                   EntityExecutionLockQueries.CanExecuteMovementIntent(state, tickIndex);
        }

        public bool HasAnyUnitAt(SurfaceCell cell)
        {
            return HasAnyUnitAt(_topology, cell);
        }

        public bool HasAnyUnitAt(Vector2Int cell)
        {
            return HasAnyUnitAt(CreateDefaultQueryCell(cell));
        }

        public void EnumerateUnitsAt(SurfaceCell cell, List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateUnitsAt(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                _topology,
                cell,
                buffer);
        }

        public void EnumerateUnitsAt(Vector2Int cell, List<EntityState> buffer)
        {
            EnumerateUnitsAt(CreateDefaultQueryCell(cell), buffer);
        }

        internal void EnumerateUnitsAt(CubeTopologyState topology, SurfaceCell cell, List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateUnitsAt(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                topology,
                cell,
                buffer);
        }

        public bool TryGetPrimaryUnitAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetPrimaryUnitAt(_topology, cell, out entity);
        }

        public bool TryGetPrimaryUnitAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetPrimaryUnitAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool TryGetBoxAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetBoxAt(_topology, cell, out entity);
        }

        public bool TryGetBoxAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetBoxAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool TryGetSolidSemanticAt(SurfaceCell cell, out SolidSemantic semantic)
        {
            return TryGetSolidSemanticAt(_topology, cell, out semantic);
        }

        public bool TryGetSolidSemanticAt(Vector2Int cell, out SolidSemantic semantic)
        {
            return TryGetSolidSemanticAt(CreateDefaultQueryCell(cell), out semantic);
        }

        public bool TryGetSolidOccupantAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetSolidOccupantAt(_topology, cell, out entity);
        }

        public bool TryGetSolidOccupantAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetSolidOccupantAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool IsWallAt(SurfaceCell cell)
        {
            return IsWallAt(_topology, cell);
        }

        public bool IsWallAt(Vector2Int cell)
        {
            return IsWallAt(CreateDefaultQueryCell(cell));
        }

        public bool IsBoxAt(SurfaceCell cell)
        {
            return IsBoxAt(_topology, cell);
        }

        public bool IsBoxAt(Vector2Int cell)
        {
            return IsBoxAt(CreateDefaultQueryCell(cell));
        }

        public bool TryPickImpactTargetAt(SurfaceCell cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickImpactTargetAt(_topology, cell, sourceTeamId, out entity);
        }

        public bool TryPickHostileUnitImpactTargetAt(SurfaceCell cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickHostileUnitImpactTargetAt(_topology, cell, sourceTeamId, out entity);
        }

        public bool TryPickHostileUnitImpactTargetAtForBoxSlide(SurfaceCell cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickHostileUnitImpactTargetAtForBoxSlide(_topology, cell, sourceTeamId, out entity);
        }

        public bool TryPickImpactTargetAt(Vector2Int cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickImpactTargetAt(CreateDefaultQueryCell(cell), sourceTeamId, out entity);
        }

        public bool TryPickHostileUnitImpactTargetAt(Vector2Int cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickHostileUnitImpactTargetAt(CreateDefaultQueryCell(cell), sourceTeamId, out entity);
        }

        public bool TryGetProjectileAt(SurfaceCell cell, out EntityState entity)
        {
            return TryGetProjectileAt(_topology, cell, out entity);
        }

        public bool TryGetProjectileAt(Vector2Int cell, out EntityState entity)
        {
            return TryGetProjectileAt(CreateDefaultQueryCell(cell), out entity);
        }

        public bool IsInsideBoard(SurfaceCell cell)
        {
            return _boardBounds.Contains(cell.PlanarPosition);
        }

        public bool IsInsideBoard(Vector2Int cell)
        {
            return _boardBounds.Contains(cell);
        }

        public bool IsTerrainBlockedForUnit(SurfaceCell cell)
        {
            return SnapshotReadQueries.IsTerrainBlockedForUnit(_topology, _terrainData, cell);
        }

        public bool IsTerrainBlockedForUnit(Vector2Int cell)
        {
            return IsTerrainBlockedForUnit(CreateDefaultQueryCell(cell));
        }

        public bool TryGetTerrain(SurfaceCell cell, out TerrainCellState terrainCell)
        {
            return SnapshotReadQueries.TryGetTerrain(_terrainData, cell, out terrainCell);
        }

        public bool TryGetTerrain(Vector2Int cell, out TerrainCellState terrainCell)
        {
            return TryGetTerrain(CreateDefaultQueryCell(cell), out terrainCell);
        }

        public bool TryGetUnitTraversalBlocker(SurfaceCell cell, out SlideStopper blocker)
        {
            return TryGetUnitBlocker(cell, out blocker);
        }

        public bool TryGetUnitTraversalBlocker(Vector2Int cell, out SlideStopper blocker)
        {
            return TryGetUnitTraversalBlocker(CreateDefaultQueryCell(cell), out blocker);
        }

        internal bool TryGetPlacementBlocker(
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlocker(_topology, entityType, cell, ignoredEntityId, out blocker);
        }

        internal bool TryGetPlacementBlocker(
            CubeTopologyState topology,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetGameplayPlacementBlocker(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _enemyGlideStatesByEntityId,
                _phasedStatesByEntityId,
                _solidOccupancy,
                _projectileOccupancy,
                topology,
                _boardBounds,
                _terrainData,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        internal bool TryGetPlacementBlocker(
            EntityType entityType,
            Vector2Int cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetPlacementBlocker(entityType, CreateDefaultQueryCell(cell), ignoredEntityId, out blocker);
        }

        internal bool TryGetAuthoritativePlacementBlocker(
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetAuthoritativePlacementBlocker(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _enemyGlideStatesByEntityId,
                _phasedStatesByEntityId,
                _solidOccupancy,
                _projectileOccupancy,
                _boardBounds,
                _terrainData,
                entityType,
                cell,
                ignoredEntityId,
                out blocker);
        }

        internal bool TryGetAuthoritativePlacementBlocker(
            EntityType entityType,
            Vector2Int cell,
            int ignoredEntityId,
            out SlideStopper blocker)
        {
            return TryGetAuthoritativePlacementBlocker(
                entityType,
                CreateDefaultQueryCell(cell),
                ignoredEntityId,
                out blocker);
        }

        public bool TryResolvePlayerStep(
            SurfaceCell origin,
            Direction direction,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return SurfaceTraversalQueries.TryResolvePlayerStep(
                _topology,
                _boardBounds,
                origin,
                direction,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public bool TryResolvePlayerStep(
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return SurfaceTraversalQueries.TryResolvePlayerStep(
                _topology,
                _boardBounds,
                origin,
                delta,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        public bool TryResolveUnitStep(
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology)
        {
            return SurfaceTraversalQueries.TryResolveUnitStep(
                _topology,
                _boardBounds,
                origin,
                delta,
                out destination,
                out rotationKind,
                out updatedTopology);
        }

        internal bool TryResolveLocalFlipCells(
            SurfaceCell actorCell,
            Vector2Int delta,
            out SurfaceCell targetCell,
            out SurfaceCell landingCell)
        {
            return SurfaceTraversalQueries.TryResolveLocalFlipCells(
                _topology,
                _boardBounds,
                actorCell,
                delta,
                out targetCell,
                out landingCell);
        }

        public bool TryResolveNextSurfaceBoxSlideStep(
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            return TryResolveNextSurfaceBoxSlideStep(_topology, origin, delta, out destination, out stopper);
        }

        internal bool TryResolveNextSurfaceBoxSlideStep(
            CubeTopologyState topology,
            SurfaceCell origin,
            Vector2Int delta,
            out SurfaceCell destination,
            out SlideStopper stopper)
        {
            return SurfaceSlideQueries.TryResolveNextSurfaceBoxSlideStep(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _enemyGlideStatesByEntityId,
                _phasedStatesByEntityId,
                _solidOccupancy,
                topology,
                _boardBounds,
                _terrainData,
                origin,
                delta,
                out destination,
                out stopper);
        }

        public bool CanBeTargetedForNewSelection(int entityId)
        {
            return SnapshotReadQueries.CanBeTargetedForNewSelection(
                _entitiesById,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                _topology,
                entityId);
        }

        public void EnumerateEntitiesOrdered(List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateEntitiesOrdered(_entitiesById, buffer);
        }

        internal bool TryGetUnitBlocker(SurfaceCell cell, out SlideStopper blocker)
        {
            return TryGetUnitBlocker(_topology, cell, out blocker);
        }

        internal bool TryGetUnitBlocker(
            CubeTopologyState topology,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetUnitBlocker(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _enemyGlideStatesByEntityId,
                _phasedStatesByEntityId,
                _solidOccupancy,
                topology,
                _boardBounds,
                _terrainData,
                cell,
                out blocker);
        }

        internal bool TryGetUnitBlocker(Vector2Int cell, out SlideStopper blocker)
        {
            return TryGetUnitBlocker(CreateDefaultQueryCell(cell), out blocker);
        }

        internal void EnumerateTerrainBlockedCellsOrdered(List<Vector2Int> buffer)
        {
            SnapshotReadQueries.EnumerateTerrainBlockedCellsOrdered(_terrainData, buffer);
        }

        internal void EnumerateTerrainCellsOrdered(List<TerrainCellState> buffer)
        {
            SnapshotReadQueries.EnumerateTerrainCellsOrdered(_terrainData, buffer);
        }

        internal void EnumerateUnitOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateStackedUnitOccupancyOrdered(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                _topology,
                buffer);
        }

        internal void EnumerateSolidOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(
                _entitiesById,
                _solidOccupancy,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                _topology,
                buffer);
        }

        internal void EnumeratePlayerControlStatesOrdered(List<PlayerControlSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _playerControlStatesByEntityId)
            {
                buffer.Add(new PlayerControlSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumeratePlayerDamageStatesOrdered(List<PlayerDamageSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _playerDamageStatesByEntityId)
            {
                buffer.Add(new PlayerDamageSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyActionStatesOrdered(List<EnemyActionSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _enemyActionStatesByEntityId)
            {
                buffer.Add(new EnemyActionSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyPatrolStatesOrdered(List<EnemyPatrolSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _enemyPatrolStatesByEntityId)
            {
                buffer.Add(new EnemyPatrolSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyJumpStatesOrdered(List<EnemyJumpSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _enemyJumpStatesByEntityId)
            {
                buffer.Add(new EnemyJumpSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEnemyGlideStatesOrdered(List<EnemyGlideSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _enemyGlideStatesByEntityId)
            {
                buffer.Add(new EnemyGlideSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
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

        internal void EnumerateEnemyChargeStatesOrdered(List<EnemyChargeSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _enemyChargeStatesByEntityId)
            {
                buffer.Add(new EnemyChargeSnapshotEntry(pair.Key, pair.Value));
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

        internal void EnumeratePhasedStatesOrdered(List<PhasedSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _phasedStatesByEntityId)
            {
                buffer.Add(new PhasedSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateEntityExecutionLockStatesOrdered(List<EntityExecutionLockSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _executionLockStatesByEntityId)
            {
                buffer.Add(new EntityExecutionLockSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal void EnumerateProjectileOccupancyOrdered(List<SnapshotOccupancyEntry> buffer)
        {
            SnapshotReadQueries.EnumerateOccupancyOrdered(_entitiesById, _projectileOccupancy, _topology, buffer);
        }

        internal bool HasAnyUnitAt(CubeTopologyState topology, SurfaceCell cell)
        {
            return SnapshotReadQueries.HasAnyUnitAt(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                topology,
                cell);
        }

        internal bool TryGetPrimaryUnitAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetPrimaryUnitAt(
                _entitiesById,
                _stackedUnitsByCell,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                topology,
                cell,
                out entity);
        }

        internal bool TryGetBoxAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetBoxAt(_entitiesById, _solidOccupancy, topology, cell, out entity);
        }

        internal bool TryGetSolidSemanticAt(CubeTopologyState topology, SurfaceCell cell, out SolidSemantic semantic)
        {
            return SnapshotReadQueries.TryGetSolidSemanticAt(_entitiesById, _solidOccupancy, topology, cell, out semantic);
        }

        internal bool TryGetSolidOccupantAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetSolidOccupantAt(_entitiesById, _solidOccupancy, topology, cell, out entity);
        }

        internal bool IsWallAt(CubeTopologyState topology, SurfaceCell cell)
        {
            return SnapshotReadQueries.IsWallAt(_entitiesById, _solidOccupancy, topology, cell);
        }

        internal bool IsBoxAt(CubeTopologyState topology, SurfaceCell cell)
        {
            return SnapshotReadQueries.IsBoxAt(_entitiesById, _solidOccupancy, topology, cell);
        }

        internal bool TryPickImpactTargetAt(
            CubeTopologyState topology,
            SurfaceCell cell,
            int sourceTeamId,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryPickImpactTargetAt(
                _entitiesById,
                _stackedUnitsByCell,
                _solidOccupancy,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                topology,
                cell,
                sourceTeamId,
                out entity);
        }

        internal bool TryPickHostileUnitImpactTargetAt(
            CubeTopologyState topology,
            SurfaceCell cell,
            int sourceTeamId,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryPickHostileUnitImpactTargetAt(
                _entitiesById,
                _stackedUnitsByCell,
                _solidOccupancy,
                _enemyJumpStatesByEntityId,
                _enemyGlideStatesByEntityId,
                _phasedStatesByEntityId,
                topology,
                cell,
                sourceTeamId,
                skipActiveGlideTargets: false,
                allowGlideTargetsOverSolid: true,
                out entity);
        }

        internal bool TryPickHostileUnitImpactTargetAtForBoxSlide(
            CubeTopologyState topology,
            SurfaceCell cell,
            int sourceTeamId,
            out EntityState entity)
        {
            return SnapshotReadQueries.TryPickHostileUnitImpactTargetAt(
                _entitiesById,
                _stackedUnitsByCell,
                _solidOccupancy,
                _enemyJumpStatesByEntityId,
                _enemyGlideStatesByEntityId,
                _phasedStatesByEntityId,
                topology,
                cell,
                sourceTeamId,
                skipActiveGlideTargets: true,
                allowGlideTargetsOverSolid: true,
                out entity);
        }

        internal bool TryGetProjectileAt(CubeTopologyState topology, SurfaceCell cell, out EntityState entity)
        {
            return SnapshotReadQueries.TryGetEntityAt(
                _entitiesById,
                _projectileOccupancy,
                _enemyJumpStatesByEntityId,
                _phasedStatesByEntityId,
                topology,
                cell,
                out entity);
        }

        private SurfaceCell CreateDefaultQueryCell(Vector2Int cell)
        {
            return SurfaceCell.FromPlanar(cell, _topology.BottomFace);
        }

        private static ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> CreateReadonlyStackedUnitsByCell(
            Dictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell)
        {
            var buffer = new Dictionary<SurfaceCell, IReadOnlyCollection<int>>(stackedUnitsByCell.Count);

            foreach (var pair in stackedUnitsByCell)
            {
                if (pair.Value == null)
                {
                    throw new ArgumentNullException(nameof(stackedUnitsByCell));
                }

                var orderedEntityIds = new List<int>(pair.Value.Count);
                foreach (var entityId in pair.Value)
                {
                    orderedEntityIds.Add(entityId);
                }

                buffer.Add(pair.Key, orderedEntityIds.AsReadOnly());
            }

            return new ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>>(buffer);
        }
    }
}
