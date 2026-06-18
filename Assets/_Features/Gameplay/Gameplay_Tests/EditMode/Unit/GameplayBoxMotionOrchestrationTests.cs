using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayBoxMotionOrchestrationTests
    {
        [Test]
        [Category("Core")]
        public void BoxMotionFactExtraction_ObservesSlideAndFlipAsSemanticMotionFacts()
        {
            var result = CreateBoxMotionTickResult();
            var frame = new TickPresentationFactExtractor().Extract(result);

            var slideFact = frame.Facts.Single(fact =>
                fact.Source.SemanticSource == PresentationSemanticSource.BoxSlideMotion);
            var flipFact = frame.Facts.Single(fact =>
                fact.Source.SemanticSource == PresentationSemanticSource.BoxFlipMotion);

            Assert.That(slideFact.Kind, Is.EqualTo(PresentationFactKind.Movement));
            Assert.That(slideFact.Target.Kind, Is.EqualTo(PresentationTargetKind.Entity));
            Assert.That(slideFact.Target.EntityId, Is.EqualTo(BoxEntityId));
            Assert.That(slideFact.Source.TickIndex, Is.EqualTo(11));
            Assert.That(slideFact.MotionPayload.Kind, Is.EqualTo(PresentationMotionFactKind.BoxSlide));
            Assert.That(slideFact.MotionPayload.EntityId, Is.EqualTo(BoxEntityId));
            Assert.That(slideFact.MotionPayload.ActorEntityId, Is.EqualTo(PlayerEntityId));
            Assert.That(slideFact.MotionPayload.ActionKind, Is.EqualTo(PresentationMotionActionKind.Push));
            Assert.That(slideFact.MotionPayload.SourceCell, Is.EqualTo(SlideSourceCell));
            Assert.That(slideFact.MotionPayload.DestinationCell, Is.EqualTo(SlideDestinationCell));
            Assert.That(slideFact.MotionPayload.Topology, Is.EqualTo(Topology));
            Assert.That(slideFact.MotionPayload.HasTopology, Is.True);

            Assert.That(flipFact.Kind, Is.EqualTo(PresentationFactKind.Movement));
            Assert.That(flipFact.Target.Kind, Is.EqualTo(PresentationTargetKind.Entity));
            Assert.That(flipFact.Target.EntityId, Is.EqualTo(BoxEntityId));
            Assert.That(flipFact.Source.TickIndex, Is.EqualTo(11));
            Assert.That(flipFact.MotionPayload.Kind, Is.EqualTo(PresentationMotionFactKind.BoxFlip));
            Assert.That(flipFact.MotionPayload.EntityId, Is.EqualTo(BoxEntityId));
            Assert.That(flipFact.MotionPayload.ActionKind, Is.EqualTo(PresentationMotionActionKind.Flip));
            Assert.That(flipFact.MotionPayload.SourceCell, Is.EqualTo(FlipSourceCell));
            Assert.That(flipFact.MotionPayload.DestinationCell, Is.EqualTo(FlipDestinationCell));
            Assert.That(typeof(PresentationMotionPayload).AssemblyQualifiedName, Does.Not.Contain("UnityEngine"));
        }

        [Test]
        [Category("Core")]
        public void MotionCuePlanner_UsesTypedMotionCueKeysAndSymbolicAnchors()
        {
            var cueFrame = CreateMotionCueFrame(CreateBoxMotionTickResult());

            var slideCue = cueFrame.Cues.Single(cue =>
                cue.Key.TryGetMotionCueKey(out var key) && key == PresentationMotionCueKey.BoxSlide);
            var flipCue = cueFrame.Cues.Single(cue =>
                cue.Key.TryGetMotionCueKey(out var key) && key == PresentationMotionCueKey.BoxFlip);

            Assert.That(slideCue.Domain, Is.EqualTo(PresentationDomain.Motion));
            Assert.That(slideCue.Key.Domain, Is.EqualTo(PresentationDomain.Motion));
            Assert.That(slideCue.Target, Is.EqualTo(PresentationTarget.Entity(BoxEntityId)));
            Assert.That(slideCue.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
            Assert.That(slideCue.Anchor.Target, Is.EqualTo(PresentationTarget.Entity(BoxEntityId)));
            Assert.That(slideCue.PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.Track));
            Assert.That(slideCue.PolicyHint.Blocking, Is.False);
            Assert.That(slideCue.PolicyHint.DedupeKey, Is.GreaterThan(0));

            Assert.That(flipCue.Domain, Is.EqualTo(PresentationDomain.Motion));
            Assert.That(flipCue.Key.Domain, Is.EqualTo(PresentationDomain.Motion));
            Assert.That(flipCue.Target, Is.EqualTo(PresentationTarget.Entity(BoxEntityId)));
            Assert.That(flipCue.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
            Assert.That(flipCue.PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.Track));
            Assert.That(flipCue.PolicyHint.Blocking, Is.False);
        }

        [Test]
        [Category("Core")]
        public void MotionPlaybackPlanner_CreatesNonBlockingTracksWithoutBarriers()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateMotionCueFrame(CreateBoxMotionTickResult()));
            var scheduler = new PresentationPlaybackScheduler();

            scheduler.Accept(plan);

            Assert.That(plan.Tracks.Count(track => track.Cue.Domain == PresentationDomain.Motion), Is.EqualTo(2));
            Assert.That(plan.Cues.Any(cue => cue.Cue.Domain == PresentationDomain.Motion), Is.False);
            Assert.That(plan.Barriers.Any(barrier => barrier.OwnerDomain == PresentationDomain.Motion), Is.False);
            Assert.That(plan.Tracks.All(track => !track.Policy.Blocking), Is.True);
            Assert.That(plan.Tracks.All(track => track.Policy.UnitKind == PresentationPlaybackUnitKind.Track), Is.True);
            Assert.That(plan.Tracks.All(track => track.Policy.InterruptMode == PresentationPlaybackInterruptMode.IgnoreNew), Is.True);
            Assert.That(plan.Tracks.Select(track => track.Policy.DedupeKey).Distinct().Count(), Is.EqualTo(2));
            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void MotionExecutor_DefaultLegacyMode_DoesNotCallPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateMotionCueFrame(CreateBoxMotionTickResult()));
            var port = new RecordingGameplayMotionPlaybackPort();
            var executor = new GameplayMotionPresentationExecutor(port);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.Zero);
            Assert.That(executor.Diagnostics.ObservedTrackCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.LegacyOwnerNoOpCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.Zero);
            Assert.That(executor.Diagnostics.DuplicateSuppressedCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void MotionExecutor_InvalidMode_NormalizesToLegacyAndDoesNotCallPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateMotionCueFrame(CreateBoxMotionTickResult()));
            var port = new RecordingGameplayMotionPlaybackPort();
            var executor = new GameplayMotionPresentationExecutor(
                port,
                (BoxMotionPresentationExecutionMode)999);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.Zero);
            Assert.That(executor.Diagnostics.ObservedTrackCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.LegacyOwnerNoOpCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void MotionExecutor_OrchestrationMode_RoutesSlideAndFlipToPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateMotionCueFrame(CreateBoxMotionTickResult()));
            var port = new RecordingGameplayMotionPlaybackPort();
            var guard = new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor);
            var executor = new GameplayMotionPresentationExecutor(
                port,
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                guard);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.EqualTo(2));
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationMotionCueKey.BoxSlide &&
                request.EntityId == BoxEntityId &&
                request.MotionPayload.SourceCell.Equals(SlideSourceCell) &&
                request.MotionPayload.DestinationCell.Equals(SlideDestinationCell) &&
                request.TickIndex == 11 &&
                request.Anchor.Kind == PresentationAnchorKind.EntityVisualRoot), Is.True);
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationMotionCueKey.BoxFlip &&
                request.EntityId == BoxEntityId &&
                request.MotionPayload.SourceCell.Equals(FlipSourceCell) &&
                request.MotionPayload.DestinationCell.Equals(FlipDestinationCell) &&
                request.TickIndex == 11 &&
                request.Anchor.Kind == PresentationAnchorKind.EntityVisualRoot), Is.True);
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.TrackStartedCount, Is.EqualTo(2));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(2));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
            Assert.That(plan.Tracks.All(track => !track.Policy.Blocking), Is.True);
            Assert.That(plan.Tracks.All(track =>
                track.Policy.InterruptMode == PresentationPlaybackInterruptMode.IgnoreNew), Is.True);
        }

        [Test]
        [Category("Core")]
        public void LegacyAndOrchestrationSemanticMotionRequests_AreEquivalent()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateMotionCueFrame(CreateBoxMotionTickResult()));
            var port = new RecordingGameplayMotionPlaybackPort();
            var executor = new GameplayMotionPresentationExecutor(
                port,
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));

            executor.Play(plan);

            var slideTrack = plan.Tracks.Single(track =>
                track.Cue.Key.TryGetMotionCueKey(out var key) && key == PresentationMotionCueKey.BoxSlide);
            var flipTrack = plan.Tracks.Single(track =>
                track.Cue.Key.TryGetMotionCueKey(out var key) && key == PresentationMotionCueKey.BoxFlip);
            var slideRequest = port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxSlide);
            var flipRequest = port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxFlip);

            Assert.That(slideRequest.MotionPayload.EntityId, Is.EqualTo(slideTrack.Cue.MotionPayload.EntityId));
            Assert.That(slideRequest.MotionPayload.Direction, Is.EqualTo(slideTrack.Cue.MotionPayload.Direction));
            Assert.That(slideRequest.MotionPayload.SourceCell, Is.EqualTo(slideTrack.Cue.MotionPayload.SourceCell));
            Assert.That(slideRequest.MotionPayload.DestinationCell, Is.EqualTo(slideTrack.Cue.MotionPayload.DestinationCell));
            Assert.That(flipRequest.MotionPayload.EntityId, Is.EqualTo(flipTrack.Cue.MotionPayload.EntityId));
            Assert.That(flipRequest.MotionPayload.SourceCell, Is.EqualTo(flipTrack.Cue.MotionPayload.SourceCell));
            Assert.That(flipRequest.MotionPayload.DestinationCell, Is.EqualTo(flipTrack.Cue.MotionPayload.DestinationCell));
            Assert.That(flipRequest.MotionPayload.FlipDisposition, Is.EqualTo(flipTrack.Cue.MotionPayload.FlipDisposition));
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_LegacyAndOrchestrationSemanticEquivalence()
        {
            var cueFrame = CreateMotionCueFrame(CreateBoxMotionTickResultWithImpact());
            var plan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var port = new RecordingGameplayMotionPlaybackPort();
            var executor = new GameplayMotionPresentationExecutor(
                port,
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));

            executor.Play(plan);

            Assert.That(port.Requests, Has.Length.EqualTo(3));
            AssertEquivalentMotionRequest(
                plan.Tracks.Single(track => IsMotionTrack(track, PresentationMotionCueKey.BoxSlide)),
                port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxSlide));
            AssertEquivalentMotionRequest(
                plan.Tracks.Single(track => IsMotionTrack(track, PresentationMotionCueKey.BoxFlip)),
                port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxFlip));
            AssertEquivalentMotionRequest(
                plan.Tracks.Single(track => IsMotionTrack(track, PresentationMotionCueKey.BoxFlipImpact)),
                port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxFlipImpact));
            Assert.That(plan.Tracks.All(track => track.Policy.UnitKind == PresentationPlaybackUnitKind.Track), Is.True);
            Assert.That(plan.Tracks.All(track => !track.Policy.Blocking), Is.True);
            Assert.That(plan.Tracks.All(track =>
                track.Policy.InterruptMode == PresentationPlaybackInterruptMode.IgnoreNew), Is.True);
        }

        [Test]
        [Category("Core")]
        public void MotionExecutionGuard_BlocksDuplicateOwnerAttemptForSameBoxMotionKey()
        {
            var key = new BoxMotionPlaybackKey(
                11,
                PresentationSemanticSource.BoxSlideMotion,
                BoxEntityId,
                PresentationMotionCueKey.BoxSlide,
                SlideSourceCell,
                SlideDestinationCell,
                sourceActionPlanId: 0,
                sourceSequenceId: 0);
            var guard = new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor);

            Assert.That(
                guard.TryBeginExecution(BoxMotionPresentationExecutionOwner.OrchestrationMotionExecutor, key),
                Is.True);
            Assert.That(
                guard.TryBeginExecution(BoxMotionPresentationExecutionOwner.LegacyTrackPlanner, key),
                Is.False);

            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void MotionExecutor_DistinguishesMissingTargetAnchorBindingAndDriver()
        {
            var validCue = CreateMotionCueFrame(CreateBoxMotionTickResult())
                .Cues.Single(cue =>
                    cue.Key.TryGetMotionCueKey(out var key) && key == PresentationMotionCueKey.BoxSlide);
            var targetMissingCue = new PresentationCue(
                PresentationDomain.Motion,
                PresentationCueKey.ForMotion(PresentationMotionCueKey.BoxSlide),
                validCue.Source,
                PresentationTarget.None(),
                validCue.Anchor,
                validCue.PolicyHint,
                validCue.TopologyPayload,
                validCue.MotionPayload);
            var anchorMissingCue = new PresentationCue(
                PresentationDomain.Motion,
                PresentationCueKey.ForMotion(PresentationMotionCueKey.BoxSlide),
                new PresentationSource(12, PresentationSemanticSource.BoxSlideMotion, BoxEntityId),
                PresentationTarget.Entity(BoxEntityId),
                PresentationAnchor.None(),
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.Track,
                    blocking: false,
                    dedupeKey: 1234),
                validCue.TopologyPayload,
                validCue.MotionPayload);
            var driverMissingCue = new PresentationCue(
                PresentationDomain.Motion,
                PresentationCueKey.ForMotion(PresentationMotionCueKey.BoxFlip),
                new PresentationSource(13, PresentationSemanticSource.BoxFlipMotion, BoxEntityId),
                PresentationTarget.Entity(BoxEntityId),
                validCue.Anchor,
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.Track,
                    blocking: false,
                    dedupeKey: 5678),
                validCue.TopologyPayload,
                validCue.MotionPayload);
            var frame = new PresentationCueFrame(
                13,
                new[] { targetMissingCue, anchorMissingCue, validCue, driverMissingCue },
                new PresentationCueFrameDiagnostics(4, 4, 0));
            var plan = new PresentationPlaybackPlanner().Plan(frame);
            var port = new RecordingGameplayMotionPlaybackPort(request =>
                request.CueKey == PresentationMotionCueKey.BoxFlip
                    ? GameplayMotionPlaybackResultKind.DriverMissing
                    : GameplayMotionPlaybackResultKind.BindingMissing);
            var executor = new GameplayMotionPresentationExecutor(
                port,
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));

            executor.Play(plan);

            Assert.That(executor.Diagnostics.TargetMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.AnchorMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.BindingMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.DriverMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(2));
            Assert.That(port.TryPlayCallCount, Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_MissingDiagnosticsSeparated()
        {
            var validCue = CreateMotionCueFrame(CreateBoxMotionTickResult())
                .Cues.Single(cue =>
                    cue.Key.TryGetMotionCueKey(out var key) && key == PresentationMotionCueKey.BoxSlide);
            var targetMissingCue = new PresentationCue(
                PresentationDomain.Motion,
                PresentationCueKey.ForMotion(PresentationMotionCueKey.BoxSlide),
                validCue.Source,
                PresentationTarget.None(),
                validCue.Anchor,
                validCue.PolicyHint,
                motionPayload: validCue.MotionPayload);
            var anchorMissingCue = new PresentationCue(
                PresentationDomain.Motion,
                PresentationCueKey.ForMotion(PresentationMotionCueKey.BoxSlide),
                new PresentationSource(12, PresentationSemanticSource.BoxSlideMotion, BoxEntityId),
                PresentationTarget.Entity(BoxEntityId),
                PresentationAnchor.None(),
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.Track,
                    blocking: false,
                    dedupeKey: 1234),
                motionPayload: validCue.MotionPayload);
            var bindingMissingCue = new PresentationCue(
                PresentationDomain.Motion,
                PresentationCueKey.ForMotion(PresentationMotionCueKey.BoxSlide),
                new PresentationSource(13, PresentationSemanticSource.BoxSlideMotion, BoxEntityId),
                PresentationTarget.Entity(BoxEntityId),
                validCue.Anchor,
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.Track,
                    blocking: false,
                    dedupeKey: 5678),
                motionPayload: validCue.MotionPayload);
            var driverMissingCue = new PresentationCue(
                PresentationDomain.Motion,
                PresentationCueKey.ForMotion(PresentationMotionCueKey.BoxFlip),
                new PresentationSource(14, PresentationSemanticSource.BoxFlipMotion, BoxEntityId),
                PresentationTarget.Entity(BoxEntityId),
                validCue.Anchor,
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.Track,
                    blocking: false,
                    dedupeKey: 9012),
                motionPayload: validCue.MotionPayload);
            var port = new RecordingGameplayMotionPlaybackPort(request =>
                request.CueKey == PresentationMotionCueKey.BoxFlip
                    ? GameplayMotionPlaybackResultKind.DriverMissing
                    : GameplayMotionPlaybackResultKind.BindingMissing);
            var executor = new GameplayMotionPresentationExecutor(
                port,
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));
            var plan = new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                14,
                new[] { targetMissingCue, anchorMissingCue, bindingMissingCue, driverMissingCue },
                new PresentationCueFrameDiagnostics(4, 4, 1)));

            Assert.DoesNotThrow(() => executor.Play(plan));

            Assert.That(executor.Diagnostics.TargetMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.AnchorMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.BindingMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.DriverMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.MissingPortCount, Is.Zero);
            Assert.That(executor.Diagnostics.DuplicateSuppressedCount, Is.Zero);
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.TrackStartedCount, Is.Zero);

            var missingPortExecutor = new GameplayMotionPresentationExecutor(
                null,
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));
            missingPortExecutor.Play(new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                15,
                new[] { validCue },
                new PresentationCueFrameDiagnostics(1, 1, 1))));
            Assert.That(missingPortExecutor.Diagnostics.MissingPortCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void MotionExecutor_ResetSessionAndHardCleanup_ClearDiagnosticsGuardAndPortState()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateMotionCueFrame(CreateBoxMotionTickResult()));
            var port = new RecordingGameplayMotionPlaybackPort();
            var guard = new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor);
            var executor = new GameplayMotionPresentationExecutor(
                port,
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                guard);

            executor.Play(plan);
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(2));

            executor.ResetSession();
            guard.ResetSession();
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.Zero);
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.Zero);
            Assert.That(port.ResetSessionCallCount, Is.EqualTo(1));

            executor.Play(plan);
            executor.HardCleanup();
            guard.ResetSession();
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.Zero);
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.Zero);
            Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void MotionOrchestrationRoute_DoesNotMutateAuthoritativeTickResultOrBlockingState()
        {
            var result = CreateBoxMotionTickResult();
            var determinismHash = result.DeterminismHash;
            var finalEntities = result.FinalEntities.ToArray();
            var eventLog = result.EventLog.ToArray();
            var objectiveResult = result.ObjectiveResult;
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new MotionCuePlanner(),
            }).Plan(factFrame);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var scheduler = new PresentationPlaybackScheduler();
            var executor = new GameplayMotionPresentationExecutor(
                new RecordingGameplayMotionPlaybackPort(),
                BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor,
                new BoxMotionExecutionGuard(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));

            scheduler.Accept(playbackPlan);
            executor.Play(playbackPlan);

            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
            Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
            Assert.That(result.EventLog, Is.EqualTo(eventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
            Assert.That(result.MovementPhaseResult, Is.SameAs(MovementPhaseResult.Empty));
            Assert.That(result.AttackPhaseResult, Is.SameAs(AttackPhaseResult.Empty));
        }

        private const int BoxEntityId = 40;
        private const int PlayerEntityId = 10;
        private static readonly SurfaceCell SlideSourceCell = new(FaceId.Floor, 1, 1);
        private static readonly SurfaceCell SlideDestinationCell = new(FaceId.Floor, 2, 1);
        private static readonly SurfaceCell FlipSourceCell = new(FaceId.Floor, 2, 1);
        private static readonly SurfaceCell FlipDestinationCell = new(FaceId.Floor, 2, 2);
        private static readonly SurfaceCell ImpactCell = new(FaceId.Floor, 2, 3);
        private static readonly CubeTopologyState Topology = new(FaceId.Floor);

        private static TickResult CreateBoxMotionTickResult()
        {
            var presentationData = new TickPresentationData(
                new[]
                {
                    new TickEntityMotion(
                        BoxEntityId,
                        TickEntityMotionKind.BoxSlide,
                        SlideSourceCell,
                        SlideDestinationCell,
                        Topology,
                        Topology,
                        Direction.Right,
                        Direction.Right),
                    new TickEntityMotion(
                        BoxEntityId,
                        TickEntityMotionKind.Flip,
                        FlipSourceCell,
                        FlipDestinationCell,
                        Topology,
                        Topology,
                        Direction.Up,
                        Direction.Up),
                },
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                boxSlideStartSignals: new[]
                {
                    new BoxSlideStartPresentationSignal(
                        BoxEntityId,
                        PlayerEntityId,
                        SlideSourceCell,
                        SlideDestinationCell,
                        Topology),
                });

            return new TickResult(
                tickIndex: 11,
                completedPhases: Array.Empty<TickPhase>(),
                phaseTrace: Array.Empty<string>(),
                movementPhaseResult: MovementPhaseResult.Empty,
                attackPhaseResult: AttackPhaseResult.Empty,
                finalEntities: new[]
                {
                    new EntityState
                    {
                        entityId = BoxEntityId,
                        type = EntityType.Box,
                        position = FlipDestinationCell,
                        facing = Direction.Up,
                        hp = 1,
                        maxHp = 1,
                        boardPresence = EntityBoardPresence.Occupying,
                        boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
                    },
                },
                eventLog: new[] { "AuthoritativeEvent" },
                finalTopology: Topology,
                presentationData: presentationData,
                determinismHash: "BOX-MOTION-HASH",
                trace: TickTrace.Empty,
                objectiveResult: StageObjectiveTickResult.NoObjective);
        }

        private static TickResult CreateBoxMotionTickResultWithImpact()
        {
            var presentationData = new TickPresentationData(
                new[]
                {
                    new TickEntityMotion(
                        BoxEntityId,
                        TickEntityMotionKind.BoxSlide,
                        SlideSourceCell,
                        SlideDestinationCell,
                        Topology,
                        Topology,
                        Direction.Right,
                        Direction.Right),
                    new TickEntityMotion(
                        BoxEntityId,
                        TickEntityMotionKind.Flip,
                        FlipSourceCell,
                        FlipDestinationCell,
                        Topology,
                        Topology,
                        Direction.Up,
                        Direction.Up),
                },
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                flipImpactSignals: new[]
                {
                    new FlipImpactPresentationSignal(
                        sourceActionPlanId: 711,
                        BoxEntityId,
                        impactTargetEntityId: 50,
                        actorEntityId: PlayerEntityId,
                        FlipDestinationCell,
                        ImpactCell,
                        Topology,
                        Direction.Up,
                        Direction.Right,
                        FlipImpactPresentationDisposition.Stay,
                        hasLandingCell: true,
                        landingCell: FlipDestinationCell),
                },
                boxSlideStartSignals: new[]
                {
                    new BoxSlideStartPresentationSignal(
                        BoxEntityId,
                        PlayerEntityId,
                        SlideSourceCell,
                        SlideDestinationCell,
                        Topology),
                });

            return new TickResult(
                tickIndex: 11,
                completedPhases: Array.Empty<TickPhase>(),
                phaseTrace: Array.Empty<string>(),
                movementPhaseResult: MovementPhaseResult.Empty,
                attackPhaseResult: AttackPhaseResult.Empty,
                finalEntities: new[]
                {
                    new EntityState
                    {
                        entityId = BoxEntityId,
                        type = EntityType.Box,
                        position = FlipDestinationCell,
                        facing = Direction.Up,
                        hp = 1,
                        maxHp = 1,
                        boardPresence = EntityBoardPresence.Occupying,
                        boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
                    },
                },
                eventLog: new[] { "AuthoritativeEvent" },
                finalTopology: Topology,
                presentationData: presentationData,
                determinismHash: "BOX-MOTION-HASH",
                trace: TickTrace.Empty,
                objectiveResult: StageObjectiveTickResult.NoObjective);
        }

        private static PresentationCueFrame CreateMotionCueFrame(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new MotionCuePlanner(),
            }).Plan(factFrame);
        }

        private static bool IsMotionTrack(PresentationPlaybackTrack track, PresentationMotionCueKey cueKey)
        {
            return track.Cue.Key.TryGetMotionCueKey(out var key) && key == cueKey;
        }

        private static void AssertEquivalentMotionRequest(
            PresentationPlaybackTrack legacySemanticTrack,
            GameplayMotionPlaybackRequest orchestrationRequest)
        {
            Assert.That(orchestrationRequest.CueKey, Is.EqualTo((PresentationMotionCueKey)legacySemanticTrack.Cue.Key.LocalKey));
            Assert.That(orchestrationRequest.TickIndex, Is.EqualTo(legacySemanticTrack.Cue.Source.TickIndex));
            Assert.That(orchestrationRequest.EntityId, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.EntityId));
            Assert.That(orchestrationRequest.Target, Is.EqualTo(legacySemanticTrack.Cue.Target));
            Assert.That(orchestrationRequest.Anchor, Is.EqualTo(legacySemanticTrack.Cue.Anchor));
            Assert.That(orchestrationRequest.MotionPayload.Kind, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.Kind));
            Assert.That(orchestrationRequest.MotionPayload.EntityId, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.EntityId));
            Assert.That(orchestrationRequest.MotionPayload.SourceCell, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.SourceCell));
            Assert.That(orchestrationRequest.MotionPayload.DestinationCell, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.DestinationCell));
            Assert.That(orchestrationRequest.MotionPayload.Topology, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.Topology));
            Assert.That(orchestrationRequest.MotionPayload.HasTopology, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.HasTopology));
            Assert.That(orchestrationRequest.MotionPayload.Direction, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.Direction));
            Assert.That(orchestrationRequest.MotionPayload.SourceFacing, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.SourceFacing));
            Assert.That(orchestrationRequest.MotionPayload.DestinationFacing, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.DestinationFacing));
            Assert.That(orchestrationRequest.MotionPayload.ActionKind, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.ActionKind));
            Assert.That(orchestrationRequest.MotionPayload.SourceSequenceId, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.SourceSequenceId));
            Assert.That(orchestrationRequest.MotionPayload.SourceActionPlanId, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.SourceActionPlanId));
            Assert.That(orchestrationRequest.MotionPayload.ImpactTargetEntityId, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.ImpactTargetEntityId));
            Assert.That(orchestrationRequest.MotionPayload.FlipDisposition, Is.EqualTo(legacySemanticTrack.Cue.MotionPayload.FlipDisposition));
            Assert.That(legacySemanticTrack.Policy.DedupeKey, Is.GreaterThan(0));
            Assert.That(legacySemanticTrack.Policy.Blocking, Is.False);
            Assert.That(legacySemanticTrack.Policy.InterruptMode, Is.EqualTo(PresentationPlaybackInterruptMode.IgnoreNew));
        }

        private sealed class RecordingGameplayMotionPlaybackPort : IGameplayMotionPlaybackPort
        {
            private readonly Func<GameplayMotionPlaybackRequest, GameplayMotionPlaybackResultKind> _resultFactory;

            public RecordingGameplayMotionPlaybackPort(
                GameplayMotionPlaybackResultKind resultKind = GameplayMotionPlaybackResultKind.Started)
                : this(_ => resultKind)
            {
            }

            public RecordingGameplayMotionPlaybackPort(
                Func<GameplayMotionPlaybackRequest, GameplayMotionPlaybackResultKind> resultFactory)
            {
                _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
            }

            public int TryPlayCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public GameplayMotionPlaybackRequest[] Requests { get; private set; } =
                Array.Empty<GameplayMotionPlaybackRequest>();

            public bool TryPlayBoxMotion(
                in GameplayMotionPlaybackRequest request,
                out GameplayMotionPlaybackResult result)
            {
                TryPlayCallCount++;
                Requests = Requests.Concat(new[] { request }).ToArray();
                var resultKind = _resultFactory(request);
                result = new GameplayMotionPlaybackResult(resultKind);
                return resultKind == GameplayMotionPlaybackResultKind.Started ||
                       resultKind == GameplayMotionPlaybackResultKind.Requested;
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                Requests = Array.Empty<GameplayMotionPlaybackRequest>();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                Requests = Array.Empty<GameplayMotionPlaybackRequest>();
            }
        }
    }
}
