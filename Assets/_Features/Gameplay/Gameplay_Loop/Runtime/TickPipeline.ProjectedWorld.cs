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
        private bool _isDirty = true;
        private WorldSnapshot _materializedSnapshot;

        public ProjectedWorld(WorldSnapshot baseSnapshot)
        {
            _baseSnapshot = baseSnapshot ?? throw new ArgumentNullException(nameof(baseSnapshot));
        }

        public void ApplyBatch(FinalizationBatch batch)
        {
            var resolvedBatch = batch ?? throw new ArgumentNullException(nameof(batch));
            SnapshotMaterializationDiagnostics.RecordProjectedWorldApplyBatch(resolvedBatch.Operations.Count == 0);
            _overlayBatch.MergeFrom(resolvedBatch);
            _isDirty = true;
        }

        public WorldSnapshot CreateSnapshot()
        {
            if (!_isDirty && _materializedSnapshot != null)
            {
                SnapshotMaterializationDiagnostics.RecordProjectedWorldCacheHit();
                return _materializedSnapshot;
            }

            SnapshotMaterializationDiagnostics.RecordProjectedWorldMaterializedSnapshot();
            var projectedWorldState = MaterializeWorldState(_baseSnapshot);
            _overlayBatch.ApplyTo(projectedWorldState.CreateWriteContext(), delayedAttackEffectSink: null);
            _materializedSnapshot = SnapshotBuilder.Create(projectedWorldState);
            _isDirty = false;
            return _materializedSnapshot;
        }

        private static WorldState MaterializeWorldState(WorldSnapshot snapshot)
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
            snapshot.EnumerateTileFeaturesOrdered(tileFeatures);

            var worldState = new WorldState(
                entities,
                snapshot.BoardBounds,
                snapshot.TerrainData,
                snapshot.Topology,
                tileFeatures,
                enemyGlideStatesByEntityId);
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

                if (snapshot.TryGetEnemyFrontFaceSupportState(entityId, out var enemyFrontFaceSupportState))
                {
                    writeContext.SetEnemyFrontFaceSupportState(entityId, enemyFrontFaceSupportState);
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
            // Projection rehydrates already-authoritative snapshots. Occupying projectiles
            // must materialize ahead of solids so box/projectile overlap states that are
            // legal in the live world can be reconstructed without relaxing placement
            // invariants for normal world writes.
            if (entity.boardPresence != EntityBoardPresence.Occupying)
            {
                return 2;
            }

            if (entity.type == EntityType.Projectile)
            {
                return 0;
            }

            return entity.type == EntityType.Unit ? 2 : 1;
        }
    }
}
