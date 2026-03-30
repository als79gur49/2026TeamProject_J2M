using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayTickViewPresenter : MonoBehaviour
    {
        private readonly AnchorTrack _topologyTrack = new();
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

        private Vector3 _committedContinuityAnchor;
        private CubeTopologyState _committedTopology;
        private bool _hasAnyCommittedFrame;
        private bool _hasPresentedFrame;
        private bool _isInitialized;
        private Vector3 _presentedContinuityAnchor;
        private GameplayCubeProjector _projector;
        private GameplayTimingProfile _timingProfile;
        private GameplayEntityViewBinder _viewBinder;

        public event Action<Vector3> StripCenterChanged;

        public Vector3 ActiveStripCenter { get; private set; }

        public Vector3 ContinuityAnchor => _presentedContinuityAnchor;

        public CubeTopologyState CurrentTopology => _committedTopology;

        public void Initialize(
            GameplayEntityViewBinder viewBinder,
            BoardBounds boardBounds,
            CubeTopologyState initialTopology,
            Vector3 gridOrigin,
            float cellSize,
            GameplayTimingProfile timingProfile)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _viewBinder = viewBinder;
            _projector = new GameplayCubeProjector(boardBounds, gridOrigin, cellSize);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _committedTopology = initialTopology;
            _committedContinuityAnchor = Vector3.zero;
            _presentedContinuityAnchor = Vector3.zero;
            ActiveStripCenter = _projector.GetCubeCenter();
            _hasPresentedFrame = false;
            _hasAnyCommittedFrame = false;
            _committedLocalTargetPoses.Clear();
            _entityTypesByEntityId.Clear();
            _retainedLocalTargetPoses.Clear();
            _localMotionTracks.Clear();
            _visibilityTracks.Clear();
            _viewsByEntityId.Clear();
            _topologyTrack.Clear();
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
            StoreCommittedFrame(result.FinalEntities, result.FinalTopology, updateContinuity: true);
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
            _topologyTrack.Clear();
            _committedContinuityAnchor = Vector3.zero;
            _presentedContinuityAnchor = Vector3.zero;
            _committedTopology = topology;
            StoreCommittedFrame(entities, topology, updateContinuity: false);
            ApplyPresentedContinuityAnchor(_committedContinuityAnchor, forceNotify: true);
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

            var presentedContinuityAnchor = _topologyTrack.HasClips
                ? _topologyTrack.SampleAndAdvance(deltaTime, _committedContinuityAnchor)
                : _committedContinuityAnchor;
            ApplyPresentedContinuityAnchor(presentedContinuityAnchor);

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
                view.ApplyLocalPose(localPose.Position + _presentedContinuityAnchor, localPose.Rotation);
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
            CubeTopologyState topology,
            bool updateContinuity)
        {
            if (updateContinuity && _hasPresentedFrame)
            {
                _committedContinuityAnchor = Vector3.zero;
            }

            _committedTopology = topology;
            StoreCommittedEntityTargets(entities, topology);
            _hasPresentedFrame = true;
            _hasAnyCommittedFrame = true;
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
                _committedLocalTargetPoses[entity.entityId] = CreateEntityPose(projectedPose, entity.facing);
            }
        }

        private void RefreshTopologyTrack(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _topologyTrack.Clear();
            ApplyPresentedContinuityAnchor(_committedContinuityAnchor);
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

            pose = CreateEntityPose(projectedPose, facing);
            return true;
        }

        private static GameplayEntityPose CreateEntityPose(ProjectedCellPose projectedPose, Direction facing)
        {
            return new GameplayEntityPose(
                projectedPose.LocalPosition,
                projectedPose.LocalRotation * ResolveFacingLocalRotation(facing));
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

        private float ResolveTopologyMotionDurationSeconds(TickTopologyMotion topologyMotion)
        {
            return topologyMotion.RotationKind == CubeRotationKind.None
                ? _timingProfile.PushMotionDurationSeconds
                : _timingProfile.PushMotionDurationSeconds;
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

        private void ApplyPresentedContinuityAnchor(Vector3 continuityAnchor, bool forceNotify = false)
        {
            if (!forceNotify &&
                (_presentedContinuityAnchor - continuityAnchor).sqrMagnitude <= 0.000001f)
            {
                return;
            }

            _presentedContinuityAnchor = continuityAnchor;
            ActiveStripCenter = _projector.GetCubeCenter();
            StripCenterChanged?.Invoke(ActiveStripCenter);
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

        private static Quaternion ResolveFacingLocalRotation(Direction facing)
        {
            var zRotation = facing switch
            {
                Direction.Up => 0f,
                Direction.Right => -90f,
                Direction.Down => 180f,
                Direction.Left => 90f,
                _ => 0f,
            };

            return Quaternion.Euler(0f, 0f, zRotation);
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("GameplayTickViewPresenter must be initialized before use.");
            }
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

        private sealed class AnchorTrack
        {
            private readonly List<AnchorClip> _clips = new();

            public bool HasClips => _clips.Count > 0;

            public Vector3 TailEndValue => _clips[_clips.Count - 1].EndValue;

            public void Append(AnchorClip clip)
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

            public Vector3 SampleAndAdvance(float deltaTime, Vector3 fallbackValue)
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

        private sealed class AnchorClip
        {
            private AnchorClip(Vector3 startValue, Vector3 endValue, float durationSeconds)
            {
                StartValue = startValue;
                EndValue = endValue;
                DurationSeconds = durationSeconds;
                ElapsedSeconds = 0f;
            }

            public Vector3 StartValue { get; }

            public Vector3 EndValue { get; }

            public float DurationSeconds { get; }

            public float ElapsedSeconds { get; private set; }

            public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

            public bool IsComplete => RemainingSeconds <= 0.0001f;

            public static AnchorClip Create(Vector3 startValue, Vector3 endValue, float durationSeconds)
            {
                return new AnchorClip(startValue, endValue, Mathf.Max(durationSeconds, 0.0001f));
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

            public Vector3 Sample()
            {
                var t = DurationSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(ElapsedSeconds / DurationSeconds);

                return Vector3.LerpUnclamped(StartValue, EndValue, EaseOutQuad(t));
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
                var controlPoint = (StartPose.Position + EndPose.Position) * 0.5f + (Vector3.up * _flipArcHeightWorld);
                var firstLerp = Vector3.LerpUnclamped(StartPose.Position, controlPoint, easedT);
                var secondLerp = Vector3.LerpUnclamped(controlPoint, EndPose.Position, easedT);
                var position = Vector3.LerpUnclamped(firstLerp, secondLerp, easedT);
                var baseRotation = Quaternion.Slerp(StartPose.Rotation, EndPose.Rotation, easedT);
                var flipRotation = Quaternion.AngleAxis(180f * Mathf.Sin(Mathf.PI * easedT), Vector3.forward);
                return new GameplayEntityPose(position, flipRotation * baseRotation);
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
