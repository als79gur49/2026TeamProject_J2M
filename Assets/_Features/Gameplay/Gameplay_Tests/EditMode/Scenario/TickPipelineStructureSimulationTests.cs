using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineStructureTests
    {
        [Test]
        [Category("Extended")]
        public void RunTick_CompletesPlanResolveFinalizeCleanupRespawn()
        {
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()));

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(result.TickIndex, Is.EqualTo(7));
            Assert.That(result.CompletedAllPhases, Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    TickPhase.Plan,
                    TickPhase.Resolve,
                    TickPhase.Finalize,
                    TickPhase.Cleanup,
                    TickPhase.Respawn,
                },
                result.CompletedPhases);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Plan:Enter",
                    "Plan:Exit",
                    "Resolve:Enter",
                    "Resolve:Exit",
                    "Finalize:Enter",
                    "Finalize:Exit",
                    "Cleanup:Enter",
                    "Cleanup:Exit",
                    "Respawn:Enter",
                    "Respawn:Exit",
                },
                result.PhaseTrace);
        }
    }
}
