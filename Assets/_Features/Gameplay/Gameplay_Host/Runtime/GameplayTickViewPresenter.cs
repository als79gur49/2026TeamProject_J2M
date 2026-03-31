using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum GameplayPresentationPhase
    {
        Idle = 0,
        EntityMotion = 1,
        TopologyTransition = 2,
    }

    public sealed class GameplayTickViewPresenter : MonoBehaviour
    {
        private readonly RotationTrack _boardRotationTrack = new();
        private readonly List<int> _completedMotionTrackIds = new();
        private readonly List<int> _completedVisibilityTrackIds = new();
        private readonly Dictionary<int, GameplayEntityPose> _committedLocalTargetPoses = new();
        private readonly Dictionary<int, EntityType> _entityTypesByEntityId = new();
        private readonly Dictionary<int, MotionTrack> _localMotionTracks = new();
        private readonly HashSet<int> _processingEntityIds = new();
        private readonly List<int> _processingEntityIdBuffer = new();
        private readonly Dictionary<int, GameplayEntityPose> _retainedLocalTargetPoses = new();
        private readonly HashSet<int> _visibleEntityIds = new();
        private readonly Dictionary<int, VisibilityTrack> _visibilityTracks = new();
        private readonly Dictionary<int, GameplayEntityView> _viewsByEntityId = new();

        private GameplayBoardRoot _boardRoot;
        private CubeTopologyState _committedTopology;
        private bool _hasAnyCommittedFrame;
        private bool _isInitialized;
        private Quaternion _presentedBoardRotation = Quaternion.identity;
        private GameplayCubeProjector _projector;
        private GameplayTimingProfile _timingProfile;
        private GameplayEntityViewBinder _viewBinder;

        public event Action<CubeTopologyState> TopologyCommitted;

        public CubeTopologyState CurrentTopology => _committedTopology;

        public GameplayPresentationPhase CurrentPresentationPhase => ResolveCurrentPresentationPhase();

        public bool IsPresentationActive => CurrentPresentationPhase != GameplayPresentationPhase.Idle;

        public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;

        public Quaternion PresentedBoardRotation => _presentedBoardRotation;

        public Vector3 CubeCenter => _projector != null ? _projector.GetCubeCenter() : Vector3.zero;

        public Bounds VisibleCubeBounds
        {
            get
            {
                EnsureInitialized();
                return _projector.GetVisibleCubeBounds(_committedTopology);
            }
        }

        public void Initialize(
            GameplayEntityViewBinder viewBinder,
            BoardBounds boardBounds,
            CubeTopologyState initialTopology,
            float cellSize,
            GameplayTimingProfile timingProfile,
            GameplayBoardRoot boardRoot = null)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _boardRoot = boardRoot;
            _viewBinder = viewBinder;
            _projector = new GameplayCubeProjector(boardBounds, cellSize);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _committedTopology = initialTopology;
            _presentedBoardRotation = Quaternion.identity;
            _hasAnyCommittedFrame = false;
            _committedLocalTargetPoses.Clear();
            _entityTypesByEntityId.Clear();
            _retainedLocalTargetPoses.Clear();
            _localMotionTracks.Clear();
            _visibilityTracks.Clear();
            _viewsByEntityId.Clear();
            _boardRotationTrack.Clear();
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

            var previousCommittedLocalTargetPoses = new Dictionary<int, GameplayEntityPose>(_committedLocalTargetPoses);
            var previousCommittedTopology = _committedTopology;

            // WorldState remains authoritative; presentation tracks only delay what is rendered.
            StoreCommittedFrame(result.FinalEntities, result.FinalTopology);
            RefreshTopologyTrack(result.PresentationData);
            RefreshMotionClips(result.PresentationData, previousCommittedLocalTargetPoses, previousCommittedTopology);
            RefreshVisibilityTracks(result.PresentationData, previousCommittedLocalTargetPoses);
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
            _entityTypesByEntityId.Clear();
            _retainedLocalTargetPoses.Clear();
            _boardRotationTrack.Clear();
            _committedTopology = topology;
            _presentedBoardRotation = Quaternion.identity;
            StoreCommittedFrame(entities, topology);
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

            if (!_hasAnyCommittedFrame)
            {
                return;
            }

            var presentedBoardRotation = _boardRotationTrack.HasClips
                ? _boardRotationTrack.SampleAndAdvance(deltaTime, Quaternion.identity)
                : Quaternion.identity;
            ApplyPresentedBoardRotation(presentedBoardRotation);

            _completedMotionTrackIds.Clear();
            _completedVisibilityTrackIds.Clear();
            _visibleEntityIds.Clear();
            BuildProcessingEntityIds();

            for (var i = 0; i < _processingEntityIdBuffer.Count; i++)
            {
                var entityId = _processingEntityIdBuffer[i];
                if (!_viewsByEntityId.TryGetValue(entityId, out var view) || view == null)
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

                var isVisible = _committedLocalTargetPoses.ContainsKey(entityId);
                if (_visibilityTracks.TryGetValue(entityId, out var visibilityTrack))
                {
                    isVisible = visibilityTrack.SampleAndAdvance(deltaTime, isVisible);
                    if (visibilityTrack.IsComplete)
                    {
                        _completedVisibilityTrackIds.Add(entityId);
                    }
                }

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
                if (!visibilityTrack.TargetVisibility && !_committedLocalTargetPoses.ContainsKey(entityId))
                {
                    _retainedLocalTargetPoses.Remove(entityId);
                    _entityTypesByEntityId.Remove(entityId);
                }
            }

            _viewBinder.HideViewsExcept(_visibleEntityIds);
        }

        private void LateUpdate()
        {
            if (_isInitialized)
            {
                UpdatePresentation(Time.deltaTime);
            }
        }

        private void StoreCommittedFrame(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology)
        {
            _committedTopology = topology;
            StoreCommittedEntityTargets(entities, topology);
            _hasAnyCommittedFrame = true;
            TopologyCommitted?.Invoke(_committedTopology);
        }

        private void StoreCommittedEntityTargets(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _committedLocalTargetPoses.Clear();

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                _entityTypesByEntityId[entity.entityId] = entity.type;

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

                _viewsByEntityId[entity.entityId] = view;
                _committedLocalTargetPoses[entity.entityId] = CreateEntityPose(
                    entity.position,
                    topology,
                    projectedPose,
                    entity.facing);
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
                var endLocalPose = ResolveMotionEndPose(motion);
                var startLocalPose = ResolveMotionStartPose(
                    motion,
                    previousCommittedLocalTargetPoses,
                    previousCommittedTopology,
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
                        _timingProfile.FlipArcHeightInCells * _projector.CellSize));

                if (!_committedLocalTargetPoses.ContainsKey(motion.EntityId))
                {
                    _retainedLocalTargetPoses[motion.EntityId] = endLocalPose;
                }
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
                    _retainedLocalTargetPoses.Remove(entityId);
                    _visibilityTracks[entityId] = VisibilityTrack.CreateShow();
                    continue;
                }

                if (!TryResolveVisibilityLocalPose(change, previousCommittedLocalTargetPoses, out var retainedLocalPose))
                {
                    continue;
                }

                _retainedLocalTargetPoses[entityId] = retainedLocalPose;
                _visibilityTracks[entityId] = VisibilityTrack.CreateHide(ResolveVisibilityDurationSeconds(entityId));
            }
        }

        private GameplayEntityPose ResolveMotionStartPose(
            TickEntityMotion motion,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            GameplayEntityPose fallbackPose)
        {
            if (_localMotionTracks.TryGetValue(motion.EntityId, out var track) &&
                track.HasClips)
            {
                return track.TailEndPose;
            }

            if (previousCommittedLocalTargetPoses.TryGetValue(motion.EntityId, out var previousCommittedPose))
            {
                return previousCommittedPose;
            }

            if (_retainedLocalTargetPoses.TryGetValue(motion.EntityId, out var retainedPose))
            {
                return retainedPose;
            }

            var sourceTopology = motion.SourceTopology ?? previousCommittedTopology;
            var sourceFacing = motion.SourceFacing ?? motion.DestinationFacing ?? Direction.Up;
            return TryResolveLocalPose(motion.EntityId, motion.SourceCell, sourceTopology, sourceFacing, out var sourcePose)
                ? sourcePose
                : fallbackPose;
        }

        private GameplayEntityPose ResolveMotionEndPose(TickEntityMotion motion)
        {
            if (_committedLocalTargetPoses.TryGetValue(motion.EntityId, out var committedPose))
            {
                return committedPose;
            }

            if (_retainedLocalTargetPoses.TryGetValue(motion.EntityId, out var retainedPose))
            {
                return retainedPose;
            }

            var destinationTopology = motion.DestinationTopology ?? _committedTopology;
            var destinationFacing = motion.DestinationFacing ?? motion.SourceFacing ?? Direction.Up;
            return TryResolveLocalPose(motion.EntityId, motion.DestinationCell, destinationTopology, destinationFacing, out var destinationPose)
                ? destinationPose
                : default;
        }

        private bool TryResolveVisibilityLocalPose(
            TickVisibilityChange change,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            out GameplayEntityPose localPose)
        {
            if (_localMotionTracks.TryGetValue(change.EntityId, out var motionTrack) &&
                motionTrack.HasClips)
            {
                localPose = motionTrack.TailEndPose;
                return true;
            }

            if (_retainedLocalTargetPoses.TryGetValue(change.EntityId, out localPose))
            {
                return true;
            }

            if (previousCommittedLocalTargetPoses.TryGetValue(change.EntityId, out localPose))
            {
                return true;
            }

            return TryResolveLocalPose(change.EntityId, change.Cell, change.Topology, change.Facing, out localPose);
        }

        private bool TryResolveFallbackLocalPose(int entityId, out GameplayEntityPose localPose)
        {
            if (_committedLocalTargetPoses.TryGetValue(entityId, out localPose))
            {
                return true;
            }

            return _retainedLocalTargetPoses.TryGetValue(entityId, out localPose);
        }

        private bool TryResolveLocalPose(
            int entityId,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing,
            out GameplayEntityPose pose)
        {
            pose = default;
            var entityType = _entityTypesByEntityId.TryGetValue(entityId, out var knownEntityType)
                ? knownEntityType
                : EntityType.Unit;
            if (!_projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose))
            {
                return false;
            }

            pose = CreateEntityPose(cell, topology, projectedPose, facing);
            return true;
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

        private float ResolveMotionDurationSeconds(TickEntityMotionKind motionKind)
        {
            return motionKind switch
            {
                TickEntityMotionKind.Flip => _timingProfile.FlipMotionDurationSeconds,
                TickEntityMotionKind.ProjectileMove => _timingProfile.ProjectileStepIntervalSeconds,
                _ => _timingProfile.PushMotionDurationSeconds,
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

        private void BuildProcessingEntityIds()
        {
            _processingEntityIds.Clear();
            _processingEntityIdBuffer.Clear();

            foreach (var pair in _committedLocalTargetPoses)
            {
                AddProcessingEntityId(pair.Key);
            }

            foreach (var pair in _retainedLocalTargetPoses)
            {
                AddProcessingEntityId(pair.Key);
            }

            _processingEntityIdBuffer.Sort();
        }

        private void AddProcessingEntityId(int entityId)
        {
            if (_processingEntityIds.Add(entityId))
            {
                _processingEntityIdBuffer.Add(entityId);
            }
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

        private static Quaternion ResolveTopologyRotationOffset(CubeRotationKind rotationKind)
        {
            return rotationKind switch
            {
                CubeRotationKind.Forward => Quaternion.Euler(90f, 0f, 0f),
                CubeRotationKind.Backward => Quaternion.Euler(-90f, 0f, 0f),
                _ => Quaternion.identity,
            };
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

        private static bool ShouldPresent(EntityState entity, CubeTopologyState topology)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying &&
                   topology.IsFaceActive(entity.position.face);
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

        private readonly struct GameplayEntityPose
        {
            public GameplayEntityPose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; }

            public Quaternion Rotation { get; }
        }

        private sealed class RotationTrack
        {
            private readonly List<RotationClip> _clips = new();

            public bool HasClips => _clips.Count > 0;

            public Quaternion TailEndValue => _clips[_clips.Count - 1].EndValue;

            public void Append(RotationClip clip)
            {
                if (clip == null)
                {
                    throw new ArgumentNullException(nameof(clip));
                }

                _clips.Add(clip);
            }

            public void Clear()
            {
                _clips.Clear();
            }

            public Quaternion SampleAndAdvance(float deltaTime, Quaternion fallbackValue)
            {
                if (_clips.Count == 0)
                {
                    return fallbackValue;
                }

                var remainingDeltaTime = deltaTime;
                while (_clips.Count > 0)
                {
                    var clip = _clips[0];
                    remainingDeltaTime = clip.Advance(remainingDeltaTime);
                    var value = clip.IsComplete
                        ? clip.EndValue
                        : clip.Sample();
                    if (!clip.IsComplete)
                    {
                        return value;
                    }

                    _clips.RemoveAt(0);
                    if (_clips.Count == 0)
                    {
                        return value;
                    }

                    if (remainingDeltaTime <= 0f)
                    {
                        return value;
                    }
                }

                return fallbackValue;
            }
        }

        private sealed class RotationClip
        {
            private RotationClip(Quaternion startValue, Quaternion endValue, float durationSeconds)
            {
                StartValue = startValue;
                EndValue = endValue;
                DurationSeconds = durationSeconds;
                ElapsedSeconds = 0f;
            }

            public Quaternion StartValue { get; }

            public Quaternion EndValue { get; }

            public float DurationSeconds { get; }

            public float ElapsedSeconds { get; private set; }

            public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

            public bool IsComplete => RemainingSeconds <= 0.0001f;

            public static RotationClip Create(Quaternion startValue, Quaternion endValue, float durationSeconds)
            {
                return new RotationClip(startValue, endValue, Mathf.Max(durationSeconds, 0.0001f));
            }

            public float Advance(float deltaTime)
            {
                if (deltaTime <= 0f)
                {
                    return 0f;
                }

                var consumedTime = Mathf.Min(RemainingSeconds, deltaTime);
                ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
                return Mathf.Max(0f, deltaTime - consumedTime);
            }

            public Quaternion Sample()
            {
                var t = DurationSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(ElapsedSeconds / DurationSeconds);

                return Quaternion.SlerpUnclamped(StartValue, EndValue, EaseOutQuad(t));
            }
        }

        private sealed class MotionTrack
        {
            private readonly List<MotionClip> _clips = new();

            public bool HasClips => _clips.Count > 0;

            public GameplayEntityPose TailEndPose => _clips[_clips.Count - 1].EndPose;

            public TickEntityMotionKind TailMotionKind => _clips[_clips.Count - 1].MotionKind;

            public float TotalRemainingSeconds
            {
                get
                {
                    var total = 0f;
                    for (var i = 0; i < _clips.Count; i++)
                    {
                        total += _clips[i].RemainingSeconds;
                    }

                    return total;
                }
            }

            public void Append(MotionClip clip)
            {
                if (clip == null)
                {
                    throw new ArgumentNullException(nameof(clip));
                }

                _clips.Add(clip);
            }

            public void Clear()
            {
                _clips.Clear();
            }

            public void AlignToCommittedTargetPose(GameplayEntityPose committedTargetPose)
            {
                if (_clips.Count == 0)
                {
                    return;
                }

                var positionDelta = committedTargetPose.Position - TailEndPose.Position;
                if (positionDelta.sqrMagnitude > 0f)
                {
                    for (var i = 0; i < _clips.Count; i++)
                    {
                        _clips[i].Translate(positionDelta);
                    }
                }

                _clips[_clips.Count - 1].SetEndPose(committedTargetPose);
            }

            public GameplayEntityPose SampleAndAdvance(float deltaTime, GameplayEntityPose fallbackPose)
            {
                if (_clips.Count == 0)
                {
                    return fallbackPose;
                }

                var remainingDeltaTime = deltaTime;
                while (_clips.Count > 0)
                {
                    var clip = _clips[0];
                    remainingDeltaTime = clip.Advance(remainingDeltaTime);
                    var pose = clip.IsComplete
                        ? clip.EndPose
                        : clip.Sample();
                    if (!clip.IsComplete)
                    {
                        return pose;
                    }

                    _clips.RemoveAt(0);
                    if (_clips.Count == 0)
                    {
                        return fallbackPose;
                    }

                    if (remainingDeltaTime <= 0f)
                    {
                        return pose;
                    }
                }

                return fallbackPose;
            }
        }

        private sealed class MotionClip
        {
            private readonly float _flipArcHeightWorld;
            private readonly TickEntityMotionKind _motionKind;

            private MotionClip(
                TickEntityMotionKind motionKind,
                GameplayEntityPose startPose,
                GameplayEntityPose endPose,
                float durationSeconds,
                float flipArcHeightWorld)
            {
                _motionKind = motionKind;
                _flipArcHeightWorld = flipArcHeightWorld;
                StartPose = startPose;
                EndPose = endPose;
                DurationSeconds = durationSeconds;
                ElapsedSeconds = 0f;
            }

            public GameplayEntityPose StartPose { get; private set; }

            public GameplayEntityPose EndPose { get; private set; }

            public TickEntityMotionKind MotionKind => _motionKind;

            public float DurationSeconds { get; }

            public float ElapsedSeconds { get; private set; }

            public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

            public bool IsComplete => RemainingSeconds <= 0.0001f;

            public static MotionClip Create(
                TickEntityMotionKind motionKind,
                GameplayEntityPose startPose,
                GameplayEntityPose endPose,
                float durationSeconds,
                float flipArcHeightWorld)
            {
                return new MotionClip(
                    motionKind,
                    startPose,
                    endPose,
                    Mathf.Max(durationSeconds, 0.0001f),
                    flipArcHeightWorld);
            }

            public float Advance(float deltaTime)
            {
                if (deltaTime <= 0f)
                {
                    return 0f;
                }

                var consumedTime = Mathf.Min(RemainingSeconds, deltaTime);
                ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
                return Mathf.Max(0f, deltaTime - consumedTime);
            }

            public void Translate(Vector3 positionDelta)
            {
                if (positionDelta.sqrMagnitude <= 0f)
                {
                    return;
                }

                StartPose = new GameplayEntityPose(StartPose.Position + positionDelta, StartPose.Rotation);
                EndPose = new GameplayEntityPose(EndPose.Position + positionDelta, EndPose.Rotation);
            }

            public void SetEndPose(GameplayEntityPose endPose)
            {
                EndPose = endPose;
            }

            public GameplayEntityPose Sample()
            {
                var t = DurationSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(ElapsedSeconds / DurationSeconds);

                return _motionKind switch
                {
                    TickEntityMotionKind.Flip => SampleFlip(t),
                    _ => SampleLinear(t),
                };
            }

            private GameplayEntityPose SampleLinear(float t)
            {
                var easedT = EaseOutQuad(t);
                return new GameplayEntityPose(
                    Vector3.LerpUnclamped(StartPose.Position, EndPose.Position, easedT),
                    EndPose.Rotation);
            }

            private GameplayEntityPose SampleFlip(float t)
            {
                var easedT = Mathf.Clamp01(t);
                if (!TryResolveFlipBasis(out var liftAxis, out var flipAxis))
                {
                    return SampleLinear(easedT);
                }

                if (!TrySampleFlipArcPosition(easedT, liftAxis, flipAxis, out var position))
                {
                    return SampleLinear(easedT);
                }

                return new GameplayEntityPose(
                    position,
                    SampleFlipRotation(easedT, flipAxis));
            }

            private bool TryResolveFlipBasis(out Vector3 liftAxis, out Vector3 flipAxis)
            {
                liftAxis = default;
                flipAxis = default;

                var travelDelta = EndPose.Position - StartPose.Position;
                if (travelDelta.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                var surfaceNormal = ResolveFlipSurfaceNormal();
                if (surfaceNormal.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                liftAxis = -surfaceNormal.normalized;
                flipAxis = Vector3.Cross(travelDelta.normalized, liftAxis);
                if (flipAxis.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                flipAxis.Normalize();
                return true;
            }

            private Vector3 ResolveFlipSurfaceNormal()
            {
                var startNormal = StartPose.Rotation * Vector3.forward;
                var endNormal = EndPose.Rotation * Vector3.forward;
                var averagedNormal = startNormal + endNormal;

                if (averagedNormal.sqrMagnitude > 0.000001f)
                {
                    return averagedNormal.normalized;
                }

                if (startNormal.sqrMagnitude > 0.000001f)
                {
                    return startNormal.normalized;
                }

                return endNormal.sqrMagnitude > 0.000001f
                    ? endNormal.normalized
                    : Vector3.zero;
            }

            private bool TrySampleFlipArcPosition(
                float normalizedTime,
                Vector3 liftAxis,
                Vector3 flipAxis,
                out Vector3 position)
            {
                position = default;

                var chord = EndPose.Position - StartPose.Position;
                var chordLength = chord.magnitude;
                if (chordLength <= 0.000001f)
                {
                    return false;
                }

                var halfChord = chordLength * 0.5f;
                var sagitta = Mathf.Clamp(_flipArcHeightWorld, 0.0001f, halfChord * 0.95f);
                var radius = ((halfChord * halfChord) + (sagitta * sagitta)) / (2f * sagitta);
                var midpoint = (StartPose.Position + EndPose.Position) * 0.5f;

                // Offset the circle center opposite the lift axis so the sampled midpoint bulges outward.
                var center = midpoint - (liftAxis * (radius - sagitta));
                var startRadius = StartPose.Position - center;
                var endRadius = EndPose.Position - center;
                if (startRadius.sqrMagnitude <= 0.000001f ||
                    endRadius.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                var totalAngle = Vector3.SignedAngle(startRadius, endRadius, flipAxis);
                if (Mathf.Abs(totalAngle) <= 0.0001f)
                {
                    return false;
                }

                var rotatedRadius = Quaternion.AngleAxis(totalAngle * normalizedTime, flipAxis) * startRadius;
                position = center + rotatedRadius;
                return true;
            }

            private Quaternion SampleFlipRotation(float normalizedTime, Vector3 flipAxis)
            {
                var tumbleRotation = Quaternion.AngleAxis(180f * normalizedTime, flipAxis);
                var endCorrection = EndPose.Rotation * Quaternion.Inverse(Quaternion.AngleAxis(180f, flipAxis) * StartPose.Rotation);
                var correctionRotation = Quaternion.Slerp(Quaternion.identity, endCorrection, normalizedTime);
                return correctionRotation * tumbleRotation * StartPose.Rotation;
            }
        }

        private sealed class VisibilityTrack
        {
            private readonly VisibilityClip _clip;

            private VisibilityTrack(VisibilityClip clip)
            {
                _clip = clip ?? throw new ArgumentNullException(nameof(clip));
            }

            public bool IsComplete => _clip.IsComplete;

            public bool IsActive => !_clip.IsComplete;

            public bool TargetVisibility => _clip.FinalVisibility;

            public static VisibilityTrack CreateShow()
            {
                return new VisibilityTrack(VisibilityClip.Create(initialVisibility: false, finalVisibility: true, durationSeconds: 0.0001f, transitionThreshold: 0f));
            }

            public static VisibilityTrack CreateHide(float durationSeconds)
            {
                return new VisibilityTrack(VisibilityClip.Create(initialVisibility: true, finalVisibility: false, durationSeconds, transitionThreshold: 1f));
            }

            public bool SampleAndAdvance(float deltaTime, bool fallbackVisibility)
            {
                return _clip.SampleAndAdvance(deltaTime, fallbackVisibility);
            }
        }

        private sealed class VisibilityClip
        {
            private VisibilityClip(
                bool initialVisibility,
                bool finalVisibility,
                float durationSeconds,
                float transitionThreshold)
            {
                InitialVisibility = initialVisibility;
                FinalVisibility = finalVisibility;
                DurationSeconds = Mathf.Max(durationSeconds, 0.0001f);
                TransitionThreshold = Mathf.Clamp01(transitionThreshold);
                ElapsedSeconds = 0f;
            }

            public bool InitialVisibility { get; }

            public bool FinalVisibility { get; }

            public float DurationSeconds { get; }

            public float TransitionThreshold { get; }

            public float ElapsedSeconds { get; private set; }

            public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

            public bool IsComplete => RemainingSeconds <= 0.0001f;

            public static VisibilityClip Create(
                bool initialVisibility,
                bool finalVisibility,
                float durationSeconds,
                float transitionThreshold)
            {
                return new VisibilityClip(
                    initialVisibility,
                    finalVisibility,
                    durationSeconds,
                    transitionThreshold);
            }

            public bool SampleAndAdvance(float deltaTime, bool fallbackVisibility)
            {
                if (deltaTime > 0f)
                {
                    ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
                }

                if (DurationSeconds <= 0f)
                {
                    return FinalVisibility;
                }

                var normalizedTime = Mathf.Clamp01(ElapsedSeconds / DurationSeconds);
                return normalizedTime >= TransitionThreshold
                    ? FinalVisibility
                    : InitialVisibility;
            }
        }

        private static float EaseOutQuad(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - (inverse * inverse);
        }
    }
}
