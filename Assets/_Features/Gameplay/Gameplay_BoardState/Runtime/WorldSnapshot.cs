using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    internal sealed class SnapshotOwnedCellIndex<TKey>
    {
        internal SnapshotOwnedCellIndex(Dictionary<TKey, IReadOnlyCollection<int>> values)
        {
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        internal Dictionary<TKey, IReadOnlyCollection<int>> Values { get; }
    }

    public enum BoxInteractionLockSourceReason
    {
        Unspecified = 0,
        EnemyUtility = 1,
        GravityField = 2,
        MoonBlockGeneratorSpawn = 3,
        EnemyGravityFieldAura = 4,
    }

    public readonly struct BoxInteractionLockState
    {
        public BoxInteractionLockState(
            int sourceEntityId,
            int sourceEffectIndex,
            int expiresTickExclusive,
            bool blocksPush,
            bool blocksFlip)
            : this(
                sourceEntityId,
                sourceEffectIndex,
                expiresTickExclusive,
                blocksPush,
                blocksFlip,
                blocksDestroy: false,
                BoxInteractionLockSourceReason.EnemyUtility)
        {
        }

        public BoxInteractionLockState(
            int sourceEntityId,
            int sourceEffectIndex,
            int expiresTickExclusive,
            bool blocksPush,
            bool blocksFlip,
            bool blocksDestroy,
            BoxInteractionLockSourceReason sourceReason)
        {
            SourceEntityId = sourceEntityId;
            SourceEffectIndex = sourceEffectIndex;
            ExpiresTickExclusive = expiresTickExclusive;
            BlocksPush = blocksPush;
            BlocksFlip = blocksFlip;
            BlocksDestroy = blocksDestroy;
            SourceReason = sourceReason;
        }

        public int SourceEntityId { get; }

        public int SourceEffectIndex { get; }

        public int ExpiresTickExclusive { get; }

        public bool BlocksPush { get; }

        public bool BlocksFlip { get; }

        public bool BlocksDestroy { get; }

        public BoxInteractionLockSourceReason SourceReason { get; }
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

    public readonly struct EnemyGravityFieldAuraFieldState
    {
        public EnemyGravityFieldAuraFieldState(
            int sourceEntityId,
            int sourceEffectIndex,
            int activationSequence,
            SurfaceCell originCell,
            int radius,
            int startedTick,
            int expiresTickExclusive,
            bool blocksPush,
            bool blocksFlip,
            bool blocksDestroy)
        {
            SourceEntityId = sourceEntityId;
            SourceEffectIndex = sourceEffectIndex;
            ActivationSequence = activationSequence;
            OriginCell = originCell;
            Radius = radius;
            StartedTick = startedTick;
            ExpiresTickExclusive = expiresTickExclusive;
            BlocksPush = blocksPush;
            BlocksFlip = blocksFlip;
            BlocksDestroy = blocksDestroy;
        }

        public int SourceEntityId { get; }

        public int SourceEffectIndex { get; }

        public int ActivationSequence { get; }

        public SurfaceCell OriginCell { get; }

        public int Radius { get; }

        public int StartedTick { get; }

        public int ExpiresTickExclusive { get; }

        public bool BlocksPush { get; }

        public bool BlocksFlip { get; }

        public bool BlocksDestroy { get; }

        public bool IsActive(int tickIndex)
        {
            return tickIndex < ExpiresTickExclusive;
        }
    }

    internal readonly struct EnemyGravityFieldAuraFieldSnapshotEntry
    {
        public EnemyGravityFieldAuraFieldSnapshotEntry(int fieldId, EnemyGravityFieldAuraFieldState state)
        {
            FieldId = fieldId;
            State = state;
        }

        public int FieldId { get; }

        public EnemyGravityFieldAuraFieldState State { get; }
    }

    internal static class EnemyGravityFieldAuraFieldIds
    {
        public static int Compute(int sourceEntityId, int sourceEffectIndex, int activationSequence)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 397) ^ sourceEntityId;
                hash = (hash * 397) ^ sourceEffectIndex;
                hash = (hash * 397) ^ activationSequence;
                return hash;
            }
        }
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
        private readonly IReadOnlyDictionary<int, PendingCellImpact> _pendingCellImpactsById;
        private readonly IReadOnlyDictionary<int, PendingEnemyBlockedReaction> _pendingEnemyBlockedReactionsByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyPatrolRuntimeState> _enemyPatrolStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyChargeRuntimeState> _enemyChargeStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EntityExecutionLockState> _executionLockStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyJumpRuntimeState> _enemyJumpStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyGlideRuntimeState> _enemyGlideStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyUtilityRuntimeState> _enemyUtilityStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyFrontFaceSupportRuntimeState> _enemyFrontFaceSupportStatesByEntityId;
        private readonly IReadOnlyDictionary<int, BoxInteractionLockState> _boxInteractionLockStatesByEntityId;
        private readonly IReadOnlyDictionary<int, EnemyGravityFieldAuraFieldState> _enemyGravityFieldAuraFieldsById;
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
        private readonly IReadOnlyDictionary<int, TileFeatureState> _tileFeaturesById;
        private readonly IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> _tileFeatureIdsByCell;
        private readonly TerrainData _terrainData;
        private readonly CubeTopologyState _topology;
        private readonly int _topologyRevision;
        private EntityState[] _orderedEntitiesCache;
        private TileFeatureState[] _orderedTileFeaturesCache;

        internal WorldSnapshot(
            Dictionary<int, EntityState> entitiesById,
            Dictionary<SurfaceCell, SortedSet<int>> stackedUnitsByCell,
            Dictionary<SurfaceCell, int> solidOccupancy,
            Dictionary<SurfaceCell, int> projectileOccupancy,
            Dictionary<int, TileFeatureState> tileFeaturesById,
            Dictionary<SurfaceCell, SortedSet<int>> tileFeatureIdsByCell,
            Dictionary<int, EnemyActionRuntimeState> enemyActionStatesByEntityId,
            Dictionary<int, PendingCellImpact> pendingCellImpactsById,
            Dictionary<int, PendingEnemyBlockedReaction> pendingEnemyBlockedReactionsByEntityId,
            Dictionary<int, EnemyPatrolRuntimeState> enemyPatrolStatesByEntityId,
            Dictionary<int, EnemyChargeRuntimeState> enemyChargeStatesByEntityId,
            Dictionary<int, EntityExecutionLockState> executionLockStatesByEntityId,
            Dictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            Dictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            Dictionary<int, EnemyUtilityRuntimeState> enemyUtilityStatesByEntityId,
            Dictionary<int, EnemyFrontFaceSupportRuntimeState> enemyFrontFaceSupportStatesByEntityId,
            Dictionary<int, BoxInteractionLockState> boxInteractionLockStatesByEntityId,
            Dictionary<int, EnemyGravityFieldAuraFieldState> enemyGravityFieldAuraFieldsById,
            Dictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            Dictionary<int, PlayerDamageState> playerDamageStatesByEntityId,
            Dictionary<int, PlayerControlState> playerControlStatesByEntityId,
            Dictionary<int, SummonedEntityState> summonedEntitiesByEntityId,
            Dictionary<int, EnemyDefinitionBindingState> enemyDefinitionBindingsByEntityId,
            Dictionary<int, UnitKinematicRuntimeState> unitKinematicStatesByEntityId,
            Dictionary<int, UnitContinuousLocomotionState> unitContinuousLocomotionStatesByEntityId,
            CubeTopologyState topology,
            int topologyRevision,
            BoardBounds boardBounds,
            TerrainData terrainData)
            : this(
                entitiesById,
                CreateReadonlyStackedUnitsByCell(stackedUnitsByCell ?? throw new ArgumentNullException(nameof(stackedUnitsByCell))),
                solidOccupancy,
                projectileOccupancy,
                tileFeaturesById,
                CreateReadonlyTileFeatureIdsByCell(tileFeatureIdsByCell ?? throw new ArgumentNullException(nameof(tileFeatureIdsByCell))),
                enemyActionStatesByEntityId,
                pendingCellImpactsById,
                pendingEnemyBlockedReactionsByEntityId,
                enemyPatrolStatesByEntityId,
                enemyChargeStatesByEntityId,
                executionLockStatesByEntityId,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                enemyUtilityStatesByEntityId,
                enemyFrontFaceSupportStatesByEntityId,
                boxInteractionLockStatesByEntityId,
                enemyGravityFieldAuraFieldsById,
                phasedStatesByEntityId,
                playerDamageStatesByEntityId,
                playerControlStatesByEntityId,
                summonedEntitiesByEntityId,
                enemyDefinitionBindingsByEntityId,
                unitKinematicStatesByEntityId,
                unitContinuousLocomotionStatesByEntityId,
                topology,
                topologyRevision,
                boardBounds,
                terrainData)
        {
        }

        internal static WorldSnapshot CreateWithSnapshotOwnedCellIndexes(
            Dictionary<int, EntityState> entitiesById,
            SnapshotOwnedCellIndex<SurfaceCell> stackedUnitsByCell,
            Dictionary<SurfaceCell, int> solidOccupancy,
            Dictionary<SurfaceCell, int> projectileOccupancy,
            Dictionary<int, TileFeatureState> tileFeaturesById,
            SnapshotOwnedCellIndex<SurfaceCell> tileFeatureIdsByCell,
            Dictionary<int, EnemyActionRuntimeState> enemyActionStatesByEntityId,
            Dictionary<int, PendingCellImpact> pendingCellImpactsById,
            Dictionary<int, PendingEnemyBlockedReaction> pendingEnemyBlockedReactionsByEntityId,
            Dictionary<int, EnemyPatrolRuntimeState> enemyPatrolStatesByEntityId,
            Dictionary<int, EnemyChargeRuntimeState> enemyChargeStatesByEntityId,
            Dictionary<int, EntityExecutionLockState> executionLockStatesByEntityId,
            Dictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            Dictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            Dictionary<int, EnemyUtilityRuntimeState> enemyUtilityStatesByEntityId,
            Dictionary<int, EnemyFrontFaceSupportRuntimeState> enemyFrontFaceSupportStatesByEntityId,
            Dictionary<int, BoxInteractionLockState> boxInteractionLockStatesByEntityId,
            Dictionary<int, EnemyGravityFieldAuraFieldState> enemyGravityFieldAuraFieldsById,
            Dictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            Dictionary<int, PlayerDamageState> playerDamageStatesByEntityId,
            Dictionary<int, PlayerControlState> playerControlStatesByEntityId,
            Dictionary<int, SummonedEntityState> summonedEntitiesByEntityId,
            Dictionary<int, EnemyDefinitionBindingState> enemyDefinitionBindingsByEntityId,
            Dictionary<int, UnitKinematicRuntimeState> unitKinematicStatesByEntityId,
            Dictionary<int, UnitContinuousLocomotionState> unitContinuousLocomotionStatesByEntityId,
            CubeTopologyState topology,
            int topologyRevision,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            return new WorldSnapshot(
                entitiesById,
                CreateReadonlySnapshotOwnedCellIndex(stackedUnitsByCell ?? throw new ArgumentNullException(nameof(stackedUnitsByCell))),
                solidOccupancy,
                projectileOccupancy,
                tileFeaturesById,
                CreateReadonlySnapshotOwnedCellIndex(tileFeatureIdsByCell ?? throw new ArgumentNullException(nameof(tileFeatureIdsByCell))),
                enemyActionStatesByEntityId,
                pendingCellImpactsById,
                pendingEnemyBlockedReactionsByEntityId,
                enemyPatrolStatesByEntityId,
                enemyChargeStatesByEntityId,
                executionLockStatesByEntityId,
                enemyJumpStatesByEntityId,
                enemyGlideStatesByEntityId,
                enemyUtilityStatesByEntityId,
                enemyFrontFaceSupportStatesByEntityId,
                boxInteractionLockStatesByEntityId,
                enemyGravityFieldAuraFieldsById,
                phasedStatesByEntityId,
                playerDamageStatesByEntityId,
                playerControlStatesByEntityId,
                summonedEntitiesByEntityId,
                enemyDefinitionBindingsByEntityId,
                unitKinematicStatesByEntityId,
                unitContinuousLocomotionStatesByEntityId,
                topology,
                topologyRevision,
                boardBounds,
                terrainData);
        }

        private WorldSnapshot(
            Dictionary<int, EntityState> entitiesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> stackedUnitsByCell,
            Dictionary<SurfaceCell, int> solidOccupancy,
            Dictionary<SurfaceCell, int> projectileOccupancy,
            Dictionary<int, TileFeatureState> tileFeaturesById,
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> tileFeatureIdsByCell,
            Dictionary<int, EnemyActionRuntimeState> enemyActionStatesByEntityId,
            Dictionary<int, PendingCellImpact> pendingCellImpactsById,
            Dictionary<int, PendingEnemyBlockedReaction> pendingEnemyBlockedReactionsByEntityId,
            Dictionary<int, EnemyPatrolRuntimeState> enemyPatrolStatesByEntityId,
            Dictionary<int, EnemyChargeRuntimeState> enemyChargeStatesByEntityId,
            Dictionary<int, EntityExecutionLockState> executionLockStatesByEntityId,
            Dictionary<int, EnemyJumpRuntimeState> enemyJumpStatesByEntityId,
            Dictionary<int, EnemyGlideRuntimeState> enemyGlideStatesByEntityId,
            Dictionary<int, EnemyUtilityRuntimeState> enemyUtilityStatesByEntityId,
            Dictionary<int, EnemyFrontFaceSupportRuntimeState> enemyFrontFaceSupportStatesByEntityId,
            Dictionary<int, BoxInteractionLockState> boxInteractionLockStatesByEntityId,
            Dictionary<int, EnemyGravityFieldAuraFieldState> enemyGravityFieldAuraFieldsById,
            Dictionary<int, PhasedRuntimeState> phasedStatesByEntityId,
            Dictionary<int, PlayerDamageState> playerDamageStatesByEntityId,
            Dictionary<int, PlayerControlState> playerControlStatesByEntityId,
            Dictionary<int, SummonedEntityState> summonedEntitiesByEntityId,
            Dictionary<int, EnemyDefinitionBindingState> enemyDefinitionBindingsByEntityId,
            Dictionary<int, UnitKinematicRuntimeState> unitKinematicStatesByEntityId,
            Dictionary<int, UnitContinuousLocomotionState> unitContinuousLocomotionStatesByEntityId,
            CubeTopologyState topology,
            int topologyRevision,
            BoardBounds boardBounds,
            TerrainData terrainData)
        {
            _entitiesById = new ReadOnlyDictionary<int, EntityState>(entitiesById ?? throw new ArgumentNullException(nameof(entitiesById)));
            _stackedUnitsByCell = stackedUnitsByCell ?? throw new ArgumentNullException(nameof(stackedUnitsByCell));
            _solidOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(solidOccupancy ?? throw new ArgumentNullException(nameof(solidOccupancy)));
            _projectileOccupancy = new ReadOnlyDictionary<SurfaceCell, int>(projectileOccupancy ?? throw new ArgumentNullException(nameof(projectileOccupancy)));
            _tileFeaturesById = new ReadOnlyDictionary<int, TileFeatureState>(tileFeaturesById ?? throw new ArgumentNullException(nameof(tileFeaturesById)));
            _tileFeatureIdsByCell = tileFeatureIdsByCell ?? throw new ArgumentNullException(nameof(tileFeatureIdsByCell));
            _enemyActionStatesByEntityId = new ReadOnlyDictionary<int, EnemyActionRuntimeState>(enemyActionStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyActionStatesByEntityId)));
            _pendingCellImpactsById = new ReadOnlyDictionary<int, PendingCellImpact>(pendingCellImpactsById ?? throw new ArgumentNullException(nameof(pendingCellImpactsById)));
            _pendingEnemyBlockedReactionsByEntityId = new ReadOnlyDictionary<int, PendingEnemyBlockedReaction>(pendingEnemyBlockedReactionsByEntityId ?? throw new ArgumentNullException(nameof(pendingEnemyBlockedReactionsByEntityId)));
            _enemyPatrolStatesByEntityId = new ReadOnlyDictionary<int, EnemyPatrolRuntimeState>(enemyPatrolStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyPatrolStatesByEntityId)));
            _enemyChargeStatesByEntityId = new ReadOnlyDictionary<int, EnemyChargeRuntimeState>(enemyChargeStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyChargeStatesByEntityId)));
            _executionLockStatesByEntityId = new ReadOnlyDictionary<int, EntityExecutionLockState>(executionLockStatesByEntityId ?? throw new ArgumentNullException(nameof(executionLockStatesByEntityId)));
            _enemyJumpStatesByEntityId = new ReadOnlyDictionary<int, EnemyJumpRuntimeState>(enemyJumpStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyJumpStatesByEntityId)));
            _enemyGlideStatesByEntityId = new ReadOnlyDictionary<int, EnemyGlideRuntimeState>(enemyGlideStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyGlideStatesByEntityId)));
            _enemyUtilityStatesByEntityId = new ReadOnlyDictionary<int, EnemyUtilityRuntimeState>(enemyUtilityStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyUtilityStatesByEntityId)));
            _enemyFrontFaceSupportStatesByEntityId = new ReadOnlyDictionary<int, EnemyFrontFaceSupportRuntimeState>(enemyFrontFaceSupportStatesByEntityId ?? throw new ArgumentNullException(nameof(enemyFrontFaceSupportStatesByEntityId)));
            _boxInteractionLockStatesByEntityId = new ReadOnlyDictionary<int, BoxInteractionLockState>(boxInteractionLockStatesByEntityId ?? throw new ArgumentNullException(nameof(boxInteractionLockStatesByEntityId)));
            _enemyGravityFieldAuraFieldsById = new ReadOnlyDictionary<int, EnemyGravityFieldAuraFieldState>(enemyGravityFieldAuraFieldsById ?? throw new ArgumentNullException(nameof(enemyGravityFieldAuraFieldsById)));
            _phasedStatesByEntityId = new ReadOnlyDictionary<int, PhasedRuntimeState>(phasedStatesByEntityId ?? throw new ArgumentNullException(nameof(phasedStatesByEntityId)));
            _playerDamageStatesByEntityId = new ReadOnlyDictionary<int, PlayerDamageState>(playerDamageStatesByEntityId ?? throw new ArgumentNullException(nameof(playerDamageStatesByEntityId)));
            _playerControlStatesByEntityId = new ReadOnlyDictionary<int, PlayerControlState>(playerControlStatesByEntityId ?? throw new ArgumentNullException(nameof(playerControlStatesByEntityId)));
            _summonedEntitiesByEntityId = new ReadOnlyDictionary<int, SummonedEntityState>(summonedEntitiesByEntityId ?? throw new ArgumentNullException(nameof(summonedEntitiesByEntityId)));
            _enemyDefinitionBindingsByEntityId = new ReadOnlyDictionary<int, EnemyDefinitionBindingState>(enemyDefinitionBindingsByEntityId ?? throw new ArgumentNullException(nameof(enemyDefinitionBindingsByEntityId)));
            _unitKinematicStatesByEntityId = new ReadOnlyDictionary<int, UnitKinematicRuntimeState>(unitKinematicStatesByEntityId ?? throw new ArgumentNullException(nameof(unitKinematicStatesByEntityId)));
            _unitContinuousLocomotionStatesByEntityId = new ReadOnlyDictionary<int, UnitContinuousLocomotionState>(unitContinuousLocomotionStatesByEntityId ?? throw new ArgumentNullException(nameof(unitContinuousLocomotionStatesByEntityId)));
            _topology = topology;
            _topologyRevision = topologyRevision;
            _boardBounds = boardBounds;
            _terrainData = terrainData ?? throw new ArgumentNullException(nameof(terrainData));
        }

        public BoardBounds BoardBounds => _boardBounds;

        public CubeTopologyState Topology => _topology;

        public int TopologyRevision => _topologyRevision;

        internal TerrainData TerrainData => _terrainData;

        internal IReadOnlyDictionary<int, EntityState> EntitiesById => _entitiesById;

        internal int EntityCount => _entitiesById.Count;

        internal int TileFeatureCount => _tileFeaturesById.Count;

        internal void CopyEntitiesByIdTo(Dictionary<int, EntityState> target)
        {
            CopyDictionaryTo(_entitiesById, target);
        }

        internal void CopyStackedUnitsByCellTo(Dictionary<SurfaceCell, SortedSet<int>> target)
        {
            CopyCellCollectionTo(_stackedUnitsByCell, target);
        }

        internal void CopySolidOccupancyTo(Dictionary<SurfaceCell, int> target)
        {
            CopyDictionaryTo(_solidOccupancy, target);
        }

        internal void CopyProjectileOccupancyTo(Dictionary<SurfaceCell, int> target)
        {
            CopyDictionaryTo(_projectileOccupancy, target);
        }

        internal void CopyTileFeaturesByIdTo(Dictionary<int, TileFeatureState> target)
        {
            CopyDictionaryTo(_tileFeaturesById, target);
        }

        internal void CopyTileFeatureIdsByCellTo(Dictionary<SurfaceCell, SortedSet<int>> target)
        {
            CopyCellCollectionTo(_tileFeatureIdsByCell, target);
        }

        internal void CopyEnemyActionStatesByEntityIdTo(Dictionary<int, EnemyActionRuntimeState> target)
        {
            CopyDictionaryTo(_enemyActionStatesByEntityId, target);
        }

        internal void CopyPendingCellImpactsByIdTo(Dictionary<int, PendingCellImpact> target)
        {
            CopyDictionaryTo(_pendingCellImpactsById, target);
        }

        internal void CopyPendingEnemyBlockedReactionsByEntityIdTo(Dictionary<int, PendingEnemyBlockedReaction> target)
        {
            CopyDictionaryTo(_pendingEnemyBlockedReactionsByEntityId, target);
        }

        internal void CopyEnemyPatrolStatesByEntityIdTo(Dictionary<int, EnemyPatrolRuntimeState> target)
        {
            CopyDictionaryTo(_enemyPatrolStatesByEntityId, target);
        }

        internal void CopyEnemyChargeStatesByEntityIdTo(Dictionary<int, EnemyChargeRuntimeState> target)
        {
            CopyDictionaryTo(_enemyChargeStatesByEntityId, target);
        }

        internal void CopyEntityExecutionLockStatesByEntityIdTo(Dictionary<int, EntityExecutionLockState> target)
        {
            CopyDictionaryTo(_executionLockStatesByEntityId, target);
        }

        internal void CopyEnemyJumpStatesByEntityIdTo(Dictionary<int, EnemyJumpRuntimeState> target)
        {
            CopyDictionaryTo(_enemyJumpStatesByEntityId, target);
        }

        internal void CopyEnemyGlideStatesByEntityIdTo(Dictionary<int, EnemyGlideRuntimeState> target)
        {
            CopyDictionaryTo(_enemyGlideStatesByEntityId, target);
        }

        internal void CopyEnemyUtilityStatesByEntityIdTo(Dictionary<int, EnemyUtilityRuntimeState> target)
        {
            CopyDictionaryTo(_enemyUtilityStatesByEntityId, target);
        }

        internal void CopyEnemyFrontFaceSupportStatesByEntityIdTo(Dictionary<int, EnemyFrontFaceSupportRuntimeState> target)
        {
            CopyDictionaryTo(_enemyFrontFaceSupportStatesByEntityId, target);
        }

        internal void CopyBoxInteractionLockStatesByEntityIdTo(Dictionary<int, BoxInteractionLockState> target)
        {
            CopyDictionaryTo(_boxInteractionLockStatesByEntityId, target);
        }

        internal void CopyEnemyGravityFieldAuraFieldsByIdTo(Dictionary<int, EnemyGravityFieldAuraFieldState> target)
        {
            CopyDictionaryTo(_enemyGravityFieldAuraFieldsById, target);
        }

        internal void CopyPhasedStatesByEntityIdTo(Dictionary<int, PhasedRuntimeState> target)
        {
            CopyDictionaryTo(_phasedStatesByEntityId, target);
        }

        internal void CopyPlayerDamageStatesByEntityIdTo(Dictionary<int, PlayerDamageState> target)
        {
            CopyDictionaryTo(_playerDamageStatesByEntityId, target);
        }

        internal void CopyPlayerControlStatesByEntityIdTo(Dictionary<int, PlayerControlState> target)
        {
            CopyDictionaryTo(_playerControlStatesByEntityId, target);
        }

        internal void CopySummonedEntitiesByEntityIdTo(Dictionary<int, SummonedEntityState> target)
        {
            CopyDictionaryTo(_summonedEntitiesByEntityId, target);
        }

        internal void CopyEnemyDefinitionBindingsByEntityIdTo(Dictionary<int, EnemyDefinitionBindingState> target)
        {
            CopyDictionaryTo(_enemyDefinitionBindingsByEntityId, target);
        }

        internal void CopyUnitKinematicStatesByEntityIdTo(Dictionary<int, UnitKinematicRuntimeState> target)
        {
            CopyDictionaryTo(_unitKinematicStatesByEntityId, target);
        }

        internal void CopyUnitContinuousLocomotionStatesByEntityIdTo(Dictionary<int, UnitContinuousLocomotionState> target)
        {
            CopyDictionaryTo(_unitContinuousLocomotionStatesByEntityId, target);
        }

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

        public bool TryGetPendingCellImpact(int impactId, out PendingCellImpact impact)
        {
            return _pendingCellImpactsById.TryGetValue(impactId, out impact);
        }

        public int CountPendingCellImpactsForOwner(int ownerId)
        {
            var count = 0;
            foreach (var pair in _pendingCellImpactsById)
            {
                if (pair.Value.OwnerId == ownerId)
                {
                    count++;
                }
            }

            return count;
        }

        internal bool TryGetPendingEnemyBlockedReaction(
            int entityId,
            out PendingEnemyBlockedReaction reaction)
        {
            return _pendingEnemyBlockedReactionsByEntityId.TryGetValue(entityId, out reaction);
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

        internal bool TryGetEnemyGravityFieldAuraFieldState(
            int fieldId,
            out EnemyGravityFieldAuraFieldState state)
        {
            return _enemyGravityFieldAuraFieldsById.TryGetValue(fieldId, out state);
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

        public void EnumerateUnitImpactTargetsAt(SurfaceCell cell, List<EntityState> buffer)
        {
            EnumerateUnitImpactTargetsAt(_topology, cell, buffer);
        }

        public void EnumerateUnitImpactTargetsAt(Vector2Int cell, List<EntityState> buffer)
        {
            EnumerateUnitImpactTargetsAt(CreateDefaultQueryCell(cell), buffer);
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

        internal void EnumerateUnitImpactTargetsAt(CubeTopologyState topology, SurfaceCell cell, List<EntityState> buffer)
        {
            SnapshotReadQueries.EnumerateUnitImpactTargetsAt(
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

        public bool TryGetBoxArchetypeAt(SurfaceCell cell, out BoxArchetype boxArchetype)
        {
            return TryGetBoxArchetypeAt(_topology, cell, out boxArchetype);
        }

        public bool TryGetBoxArchetypeAt(Vector2Int cell, out BoxArchetype boxArchetype)
        {
            return TryGetBoxArchetypeAt(CreateDefaultQueryCell(cell), out boxArchetype);
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

        public bool TryPickHostileUnitImpactTargetAtForBoxFlip(SurfaceCell cell, int sourceTeamId, out EntityState entity)
        {
            return TryPickHostileUnitImpactTargetAtForBoxFlip(_topology, cell, sourceTeamId, out entity);
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

        public bool TryGetTileFeature(int tileId, out TileFeatureState tileFeature)
        {
            return _tileFeaturesById.TryGetValue(tileId, out tileFeature);
        }

        public void EnumerateTileFeaturesAt(SurfaceCell cell, List<TileFeatureState> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            if (!_tileFeatureIdsByCell.TryGetValue(cell, out var tileIds))
            {
                return;
            }

            foreach (var tileId in tileIds)
            {
                if (_tileFeaturesById.TryGetValue(tileId, out var tileFeature))
                {
                    buffer.Add(tileFeature);
                }
            }
        }

        public void EnumerateTileFeaturesOrdered(List<TileFeatureState> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            var orderedTileFeatures = _orderedTileFeaturesCache;
            if (orderedTileFeatures == null)
            {
                SnapshotMaterializationDiagnostics.RecordOrderedTileFeaturesCacheMiss(_tileFeaturesById.Count);
                orderedTileFeatures = BuildOrderedTileFeaturesCache();
                _orderedTileFeaturesCache = orderedTileFeatures;
            }
            else
            {
                SnapshotMaterializationDiagnostics.RecordOrderedTileFeaturesCacheHit(orderedTileFeatures.Length);
            }

            AddRange(buffer, orderedTileFeatures);
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

        internal bool TryGetBoxFlipPlacementBlocker(
            CubeTopologyState topology,
            SurfaceCell cell,
            out SlideStopper blocker)
        {
            return WorldPlacementPolicy.TryGetBoxFlipPlacementBlocker(
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
                cell,
                out blocker);
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
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            var orderedEntities = _orderedEntitiesCache;
            if (orderedEntities == null)
            {
                SnapshotMaterializationDiagnostics.RecordOrderedEntitiesCacheMiss(_entitiesById.Count);
                orderedEntities = BuildOrderedEntitiesCache();
                _orderedEntitiesCache = orderedEntities;
            }
            else
            {
                SnapshotMaterializationDiagnostics.RecordOrderedEntitiesCacheHit(orderedEntities.Length);
            }

            AddRange(buffer, orderedEntities);
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

        internal void EnumeratePendingCellImpactsOrdered(List<PendingCellImpactSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _pendingCellImpactsById)
            {
                buffer.Add(new PendingCellImpactSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort(ComparePendingCellImpactEntries);
        }

        internal void EnumeratePendingEnemyBlockedReactionsOrdered(List<PendingEnemyBlockedReactionSnapshotEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            foreach (var pair in _pendingEnemyBlockedReactionsByEntityId)
            {
                buffer.Add(new PendingEnemyBlockedReactionSnapshotEntry(pair.Key, pair.Value));
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        private static int ComparePendingCellImpactEntries(
            PendingCellImpactSnapshotEntry left,
            PendingCellImpactSnapshotEntry right)
        {
            var result = left.Impact.ImpactTick.CompareTo(right.Impact.ImpactTick);
            if (result != 0)
            {
                return result;
            }

            result = left.Impact.OwnerId.CompareTo(right.Impact.OwnerId);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.Impact.TargetCell.face).CompareTo((int)right.Impact.TargetCell.face);
            if (result != 0)
            {
                return result;
            }

            result = left.Impact.TargetCell.x.CompareTo(right.Impact.TargetCell.x);
            if (result != 0)
            {
                return result;
            }

            result = left.Impact.TargetCell.y.CompareTo(right.Impact.TargetCell.y);
            if (result != 0)
            {
                return result;
            }

            return left.ImpactId.CompareTo(right.ImpactId);
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

        internal bool TryGetBoxArchetypeAt(CubeTopologyState topology, SurfaceCell cell, out BoxArchetype boxArchetype)
        {
            if (TryGetSolidSemanticAt(topology, cell, out var semantic) &&
                semantic.Kind == SolidKind.Box)
            {
                boxArchetype = semantic.Entity.boxArchetype;
                return true;
            }

            boxArchetype = BoxArchetype.Normal;
            return false;
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

        internal bool TryPickHostileUnitImpactTargetAtForBoxFlip(
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

        private EntityState[] BuildOrderedEntitiesCache()
        {
            var ordered = new EntityState[_entitiesById.Count];
            var index = 0;
            foreach (var entity in _entitiesById.Values)
            {
                ordered[index++] = entity;
            }

            Array.Sort(ordered, CompareEntityById);
            SnapshotMaterializationDiagnostics.RecordOrderedEntitiesSort(ordered.Length);
            return ordered;
        }

        private TileFeatureState[] BuildOrderedTileFeaturesCache()
        {
            var ordered = new TileFeatureState[_tileFeaturesById.Count];
            var index = 0;
            foreach (var tileFeature in _tileFeaturesById.Values)
            {
                ordered[index++] = tileFeature;
            }

            Array.Sort(ordered, CompareTileFeatureByCellThenId);
            SnapshotMaterializationDiagnostics.RecordOrderedTileFeaturesSort(ordered.Length);
            return ordered;
        }

        private static void AddRange<T>(List<T> buffer, T[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                buffer.Add(values[i]);
            }
        }

        private static void CopyDictionaryTo<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> source,
            Dictionary<TKey, TValue> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Clear();
            foreach (var pair in source)
            {
                target.Add(pair.Key, pair.Value);
            }
        }

        private static void CopyCellCollectionTo(
            IReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> source,
            Dictionary<SurfaceCell, SortedSet<int>> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Clear();
            foreach (var pair in source)
            {
                target.Add(pair.Key, new SortedSet<int>(pair.Value));
            }
        }

        private static int CompareEntityById(EntityState left, EntityState right)
        {
            return left.entityId.CompareTo(right.entityId);
        }

        private static int CompareTileFeatureByCellThenId(TileFeatureState left, TileFeatureState right)
        {
            var faceComparison = ((int)left.Cell.face).CompareTo((int)right.Cell.face);
            if (faceComparison != 0)
            {
                return faceComparison;
            }

            var xComparison = left.Cell.x.CompareTo(right.Cell.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            var yComparison = left.Cell.y.CompareTo(right.Cell.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return left.TileId.CompareTo(right.TileId);
        }

        private static ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> CreateReadonlyTileFeatureIdsByCell(
            Dictionary<SurfaceCell, SortedSet<int>> tileFeatureIdsByCell)
        {
            var buffer = new Dictionary<SurfaceCell, IReadOnlyCollection<int>>(tileFeatureIdsByCell.Count);

            foreach (var pair in tileFeatureIdsByCell)
            {
                if (pair.Value == null)
                {
                    throw new ArgumentNullException(nameof(tileFeatureIdsByCell));
                }

                var orderedTileIds = new List<int>(pair.Value.Count);
                foreach (var tileId in pair.Value)
                {
                    orderedTileIds.Add(tileId);
                }

                buffer.Add(pair.Key, orderedTileIds.AsReadOnly());
            }

            return new ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>>(buffer);
        }

        private static ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>> CreateReadonlySnapshotOwnedCellIndex(
            SnapshotOwnedCellIndex<SurfaceCell> cellIndex)
        {
            var values = cellIndex.Values;
            foreach (var pair in values)
            {
                if (pair.Value == null)
                {
                    throw new ArgumentNullException(nameof(cellIndex));
                }
            }

            SnapshotMaterializationDiagnostics.RecordSnapshotReadonlyCellIndexSecondCopySkipped(values.Count);
            return new ReadOnlyDictionary<SurfaceCell, IReadOnlyCollection<int>>(values);
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
