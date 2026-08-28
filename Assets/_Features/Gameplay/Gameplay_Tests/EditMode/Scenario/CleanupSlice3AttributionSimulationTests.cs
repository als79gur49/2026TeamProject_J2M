using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class CleanupSlice3AttributionSimulationTests
    {
        [Test]
        [Category("Extended")]
        public void S3A_FullScanDiagnostics_RecordCandidatesProcessingAndTiming()
        {
            var world = CreateWorld(
                CreateWall(1, hp: 1, EntityPhaseState.Idle, stateTimer: 0),
                CreateWall(2, hp: 0, EntityPhaseState.Idle, stateTimer: 0),
                CreateWall(3, hp: 1, EntityPhaseState.Acting, stateTimer: 2),
                CreateWall(4, hp: 1, EntityPhaseState.Cooldown, stateTimer: 0));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(world);

            CleanupSlice3Counts counts;
            using (var capture = CleanupSlice3Diagnostics.BeginCapture(compareReferenceOracle: true))
            {
                pipeline.RunTick(new TickInput(10));
                counts = capture.Counts;
            }

            Assert.That(counts.FullScanInvocationCount, Is.EqualTo(1));
            Assert.That(counts.FullScanEntityVisitCount, Is.EqualTo(4));
            Assert.That(counts.SurvivorCopyCount, Is.EqualTo(3));
            Assert.That(counts.RemovalCandidateCount, Is.EqualTo(1));
            Assert.That(counts.TimerCandidateCount, Is.EqualTo(1));
            Assert.That(counts.ImmediateTransitionCandidateCount, Is.EqualTo(1));
            Assert.That(counts.RemovalProcessedCount, Is.EqualTo(1));
            Assert.That(counts.TimerProcessedCount, Is.EqualTo(1));
            Assert.That(counts.TransitionProcessedCount, Is.EqualTo(1));
            Assert.That(counts.ReferenceOracleInvocationCount, Is.EqualTo(1));
            Assert.That(counts.InvariantMismatchCount, Is.Zero);
            Assert.That(counts.IndexedInvocationCount, Is.Zero);
            Assert.That(counts.HiddenFallbackCount, Is.Zero);
            Assert.That(counts.CleanupProcessorTimingSampleCount, Is.EqualTo(1));
            Assert.That(counts.RunCleanupPhaseTimingSampleCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void S3A_ReferenceOracle_MatchesAllCleanupResultFields()
        {
            var world = CreateWorld(
                CreateWall(1, hp: 0, EntityPhaseState.Idle, stateTimer: 0),
                CreateWall(2, hp: 1, EntityPhaseState.Acting, stateTimer: 1),
                CreateWall(3, hp: 1, EntityPhaseState.Cooldown, stateTimer: 0));
            var snapshot = world.CreateSnapshot();
            var expected = CleanupReferenceOracle.BuildPlan(snapshot, tickIndex: 20);
            var actual = new Game.Feature.Gameplay.Cleanup.CleanupProcessor()
                .Process(snapshot, (ICleanupCommitContext)world.CreateWriteContext(), tickIndex: 20);

            CollectionAssert.AreEqual(expected.RemovedEntityIds, actual.RemovedEntityIds);
            CollectionAssert.AreEqual(expected.TimerChanges, actual.TimerChanges);
            CollectionAssert.AreEqual(expected.StateTransitions, actual.StateTransitions);
            CollectionAssert.AreEqual(expected.EventLogEntries, actual.EventLogEntries);
            CollectionAssert.AreEqual(expected.RemovedUnitKinematicPoses, actual.RemovedUnitKinematicPoses);
            CollectionAssert.AreEqual(
                expected.RemovedUnitContinuousLocomotionPoses,
                actual.RemovedUnitContinuousLocomotionPoses);
        }

        [Test]
        [Category("Extended")]
        public void S3A_ReferenceOracle_ProducesExactCommitOperationOrder()
        {
            var world = CreateWorld(
                CreateWall(1, hp: 0, EntityPhaseState.Idle, stateTimer: 0),
                CreateWall(2, hp: 1, EntityPhaseState.Acting, stateTimer: 1),
                CreateWall(3, hp: 1, EntityPhaseState.Cooldown, stateTimer: 0));

            var plan = CleanupReferenceOracle.BuildOperationPlan(world.CreateSnapshot(), tickIndex: 20);

            Assert.That(plan.Operations.Count, Is.EqualTo(4));
            AssertOperation(plan.Operations[0], CleanupCommitOperationKind.RemoveEntity, 1, EntityPhaseState.None, 0);
            AssertOperation(plan.Operations[1], CleanupCommitOperationKind.ApplyStateChange, 2, EntityPhaseState.Acting, 0);
            AssertOperation(plan.Operations[2], CleanupCommitOperationKind.ApplyStateChange, 2, EntityPhaseState.Idle, 0);
            AssertOperation(plan.Operations[3], CleanupCommitOperationKind.ApplyStateChange, 3, EntityPhaseState.Idle, 0);
        }

        [TestCase("B")]
        [TestCase("C")]
        [Category("Extended")]
        public void S3A_UnavailableStrategy_FailsClosedWithoutAFallback(string requested)
        {
            var resolved = CleanupCaptureStrategySelector.TryResolveS3A(
                requested,
                out var strategy,
                out var rejectionReason);

            Assert.That(resolved, Is.False);
            Assert.That(strategy, Is.EqualTo(CleanupCaptureStrategy.AFullScanNoCandidates));
            Assert.That(rejectionReason, Does.Contain("unavailable"));
        }

        [Test]
        [Category("Extended")]
        public void S3A_TargetAndStressWorkloads_HaveFrozenIdentityAndDeterministicCandidateSchedules()
        {
            var target = CleanupSlice3SyntheticWorkload.CreateTarget();
            var targetObservation = target.RunTick(101, compareReferenceOracle: true);
            var stress = CleanupSlice3SyntheticWorkload.CreateStress();
            stress.RunTick(100);
            var stressObservation = stress.RunTick(101, compareReferenceOracle: true);

            Assert.That(target.WorkloadId, Is.EqualTo("cleanup-s3-target-wall-empty-v2"));
            Assert.That(target.Seed, Is.EqualTo(31001));
            Assert.That(target.ScheduleHash, Is.EqualTo("672ce82fe95f40bb5077fa7014ab501a849dc35072a997328bd06108fb1e6385"));
            Assert.That(target.InitialWorldFingerprint, Is.EqualTo("6d3a0d63249ca1bf4517ccbc39c808fa4db0efee2f61e80fd14757fa18da39bc"));
            Assert.That(target.EntityCount, Is.EqualTo(256));
            Assert.That(target.WallCount, Is.EqualTo(256));
            Assert.That(target.ScheduledOperationCount, Is.Zero);
            Assert.That(targetObservation.MutationCount, Is.Zero);
            Assert.That(targetObservation.Counts.RemovalCandidateCount, Is.Zero);
            Assert.That(targetObservation.Counts.TimerCandidateCount, Is.Zero);
            Assert.That(targetObservation.Counts.ImmediateTransitionCandidateCount, Is.Zero);

            Assert.That(stress.WorkloadId, Is.EqualTo("cleanup-s3-stress-dense-v2"));
            Assert.That(stress.Seed, Is.EqualTo(31002));
            Assert.That(stress.ScheduleHash, Is.EqualTo("ac786715ea3dd77f89423fb30db33d209c2529da97e7c53d140760522a517b24"));
            Assert.That(stress.InitialWorldFingerprint, Is.EqualTo("2282b3ceaea055d8d382f7c2c20d4d915d11d54ed37c6cc0231784753e2dc924"));
            Assert.That(stress.EntityCount, Is.EqualTo(256));
            Assert.That(stress.WallCount, Is.EqualTo(256));
            Assert.That(stress.ScheduledOperationCount, Is.EqualTo(96));
            Assert.That(stressObservation.MutationCount, Is.EqualTo(96));
            Assert.That(stressObservation.Counts.RemovalCandidateCount, Is.EqualTo(16));
            Assert.That(stressObservation.Counts.TimerCandidateCount, Is.EqualTo(32));
            Assert.That(stressObservation.Counts.ImmediateTransitionCandidateCount, Is.EqualTo(32));
            Assert.That(stressObservation.Counts.InvariantMismatchCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void S3A_DiagnosticsCaptureOff_IsAllocationFreeAndAuthoritativelyNonInterfering()
        {
            CleanupSlice3Diagnostics.MeasureDisabledNoOpAllocatedBytes(iterations: 1);
            var allocatedBytes = CleanupSlice3Diagnostics.MeasureDisabledNoOpAllocatedBytes(iterations: 10000);

            Assert.That(allocatedBytes, Is.Zero);

            var uncapturedWorld = CreatePoseRemovalWorld();
            var capturedWorld = CreatePoseRemovalWorld();
            var uncaptured = GameplayCompositionRoot.CreateTickPipeline(uncapturedWorld)
                .RunTick(new TickInput(40));
            TickResult captured;
            CleanupSlice3Counts counts;
            using (var capture = CleanupSlice3Diagnostics.BeginCapture(compareReferenceOracle: true))
            {
                captured = GameplayCompositionRoot.CreateTickPipeline(capturedWorld)
                    .RunTick(new TickInput(40));
                counts = capture.Counts;
            }

            CollectionAssert.AreEqual(uncaptured.EventLog, captured.EventLog);
            CollectionAssert.AreEqual(uncaptured.FinalEntities, captured.FinalEntities);
            CollectionAssert.AreEqual(uncaptured.PhaseTrace, captured.PhaseTrace);
            Assert.That(captured.DeterminismHash, Is.EqualTo(uncaptured.DeterminismHash));
            Assert.That(captured.Trace.Text, Is.EqualTo(uncaptured.Trace.Text));
            CollectionAssert.AreEqual(
                uncaptured.PresentationData.KinematicMotionTracks,
                captured.PresentationData.KinematicMotionTracks);
            CollectionAssert.AreEqual(
                uncaptured.PresentationData.ContinuousLocomotionTracks,
                captured.PresentationData.ContinuousLocomotionTracks);
            Assert.That(counts.ReferenceOracleInvocationCount, Is.EqualTo(1));
            Assert.That(counts.InvariantMismatchCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void S3A_TimingOnlyCapture_DoesNotCollectStructuralCounters()
        {
            var world = CreateWorld(
                CreateWall(1, hp: 1, EntityPhaseState.Idle, stateTimer: 0),
                CreateWall(2, hp: 0, EntityPhaseState.Idle, stateTimer: 0));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(world);

            CleanupSlice3Counts counts;
            using (var capture = CleanupSlice3Diagnostics.BeginCapture(CleanupCaptureMode.Timing))
            {
                pipeline.RunTick(new TickInput(60));
                counts = capture.Counts;
            }

            Assert.That(counts.FullScanInvocationCount, Is.Zero);
            Assert.That(counts.FullScanEntityVisitCount, Is.Zero);
            Assert.That(counts.RemovalCandidateCount, Is.Zero);
            Assert.That(counts.CleanupProcessorTimingSampleCount, Is.EqualTo(1));
            Assert.That(counts.RunCleanupPhaseTimingSampleCount, Is.EqualTo(1));
        }

        private static WorldState CreateWorld(params EntityState[] entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(8, 8)),
                new CubeTopologyState(FaceId.Floor));
        }

        private static void AssertOperation(
            CleanupCommitOperation operation,
            CleanupCommitOperationKind kind,
            int entityId,
            EntityPhaseState state,
            int stateTimer)
        {
            Assert.That(operation.Kind, Is.EqualTo(kind));
            Assert.That(operation.EntityId, Is.EqualTo(entityId));
            Assert.That(operation.State, Is.EqualTo(state));
            Assert.That(operation.StateTimer, Is.EqualTo(stateTimer));
        }

        private static WorldState CreatePoseRemovalWorld()
        {
            var world = CreateWorld(
                new EntityState
                {
                    entityId = 1,
                    position = new SurfaceCell(FaceId.Floor, 1, 1),
                    hp = 0,
                    maxHp = 1,
                    type = EntityType.Unit,
                    state = EntityPhaseState.Idle,
                    facing = Direction.Right,
                    boardPresence = EntityBoardPresence.Occupying,
                },
                new EntityState
                {
                    entityId = 2,
                    position = new SurfaceCell(FaceId.Floor, 2, 1),
                    hp = 0,
                    maxHp = 1,
                    type = EntityType.Unit,
                    state = EntityPhaseState.Idle,
                    facing = Direction.Left,
                    boardPresence = EntityBoardPresence.Occupying,
                });
            var writeContext = world.CreateWriteContext();
            writeContext.SetUnitKinematicState(
                1,
                new UnitKinematicRuntimeState
                {
                    localOffset = new SimulationOffset2(SimulationFixed.FromRaw(1024), SimulationFixed.Zero),
                    mode = MotionMode.Held,
                    sequenceId = 7,
                });
            writeContext.SetUnitContinuousLocomotionState(
                2,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
                    facing = Direction.Left,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 8,
                });
            return world;
        }

        private static EntityState CreateWall(
            int entityId,
            int hp,
            EntityPhaseState state,
            int stateTimer)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, entityId, 0),
                hp = hp,
                maxHp = 1,
                type = EntityType.None,
                state = state,
                stateTimer = stateTimer,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
