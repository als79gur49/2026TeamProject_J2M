using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class BoxMotionReadinessPlayModeTests
    {
        private const int PlayerEntityId = 10;
        private const int BoxEntityId = 40;
        private const int ImpactTargetEntityId = 50;
        private const float PositionTolerance = 0.0001f;
        private const float RotationToleranceDegrees = 0.001f;
        private static readonly CubeTopologyState Topology = new(FaceId.Floor);
        private static readonly BoardBounds Bounds = new(new Vector2Int(-2, -2), new Vector2Int(4, 4));
        private static readonly SurfaceCell SlideSourceCell = new(FaceId.Floor, 0, 0);
        private static readonly SurfaceCell SlideDestinationCell = new(FaceId.Floor, 1, 0);
        private static readonly SurfaceCell FlipSourceCell = new(FaceId.Floor, 1, 0);
        private static readonly SurfaceCell FlipDestinationCell = new(FaceId.Floor, 1, 1);
        private static readonly SurfaceCell ImpactCell = new(FaceId.Floor, 1, 2);

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_UsesOrchestrationOwner()
        {
            var context = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_UsesOrchestrationOwner));
            try
            {
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
                Assert.That(context.Host.Presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(context.Host.Presenter.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);

                context.Host.Presenter.Present(CreateSlideResult(11));

                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.LastExecutionOwner, Is.EqualTo(BoxMotionPresentationExecutionOwner.CurrentExecutor));
                Assert.That(
                    typeof(GameplaySceneHostConfiguration).GetField("BoxMotionPresentationExecutionMode"),
                    Is.Null,
                    "Box motion execution mode must not be serialized into production scene host configuration.");
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_RoutesSlideFlipImpact()
        {
            var port = new RecordingGameplayMotionPlaybackPort();
            var context = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_RoutesSlideFlipImpact));
            try
            {
                context.Host.Presenter.ConfigureBoxMotionPlaybackPort(port);

                context.Host.Presenter.Present(CreateCombinedRoutingResult(21));

                Assert.That(port.TryPlayCallCount, Is.EqualTo(3));
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.TrackStartedCount, Is.EqualTo(3));
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(3));
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                var telemetry = context.Host.Presenter.BoxMotionProductionTelemetrySnapshot;
                Assert.That(telemetry.IsCurrentProductionOwner, Is.True);
                Assert.That(telemetry.ExecutorOwnerExecutedCount, Is.EqualTo(3));
                Assert.That(telemetry.DuplicateOwnerAttemptCount, Is.Zero);
                Assert.That(telemetry.SemanticDiagnostics.Single(item => item.Semantic == PresentationMotionFactKind.BoxSlide).StartedCount, Is.EqualTo(1));
                Assert.That(telemetry.SemanticDiagnostics.Single(item => item.Semantic == PresentationMotionFactKind.BoxFlip).StartedCount, Is.EqualTo(1));
                Assert.That(telemetry.SemanticDiagnostics.Single(item => item.Semantic == PresentationMotionFactKind.BoxFlipImpact).StartedCount, Is.EqualTo(1));
                AssertMotionRequest(port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxSlide), PresentationMotionCueKey.BoxSlide, 21, SlideSourceCell, SlideDestinationCell);
                AssertMotionRequest(port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxFlip), PresentationMotionCueKey.BoxFlip, 21, FlipSourceCell, FlipDestinationCell);
                AssertMotionRequest(port.Requests.Single(request => request.CueKey == PresentationMotionCueKey.BoxFlipImpact), PresentationMotionCueKey.BoxFlipImpact, 21, FlipDestinationCell, ImpactCell);
                AssertBlockingSnapshotCleared(context.Host.Presenter.BoxMotionExecutionPipelineBlockingSnapshot);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_ConcreteAdapterStartsAndCompletesTracks()
        {
            var context = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_ConcreteAdapterStartsAndCompletesTracks));
            try
            {
                context.Host.Presenter.Present(CreateSlideResult(31));

                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.DefaultAdapterDiagnostics.StartedCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.EqualTo(1));

                AdvancePresentation(context.Host, context.Host.TimingProfile.MoveMotionDurationSeconds * 0.5f);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.EqualTo(1));

                AdvancePresentation(context.Host, context.Host.TimingProfile.MoveMotionDurationSeconds + context.Host.TimingProfile.SimulationTickIntervalSeconds);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.ActiveTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.PlaybackTrackCompletedCount, Is.GreaterThanOrEqualTo(1));

                context.Host.Presenter.PresentInitial(context.InitialEntities, Topology);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.DefaultAdapterDiagnostics.HasTickContext, Is.False);
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(BoxMotionTelemetryCleanupReason.PresentInitial));

                context.Host.Presenter.Present(CreateSlideResult(31));
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.DefaultAdapterDiagnostics.StartedCount, Is.EqualTo(2));
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.DuplicateRejectedCount, Is.Zero);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_SlidePoseRemainsEquivalent()
        {
            var context = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_SlidePoseRemainsEquivalent));
            try
            {
                context.Host.Presenter.Present(CreateSlideResult(41));

                var sourcePosition = ProjectWorldPosition(context.Host, SlideSourceCell, EntityType.Box);
                var destinationPosition = ProjectWorldPosition(context.Host, SlideDestinationCell, EntityType.Box);
                var view = GetView(context.Host, BoxEntityId);
                AssertVectorClose(view.transform.position, sourcePosition);

                AdvancePresentation(context.Host, context.Host.TimingProfile.MoveMotionDurationSeconds * 0.5f);
                Assert.That(Vector3.Distance(view.transform.position, sourcePosition), Is.GreaterThan(PositionTolerance));
                Assert.That(Vector3.Distance(view.transform.position, destinationPosition), Is.GreaterThan(PositionTolerance));
                Assert.That(context.Host.Presenter.HasBlockingPresentation, Is.False);

                AdvancePresentation(context.Host, context.Host.TimingProfile.MoveMotionDurationSeconds + context.Host.TimingProfile.SimulationTickIntervalSeconds);
                AssertVectorClose(view.transform.position, destinationPosition);
                AssertVisualRootReset(view);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_FlipPoseAndVisualRootReset()
        {
            var context = CreateHostContext(
                nameof(BoxMotionProductionDefault_PlayMode_FlipPoseAndVisualRootReset),
                initialBoxCell: FlipSourceCell);
            try
            {
                context.Host.Presenter.Present(CreateFlipResult(51, includeActiveFlipSignal: true));

                var sourcePosition = ProjectWorldPosition(context.Host, FlipSourceCell, EntityType.Box);
                var destinationPosition = ProjectWorldPosition(context.Host, FlipDestinationCell, EntityType.Box);
                var view = GetView(context.Host, BoxEntityId);
                var visualRoot = view.ModelRoot;
                AssertVectorClose(view.transform.position, sourcePosition);

                AdvancePresentation(context.Host, context.Host.TimingProfile.FlipMotionDurationSeconds * 0.5f);
                Assert.That(Vector3.Distance(view.transform.position, sourcePosition), Is.GreaterThan(PositionTolerance));
                Assert.That(
                    visualRoot.localPosition.sqrMagnitude > PositionTolerance ||
                    Quaternion.Angle(visualRoot.localRotation, Quaternion.identity) > RotationToleranceDegrees,
                    Is.True,
                    "Flip interaction should move or rotate the box visual root while active.");

                AdvancePresentation(context.Host, context.Host.TimingProfile.FlipMotionDurationSeconds + context.Host.TimingProfile.SimulationTickIntervalSeconds);
                AssertVectorClose(view.transform.position, destinationPosition);

                context.Host.Presenter.Present(CreateFlipCompletionResult(52));
                context.Host.Presenter.UpdatePresentation(context.Host.TimingProfile.SimulationTickIntervalSeconds);
                AssertVisualRootReset(view);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.FlipInteractionTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.CleanupDiagnostics.VisualRootPositionResetCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.CleanupDiagnostics.VisualRootRotationResetCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.CleanupDiagnostics.FlipDriverResetCount, Is.GreaterThanOrEqualTo(1));
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_FlipImpactRemainsSeparateSemantic()
        {
            var port = new RecordingGameplayMotionPlaybackPort();
            var context = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_FlipImpactRemainsSeparateSemantic));
            try
            {
                context.Host.Presenter.ConfigureBoxMotionPlaybackPort(port);

                context.Host.Presenter.Present(CreateCombinedRoutingResult(61));

                var flipIndex = port.Requests.FindIndex(request => request.CueKey == PresentationMotionCueKey.BoxFlip);
                var impactIndex = port.Requests.FindIndex(request => request.CueKey == PresentationMotionCueKey.BoxFlipImpact);
                var flip = port.Requests[flipIndex];
                var impact = port.Requests[impactIndex];
                Assert.That(flipIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(impactIndex, Is.GreaterThan(flipIndex));
                Assert.That(impact.MotionPayload.Kind, Is.EqualTo(PresentationMotionFactKind.BoxFlipImpact));
                Assert.That(impact.MotionPayload.ImpactTargetEntityId, Is.EqualTo(ImpactTargetEntityId));
                Assert.That(impact.MotionPayload.ActorEntityId, Is.EqualTo(PlayerEntityId));
                Assert.That(impact.OwnershipKey.CueKey, Is.Not.EqualTo(flip.OwnershipKey.CueKey));
                Assert.That(impact.OwnershipKey.GetHashCode(), Is.Not.EqualTo(flip.OwnershipKey.GetHashCode()));
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.DuplicateRejectedCount, Is.Zero);
                AssertBlockingSnapshotCleared(context.Host.Presenter.BoxMotionExecutionPipelineBlockingSnapshot);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_DuplicateGuardNormalAndForced()
        {
            var port = new RecordingGameplayMotionPlaybackPort();
            var context = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_DuplicateGuardNormalAndForced));
            try
            {
                context.Host.Presenter.ConfigureBoxMotionPlaybackPort(port);
                var result = CreateSlideResult(71);

                context.Host.Presenter.Present(result);
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.DuplicateRejectedCount, Is.Zero);

                context.Host.Presenter.Present(result);
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.DuplicateRejectedCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.DuplicateOwnerAttemptCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.DuplicateRejectedCount, Is.EqualTo(1));
                Assert.That(result.DeterminismHash, Is.EqualTo("BOX-MOTION-71"));
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_MissingDiagnosticsAreNoOp()
        {
            var portMissingContext = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_MissingDiagnosticsAreNoOp) + "_PortMissing");
            try
            {
                portMissingContext.Host.Presenter.ConfigureBoxMotionPlaybackPort(
                    playbackPort: null,
                    useDefaultPlaybackPort: false);
                portMissingContext.Host.Presenter.Present(CreateSlideResult(81));
                Assert.That(portMissingContext.Host.Presenter.BoxMotionExecutorDiagnostics.MissingPortCount, Is.EqualTo(1));
                Assert.That(portMissingContext.Host.Presenter.BoxMotionProductionTelemetrySnapshot.PortMissingCount, Is.EqualTo(1));
                Assert.That(portMissingContext.Host.Presenter.BoxMotionProductionTelemetrySnapshot.LastFailureReason, Is.EqualTo(BoxMotionTelemetryFailureReason.PortMissing));
                Assert.That(portMissingContext.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                AssertBlockingSnapshotCleared(portMissingContext.Host.Presenter.BoxMotionExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                portMissingContext.Dispose();
            }

            var bindingMissingContext = CreateHostContext(
                nameof(BoxMotionProductionDefault_PlayMode_MissingDiagnosticsAreNoOp) + "_BindingMissing",
                autoCreateViews: false);
            try
            {
                bindingMissingContext.Host.Presenter.Present(CreateSlideResult(82));
                Assert.That(bindingMissingContext.Host.Presenter.BoxMotionExecutorDiagnostics.BindingMissingCount, Is.EqualTo(1));
                Assert.That(bindingMissingContext.Host.Presenter.BoxMotionProductionTelemetrySnapshot.BindingMissingCount, Is.EqualTo(1));
                Assert.That(bindingMissingContext.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                AssertBlockingSnapshotCleared(bindingMissingContext.Host.Presenter.BoxMotionExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                bindingMissingContext.Dispose();
            }

            var driverMissingContext = CreateHostContext(
                nameof(BoxMotionProductionDefault_PlayMode_MissingDiagnosticsAreNoOp) + "_DriverMissing",
                attachBoxDriver: false);
            try
            {
                driverMissingContext.Host.Presenter.Present(CreateFlipImpactResult(83));
                Assert.That(driverMissingContext.Host.Presenter.BoxMotionExecutorDiagnostics.DriverMissingCount, Is.EqualTo(1));
                Assert.That(driverMissingContext.Host.Presenter.BoxMotionProductionTelemetrySnapshot.DriverMissingCount, Is.EqualTo(1));
                Assert.That(driverMissingContext.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                AssertBlockingSnapshotCleared(driverMissingContext.Host.Presenter.BoxMotionExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                driverMissingContext.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_LifecycleCleanupClearsTrackAndPose()
        {
            var context = CreateHostContext(
                nameof(BoxMotionProductionDefault_PlayMode_LifecycleCleanupClearsTrackAndPose),
                initialBoxCell: FlipSourceCell);
            try
            {
                context.Host.Presenter.Present(CreateFlipResult(91, includeActiveFlipSignal: true));
                AdvancePresentation(context.Host, context.Host.TimingProfile.FlipMotionDurationSeconds * 0.5f);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.FlipInteractionTrackCount, Is.EqualTo(1));

                var view = GetView(context.Host, BoxEntityId);
                context.Host.Presenter.PresentInitial(context.InitialEntities, Topology);
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.ObservedTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.FlipInteractionTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.CompletedPresentationMotionKeyCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.DefaultAdapterDiagnostics.HasTickContext, Is.False);
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(BoxMotionTelemetryCleanupReason.PresentInitial));
                AssertVisualRootReset(view);

                context.Host.Presenter.Present(CreateSlideResult(92));
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.EqualTo(1));
                context.Host.Presenter.DebugHardCleanupPresentationExtensions();
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.ObservedTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.ActiveLocalMotionTrackCount, Is.Zero);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.DefaultAdapterDiagnostics.HasTickContext, Is.False);
                Assert.That(context.Host.Presenter.BoxMotionRuntimeDebugSnapshot.DefaultAdapterDiagnostics.HardCleanupCallCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(BoxMotionTelemetryCleanupReason.HardCleanupPresentationExtensions));
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.CleanupDiagnostics.StaleTrackClearedCount, Is.GreaterThanOrEqualTo(1));
                AssertVisualRootReset(view);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotion_LegacyRoute_NotReachable_PlayMode()
        {
            var port = new RecordingGameplayMotionPlaybackPort();
            var context = CreateHostContext(nameof(BoxMotion_LegacyRoute_NotReachable_PlayMode));
            try
            {
                context.Host.Presenter.ConfigureBoxMotionPlaybackPort(port);
                context.Host.Presenter.Present(CreateSlideResult(101));

                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                Assert.That(context.Host.Presenter.BoxMotionOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.BoxMotionProductionTelemetrySnapshot.IsCurrentProductionOwner, Is.True);
                Assert.That(typeof(GameplayTickViewPresenter).GetMethod(
                    "ConfigureBoxMotionPresentationExecution",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                    Is.Null);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_IsNonBlockingAndInputLockNeutral()
        {
            var context = CreateHostContext(
                nameof(BoxMotionProductionDefault_PlayMode_IsNonBlockingAndInputLockNeutral),
                initialBoxCell: FlipSourceCell);
            try
            {
                context.Host.Presenter.Present(CreateFlipResult(111, includeActiveFlipSignal: true));

                AssertBlockingSnapshotCleared(context.Host.Presenter.BoxMotionExecutionPipelineBlockingSnapshot);
                Assert.That(context.Host.Presenter.HasBlockingPresentation, Is.False);
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.False);
                Assert.That(context.Host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(context.Host.Presenter.HasBlockingPresentation, Is.False);
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.False);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator BoxMotionProductionDefault_PlayMode_IsNonAuthoritative()
        {
            var context = CreateHostContext(nameof(BoxMotionProductionDefault_PlayMode_IsNonAuthoritative));
            try
            {
                var result = CreateCombinedRoutingResult(121);
                var determinismHash = result.DeterminismHash;
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;

                context.Host.Presenter.Present(result);
                AdvancePresentation(context.Host, context.Host.TimingProfile.FlipMotionDurationSeconds + context.Host.TimingProfile.SimulationTickIntervalSeconds);

                Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfxAndDamageDeathVfx_RemainStableAfterBoxMotionSwitch()
        {
            var context = CreateHostContext(nameof(CoreSfxAndDamageDeathVfx_RemainStableAfterBoxMotionSwitch));
            try
            {
                context.Host.Presenter.Present(CreateSlideResult(131));

                Assert.That(context.Host.Presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }

        private static BoxMotionSmokeContext CreateHostContext(
            string rootName,
            bool autoCreateViews = true,
            bool attachBoxDriver = true,
            SurfaceCell? initialBoxCell = null)
        {
            var initialEntities = CreateInitialEntities(initialBoxCell ?? SlideSourceCell);
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
                    ? new BoxMotionSmokeViewFactory(hostObject.transform, attachBoxDriver)
                    : null,
            });

            return new BoxMotionSmokeContext(hostObject, host, initialEntities);
        }

        private static EntityState[] CreateInitialEntities(SurfaceCell boxCell)
        {
            return new[]
            {
                CreateUnit(PlayerEntityId, new SurfaceCell(FaceId.Floor, -1, 0), UnitRole.Player),
                CreateBox(BoxEntityId, boxCell),
                CreateUnit(ImpactTargetEntityId, ImpactCell, UnitRole.Enemy),
            };
        }

        private static TickResult CreateSlideResult(int tickIndex)
        {
            return CreateTickResult(
                tickIndex,
                finalBoxCell: SlideDestinationCell,
                presentationData: CreatePresentationData(
                    entityMotions: new[]
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
                    },
                    boxSlideStartSignals: new[]
                    {
                        new BoxSlideStartPresentationSignal(
                            BoxEntityId,
                            PlayerEntityId,
                            SlideSourceCell,
                            SlideDestinationCell,
                            Topology),
                    }));
        }

        private static TickResult CreateFlipResult(int tickIndex, bool includeActiveFlipSignal)
        {
            return CreateTickResult(
                tickIndex,
                finalBoxCell: FlipDestinationCell,
                presentationData: CreatePresentationData(
                    entityMotions: new[]
                    {
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
                    playerActionSignals: includeActiveFlipSignal
                        ? new[]
                        {
                            new TickPlayerActionPresentationSignal(
                                PlayerEntityId,
                                PlayerActionKind.Flip,
                                activeActionSequence: 1,
                                startedThisTick: true,
                                completedThisTick: false,
                                canceledThisTick: false,
                                executedThisTick: true,
                                targetEntityId: BoxEntityId,
                                direction: Direction.Up,
                                actionPlanId: 510,
                                flipOutcome: TickPlayerFlipOutcomeKind.FollowThrough),
                        }
                        : Array.Empty<TickPlayerActionPresentationSignal>()));
        }

        private static TickResult CreateFlipCompletionResult(int tickIndex)
        {
            return CreateTickResult(
                tickIndex,
                finalBoxCell: FlipDestinationCell,
                presentationData: CreatePresentationData(
                    playerActionSignals: new[]
                    {
                        new TickPlayerActionPresentationSignal(
                            PlayerEntityId,
                            PlayerActionKind.None,
                            activeActionSequence: 1,
                            startedThisTick: false,
                            completedThisTick: true,
                            canceledThisTick: false,
                            targetEntityId: BoxEntityId,
                            direction: Direction.Up,
                            actionPlanId: 510,
                            flipOutcome: TickPlayerFlipOutcomeKind.FollowThrough),
                    }));
        }

        private static TickResult CreateFlipImpactResult(int tickIndex)
        {
            return CreateTickResult(
                tickIndex,
                finalBoxCell: FlipDestinationCell,
                presentationData: CreatePresentationData(
                    flipImpactSignals: new[]
                    {
                        new FlipImpactPresentationSignal(
                            sourceActionPlanId: 710 + tickIndex,
                            BoxEntityId,
                            ImpactTargetEntityId,
                            PlayerEntityId,
                            FlipDestinationCell,
                            ImpactCell,
                            Topology,
                            Direction.Up,
                            Direction.Right,
                            FlipImpactPresentationDisposition.Stay,
                            hasLandingCell: true,
                            landingCell: FlipDestinationCell),
                    }));
        }

        private static TickResult CreateCombinedRoutingResult(int tickIndex)
        {
            return CreateTickResult(
                tickIndex,
                finalBoxCell: FlipDestinationCell,
                presentationData: CreatePresentationData(
                    entityMotions: new[]
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
                    flipImpactSignals: new[]
                    {
                        new FlipImpactPresentationSignal(
                            sourceActionPlanId: 711,
                            BoxEntityId,
                            ImpactTargetEntityId,
                            PlayerEntityId,
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
                    }));
        }

        private static TickPresentationData CreatePresentationData(
            IEnumerable<TickEntityMotion> entityMotions = null,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals = null,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals = null,
            IEnumerable<BoxSlideStartPresentationSignal> boxSlideStartSignals = null)
        {
            return new TickPresentationData(
                entityMotions ?? Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals ?? Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                flipImpactSignals ?? Array.Empty<FlipImpactPresentationSignal>(),
                boxSlideStartSignals: boxSlideStartSignals ?? Array.Empty<BoxSlideStartPresentationSignal>());
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            SurfaceCell finalBoxCell,
            TickPresentationData presentationData)
        {
            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetPrivateField(typeof(TickResult), result, "<PresentationData>k__BackingField", presentationData);
            SetPrivateField(typeof(TickResult), result, "<FinalTopology>k__BackingField", Topology);
            SetPrivateField(typeof(TickResult), result, "<DeterminismHash>k__BackingField", $"BOX-MOTION-{tickIndex}");
            SetPrivateField(typeof(TickResult), result, "<Trace>k__BackingField", TickTrace.Empty);
            SetPrivateField(typeof(TickResult), result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetPrivateField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(
                    new List<EntityState>
                    {
                        CreateUnit(PlayerEntityId, new SurfaceCell(FaceId.Floor, -1, 0), UnitRole.Player),
                        CreateBox(BoxEntityId, finalBoxCell),
                        CreateUnit(ImpactTargetEntityId, ImpactCell, UnitRole.Enemy),
                    }));
            SetPrivateField(
                typeof(TickResult),
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string> { $"AuthoritativeEvent:{tickIndex}" }));
            return result;
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
                facing = Direction.Up,
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

        private static void AdvancePresentation(GameplaySceneHost host, float durationSeconds)
        {
            var stepSeconds = Mathf.Max(host.TimingProfile.SimulationTickIntervalSeconds, 1f / 60f);
            var elapsedSeconds = 0f;
            while (elapsedSeconds < durationSeconds)
            {
                var deltaSeconds = Mathf.Min(stepSeconds, durationSeconds - elapsedSeconds);
                host.Presenter.UpdatePresentation(deltaSeconds);
                elapsedSeconds += deltaSeconds;
            }

            host.Presenter.UpdatePresentation(0f);
        }

        private static GameplayEntityView GetView(GameplaySceneHost host, int entityId)
        {
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            return view;
        }

        private static Vector3 ProjectWorldPosition(GameplaySceneHost host, SurfaceCell cell, EntityType entityType)
        {
            var projector = new GameplayCubeProjector(Bounds, 1f);
            Assert.That(projector.TryProjectEntityCell(cell, Topology, entityType, out var pose), Is.True);
            return host.BoardRoot.transform.TransformPoint(pose.LocalPosition);
        }

        private static void AssertMotionRequest(
            GameplayMotionPlaybackRequest request,
            PresentationMotionCueKey cueKey,
            int tickIndex,
            SurfaceCell source,
            SurfaceCell destination)
        {
            Assert.That(request.CueKey, Is.EqualTo(cueKey));
            Assert.That(request.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(request.EntityId, Is.EqualTo(BoxEntityId));
            Assert.That(request.MotionPayload.EntityId, Is.EqualTo(BoxEntityId));
            Assert.That(request.MotionPayload.ActorEntityId, Is.GreaterThanOrEqualTo(0));
            Assert.That(request.MotionPayload.SourceCell, Is.EqualTo(source));
            Assert.That(request.MotionPayload.DestinationCell, Is.EqualTo(destination));
            Assert.That(request.MotionPayload.Topology, Is.EqualTo(Topology));
            Assert.That(request.MotionPayload.HasTopology, Is.True);
            Assert.That(request.Target, Is.EqualTo(PresentationTarget.Entity(BoxEntityId)));
            Assert.That(request.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
            Assert.That(request.OwnershipKey.CueKey, Is.EqualTo(cueKey));
        }

        private static void AssertBlockingSnapshotCleared(PresentationBlockingSnapshot snapshot)
        {
            Assert.That(snapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(snapshot.HasActiveBlockingPresentation, Is.False);
        }

        private static void AssertVectorClose(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(PositionTolerance));
        }

        private static void AssertVisualRootReset(GameplayEntityView view)
        {
            Assert.That(view.ModelRoot.localPosition.sqrMagnitude, Is.LessThan(PositionTolerance));
            Assert.That(Quaternion.Angle(view.ModelRoot.localRotation, Quaternion.identity), Is.LessThan(RotationToleranceDegrees));
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

        private sealed class BoxMotionSmokeContext : IDisposable
        {
            private readonly GameObject _rootObject;

            public BoxMotionSmokeContext(GameObject rootObject, GameplaySceneHost host, EntityState[] initialEntities)
            {
                _rootObject = rootObject;
                Host = host;
                InitialEntities = initialEntities;
            }

            public GameplaySceneHost Host { get; }

            public EntityState[] InitialEntities { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class BoxMotionSmokeViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _hostRoot;
            private readonly bool _attachBoxDriver;

            public BoxMotionSmokeViewFactory(Transform hostRoot, bool attachBoxDriver)
            {
                _hostRoot = hostRoot;
                _attachBoxDriver = attachBoxDriver;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"BoxMotionSmokeView_{entity.entityId}");
                var boardRoot = _hostRoot.GetComponentInChildren<GameplayBoardRoot>(includeInactive: true);
                var parent = boardRoot != null ? boardRoot.EntityRoot : _hostRoot;
                viewObject.transform.SetParent(parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                view.EnsureModelRoot();
                if (entity.entityId == PlayerEntityId)
                {
                    viewObject.AddComponent<PlayerAnimatorDriver>();
                    viewObject.AddComponent<PlayerAnimationTimingAuthoring>();
                }

                if (entity.type == EntityType.Box && _attachBoxDriver)
                {
                    var gripPointObject = new GameObject("GripPoint");
                    gripPointObject.transform.SetParent(view.ModelRoot, worldPositionStays: false);
                    gripPointObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                    gripPointObject.transform.localRotation = Quaternion.identity;
                    var driver = viewObject.AddComponent<BoxFlipInteractionDriver>();
                    SetPrivateField(driver, "visualRoot", view.ModelRoot);
                    SetPrivateField(driver, "gripPoint", gripPointObject.transform);
                }

                return view;
            }
        }

        private sealed class RecordingGameplayMotionPlaybackPort : IGameplayMotionPlaybackPort
        {
            private readonly List<GameplayMotionPlaybackRequest> _requests = new();

            public int TryPlayCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public List<GameplayMotionPlaybackRequest> Requests => _requests;

            public bool TryPlayBoxMotion(
                in GameplayMotionPlaybackRequest request,
                out GameplayMotionPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                result = new GameplayMotionPlaybackResult(GameplayMotionPlaybackResultKind.Started);
                return true;
            }

            public void UpdatePresentation(float deltaTime)
            {
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
        }
    }
}
