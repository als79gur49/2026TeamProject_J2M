using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class ProjectedWorld
    {
        private readonly WorldSnapshot _baseSnapshot;
        private readonly FinalizationBatch _overlayBatch = new();
        private readonly List<TileFeatureOperation> _overlayTileFeatureOperations = new();
        private Dictionary<int, TileFeatureState> _projectedTileFeaturesById;
        private int _overlayTileFeatureOperationCount;
        private bool _isDirty = true;
        private WorldSnapshot _materializedSnapshot;

        public ProjectedWorld(WorldSnapshot baseSnapshot)
            : this(baseSnapshot, seedBaseSnapshot: false)
        {
        }

        public ProjectedWorld(WorldSnapshot baseSnapshot, bool seedBaseSnapshot)
        {
            _baseSnapshot = baseSnapshot ?? throw new ArgumentNullException(nameof(baseSnapshot));
            if (seedBaseSnapshot)
            {
                _materializedSnapshot = _baseSnapshot;
                _isDirty = false;
            }
        }

        public void ApplyBatch(FinalizationBatch batch)
        {
            ApplyBatch(batch, ProjectedWorldBatchReason.Unspecified);
        }

        public void ApplyBatch(FinalizationBatch batch, ProjectedWorldBatchReason reason)
        {
            var resolvedBatch = batch ?? throw new ArgumentNullException(nameof(batch));
            var isEmpty =
                resolvedBatch.Operations.Count == 0 &&
                resolvedBatch.TileFeatureOperations.Count == 0;
            SnapshotMaterializationDiagnostics.RecordProjectedWorldApplyBatch(isEmpty, reason);
            if (isEmpty)
            {
                return;
            }

            if (resolvedBatch.TileFeatureOperations.Count > 0)
            {
                ApplyTileFeatureOperations(new TileFeatureOperationBatch(resolvedBatch.TileFeatureOperations), reason);
            }

            _overlayBatch.MergeFrom(resolvedBatch, includeTileFeatureOperations: false);
            _isDirty = true;
        }

        public void ApplyTileFeatureOperations(TileFeatureOperationBatch batch)
        {
            ApplyTileFeatureOperations(batch, ProjectedWorldBatchReason.Unspecified);
        }

        public void ApplyTileFeatureOperations(TileFeatureOperationBatch batch, ProjectedWorldBatchReason reason)
        {
            var resolvedBatch = batch ?? throw new ArgumentNullException(nameof(batch));
            if (resolvedBatch.IsEmpty)
            {
                return;
            }

            EnsureProjectedTileFeatureMap();
            ValidateTileFeatureOperations(resolvedBatch);

            var operations = resolvedBatch.Operations;
            _overlayTileFeatureOperationCount += operations.Count;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                _overlayTileFeatureOperations.Add(operation);
                switch (operation.Kind)
                {
                    case TileFeatureOperationKind.Add:
                    case TileFeatureOperationKind.Update:
                        _projectedTileFeaturesById[operation.TileId] = operation.State;
                        break;

                    case TileFeatureOperationKind.Remove:
                        _projectedTileFeaturesById.Remove(operation.TileId);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            _isDirty = true;
        }

        public WorldSnapshot CreateSnapshot()
        {
            return CreateSnapshot(ProjectedWorldSnapshotReason.Unspecified);
        }

        public WorldSnapshot CreateSnapshot(ProjectedWorldSnapshotReason reason)
        {
            if (!_isDirty && _materializedSnapshot != null)
            {
                SnapshotMaterializationDiagnostics.RecordProjectedWorldCacheHit(reason);
                return _materializedSnapshot;
            }

            var baseEntityCount = _baseSnapshot.EntityCount;
            var overlayEntityOperationCount = _overlayBatch.Operations.Count;
            var overlayTileFeatureOperationCount = _overlayTileFeatureOperationCount;
#if VECTORQUAKE_CAPTURE_BUILD
            var captureResolveInitialSnapshot =
                GameplaySimulationAttributionCapture.IsActive &&
                reason == ProjectedWorldSnapshotReason.ResolveInitialPostMovement;
            var baseImportStartedAt = captureResolveInitialSnapshot
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0L;
#endif
            var projectedWorldState = WorldState.CreateFromSnapshotFast(_baseSnapshot);
#if VECTORQUAKE_CAPTURE_BUILD
            var overlayApplyStartedAt = captureResolveInitialSnapshot
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0L;
#endif
            var writeContext = projectedWorldState.CreateWriteContext();
            ApplyOverlayTileFeatureOperations(writeContext);
            _overlayBatch.ApplyTo(writeContext, delayedAttackEffectSink: null);
            SnapshotMaterializationDiagnostics.RecordFastImportOverlayApply(
                overlayEntityOperationCount,
                overlayTileFeatureOperationCount);
#if VECTORQUAKE_CAPTURE_BUILD
            var materializationStartedAt = captureResolveInitialSnapshot
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0L;
#endif
            _materializedSnapshot = SnapshotBuilder.Create(projectedWorldState);
#if VECTORQUAKE_CAPTURE_BUILD
            if (captureResolveInitialSnapshot)
            {
                var completedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                GameplaySimulationAttributionCapture.RecordResolveInitialPostMovementSnapshot(
                    overlayApplyStartedAt - baseImportStartedAt,
                    materializationStartedAt - overlayApplyStartedAt,
                    completedAt - materializationStartedAt);
            }
#endif
            SnapshotMaterializationDiagnostics.RecordProjectedWorldMaterializedSnapshot(
                reason,
                baseEntityCount,
                overlayEntityOperationCount,
                overlayTileFeatureOperationCount,
                _materializedSnapshot.EntityCount);
            _isDirty = false;
            return _materializedSnapshot;
        }

        private void ApplyOverlayTileFeatureOperations(IWorldWriteContext writeContext)
        {
            for (var i = 0; i < _overlayTileFeatureOperations.Count; i++)
            {
                var operation = _overlayTileFeatureOperations[i];
                switch (operation.Kind)
                {
                    case TileFeatureOperationKind.Add:
                        writeContext.AddTileFeature(operation.State);
                        break;

                    case TileFeatureOperationKind.Update:
                        writeContext.UpdateTileFeature(operation.State);
                        break;

                    case TileFeatureOperationKind.Remove:
                        writeContext.RemoveTileFeature(operation.TileId);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void EnsureProjectedTileFeatureMap()
        {
            if (_projectedTileFeaturesById != null)
            {
                return;
            }

            var tileFeatures = new List<TileFeatureState>();
            _baseSnapshot.EnumerateTileFeaturesOrdered(tileFeatures);
            _projectedTileFeaturesById = new Dictionary<int, TileFeatureState>(tileFeatures.Count);
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                _projectedTileFeaturesById.Add(tileFeatures[i].TileId, tileFeatures[i]);
            }
        }

        private void ValidateTileFeatureOperations(TileFeatureOperationBatch batch)
        {
            var seenTileIds = new HashSet<int>();
            var operations = batch.Operations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.TileId <= 0)
                {
                    throw new InvalidOperationException(
                        $"TileFeature operation TileId {operation.TileId} must be positive.");
                }

                if (!seenTileIds.Add(operation.TileId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate TileFeature operation for TileId {operation.TileId} detected in the same batch.");
                }

                switch (operation.Kind)
                {
                    case TileFeatureOperationKind.Add:
                        ValidateStateTileIdMatchesOperation(operation);
                        ValidateTileFeatureCellInBounds(operation.State);
                        if (_projectedTileFeaturesById.ContainsKey(operation.TileId))
                        {
                            throw new InvalidOperationException(
                                $"Cannot add existing TileFeature id {operation.TileId}.");
                        }
                        break;

                    case TileFeatureOperationKind.Update:
                        ValidateStateTileIdMatchesOperation(operation);
                        ValidateTileFeatureCellInBounds(operation.State);
                        if (!_projectedTileFeaturesById.ContainsKey(operation.TileId))
                        {
                            throw new InvalidOperationException(
                                $"Cannot update missing TileFeature id {operation.TileId}.");
                        }
                        break;

                    case TileFeatureOperationKind.Remove:
                        if (!_projectedTileFeaturesById.ContainsKey(operation.TileId))
                        {
                            throw new InvalidOperationException(
                                $"Cannot remove missing TileFeature id {operation.TileId}.");
                        }
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unsupported TileFeature operation kind {operation.Kind}.");
                }
            }
        }

        private void ValidateStateTileIdMatchesOperation(TileFeatureOperation operation)
        {
            if (operation.TileId == operation.State.TileId)
            {
                return;
            }

            throw new InvalidOperationException(
                $"TileFeature operation TileId {operation.TileId} must match state TileId {operation.State.TileId}.");
        }

        private void ValidateTileFeatureCellInBounds(TileFeatureState state)
        {
            if (!_baseSnapshot.BoardBounds.IsBounded ||
                _baseSnapshot.BoardBounds.Contains(state.Cell.PlanarPosition))
            {
                return;
            }

            throw new InvalidOperationException(
                $"TileFeature {state.TileId} at {state.Cell} is outside the configured board bounds.");
        }

        internal static WorldState MaterializeWorldStateSlowForTest(WorldSnapshot snapshot)
        {
            return MaterializeWorldStateSlow(snapshot, projectedTileFeaturesById: null);
        }

        private static WorldState MaterializeWorldStateSlow(
            WorldSnapshot snapshot,
            IReadOnlyDictionary<int, TileFeatureState> projectedTileFeaturesById)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            entities.Sort(CompareProjectedMaterializationOrder);
            var enemyGlideStates = new List<EnemyGlideSnapshotEntry>();
            snapshot.EnumerateEnemyGlideStatesOrdered(enemyGlideStates);
            var enemyGlideStatesByEntityId = new Dictionary<int, EnemyGlideRuntimeState>(enemyGlideStates.Count);
            for (var i = 0; i < enemyGlideStates.Count; i++)
            {
                enemyGlideStatesByEntityId[enemyGlideStates[i].EntityId] = enemyGlideStates[i].State;
            }

            var tileFeatures = new List<TileFeatureState>();
            if (projectedTileFeaturesById == null)
            {
                snapshot.EnumerateTileFeaturesOrdered(tileFeatures);
            }
            else
            {
                foreach (var tileFeature in projectedTileFeaturesById.Values)
                {
                    tileFeatures.Add(tileFeature);
                }
            }

            SnapshotMaterializationDiagnostics.RecordSlowBaseSnapshotImport(
                snapshot.EntityCount,
                tileFeatures.Count);
            var worldState = new WorldState(
                entities,
                snapshot.BoardBounds,
                snapshot.Topology,
                tileFeatures,
                enemyGlideStatesByEntityId,
                snapshot.TopologyRevision);
            var writeContext = worldState.CreateWriteContext();

            for (var i = 0; i < entities.Count; i++)
            {
                var entityId = entities[i].entityId;
                if (snapshot.TryGetPlayerControlState(entityId, out var playerControlState))
                {
                    writeContext.SetPlayerControlState(entityId, playerControlState);
                }

                if (snapshot.TryGetPlayerDamageState(entityId, out var playerDamageState))
                {
                    writeContext.SetPlayerDamageState(entityId, playerDamageState);
                }

                if (snapshot.TryGetEnemyActionState(entityId, out var enemyActionState))
                {
                    writeContext.SetEnemyActionState(entityId, enemyActionState);
                }

                if (snapshot.TryGetEnemyPatrolState(entityId, out var enemyPatrolState))
                {
                    writeContext.SetEnemyPatrolState(entityId, enemyPatrolState);
                }

                if (snapshot.TryGetEntityExecutionLockState(entityId, out var executionLockState))
                {
                    writeContext.SetEntityExecutionLockState(entityId, executionLockState);
                }

                if (snapshot.TryGetEnemyJumpState(entityId, out var enemyJumpState))
                {
                    writeContext.SetEnemyJumpState(entityId, enemyJumpState);
                }

                if (snapshot.TryGetEnemyGlideState(entityId, out var enemyGlideState))
                {
                    writeContext.SetEnemyGlideState(entityId, enemyGlideState);
                }

                if (snapshot.TryGetEnemyUtilityState(entityId, out var enemyUtilityState))
                {
                    writeContext.SetEnemyUtilityState(entityId, enemyUtilityState);
                }

                if (snapshot.TryGetEnemySummonBehaviorState(entityId, out var enemySummonBehaviorState))
                {
                    writeContext.SetEnemySummonBehaviorState(entityId, enemySummonBehaviorState);
                }

                if (snapshot.TryGetBoxInteractionLockState(entityId, out var boxInteractionLockState))
                {
                    writeContext.SetBoxInteractionLockState(entityId, boxInteractionLockState);
                }

                if (snapshot.TryGetUnitKinematicState(entityId, out var unitKinematicState))
                {
                    writeContext.SetUnitKinematicState(entityId, unitKinematicState);
                }

                if (snapshot.TryGetUnitContinuousLocomotionState(entityId, out var unitContinuousLocomotionState))
                {
                    writeContext.SetUnitContinuousLocomotionState(entityId, unitContinuousLocomotionState);
                }

                if (snapshot.TryGetEnemyChargeState(entityId, out var enemyChargeState))
                {
                    writeContext.SetEnemyChargeState(entityId, enemyChargeState);
                }

                if (snapshot.TryGetPhasedState(entityId, out var phasedState))
                {
                    writeContext.SetPhasedState(entityId, phasedState);
                }

                if (snapshot.TryGetSummonedEntityState(entityId, out var summonedEntityState))
                {
                    writeContext.SetSummonedEntityState(entityId, summonedEntityState);
                }

                if (snapshot.TryGetEnemyDefinitionBindingState(entityId, out var enemyDefinitionBindingState))
                {
                    writeContext.SetEnemyDefinitionBindingState(entityId, enemyDefinitionBindingState);
                }
            }

            var enemyGravityFieldAuraFields = new List<EnemyGravityFieldAuraFieldSnapshotEntry>();
            snapshot.EnumerateEnemyGravityFieldAuraFieldStatesOrdered(enemyGravityFieldAuraFields);
            for (var i = 0; i < enemyGravityFieldAuraFields.Count; i++)
            {
                writeContext.SetEnemyGravityFieldAuraFieldState(
                    enemyGravityFieldAuraFields[i].FieldId,
                    enemyGravityFieldAuraFields[i].State);
            }

            var pendingCellImpacts = new List<PendingCellImpactSnapshotEntry>();
            snapshot.EnumeratePendingCellImpactsOrdered(pendingCellImpacts);
            for (var i = 0; i < pendingCellImpacts.Count; i++)
            {
                writeContext.AddPendingCellImpact(pendingCellImpacts[i].Impact);
            }

            return worldState;
        }

        private static int CompareProjectedMaterializationOrder(EntityState left, EntityState right)
        {
            var leftPriority = ResolveProjectedMaterializationPriority(left);
            var rightPriority = ResolveProjectedMaterializationPriority(right);
            if (leftPriority != rightPriority)
            {
                return leftPriority.CompareTo(rightPriority);
            }

            return left.entityId.CompareTo(right.entityId);
        }

        private static int ResolveProjectedMaterializationPriority(EntityState entity)
        {
            if (entity.boardPresence != EntityBoardPresence.Occupying)
            {
                return 2;
            }

            return entity.type == EntityType.Unit ? 2 : 1;
        }
    }
}
