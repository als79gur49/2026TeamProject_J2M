using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayTickPresentationCoordinator
    {
        private readonly GameplayAnimationSyncCoordinator _animationSync = new();
        private readonly GameplayActionAudioRequestPlanner _actionAudioRequestPlanner = new();
        private readonly GameplayActionAudioPresentationController _actionAudioPresentationController;
        private readonly GameplayAudioRequestPlanner _audioRequestPlanner = new();
        private readonly GameplayAudioPresentationController _audioPresentationController;
        private readonly GameplayCommittedFrameBuilder _committedFrameBuilder;
        private readonly GameplayEntityPresentationApplier _entityPresentationApplier;
        private readonly IEnemyVisualSemanticResolver _enemyVisualSemanticResolver = new DefaultEnemyVisualSemanticResolver();
        private readonly GameplayExitPresentationController _exitPresentationController;
        private readonly GameplayTrackPlanner _planner;
        private readonly GameplayPresentationActivityInspector _presentationActivityInspector;
        private readonly GameplayPresentationStateStore _stateStore = new();
        private readonly SummonedEnemyPresentationResolver _summonedEnemyPresentationResolver = new();
        private readonly GameplayPresentationTrackState _trackState = new();
        private readonly GameplayTopologyTransitionController _topologyTransitionController;
        private readonly GameplayTransientEffectPresenter _transientEffectPresenter = new();
        private readonly GameplayFrontFaceShieldVfxPresenter _frontFaceShieldVfxPresenter = new();
        private readonly GameplayUtilityWindupVfxPresenter _utilityWindupVfxPresenter = new();
        private readonly GameplayMotionTimingResolver _motionTimingResolver;
        private readonly GameplayPoseResolver _poseResolver;
        private readonly List<IGameplayTickPresentationExtension> _presentationExtensions = new();

        private bool _isInitialized;
        private GameplayCubeProjector _projector;
        private EnemyPresentationBinding[] _enemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        private EnemyPresentationCatalog _enemyPresentationCatalog;
        private GameplayTimingProfile _timingProfile;
        private GameplayEntityViewBinder _viewBinder;
        private Camera _outputCamera;
        private Action<string> _traceSink;

        public GameplayTickPresentationCoordinator()
        {
            _audioPresentationController = new GameplayAudioPresentationController(_stateStore);
            _actionAudioPresentationController = new GameplayActionAudioPresentationController(_stateStore);
            _presentationActivityInspector = new GameplayPresentationActivityInspector(
                _trackState,
                _transientEffectPresenter);
            _motionTimingResolver = new GameplayMotionTimingResolver(_stateStore, _trackState);
            _topologyTransitionController = new GameplayTopologyTransitionController(_motionTimingResolver);
            _poseResolver = new GameplayPoseResolver(
                _stateStore,
                _trackState);
            _committedFrameBuilder = new GameplayCommittedFrameBuilder(
                _stateStore,
                _poseResolver,
                _animationSync);
            _entityPresentationApplier = new GameplayEntityPresentationApplier(
                _stateStore,
                _trackState,
                _poseResolver,
                _animationSync,
                _motionTimingResolver,
                _enemyVisualSemanticResolver,
                _committedFrameBuilder);
            _exitPresentationController = new GameplayExitPresentationController(
                _stateStore,
                _trackState,
                _motionTimingResolver,
                _poseResolver,
                _transientEffectPresenter);
            _planner = new GameplayTrackPlanner(
                _stateStore,
                _trackState,
                _motionTimingResolver,
                _poseResolver,
                _exitPresentationController,
                _entityPresentationApplier);
        }

        public event Action<CubeTopologyState> TopologyCommitted;

        public CubeTopologyState CurrentTopology => _stateStore.CommittedTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => ResolveCurrentPresentationPhase();

        public Vector3 CubeCenter => _projector != null ? _projector.GetCubeCenter() : Vector3.zero;

        public bool HasBlockingPresentation => _topologyTransitionController.HasActiveBoardRotationTween;

        public bool IsInitialized => _isInitialized;

        public bool IsPresentationActive => CurrentPresentationPhase != GameplayPresentationPhase.Idle;

        public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;

        public int ActiveTransientEffectCount => _transientEffectPresenter.ActiveEffectCount;

        public TopologyTransitionVisualState CurrentTopologyTransitionVisualState =>
            _topologyTransitionController.CurrentVisualState;

        public Quaternion PresentedBoardRotation => _topologyTransitionController.PresentedBoardRotation;

        internal int PendingGameplayAudioRequestCount => _audioPresentationController.PendingRequestCount;

        public Bounds VisibleCubeBounds
        {
            get
            {
                EnsureInitialized();
                return _projector.GetVisibleCubeBounds(_stateStore.CommittedTopology);
            }
        }

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
            EnemyPresentationBinding[] enemyPresentationBindings = null)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _viewBinder = viewBinder;
            _enemyPresentationCatalog = enemyPresentationCatalog;
            _enemyPresentationBindings = enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
            var resolvedFaceSeamGap = faceSeamGap >= 0f ? faceSeamGap : cellSize;
            _projector = new GameplayCubeProjector(boardBounds, cellSize, resolvedFaceSeamGap);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _topologyTransitionController.Configure(
                boardRoot,
                boardSurfaceRenderer,
                _timingProfile,
                topologyRotationVisualMapping,
                topologyRotationTweenSettings,
                () => CubeCenter);
            _topologyTransitionController.Reset();
            _exitPresentationController.Configure(_projector, _timingProfile);
            _exitPresentationController.Reset();
            _audioPresentationController.ResetSession();
            _actionAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _trackState.ResetSession();
            _transientEffectPresenter.Initialize(viewBinder.SearchRoot, cellSize);
            _frontFaceShieldVfxPresenter.Initialize(viewBinder.SearchRoot, cellSize);
            _utilityWindupVfxPresenter.Initialize(viewBinder.SearchRoot);
            _animationSync.Reset();
            _stateStore.ResetSession(initialTopology);
            _summonedEnemyPresentationResolver.Initialize(
                boardRoot != null ? boardRoot.EntityRoot : viewBinder.SearchRoot,
                viewBinder.ViewRegistry,
                _stateStore,
                _animationSync,
                enemyPresentationArchetypeRegistry);

            _isInitialized = true;
        }

        public void AttachCameraRig(GameplayCameraRig viewCameraRig)
        {
            _topologyTransitionController.AttachCameraRig(viewCameraRig);
        }

        public void AttachOutputCamera(Camera outputCamera)
        {
            _outputCamera = outputCamera;
            _transientEffectPresenter.ConfigureOutputCamera(outputCamera);
            _planner.ConfigureOutputCamera(outputCamera, _viewBinder != null ? _viewBinder.SearchRoot : null);
            ConfigureOutputCameraPresentationExtensions();
        }

        public void AttachPresentationExtension(IGameplayTickPresentationExtension extension)
        {
            if (extension == null ||
                _presentationExtensions.Contains(extension))
            {
                return;
            }

            _presentationExtensions.Add(extension);
            extension.ResetSession();
            if (extension is IGameplayOutputCameraPresentationExtension outputCameraExtension)
            {
                outputCameraExtension.ConfigureOutputCamera(
                    _outputCamera,
                    _viewBinder != null ? _viewBinder.SearchRoot : null);
            }
        }

        public void DetachPresentationExtension(IGameplayTickPresentationExtension extension)
        {
            if (extension == null ||
                !_presentationExtensions.Remove(extension))
            {
                return;
            }

            extension.HardCleanup();
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            EnsureInitialized();

            var previousCommittedLocalTargetPoses =
                new Dictionary<int, GameplayEntityPose>(_stateStore.CommittedLocalTargetPoses);
            var previousCommittedTopology = _stateStore.CommittedTopology;

            TraceStep("RefreshAudioPlan");
            _audioPresentationController.ReplacePendingPlan(_audioRequestPlanner.BuildRequests(result));
            _actionAudioPresentationController.ReplacePendingPlan(_actionAudioRequestPlanner.BuildRequests(result));
            _summonedEnemyPresentationResolver.Reconcile(result);
            _committedFrameBuilder.StoreCommittedFrame(
                result.FinalEntities,
                result.FinalTopology,
                _projector,
                _viewBinder,
                TopologyCommitted);
            PresentExtensions(result);
            TraceStep("RefreshUtilityWindupWarnings");
            _utilityWindupVfxPresenter.RefreshSummonWarnings(
                Array.Empty<TickSummonWindupWarningSignal>(),
                _stateStore,
                _projector);

            _frontFaceShieldVfxPresenter.RefreshWindupWarnings(
                result.PresentationData.FrontFaceShieldWindupWarnings,
                _stateStore,
                _projector);
            TraceStep("RefreshFrontFaceShieldSources");
            _frontFaceShieldVfxPresenter.RefreshActiveSources(
                Array.Empty<TickFrontFaceShieldSourceSignal>(),
                _stateStore,
                _projector);
            _exitPresentationController.RefreshEntityExitPlan(result.PresentationData);
            _planner.RefreshPlayerLocomotionSignals(result.PresentationData);
            _topologyTransitionController.RefreshTopologyTrack(
                result.PresentationData,
                _stateStore.CommittedTopology);
            _topologyTransitionController.RefreshBoardSurfaceTransition(
                result.PresentationData,
                _stateStore.CommittedTopology);
            _planner.RefreshTracks(
                result,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology,
                _projector,
                _timingProfile);
            TraceStep("PlayEntityExitEffects");
            _exitPresentationController.PlayEntityExitEffects();
            _animationSync.ApplyTickPresentation(
                result,
                _stateStore.ViewsByEntityId,
                (entityId, actionKind) => _motionTimingResolver.ResolvePlayerMotionDurationSeconds(
                    entityId,
                    actionKind,
                    _timingProfile));
            TraceStep("PlayPlannedAudio");
            _audioPresentationController.PlayPlannedAudio();
            _actionAudioPresentationController.PlayPlannedAudio();
            TraceStep("ApplyEntityExitOwnership");
            _exitPresentationController.ApplyEntityExitOwnership();
            _summonedEnemyPresentationResolver.CleanupOwnedViews(result.FinalEntities);
            UpdatePresentation(0f);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();

            _audioPresentationController.ResetSession();
            _actionAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _trackState.ResetSession();
            _exitPresentationController.Reset();
            _transientEffectPresenter.Clear();
            _frontFaceShieldVfxPresenter.Clear();
            _utilityWindupVfxPresenter.Clear();
            ResetExtensions();
            _animationSync.Reset();
            _stateStore.ResetSession(topology);
            _topologyTransitionController.Reset();

            _committedFrameBuilder.StoreCommittedFrame(
                entities,
                topology,
                _projector,
                _viewBinder,
                TopologyCommitted);
            _topologyTransitionController.CompleteInitialTopology(topology);
            _animationSync.ApplyInitialEnemyPresentation(
                entities,
                _stateStore.CommittedLocalTargetPoses,
                _stateStore.ViewsByEntityId);
            _animationSync.ApplyInitialPlayerPresentation(_stateStore.CommittedLocalTargetPoses);
            UpdatePresentation(0f);
        }

        public void UpdatePresentation(float deltaTime)
        {
            EnsureInitialized();

            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (!_stateStore.HasAnyCommittedFrame)
            {
                return;
            }

            _topologyTransitionController.UpdatePresentation(deltaTime, _stateStore.CommittedTopology);
            _transientEffectPresenter.Update(deltaTime);
            _frontFaceShieldVfxPresenter.Update(deltaTime);
            UpdateExtensions(deltaTime);
            _entityPresentationApplier.Apply(
                deltaTime,
                _topologyTransitionController.HasActiveBoardRotationTween,
                _viewBinder,
                _timingProfile);
        }

        internal void AttachGameplayAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            _audioPresentationController.AttachRuntime(playbackPort, gameplayAudioMap);
            _actionAudioPresentationController.AttachRuntime(playbackPort);
        }

        internal void DetachGameplayAudioRuntime()
        {
            _actionAudioPresentationController.DetachRuntime();
            _audioPresentationController.DetachRuntime();
        }

        internal void DebugRefreshGameplayAudioPlan(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _audioPresentationController.ReplacePendingPlan(_audioRequestPlanner.BuildRequests(result));
            _actionAudioPresentationController.ReplacePendingPlan(_actionAudioRequestPlanner.BuildRequests(result));
        }

        internal void SetTraceSink(Action<string> traceSink)
        {
            _traceSink = traceSink;
        }

        internal void HardCleanupPresentationExtensions()
        {
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.HardCleanup();
            }
        }

        private void PresentExtensions(TickResult result)
        {
            if (_presentationExtensions.Count == 0)
            {
                return;
            }

            var context = new GameplayTickPresentationExtensionContext(
                result,
                _stateStore.CommittedTopology,
                _stateStore,
                _projector,
                _enemyPresentationCatalog,
                _enemyPresentationBindings,
                _timingProfile);
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.Present(context);
            }
        }

        private void ResetExtensions()
        {
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.ResetSession();
            }
        }

        private void UpdateExtensions(float deltaTime)
        {
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.UpdatePresentation(deltaTime);
            }
        }

        private void ConfigureOutputCameraPresentationExtensions()
        {
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayOutputCameraPresentationExtension outputCameraExtension)
                {
                    outputCameraExtension.ConfigureOutputCamera(
                        _outputCamera,
                        _viewBinder != null ? _viewBinder.SearchRoot : null);
                }
            }
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("GameplayTickViewPresenter must be initialized before use.");
            }
        }

        private GameplayPresentationPhase ResolveCurrentPresentationPhase()
        {
            if (_topologyTransitionController.HasActiveBoardRotationTween)
            {
                return GameplayPresentationPhase.TopologyTransition;
            }

            if (_presentationActivityInspector.HasActiveEntityPresentationClips())
            {
                return GameplayPresentationPhase.EntityMotion;
            }

            if (_animationSync.HasActivePlayerVisualHold)
            {
                return GameplayPresentationPhase.EntityMotion;
            }

            return GameplayPresentationPhase.Idle;
        }

        private void TraceStep(string stepName)
        {
            _traceSink?.Invoke(stepName);
        }

    }
}
