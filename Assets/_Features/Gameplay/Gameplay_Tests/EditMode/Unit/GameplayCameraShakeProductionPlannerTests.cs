using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayCameraShakeProductionPlannerTests
    {
        private static readonly SurfaceCell SourceCell = new(FaceId.Floor, 1, 0);
        private static readonly SurfaceCell ContactCell = new(FaceId.Floor, 2, 0);
        private static readonly CubeTopologyState Topology = new(FaceId.Floor);

        [Test]
        [Category("Extended")]
        public void PushSlideLaunch_ActiveCanonicalSignal_SubmitsLightRequestExactlyOnce()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            var signal = CreatePushSignal(actionPlanId: 71);

            Assert.That(
                planner.Present(
                    tickIndex: 19,
                    new[] { signal },
                    new FlipFloorImpactPresentationSignal[0],
                    HasExpectedActiveTrack,
                    HasExpectedTrack),
                Is.True);
            Assert.That(
                planner.Present(
                    tickIndex: 19,
                    new[] { signal },
                    new FlipFloorImpactPresentationSignal[0],
                    HasExpectedActiveTrack,
                    HasExpectedTrack),
                Is.False);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            var request = sink.Requests[0];
            Assert.That(request.TickIndex, Is.EqualTo(19));
            Assert.That(request.Semantic, Is.EqualTo(CameraShakeSemantic.PushSlideLaunch));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.SequenceOrActionPlanId, Is.EqualTo(71));
            Assert.That(request.Priority, Is.EqualTo(CameraShakePriority.Light));
            Assert.That(request.HasAnchor, Is.True);
            Assert.That(request.AnchorCell, Is.EqualTo(SourceCell));
            Assert.That(request.HasDirection, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PushInputWindupBlockedDestroyAndContinuation_WithoutActiveStartSignal_SubmitNothing()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);

            Assert.That(
                planner.Present(
                    tickIndex: 20,
                    new BoxSlideStartPresentationSignal[0],
                    new FlipFloorImpactPresentationSignal[0],
                    HasExpectedActiveTrack,
                    HasExpectedTrack),
                Is.False,
                "Input, windup, blocked, immediate destroy, and continued sliding have no canonical start signal.");
            Assert.That(
                planner.Present(
                    tickIndex: 21,
                    new[] { CreatePushSignal(actionPlanId: 72) },
                    new FlipFloorImpactPresentationSignal[0],
                    (_, _, _) => false,
                    HasExpectedTrack),
                Is.False,
                "A signal without an actually started visible BoxSlide track must not shake.");
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipLanding_ActualMotionCrossing_SubmitsMediumRequestExactlyOnce()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            var signal = CreateFlipSignal(
                FlipFloorImpactPresentationKind.Landing,
                actionPlanId: 91,
                contactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime);

            Assert.That(
                planner.Present(
                    tickIndex: 27,
                    new BoxSlideStartPresentationSignal[0],
                    new[] { signal },
                    HasExpectedActiveTrack,
                    HasExpectedTrack),
                Is.False,
                "Registering a landing milestone is not an impulse submission.");
            Assert.That(planner.PendingFlipLandingCount, Is.EqualTo(1));
            Assert.That(sink.Requests, Is.Empty);

            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0f, 0.9f, 91),
                }),
                Is.False);
            Assert.That(sink.Requests, Is.Empty);

            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0.9f, 0.97f, 91),
                }),
                Is.True);
            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0.97f, 1f, 91),
                }),
                Is.False);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            var request = sink.Requests[0];
            Assert.That(request.TickIndex, Is.EqualTo(27));
            Assert.That(request.Semantic, Is.EqualTo(CameraShakeSemantic.FlipFloorLanding));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.SequenceOrActionPlanId, Is.EqualTo(91));
            Assert.That(request.Priority, Is.EqualTo(CameraShakePriority.Medium));
            Assert.That(request.AnchorCell, Is.EqualTo(ContactCell));
            Assert.That(planner.PendingFlipLandingCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipLanding_LargeDeltaCrossing_DoesNotMissContact()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.Present(
                tickIndex: 28,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.Landing, actionPlanId: 92) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0.1f, 1f, 92),
                }),
                Is.True);
            Assert.That(sink.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void QueuedFlipLandings_ProgressIdentityOnlyConsumesMatchingActionTrack()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.Present(
                tickIndex: 32,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.Landing, actionPlanId: 95) },
                HasExpectedActiveTrack,
                HasExpectedTrack);
            planner.Present(
                tickIndex: 33,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.Landing, actionPlanId: 96) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            Assert.That(planner.PendingFlipLandingCount, Is.EqualTo(2));
            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0.9f, 0.97f, 95),
            });
            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            Assert.That(sink.Requests[0].SequenceOrActionPlanId, Is.EqualTo(95));
            Assert.That(planner.PendingFlipLandingCount, Is.EqualTo(1));

            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0.9f, 0.97f, 96),
            });
            Assert.That(sink.Requests, Has.Count.EqualTo(2));
            Assert.That(sink.Requests[1].SequenceOrActionPlanId, Is.EqualTo(96));
            Assert.That(planner.PendingFlipLandingCount, Is.Zero);
        }

        [TestCase(FlipFloorImpactPresentationKind.FollowThrough)]
        [TestCase(FlipFloorImpactPresentationKind.Stay)]
        [TestCase(FlipFloorImpactPresentationKind.DestroySelf)]
        [Category("Extended")]
        public void HostileFlipDisposition_DoesNotRegisterOrdinaryLanding(
            FlipFloorImpactPresentationKind kind)
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);

            planner.Present(
                tickIndex: 29,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateFlipSignal(kind, actionPlanId: 93) },
                HasExpectedActiveTrack,
                HasExpectedTrack);
            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0f, 1f, 93),
            });

            Assert.That(planner.PendingFlipLandingCount, Is.Zero);
            Assert.That(
                sink.Requests.Count(request => request.Semantic == CameraShakeSemantic.FlipFloorLanding),
                Is.Zero);
            Assert.That(
                sink.Requests.Count(request => request.Semantic == CameraShakeSemantic.FlipHostileImpact),
                Is.EqualTo(kind == FlipFloorImpactPresentationKind.FollowThrough ? 1 : 0));
        }

        [TestCase(
            FlipImpactPresentationDisposition.Stay,
            CameraShakeVariant.FlipHostileStay,
            (int)MotionTrackProgressSourceKind.OriginalViewMotion)]
        [TestCase(
            FlipImpactPresentationDisposition.DestroySelf,
            CameraShakeVariant.FlipHostileDestroySelf,
            (int)MotionTrackProgressSourceKind.FlipInteraction)]
        [Category("Core")]
        public void StayAndDestroySelf_CanonicalContactCrossing_SubmitTypedMediumExactlyOnce(
            FlipImpactPresentationDisposition disposition,
            CameraShakeVariant expectedVariant,
            int progressSourceValue)
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            const int actionPlanId = 131;
            var progressSource = (MotionTrackProgressSourceKind)progressSourceValue;

            planner.Present(
                tickIndex: 41,
                pushSlideStartSignals: new BoxSlideStartPresentationSignal[0],
                flipImpactSignals: new[] { CreateImpactSignal(disposition, actionPlanId) },
                flipFloorImpactSignals: new[]
                {
                    CreateFlipSignal(
                        disposition == FlipImpactPresentationDisposition.Stay
                            ? FlipFloorImpactPresentationKind.Stay
                            : FlipFloorImpactPresentationKind.DestroySelf,
                        actionPlanId),
                },
                hasActiveLocalMotionTrack: HasExpectedActiveTrack,
                hasLocalMotionTrack: HasExpectedTrack);

            Assert.That(planner.PendingFlipHostileImpactCount, Is.EqualTo(1));
            Assert.That(planner.PendingFlipLandingCount, Is.Zero);
            Assert.That(sink.Requests, Is.Empty);

            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    30,
                    TickEntityMotionKind.Flip,
                    0f,
                    0.61f,
                    actionPlanId,
                    progressSource),
            });
            Assert.That(sink.Requests, Is.Empty);

            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(
                        30,
                        TickEntityMotionKind.Flip,
                        0.61f,
                        0.8f,
                        actionPlanId,
                        progressSource),
                }),
                Is.True);
            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(
                        30,
                        TickEntityMotionKind.Flip,
                        0.8f,
                        1f,
                        actionPlanId,
                        progressSource),
                }),
                Is.False);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            Assert.That(sink.Requests[0].Semantic, Is.EqualTo(CameraShakeSemantic.FlipHostileImpact));
            Assert.That(sink.Requests[0].Variant, Is.EqualTo(expectedVariant));
            Assert.That(sink.Requests[0].Priority, Is.EqualTo(CameraShakePriority.Medium));
            Assert.That(sink.Requests[0].SequenceOrActionPlanId, Is.EqualTo(actionPlanId));
            Assert.That(planner.PendingFlipHostileImpactCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void Stay_LargeDeltaCanonicalContactCrossing_DoesNotMissImpact()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            const int actionPlanId = 132;
            planner.Present(
                42,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateImpactSignal(FlipImpactPresentationDisposition.Stay, actionPlanId) },
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.Stay, actionPlanId) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(
                        30,
                        TickEntityMotionKind.Flip,
                        0.1f,
                        1f,
                        actionPlanId,
                        MotionTrackProgressSourceKind.OriginalViewMotion),
                }),
                Is.True);
            Assert.That(sink.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void FollowThrough_InitialHostileContactDoesNotShake_FinalLandingSubmitsHeavyExactlyOnce()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            const int actionPlanId = 141;
            planner.Present(
                51,
                new BoxSlideStartPresentationSignal[0],
                new FlipImpactPresentationSignal[0],
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.FollowThrough, actionPlanId) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            Assert.That(planner.PendingFlipHostileImpactCount, Is.EqualTo(1));
            Assert.That(planner.PendingFlipLandingCount, Is.Zero);

            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    30,
                    TickEntityMotionKind.Flip,
                    0.5f,
                    0.7f,
                    actionPlanId,
                    MotionTrackProgressSourceKind.FlipInteraction),
            });
            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    30,
                    TickEntityMotionKind.Flip,
                    0.7f,
                    0.93f,
                    actionPlanId,
                    MotionTrackProgressSourceKind.LocalMotion),
            });
            Assert.That(sink.Requests, Is.Empty, "Initial hostile contact and pre-landing motion must stay silent.");

            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(
                        30,
                        TickEntityMotionKind.Flip,
                        0.93f,
                        1f,
                        actionPlanId,
                        MotionTrackProgressSourceKind.LocalMotion),
                }),
                Is.True);
            Assert.That(
                planner.ObserveMotionProgress(new[]
                {
                    new MotionTrackProgressSample(
                        30,
                        TickEntityMotionKind.Flip,
                        1f,
                        1f,
                        actionPlanId,
                        MotionTrackProgressSourceKind.LocalMotion),
                }),
                Is.False);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            Assert.That(sink.Requests[0].Semantic, Is.EqualTo(CameraShakeSemantic.FlipHostileImpact));
            Assert.That(sink.Requests[0].Variant, Is.EqualTo(CameraShakeVariant.FlipHostileFollowThrough));
            Assert.That(sink.Requests[0].Priority, Is.EqualTo(CameraShakePriority.Heavy));
            Assert.That(planner.PendingFlipHostileImpactCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void FollowThrough_LargeDeltaLandingCrossing_DoesNotMissOrDoubleFire()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            const int actionPlanId = 142;
            planner.Present(
                52,
                new BoxSlideStartPresentationSignal[0],
                new FlipImpactPresentationSignal[0],
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.FollowThrough, actionPlanId) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    30,
                    TickEntityMotionKind.Flip,
                    0.1f,
                    1f,
                    actionPlanId,
                    MotionTrackProgressSourceKind.LocalMotion),
            });
            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    30,
                    TickEntityMotionKind.Flip,
                    0.1f,
                    1f,
                    actionPlanId,
                    MotionTrackProgressSourceKind.LocalMotion),
            });

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            Assert.That(sink.Requests.Count(request =>
                request.Semantic == CameraShakeSemantic.FlipFloorLanding), Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void SameFlipAction_MutuallyExclusiveHostileOutcomes_FailFast()
        {
            var planner = new GameplayCameraShakeProductionPlanner(new RecordingSink());
            const int actionPlanId = 151;
            planner.Present(
                61,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateImpactSignal(FlipImpactPresentationDisposition.Stay, actionPlanId) },
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.Stay, actionPlanId) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            Assert.Throws<System.InvalidOperationException>(() => planner.Present(
                61,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateImpactSignal(FlipImpactPresentationDisposition.DestroySelf, actionPlanId) },
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.DestroySelf, actionPlanId) },
                HasExpectedActiveTrack,
                HasExpectedTrack));
        }

        [Test]
        [Category("Core")]
        public void ResetBeforeHostileMilestone_CancelsPendingWithoutStaleReplay()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            const int actionPlanId = 161;
            planner.Present(
                71,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateImpactSignal(FlipImpactPresentationDisposition.DestroySelf, actionPlanId) },
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.DestroySelf, actionPlanId) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            planner.HardCleanup();
            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    30,
                    TickEntityMotionKind.Flip,
                    0f,
                    1f,
                    actionPlanId,
                    MotionTrackProgressSourceKind.FlipInteraction),
            });

            Assert.That(planner.PendingFlipHostileImpactCount, Is.Zero);
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void FlipInputWindupBlockedAndRotationStart_DoNotSubmitBeforeLandingSignalAndContact()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);

            planner.Present(
                tickIndex: 30,
                new BoxSlideStartPresentationSignal[0],
                new FlipFloorImpactPresentationSignal[0],
                HasExpectedActiveTrack,
                HasExpectedTrack);
            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0f, 0.8f),
            });

            Assert.That(planner.PendingFlipLandingCount, Is.Zero);
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void ResetBeforeFlipContact_CancelsPendingMilestoneWithoutStaleReplay()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.Present(
                tickIndex: 31,
                new BoxSlideStartPresentationSignal[0],
                new[] { CreateFlipSignal(FlipFloorImpactPresentationKind.Landing, actionPlanId: 94) },
                HasExpectedActiveTrack,
                HasExpectedTrack);

            planner.ResetSession();
            planner.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(30, TickEntityMotionKind.Flip, 0f, 1f, 94),
            });

            Assert.That(planner.PendingFlipLandingCount, Is.Zero);
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerDamage_AcceptedAggregatedOutcome_SubmitsOneLightRequestAtMostOncePerTick()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            var signals = new[]
            {
                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 3),
                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 3),
            };

            Assert.That(
                planner.PresentPlayerImpacts(
                    tickIndex: 81,
                    damageSignals: signals,
                    deathSignals: System.Array.Empty<TickPlayerDeathPresentationSignal>()),
                Is.True);
            Assert.That(
                planner.PresentPlayerImpacts(
                    tickIndex: 81,
                    damageSignals: signals,
                    deathSignals: System.Array.Empty<TickPlayerDeathPresentationSignal>()),
                Is.False);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            var request = sink.Requests.Single();
            Assert.That(request.TickIndex, Is.EqualTo(81));
            Assert.That(request.Semantic, Is.EqualTo(CameraShakeSemantic.PlayerDamageImpact));
            Assert.That(request.Variant, Is.EqualTo(CameraShakeVariant.Default));
            Assert.That(request.Priority, Is.EqualTo(CameraShakePriority.Light));
            Assert.That(request.SourceEntityId, Is.EqualTo(10));
            Assert.That(request.SequenceOrActionPlanId, Is.EqualTo(81));
            Assert.That(request.HasAnchor, Is.False);
            Assert.That(request.HasDirection, Is.False);
            Assert.That(planner.ObservedPlayerDamageImpactCount, Is.EqualTo(1));
            Assert.That(planner.ObservedPlayerLethalImpactCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PlayerDamage_NoAcceptedPositiveOutcome_SubmitsNothing()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);

            Assert.That(
                planner.PresentPlayerImpacts(
                    tickIndex: 82,
                    damageSignals: new[]
                    {
                        new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: false, damageAmount: 1),
                        new TickPlayerDamagePresentationSignal(11, tookDamageThisTick: true, damageAmount: 0),
                    },
                    deathSignals: System.Array.Empty<TickPlayerDeathPresentationSignal>()),
                Is.False);
            Assert.That(sink.Requests, Is.Empty);
        }

        [TestCase(true, false, 1, 0)]
        [TestCase(false, true, 0, 1)]
        [TestCase(true, true, 0, 1)]
        [Category("Core")]
        public void PlayerLethalPromotion_TypedFactsProduceExactlyOneMutuallyExclusiveImpact(
            bool includeDamage,
            bool includeDeath,
            int expectedDamageCount,
            int expectedLethalCount)
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            var damageSignals = includeDamage
                ? new[] { new TickPlayerDamagePresentationSignal(10, true, 4) }
                : System.Array.Empty<TickPlayerDamagePresentationSignal>();
            var deathSignals = includeDeath
                ? new[] { CreatePlayerDeathSignal(10) }
                : System.Array.Empty<TickPlayerDeathPresentationSignal>();

            Assert.That(
                planner.PresentPlayerImpacts(83, damageSignals, deathSignals),
                Is.True);
            Assert.That(
                sink.Requests.Count(request =>
                    request.Semantic == CameraShakeSemantic.PlayerDamageImpact),
                Is.EqualTo(expectedDamageCount));
            Assert.That(
                sink.Requests.Count(request =>
                    request.Semantic == CameraShakeSemantic.PlayerLethalImpact),
                Is.EqualTo(expectedLethalCount));
            Assert.That(sink.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void PlayerDeath_DuplicateSignalAndRefresh_SubmitsOneHeavyLethalRequest()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            var signals = new[]
            {
                CreatePlayerDeathSignal(10),
                CreatePlayerDeathSignal(10),
            };

            Assert.That(
                planner.PresentPlayerImpacts(
                    84,
                    System.Array.Empty<TickPlayerDamagePresentationSignal>(),
                    signals),
                Is.True);
            Assert.That(
                planner.PresentPlayerImpacts(
                    84,
                    System.Array.Empty<TickPlayerDamagePresentationSignal>(),
                    signals),
                Is.False);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            var request = sink.Requests.Single();
            Assert.That(request.Semantic, Is.EqualTo(CameraShakeSemantic.PlayerLethalImpact));
            Assert.That(request.Priority, Is.EqualTo(CameraShakePriority.Heavy));
            Assert.That(request.SourceEntityId, Is.EqualTo(10));
            Assert.That(request.SequenceOrActionPlanId, Is.EqualTo(84));
            Assert.That(request.HasDirection, Is.False);
            Assert.That(planner.ObservedPlayerDamageImpactCount, Is.Zero);
            Assert.That(planner.ObservedPlayerLethalImpactCount, Is.EqualTo(1));
        }

        [TestCase(TickEnemyJumpPresentationOutcome.WindupStarted)]
        [TestCase(TickEnemyJumpPresentationOutcome.AirborneStarted)]
        [TestCase(TickEnemyJumpPresentationOutcome.Retried)]
        [Category("Core")]
        public void HeavyEnemyJump_NonLandingPhase_DoesNotRegisterOrSubmit(
            TickEnemyJumpPresentationOutcome outcome)
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);

            Assert.That(
                planner.RegisterHeavyEnemyJumpLandings(
                    201,
                    new[] { CreateJumpSignal(outcome, entityId: 40, sequence: 11) },
                    _ => EnemyJumpLandingCameraFeedbackKind.Heavy),
                Is.False);
            Assert.That(planner.PendingHeavyEnemyJumpLandingCount, Is.Zero);
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void HeavyEnemyJumpLanded_VisualCompletionCrossing_SubmitsDefaultMediumExactlyOnce()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);

            Assert.That(
                planner.RegisterHeavyEnemyJumpLandings(
                    202,
                    new[] { CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 12) },
                    _ => EnemyJumpLandingCameraFeedbackKind.Heavy),
                Is.True);
            Assert.That(planner.PendingHeavyEnemyJumpLandingCount, Is.EqualTo(1));
            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => true,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: false),
                Is.False,
                "The canonical semantic must remain pending while its visible landing hold is active.");
            Assert.That(sink.Requests, Is.Empty);

            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: false),
                Is.True,
                "A large presentation delta that completes the hold must not miss the landing milestone.");
            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: false),
                Is.False);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            var request = sink.Requests.Single();
            Assert.That(request.TickIndex, Is.EqualTo(202));
            Assert.That(request.Semantic, Is.EqualTo(CameraShakeSemantic.HeavyEnemyJumpLanding));
            Assert.That(request.Variant, Is.EqualTo(CameraShakeVariant.Default));
            Assert.That(request.Priority, Is.EqualTo(CameraShakePriority.Medium));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.SequenceOrActionPlanId, Is.EqualTo(12));
            Assert.That(request.AnchorCell, Is.EqualTo(ContactCell));
            Assert.That(planner.PendingHeavyEnemyJumpLandingCount, Is.Zero);
            Assert.That(planner.ObservedHeavyEnemyJumpLandingCount, Is.EqualTo(1));
            Assert.That(planner.AcceptedHeavyEnemyJumpLandingCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void HeavyEnemyJumpCrushedBoxAndLanded_UsesSameDefaultSemanticWithoutSecondImpact()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            var signal = CreateJumpSignal(
                TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded,
                entityId: 40,
                sequence: 13);

            Assert.That(
                planner.RegisterHeavyEnemyJumpLandings(
                    203,
                    new[] { signal, signal },
                    _ => EnemyJumpLandingCameraFeedbackKind.Heavy),
                Is.True);
            planner.ObserveHeavyEnemyJumpLandingMilestones(
                _ => false,
                _ => Vector3.zero,
                new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                isTopologyTransitionActive: false);

            Assert.That(sink.Requests, Has.Count.EqualTo(1));
            Assert.That(sink.Requests.Single().Semantic, Is.EqualTo(CameraShakeSemantic.HeavyEnemyJumpLanding));
            Assert.That(sink.Requests.Single().Variant, Is.EqualTo(CameraShakeVariant.Default));
            Assert.That(
                sink.Requests.Count(request => request.Semantic != CameraShakeSemantic.HeavyEnemyJumpLanding),
                Is.Zero,
                "Crushing a box has no additional camera semantic in M3-B.");
        }

        [Test]
        [Category("Core")]
        public void JumpLandingEligibility_NonHeavyAndMissingAuthoring_AreSafeNoOps()
        {
            var signal = CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 14);
            foreach (var feedback in new[]
                     {
                         EnemyJumpLandingCameraFeedbackKind.None,
                         (EnemyJumpLandingCameraFeedbackKind)0,
                     })
            {
                var sink = new RecordingSink();
                var planner = new GameplayCameraShakeProductionPlanner(sink);
                Assert.That(
                    planner.RegisterHeavyEnemyJumpLandings(204, new[] { signal }, _ => feedback),
                    Is.False);
                Assert.That(sink.Requests, Is.Empty);
            }

            var missingResolverPlanner = new GameplayCameraShakeProductionPlanner(new RecordingSink());
            Assert.That(
                missingResolverPlanner.RegisterHeavyEnemyJumpLandings(204, new[] { signal }, null),
                Is.False);
        }

        [TestCase(0.5f, 0.5f, 2f, true, TestName = "HeavyLandingVisibility_CenterVisible_Submits")]
        [TestCase(1.05f, 0.5f, 2f, true, TestName = "HeavyLandingVisibility_EdgeAtMargin_Submits")]
        [TestCase(1.051f, 0.5f, 2f, false, TestName = "HeavyLandingVisibility_OutsideMargin_Drops")]
        [TestCase(0.5f, 0.5f, -0.01f, false, TestName = "HeavyLandingVisibility_BehindCamera_Drops")]
        [Category("Core")]
        public void HeavyEnemyJumpLanding_VisibilityPolicy(
            float viewportX,
            float viewportY,
            float viewportZ,
            bool expectedSubmit)
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.RegisterHeavyEnemyJumpLandings(
                205,
                new[] { CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 15) },
                _ => EnemyJumpLandingCameraFeedbackKind.Heavy);

            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(viewportX, viewportY, viewportZ)),
                    isTopologyTransitionActive: false),
                Is.EqualTo(expectedSubmit));
            Assert.That(sink.Requests.Count, Is.EqualTo(expectedSubmit ? 1 : 0));
            Assert.That(planner.OffscreenHeavyEnemyJumpLandingCount, Is.EqualTo(expectedSubmit ? 0 : 1));
            Assert.That(planner.PendingHeavyEnemyJumpLandingCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void HeavyEnemyJumpLanding_MissingLiveAnchor_DropsWithoutFallbackOrReplay()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.RegisterHeavyEnemyJumpLandings(
                206,
                new[] { CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 16) },
                _ => EnemyJumpLandingCameraFeedbackKind.Heavy);

            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => null,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: false),
                Is.False);
            Assert.That(planner.MissingAnchorHeavyEnemyJumpLandingCount, Is.EqualTo(1));
            Assert.That(planner.PendingHeavyEnemyJumpLandingCount, Is.Zero);
            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: false),
                Is.False);
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void HeavyEnemyJumpLanding_TopologyOverlap_SubmitsToCommonMixerAndDoesNotReplay()
        {
            var sink = new RecordingSink(acceptRequests: false);
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.RegisterHeavyEnemyJumpLandings(
                207,
                new[] { CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 17) },
                _ => EnemyJumpLandingCameraFeedbackKind.Heavy);

            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: true),
                Is.False);
            Assert.That(planner.TopologySuppressedHeavyEnemyJumpLandingCount, Is.EqualTo(1));
            Assert.That(planner.PendingHeavyEnemyJumpLandingCount, Is.Zero);
            Assert.That(
                sink.Requests,
                Has.Count.EqualTo(1),
                "A visible completed landing must reach the common mixer for topology arbitration.");
            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: false),
                Is.False);
            Assert.That(sink.Requests, Has.Count.EqualTo(1), "Suppressed landings must not replay.");
        }

        [Test]
        [Category("Core")]
        public void HeavyEnemyJumpLanding_MultipleVisibleEnemies_UseCanonicalIdentityOrdering()
        {
            var forward = ObserveOrderedJumpLandings(new[]
            {
                CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 22),
                CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 41, 21),
            });
            var reverse = ObserveOrderedJumpLandings(new[]
            {
                CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 41, 21),
                CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 22),
            });

            CollectionAssert.AreEqual(new[] { 40, 41 }, forward);
            CollectionAssert.AreEqual(forward, reverse);
        }

        [Test]
        [Category("Core")]
        public void HeavyEnemyJumpLanding_HardCleanupBeforeCompletion_CancelsPendingCue()
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.RegisterHeavyEnemyJumpLandings(
                209,
                new[] { CreateJumpSignal(TickEnemyJumpPresentationOutcome.Landed, 40, 18) },
                _ => EnemyJumpLandingCameraFeedbackKind.Heavy);

            planner.HardCleanup();
            Assert.That(planner.PendingHeavyEnemyJumpLandingCount, Is.Zero);
            Assert.That(
                planner.ObserveHeavyEnemyJumpLandingMilestones(
                    _ => false,
                    _ => Vector3.zero,
                    new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                    isTopologyTransitionActive: false),
                Is.False);
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void MotionTrack_ProgressSampleUsesActualClipAdvanceAndSurvivesLargeDeltaCompletion()
        {
            var track = new MotionTrack();
            var pose = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            track.Append(MotionClip.Create(
                TickEntityMotionKind.Flip,
                pose,
                new GameplayEntityPose(Vector3.right, Quaternion.identity),
                durationSeconds: 0.2f,
                interpolateRotation: false,
                flipPeakHeightWorld: 1f,
                sequenceOrActionPlanId: 101));

            track.SampleAndAdvance(
                deltaTime: 0.19f,
                pose,
                out _,
                out var firstProgress);
            Assert.That(firstProgress.PreviousNormalizedTime, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(firstProgress.CurrentNormalizedTime, Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(firstProgress.SequenceOrActionPlanId, Is.EqualTo(101));

            track.SampleAndAdvance(
                deltaTime: 0.2f,
                pose,
                out _,
                out var completionProgress);
            Assert.That(completionProgress.PreviousNormalizedTime, Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(completionProgress.CurrentNormalizedTime, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(track.HasClips, Is.False);
        }

        private static bool HasExpectedActiveTrack(
            int entityId,
            TickEntityMotionKind motionKind,
            int sequenceOrActionPlanId)
        {
            return HasExpectedTrack(entityId, motionKind, sequenceOrActionPlanId);
        }

        private static bool HasExpectedTrack(
            int entityId,
            TickEntityMotionKind motionKind,
            int sequenceOrActionPlanId)
        {
            return entityId == 30 &&
                   sequenceOrActionPlanId > 0 &&
                   (motionKind == TickEntityMotionKind.BoxSlide || motionKind == TickEntityMotionKind.Flip);
        }

        private static BoxSlideStartPresentationSignal CreatePushSignal(int actionPlanId)
        {
            return new BoxSlideStartPresentationSignal(
                boxEntityId: 30,
                actorEntityId: 10,
                SourceCell,
                ContactCell,
                Topology,
                actionPlanId);
        }

        private static FlipFloorImpactPresentationSignal CreateFlipSignal(
            FlipFloorImpactPresentationKind kind,
            int actionPlanId,
            float contactNormalizedTime = GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime)
        {
            return new FlipFloorImpactPresentationSignal(
                sourceActionPlanId: actionPlanId,
                boxEntityId: 30,
                actorEntityId: 10,
                SourceCell,
                ContactCell,
                Topology,
                sourceFacing: Direction.Right,
                contactFacing: Direction.Right,
                kind,
                contactNormalizedTime);
        }

        private static FlipImpactPresentationSignal CreateImpactSignal(
            FlipImpactPresentationDisposition disposition,
            int actionPlanId)
        {
            return new FlipImpactPresentationSignal(
                sourceActionPlanId: actionPlanId,
                boxEntityId: 30,
                impactTargetEntityId: 40,
                actorEntityId: 10,
                SourceCell,
                ContactCell,
                Topology,
                sourceFacing: Direction.Right,
                impactFacing: Direction.Right,
                disposition);
        }

        private static TickPlayerDeathPresentationSignal CreatePlayerDeathSignal(int entityId)
        {
            return new TickPlayerDeathPresentationSignal(
                entityId,
                didDieThisTick: true,
                sourceEntityId: 40,
                fallbackFacing: Direction.Right,
                resolvedDamageSourceAvailable: true,
                damageAmountAtFatalHit: 1,
                deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse);
        }

        private static TickEnemyJumpPresentationSignal CreateJumpSignal(
            TickEnemyJumpPresentationOutcome outcome,
            int entityId,
            int sequence)
        {
            return new TickEnemyJumpPresentationSignal(
                entityId,
                sequence,
                outcome == TickEnemyJumpPresentationOutcome.Landed ||
                outcome == TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded
                    ? EnemyJumpPhase.Cooldown
                    : outcome == TickEnemyJumpPresentationOutcome.AirborneStarted
                        ? EnemyJumpPhase.Airborne
                        : EnemyJumpPhase.Windup,
                startedWindupThisTick: outcome == TickEnemyJumpPresentationOutcome.WindupStarted,
                startedAirborneThisTick: outcome == TickEnemyJumpPresentationOutcome.AirborneStarted,
                landedThisTick: outcome == TickEnemyJumpPresentationOutcome.Landed,
                retryThisTick: outcome == TickEnemyJumpPresentationOutcome.Retried,
                SourceCell,
                ContactCell,
                ContactCell,
                Direction.Right,
                windupTicks: 1,
                landingTick: 202,
                remainingAirborneTicks: 0,
                retryCount: 0,
                outcome);
        }

        private static int[] ObserveOrderedJumpLandings(
            IReadOnlyList<TickEnemyJumpPresentationSignal> signals)
        {
            var sink = new RecordingSink();
            var planner = new GameplayCameraShakeProductionPlanner(sink);
            planner.RegisterHeavyEnemyJumpLandings(
                208,
                signals,
                _ => EnemyJumpLandingCameraFeedbackKind.Heavy);
            planner.ObserveHeavyEnemyJumpLandingMilestones(
                _ => false,
                _ => Vector3.zero,
                new FixedVisibilityPort(new Vector3(0.5f, 0.5f, 2f)),
                isTopologyTransitionActive: false);
            return sink.Requests.Select(request => request.SourceEntityId).ToArray();
        }

        private sealed class FixedVisibilityPort : IGameplayCameraVisibilityPort
        {
            private readonly Vector3 _viewportPoint;

            public FixedVisibilityPort(Vector3 viewportPoint)
            {
                _viewportPoint = viewportPoint;
            }

            public bool TryProjectUnshakenWorldPoint(Vector3 worldPosition, out Vector3 viewportPoint)
            {
                viewportPoint = _viewportPoint;
                return true;
            }
        }

        private sealed class RecordingSink : ICameraShakeImpulseSink
        {
            private readonly bool _acceptRequests;

            public RecordingSink(bool acceptRequests = true)
            {
                _acceptRequests = acceptRequests;
            }

            public List<CameraShakeImpulseRequest> Requests { get; } = new();

            public bool Submit(in CameraShakeImpulseRequest request)
            {
                Requests.Add(request);
                return _acceptRequests;
            }
        }
    }
}
