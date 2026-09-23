using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class ProjectedWorldFastImportCoreTests
    {
        private static readonly BoardBounds TestBounds = new(
            Vector2Int.zero,
            new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void WorldState_FastImport_ExplicitWallPreservesSolidAndPendingReactionFieldsInOrder()
        {
            var wallCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = GameplayCompositionRoot.CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), UnitRole.Enemy, teamId: 2),
                    CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), UnitRole.Enemy, teamId: 2),
                    CreateWall(40, wallCell),
                },
                TestBounds,
                new CubeTopologyState(FaceId.Floor));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPendingEnemyBlockedReaction(20, CreateWallReaction(20, 40, wallCell, createdTick: 7));
            writeContext.SetPendingEnemyBlockedReaction(10, CreateWallReaction(10, 40, wallCell, createdTick: 5));
            var before = worldState.CreateSnapshot();

            var after = WorldState.CreateFromSnapshotFast(before).CreateSnapshot();

            Assert.That(after.TryGetEntity(40, out var wall), Is.True);
            Assert.That(wall.type, Is.EqualTo(EntityType.Wall));
            Assert.That(after.TryGetSolidOccupantAt(wallCell, out var solid), Is.True);
            Assert.That(solid.entityId, Is.EqualTo(40));
            Assert.That(after.TryGetSolidSemanticAt(wallCell, out var semantic), Is.True);
            Assert.That(semantic.Kind, Is.EqualTo(SolidKind.Wall));
            var beforeReactions = Collect<PendingEnemyBlockedReactionSnapshotEntry>(before.EnumeratePendingEnemyBlockedReactionsOrdered);
            var afterReactions = Collect<PendingEnemyBlockedReactionSnapshotEntry>(after.EnumeratePendingEnemyBlockedReactionsOrdered);
            CollectionAssert.AreEqual(new[] { 10, 20 }, afterReactions.Select(entry => entry.EntityId).ToArray());
            Assert.That(afterReactions, Has.Count.EqualTo(beforeReactions.Count));
            for (var i = 0; i < beforeReactions.Count; i++)
            {
                AssertReactionEqual(beforeReactions[i], afterReactions[i]);
            }
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_ProducesSameSnapshotAsSlowImport()
        {
            var baseSnapshot = CreateRichSnapshot();

            var slowSnapshot = SnapshotBuilder.Create(ProjectedWorld.MaterializeWorldStateSlowForTest(baseSnapshot));
            var fastSnapshot = SnapshotBuilder.Create(WorldState.CreateFromSnapshotFast(baseSnapshot));

            AssertSnapshotsEquivalent(slowSnapshot, fastSnapshot);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_PreservesNonEmptyCleanupCandidateIndexes()
        {
            var timerEntity = CreateUnit(
                10,
                new SurfaceCell(FaceId.Floor, 0, 0),
                UnitRole.Enemy,
                teamId: 2,
                boardPresence: EntityBoardPresence.Detached);
            timerEntity.state = EntityPhaseState.Cooldown;
            timerEntity.stateTimer = 2;
            var removalAndTransitionEntity = CreateUnit(
                20,
                new SurfaceCell(FaceId.Floor, 0, 0),
                UnitRole.Enemy,
                teamId: 2,
                boardPresence: EntityBoardPresence.Detached);
            removalAndTransitionEntity.hp = 0;
            removalAndTransitionEntity.state = EntityPhaseState.Acting;

            var baseWorld = GameplayCompositionRoot.CreateWorldState(
                new[] { removalAndTransitionEntity, timerEntity },
                TestBounds,
                new CubeTopologyState(FaceId.Floor));
            var baseSnapshot = baseWorld.CreateSnapshot();

            var slowSnapshot = SnapshotBuilder.Create(ProjectedWorld.MaterializeWorldStateSlowForTest(baseSnapshot));
            var fastSnapshot = SnapshotBuilder.Create(WorldState.CreateFromSnapshotFast(baseSnapshot));

            AssertSnapshotsEquivalent(slowSnapshot, fastSnapshot);
            CollectionAssert.AreEqual(new[] { 20 }, fastSnapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 10 }, fastSnapshot.CleanupTimerCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 20 }, fastSnapshot.CleanupImmediateTransitionCandidateIds.ToArray());
        }

        [Test]
        [Category("Core")]
        public void WorldState_FastImport_PreservesOccupancyAndPlacementQueries()
        {
            var baseSnapshot = CreateRichSnapshot();
            var slowSnapshot = SnapshotBuilder.Create(ProjectedWorld.MaterializeWorldStateSlowForTest(baseSnapshot));
            var fastSnapshot = SnapshotBuilder.Create(WorldState.CreateFromSnapshotFast(baseSnapshot));
            var unitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var boxCell = new SurfaceCell(FaceId.Floor, 2, 1);

            Assert.That(fastSnapshot.HasAnyUnitAt(unitCell), Is.EqualTo(slowSnapshot.HasAnyUnitAt(unitCell)));
            Assert.That(fastSnapshot.TryGetPrimaryUnitAt(unitCell, out var fastUnit), Is.EqualTo(slowSnapshot.TryGetPrimaryUnitAt(unitCell, out var slowUnit)));
            Assert.That(fastUnit.entityId, Is.EqualTo(slowUnit.entityId));
            Assert.That(fastSnapshot.TryGetSolidOccupantAt(boxCell, out var fastBox), Is.EqualTo(slowSnapshot.TryGetSolidOccupantAt(boxCell, out var slowBox)));
            Assert.That(fastBox.entityId, Is.EqualTo(slowBox.entityId));
            Assert.That(
                fastSnapshot.TryGetPlacementBlocker(EntityType.Box, unitCell, ignoredEntityId: 0, out var fastBlocker),
                Is.EqualTo(slowSnapshot.TryGetPlacementBlocker(EntityType.Box, unitCell, ignoredEntityId: 0, out var slowBlocker)));
            Assert.That(fastBlocker.Kind, Is.EqualTo(slowBlocker.Kind));
            Assert.That(fastBlocker.EntityId, Is.EqualTo(slowBlocker.EntityId));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_PreservesStackedUnitsTileFeaturesAndOrderedEnumeration()
        {
            var baseSnapshot = CreateRichSnapshot();
            var slowSnapshot = SnapshotBuilder.Create(ProjectedWorld.MaterializeWorldStateSlowForTest(baseSnapshot));
            var fastSnapshot = SnapshotBuilder.Create(WorldState.CreateFromSnapshotFast(baseSnapshot));
            var stackedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var tileFeatureCell = new SurfaceCell(FaceId.Floor, 0, 1);

            CollectionAssert.AreEqual(
                CollectUnitOccupancyIdsAt(slowSnapshot, stackedCell),
                CollectUnitOccupancyIdsAt(fastSnapshot, stackedCell));
            CollectionAssert.AreEqual(
                CollectTileFeatureIdsAt(slowSnapshot, tileFeatureCell),
                CollectTileFeatureIdsAt(fastSnapshot, tileFeatureCell));
            CollectionAssert.AreEqual(
                Collect<EntityState>(slowSnapshot.EnumerateEntitiesOrdered).Select(entity => entity.entityId).ToArray(),
                Collect<EntityState>(fastSnapshot.EnumerateEntitiesOrdered).Select(entity => entity.entityId).ToArray());
            CollectionAssert.AreEqual(
                Collect<TileFeatureState>(slowSnapshot.EnumerateTileFeaturesOrdered).Select(tile => tile.TileId).ToArray(),
                Collect<TileFeatureState>(fastSnapshot.EnumerateTileFeaturesOrdered).Select(tile => tile.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_DoesNotUseSlowBaseSpawnPath()
        {
            var baseSnapshot = CreateRichSnapshot();
            var projectedWorld = new ProjectedWorld(baseSnapshot);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanPostPreMovement);
                counts = capture.Counts;
            }

            Assert.That(counts.FastBaseSnapshotImportCount, Is.EqualTo(1));
            Assert.That(counts.SlowBaseSnapshotImportCount, Is.EqualTo(0));
            Assert.That(counts.FastImportedEntityCount, Is.EqualTo(baseSnapshot.EntityCount));
            Assert.That(counts.FastImportedTileFeatureCount, Is.EqualTo(baseSnapshot.TileFeatureCount));
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.WorldStateSnapshotCacheHitCount, Is.Zero);
            Assert.That(counts.WorldStateSnapshotMaterializationCount, Is.EqualTo(1));
            Assert.That(counts.WorldStateSnapshotRequestAccountingIsBalanced, Is.True);
            Assert.That(counts.SnapshotOwnedTileFeatureCellIndexBuildCount, Is.EqualTo(1));
            Assert.That(counts.SnapshotOwnedStackedUnitCellIndexBuildCount, Is.Zero);
            Assert.That(counts.SnapshotReadonlyCellIndexSecondCopySkippedCount, Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_ReusesUnitCellIndexUntilOccupancyMembershipChanges()
        {
            var baseSnapshot = CreateRichSnapshot();
            var projectedWorld = new ProjectedWorld(baseSnapshot);
            var firstAuxiliaryBatch = new FinalizationBatch();
            firstAuxiliaryBatch.ApplyDamage(10, amount: 1);
            projectedWorld.ApplyBatch(firstAuxiliaryBatch);

            WorldSnapshot movedSnapshot;
            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanPostPreMovement);

                var secondAuxiliaryBatch = new FinalizationBatch();
                secondAuxiliaryBatch.SetFacing(20, Direction.Left);
                projectedWorld.ApplyBatch(secondAuxiliaryBatch);
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolveEnemyActionBeforeAttackInput);

                var occupancyBatch = new FinalizationBatch();
                occupancyBatch.MoveEntity(30, new SurfaceCell(FaceId.Floor, 0, 3));
                projectedWorld.ApplyBatch(occupancyBatch);
                movedSnapshot = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.ResolvePostAttack);
                counts = capture.Counts;
            }

            CollectionAssert.IsEmpty(
                CollectUnitOccupancyIdsAt(movedSnapshot, new SurfaceCell(FaceId.Floor, 0, 2)));
            CollectionAssert.AreEqual(
                new[] { 30 },
                CollectUnitOccupancyIdsAt(movedSnapshot, new SurfaceCell(FaceId.Floor, 0, 3)));
            Assert.That(counts.FastBaseSnapshotImportCount, Is.EqualTo(3));
            Assert.That(counts.SlowBaseSnapshotImportCount, Is.Zero);
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(3));
            Assert.That(counts.WorldStateSnapshotMaterializationCount, Is.EqualTo(3));
            Assert.That(counts.SnapshotOwnedStackedUnitCellIndexBuildCount, Is.EqualTo(1));
            Assert.That(counts.SnapshotStackedUnitCellIndexCellCount, Is.EqualTo(2));
            Assert.That(
                counts.WorldStateSnapshotMaterializationCount -
                counts.SnapshotOwnedStackedUnitCellIndexBuildCount,
                Is.EqualTo(2),
                "the two auxiliary-only fast imports should inherit the immutable Unit cell index");
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_ReusesOrderedEntityIdsUntilMembershipChanges()
        {
            var baseSnapshot = CreateRichSnapshot();
            var sourceCarrier = baseSnapshot.SnapshotOwnedOrderedEntityIds;
            var projectedWorld = new ProjectedWorld(baseSnapshot);
            var stateOnlyBatch = new FinalizationBatch();
            stateOnlyBatch.ApplyDamage(10, amount: 1);
            stateOnlyBatch.SetFacing(20, Direction.Left);
            stateOnlyBatch.MoveEntity(30, new SurfaceCell(FaceId.Floor, 0, 3));
            projectedWorld.ApplyBatch(stateOnlyBatch);

            WorldSnapshot stateOnlySnapshot;
            WorldSnapshot spawnedSnapshot;
            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                stateOnlySnapshot = projectedWorld.CreateSnapshot(
                    ProjectedWorldSnapshotReason.PlanPostPreMovement);

                var membershipBatch = new FinalizationBatch();
                membershipBatch.SpawnEntity(CreateBox(90, new SurfaceCell(FaceId.Floor, 4, 4)));
                projectedWorld.ApplyBatch(membershipBatch);
                spawnedSnapshot = projectedWorld.CreateSnapshot(
                    ProjectedWorldSnapshotReason.ResolvePostAttack);

                stateOnlySnapshot.GetOrderedEntitiesForRead();
                spawnedSnapshot.GetOrderedEntitiesForRead();
                counts = capture.Counts;
            }

            Assert.That(stateOnlySnapshot.SnapshotOwnedOrderedEntityIds, Is.SameAs(sourceCarrier));
            Assert.That(spawnedSnapshot.SnapshotOwnedOrderedEntityIds, Is.Not.SameAs(sourceCarrier));
            Assert.That(baseSnapshot.TryGetEntity(90, out _), Is.False);
            Assert.That(spawnedSnapshot.TryGetEntity(90, out var spawned), Is.True);
            Assert.That(spawned.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 4)));
            Assert.That(baseSnapshot.TryGetEntity(10, out var sourcePlayer), Is.True);
            Assert.That(stateOnlySnapshot.TryGetEntity(10, out var damagedPlayer), Is.True);
            Assert.That(sourcePlayer.hp, Is.EqualTo(3));
            Assert.That(damagedPlayer.hp, Is.EqualTo(2));
            CollectionAssert.AreEqual(
                new[] { 10, 20, 30, 40, 60, 90 },
                spawnedSnapshot.GetOrderedEntitiesForRead().ToArray()
                    .Select(entity => entity.entityId)
                    .ToArray());
            Assert.That(counts.FastBaseSnapshotImportCount, Is.EqualTo(2));
            Assert.That(counts.WorldStateSnapshotMaterializationCount, Is.EqualTo(2));
            Assert.That(counts.OrderedEntitiesCacheMissCount, Is.EqualTo(2));
            Assert.That(counts.OrderedEntitiesSortCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_OverlayEntityOperationStillValidates()
        {
            var baseSnapshot = CreateRichSnapshot();
            var invalidProjectedWorld = new ProjectedWorld(baseSnapshot);
            var invalidBatch = new FinalizationBatch();
            invalidBatch.SpawnEntity(CreateBox(90, new SurfaceCell(FaceId.Floor, 8, 8)));
            invalidProjectedWorld.ApplyBatch(invalidBatch);

            Assert.Throws<InvalidOperationException>(() => invalidProjectedWorld.CreateSnapshot());

            var validProjectedWorld = new ProjectedWorld(baseSnapshot);
            var validBatch = new FinalizationBatch();
            validBatch.SpawnEntity(CreateBox(91, new SurfaceCell(FaceId.Floor, 4, 4)));
            validProjectedWorld.ApplyBatch(validBatch);

            var projectedSnapshot = validProjectedWorld.CreateSnapshot();

            Assert.That(projectedSnapshot.TryGetEntity(91, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 4)));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_OverlayEntityOperations_UpdateCleanupCandidateIndexes()
        {
            var immediateEntity = CreateUnit(30, new SurfaceCell(FaceId.Floor, 3, 1), UnitRole.Enemy, teamId: 2);
            immediateEntity.state = EntityPhaseState.Acting;
            var timerEntity = CreateUnit(20, new SurfaceCell(FaceId.Floor, 2, 1), UnitRole.Enemy, teamId: 2);
            timerEntity.state = EntityPhaseState.Cooldown;
            timerEntity.stateTimer = 2;
            var baseWorld = GameplayCompositionRoot.CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 1), UnitRole.Enemy, teamId: 2),
                    timerEntity,
                    immediateEntity,
                },
                TestBounds,
                new CubeTopologyState(FaceId.Floor));
            var projectedWorld = new ProjectedWorld(baseWorld.CreateSnapshot());
            var spawnedTimerEntity = CreateUnit(
                40,
                new SurfaceCell(FaceId.Floor, 4, 1),
                UnitRole.Enemy,
                teamId: 2);
            spawnedTimerEntity.state = EntityPhaseState.Sliding;
            spawnedTimerEntity.stateTimer = 3;
            var batch = new FinalizationBatch();
            batch.ApplyDamage(10, amount: 3);
            batch.ApplyStateChange(20, EntityPhaseState.Cooldown, stateTimer: 0);
            batch.MarkDestroy(30);
            batch.SpawnEntity(spawnedTimerEntity);
            projectedWorld.ApplyBatch(batch);

            var projectedSnapshot = projectedWorld.CreateSnapshot();

            CollectionAssert.AreEqual(new[] { 10, 30 }, projectedSnapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 40 }, projectedSnapshot.CleanupTimerCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 20, 30 }, projectedSnapshot.CleanupImmediateTransitionCandidateIds.ToArray());
            AssertCleanupCandidateIndexesMatchEntities(projectedSnapshot);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_OverlayTileFeatureOperationStillValidates()
        {
            var baseSnapshot = CreateRichSnapshot();
            var invalidProjectedWorld = new ProjectedWorld(baseSnapshot);
            var invalidFeature = CreateTileFeature(90, new SurfaceCell(FaceId.Floor, 8, 8), TileFeatureKind.Button);

            Assert.Throws<InvalidOperationException>(
                () => invalidProjectedWorld.ApplyTileFeatureOperations(
                    new TileFeatureOperationBatch(new[] { TileFeatureOperation.Add(invalidFeature) })));

            var validProjectedWorld = new ProjectedWorld(baseSnapshot);
            var validFeature = CreateTileFeature(91, new SurfaceCell(FaceId.Floor, 4, 4), TileFeatureKind.Button);
            validProjectedWorld.ApplyTileFeatureOperations(
                new TileFeatureOperationBatch(new[] { TileFeatureOperation.Add(validFeature) }));

            var projectedSnapshot = validProjectedWorld.CreateSnapshot();

            Assert.That(projectedSnapshot.TryGetTileFeature(91, out var tileFeature), Is.True);
            Assert.That(tileFeature, Is.EqualTo(validFeature));
        }

        private static WorldSnapshot CreateRichSnapshot()
        {
            var stackedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = GameplayCompositionRoot.CreateWorldState(
                new[]
                {
                    CreateUnit(10, stackedCell, UnitRole.Player, teamId: 1),
                    CreateUnit(20, stackedCell, UnitRole.Enemy, teamId: 2),
                    CreateUnit(30, new SurfaceCell(FaceId.Floor, 0, 2), UnitRole.Enemy, teamId: 2),
                    CreateUnit(60, new SurfaceCell(FaceId.Floor, 3, 3), UnitRole.Enemy, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                    CreateBox(40, new SurfaceCell(FaceId.Floor, 2, 1)),
                },
                TestBounds,
                new CubeTopologyState(FaceId.Floor),
                new[]
                {
                    CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 0, 1), TileFeatureKind.Button),
                    CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 1), TileFeatureKind.Slide),
                    CreateTileFeature(30, new SurfaceCell(FaceId.Front, 1, 0), TileFeatureKind.Button),
                });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    nextExplicitActionAllowedTick = 7,
                    actionSequenceCounter = 3,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 3,
                        direction = Direction.Right,
                        targetEntityId = 40,
                        startTick = 2,
                        executeTick = 4,
                        recoveryEndTick = 6,
                    },
                });
            writeContext.SetPlayerDamageState(10, new PlayerDamageState { nextDamageAllowedTick = 11 });
            writeContext.SetEnemyActionState(
                20,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 5,
                    lockedTargetEntityId = 10,
                    direction = Direction.Left,
                    startTick = 8,
                    executeTick = 9,
                });
            writeContext.SetEnemyPatrolState(
                20,
                new EnemyPatrolRuntimeState
                {
                    sequence = 6,
                    homeCell = stackedCell,
                    lastCommittedDirection = Direction.Right,
                });
            writeContext.SetEnemyChargeState(
                20,
                new EnemyChargeRuntimeState
                {
                    sequence = 7,
                    phase = EnemyChargePhase.Active,
                    lockedDirection = Direction.Left,
                    remainingActiveSteps = 2,
                });
            writeContext.SetEnemyGlideState(
                20,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Cooldown,
                    sequence: 8,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 0,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 20,
                    windupTicks: 1,
                    durationTicks: 2,
                    recoveryTicks: 3,
                    cooldownTicks: 4,
                    lastExitedTick: 16));
            writeContext.SetEntityExecutionLockState(
                20,
                new EntityExecutionLockState
                {
                    phase = EntityExecutionPhase.Attack,
                    sequence = 9,
                    unlockTickExclusive = 30,
                });
            writeContext.SetEnemyJumpState(
                60,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 10,
                    sourceCell = new SurfaceCell(FaceId.Floor, 3, 3),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 3, 2),
                    landingTick = 18,
                });
            writeContext.SetEnemyUtilityState(20, new EnemyUtilityRuntimeState(Array.Empty<EnemyUtilityEffectState>()));
            writeContext.SetBoxInteractionLockState(
                40,
                new BoxInteractionLockState(sourceEntityId: 20, sourceEffectIndex: 1, expiresTickExclusive: 40, blocksPush: true, blocksFlip: false));
            writeContext.SetEnemyGravityFieldAuraFieldState(
                1,
                new EnemyGravityFieldAuraFieldState(
                    sourceEntityId: 40,
                    sourceEffectIndex: 1,
                    activationSequence: 1,
                    originCell: new SurfaceCell(FaceId.Floor, 2, 1),
                    radius: 2,
                    startedTick: 3,
                    expiresTickExclusive: 20,
                    blocksPush: true,
                    blocksFlip: true,
                    blocksDestroy: false));
            writeContext.SetUnitKinematicState(20, CreateKinematicState());
            writeContext.SetUnitContinuousLocomotionState(10, CreateContinuousLocomotionState());
            writeContext.SetPhasedState(30, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5, exitTickExclusive: 12));
            writeContext.SetSummonedEntityState(30, new SummonedEntityState(sourceEntityId: 20, sourceEffectIndex: 2));
            writeContext.SetEnemyDefinitionBindingState(20, new EnemyDefinitionBindingState(new EnemyUnitArchetypeId("FastImportParityEnemy")));
            writeContext.AddPendingCellImpact(
                new PendingCellImpact(
                    impactId: 1,
                    ownerId: 20,
                    sourceEnemyId: 20,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    targetCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    launchTopology: new CubeTopologyState(FaceId.Floor),
                    direction: Direction.Down,
                    damage: 1,
                    createdTick: 6,
                    releaseTick: 8,
                    impactTick: 10));

            return worldState.CreateSnapshot();
        }

        private static void AssertSnapshotsEquivalent(WorldSnapshot expected, WorldSnapshot actual)
        {
            Assert.That(actual.BoardBounds, Is.EqualTo(expected.BoardBounds));
            Assert.That(actual.Topology, Is.EqualTo(expected.Topology));
            Assert.That(actual.TopologyRevision, Is.EqualTo(expected.TopologyRevision));
            CollectionAssert.AreEqual(Collect<EntityState>(expected.EnumerateEntitiesOrdered), Collect<EntityState>(actual.EnumerateEntitiesOrdered));
            CollectionAssert.AreEqual(Collect<TileFeatureState>(expected.EnumerateTileFeaturesOrdered), Collect<TileFeatureState>(actual.EnumerateTileFeaturesOrdered));
            CollectionAssert.AreEqual(Collect<SnapshotOccupancyEntry>(expected.EnumerateUnitOccupancyOrdered), Collect<SnapshotOccupancyEntry>(actual.EnumerateUnitOccupancyOrdered));
            CollectionAssert.AreEqual(Collect<SnapshotOccupancyEntry>(expected.EnumerateSolidOccupancyOrdered), Collect<SnapshotOccupancyEntry>(actual.EnumerateSolidOccupancyOrdered));
            CollectionAssert.AreEqual(Collect<PlayerControlSnapshotEntry>(expected.EnumeratePlayerControlStatesOrdered), Collect<PlayerControlSnapshotEntry>(actual.EnumeratePlayerControlStatesOrdered));
            CollectionAssert.AreEqual(Collect<PlayerDamageSnapshotEntry>(expected.EnumeratePlayerDamageStatesOrdered), Collect<PlayerDamageSnapshotEntry>(actual.EnumeratePlayerDamageStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyActionSnapshotEntry>(expected.EnumerateEnemyActionStatesOrdered), Collect<EnemyActionSnapshotEntry>(actual.EnumerateEnemyActionStatesOrdered));
            CollectionAssert.AreEqual(Collect<PendingCellImpactSnapshotEntry>(expected.EnumeratePendingCellImpactsOrdered), Collect<PendingCellImpactSnapshotEntry>(actual.EnumeratePendingCellImpactsOrdered));
            CollectionAssert.AreEqual(Collect<EnemyPatrolSnapshotEntry>(expected.EnumerateEnemyPatrolStatesOrdered), Collect<EnemyPatrolSnapshotEntry>(actual.EnumerateEnemyPatrolStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyChargeSnapshotEntry>(expected.EnumerateEnemyChargeStatesOrdered), Collect<EnemyChargeSnapshotEntry>(actual.EnumerateEnemyChargeStatesOrdered));
            CollectionAssert.AreEqual(Collect<EntityExecutionLockSnapshotEntry>(expected.EnumerateEntityExecutionLockStatesOrdered), Collect<EntityExecutionLockSnapshotEntry>(actual.EnumerateEntityExecutionLockStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyJumpSnapshotEntry>(expected.EnumerateEnemyJumpStatesOrdered), Collect<EnemyJumpSnapshotEntry>(actual.EnumerateEnemyJumpStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyGlideSnapshotEntry>(expected.EnumerateEnemyGlideStatesOrdered), Collect<EnemyGlideSnapshotEntry>(actual.EnumerateEnemyGlideStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyUtilitySnapshotEntry>(expected.EnumerateEnemyUtilityStatesOrdered), Collect<EnemyUtilitySnapshotEntry>(actual.EnumerateEnemyUtilityStatesOrdered));
            CollectionAssert.AreEqual(Collect<BoxInteractionLockSnapshotEntry>(expected.EnumerateBoxInteractionLockStatesOrdered), Collect<BoxInteractionLockSnapshotEntry>(actual.EnumerateBoxInteractionLockStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyGravityFieldAuraFieldSnapshotEntry>(expected.EnumerateEnemyGravityFieldAuraFieldStatesOrdered), Collect<EnemyGravityFieldAuraFieldSnapshotEntry>(actual.EnumerateEnemyGravityFieldAuraFieldStatesOrdered));
            CollectionAssert.AreEqual(Collect<SummonedEntitySnapshotEntry>(expected.EnumerateSummonedEntityStatesOrdered), Collect<SummonedEntitySnapshotEntry>(actual.EnumerateSummonedEntityStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyDefinitionBindingSnapshotEntry>(expected.EnumerateEnemyDefinitionBindingStatesOrdered), Collect<EnemyDefinitionBindingSnapshotEntry>(actual.EnumerateEnemyDefinitionBindingStatesOrdered));
            CollectionAssert.AreEqual(Collect<UnitKinematicSnapshotEntry>(expected.EnumerateUnitKinematicStatesOrdered), Collect<UnitKinematicSnapshotEntry>(actual.EnumerateUnitKinematicStatesOrdered));
            CollectionAssert.AreEqual(Collect<UnitContinuousLocomotionSnapshotEntry>(expected.EnumerateUnitContinuousLocomotionStatesOrdered), Collect<UnitContinuousLocomotionSnapshotEntry>(actual.EnumerateUnitContinuousLocomotionStatesOrdered));
            CollectionAssert.AreEqual(Collect<PhasedSnapshotEntry>(expected.EnumeratePhasedStatesOrdered), Collect<PhasedSnapshotEntry>(actual.EnumeratePhasedStatesOrdered));
            CollectionAssert.AreEqual(
                expected.CleanupRemovalCandidateIds.ToArray(),
                actual.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(
                expected.CleanupTimerCandidateIds.ToArray(),
                actual.CleanupTimerCandidateIds.ToArray());
            CollectionAssert.AreEqual(
                expected.CleanupImmediateTransitionCandidateIds.ToArray(),
                actual.CleanupImmediateTransitionCandidateIds.ToArray());
        }

        private static void AssertCleanupCandidateIndexesMatchEntities(WorldSnapshot snapshot)
        {
            var entities = Collect<EntityState>(snapshot.EnumerateEntitiesOrdered);
            CollectionAssert.AreEqual(
                entities.Where(entity => entity.hp <= 0 || entity.markedForDeath)
                    .Select(entity => entity.entityId)
                    .ToArray(),
                snapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(
                entities.Where(entity => entity.stateTimer > 0)
                    .Select(entity => entity.entityId)
                    .ToArray(),
                snapshot.CleanupTimerCandidateIds.ToArray());
            CollectionAssert.AreEqual(
                entities.Where(entity =>
                        entity.stateTimer <= 0 &&
                        (entity.state == EntityPhaseState.Acting || entity.state == EntityPhaseState.Cooldown))
                    .Select(entity => entity.entityId)
                    .ToArray(),
                snapshot.CleanupImmediateTransitionCandidateIds.ToArray());
        }

        private static List<T> Collect<T>(Action<List<T>> enumerate)
        {
            var buffer = new List<T>();
            enumerate(buffer);
            return buffer;
        }

        private static int[] CollectUnitOccupancyIdsAt(WorldSnapshot snapshot, SurfaceCell cell)
        {
            return Collect<SnapshotOccupancyEntry>(snapshot.EnumerateUnitOccupancyOrdered)
                .Where(entry => entry.Cell == cell)
                .Select(entry => entry.EntityId)
                .ToArray();
        }

        private static int[] CollectTileFeatureIdsAt(WorldSnapshot snapshot, SurfaceCell cell)
        {
            var tileFeatures = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesAt(cell, tileFeatures);
            return tileFeatures.Select(tile => tile.TileId).ToArray();
        }

        private static TileFeatureState CreateTileFeature(int tileId, SurfaceCell cell, TileFeatureKind kind)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            UnitRole unitRole,
            int teamId,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = boardPresence,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 2,
                maxHp = 2,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            var wall = CreateBox(entityId, position);
            wall.type = EntityType.Wall;
            wall.boxCapabilities = BoxCapabilities.None;
            return wall;
        }

        private static PendingEnemyBlockedReaction CreateWallReaction(
            int enemyEntityId,
            int blockerEntityId,
            SurfaceCell wallCell,
            int createdTick)
        {
            return new PendingEnemyBlockedReaction(
                enemyEntityId,
                EnemyBlockedReactionKind.KinematicContinuationTargetBlocked,
                EnemyAiMode.Chase,
                wallCell + Vector2Int.left,
                wallCell,
                Direction.Right,
                LegalityBlockerKind.Solid,
                SolidKind.Wall,
                EntityType.Wall,
                blockerEntityId,
                createdTick,
                createdTick + 2);
        }

        private static void AssertReactionEqual(
            PendingEnemyBlockedReactionSnapshotEntry expected,
            PendingEnemyBlockedReactionSnapshotEntry actual)
        {
            Assert.That(actual.EntityId, Is.EqualTo(expected.EntityId));
            Assert.That(actual.Reaction.EnemyEntityId, Is.EqualTo(expected.Reaction.EnemyEntityId));
            Assert.That(actual.Reaction.Kind, Is.EqualTo(expected.Reaction.Kind));
            Assert.That(actual.Reaction.ModeAtBlock, Is.EqualTo(expected.Reaction.ModeAtBlock));
            Assert.That(actual.Reaction.SourceCell, Is.EqualTo(expected.Reaction.SourceCell));
            Assert.That(actual.Reaction.BlockedTargetCell, Is.EqualTo(expected.Reaction.BlockedTargetCell));
            Assert.That(actual.Reaction.BlockedDirection, Is.EqualTo(expected.Reaction.BlockedDirection));
            Assert.That(actual.Reaction.BlockerKind, Is.EqualTo(expected.Reaction.BlockerKind));
            Assert.That(actual.Reaction.BlockerSolidKind, Is.EqualTo(expected.Reaction.BlockerSolidKind));
            Assert.That(actual.Reaction.BlockerEntityType, Is.EqualTo(expected.Reaction.BlockerEntityType));
            Assert.That(actual.Reaction.BlockerEntityId, Is.EqualTo(expected.Reaction.BlockerEntityId));
            Assert.That(actual.Reaction.CreatedTick, Is.EqualTo(expected.Reaction.CreatedTick));
            Assert.That(actual.Reaction.ExpireTick, Is.EqualTo(expected.Reaction.ExpireTick));
        }

        private static UnitKinematicRuntimeState CreateKinematicState()
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = new SimulationOffset2(SimulationFixed.FromRaw(256), SimulationFixed.Zero),
                velocity = new SimulationVelocity2(SimulationFixed.FromRaw(256), SimulationFixed.Zero),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = 1024,
                remainingTicks = 3,
                speedScalePermille = 1000,
                sequenceId = 4,
                elapsedTicks = 1,
                totalTicks = 5,
                commitTick = 12,
                startedTick = 7,
                stepDirectionX = 1,
            }.NormalizedForStorage();
        }

        private static UnitContinuousLocomotionState CreateContinuousLocomotionState()
        {
            return new UnitContinuousLocomotionState
            {
                localOffset = new SimulationOffset2(SimulationFixed.FromRaw(-256), SimulationFixed.Zero),
                velocity = new SimulationVelocity2(SimulationFixed.FromRaw(-256), SimulationFixed.Zero),
                facing = Direction.Left,
                lastMoveDirection = Direction.Left,
                speedUnitsPerTick = 256,
                mode = ContinuousLocomotionMode.Moving,
                sequenceId = 5,
            }.NormalizedForStorage();
        }
    }
}
