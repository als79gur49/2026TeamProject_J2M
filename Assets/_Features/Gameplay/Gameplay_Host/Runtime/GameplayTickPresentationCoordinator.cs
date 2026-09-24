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
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct BoxMotionPresentationRuntimeDebugSnapshot
    {
        public BoxMotionPresentationRuntimeDebugSnapshot(
            int activeLocalMotionTrackCount,
            int activeOriginalViewMotionTrackCount,
            int completedPresentationMotionKeyCount,
            int completedMotionTrackCount,
            int completedOriginalViewMotionTrackCount,
            int motionVisualScaleEntityCount,
            int flipInteractionTrackCount,
            int flipInteractionResetRequestCount,
            int completedFlipInteractionTrackCount,
            GameplayMotionTrackPlannerPlaybackPortDiagnostics defaultAdapterDiagnostics)
        {
            ActiveLocalMotionTrackCount = Math.Max(0, activeLocalMotionTrackCount);
            ActiveOriginalViewMotionTrackCount = Math.Max(0, activeOriginalViewMotionTrackCount);
            CompletedPresentationMotionKeyCount = Math.Max(0, completedPresentationMotionKeyCount);
            CompletedMotionTrackCount = Math.Max(0, completedMotionTrackCount);
            CompletedOriginalViewMotionTrackCount = Math.Max(0, completedOriginalViewMotionTrackCount);
            MotionVisualScaleEntityCount = Math.Max(0, motionVisualScaleEntityCount);
            FlipInteractionTrackCount = Math.Max(0, flipInteractionTrackCount);
            FlipInteractionResetRequestCount = Math.Max(0, flipInteractionResetRequestCount);
            CompletedFlipInteractionTrackCount = Math.Max(0, completedFlipInteractionTrackCount);
            DefaultAdapterDiagnostics = defaultAdapterDiagnostics;
        }

        public int ActiveLocalMotionTrackCount { get; }

        public int ActiveOriginalViewMotionTrackCount { get; }

        public int CompletedPresentationMotionKeyCount { get; }

        public int CompletedMotionTrackCount { get; }

        public int CompletedOriginalViewMotionTrackCount { get; }

        public int MotionVisualScaleEntityCount { get; }

        public int FlipInteractionTrackCount { get; }

        public int FlipInteractionResetRequestCount { get; }

        public int CompletedFlipInteractionTrackCount { get; }

        public GameplayMotionTrackPlannerPlaybackPortDiagnostics DefaultAdapterDiagnostics { get; }
    }

    internal readonly struct BoxMotionProductionTelemetrySnapshot
    {
        public BoxMotionProductionTelemetrySnapshot(
            int lastTickIndex,
            PresentationMotionCueKey lastCueKey,
            int lastDedupeKey,
            int lastTargetEntityId,
            PresentationMotionFactKind lastMotionFactKind,
            BoxMotionTelemetryFailureReason lastFailureReason,
            BoxMotionTelemetryCleanupReason lastCleanupReason,
            int executorOwnerAttemptCount,
            int executorOwnerExecutedCount,
            int duplicateOwnerAttemptCount,
            int duplicateRejectedCount,
            int playbackTrackPlannedCount,
            int playbackTrackRequestedCount,
            int playbackTrackStartedCount,
            int playbackTrackCompletedCount,
            int playbackTrackCanceledCount,
            int playbackTrackIgnoredCount,
            int activeTrackCount,
            int pendingTrackCount,
            int completedTrackKeyCount,
            GameplayMotionTrackPlannerPlaybackPortDiagnostics adapterDiagnostics,
            BoxMotionCleanupDiagnostics cleanupDiagnostics,
            int targetMissingCount,
            int anchorMissingCount,
            int bindingMissingCount,
            int driverMissingCount,
            int portMissingCount,
            int unsupportedSemanticCount,
            IReadOnlyList<BoxMotionSemanticDiagnostics> semanticDiagnostics)
        {
            IsCurrentProductionOwner = true;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastCueKey = lastCueKey;
            LastDedupeKey = lastDedupeKey;
            LastTargetEntityId = Math.Max(0, lastTargetEntityId);
            LastMotionFactKind = lastMotionFactKind;
            LastFailureReason = lastFailureReason;
            LastCleanupReason = lastCleanupReason;
            ExecutorOwnerAttemptCount = Math.Max(0, executorOwnerAttemptCount);
            ExecutorOwnerExecutedCount = Math.Max(0, executorOwnerExecutedCount);
            DuplicateOwnerAttemptCount = Math.Max(0, duplicateOwnerAttemptCount);
            DuplicateRejectedCount = Math.Max(0, duplicateRejectedCount);
            PlaybackTrackPlannedCount = Math.Max(0, playbackTrackPlannedCount);
            PlaybackTrackRequestedCount = Math.Max(0, playbackTrackRequestedCount);
            PlaybackTrackStartedCount = Math.Max(0, playbackTrackStartedCount);
            PlaybackTrackCompletedCount = Math.Max(0, playbackTrackCompletedCount);
            PlaybackTrackCanceledCount = Math.Max(0, playbackTrackCanceledCount);
            PlaybackTrackIgnoredCount = Math.Max(0, playbackTrackIgnoredCount);
            ActiveTrackCount = Math.Max(0, activeTrackCount);
            PendingTrackCount = Math.Max(0, pendingTrackCount);
            CompletedTrackKeyCount = Math.Max(0, completedTrackKeyCount);
            AdapterDiagnostics = adapterDiagnostics;
            CleanupDiagnostics = cleanupDiagnostics;
            TargetMissingCount = Math.Max(0, targetMissingCount);
            AnchorMissingCount = Math.Max(0, anchorMissingCount);
            BindingMissingCount = Math.Max(0, bindingMissingCount);
            DriverMissingCount = Math.Max(0, driverMissingCount);
            PortMissingCount = Math.Max(0, portMissingCount);
            UnsupportedSemanticCount = Math.Max(0, unsupportedSemanticCount);
            SemanticDiagnostics = semanticDiagnostics ?? Array.Empty<BoxMotionSemanticDiagnostics>();
        }

        public bool IsCurrentProductionOwner { get; }
        public int LastTickIndex { get; }
        public PresentationMotionCueKey LastCueKey { get; }
        public int LastDedupeKey { get; }
        public int LastTargetEntityId { get; }
        public PresentationMotionFactKind LastMotionFactKind { get; }
        public BoxMotionTelemetryFailureReason LastFailureReason { get; }
        public BoxMotionTelemetryCleanupReason LastCleanupReason { get; }
        public int ExecutorOwnerAttemptCount { get; }
        public int ExecutorOwnerExecutedCount { get; }
        public int DuplicateOwnerAttemptCount { get; }
        public int DuplicateRejectedCount { get; }
        public int PlaybackTrackPlannedCount { get; }
        public int PlaybackTrackRequestedCount { get; }
        public int PlaybackTrackStartedCount { get; }
        public int PlaybackTrackCompletedCount { get; }
        public int PlaybackTrackCanceledCount { get; }
        public int PlaybackTrackIgnoredCount { get; }
        public int ActiveTrackCount { get; }
        public int PendingTrackCount { get; }
        public int CompletedTrackKeyCount { get; }
        public GameplayMotionTrackPlannerPlaybackPortDiagnostics AdapterDiagnostics { get; }
        public BoxMotionCleanupDiagnostics CleanupDiagnostics { get; }
        public int TargetMissingCount { get; }
        public int AnchorMissingCount { get; }
        public int BindingMissingCount { get; }
        public int DriverMissingCount { get; }
        public int PortMissingCount { get; }
        public int UnsupportedSemanticCount { get; }
        public IReadOnlyList<BoxMotionSemanticDiagnostics> SemanticDiagnostics { get; }
    }

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

        private readonly GameplayAnimationSyncCoordinator _animationSync;
        private readonly GameplayActionAudioLaneRuntime _actionAudioLane;
        private readonly EnemyOneShotAudioLaneRuntime _enemyOneShotAudioLane;
        private readonly EnemyChargeLoopAudioPresentationController _enemyChargeLoopAudioPresentationController;
        private readonly BlockAudioRequestPlanner _blockAudioRequestPlanner;
        private readonly BlockAudioPresentationController _blockAudioPresentationController;
        private readonly PlayerLocomotionAudioPresentationController _playerLocomotionAudioPresentationController;
        private readonly CoreGameplaySfxLaneRuntime _coreGameplaySfxLane;
        private readonly GameplayCommittedFrameBuilder _committedFrameBuilder;
        private StageStaticWallPresentationProvenance _staticWallPresentationProvenance =
            StageStaticWallPresentationProvenance.Empty;
        private readonly GameplayEntityPresentationApplier _entityPresentationApplier;
        private readonly IEnemyVisualSemanticResolver _enemyVisualSemanticResolver;
        private readonly GameplayExitPresentationController _exitPresentationController;
        private readonly MoonBlockDestructionPresentationController _moonBlockDestructionPresentationController;
        private readonly GameplayTrackPlanner _planner;
        private readonly GameplayPresentationActivityInspector _presentationActivityInspector;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GravityFieldAudioRequestPlanner _gravityFieldAudioRequestPlanner;
        private readonly GravityFieldAudioPresentationController _gravityFieldAudioPresentationController;
        private readonly GravityFieldPresentationRequestPlanner _gravityFieldPresentationRequestPlanner;
        private readonly GravityFieldVisualPresentationController _gravityFieldVisualPresentationController;
        private readonly SummonedEnemyPresentationResolver _summonedEnemyPresentationResolver;
        private readonly TileFeatureAudioRequestPlanner _tileFeatureAudioRequestPlanner;
        private readonly TileFeatureAudioPresentationController _tileFeatureAudioPresentationController;
        private readonly TopologyAudioRequestPlanner _topologyAudioRequestPlanner;
        private readonly TopologyAudioPresentationController _topologyAudioPresentationController;
        private readonly GameplayPresentationTrackState _trackState;
        private readonly PresentationPoseCandidateCollector _presentationPoseCandidateCollector;
        private readonly PresentationBasePoseFrameResolver _basePoseFrameResolver;
        private readonly PresentationResolvedChannelResolver _resolvedChannelResolver;
        private readonly PresentationVisibilityCandidateCollector _visibilityCandidateCollector;
        private readonly PresentationResolvedVisibilityResolver _resolvedVisibilityResolver;
        private readonly ResolvedPresentationFrameSet _resolvedPresentationFrames = new();
        private readonly ResolvedPresentationChannelSet _resolvedPresentationChannels = new();
        private readonly PresentationVisibilityCandidateSet _presentationVisibilityCandidates = new();
        private readonly ResolvedPresentationVisibilitySet _resolvedPresentationVisibility = new();
        private readonly TilePresentationRequestPlanner _tilePresentationRequestPlanner;
        private readonly GameplayTopologyTransitionController _topologyTransitionController;
        private readonly GameplayPresentationPauseRegistry _presentationPauseRegistry;
        private readonly GameplayUtilityWindupVfxPresenter _utilityWindupVfxPresenter;
        private readonly TileFeatureVisualPresentationController _tileFeatureVisualPresentationController;
        private readonly MoonBlockEmergencePresentationController _moonBlockEmergencePresentationController;
        private readonly GameplayMotionTimingResolver _motionTimingResolver;
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplaySfxArbiter _gameplaySfxArbiter;
        private readonly GameplayDestroyShrinkVfxSequenceStateResolver _destroyShrinkVfxSequenceStateResolver;
        private readonly GameplayPlayerActionAnimationTimingProfileSource _playerActionAnimationTimingProfileSource;
        private readonly List<IGameplayTickPresentationExtension> _presentationExtensions = new();
        private readonly TopologyPresentationLaneRuntime _topologyLane;
        private readonly DamageDeathVfxPresentationLaneRuntime _damageDeathVfxLane;
        private readonly PlayerActionAnimationLaneRuntime _playerActionAnimationLane;
        private readonly EnemyPresentationLaneRuntime _enemyPresentationLane;
        private readonly BoxMotionPresentationLaneRuntime _boxMotionLane;

        private GameplayPresentationPipeline _presentationPipeline;
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
        private int _playerEntityId;
        private Transform _boardPresentationRoot;
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
        private bool _hasTornDownPresentationRuntime;

        internal GameplayTickPresentationCoordinator(GameplayPresentationRuntimeComposition composition)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }

            _stateStore = composition.StateStore;
            _trackState = composition.TrackState;
            _presentationPoseCandidateCollector = new PresentationPoseCandidateCollector(_stateStore, _trackState);
            _basePoseFrameResolver = new PresentationBasePoseFrameResolver(_presentationPoseCandidateCollector);
            _resolvedChannelResolver = new PresentationResolvedChannelResolver(_stateStore, _trackState);
            _visibilityCandidateCollector = new PresentationVisibilityCandidateCollector(_stateStore, _trackState);
            _resolvedVisibilityResolver = new PresentationResolvedVisibilityResolver();
            _animationSync = composition.AnimationSync;
            _motionTimingResolver = composition.MotionTimingResolver;
            _poseResolver = composition.PoseResolver;
            _committedFrameBuilder = composition.CommittedFrameBuilder;
            _entityPresentationApplier = composition.EntityPresentationApplier;
            _enemyVisualSemanticResolver = composition.EnemyVisualSemanticResolver;
            _exitPresentationController = composition.ExitPresentationController;
            _moonBlockDestructionPresentationController = composition.MoonBlockDestructionPresentationController;
            _moonBlockEmergencePresentationController = composition.MoonBlockEmergencePresentationController;
            _planner = composition.TrackPlanner;
            _topologyTransitionController = composition.TopologyTransitionController;
            _presentationActivityInspector = composition.PresentationActivityInspector;
            _presentationPauseRegistry = composition.PresentationPauseRegistry;
            _destroyShrinkVfxSequenceStateResolver = composition.DestroyShrinkVfxSequenceStateResolver;
            _playerActionAnimationTimingProfileSource = composition.PlayerActionAnimationTimingProfileSource;
            _blockAudioRequestPlanner = composition.BlockAudioRequestPlanner;
            _blockAudioPresentationController = composition.BlockAudioPresentationController;
            _playerLocomotionAudioPresentationController =
                composition.PlayerLocomotionAudioPresentationController;
            _gravityFieldAudioRequestPlanner = composition.GravityFieldAudioRequestPlanner;
            _gravityFieldAudioPresentationController = composition.GravityFieldAudioPresentationController;
            _gravityFieldPresentationRequestPlanner = composition.GravityFieldPresentationRequestPlanner;
            _gravityFieldVisualPresentationController = composition.GravityFieldVisualPresentationController;
            _summonedEnemyPresentationResolver = composition.SummonedEnemyPresentationResolver;
            _tileFeatureAudioRequestPlanner = composition.TileFeatureAudioRequestPlanner;
            _tileFeatureAudioPresentationController = composition.TileFeatureAudioPresentationController;
            _topologyAudioRequestPlanner = composition.TopologyAudioRequestPlanner;
            _topologyAudioPresentationController = composition.TopologyAudioPresentationController;
            _tilePresentationRequestPlanner = composition.TilePresentationRequestPlanner;
            _utilityWindupVfxPresenter = composition.UtilityWindupVfxPresenter;
            _tileFeatureVisualPresentationController = composition.TileFeatureVisualPresentationController;
            _gameplaySfxArbiter = composition.GameplaySfxArbiter;
            _enemyChargeLoopAudioPresentationController =
                composition.EnemyChargeLoopAudioPresentationController;
            _enemyOneShotAudioLane = composition.EnemyOneShotAudioLane;
            _actionAudioLane = composition.GameplayActionAudioLane;
            _coreGameplaySfxLane = composition.CoreGameplaySfxLane;
            _playerActionAnimationLane = composition.PlayerActionAnimationLane;
            _enemyPresentationLane = composition.EnemyPresentationLane;
            _boxMotionLane = composition.BoxMotionLane;
            _topologyLane = composition.TopologyLane;
            _damageDeathVfxLane = composition.DamageDeathVfxLane;
            _destroyShrinkVfxSequenceStateResolver.BindExtensions(_presentationExtensions);
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

        public bool IsPlayerInteractionPlaybackActive(int entityId) =>
            _animationSync.IsPlayerInteractionHoldActive(entityId);

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

        internal int PendingGameplayAudioRequestCount => _coreGameplaySfxLane.PendingRequestCount;

        internal bool HasActiveJumpLandingCompletionTrack(int entityId)
        {
            return _trackState.JumpLandingCompletionHoldEntityIds.Contains(entityId) &&
                   _trackState.JumpTracks.TryGetValue(entityId, out var jumpTrack) &&
                   jumpTrack != null &&
                   jumpTrack.HasClip;
        }

        internal bool TryGetLiveEntityPresentationView(int entityId, out GameplayEntityView view)
        {
            if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out view) &&
                view != null &&
                view.gameObject.activeInHierarchy)
            {
                return true;
            }

            view = null;
            return false;
        }

        internal int DeferredGameplayAudioRequestCount =>
            _coreGameplaySfxLane.DeferredRequestCount +
            _enemyOneShotAudioLane.DeferredRequestCount;

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
            _topologyLane.BlockingSnapshot;

        internal TopologyPresentationOwnershipDiagnostics TopologyPresentationOwnershipDiagnostics =>
            _topologyLane.OwnershipDiagnostics;

        internal TopologyExecutorDiagnostics TopologyExecutorDiagnostics =>
            _topologyLane.ExecutorDiagnostics;

        internal TopologyProductionTelemetrySnapshot TopologyProductionTelemetrySnapshot =>
            _topologyLane.BuildProductionTelemetrySnapshot(
                HasBlockingPresentation,
                IsTopologyTransitionActive,
                PresentationPipelineBlockingSnapshot);

        internal DamageDeathVfxOwnershipDiagnostics DamageDeathVfxOwnershipDiagnostics =>
            _damageDeathVfxLane.OwnershipDiagnostics;

        internal BoxMotionOwnershipDiagnostics BoxMotionOwnershipDiagnostics =>
            _boxMotionLane.OwnershipDiagnostics;

        internal PlayerActionAnimationExecutionMode PlayerActionAnimationExecutionMode =>
            _playerActionAnimationLane.ExecutionMode;

        internal PlayerActionAnimationOwnershipDiagnostics PlayerActionAnimationOwnershipDiagnostics =>
            _playerActionAnimationLane.OwnershipDiagnostics;

        internal EnemyPresentationOwnershipDiagnostics EnemyPresentationOwnershipDiagnostics =>
            _enemyPresentationLane.OwnershipDiagnostics;

        internal CoreGameplaySfxRoute CoreGameplaySfxRoute =>
            CoreGameplaySfxRoute.CurrentExecutor;

        internal CoreGameplaySfxOwnershipDiagnostics CoreGameplaySfxOwnershipDiagnostics =>
            _coreGameplaySfxLane.OwnershipDiagnostics;

        internal PresentationBlockingSnapshot DamageDeathVfxExecutionPipelineBlockingSnapshot =>
            _damageDeathVfxLane.BlockingSnapshot;

        internal PresentationBlockingSnapshot BoxMotionExecutionPipelineBlockingSnapshot =>
            _boxMotionLane.BlockingSnapshot;

        internal PresentationBlockingSnapshot PlayerActionAnimationExecutionPipelineBlockingSnapshot =>
            _playerActionAnimationLane.BlockingSnapshot;

        internal PresentationBlockingSnapshot EnemyPresentationExecutionPipelineBlockingSnapshot =>
            _enemyPresentationLane.BlockingSnapshot;

        internal PresentationBlockingSnapshot CoreGameplaySfxExecutionPipelineBlockingSnapshot =>
            _coreGameplaySfxLane.BlockingSnapshot;

        internal PresentationBlockingSnapshot ActionAudioExecutionPipelineBlockingSnapshot =>
            _actionAudioLane.BlockingSnapshot;

        internal PresentationBlockingSnapshot EnemyAudioExecutionPipelineBlockingSnapshot =>
            _enemyOneShotAudioLane.BlockingSnapshot;

        internal DamageDeathVfxExecutorDiagnostics DamageDeathVfxExecutorDiagnostics =>
            _damageDeathVfxLane.ExecutorDiagnostics;

        internal GameplayMotionExecutorDiagnostics BoxMotionExecutorDiagnostics =>
            _boxMotionLane.ExecutorDiagnostics;

        internal BoxMotionPresentationRuntimeDebugSnapshot BoxMotionRuntimeDebugSnapshot =>
            _boxMotionLane.RuntimeDebugSnapshot;

        internal BoxMotionProductionTelemetrySnapshot BoxMotionProductionTelemetrySnapshot =>
            _boxMotionLane.ProductionTelemetrySnapshot;

        internal IReadOnlyList<MotionTrackProgressSample> MotionTrackProgressSamples =>
            _trackState.MotionTrackProgressSamples;

        internal bool HasActiveLocalMotionTrack(
            int entityId,
            TickEntityMotionKind motionKind,
            int sourceTickIndex,
            int sequenceOrActionPlanId)
        {
            return entityId > 0 &&
                   sequenceOrActionPlanId > 0 &&
                   _trackState.LocalMotionTracks.TryGetValue(entityId, out var track) &&
                   track != null &&
                   track.HasClips &&
                   track.HeadMotionKind == motionKind &&
                   track.HeadSourceTickIndex == sourceTickIndex &&
                   track.HeadSequenceOrActionPlanId == sequenceOrActionPlanId;
        }

        internal bool HasLocalMotionTrack(
            int entityId,
            TickEntityMotionKind motionKind,
            int sourceTickIndex,
            int sequenceOrActionPlanId)
        {
            return entityId > 0 &&
                   sequenceOrActionPlanId > 0 &&
                   _trackState.LocalMotionTracks.TryGetValue(entityId, out var track) &&
                   track != null &&
                   track.Contains(motionKind, sourceTickIndex, sequenceOrActionPlanId);
        }

        internal GameplayAnimationExecutorDiagnostics PlayerActionAnimationExecutorDiagnostics =>
            _playerActionAnimationLane.ExecutorDiagnostics;

        internal PlayerActionAnimationProductionTelemetrySnapshot PlayerActionAnimationProductionTelemetrySnapshot =>
            _playerActionAnimationLane.ProductionTelemetrySnapshot;

        internal GameplayEnemyPresentationExecutorDiagnostics EnemyPresentationExecutorDiagnostics =>
            _enemyPresentationLane.ExecutorDiagnostics;

        internal EnemyPresentationProductionTelemetrySnapshot EnemyPresentationProductionTelemetrySnapshot =>
            _enemyPresentationLane.ProductionTelemetrySnapshot;

        internal GameplaySfxExecutorDiagnostics CoreGameplaySfxExecutorDiagnostics =>
            _coreGameplaySfxLane.ExecutorDiagnostics;

        internal GameplayActionAudioExecutorDiagnostics ActionAudioExecutorDiagnostics =>
            _actionAudioLane.ExecutorDiagnostics;

        internal ActionAudioProductionTelemetrySnapshot ActionAudioProductionTelemetrySnapshot =>
            _actionAudioLane.ProductionTelemetrySnapshot;

        internal GameplayEnemyAudioExecutorDiagnostics EnemyAudioExecutorDiagnostics =>
            _enemyOneShotAudioLane.ExecutorDiagnostics;

        internal EnemyAudioProductionTelemetrySnapshot EnemyAudioProductionTelemetrySnapshot =>
            _enemyOneShotAudioLane.ProductionTelemetrySnapshot;

        internal void ConfigureDamageDeathVfxPlaybackPort(IDamageDeathVfxPlaybackPort playbackPort)
        {
            _damageDeathVfxLane.ConfigurePlaybackPort(playbackPort);
        }

        internal void ConfigureBoxMotionPlaybackPort(
            IGameplayMotionPlaybackPort playbackPort,
            bool useDefaultPlaybackPort = true)
        {
            _boxMotionLane.ConfigurePlaybackPort(playbackPort, useDefaultPlaybackPort);
        }

        internal void ConfigurePlayerActionAnimationExecution(
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort = null)
        {
            _playerActionAnimationLane.ConfigureExecution(mode, playbackPort);
        }

        internal void ConfigureEnemyPresentationPlaybackPort(
            IGameplayEnemyPresentationPlaybackPort playbackPort,
            bool useDefaultPlaybackPort = true)
        {
            _enemyPresentationLane.ConfigurePlaybackPort(playbackPort, useDefaultPlaybackPort);
        }

        internal void ConfigureCoreGameplaySfxPlaybackPort(
            IGameplaySfxPlaybackPort playbackPort,
            bool useDefaultPlaybackPort = true)
        {
            _coreGameplaySfxLane.ConfigurePlaybackPort(playbackPort, useDefaultPlaybackPort);
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
                _stateStore.RetainedLocalTargetPoses.ContainsKey(entityId),
                _exitPresentationController.HasPendingContactDelayedExit(entityId),
                pendingContactRemaining,
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
            EnemyInactiveVisualSettings enemyInactiveVisualSettings = null)
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
            _committedFrameBuilder.ResetSession(_staticWallPresentationProvenance, _projector);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _playerActionAnimationTimingProfileSource.TimingProfile = _timingProfile;
            _coreGameplaySfxLane.ConfigureTiming(_timingProfile);
            _damageDeathVfxLane.ConfigureTiming(_timingProfile);
            _enemyOneShotAudioLane.ConfigureTiming(_timingProfile);
            _topologyTransitionController.Configure(
                boardRoot,
                boardSurfaceRenderer,
                _timingProfile,
                topologyRotationVisualMapping,
                topologyRotationTweenSettings,
                () => CubeCenter);
            _exitPresentationController.Configure(_projector, _timingProfile);
            _summonedEnemyPresentationResolver.ResetSession();
            _exitPresentationController.Reset();
            _moonBlockDestructionPresentationController.ConfigureViewRegistry(viewBinder.ViewRegistry);
            _moonBlockDestructionPresentationController.ResetSession();
            SetGameplayAudioPlaybackGate(GameplayAudioPlaybackGateState.Open);
            _enemyChargeLoopAudioPresentationController.ResetSession();
            _blockAudioPresentationController.ResetSession();
            _playerLocomotionAudioPresentationController.ResetSession();
            _tileFeatureAudioPresentationController.ResetSession();
            _topologyAudioPresentationController.ResetSession();
            _gravityFieldAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _entityPresentationApplier.ResetEnemySemanticPresentationDriverCache();
            _boxMotionLane.ResetSession(BoxMotionTelemetryCleanupReason.ResetSession);
            _trackState.ResetSession();
            _resolvedPresentationFrames.Clear();
            _resolvedPresentationChannels.Clear();
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
            ResetTypedPresentationLanesExceptBox();
            ObserveTopologyActiveStateForPresentationPipelines(_lastPresentedTickIndex);
            ResetPresentationPipelineDiagnosticsIfEnabled();
        }

        internal void ConfigureStaticWallPresentationProvenance(
            StageStaticWallPresentationProvenance provenance)
        {
            _staticWallPresentationProvenance =
                provenance ?? StageStaticWallPresentationProvenance.Empty;
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

        public void ConfigurePlayerAnchorContext(int playerEntityId, Transform boardPresentationRoot)
        {
            _playerEntityId = playerEntityId;
            _boardPresentationRoot = boardPresentationRoot;
            ConfigurePlayerAnchorPresentationExtensions();
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
            if (_playerEntityId > 0 &&
                _boardPresentationRoot != null &&
                extension is IGameplayPlayerAnchoredPresentationExtension playerAnchoredExtension)
            {
                playerAnchoredExtension.ConfigurePlayerAnchorContext(
                    _playerEntityId,
                    _boardPresentationRoot);
            }

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
                TopologyCommitted,
                result.PresentationData,
                result.TickIndex,
                CommittedFrameStoreReason.Tick);
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
            _topologyLane.Present(result);
            ObserveTopologyActiveStateForPresentationPipelines(result.TickIndex);
            RefreshGameplayAudioPlaybackGate();
            _topologyAudioPresentationController.ReplacePendingPlan(
                _topologyAudioRequestPlanner.BuildRequests(result));
            var boxMotionPreparation = _boxMotionLane.Prepare(
                result,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology,
                _projector,
                _timingProfile);
            _planner.RefreshTracks(
                result,
                previousCommittedLocalTargetPoses,
                previousCommittedTopology,
                _projector,
                _timingProfile);
            _boxMotionLane.PresentPrepared(result, boxMotionPreparation, 0f);
            RetainTopologyMoonBlockGeneratedPoses(result.PresentationData);
            _lastPresentedTickIndex = result.TickIndex;
            RefreshPresentationMotionVfx(result.TickIndex);
            var playerActionAnimationPreparation = _playerActionAnimationLane.Prepare(result);
            var enemyPresentationPreparation = _enemyPresentationLane.Prepare(result);
            _animationSync.ApplyTickPresentation(
                result,
                _stateStore.ViewsByEntityId,
                _trackState.JumpLandingCompletionHoldEntityIds,
                (entityId, actionKind) => _motionTimingResolver.ResolvePlayerMotionDurationSeconds(
                    entityId,
                    actionKind,
                    _timingProfile),
                playerActionAnimationPreparation.SuppressPlayerActionFieldsInSharedSync,
                enemyPresentationPreparation.OneShotBlockMask);
            _enemyPresentationLane.PresentPrepared(result, enemyPresentationPreparation);
            _playerActionAnimationLane.PresentPrepared(result, playerActionAnimationPreparation);
            _damageDeathVfxLane.Present(result);
            PresentExtensions(result);
            TraceStep("PlayPlannedAudio");
            _arbitratingGameplayAudioPlaybackPort?.BeginBatch(
                result.TickIndex,
                _timingProfile.SimulationTicksPerSecond);
            try
            {
                _coreGameplaySfxLane.PresentPrepared(result);
                _actionAudioLane.PresentPrepared(result);
                _enemyOneShotAudioLane.PresentPrepared(result);
                _coreGameplaySfxLane.CompletePrepared();
                _actionAudioLane.CompletePrepared();
                _enemyOneShotAudioLane.CompletePrepared();
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
            SetGameplayAudioPlaybackGate(GameplayAudioPlaybackGateState.Open);
            _enemyChargeLoopAudioPresentationController.ResetSession();
            _blockAudioPresentationController.ResetSession();
            _playerLocomotionAudioPresentationController.ResetSession();
            _tileFeatureAudioPresentationController.ResetSession();
            _topologyAudioPresentationController.ResetSession();
            _gravityFieldAudioPresentationController.ResetSession();
            _entityPresentationApplier.ResetAllPlayerDeathDisplacements();
            _entityPresentationApplier.ResetEnemySemanticPresentationDriverCache();
            _boxMotionLane.ResetSession(BoxMotionTelemetryCleanupReason.PresentInitial);
            _summonedEnemyPresentationResolver.ResetSession();
            _trackState.ResetSession();
            _resolvedPresentationFrames.Clear();
            _resolvedPresentationChannels.Clear();
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
            ResetTypedPresentationLanesExceptBox();
            ObserveTopologyActiveStateForPresentationPipelines(_lastPresentedTickIndex);
            _lastPresentedResult = null;
            _topologyTransitionEpoch = 0;
            ResetPresentationPipelineDiagnosticsIfEnabled();

            _committedFrameBuilder.ResetSession(_staticWallPresentationProvenance, _projector);

            _committedFrameBuilder.StoreCommittedFrame(
                entities,
                topology,
                _projector,
                _viewBinder,
                TopologyCommitted,
                presentationData: null,
                tickIndex: -1,
                reason: CommittedFrameStoreReason.Initial);
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
            _coreGameplaySfxLane.Update(deltaTime);
            _actionAudioLane.Update(deltaTime);
            _enemyOneShotAudioLane.Update(_lastPresentedTickIndex, deltaTime);
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
            _basePoseFrameResolver.Resolve(_lastPresentedTickIndex, _resolvedPresentationFrames);
            _resolvedPresentationChannels.Clear();
            _presentationVisibilityCandidates.Clear();
            _resolvedPresentationVisibility.Clear();
            _trackState.CompletedJumpTrackIds.Clear();
            _trackState.CompletedJumpWindupRotationTrackIds.Clear();
            _trackState.CompletedPlayerFlipResultTurnTrackIds.Clear();
            _resolvedChannelResolver.ResolveAdditiveChannels(
                deltaTime,
                hadActiveBoardRotationTween || _topologyTransitionController.HasActiveBoardRotationTween,
                _lastPresentedTickIndex,
                _resolvedPresentationFrames,
                _resolvedPresentationChannels);
            _visibilityCandidateCollector.CollectJumpDetachedVisibility(
                _stateStore.CommittedTopology,
                _lastPresentedTickIndex,
                _resolvedPresentationFrames,
                _presentationVisibilityCandidates);
            _visibilityCandidateCollector.CollectRetainedDeathOrExitVisibility(
                _lastPresentedTickIndex,
                _resolvedPresentationFrames,
                _presentationVisibilityCandidates);
            _visibilityCandidateCollector.CollectTransitionEntityVisibility(
                _lastPresentedTickIndex,
                _presentationVisibilityCandidates);
            var tickVisibilityChanges = _lastPresentedResult?.PresentationData?.VisibilityChanges;
            if (tickVisibilityChanges != null)
            {
                _visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly(
                    tickVisibilityChanges,
                    _lastPresentedTickIndex,
                    _presentationVisibilityCandidates);
            }
            _visibilityCandidateCollector.CollectVisibilityTrackSamples(
                deltaTime,
                _lastPresentedTickIndex,
                _resolvedPresentationFrames,
                _presentationVisibilityCandidates);
            _resolvedVisibilityResolver.ResolveCandidates(
                _presentationVisibilityCandidates,
                _resolvedPresentationVisibility);
            if (tickVisibilityChanges != null)
            {
                // Post-resolve carrier/shadow collection only. These carriers must not feed the
                // production final visibility resolver; Detach/Remove hide timing is owned by
                // VisibilityTrackSample.
                _visibilityCandidateCollector.CollectPostResolveVisibilityCarriers(
                    tickVisibilityChanges,
                    _lastPresentedTickIndex,
                    _presentationVisibilityCandidates);
            }
            _entityPresentationApplier.Apply(
                deltaTime,
                hadActiveBoardRotationTween || _topologyTransitionController.HasActiveBoardRotationTween,
                _resolvedPresentationFrames,
                _resolvedPresentationChannels,
                _resolvedPresentationVisibility,
                _viewBinder,
                _timingProfile);
            ObserveMotionProgressExtensions();
            _exitPresentationController.CompleteDeferredEntityExits();
            RefreshPresentationMotionVfx(_lastPresentedTickIndex);
            _moonBlockDestructionPresentationController.UpdateSequences(
                _moonBlockEmergencePresentationController,
                _lastPresentedTickIndex);
            _exitPresentationController.AdvanceContactDelayedEntityExits(deltaTime);
            RefreshPresentationMotionVfx(_lastPresentedTickIndex);
            _moonBlockDestructionPresentationController.UpdateSequences(
                _moonBlockEmergencePresentationController,
                _lastPresentedTickIndex);
            _moonBlockEmergencePresentationController.StartReadyRequests(_lastPresentedTickIndex);
            _damageDeathVfxLane.Update(deltaTime);
            _boxMotionLane.Update(deltaTime);
            _playerActionAnimationLane.Update(deltaTime);
            _enemyPresentationLane.Update(deltaTime);
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
            _coreGameplaySfxLane.AttachRuntime(arbitratingPort, gameplayAudioMap);
            _actionAudioLane.AttachRuntime(arbitratingPort);
            _enemyOneShotAudioLane.AttachRuntime(arbitratingPort);
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
            _actionAudioLane.DetachRuntime();
            _enemyOneShotAudioLane.DetachRuntime();
            _enemyChargeLoopAudioPresentationController.DetachRuntime();
            _coreGameplaySfxLane.DetachRuntime();
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
            var enemyOneShotPlan = _enemyOneShotAudioLane.RefreshPlan(result, _timingProfile);
            _coreGameplaySfxLane.PrepareCurrentRoute(
                result,
                _topologyTransitionController.HasActiveBoardRotationTween,
                enemyOneShotPlan.PlayableDeathCueEntityIds);

            _actionAudioLane.RefreshPlan(result);

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
            _coreGameplaySfxLane.SetTopologyTransitionActive(gateState.IsBlocked);
            _enemyOneShotAudioLane.SetPlaybackGateState(gateState);
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
            if (_isInitialized && _stateStore.HasAnyCommittedFrame)
            {
                _topologyTransitionController.HardCleanup(_stateStore.CommittedTopology);
            }

            _moonBlockDestructionPresentationController.Dispose();
            _moonBlockEmergencePresentationController.Dispose();
            HardCleanupTypedPresentationLanes();
            _enemyChargeLoopAudioPresentationController.ResetSession();
            _presentationPipeline?.HardCleanup();
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                _presentationExtensions[i]?.HardCleanup();
            }
        }

        private void ResetTypedPresentationLanesExceptBox()
        {
            _topologyLane.ResetSession();
            _damageDeathVfxLane.ResetSession();
            _playerActionAnimationLane.ResetSession();
            _enemyPresentationLane.ResetSession();
            _coreGameplaySfxLane.ResetSession();
            _actionAudioLane.ResetSession();
            _enemyOneShotAudioLane.ResetSession();
        }

        private void HardCleanupTypedPresentationLanes()
        {
            _topologyLane.HardCleanup();
            _damageDeathVfxLane.HardCleanup();
            _boxMotionLane.HardCleanup(BoxMotionTelemetryCleanupReason.HardCleanupPresentationExtensions);
            _playerActionAnimationLane.HardCleanup();
            _enemyPresentationLane.HardCleanup();
            _coreGameplaySfxLane.HardCleanup();
            _actionAudioLane.HardCleanup();
            _enemyOneShotAudioLane.HardCleanup();
        }

        internal void TeardownPresentationRuntime()
        {
            if (_hasTornDownPresentationRuntime)
            {
                return;
            }

            List<Exception> cleanupExceptions = null;
            TryTeardownStep(_summonedEnemyPresentationResolver.ResetSession, ref cleanupExceptions);
            TryTeardownStep(HardCleanupPresentationExtensions, ref cleanupExceptions);
            TryTeardownStep(DetachBlockAudioRuntime, ref cleanupExceptions);
            TryTeardownStep(DetachGravityFieldAudioRuntime, ref cleanupExceptions);
            TryTeardownStep(DetachTileFeatureAudioRuntime, ref cleanupExceptions);
            TryTeardownStep(DetachTopologyAudioRuntime, ref cleanupExceptions);
            TryTeardownStep(DetachGameplayAudioRuntime, ref cleanupExceptions);

            if (cleanupExceptions == null)
            {
                _hasTornDownPresentationRuntime = true;
                return;
            }

            if (cleanupExceptions.Count == 1)
            {
                throw cleanupExceptions[0];
            }

            throw new AggregateException(
                "One or more presentation runtime teardown steps failed.",
                cleanupExceptions);
        }

        private static void TryTeardownStep(Action cleanupStep, ref List<Exception> cleanupExceptions)
        {
            try
            {
                cleanupStep();
            }
            catch (Exception exception)
            {
                cleanupExceptions ??= new List<Exception>();
                cleanupExceptions.Add(exception);
            }
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

            _topologyLane.ObserveControllerActivity(isTopologyActive, tickIndex);
            _actionAudioLane.ObserveTopologyActiveState(isTopologyActive, tickIndex);
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

        private void ObserveMotionProgressExtensions()
        {
            if (_trackState.MotionTrackProgressSamples.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayMotionProgressPresentationExtension motionProgressExtension)
                {
                    motionProgressExtension.ObserveMotionProgress(_trackState.MotionTrackProgressSamples);
                }
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

        private void ConfigurePlayerAnchorPresentationExtensions()
        {
            for (var i = 0; i < _presentationExtensions.Count; i++)
            {
                if (_presentationExtensions[i] is IGameplayPlayerAnchoredPresentationExtension playerAnchoredExtension)
                {
                    playerAnchoredExtension.ConfigurePlayerAnchorContext(
                        _playerEntityId,
                        _boardPresentationRoot);
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
            bool retainedLocalTargetPosesContainsEntityId,
            bool pendingContactExitContainsEntityId,
            float pendingContactExitRemainingSeconds,
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
            RetainedLocalTargetPosesContainsEntityId = retainedLocalTargetPosesContainsEntityId;
            PendingContactExitContainsEntityId = pendingContactExitContainsEntityId;
            PendingContactExitRemainingSeconds = pendingContactExitRemainingSeconds;
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

        public bool RetainedLocalTargetPosesContainsEntityId { get; }

        public bool PendingContactExitContainsEntityId { get; }

        public float PendingContactExitRemainingSeconds { get; }

        public bool IsVfxPooledInstance { get; }

        public bool RendererEnabled { get; }

        public bool RendererActiveInHierarchy { get; }

        public int AnimatorCurrentStateShortNameHash { get; }

        public float AnimatorNormalizedTime { get; }

        public int DeathTriggerCount { get; }
    }

}
