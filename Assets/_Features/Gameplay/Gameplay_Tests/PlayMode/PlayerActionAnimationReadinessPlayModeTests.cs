using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class PlayerActionAnimationReadinessPlayModeTests
    {
        private const int PlayerEntityId = 10;
        private const int BoxEntityId = 40;
        private const int TargetEntityId = 50;
        private const string PlayerAnimatorControllerPath = "Assets/3DM/1Player/Player_S1.controller";

        private static readonly CubeTopologyState Topology = new(FaceId.Floor);
        private static readonly BoardBounds Bounds = new(new Vector2Int(-2, -2), new Vector2Int(4, 4));
        private static readonly SurfaceCell PlayerCell = new(FaceId.Floor, 0, 0);
        private static readonly SurfaceCell BoxCell = new(FaceId.Floor, 1, 0);
        private static readonly SurfaceCell TargetCell = new(FaceId.Floor, 2, 0);

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_DefaultMode_IsOrchestrationExecutor()
        {
            var port = new RecordingGameplayAnimationPlaybackPort();
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_DefaultMode_IsOrchestrationExecutor));
            try
            {
                Assert.That(Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), default(PlayerActionAnimationExecutionMode)), Is.False);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(context.Host.Presenter.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.False);
                Assert.That(context.Host.Presenter.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(
                    typeof(GameplaySceneHostConfiguration).GetField(nameof(PlayerActionAnimationExecutionMode)),
                    Is.Null,
                    "Player action animation execution mode must not be serialized into production scene host configuration.");

                context.Host.Presenter.Present(CreateSingleActionResult(
                    11,
                    CreateSignal(PlayerActionKind.Push, 101, started: true)));

                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.PlannedCueCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(1));
                Assert.That(context.Driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushWindup));

                context.Host.Presenter.ConfigurePlayerActionAnimationExecution((PlayerActionAnimationExecutionMode)999, port);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                context.Host.Presenter.Present(CreateSingleActionResult(
                    12,
                    CreateSignal(PlayerActionKind.Push, 102, started: true)));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_ControlledRoutesSupportedCues()
        {
            var port = new RecordingGameplayAnimationPlaybackPort();
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_ControlledRoutesSupportedCues));
            try
            {
                context.Host.Presenter.ConfigurePlayerActionAnimationExecution(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port);

                context.Host.Presenter.Present(CreateActionResult(
                    21,
                    CreateSupportedCueSignals()));

                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(12));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(12));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.PlannedCueCount, Is.EqualTo(12));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(12));
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(6));

                var routedKeys = port.Requests.Select(request => request.CueKey).ToArray();
                CollectionAssert.AreEquivalent(Enum.GetValues(typeof(PresentationAnimationCueKey))
                    .Cast<PresentationAnimationCueKey>()
                    .Where(key => key.ToString().StartsWith("PlayerPush", StringComparison.Ordinal) ||
                                  key.ToString().StartsWith("PlayerFlip", StringComparison.Ordinal))
                    .Where(key => key != PresentationAnimationCueKey.None)
                    .ToArray(), routedKeys);

                var pushExecute = port.Requests.Single(request => request.CueKey == PresentationAnimationCueKey.PlayerPushExecute);
                Assert.That(pushExecute.PlayerEntityId, Is.EqualTo(PlayerEntityId));
                Assert.That(pushExecute.AnimationPayload.ActionKind, Is.EqualTo(PresentationAnimationActionKind.Push));
                Assert.That(pushExecute.AnimationPayload.PhaseKind, Is.EqualTo(PresentationAnimationPhaseKind.Execute));
                Assert.That(pushExecute.AnimationPayload.TargetEntityId, Is.EqualTo(BoxEntityId));
                Assert.That(pushExecute.Target, Is.EqualTo(PresentationTarget.Entity(PlayerEntityId)));
                Assert.That(pushExecute.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_ConcreteAnimatorLifecycle()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_ConcreteAnimatorLifecycle));
            try
            {
                context.Host.Presenter.Present(CreateSingleActionResult(31, CreateSignal(PlayerActionKind.Push, 301, started: true)));
                yield return null;
                AssertDriverAndAnimator(context, PlayerPresentationPhase.PushWindup, "Push_Windup", 2);

                context.Host.Presenter.Present(CreateSingleActionResult(32, CreateSignal(PlayerActionKind.Push, 301, executed: true)));
                yield return null;
                AssertDriverAndAnimator(context, PlayerPresentationPhase.PushRecovery, "Push_Recovery", 3);

                context.Host.Presenter.Present(CreateSingleActionResult(33, CreateSignal(PlayerActionKind.Push, 301, recovery: true)));
                yield return null;
                AssertDriverAndAnimator(context, PlayerPresentationPhase.PushRecovery, "Push_Recovery", 3);

                context.Host.Presenter.PresentInitial(context.InitialEntities, Topology);
                yield return null;
                AssertDriverAndAnimator(context, PlayerPresentationPhase.None, "Idle", 4);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_PushWindupExecuteRecoveryTiming()
        {
            var legacy = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_PushWindupExecuteRecoveryTiming) + "_Legacy");
            var orchestration = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_PushWindupExecuteRecoveryTiming) + "_Orchestration");
            try
            {
                orchestration.Host.Presenter.ConfigurePlayerActionAnimationExecution(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
                legacy.Host.Presenter.ConfigurePlayerActionAnimationExecution(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);

                var legacySnapshots = new List<AnimatorPlaybackSnapshot>();
                var orchestrationSnapshots = new List<AnimatorPlaybackSnapshot>();
                foreach (var step in CreateLifecycle(PlayerActionKind.Push, 401, 41))
                {
                    legacy.Host.Presenter.Present(step);
                    orchestration.Host.Presenter.Present(step);
                    yield return null;
                    legacySnapshots.Add(Capture(legacy));
                    orchestrationSnapshots.Add(Capture(orchestration));
                }

                AssertLifecycleParity(legacySnapshots, orchestrationSnapshots, "Push_Windup", "Push_Recovery");
                Assert.That(orchestrationSnapshots[1].ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(1));
                Assert.That(orchestrationSnapshots[2].ExecuteCueMappedToRecoveryCommandCount, Is.Zero);
                Assert.That(orchestration.Driver.CrossFadeCommandCount, Is.EqualTo(3));
            }
            finally
            {
                legacy.Dispose();
                orchestration.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_FlipWindupExecuteRecoveryTiming()
        {
            var legacy = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_FlipWindupExecuteRecoveryTiming) + "_Legacy");
            var orchestration = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_FlipWindupExecuteRecoveryTiming) + "_Orchestration");
            try
            {
                orchestration.Host.Presenter.ConfigurePlayerActionAnimationExecution(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
                legacy.Host.Presenter.ConfigurePlayerActionAnimationExecution(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);

                var legacySnapshots = new List<AnimatorPlaybackSnapshot>();
                var orchestrationSnapshots = new List<AnimatorPlaybackSnapshot>();
                foreach (var step in CreateLifecycle(PlayerActionKind.Flip, 501, 51))
                {
                    legacy.Host.Presenter.Present(step);
                    orchestration.Host.Presenter.Present(step);
                    yield return null;
                    legacySnapshots.Add(Capture(legacy));
                    orchestrationSnapshots.Add(Capture(orchestration));
                }

                AssertLifecycleParity(legacySnapshots, orchestrationSnapshots, "Flip_Windup", "Flip_Recovery");
                Assert.That(orchestrationSnapshots[1].ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(1));
                Assert.That(orchestrationSnapshots[2].ExecuteCueMappedToRecoveryCommandCount, Is.Zero);
                Assert.That(orchestration.Driver.CrossFadeCommandCount, Is.EqualTo(3));
                Assert.That(orchestration.Host.Presenter.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                legacy.Dispose();
                orchestration.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_ExecuteLoweringTransitionGate()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_ExecuteLoweringTransitionGate));
            try
            {
                context.Host.Presenter.Present(CreateSingleActionResult(61, CreateSignal(PlayerActionKind.Push, 601, started: true)));
                yield return null;
                var windupCount = context.Driver.CrossFadeCommandCount;

                context.Host.Presenter.Present(CreateActionResult(
                    62,
                    new[]
                    {
                        CreateSignal(PlayerActionKind.Push, 601, executed: true),
                        CreateSignal(PlayerActionKind.Push, 602, recovery: true),
                    }));
                yield return null;

                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(2));
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(context.Driver.CrossFadeCommandCount, Is.EqualTo(windupCount + 1), "Execute lowering plus later recovery must not duplicate the recovery transition.");
                AssertDriverAndAnimator(context, PlayerPresentationPhase.PushRecovery, "Push_Recovery", windupCount + 1);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_BlockedImpactFailedOutcomes()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_BlockedImpactFailedOutcomes));
            try
            {
                var cases = new[]
                {
                    (CreateSignal(PlayerActionKind.Push, 701, executed: true, resolutionKind: TickPlayerActionResolutionKind.Blocked), PlayerPresentationPhase.PushRecovery, "Push_Recovery"),
                    (CreateSignal(PlayerActionKind.Push, 702, executed: true, resolutionKind: TickPlayerActionResolutionKind.Impact), PlayerPresentationPhase.PushRecovery, "Push_Recovery"),
                    (CreateSignal(PlayerActionKind.Push, 703, canceled: true), PlayerPresentationPhase.PushWindup, "Push_Windup"),
                    (CreateSignal(PlayerActionKind.Flip, 704, executed: true, resolutionKind: TickPlayerActionResolutionKind.Blocked), PlayerPresentationPhase.FlipRecovery, "Flip_Recovery"),
                    (CreateSignal(PlayerActionKind.Flip, 705, executed: true, resolutionKind: TickPlayerActionResolutionKind.Impact), PlayerPresentationPhase.FlipRecovery, "Flip_Recovery"),
                    (CreateSignal(PlayerActionKind.Flip, 706, canceled: true), PlayerPresentationPhase.FlipWindup, "Flip_Windup"),
                };

                var expectedCommandCounts = new[] { 2, 2, 3, 4, 4, 5 };
                for (var i = 0; i < cases.Length; i++)
                {
                    context.Host.Presenter.Present(CreateSingleActionResult(70 + i, cases[i].Item1));
                    yield return null;
                    AssertDriverAndAnimator(context, cases[i].Item2, cases[i].Item3, expectedCommandCounts[i]);
                }

                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_CurrentHostsMaintainAnimatorParity()
        {
            var legacy = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_CurrentHostsMaintainAnimatorParity) + "_CurrentA");
            var orchestration = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_CurrentHostsMaintainAnimatorParity) + "_CurrentB");
            try
            {
                legacy.Host.Presenter.ConfigurePlayerActionAnimationExecution(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
                orchestration.Host.Presenter.ConfigurePlayerActionAnimationExecution(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);

                var sequence = CreateLifecycle(PlayerActionKind.Push, 801, 81)
                    .Concat(CreateLifecycle(PlayerActionKind.Flip, 901, 91))
                    .ToArray();
                foreach (var result in sequence)
                {
                    legacy.Host.Presenter.Present(result);
                    orchestration.Host.Presenter.Present(result);
                    yield return null;
                    var legacySnapshot = Capture(legacy);
                    var orchestrationSnapshot = Capture(orchestration);
                    Assert.That(orchestrationSnapshot.Phase, Is.EqualTo(legacySnapshot.Phase));
                    Assert.That(orchestrationSnapshot.LastCrossFadedStateName, Is.EqualTo(legacySnapshot.LastCrossFadedStateName));
                    Assert.That(orchestrationSnapshot.AnimatorStateHash, Is.EqualTo(legacySnapshot.AnimatorStateHash));
                    Assert.That(orchestrationSnapshot.IsInTransition, Is.EqualTo(legacySnapshot.IsInTransition));
                }

                legacy.Host.Presenter.PresentInitial(legacy.InitialEntities, Topology);
                orchestration.Host.Presenter.PresentInitial(orchestration.InitialEntities, Topology);
                yield return null;
                Assert.That(Capture(orchestration).LastCrossFadedStateName, Is.EqualTo(Capture(legacy).LastCrossFadedStateName));
                Assert.That(orchestration.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                legacy.Dispose();
                orchestration.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_InvalidModeStillUsesExecutor()
        {
            var port = new RecordingGameplayAnimationPlaybackPort();
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_InvalidModeStillUsesExecutor));
            try
            {
                context.Host.Presenter.ConfigurePlayerActionAnimationExecution((PlayerActionAnimationExecutionMode)999, port);
                foreach (var step in CreateLifecycle(PlayerActionKind.Flip, 1001, 101))
                {
                    context.Host.Presenter.Present(step);
                    yield return null;
                }

                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(3));
                Assert.That(port.Requests.Last().CueKey, Is.EqualTo(PresentationAnimationCueKey.PlayerFlipRecovery));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(3));
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_DuplicateGuardNormalAndForced()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_DuplicateGuardNormalAndForced) + "_Normal");
            var forced = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_DuplicateGuardNormalAndForced) + "_Forced");
            try
            {
                var result = CreateSingleActionResult(111, CreateSignal(PlayerActionKind.Push, 1101, started: true));

                context.Host.Presenter.Present(result);
                yield return null;
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(context.Driver.CrossFadeCommandCount, Is.EqualTo(2));

                var duplicateSignal = CreateSignal(PlayerActionKind.Push, 1102, started: true);
                var duplicateResult = CreateActionResult(
                    112,
                    new[]
                    {
                        duplicateSignal,
                        duplicateSignal,
                    });
                forced.Host.Presenter.Present(duplicateResult);
                yield return null;
                Assert.That(forced.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.EqualTo(1));
                Assert.That(forced.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.DuplicateSuppressedCount, Is.EqualTo(1));
                Assert.That(forced.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(1));
                Assert.That(forced.Driver.CrossFadeCommandCount, Is.EqualTo(2));
                Assert.That(result.DeterminismHash, Is.EqualTo("PLAYER-ACTION-111"));
                Assert.That(duplicateResult.DeterminismHash, Is.EqualTo("PLAYER-ACTION-112"));
            }
            finally
            {
                context.Dispose();
                forced.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_MissingAnimatorDriverBindingPortAreNoOp()
        {
            var animatorMissing = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_MissingAnimatorDriverBindingPortAreNoOp) + "_AnimatorMissing", animatorMode: AnimatorFixtureMode.MissingController);
            try
            {
                animatorMissing.Host.Presenter.Present(CreateSingleActionResult(121, CreateSignal(PlayerActionKind.Push, 1201, started: true)));
                Assert.That(animatorMissing.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.AnimatorMissingCount, Is.EqualTo(1));
                Assert.That(animatorMissing.Driver.CrossFadeCommandCount, Is.Zero);
                Assert.That(animatorMissing.Host.Presenter.HasBlockingPresentation, Is.False);
            }
            finally
            {
                animatorMissing.Dispose();
            }

            var driverMissing = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_MissingAnimatorDriverBindingPortAreNoOp) + "_DriverMissing", attachDriver: false);
            try
            {
                driverMissing.Host.Presenter.Present(CreateSingleActionResult(122, CreateSignal(PlayerActionKind.Push, 1202, started: true)));
                Assert.That(driverMissing.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.DriverMissingCount, Is.EqualTo(1));
            }
            finally
            {
                driverMissing.Dispose();
            }

            var bindingMissing = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_MissingAnimatorDriverBindingPortAreNoOp) + "_BindingMissing", autoCreateViews: false);
            try
            {
                bindingMissing.Host.Presenter.Present(CreateSingleActionResult(123, CreateSignal(PlayerActionKind.Push, 1203, started: true)));
                Assert.That(bindingMissing.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.BindingMissingCount, Is.EqualTo(1));
            }
            finally
            {
                bindingMissing.Dispose();
            }

            var portMissingExecutor = new GameplayAnimationPresentationExecutor(
                playbackPort: null,
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                new PlayerActionAnimationExecutionGuard(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
            portMissingExecutor.Play(CreatePlanForPortMissing(124));
            Assert.That(portMissingExecutor.Diagnostics.MissingPortCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_LifecycleCleanupClearsAnimatorState()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_LifecycleCleanupClearsAnimatorState));
            try
            {
                context.Host.Presenter.Present(CreateSingleActionResult(131, CreateSignal(PlayerActionKind.Push, 1301, executed: true)));
                yield return null;
                AssertDriverAndAnimator(context, PlayerPresentationPhase.PushRecovery, "Push_Recovery", 2);

                context.Host.Presenter.PresentInitial(context.InitialEntities, Topology);
                yield return null;
                AssertDriverAndAnimator(context, PlayerPresentationPhase.None, "Idle", 3);
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.Zero);
                Assert.That(context.Animator.GetInteger(Animator.StringToHash("PlayerPresentationState")), Is.EqualTo((int)PlayerViewAnimationState.Idle));

                context.Host.Presenter.Present(CreateSingleActionResult(132, CreateSignal(PlayerActionKind.Push, 1302, recovery: true)));
                yield return null;
                AssertDriverAndAnimator(context, PlayerPresentationPhase.PushRecovery, "Push_Recovery", 4);

                context.Host.Presenter.DebugHardCleanupPresentationExtensions();
                Assert.That(context.Host.Presenter.PlayerActionAnimationOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_ActionAudioOwnershipRemainsSeparated()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_ActionAudioOwnershipRemainsSeparated));
            try
            {
                context.Host.Presenter.Present(CreateSingleActionResult(141, CreateSignal(PlayerActionKind.Push, 1401, executed: true)));
                yield return null;

                Assert.That(Enum.GetNames(typeof(GameplayActionAudioMoment)), Is.EquivalentTo(new[]
                {
                    nameof(GameplayActionAudioMoment.Windup),
                    nameof(GameplayActionAudioMoment.AssistOutOfRange),
                    nameof(GameplayActionAudioMoment.NoTarget),
                    nameof(GameplayActionAudioMoment.Invalid),
                }));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_IsNonBlockingAndInputLockNeutral()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_IsNonBlockingAndInputLockNeutral));
            try
            {
                context.Host.Presenter.Present(CreateSingleActionResult(151, CreateSignal(PlayerActionKind.Flip, 1501, started: true)));
                yield return null;

                Assert.That(context.Host.Presenter.HasBlockingPresentation, Is.False);
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.False);
                AssertBlockingSnapshotCleared(context.Host.Presenter.PlayerActionAnimationExecutionPipelineBlockingSnapshot);
                var next = context.Host.InputHost.RunSingleTick();
                Assert.That(next, Is.Not.Null);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator PlayerActionAnimationReadiness_PlayMode_IsNonAuthoritative()
        {
            var context = CreateHostContext(nameof(PlayerActionAnimationReadiness_PlayMode_IsNonAuthoritative));
            try
            {
                var result = CreateSingleActionResult(161, CreateSignal(PlayerActionKind.Push, 1601, executed: true));
                var hash = result.DeterminismHash;
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objective = result.ObjectiveResult;

                context.Host.Presenter.Present(result);
                yield return null;

                Assert.That(result.DeterminismHash, Is.EqualTo(hash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objective));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfxDamageVfxAndBoxMotion_ProductionDefaultsRemainStableAfterPlayerAnimationSmoke()
        {
            var context = CreateHostContext(nameof(CoreSfxDamageVfxAndBoxMotion_ProductionDefaultsRemainStableAfterPlayerAnimationSmoke));
            try
            {
                context.Host.Presenter.Present(CreateSingleActionResult(171, CreateSignal(PlayerActionKind.Push, 1701, started: true)));
                yield return null;

                Assert.That(context.Host.Presenter.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));

                context.Host.Presenter.ConfigurePlayerActionAnimationExecution((PlayerActionAnimationExecutionMode)999);
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(context.Host.Presenter.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
            }
            finally
            {
                context.Dispose();
            }
        }

        private static PlayerActionAnimationSmokeContext CreateHostContext(
            string rootName,
            bool autoCreateViews = true,
            bool attachDriver = true,
            AnimatorFixtureMode animatorMode = AnimatorFixtureMode.ProductionController)
        {
            var initialEntities = CreateInitialEntities();
            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<GameplaySceneHost>();
            hostObject.SetActive(true);
            host.Initialize(new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = autoCreateViews,
                CellSize = 1f,
                InitialBoardBounds = Bounds,
                InitialEntities = initialEntities,
                InitialTopology = Topology,
                PlayerEntityId = PlayerEntityId,
                MoveMotionDurationSeconds = 0.2f,
                PushMotionDurationSeconds = 0.2f,
                FlipMotionDurationSeconds = 0.2f,
                FlipArcHeightInCells = 0.65f,
                BoxSlideStepIntervalSeconds = 0.2f,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
                ViewFactory = autoCreateViews
                    ? new PlayerActionAnimationSmokeViewFactory(hostObject.transform, attachDriver, animatorMode)
                    : null,
            });

            host.ViewRegistry.TryGetView(PlayerEntityId, out var playerView);
            var driver = playerView != null ? playerView.GetComponent<PlayerAnimatorDriver>() : null;
            var animator = driver != null ? playerView.GetComponentInChildren<Animator>() : null;
            return new PlayerActionAnimationSmokeContext(hostObject, host, initialEntities, playerView, driver, animator);
        }

        private static EntityState[] CreateInitialEntities()
        {
            return new[]
            {
                CreateUnit(PlayerEntityId, PlayerCell, UnitRole.Player),
                CreateBox(BoxEntityId, BoxCell),
                CreateUnit(TargetEntityId, TargetCell, UnitRole.Enemy),
            };
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, UnitRole role)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = role == UnitRole.Player ? 1 : 2,
                type = EntityType.Unit,
                unitRole = role,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }

        private static TickResult CreateSingleActionResult(int tickIndex, TickPlayerActionPresentationSignal signal)
        {
            return CreateActionResult(tickIndex, new[] { signal });
        }

        private static TickResult CreateActionResult(int tickIndex, IEnumerable<TickPlayerActionPresentationSignal> signals)
        {
            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetPrivateField(typeof(TickResult), result, "<PresentationData>k__BackingField", CreatePresentationData(signals));
            SetPrivateField(typeof(TickResult), result, "<FinalTopology>k__BackingField", Topology);
            SetPrivateField(typeof(TickResult), result, "<DeterminismHash>k__BackingField", $"PLAYER-ACTION-{tickIndex}");
            SetPrivateField(typeof(TickResult), result, "<Trace>k__BackingField", TickTrace.Empty);
            SetPrivateField(typeof(TickResult), result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetPrivateField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>(CreateInitialEntities())));
            SetPrivateField(
                typeof(TickResult),
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string> { $"AuthoritativeEvent:{tickIndex}" }));
            return result;
        }

        private static TickPresentationData CreatePresentationData(IEnumerable<TickPlayerActionPresentationSignal> signals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                signals);
        }

        private static TickPlayerActionPresentationSignal CreateSignal(
            PlayerActionKind actionKind,
            int sequence,
            bool started = false,
            bool executed = false,
            bool recovery = false,
            bool canceled = false,
            TickPlayerActionResolutionKind resolutionKind = TickPlayerActionResolutionKind.Success)
        {
            return new TickPlayerActionPresentationSignal(
                PlayerEntityId,
                actionKind,
                sequence,
                started,
                completedThisTick: recovery,
                canceled,
                executed,
                isRecoveryPhase: recovery,
                resolutionKind,
                BoxEntityId,
                Direction.Right,
                actionPlanId: 9000 + sequence,
                flipOutcome: actionKind == PlayerActionKind.Flip ? TickPlayerFlipOutcomeKind.FollowThrough : TickPlayerFlipOutcomeKind.None,
                hasFlipImpactContactTiming: resolutionKind == TickPlayerActionResolutionKind.Impact,
                flipTargetBoxEntityId: actionKind == PlayerActionKind.Flip ? BoxEntityId : 0);
        }

        private static TickPlayerActionPresentationSignal[] CreateSupportedCueSignals()
        {
            return new[]
            {
                CreateSignal(PlayerActionKind.Push, 201, started: true),
                CreateSignal(PlayerActionKind.Push, 202, executed: true),
                CreateSignal(PlayerActionKind.Push, 203, recovery: true),
                CreateSignal(PlayerActionKind.Push, 204, executed: true, resolutionKind: TickPlayerActionResolutionKind.Blocked),
                CreateSignal(PlayerActionKind.Push, 205, executed: true, resolutionKind: TickPlayerActionResolutionKind.Impact),
                CreateSignal(PlayerActionKind.Push, 206, canceled: true),
                CreateSignal(PlayerActionKind.Flip, 207, started: true),
                CreateSignal(PlayerActionKind.Flip, 208, executed: true),
                CreateSignal(PlayerActionKind.Flip, 209, recovery: true),
                CreateSignal(PlayerActionKind.Flip, 210, executed: true, resolutionKind: TickPlayerActionResolutionKind.Blocked),
                CreateSignal(PlayerActionKind.Flip, 211, executed: true, resolutionKind: TickPlayerActionResolutionKind.Impact),
                CreateSignal(PlayerActionKind.Flip, 212, canceled: true),
            };
        }

        private static IReadOnlyList<TickResult> CreateLifecycle(PlayerActionKind actionKind, int sequence, int tickStart)
        {
            return new[]
            {
                CreateSingleActionResult(tickStart, CreateSignal(actionKind, sequence, started: true)),
                CreateSingleActionResult(tickStart + 1, CreateSignal(actionKind, sequence, executed: true)),
                CreateSingleActionResult(tickStart + 2, CreateSignal(actionKind, sequence, recovery: true)),
            };
        }

        private static PresentationPlaybackPlan CreatePlanForPortMissing(int tickIndex)
        {
            var payload = new PresentationAnimationPayload(
                PresentationAnimationFactKind.PlayerAction,
                PlayerEntityId,
                PresentationAnimationActionKind.Push,
                PresentationAnimationPhaseKind.Windup,
                PresentationAnimationOutcomeKind.Started,
                tickIndex,
                sourceSequenceId: 12401,
                sourceActionPlanId: 21401,
                targetEntityId: BoxEntityId,
                direction: Direction.Right);
            var cue = new PresentationCue(
                PresentationDomain.Animation,
                PresentationCueKey.ForAnimation(PresentationAnimationCueKey.PlayerPushWindup),
                new PresentationSource(
                    tickIndex,
                    PresentationSemanticSource.PlayerAction,
                    PlayerEntityId,
                    (int)PresentationAnimationActionKind.Push,
                    12401),
                PresentationTarget.Entity(PlayerEntityId),
                PresentationAnchor.ForEntityVisualRoot(PlayerEntityId),
                PresentationPlaybackPolicyHint.OneShot(12401),
                animationPayload: payload);
            var cueFrame = new PresentationCueFrame(
                tickIndex,
                new[] { cue },
                new PresentationCueFrameDiagnostics(1, 1, 0));
            return new PresentationPlaybackPlanner().Plan(cueFrame);
        }

        private static void AssertDriverAndAnimator(
            PlayerActionAnimationSmokeContext context,
            PlayerPresentationPhase phase,
            string expectedStateName,
            int expectedCrossFadeCount)
        {
            Assert.That(context.Driver, Is.Not.Null);
            Assert.That(context.Animator, Is.Not.Null);
            context.Animator.Update(0f);
            var snapshot = Capture(context);
            Assert.That(snapshot.Phase, Is.EqualTo(phase));
            Assert.That(snapshot.LastCrossFadedStateName, Is.EqualTo(expectedStateName));
            Assert.That(snapshot.CrossFadeCommandCount, Is.EqualTo(expectedCrossFadeCount));
            Assert.That(
                snapshot.AnimatorStateHash == Animator.StringToHash(expectedStateName) ||
                snapshot.AnimatorNextStateHash == Animator.StringToHash(expectedStateName),
                Is.True,
                $"Animator should target state {expectedStateName}.");
        }

        private static void AssertLifecycleParity(
            IReadOnlyList<AnimatorPlaybackSnapshot> legacy,
            IReadOnlyList<AnimatorPlaybackSnapshot> orchestration,
            string windupState,
            string recoveryState)
        {
            Assert.That(legacy, Has.Count.EqualTo(3));
            Assert.That(orchestration, Has.Count.EqualTo(3));
            for (var i = 0; i < legacy.Count; i++)
            {
                Assert.That(orchestration[i].Phase, Is.EqualTo(legacy[i].Phase), $"step {i}");
                Assert.That(orchestration[i].LastCrossFadedStateName, Is.EqualTo(legacy[i].LastCrossFadedStateName), $"step {i}");
                Assert.That(orchestration[i].AnimatorStateHash, Is.EqualTo(legacy[i].AnimatorStateHash), $"step {i}");
            }

            Assert.That(orchestration[0].LastCrossFadedStateName, Is.EqualTo(windupState));
            Assert.That(orchestration[1].LastCrossFadedStateName, Is.EqualTo(recoveryState));
            Assert.That(orchestration[2].LastCrossFadedStateName, Is.EqualTo(recoveryState));
            Assert.That(orchestration[1].CommandAppliedCount, Is.EqualTo(1));
            Assert.That(orchestration[1].ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(1));
            Assert.That(orchestration[2].CommandAppliedCount, Is.EqualTo(1));
            Assert.That(orchestration[2].ExecuteCueMappedToRecoveryCommandCount, Is.Zero);
            Assert.That(orchestration[2].CrossFadeCommandCount, Is.EqualTo(3));
        }

        private static void AssertBlockingSnapshotCleared(PresentationBlockingSnapshot snapshot)
        {
            Assert.That(snapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(snapshot.HasActiveBlockingPresentation, Is.False);
        }

        private static AnimatorPlaybackSnapshot Capture(PlayerActionAnimationSmokeContext context)
        {
            context.Animator?.Update(0f);
            var state = context.Animator != null && context.Animator.runtimeAnimatorController != null
                ? context.Animator.GetCurrentAnimatorStateInfo(0)
                : default;
            var nextState = context.Animator != null &&
                            context.Animator.runtimeAnimatorController != null &&
                            context.Animator.IsInTransition(0)
                ? context.Animator.GetNextAnimatorStateInfo(0)
                : default;
            return new AnimatorPlaybackSnapshot(
                context.Driver != null ? context.Driver.CurrentPresentationPhase : PlayerPresentationPhase.None,
                context.Driver != null ? context.Driver.LastCrossFadedStateName : string.Empty,
                context.Driver != null ? context.Driver.CrossFadeCommandCount : 0,
                context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount,
                context.Host.Presenter.PlayerActionAnimationExecutorDiagnostics.ExecuteCueMappedToRecoveryCommandCount,
                context.Animator != null && context.Animator.runtimeAnimatorController != null && context.Animator.IsInTransition(0),
                state.shortNameHash,
                nextState.shortNameHash);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetPrivateField(Type targetType, object target, string fieldName, object value)
        {
            var field = targetType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {targetType.Name}.");
            field.SetValue(target, value);
        }

        private sealed class PlayerActionAnimationSmokeContext : IDisposable
        {
            private readonly GameObject _rootObject;

            public PlayerActionAnimationSmokeContext(
                GameObject rootObject,
                GameplaySceneHost host,
                EntityState[] initialEntities,
                GameplayEntityView playerView,
                PlayerAnimatorDriver driver,
                Animator animator)
            {
                _rootObject = rootObject;
                Host = host;
                InitialEntities = initialEntities;
                PlayerView = playerView;
                Driver = driver;
                Animator = animator;
            }

            public GameplaySceneHost Host { get; }

            public EntityState[] InitialEntities { get; }

            public GameplayEntityView PlayerView { get; }

            public PlayerAnimatorDriver Driver { get; }

            public Animator Animator { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class PlayerActionAnimationSmokeViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _hostRoot;
            private readonly bool _attachDriver;
            private readonly AnimatorFixtureMode _animatorMode;

            public PlayerActionAnimationSmokeViewFactory(
                Transform hostRoot,
                bool attachDriver,
                AnimatorFixtureMode animatorMode)
            {
                _hostRoot = hostRoot;
                _attachDriver = attachDriver;
                _animatorMode = animatorMode;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"PlayerActionAnimationSmokeView_{entity.entityId}");
                var boardRoot = _hostRoot.GetComponentInChildren<GameplayBoardRoot>(includeInactive: true);
                var parent = boardRoot != null ? boardRoot.EntityRoot : _hostRoot;
                viewObject.transform.SetParent(parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                view.EnsureModelRoot();
                if (entity.entityId == PlayerEntityId && _attachDriver)
                {
                    viewObject.AddComponent<PlayerAnimationTimingAuthoring>();
                    var driver = viewObject.AddComponent<PlayerAnimatorDriver>();
                    var animatorObject = new GameObject("ProductionPlayerAnimator");
                    animatorObject.transform.SetParent(viewObject.transform, worldPositionStays: false);
                    var animator = animatorObject.AddComponent<Animator>();
                    if (_animatorMode == AnimatorFixtureMode.ProductionController)
                    {
                        animator.runtimeAnimatorController = LoadProductionPlayerController();
                        Assert.That(animator.runtimeAnimatorController, Is.Not.Null, PlayerAnimatorControllerPath);
                    }

                    SetPrivateField(driver, "animator", animator);
                }

                return view;
            }
        }

        private sealed class RecordingGameplayAnimationPlaybackPort : IGameplayAnimationPlaybackPort
        {
            private readonly List<GameplayAnimationPlaybackRequest> _requests = new();

            public IReadOnlyList<GameplayAnimationPlaybackRequest> Requests => _requests;

            public int TryPlayCallCount { get; private set; }

            public bool TryPlayPlayerActionAnimation(
                in GameplayAnimationPlaybackRequest request,
                out GameplayAnimationPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                result = new GameplayAnimationPlaybackResult(
                    GameplayAnimationPlaybackResultKind.Applied,
                    request.AnimationPayload.PhaseKind == PresentationAnimationPhaseKind.Execute);
                return true;
            }

            public void ResetSession()
            {
                _requests.Clear();
                TryPlayCallCount = 0;
            }

            public void HardCleanup()
            {
                ResetSession();
            }
        }

        private readonly struct AnimatorPlaybackSnapshot
        {
            public AnimatorPlaybackSnapshot(
                PlayerPresentationPhase phase,
                string lastCrossFadedStateName,
                int crossFadeCommandCount,
                int commandAppliedCount,
                int executeCueMappedToRecoveryCommandCount,
                bool isInTransition,
                int animatorStateHash,
                int animatorNextStateHash)
            {
                Phase = phase;
                LastCrossFadedStateName = lastCrossFadedStateName;
                CrossFadeCommandCount = crossFadeCommandCount;
                CommandAppliedCount = commandAppliedCount;
                ExecuteCueMappedToRecoveryCommandCount = executeCueMappedToRecoveryCommandCount;
                IsInTransition = isInTransition;
                AnimatorStateHash = animatorStateHash;
                AnimatorNextStateHash = animatorNextStateHash;
            }

            public PlayerPresentationPhase Phase { get; }

            public string LastCrossFadedStateName { get; }

            public int CrossFadeCommandCount { get; }

            public int CommandAppliedCount { get; }

            public int ExecuteCueMappedToRecoveryCommandCount { get; }

            public bool IsInTransition { get; }

            public int AnimatorStateHash { get; }

            public int AnimatorNextStateHash { get; }
        }

        private enum AnimatorFixtureMode
        {
            ProductionController,
            MissingController,
        }

        private static RuntimeAnimatorController LoadProductionPlayerController()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorControllerPath);
#else
            return null;
#endif
        }
    }
}
