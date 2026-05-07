using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GravityFieldPresentationRequestPlannerTests
    {
        [Test]
        [Category("Core")]
        public void TickPresentationDataEmpty_HasEmptyGravityFieldEvents()
        {
            Assert.That(TickPresentationData.Empty.GravityFieldEvents, Is.Not.Null);
            Assert.That(TickPresentationData.Empty.GravityFieldEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityFieldPresentationRequestPlanner_MapsActivatedExpired_AndPreservesOrderAndDuplicates()
        {
            var planner = new GravityFieldPresentationRequestPlanner();
            var first = new GravityFieldPresentationEvent(
                GravityFieldPresentationEventKind.Activated,
                30,
                new SurfaceCell(FaceId.Floor, 0, 0));
            var second = new GravityFieldPresentationEvent(
                GravityFieldPresentationEventKind.Expired,
                31,
                new SurfaceCell(FaceId.Floor, 1, 0));

            var requests = planner.BuildRequests(CreatePresentationData(first, second, first));

            Assert.That(
                requests.Select(request => request.RequestKind).ToArray(),
                Is.EqualTo(new[]
                {
                    GravityFieldPresentationRequestKind.Activated,
                    GravityFieldPresentationRequestKind.Expired,
                    GravityFieldPresentationRequestKind.Activated,
                }));
            Assert.That(
                requests.Select(request => request.EmitterEntityId).ToArray(),
                Is.EqualTo(new[] { 30, 31, 30 }));
            Assert.That(requests[0].Cell, Is.EqualTo(first.Cell));
            Assert.That(requests[1].Cell, Is.EqualTo(second.Cell));
        }

        [Test]
        [Category("Core")]
        public void TilePresentationRequestPlanner_IgnoresGravityFieldEvents()
        {
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(new GravityFieldPresentationEvent(
                GravityFieldPresentationEventKind.Activated,
                30,
                new SurfaceCell(FaceId.Floor, 0, 0))));

            Assert.That(requests, Is.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                gravityFieldEvents: gravityFieldEvents);
        }
    }
}
