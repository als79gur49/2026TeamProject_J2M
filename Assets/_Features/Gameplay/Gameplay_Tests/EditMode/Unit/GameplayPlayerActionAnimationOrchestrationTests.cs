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
    public sealed class GameplayPlayerActionAnimationOrchestrationTests
    {
        [Test]
        [Category("Core")]
        public void PlayerActionAnimationFactExtraction_ObservesPushFlipActionPhasesAsSemanticFacts()
        {
            var result = CreatePlayerActionAnimationTickResult();
            var frame = new TickPresentationFactExtractor().Extract(result);
            var facts = frame.Facts
                .Where(fact => fact.AnimationPayload.Kind == PresentationAnimationFactKind.PlayerAction)
                .ToArray();

            Assert.That(facts.Length, Is.EqualTo(8));
            Assert.That(facts.Any(fact =>
                fact.AnimationPayload.EntityId == PlayerEntityId &&
                fact.AnimationPayload.ActionKind == PresentationAnimationActionKind.Push &&
                fact.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Windup &&
                fact.AnimationPayload.OutcomeKind == PresentationAnimationOutcomeKind.Started &&
                fact.AnimationPayload.SourceTickIndex == 21 &&
                fact.AnimationPayload.SourceSequenceId == 101), Is.True);
            Assert.That(facts.Any(fact =>
                fact.AnimationPayload.ActionKind == PresentationAnimationActionKind.Push &&
                fact.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Execute &&
                fact.AnimationPayload.OutcomeKind == PresentationAnimationOutcomeKind.Executed), Is.True);
            Assert.That(facts.Any(fact =>
                fact.AnimationPayload.ActionKind == PresentationAnimationActionKind.Push &&
                fact.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Recovery &&
                fact.AnimationPayload.OutcomeKind == PresentationAnimationOutcomeKind.Recovery), Is.True);
            Assert.That(facts.Any(fact =>
                fact.AnimationPayload.ActionKind == PresentationAnimationActionKind.Push &&
                fact.AnimationPayload.OutcomeKind == PresentationAnimationOutcomeKind.Blocked), Is.True);
            Assert.That(facts.Any(fact =>
                fact.AnimationPayload.ActionKind == PresentationAnimationActionKind.Flip &&
                fact.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Windup), Is.True);
            Assert.That(facts.Any(fact =>
                fact.AnimationPayload.ActionKind == PresentationAnimationActionKind.Flip &&
                fact.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Execute &&
                fact.AnimationPayload.OutcomeKind == PresentationAnimationOutcomeKind.Impact), Is.True);
            Assert.That(facts.Any(fact =>
                fact.AnimationPayload.ActionKind == PresentationAnimationActionKind.Flip &&
                fact.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Failed &&
                fact.Source.SemanticSource == PresentationSemanticSource.PlayerActionAttempt), Is.True);
            Assert.That(typeof(PresentationAnimationPayload).AssemblyQualifiedName, Does.Not.Contain("UnityEngine"));
            Assert.That(typeof(PresentationAnimationPayload).GetProperties().Select(property => property.Name), Does.Not.Contain("Animator"));
        }

        [Test]
        [Category("Core")]
        public void AnimationCuePlanner_UsesTypedAnimationCueKeysAndSymbolicAnchors()
        {
            var cueFrame = CreateAnimationCueFrame(CreatePlayerActionAnimationTickResult());

            var pushWindup = cueFrame.Cues.Single(cue =>
                cue.Key.TryGetAnimationCueKey(out var key) && key == PresentationAnimationCueKey.PlayerPushWindup);
            var flipExecute = cueFrame.Cues.Single(cue =>
                cue.Key.TryGetAnimationCueKey(out var key) && key == PresentationAnimationCueKey.PlayerFlipImpactContact);

            Assert.That(pushWindup.Domain, Is.EqualTo(PresentationDomain.Animation));
            Assert.That(pushWindup.Key.Domain, Is.EqualTo(PresentationDomain.Animation));
            Assert.That(pushWindup.Target, Is.EqualTo(PresentationTarget.Entity(PlayerEntityId)));
            Assert.That(pushWindup.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
            Assert.That(pushWindup.Anchor.Target, Is.EqualTo(PresentationTarget.Entity(PlayerEntityId)));
            Assert.That(pushWindup.PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.OneShot));
            Assert.That(pushWindup.PolicyHint.Blocking, Is.False);
            Assert.That(pushWindup.AnimationPayload.PhaseKind, Is.EqualTo(PresentationAnimationPhaseKind.Windup));

            Assert.That(flipExecute.Domain, Is.EqualTo(PresentationDomain.Animation));
            Assert.That(flipExecute.Target, Is.EqualTo(PresentationTarget.Entity(PlayerEntityId)));
            Assert.That(flipExecute.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
            Assert.That(flipExecute.AnimationPayload.PhaseKind, Is.EqualTo(PresentationAnimationPhaseKind.Execute));
            Assert.That(flipExecute.AnimationPayload.OutcomeKind, Is.EqualTo(PresentationAnimationOutcomeKind.Impact));
        }

        [Test]
        [Category("Core")]
        public void AnimationPlaybackPlanner_CreatesNonBlockingCuesWithoutBarriersOrTracks()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateAnimationCueFrame(CreatePlayerActionAnimationTickResult()));
            var scheduler = new PresentationPlaybackScheduler();

            scheduler.Accept(plan);

            Assert.That(plan.Cues.Count(cue => cue.Cue.Domain == PresentationDomain.Animation), Is.EqualTo(8));
            Assert.That(plan.Tracks.Any(track => track.Cue.Domain == PresentationDomain.Animation), Is.False);
            Assert.That(plan.Barriers.Any(barrier => barrier.OwnerDomain == PresentationDomain.Animation), Is.False);
            Assert.That(plan.Cues.All(cue => !cue.Policy.Blocking), Is.True);
            Assert.That(plan.Cues.All(cue => cue.Policy.UnitKind == PresentationPlaybackUnitKind.OneShot), Is.True);
            Assert.That(plan.Cues.All(cue => cue.Policy.InterruptMode == PresentationPlaybackInterruptMode.IgnoreNew), Is.True);
            Assert.That(plan.Cues.Select(cue => cue.Policy.DedupeKey).Distinct().Count(), Is.EqualTo(8));
            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void AnimationExecutor_DefaultLegacyMode_DoesNotCallPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateAnimationCueFrame(CreatePlayerActionAnimationTickResult()));
            var port = new RecordingGameplayAnimationPlaybackPort();
            var executor = new GameplayAnimationPresentationExecutor(port);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.Zero);
            Assert.That(executor.Diagnostics.ObservedCueCount, Is.EqualTo(8));
            Assert.That(executor.Diagnostics.LegacyOwnerNoOpCount, Is.EqualTo(8));
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(executor.Diagnostics.DuplicateSuppressedCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void AnimationExecutor_OrchestrationMode_RoutesPushAndFlipPhasesToPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateAnimationCueFrame(CreatePlayerActionAnimationTickResult()));
            var port = new RecordingGameplayAnimationPlaybackPort();
            var guard = new PlayerActionAnimationExecutionGuard(
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
            var executor = new GameplayAnimationPresentationExecutor(
                port,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                guard);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.EqualTo(8));
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerPushWindup &&
                request.PlayerEntityId == PlayerEntityId &&
                request.AnimationPayload.ActionKind == PresentationAnimationActionKind.Push &&
                request.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Windup &&
                request.AnimationPayload.SourceTickIndex == 21 &&
                request.Anchor.Kind == PresentationAnchorKind.EntityVisualRoot), Is.True);
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerPushExecute &&
                request.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Execute), Is.True);
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerPushRecovery &&
                request.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Recovery), Is.True);
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerFlipWindup &&
                request.AnimationPayload.ActionKind == PresentationAnimationActionKind.Flip), Is.True);
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerFlipImpactContact &&
                request.AnimationPayload.OutcomeKind == PresentationAnimationOutcomeKind.Impact), Is.True);
            Assert.That(port.Requests.Any(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerFlipRecovery &&
                request.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Recovery), Is.True);
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(8));
            Assert.That(executor.Diagnostics.CommandAppliedCount, Is.EqualTo(8));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(8));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void LegacyAndOrchestrationSemanticAnimationRequests_AreEquivalent()
        {
            var cueFrame = CreateAnimationCueFrame(CreatePlayerActionAnimationTickResult());
            var plan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var port = new RecordingGameplayAnimationPlaybackPort();
            var executor = new GameplayAnimationPresentationExecutor(
                port,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                new PlayerActionAnimationExecutionGuard(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));

            executor.Play(plan);

            foreach (var cue in cueFrame.Cues.Where(cue => cue.Domain == PresentationDomain.Animation))
            {
                cue.Key.TryGetAnimationCueKey(out var cueKey);
                var request = port.Requests.Single(candidate =>
                    candidate.CueKey == cueKey &&
                    candidate.AnimationPayload.SourceSequenceId == cue.AnimationPayload.SourceSequenceId);

                Assert.That(request.AnimationPayload.ActionKind, Is.EqualTo(cue.AnimationPayload.ActionKind));
                Assert.That(request.AnimationPayload.PhaseKind, Is.EqualTo(cue.AnimationPayload.PhaseKind));
                Assert.That(request.AnimationPayload.OutcomeKind, Is.EqualTo(cue.AnimationPayload.OutcomeKind));
                Assert.That(request.AnimationPayload.SourceActionPlanId, Is.EqualTo(cue.AnimationPayload.SourceActionPlanId));
                Assert.That(request.Target, Is.EqualTo(cue.Target));
                Assert.That(request.Anchor, Is.EqualTo(cue.Anchor));
            }
        }

        [Test]
        [Category("Core")]
        public void AnimationExecutionGuard_BlocksDuplicateOwnerAttemptForSamePlayerActionKey()
        {
            var key = new PlayerActionAnimationPlaybackKey(
                21,
                PresentationSemanticSource.PlayerAction,
                PlayerEntityId,
                PresentationAnimationCueKey.PlayerPushWindup,
                PresentationAnimationActionKind.Push,
                PresentationAnimationPhaseKind.Windup,
                sourceSequenceId: 101,
                sourceActionPlanId: 1001);
            var guard = new PlayerActionAnimationExecutionGuard(
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);

            Assert.That(
                guard.TryBeginExecution(PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor, key),
                Is.True);
            Assert.That(
                guard.TryBeginExecution(PlayerActionAnimationExecutionOwner.LegacyAnimationSync, key),
                Is.False);

            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void AnimationExecutor_DistinguishesMissingTargetAnchorBindingDriverAnimatorAndPort()
        {
            var validCue = CreateAnimationCueFrame(CreatePlayerActionAnimationTickResult())
                .Cues.Single(cue =>
                    cue.Key.TryGetAnimationCueKey(out var key) && key == PresentationAnimationCueKey.PlayerPushWindup);
            var targetMissingCue = new PresentationCue(
                PresentationDomain.Animation,
                PresentationCueKey.ForAnimation(PresentationAnimationCueKey.PlayerPushWindup),
                validCue.Source,
                PresentationTarget.None(),
                validCue.Anchor,
                validCue.PolicyHint,
                validCue.TopologyPayload,
                validCue.MotionPayload,
                validCue.AnimationPayload);
            var anchorMissingCue = CreateAnimationCue(
                PresentationAnimationCueKey.PlayerPushExecute,
                PresentationAnimationPhaseKind.Execute,
                PresentationAnimationOutcomeKind.Executed,
                22,
                102,
                PresentationAnchor.None());
            var bindingMissingCue = CreateAnimationCue(
                PresentationAnimationCueKey.PlayerPushRecovery,
                PresentationAnimationPhaseKind.Recovery,
                PresentationAnimationOutcomeKind.Recovery,
                23,
                103);
            var driverMissingCue = CreateAnimationCue(
                PresentationAnimationCueKey.PlayerFlipWindup,
                PresentationAnimationPhaseKind.Windup,
                PresentationAnimationOutcomeKind.Started,
                24,
                104);
            var animatorMissingCue = CreateAnimationCue(
                PresentationAnimationCueKey.PlayerFlipRecovery,
                PresentationAnimationPhaseKind.Recovery,
                PresentationAnimationOutcomeKind.Recovery,
                25,
                105);
            var frame = new PresentationCueFrame(
                25,
                new[] { targetMissingCue, anchorMissingCue, bindingMissingCue, driverMissingCue, animatorMissingCue },
                new PresentationCueFrameDiagnostics(5, 5, 0));
            var plan = new PresentationPlaybackPlanner().Plan(frame);
            var port = new RecordingGameplayAnimationPlaybackPort(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerPushRecovery
                    ? GameplayAnimationPlaybackResultKind.BindingMissing
                    : request.CueKey == PresentationAnimationCueKey.PlayerFlipWindup
                        ? GameplayAnimationPlaybackResultKind.DriverMissing
                        : request.CueKey == PresentationAnimationCueKey.PlayerFlipRecovery
                            ? GameplayAnimationPlaybackResultKind.AnimatorMissing
                            : GameplayAnimationPlaybackResultKind.Applied);
            var executor = new GameplayAnimationPresentationExecutor(
                port,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                new PlayerActionAnimationExecutionGuard(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));

            executor.Play(plan);

            Assert.That(executor.Diagnostics.TargetMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.AnchorMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.BindingMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.DriverMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.AnimatorMissingCount, Is.EqualTo(1));
            Assert.That(port.TryPlayCallCount, Is.EqualTo(3));

            var missingPortExecutor = new GameplayAnimationPresentationExecutor(
                playbackPort: null,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                new PlayerActionAnimationExecutionGuard(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
            missingPortExecutor.Play(new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                21,
                new[] { validCue },
                new PresentationCueFrameDiagnostics(1, 1, 0))));
            Assert.That(missingPortExecutor.Diagnostics.MissingPortCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void AnimationExecutor_ResetSessionAndHardCleanup_ClearDiagnosticsGuardAndPortState()
        {
            var plan = new PresentationPlaybackPlanner().Plan(
                CreateAnimationCueFrame(CreatePlayerActionAnimationTickResult()));
            var port = new RecordingGameplayAnimationPlaybackPort();
            var guard = new PlayerActionAnimationExecutionGuard(
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
            var executor = new GameplayAnimationPresentationExecutor(
                port,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                guard);

            executor.Play(plan);
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(8));

            executor.ResetSession();
            guard.ResetSession();
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.Zero);
            Assert.That(port.ResetSessionCallCount, Is.EqualTo(1));

            executor.Play(plan);
            executor.HardCleanup();
            guard.ResetSession();
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.Zero);
            Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void AnimationOrchestrationRoute_DoesNotMutateAuthoritativeTickResultOrBlockingState()
        {
            var result = CreatePlayerActionAnimationTickResult();
            var determinismHash = result.DeterminismHash;
            var finalEntities = result.FinalEntities.ToArray();
            var eventLog = result.EventLog.ToArray();
            var objectiveResult = result.ObjectiveResult;
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new AnimationCuePlanner(),
            }).Plan(factFrame);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var scheduler = new PresentationPlaybackScheduler();
            var executor = new GameplayAnimationPresentationExecutor(
                new RecordingGameplayAnimationPlaybackPort(),
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                new PlayerActionAnimationExecutionGuard(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));

            scheduler.Accept(playbackPlan);
            executor.Play(playbackPlan);

            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
            Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
            Assert.That(result.EventLog, Is.EqualTo(eventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
        }

        private const int PlayerEntityId = 10;
        private const int TargetEntityId = 40;
        private static readonly CubeTopologyState Topology = new(FaceId.Floor);

        private static TickResult CreatePlayerActionAnimationTickResult()
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Push,
                        101,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        direction: Direction.Right,
                        actionPlanId: 1001),
                    new TickPlayerActionPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Push,
                        102,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Success,
                        targetEntityId: TargetEntityId,
                        direction: Direction.Right,
                        actionPlanId: 1002),
                    new TickPlayerActionPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Push,
                        103,
                        startedThisTick: false,
                        completedThisTick: true,
                        canceledThisTick: false,
                        isRecoveryPhase: true,
                        direction: Direction.Right,
                        actionPlanId: 1003),
                    new TickPlayerActionPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Push,
                        104,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Blocked,
                        targetEntityId: TargetEntityId,
                        direction: Direction.Right,
                        actionPlanId: 1004),
                    new TickPlayerActionPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Flip,
                        201,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        direction: Direction.Up,
                        actionPlanId: 2001),
                    new TickPlayerActionPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Flip,
                        202,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Impact,
                        targetEntityId: TargetEntityId,
                        direction: Direction.Up,
                        actionPlanId: 2002),
                    new TickPlayerActionPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Flip,
                        203,
                        startedThisTick: false,
                        completedThisTick: true,
                        canceledThisTick: false,
                        isRecoveryPhase: true,
                        direction: Direction.Up,
                        actionPlanId: 2003),
                },
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                playerActionAttemptSignals: new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Flip,
                        Direction.Up,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        TargetEntityId,
                        hasTarget: true),
                });

            return new TickResult(
                tickIndex: 21,
                completedPhases: Array.Empty<TickPhase>(),
                phaseTrace: Array.Empty<string>(),
                movementPhaseResult: MovementPhaseResult.Empty,
                attackPhaseResult: AttackPhaseResult.Empty,
                finalEntities: new[]
                {
                    new EntityState
                    {
                        entityId = PlayerEntityId,
                        type = EntityType.Unit,
                        position = new SurfaceCell(FaceId.Floor, 1, 1),
                        facing = Direction.Right,
                        hp = 3,
                        maxHp = 3,
                        boardPresence = EntityBoardPresence.Occupying,
                    },
                },
                eventLog: new[] { "AuthoritativePlayerActionEvent" },
                finalTopology: Topology,
                presentationData: presentationData,
                determinismHash: "PLAYER-ACTION-ANIMATION-HASH",
                trace: TickTrace.Empty,
                objectiveResult: StageObjectiveTickResult.NoObjective);
        }

        private static PresentationCueFrame CreateAnimationCueFrame(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new AnimationCuePlanner(),
            }).Plan(factFrame);
        }

        private static PresentationCue CreateAnimationCue(
            PresentationAnimationCueKey cueKey,
            PresentationAnimationPhaseKind phase,
            PresentationAnimationOutcomeKind outcome,
            int tickIndex,
            int sequenceId,
            PresentationAnchor? anchor = null)
        {
            var actionKind = cueKey == PresentationAnimationCueKey.PlayerFlipWindup ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipRecovery ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipExecute ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipBlocked ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipImpactContact ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipFailed
                ? PresentationAnimationActionKind.Flip
                : PresentationAnimationActionKind.Push;
            var payload = new PresentationAnimationPayload(
                PresentationAnimationFactKind.PlayerAction,
                PlayerEntityId,
                actionKind,
                phase,
                outcome,
                tickIndex,
                sequenceId,
                sourceActionPlanId: sequenceId + 9000,
                targetEntityId: TargetEntityId,
                direction: Direction.Right);
            return new PresentationCue(
                PresentationDomain.Animation,
                PresentationCueKey.ForAnimation(cueKey),
                new PresentationSource(tickIndex, PresentationSemanticSource.PlayerAction, PlayerEntityId),
                PresentationTarget.Entity(PlayerEntityId),
                anchor ?? PresentationAnchor.ForEntityVisualRoot(PlayerEntityId),
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.OneShot,
                    blocking: false,
                    dedupeKey: sequenceId + 10000),
                animationPayload: payload);
        }

        private sealed class RecordingGameplayAnimationPlaybackPort : IGameplayAnimationPlaybackPort
        {
            private readonly Func<GameplayAnimationPlaybackRequest, GameplayAnimationPlaybackResultKind> _resultFactory;

            public RecordingGameplayAnimationPlaybackPort(
                GameplayAnimationPlaybackResultKind resultKind = GameplayAnimationPlaybackResultKind.Applied)
                : this(_ => resultKind)
            {
            }

            public RecordingGameplayAnimationPlaybackPort(
                Func<GameplayAnimationPlaybackRequest, GameplayAnimationPlaybackResultKind> resultFactory)
            {
                _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
            }

            public int TryPlayCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public GameplayAnimationPlaybackRequest[] Requests { get; private set; } =
                Array.Empty<GameplayAnimationPlaybackRequest>();

            public bool TryPlayPlayerActionAnimation(
                in GameplayAnimationPlaybackRequest request,
                out GameplayAnimationPlaybackResult result)
            {
                TryPlayCallCount++;
                Requests = Requests.Concat(new[] { request }).ToArray();
                var resultKind = _resultFactory(request);
                result = new GameplayAnimationPlaybackResult(resultKind);
                return resultKind == GameplayAnimationPlaybackResultKind.Applied ||
                       resultKind == GameplayAnimationPlaybackResultKind.Requested;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                Requests = Array.Empty<GameplayAnimationPlaybackRequest>();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                Requests = Array.Empty<GameplayAnimationPlaybackRequest>();
            }
        }
    }
}
