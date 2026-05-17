using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayExitPresentationController
    {
        private readonly HashSet<int> _exitOwnedEntityIds = new();
        private readonly HashSet<int> _deferredAfterEntityMotionExitIds = new();
        private readonly HashSet<int> _contactDelayedExitIds = new();
        private readonly HashSet<int> _entitiesWithDestroySelfFlipImpact = new();
        private readonly Dictionary<int, EntityExitPresentationTiming> _exitTimingsByEntityId = new();
        private readonly Dictionary<int, float> _exitContactTimesByEntityId = new();
        private readonly Dictionary<int, PendingEntityExitPresentation> _pendingContactDelayedExits = new();
        private readonly List<int> _completedDeferredExitIds = new();
        private readonly List<int> _completedContactDelayedExitIds = new();
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private GameplayTimingProfile _timingProfile;

        public GameplayExitPresentationController(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
        }

        public void Configure(GameplayCubeProjector projector, GameplayTimingProfile timingProfile)
        {
            _ = projector ?? throw new ArgumentNullException(nameof(projector));
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
        }

        public void Reset()
        {
            _exitOwnedEntityIds.Clear();
            _deferredAfterEntityMotionExitIds.Clear();
            _contactDelayedExitIds.Clear();
            _entitiesWithDestroySelfFlipImpact.Clear();
            _exitTimingsByEntityId.Clear();
            _exitContactTimesByEntityId.Clear();
            _pendingContactDelayedExits.Clear();
            _completedDeferredExitIds.Clear();
            _completedContactDelayedExitIds.Clear();
        }

        public bool IsExitOwned(int entityId)
        {
            return _exitOwnedEntityIds.Contains(entityId) ||
                   _deferredAfterEntityMotionExitIds.Contains(entityId) ||
                   _contactDelayedExitIds.Contains(entityId);
        }

        public void RefreshEntityExitPlan(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _exitOwnedEntityIds.Clear();
            _entitiesWithDestroySelfFlipImpact.Clear();
            _exitTimingsByEntityId.Clear();
            _exitContactTimesByEntityId.Clear();

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                var signal = presentationData.EntityExitSignals[i];
                _exitOwnedEntityIds.Add(signal.ExitedEntityId);
                _exitTimingsByEntityId[signal.ExitedEntityId] = signal.Timing;
                _exitContactTimesByEntityId[signal.ExitedEntityId] = signal.VisualContactNormalizedTime;
            }

            for (var i = 0; i < presentationData.FlipImpactSignals.Count; i++)
            {
                var signal = presentationData.FlipImpactSignals[i];
                if (signal.Disposition != FlipImpactPresentationDisposition.DestroySelf)
                {
                    continue;
                }

                if (_entitiesWithDestroySelfFlipImpact.Contains(signal.BoxEntityId))
                {
                    continue;
                }

                _entitiesWithDestroySelfFlipImpact.Add(signal.BoxEntityId);
            }
        }

        public void ApplyEntityExitOwnership()
        {
            foreach (var entityId in _exitOwnedEntityIds)
            {
                var timing = _exitTimingsByEntityId.TryGetValue(entityId, out var resolvedTiming)
                    ? resolvedTiming
                    : EntityExitPresentationTiming.Immediate;
                if (timing == EntityExitPresentationTiming.AfterEntityMotion &&
                    _trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                    motionTrack.HasClips)
                {
                    ApplyDeferredAfterEntityMotionExitStart(entityId);
                    continue;
                }

                if (timing == EntityExitPresentationTiming.AtContactTime)
                {
                    ApplyContactDelayedExitStart(entityId);
                    continue;
                }

                ApplyImmediateExitCleanup(entityId, queueFlipInteractionReset: true);
            }
        }

        public void AdvanceContactDelayedEntityExits(float deltaTime)
        {
            if (_pendingContactDelayedExits.Count == 0)
            {
                return;
            }

            var advanceSeconds = Math.Max(0f, deltaTime);
            _completedContactDelayedExitIds.Clear();
            foreach (var entityId in _contactDelayedExitIds)
            {
                if (!_pendingContactDelayedExits.TryGetValue(entityId, out var existing))
                {
                    _completedContactDelayedExitIds.Add(entityId);
                    continue;
                }

                var pending = existing.Advance(advanceSeconds);
                if (pending.RemainingSeconds > 0.0001f)
                {
                    _pendingContactDelayedExits[entityId] = pending;
                    continue;
                }

                _completedContactDelayedExitIds.Add(entityId);
            }

            for (var i = 0; i < _completedContactDelayedExitIds.Count; i++)
            {
                var entityId = _completedContactDelayedExitIds[i];
                _pendingContactDelayedExits.Remove(entityId);
                _contactDelayedExitIds.Remove(entityId);
                ApplyImmediateExitCleanup(entityId, queueFlipInteractionReset: false);
            }
        }

        public void CompleteDeferredEntityExits()
        {
            if (_deferredAfterEntityMotionExitIds.Count == 0)
            {
                return;
            }

            _completedDeferredExitIds.Clear();
            foreach (var entityId in _deferredAfterEntityMotionExitIds)
            {
                if (_trackState.LocalMotionTracks.TryGetValue(entityId, out var motionTrack) &&
                    motionTrack.HasClips)
                {
                    continue;
                }

                _completedDeferredExitIds.Add(entityId);
            }

            for (var i = 0; i < _completedDeferredExitIds.Count; i++)
            {
                var entityId = _completedDeferredExitIds[i];
                _deferredAfterEntityMotionExitIds.Remove(entityId);
                _trackState.DeferredExitRetainedEntityIds.Remove(entityId);
                ApplyImmediateExitCleanup(entityId, queueFlipInteractionReset: false);
            }
        }

        private void ApplyDeferredAfterEntityMotionExitStart(int entityId)
        {
            if (!_entitiesWithDestroySelfFlipImpact.Contains(entityId))
            {
                QueueFlipInteractionReset(entityId);
            }

            _deferredAfterEntityMotionExitIds.Add(entityId);
            _trackState.DeferredExitRetainedEntityIds.Add(entityId);
            _trackState.JumpTracks.Remove(entityId);
            _trackState.OriginalViewMotionTracks.Remove(entityId);
            _trackState.VisibilityTracks.Remove(entityId);
            _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
            _stateStore.TransitionVisibilityStates.Remove(entityId);
        }

        private void ApplyContactDelayedExitStart(int entityId)
        {
            if (_pendingContactDelayedExits.ContainsKey(entityId))
            {
                return;
            }

            if (!_entitiesWithDestroySelfFlipImpact.Contains(entityId))
            {
                QueueFlipInteractionReset(entityId);
            }

            _contactDelayedExitIds.Add(entityId);
            var normalizedTime = _exitContactTimesByEntityId.TryGetValue(entityId, out var resolvedNormalizedTime) &&
                                 resolvedNormalizedTime > 0f
                ? resolvedNormalizedTime
                : GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;
            var flipDurationSeconds = _timingProfile != null
                ? _timingProfile.FlipMotionDurationSeconds
                : GameplayTimingProfile.CreateDefault().FlipMotionDurationSeconds;
            _pendingContactDelayedExits[entityId] =
                new PendingEntityExitPresentation(entityId, flipDurationSeconds * normalizedTime);

            if (!_stateStore.RetainedLocalTargetPoses.ContainsKey(entityId))
            {
                if (_stateStore.PresentedLocalPosesByEntityId.TryGetValue(entityId, out var presentedPose))
                {
                    _stateStore.RetainedLocalTargetPoses[entityId] = presentedPose;
                }
                else if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                         view != null)
                {
                    _stateStore.RetainedLocalTargetPoses[entityId] =
                        new GameplayEntityPose(view.transform.localPosition, view.transform.localRotation);
                }
            }

            _trackState.JumpTracks.Remove(entityId);
            _trackState.LocalMotionTracks.Remove(entityId);
            _trackState.OriginalViewMotionTracks.Remove(entityId);
            _trackState.VisibilityTracks.Remove(entityId);
            _trackState.DeferredExitRetainedEntityIds.Remove(entityId);
            _stateStore.CommittedLocalTargetPoses.Remove(entityId);
            _stateStore.CommittedFacesByEntityId.Remove(entityId);
            _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
            _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
            _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
            _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
            _stateStore.TransitionVisibilityStates.Remove(entityId);
        }

        private void ApplyImmediateExitCleanup(int entityId, bool queueFlipInteractionReset)
        {
            if (queueFlipInteractionReset &&
                !_entitiesWithDestroySelfFlipImpact.Contains(entityId))
            {
                QueueFlipInteractionReset(entityId);
            }

            _trackState.JumpTracks.Remove(entityId);
            _trackState.LocalMotionTracks.Remove(entityId);
            _trackState.OriginalViewMotionTracks.Remove(entityId);
            _trackState.VisibilityTracks.Remove(entityId);
            _trackState.DeferredExitRetainedEntityIds.Remove(entityId);
            _stateStore.CommittedLocalTargetPoses.Remove(entityId);
            _stateStore.CommittedFacesByEntityId.Remove(entityId);
            _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
            _stateStore.EnemyAiModesByEntityId.Remove(entityId);
            _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
            _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
            _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
            _stateStore.RetainedLocalTargetPoses.Remove(entityId);
            _stateStore.TransitionVisibilityStates.Remove(entityId);
            _stateStore.EntityTypesByEntityId.Remove(entityId);
            _stateStore.UnitRolesByEntityId.Remove(entityId);

            if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                view != null)
            {
                view.SetVisible(false);
            }
        }

        private void QueueFlipInteractionReset(int exitedEntityId)
        {
            _trackState.CompletedFlipInteractionTrackIds.Clear();

            foreach (var pair in _trackState.FlipInteractionTracks)
            {
                var track = pair.Value;
                if (track.PlayerEntityId != exitedEntityId &&
                    track.BoxEntityId != exitedEntityId)
                {
                    continue;
                }

                _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                _trackState.FlipInteractionResetRequests.Add(
                    new FlipInteractionResetRequest(track.PlayerEntityId, track.BoxEntityId));
            }

            for (var i = 0; i < _trackState.CompletedFlipInteractionTrackIds.Count; i++)
            {
                _trackState.FlipInteractionTracks.Remove(_trackState.CompletedFlipInteractionTrackIds[i]);
            }
        }

        private readonly struct PendingEntityExitPresentation
        {
            public PendingEntityExitPresentation(int entityId, float remainingSeconds)
            {
                EntityId = entityId;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
            }

            public int EntityId { get; }

            public float RemainingSeconds { get; }

            public PendingEntityExitPresentation Advance(float deltaTime)
            {
                return new PendingEntityExitPresentation(EntityId, RemainingSeconds - Math.Max(0f, deltaTime));
            }
        }
    }
}
