using System;
using System.Collections.Generic;
using System.IO;
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

        [Test]
        [Category("Extended")]
        public void S3A_PlayerCalibrationCore_DerivesMeasuredReferenceParity()
        {
            var result = CleanupSlice3PlayerCalibrationCore.Capture(
                "A",
                warmupTicks: 2,
                sampleTicks: 3,
                repetitions: 2);

            Assert.That(result.Workloads, Is.Not.Empty);
            foreach (var workload in result.Workloads)
            {
                Assert.That(workload.OracleParityVerified, Is.True);
                Assert.That(workload.Runs.Count, Is.EqualTo(2));
                foreach (var run in workload.Runs)
                {
                    Assert.That(run.ExecutedTicks, Is.EqualTo(3));
                    Assert.That(run.ReferenceOracleInvocationCount, Is.EqualTo(run.ExecutedTicks));
                    Assert.That(run.InvariantMismatchCount, Is.Zero);
                }
            }

            Assert.That(result.Json, Does.Contain("\"oracleParityVerified\":true"));
        }

        [Test]
        [Category("Extended")]
        public void S3A_PlayerCalibrationCore_SeparatesTimingAndReferenceCompanionPhases()
        {
            var result = CleanupSlice3PlayerCalibrationCore.Capture(
                "A",
                warmupTicks: 2,
                sampleTicks: 3,
                repetitions: 2);

            Assert.That(result.GlobalPhaseOrderVerified, Is.True);
            foreach (var workload in result.Workloads)
            {
                foreach (var run in workload.Runs)
                {
                    Assert.That(run.Repetition, Is.InRange(1, 2));
                    Assert.That(run.TimingReferenceInvocationCount, Is.Zero);
                    Assert.That(run.CaptureOffReferenceInvocationCount, Is.Zero);
                    Assert.That(run.WarmupReferenceInvocationCount, Is.Zero);
                    Assert.That(run.ReferenceOracleInvocationCount, Is.EqualTo(3));
                    Assert.That(run.ReferenceUsesStructuralAndReference, Is.True);
                    Assert.That(run.PhaseOrder, Is.EqualTo("Timing,CaptureOff,Reference"));
                    Assert.That(run.IdentityBindingVerified, Is.True);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void S3A_BRawMembershipAndActualProcessing_MatchExactMatrix()
        {
            var world = CreateWorld(
                CreateWall(1, hp: 0, EntityPhaseState.Idle, stateTimer: 2),
                CreateWall(2, hp: 1, EntityPhaseState.Acting, stateTimer: 0, markedForDeath: true),
                CreateWall(3, hp: 1, EntityPhaseState.Cooldown, stateTimer: -1, markedForDeath: true),
                CreateWall(4, hp: 1, EntityPhaseState.Idle, stateTimer: 2),
                CreateWall(5, hp: 1, EntityPhaseState.Acting, stateTimer: 0),
                CreateWall(6, hp: 1, EntityPhaseState.Idle, stateTimer: 0),
                CreateWall(7, hp: 0, EntityPhaseState.Idle, stateTimer: 2, spawnTick: 10),
                CreateWall(8, hp: 1, EntityPhaseState.Idle, stateTimer: 2, spawnTick: 10),
                CreateWall(9, hp: 1, EntityPhaseState.Acting, stateTimer: 0, spawnTick: 10));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(world);

            CleanupSlice3Counts counts;
            TickResult result;
            using (var capture = CleanupSlice3Diagnostics.BeginCapture(
                       CleanupCaptureMode.Structural | CleanupCaptureMode.Reference))
            {
                result = pipeline.RunTick(new TickInput(10));
                counts = capture.Counts;
            }

            Assert.That(counts.RemovalCandidateCount, Is.EqualTo(4));
            Assert.That(counts.TimerCandidateCount, Is.EqualTo(4));
            Assert.That(counts.ImmediateTransitionCandidateCount, Is.EqualTo(4));
            Assert.That(counts.RemovalProcessedCount, Is.EqualTo(4));
            Assert.That(counts.TimerProcessedCount, Is.EqualTo(1));
            Assert.That(counts.TransitionProcessedCount, Is.EqualTo(2));
            Assert.That(counts.ReferenceOracleInvocationCount, Is.EqualTo(1));
            Assert.That(counts.InvariantMismatchCount, Is.Zero);

            var snapshot = world.CreateSnapshot();
            foreach (var removedId in new[] { 1, 2, 3, 7 })
            {
                Assert.That(snapshot.TryGetEntity(removedId, out _), Is.False);
            }

            Assert.That(snapshot.TryGetEntity(4, out var timerSurvivor), Is.True);
            Assert.That(timerSurvivor.stateTimer, Is.EqualTo(1));
            Assert.That(snapshot.TryGetEntity(5, out var transitioned), Is.True);
            Assert.That(transitioned.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(snapshot.TryGetEntity(8, out var sameTickTimer), Is.True);
            Assert.That(sameTickTimer.stateTimer, Is.EqualTo(2));
            Assert.That(snapshot.TryGetEntity(9, out var sameTickTransition), Is.True);
            Assert.That(sameTickTransition.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=1"));

            var projection = CleanupSlice3PlayerCalibrationCore.Project(counts);
            Assert.That(projection.RawRemovalMatchCount, Is.EqualTo(4));
            Assert.That(projection.RawTimerMatchCount, Is.EqualTo(4));
            Assert.That(projection.RawImmediateTransitionMatchCount, Is.EqualTo(4));
            Assert.That(projection.RemovalProcessedCount, Is.EqualTo(4));
            Assert.That(projection.TimerProcessedCount, Is.EqualTo(1));
            Assert.That(projection.TransitionProcessedCount, Is.EqualTo(2));
        }

        [TestCase(-1, false)]
        [TestCase(-1, true)]
        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(1, true)]
        [Category("Extended")]
        public void S3A_BRawAdditionalBoundaries_CoverHpMarkedStatesAndNonpositiveTimers(
            int hp,
            bool markedForDeath)
        {
            var states = new List<EntityPhaseState>(
                (EntityPhaseState[])Enum.GetValues(typeof(EntityPhaseState)))
            {
                (EntityPhaseState)int.MaxValue,
            };
            var timers = new[] { -1, 0 };

            for (var stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                for (var timerIndex = 0; timerIndex < timers.Length; timerIndex++)
                {
                    var state = states[stateIndex];
                    var stateTimer = timers[timerIndex];
                    var context = $"hp={hp}, marked={markedForDeath}, state={(int)state}, timer={stateTimer}";
                    var world = CreateWorld(CreateWall(
                        1,
                        hp,
                        state,
                        stateTimer,
                        markedForDeath));
                    var pipeline = GameplayCompositionRoot.CreateTickPipeline(world);

                    CleanupSlice3Counts counts;
                    TickResult result;
                    using (var capture = CleanupSlice3Diagnostics.BeginCapture(
                               CleanupCaptureMode.Structural | CleanupCaptureMode.Reference))
                    {
                        result = pipeline.RunTick(new TickInput(10));
                        counts = capture.Counts;
                    }

                    var shouldRemove = hp <= 0 || markedForDeath;
                    var shouldTransition = !shouldRemove &&
                                           (state == EntityPhaseState.Acting ||
                                            state == EntityPhaseState.Cooldown);
                    Assert.That(counts.RemovalCandidateCount, Is.EqualTo(shouldRemove ? 1 : 0), context);
                    Assert.That(counts.TimerCandidateCount, Is.Zero, context);
                    Assert.That(
                        counts.ImmediateTransitionCandidateCount,
                        Is.EqualTo(
                            state == EntityPhaseState.Acting || state == EntityPhaseState.Cooldown
                                ? 1
                                : 0),
                        context);
                    Assert.That(counts.RemovalProcessedCount, Is.EqualTo(shouldRemove ? 1 : 0), context);
                    Assert.That(counts.TimerProcessedCount, Is.Zero, context);
                    Assert.That(counts.TransitionProcessedCount, Is.EqualTo(shouldTransition ? 1 : 0), context);
                    Assert.That(counts.ReferenceOracleInvocationCount, Is.EqualTo(1), context);
                    Assert.That(counts.InvariantMismatchCount, Is.Zero, context);

                    var cleanupEvents = CollectCleanupEvents(result);
                    var snapshot = world.CreateSnapshot();
                    if (shouldRemove)
                    {
                        CollectionAssert.AreEqual(
                            new[] { "CleanupRemoved|E=1" },
                            cleanupEvents,
                            context);
                        Assert.That(snapshot.TryGetEntity(1, out _), Is.False, context);
                        Assert.That(result.FinalEntities, Is.Empty, context);
                        continue;
                    }

                    var expectedEvents = shouldTransition
                        ? new[]
                        {
                            $"StateTransitioned|E=1|From={state}|To={EntityPhaseState.Idle}|Timer={stateTimer}",
                        }
                        : Array.Empty<string>();
                    CollectionAssert.AreEqual(expectedEvents, cleanupEvents, context);
                    Assert.That(snapshot.TryGetEntity(1, out var survivor), Is.True, context);
                    Assert.That(
                        survivor.state,
                        Is.EqualTo(shouldTransition ? EntityPhaseState.Idle : state),
                        context);
                    Assert.That(survivor.stateTimer, Is.EqualTo(stateTimer), context);
                    Assert.That(result.FinalEntities.Count, Is.EqualTo(1), context);
                    Assert.That(
                        result.FinalEntities[0].state,
                        Is.EqualTo(shouldTransition ? EntityPhaseState.Idle : state),
                        context);
                    Assert.That(result.FinalEntities[0].stateTimer, Is.EqualTo(stateTimer), context);
                }
            }
        }

        [TestCase(EntityPhaseState.Acting)]
        [TestCase(EntityPhaseState.Cooldown)]
        [Category("Extended")]
        public void S3A_BRawTimerOne_TransitionsAfterTimerProcessing(EntityPhaseState initialState)
        {
            var world = CreateWorld(CreateWall(1, hp: 1, state: initialState, stateTimer: 1));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(world);

            CleanupSlice3Counts counts;
            TickResult result;
            using (var capture = CleanupSlice3Diagnostics.BeginCapture(
                       CleanupCaptureMode.Structural | CleanupCaptureMode.Reference))
            {
                result = pipeline.RunTick(new TickInput(10));
                counts = capture.Counts;
            }

            Assert.That(counts.RemovalCandidateCount, Is.Zero);
            Assert.That(counts.TimerCandidateCount, Is.EqualTo(1));
            Assert.That(counts.ImmediateTransitionCandidateCount, Is.Zero);
            Assert.That(counts.RemovalProcessedCount, Is.Zero);
            Assert.That(counts.TimerProcessedCount, Is.EqualTo(1));
            Assert.That(counts.TransitionProcessedCount, Is.EqualTo(1));
            Assert.That(counts.ReferenceOracleInvocationCount, Is.EqualTo(1));
            Assert.That(counts.InvariantMismatchCount, Is.Zero);
            CollectionAssert.AreEqual(
                new[]
                {
                    $"TimerTicked|E=1|State={initialState}|From=1|To=0",
                    $"StateTransitioned|E=1|From={initialState}|To={EntityPhaseState.Idle}|Timer=0",
                },
                CollectCleanupEvents(result));

            var snapshot = world.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(1, out var survivor), Is.True);
            Assert.That(survivor.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(survivor.stateTimer, Is.Zero);
            Assert.That(result.FinalEntities.Count, Is.EqualTo(1));
            Assert.That(result.FinalEntities[0].state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(result.FinalEntities[0].stateTimer, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void S3A_PlayerCalibrationFacade_RemainsDelegateOnly()
        {
            var sourcePath = Path.Combine(
                Application.dataPath,
                "_Features/Gameplay/Gameplay_Loop/Runtime/CleanupSlice3Diagnostics.cs");
            var source = File.ReadAllText(sourcePath);
            var facadeStart = source.IndexOf(
                "public static class CleanupSlice3PlayerCalibration",
                StringComparison.Ordinal);
            var allocationSessionStart = source.IndexOf(
                "public sealed class CleanupSlice3AllocationSession",
                StringComparison.Ordinal);

            Assert.That(facadeStart, Is.GreaterThanOrEqualTo(0), sourcePath);
            Assert.That(allocationSessionStart, Is.GreaterThan(facadeStart), sourcePath);
            var facadeSource = source.Substring(facadeStart, allocationSessionStart - facadeStart);
            Assert.That(facadeSource, Does.Not.Contain("CaptureWorkload"), sourcePath);
            Assert.That(facadeSource, Does.Not.Contain("TimingRunCapture"), sourcePath);
            Assert.That(
                facadeSource,
                Does.Not.Contain("oracleParityVerified\\\":true"),
                sourcePath);
        }

        private static WorldState CreateWorld(params EntityState[] entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(16, 16)),
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

        private static IReadOnlyList<string> CollectCleanupEvents(TickResult result)
        {
            var cleanupEvents = new List<string>();
            for (var i = 0; i < result.EventLog.Count; i++)
            {
                var entry = result.EventLog[i];
                if (entry.StartsWith("CleanupRemoved|", StringComparison.Ordinal) ||
                    entry.StartsWith("TimerTicked|", StringComparison.Ordinal) ||
                    entry.StartsWith("StateTransitioned|", StringComparison.Ordinal))
                {
                    cleanupEvents.Add(entry);
                }
            }

            return cleanupEvents;
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
            int stateTimer,
            bool markedForDeath = false,
            int spawnTick = 0)
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
                markedForDeath = markedForDeath,
                spawnTick = spawnTick,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
