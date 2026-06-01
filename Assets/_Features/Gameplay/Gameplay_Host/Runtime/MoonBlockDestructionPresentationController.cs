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
        private readonly Dictionary<int, ActiveMoonBlockDestructionSequence> _activeByEntityId = new();
        private readonly List<int> _completedEntityIds = new();
        private readonly Func<int, int, DestroyShrinkVfxSequenceState> _resolveDestroyShrinkState;
        private readonly GameplayExitPresentationController _exitPresentationController;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

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

        public bool HasActiveBlockingSequence => _activeByEntityId.Count > 0;

        public void ResetSession()
        {
            _activeByEntityId.Clear();
            _completedEntityIds.Clear();
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
                _activeByEntityId[signal.ExitedEntityId] = new ActiveMoonBlockDestructionSequence(
                    signal.ExitedEntityId,
                    sequenceId,
                    tickIndex,
                    signal.PresentationTargetCell,
                    signal.Topology,
                    signal.Facing);
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
                if (_activeByEntityId.TryGetValue(request.TargetEntityId, out var sequence))
                {
                    sequence.PendingReappearance = emergenceRequest;
                    sequence.HasPendingReappearance = true;
                    continue;
                }

                emergenceController.QueueRequest(emergenceRequest, currentTickIndex);
            }
        }

        public void UpdateSequences(
            MoonBlockEmergencePresentationController emergenceController,
            int currentTickIndex)
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
                if (!sequence.MotionComplete &&
                    !HasActiveLocalMotion(sequence.EntityId))
                {
                    sequence.MotionComplete = true;
                }

                var vfxState = _resolveDestroyShrinkState(sequence.EntityId, sequence.SequenceId);
                if (!sequence.ShrinkCloneCaptured &&
                    IsCloneCapturedState(vfxState))
                {
                    sequence.ShrinkCloneCaptured = true;
                    ReleaseSourceOwnershipAfterCloneCaptured(sequence, emergenceController);
                }

                if (!sequence.ShrinkCloneCaptured &&
                    sequence.MotionComplete &&
                    (vfxState == DestroyShrinkVfxSequenceState.None ||
                     vfxState == DestroyShrinkVfxSequenceState.Failed))
                {
                    sequence.ShrinkCloneCaptured = true;
                    sequence.ShrinkVfxComplete = true;
                    ReleaseSourceOwnershipAfterCloneCaptured(sequence, emergenceController);
                }

                if (!sequence.ShrinkVfxComplete &&
                    (vfxState == DestroyShrinkVfxSequenceState.Completed ||
                     vfxState == DestroyShrinkVfxSequenceState.Failed))
                {
                    sequence.ShrinkVfxComplete = true;
                }

                if (!sequence.ShrinkVfxComplete)
                {
                    continue;
                }

                if (sequence.HasPendingReappearance)
                {
                    emergenceController.QueueRequest(sequence.PendingReappearance, currentTickIndex);
                }

                _completedEntityIds.Add(sequence.EntityId);
            }

            for (var i = 0; i < _completedEntityIds.Count; i++)
            {
                _activeByEntityId.Remove(_completedEntityIds[i]);
            }

            _completedEntityIds.Clear();
        }

        private static bool IsMoonBlockDestructionCandidate(in TickEntityExitPresentationSignal signal)
        {
            return signal.ExitedEntityId > 0 &&
                   signal.EntityType == EntityType.Box &&
                   signal.ExitCause == TickEntityExitCause.BoxDestroy &&
                   signal.Timing == EntityExitPresentationTiming.AfterEntityMotion &&
                   signal.HasPresentationTargetCell;
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

        private bool HasActiveLocalMotion(int entityId)
        {
            return _trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                   motionTrack.HasClips;
        }

        private void ReleaseSourceOwnershipAfterCloneCaptured(
            ActiveMoonBlockDestructionSequence sequence,
            MoonBlockEmergencePresentationController emergenceController)
        {
            _exitPresentationController.ReleaseDeferredAfterEntityMotionExitOwnership(sequence.EntityId);
            _trackState.LocalMotionTracks.Remove(sequence.EntityId);
            _trackState.MotionVisualScaleEntityIds.Remove(sequence.EntityId);
            _stateStore.RetainedLocalTargetPoses.Remove(sequence.EntityId);
            _stateStore.LastEnemyApplySignaturesByEntityId.Remove(sequence.EntityId);

            if (!_stateStore.ViewsByEntityId.TryGetValue(sequence.EntityId, out var view) ||
                view == null)
            {
                return;
            }

            if (_stateStore.CommittedLocalTargetPoses.TryGetValue(sequence.EntityId, out var committedPose))
            {
                view.ApplyLocalPose(committedPose.Position, committedPose.Rotation);
            }

            view.ResetModelRootVisualScale();
            view.SetVisible(true);
            emergenceController.PrepareHiddenReady(sequence.EntityId);
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

            public bool MotionComplete { get; set; }

            public bool ShrinkCloneCaptured { get; set; }

            public bool ShrinkVfxComplete { get; set; }

            public bool HasPendingReappearance { get; set; }

            public MoonBlockEmergencePresentationRequest PendingReappearance { get; set; }
        }
    }
}
