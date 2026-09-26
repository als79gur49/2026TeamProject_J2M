using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class CleanupPhaseScenarioTests
    {
        [Test]
        [Category("Core")]
        public void WorldState_ExplicitWallAndLegacyNone_PreserveGenericLifecycle()
        {
            var explicitWall = CreateWall(10, new SurfaceCell(FaceId.Floor, 0, 0));
            explicitWall.type = EntityType.Wall;
            explicitWall.hp = 3;
            explicitWall.maxHp = 3;
            var legacyNone = CreateWall(20, new SurfaceCell(FaceId.Floor, 1, 0));
            legacyNone.hp = 3;
            legacyNone.maxHp = 3;
            var worldState = GameplayCompositionRoot.CreateWorldState(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor));
            var writeContext = worldState.CreateWriteContext();

            writeContext.SpawnEntity(explicitWall);
            writeContext.SpawnEntity(legacyNone);
            ((IAttackCommitContext)writeContext).ApplyDamage(10, 1);
            ((IAttackCommitContext)writeContext).ApplyDamage(20, 1);
            ((ICleanupCommitContext)writeContext).ApplyStateChange(10, EntityPhaseState.Cooldown, 2);
            ((ICleanupCommitContext)writeContext).ApplyStateChange(20, EntityPhaseState.Cooldown, 2);
            ((IAttackCommitContext)writeContext).MarkDestroy(10);
            ((IAttackCommitContext)writeContext).MarkDestroy(20);
            var mutated = worldState.CreateSnapshot();

            Assert.That(mutated.TryGetEntity(10, out var explicitMutated), Is.True);
            Assert.That(mutated.TryGetEntity(20, out var legacyMutated), Is.True);
            Assert.That(explicitMutated.type, Is.EqualTo(EntityType.Wall));
            Assert.That(legacyMutated.type, Is.EqualTo(EntityType.None));
            Assert.That(explicitMutated.hp, Is.EqualTo(2));
            Assert.That(legacyMutated.hp, Is.EqualTo(2));
            Assert.That(explicitMutated.state, Is.EqualTo(EntityPhaseState.Cooldown));
            Assert.That(legacyMutated.state, Is.EqualTo(EntityPhaseState.Cooldown));
            Assert.That(explicitMutated.stateTimer, Is.EqualTo(2));
            Assert.That(legacyMutated.stateTimer, Is.EqualTo(2));
            Assert.That(explicitMutated.markedForDeath, Is.True);
            Assert.That(legacyMutated.markedForDeath, Is.True);

            ((ICleanupCommitContext)writeContext).RemoveEntity(10);
            ((ICleanupCommitContext)writeContext).RemoveEntity(20);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Cleanup_MarkedForDeathOccupyingEntity_RemainsPresentUntilCleanupThenIsRemoved()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 2, markedForDeath: true),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);
            var beforeSnapshot = CreateSnapshot(worldState);

            Assert.That(beforeSnapshot.TryGetUnitTraversalBlocker(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
            Assert.That(beforeSnapshot.TryGetEntity(10, out var entityBefore), Is.True);
            Assert.That(entityBefore.markedForDeath, Is.True);

            var result = pipeline.RunTick(new TickInput(5));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(afterSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(afterSnapshot.TryGetUnitTraversalBlocker(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Cleanup_DetachedEntityWithoutDestroyMark_RemainsDetachedAndSurvivesCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    hp: 2,
                    boardPresence: EntityBoardPresence.Detached),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);
            var beforeSnapshot = CreateSnapshot(worldState);
            var beforeUnits = new List<EntityState>();

            Assert.That(beforeSnapshot.TryGetEntity(10, out var entityBefore), Is.True);
            Assert.That(entityBefore.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            beforeSnapshot.EnumerateUnitsAt(new SurfaceCell(FaceId.Floor, 1, 0), beforeUnits);
            Assert.That(beforeUnits, Is.Empty);
            Assert.That(beforeSnapshot.TryGetUnitTraversalBlocker(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);

            var result = pipeline.RunTick(new TickInput(6));
            var afterSnapshot = CreateSnapshot(worldState);
            var afterUnits = new List<EntityState>();

            Assert.That(SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog), Is.Empty);
            Assert.That(afterSnapshot.TryGetEntity(10, out var entityAfter), Is.True);
            Assert.That(entityAfter.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            afterSnapshot.EnumerateUnitsAt(new SurfaceCell(FaceId.Floor, 1, 0), afterUnits);
            Assert.That(afterUnits, Is.Empty);
            Assert.That(afterSnapshot.TryGetUnitTraversalBlocker(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Cleanup_HpZeroEntity_IsRemovedInCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 2, 0), hp: 0),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(7));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(afterSnapshot.TryGetEntity(10, out _), Is.False);
        }

        [TestCase(6, true)]
        [TestCase(7, false)]
        [TestCase(8, false)]
        [Category("Extended")]
        public void Cleanup_UnconsumedPendingEnemyBlockedReaction_HonorsExpiryBoundary(
            int tickIndex,
            bool expectedToRemain)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: sourceCell, hp: 3),
            });
            worldState.CreateWriteContext().SetPendingEnemyBlockedReaction(
                10,
                CreatePendingEnemyBlockedReaction(10, sourceCell, expireTick: 7));
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(tickIndex));
            var afterSnapshot = GameplayCompositionRoot.CreateSnapshot(worldState);
            var expiryEvents = result.EventLog
                .Where(entry => entry.StartsWith("PendingEnemyBlockedReactionExpired|", StringComparison.Ordinal))
                .ToArray();

            Assert.That(afterSnapshot.TryGetEntity(10, out var survivingEntity), Is.True);
            Assert.That(survivingEntity.position, Is.EqualTo(sourceCell));
            Assert.That(survivingEntity.hp, Is.EqualTo(3));
            Assert.That(
                afterSnapshot.TryGetPendingEnemyBlockedReaction(10, out _),
                Is.EqualTo(expectedToRemain));

            if (expectedToRemain)
            {
                Assert.That(expiryEvents, Is.Empty);
                return;
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    $"PendingEnemyBlockedReactionExpired|E=10|Created=6|Expire=7|Tick={tickIndex}",
                },
                expiryEvents);
        }

        [Test]
        [Category("Extended")]
        public void Cleanup_ExpiredPendingEnemyBlockedReactions_OrderByEntityIdAndSkipRemovedEnemy()
        {
            var sourceCell10 = new SurfaceCell(FaceId.Floor, 1, 0);
            var sourceCell20 = new SurfaceCell(FaceId.Floor, 2, 0);
            var sourceCell30 = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 30, position: sourceCell30, hp: 3),
                CreateUnit(entityId: 10, position: sourceCell10, hp: 3),
                CreateUnit(entityId: 20, position: sourceCell20, hp: 0),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPendingEnemyBlockedReaction(
                30,
                CreatePendingEnemyBlockedReaction(30, sourceCell30, expireTick: 7));
            writeContext.SetPendingEnemyBlockedReaction(
                10,
                CreatePendingEnemyBlockedReaction(10, sourceCell10, expireTick: 7));
            writeContext.SetPendingEnemyBlockedReaction(
                20,
                CreatePendingEnemyBlockedReaction(20, sourceCell20, expireTick: 7));
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(7));
            var afterSnapshot = GameplayCompositionRoot.CreateSnapshot(worldState);
            var expiryEvents = result.EventLog
                .Where(entry => entry.StartsWith("PendingEnemyBlockedReactionExpired|", StringComparison.Ordinal))
                .ToArray();

            Assert.That(afterSnapshot.TryGetEntity(10, out _), Is.True);
            Assert.That(afterSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(afterSnapshot.TryGetEntity(30, out _), Is.True);
            Assert.That(afterSnapshot.TryGetPendingEnemyBlockedReaction(10, out _), Is.False);
            Assert.That(afterSnapshot.TryGetPendingEnemyBlockedReaction(20, out _), Is.False);
            Assert.That(afterSnapshot.TryGetPendingEnemyBlockedReaction(30, out _), Is.False);
            CollectionAssert.AreEqual(
                new[] { 20 },
                SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            CollectionAssert.AreEqual(
                new[]
                {
                    "PendingEnemyBlockedReactionExpired|E=10|Created=6|Expire=7|Tick=7",
                    "PendingEnemyBlockedReactionExpired|E=30|Created=6|Expire=7|Tick=7",
                },
                expiryEvents);
        }

        [Test]
        [Category("Extended")]
        public void S3A_RunCleanupPhaseCaptureParity_IncludesAuxiliaryExpiry()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var captureOffWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: sourceCell, hp: 3),
            });
            var capturedWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: sourceCell, hp: 3),
            });
            captureOffWorld.CreateWriteContext().SetPendingEnemyBlockedReaction(
                10,
                CreatePendingEnemyBlockedReaction(10, sourceCell, expireTick: 7));
            capturedWorld.CreateWriteContext().SetPendingEnemyBlockedReaction(
                10,
                CreatePendingEnemyBlockedReaction(10, sourceCell, expireTick: 7));

            var captureOff = CreateMinimalRespawnPipeline(captureOffWorld)
                .RunTick(new TickInput(7));
            TickResult captured;
            CleanupSlice3Counts counts;
            using (var capture = CleanupSlice3Diagnostics.BeginCapture(
                       CleanupCaptureMode.Structural | CleanupCaptureMode.Reference))
            {
                captured = CreateMinimalRespawnPipeline(capturedWorld)
                    .RunTick(new TickInput(7));
                counts = capture.Counts;
            }

            CollectionAssert.AreEqual(captureOff.EventLog, captured.EventLog);
            CollectionAssert.AreEqual(captureOff.FinalEntities, captured.FinalEntities);
            Assert.That(captureOff.DeterminismHash, Is.EqualTo(captured.DeterminismHash));
            Assert.That(captureOff.Trace.Text, Is.EqualTo(captured.Trace.Text));
            Assert.That(counts.ReferenceOracleInvocationCount, Is.EqualTo(1));
            Assert.That(counts.InvariantMismatchCount, Is.Zero);
            Assert.That(
                CleanupSlice3PlayerCalibrationCore.VerifyWholeCleanupParity(captureOff, captured),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerRemovedInCleanup_DoesNotRespawnOrResetTopologyOnLaterTicks()
        {
            var spawnCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayerUnit(entityId: 10, position: spawnCell, hp: 4, facing: Direction.Left),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);
            worldState.CreateWriteContext().ApplyDamage(10, amount: 4);

            var deathTick = pipeline.RunTick(new TickInput(20));
            var laterTick = pipeline.RunTick(new TickInput(21));
            var snapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(deathTick.EventLog));
            Assert.That(snapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(laterTick.PresentationData.EntitySpawnSignals, Is.Empty);
            Assert.That(laterTick.PresentationData.TopologyMotion.HasValue, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Cleanup_RemovalClearsOccupancy()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 3, 1), hp: 1, markedForDeath: true),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            pipeline.RunTick(new TickInput(8));

            var afterSnapshot = CreateSnapshot(worldState);
            var units = new List<EntityState>();
            Assert.That(afterSnapshot.TryGetUnitTraversalBlocker(new SurfaceCell(FaceId.Floor, 3, 1), out _), Is.False);
            afterSnapshot.EnumerateUnitsAt(new SurfaceCell(FaceId.Floor, 3, 1), units);
            Assert.That(units, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void Cleanup_SpawnedThisTick_DoesNotTickTimer()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 2,
                    spawnTick: 11),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(11));
            var afterSnapshot = CreateSnapshot(worldState);

            Assert.That(SemanticEventAssertions.FilterEvents(result.EventLog, "TimerTicked"), Is.Empty);
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(2));
            Assert.That(GetEntityState(afterSnapshot, 10).state, Is.EqualTo(EntityPhaseState.Cooldown));
        }

        [Test]
        [Category("Core")]
        public void Cleanup_StateTimer_DecrementsOnlyForSurvivors()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 2, 0),
                    hp: 0,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 3,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 3,
                    spawnTick: 1),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(12));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            CollectionAssert.AreEqual(
                new[]
                {
                    "TimerTicked|E=10|State=Cooldown|From=3|To=2",
                },
                SemanticEventAssertions.FilterEvents(result.EventLog, "TimerTicked"));
            Assert.That(afterSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void Cleanup_StateTransition_AppliesAfterTimerTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 1,
                    spawnTick: 1),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(13));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TimerTicked|E=10|State=Cooldown|From=1|To=0",
                },
                SemanticEventAssertions.FilterEvents(result.EventLog, "TimerTicked"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateTransitioned|E=10|From=Cooldown|To=Idle|Timer=0",
                },
                SemanticEventAssertions.FilterEvents(result.EventLog, "StateTransitioned"));
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(0));
            Assert.That(GetEntityState(afterSnapshot, 10).state, Is.EqualTo(EntityPhaseState.Idle));
        }

        [Test]
        [Category("Core")]
        public void Cleanup_SlidingState_DoesNotAutoTransitionWhenTimerReachesZero()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Sliding,
                    stateTimer: 1,
                    spawnTick: 1),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(14));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TimerTicked|E=10|State=Sliding|From=1|To=0",
                },
                SemanticEventAssertions.FilterEvents(result.EventLog, "TimerTicked"));
            Assert.That(SemanticEventAssertions.FilterEvents(result.EventLog, "StateTransitioned"), Is.Empty);
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(0));
            Assert.That(GetEntityState(afterSnapshot, 10).state, Is.EqualTo(EntityPhaseState.Sliding));
        }

        [Test]
        [Category("Core")]
        public void Cleanup_SameInput_ProducesDeterministicResult()
        {
            var firstRun = RunDeterministicCleanupTick();
            var secondRun = RunDeterministicCleanupTick();

            CollectionAssert.AreEqual(
                SemanticEventAssertions.GetCleanupRemovedEntityIds(firstRun.Result.EventLog),
                SemanticEventAssertions.GetCleanupRemovedEntityIds(secondRun.Result.EventLog));
            CollectionAssert.AreEqual(
                SemanticEventAssertions.FilterEvents(firstRun.Result.EventLog, "TimerTicked"),
                SemanticEventAssertions.FilterEvents(secondRun.Result.EventLog, "TimerTicked"));
            CollectionAssert.AreEqual(
                SemanticEventAssertions.FilterEvents(firstRun.Result.EventLog, "StateTransitioned"),
                SemanticEventAssertions.FilterEvents(secondRun.Result.EventLog, "StateTransitioned"));
            Assert.That(firstRun.Result.DeterminismHash, Is.EqualTo(secondRun.Result.DeterminismHash));
            Assert.That(firstRun.StateDumpAfter, Is.EqualTo(secondRun.StateDumpAfter));
        }

        [Test]
        [Category("Core")]
        public void CleanupCandidateIndex_SnapshotClassifiesAndOrdersOverlappingMembership()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(50, new SurfaceCell(FaceId.Floor, 4, 0), hp: 3),
                CreateUnit(30, new SurfaceCell(FaceId.Floor, 2, 0), hp: 3, state: EntityPhaseState.Acting),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), hp: 0, state: EntityPhaseState.Cooldown, stateTimer: 5),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 3, 0), hp: 3, markedForDeath: true),
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, state: EntityPhaseState.Sliding, stateTimer: 2),
            });

            var snapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 20, 40 }, snapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 10, 20 }, snapshot.CleanupTimerCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 30 }, snapshot.CleanupImmediateTransitionCandidateIds.ToArray());
        }

        [Test]
        [Category("Core")]
        public void CleanupCandidateIndex_AppliesPredicatesToWallAndDetachedNoneEntities()
        {
            var removedWall = CreateWall(10, new SurfaceCell(FaceId.Floor, 0, 0));
            removedWall.hp = 0;
            var detachedNone = CreateWall(20, new SurfaceCell(FaceId.Floor, 1, 0));
            detachedNone.boardPresence = EntityBoardPresence.Detached;
            detachedNone.state = EntityPhaseState.Cooldown;
            detachedNone.stateTimer = 2;
            var immediateWall = CreateWall(30, new SurfaceCell(FaceId.Floor, 2, 0));
            immediateWall.state = EntityPhaseState.Acting;

            var snapshot = CreateSnapshot(new WorldState(new[]
            {
                immediateWall,
                detachedNone,
                removedWall,
            }));

            CollectionAssert.AreEqual(new[] { 10 }, snapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 20 }, snapshot.CleanupTimerCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 30 }, snapshot.CleanupImmediateTransitionCandidateIds.ToArray());
        }

        [Test]
        [Category("Core")]
        public void CleanupCandidateIndex_OldSnapshotIsImmutableAcrossSameIdReuse()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), hp: 0, state: EntityPhaseState.Cooldown),
            });
            var oldSnapshot = CreateSnapshot(worldState);
            var writeContext = worldState.CreateWriteContext();

            writeContext.RemoveEntity(10);
            writeContext.SpawnEntity(CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0), hp: 3));
            var currentSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, oldSnapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 10 }, oldSnapshot.CleanupImmediateTransitionCandidateIds.ToArray());
            Assert.That(currentSnapshot.CleanupRemovalCandidateIds.IsEmpty, Is.True);
            Assert.That(currentSnapshot.CleanupTimerCandidateIds.IsEmpty, Is.True);
            Assert.That(currentSnapshot.CleanupImmediateTransitionCandidateIds.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void CleanupCandidateIndex_MutationsAndFastImportPreserveMembership()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
            });
            var writeContext = worldState.CreateWriteContext();

            ((ICleanupCommitContext)writeContext).ApplyStateChange(10, EntityPhaseState.Cooldown, stateTimer: 2);
            ((IAttackCommitContext)writeContext).MarkDestroy(10);

            var indexedSnapshot = CreateSnapshot(worldState);
            CollectionAssert.AreEqual(new[] { 10 }, indexedSnapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(new[] { 10 }, indexedSnapshot.CleanupTimerCandidateIds.ToArray());
            Assert.That(indexedSnapshot.CleanupImmediateTransitionCandidateIds.IsEmpty, Is.True);

            var restoredWorld = WorldState.CreateFromSnapshotFast(indexedSnapshot);
            var restoredWriteContext = restoredWorld.CreateWriteContext();
            ((ICleanupCommitContext)restoredWriteContext).ApplyStateChange(10, EntityPhaseState.Cooldown, stateTimer: 0);

            var restoredSnapshot = CreateSnapshot(restoredWorld);
            CollectionAssert.AreEqual(new[] { 10 }, restoredSnapshot.CleanupRemovalCandidateIds.ToArray());
            Assert.That(restoredSnapshot.CleanupTimerCandidateIds.IsEmpty, Is.True);
            CollectionAssert.AreEqual(new[] { 10 }, restoredSnapshot.CleanupImmediateTransitionCandidateIds.ToArray());

            ((ICleanupCommitContext)restoredWriteContext).RemoveEntity(10);
            var removedSnapshot = CreateSnapshot(restoredWorld);
            Assert.That(removedSnapshot.CleanupRemovalCandidateIds.IsEmpty, Is.True);
            Assert.That(removedSnapshot.CleanupTimerCandidateIds.IsEmpty, Is.True);
            Assert.That(removedSnapshot.CleanupImmediateTransitionCandidateIds.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void CleanupCandidateIndex_MatchesIndependentFullScanAcrossAuthoritativeMutations()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(50, new SurfaceCell(FaceId.Floor, 4, 0), hp: 3),
                CreateUnit(30, new SurfaceCell(FaceId.Floor, 2, 0), hp: 3, state: EntityPhaseState.Acting),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), hp: 0, state: EntityPhaseState.Cooldown, stateTimer: 5),
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, state: EntityPhaseState.Sliding, stateTimer: 2),
            });
            var writeContext = worldState.CreateWriteContext();

            AssertCleanupCandidateIndexMatchesIndependentFullScan(CreateSnapshot(worldState));

            ((IAttackCommitContext)writeContext).ApplyDamage(50, amount: 3);
            ((ICleanupCommitContext)writeContext).ApplyStateChange(10, EntityPhaseState.Idle, stateTimer: 0);
            ((IAttackCommitContext)writeContext).SpawnEntity(
                CreateUnit(60, new SurfaceCell(FaceId.Floor, 5, 0), hp: 3, state: EntityPhaseState.Cooldown));
            ((ICleanupCommitContext)writeContext).RemoveEntity(20);

            AssertCleanupCandidateIndexMatchesIndependentFullScan(CreateSnapshot(worldState));
            AssertCleanupCandidateIndexMatchesIndependentFullScan(
                CreateSnapshot(WorldState.CreateFromSnapshotFast(CreateSnapshot(worldState))));
        }

        [Test]
        [Category("Core")]
        public void Cleanup_OverlappingCandidates_PreservesFullScanPhaseAndEntityOrdering()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(30, new SurfaceCell(FaceId.Floor, 2, 0), hp: 3, state: EntityPhaseState.Acting),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), hp: 0, state: EntityPhaseState.Cooldown, stateTimer: 4),
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, state: EntityPhaseState.Cooldown, stateTimer: 1),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(15));

            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            CollectionAssert.AreEqual(
                new[] { "TimerTicked|E=10|State=Cooldown|From=1|To=0" },
                SemanticEventAssertions.FilterEvents(result.EventLog, "TimerTicked"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateTransitioned|E=10|From=Cooldown|To=Idle|Timer=0",
                    "StateTransitioned|E=30|From=Acting|To=Idle|Timer=0",
                },
                SemanticEventAssertions.FilterEvents(result.EventLog, "StateTransitioned"));
        }

        [Test]
        [Category("Core")]
        public void Cleanup_TimerExpiryTransitionsOnceAndDoesNotRepeatOnNextTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    10,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 1),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var first = pipeline.RunTick(new TickInput(15));
            var second = pipeline.RunTick(new TickInput(16));

            CollectionAssert.AreEqual(
                new[] { "StateTransitioned|E=10|From=Cooldown|To=Idle|Timer=0" },
                SemanticEventAssertions.FilterEvents(first.EventLog, "StateTransitioned"));
            Assert.That(SemanticEventAssertions.FilterEvents(second.EventLog, "TimerTicked"), Is.Empty);
            Assert.That(SemanticEventAssertions.FilterEvents(second.EventLog, "StateTransitioned"), Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void CleanupCandidateIndex_GeneratedMatrix_MatchesIndependentFullScanReference()
        {
            const int tickIndex = 25;
            var states = new[]
            {
                EntityPhaseState.Idle,
                EntityPhaseState.Acting,
                EntityPhaseState.Cooldown,
                EntityPhaseState.Sliding,
            };
            var timers = new[] { -1, 0, 1, 2 };
            var hitPoints = new[] { -1, 0, 1 };
            var entities = new List<EntityState>();
            var entityId = 1;
            for (var hpIndex = 0; hpIndex < hitPoints.Length; hpIndex++)
            {
                for (var markedIndex = 0; markedIndex < 2; markedIndex++)
                {
                    for (var stateIndex = 0; stateIndex < states.Length; stateIndex++)
                    {
                        for (var timerIndex = 0; timerIndex < timers.Length; timerIndex++)
                        {
                            for (var spawnedThisTickIndex = 0; spawnedThisTickIndex < 2; spawnedThisTickIndex++)
                            {
                                entities.Add(
                                    CreateUnit(
                                        entityId++,
                                        new SurfaceCell(FaceId.Floor, 0, 0),
                                        hp: hitPoints[hpIndex],
                                        markedForDeath: markedIndex == 1,
                                        boardPresence: EntityBoardPresence.Detached,
                                        state: states[stateIndex],
                                        stateTimer: timers[timerIndex],
                                        spawnTick: spawnedThisTickIndex == 1 ? tickIndex : tickIndex - 1));
                            }
                        }
                    }
                }
            }

            var expected = BuildIndependentFullScanCleanupReference(entities, tickIndex);
            var worldState = new WorldState(entities);
            var snapshot = CreateSnapshot(worldState);
            var actual = new CleanupProcessor().Process(snapshot, worldState.CreateWriteContext(), tickIndex);
            var finalSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(expected.RemovedEntityIds, actual.RemovedEntityIds);
            CollectionAssert.AreEqual(expected.TimerChanges, actual.TimerChanges);
            CollectionAssert.AreEqual(expected.StateTransitions, actual.StateTransitions);
            Assert.That(actual.EventLogEntries, Is.Empty);
            Assert.That(DumpEntityStates(finalSnapshot), Is.EqualTo(expected.FinalEntityDump));
            AssertCleanupCandidateIndexMatchesIndependentFullScan(snapshot);
        }

        private static (TickResult Result, string StateDumpAfter) RunDeterministicCleanupTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 30,
                    position: new SurfaceCell(FaceId.Floor, 2, 0),
                    hp: 4,
                    state: EntityPhaseState.Acting,
                    stateTimer: 1,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 4,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 2,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    hp: 0,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 5,
                    spawnTick: 1),
            });
            var pipeline = CreateMinimalRespawnPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(14));
            return (result, DumpEntityStates(CreateSnapshot(worldState)));
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
            int hp,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            EntityPhaseState state = EntityPhaseState.Idle,
            int stateTimer = 0,
            int spawnTick = 0)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position), hp, markedForDeath, boardPresence, state, stateTimer, spawnTick);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int hp,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            EntityPhaseState state = EntityPhaseState.Idle,
            int stateTimer = 0,
            int spawnTick = 0)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp > 0 ? hp : 3,
                teamId = 1,
                type = EntityType.Unit,
                state = state,
                stateTimer = stateTimer,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
                spawnTick = spawnTick,
            };
        }

        private static EntityState CreatePlayerUnit(
            int entityId,
            SurfaceCell position,
            int hp,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static PendingEnemyBlockedReaction CreatePendingEnemyBlockedReaction(
            int enemyEntityId,
            SurfaceCell sourceCell,
            int expireTick)
        {
            return new PendingEnemyBlockedReaction(
                enemyEntityId,
                EnemyBlockedReactionKind.KinematicContinuationTargetBlocked,
                EnemyAiMode.Chase,
                sourceCell,
                sourceCell + Vector2Int.right,
                Direction.Right,
                LegalityBlockerKind.Solid,
                SolidKind.Box,
                EntityType.Box,
                blockerEntityId: 90,
                createdTick: expireTick - 1,
                expireTick: expireTick);
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static string DumpEntityStates(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            return string.Join(
                ",",
                entities.Select(entity =>
                    $"{entity.entityId}:{entity.state}:{entity.stateTimer}:{entity.position.x}:{entity.position.y}:{entity.hp}:{entity.markedForDeath}"));
        }

        private static EntityState GetEntityState(WorldSnapshot snapshot, int entityId)
        {
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static void AssertCleanupCandidateIndexMatchesIndependentFullScan(WorldSnapshot snapshot)
        {
            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var expectedRemovalIds = new List<int>();
            var expectedTimerIds = new List<int>();
            var expectedImmediateTransitionIds = new List<int>();
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var entity = orderedEntities[i];
                if (entity.hp <= 0 || entity.markedForDeath)
                {
                    expectedRemovalIds.Add(entity.entityId);
                }

                if (entity.stateTimer > 0)
                {
                    expectedTimerIds.Add(entity.entityId);
                }

                if (entity.stateTimer <= 0 &&
                    (entity.state == EntityPhaseState.Acting || entity.state == EntityPhaseState.Cooldown))
                {
                    expectedImmediateTransitionIds.Add(entity.entityId);
                }
            }

            CollectionAssert.AreEqual(expectedRemovalIds, snapshot.CleanupRemovalCandidateIds.ToArray());
            CollectionAssert.AreEqual(expectedTimerIds, snapshot.CleanupTimerCandidateIds.ToArray());
            CollectionAssert.AreEqual(
                expectedImmediateTransitionIds,
                snapshot.CleanupImmediateTransitionCandidateIds.ToArray());
        }

        private static CleanupReferenceOutcome BuildIndependentFullScanCleanupReference(
            IReadOnlyList<EntityState> sourceEntities,
            int tickIndex)
        {
            var orderedEntities = sourceEntities.OrderBy(entity => entity.entityId).ToList();
            var removedEntityIds = new List<int>();
            var survivingEntities = new List<EntityState>();
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var entity = orderedEntities[i];
                if (entity.hp <= 0 || entity.markedForDeath)
                {
                    removedEntityIds.Add(entity.entityId);
                }
                else
                {
                    survivingEntities.Add(entity);
                }
            }

            var timerChanges = new List<string>();
            for (var i = 0; i < survivingEntities.Count; i++)
            {
                var entity = survivingEntities[i];
                if (entity.spawnTick == tickIndex || entity.stateTimer <= 0)
                {
                    continue;
                }

                var previousTimer = entity.stateTimer;
                entity.stateTimer--;
                survivingEntities[i] = entity;
                timerChanges.Add(
                    $"TimerTicked|E={entity.entityId}|State={entity.state}|From={previousTimer}|To={entity.stateTimer}");
            }

            var stateTransitions = new List<string>();
            for (var i = 0; i < survivingEntities.Count; i++)
            {
                var entity = survivingEntities[i];
                if (entity.stateTimer > 0 ||
                    (entity.state != EntityPhaseState.Acting && entity.state != EntityPhaseState.Cooldown))
                {
                    continue;
                }

                var previousState = entity.state;
                entity.state = EntityPhaseState.Idle;
                survivingEntities[i] = entity;
                stateTransitions.Add(
                    $"StateTransitioned|E={entity.entityId}|From={previousState}|To={entity.state}|Timer={entity.stateTimer}");
            }

            var finalEntityDump = string.Join(
                ",",
                survivingEntities.Select(entity =>
                    $"{entity.entityId}:{entity.state}:{entity.stateTimer}:{entity.position.x}:{entity.position.y}:{entity.hp}:{entity.markedForDeath}"));
            return new CleanupReferenceOutcome(
                removedEntityIds,
                timerChanges,
                stateTransitions,
                finalEntityDump);
        }

        private readonly struct CleanupReferenceOutcome
        {
            public CleanupReferenceOutcome(
                IReadOnlyList<int> removedEntityIds,
                IReadOnlyList<string> timerChanges,
                IReadOnlyList<string> stateTransitions,
                string finalEntityDump)
            {
                RemovedEntityIds = removedEntityIds;
                TimerChanges = timerChanges;
                StateTransitions = stateTransitions;
                FinalEntityDump = finalEntityDump;
            }

            public IReadOnlyList<int> RemovedEntityIds { get; }

            public IReadOnlyList<string> TimerChanges { get; }

            public IReadOnlyList<string> StateTransitions { get; }

            public string FinalEntityDump { get; }
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            var createSnapshotMethod = typeof(WorldState).GetMethod(
                "CreateSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createSnapshotMethod, Is.Not.Null);

            return (WorldSnapshot)createSnapshotMethod.Invoke(worldState, null);
        }

        private static TickPipeline CreateMinimalRespawnPipeline(WorldState worldState)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                playerControlTiming);
        }
    }
}
