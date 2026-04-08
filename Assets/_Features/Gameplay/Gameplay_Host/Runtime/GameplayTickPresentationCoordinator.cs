using System;
using System.Collections.Generic;
using DG.Tweening;
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
        private static readonly bool EnableUnitPresentationPlaneOffsets = false;
        private const float DefaultJumpArcHeightInCells = 0.75f;
        private const float MinimumTopologyTweenDurationSeconds = 0.0001f;
        private const float UnitPresentationOffsetRadiusInCells = 0.2f;
        private const float UnitPresentationSquareHalfExtentInCells = 0.14f;

        private readonly List<int> _completedJumpTrackIds = new();
        private readonly GameplayAnimationSyncCoordinator _animationSync = new();
        private readonly List<int> _completedMotionTrackIds = new();
        private readonly List<int> _completedTransitionVisibilityStateIds = new();
        private readonly List<int> _completedVisibilityTrackIds = new();
        private readonly IEnemyVisualSemanticResolver _enemyVisualSemanticResolver = new DefaultEnemyVisualSemanticResolver();
        private readonly HashSet<int> _exitOwnedEntityIds = new();
        private readonly Dictionary<int, JumpTrack> _jumpTracks = new();
        private readonly Dictionary<int, MotionTrack> _localMotionTracks = new();
        private readonly List<TickEntityExitPresentationSignal> _pendingEntityExitSignals = new();
        private readonly Dictionary<int, TickPlayerLocomotionPresentationSignal> _playerLocomotionSignalsByEntityId = new();
        private readonly GameplayPresentationStateStore _stateStore = new();
        private readonly GameplayTransientEffectPresenter _transientEffectPresenter = new();
        private readonly HashSet<int> _visibleEntityIds = new();
        private readonly Dictionary<int, VisibilityTrack> _visibilityTracks = new();

        private GameplayBoardRoot _boardRoot;
        private Tween _boardRotationTween;
        private GameplayBoardSurfaceRenderer _boardSurfaceRenderer;
        private bool _isBoardRotationTweenActive;
        private bool _isBoardSurfaceTransitionActive;
        private bool _isInitialized;
        private Quaternion _boardSurfaceTransitionStartRotation = Quaternion.identity;
        private Quaternion _presentedBoardRotation = Quaternion.identity;
        private GameplayCubeProjector _projector;
        private GameplayTimingProfile _timingProfile;
        private TopologyRotationTweenSettings _topologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault();
        private TopologyRotationVisualMapping _topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX;
        private GameplayEntityViewBinder _viewBinder;

        public event Action<CubeTopologyState> TopologyCommitted;

        public CubeTopologyState CurrentTopology => _stateStore.CommittedTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => ResolveCurrentPresentationPhase();

        public Vector3 CubeCenter => _projector != null ? _projector.GetCubeCenter() : Vector3.zero;

        public bool HasBlockingPresentation => HasActiveBoardRotationTween();

        public bool IsInitialized => _isInitialized;

        public bool IsPresentationActive => CurrentPresentationPhase != GameplayPresentationPhase.Idle;

        public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;

        public int ActiveTransientEffectCount => _transientEffectPresenter.ActiveEffectCount;

        public Quaternion PresentedBoardRotation => _presentedBoardRotation;

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
            TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX,
            TopologyRotationTweenSettings topologyRotationTweenSettings = default)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _boardRoot = boardRoot;
            _boardSurfaceRenderer = boardSurfaceRenderer != null ? boardSurfaceRenderer : boardRoot?.BoardSurfaceRenderer;
            _viewBinder = viewBinder;
            _projector = new GameplayCubeProjector(boardBounds, cellSize);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _topologyRotationVisualMapping = topologyRotationVisualMapping;
            _topologyRotationTweenSettings = NormalizeTopologyRotationTweenSettings(topologyRotationTweenSettings);
            _presentedBoardRotation = Quaternion.identity;
            _boardSurfaceTransitionStartRotation = Quaternion.identity;
            KillBoardRotationTween();
            _isBoardSurfaceTransitionActive = false;

            _exitOwnedEntityIds.Clear();
            _jumpTracks.Clear();
            _localMotionTracks.Clear();
            _pendingEntityExitSignals.Clear();
            _playerLocomotionSignalsByEntityId.Clear();
            _visibilityTracks.Clear();
            _transientEffectPresenter.Initialize(viewBinder.SearchRoot, cellSize);
            _animationSync.Reset();
            _stateStore.ResetSession(initialTopology);

            ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true);
            _isInitialized = true;
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            EnsureInitialized();

            var previousCommittedLocalTargetPoses = new Dictionary<int, GameplayEntityPose>(_stateStore.CommittedLocalTargetPoses);
            var previousCommittedTopology = _stateStore.CommittedTopology;

            StoreCommittedFrame(result.FinalEntities, result.FinalTopology);
            RefreshEntityExitPlan(result.PresentationData);
            RefreshPlayerLocomotionSignals(result.PresentationData);
            RefreshTopologyTrack(result.PresentationData);
            RefreshBoardSurfaceTransition(result.PresentationData);
            RefreshMotionClips(result.PresentationData, previousCommittedLocalTargetPoses, previousCommittedTopology);
            RefreshJumpDetachedVisibilityState(result, previousCommittedLocalTargetPoses);
            RefreshVisibilityTracks(result.PresentationData, previousCommittedLocalTargetPoses);
            RefreshTransitionVisibilityState(result.PresentationData);
            PlayEntityExitEffects();
            ApplyEntityExitOwnership();
            _animationSync.ApplyTickPresentation(result, _stateStore.ViewsByEntityId, ResolvePlayerMotionDurationSeconds);
            UpdatePresentation(0f);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();

            _localMotionTracks.Clear();
            _exitOwnedEntityIds.Clear();
            _visibilityTracks.Clear();
            KillBoardRotationTween();
            _jumpTracks.Clear();
            _pendingEntityExitSignals.Clear();
            _playerLocomotionSignalsByEntityId.Clear();
            _transientEffectPresenter.Clear();
            _animationSync.Reset();
            _stateStore.ResetSession(topology);
            _presentedBoardRotation = Quaternion.identity;
            _boardSurfaceTransitionStartRotation = Quaternion.identity;
            _isBoardSurfaceTransitionActive = false;

            StoreCommittedFrame(entities, topology);
            _boardSurfaceRenderer?.CompleteTopologyTransition(topology);
            _animationSync.ApplyInitialEnemyPresentation(entities, _stateStore.CommittedLocalTargetPoses, _stateStore.ViewsByEntityId);
            _animationSync.ApplyInitialPlayerPresentation(_stateStore.CommittedLocalTargetPoses);
            ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true);
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

            _animationSync.AdvancePlayerPresentation(deltaTime);

            if (_isBoardRotationTweenActive &&
                _boardRotationTween != null)
            {
                _boardRotationTween.ManualUpdate(deltaTime, deltaTime);
            }

            UpdateBoardSurfaceTransition(_presentedBoardRotation);
            CleanupCompletedBoardSurfaceTransitionState();
            CleanupCompletedTopologyTransitionState();
            _transientEffectPresenter.Update(deltaTime);

            _completedMotionTrackIds.Clear();
            _completedJumpTrackIds.Clear();
            _completedVisibilityTrackIds.Clear();
            _visibleEntityIds.Clear();
            _stateStore.EnemyVisualFactsByEntityId.Clear();
            _stateStore.EnemyVisualSemanticStatesByEntityId.Clear();

            var processingEntityIds = _stateStore.BuildProcessingEntityIds();
            for (var i = 0; i < processingEntityIds.Count; i++)
            {
                var entityId = processingEntityIds[i];
                if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) || view == null)
                {
                    continue;
                }

                if (!TryResolveFallbackLocalPose(entityId, out var localPose))
                {
                    continue;
                }

                if (_localMotionTracks.TryGetValue(entityId, out var motionTrack))
                {
                    localPose = motionTrack.SampleAndAdvance(deltaTime, localPose);
                    if (!motionTrack.HasClips)
                    {
                        _completedMotionTrackIds.Add(entityId);
                    }
                }

                if (_jumpTracks.TryGetValue(entityId, out var jumpTrack) &&
                    jumpTrack.HasClip)
                {
                    localPose = jumpTrack.SampleAndAdvance(deltaTime, localPose);
                    if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedState))
                    {
                        _stateStore.JumpDetachedVisibilityStates[entityId] =
                            new JumpDetachedVisibilityState(jumpDetachedState.JumpPhase, localPose);
                    }

                    if (!jumpTrack.HasClip)
                    {
                        _completedJumpTrackIds.Add(entityId);
                    }
                }

                var isVisible = _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId) ||
                                _stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId) ||
                                _stateStore.TransitionVisibilityStates.ContainsKey(entityId);
                if (_visibilityTracks.TryGetValue(entityId, out var visibilityTrack))
                {
                    isVisible = visibilityTrack.SampleAndAdvance(deltaTime, isVisible);
                    if (visibilityTrack.IsComplete)
                    {
                        _completedVisibilityTrackIds.Add(entityId);
                    }
                }

                var hasActiveMotion = _localMotionTracks.TryGetValue(entityId, out var activeMotionTrack) &&
                                      activeMotionTrack.HasClips;
                var resolvedPlayerAnimationState = _animationSync.ResolvePlayerAnimationState(
                    entityId,
                    ShouldPlayPlayerWalkLoop(entityId),
                    HasActivePlayerWalkMotion(entityId));
                _animationSync.SyncEnemyRuntimeState(entityId, isVisible, hasActiveMotion, _stateStore.ViewsByEntityId);
                _animationSync.SyncPlayerRuntimeState(
                    entityId,
                    isVisible,
                    resolvedPlayerAnimationState,
                    ResolvePlayerAnimationStateMotionDurationSeconds(entityId, resolvedPlayerAnimationState),
                    _stateStore.ViewsByEntityId);
                UpdateEnemyVisualPresentationState(entityId, isVisible, hasActiveMotion, view);

                if (!isVisible)
                {
                    continue;
                }

                view.SetVisible(true);
                view.ApplyLocalPose(localPose.Position, localPose.Rotation);
                _visibleEntityIds.Add(entityId);
            }

            for (var i = 0; i < _completedMotionTrackIds.Count; i++)
            {
                _localMotionTracks.Remove(_completedMotionTrackIds[i]);
            }

            for (var i = 0; i < _completedJumpTrackIds.Count; i++)
            {
                _jumpTracks.Remove(_completedJumpTrackIds[i]);
            }

            for (var i = 0; i < _completedVisibilityTrackIds.Count; i++)
            {
                var entityId = _completedVisibilityTrackIds[i];
                if (!_visibilityTracks.TryGetValue(entityId, out var visibilityTrack))
                {
                    continue;
                }

                _visibilityTracks.Remove(entityId);
                if (!visibilityTrack.TargetVisibility && !_stateStore.CommittedLocalTargetPoses.ContainsKey(entityId))
                {
                    _stateStore.RetainedLocalTargetPoses.Remove(entityId);
                    if (!_stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId) &&
                        !_stateStore.TransitionVisibilityStates.ContainsKey(entityId))
                    {
                        _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
                        _stateStore.EnemyAiModesByEntityId.Remove(entityId);
                        _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
                        _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
                        _stateStore.EntityTypesByEntityId.Remove(entityId);
                    }
                }
            }

            _viewBinder.HideViewsExcept(_visibleEntityIds);
            _animationSync.SyncHiddenDrivers(
                _visibleEntityIds,
                entityId => _animationSync.ResolvePlayerAnimationState(
                    entityId,
                    ShouldPlayPlayerWalkLoop(entityId),
                    hasActiveWalkMotion: false),
                ResolvePlayerAnimationStateMotionDurationSeconds,
                _stateStore.ViewsByEntityId);
        }

        private void ApplyPresentedBoardRotation(Quaternion boardRotation, bool forceApply = false)
        {
            if (!forceApply &&
                Quaternion.Angle(_presentedBoardRotation, boardRotation) <= 0.001f)
            {
                return;
            }

            _presentedBoardRotation = boardRotation;
            _boardRoot?.ApplyPresentationRotation(boardRotation, CubeCenter);
        }

        private GameplayEntityPose CreateEntityPose(
            SurfaceCell cell,
            CubeTopologyState topology,
            ProjectedCellPose projectedPose,
            Direction facing,
            Vector2 presentationPlaneOffset = default)
        {
            var localRotation = _projector.TryResolveEntityRotation(cell, topology, facing, out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;

            return new GameplayEntityPose(
                ResolvePresentationLocalPosition(projectedPose, presentationPlaneOffset),
                localRotation);
        }

        private void CleanupCompletedTopologyTransitionState()
        {
            if (HasActiveBoardRotationTween() ||
                _stateStore.TransitionVisibilityStates.Count == 0)
            {
                return;
            }

            _completedTransitionVisibilityStateIds.Clear();
            foreach (var pair in _stateStore.TransitionVisibilityStates)
            {
                _completedTransitionVisibilityStateIds.Add(pair.Key);
                if (pair.Value.Mode != TickTransitionVisibilityMode.RetainUntilTransitionComplete ||
                    _stateStore.CommittedLocalTargetPoses.ContainsKey(pair.Key) ||
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(pair.Key) ||
                    _stateStore.JumpDetachedVisibilityStates.ContainsKey(pair.Key))
                {
                    continue;
                }

                _stateStore.CommittedProjectedSlotsByEntityId.Remove(pair.Key);
                _stateStore.EnemyAiModesByEntityId.Remove(pair.Key);
                _stateStore.EnemyVisualFactsByEntityId.Remove(pair.Key);
                _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(pair.Key);
                _stateStore.EntityTypesByEntityId.Remove(pair.Key);
            }

            for (var i = 0; i < _completedTransitionVisibilityStateIds.Count; i++)
            {
                _stateStore.TransitionVisibilityStates.Remove(_completedTransitionVisibilityStateIds[i]);
            }
        }

        private void CleanupCompletedBoardSurfaceTransitionState()
        {
            if (!_isBoardSurfaceTransitionActive ||
                _boardSurfaceRenderer == null ||
                HasActiveBoardRotationTween())
            {
                return;
            }

            _boardSurfaceRenderer.CompleteTopologyTransition(_stateStore.CommittedTopology);
            _boardSurfaceTransitionStartRotation = Quaternion.identity;
            _isBoardSurfaceTransitionActive = false;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("GameplayTickViewPresenter must be initialized before use.");
            }
        }

        private bool HasActiveEntityPresentationClips()
        {
            foreach (var pair in _localMotionTracks)
            {
                if (pair.Value.HasClips)
                {
                    return true;
                }
            }

            foreach (var pair in _visibilityTracks)
            {
                if (pair.Value.IsActive)
                {
                    return true;
                }
            }

            foreach (var pair in _jumpTracks)
            {
                if (pair.Value.HasClip)
                {
                    return true;
                }
            }

            if (_transientEffectPresenter.HasActiveEffects)
            {
                return true;
            }

            return false;
        }

        private bool HasActiveBoardRotationTween()
        {
            return _isBoardRotationTweenActive &&
                   _boardRotationTween != null;
        }

        private bool HasActivePlayerWalkMotion(int entityId)
        {
            return _localMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                   motionTrack.HasClips &&
                   motionTrack.TailMotionKind == TickEntityMotionKind.Move;
        }

        private bool ShouldPlayPlayerWalkLoop(int entityId)
        {
            return _playerLocomotionSignalsByEntityId.TryGetValue(entityId, out var signal) &&
                   signal.ShouldPlayWalkLoop;
        }

        private static int GetVisibilityPriority(TickVisibilityChangeKind changeKind)
        {
            return changeKind switch
            {
                TickVisibilityChangeKind.Remove => 3,
                TickVisibilityChangeKind.Detach => 2,
                TickVisibilityChangeKind.Spawn => 1,
                _ => 0,
            };
        }

        private static bool IsTopologyTransitionPresentation(TickTopologyMotion? topologyMotion)
        {
            return topologyMotion.HasValue &&
                   topologyMotion.Value.RotationKind != CubeRotationKind.None;
        }

        private void RefreshMotionClips(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var motionEntityIds = new HashSet<int>();
            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                motionEntityIds.Add(presentationData.EntityMotions[i].EntityId);
            }

            _completedMotionTrackIds.Clear();
            foreach (var pair in _localMotionTracks)
            {
                if (motionEntityIds.Contains(pair.Key))
                {
                    continue;
                }

                if (TryResolveFallbackLocalPose(pair.Key, out var targetLocalPose))
                {
                    pair.Value.AlignToCommittedTargetPose(targetLocalPose);
                    continue;
                }

                _completedMotionTrackIds.Add(pair.Key);
            }

            for (var i = 0; i < _completedMotionTrackIds.Count; i++)
            {
                _localMotionTracks.Remove(_completedMotionTrackIds[i]);
            }

            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var motion = presentationData.EntityMotions[i];
                var endLocalPose = ResolveMotionEndPose(motion, presentationData.TopologyMotion);
                var startLocalPose = ResolveMotionStartPose(
                    motion,
                    previousCommittedLocalTargetPoses,
                    previousCommittedTopology,
                    presentationData.TopologyMotion,
                    endLocalPose);

                if (_localMotionTracks.TryGetValue(motion.EntityId, out var existingTrack) &&
                    existingTrack.HasClips)
                {
                    if (existingTrack.TailMotionKind == TickEntityMotionKind.Flip &&
                        motion.MotionKind != TickEntityMotionKind.Flip)
                    {
                        startLocalPose = existingTrack.TailEndPose;
                        existingTrack.Clear();
                    }
                    else
                    {
                        startLocalPose = existingTrack.TailEndPose;
                    }
                }

                if (!_localMotionTracks.TryGetValue(motion.EntityId, out var track))
                {
                    track = new MotionTrack();
                    _localMotionTracks[motion.EntityId] = track;
                }

                track.Append(
                    MotionClip.Create(
                        motion.MotionKind,
                        startLocalPose,
                        endLocalPose,
                        ResolveMotionDurationSeconds(motion.EntityId, motion.MotionKind),
                        IsTopologyTransitionPresentation(presentationData.TopologyMotion),
                        _timingProfile.FlipArcHeightInCells * _projector.CellSize));

                if (!_stateStore.CommittedLocalTargetPoses.ContainsKey(motion.EntityId))
                {
                    _stateStore.RetainedLocalTargetPoses[motion.EntityId] = endLocalPose;
                }
            }
        }

        private void RefreshTopologyTrack(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            KillBoardRotationTween();

            if (!presentationData.TopologyMotion.HasValue ||
                presentationData.TopologyMotion.Value.RotationKind == CubeRotationKind.None)
            {
                ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true);
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            var startRotation = _presentedBoardRotation * ResolveTopologyRotationOffset(topologyMotion.RotationKind);
            ApplyPresentedBoardRotation(startRotation, forceApply: true);
            StartBoardRotationTween(startRotation, ResolveTopologyMotionDurationSeconds());
        }

        private void RefreshTransitionVisibilityState(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _stateStore.TransitionVisibilityStates.Clear();

            if (!IsTopologyTransitionPresentation(presentationData.TopologyMotion))
            {
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            for (var i = 0; i < presentationData.TransitionVisibilityChanges.Count; i++)
            {
                var change = presentationData.TransitionVisibilityChanges[i];
                if (change.Mode == TickTransitionVisibilityMode.None ||
                    _exitOwnedEntityIds.Contains(change.EntityId) ||
                    !TryResolveTransitionLocalPose(
                        change.EntityId,
                        change.Cell,
                        change.Topology,
                        topologyMotion.DestinationTopology,
                        change.Facing,
                        out var localPose))
                {
                    continue;
                }

                var projectedSlot = _projector.TryGetProjectedTransitionEntitySlot(
                    change.Cell,
                    change.Topology,
                    topologyMotion.DestinationTopology,
                    out var resolvedProjectedSlot)
                    ? (GameplayProjectedFaceSlot?)resolvedProjectedSlot
                    : null;
                _stateStore.TransitionVisibilityStates[change.EntityId] = new TransitionVisibilityState(
                    change.Mode,
                    localPose,
                    projectedSlot);
            }
        }

        private void RefreshPlayerLocomotionSignals(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _playerLocomotionSignalsByEntityId.Clear();
            for (var i = 0; i < presentationData.PlayerLocomotionSignals.Count; i++)
            {
                var signal = presentationData.PlayerLocomotionSignals[i];
                _playerLocomotionSignalsByEntityId[signal.EntityId] = signal;
            }
        }

        private void RefreshBoardSurfaceTransition(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            if (_boardSurfaceRenderer == null)
            {
                return;
            }

            if (!IsTopologyTransitionPresentation(presentationData.TopologyMotion))
            {
                _boardSurfaceRenderer.CompleteTopologyTransition(_stateStore.CommittedTopology);
                _boardSurfaceTransitionStartRotation = Quaternion.identity;
                _isBoardSurfaceTransitionActive = false;
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            _boardSurfaceTransitionStartRotation = _presentedBoardRotation;
            _boardSurfaceRenderer.BeginTopologyTransition(
                topologyMotion.SourceTopology,
                topologyMotion.DestinationTopology,
                _boardSurfaceTransitionStartRotation);
            _isBoardSurfaceTransitionActive = true;
        }

        private void UpdateBoardSurfaceTransition(Quaternion presentedBoardRotation)
        {
            if (!_isBoardSurfaceTransitionActive ||
                _boardSurfaceRenderer == null)
            {
                return;
            }

            _boardSurfaceRenderer.UpdateTopologyTransition(
                ResolveBoardSurfaceTransitionProgress(presentedBoardRotation));
        }

        private float ResolveBoardSurfaceTransitionProgress(Quaternion presentedBoardRotation)
        {
            var totalAngle = Quaternion.Angle(_boardSurfaceTransitionStartRotation, Quaternion.identity);
            if (totalAngle <= 0.001f)
            {
                return 1f;
            }

            var remainingAngle = Quaternion.Angle(presentedBoardRotation, Quaternion.identity);
            return Mathf.Clamp01(1f - (remainingAngle / totalAngle));
        }

        private void RefreshVisibilityTracks(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            var highestPriorityChanges = new Dictionary<int, TickVisibilityChange>();
            for (var i = 0; i < presentationData.VisibilityChanges.Count; i++)
            {
                var change = presentationData.VisibilityChanges[i];
                if (!highestPriorityChanges.TryGetValue(change.EntityId, out var existingChange) ||
                    GetVisibilityPriority(change.ChangeKind) > GetVisibilityPriority(existingChange.ChangeKind))
                {
                    highestPriorityChanges[change.EntityId] = change;
                }
            }

            foreach (var pair in highestPriorityChanges)
            {
                var entityId = pair.Key;
                var change = pair.Value;
                if (_exitOwnedEntityIds.Contains(entityId))
                {
                    continue;
                }

                if (_stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId))
                {
                    _visibilityTracks.Remove(entityId);
                    continue;
                }

                if (change.ChangeKind == TickVisibilityChangeKind.Spawn)
                {
                    _stateStore.RetainedLocalTargetPoses.Remove(entityId);
                    _visibilityTracks[entityId] = VisibilityTrack.CreateShow();
                    continue;
                }

                if (!TryResolveVisibilityLocalPose(
                        change,
                        previousCommittedLocalTargetPoses,
                        presentationData.TopologyMotion,
                        out var retainedLocalPose))
                {
                    continue;
                }

                _stateStore.RetainedLocalTargetPoses[entityId] = retainedLocalPose;
                _visibilityTracks[entityId] = VisibilityTrack.CreateHide(ResolveVisibilityDurationSeconds(entityId));
            }
        }

        private void RefreshJumpDetachedVisibilityState(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (previousCommittedLocalTargetPoses == null)
            {
                throw new ArgumentNullException(nameof(previousCommittedLocalTargetPoses));
            }

            var activeAirborneEntityIds = new HashSet<int>();
            var jumpSignals = result.PresentationData.EnemyJumpSignals;
            for (var i = 0; i < jumpSignals.Count; i++)
            {
                var signal = jumpSignals[i];
                if (signal.Phase != EnemyJumpPhase.Airborne || signal.LandedThisTick)
                {
                    ClearJumpPresentationState(signal.EntityId);
                    continue;
                }

                activeAirborneEntityIds.Add(signal.EntityId);
                _visibilityTracks.Remove(signal.EntityId);

                if (TryResolveJumpTrack(signal, previousCommittedLocalTargetPoses, result, out var jumpTrack, out var localPose))
                {
                    _jumpTracks[signal.EntityId] = jumpTrack;
                    _stateStore.JumpDetachedVisibilityStates[signal.EntityId] =
                        new JumpDetachedVisibilityState(signal.Phase, localPose);
                    continue;
                }

                if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(signal.EntityId, out var existingState))
                {
                    _stateStore.JumpDetachedVisibilityStates[signal.EntityId] =
                        new JumpDetachedVisibilityState(signal.Phase, existingState.LocalPose);
                }
            }

            _completedTransitionVisibilityStateIds.Clear();
            foreach (var pair in _stateStore.JumpDetachedVisibilityStates)
            {
                if (!activeAirborneEntityIds.Contains(pair.Key))
                {
                    _completedTransitionVisibilityStateIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < _completedTransitionVisibilityStateIds.Count; i++)
            {
                ClearJumpPresentationState(_completedTransitionVisibilityStateIds[i]);
            }

            var removedEntityIds = result.CleanupPhaseResult.RemovedEntityIds;
            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                ClearJumpPresentationState(removedEntityIds[i]);
            }
        }

        private void RefreshEntityExitPlan(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _exitOwnedEntityIds.Clear();
            _pendingEntityExitSignals.Clear();

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                var signal = presentationData.EntityExitSignals[i];
                if (!_exitOwnedEntityIds.Add(signal.ExitedEntityId))
                {
                    continue;
                }

                _pendingEntityExitSignals.Add(signal);
            }
        }

        private void PlayEntityExitEffects()
        {
            for (var i = 0; i < _pendingEntityExitSignals.Count; i++)
            {
                var signal = _pendingEntityExitSignals[i];
                if (!TryResolveEntityExitSignalLocalPose(signal, out var localPose))
                {
                    continue;
                }

                _stateStore.ViewsByEntityId.TryGetValue(signal.ExitedEntityId, out var sourceView);
                _transientEffectPresenter.PlayExitEffect(
                    signal,
                    sourceView,
                    localPose,
                    ResolveEntityExitEffectDurationSeconds(signal.ExitCause));
            }
        }

        private GameplayPresentationPhase ResolveCurrentPresentationPhase()
        {
            if (HasActiveBoardRotationTween())
            {
                return GameplayPresentationPhase.TopologyTransition;
            }

            if (HasActiveEntityPresentationClips())
            {
                return GameplayPresentationPhase.EntityMotion;
            }

            if (_animationSync.HasActivePlayerVisualHold)
            {
                return GameplayPresentationPhase.EntityMotion;
            }

            return GameplayPresentationPhase.Idle;
        }

        private GameplayEntityPose ResolveMotionEndPose(
            TickEntityMotion motion,
            TickTopologyMotion? topologyMotion)
        {
            if (_stateStore.CommittedLocalTargetPoses.TryGetValue(motion.EntityId, out var committedPose))
            {
                return committedPose;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(motion.EntityId, out var retainedPose))
            {
                return retainedPose;
            }

            var destinationTopology = motion.DestinationTopology ?? _stateStore.CommittedTopology;
            var destinationFacing = motion.DestinationFacing ?? motion.SourceFacing ?? Direction.Up;
            var sourceTopology = motion.SourceTopology ?? destinationTopology;
            if (IsTopologyTransitionPresentation(topologyMotion) &&
                TryResolveTransitionLocalPose(
                    motion.EntityId,
                    motion.DestinationCell,
                    sourceTopology,
                    destinationTopology,
                    destinationFacing,
                    out var transitionEndPose))
            {
                return transitionEndPose;
            }

            return TryResolveLocalPose(motion.EntityId, motion.DestinationCell, destinationTopology, destinationFacing, out var destinationPose)
                ? destinationPose
                : default;
        }

        private float ResolveMotionDurationSeconds(int entityId, TickEntityMotionKind motionKind)
        {
            if (TryResolveUnitMotionOverrideDurationSeconds(entityId, motionKind, out var durationSeconds) ||
                TryResolveEntityMotionOverrideDurationSeconds(entityId, motionKind, out durationSeconds))
            {
                return durationSeconds;
            }

            return ResolveGlobalMotionDurationSeconds(motionKind);
        }

        private float ResolvePlayerAnimationStateMotionDurationSeconds(
            int entityId,
            PlayerViewAnimationState animationState)
        {
            return animationState switch
            {
                PlayerViewAnimationState.Push => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Push),
                PlayerViewAnimationState.Flip => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Flip),
                _ => 0f,
            };
        }

        private float ResolvePlayerMotionDurationSeconds(
            int entityId,
            PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Push),
                PlayerActionKind.Flip => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Flip),
                _ => 0f,
            };
        }

        private float ResolveGlobalMotionDurationSeconds(TickEntityMotionKind motionKind)
        {
            return motionKind switch
            {
                TickEntityMotionKind.Move => _timingProfile.MoveMotionDurationSeconds,
                TickEntityMotionKind.Flip => _timingProfile.FlipMotionDurationSeconds,
                TickEntityMotionKind.Push => _timingProfile.PushMotionDurationSeconds,
                TickEntityMotionKind.BoxSlide => _timingProfile.BoxSlideStepIntervalSeconds,
                TickEntityMotionKind.ProjectileMove => _timingProfile.ProjectileStepIntervalSeconds,
                _ => _timingProfile.PushMotionDurationSeconds,
            };
        }

        private bool TryResolveEntityMotionOverrideDurationSeconds(
            int entityId,
            TickEntityMotionKind motionKind,
            out float durationSeconds)
        {
            durationSeconds = 0f;

            if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) ||
                view == null)
            {
                return false;
            }

            var authoring = EntityMotionPresentationAuthoring.GetOptionalValidatedAuthoring(view);
            return authoring != null &&
                   authoring.TryGetMotionDurationOverride(motionKind, out durationSeconds);
        }

        private bool TryResolveUnitMotionOverrideDurationSeconds(
            int entityId,
            TickEntityMotionKind motionKind,
            out float durationSeconds)
        {
            durationSeconds = 0f;

            if (motionKind != TickEntityMotionKind.Move ||
                !_stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType) ||
                entityType != EntityType.Unit ||
                !_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) ||
                view == null)
            {
                return false;
            }

            var authoring = UnitLocomotionPresentationAuthoring.GetOptionalValidatedAuthoring(view);
            return authoring != null &&
                   authoring.TryGetMotionDurationOverride(motionKind, out durationSeconds);
        }

        private GameplayEntityPose ResolveMotionStartPose(
            TickEntityMotion motion,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            TickTopologyMotion? topologyMotion,
            GameplayEntityPose fallbackPose)
        {
            if (_localMotionTracks.TryGetValue(motion.EntityId, out var track) &&
                track.HasClips)
            {
                return track.TailEndPose;
            }

            var sourceTopology = motion.SourceTopology ?? previousCommittedTopology;
            var destinationTopology = motion.DestinationTopology ?? _stateStore.CommittedTopology;
            var sourceFacing = motion.SourceFacing ?? motion.DestinationFacing ?? Direction.Up;
            if (IsTopologyTransitionPresentation(topologyMotion) &&
                TryResolveTransitionLocalPose(
                    motion.EntityId,
                    motion.SourceCell,
                    sourceTopology,
                    destinationTopology,
                    sourceFacing,
                    out var transitionStartPose))
            {
                return transitionStartPose;
            }

            if (previousCommittedLocalTargetPoses.TryGetValue(motion.EntityId, out var previousCommittedPose))
            {
                return previousCommittedPose;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(motion.EntityId, out var retainedPose))
            {
                return retainedPose;
            }

            return TryResolveLocalPose(motion.EntityId, motion.SourceCell, sourceTopology, sourceFacing, out var sourcePose)
                ? sourcePose
                : fallbackPose;
        }

        private Quaternion ResolveTopologyRotationOffset(CubeRotationKind rotationKind)
        {
            var forwardDegrees = _topologyRotationVisualMapping == TopologyRotationVisualMapping.ForwardUsesPositiveX
                ? 90f
                : -90f;

            return rotationKind switch
            {
                CubeRotationKind.Forward => Quaternion.Euler(forwardDegrees, 0f, 0f),
                CubeRotationKind.Backward => Quaternion.Euler(-forwardDegrees, 0f, 0f),
                _ => Quaternion.identity,
            };
        }

        private float ResolveTopologyMotionDurationSeconds()
        {
            return _timingProfile.TopologyMotionDurationSeconds;
        }

        private float ResolveVisibilityDurationSeconds(int entityId)
        {
            // Legacy detach/remove visibility tracks borrow push presentation timing as a
            // minimum hide tail. Exit-owned removals must not route through this fallback.
            var durationSeconds = _timingProfile.PushMotionDurationSeconds;
            if (_localMotionTracks.TryGetValue(entityId, out var track))
            {
                durationSeconds = Mathf.Max(durationSeconds, track.TotalRemainingSeconds);
            }

            if (_jumpTracks.TryGetValue(entityId, out var jumpTrack))
            {
                durationSeconds = Mathf.Max(durationSeconds, jumpTrack.RemainingSeconds);
            }

            return durationSeconds;
        }

        private float ResolveEntityExitEffectDurationSeconds(TickEntityExitCause exitCause)
        {
            return exitCause switch
            {
                TickEntityExitCause.ItemConsume => _timingProfile.ItemConsumeEffectDurationSeconds,
                TickEntityExitCause.BoxDestroy => _timingProfile.BoxDestroyEffectDurationSeconds,
                _ => _timingProfile.ItemConsumeEffectDurationSeconds,
            };
        }

        private void ApplyEntityExitOwnership()
        {
            foreach (var entityId in _exitOwnedEntityIds)
            {
                // Exit ownership removes the authoritative entity view from presentation
                // state immediately. Any lingering visual is transient-effect-only.
                _jumpTracks.Remove(entityId);
                _localMotionTracks.Remove(entityId);
                _visibilityTracks.Remove(entityId);
                _stateStore.CommittedLocalTargetPoses.Remove(entityId);
                _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
                _stateStore.EnemyAiModesByEntityId.Remove(entityId);
                _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
                _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
                _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
                _stateStore.RetainedLocalTargetPoses.Remove(entityId);
                _stateStore.TransitionVisibilityStates.Remove(entityId);
                _stateStore.EntityTypesByEntityId.Remove(entityId);

                if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                    view != null)
                {
                    view.SetVisible(false);
                }
            }
        }

        private void StoreCommittedEntityTargets(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _stateStore.BeginCommittedFrame(topology);
            var presentableTargets = new List<PresentableEntityTarget>(entities.Count);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                _stateStore.EntityTypesByEntityId[entity.entityId] = entity.type;
                _stateStore.EnemyAiModesByEntityId[entity.entityId] = entity.aiMode;

                if (!ShouldPresent(entity, topology) ||
                    !_projector.TryProjectEntityCell(entity.position, topology, entity.type, out var projectedPose))
                {
                    continue;
                }

                var view = _viewBinder.ResolveOrCreate(entity);
                if (view == null)
                {
                    continue;
                }

                _stateStore.ViewsByEntityId[entity.entityId] = view;
                _animationSync.CacheDrivers(entity.entityId, view);
                presentableTargets.Add(new PresentableEntityTarget(entity, projectedPose));
            }

            Dictionary<int, Vector2> unitPresentationPlaneOffsetsByEntityId = null;
            if (EnableUnitPresentationPlaneOffsets)
            {
                var stackedUnitEntityIdsByCell = new Dictionary<SurfaceCell, List<int>>();
                for (var i = 0; i < presentableTargets.Count; i++)
                {
                    var target = presentableTargets[i];
                    if (target.Entity.type != EntityType.Unit)
                    {
                        continue;
                    }

                    if (!stackedUnitEntityIdsByCell.TryGetValue(target.Entity.position, out var stackedEntityIds))
                    {
                        stackedEntityIds = new List<int>();
                        stackedUnitEntityIdsByCell[target.Entity.position] = stackedEntityIds;
                    }

                    stackedEntityIds.Add(target.Entity.entityId);
                }

                unitPresentationPlaneOffsetsByEntityId =
                    BuildUnitPresentationPlaneOffsetsByEntityId(stackedUnitEntityIdsByCell);
            }

            for (var i = 0; i < presentableTargets.Count; i++)
            {
                var target = presentableTargets[i];
                var presentationPlaneOffset = EnableUnitPresentationPlaneOffsets &&
                                              target.Entity.type == EntityType.Unit &&
                                              unitPresentationPlaneOffsetsByEntityId != null &&
                                              unitPresentationPlaneOffsetsByEntityId.TryGetValue(
                                                  target.Entity.entityId,
                                                  out var resolvedPresentationPlaneOffset)
                    ? resolvedPresentationPlaneOffset
                    : Vector2.zero;

                _stateStore.CommittedLocalTargetPoses[target.Entity.entityId] = CreateEntityPose(
                    target.Entity.position,
                    topology,
                    target.ProjectedPose,
                    target.Entity.facing,
                    presentationPlaneOffset);

                if (_projector.TryGetProjectedEntitySlot(target.Entity.position, topology, out var projectedSlot))
                {
                    _stateStore.CommittedProjectedSlotsByEntityId[target.Entity.entityId] = projectedSlot;
                }
            }
        }

        private Dictionary<int, Vector2> BuildUnitPresentationPlaneOffsetsByEntityId(
            Dictionary<SurfaceCell, List<int>> stackedUnitEntityIdsByCell)
        {
            var planeOffsetsByEntityId = new Dictionary<int, Vector2>();

            foreach (var pair in stackedUnitEntityIdsByCell)
            {
                var entityIds = pair.Value;
                entityIds.Sort();

                for (var slotIndex = 0; slotIndex < entityIds.Count; slotIndex++)
                {
                    planeOffsetsByEntityId[entityIds[slotIndex]] = ResolveUnitPresentationPlaneOffset(
                        slotIndex,
                        entityIds.Count);
                }
            }

            return planeOffsetsByEntityId;
        }

        private Vector2 ResolveUnitPresentationPlaneOffset(int slotIndex, int slotCount)
        {
            if (slotCount <= 1)
            {
                return Vector2.zero;
            }

            var circleRadius = UnitPresentationOffsetRadiusInCells * _projector.CellSize;
            var squareHalfExtent = UnitPresentationSquareHalfExtentInCells * _projector.CellSize;

            return slotCount switch
            {
                2 => new Vector2(slotIndex == 0 ? -circleRadius : circleRadius, 0f),
                3 => slotIndex switch
                {
                    0 => new Vector2(0f, circleRadius),
                    1 => new Vector2(-circleRadius * 0.8660254f, -circleRadius * 0.5f),
                    _ => new Vector2(circleRadius * 0.8660254f, -circleRadius * 0.5f),
                },
                4 => slotIndex switch
                {
                    0 => new Vector2(-squareHalfExtent, squareHalfExtent),
                    1 => new Vector2(squareHalfExtent, squareHalfExtent),
                    2 => new Vector2(-squareHalfExtent, -squareHalfExtent),
                    _ => new Vector2(squareHalfExtent, -squareHalfExtent),
                },
                _ => ResolveCircularPresentationPlaneOffset(slotIndex, slotCount, circleRadius),
            };
        }

        private static Vector3 ResolvePresentationLocalPosition(
            ProjectedCellPose projectedPose,
            Vector2 presentationPlaneOffset)
        {
            if (presentationPlaneOffset.sqrMagnitude <= 0.000001f)
            {
                return projectedPose.LocalPosition;
            }

            return projectedPose.LocalPosition +
                   (projectedPose.LocalRotation * new Vector3(
                       presentationPlaneOffset.x,
                       presentationPlaneOffset.y,
                       0f));
        }

        private static Vector2 ResolveCircularPresentationPlaneOffset(
            int slotIndex,
            int slotCount,
            float radius)
        {
            var angle = ((Mathf.PI * 2f) / slotCount * slotIndex) + (Mathf.PI * 0.5f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void StoreCommittedFrame(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology)
        {
            StoreCommittedEntityTargets(entities, topology);
            _stateStore.HasAnyCommittedFrame = true;
            TopologyCommitted?.Invoke(_stateStore.CommittedTopology);
        }

        private static bool ShouldPresent(EntityState entity, CubeTopologyState topology)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying &&
                   topology.IsFaceActive(entity.position.face);
        }

        private bool TryResolveFallbackLocalPose(int entityId, out GameplayEntityPose localPose)
        {
            if (_stateStore.CommittedLocalTargetPoses.TryGetValue(entityId, out localPose))
            {
                return true;
            }

            if (_stateStore.TransitionVisibilityStates.TryGetValue(entityId, out var transitionVisibilityState))
            {
                localPose = transitionVisibilityState.LocalPose;
                return true;
            }

            if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var jumpDetachedVisibilityState))
            {
                localPose = jumpDetachedVisibilityState.LocalPose;
                return true;
            }

            return _stateStore.RetainedLocalTargetPoses.TryGetValue(entityId, out localPose);
        }

        private void UpdateEnemyVisualPresentationState(
            int entityId,
            bool isVisible,
            bool hasActiveMotion,
            GameplayEntityView view)
        {
            var facts = BuildEnemyVisualPresentationFacts(entityId, isVisible, hasActiveMotion);
            _stateStore.EnemyVisualFactsByEntityId[entityId] = facts;

            var semanticState = _enemyVisualSemanticResolver.Resolve(facts);
            _stateStore.EnemyVisualSemanticStatesByEntityId[entityId] = semanticState;

            if (view != null &&
                view.TryGetComponent<EnemyInactiveVisualController>(out var controller) &&
                controller != null)
            {
                controller.Apply(semanticState);
            }
        }

        private EnemyVisualPresentationFacts BuildEnemyVisualPresentationFacts(
            int entityId,
            bool isVisible,
            bool hasActiveMotion)
        {
            var isCommittedVisible = _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId);
            var isTransitionVisible = _stateStore.TransitionVisibilityStates.TryGetValue(entityId, out var transitionVisibilityState);
            var isJumpDetachedVisible = _stateStore.JumpDetachedVisibilityStates.ContainsKey(entityId);
            var isTransitionOnlyVisible = isTransitionVisible && !isCommittedVisible;
            var hasEntityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType);
            var hasEnemyAiMode = _stateStore.EnemyAiModesByEntityId.TryGetValue(entityId, out var aiMode);
            var isEnemy = hasEntityType &&
                          entityType == EntityType.Unit &&
                          hasEnemyAiMode &&
                          aiMode != EnemyAiMode.None;

            return new EnemyVisualPresentationFacts(
                entityId,
                isEnemy,
                isVisible,
                isCommittedVisible,
                isTransitionVisible,
                isTransitionOnlyVisible,
                isJumpDetachedVisible,
                ResolveProjectedSlot(entityId, isCommittedVisible, isTransitionVisible, transitionVisibilityState),
                isEnemy ? aiMode : EnemyAiMode.None,
                hasActiveMotion);
        }

        private GameplayProjectedFaceSlot? ResolveProjectedSlot(
            int entityId,
            bool isCommittedVisible,
            bool isTransitionVisible,
            TransitionVisibilityState transitionVisibilityState)
        {
            if (isCommittedVisible &&
                _stateStore.CommittedProjectedSlotsByEntityId.TryGetValue(entityId, out var committedSlot))
            {
                return committedSlot;
            }

            if (isTransitionVisible)
            {
                return transitionVisibilityState.ProjectedSlot;
            }

            return null;
        }

        private void ClearJumpPresentationState(int entityId)
        {
            _jumpTracks.Remove(entityId);
            _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
        }

        private bool TryResolveLegacyTransitionLocalPose(
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            EntityType entityType,
            Direction facing,
            out GameplayEntityPose pose)
        {
            pose = default;
            if (!_projector.TryProjectTransitionEntityCell(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    entityType,
                    out var projectedPose))
            {
                return false;
            }

            var localRotation = _projector.TryResolveTransitionEntityRotation(
                cell,
                sourceTopology,
                destinationTopology,
                facing,
                out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;

            pose = new GameplayEntityPose(
                projectedPose.LocalPosition,
                localRotation);
            return true;
        }

        private bool TryResolveLocalPose(
            int entityId,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing,
            out GameplayEntityPose pose)
        {
            pose = default;
            var entityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var knownEntityType)
                ? knownEntityType
                : EntityType.Unit;
            if (!_projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose))
            {
                return false;
            }

            pose = CreateEntityPose(cell, topology, projectedPose, facing);
            return true;
        }

        private bool TryResolveEntityExitSignalLocalPose(
            TickEntityExitPresentationSignal signal,
            out GameplayEntityPose pose)
        {
            pose = default;
            if (!_projector.TryProjectEntityCell(signal.SourceCell, signal.Topology, signal.EntityType, out var projectedPose))
            {
                return false;
            }

            pose = CreateEntityPose(signal.SourceCell, signal.Topology, projectedPose, signal.Facing);
            return true;
        }

        private bool TryResolveTransitionLocalPose(
            int entityId,
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            Direction facing,
            out GameplayEntityPose pose)
        {
            pose = default;
            var entityType = _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var knownEntityType)
                ? knownEntityType
                : EntityType.Unit;
            if (!TryResolveTransitionStartRotation(
                    sourceTopology,
                    destinationTopology,
                    out var transitionStartRotation))
            {
                return TryResolveLegacyTransitionLocalPose(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    entityType,
                    facing,
                    out pose);
            }

            if (!_projector.TryProjectTransitionEntityCell(
                    cell,
                    destinationTopology,
                    sourceTopology,
                    entityType,
                    out var projectedPose))
            {
                return false;
            }

            var localRotation = _projector.TryResolveTransitionEntityRotation(
                cell,
                destinationTopology,
                sourceTopology,
                facing,
                out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;
            var inverseTransitionStartRotation = Quaternion.Inverse(transitionStartRotation);

            pose = new GameplayEntityPose(
                inverseTransitionStartRotation * projectedPose.LocalPosition,
                inverseTransitionStartRotation * localRotation);
            return true;
        }

        private bool TryResolveTransitionStartRotation(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            out Quaternion transitionStartRotation)
        {
            transitionStartRotation = Quaternion.identity;
            if (sourceTopology.Equals(destinationTopology))
            {
                return true;
            }

            if (destinationTopology.Equals(sourceTopology.Rotate(CubeRotationKind.Forward)))
            {
                transitionStartRotation = ResolveTopologyRotationOffset(CubeRotationKind.Forward);
                return true;
            }

            if (destinationTopology.Equals(sourceTopology.Rotate(CubeRotationKind.Backward)))
            {
                transitionStartRotation = ResolveTopologyRotationOffset(CubeRotationKind.Backward);
                return true;
            }

            return false;
        }

        private bool TryResolveVisibilityLocalPose(
            TickVisibilityChange change,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickTopologyMotion? topologyMotion,
            out GameplayEntityPose localPose)
        {
            if (_localMotionTracks.TryGetValue(change.EntityId, out var motionTrack) &&
                motionTrack.HasClips)
            {
                localPose = motionTrack.TailEndPose;
                return true;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(change.EntityId, out localPose))
            {
                return true;
            }

            if (IsTopologyTransitionPresentation(topologyMotion) &&
                TryResolveTransitionLocalPose(
                    change.EntityId,
                    change.Cell,
                    change.Topology,
                    topologyMotion.Value.DestinationTopology,
                    change.Facing,
                    out localPose))
            {
                return true;
            }

            if (previousCommittedLocalTargetPoses.TryGetValue(change.EntityId, out localPose))
            {
                return true;
            }

            return TryResolveLocalPose(change.EntityId, change.Cell, change.Topology, change.Facing, out localPose);
        }

        private bool TryResolveJumpDetachedLocalPose(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickResult result,
            out GameplayEntityPose localPose)
        {
            if (previousCommittedLocalTargetPoses.TryGetValue(entityId, out localPose))
            {
                return true;
            }

            if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var existingState))
            {
                localPose = existingState.LocalPose;
                return true;
            }

            if (_stateStore.RetainedLocalTargetPoses.TryGetValue(entityId, out localPose))
            {
                return true;
            }

            var visibilityChanges = result.PresentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.EntityId != entityId ||
                    change.ChangeKind != TickVisibilityChangeKind.Detach)
                {
                    continue;
                }

                return TryResolveVisibilityLocalPose(
                    change,
                    previousCommittedLocalTargetPoses,
                    result.PresentationData.TopologyMotion,
                    out localPose);
            }

            var finalEntities = result.FinalEntities;
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (entity.entityId != entityId)
                {
                    continue;
                }

                return TryResolveLocalPose(entityId, entity.position, result.FinalTopology, entity.facing, out localPose);
            }

            localPose = default;
            return false;
        }

        private static TopologyRotationTweenSettings NormalizeTopologyRotationTweenSettings(
            TopologyRotationTweenSettings settings)
        {
            if (!Enum.IsDefined(typeof(TopologyRotationTweenMode), settings.Mode) ||
                !Enum.IsDefined(typeof(TopologyRotationTweenEase), settings.Ease))
            {
                return TopologyRotationTweenSettings.CreateDefault();
            }

            return settings;
        }

        private void KillBoardRotationTween(bool complete = false)
        {
            if (_boardRotationTween != null &&
                _boardRotationTween.IsActive())
            {
                _boardRotationTween.Kill(complete);
            }

            _boardRotationTween = null;
            _isBoardRotationTweenActive = false;
        }

        private Ease ResolveTopologyRotationEase()
        {
            return Enum.TryParse(_topologyRotationTweenSettings.Ease.ToString(), out Ease ease)
                ? ease
                : Ease.OutQuad;
        }

        private Quaternion ResolveTweenedBoardRotation(Quaternion startRotation, float progress)
        {
            return _topologyRotationTweenSettings.Mode switch
            {
                TopologyRotationTweenMode.QuaternionSlerp => Quaternion.SlerpUnclamped(
                    startRotation,
                    Quaternion.identity,
                    progress),
                _ => Quaternion.Euler(
                    Mathf.LerpUnclamped(
                        NormalizeSignedAxisAngle(startRotation.eulerAngles.x),
                        0f,
                        progress),
                    0f,
                    0f),
            };
        }

        private void StartBoardRotationTween(Quaternion startRotation, float durationSeconds)
        {
            var progress = 0f;
            _isBoardRotationTweenActive = true;
            _boardRotationTween = DOTween
                .To(
                    () => progress,
                    value =>
                    {
                        progress = value;
                        ApplyPresentedBoardRotation(ResolveTweenedBoardRotation(startRotation, progress));
                    },
                    1f,
                    Mathf.Max(durationSeconds, MinimumTopologyTweenDurationSeconds))
                .SetEase(ResolveTopologyRotationEase())
                .SetUpdate(UpdateType.Manual)
                .SetAutoKill(true)
                .OnComplete(() => ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true))
                .OnKill(() =>
                {
                    _boardRotationTween = null;
                    _isBoardRotationTweenActive = false;
                });
        }

        private static float NormalizeSignedAxisAngle(float eulerDegrees)
        {
            return Mathf.DeltaAngle(0f, eulerDegrees);
        }

        private bool TryResolveJumpTrack(
            TickEnemyJumpPresentationSignal signal,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickResult result,
            out JumpTrack jumpTrack,
            out GameplayEntityPose localPose)
        {
            jumpTrack = null;
            localPose = default;
            _jumpTracks.TryGetValue(signal.EntityId, out var existingTrack);

            if (!signal.StartedAirborneThisTick &&
                !signal.RetryThisTick &&
                existingTrack != null &&
                existingTrack.HasClip &&
                _stateStore.JumpDetachedVisibilityStates.TryGetValue(signal.EntityId, out var existingState))
            {
                jumpTrack = existingTrack;
                localPose = existingState.LocalPose;
                return true;
            }

            if (!TryResolveJumpTrackStartPose(signal.EntityId, previousCommittedLocalTargetPoses, result, out var startPose) ||
                !TryResolveJumpTrackEndPose(signal, out var endPose))
            {
                return false;
            }

            jumpTrack = existingTrack ?? new JumpTrack();
            jumpTrack.Replace(
                JumpClip.Create(
                    startPose,
                    endPose,
                    ResolveJumpDurationSeconds(signal),
                    ResolveJumpArcHeightWorld(signal.EntityId)));
            localPose = startPose;
            return true;
        }

        private bool TryResolveJumpTrackStartPose(
            int entityId,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            TickResult result,
            out GameplayEntityPose localPose)
        {
            if (_stateStore.JumpDetachedVisibilityStates.TryGetValue(entityId, out var existingState))
            {
                localPose = existingState.LocalPose;
                return true;
            }

            if (_jumpTracks.TryGetValue(entityId, out var existingTrack) &&
                existingTrack.HasClip)
            {
                localPose = existingTrack.CurrentPose;
                return true;
            }

            return TryResolveJumpDetachedLocalPose(
                entityId,
                previousCommittedLocalTargetPoses,
                result,
                out localPose);
        }

        private bool TryResolveJumpTrackEndPose(
            TickEnemyJumpPresentationSignal signal,
            out GameplayEntityPose localPose)
        {
            return TryResolveLocalPose(
                signal.EntityId,
                signal.PresentationTargetCell,
                _stateStore.CommittedTopology,
                signal.Facing == Direction.None ? Direction.Up : signal.Facing,
                out localPose);
        }

        private float ResolveJumpArcHeightWorld(int entityId)
        {
            return DefaultJumpArcHeightInCells * _projector.CellSize;
        }

        private float ResolveJumpDurationSeconds(TickEnemyJumpPresentationSignal signal)
        {
            return Mathf.Max(
                0.0001f,
                Mathf.Max(0, signal.RemainingAirborneTicks) * _timingProfile.SimulationTickIntervalSeconds);
        }

        private readonly struct PresentableEntityTarget
        {
            public PresentableEntityTarget(EntityState entity, ProjectedCellPose projectedPose)
            {
                Entity = entity;
                ProjectedPose = projectedPose;
            }

            public EntityState Entity { get; }

            public ProjectedCellPose ProjectedPose { get; }
        }
    }
}
