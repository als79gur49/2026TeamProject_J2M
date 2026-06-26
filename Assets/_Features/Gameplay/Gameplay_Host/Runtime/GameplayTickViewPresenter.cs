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
        private TopologyTransitionPostFxController _topologyTransitionPostFxController;
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
            EnemyInactiveVisualSettings enemyInactiveVisualSettings = null,
            TopologyPresentationExecutionMode topologyPresentationExecutionMode =
                TopologyPresentationExecutionDefaults.ProductionDefault)
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
                enemyInactiveVisualSettings,
                topologyPresentationExecutionMode);
            CapturePresentationState();
        }

        public void Present(TickResult result)
        {
            PresentationCoordinator.Present(result);
            NotifyPresentationStateChangedIfNeeded();
        }

        public void PresentInitial(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            InitialPresentationData presentationData = null)
        {
            PresentationCoordinator.PresentInitial(entities, topology, presentationData);
            CapturePresentationState();
        }

        public void AttachCameraRig(GameplayCameraRig viewCameraRig)
        {
            PresentationCoordinator.AttachCameraRig(viewCameraRig);
            _viewCameraRig = viewCameraRig;
            _viewCameraBrain = null;
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

        internal void ConfigureEnemyPresentationExecution(
            EnemyPresentationExecutionMode mode,
            IGameplayEnemyPresentationPlaybackPort playbackPort = null)
        {
            PresentationCoordinator.ConfigureEnemyPresentationExecution(mode, playbackPort);
        }

        internal void ConfigureCoreGameplaySfxExecution(
            CoreGameplaySfxExecutionMode mode,
            IGameplaySfxPlaybackPort playbackPort = null)
        {
            PresentationCoordinator.ConfigureCoreGameplaySfxExecution(mode, playbackPort);
        }

        public void DetachPresentationExtension(IGameplayTickPresentationExtension extension)
        {
            PresentationCoordinator.DetachPresentationExtension(extension);
        }

        public void ApplyStageTerminalPresentation(
            GameplayStageTerminalPresentationReason reason,
            TickResult terminalTickResult)
        {
            PresentationCoordinator.ApplyStageTerminalPresentation(reason, terminalTickResult);
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

        internal TopologyPresentationExecutionMode TopologyPresentationExecutionMode =>
            PresentationCoordinator.TopologyPresentationExecutionMode;

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

        internal EnemyPresentationExecutionMode EnemyPresentationExecutionMode =>
            PresentationCoordinator.EnemyPresentationExecutionMode;

        internal EnemyPresentationOwnershipDiagnostics EnemyPresentationOwnershipDiagnostics =>
            PresentationCoordinator.EnemyPresentationOwnershipDiagnostics;

        internal PresentationBlockingSnapshot EnemyPresentationExecutionPipelineBlockingSnapshot =>
            PresentationCoordinator.EnemyPresentationExecutionPipelineBlockingSnapshot;

        internal GameplayEnemyPresentationExecutorDiagnostics EnemyPresentationExecutorDiagnostics =>
            PresentationCoordinator.EnemyPresentationExecutorDiagnostics;

        internal EnemyPresentationProductionTelemetrySnapshot EnemyPresentationProductionTelemetrySnapshot =>
            PresentationCoordinator.EnemyPresentationProductionTelemetrySnapshot;

        internal CoreGameplaySfxExecutionMode CoreGameplaySfxExecutionMode =>
            PresentationCoordinator.CoreGameplaySfxExecutionMode;

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
            PresentationCoordinator.AttachCameraRig(viewCameraRig);
            _viewCameraRig = viewCameraRig;
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
                return;
            }

            PresentationCoordinator.UpdatePresentation(deltaTime);
            SyncViewCameraRuntime();
            RefreshTopologyTransitionPostFx();
            NotifyPresentationStateChangedIfNeeded();
            PresentationAdvanced?.Invoke(deltaTime);
        }

        public void SetPresentationPaused(bool paused)
        {
            PresentationCoordinator.SetPresentationPaused(paused);
            NotifyPresentationStateChangedIfNeeded();
        }

        private void RefreshTopologyTransitionPostFx()
        {
            _topologyTransitionPostFxController?.Apply(PresentationCoordinator.CurrentTopologyTransitionVisualState);
        }

        private void SyncViewCameraRuntime()
        {
            if (_viewCameraRig != null)
            {
                _viewCameraRig.ApplyTopologyTransitionVisualState(PresentationCoordinator.CurrentTopologyTransitionVisualState);
                _viewCameraRig.SnapToTarget();
            }

            if (_viewCameraBrain != null &&
                _viewCameraBrain.isActiveAndEnabled)
            {
                _viewCameraBrain.ManualUpdate();
            }
        }

        private void LateUpdate()
        {
            if (_presentationCoordinator != null &&
                PresentationCoordinator.IsInitialized)
            {
                UpdatePresentation(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (_presentationCoordinator == null ||
                _hasTornDownCoordinator)
            {
                return;
            }

            _hasTornDownCoordinator = true;
            PresentationCoordinator.TeardownPresentationRuntime();
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
