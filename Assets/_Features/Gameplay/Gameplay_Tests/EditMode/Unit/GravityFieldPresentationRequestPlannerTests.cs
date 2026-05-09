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
        public void GravityFieldPresentationRequestPlanner_MapsActivatedExpiredLockedBox_AndPreservesOrderPayloadAndDuplicates()
        {
            var planner = new GravityFieldPresentationRequestPlanner();
            var first = new GravityFieldPresentationEvent(
                GravityFieldPresentationEventKind.Activated,
                30,
                new SurfaceCell(FaceId.Floor, 0, 0));
            var lockedBoxPayload = new GravityFieldLockedBoxPayload(
                30,
                20,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0));
            var lockedBox = new GravityFieldPresentationEvent(
                GravityFieldPresentationEventKind.LockedBox,
                30,
                new SurfaceCell(FaceId.Floor, 0, 0),
                targetEntityId: 20,
                lockedBoxPayload: lockedBoxPayload);
            var second = new GravityFieldPresentationEvent(
                GravityFieldPresentationEventKind.Expired,
                31,
                new SurfaceCell(FaceId.Floor, 1, 0));

            var requests = planner.BuildRequests(CreatePresentationData(first, lockedBox, second, lockedBox));

            Assert.That(
                requests.Select(request => request.RequestKind).ToArray(),
                Is.EqualTo(new[]
                {
                    GravityFieldPresentationRequestKind.Activated,
                    GravityFieldPresentationRequestKind.LockedBox,
                    GravityFieldPresentationRequestKind.Expired,
                    GravityFieldPresentationRequestKind.LockedBox,
                }));
            Assert.That(
                requests.Select(request => request.EmitterEntityId).ToArray(),
                Is.EqualTo(new[] { 30, 30, 31, 30 }));
            Assert.That(requests[0].Cell, Is.EqualTo(first.Cell));
            Assert.That(requests[1].Cell, Is.EqualTo(lockedBox.Cell));
            Assert.That(requests[1].TargetEntityId, Is.EqualTo(20));
            Assert.That(requests[1].LockedBoxPayload, Is.EqualTo(lockedBoxPayload));
            Assert.That(requests[2].Cell, Is.EqualTo(second.Cell));
            Assert.That(requests[3].LockedBoxPayload, Is.EqualTo(lockedBoxPayload));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldPresentationRequestPlanner_ReadModelOnlyLockedTargets_ProducesNoRequests()
        {
            var planner = new GravityFieldPresentationRequestPlanner();
            var presentationData = CreatePresentationData(new[]
            {
                new GravityFieldVisualState(
                    30,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 180,
                    progress01: 0.5f,
                    areaFootprint: GravityFieldAreaFootprint.Empty,
                    lockedTargetEntityIds: new[] { 20, 30 }),
            });

            var requests = planner.BuildRequests(presentationData);

            Assert.That(requests, Is.Empty);
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
            GravityFieldVisualState[] gravityFieldVisualStates)
        {
            return CreatePresentationData(
                gravityFieldVisualStates,
                Array.Empty<GravityFieldPresentationEvent>());
        }

        private static TickPresentationData CreatePresentationData(
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return CreatePresentationData(
                Array.Empty<GravityFieldVisualState>(),
                gravityFieldEvents);
        }

        private static TickPresentationData CreatePresentationData(
            GravityFieldVisualState[] gravityFieldVisualStates,
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
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates);
        }
    }
}
