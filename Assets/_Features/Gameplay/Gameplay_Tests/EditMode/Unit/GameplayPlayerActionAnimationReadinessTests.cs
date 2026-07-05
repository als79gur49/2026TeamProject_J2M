using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.ActionAudio;
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
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayPlayerActionAnimationReadinessTests
    {
        private const int PlayerEntityId = 10;
        private const int TargetEntityId = 40;
        private static readonly CubeTopologyState Topology = new(FaceId.Floor);

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_DefaultMode_IsOrchestrationExecutor()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_DefaultMode_IsOrchestrationExecutor));

            try
            {
                var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);

                Assert.That(Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), default(PlayerActionAnimationExecutionMode)), Is.False);
                Assert.That(coordinator.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(
                    PlayerActionAnimationExecutionPolicy.Normalize((PlayerActionAnimationExecutionMode)999),
                    Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(
                    typeof(GameplaySceneHostConfiguration).GetField(nameof(PlayerActionAnimationExecutionMode)),
                    Is.Null,
                    "Player action animation execution mode must not be serialized into production host configuration.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_ControlledRoutesSupportedCues()
        {
            var cues = CreateSupportedCueInventory();
            var port = new RecordingGameplayAnimationPlaybackPort();
            var executor = CreateOrchestrationExecutor(port);

            executor.Play(CreatePlan(cues));

            Assert.That(port.Requests.Select(request => request.CueKey).ToArray(), Is.EqualTo(cues.Select(GetCueKey).ToArray()));
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(cues.Count));
            Assert.That(executor.Diagnostics.CommandAppliedCount, Is.EqualTo(cues.Count));
            foreach (var cue in cues)
            {
                var cueKey = GetCueKey(cue);
                var request = port.Requests.Single(candidate => candidate.CueKey == cueKey);
                AssertRequestPreservesCue(cue, request);
                Assert.That(request.OwnershipKey.CueKey, Is.EqualTo(cueKey));
                Assert.That(request.OwnershipKey.SourceActionPlanId, Is.EqualTo(cue.AnimationPayload.SourceActionPlanId));
                Assert.That(request.OwnershipKey.SourceSequenceId, Is.EqualTo(cue.AnimationPayload.SourceSequenceId));
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_LegacyAndOrchestrationSemanticParity()
        {
            foreach (var cue in CreateSupportedCueInventory())
            {
                var legacy = DriveLegacy(cue);
                var orchestration = DriveOrchestration(cue);

                Assert.That(orchestration.Diagnostics.CommandAppliedCount, Is.EqualTo(1), GetCueKey(cue).ToString());
                Assert.That(orchestration.Request.CueKey, Is.EqualTo(GetCueKey(cue)));
                AssertRequestPreservesCue(cue, orchestration.Request);
                Assert.That(orchestration.DriverState.ActiveActionKind, Is.EqualTo(legacy.DriverState.ActiveActionKind));
                Assert.That(orchestration.DriverState.ActiveActionSequence, Is.EqualTo(legacy.DriverState.ActiveActionSequence));
                Assert.That(orchestration.DriverState.ActionPlanId, Is.EqualTo(legacy.DriverState.ActionPlanId));
                Assert.That(orchestration.DriverState.StartedThisTick, Is.EqualTo(legacy.DriverState.StartedThisTick));
                Assert.That(orchestration.DriverState.ExecutedThisTick, Is.EqualTo(legacy.DriverState.ExecutedThisTick));
                Assert.That(orchestration.DriverState.IsRecoveryPhase, Is.EqualTo(legacy.DriverState.IsRecoveryPhase));
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_ExecuteLoweringContractIsExplicit()
        {
            var push = DriveOrchestration(CreateAnimationCue(
                PresentationAnimationCueKey.PlayerPushExecute,
                PresentationAnimationActionKind.Push,
                PresentationAnimationPhaseKind.Execute,
                PresentationAnimationOutcomeKind.Executed,
                tickIndex: 31,
                sequenceId: 301));
            var flip = DriveOrchestration(CreateAnimationCue(
                PresentationAnimationCueKey.PlayerFlipExecute,
                PresentationAnimationActionKind.Flip,
                PresentationAnimationPhaseKind.Execute,
                PresentationAnimationOutcomeKind.Executed,
                tickIndex: 32,
                sequenceId: 302));

            Assert.That(push.Diagnostics.ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(1));
            Assert.That(push.DriverPhase, Is.EqualTo(PlayerPresentationPhase.PushRecovery));
            Assert.That(push.LastCrossFadedStateName, Is.EqualTo("Push_Recovery"));
            Assert.That(flip.Diagnostics.ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(1));
            Assert.That(flip.DriverPhase, Is.EqualTo(PlayerPresentationPhase.FlipRecovery));
            Assert.That(flip.LastCrossFadedStateName, Is.EqualTo("Flip_Recovery"));
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_ExecuteAndRecoveryDoNotDuplicateDriverCommand()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject(
                nameof(PlayerActionAnimation_Readiness_ExecuteAndRecoveryDoNotDuplicateDriverCommand));

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var sync = new GameplayAnimationSyncCoordinator();
                sync.CacheDrivers(PlayerEntityId, view);
                var executor = CreateOrchestrationExecutor(new GameplayAnimationSyncPlaybackPort(
                    sync,
                    CreateStateStore(view)));
                var execute = CreateAnimationCue(
                    PresentationAnimationCueKey.PlayerPushExecute,
                    PresentationAnimationActionKind.Push,
                    PresentationAnimationPhaseKind.Execute,
                    PresentationAnimationOutcomeKind.Executed,
                    tickIndex: 41,
                    sequenceId: 401);
                var recovery = CreateAnimationCue(
                    PresentationAnimationCueKey.PlayerPushRecovery,
                    PresentationAnimationActionKind.Push,
                    PresentationAnimationPhaseKind.Recovery,
                    PresentationAnimationOutcomeKind.Recovery,
                    tickIndex: 41,
                    sequenceId: 402);

                executor.Play(CreatePlan(new[] { execute, recovery }));

                Assert.That(executor.Diagnostics.CommandAppliedCount, Is.EqualTo(2));
                Assert.That(executor.Diagnostics.ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(1));
                Assert.That(driver.ActionExecuteSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Recovery"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_ConcreteDriverCommandParity()
        {
            var expected = new Dictionary<PresentationAnimationCueKey, PlayerPresentationPhase>
            {
                [PresentationAnimationCueKey.PlayerPushWindup] = PlayerPresentationPhase.PushWindup,
                [PresentationAnimationCueKey.PlayerPushExecute] = PlayerPresentationPhase.PushRecovery,
                [PresentationAnimationCueKey.PlayerPushRecovery] = PlayerPresentationPhase.PushRecovery,
                [PresentationAnimationCueKey.PlayerPushBlocked] = PlayerPresentationPhase.PushRecovery,
                [PresentationAnimationCueKey.PlayerPushImpactContact] = PlayerPresentationPhase.PushRecovery,
                [PresentationAnimationCueKey.PlayerPushFailed] = PlayerPresentationPhase.PushWindup,
                [PresentationAnimationCueKey.PlayerFlipWindup] = PlayerPresentationPhase.FlipWindup,
                [PresentationAnimationCueKey.PlayerFlipExecute] = PlayerPresentationPhase.FlipRecovery,
                [PresentationAnimationCueKey.PlayerFlipRecovery] = PlayerPresentationPhase.FlipRecovery,
                [PresentationAnimationCueKey.PlayerFlipBlocked] = PlayerPresentationPhase.FlipRecovery,
                [PresentationAnimationCueKey.PlayerFlipImpactContact] = PlayerPresentationPhase.FlipRecovery,
                [PresentationAnimationCueKey.PlayerFlipFailed] = PlayerPresentationPhase.FlipWindup,
            };

            foreach (var cue in CreateSupportedCueInventory())
            {
                var key = GetCueKey(cue);
                var legacy = DriveLegacy(cue);
                var orchestration = DriveOrchestration(cue);

                Assert.That(legacy.DriverPhase, Is.EqualTo(expected[key]), key.ToString());
                Assert.That(orchestration.DriverPhase, Is.EqualTo(expected[key]), key.ToString());
                Assert.That(orchestration.DriverPhase, Is.EqualTo(legacy.DriverPhase), key.ToString());
                Assert.That(orchestration.LastCrossFadedStateName, Is.EqualTo(legacy.LastCrossFadedStateName), key.ToString());
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_DuplicateGuardNormalAndForced()
        {
            var normalPort = new RecordingGameplayAnimationPlaybackPort();
            var normalExecutor = CreateOrchestrationExecutor(normalPort);
            normalExecutor.Play(CreatePlan(CreateSupportedCueInventory()));

            Assert.That(normalPort.TryPlayCallCount, Is.EqualTo(12));
            Assert.That(normalExecutor.Diagnostics.DuplicateSuppressedCount, Is.Zero);

            var duplicatePort = new RecordingGameplayAnimationPlaybackPort();
            var guard = new PlayerActionAnimationExecutionGuard(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
            var duplicateExecutor = new GameplayAnimationPresentationExecutor(
                duplicatePort,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                guard);
            var cue = CreateAnimationCue(
                PresentationAnimationCueKey.PlayerFlipExecute,
                PresentationAnimationActionKind.Flip,
                PresentationAnimationPhaseKind.Execute,
                PresentationAnimationOutcomeKind.Executed,
                tickIndex: 51,
                sequenceId: 501);

            duplicateExecutor.Play(CreatePlan(new[] { cue, cue }));

            Assert.That(duplicatePort.TryPlayCallCount, Is.EqualTo(1));
            Assert.That(duplicateExecutor.Diagnostics.DuplicateSuppressedCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_MissingDiagnosticsSeparated()
        {
            var validCue = CreateAnimationCue(
                PresentationAnimationCueKey.PlayerPushWindup,
                PresentationAnimationActionKind.Push,
                PresentationAnimationPhaseKind.Windup,
                PresentationAnimationOutcomeKind.Started,
                tickIndex: 61,
                sequenceId: 601);
            var targetMissing = ReplaceTarget(validCue, PresentationTarget.None());
            var anchorMissing = ReplaceAnchor(CreateAnimationCue(
                PresentationAnimationCueKey.PlayerPushExecute,
                PresentationAnimationActionKind.Push,
                PresentationAnimationPhaseKind.Execute,
                PresentationAnimationOutcomeKind.Executed,
                tickIndex: 61,
                sequenceId: 602), PresentationAnchor.None());
            var adapterPort = new RecordingGameplayAnimationPlaybackPort(request =>
                request.CueKey == PresentationAnimationCueKey.PlayerPushRecovery
                    ? GameplayAnimationPlaybackResultKind.BindingMissing
                    : request.CueKey == PresentationAnimationCueKey.PlayerFlipWindup
                        ? GameplayAnimationPlaybackResultKind.DriverMissing
                        : GameplayAnimationPlaybackResultKind.AnimatorMissing);
            var adapterExecutor = CreateOrchestrationExecutor(adapterPort);

            adapterExecutor.Play(CreatePlan(new[]
            {
                targetMissing,
                anchorMissing,
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushRecovery, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Recovery, PresentationAnimationOutcomeKind.Recovery, 61, 603),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipWindup, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Windup, PresentationAnimationOutcomeKind.Started, 61, 604),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipRecovery, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Recovery, PresentationAnimationOutcomeKind.Recovery, 61, 605),
            }));

            Assert.That(adapterExecutor.Diagnostics.TargetMissingCount, Is.EqualTo(1));
            Assert.That(adapterExecutor.Diagnostics.AnchorMissingCount, Is.EqualTo(1));
            Assert.That(adapterExecutor.Diagnostics.BindingMissingCount, Is.EqualTo(1));
            Assert.That(adapterExecutor.Diagnostics.DriverMissingCount, Is.EqualTo(1));
            Assert.That(adapterExecutor.Diagnostics.AnimatorMissingCount, Is.EqualTo(1));

            var missingPortExecutor = CreateOrchestrationExecutor(playbackPort: null);
            missingPortExecutor.Play(CreatePlan(new[] { validCue }));
            Assert.That(missingPortExecutor.Diagnostics.MissingPortCount, Is.EqualTo(1));

            var ignoredPort = new RecordingGameplayAnimationPlaybackPort();
            var ignoredExecutor = CreateOrchestrationExecutor(ignoredPort);
            ignoredExecutor.Play(CreatePlan(new[] { CreateEnemyAnimationCue() }));
            Assert.That(ignoredExecutor.Diagnostics.ObservedCueCount, Is.Zero);
            Assert.That(ignoredPort.TryPlayCallCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_LifecycleCleanupClearsState()
        {
            var port = new RecordingGameplayAnimationPlaybackPort();
            var guard = new PlayerActionAnimationExecutionGuard(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
            var executor = new GameplayAnimationPresentationExecutor(
                port,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                guard);
            var plan = CreatePlan(new[]
            {
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushWindup, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Windup, PresentationAnimationOutcomeKind.Started, 71, 701),
            });

            executor.Play(plan);
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(1));
            executor.ResetSession();
            guard.ResetSession();

            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(port.TryPlayCallCount, Is.Zero);
            Assert.That(port.ResetSessionCallCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.Zero);

            executor.Play(plan);
            executor.HardCleanup();
            guard.ResetSession();

            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(port.TryPlayCallCount, Is.Zero);
            Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_ActionAudioVocabularyAndOwnershipRemainSeparated()
        {
            var result = CreateTickResult(
                tickIndex: 81,
                new[]
                {
                    CreateActionSignal(PlayerActionKind.Push, 801, startedThisTick: true, actionPlanId: 1801),
                },
                new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        PlayerEntityId,
                        PlayerActionKind.Flip,
                        Direction.Up,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        TargetEntityId,
                        hasTarget: true),
                });
            var facts = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new AnimationCuePlanner(),
                new ActionAudioCuePlanner(),
            }).Plan(facts);

            Assert.That(Enum.GetNames(typeof(GameplayActionAudioMoment)), Does.Contain(nameof(GameplayActionAudioMoment.Windup)));
            Assert.That(Enum.GetNames(typeof(GameplayActionAudioMoment)), Does.Not.Contain("Execute"));
            Assert.That(Enum.GetNames(typeof(PresentationAnimationCueKey)), Does.Contain(nameof(PresentationAnimationCueKey.PlayerPushExecute)));
            Assert.That(cueFrame.Cues.Any(cue => cue.Domain == PresentationDomain.Animation), Is.True);
            Assert.That(cueFrame.Cues.Any(cue => cue.Domain == PresentationDomain.ActionAudio), Is.True);
            Assert.That(cueFrame.Cues.Any(cue =>
                cue.Key.TryGetAnimationCueKey(out var key) && key == PresentationAnimationCueKey.PlayerPushWindup), Is.True);
            Assert.That(cueFrame.Cues.Any(cue =>
                cue.Key.TryGetActionAudioCueKey(out var key) && key == PresentationActionAudioCueKey.PlayerPushWindup), Is.True);
            Assert.That(cueFrame.Cues.All(cue => cue.Domain != PresentationDomain.ActionAudio || !cue.Key.TryGetAnimationCueKey(out _)), Is.True);
            Assert.That(cueFrame.Cues.All(cue => cue.Domain != PresentationDomain.Animation || !cue.Key.TryGetActionAudioCueKey(out _)), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_IsNonBlockingAndInputLockNeutral()
        {
            var result = CreateTickResult(
                tickIndex: 91,
                CreateSupportedSignals(),
                Array.Empty<TickPlayerActionAttemptPresentationSignal>());
            var plan = CreatePlan(PlanAnimationCues(result));
            var scheduler = new PresentationPlaybackScheduler();
            var executor = CreateOrchestrationExecutor(new RecordingGameplayAnimationPlaybackPort());

            scheduler.Accept(plan);
            executor.Play(plan);

            Assert.That(plan.Tracks, Is.Empty);
            Assert.That(plan.Barriers, Is.Empty);
            Assert.That(plan.Cues.All(cue => cue.Policy.UnitKind == PresentationPlaybackUnitKind.OneShot), Is.True);
            Assert.That(plan.Cues.All(cue => !cue.Policy.Blocking), Is.True);
            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_Readiness_IsNonAuthoritative()
        {
            var result = CreateTickResult(
                tickIndex: 101,
                CreateSupportedSignals(),
                Array.Empty<TickPlayerActionAttemptPresentationSignal>());
            var finalEntities = result.FinalEntities.ToArray();
            var eventLog = result.EventLog.ToArray();
            var determinismHash = result.DeterminismHash;
            var movementPhaseResult = result.MovementPhaseResult;
            var attackPhaseResult = result.AttackPhaseResult;
            var objectiveResult = result.ObjectiveResult;
            var executor = CreateOrchestrationExecutor(new RecordingGameplayAnimationPlaybackPort());

            executor.Play(CreatePlan(PlanAnimationCues(result)));

            Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
            Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
            Assert.That(result.EventLog, Is.EqualTo(eventLog));
            Assert.That(result.MovementPhaseResult, Is.SameAs(movementPhaseResult));
            Assert.That(result.AttackPhaseResult, Is.SameAs(attackPhaseResult));
            Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
        }

        [Test]
        [Category("Core")]
        public void ProductionSwitchReadiness_ReflectsPlayerActionAnimationAssessment()
        {
            const string readinessPath = "Docs/Architecture/Presentation-Orchestration-Production-Switch-Readiness.md";
            var document = System.IO.File.ReadAllText(ToAbsolutePath(readinessPath));

            Assert.That(document, Does.Contain("Phase 9K"));
            Assert.That(document, Does.Contain("Phase 9L"));
            Assert.That(document, Does.Contain("Phase 9M"));
            Assert.That(document, Does.Contain("Phase 9N"));
            Assert.That(document, Does.Contain("Player action animation"));
            Assert.That(document, Does.Contain("AcceptedTemporaryAdapterContract"));
            Assert.That(document, Does.Contain("ProductionDefaultOnTelemetryHardened"));
            Assert.That(document, Does.Contain("PlayerPushExecute -> PlayerPresentationPhase.PushRecovery"));
            Assert.That(document, Does.Contain("PlayerFlipExecute -> PlayerPresentationPhase.FlipRecovery"));
            Assert.That(document, Does.Contain("Player action animation production default is now `OrchestrationAnimationExecutor`"));
        }

        [Test]
        [Category("Core")]
        public void CoreSfxDamageVfxAndBoxMotion_ProductionDefaultsRemainStable()
        {
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();
            var rootObject = new GameObject(nameof(CoreSfxDamageVfxAndBoxMotion_ProductionDefaultsRemainStable));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);

                Assert.That(coordinator.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.ObservedTrackCount, Is.Zero);
                Assert.That(coordinator.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(
                    coordinator.TopologyProductionTelemetrySnapshot.IsProductionDefaultOwner,
                    Is.True,
                    "Topology remains on the current presentation route while player action animation decommission stays stable.");
                Assert.That(coordinator.EnemyAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ArchitectureBoundary_AfterPlayerAnimationSwitch_RemainsSeparated()
        {
            var contracts = ReadDirectory("Assets/_Features/Gameplay/Gameplay_PresentationContracts/Runtime");
            var planning = ReadDirectory("Assets/_Features/Gameplay/Gameplay_PresentationPlanning/Runtime");
            var playback = ReadDirectory("Assets/_Features/Gameplay/Gameplay_PresentationPlayback/Runtime");
            var actionAudio = ReadDirectory("Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime");
            var ui = ReadDirectory("Assets/_Features/UI");
            var simulation = ReadDirectory("Assets/_Features/Gameplay/Gameplay_Model/Runtime") + "\n" +
                             ReadDirectory("Assets/_Features/Gameplay/Gameplay_Loop/Runtime");

            foreach (var source in new[] { contracts, planning, playback })
            {
                Assert.That(source, Does.Not.Contain("PlayerAnimatorDriver"));
                Assert.That(source, Does.Not.Contain("GameplayAnimationSyncCoordinator"));
                Assert.That(source, Does.Not.Contain("AnimatorController"));
                Assert.That(source, Does.Not.Contain("AnimationClip"));
                Assert.That(source, Does.Not.Contain("GameObject"));
                Assert.That(source, Does.Not.Contain("Transform"));
                Assert.That(source, Does.Not.Contain("AnimatorStateInfo"));
            }

            Assert.That(actionAudio, Does.Not.Contain("PresentationAnimationCueKey"));
            Assert.That(actionAudio, Does.Not.Contain("GameplayAnimationExecutorDiagnostics"));
            Assert.That(actionAudio, Does.Not.Contain("PlayerActionAnimationProductionTelemetrySnapshot"));
            Assert.That(ui, Does.Not.Contain("GameplayAnimationExecutorDiagnostics"));
            Assert.That(ui, Does.Not.Contain("PlayerActionAnimationProductionTelemetrySnapshot"));
            Assert.That(simulation, Does.Not.Contain("PlayerActionAnimationExecutionMode"));
            Assert.That(simulation, Does.Not.Contain("GameplayAnimationPresentationExecutor"));
            Assert.That(simulation, Does.Not.Contain("PlayerActionAnimationProductionTelemetrySnapshot"));
        }

        private static GameplayAnimationPresentationExecutor CreateOrchestrationExecutor(
            IGameplayAnimationPlaybackPort playbackPort)
        {
            return new GameplayAnimationPresentationExecutor(
                playbackPort,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                new PlayerActionAnimationExecutionGuard(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
        }

        private static DriverPlaybackSnapshot DriveLegacy(PresentationCue cue)
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject(
                $"{nameof(DriveLegacy)}_{GetCueKey(cue)}_{cue.AnimationPayload.SourceSequenceId}");

            try
            {
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var state = new PlayerViewPresentationState(
                    PlayerEntityId,
                    cue.Source.TickIndex,
                    ToPlayerActionKind(cue.AnimationPayload.ActionKind),
                    cue.AnimationPayload.SourceSequenceId,
                    startedThisTick: cue.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Windup,
                    executedThisTick: cue.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Execute,
                    completedThisTick: cue.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Recovery,
                    canceledThisTick: cue.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Failed,
                    shouldPlayWalkLoop: false,
                    isRecoveryPhase: cue.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Recovery,
                    actionPlanId: cue.AnimationPayload.SourceActionPlanId);

                driver.Apply(state);
                driver.SyncRuntimeState(
                    isVisible: true,
                    cue.AnimationPayload.ActionKind == PresentationAnimationActionKind.Push
                        ? PlayerViewAnimationState.Push
                        : PlayerViewAnimationState.Flip);

                return DriverPlaybackSnapshot.From(default, default, driver);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static DriverPlaybackSnapshot DriveOrchestration(PresentationCue cue)
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject(
                $"{nameof(DriveOrchestration)}_{GetCueKey(cue)}_{cue.AnimationPayload.SourceSequenceId}");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var sync = new GameplayAnimationSyncCoordinator();
                sync.CacheDrivers(PlayerEntityId, view);
                var port = new RecordingGameplayAnimationPlaybackPort(
                    (RecordingGameplayAnimationPlaybackPort.PlaybackDelegate)(
                        (GameplayAnimationPlaybackRequest request, out GameplayAnimationPlaybackResult result) =>
                            new GameplayAnimationSyncPlaybackPort(sync, CreateStateStore(view))
                                .TryPlayPlayerActionAnimation(request, out result)));
                var executor = CreateOrchestrationExecutor(port);

                executor.Play(CreatePlan(new[] { cue }));

                return DriverPlaybackSnapshot.From(executor.Diagnostics, port.Requests.Single(), driver);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static PresentationPlaybackPlan CreatePlan(IReadOnlyList<PresentationCue> cues)
        {
            return new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                cues.Count > 0 ? cues[0].Source.TickIndex : 0,
                cues,
                new PresentationCueFrameDiagnostics(cues.Count, cues.Count, 0)));
        }

        private static IReadOnlyList<PresentationCue> PlanAnimationCues(TickResult result)
        {
            var facts = new TickPresentationFactExtractor().Extract(result);
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new AnimationCuePlanner(),
            }).Plan(facts).Cues;
        }

        private static IReadOnlyList<PresentationCue> CreateSupportedCueInventory()
        {
            return new[]
            {
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushWindup, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Windup, PresentationAnimationOutcomeKind.Started, 21, 101),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushExecute, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Execute, PresentationAnimationOutcomeKind.Executed, 22, 102),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushRecovery, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Recovery, PresentationAnimationOutcomeKind.Recovery, 23, 103),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushBlocked, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Execute, PresentationAnimationOutcomeKind.Blocked, 24, 104),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushImpactContact, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Execute, PresentationAnimationOutcomeKind.Impact, 25, 105),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerPushFailed, PresentationAnimationActionKind.Push, PresentationAnimationPhaseKind.Failed, PresentationAnimationOutcomeKind.Failed, 26, 106, PresentationSemanticSource.PlayerActionAttempt, actionPlanId: 0),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipWindup, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Windup, PresentationAnimationOutcomeKind.Started, 27, 201),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipExecute, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Execute, PresentationAnimationOutcomeKind.Executed, 28, 202),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipRecovery, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Recovery, PresentationAnimationOutcomeKind.Recovery, 29, 203),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipBlocked, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Execute, PresentationAnimationOutcomeKind.Blocked, 30, 204),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipImpactContact, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Execute, PresentationAnimationOutcomeKind.Impact, 31, 205),
                CreateAnimationCue(PresentationAnimationCueKey.PlayerFlipFailed, PresentationAnimationActionKind.Flip, PresentationAnimationPhaseKind.Failed, PresentationAnimationOutcomeKind.Failed, 32, 206, PresentationSemanticSource.PlayerActionAttempt, actionPlanId: 0),
            };
        }

        private static PresentationCue CreateAnimationCue(
            PresentationAnimationCueKey cueKey,
            PresentationAnimationActionKind actionKind,
            PresentationAnimationPhaseKind phaseKind,
            PresentationAnimationOutcomeKind outcomeKind,
            int tickIndex,
            int sequenceId,
            PresentationSemanticSource source = PresentationSemanticSource.PlayerAction,
            int? actionPlanId = null)
        {
            var key = PresentationCueKey.ForAnimation(cueKey);
            var payload = new PresentationAnimationPayload(
                PresentationAnimationFactKind.PlayerAction,
                PlayerEntityId,
                actionKind,
                phaseKind,
                outcomeKind,
                tickIndex,
                sequenceId,
                actionPlanId ?? sequenceId + 1000,
                TargetEntityId,
                Direction.Right);

            return new PresentationCue(
                PresentationDomain.Animation,
                key,
                new PresentationSource(tickIndex, source, PlayerEntityId, (int)actionKind, sequenceId),
                PresentationTarget.Entity(PlayerEntityId),
                PresentationAnchor.ForEntityVisualRoot(PlayerEntityId),
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.OneShot,
                    blocking: false,
                    dedupeKey: tickIndex * 10000 + sequenceId),
                animationPayload: payload);
        }

        private static PresentationCue CreateEnemyAnimationCue()
        {
            return new PresentationCue(
                PresentationDomain.Animation,
                PresentationCueKey.ForAnimation(PresentationAnimationCueKey.EnemyDeath),
                new PresentationSource(62, PresentationSemanticSource.EnemyAction, sourceEntityId: 50),
                PresentationTarget.Entity(50),
                PresentationAnchor.ForEntityVisualRoot(50),
                PresentationPlaybackPolicyHint.OneShot(62001));
        }

        private static PresentationCue ReplaceTarget(PresentationCue cue, PresentationTarget target)
        {
            return new PresentationCue(
                cue.Domain,
                cue.Key,
                cue.Source,
                target,
                cue.Anchor,
                cue.PolicyHint,
                cue.TopologyPayload,
                cue.MotionPayload,
                cue.AnimationPayload,
                cue.EnemyPayload,
                cue.SfxPayload,
                cue.ActionAudioPayload,
                cue.EnemyAudioPayload);
        }

        private static PresentationCue ReplaceAnchor(PresentationCue cue, PresentationAnchor anchor)
        {
            return new PresentationCue(
                cue.Domain,
                cue.Key,
                cue.Source,
                cue.Target,
                anchor,
                cue.PolicyHint,
                cue.TopologyPayload,
                cue.MotionPayload,
                cue.AnimationPayload,
                cue.EnemyPayload,
                cue.SfxPayload,
                cue.ActionAudioPayload,
                cue.EnemyAudioPayload);
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            IReadOnlyList<TickPlayerActionPresentationSignal> playerActionSignals,
            IReadOnlyList<TickPlayerActionAttemptPresentationSignal> attemptSignals)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals,
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                playerActionAttemptSignals: attemptSignals);

            return new TickResult(
                tickIndex,
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
                eventLog: new[] { "PlayerActionAnimationReadiness.AuthoritativeEvent" },
                finalTopology: Topology,
                presentationData: presentationData,
                determinismHash: $"PLAYER-ACTION-ANIMATION-READINESS-{tickIndex}",
                trace: TickTrace.Empty,
                objectiveResult: StageObjectiveTickResult.NoObjective);
        }

        private static IReadOnlyList<TickPlayerActionPresentationSignal> CreateSupportedSignals()
        {
            return new[]
            {
                CreateActionSignal(PlayerActionKind.Push, 101, startedThisTick: true, actionPlanId: 1101),
                CreateActionSignal(PlayerActionKind.Push, 102, executedThisTick: true, actionPlanId: 1102),
                CreateActionSignal(PlayerActionKind.Push, 103, isRecoveryPhase: true, actionPlanId: 1103),
                CreateActionSignal(PlayerActionKind.Push, 104, executedThisTick: true, resolutionKind: TickPlayerActionResolutionKind.Blocked, actionPlanId: 1104),
                CreateActionSignal(PlayerActionKind.Flip, 201, startedThisTick: true, actionPlanId: 1201),
                CreateActionSignal(PlayerActionKind.Flip, 202, executedThisTick: true, actionPlanId: 1202),
                CreateActionSignal(PlayerActionKind.Flip, 203, isRecoveryPhase: true, actionPlanId: 1203),
                CreateActionSignal(PlayerActionKind.Flip, 204, executedThisTick: true, resolutionKind: TickPlayerActionResolutionKind.Impact, actionPlanId: 1204),
            };
        }

        private static TickPlayerActionPresentationSignal CreateActionSignal(
            PlayerActionKind actionKind,
            int sequenceId,
            bool startedThisTick = false,
            bool executedThisTick = false,
            bool isRecoveryPhase = false,
            TickPlayerActionResolutionKind resolutionKind = TickPlayerActionResolutionKind.Success,
            int actionPlanId = 0)
        {
            return new TickPlayerActionPresentationSignal(
                PlayerEntityId,
                actionKind,
                sequenceId,
                startedThisTick,
                completedThisTick: isRecoveryPhase,
                canceledThisTick: false,
                executedThisTick: executedThisTick,
                isRecoveryPhase: isRecoveryPhase,
                resolutionKind: resolutionKind,
                targetEntityId: TargetEntityId,
                direction: Direction.Right,
                actionPlanId: actionPlanId);
        }

        private static void AssertRequestPreservesCue(
            PresentationCue cue,
            GameplayAnimationPlaybackRequest request)
        {
            Assert.That(request.TickIndex, Is.EqualTo(cue.Source.TickIndex));
            Assert.That(request.Target, Is.EqualTo(cue.Target));
            Assert.That(request.Anchor, Is.EqualTo(cue.Anchor));
            Assert.That(request.AnimationPayload.ActionKind, Is.EqualTo(cue.AnimationPayload.ActionKind));
            Assert.That(request.AnimationPayload.PhaseKind, Is.EqualTo(cue.AnimationPayload.PhaseKind));
            Assert.That(request.AnimationPayload.OutcomeKind, Is.EqualTo(cue.AnimationPayload.OutcomeKind));
            Assert.That(request.AnimationPayload.SourceTickIndex, Is.EqualTo(cue.AnimationPayload.SourceTickIndex));
            Assert.That(request.AnimationPayload.SourceSequenceId, Is.EqualTo(cue.AnimationPayload.SourceSequenceId));
            Assert.That(request.AnimationPayload.SourceActionPlanId, Is.EqualTo(cue.AnimationPayload.SourceActionPlanId));
            Assert.That(request.AnimationPayload.TargetEntityId, Is.EqualTo(cue.AnimationPayload.TargetEntityId));
            Assert.That(request.OwnershipKey.SemanticSource, Is.EqualTo(cue.Source.SemanticSource));
        }

        private static PresentationAnimationCueKey GetCueKey(PresentationCue cue)
        {
            Assert.That(cue.Key.TryGetAnimationCueKey(out var key), Is.True);
            return key;
        }

        private static PlayerActionKind ToPlayerActionKind(PresentationAnimationActionKind actionKind)
        {
            return actionKind == PresentationAnimationActionKind.Push
                ? PlayerActionKind.Push
                : PlayerActionKind.Flip;
        }

        private static GameplayPresentationStateStore CreateStateStore(GameplayEntityView view)
        {
            var store = new GameplayPresentationStateStore();
            store.ViewsByEntityId[view.EntityId] = view;
            return store;
        }

        private static string ReadDirectory(string relativePath)
        {
            var absolutePath = ToAbsolutePath(relativePath);
            if (!System.IO.Directory.Exists(absolutePath))
            {
                return string.Empty;
            }

            return string.Join(
                "\n",
                System.IO.Directory.EnumerateFiles(absolutePath, "*.cs", System.IO.SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(System.IO.File.ReadAllText));
        }

        private static string ToAbsolutePath(string relativePath)
        {
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", relativePath));
        }

        private readonly struct DriverPlaybackSnapshot
        {
            public DriverPlaybackSnapshot(
                GameplayAnimationExecutorDiagnostics diagnostics,
                GameplayAnimationPlaybackRequest request,
                PlayerViewPresentationState driverState,
                PlayerPresentationPhase driverPhase,
                string lastCrossFadedStateName)
            {
                Diagnostics = diagnostics;
                Request = request;
                DriverState = driverState;
                DriverPhase = driverPhase;
                LastCrossFadedStateName = lastCrossFadedStateName;
            }

            public GameplayAnimationExecutorDiagnostics Diagnostics { get; }
            public GameplayAnimationPlaybackRequest Request { get; }
            public PlayerViewPresentationState DriverState { get; }
            public PlayerPresentationPhase DriverPhase { get; }
            public string LastCrossFadedStateName { get; }

            public static DriverPlaybackSnapshot From(
                GameplayAnimationExecutorDiagnostics diagnostics,
                GameplayAnimationPlaybackRequest request,
                PlayerAnimatorDriver driver)
            {
                return new DriverPlaybackSnapshot(
                    diagnostics,
                    request,
                    driver.LastPresentationState,
                    driver.CurrentPresentationPhase,
                    driver.LastCrossFadedStateName);
            }
        }

        private sealed class RecordingGameplayAnimationPlaybackPort : IGameplayAnimationPlaybackPort
        {
            private readonly Func<GameplayAnimationPlaybackRequest, GameplayAnimationPlaybackResult> _resultFactory;
            private readonly Func<GameplayAnimationPlaybackRequest, PlaybackDelegateResult> _delegate;
            private readonly List<GameplayAnimationPlaybackRequest> _requests = new();

            public RecordingGameplayAnimationPlaybackPort(
                GameplayAnimationPlaybackResultKind resultKind = GameplayAnimationPlaybackResultKind.Applied)
                : this(request => new GameplayAnimationPlaybackResult(resultKind))
            {
            }

            public RecordingGameplayAnimationPlaybackPort(
                Func<GameplayAnimationPlaybackRequest, GameplayAnimationPlaybackResultKind> resultFactory)
                : this(request => new GameplayAnimationPlaybackResult(resultFactory(request)))
            {
            }

            public RecordingGameplayAnimationPlaybackPort(PlaybackDelegate playbackDelegate)
            {
                _delegate = request =>
                {
                    var succeeded = playbackDelegate(request, out var result);
                    return new PlaybackDelegateResult(succeeded, result);
                };
            }

            private RecordingGameplayAnimationPlaybackPort(
                Func<GameplayAnimationPlaybackRequest, GameplayAnimationPlaybackResult> resultFactory)
            {
                _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
            }

            public delegate bool PlaybackDelegate(
                GameplayAnimationPlaybackRequest request,
                out GameplayAnimationPlaybackResult result);

            public int TryPlayCallCount { get; private set; }
            public int ResetSessionCallCount { get; private set; }
            public int HardCleanupCallCount { get; private set; }
            public IReadOnlyList<GameplayAnimationPlaybackRequest> Requests => _requests;

            public bool TryPlayPlayerActionAnimation(
                in GameplayAnimationPlaybackRequest request,
                out GameplayAnimationPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                if (_delegate != null)
                {
                    var delegateResult = _delegate(request);
                    result = delegateResult.Result;
                    return delegateResult.Succeeded;
                }

                result = _resultFactory(request);
                return result.Kind == GameplayAnimationPlaybackResultKind.Applied ||
                       result.Kind == GameplayAnimationPlaybackResultKind.Requested;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }

            private readonly struct PlaybackDelegateResult
            {
                public PlaybackDelegateResult(bool succeeded, GameplayAnimationPlaybackResult result)
                {
                    Succeeded = succeeded;
                    Result = result;
                }

                public bool Succeeded { get; }
                public GameplayAnimationPlaybackResult Result { get; }
            }
        }
    }
}
