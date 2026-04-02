using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayTickPresentationCoordinator
    {
        private readonly GameplayAnimationSyncCoordinator _animationSync = new();
        private readonly RotationTrack _boardRotationTrack = new();
        private readonly List<int> _completedMotionTrackIds = new();
        private readonly List<int> _completedTransitionVisibilityStateIds = new();
        private readonly List<int> _completedVisibilityTrackIds = new();
        private readonly Dictionary<int, MotionTrack> _localMotionTracks = new();
        private readonly GameplayPresentationStateStore _stateStore = new();
        private readonly HashSet<int> _visibleEntityIds = new();
        private readonly Dictionary<int, VisibilityTrack> _visibilityTracks = new();

        private GameplayBoardRoot _boardRoot;
        private bool _isInitialized;
        private Quaternion _presentedBoardRotation = Quaternion.identity;
        private GameplayCubeProjector _projector;
        private GameplayTimingProfile _timingProfile;
        private TopologyRotationVisualMapping _topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX;
        private GameplayEntityViewBinder _viewBinder;

        public event Action<CubeTopologyState> TopologyCommitted;

        public CubeTopologyState CurrentTopology => _stateStore.CommittedTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => ResolveCurrentPresentationPhase();

        public Vector3 CubeCenter => _projector != null ? _projector.GetCubeCenter() : Vector3.zero;

        public bool HasBlockingPresentation => _boardRotationTrack.HasClips;

        public bool IsInitialized => _isInitialized;

        public bool IsPresentationActive => CurrentPresentationPhase != GameplayPresentationPhase.Idle;

        public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;

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
            TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _boardRoot = boardRoot;
            _viewBinder = viewBinder;
            _projector = new GameplayCubeProjector(boardBounds, cellSize);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _topologyRotationVisualMapping = topologyRotationVisualMapping;
            _presentedBoardRotation = Quaternion.identity;

            _boardRotationTrack.Clear();
            _localMotionTracks.Clear();
            _visibilityTracks.Clear();
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
            RefreshTopologyTrack(result.PresentationData);
            RefreshMotionClips(result.PresentationData, previousCommittedLocalTargetPoses, previousCommittedTopology);
            RefreshVisibilityTracks(result.PresentationData, previousCommittedLocalTargetPoses);
            RefreshTransitionVisibilityState(result.PresentationData);
            _animationSync.ApplyTickPresentation(result, _stateStore.ViewsByEntityId);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();

            _localMotionTracks.Clear();
            _visibilityTracks.Clear();
            _boardRotationTrack.Clear();
            _animationSync.Reset();
            _stateStore.ResetSession(topology);
            _presentedBoardRotation = Quaternion.identity;

            StoreCommittedFrame(entities, topology);
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

            var presentedBoardRotation = _boardRotationTrack.HasClips
                ? _boardRotationTrack.SampleAndAdvance(deltaTime, Quaternion.identity)
                : Quaternion.identity;
            ApplyPresentedBoardRotation(presentedBoardRotation);
            CleanupCompletedTopologyTransitionState();

            _completedMotionTrackIds.Clear();
            _completedVisibilityTrackIds.Clear();
            _visibleEntityIds.Clear();

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

                var isVisible = _stateStore.CommittedLocalTargetPoses.ContainsKey(entityId) ||
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
                _animationSync.SyncEnemyRuntimeState(entityId, isVisible, hasActiveMotion, _stateStore.ViewsByEntityId);
                _animationSync.SyncPlayerRuntimeState(
                    entityId,
                    isVisible,
                    ResolvePlayerAnimationState(entityId, HasActivePlayerWalkMotion(entityId)),
                    _stateStore.ViewsByEntityId);

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
                    _stateStore.EntityTypesByEntityId.Remove(entityId);
                }
            }

            _viewBinder.HideViewsExcept(_visibleEntityIds);
            _animationSync.SyncHiddenDrivers(
                _visibleEntityIds,
                entityId => ResolvePlayerAnimationState(entityId, hasActiveWalkMotion: false),
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
            Direction facing)
        {
            var localRotation = _projector.TryResolveEntityRotation(cell, topology, facing, out var resolvedRotation)
                ? resolvedRotation
                : projectedPose.LocalRotation;

            return new GameplayEntityPose(
                projectedPose.LocalPosition,
                localRotation);
        }

        private void CleanupCompletedTopologyTransitionState()
        {
            if (_boardRotationTrack.HasClips ||
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
                    _stateStore.RetainedLocalTargetPoses.ContainsKey(pair.Key))
                {
                    continue;
                }

                _stateStore.EntityTypesByEntityId.Remove(pair.Key);
            }

            for (var i = 0; i < _completedTransitionVisibilityStateIds.Count; i++)
            {
                _stateStore.TransitionVisibilityStates.Remove(_completedTransitionVisibilityStateIds[i]);
            }
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

            return false;
        }

        private bool HasActivePlayerWalkMotion(int entityId)
        {
            return _localMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                   motionTrack.HasClips &&
                   motionTrack.TailMotionKind == TickEntityMotionKind.Move;
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
                        ResolveMotionDurationSeconds(motion.MotionKind),
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

            _boardRotationTrack.Clear();

            if (!presentationData.TopologyMotion.HasValue ||
                presentationData.TopologyMotion.Value.RotationKind == CubeRotationKind.None)
            {
                ApplyPresentedBoardRotation(Quaternion.identity, forceApply: true);
                return;
            }

            var topologyMotion = presentationData.TopologyMotion.Value;
            var startRotation = _presentedBoardRotation * ResolveTopologyRotationOffset(topologyMotion.RotationKind);
            _boardRotationTrack.Append(
                RotationClip.Create(
                    startRotation,
                    Quaternion.identity,
                    ResolveTopologyMotionDurationSeconds()));
            ApplyPresentedBoardRotation(startRotation, forceApply: true);
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

                _stateStore.TransitionVisibilityStates[change.EntityId] = new TransitionVisibilityState(change.Mode, localPose);
            }
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

        private GameplayPresentationPhase ResolveCurrentPresentationPhase()
        {
            if (_boardRotationTrack.HasClips)
            {
                return GameplayPresentationPhase.TopologyTransition;
            }

            if (HasActiveEntityPresentationClips())
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

        private float ResolveMotionDurationSeconds(TickEntityMotionKind motionKind)
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

        private PlayerViewAnimationState ResolvePlayerAnimationState(int entityId, bool hasActiveWalkMotion)
        {
            if (_animationSync.PlayerPresentationStates.TryGetValue(entityId, out var state))
            {
                if (state.ActiveActionKind == PlayerActionKind.Flip)
                {
                    return PlayerViewAnimationState.Flip;
                }

                if (state.ActiveActionKind == PlayerActionKind.Push)
                {
                    return PlayerViewAnimationState.Push;
                }
            }

            return hasActiveWalkMotion
                ? PlayerViewAnimationState.Walk
                : PlayerViewAnimationState.Idle;
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
            var durationSeconds = _timingProfile.PushMotionDurationSeconds;
            if (_localMotionTracks.TryGetValue(entityId, out var track))
            {
                durationSeconds = Mathf.Max(durationSeconds, track.TotalRemainingSeconds);
            }

            return durationSeconds;
        }

        private void StoreCommittedEntityTargets(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _stateStore.BeginCommittedFrame(topology);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                _stateStore.EntityTypesByEntityId[entity.entityId] = entity.type;

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
                _stateStore.CommittedLocalTargetPoses[entity.entityId] = CreateEntityPose(
                    entity.position,
                    topology,
                    projectedPose,
                    entity.facing);
            }
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

            return _stateStore.RetainedLocalTargetPoses.TryGetValue(entityId, out localPose);
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
    }
}
