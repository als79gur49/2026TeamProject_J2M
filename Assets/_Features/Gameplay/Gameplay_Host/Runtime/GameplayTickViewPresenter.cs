using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplaySurfaceProjector
    {
        private readonly BoardBounds _boardBounds;
        private readonly float _cellSize;
        private readonly Vector3 _gridOrigin;

        public GameplaySurfaceProjector(
            BoardBounds boardBounds,
            Vector3 gridOrigin,
            float cellSize)
        {
            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException("GameplaySurfaceProjector requires bounded board bounds.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            _boardBounds = boardBounds;
            _gridOrigin = gridOrigin;
            _cellSize = cellSize;
        }

        public BoardBounds BoardBounds => _boardBounds;

        public float CellSize => _cellSize;

        public Vector3 GridOrigin => _gridOrigin;

        public int Width => _boardBounds.MaxInclusive.x - _boardBounds.MinInclusive.x + 1;

        public int Height => _boardBounds.MaxInclusive.y - _boardBounds.MinInclusive.y + 1;

        public bool TryProject(
            SurfaceCell cell,
            CubeTopologyState topology,
            Vector3 continuityAnchor,
            out Vector3 worldPosition)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) || !topology.IsFaceActive(cell.face))
            {
                worldPosition = default;
                return false;
            }

            var localX = (cell.x - _boardBounds.MinInclusive.x) * _cellSize;
            var localY = (cell.y - _boardBounds.MinInclusive.y) * _cellSize;
            var faceOffsetY = ResolveFaceOffsetY(cell.face, topology);
            worldPosition = _gridOrigin + continuityAnchor + new Vector3(localX, localY + faceOffsetY, 0f);
            return true;
        }

        public Vector3 GetActiveStripCenter(Vector3 continuityAnchor)
        {
            return _gridOrigin +
                   continuityAnchor +
                   new Vector3(
                       (Width - 1) * _cellSize * 0.5f,
                       ((Height * 2) - 1) * _cellSize * 0.5f,
                       0f);
        }

        private float ResolveFaceOffsetY(FaceId face, CubeTopologyState topology)
        {
            if (face == topology.BottomFace)
            {
                return 0f;
            }

            if (face == topology.FrontFace)
            {
                return Height * _cellSize;
            }

            throw new InvalidOperationException(
                $"Cannot project inactive face {face}. Bottom={topology.BottomFace}, Front={topology.FrontFace}");
        }
    }

    public sealed class GameplayTickViewPresenter : MonoBehaviour
    {
        private readonly Dictionary<int, MotionTrack> _motionTracks = new();
        private readonly Dictionary<int, GameplayEntityPose> _committedTargetPoses = new();
        private readonly List<int> _completedTrackIds = new();
        private readonly Dictionary<int, GameplayEntityView> _viewsByEntityId = new();
        private readonly HashSet<int> _visibleEntityIds = new();

        private CubeTopologyState _currentTopology;
        private bool _hasAnyCommittedFrame;
        private GameplayTimingProfile _timingProfile;
        private GameplaySurfaceProjector _projector;
        private GameplayEntityViewBinder _viewBinder;
        private bool _hasPresentedFrame;
        private bool _isInitialized;

        public event Action<Vector3> StripCenterChanged;

        public Vector3 ActiveStripCenter { get; private set; }

        public Vector3 ContinuityAnchor { get; private set; }

        public CubeTopologyState CurrentTopology => _currentTopology;

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
            _projector = new GameplaySurfaceProjector(boardBounds, gridOrigin, cellSize);
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _currentTopology = initialTopology;
            ContinuityAnchor = Vector3.zero;
            ActiveStripCenter = _projector.GetActiveStripCenter(ContinuityAnchor);
            _hasPresentedFrame = false;
            _hasAnyCommittedFrame = false;
            _committedTargetPoses.Clear();
            _motionTracks.Clear();
            _viewsByEntityId.Clear();
            _isInitialized = true;
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            EnsureInitialized();
            var previousCommittedTargetPoses = new Dictionary<int, GameplayEntityPose>(_committedTargetPoses);
            var previousTopology = _currentTopology;
            var previousContinuityAnchor = ContinuityAnchor;

            // WorldState remains authoritative; presenter only caches the latest committed render targets.
            StoreCommittedFrame(result.FinalEntities, result.FinalTopology, updateContinuity: true);
            RefreshMotionClips(
                result.PresentationData,
                previousCommittedTargetPoses,
                previousTopology,
                previousContinuityAnchor);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();
            _motionTracks.Clear();
            StoreCommittedFrame(entities, topology, updateContinuity: false);
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

            _completedTrackIds.Clear();

            foreach (var pair in _committedTargetPoses)
            {
                var entityId = pair.Key;
                if (!_viewsByEntityId.TryGetValue(entityId, out var view) || view == null)
                {
                    continue;
                }

                view.SetVisible(true);

                var pose = pair.Value;
                if (_motionTracks.TryGetValue(entityId, out var track))
                {
                    pose = track.SampleAndAdvance(deltaTime, pair.Value);
                    if (!track.HasClips)
                    {
                        _completedTrackIds.Add(entityId);
                    }
                }

                view.ApplyPose(pose.Position, pose.Rotation);
            }

            for (var i = 0; i < _completedTrackIds.Count; i++)
            {
                _motionTracks.Remove(_completedTrackIds[i]);
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
                UpdateContinuityAnchor(_currentTopology, topology);
            }

            _currentTopology = topology;
            StoreCommittedEntityTargets(entities, topology);

            ActiveStripCenter = _projector.GetActiveStripCenter(ContinuityAnchor);
            StripCenterChanged?.Invoke(ActiveStripCenter);
            _hasPresentedFrame = true;
            _hasAnyCommittedFrame = true;
        }

        private void StoreCommittedEntityTargets(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _visibleEntityIds.Clear();
            _committedTargetPoses.Clear();

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!ShouldPresent(entity, topology) ||
                    !_projector.TryProject(entity.position, topology, ContinuityAnchor, out var worldPosition))
                {
                    continue;
                }

                var view = _viewBinder.ResolveOrCreate(entity);
                if (view == null)
                {
                    continue;
                }

                _viewsByEntityId[entity.entityId] = view;
                _visibleEntityIds.Add(entity.entityId);
                _committedTargetPoses[entity.entityId] = new GameplayEntityPose(
                    worldPosition,
                    ResolveWorldRotation(entity.facing));
            }
        }

        private void RefreshMotionClips(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedTargetPoses,
            CubeTopologyState previousTopology,
            Vector3 previousContinuityAnchor)
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

            _completedTrackIds.Clear();
            foreach (var pair in _motionTracks)
            {
                if (!_committedTargetPoses.TryGetValue(pair.Key, out var committedTargetPose))
                {
                    _completedTrackIds.Add(pair.Key);
                    continue;
                }

                if (motionEntityIds.Contains(pair.Key))
                {
                    continue;
                }

                pair.Value.AlignToCommittedTargetPose(committedTargetPose);
            }

            for (var i = 0; i < _completedTrackIds.Count; i++)
            {
                _motionTracks.Remove(_completedTrackIds[i]);
            }

            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var motion = presentationData.EntityMotions[i];
                if (!_committedTargetPoses.TryGetValue(motion.EntityId, out var committedTargetPose))
                {
                    _motionTracks.Remove(motion.EntityId);
                    continue;
                }

                var startPose = ResolveMotionStartPose(
                    motion,
                    previousCommittedTargetPoses,
                    committedTargetPose,
                    previousTopology,
                    previousContinuityAnchor);
                if (_motionTracks.TryGetValue(motion.EntityId, out var existingTrack) &&
                    existingTrack.HasClips)
                {
                    startPose = existingTrack.TailEndPose;
                }
                else if (previousCommittedTargetPoses.TryGetValue(motion.EntityId, out var previousCommittedTargetPose))
                {
                    startPose = new GameplayEntityPose(startPose.Position, previousCommittedTargetPose.Rotation);
                }

                if (!_motionTracks.TryGetValue(motion.EntityId, out var track))
                {
                    track = new MotionTrack();
                    _motionTracks[motion.EntityId] = track;
                }

                track.Append(MotionClip.Create(
                    motion.MotionKind,
                    startPose,
                    committedTargetPose,
                    ResolveMotionDurationSeconds(motion.MotionKind),
                    _timingProfile.FlipArcHeightInCells * _projector.CellSize));
            }
        }

        private GameplayEntityPose ResolveMotionStartPose(
            TickEntityMotion motion,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedTargetPoses,
            GameplayEntityPose committedTargetPose,
            CubeTopologyState previousTopology,
            Vector3 previousContinuityAnchor)
        {
            if (_motionTracks.TryGetValue(motion.EntityId, out var track) &&
                track.HasClips)
            {
                return track.TailEndPose;
            }

            if (previousCommittedTargetPoses.TryGetValue(motion.EntityId, out var previousCommittedTargetPose))
            {
                return previousCommittedTargetPose;
            }

            if (_projector.TryProject(
                    motion.SourceCell,
                    previousTopology,
                    previousContinuityAnchor,
                    out var sourceWorldPosition))
            {
                return new GameplayEntityPose(sourceWorldPosition, committedTargetPose.Rotation);
            }

            return committedTargetPose;
        }

        private float ResolveMotionDurationSeconds(TickEntityMotionKind motionKind)
        {
            return motionKind == TickEntityMotionKind.Flip
                ? _timingProfile.FlipMotionDurationSeconds
                : _timingProfile.PushMotionDurationSeconds;
        }

        private void UpdateContinuityAnchor(CubeTopologyState previousTopology, CubeTopologyState currentTopology)
        {
            var stepDistance = Vector3.up * (_projector.Height * _projector.CellSize);

            if (FaceIdUtility.GetNext(previousTopology.BottomFace) == currentTopology.BottomFace)
            {
                ContinuityAnchor += stepDistance;
                return;
            }

            if (FaceIdUtility.GetPrevious(previousTopology.BottomFace) == currentTopology.BottomFace)
            {
                ContinuityAnchor -= stepDistance;
            }
        }

        private static bool ShouldPresent(EntityState entity, CubeTopologyState topology)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying &&
                   topology.IsFaceActive(entity.position.face);
        }

        private static Quaternion ResolveWorldRotation(Direction facing)
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

        private sealed class MotionTrack
        {
            private readonly List<MotionClip> _clips = new();

            public bool HasClips => _clips.Count > 0;

            public GameplayEntityPose TailEndPose => _clips[_clips.Count - 1].EndPose;

            public void Append(MotionClip clip)
            {
                if (clip == null)
                {
                    throw new ArgumentNullException(nameof(clip));
                }

                _clips.Add(clip);
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
            private readonly TickEntityMotionKind _motionKind;
            private readonly float _flipArcHeightWorld;

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

            public float DurationSeconds { get; private set; }

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
                    _ => SamplePush(t),
                };
            }

            private GameplayEntityPose SamplePush(float t)
            {
                var easedT = EaseOutQuad(t);
                return new GameplayEntityPose(
                    Vector3.LerpUnclamped(StartPose.Position, EndPose.Position, easedT),
                    EndPose.Rotation);
            }

            private GameplayEntityPose SampleFlip(float t)
            {
                var easedT = EaseOutQuad(t);
                var controlPoint = (StartPose.Position + EndPose.Position) * 0.5f + (Vector3.up * _flipArcHeightWorld);
                var firstLerp = Vector3.LerpUnclamped(StartPose.Position, controlPoint, easedT);
                var secondLerp = Vector3.LerpUnclamped(controlPoint, EndPose.Position, easedT);
                var position = Vector3.LerpUnclamped(firstLerp, secondLerp, easedT);
                var baseRotation = Quaternion.Slerp(StartPose.Rotation, EndPose.Rotation, easedT);
                var flipRotation = Quaternion.AngleAxis(180f * Mathf.Sin(Mathf.PI * easedT), Vector3.forward);
                return new GameplayEntityPose(position, flipRotation * baseRotation);
            }

            private static float EaseOutQuad(float t)
            {
                var inverse = 1f - Mathf.Clamp01(t);
                return 1f - (inverse * inverse);
            }
        }
    }
}
