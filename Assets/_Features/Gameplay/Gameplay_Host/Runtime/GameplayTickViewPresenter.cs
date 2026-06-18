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
        private readonly GameplayTickPresentationCoordinator _presentationCoordinator = new();
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
            add => _presentationCoordinator.TopologyCommitted += value;
            remove => _presentationCoordinator.TopologyCommitted -= value;
        }

        public event System.Action PresentationStateChanged;

        public event System.Action<float> PresentationAdvanced;

        public CubeTopologyState CurrentTopology => _presentationCoordinator.CurrentTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => _presentationCoordinator.CurrentPresentationPhase;

        public bool IsPresentationActive => _presentationCoordinator.IsPresentationActive;

        public bool HasBlockingPresentation => _presentationCoordinator.HasBlockingPresentation;

        public bool IsTopologyTransitionActive => _presentationCoordinator.IsTopologyTransitionActive;

        public bool IsPresentationPaused => _presentationCoordinator.IsPresentationPaused;

        public float LastStageClearPlayerPresentationDelaySeconds =>
            _presentationCoordinator.LastStageClearPlayerPresentationDelaySeconds;

        public bool IsPlayerActionAttemptPlaybackActive(int entityId) =>
            _presentationCoordinator.IsPlayerActionAttemptPlaybackActive(entityId);

        public TopologyTransitionVisualState CurrentTopologyTransitionVisualState =>
            _presentationCoordinator.CurrentTopologyTransitionVisualState;

        public Quaternion PresentedBoardRotation => _presentationCoordinator.PresentedBoardRotation;

        public Vector3 CubeCenter => _presentationCoordinator.CubeCenter;

        public Bounds VisibleCubeBounds => _presentationCoordinator.VisibleCubeBounds;

        public IReadOnlyList<TilePresentationRequest> CurrentTilePresentationRequests =>
            _presentationCoordinator.CurrentTilePresentationRequests;

        public IReadOnlyList<GravityFieldPresentationRequest> CurrentGravityFieldPresentationRequests =>
            _presentationCoordinator.CurrentGravityFieldPresentationRequests;

        public IReadOnlyList<GravityFieldVisualState> CurrentGravityFieldVisualStates =>
            _presentationCoordinator.CurrentGravityFieldVisualStates;

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
                TopologyPresentationExecutionMode.LegacyCoordinator)
        {
            _presentationCoordinator.Initialize(
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
            _presentationCoordinator.Present(result);
            NotifyPresentationStateChangedIfNeeded();
        }

        public void PresentInitial(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            InitialPresentationData presentationData = null)
        {
            _presentationCoordinator.PresentInitial(entities, topology, presentationData);
            CapturePresentationState();
        }

        public void AttachCameraRig(GameplayCameraRig viewCameraRig)
        {
            _presentationCoordinator.AttachCameraRig(viewCameraRig);
            _viewCameraRig = viewCameraRig;
            _viewCameraBrain = null;
        }

        public void AttachOutputCamera(Camera outputCamera)
        {
            _presentationCoordinator.AttachOutputCamera(outputCamera);
        }

        internal void AttachGameplayAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            _presentationCoordinator.AttachGameplayAudioRuntime(playbackPort, gameplayAudioMap);
        }

        internal void AttachTileFeatureAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TileFeatureAudioMap tileFeatureAudioMap)
        {
            _presentationCoordinator.AttachTileFeatureAudioRuntime(playbackPort, tileFeatureAudioMap);
        }

        internal void AttachTopologyAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TopologyAudioMap topologyAudioMap)
        {
            _presentationCoordinator.AttachTopologyAudioRuntime(playbackPort, topologyAudioMap);
        }

        internal void AttachGravityFieldAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GravityFieldAudioMap gravityFieldAudioMap)
        {
            _presentationCoordinator.AttachGravityFieldAudioRuntime(playbackPort, gravityFieldAudioMap);
        }

        internal void AttachBlockAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            BlockAudioMap blockAudioMap)
        {
            _presentationCoordinator.AttachBlockAudioRuntime(playbackPort, blockAudioMap);
        }

        internal void AttachPlayerLocomotionAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            PlayerLocomotionAudioMap playerLocomotionAudioMap)
        {
            _presentationCoordinator.AttachPlayerLocomotionAudioRuntime(playbackPort, playerLocomotionAudioMap);
        }

        public void AttachTileFeatureVisualRegistry(ITileFeatureVisualRegistry registry)
        {
            _presentationCoordinator.AttachTileFeatureVisualRegistry(registry);
        }

        internal void AttachTileFeatureVisualPoseSynchronizer(TileFeatureVisualPoseSynchronizer synchronizer)
        {
            _presentationCoordinator.AttachTileFeatureVisualPoseSynchronizer(synchronizer);
        }

        internal void RegisterPresentationPauseRoot(GameObject root)
        {
            _presentationCoordinator.RegisterPresentationPauseRoot(root);
        }

        public void AttachPresentationExtension(IGameplayTickPresentationExtension extension)
        {
            _presentationCoordinator.AttachPresentationExtension(extension);
        }

        internal void ConfigureDamageDeathVfxExecution(
            DamageDeathVfxExecutionMode mode,
            IDamageDeathVfxPlaybackPort playbackPort = null)
        {
            _presentationCoordinator.ConfigureDamageDeathVfxExecution(mode, playbackPort);
        }

        internal void ConfigureBoxMotionPresentationExecution(
            BoxMotionPresentationExecutionMode mode,
            IGameplayMotionPlaybackPort playbackPort = null,
            bool useDefaultPlaybackPort = true)
        {
            _presentationCoordinator.ConfigureBoxMotionPresentationExecution(
                mode,
                playbackPort,
                useDefaultPlaybackPort);
        }

        internal void ConfigurePlayerActionAnimationExecution(
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort = null)
        {
            _presentationCoordinator.ConfigurePlayerActionAnimationExecution(mode, playbackPort);
        }

        internal void ConfigureEnemyPresentationExecution(
            EnemyPresentationExecutionMode mode,
            IGameplayEnemyPresentationPlaybackPort playbackPort = null)
        {
            _presentationCoordinator.ConfigureEnemyPresentationExecution(mode, playbackPort);
        }

        internal void ConfigureCoreGameplaySfxExecution(
            CoreGameplaySfxExecutionMode mode,
            IGameplaySfxPlaybackPort playbackPort = null)
        {
            _presentationCoordinator.ConfigureCoreGameplaySfxExecution(mode, playbackPort);
        }

        internal void ConfigureActionAudioExecution(
            ActionAudioExecutionMode mode,
            IGameplayActionAudioPlaybackPort playbackPort = null)
        {
            _presentationCoordinator.ConfigureActionAudioExecution(mode, playbackPort);
        }

        internal void ConfigureEnemyAudioExecution(
            EnemyAudioExecutionMode mode,
            IGameplayEnemyAudioPlaybackPort playbackPort = null)
        {
            _presentationCoordinator.ConfigureEnemyAudioExecution(mode, playbackPort);
        }

        public void DetachPresentationExtension(IGameplayTickPresentationExtension extension)
        {
            _presentationCoordinator.DetachPresentationExtension(extension);
        }

        public void ApplyStageTerminalPresentation(
            GameplayStageTerminalPresentationReason reason,
            TickResult terminalTickResult)
        {
            _presentationCoordinator.ApplyStageTerminalPresentation(reason, terminalTickResult);
        }

        internal void DebugRefreshGameplayAudioPlan(TickResult result)
        {
            _presentationCoordinator.DebugRefreshGameplayAudioPlan(result);
        }

        internal void SetPresentationTraceSink(System.Action<string> traceSink)
        {
            _presentationCoordinator.SetTraceSink(traceSink);
        }

        internal void SetTileFeatureVisualDiagnosticSink(System.Action<string> diagnosticSink)
        {
            _presentationCoordinator.SetTileFeatureVisualDiagnosticSink(diagnosticSink);
        }

        internal void SetGravityFieldVisualDiagnosticSink(System.Action<string> diagnosticSink)
        {
            _presentationCoordinator.SetGravityFieldVisualDiagnosticSink(diagnosticSink);
        }

        internal GameplayEntityPresentationLifecycleDebugSnapshot DebugCaptureEntityPresentationLifecycle(
            int entityId,
            float timelineTimeSeconds = 0f)
        {
            return _presentationCoordinator.DebugCaptureEntityPresentationLifecycle(entityId, timelineTimeSeconds);
        }

        internal int PendingGameplayAudioRequestCount => _presentationCoordinator.PendingGameplayAudioRequestCount;

        internal int DeferredGameplayAudioRequestCount => _presentationCoordinator.DeferredGameplayAudioRequestCount;

        internal int PendingMoonBlockEmergenceRequestCount =>
            _presentationCoordinator.PendingMoonBlockEmergenceRequestCount;

        internal int ActiveMoonBlockDestructionGhostCount =>
            _presentationCoordinator.ActiveMoonBlockDestructionGhostCount;

        internal EntityPresentationApplyDiagnostics DebugLastEntityPresentationApplyDiagnostics =>
            _presentationCoordinator.DebugLastEntityPresentationApplyDiagnostics;

        internal TopologyPresentationExecutionMode TopologyPresentationExecutionMode =>
            _presentationCoordinator.TopologyPresentationExecutionMode;

        internal TopologyPresentationOwnershipDiagnostics TopologyPresentationOwnershipDiagnostics =>
            _presentationCoordinator.TopologyPresentationOwnershipDiagnostics;

        internal DamageDeathVfxExecutionMode DamageDeathVfxExecutionMode =>
            _presentationCoordinator.DamageDeathVfxExecutionMode;

        internal DamageDeathVfxOwnershipDiagnostics DamageDeathVfxOwnershipDiagnostics =>
            _presentationCoordinator.DamageDeathVfxOwnershipDiagnostics;

        internal PresentationBlockingSnapshot DamageDeathVfxExecutionPipelineBlockingSnapshot =>
            _presentationCoordinator.DamageDeathVfxExecutionPipelineBlockingSnapshot;

        internal GameplayVfxExecutorDiagnostics DamageDeathVfxExecutorDiagnostics =>
            _presentationCoordinator.DamageDeathVfxExecutorDiagnostics;

        internal BoxMotionPresentationExecutionMode BoxMotionPresentationExecutionMode =>
            _presentationCoordinator.BoxMotionPresentationExecutionMode;

        internal BoxMotionOwnershipDiagnostics BoxMotionOwnershipDiagnostics =>
            _presentationCoordinator.BoxMotionOwnershipDiagnostics;

        internal PresentationBlockingSnapshot BoxMotionExecutionPipelineBlockingSnapshot =>
            _presentationCoordinator.BoxMotionExecutionPipelineBlockingSnapshot;

        internal GameplayMotionExecutorDiagnostics BoxMotionExecutorDiagnostics =>
            _presentationCoordinator.BoxMotionExecutorDiagnostics;

        internal BoxMotionPresentationRuntimeDebugSnapshot BoxMotionRuntimeDebugSnapshot =>
            _presentationCoordinator.BoxMotionRuntimeDebugSnapshot;

        internal BoxMotionProductionTelemetrySnapshot BoxMotionProductionTelemetrySnapshot =>
            _presentationCoordinator.BoxMotionProductionTelemetrySnapshot;

        internal void DebugHardCleanupPresentationExtensions()
        {
            _presentationCoordinator.HardCleanupPresentationExtensions();
        }

        internal PlayerActionAnimationExecutionMode PlayerActionAnimationExecutionMode =>
            _presentationCoordinator.PlayerActionAnimationExecutionMode;

        internal PlayerActionAnimationOwnershipDiagnostics PlayerActionAnimationOwnershipDiagnostics =>
            _presentationCoordinator.PlayerActionAnimationOwnershipDiagnostics;

        internal PresentationBlockingSnapshot PlayerActionAnimationExecutionPipelineBlockingSnapshot =>
            _presentationCoordinator.PlayerActionAnimationExecutionPipelineBlockingSnapshot;

        internal GameplayAnimationExecutorDiagnostics PlayerActionAnimationExecutorDiagnostics =>
            _presentationCoordinator.PlayerActionAnimationExecutorDiagnostics;

        internal EnemyPresentationExecutionMode EnemyPresentationExecutionMode =>
            _presentationCoordinator.EnemyPresentationExecutionMode;

        internal EnemyPresentationOwnershipDiagnostics EnemyPresentationOwnershipDiagnostics =>
            _presentationCoordinator.EnemyPresentationOwnershipDiagnostics;

        internal PresentationBlockingSnapshot EnemyPresentationExecutionPipelineBlockingSnapshot =>
            _presentationCoordinator.EnemyPresentationExecutionPipelineBlockingSnapshot;

        internal GameplayEnemyPresentationExecutorDiagnostics EnemyPresentationExecutorDiagnostics =>
            _presentationCoordinator.EnemyPresentationExecutorDiagnostics;

        internal CoreGameplaySfxExecutionMode CoreGameplaySfxExecutionMode =>
            _presentationCoordinator.CoreGameplaySfxExecutionMode;

        internal CoreGameplaySfxOwnershipDiagnostics CoreGameplaySfxOwnershipDiagnostics =>
            _presentationCoordinator.CoreGameplaySfxOwnershipDiagnostics;

        internal PresentationBlockingSnapshot CoreGameplaySfxExecutionPipelineBlockingSnapshot =>
            _presentationCoordinator.CoreGameplaySfxExecutionPipelineBlockingSnapshot;

        internal GameplaySfxExecutorDiagnostics CoreGameplaySfxExecutorDiagnostics =>
            _presentationCoordinator.CoreGameplaySfxExecutorDiagnostics;

        internal ActionAudioExecutionMode ActionAudioExecutionMode =>
            _presentationCoordinator.ActionAudioExecutionMode;

        internal ActionAudioOwnershipDiagnostics ActionAudioOwnershipDiagnostics =>
            _presentationCoordinator.ActionAudioOwnershipDiagnostics;

        internal PresentationBlockingSnapshot ActionAudioExecutionPipelineBlockingSnapshot =>
            _presentationCoordinator.ActionAudioExecutionPipelineBlockingSnapshot;

        internal GameplayActionAudioExecutorDiagnostics ActionAudioExecutorDiagnostics =>
            _presentationCoordinator.ActionAudioExecutorDiagnostics;

        internal EnemyAudioExecutionMode EnemyAudioExecutionMode =>
            _presentationCoordinator.EnemyAudioExecutionMode;

        internal EnemyAudioOwnershipDiagnostics EnemyAudioOwnershipDiagnostics =>
            _presentationCoordinator.EnemyAudioOwnershipDiagnostics;

        internal PresentationBlockingSnapshot EnemyAudioExecutionPipelineBlockingSnapshot =>
            _presentationCoordinator.EnemyAudioExecutionPipelineBlockingSnapshot;

        internal GameplayEnemyAudioExecutorDiagnostics EnemyAudioExecutorDiagnostics =>
            _presentationCoordinator.EnemyAudioExecutorDiagnostics;

        public void AttachCameraRuntime(GameplayCameraRig viewCameraRig, CinemachineBrain viewCameraBrain)
        {
            _presentationCoordinator.AttachCameraRig(viewCameraRig);
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

            if (_presentationCoordinator.IsPresentationPaused)
            {
                return;
            }

            _presentationCoordinator.UpdatePresentation(deltaTime);
            SyncViewCameraRuntime();
            RefreshTopologyTransitionPostFx();
            NotifyPresentationStateChangedIfNeeded();
            PresentationAdvanced?.Invoke(deltaTime);
        }

        public void SetPresentationPaused(bool paused)
        {
            _presentationCoordinator.SetPresentationPaused(paused);
            NotifyPresentationStateChangedIfNeeded();
        }

        private void RefreshTopologyTransitionPostFx()
        {
            _topologyTransitionPostFxController?.Apply(_presentationCoordinator.CurrentTopologyTransitionVisualState);
        }

        private void SyncViewCameraRuntime()
        {
            if (_viewCameraRig != null)
            {
                _viewCameraRig.ApplyTopologyTransitionVisualState(_presentationCoordinator.CurrentTopologyTransitionVisualState);
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
            if (_presentationCoordinator.IsInitialized)
            {
                UpdatePresentation(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            _presentationCoordinator.HardCleanupPresentationExtensions();
            _presentationCoordinator.DetachBlockAudioRuntime();
            _presentationCoordinator.DetachGravityFieldAudioRuntime();
            _presentationCoordinator.DetachTileFeatureAudioRuntime();
            _presentationCoordinator.DetachTopologyAudioRuntime();
            _presentationCoordinator.DetachGameplayAudioRuntime();
        }

        private void CapturePresentationState()
        {
            _lastObservedTopology = _presentationCoordinator.CurrentTopology;
            _lastIsPresentationActive = _presentationCoordinator.IsPresentationActive;
            _lastHasBlockingPresentation = _presentationCoordinator.HasBlockingPresentation;
            _lastIsTopologyTransitionActive = _presentationCoordinator.IsTopologyTransitionActive;
            _hasObservedPresentationState = true;
        }

        private void NotifyPresentationStateChangedIfNeeded()
        {
            if (!_hasObservedPresentationState)
            {
                CapturePresentationState();
                return;
            }

            var currentTopology = _presentationCoordinator.CurrentTopology;
            var isPresentationActive = _presentationCoordinator.IsPresentationActive;
            var hasBlockingPresentation = _presentationCoordinator.HasBlockingPresentation;
            var isTopologyTransitionActive = _presentationCoordinator.IsTopologyTransitionActive;

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
