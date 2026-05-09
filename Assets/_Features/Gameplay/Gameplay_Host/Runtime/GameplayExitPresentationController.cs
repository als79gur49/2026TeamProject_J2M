using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayExitPresentationController
    {
        private readonly HashSet<int> _exitOwnedEntityIds = new();
        private readonly HashSet<int> _deferredAfterEntityMotionExitIds = new();
        private readonly HashSet<int> _entitiesWithDestroySelfFlipImpact = new();
        private readonly Dictionary<int, EntityExitPresentationTiming> _exitTimingsByEntityId = new();
        private readonly List<int> _completedDeferredExitIds = new();
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

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
            _ = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
        }

        public void Reset()
        {
            _exitOwnedEntityIds.Clear();
            _deferredAfterEntityMotionExitIds.Clear();
            _entitiesWithDestroySelfFlipImpact.Clear();
            _exitTimingsByEntityId.Clear();
            _completedDeferredExitIds.Clear();
        }

        public bool IsExitOwned(int entityId)
        {
            return _exitOwnedEntityIds.Contains(entityId) ||
                   _deferredAfterEntityMotionExitIds.Contains(entityId);
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

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                var signal = presentationData.EntityExitSignals[i];
                _exitOwnedEntityIds.Add(signal.ExitedEntityId);
                _exitTimingsByEntityId[signal.ExitedEntityId] = signal.Timing;
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

                ApplyImmediateExitCleanup(entityId, queueFlipInteractionReset: true);
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
    }
}
