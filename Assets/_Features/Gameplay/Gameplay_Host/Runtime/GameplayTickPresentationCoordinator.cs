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
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
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
        private static readonly IReadOnlyList<TickEnemyGravityFieldAuraVisualState> EmptyEnemyGravityFieldAuraVisualStates =
            Array.Empty<TickEnemyGravityFieldAuraVisualState>();
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
        private readonly MoonBlockDestructionPresentationController _moonBlockDestructionPresentationController;
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
        private readonly GameplayPresentationPauseRegistry _presentationPauseRegistry = new();
        private readonly GameplayUtilityWindupVfxPresenter _utilityWindupVfxPresenter = new();
        private readonly TileFeatureVisualPresentationController _tileFeatureVisualPresentationController = new();
        private readonly MoonBlockEmergencePresentationController _moonBlockEmergencePresentationController = new();
        private readonly GameplayMotionTimingResolver _motionTimingResolver;
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplaySfxArbiter _gameplaySfxArbiter = new();
        private readonly List<IGameplayTickPresentationExtension> _presentationExtensions = new();
        private readonly TopologyPresentationExecutionGuard _topologyExecutionGuard = new();
        private readonly TopologyExecutionPipelineFactory _topologyExecutionPipelineFactory;
        private readonly DamageDeathVfxExecutionGuard _damageDeathVfxExecutionGuard = new();
        private readonly DamageDeathVfxExecutionPipelineFactory _damageDeathVfxExecutionPipelineFactory;
        private readonly BoxMotionExecutionGuard _boxMotionExecutionGuard = new();
        private readonly BoxMotionExecutionPipelineFactory _boxMotionExecutionPipelineFactory;
        private readonly PlayerActionAnimationExecutionGuard _playerActionAnimationExecutionGuard = new();
        private readonly PlayerActionAnimationExecutionPipelineFactory _playerActionAnimationExecutionPipelineFactory;
        private readonly EnemyPresentationExecutionGuard _enemyPresentationExecutionGuard = new();
        private readonly EnemyPresentationExecutionPipelineFactory _enemyPresentationExecutionPipelineFactory;
        private readonly CoreGameplaySfxExecutionGuard _coreGameplaySfxExecutionGuard = new();
        private readonly CoreGameplaySfxExecutionPipelineFactory _coreGameplaySfxExecutionPipelineFactory;
        private readonly ActionAudioExecutionGuard _actionAudioExecutionGuard = new();
        private readonly ActionAudioExecutionPipelineFactory _actionAudioExecutionPipelineFactory;
        private readonly EnemyAudioExecutionGuard _enemyAudioExecutionGuard = new();
        private readonly EnemyAudioExecutionPipelineFactory _enemyAudioExecutionPipelineFactory;

        private GameplayPresentationPipeline _presentationPipeline;
        private GameplayPresentationPipeline _topologyExecutionPipeline;
        private GameplayPresentationPipeline _damageDeathVfxExecutionPipeline;
        private GameplayPresentationPipeline _boxMotionExecutionPipeline;
        private GameplayPresentationPipeline _playerActionAnimationExecutionPipeline;
        private GameplayPresentationPipeline _enemyPresentationExecutionPipeline;
        private GameplayPresentationPipeline _coreGameplaySfxExecutionPipeline;
        private GameplayPresentationPipeline _actionAudioExecutionPipeline;
        private GameplayPresentationPipeline _enemyAudioExecutionPipeline;
        private TopologyPresentationExecutionMode _topologyExecutionMode =
            TopologyPresentationExecutionMode.LegacyCoordinator;
        private DamageDeathVfxExecutionMode _damageDeathVfxExecutionMode =
            DamageDeathVfxExecutionMode.LegacyExtension;
        private BoxMotionPresentationExecutionMode _boxMotionExecutionMode =
            BoxMotionPresentationExecutionMode.LegacyTrackPlanner;
        private PlayerActionAnimationExecutionMode _playerActionAnimationExecutionMode =
            PlayerActionAnimationExecutionMode.LegacyAnimationSync;
        private EnemyPresentationExecutionMode _enemyPresentationExecutionMode =
            EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper;
        private CoreGameplaySfxExecutionMode _coreGameplaySfxExecutionMode =
            CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor;
        private ActionAudioExecutionMode _actionAudioExecutionMode =
            ActionAudioExecutionMode.LegacyActionAudioController;
        private EnemyAudioExecutionMode _enemyAudioExecutionMode =
            EnemyAudioExecutionMode.LegacyEnemyAudioController;
        private IDamageDeathVfxPlaybackPort _damageDeathVfxPlaybackPort;
        private IGameplayMotionPlaybackPort _boxMotionPlaybackPort;
        private GameplayMotionTrackPlannerPlaybackPort _boxMotionTrackPlannerPlaybackPort;
        private IGameplayAnimationPlaybackPort _playerActionAnimationPlaybackPort;
        private GameplayAnimationSyncPlaybackPort _playerActionAnimationSyncPlaybackPort;
        private IGameplayEnemyPresentationPlaybackPort _enemyPresentationPlaybackPort;
        private GameplayEnemyPresentationSyncPlaybackPort _enemyPresentationSyncPlaybackPort;
        private IGameplaySfxPlaybackPort _coreGameplaySfxPlaybackPort;
        private GameplaySfxPlaybackPortAdapter _coreGameplaySfxPlaybackPortAdapter;
        private IGameplayActionAudioPlaybackPort _actionAudioPlaybackPort;
        private GameplayActionAudioPlaybackPortAdapter _actionAudioPlaybackPortAdapter;
        private IGameplayEnemyAudioPlaybackPort _enemyAudioPlaybackPort;
        private GameplayEnemyAudioPlaybackPortAdapter _enemyAudioPlaybackPortAdapter;
        private bool _isInitialized;
        private bool _presentationPipelineDiagnosticsEnabled;
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
        private IReadOnlyList<TickEnemyGravityFieldAuraVisualState> _currentEnemyGravityFieldAuraVisualStates =
            EmptyEnemyGravityFieldAuraVisualStates;
        private IReadOnlyList<TileFeatureVisualState> _currentTileFeatureVisualStates =
            EmptyTileFeatureVisualStates;
        private Action<string> _traceSink;
        private int _lastPresentedTickIndex;
        private TileFeatureVisualPoseSynchronizer _tileFeatureVisualPoseSynchronizer;
        private IGameplayAudioPlaybackPort _rawGameplayAudioPlaybackPort;
        private GameplaySfxArbitratingPlaybackPort _arbitratingGameplayAudioPlaybackPort;
        private TickResult _lastPresentedResult;
        private int _topologyTransitionEpoch;
        private bool _isPresentationPaused;

        public GameplayTickPresentationCoordinator()
            : this(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreatePlayerActionAnimationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyPresentationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateCoreGameplaySfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateActionAudioExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyAudioExecutionPipeline)
        {
        }

        internal GameplayTickPresentationCoordinator(TopologyExecutionPipelineFactory topologyExecutionPipelineFactory)
            : this(
                topologyExecutionPipelineFactory,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreatePlayerActionAnimationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyPresentationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateCoreGameplaySfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateActionAudioExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyAudioExecutionPipeline)
        {
        }

        internal GameplayTickPresentationCoordinator(
            TopologyExecutionPipelineFactory topologyExecutionPipelineFactory,
            DamageDeathVfxExecutionPipelineFactory damageDeathVfxExecutionPipelineFactory)
            : this(
                topologyExecutionPipelineFactory,
                damageDeathVfxExecutionPipelineFactory,
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreatePlayerActionAnimationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyPresentationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateCoreGameplaySfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateActionAudioExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyAudioExecutionPipeline)
        {
        }

        internal GameplayTickPresentationCoordinator(
            TopologyExecutionPipelineFactory topologyExecutionPipelineFactory,
            DamageDeathVfxExecutionPipelineFactory damageDeathVfxExecutionPipelineFactory,
            BoxMotionExecutionPipelineFactory boxMotionExecutionPipelineFactory,
            PlayerActionAnimationExecutionPipelineFactory playerActionAnimationExecutionPipelineFactory = null,
            EnemyPresentationExecutionPipelineFactory enemyPresentationExecutionPipelineFactory = null,
            CoreGameplaySfxExecutionPipelineFactory coreGameplaySfxExecutionPipelineFactory = null,
            ActionAudioExecutionPipelineFactory actionAudioExecutionPipelineFactory = null,
            EnemyAudioExecutionPipelineFactory enemyAudioExecutionPipelineFactory = null)
        {
            _topologyExecutionPipelineFactory = topologyExecutionPipelineFactory ??
                                                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline;
            _damageDeathVfxExecutionPipelineFactory = damageDeathVfxExecutionPipelineFactory ??
                                                      GameplayHostPresentationPipelineFactory
                                                          .CreateDamageDeathVfxExecutionPipeline;
            _boxMotionExecutionPipelineFactory = boxMotionExecutionPipelineFactory ??
                                                 GameplayHostPresentationPipelineFactory
                                                     .CreateBoxMotionExecutionPipeline;
            _playerActionAnimationExecutionPipelineFactory = playerActionAnimationExecutionPipelineFactory ??
                                                             GameplayHostPresentationPipelineFactory
                                                                 .CreatePlayerActionAnimationExecutionPipeline;
            _enemyPresentationExecutionPipelineFactory = enemyPresentationExecutionPipelineFactory ??
                                                         GameplayHostPresentationPipelineFactory
                                                             .CreateEnemyPresentationExecutionPipeline;
            _coreGameplaySfxExecutionPipelineFactory = coreGameplaySfxExecutionPipelineFactory ??
                                                       GameplayHostPresentationPipelineFactory
                                                           .CreateCoreGameplaySfxExecutionPipeline;
            _actionAudioExecutionPipelineFactory = actionAudioExecutionPipelineFactory ??
                                                   GameplayHostPresentationPipelineFactory
                                                       .CreateActionAudioExecutionPipeline;
            _enemyAudioExecutionPipelineFactory = enemyAudioExecutionPipelineFactory ??
                                                  GameplayHostPresentationPipelineFactory
                                                      .CreateEnemyAudioExecutionPipeline;
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
            _moonBlockDestructionPresentationController = new MoonBlockDestructionPresentationController(
                _stateStore,
                _trackState,
                _exitPresentationController,
                ResolveDestroyShrinkVfxSequenceState);
            _exitPresentationController.SetDeferredExitCleanupHoldPredicate(
                _moonBlockDestructionPresentationController.ShouldHoldDeferredExitCleanup);
            _exitPresentationController.SetLiveExitOwnershipBypassPredicate(
                _moonBlockDestructionPresentationController.ShouldBypassLiveExitOwnership);
            _planner = new GameplayTrackPlanner(
                _stateStore,
                _trackState,
                _motionTimingResolver,
                _poseResolver,
                _exitPresentationController,
                _moonBlockDestructionPresentationController,
                _entityPresentationApplier);
            _boxMotionTrackPlannerPlaybackPort = new GameplayMotionTrackPlannerPlaybackPort(_planner, _stateStore);
            _playerActionAnimationSyncPlaybackPort =
                new GameplayAnimationSyncPlaybackPort(_animationSync, _stateStore);
            _enemyPresentationSyncPlaybackPort =
                new GameplayEnemyPresentationSyncPlaybackPort(_animationSync, _stateStore);
            _coreGameplaySfxPlaybackPortAdapter = new GameplaySfxPlaybackPortAdapter(_stateStore);
            _actionAudioPlaybackPortAdapter =
                new GameplayActionAudioPlaybackPortAdapter(_actionAudioPresentationController);
            _enemyAudioPlaybackPortAdapter =
                new GameplayEnemyAudioPlaybackPortAdapter(_enemyAudioPresentationController);
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

        public bool IsPresentationPaused => _isPresentationPaused;

        public bool IsPresentationActive => CurrentPresentationPhase != GameplayPresentationPhase.Idle;

        public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;

        public float LastStageClearPlayerPresentationDelaySeconds =>
            _animationSync.LastStageClearPlayerPresentationDelaySeconds;

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

        internal int DeferredGameplayAudioRequestCount =>
            _audioPresentationController.DeferredRequestCount +
            _coreGameplaySfxPlaybackPortAdapter.DeferredRequestCount +
            _actionAudioPresentationController.DeferredRequestCount +
            _enemyAudioPresentationController.DeferredRequestCount;

        internal int PendingMoonBlockEmergenceRequestCount =>
            _moonBlockEmergencePresentationController.PendingRequestCount;

        internal int ActiveMoonBlockDestructionGhostCount =>
            _moonBlockDestructionPresentationController.ActiveGhostCount;

        internal EntityPresentationApplyDiagnostics DebugLastEntityPresentationApplyDiagnostics =>
            _stateStore.LastEntityPresentationApplyDiagnostics;

        internal bool IsPresentationPipelineDiagnosticsEnabled => _presentationPipelineDiagnosticsEnabled;

        internal int PresentationPipelineNoOpSchedulerAcceptCount =>
            _presentationPipeline?.NoOpSchedulerAcceptCount ?? 0;

        internal PresentationBlockingSnapshot PresentationPipelineBlockingSnapshot =>
            _presentationPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal PresentationBlockingSnapshot TopologyExecutionPipelineBlockingSnapshot =>
            _topologyExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal TopologyPresentationExecutionMode TopologyPresentationExecutionMode => _topologyExecutionMode;

        internal TopologyPresentationOwnershipDiagnostics TopologyPresentationOwnershipDiagnostics =>
            _topologyExecutionGuard.Diagnostics;

        internal DamageDeathVfxExecutionMode DamageDeathVfxExecutionMode => _damageDeathVfxExecutionMode;

        internal DamageDeathVfxOwnershipDiagnostics DamageDeathVfxOwnershipDiagnostics =>
            _damageDeathVfxExecutionGuard.Diagnostics;

        internal BoxMotionPresentationExecutionMode BoxMotionPresentationExecutionMode => _boxMotionExecutionMode;

        internal BoxMotionOwnershipDiagnostics BoxMotionOwnershipDiagnostics =>
            _boxMotionExecutionGuard.Diagnostics;

        internal PlayerActionAnimationExecutionMode PlayerActionAnimationExecutionMode =>
            _playerActionAnimationExecutionMode;

        internal PlayerActionAnimationOwnershipDiagnostics PlayerActionAnimationOwnershipDiagnostics =>
            _playerActionAnimationExecutionGuard.Diagnostics;

        internal EnemyPresentationExecutionMode EnemyPresentationExecutionMode =>
            _enemyPresentationExecutionMode;

        internal EnemyPresentationOwnershipDiagnostics EnemyPresentationOwnershipDiagnostics =>
            _enemyPresentationExecutionGuard.Diagnostics;

        internal CoreGameplaySfxExecutionMode CoreGameplaySfxExecutionMode =>
            _coreGameplaySfxExecutionMode;

        internal CoreGameplaySfxOwnershipDiagnostics CoreGameplaySfxOwnershipDiagnostics =>
            _coreGameplaySfxExecutionGuard.Diagnostics;

        internal ActionAudioExecutionMode ActionAudioExecutionMode =>
            _actionAudioExecutionMode;

        internal ActionAudioOwnershipDiagnostics ActionAudioOwnershipDiagnostics =>
            _actionAudioExecutionGuard.Diagnostics;

        internal EnemyAudioExecutionMode EnemyAudioExecutionMode =>
            _enemyAudioExecutionMode;

        internal EnemyAudioOwnershipDiagnostics EnemyAudioOwnershipDiagnostics =>
            _enemyAudioExecutionGuard.Diagnostics;

        internal PresentationBlockingSnapshot DamageDeathVfxExecutionPipelineBlockingSnapshot =>
            _damageDeathVfxExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal PresentationBlockingSnapshot BoxMotionExecutionPipelineBlockingSnapshot =>
            _boxMotionExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal PresentationBlockingSnapshot PlayerActionAnimationExecutionPipelineBlockingSnapshot =>
            _playerActionAnimationExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal PresentationBlockingSnapshot EnemyPresentationExecutionPipelineBlockingSnapshot =>
            _enemyPresentationExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal PresentationBlockingSnapshot CoreGameplaySfxExecutionPipelineBlockingSnapshot =>
            _coreGameplaySfxExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal PresentationBlockingSnapshot ActionAudioExecutionPipelineBlockingSnapshot =>
            _actionAudioExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal PresentationBlockingSnapshot EnemyAudioExecutionPipelineBlockingSnapshot =>
            _enemyAudioExecutionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        internal GameplayVfxExecutorDiagnostics DamageDeathVfxExecutorDiagnostics =>
            ResolveDamageDeathVfxExecutorDiagnostics();

        internal GameplayMotionExecutorDiagnostics BoxMotionExecutorDiagnostics =>
            ResolveBoxMotionExecutorDiagnostics();

        internal GameplayAnimationExecutorDiagnostics PlayerActionAnimationExecutorDiagnostics =>
            ResolvePlayerActionAnimationExecutorDiagnostics();

        internal GameplayEnemyPresentationExecutorDiagnostics EnemyPresentationExecutorDiagnostics =>
            ResolveEnemyPresentationExecutorDiagnostics();

        internal GameplaySfxExecutorDiagnostics CoreGameplaySfxExecutorDiagnostics =>
            ResolveCoreGameplaySfxExecutorDiagnostics();

        internal GameplayActionAudioExecutorDiagnostics ActionAudioExecutorDiagnostics =>
            ResolveActionAudioExecutorDiagnostics();

        internal GameplayEnemyAudioExecutorDiagnostics EnemyAudioExecutorDiagnostics =>
            ResolveEnemyAudioExecutorDiagnostics();

        internal void ConfigureDamageDeathVfxExecution(
            DamageDeathVfxExecutionMode mode,
            IDamageDeathVfxPlaybackPort playbackPort = null)
        {
            _damageDeathVfxExecutionMode = NormalizeDamageDeathVfxExecutionMode(mode);
            _damageDeathVfxPlaybackPort = playbackPort;
            _damageDeathVfxExecutionGuard.Configure(_damageDeathVfxExecutionMode);
            _damageDeathVfxExecutionGuard.ResetSession();
            _damageDeathVfxExecutionPipeline = _damageDeathVfxExecutionPipelineFactory(
                _damageDeathVfxExecutionMode,
                _damageDeathVfxPlaybackPort,
                _damageDeathVfxExecutionGuard);
            _damageDeathVfxExecutionPipeline?.ResetSession();
        }

        internal void ConfigureBoxMotionPresentationExecution(
            BoxMotionPresentationExecutionMode mode,
            IGameplayMotionPlaybackPort playbackPort = null)
        {
            _boxMotionExecutionMode = NormalizeBoxMotionPresentationExecutionMode(mode);
            _boxMotionPlaybackPort = playbackPort;
            _boxMotionExecutionGuard.Configure(_boxMotionExecutionMode);
            _boxMotionExecutionGuard.ResetSession();
            _boxMotionExecutionPipeline = _boxMotionExecutionPipelineFactory(
                _boxMotionExecutionMode,
                ResolveBoxMotionPlaybackPort(),
                _boxMotionExecutionGuard);
            _boxMotionExecutionPipeline?.ResetSession();
        }

        internal void ConfigurePlayerActionAnimationExecution(
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort = null)
        {
            _playerActionAnimationExecutionMode = NormalizePlayerActionAnimationExecutionMode(mode);
            _playerActionAnimationPlaybackPort = playbackPort;
            _playerActionAnimationExecutionGuard.Configure(_playerActionAnimationExecutionMode);
            _playerActionAnimationExecutionGuard.ResetSession();
            _playerActionAnimationExecutionPipeline = _playerActionAnimationExecutionPipelineFactory(
                _playerActionAnimationExecutionMode,
                ResolvePlayerActionAnimationPlaybackPort(),
                _playerActionAnimationExecutionGuard);
            _enemyPresentationExecutionMode =
                NormalizeEnemyPresentationExecutionMode(_enemyPresentationExecutionMode);
            _enemyPresentationExecutionGuard.Configure(_enemyPresentationExecutionMode);
            _enemyPresentationExecutionGuard.ResetSession();
            _enemyPresentationExecutionPipeline = _enemyPresentationExecutionPipelineFactory(
                _enemyPresentationExecutionMode,
                ResolveEnemyPresentationPlaybackPort(),
                _enemyPresentationExecutionGuard);
            _coreGameplaySfxExecutionMode =
                NormalizeCoreGameplaySfxExecutionMode(_coreGameplaySfxExecutionMode);
            _coreGameplaySfxExecutionGuard.Configure(_coreGameplaySfxExecutionMode);
            _coreGameplaySfxExecutionGuard.ResetSession();
            _coreGameplaySfxExecutionPipeline = _coreGameplaySfxExecutionPipelineFactory(
                _coreGameplaySfxExecutionMode,
                ResolveCoreGameplaySfxPlaybackPort(),
                _coreGameplaySfxExecutionGuard);
            _playerActionAnimationExecutionPipeline?.ResetSession();
        }

        internal void ConfigureEnemyPresentationExecution(
            EnemyPresentationExecutionMode mode,
            IGameplayEnemyPresentationPlaybackPort playbackPort = null)
        {
            _enemyPresentationExecutionMode = NormalizeEnemyPresentationExecutionMode(mode);
            _enemyPresentationPlaybackPort = playbackPort;
            _enemyPresentationExecutionGuard.Configure(_enemyPresentationExecutionMode);
            _enemyPresentationExecutionGuard.ResetSession();
            _enemyPresentationExecutionPipeline = _enemyPresentationExecutionPipelineFactory(
                _enemyPresentationExecutionMode,
                ResolveEnemyPresentationPlaybackPort(),
                _enemyPresentationExecutionGuard);
            _enemyPresentationExecutionPipeline?.ResetSession();
        }

        internal void ConfigureCoreGameplaySfxExecution(
            CoreGameplaySfxExecutionMode mode,
            IGameplaySfxPlaybackPort playbackPort = null)
        {
            _coreGameplaySfxExecutionMode = NormalizeCoreGameplaySfxExecutionMode(mode);
            _coreGameplaySfxPlaybackPort = playbackPort;
            _coreGameplaySfxExecutionGuard.Configure(_coreGameplaySfxExecutionMode);
            _coreGameplaySfxExecutionGuard.ResetSession();
            _coreGameplaySfxExecutionPipeline = _coreGameplaySfxExecutionPipelineFactory(
                _coreGameplaySfxExecutionMode,
                ResolveCoreGameplaySfxPlaybackPort(),
                _coreGameplaySfxExecutionGuard);
            _coreGameplaySfxExecutionPipeline?.ResetSession();
        }

        internal void ConfigureActionAudioExecution(
            ActionAudioExecutionMode mode,
            IGameplayActionAudioPlaybackPort playbackPort = null)
        {
            _actionAudioExecutionMode = NormalizeActionAudioExecutionMode(mode);
            _actionAudioPlaybackPort = playbackPort;
            _actionAudioExecutionGuard.Configure(_actionAudioExecutionMode);
            _actionAudioExecutionGuard.ResetSession();
            _actionAudioExecutionPipeline = _actionAudioExecutionPipelineFactory(
                _actionAudioExecutionMode,
                ResolveActionAudioPlaybackPort(),
                _actionAudioExecutionGuard);
            _actionAudioExecutionPipeline?.ResetSession();
        }

        internal void ConfigureEnemyAudioExecution(
            EnemyAudioExecutionMode mode,
            IGameplayEnemyAudioPlaybackPort playbackPort = null)
        {
            _enemyAudioExecutionMode = NormalizeEnemyAudioExecutionMode(mode);
            _enemyAudioPlaybackPort = playbackPort;
            _enemyAudioExecutionGuard.Configure(_enemyAudioExecutionMode);
            _enemyAudioExecutionGuard.ResetSession();
            _enemyAudioExecutionPipeline = _enemyAudioExecutionPipelineFactory(
                _enemyAudioExecutionMode,
                ResolveEnemyAudioPlaybackPort(),
                _enemyAudioExecutionGuard);
            _enemyAudioExecutionPipeline?.ResetSession();
        }

        internal void EnablePresentationPipelineDiagnostics(GameplayPresentationPipeline pipeline = null)
        {
            _presentationPipeline = pipeline ?? GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();
            _presentationPipeline.ResetSession();
            _presentationPipelineDiagnosticsEnabled = true;
        }

        internal void DisablePresentationPipelineDiagnostics()
        {
            _presentationPipelineDiagnosticsEnabled = false;
            _presentationPipeline?.ResetSession();
        }

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
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null,
            EnemyInactiveVisualSettings enemyInactiveVisualSettings = null,
            TopologyPresentationExecutionMode topologyPresentationExecutionMode =
                TopologyPresentationExecutionMode.LegacyCoordinator)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _topologyExecutionMode = NormalizeTopologyPresentationExecutionMode(topologyPresentationExecutionMode);
            _topologyExecutionGuard.Configure(_topologyExecutionMode);
            _topologyExecutionGuard.ResetSession();
            _topologyExecutionPipeline = _topologyExecutionPipelineFactory(
                _topologyExecutionMode,
                _topologyTransitionController,
                _topologyExecutionGuard);
            _damageDeathVfxExecutionMode = NormalizeDamageDeathVfxExecutionMode(_damageDeathVfxExecutionMode);
            _damageDeathVfxExecutionGuard.Configure(_damageDeathVfxExecutionMode);
            _damageDeathVfxExecutionGuard.ResetSession();
            _damageDeathVfxExecutionPipeline = _damageDeathVfxExecutionPipelineFactory(
                _damageDeathVfxExecutionMode,
                _damageDeathVfxPlaybackPort,
                _damageDeathVfxExecutionGuard);
            _boxMotionExecutionMode = NormalizeBoxMotionPresentationExecutionMode(_boxMotionExecutionMode);
            _boxMotionExecutionGuard.Configure(_boxMotionExecutionMode);
            _boxMotionExecutionGuard.ResetSession();
            _boxMotionExecutionPipeline = _boxMotionExecutionPipelineFactory(
                _boxMotionExecutionMode,
                ResolveBoxMotionPlaybackPort(),
                _boxMotionExecutionGuard);
            _playerActionAnimationExecutionMode =
                NormalizePlayerActionAnimationExecutionMode(_playerActionAnimationExecutionMode);
            _playerActionAnimationExecutionGuard.Configure(_playerActionAnimationExecutionMode);
            _playerActionAnimationExecutionGuard.ResetSession();
            _playerActionAnimationExecutionPipeline = _playerActionAnimationExecutionPipelineFactory(
                _playerActionAnimationExecutionMode,
                ResolvePlayerActionAnimationPlaybackPort(),
                _playerActionAnimationExecutionGuard);
            _enemyPresentationExecutionMode =
                NormalizeEnemyPresentationExecutionMode(_enemyPresentationExecutionMode);
            _enemyPresentationExecutionGuard.Configure(_enemyPresentationExecutionMode);
            _enemyPresentationExecutionGuard.ResetSession();
            _enemyPresentationExecutionPipeline = _enemyPresentationExecutionPipelineFactory(
                _enemyPresentationExecutionMode,
                ResolveEnemyPresentationPlaybackPort(),
                _enemyPresentationExecutionGuard);
            _coreGameplaySfxExecutionMode =
                NormalizeCoreGameplaySfxExecutionMode(_coreGameplaySfxExecutionMode);
            _coreGameplaySfxExecutionGuard.Configure(_coreGameplaySfxExecutionMode);
            _coreGameplaySfxExecutionGuard.ResetSession();
            _coreGameplaySfxExecutionPipeline = _coreGameplaySfxExecutionPipelineFactory(
                _coreGameplaySfxExecutionMode,
                ResolveCoreGameplaySfxPlaybackPort(),
                _coreGameplaySfxExecutionGuard);
            _actionAudioExecutionMode = NormalizeActionAudioExecutionMode(_actionAudioExecutionMode);
            _actionAudioExecutionGuard.Configure(_actionAudioExecutionMode);
            _actionAudioExecutionGuard.ResetSession();
            _actionAudioExecutionPipeline = _actionAudioExecutionPipelineFactory(
                _actionAudioExecutionMode,
                ResolveActionAudioPlaybackPort(),
                _actionAudioExecutionGuard);
            _enemyAudioExecutionMode = NormalizeEnemyAudioExecutionMode(_enemyAudioExecutionMode);
            _enemyAudioExecutionGuard.Configure(_enemyAudioExecutionMode);
            _enemyAudioExecutionGuard.ResetSession();
            _enemyAudioExecutionPipeline = _enemyAudioExecutionPipelineFactory(
                _enemyAudioExecutionMode,
                ResolveEnemyAudioPlaybackPort(),
                _enemyAudioExecutionGuard);
            _viewBinder = viewBinder;
            _gravityFieldVisualPresentationController.AttachTargetViewRegistry(_viewBinder.ViewRegistry);
            _moonBlockEmergencePresentationController.Configure(_viewBinder.ViewRegistry, timingProfile);
            _enemyPresentationCatalog = enemyPresentationCatalog;
            _enemyPresentationBindings = enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
            _tileFeatureVfxStyleBindings = tileFeatureVfxStyleBindings ?? Array.Empty<TileFeatureVfxStyleBinding>();
            var resolvedFaceSeamGap = faceSeamGap >= 0f ? faceSeamGap : cellSize;
            _projector = new GameplayCubeProjector(boardBounds, cellSize, resolvedFaceSeamGap);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _enemyAudioPresentationController.ConfigureTiming(_timingProfile);
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
            _moonBlockDestructionPresentationController.ConfigureViewRegistry(viewBinder.ViewRegistry);
            _moonBlockDestructionPresentationController.ResetSession();
            _audioPresentationController.ResetSession();
            _actionAudioPresentationController.ResetSession();
            _enemyAudioPresentationController.ResetSession();
            SetGameplayAudioPlaybackGate(GameplayAudioPlaybackGateState.Open);
            _enemyChargeLoopAudioPresentationController.ResetSession();
            _blockAudioPresentationController.ResetSession();
            _playerLocomotionAudioPresentationController.ResetSession();
            _tileFeatureAudioPresentationController.ResetSession();
            _topologyAudioPresentationController.ResetSession();
            _gravityFieldAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _entityPresentationApplier.ResetEnemySemanticPresentationDriverCache();
            ClearBoxMotionPresentationRuntimeState();
            _trackState.ResetSession();
            _utilityWindupVfxPresenter.Initialize(viewBinder.SearchRoot);
            _animationSync.Reset();
            _stateStore.ResetSession(initialTopology);
            _currentTilePresentationRequests = EmptyTilePresentationRequests;
            _currentGravityFieldPresentationRequests = EmptyGravityFieldPresentationRequests;
            _currentGravityFieldVisualStates = EmptyGravityFieldVisualStates;
            _currentEnemyGravityFieldAuraVisualStates = EmptyEnemyGravityFieldAuraVisualStates;
            _currentTileFeatureVisualStates = EmptyTileFeatureVisualStates;
            _presentationPauseRegistry.Clear();
            _summonedEnemyPresentationResolver.Initialize(
                boardRoot != null ? boardRoot.EntityRoot : viewBinder.SearchRoot,
                viewBinder.ViewRegistry,
                _stateStore,
                _animationSync,
                enemyPresentationArchetypeRegistry,
                enemyInactiveVisualSettings);
            _moonBlockEmergencePresentationController.ResetSession();

            _isInitialized = true;
            _topologyExecutionPipeline?.ResetSession();
            _damageDeathVfxExecutionPipeline?.ResetSession();
            _boxMotionExecutionPipeline?.ResetSession();
            _playerActionAnimationExecutionPipeline?.ResetSession();
            _enemyPresentationExecutionPipeline?.ResetSession();
            _coreGameplaySfxExecutionGuard.ResetSession();
            _coreGameplaySfxExecutionPipeline?.ResetSession();
            _actionAudioExecutionGuard.ResetSession();
            _actionAudioExecutionPipeline?.ResetSession();
            _enemyAudioExecutionGuard.ResetSession();
            _enemyAudioExecutionPipeline?.ResetSession();
            ObserveTopologyActiveStateForPresentationPipelines(_lastPresentedTickIndex);
            ResetPresentationPipelineDiagnosticsIfEnabled();
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

            if (_isPresentationPaused &&
                extension is IGameplayPresentationPausable pausable)
            {
                pausable.SetPresentationPaused(true);
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

        public void ApplyStageTerminalPresentation(GameplayStageTerminalPresentationReason reason, TickResult terminalTickResult)
        {
            var context = new GameplayStageTerminalPresentationContext(reason, terminalTickResult);
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayStageTerminalPresentationExtension extension)
                {
                    extension.ApplyStageTerminalPresentation(context);
                }
            }
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
            RefreshGameplayAudioPlan(result);
            _blockAudioPresentationController.ReplacePendingPlan(
                _blockAudioRequestPlanner.BuildRequests(result, _timingProfile));
            RefreshTilePresentationRequests(result.PresentationData);
            _moonBlockDestructionPresentationController.RefreshSequences(
                result.PresentationData,
                _currentTilePresentationRequests,
                result.TickIndex);
            RefreshGravityFieldPresentationRequests(result.PresentationData);
            RefreshTileFeatureVisualStates(result.PresentationData);
            RefreshGravityFieldVisualStates(result.PresentationData);
            RefreshEnemyGravityFieldAuraVisualStates(result.PresentationData);
            SyncTileFeatureVisualPoseForTopologyMotionIfNeeded(result.PresentationData);
            _tileFeatureAudioPresentationController.ReplacePendingPlan(
                _tileFeatureAudioRequestPlanner.BuildRequests(
                    _currentTilePresentationRequests,
                    result.TickIndex,
                    _timingProfile));
            _gravityFieldAudioPresentationController.ReplacePendingPlan(
                _gravityFieldAudioRequestPlanner.BuildRequests(_currentGravityFieldPresentationRequests));
            _tileFeatureVisualPresentationController.PlayRequests(_currentTilePresentationRequests, _timingProfile);
            _moonBlockDestructionPresentationController.QueueOrStartMoonBlockGeneratedRequests(
                _currentTilePresentationRequests,
                _moonBlockEmergencePresentationController,
                result.TickIndex);
            _gravityFieldVisualPresentationController.PlayRequests(_currentGravityFieldPresentationRequests);
            _tileFeatureVisualPresentationController.RefreshContinuousStates(_currentTileFeatureVisualStates);
            _summonedEnemyPresentationResolver.Reconcile(result);
            _committedFrameBuilder.StoreCommittedFrame(
                result.FinalEntities,
                result.FinalTopology,
                _projector,
                _viewBinder,
                TopologyCommitted);
            RegisterCommittedViewPauseTargets();
            _gravityFieldVisualPresentationController.RefreshContinuousStates(_currentGravityFieldVisualStates);
            _gravityFieldVisualPresentationController.RefreshEnemyGravityFieldAuraLockedTargets(
                _currentEnemyGravityFieldAuraVisualStates);
            if (IsTopologyTransitionPresentation(result.PresentationData.TopologyMotion))
            {
                _moonBlockDestructionPresentationController.ClearForTopologyTransitionStart(
                    _moonBlockEmergencePresentationController,
                    result.TickIndex);
                _topologyTransitionEpoch++;
            }

            _lastPresentedResult = result;
            TraceStep("RefreshUtilityWindupWarnings");
            _utilityWindupVfxPresenter.RefreshSummonWarnings(
                Array.Empty<TickSummonWindupWarningSignal>(),
                _stateStore,
                _projector);

            _exitPresentationController.RefreshEntityExitPlan(result.PresentationData);
            _planner.RefreshPlayerLocomotionSignals(result.PresentationData);
            _playerLocomotionAudioPresentationController.RefreshSignals(
                result,
                _timingProfile.MoveMotionDurationSeconds);
            RefreshTopologyExecution(result);
            ObserveTopologyActiveStateForPresentationPipelines(result.TickIndex);
            RefreshGameplayAudioPlaybackGate();
            _topologyAudioPresentationController.ReplacePendingPlan(
                _topologyAudioRequestPlanner.BuildRequests(result));
            RefreshBoxMotionExecution(
                result,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology);
            _planner.RefreshTracks(
                result,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology,
                _projector,
                _timingProfile,
                suppressBoxMotionTracks:
                    _boxMotionExecutionMode == BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor);
            RetainTopologyMoonBlockGeneratedPoses(result.PresentationData);
            _lastPresentedTickIndex = result.TickIndex;
            RefreshPresentationMotionVfx(result.TickIndex);
            var suppressLegacyPlayerActionAnimations =
                _playerActionAnimationExecutionMode ==
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor;
            var suppressLegacyEnemyPresentationAnimations =
                _enemyPresentationExecutionMode ==
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor;
            _animationSync.ApplyTickPresentation(
                result,
                _stateStore.ViewsByEntityId,
                _trackState.JumpLandingCompletionHoldEntityIds,
                (entityId, actionKind) => _motionTimingResolver.ResolvePlayerMotionDurationSeconds(
                    entityId,
                    actionKind,
                    _timingProfile),
                suppressLegacyPlayerActionAnimations,
                suppressLegacyEnemyPresentationAnimations);
            RefreshEnemyPresentationExecution(result);
            RefreshPlayerActionAnimationExecution(result);
            RefreshDamageDeathVfxExecution(result);
            PresentExtensions(result);
            TraceStep("PlayPlannedAudio");
            _arbitratingGameplayAudioPlaybackPort?.BeginBatch(
                result.TickIndex,
                _timingProfile.SimulationTicksPerSecond);
            try
            {
                RefreshCoreGameplaySfxExecution(result);
                RefreshActionAudioExecution(result);
                RefreshEnemyAudioExecution(result);
                _audioPresentationController.PlayPlannedAudio();
                _actionAudioPresentationController.PlayPlannedAudio(result.TickIndex);
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
            PresentDiagnosticsPipelineIfEnabled(result);
        }

        private void RetainTopologyMoonBlockGeneratedPoses(TickPresentationData presentationData)
        {
            if (!IsTopologyTransitionPresentation(presentationData?.TopologyMotion) ||
                _currentTilePresentationRequests.Count == 0)
            {
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            for (var i = 0; i < _currentTilePresentationRequests.Count; i++)
            {
                var request = _currentTilePresentationRequests[i];
                if (request.RequestKind != TilePresentationRequestKind.MoonBlockGenerated ||
                    request.TargetEntityId <= 0 ||
                    !_projector.TryProjectTransitionEntityCell(
                        request.Cell,
                        topologyMotion.SourceTopology,
                        topologyMotion.DestinationTopology,
                        EntityType.Box,
                        out var projectedPose))
                {
                    continue;
                }

                var retainedPose = new GameplayEntityPose(projectedPose.LocalPosition, projectedPose.LocalRotation);
                var projectedSlot = _projector.TryGetProjectedTransitionEntitySlot(
                    request.Cell,
                    topologyMotion.SourceTopology,
                    topologyMotion.DestinationTopology,
                    out var resolvedProjectedSlot)
                    ? (GameplayProjectedFaceSlot?)resolvedProjectedSlot
                    : null;

                _trackState.LocalMotionTracks.Remove(request.TargetEntityId);
                _trackState.MotionVisualScaleEntityIds.Remove(request.TargetEntityId);
                _exitPresentationController.ReleasePlannedLiveExitOwnership(request.TargetEntityId);
                _stateStore.RetainedLocalTargetPoses[request.TargetEntityId] = retainedPose;
                _stateStore.TransitionVisibilityStates[request.TargetEntityId] =
                    new TransitionVisibilityState(
                        TickTransitionVisibilityMode.ShowAtTransitionStart,
                        retainedPose,
                        projectedSlot,
                        request.Cell.face);
            }
        }

        private void RefreshTopologyExecution(TickResult result)
        {
            if (_topologyExecutionMode == TopologyPresentationExecutionMode.ExecutorBridge)
            {
                ExecuteExecutorBridgeTopologyPath(result);
                return;
            }

            ExecuteLegacyTopologyPath(result);
        }

        private void ExecuteLegacyTopologyPath(TickResult result)
        {
            if (IsTopologyTransitionPresentation(result.PresentationData.TopologyMotion) &&
                !_topologyExecutionGuard.TryBeginExecution(
                    TopologyPresentationExecutionOwner.LegacyCoordinator,
                    result.TickIndex,
                    hasSourceMetadata: false,
                    sourceMetadataKey: 0))
            {
                return;
            }

            _topologyTransitionController.RefreshTopologyTrack(
                result.PresentationData,
                _stateStore.CommittedTopology);
            _topologyTransitionController.RefreshBoardSurfaceTransition(
                result.PresentationData,
                _stateStore.CommittedTopology);
        }

        private void RefreshBoxMotionExecution(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology)
        {
            if (_boxMotionExecutionMode == BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor)
            {
                RecordBoxMotionLegacySkippedByPolicy(result);
                var playbackPort = ResolveBoxMotionPlaybackPort();
                if (playbackPort is GameplayMotionTrackPlannerPlaybackPort adapter)
                {
                    adapter.BeginTickContext(
                        result,
                        previousCommittedLocalTargetPoses,
                        previousCommittedTopology,
                        _projector,
                        _timingProfile);
                }

                _boxMotionExecutionPipeline ??= _boxMotionExecutionPipelineFactory(
                    _boxMotionExecutionMode,
                    playbackPort,
                    _boxMotionExecutionGuard);
                _boxMotionExecutionPipeline?.Present(result);
                return;
            }

            RecordBoxMotionLegacyOwnership(result);
        }

        private IGameplayMotionPlaybackPort ResolveBoxMotionPlaybackPort()
        {
            return _boxMotionPlaybackPort ?? _boxMotionTrackPlannerPlaybackPort;
        }

        private IGameplayAnimationPlaybackPort ResolvePlayerActionAnimationPlaybackPort()
        {
            return _playerActionAnimationPlaybackPort ?? _playerActionAnimationSyncPlaybackPort;
        }

        private IGameplayEnemyPresentationPlaybackPort ResolveEnemyPresentationPlaybackPort()
        {
            return _enemyPresentationPlaybackPort ?? _enemyPresentationSyncPlaybackPort;
        }

        private IGameplaySfxPlaybackPort ResolveCoreGameplaySfxPlaybackPort()
        {
            return _coreGameplaySfxPlaybackPort ?? _coreGameplaySfxPlaybackPortAdapter;
        }

        private IGameplayActionAudioPlaybackPort ResolveActionAudioPlaybackPort()
        {
            return _actionAudioPlaybackPort ?? _actionAudioPlaybackPortAdapter;
        }

        private IGameplayEnemyAudioPlaybackPort ResolveEnemyAudioPlaybackPort()
        {
            return _enemyAudioPlaybackPort ?? _enemyAudioPlaybackPortAdapter;
        }

        private void RecordBoxMotionLegacyOwnership(TickResult result)
        {
            foreach (var key in BuildBoxMotionPlaybackKeys(result))
            {
                _boxMotionExecutionGuard.TryBeginExecution(
                    BoxMotionPresentationExecutionOwner.LegacyTrackPlanner,
                    key);
            }
        }

        private void RecordBoxMotionLegacySkippedByPolicy(TickResult result)
        {
            foreach (var _ in BuildBoxMotionPlaybackKeys(result))
            {
                _boxMotionExecutionGuard.RecordSkippedByPolicy(
                    BoxMotionPresentationExecutionOwner.LegacyTrackPlanner);
            }
        }

        private static IEnumerable<BoxMotionPlaybackKey> BuildBoxMotionPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            for (var i = 0; i < factFrame.Facts.Count; i++)
            {
                var fact = factFrame.Facts[i];
                if (!fact.MotionPayload.IsValid ||
                    !TryMapMotionFactKind(fact.MotionPayload.Kind, out var cueKey))
                {
                    continue;
                }

                var payload = fact.MotionPayload;
                yield return new BoxMotionPlaybackKey(
                    fact.Source.TickIndex,
                    fact.Source.SemanticSource,
                    payload.EntityId,
                    cueKey,
                    payload.SourceCell,
                    payload.DestinationCell,
                    payload.SourceActionPlanId,
                    payload.SourceSequenceId);
            }
        }

        private static bool TryMapMotionFactKind(
            PresentationMotionFactKind factKind,
            out PresentationMotionCueKey cueKey)
        {
            switch (factKind)
            {
                case PresentationMotionFactKind.BoxSlide:
                    cueKey = PresentationMotionCueKey.BoxSlide;
                    return true;
                case PresentationMotionFactKind.BoxFlip:
                    cueKey = PresentationMotionCueKey.BoxFlip;
                    return true;
                case PresentationMotionFactKind.BoxFlipImpact:
                    cueKey = PresentationMotionCueKey.BoxFlipImpact;
                    return true;
                default:
                    cueKey = PresentationMotionCueKey.None;
                    return false;
            }
        }

        private void RefreshPlayerActionAnimationExecution(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            if (_playerActionAnimationExecutionMode ==
                PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor)
            {
                RecordPlayerActionAnimationLegacySkippedByPolicy(result);
                _playerActionAnimationExecutionPipeline ??= _playerActionAnimationExecutionPipelineFactory(
                    _playerActionAnimationExecutionMode,
                    ResolvePlayerActionAnimationPlaybackPort(),
                    _playerActionAnimationExecutionGuard);
                _playerActionAnimationExecutionPipeline?.Present(result);
                return;
            }

            RecordPlayerActionAnimationLegacyOwnership(result);
        }

        private void RecordPlayerActionAnimationLegacyOwnership(TickResult result)
        {
            foreach (var key in BuildPlayerActionAnimationPlaybackKeys(result))
            {
                _playerActionAnimationExecutionGuard.TryBeginExecution(
                    PlayerActionAnimationExecutionOwner.LegacyAnimationSync,
                    key);
            }
        }

        private void RecordPlayerActionAnimationLegacySkippedByPolicy(TickResult result)
        {
            foreach (var _ in BuildPlayerActionAnimationPlaybackKeys(result))
            {
                _playerActionAnimationExecutionGuard.RecordSkippedByPolicy(
                    PlayerActionAnimationExecutionOwner.LegacyAnimationSync);
            }
        }

        private static IEnumerable<PlayerActionAnimationPlaybackKey> BuildPlayerActionAnimationPlaybackKeys(
            TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new AnimationCuePlanner(),
            }).Plan(factFrame);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Animation ||
                    !cue.Key.TryGetAnimationCueKey(out var cueKey) ||
                    !cue.AnimationPayload.IsValid ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                yield return new PlayerActionAnimationPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Target.EntityId,
                    cueKey,
                    cue.AnimationPayload.ActionKind,
                    cue.AnimationPayload.PhaseKind,
                    cue.AnimationPayload.SourceSequenceId,
                    cue.AnimationPayload.SourceActionPlanId);
            }
        }

        private void RefreshEnemyPresentationExecution(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            if (_enemyPresentationExecutionMode ==
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor)
            {
                RecordEnemyPresentationLegacySkippedByPolicy(result);
                _enemyPresentationExecutionPipeline ??= _enemyPresentationExecutionPipelineFactory(
                    _enemyPresentationExecutionMode,
                    ResolveEnemyPresentationPlaybackPort(),
                    _enemyPresentationExecutionGuard);
                _enemyPresentationExecutionPipeline?.Present(result);
                return;
            }

            RecordEnemyPresentationLegacyOwnership(result);
        }

        private void RecordEnemyPresentationLegacyOwnership(TickResult result)
        {
            foreach (var key in BuildEnemyPresentationPlaybackKeys(result))
            {
                _enemyPresentationExecutionGuard.TryBeginExecution(
                    EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper,
                    key);
            }
        }

        private void RecordEnemyPresentationLegacySkippedByPolicy(TickResult result)
        {
            foreach (var _ in BuildEnemyPresentationPlaybackKeys(result))
            {
                _enemyPresentationExecutionGuard.RecordSkippedByPolicy(
                    EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper);
            }
        }

        private static IEnumerable<EnemyPresentationPlaybackKey> BuildEnemyPresentationPlaybackKeys(
            TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyPresentationCuePlanner(),
            }).Plan(factFrame);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Animation ||
                    !cue.Key.TryGetAnimationCueKey(out var cueKey) ||
                    !cue.EnemyPayload.IsValid ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                yield return new EnemyPresentationPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Target.EntityId,
                    cueKey,
                    cue.EnemyPayload.Kind,
                    cue.EnemyPayload.Phase,
                    cue.EnemyPayload.SourceSequenceId);
            }
        }

        private void ExecuteExecutorBridgeTopologyPath(TickResult result)
        {
            if (IsTopologyTransitionPresentation(result.PresentationData.TopologyMotion))
            {
                _topologyExecutionGuard.RecordSkippedByPolicy(
                    TopologyPresentationExecutionOwner.LegacyCoordinator);
            }

            _topologyExecutionPipeline ??= _topologyExecutionPipelineFactory(
                _topologyExecutionMode,
                _topologyTransitionController,
                _topologyExecutionGuard);
            _topologyExecutionPipeline?.Present(result);
        }

        private void RefreshDamageDeathVfxExecution(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            var keys = BuildDamageDeathVfxPlaybackKeys(result);
            if (_damageDeathVfxExecutionMode == DamageDeathVfxExecutionMode.OrchestrationExecutor)
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    _damageDeathVfxExecutionGuard.RecordSkippedByPolicy(
                        DamageDeathVfxExecutionOwner.LegacyExtension);
                }

                _damageDeathVfxExecutionPipeline ??= _damageDeathVfxExecutionPipelineFactory(
                    _damageDeathVfxExecutionMode,
                    _damageDeathVfxPlaybackPort,
                    _damageDeathVfxExecutionGuard);
                _damageDeathVfxExecutionPipeline?.Present(result);
                return;
            }

            for (var i = 0; i < keys.Count; i++)
            {
                _damageDeathVfxExecutionGuard.TryBeginExecution(
                    DamageDeathVfxExecutionOwner.LegacyExtension,
                keys[i]);
            }
        }

        private void RefreshCoreGameplaySfxExecution(TickResult result)
        {
            if (result == null ||
                _coreGameplaySfxExecutionMode != CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor)
            {
                return;
            }

            _coreGameplaySfxExecutionPipeline ??= _coreGameplaySfxExecutionPipelineFactory(
                _coreGameplaySfxExecutionMode,
                ResolveCoreGameplaySfxPlaybackPort(),
                _coreGameplaySfxExecutionGuard);
            _coreGameplaySfxExecutionPipeline?.Present(result);
        }

        private void RefreshActionAudioExecution(TickResult result)
        {
            if (result == null ||
                _actionAudioExecutionMode != ActionAudioExecutionMode.OrchestrationActionAudioBridge)
            {
                return;
            }

            _actionAudioExecutionPipeline ??= _actionAudioExecutionPipelineFactory(
                _actionAudioExecutionMode,
                ResolveActionAudioPlaybackPort(),
                _actionAudioExecutionGuard);
            _actionAudioExecutionPipeline?.Present(result);
        }

        private void RefreshEnemyAudioExecution(TickResult result)
        {
            if (result == null ||
                _enemyAudioExecutionMode != EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge)
            {
                return;
            }

            _enemyAudioExecutionPipeline ??= _enemyAudioExecutionPipelineFactory(
                _enemyAudioExecutionMode,
                ResolveEnemyAudioPlaybackPort(),
                _enemyAudioExecutionGuard);
            _enemyAudioExecutionPipeline?.Present(result);
        }

        private static IReadOnlyList<DamageDeathVfxPlaybackKey> BuildDamageDeathVfxPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new VfxCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<DamageDeathVfxPlaybackKey>();
            }

            var keys = new List<DamageDeathVfxPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Vfx ||
                    !cue.Key.TryGetVfxCueKey(out var cueKey) ||
                    (cueKey != PresentationVfxCueKey.DamageHit &&
                     cueKey != PresentationVfxCueKey.EnemyDeath) ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                keys.Add(new DamageDeathVfxPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Source.SourceEntityId,
                    cue.Target.EntityId,
                    cueKey));
            }

            return keys.Count == 0
                ? Array.Empty<DamageDeathVfxPlaybackKey>()
                : keys;
        }

        private static IReadOnlyList<CoreGameplaySfxPlaybackKey> BuildCoreGameplaySfxPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<CoreGameplaySfxPlaybackKey>();
            }

            var keys = new List<CoreGameplaySfxPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Sfx ||
                    !cue.Key.TryGetSfxCueKey(out var cueKey) ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                keys.Add(new CoreGameplaySfxPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Source.SourceEntityId,
                    cue.Target.EntityId,
                    cueKey));
            }

            return keys.Count == 0
                ? Array.Empty<CoreGameplaySfxPlaybackKey>()
                : keys;
        }

        private static IReadOnlyList<ActionAudioPlaybackKey> BuildActionAudioPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new ActionAudioCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<ActionAudioPlaybackKey>();
            }

            var keys = new List<ActionAudioPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.ActionAudio ||
                    !cue.Key.TryGetActionAudioCueKey(out _) ||
                    !TryMapActionAudioPayload(cue.ActionAudioPayload, out var action, out var moment))
                {
                    continue;
                }

                var payload = cue.ActionAudioPayload;
                keys.Add(new ActionAudioPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    payload.OwnerEntityId,
                    action,
                    moment,
                    payload.SourceSequenceId,
                    payload.SourceActionPlanId,
                    payload.TargetEntityId,
                    i));
            }

            return keys.Count == 0
                ? Array.Empty<ActionAudioPlaybackKey>()
                : keys;
        }

        private static IReadOnlyList<EnemyAudioPlaybackKey> BuildEnemyAudioPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyAudioCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<EnemyAudioPlaybackKey>();
            }

            var keys = new List<EnemyAudioPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.EnemyAudio ||
                    !cue.Key.TryGetEnemyAudioCueKey(out var cueKey) ||
                    !cue.EnemyAudioPayload.IsValid)
                {
                    continue;
                }

                var payload = cue.EnemyAudioPayload;
                keys.Add(new EnemyAudioPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    payload.OwnerEntityId,
                    cueKey,
                    payload.OriginKind,
                    payload.Phase,
                    payload.SourceSequenceId,
                    payload.TargetEntityId,
                    payload.ImpactTick,
                    payload.ImpactId,
                    payload.PresentationKey,
                    i));
            }

            return keys.Count == 0
                ? Array.Empty<EnemyAudioPlaybackKey>()
                : keys;
        }

        private static bool TryMapActionAudioPayload(
            PresentationActionAudioPayload payload,
            out GameplayActionKind action,
            out GameplayActionAudioMoment moment)
        {
            action = default;
            moment = default;
            if (!payload.IsValid ||
                !Enum.IsDefined(typeof(GameplayActionKind), payload.ActionKind))
            {
                return false;
            }

            action = (GameplayActionKind)payload.ActionKind;
            switch ((GameplayActionAudioMoment)payload.Moment)
            {
                case GameplayActionAudioMoment.Windup:
                case GameplayActionAudioMoment.AssistOutOfRange:
                case GameplayActionAudioMoment.NoTarget:
                case GameplayActionAudioMoment.Invalid:
                    moment = (GameplayActionAudioMoment)payload.Moment;
                    return true;
                default:
                    return false;
            }
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
            SetGameplayAudioPlaybackGate(GameplayAudioPlaybackGateState.Open);
            _enemyChargeLoopAudioPresentationController.ResetSession();
            _blockAudioPresentationController.ResetSession();
            _playerLocomotionAudioPresentationController.ResetSession();
            _tileFeatureAudioPresentationController.ResetSession();
            _topologyAudioPresentationController.ResetSession();
            _gravityFieldAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _entityPresentationApplier.ResetEnemySemanticPresentationDriverCache();
            ClearBoxMotionPresentationRuntimeState();
            _trackState.ResetSession();
            _exitPresentationController.Reset();
            _moonBlockDestructionPresentationController.ResetSession();
            _utilityWindupVfxPresenter.Clear();
            _tileFeatureVisualPresentationController.ResetSession();
            _moonBlockEmergencePresentationController.ResetSession();
            ResetExtensions();
            _animationSync.Reset();
            _stateStore.ResetSession(topology);
            _currentTilePresentationRequests = EmptyTilePresentationRequests;
            _currentGravityFieldPresentationRequests = EmptyGravityFieldPresentationRequests;
            _currentGravityFieldVisualStates = EmptyGravityFieldVisualStates;
            _currentEnemyGravityFieldAuraVisualStates = EmptyEnemyGravityFieldAuraVisualStates;
            _currentTileFeatureVisualStates = EmptyTileFeatureVisualStates;
            _topologyTransitionController.Reset();
            _topologyExecutionGuard.ResetSession();
            _topologyExecutionPipeline?.ResetSession();
            _damageDeathVfxExecutionGuard.ResetSession();
            _damageDeathVfxExecutionPipeline?.ResetSession();
            _boxMotionExecutionGuard.ResetSession();
            _boxMotionExecutionPipeline?.ResetSession();
            _playerActionAnimationExecutionGuard.ResetSession();
            _playerActionAnimationExecutionPipeline?.ResetSession();
            _enemyPresentationExecutionGuard.ResetSession();
            _enemyPresentationExecutionPipeline?.ResetSession();
            _coreGameplaySfxExecutionGuard.ResetSession();
            _coreGameplaySfxExecutionPipeline?.ResetSession();
            _actionAudioExecutionGuard.ResetSession();
            _actionAudioExecutionPipeline?.ResetSession();
            _enemyAudioExecutionGuard.ResetSession();
            _enemyAudioExecutionPipeline?.ResetSession();
            ObserveTopologyActiveStateForPresentationPipelines(_lastPresentedTickIndex);
            _lastPresentedResult = null;
            _topologyTransitionEpoch = 0;
            ResetPresentationPipelineDiagnosticsIfEnabled();

            _committedFrameBuilder.StoreCommittedFrame(
                entities,
                topology,
                _projector,
                _viewBinder,
                TopologyCommitted);
            RegisterCommittedViewPauseTargets();
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

            if (_isPresentationPaused)
            {
                return;
            }

            var hadActiveBoardRotationTween = _topologyTransitionController.HasActiveBoardRotationTween;
            _topologyTransitionController.UpdatePresentation(deltaTime, _stateStore.CommittedTopology);
            ObserveTopologyActiveStateForPresentationPipelines(_lastPresentedTickIndex);
            RefreshGameplayAudioPlaybackGate();
            var gameplayAudioDeltaTime =
                hadActiveBoardRotationTween || _topologyTransitionController.HasActiveBoardRotationTween
                    ? 0f
                    : deltaTime;
            _audioPresentationController.Update(gameplayAudioDeltaTime);
            _coreGameplaySfxPlaybackPortAdapter.Update();
            _actionAudioPresentationController.Update();
            _enemyAudioPresentationController.Update(_lastPresentedTickIndex, gameplayAudioDeltaTime);
            _blockAudioPresentationController.Update(deltaTime);
            _playerLocomotionAudioPresentationController.Update(deltaTime);
            _tileFeatureAudioPresentationController.Update(deltaTime);
            _tileFeatureVisualPresentationController.Update(deltaTime);
            _moonBlockEmergencePresentationController.UpdatePresentation(deltaTime);
            _gravityFieldVisualPresentationController.UpdatePresentation(deltaTime);
            UpdateExtensions(deltaTime);
            _moonBlockDestructionPresentationController.UpdateSequences(
                _moonBlockEmergencePresentationController,
                _lastPresentedTickIndex,
                deltaTime);
            _entityPresentationApplier.Apply(
                deltaTime,
                hadActiveBoardRotationTween || _topologyTransitionController.HasActiveBoardRotationTween,
                _viewBinder,
                _timingProfile);
            _exitPresentationController.CompleteDeferredEntityExits();
            RefreshPresentationMotionVfx(_lastPresentedTickIndex);
            _moonBlockDestructionPresentationController.UpdateSequences(
                _moonBlockEmergencePresentationController,
                _lastPresentedTickIndex);
            _exitPresentationController.AdvanceDeathPresentationCleanups(deltaTime);
            _exitPresentationController.AdvanceContactDelayedEntityExits(deltaTime);
            RefreshPresentationMotionVfx(_lastPresentedTickIndex);
            _moonBlockDestructionPresentationController.UpdateSequences(
                _moonBlockEmergencePresentationController,
                _lastPresentedTickIndex);
            _moonBlockEmergencePresentationController.StartReadyRequests(_lastPresentedTickIndex);
            _damageDeathVfxExecutionPipeline?.Update(deltaTime);
            _boxMotionExecutionPipeline?.Update(deltaTime);
            _playerActionAnimationExecutionPipeline?.Update(deltaTime);
            _enemyPresentationExecutionPipeline?.Update(deltaTime);
            _coreGameplaySfxExecutionPipeline?.Update(deltaTime);
            _actionAudioExecutionPipeline?.Update(deltaTime);
            _enemyAudioExecutionPipeline?.Update(deltaTime);
            UpdatePresentationPipelineDiagnosticsIfEnabled(deltaTime);
        }

        public void SetPresentationPaused(bool paused)
        {
            if (_isPresentationPaused == paused)
            {
                return;
            }

            _isPresentationPaused = paused;
            _presentationPauseRegistry.SetPresentationPaused(paused);
            SetPresentationPausedOnExtensions(paused);
        }

        internal void AttachGameplayAudioRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            var arbitratingPort = GetOrCreateArbitratingPlaybackPort(playbackPort);
            _audioPresentationController.AttachRuntime(arbitratingPort, gameplayAudioMap);
            _coreGameplaySfxPlaybackPortAdapter.AttachRuntime(arbitratingPort, gameplayAudioMap);
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
            if (registry is TileFeatureVisualRegistry concreteRegistry &&
                concreteRegistry.SearchRoot != null)
            {
                RegisterPresentationPauseRoot(concreteRegistry.SearchRoot.gameObject);
            }
        }

        internal void RegisterPresentationPauseRoot(GameObject root)
        {
            _presentationPauseRegistry.RegisterRoot(root);
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
            _enemyAudioPlaybackPort?.HardCleanup();
            _coreGameplaySfxPlaybackPortAdapter.DetachRuntime();
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

            RefreshGameplayAudioPlan(result);
        }

        private void RefreshGameplayAudioPlan(TickResult result)
        {
            var gameplayAudioRequests = _audioRequestPlanner.BuildRequests(result, _timingProfile);
            var enemyAudioRequests = _enemyAudioRequestPlanner.BuildRequests(result, _timingProfile);
            var playableDeathCueEntityIds = BuildPlayableEnemyDeathCueEntityIds(
                result.PresentationData,
                enemyAudioRequests);
            _coreGameplaySfxPlaybackPortAdapter.ConfigureEnemyDeathCueSuppression(playableDeathCueEntityIds);
            var filteredGameplayAudioRequests = SuppressLethalEnemyDamageRequests(
                gameplayAudioRequests,
                playableDeathCueEntityIds);

            if (_coreGameplaySfxExecutionMode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor)
            {
                var coreSfxKeys = BuildCoreGameplaySfxPlaybackKeys(result);
                for (var i = 0; i < coreSfxKeys.Count; i++)
                {
                    _coreGameplaySfxExecutionGuard.RecordSkippedByPolicy(
                        CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController);
                }

                _audioPresentationController.ReplacePendingPlan(Array.Empty<GameplayAudioRequest>(), result.TickIndex);
            }
            else
            {
                _audioPresentationController.ReplacePendingPlan(filteredGameplayAudioRequests, result.TickIndex);
                var coreSfxKeys = BuildCoreGameplaySfxPlaybackKeys(result);
                for (var i = 0; i < coreSfxKeys.Count; i++)
                {
                    _coreGameplaySfxExecutionGuard.TryBeginExecution(
                        CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController,
                        coreSfxKeys[i]);
                }
            }

            var actionAudioRequests = _actionAudioRequestPlanner.BuildRequests(result);
            var actionAudioKeys = BuildActionAudioPlaybackKeys(result);
            if (_actionAudioExecutionMode == ActionAudioExecutionMode.OrchestrationActionAudioBridge)
            {
                for (var i = 0; i < actionAudioKeys.Count; i++)
                {
                    _actionAudioExecutionGuard.RecordSkippedByPolicy(
                        ActionAudioExecutionOwner.LegacyActionAudioController);
                }

                _actionAudioPresentationController.ReplacePendingPlan(
                    Array.Empty<GameplayActionAudioRequest>(),
                    result.TickIndex);
            }
            else
            {
                _actionAudioPresentationController.ReplacePendingPlan(actionAudioRequests, result.TickIndex);
                for (var i = 0; i < actionAudioKeys.Count; i++)
                {
                    _actionAudioExecutionGuard.TryBeginExecution(
                        ActionAudioExecutionOwner.LegacyActionAudioController,
                        actionAudioKeys[i]);
                }
            }

            var enemyAudioKeys = BuildEnemyAudioPlaybackKeys(result);
            if (_enemyAudioExecutionMode == EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge)
            {
                for (var i = 0; i < enemyAudioKeys.Count; i++)
                {
                    _enemyAudioExecutionGuard.RecordSkippedByPolicy(
                        EnemyAudioExecutionOwner.LegacyEnemyAudioController);
                }

                _enemyAudioPresentationController.ReplacePendingPlan(Array.Empty<EnemyAudioRequest>(), result.TickIndex);
            }
            else
            {
                _enemyAudioPresentationController.ReplacePendingPlan(enemyAudioRequests, result.TickIndex);
                for (var i = 0; i < enemyAudioKeys.Count; i++)
                {
                    _enemyAudioExecutionGuard.TryBeginExecution(
                        EnemyAudioExecutionOwner.LegacyEnemyAudioController,
                        enemyAudioKeys[i]);
                }
            }
        }

        private void RefreshGameplayAudioPlaybackGate()
        {
            SetGameplayAudioPlaybackGate(
                _topologyTransitionController.HasActiveBoardRotationTween
                    ? GameplayAudioPlaybackGateState.TopologyLocked
                    : GameplayAudioPlaybackGateState.Open);
        }

        private void SetGameplayAudioPlaybackGate(GameplayAudioPlaybackGateState gateState)
        {
            _audioPresentationController.SetPlaybackGateState(gateState);
            _coreGameplaySfxPlaybackPortAdapter.SetPlaybackGateState(gateState);
            _actionAudioPresentationController.SetPlaybackGateState(gateState);
            _enemyAudioPresentationController.SetPlaybackGateState(gateState);
        }

        private IReadOnlyList<GameplayAudioRequest> SuppressLethalEnemyDamageRequests(
            IReadOnlyList<GameplayAudioRequest> gameplayAudioRequests,
            ISet<int> playableDeathCueEntityIds)
        {
            if (gameplayAudioRequests.Count == 0 ||
                playableDeathCueEntityIds == null ||
                playableDeathCueEntityIds.Count == 0)
            {
                return gameplayAudioRequests;
            }

            List<GameplayAudioRequest> filteredRequests = null;
            for (var i = 0; i < gameplayAudioRequests.Count; i++)
            {
                var request = gameplayAudioRequests[i];
                if (ShouldSuppressLethalEnemyDamageRequest(request, playableDeathCueEntityIds))
                {
                    if (filteredRequests == null)
                    {
                        filteredRequests = new List<GameplayAudioRequest>(gameplayAudioRequests.Count);
                        for (var copyIndex = 0; copyIndex < i; copyIndex++)
                        {
                            filteredRequests.Add(gameplayAudioRequests[copyIndex]);
                        }
                    }

                    continue;
                }

                filteredRequests?.Add(request);
            }

            return filteredRequests ?? gameplayAudioRequests;
        }

        private HashSet<int> BuildPlayableEnemyDeathCueEntityIds(
            TickPresentationData presentationData,
            IReadOnlyList<EnemyAudioRequest> enemyAudioRequests)
        {
            var deathExitEntityIds = BuildEnemyDeathExitEntityIds(presentationData);
            if (deathExitEntityIds.Count == 0)
            {
                return deathExitEntityIds;
            }

            var playableDeathCueEntityIds = new HashSet<int>();
            for (var i = 0; i < enemyAudioRequests.Count; i++)
            {
                var request = enemyAudioRequests[i];
                if (request.Cue != EnemyAudioCue.Death ||
                    !deathExitEntityIds.Contains(request.OwnerEntityId) ||
                    !HasPlayableEnemyDeathCue(request.OwnerEntityId))
                {
                    continue;
                }

                playableDeathCueEntityIds.Add(request.OwnerEntityId);
            }

            return playableDeathCueEntityIds;
        }

        private static HashSet<int> BuildEnemyDeathExitEntityIds(TickPresentationData presentationData)
        {
            var entityIds = new HashSet<int>();
            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                var signal = exitSignals[i];
                if (signal.ExitCause != TickEntityExitCause.EnemyDeath &&
                    signal.ExitCause != TickEntityExitCause.Killed)
                {
                    continue;
                }

                entityIds.Add(signal.ExitedEntityId);
            }

            return entityIds;
        }

        private bool HasPlayableEnemyDeathCue(int ownerEntityId)
        {
            if (!TryResolveActiveOwner(ownerEntityId, out var owner))
            {
                return false;
            }

            var authoring = EnemyAudioAuthoring.GetOptionalValidatedAuthoring(owner);
            return authoring != null &&
                   authoring.Profile.HasCue(EnemyAudioCue.Death);
        }

        private static bool ShouldSuppressLethalEnemyDamageRequest(
            in GameplayAudioRequest request,
            ISet<int> playableDeathCueEntityIds)
        {
            return request.SemanticId == GameplayAudioSemanticId.EnemyDamage &&
                   request.OwnerEntityId.HasValue &&
                   playableDeathCueEntityIds.Contains(request.OwnerEntityId.Value);
        }

        private bool TryResolveActiveOwner(int ownerEntityId, out GameplayEntityView owner)
        {
            owner = null;
            if (!_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId, out owner) ||
                owner == null ||
                !owner.gameObject.activeInHierarchy)
            {
                owner = null;
                return false;
            }

            return true;
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

        private void RefreshEnemyGravityFieldAuraVisualStates(TickPresentationData presentationData)
        {
            var visualStates = presentationData.EnemyGravityFieldAuraVisualStates;
            _currentEnemyGravityFieldAuraVisualStates = visualStates.Count == 0
                ? EmptyEnemyGravityFieldAuraVisualStates
                : new ReadOnlyCollection<TickEnemyGravityFieldAuraVisualState>(
                    new List<TickEnemyGravityFieldAuraVisualState>(visualStates));
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
            _moonBlockDestructionPresentationController.Dispose();
            _moonBlockEmergencePresentationController.Dispose();
            _topologyExecutionPipeline?.HardCleanup();
            _damageDeathVfxExecutionPipeline?.HardCleanup();
            _boxMotionExecutionPipeline?.HardCleanup();
            _boxMotionExecutionGuard.ResetSession();
            _playerActionAnimationExecutionPipeline?.HardCleanup();
            _playerActionAnimationExecutionGuard.ResetSession();
            _enemyPresentationExecutionPipeline?.HardCleanup();
            _enemyPresentationExecutionGuard.ResetSession();
            _coreGameplaySfxExecutionPipeline?.HardCleanup();
            _coreGameplaySfxExecutionGuard.ResetSession();
            _actionAudioExecutionPipeline?.HardCleanup();
            _actionAudioExecutionGuard.ResetSession();
            _enemyAudioExecutionPipeline?.HardCleanup();
            _enemyAudioExecutionGuard.ResetSession();
            ClearBoxMotionPresentationRuntimeState();
            _presentationPipeline?.HardCleanup();
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.HardCleanup();
            }
        }

        private void ClearBoxMotionPresentationRuntimeState()
        {
            _entityPresentationApplier.ResetBoxFlipInteractionsForKnownViews();
            _trackState.FlipInteractionResetRequests.Clear();

            var boxEntityIds = new List<int>();
            foreach (var pair in _stateStore.EntityTypesByEntityId)
            {
                if (pair.Value == EntityType.Box)
                {
                    boxEntityIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < boxEntityIds.Count; i++)
            {
                var entityId = boxEntityIds[i];
                _trackState.LocalMotionTracks.Remove(entityId);
                _trackState.MotionVisualScaleEntityIds.Remove(entityId);
                _trackState.OriginalViewMotionTracks.Remove(entityId);
                _trackState.CompletedMotionTrackIds.Remove(entityId);
                _trackState.CompletedMotionVisualScaleEntityIds.Remove(entityId);
                _trackState.CompletedOriginalViewMotionTrackIds.Remove(entityId);
                _trackState.CompletedPresentationMotionKeys.RemoveWhere(key => key.EntityId == entityId);
            }

            _trackState.CompletedFlipInteractionTrackIds.Clear();
            foreach (var pair in _trackState.FlipInteractionTracks)
            {
                if (boxEntityIds.Contains(pair.Value.BoxEntityId))
                {
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < _trackState.CompletedFlipInteractionTrackIds.Count; i++)
            {
                _trackState.FlipInteractionTracks.Remove(_trackState.CompletedFlipInteractionTrackIds[i]);
            }

            _trackState.CompletedFlipInteractionTrackIds.Clear();
        }

        private void PresentDiagnosticsPipelineIfEnabled(TickResult result)
        {
            if (!_presentationPipelineDiagnosticsEnabled)
            {
                return;
            }

            _presentationPipeline ??= GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();
            _presentationPipeline.Present(result);
            ObserveTopologyActiveStateForPresentationPipelines(result.TickIndex);
        }

        private void UpdatePresentationPipelineDiagnosticsIfEnabled(float deltaTime)
        {
            if (!_presentationPipelineDiagnosticsEnabled)
            {
                return;
            }

            _presentationPipeline?.Update(deltaTime);
            ObserveTopologyActiveStateForPresentationPipelines(_lastPresentedTickIndex);
        }

        private void ResetPresentationPipelineDiagnosticsIfEnabled()
        {
            if (!_presentationPipelineDiagnosticsEnabled)
            {
                return;
            }

            _presentationPipeline?.ResetSession();
            ObserveTopologyActiveStateForPresentationPipelines(_lastPresentedTickIndex);
        }

        private void ObserveTopologyActiveStateForPresentationPipelines(int tickIndex)
        {
            var isTopologyActive = _topologyTransitionController.HasActiveBoardRotationTween;
            if (_presentationPipelineDiagnosticsEnabled)
            {
                _presentationPipeline?.ObserveTopologyActiveState(isTopologyActive, tickIndex);
            }

            _topologyExecutionPipeline?.ObserveTopologyActiveState(isTopologyActive, tickIndex);
            _actionAudioExecutionPipeline?.ObserveTopologyActiveState(isTopologyActive, tickIndex);
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
                isTopologyTransitionCompletionReconcile: false,
                damageDeathVfxExecutionMode: _damageDeathVfxExecutionMode);
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

        private void RegisterCommittedViewPauseTargets()
        {
            foreach (var pair in _stateStore.ViewsByEntityId)
            {
                if (pair.Value != null)
                {
                    _presentationPauseRegistry.RegisterRoot(pair.Value.gameObject);
                }
            }
        }

        private void UpdateExtensions(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.UpdatePresentation(deltaTime);
            }
        }

        private void SetPresentationPausedOnExtensions(bool paused)
        {
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayPresentationPausable pausable)
                {
                    pausable.SetPresentationPaused(paused);
                }
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
                isTopologyTransitionCompletionReconcile: true,
                damageDeathVfxExecutionMode: _damageDeathVfxExecutionMode);
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

        private static TopologyPresentationExecutionMode NormalizeTopologyPresentationExecutionMode(
            TopologyPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(TopologyPresentationExecutionMode), mode)
                ? mode
                : TopologyPresentationExecutionMode.LegacyCoordinator;
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

        private DestroyShrinkVfxSequenceState ResolveDestroyShrinkVfxSequenceState(
            int sourceEntityId,
            int sequenceId)
        {
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayDestroyShrinkVfxSequenceStateProvider provider)
                {
                    var state = provider.GetDestroyShrinkState(sourceEntityId, sequenceId);
                    if (state != DestroyShrinkVfxSequenceState.None)
                    {
                        return state;
                    }
                }
            }

            return DestroyShrinkVfxSequenceState.None;
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

        private GameplayVfxExecutorDiagnostics ResolveDamageDeathVfxExecutorDiagnostics()
        {
            if (_damageDeathVfxExecutionPipeline == null)
            {
                return default;
            }

            var executors = _damageDeathVfxExecutionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayVfxPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        private GameplayMotionExecutorDiagnostics ResolveBoxMotionExecutorDiagnostics()
        {
            if (_boxMotionExecutionPipeline == null)
            {
                return default;
            }

            var executors = _boxMotionExecutionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayMotionPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        private GameplayAnimationExecutorDiagnostics ResolvePlayerActionAnimationExecutorDiagnostics()
        {
            if (_playerActionAnimationExecutionPipeline == null)
            {
                return default;
            }

            var executors = _playerActionAnimationExecutionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayAnimationPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        private GameplayEnemyPresentationExecutorDiagnostics ResolveEnemyPresentationExecutorDiagnostics()
        {
            if (_enemyPresentationExecutionPipeline == null)
            {
                return default;
            }

            var executors = _enemyPresentationExecutionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayEnemyPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        private GameplaySfxExecutorDiagnostics ResolveCoreGameplaySfxExecutorDiagnostics()
        {
            var ownershipDiagnostics = _coreGameplaySfxExecutionGuard.Diagnostics;
            var adapterDiagnostics = _coreGameplaySfxPlaybackPortAdapter.Diagnostics;
            if (_coreGameplaySfxExecutionPipeline == null)
            {
                return new GameplaySfxExecutorDiagnostics(
                    ownershipDiagnostics.Mode,
                    ownershipDiagnostics.Mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor,
                    ownershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount,
                    observedCueCount: 0,
                    semanticUnsupportedCount: 0,
                    mapMissingCount: 0,
                    bindingMissingCount: 0,
                    targetMissingCount: 0,
                    ownerViewMissingCount: 0,
                    portMissingCount: 0,
                    duplicateSuppressedCount: ownershipDiagnostics.DuplicateAttemptCount,
                    legacyOwnerNoOpCount: ownershipDiagnostics.SkippedExecutorBecauseLegacyOwnerCount,
                    requestPlannedCount: 0,
                    playbackRequestedCount: 0,
                    playbackSucceededCount: 0,
                    playbackNoOpFallbackCount: 0,
                    fallbackCount: 0,
                    attachedLikePlaybackCount: adapterDiagnostics.AttachedLikePlaybackCount,
                    twoDFallbackPlaybackCount: adapterDiagnostics.TwoDFallbackPlaybackCount,
                    deferredDuringTopologyLockCount: adapterDiagnostics.DeferredDuringTopologyLockCount,
                    deferredDrainCount: adapterDiagnostics.DeferredDrainCount,
                    enemyDeathGenericCoreSfxSuppressedCount: adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount,
                    lethalEnemyDamageSuppressedByDeathCount: adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount,
                    lastTickIndex: adapterDiagnostics.LastTickIndex,
                    lastSemanticKey: adapterDiagnostics.LastSemanticKey,
                    lastFallbackReason: adapterDiagnostics.LastFallbackReason,
                    semanticDiagnostics: Array.Empty<GameplaySfxSemanticDiagnostics>());
            }

            var executors = _coreGameplaySfxExecutionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplaySfxPresentationExecutor executor)
                {
                    return MergeCoreGameplaySfxDiagnostics(
                        executor.Diagnostics,
                        ownershipDiagnostics,
                        adapterDiagnostics);
                }
            }

            return new GameplaySfxExecutorDiagnostics(
                ownershipDiagnostics.Mode,
                ownershipDiagnostics.Mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor,
                ownershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount,
                observedCueCount: 0,
                semanticUnsupportedCount: 0,
                mapMissingCount: 0,
                bindingMissingCount: 0,
                targetMissingCount: 0,
                ownerViewMissingCount: 0,
                portMissingCount: 0,
                duplicateSuppressedCount: ownershipDiagnostics.DuplicateAttemptCount,
                legacyOwnerNoOpCount: ownershipDiagnostics.SkippedExecutorBecauseLegacyOwnerCount,
                requestPlannedCount: 0,
                playbackRequestedCount: 0,
                playbackSucceededCount: 0,
                playbackNoOpFallbackCount: 0,
                fallbackCount: 0,
                attachedLikePlaybackCount: adapterDiagnostics.AttachedLikePlaybackCount,
                twoDFallbackPlaybackCount: adapterDiagnostics.TwoDFallbackPlaybackCount,
                deferredDuringTopologyLockCount: adapterDiagnostics.DeferredDuringTopologyLockCount,
                deferredDrainCount: adapterDiagnostics.DeferredDrainCount,
                enemyDeathGenericCoreSfxSuppressedCount: adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount,
                lethalEnemyDamageSuppressedByDeathCount: adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount,
                lastTickIndex: adapterDiagnostics.LastTickIndex,
                lastSemanticKey: adapterDiagnostics.LastSemanticKey,
                lastFallbackReason: adapterDiagnostics.LastFallbackReason,
                semanticDiagnostics: Array.Empty<GameplaySfxSemanticDiagnostics>());
        }

        private static GameplaySfxExecutorDiagnostics MergeCoreGameplaySfxDiagnostics(
            GameplaySfxExecutorDiagnostics executorDiagnostics,
            CoreGameplaySfxOwnershipDiagnostics ownershipDiagnostics,
            GameplaySfxPlaybackAdapterDiagnostics adapterDiagnostics)
        {
            var adapterFallbackCount =
                adapterDiagnostics.TwoDFallbackPlaybackCount +
                adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount +
                adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount;
            var lastFallbackReason = adapterDiagnostics.LastFallbackReason != GameplaySfxFallbackReason.None
                ? adapterDiagnostics.LastFallbackReason
                : executorDiagnostics.LastFallbackReason;
            var lastSemanticKey = adapterDiagnostics.LastSemanticKey != PresentationSfxCueKey.None
                ? adapterDiagnostics.LastSemanticKey
                : executorDiagnostics.LastSemanticKey;
            var lastTickIndex = adapterDiagnostics.LastTickIndex > 0
                ? adapterDiagnostics.LastTickIndex
                : executorDiagnostics.LastTickIndex;

            return new GameplaySfxExecutorDiagnostics(
                ownershipDiagnostics.Mode,
                ownershipDiagnostics.Mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor,
                ownershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount,
                executorDiagnostics.ObservedCueCount,
                executorDiagnostics.SemanticUnsupportedCount,
                executorDiagnostics.MapMissingCount,
                executorDiagnostics.BindingMissingCount,
                executorDiagnostics.TargetMissingCount,
                executorDiagnostics.OwnerViewMissingCount,
                executorDiagnostics.PortMissingCount,
                Math.Max(
                    executorDiagnostics.DuplicateSuppressedCount,
                    ownershipDiagnostics.DuplicateAttemptCount),
                executorDiagnostics.LegacyOwnerNoOpCount,
                executorDiagnostics.RequestPlannedCount,
                executorDiagnostics.PlaybackRequestedCount,
                executorDiagnostics.PlaybackSucceededCount,
                executorDiagnostics.PlaybackNoOpFallbackCount,
                Math.Max(executorDiagnostics.FallbackCount, adapterFallbackCount),
                adapterDiagnostics.AttachedLikePlaybackCount,
                adapterDiagnostics.TwoDFallbackPlaybackCount,
                adapterDiagnostics.DeferredDuringTopologyLockCount,
                adapterDiagnostics.DeferredDrainCount,
                adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount,
                adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount,
                lastTickIndex,
                lastSemanticKey,
                lastFallbackReason,
                executorDiagnostics.SemanticDiagnostics);
        }

        private GameplayActionAudioExecutorDiagnostics ResolveActionAudioExecutorDiagnostics()
        {
            if (_actionAudioExecutionPipeline == null)
            {
                return default;
            }

            var executors = _actionAudioExecutionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayActionAudioPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        private GameplayEnemyAudioExecutorDiagnostics ResolveEnemyAudioExecutorDiagnostics()
        {
            if (_enemyAudioExecutionPipeline == null)
            {
                return default;
            }

            var executors = _enemyAudioExecutionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayEnemyAudioPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        private static DamageDeathVfxExecutionMode NormalizeDamageDeathVfxExecutionMode(
            DamageDeathVfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(DamageDeathVfxExecutionMode), mode)
                ? mode
                : DamageDeathVfxExecutionMode.LegacyExtension;
        }

        private static BoxMotionPresentationExecutionMode NormalizeBoxMotionPresentationExecutionMode(
            BoxMotionPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(BoxMotionPresentationExecutionMode), mode)
                ? mode
                : BoxMotionPresentationExecutionMode.LegacyTrackPlanner;
        }

        private static PlayerActionAnimationExecutionMode NormalizePlayerActionAnimationExecutionMode(
            PlayerActionAnimationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), mode)
                ? mode
                : PlayerActionAnimationExecutionMode.LegacyAnimationSync;
        }

        private static EnemyPresentationExecutionMode NormalizeEnemyPresentationExecutionMode(
            EnemyPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyPresentationExecutionMode), mode)
                ? mode
                : EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper;
        }

        private static CoreGameplaySfxExecutionMode NormalizeCoreGameplaySfxExecutionMode(
            CoreGameplaySfxExecutionMode mode)
        {
            return Enum.IsDefined(typeof(CoreGameplaySfxExecutionMode), mode)
                ? mode
                : CoreGameplaySfxExecutionMode.LegacyGameplayAudioController;
        }

        private static ActionAudioExecutionMode NormalizeActionAudioExecutionMode(
            ActionAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(ActionAudioExecutionMode), mode)
                ? mode
                : ActionAudioExecutionMode.LegacyActionAudioController;
        }

        private static EnemyAudioExecutionMode NormalizeEnemyAudioExecutionMode(
            EnemyAudioExecutionMode mode)
        {
            return Enum.IsDefined(typeof(EnemyAudioExecutionMode), mode)
                ? mode
                : EnemyAudioExecutionMode.LegacyEnemyAudioController;
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
