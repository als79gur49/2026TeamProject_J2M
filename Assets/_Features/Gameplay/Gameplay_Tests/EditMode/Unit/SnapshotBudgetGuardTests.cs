using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class SnapshotBudgetGuardTests
    {
        [Test]
        [Category("Extended")]
        public void IdleTick_SnapshotMaterializationBudget_RemainsPinned()
        {
            RunIdleTickBudgetScenario(out _, out _);
            var baselineCounts = RunIdleTickBudgetScenario(out _, out _);
            var counts = RunIdleTickBudgetScenario(out var result, out var finalSnapshot);

            // Empty bounded world, default feature flags. Constructor snapshots are
            // intentionally outside the capture; this budget covers RunTick only.
            // ApplyBatch is counted separately from materialization so future empty
            // batch optimizations can reduce it without hiding snapshot regressions.
            AssertSameProjectedSnapshotBudget(baselineCounts, counts);
            AssertIdleTickRuntimeBehavior(result, finalSnapshot);
            AssertIdleTickBudgetSentinel(counts);
        }

        [Test]
        [Category("Extended")]
        public void SnapshotDiagnosticsCapture_DoesNotChangeDeterminismHash()
        {
            var uncapturedResult = GameplayCompositionRoot
                .CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()))
                .RunTick(new TickInput(7));

            TickResult capturedResult;
            using (SnapshotMaterializationDiagnostics.BeginCapture())
            {
                capturedResult = GameplayCompositionRoot
                    .CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()))
                    .RunTick(new TickInput(7));
            }

            Assert.That(capturedResult.DeterminismHash, Is.EqualTo(uncapturedResult.DeterminismHash));
            CollectionAssert.AreEqual(uncapturedResult.CompletedPhases, capturedResult.CompletedPhases);
            CollectionAssert.AreEqual(uncapturedResult.PhaseTrace, capturedResult.PhaseTrace);
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_RunPlanPhase_PlayerControlSameState_ReducesMaterialization()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                new EntityState
                {
                    entityId = 10,
                    position = new SurfaceCell(FaceId.Floor, 0, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                    state = EntityPhaseState.Idle,
                    facing = Direction.Right,
                },
            });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[] { new PlayerLogic(10) });

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                pipeline.RunTick(new TickInput(7));
                counts = capture.Counts;
            }

            Assert.That(counts.PlayerControlStateSameStateSkippedCount, Is.EqualTo(1));
            Assert.That(counts.PlayerControlStateWrittenCount, Is.Zero);
            Assert.That(counts.GetEmptyApplyBatchCount(ProjectedWorldBatchReason.PlanPreMovementState), Is.EqualTo(1));
            Assert.That(counts.GetMaterializedSnapshotCount(ProjectedWorldSnapshotReason.PlanPreMovementUtilityInput), Is.Zero);
            Assert.That(counts.GetCacheHitCount(ProjectedWorldSnapshotReason.PlanPreMovementUtilityInput), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void ProjectedWorld_CreateSnapshot_ReusesCachedSnapshotUntilBatchApplied()
        {
            var baseSnapshot = SnapshotBuilder.Create(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()));
            var projectedWorld = new ProjectedWorld(baseSnapshot);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                var first = projectedWorld.CreateSnapshot();
                var second = projectedWorld.CreateSnapshot();

                Assert.That(second, Is.SameAs(first));
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(1));
        }

        private static SnapshotMaterializationCounts RunIdleTickBudgetScenario(
            out TickResult result,
            out WorldSnapshot finalSnapshot)
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>());
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                result = pipeline.RunTick(new TickInput(7));
                counts = capture.Counts;
            }

            finalSnapshot = worldState.CreateSnapshot();
            return counts;
        }

        private static void AssertIdleTickRuntimeBehavior(
            TickResult result,
            WorldSnapshot finalSnapshot)
        {
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(result.PresentationData.EntityExitSignals, Is.Empty);
            Assert.That(result.EventLog, Is.Empty);
            Assert.That(finalSnapshot.EntityCount, Is.Zero);
            Assert.That(finalSnapshot.TileFeatureCount, Is.Zero);
        }

        private static void AssertIdleTickBudgetSentinel(SnapshotMaterializationCounts counts)
        {
            Assert.That(
                counts.WorldStateCreateSnapshotCount,
                Is.EqualTo(5),
                "idle budget sentinel: empty idle RunTick authoritative snapshot requests should remain pinned");
            Assert.That(
                counts.ProjectedWorldMaterializedSnapshotCount,
                Is.EqualTo(1),
                "idle budget sentinel: empty tick projected snapshot materialization should remain pinned");
            Assert.That(
                counts.ProjectedWorldApplyBatchCount,
                Is.EqualTo(counts.ProjectedWorldEmptyApplyBatchCount),
                "idle budget sentinel: every idle projected batch should remain empty");
        }

        private static void AssertSameProjectedSnapshotBudget(
            SnapshotMaterializationCounts expected,
            SnapshotMaterializationCounts actual)
        {
            Assert.That(actual.WorldStateCreateSnapshotCount, Is.EqualTo(expected.WorldStateCreateSnapshotCount));
            Assert.That(actual.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(expected.ProjectedWorldMaterializedSnapshotCount));
            Assert.That(actual.ProjectedWorldCacheHitCount, Is.EqualTo(expected.ProjectedWorldCacheHitCount));
            Assert.That(actual.ProjectedWorldApplyBatchCount, Is.EqualTo(expected.ProjectedWorldApplyBatchCount));
            Assert.That(actual.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(expected.ProjectedWorldEmptyApplyBatchCount));
        }
    }
}
