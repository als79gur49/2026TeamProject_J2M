using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayTickViewPresenter : MonoBehaviour
    {
        private GameplayTickPresentationCoordinator _presentationCoordinator;
        private bool _hasTornDownCoordinator;
        private bool _hasObservedPresentationState;
        private bool _lastHasBlockingPresentation;
        private bool _lastIsPresentationActive;
        private bool _lastIsTopologyTransitionActive;
        private CubeTopologyState _lastObservedTopology;
        private readonly GameplayCameraShakeMixer _cameraShakeMixer = new();
        private GameplayCameraShakeProductionPlanner _cameraShakeProductionPlanner;
        private readonly TopologyTransitionCameraShakeController _topologyTransitionCameraShakeController = new();
        private TopologyTransitionCameraShakeProfile _topologyTransitionCameraShakeProfile =
            TopologyTransitionCameraShakeProfile.CreateDefault();
        private TopologyTransitionPostFxController _topologyTransitionPostFxController;
        private IGameplayCameraAdditivePosePort _cameraAdditivePosePort;
        private IGameplayCameraVisibilityPort _cameraVisibilityPort;
        private CinemachineBrain _viewCameraBrain;
        private GameplayCameraRig _viewCameraRig;

        public event System.Action<CubeTopologyState> TopologyCommitted
        {
            add => PresentationCoordinator.TopologyCommitted += value;
            remove => PresentationCoordinator.TopologyCommitted -= value;
        }

        public event System.Action PresentationStateChanged;

        public event System.Action<float> PresentationAdvanced;

        public CubeTopologyState CurrentTopology => PresentationCoordinator.CurrentTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => PresentationCoordinator.CurrentPresentationPhase;

        public bool IsPresentationActive => PresentationCoordinator.IsPresentationActive;

        public bool HasBlockingPresentation => PresentationCoordinator.HasBlockingPresentation;

        public bool IsTopologyTransitionActive => PresentationCoordinator.IsTopologyTransitionActive;

        public bool IsPresentationPaused => PresentationCoordinator.IsPresentationPaused;

        public float LastStageClearPlayerPresentationDelaySeconds =>
            PresentationCoordinator.LastStageClearPlayerPresentationDelaySeconds;

        public bool IsPlayerActionAttemptPlaybackActive(int entityId) =>
            PresentationCoordinator.IsPlayerActionAttemptPlaybackActive(entityId);

        public TopologyTransitionVisualState CurrentTopologyTransitionVisualState =>
            PresentationCoordinator.CurrentTopologyTransitionVisualState;

        public Quaternion PresentedBoardRotation => PresentationCoordinator.PresentedBoardRotation;

        public Vector3 CubeCenter => PresentationCoordinator.CubeCenter;

        public Bounds VisibleCubeBounds => PresentationCoordinator.VisibleCubeBounds;

        public IReadOnlyList<TilePresentationRequest> CurrentTilePresentationRequests =>
            PresentationCoordinator.CurrentTilePresentationRequests;

        public IReadOnlyList<GravityFieldPresentationRequest> CurrentGravityFieldPresentationRequests =>
            PresentationCoordinator.CurrentGravityFieldPresentationRequests;

        public IReadOnlyList<GravityFieldVisualState> CurrentGravityFieldVisualStates =>
            PresentationCoordinator.CurrentGravityFieldVisualStates;

        internal void BindCoordinator(GameplayTickPresentationCoordinator coordinator)
        {
            if (coordinator == null)
            {
                throw new System.ArgumentNullException(nameof(coordinator));
            }

            if (_presentationCoordinator == null)
            {
                _presentationCoordinator = coordinator;
                return;
            }

            if (!ReferenceEquals(_presentationCoordinator, coordinator))
            {
                throw new System.InvalidOperationException(
                    "GameplayTickViewPresenter is already bound to a different presentation coordinator.");
            }
        }

        private GameplayTickPresentationCoordinator PresentationCoordinator =>
            _presentationCoordinator ?? throw new System.InvalidOperationException(
                "GameplayTickViewPresenter requires a bound presentation coordinator before use.");

        public void Initialize(
            GameplayEntityViewBinder viewBinder,
            BoardBounds boardBounds,
            CubeTopologyState initialTopology,
            float cellSize,
            GameplayTimingProfile timingProfile,
            GameplayBoardRoot boardRoot = null,
            GameplayBoardSurfaceRenderer boardSurfaceRenderer = null,
            TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX,
            TopologyRotationTweenSettings topologyRotationTweenSettings = default,
            float faceSeamGap = -1f,
            EnemyPresentationArchetypeRegistry enemyPresentationArchetypeRegistry = null,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            EnemyPresentationBinding[] enemyPresentationBindings = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null,
            EnemyInactiveVisualSettings enemyInactiveVisualSettings = null)
        {
            PresentationCoordinator.Initialize(
                viewBinder,
                boardBounds,
                initialTopology,
                cellSize,
                timingProfile,
                boardRoot,
                boardSurfaceRenderer,
                topologyRotationVisualMapping,
                topologyRotationTweenSettings,
                faceSeamGap,
                enemyPresentationArchetypeRegistry,
                enemyPresentationCatalog,
                enemyPresentationBindings,
                tileFeatureVfxStyleBindings,
                enemyInactiveVisualSettings);
            CapturePresentationState();
        }

        public void Present(TickResult result)
        {
            PresentationCoordinator.Present(result);
            SyncViewCameraRuntime(0f);
            var submittedCameraImpulse = CameraShakeProductionPlanner.Present(
                    result,
                    PresentationCoordinator.HasActiveLocalMotionTrack,
                    PresentationCoordinator.HasLocalMotionTrack,
                    ResolveEnemyJumpLandingCameraFeedback);
            submittedCameraImpulse |= CameraShakeProductionPlanner.ObserveHeavyEnemyJumpLandingMilestones(
                PresentationCoordinator.HasActiveJumpLandingCompletionTrack,
                ResolveEnemyPresentationAnchor,
                _cameraVisibilityPort,
                PresentationCoordinator.IsTopologyTransitionActive);
            if (submittedCameraImpulse)
            {
                ApplyCurrentCameraShakeMixResult();
                ManualUpdateViewCameraBrain();
            }
            NotifyPresentationStateChangedIfNeeded();
        }

        public void PresentInitial(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            InitialPresentationData presentationData = null)
        {
            CameraShakeProductionPlanner.ResetSession();
            _cameraShakeMixer.HardReset();
            PresentationCoordinator.PresentInitial(entities, topology, presentationData);
            ResetCameraAdditivePose();
            CapturePresentationState();
        }

        public void AttachCameraRig(GameplayCameraRig viewCameraRig)
        {
            ReplaceCameraRig(viewCameraRig);
            _viewCameraBrain = null;
        }

        internal void ConfigureTopologyTransitionCameraShake(TopologyTransitionCameraShakeProfile profile)
        {
            _topologyTransitionCameraShakeProfile =
                profile?.Clone() ?? TopologyTransitionCameraShakeProfile.CreateDefault();
            ResetCameraAdditivePose();
        }

        internal ICameraShakeImpulseSink CameraShakeImpulseSink => _cameraShakeMixer;

        internal CameraShakeMixResult CurrentCameraShakeMixResult => _cameraShakeMixer.CurrentResult;

        internal int ActiveGameplayCameraImpulseCount => _cameraShakeMixer.ActiveImpulseCount;

        internal int AcceptedGameplayCameraImpulseCount => _cameraShakeMixer.AcceptedIdentityCount;

        internal int PendingFlipLandingCameraShakeCount => CameraShakeProductionPlanner.PendingFlipLandingCount;

        internal int PendingFlipHostileImpactCameraShakeCount =>
            CameraShakeProductionPlanner.PendingFlipHostileImpactCount;

        internal int ObservedPlayerDamageImpactCameraShakeCount =>
            CameraShakeProductionPlanner.ObservedPlayerDamageImpactCount;

        internal int ObservedPlayerLethalImpactCameraShakeCount =>
            CameraShakeProductionPlanner.ObservedPlayerLethalImpactCount;

        internal int AcceptedPlayerDamageImpactCameraShakeCount =>
            CameraShakeProductionPlanner.AcceptedPlayerDamageImpactCount;

        internal int AcceptedPlayerLethalImpactCameraShakeCount =>
            CameraShakeProductionPlanner.AcceptedPlayerLethalImpactCount;

        internal int PendingHeavyEnemyJumpLandingCameraShakeCount =>
            CameraShakeProductionPlanner.PendingHeavyEnemyJumpLandingCount;

        internal int ObservedHeavyEnemyJumpLandingCameraShakeCount =>
            CameraShakeProductionPlanner.ObservedHeavyEnemyJumpLandingCount;

        internal int AcceptedHeavyEnemyJumpLandingCameraShakeCount =>
            CameraShakeProductionPlanner.AcceptedHeavyEnemyJumpLandingCount;

        internal int OffscreenHeavyEnemyJumpLandingCameraShakeCount =>
            CameraShakeProductionPlanner.OffscreenHeavyEnemyJumpLandingCount;

        internal int MissingAnchorHeavyEnemyJumpLandingCameraShakeCount =>
            CameraShakeProductionPlanner.MissingAnchorHeavyEnemyJumpLandingCount;

        internal int TopologySuppressedHeavyEnemyJumpLandingCameraShakeCount =>
            CameraShakeProductionPlanner.TopologySuppressedHeavyEnemyJumpLandingCount;

        internal IReadOnlyList<MotionTrackProgressSample> CurrentMotionTrackProgressSamples =>
            PresentationCoordinator.MotionTrackProgressSamples;

        internal void ConfigureGameplayCameraShakeProfile(GameplayCameraShakeProfile profile)
        {
            CameraShakeProductionPlanner.ResetSession();
            _cameraShakeMixer.ConfigureProfile(profile);
            ResetCameraAdditivePose();
        }

        internal void SetCameraMotionLevel(CameraMotionLevel motionLevel)
        {
            _cameraShakeMixer.SetMotionLevel(motionLevel);
            ApplyCurrentCameraShakeMixResult();
        }

        internal void CancelAllGameplayCameraImpulses()
        {
            _cameraShakeMixer.CancelAllGameplayImpulses();
            ApplyCurrentCameraShakeMixResult();
        }

        public void AttachOutputCamera(Camera outputCamera)
        {
            PresentationCoordinator.AttachOutputCamera(outputCamera);
        }

        internal void AttachGameplayAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            PresentationCoordinator.AttachGameplayAudioRuntime(playbackPort, gameplayAudioMap);
        }

        internal void AttachTileFeatureAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TileFeatureAudioMap tileFeatureAudioMap)
        {
            PresentationCoordinator.AttachTileFeatureAudioRuntime(playbackPort, tileFeatureAudioMap);
        }

        internal void AttachTopologyAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TopologyAudioMap topologyAudioMap)
        {
            PresentationCoordinator.AttachTopologyAudioRuntime(playbackPort, topologyAudioMap);
        }

        internal void AttachGravityFieldAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GravityFieldAudioMap gravityFieldAudioMap)
        {
            PresentationCoordinator.AttachGravityFieldAudioRuntime(playbackPort, gravityFieldAudioMap);
        }

        internal void AttachBlockAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            BlockAudioMap blockAudioMap)
        {
            PresentationCoordinator.AttachBlockAudioRuntime(playbackPort, blockAudioMap);
        }

        internal void AttachPlayerLocomotionAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            PlayerLocomotionAudioMap playerLocomotionAudioMap)
        {
            PresentationCoordinator.AttachPlayerLocomotionAudioRuntime(playbackPort, playerLocomotionAudioMap);
        }

        public void AttachTileFeatureVisualRegistry(ITileFeatureVisualRegistry registry)
        {
            PresentationCoordinator.AttachTileFeatureVisualRegistry(registry);
        }

        internal void AttachTileFeatureVisualPoseSynchronizer(TileFeatureVisualPoseSynchronizer synchronizer)
        {
            PresentationCoordinator.AttachTileFeatureVisualPoseSynchronizer(synchronizer);
        }

        internal void RegisterPresentationPauseRoot(GameObject root)
        {
            PresentationCoordinator.RegisterPresentationPauseRoot(root);
        }

        public void AttachPresentationExtension(IGameplayTickPresentationExtension extension)
        {
            PresentationCoordinator.AttachPresentationExtension(extension);
        }

        internal void ConfigureDamageDeathVfxPlaybackPort(IDamageDeathVfxPlaybackPort playbackPort)
        {
            PresentationCoordinator.ConfigureDamageDeathVfxPlaybackPort(playbackPort);
        }

        internal void ConfigureBoxMotionPlaybackPort(
            IGameplayMotionPlaybackPort playbackPort,
            bool useDefaultPlaybackPort = true)
        {
            PresentationCoordinator.ConfigureBoxMotionPlaybackPort(playbackPort, useDefaultPlaybackPort);
        }

        internal void ConfigurePlayerActionAnimationExecution(
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort = null)
        {
            PresentationCoordinator.ConfigurePlayerActionAnimationExecution(mode, playbackPort);
        }

        internal void ConfigureEnemyPresentationPlaybackPort(
            IGameplayEnemyPresentationPlaybackPort playbackPort,
            bool useDefaultPlaybackPort = true)
        {
            PresentationCoordinator.ConfigureEnemyPresentationPlaybackPort(playbackPort, useDefaultPlaybackPort);
        }

        internal void ConfigureCoreGameplaySfxPlaybackPort(
            IGameplaySfxPlaybackPort playbackPort,
            bool useDefaultPlaybackPort = true)
        {
            PresentationCoordinator.ConfigureCoreGameplaySfxPlaybackPort(playbackPort, useDefaultPlaybackPort);
        }

        public void DetachPresentationExtension(IGameplayTickPresentationExtension extension)
        {
            PresentationCoordinator.DetachPresentationExtension(extension);
        }

        public void ApplyStageTerminalPresentation(
            GameplayStageTerminalPresentationReason reason,
            TickResult terminalTickResult)
        {
            CameraShakeProductionPlanner.HardCleanup();
            PresentationCoordinator.ApplyStageTerminalPresentation(reason, terminalTickResult);
        }

        internal void CompleteStageTerminalCameraHandoff()
        {
            CameraShakeProductionPlanner.HardCleanup();
            _cameraShakeMixer.HardReset();
            ResetCameraAdditivePose();
        }

        internal void DebugRefreshGameplayAudioPlan(TickResult result)
        {
            PresentationCoordinator.DebugRefreshGameplayAudioPlan(result);
        }

        internal void SetPresentationTraceSink(System.Action<string> traceSink)
        {
            PresentationCoordinator.SetTraceSink(traceSink);
        }

        internal void SetTileFeatureVisualDiagnosticSink(System.Action<string> diagnosticSink)
        {
            PresentationCoordinator.SetTileFeatureVisualDiagnosticSink(diagnosticSink);
        }

        internal void SetGravityFieldVisualDiagnosticSink(System.Action<string> diagnosticSink)
        {
            PresentationCoordinator.SetGravityFieldVisualDiagnosticSink(diagnosticSink);
        }

        internal GameplayEntityPresentationLifecycleDebugSnapshot DebugCaptureEntityPresentationLifecycle(
            int entityId,
            float timelineTimeSeconds = 0f)
        {
            return PresentationCoordinator.DebugCaptureEntityPresentationLifecycle(entityId, timelineTimeSeconds);
        }

        internal int PendingGameplayAudioRequestCount => PresentationCoordinator.PendingGameplayAudioRequestCount;

        internal int DeferredGameplayAudioRequestCount => PresentationCoordinator.DeferredGameplayAudioRequestCount;

        internal int PendingMoonBlockEmergenceRequestCount =>
            PresentationCoordinator.PendingMoonBlockEmergenceRequestCount;

        internal int ActiveMoonBlockDestructionGhostCount =>
            PresentationCoordinator.ActiveMoonBlockDestructionGhostCount;

        internal EntityPresentationApplyDiagnostics DebugLastEntityPresentationApplyDiagnostics =>
            PresentationCoordinator.DebugLastEntityPresentationApplyDiagnostics;

        internal TopologyPresentationOwnershipDiagnostics TopologyPresentationOwnershipDiagnostics =>
            PresentationCoordinator.TopologyPresentationOwnershipDiagnostics;

        internal TopologyProductionTelemetrySnapshot TopologyProductionTelemetrySnapshot =>
            PresentationCoordinator.TopologyProductionTelemetrySnapshot;

        internal DamageDeathVfxOwnershipDiagnostics DamageDeathVfxOwnershipDiagnostics =>
            PresentationCoordinator.DamageDeathVfxOwnershipDiagnostics;

        internal PresentationBlockingSnapshot DamageDeathVfxExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.DamageDeathVfxExecutionPipelineBlockingSnapshot;

        internal DamageDeathVfxExecutorDiagnostics DamageDeathVfxExecutorDiagnostics =>
            PresentationCoordinator.DamageDeathVfxExecutorDiagnostics;

        internal BoxMotionOwnershipDiagnostics BoxMotionOwnershipDiagnostics =>
            PresentationCoordinator.BoxMotionOwnershipDiagnostics;

        internal PresentationBlockingSnapshot BoxMotionExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.BoxMotionExecutionPipelineBlockingSnapshot;

        internal GameplayMotionExecutorDiagnostics BoxMotionExecutorDiagnostics =>
            PresentationCoordinator.BoxMotionExecutorDiagnostics;

        internal BoxMotionPresentationRuntimeDebugSnapshot BoxMotionRuntimeDebugSnapshot =>
            PresentationCoordinator.BoxMotionRuntimeDebugSnapshot;

        internal BoxMotionProductionTelemetrySnapshot BoxMotionProductionTelemetrySnapshot =>
            PresentationCoordinator.BoxMotionProductionTelemetrySnapshot;

        internal void DebugHardCleanupPresentationExtensions()
        {
            PresentationCoordinator.HardCleanupPresentationExtensions();
            CameraShakeProductionPlanner.HardCleanup();
            _cameraShakeMixer.HardReset();
            ResetCameraAdditivePose();
        }

        internal PlayerActionAnimationExecutionMode PlayerActionAnimationExecutionMode =>
            PresentationCoordinator.PlayerActionAnimationExecutionMode;

        internal PlayerActionAnimationOwnershipDiagnostics PlayerActionAnimationOwnershipDiagnostics =>
            PresentationCoordinator.PlayerActionAnimationOwnershipDiagnostics;

        internal PresentationBlockingSnapshot PlayerActionAnimationExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.PlayerActionAnimationExecutionPipelineBlockingSnapshot;

        internal GameplayAnimationExecutorDiagnostics PlayerActionAnimationExecutorDiagnostics =>
            PresentationCoordinator.PlayerActionAnimationExecutorDiagnostics;

        internal PlayerActionAnimationProductionTelemetrySnapshot PlayerActionAnimationProductionTelemetrySnapshot =>
            PresentationCoordinator.PlayerActionAnimationProductionTelemetrySnapshot;

        internal EnemyPresentationOwnershipDiagnostics EnemyPresentationOwnershipDiagnostics =>
            PresentationCoordinator.EnemyPresentationOwnershipDiagnostics;

        internal PresentationBlockingSnapshot EnemyPresentationExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.EnemyPresentationExecutionPipelineBlockingSnapshot;

        internal GameplayEnemyPresentationExecutorDiagnostics EnemyPresentationExecutorDiagnostics =>
            PresentationCoordinator.EnemyPresentationExecutorDiagnostics;

        internal EnemyPresentationProductionTelemetrySnapshot EnemyPresentationProductionTelemetrySnapshot =>
            PresentationCoordinator.EnemyPresentationProductionTelemetrySnapshot;

        internal CoreGameplaySfxRoute CoreGameplaySfxRoute =>
            PresentationCoordinator.CoreGameplaySfxRoute;

        internal CoreGameplaySfxOwnershipDiagnostics CoreGameplaySfxOwnershipDiagnostics =>
            PresentationCoordinator.CoreGameplaySfxOwnershipDiagnostics;

        internal PresentationBlockingSnapshot CoreGameplaySfxExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.CoreGameplaySfxExecutionPipelineBlockingSnapshot;

        internal GameplaySfxExecutorDiagnostics CoreGameplaySfxExecutorDiagnostics =>
            PresentationCoordinator.CoreGameplaySfxExecutorDiagnostics;

        internal PresentationBlockingSnapshot ActionAudioExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.ActionAudioExecutionPipelineBlockingSnapshot;

        internal GameplayActionAudioExecutorDiagnostics ActionAudioExecutorDiagnostics =>
            PresentationCoordinator.ActionAudioExecutorDiagnostics;

        internal ActionAudioProductionTelemetrySnapshot ActionAudioProductionTelemetrySnapshot =>
            PresentationCoordinator.ActionAudioProductionTelemetrySnapshot;

        internal PresentationBlockingSnapshot EnemyAudioExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.EnemyAudioExecutionPipelineBlockingSnapshot;

        internal GameplayEnemyAudioExecutorDiagnostics EnemyAudioExecutorDiagnostics =>
            PresentationCoordinator.EnemyAudioExecutorDiagnostics;

        internal EnemyAudioProductionTelemetrySnapshot EnemyAudioProductionTelemetrySnapshot =>
            PresentationCoordinator.EnemyAudioProductionTelemetrySnapshot;

        public void AttachCameraRuntime(GameplayCameraRig viewCameraRig, CinemachineBrain viewCameraBrain)
        {
            ReplaceCameraRig(viewCameraRig);
            _viewCameraBrain = viewCameraBrain;

            if (_viewCameraBrain != null)
            {
                _viewCameraBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            }
        }

        public void AttachTopologyTransitionPostFxController(TopologyTransitionPostFxController topologyTransitionPostFxController)
        {
            _topologyTransitionPostFxController = topologyTransitionPostFxController;
            RefreshTopologyTransitionPostFx();
        }

        public void UpdatePresentation(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (PresentationCoordinator.IsPresentationPaused)
            {
                _cameraShakeMixer.SetPaused(true);
                ResetCameraAdditivePose();
                return;
            }

            PresentationCoordinator.UpdatePresentation(deltaTime);
            SyncViewCameraRuntime(deltaTime);
            if (CameraShakeProductionPlanner.ObserveMotionProgress(
                    PresentationCoordinator.MotionTrackProgressSamples))
            {
                ApplyCurrentCameraShakeMixResult();
                ManualUpdateViewCameraBrain();
            }
            if (CameraShakeProductionPlanner.ObserveHeavyEnemyJumpLandingMilestones(
                    PresentationCoordinator.HasActiveJumpLandingCompletionTrack,
                    ResolveEnemyPresentationAnchor,
                    _cameraVisibilityPort,
                    PresentationCoordinator.IsTopologyTransitionActive))
            {
                ApplyCurrentCameraShakeMixResult();
                ManualUpdateViewCameraBrain();
            }
            RefreshTopologyTransitionPostFx();
            NotifyPresentationStateChangedIfNeeded();
            PresentationAdvanced?.Invoke(deltaTime);
        }

        public void SetPresentationPaused(bool paused)
        {
            PresentationCoordinator.SetPresentationPaused(paused);
            _cameraShakeMixer.SetPaused(paused);
            if (paused)
            {
                ResetCameraAdditivePose();
            }

            NotifyPresentationStateChangedIfNeeded();
        }

        private void RefreshTopologyTransitionPostFx()
        {
            _topologyTransitionPostFxController?.Apply(PresentationCoordinator.CurrentTopologyTransitionVisualState);
        }

        private void SyncViewCameraRuntime(float deltaTime)
        {
            var topologyVisualState = PresentationCoordinator.CurrentTopologyTransitionVisualState;
            var topologyPose = _topologyTransitionCameraShakeController.Evaluate(
                topologyVisualState,
                _topologyTransitionCameraShakeProfile);
            var topologyContribution = TopologyCameraShakeContributionAdapter.Create(
                topologyPose,
                topologyVisualState.IsActive);
            _cameraShakeMixer.SetTopologyContribution(topologyContribution);
            _cameraShakeMixer.Advance(deltaTime);
            ApplyCurrentCameraShakeMixResult();

            ManualUpdateViewCameraBrain();
        }

        private void LateUpdate()
        {
            if (_presentationCoordinator != null &&
                PresentationCoordinator.IsInitialized)
            {
                UpdatePresentation(Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            CameraShakeProductionPlanner.HardCleanup();
            _cameraShakeMixer.HardReset();
            ResetCameraAdditivePose();
        }

        private void OnDestroy()
        {
            CameraShakeProductionPlanner.HardCleanup();
            _cameraShakeMixer.HardReset();
            ResetCameraAdditivePose();
            if (_presentationCoordinator == null ||
                _hasTornDownCoordinator)
            {
                return;
            }

            _hasTornDownCoordinator = true;
            PresentationCoordinator.TeardownPresentationRuntime();
        }

        private void ReplaceCameraRig(GameplayCameraRig viewCameraRig)
        {
            if (_cameraAdditivePosePort != null &&
                !ReferenceEquals(_cameraAdditivePosePort, viewCameraRig))
            {
                _cameraAdditivePosePort.ResetAdditivePose();
            }

            _viewCameraRig = viewCameraRig;
            _cameraAdditivePosePort = viewCameraRig;
            _cameraVisibilityPort = viewCameraRig;
            _cameraAdditivePosePort?.ResetAdditivePose();
            PresentationCoordinator.AttachCameraRig(viewCameraRig);
        }

        private EnemyJumpLandingCameraFeedbackKind ResolveEnemyJumpLandingCameraFeedback(int entityId)
        {
            if (!PresentationCoordinator.TryGetLiveEntityPresentationView(entityId, out var view))
            {
                return EnemyJumpLandingCameraFeedbackKind.None;
            }

            var authoring = EnemyJumpMotionPresentationAuthoring.GetOptionalValidatedAuthoring(view);
            return authoring != null
                ? authoring.JumpLandingCameraFeedback
                : EnemyJumpLandingCameraFeedbackKind.None;
        }

        private Vector3? ResolveEnemyPresentationAnchor(int entityId)
        {
            if (!PresentationCoordinator.TryGetLiveEntityPresentationView(entityId, out var view))
            {
                return null;
            }

            return view.transform.position;
        }

        private void ResetCameraAdditivePose()
        {
            _cameraAdditivePosePort?.ResetAdditivePose();
            if (_viewCameraBrain != null &&
                _viewCameraBrain.isActiveAndEnabled)
            {
                _viewCameraBrain.ManualUpdate();
            }
        }

        private void ApplyCurrentCameraShakeMixResult()
        {
            if (_cameraAdditivePosePort == null)
            {
                return;
            }

            var mixResult = _cameraShakeMixer.CurrentResult;
            _cameraAdditivePosePort.ApplyAdditivePose(
                mixResult.LocalPosition,
                mixResult.LocalRotation);
        }

        private GameplayCameraShakeProductionPlanner CameraShakeProductionPlanner =>
            _cameraShakeProductionPlanner ??= new GameplayCameraShakeProductionPlanner(_cameraShakeMixer);

        private void ManualUpdateViewCameraBrain()
        {
            if (_viewCameraBrain != null &&
                _viewCameraBrain.isActiveAndEnabled)
            {
                _viewCameraBrain.ManualUpdate();
            }
        }

        private void CapturePresentationState()
        {
            _lastObservedTopology = PresentationCoordinator.CurrentTopology;
            _lastIsPresentationActive = PresentationCoordinator.IsPresentationActive;
            _lastHasBlockingPresentation = PresentationCoordinator.HasBlockingPresentation;
            _lastIsTopologyTransitionActive = PresentationCoordinator.IsTopologyTransitionActive;
            _hasObservedPresentationState = true;
        }

        private void NotifyPresentationStateChangedIfNeeded()
        {
            if (!_hasObservedPresentationState)
            {
                CapturePresentationState();
                return;
            }

            var currentTopology = PresentationCoordinator.CurrentTopology;
            var isPresentationActive = PresentationCoordinator.IsPresentationActive;
            var hasBlockingPresentation = PresentationCoordinator.HasBlockingPresentation;
            var isTopologyTransitionActive = PresentationCoordinator.IsTopologyTransitionActive;

            if (currentTopology.Equals(_lastObservedTopology) &&
                isPresentationActive == _lastIsPresentationActive &&
                hasBlockingPresentation == _lastHasBlockingPresentation &&
                isTopologyTransitionActive == _lastIsTopologyTransitionActive)
            {
                return;
            }

            _lastObservedTopology = currentTopology;
            _lastIsPresentationActive = isPresentationActive;
            _lastHasBlockingPresentation = hasBlockingPresentation;
            _lastIsTopologyTransitionActive = isTopologyTransitionActive;
            PresentationStateChanged?.Invoke();
        }
    }
}
