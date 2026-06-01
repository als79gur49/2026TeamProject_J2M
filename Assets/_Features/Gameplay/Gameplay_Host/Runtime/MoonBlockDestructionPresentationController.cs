using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class MoonBlockDestructionPresentationController
    {
        private const float SequenceTimeoutSeconds = 5f;
        private const string GhostRootPrefix = "MoonBlockDestructionGhost";
        private const string GhostModelRootName = "ModelRoot";

        private readonly Dictionary<int, ActiveMoonBlockDestructionSequence> _activeByEntityId = new();
        private readonly List<int> _completedEntityIds = new();
        private readonly Func<int, int, DestroyShrinkVfxSequenceState> _resolveDestroyShrinkState;
        private readonly GameplayExitPresentationController _exitPresentationController;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private GameplayEntityViewRegistry _viewRegistry;

        public MoonBlockDestructionPresentationController(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayExitPresentationController exitPresentationController,
            Func<int, int, DestroyShrinkVfxSequenceState> resolveDestroyShrinkState)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _exitPresentationController =
                exitPresentationController ?? throw new ArgumentNullException(nameof(exitPresentationController));
            _resolveDestroyShrinkState =
                resolveDestroyShrinkState ?? ((_, _) => DestroyShrinkVfxSequenceState.None);
        }

        public int ActiveGhostCount
        {
            get
            {
                var count = 0;
                foreach (var pair in _activeByEntityId)
                {
                    if (pair.Value.GhostRoot != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void ResetSession()
        {
            CleanupAllSequences();
            _completedEntityIds.Clear();
        }

        public void ConfigureViewRegistry(GameplayEntityViewRegistry viewRegistry)
        {
            if (ReferenceEquals(_viewRegistry, viewRegistry))
            {
                return;
            }

            if (_viewRegistry != null)
            {
                _viewRegistry.ViewUnregistered -= HandleViewUnregistered;
            }

            _viewRegistry = viewRegistry;
            if (_viewRegistry != null)
            {
                _viewRegistry.ViewUnregistered += HandleViewUnregistered;
            }
        }

        public void Dispose()
        {
            ConfigureViewRegistry(null);
            ResetSession();
        }

        public void RefreshSequences(
            TickPresentationData presentationData,
            IReadOnlyList<TilePresentationRequest> tilePresentationRequests,
            int tickIndex)
        {
            if (presentationData == null ||
                tilePresentationRequests == null ||
                tilePresentationRequests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                var signal = presentationData.EntityExitSignals[i];
                if (!IsMoonBlockDestructionCandidate(signal) ||
                    !HasSameTickMoonBlockGeneratedRequest(tilePresentationRequests, signal.ExitedEntityId))
                {
                    continue;
                }

                var sequenceId = signal.PresentationSeed != 0
                    ? signal.PresentationSeed
                    : ComputeDestroyShrinkSequenceId(tickIndex, signal);
                if (_activeByEntityId.TryGetValue(signal.ExitedEntityId, out var existing))
                {
                    CleanupSequence(existing);
                }

                var sequence = new ActiveMoonBlockDestructionSequence(
                    signal.ExitedEntityId,
                    sequenceId,
                    tickIndex,
                    signal.PresentationTargetCell,
                    signal.Topology,
                    signal.Facing);
                CaptureVisualOnlyGhost(sequence);
                _activeByEntityId[signal.ExitedEntityId] = sequence;
            }
        }

        public bool TryGetDestructionMotionTarget(
            int entityId,
            out SurfaceCell targetCell,
            out CubeTopologyState topology,
            out Direction facing)
        {
            if (_activeByEntityId.TryGetValue(entityId, out var sequence))
            {
                targetCell = sequence.DestroyTargetCell;
                topology = sequence.Topology;
                facing = sequence.Facing;
                return true;
            }

            targetCell = default;
            topology = default;
            facing = Direction.None;
            return false;
        }

        public bool ShouldHoldDeferredExitCleanup(int entityId)
        {
            return _activeByEntityId.ContainsKey(entityId);
        }

        public bool ShouldBypassLiveExitOwnership(int entityId)
        {
            return _activeByEntityId.ContainsKey(entityId);
        }

        public bool TryStartDestructionGhostMotion(
            int entityId,
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float durationSeconds)
        {
            if (!_activeByEntityId.TryGetValue(entityId, out var sequence))
            {
                return false;
            }

            sequence.StartGhostMotion(startPose, endPose, durationSeconds);
            _trackState.LocalMotionTracks.Remove(entityId);
            _trackState.MotionVisualScaleEntityIds.Remove(entityId);
            _stateStore.RetainedLocalTargetPoses.Remove(entityId);
            return true;
        }

        public void QueueOrStartMoonBlockGeneratedRequests(
            IReadOnlyList<TilePresentationRequest> requests,
            MoonBlockEmergencePresentationController emergenceController,
            int currentTickIndex)
        {
            if (emergenceController == null)
            {
                throw new ArgumentNullException(nameof(emergenceController));
            }

            if (requests == null || requests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.RequestKind != TilePresentationRequestKind.MoonBlockGenerated ||
                    request.TargetEntityId <= 0)
                {
                    continue;
                }

                var emergenceRequest = new MoonBlockEmergencePresentationRequest(
                    request.TargetEntityId,
                    request.SpawnTick,
                    request.SpawnInteractionLockTicks);
                emergenceController.QueueRequest(emergenceRequest, currentTickIndex);
            }
        }

        public void UpdateSequences(
            MoonBlockEmergencePresentationController emergenceController,
            int currentTickIndex,
            float deltaTime = 0f)
        {
            if (emergenceController == null)
            {
                throw new ArgumentNullException(nameof(emergenceController));
            }

            if (_activeByEntityId.Count == 0)
            {
                return;
            }

            _completedEntityIds.Clear();
            foreach (var pair in _activeByEntityId)
            {
                var sequence = pair.Value;
                sequence.Advance(deltaTime);

                if (sequence.IsTimedOut)
                {
                    _completedEntityIds.Add(sequence.EntityId);
                    continue;
                }

                var vfxState = _resolveDestroyShrinkState(sequence.EntityId, sequence.SequenceId);
                if (IsCloneCapturedState(vfxState))
                {
                    CleanupGhostVisual(sequence);
                }

                if (sequence.MotionComplete &&
                    (vfxState == DestroyShrinkVfxSequenceState.None ||
                     vfxState == DestroyShrinkVfxSequenceState.Failed))
                {
                    _completedEntityIds.Add(sequence.EntityId);
                    continue;
                }

                if (vfxState == DestroyShrinkVfxSequenceState.Completed ||
                    vfxState == DestroyShrinkVfxSequenceState.Failed)
                {
                    _completedEntityIds.Add(sequence.EntityId);
                }
            }

            for (var i = 0; i < _completedEntityIds.Count; i++)
            {
                if (_activeByEntityId.TryGetValue(_completedEntityIds[i], out var sequence))
                {
                    CleanupSequence(sequence);
                    _activeByEntityId.Remove(_completedEntityIds[i]);
                }
            }

            _completedEntityIds.Clear();
        }

        public void ClearForTopologyTransitionStart(MoonBlockEmergencePresentationController emergenceController, int currentTickIndex)
        {
            CleanupAllSequences();
            emergenceController?.NormalizeReadyAndActive(currentTickIndex);
        }

        private static bool IsMoonBlockDestructionCandidate(in TickEntityExitPresentationSignal signal)
        {
            return signal.ExitedEntityId > 0 &&
                   signal.EntityType == EntityType.Box &&
                   signal.ExitCause == TickEntityExitCause.BoxDestroy &&
                   signal.Timing == EntityExitPresentationTiming.AfterEntityMotion &&
                   signal.HasPresentationTargetCell;
        }

        private void HandleViewUnregistered(int entityId, GameplayEntityView view)
        {
            if (!_activeByEntityId.TryGetValue(entityId, out var sequence))
            {
                return;
            }

            CleanupSequence(sequence);
            _activeByEntityId.Remove(entityId);
        }

        private static bool HasSameTickMoonBlockGeneratedRequest(
            IReadOnlyList<TilePresentationRequest> requests,
            int entityId)
        {
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.RequestKind == TilePresentationRequestKind.MoonBlockGenerated &&
                    request.TargetEntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ComputeDestroyShrinkSequenceId(
            int tickIndex,
            in TickEntityExitPresentationSignal signal)
        {
            unchecked
            {
                const int destroyShrinkCueValue = 10;
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.ExitedEntityId;
                hash = (hash * 31) + destroyShrinkCueValue;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        private void CaptureVisualOnlyGhost(ActiveMoonBlockDestructionSequence sequence)
        {
            if (!_stateStore.ViewsByEntityId.TryGetValue(sequence.EntityId, out var view) ||
                view == null ||
                view.ModelRoot == null)
            {
                return;
            }

            var ghostRoot = new GameObject($"{GhostRootPrefix}_{sequence.EntityId}_{sequence.SequenceId}");
            ghostRoot.transform.SetParent(view.transform.parent, worldPositionStays: false);
            ghostRoot.transform.localPosition = view.transform.localPosition;
            ghostRoot.transform.localRotation = view.transform.localRotation;
            ghostRoot.transform.localScale = view.transform.localScale;

            var ghostModelRoot = UnityEngine.Object.Instantiate(
                view.ModelRoot.gameObject,
                ghostRoot.transform,
                worldPositionStays: false);
            ghostModelRoot.name = GhostModelRootName;
            ghostModelRoot.transform.localPosition = view.ModelRoot.localPosition;
            ghostModelRoot.transform.localRotation = view.ModelRoot.localRotation;
            ghostModelRoot.transform.localScale = view.ModelRoot.localScale;
            StripGameplayComponents(ghostModelRoot);
            ghostRoot.SetActive(true);
            sequence.AttachGhost(ghostRoot.transform, ghostModelRoot.transform);
            _stateStore.VfxCloneSourceOverridesByKey[sequence.CloneSourceKey] = ghostModelRoot.transform;
        }

        private static void StripGameplayComponents(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (var i = 0; i < colliders.Length; i++)
            {
                SafeDestroy(colliders[i]);
            }

            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                SafeDestroy(rigidbodies[i]);
            }

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                SafeDestroy(behaviours[i]);
            }
        }

        private void CleanupAllSequences()
        {
            foreach (var pair in _activeByEntityId)
            {
                CleanupSequence(pair.Value);
            }

            _activeByEntityId.Clear();
        }

        private void CleanupSequence(ActiveMoonBlockDestructionSequence sequence)
        {
            CleanupGhostVisual(sequence);
            _exitPresentationController.ReleaseDeferredAfterEntityMotionExitOwnership(sequence.EntityId);
            _trackState.LocalMotionTracks.Remove(sequence.EntityId);
            _trackState.MotionVisualScaleEntityIds.Remove(sequence.EntityId);
            _stateStore.RetainedLocalTargetPoses.Remove(sequence.EntityId);
            _stateStore.LastEnemyApplySignaturesByEntityId.Remove(sequence.EntityId);
        }

        private void CleanupGhostVisual(ActiveMoonBlockDestructionSequence sequence)
        {
            _stateStore.VfxCloneSourceOverridesByKey.Remove(sequence.CloneSourceKey);
            if (sequence.GhostRoot != null)
            {
                SafeDestroy(sequence.GhostRoot.gameObject);
                sequence.ClearGhost();
            }
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private static bool IsCloneCapturedState(DestroyShrinkVfxSequenceState state)
        {
            return state == DestroyShrinkVfxSequenceState.SourceCloneCaptured ||
                   state == DestroyShrinkVfxSequenceState.Playing ||
                   state == DestroyShrinkVfxSequenceState.Completed;
        }

        private sealed class ActiveMoonBlockDestructionSequence
        {
            public ActiveMoonBlockDestructionSequence(
                int entityId,
                int sequenceId,
                int sourceTickIndex,
                SurfaceCell destroyTargetCell,
                CubeTopologyState topology,
                Direction facing)
            {
                EntityId = entityId;
                SequenceId = sequenceId;
                SourceTickIndex = sourceTickIndex;
                DestroyTargetCell = destroyTargetCell;
                Topology = topology;
                Facing = facing;
            }

            public int EntityId { get; }

            public int SequenceId { get; }

            public int SourceTickIndex { get; }

            public SurfaceCell DestroyTargetCell { get; }

            public CubeTopologyState Topology { get; }

            public Direction Facing { get; }

            public GameplayVfxCloneSourceKey CloneSourceKey => new(EntityId, SequenceId);

            public Transform GhostRoot { get; private set; }

            private Transform GhostModelRoot { get; set; }

            private GameplayEntityPose MotionStartPose { get; set; }

            private GameplayEntityPose MotionEndPose { get; set; }

            private float MotionDurationSeconds { get; set; }

            private float MotionElapsedSeconds { get; set; }

            private float SequenceElapsedSeconds { get; set; }

            private bool HasGhostMotion { get; set; }

            public bool MotionComplete { get; private set; }

            public bool IsTimedOut => SequenceElapsedSeconds >= SequenceTimeoutSeconds;

            public void AttachGhost(Transform ghostRoot, Transform ghostModelRoot)
            {
                GhostRoot = ghostRoot;
                GhostModelRoot = ghostModelRoot;
            }

            public void ClearGhost()
            {
                GhostRoot = null;
                GhostModelRoot = null;
            }

            public void StartGhostMotion(
                GameplayEntityPose startPose,
                GameplayEntityPose endPose,
                float durationSeconds)
            {
                MotionStartPose = startPose;
                MotionEndPose = endPose;
                MotionDurationSeconds = Mathf.Max(0.0001f, durationSeconds);
                MotionElapsedSeconds = 0f;
                HasGhostMotion = true;
                MotionComplete = false;
                ApplyGhostPose(startPose);
            }

            public void Advance(float deltaTime)
            {
                SequenceElapsedSeconds += Mathf.Max(0f, deltaTime);
                if (!HasGhostMotion || MotionComplete)
                {
                    return;
                }

                MotionElapsedSeconds += Mathf.Max(0f, deltaTime);
                var progress = Mathf.Clamp01(MotionElapsedSeconds / MotionDurationSeconds);
                var pose = new GameplayEntityPose(
                    Vector3.Lerp(MotionStartPose.Position, MotionEndPose.Position, progress),
                    Quaternion.Slerp(MotionStartPose.Rotation, MotionEndPose.Rotation, progress));
                ApplyGhostPose(pose);
                if (progress >= 1f)
                {
                    MotionComplete = true;
                }
            }

            private void ApplyGhostPose(GameplayEntityPose pose)
            {
                if (GhostRoot == null)
                {
                    MotionComplete = true;
                    return;
                }

                GhostRoot.localPosition = pose.Position;
                GhostRoot.localRotation = pose.Rotation;
            }
        }
    }
}
