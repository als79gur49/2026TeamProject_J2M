using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayTickPresentationCoordinator
    {
        private static readonly IReadOnlyList<TilePresentationRequest> EmptyTilePresentationRequests =
            Array.Empty<TilePresentationRequest>();
        private static readonly IReadOnlyList<GravityFieldPresentationRequest> EmptyGravityFieldPresentationRequests =
            Array.Empty<GravityFieldPresentationRequest>();
        private static readonly IReadOnlyList<GravityFieldVisualState> EmptyGravityFieldVisualStates =
            Array.Empty<GravityFieldVisualState>();
        private static readonly IReadOnlyList<TileFeatureVisualState> EmptyTileFeatureVisualStates =
            Array.Empty<TileFeatureVisualState>();

        private readonly GameplayAnimationSyncCoordinator _animationSync = new();
        private readonly GameplayActionAudioRequestPlanner _actionAudioRequestPlanner = new();
        private readonly GameplayActionAudioPresentationController _actionAudioPresentationController;
        private readonly EnemyAudioRequestPlanner _enemyAudioRequestPlanner = new();
        private readonly EnemyAudioPresentationController _enemyAudioPresentationController;
        private readonly EnemyChargeLoopAudioPresentationController _enemyChargeLoopAudioPresentationController;
        private readonly BlockAudioRequestPlanner _blockAudioRequestPlanner = new();
        private readonly BlockAudioPresentationController _blockAudioPresentationController;
        private readonly PlayerLocomotionAudioPresentationController _playerLocomotionAudioPresentationController;
        private readonly GameplayAudioRequestPlanner _audioRequestPlanner = new();
        private readonly GameplayAudioPresentationController _audioPresentationController;
        private readonly GameplayCommittedFrameBuilder _committedFrameBuilder;
        private readonly GameplayEntityPresentationApplier _entityPresentationApplier;
        private readonly IEnemyVisualSemanticResolver _enemyVisualSemanticResolver = new DefaultEnemyVisualSemanticResolver();
        private readonly GameplayExitPresentationController _exitPresentationController;
        private readonly GameplayTrackPlanner _planner;
        private readonly GameplayPresentationActivityInspector _presentationActivityInspector;
        private readonly GameplayPresentationStateStore _stateStore = new();
        private readonly GravityFieldAudioRequestPlanner _gravityFieldAudioRequestPlanner = new();
        private readonly GravityFieldAudioPresentationController _gravityFieldAudioPresentationController;
        private readonly GravityFieldPresentationRequestPlanner _gravityFieldPresentationRequestPlanner = new();
        private readonly GravityFieldVisualPresentationController _gravityFieldVisualPresentationController;
        private readonly SummonedEnemyPresentationResolver _summonedEnemyPresentationResolver = new();
        private readonly TileFeatureAudioRequestPlanner _tileFeatureAudioRequestPlanner = new();
        private readonly TileFeatureAudioPresentationController _tileFeatureAudioPresentationController;
        private readonly TopologyAudioRequestPlanner _topologyAudioRequestPlanner = new();
        private readonly TopologyAudioPresentationController _topologyAudioPresentationController = new();
        private readonly GameplayPresentationTrackState _trackState = new();
        private readonly TilePresentationRequestPlanner _tilePresentationRequestPlanner = new();
        private readonly GameplayTopologyTransitionController _topologyTransitionController;
        private readonly GameplayFrontFaceShieldVfxPresenter _frontFaceShieldVfxPresenter = new();
        private readonly GameplayUtilityWindupVfxPresenter _utilityWindupVfxPresenter = new();
        private readonly TileFeatureVisualPresentationController _tileFeatureVisualPresentationController = new();
        private readonly MoonBlockEmergencePresentationController _moonBlockEmergencePresentationController = new();
        private readonly GameplayMotionTimingResolver _motionTimingResolver;
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplaySfxArbiter _gameplaySfxArbiter = new();
        private readonly List<IGameplayTickPresentationExtension> _presentationExtensions = new();

        private bool _isInitialized;
        private GameplayCubeProjector _projector;
        private EnemyPresentationBinding[] _enemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        private EnemyPresentationCatalog _enemyPresentationCatalog;
        private IReadOnlyList<TileFeatureVfxStyleBinding> _tileFeatureVfxStyleBindings =
            Array.Empty<TileFeatureVfxStyleBinding>();
        private GameplayTimingProfile _timingProfile;
        private GameplayEntityViewBinder _viewBinder;
        private Camera _outputCamera;
        private IReadOnlyList<TilePresentationRequest> _currentTilePresentationRequests = EmptyTilePresentationRequests;
        private IReadOnlyList<GravityFieldPresentationRequest> _currentGravityFieldPresentationRequests =
            EmptyGravityFieldPresentationRequests;
        private IReadOnlyList<GravityFieldVisualState> _currentGravityFieldVisualStates =
            EmptyGravityFieldVisualStates;
        private IReadOnlyList<TileFeatureVisualState> _currentTileFeatureVisualStates =
            EmptyTileFeatureVisualStates;
        private Action<string> _traceSink;
        private int _lastPresentedTickIndex;
        private TileFeatureVisualPoseSynchronizer _tileFeatureVisualPoseSynchronizer;
        private IGameplayAudioPlaybackPort _rawGameplayAudioPlaybackPort;
        private GameplaySfxArbitratingPlaybackPort _arbitratingGameplayAudioPlaybackPort;
        private TickResult _lastPresentedResult;
        private int _topologyTransitionEpoch;

        public GameplayTickPresentationCoordinator()
        {
            _audioPresentationController = new GameplayAudioPresentationController(_stateStore);
            _actionAudioPresentationController = new GameplayActionAudioPresentationController(_stateStore);
            _enemyAudioPresentationController = new EnemyAudioPresentationController(_stateStore);
            _enemyChargeLoopAudioPresentationController = new EnemyChargeLoopAudioPresentationController(_stateStore);
            _blockAudioPresentationController = new BlockAudioPresentationController(_stateStore);
            _playerLocomotionAudioPresentationController = new PlayerLocomotionAudioPresentationController(_stateStore);
            _tileFeatureAudioPresentationController = new TileFeatureAudioPresentationController(_stateStore);
            _gravityFieldAudioPresentationController = new GravityFieldAudioPresentationController(_stateStore);
            _gravityFieldVisualPresentationController = new GravityFieldVisualPresentationController(_stateStore);
            _presentationActivityInspector = new GameplayPresentationActivityInspector(_trackState);
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
                _animationSync,
                _stateStore,
                _trackState);
            _planner = new GameplayTrackPlanner(
                _stateStore,
                _trackState,
                _motionTimingResolver,
                _poseResolver,
                _exitPresentationController,
                _entityPresentationApplier);
            _topologyTransitionController.TopologyPresentationCompleted += HandleTopologyPresentationCompleted;
            _topologyTransitionController.TopologyTransitionPresentationCompleted +=
                HandleTopologyTransitionPresentationCompleted;
        }

        public event Action<CubeTopologyState> TopologyCommitted;

        public CubeTopologyState CurrentTopology => _stateStore.CommittedTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => ResolveCurrentPresentationPhase();

        public Vector3 CubeCenter => _projector != null ? _projector.GetCubeCenter() : Vector3.zero;

        public bool HasBlockingPresentation =>
            _topologyTransitionController.HasActiveBoardRotationTween ||
            _presentationActivityInspector.HasActiveBlockingJumpLandingCompletion();

        public bool IsInitialized => _isInitialized;

        public bool IsPresentationActive => CurrentPresentationPhase != GameplayPresentationPhase.Idle;

        public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;

        public bool IsPlayerActionAttemptPlaybackActive(int entityId) =>
            _animationSync.IsPlayerActionAttemptHoldActive(entityId);

        public TopologyTransitionVisualState CurrentTopologyTransitionVisualState =>
            _topologyTransitionController.CurrentVisualState;

        public Quaternion PresentedBoardRotation => _topologyTransitionController.PresentedBoardRotation;

        public IReadOnlyList<TilePresentationRequest> CurrentTilePresentationRequests => _currentTilePresentationRequests;

        public IReadOnlyList<GravityFieldPresentationRequest> CurrentGravityFieldPresentationRequests =>
            _currentGravityFieldPresentationRequests;

        public IReadOnlyList<GravityFieldVisualState> CurrentGravityFieldVisualStates =>
            _currentGravityFieldVisualStates;

        public IReadOnlyList<TileFeatureVisualState> CurrentTileFeatureVisualStates =>
            _currentTileFeatureVisualStates;

        internal int PendingGameplayAudioRequestCount => _audioPresentationController.PendingRequestCount;

        internal int PendingMoonBlockEmergenceRequestCount =>
            _moonBlockEmergencePresentationController.PendingRequestCount;

        internal GameplayEntityPresentationLifecycleDebugSnapshot DebugCaptureEntityPresentationLifecycle(
            int entityId,
            float timelineTimeSeconds = 0f)
        {
            var hasView = _stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) && view != null;
            var hasEnemyAnimatorDriver = hasView && view.TryGetComponent<EnemyAnimatorDriver>(out _);
            var hasEntityViewComponent = hasView && view.TryGetComponent<GameplayEntityView>(out _);
            var rendererEnabled = false;
            var rendererActiveInHierarchy = false;
            var animatorCurrentStateShortNameHash = 0;
            var animatorNormalizedTime = 0f;
            var deathTriggerCount = 0;

            if (hasView)
            {
                var renderers = view.GetComponentsInChildren<Renderer>(includeInactive: true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    rendererEnabled = true;
                    if (renderer.gameObject.activeInHierarchy)
                    {
                        rendererActiveInHierarchy = true;
                    }
                }

                var animator = view.GetComponentInChildren<Animator>(includeInactive: true);
                if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                {
                    var currentState = animator.GetCurrentAnimatorStateInfo(0);
                    animatorCurrentStateShortNameHash = currentState.shortNameHash;
                    animatorNormalizedTime = currentState.normalizedTime;
                }

                if (view.TryGetComponent<EnemyAnimatorDriver>(out var enemyDriver) && enemyDriver != null)
                {
                    deathTriggerCount = enemyDriver.DeathSignalCount;
                }
            }

            var pendingContactRemaining = _exitPresentationController.TryGetPendingContactDelayedExitRemainingSeconds(
                entityId,
                out var resolvedPendingContactRemaining)
                ? resolvedPendingContactRemaining
                : 0f;
            var pendingDeathRemaining = _exitPresentationController.TryGetPendingDeathPresentationCleanupRemainingSeconds(
                entityId,
                out var resolvedPendingDeathRemaining)
                ? resolvedPendingDeathRemaining
                : 0f;

            return new GameplayEntityPresentationLifecycleDebugSnapshot(
                timelineTimeSeconds,
                entityId,
                hasView ? view.gameObject.name : string.Empty,
                hasView ? view.GetInstanceID() : 0,
                hasView && view.gameObject.activeSelf,
                hasEnemyAnimatorDriver,
                hasEntityViewComponent,
                _stateStore.ViewsByEntityId.ContainsKey(entityId),
                _trackState.ContactDelayedRetainedEntityIds.Contains(entityId),
                _trackState.DeathPresentationPlayingEntityIds.Contains(entityId),
                _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId),
                _exitPresentationController.HasPendingContactDelayedExit(entityId),
                pendingContactRemaining,
                _exitPresentationController.HasPendingDeathPresentationCleanup(entityId),
                pendingDeathRemaining,
                hasView && view.gameObject.name.Contains("_PooledVfx"),
                rendererEnabled,
                rendererActiveInHierarchy,
                animatorCurrentStateShortNameHash,
                animatorNormalizedTime,
                deathTriggerCount);
        }

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
            EnemyPresentationBinding[] enemyPresentationBindings = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _viewBinder = viewBinder;
            _gravityFieldVisualPresentationController.AttachTargetViewRegistry(_viewBinder.ViewRegistry);
            _moonBlockEmergencePresentationController.Configure(_viewBinder.ViewRegistry, timingProfile);
            _enemyPresentationCatalog = enemyPresentationCatalog;
            _enemyPresentationBindings = enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
            _tileFeatureVfxStyleBindings = tileFeatureVfxStyleBindings ?? Array.Empty<TileFeatureVfxStyleBinding>();
            var resolvedFaceSeamGap = faceSeamGap >= 0f ? faceSeamGap : cellSize;
            _projector = new GameplayCubeProjector(boardBounds, cellSize, resolvedFaceSeamGap);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _enemyAudioPresentationController.ConfigureMoveCadence(_timingProfile.SimulationTicksPerSecond);
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
            _enemyAudioPresentationController.ResetSession();
            _enemyChargeLoopAudioPresentationController.ResetSession();
            _blockAudioPresentationController.ResetSession();
            _playerLocomotionAudioPresentationController.ResetSession();
            _tileFeatureAudioPresentationController.ResetSession();
            _topologyAudioPresentationController.ResetSession();
            _gravityFieldAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _trackState.ResetSession();
            _frontFaceShieldVfxPresenter.Initialize(viewBinder.SearchRoot, cellSize);
            _utilityWindupVfxPresenter.Initialize(viewBinder.SearchRoot);
            _animationSync.Reset();
            _stateStore.ResetSession(initialTopology);
            _currentTilePresentationRequests = EmptyTilePresentationRequests;
            _currentGravityFieldPresentationRequests = EmptyGravityFieldPresentationRequests;
            _currentGravityFieldVisualStates = EmptyGravityFieldVisualStates;
            _currentTileFeatureVisualStates = EmptyTileFeatureVisualStates;
            _summonedEnemyPresentationResolver.Initialize(
                boardRoot != null ? boardRoot.EntityRoot : viewBinder.SearchRoot,
                viewBinder.ViewRegistry,
                _stateStore,
                _animationSync,
                enemyPresentationArchetypeRegistry);
            _moonBlockEmergencePresentationController.ResetSession();

            _isInitialized = true;
        }

        public void AttachCameraRig(GameplayCameraRig viewCameraRig)
        {
            _topologyTransitionController.AttachCameraRig(viewCameraRig);
        }

        public void AttachOutputCamera(Camera outputCamera)
        {
            _outputCamera = outputCamera;
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
            _audioPresentationController.ReplacePendingPlan(_audioRequestPlanner.BuildRequests(result, _timingProfile));
            _actionAudioPresentationController.ReplacePendingPlan(_actionAudioRequestPlanner.BuildRequests(result));
            _enemyAudioPresentationController.ReplacePendingPlan(_enemyAudioRequestPlanner.BuildRequests(result, _timingProfile));
            _blockAudioPresentationController.ReplacePendingPlan(
                _blockAudioRequestPlanner.BuildRequests(result, _timingProfile));
            RefreshTilePresentationRequests(result.PresentationData);
            RefreshGravityFieldPresentationRequests(result.PresentationData);
            RefreshTileFeatureVisualStates(result.PresentationData);
            RefreshGravityFieldVisualStates(result.PresentationData);
            SyncTileFeatureVisualPoseForTopologyMotionIfNeeded(result.PresentationData);
            _tileFeatureAudioPresentationController.ReplacePendingPlan(
                _tileFeatureAudioRequestPlanner.BuildRequests(
                    _currentTilePresentationRequests,
                    result.TickIndex,
                    _timingProfile));
            _gravityFieldAudioPresentationController.ReplacePendingPlan(
                _gravityFieldAudioRequestPlanner.BuildRequests(_currentGravityFieldPresentationRequests));
            _tileFeatureVisualPresentationController.PlayRequests(_currentTilePresentationRequests, _timingProfile);
            _moonBlockEmergencePresentationController.QueueRequests(_currentTilePresentationRequests, result.TickIndex);
            _gravityFieldVisualPresentationController.PlayRequests(_currentGravityFieldPresentationRequests);
            _tileFeatureVisualPresentationController.RefreshContinuousStates(_currentTileFeatureVisualStates);
            _summonedEnemyPresentationResolver.Reconcile(result);
            _committedFrameBuilder.StoreCommittedFrame(
                result.FinalEntities,
                result.FinalTopology,
                _projector,
                _viewBinder,
                TopologyCommitted);
            _gravityFieldVisualPresentationController.RefreshContinuousStates(_currentGravityFieldVisualStates);
            if (IsTopologyTransitionPresentation(result.PresentationData.TopologyMotion))
            {
                _topologyTransitionEpoch++;
            }

            _lastPresentedResult = result;
            PresentExtensions(result);
            TraceStep("RefreshUtilityWindupWarnings");
            _utilityWindupVfxPresenter.RefreshSummonWarnings(
                Array.Empty<TickSummonWindupWarningSignal>(),
                _stateStore,
                _projector);

            _frontFaceShieldVfxPresenter.RefreshWindupWarnings(
                Array.Empty<TickFrontFaceShieldWindupWarningSignal>(),
                _stateStore,
                _projector);
            TraceStep("RefreshFrontFaceShieldSources");
            _frontFaceShieldVfxPresenter.RefreshActiveSources(
                Array.Empty<TickFrontFaceShieldSourceSignal>(),
                _stateStore,
                _projector);
            _exitPresentationController.RefreshEntityExitPlan(result.PresentationData);
            _planner.RefreshPlayerLocomotionSignals(result.PresentationData);
            _playerLocomotionAudioPresentationController.RefreshSignals(
                result.PresentationData.PlayerLocomotionSignals,
                _timingProfile.MoveMotionDurationSeconds);
            _topologyTransitionController.RefreshTopologyTrack(
                result.PresentationData,
                _stateStore.CommittedTopology);
            _topologyTransitionController.RefreshBoardSurfaceTransition(
                result.PresentationData,
                _stateStore.CommittedTopology);
            _topologyAudioPresentationController.ReplacePendingPlan(
                _topologyAudioRequestPlanner.BuildRequests(result));
            _planner.RefreshTracks(
                result,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology,
                _projector,
                _timingProfile);
            _lastPresentedTickIndex = result.TickIndex;
            RefreshPresentationMotionVfx(result.TickIndex);
            _animationSync.ApplyTickPresentation(
                result,
                _stateStore.ViewsByEntityId,
                _trackState.JumpLandingCompletionHoldEntityIds,
                (entityId, actionKind) => _motionTimingResolver.ResolvePlayerMotionDurationSeconds(
                    entityId,
                    actionKind,
                    _timingProfile));
            TraceStep("PlayPlannedAudio");
            _arbitratingGameplayAudioPlaybackPort?.BeginBatch(
                result.TickIndex,
                _timingProfile.SimulationTicksPerSecond);
            try
            {
                _audioPresentationController.PlayPlannedAudio();
                _actionAudioPresentationController.PlayPlannedAudio();
                _enemyAudioPresentationController.PlayPlannedAudio(result.TickIndex);
                _blockAudioPresentationController.PlayPlannedAudio();
                _playerLocomotionAudioPresentationController.PlayPlannedAudio();
                _tileFeatureAudioPresentationController.PlayPlannedAudio();
                _topologyAudioPresentationController.PlayPlannedAudio();
                _gravityFieldAudioPresentationController.PlayPlannedAudio();
                _arbitratingGameplayAudioPlaybackPort?.FlushBatch();
            }
            catch
            {
                _arbitratingGameplayAudioPlaybackPort?.CancelBatch();
                throw;
            }
            _enemyChargeLoopAudioPresentationController.RefreshSignals(result.PresentationData.EnemyChargeSignals);
            TraceStep("ApplyEntityExitOwnership");
            _exitPresentationController.ApplyEntityExitOwnership();
            _summonedEnemyPresentationResolver.CleanupOwnedViews(result.FinalEntities);
            UpdatePresentation(0f);
            _moonBlockEmergencePresentationController.StartReadyRequests(result.TickIndex);
        }

        public void PresentInitial(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            InitialPresentationData presentationData = null)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();

            _gravityFieldVisualPresentationController.ClearTrackedContinuousStates();
            _audioPresentationController.ResetSession();
            _actionAudioPresentationController.ResetSession();
            _enemyAudioPresentationController.ResetSession();
            _enemyChargeLoopAudioPresentationController.ResetSession();
            _blockAudioPresentationController.ResetSession();
            _playerLocomotionAudioPresentationController.ResetSession();
            _tileFeatureAudioPresentationController.ResetSession();
            _topologyAudioPresentationController.ResetSession();
            _gravityFieldAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _trackState.ResetSession();
            _exitPresentationController.Reset();
            _frontFaceShieldVfxPresenter.Clear();
            _utilityWindupVfxPresenter.Clear();
            _tileFeatureVisualPresentationController.ResetSession();
            _moonBlockEmergencePresentationController.ResetSession();
            ResetExtensions();
            _animationSync.Reset();
            _stateStore.ResetSession(topology);
            _currentTilePresentationRequests = EmptyTilePresentationRequests;
            _currentGravityFieldPresentationRequests = EmptyGravityFieldPresentationRequests;
            _currentGravityFieldVisualStates = EmptyGravityFieldVisualStates;
            _currentTileFeatureVisualStates = EmptyTileFeatureVisualStates;
            _topologyTransitionController.Reset();
            _lastPresentedResult = null;
            _topologyTransitionEpoch = 0;

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
            PresentInitialExtensions(presentationData ?? InitialPresentationData.Empty);
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
            _audioPresentationController.Update(deltaTime);
            _enemyAudioPresentationController.Update(_lastPresentedTickIndex, deltaTime);
            _blockAudioPresentationController.Update(deltaTime);
            _playerLocomotionAudioPresentationController.Update(deltaTime);
            _tileFeatureAudioPresentationController.Update(deltaTime);
            _frontFaceShieldVfxPresenter.Update(deltaTime);
            _tileFeatureVisualPresentationController.Update(deltaTime);
            _moonBlockEmergencePresentationController.UpdatePresentation(deltaTime);
            _gravityFieldVisualPresentationController.UpdatePresentation(deltaTime);
            UpdateExtensions(deltaTime);
            _entityPresentationApplier.Apply(
                deltaTime,
                _topologyTransitionController.HasActiveBoardRotationTween,
                _viewBinder,
                _timingProfile);
            _exitPresentationController.CompleteDeferredEntityExits();
            RefreshPresentationMotionVfx(_lastPresentedTickIndex);
            _exitPresentationController.AdvanceDeathPresentationCleanups(deltaTime);
            _exitPresentationController.AdvanceContactDelayedEntityExits(deltaTime);
            RefreshPresentationMotionVfx(_lastPresentedTickIndex);
        }

        internal void AttachGameplayAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            var arbitratingPort = GetOrCreateArbitratingPlaybackPort(playbackPort);
            _audioPresentationController.AttachRuntime(arbitratingPort, gameplayAudioMap);
            _actionAudioPresentationController.AttachRuntime(arbitratingPort);
            _enemyAudioPresentationController.AttachRuntime(arbitratingPort);
            if (playbackPort is IGameplayAudioLoopPlaybackPort loopPlaybackPort)
            {
                _enemyChargeLoopAudioPresentationController.AttachRuntime(loopPlaybackPort);
            }
            else
            {
                _enemyChargeLoopAudioPresentationController.DetachRuntime();
            }
        }

        internal void AttachTileFeatureAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TileFeatureAudioMap tileFeatureAudioMap)
        {
            _tileFeatureAudioPresentationController.AttachRuntime(
                GetOrCreateArbitratingPlaybackPort(playbackPort),
                tileFeatureAudioMap);
        }

        internal void AttachTopologyAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TopologyAudioMap topologyAudioMap)
        {
            _topologyAudioPresentationController.AttachRuntime(
                GetOrCreateArbitratingPlaybackPort(playbackPort),
                topologyAudioMap);
        }

        internal void AttachGravityFieldAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GravityFieldAudioMap gravityFieldAudioMap)
        {
            _gravityFieldAudioPresentationController.AttachRuntime(
                GetOrCreateArbitratingPlaybackPort(playbackPort),
                gravityFieldAudioMap);
        }

        internal void AttachBlockAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            BlockAudioMap blockAudioMap)
        {
            _blockAudioPresentationController.AttachRuntime(GetOrCreateArbitratingPlaybackPort(playbackPort), blockAudioMap);
        }

        internal void AttachPlayerLocomotionAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            PlayerLocomotionAudioMap playerLocomotionAudioMap)
        {
            _playerLocomotionAudioPresentationController.AttachRuntime(
                GetOrCreateArbitratingPlaybackPort(playbackPort),
                playerLocomotionAudioMap);
        }

        internal void AttachTileFeatureVisualRegistry(ITileFeatureVisualRegistry registry)
        {
            _tileFeatureVisualPresentationController.AttachRegistry(registry);
        }

        internal void AttachTileFeatureVisualPoseSynchronizer(TileFeatureVisualPoseSynchronizer synchronizer)
        {
            _tileFeatureVisualPoseSynchronizer = synchronizer;
        }

        internal void DetachGameplayAudioRuntime()
        {
            _actionAudioPresentationController.DetachRuntime();
            _enemyAudioPresentationController.DetachRuntime();
            _enemyChargeLoopAudioPresentationController.DetachRuntime();
            _audioPresentationController.DetachRuntime();
        }

        internal void DetachTileFeatureAudioRuntime()
        {
            _tileFeatureAudioPresentationController.DetachRuntime();
        }

        internal void DetachTopologyAudioRuntime()
        {
            _topologyAudioPresentationController.DetachRuntime();
        }

        internal void DetachGravityFieldAudioRuntime()
        {
            _gravityFieldAudioPresentationController.DetachRuntime();
        }

        internal void DetachBlockAudioRuntime()
        {
            _blockAudioPresentationController.DetachRuntime();
        }

        internal void DetachPlayerLocomotionAudioRuntime()
        {
            _playerLocomotionAudioPresentationController.DetachRuntime();
        }

        private GameplaySfxArbitratingPlaybackPort GetOrCreateArbitratingPlaybackPort(
            IGameplayAudioPlaybackPort playbackPort)
        {
            if (playbackPort == null)
            {
                throw new ArgumentNullException(nameof(playbackPort));
            }

            if (_arbitratingGameplayAudioPlaybackPort != null &&
                ReferenceEquals(_rawGameplayAudioPlaybackPort, playbackPort))
            {
                return _arbitratingGameplayAudioPlaybackPort;
            }

            _rawGameplayAudioPlaybackPort = playbackPort;
            _arbitratingGameplayAudioPlaybackPort = new GameplaySfxArbitratingPlaybackPort(
                playbackPort,
                _gameplaySfxArbiter);
            return _arbitratingGameplayAudioPlaybackPort;
        }

        internal void DebugRefreshGameplayAudioPlan(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _audioPresentationController.ReplacePendingPlan(_audioRequestPlanner.BuildRequests(result, _timingProfile));
            _actionAudioPresentationController.ReplacePendingPlan(_actionAudioRequestPlanner.BuildRequests(result));
            _enemyAudioPresentationController.ReplacePendingPlan(_enemyAudioRequestPlanner.BuildRequests(result, _timingProfile));
        }

        internal void SetTraceSink(Action<string> traceSink)
        {
            _traceSink = traceSink;
        }

        internal void SetTileFeatureVisualDiagnosticSink(Action<string> diagnosticSink)
        {
            _tileFeatureVisualPresentationController.SetDiagnosticSink(diagnosticSink);
        }

        internal void SetGravityFieldVisualDiagnosticSink(Action<string> diagnosticSink)
        {
            _gravityFieldVisualPresentationController.SetDiagnosticSink(diagnosticSink);
        }

        private void RefreshTilePresentationRequests(TickPresentationData presentationData)
        {
            var plannedRequests = _tilePresentationRequestPlanner.BuildRequests(presentationData);
            _currentTilePresentationRequests = plannedRequests.Count == 0
                ? EmptyTilePresentationRequests
                : new ReadOnlyCollection<TilePresentationRequest>(new List<TilePresentationRequest>(plannedRequests));
        }

        private void RefreshGravityFieldPresentationRequests(TickPresentationData presentationData)
        {
            var plannedRequests = _gravityFieldPresentationRequestPlanner.BuildRequests(presentationData);
            _currentGravityFieldPresentationRequests = plannedRequests.Count == 0
                ? EmptyGravityFieldPresentationRequests
                : new ReadOnlyCollection<GravityFieldPresentationRequest>(
                    new List<GravityFieldPresentationRequest>(plannedRequests));
        }

        private void RefreshTileFeatureVisualStates(TickPresentationData presentationData)
        {
            var visualStates = presentationData.TileFeatureVisualStates;
            _currentTileFeatureVisualStates = visualStates.Count == 0
                ? EmptyTileFeatureVisualStates
                : new ReadOnlyCollection<TileFeatureVisualState>(
                    new List<TileFeatureVisualState>(visualStates));
        }

        private void RefreshGravityFieldVisualStates(TickPresentationData presentationData)
        {
            var visualStates = presentationData.GravityFieldVisualStates;
            _currentGravityFieldVisualStates = visualStates.Count == 0
                ? EmptyGravityFieldVisualStates
                : new ReadOnlyCollection<GravityFieldVisualState>(
                    new List<GravityFieldVisualState>(visualStates));
        }

        private void SyncTileFeatureVisualPoseForTopologyMotionIfNeeded(TickPresentationData presentationData)
        {
            if (_tileFeatureVisualPoseSynchronizer == null ||
                !presentationData.TopologyMotion.HasValue ||
                presentationData.TopologyMotion.Value.RotationKind == CubeRotationKind.None)
            {
                return;
            }

            _tileFeatureVisualPoseSynchronizer.RefreshAll(
                presentationData.TopologyMotion.Value.DestinationTopology);
        }

        internal void HardCleanupPresentationExtensions()
        {
            _moonBlockEmergencePresentationController.Dispose();
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
                _timingProfile,
                _tileFeatureVfxStyleBindings,
                _topologyTransitionEpoch,
                isTopologyTransitionCompletionReconcile: false);
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.Present(context);
            }
        }

        private void PresentInitialExtensions(InitialPresentationData presentationData)
        {
            if (_presentationExtensions.Count == 0)
            {
                return;
            }

            var context = new GameplayInitialPresentationExtensionContext(
                presentationData,
                _stateStore.CommittedTopology,
                _stateStore,
                _projector,
                _enemyPresentationCatalog,
                _enemyPresentationBindings,
                _timingProfile,
                _tileFeatureVfxStyleBindings);
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayInitialPresentationExtension extension)
                {
                    extension.PresentInitial(context);
                }
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

        private void HandleTopologyPresentationCompleted(CubeTopologyState topology)
        {
            _tileFeatureVisualPoseSynchronizer?.RefreshAll(topology);
        }

        private void HandleTopologyTransitionPresentationCompleted(CubeTopologyState topology)
        {
            _tileFeatureVisualPoseSynchronizer?.RefreshAll(topology);
            if (_lastPresentedResult == null ||
                _presentationExtensions.Count == 0)
            {
                return;
            }

            var context = new GameplayTickPresentationExtensionContext(
                _lastPresentedResult,
                topology,
                _stateStore,
                _projector,
                _enemyPresentationCatalog,
                _enemyPresentationBindings,
                _timingProfile,
                _tileFeatureVfxStyleBindings,
                _topologyTransitionEpoch,
                isTopologyTransitionCompletionReconcile: true);
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayTopologyTransitionCompletionPresentationExtension extension)
                {
                    extension.ReconcileTopologyTransitionCompleted(context);
                }
            }
        }

        private static bool IsTopologyTransitionPresentation(TickTopologyMotion? topologyMotion)
        {
            return topologyMotion.HasValue &&
                   topologyMotion.Value.RotationKind != CubeRotationKind.None;
        }

        private void RefreshPresentationMotionVfx(int tickIndex)
        {
            if (_presentationExtensions.Count == 0)
            {
                return;
            }

            var context = new GameplayPresentationMotionVfxContext(
                tickIndex,
                _trackState,
                _stateStore,
                _projector,
                _timingProfile);
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayPresentationMotionVfxExtension motionVfxExtension)
                {
                    motionVfxExtension.RefreshPresentationMotionVfx(context);
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

    internal readonly struct GameplayEntityPresentationLifecycleDebugSnapshot
    {
        public GameplayEntityPresentationLifecycleDebugSnapshot(
            float timelineTimeSeconds,
            int entityId,
            string gameObjectName,
            int instanceId,
            bool gameObjectActiveSelf,
            bool hasEnemyAnimatorDriver,
            bool hasEntityViewComponent,
            bool viewsByEntityIdContainsEntityId,
            bool contactDelayedRetainedEntityIdsContainsEntityId,
            bool deathPresentationPlayingEntityIdsContainsEntityId,
            bool retainedLocalTargetPosesContainsEntityId,
            bool pendingContactExitContainsEntityId,
            float pendingContactExitRemainingSeconds,
            bool pendingDeathCleanupContainsEntityId,
            float pendingDeathCleanupRemainingSeconds,
            bool isVfxPooledInstance,
            bool rendererEnabled,
            bool rendererActiveInHierarchy,
            int animatorCurrentStateShortNameHash,
            float animatorNormalizedTime,
            int deathTriggerCount)
        {
            TimelineTimeSeconds = timelineTimeSeconds;
            EntityId = entityId;
            GameObjectName = gameObjectName ?? string.Empty;
            InstanceId = instanceId;
            GameObjectActiveSelf = gameObjectActiveSelf;
            HasEnemyAnimatorDriver = hasEnemyAnimatorDriver;
            HasEntityViewComponent = hasEntityViewComponent;
            ViewsByEntityIdContainsEntityId = viewsByEntityIdContainsEntityId;
            ContactDelayedRetainedEntityIdsContainsEntityId = contactDelayedRetainedEntityIdsContainsEntityId;
            DeathPresentationPlayingEntityIdsContainsEntityId = deathPresentationPlayingEntityIdsContainsEntityId;
            RetainedLocalTargetPosesContainsEntityId = retainedLocalTargetPosesContainsEntityId;
            PendingContactExitContainsEntityId = pendingContactExitContainsEntityId;
            PendingContactExitRemainingSeconds = pendingContactExitRemainingSeconds;
            PendingDeathCleanupContainsEntityId = pendingDeathCleanupContainsEntityId;
            PendingDeathCleanupRemainingSeconds = pendingDeathCleanupRemainingSeconds;
            IsVfxPooledInstance = isVfxPooledInstance;
            RendererEnabled = rendererEnabled;
            RendererActiveInHierarchy = rendererActiveInHierarchy;
            AnimatorCurrentStateShortNameHash = animatorCurrentStateShortNameHash;
            AnimatorNormalizedTime = animatorNormalizedTime;
            DeathTriggerCount = deathTriggerCount;
        }

        public float TimelineTimeSeconds { get; }

        public int EntityId { get; }

        public string GameObjectName { get; }

        public int InstanceId { get; }

        public bool GameObjectActiveSelf { get; }

        public bool HasEnemyAnimatorDriver { get; }

        public bool HasEntityViewComponent { get; }

        public bool ViewsByEntityIdContainsEntityId { get; }

        public bool ContactDelayedRetainedEntityIdsContainsEntityId { get; }

        public bool DeathPresentationPlayingEntityIdsContainsEntityId { get; }

        public bool RetainedLocalTargetPosesContainsEntityId { get; }

        public bool PendingContactExitContainsEntityId { get; }

        public float PendingContactExitRemainingSeconds { get; }

        public bool PendingDeathCleanupContainsEntityId { get; }

        public float PendingDeathCleanupRemainingSeconds { get; }

        public bool IsVfxPooledInstance { get; }

        public bool RendererEnabled { get; }

        public bool RendererActiveInHierarchy { get; }

        public int AnimatorCurrentStateShortNameHash { get; }

        public float AnimatorNormalizedTime { get; }

        public int DeathTriggerCount { get; }
    }
}
