using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class TickWorkDiagnosticsSimulationTests
    {
        [Test]
        [Category("Extended")]
        public void BuilderResultPath_B2RecordsOneEnumerationNoCopyOneWrapperAndOneTrustedShare()
        {
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()));

            GameplayTickWorkloadCounts counts;
            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                pipeline.RunTick(new TickInput(1));
                counts = capture.Counts;
            }

            Assert.That(counts.FinalEntityEnumerationCount, Is.EqualTo(1));
            Assert.That(counts.FinalEntityEnumeratedItemCount, Is.Zero);
            Assert.That(counts.FinalEntityDefensiveCopyCount, Is.Zero);
            Assert.That(counts.FinalEntityDefensiveCopiedItemCount, Is.Zero);
            Assert.That(counts.FinalEntityOwnedWrapperCreationCount, Is.EqualTo(1));
            Assert.That(counts.TickResultFinalEntityTrustedShareCount, Is.EqualTo(1));
            Assert.That(counts.EntityLogicProviderBuildCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Capture_DoesNotChangeCanonicalOutputs_AndNestedScopesRestoreWithoutLeak()
        {
            var uncaptured = GameplayCompositionRoot
                .CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()))
                .RunTick(new TickInput(7));

            TickResult captured;
            using (var outer = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                GameplayTickWorkloadDiagnostics.RecordFinalEntityEnumeration(2);
                using (var inner = GameplayTickWorkloadDiagnostics.BeginCapture())
                {
                    GameplayTickWorkloadDiagnostics.RecordFinalEntityEnumeration(3);
                    Assert.That(inner.Counts.FinalEntityEnumerationCount, Is.EqualTo(1));
                    Assert.That(inner.Counts.FinalEntityEnumeratedItemCount, Is.EqualTo(3));
                }

                Assert.That(GameplayTickWorkloadDiagnostics.Current.FinalEntityEnumerationCount, Is.EqualTo(1));
                Assert.That(outer.Counts.FinalEntityEnumeratedItemCount, Is.EqualTo(2));
                captured = GameplayCompositionRoot
                    .CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()))
                    .RunTick(new TickInput(7));
            }

            Assert.That(GameplayTickWorkloadDiagnostics.Current.FinalEntityEnumerationCount, Is.Zero);
            CollectionAssert.AreEqual(uncaptured.FinalEntities, captured.FinalEntities);
            CollectionAssert.AreEqual(uncaptured.EventLog, captured.EventLog);
            CollectionAssert.AreEqual(uncaptured.PhaseTrace, captured.PhaseTrace);
            Assert.That(captured.DeterminismHash, Is.EqualTo(uncaptured.DeterminismHash));
            Assert.That(captured.Trace.Text, Is.EqualTo(uncaptured.Trace.Text));
        }
    }
}
