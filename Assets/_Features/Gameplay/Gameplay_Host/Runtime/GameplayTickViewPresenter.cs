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
        private readonly Dictionary<int, MotionClip> _activeMotionClips = new();
        private readonly Dictionary<int, GameplayEntityPose> _steadyPoses = new();
        private readonly List<int> _completedClipIds = new();
        private readonly Dictionary<int, GameplayEntityView> _viewsByEntityId = new();
        private readonly HashSet<int> _visibleEntityIds = new();

        private CubeTopologyState _currentTopology;
        private bool _hasAnyAuthoritativeFrame;
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
            _hasAnyAuthoritativeFrame = false;
            _steadyPoses.Clear();
            _activeMotionClips.Clear();
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
            var previousAuthoritativePoses = new Dictionary<int, GameplayEntityPose>(_steadyPoses);
            var clipStartPoses = CaptureRenderedPoses(_activeMotionClips.Keys);
            var motionStartPoses = CaptureRenderedPoses(result.PresentationData.EntityMotions);

            StoreAuthoritativeFrame(result.FinalEntities, result.FinalTopology, updateContinuity: true);
            RefreshMotionClips(
                result.PresentationData,
                motionStartPoses,
                clipStartPoses,
                previousAuthoritativePoses);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();
            _activeMotionClips.Clear();
            StoreAuthoritativeFrame(entities, topology, updateContinuity: false);
            UpdatePresentation(0f);
        }

        public void UpdatePresentation(float deltaTime)
        {
            EnsureInitialized();

            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (!_hasAnyAuthoritativeFrame)
            {
                return;
            }

            _completedClipIds.Clear();

            foreach (var pair in _steadyPoses)
            {
                var entityId = pair.Key;
                if (!_viewsByEntityId.TryGetValue(entityId, out var view) || view == null)
                {
                    continue;
                }

                view.SetVisible(true);

                var pose = pair.Value;
                if (_activeMotionClips.TryGetValue(entityId, out var clip))
                {
                    clip.Advance(deltaTime);
                    pose = clip.Sample();
                    if (clip.IsComplete)
                    {
                        pose = pair.Value;
                        _completedClipIds.Add(entityId);
                    }
                }

                view.ApplyPose(pose.Position, pose.Rotation);
            }

            for (var i = 0; i < _completedClipIds.Count; i++)
            {
                _activeMotionClips.Remove(_completedClipIds[i]);
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

        private void StoreAuthoritativeFrame(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            bool updateContinuity)
        {
            if (updateContinuity && _hasPresentedFrame)
            {
                UpdateContinuityAnchor(_currentTopology, topology);
            }

            _currentTopology = topology;
            StoreAuthoritativeEntities(entities, topology);

            ActiveStripCenter = _projector.GetActiveStripCenter(ContinuityAnchor);
            StripCenterChanged?.Invoke(ActiveStripCenter);
            _hasPresentedFrame = true;
            _hasAnyAuthoritativeFrame = true;
        }

        private void StoreAuthoritativeEntities(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _visibleEntityIds.Clear();
            _steadyPoses.Clear();

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
                _steadyPoses[entity.entityId] = new GameplayEntityPose(
                    worldPosition,
                    ResolveWorldRotation(entity.facing));
            }
        }

        private void RefreshMotionClips(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityPose> motionStartPoses,
            IReadOnlyDictionary<int, GameplayEntityPose> clipStartPoses,
            IReadOnlyDictionary<int, GameplayEntityPose> previousAuthoritativePoses)
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

            _completedClipIds.Clear();
            foreach (var pair in _activeMotionClips)
            {
                if (!_steadyPoses.TryGetValue(pair.Key, out var authoritativePose))
                {
                    _completedClipIds.Add(pair.Key);
                    continue;
                }

                if (motionEntityIds.Contains(pair.Key))
                {
                    continue;
                }

                if (!clipStartPoses.TryGetValue(pair.Key, out var startPose))
                {
                    startPose = pair.Value.Sample();
                }

                pair.Value.Rebase(startPose, authoritativePose);
            }

            for (var i = 0; i < _completedClipIds.Count; i++)
            {
                _activeMotionClips.Remove(_completedClipIds[i]);
            }

            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var motion = presentationData.EntityMotions[i];
                if (!_steadyPoses.TryGetValue(motion.EntityId, out var authoritativePose))
                {
                    _activeMotionClips.Remove(motion.EntityId);
                    continue;
                }

                var startPose = ResolveMotionStartPose(
                    motion,
                    motionStartPoses,
                    previousAuthoritativePoses,
                    authoritativePose);
                if (previousAuthoritativePoses.TryGetValue(motion.EntityId, out var previousAuthoritativePose))
                {
                    startPose = new GameplayEntityPose(startPose.Position, previousAuthoritativePose.Rotation);
                }

                _activeMotionClips[motion.EntityId] = MotionClip.Create(
                    motion.MotionKind,
                    startPose,
                    authoritativePose,
                    ResolveMotionDurationSeconds(motion.MotionKind),
                    _timingProfile.FlipArcHeightInCells * _projector.CellSize);
            }
        }

        private IReadOnlyDictionary<int, GameplayEntityPose> CaptureRenderedPoses(IEnumerable<int> entityIds)
        {
            var poses = new Dictionary<int, GameplayEntityPose>();
            if (entityIds == null)
            {
                return poses;
            }

            foreach (var entityId in entityIds)
            {
                if (!_viewsByEntityId.TryGetValue(entityId, out var view) || view == null)
                {
                    continue;
                }

                poses[entityId] = new GameplayEntityPose(view.transform.position, view.transform.rotation);
            }

            return poses;
        }

        private IReadOnlyDictionary<int, GameplayEntityPose> CaptureRenderedPoses(IReadOnlyList<TickEntityMotion> motions)
        {
            var poses = new Dictionary<int, GameplayEntityPose>();
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (poses.ContainsKey(motion.EntityId))
                {
                    continue;
                }

                if (!_viewsByEntityId.TryGetValue(motion.EntityId, out var view) || view == null)
                {
                    continue;
                }

                poses[motion.EntityId] = new GameplayEntityPose(view.transform.position, view.transform.rotation);
            }

            return poses;
        }

        private GameplayEntityPose ResolveMotionStartPose(
            TickEntityMotion motion,
            IReadOnlyDictionary<int, GameplayEntityPose> motionStartPoses,
            IReadOnlyDictionary<int, GameplayEntityPose> previousAuthoritativePoses,
            GameplayEntityPose authoritativePose)
        {
            if (motionStartPoses.TryGetValue(motion.EntityId, out var startPose))
            {
                return startPose;
            }

            if (previousAuthoritativePoses.TryGetValue(motion.EntityId, out var previousAuthoritativePose))
            {
                return previousAuthoritativePose;
            }

            if (_projector.TryProject(
                    motion.SourceCell,
                    _currentTopology,
                    ContinuityAnchor,
                    out var sourceWorldPosition))
            {
                return new GameplayEntityPose(sourceWorldPosition, authoritativePose.Rotation);
            }

            return authoritativePose;
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

            public void Advance(float deltaTime)
            {
                ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
            }

            public void Rebase(GameplayEntityPose startPose, GameplayEntityPose endPose)
            {
                var remainingSeconds = RemainingSeconds;
                StartPose = startPose;
                EndPose = endPose;
                DurationSeconds = Mathf.Max(remainingSeconds, 0.0001f);
                ElapsedSeconds = 0f;
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
