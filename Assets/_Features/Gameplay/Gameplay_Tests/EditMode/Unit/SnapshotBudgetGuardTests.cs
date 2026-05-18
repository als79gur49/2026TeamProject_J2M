using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class SnapshotBudgetGuardTests
    {
        [Test]
        [Category("Extended")]
        public void IdleTick_SnapshotMaterializationBudget_RemainsPinned()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>());
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                pipeline.RunTick(new TickInput(7));
                counts = capture.Counts;
            }

            // Empty bounded world, default feature flags. Constructor snapshots are
            // intentionally outside the capture; this budget covers RunTick only.
            // ApplyBatch is counted separately from materialization so future empty
            // batch optimizations can reduce it without hiding snapshot regressions.
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(6));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(2));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(10));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(12));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(12));
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
    }
}
