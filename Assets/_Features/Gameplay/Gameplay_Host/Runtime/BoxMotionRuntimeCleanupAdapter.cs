using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayBoxMotionRuntimeCleanupAdapter : IBoxMotionRuntimeCleanupPort
    {
        private readonly GameplayEntityPresentationApplier _entityPresentationApplier;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        public GameplayBoxMotionRuntimeCleanupAdapter(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayEntityPresentationApplier entityPresentationApplier)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _entityPresentationApplier =
                entityPresentationApplier ?? throw new ArgumentNullException(nameof(entityPresentationApplier));
        }

        public void ResetSession(BoxMotionTelemetryCleanupReason reason)
        {
            ClearBoxMotionPresentationRuntimeState(reason);
        }

        public void HardCleanup(BoxMotionTelemetryCleanupReason reason)
        {
            ClearBoxMotionPresentationRuntimeState(reason);
        }

        private void ClearBoxMotionPresentationRuntimeState(BoxMotionTelemetryCleanupReason reason)
        {
            var resetResult = _entityPresentationApplier.ResetBoxFlipInteractionsForKnownViews();
            _trackState.FlipInteractionResetRequests.Clear();

            var boxEntityIds = new HashSet<int>();
            foreach (var pair in _stateStore.EntityTypesByEntityId)
            {
                if (pair.Value == EntityType.Box)
                {
                    boxEntityIds.Add(pair.Key);
                }
            }

            var staleTrackClearedCount = 0;
            var staleCompletedKeyClearedCount = _trackState.CompletedPresentationMotions.Count;
            foreach (var entityId in boxEntityIds)
            {
                if (_trackState.LocalMotionTracks.Remove(entityId))
                {
                    staleTrackClearedCount++;
                }

                _trackState.MotionVisualScaleEntityIds.Remove(entityId);
                if (_trackState.OriginalViewMotionTracks.Remove(entityId))
                {
                    staleTrackClearedCount++;
                }

                _trackState.CompletedMotionTrackIds.Remove(entityId);
                _trackState.CompletedMotionVisualScaleEntityIds.Remove(entityId);
                _trackState.CompletedOriginalViewMotionTrackIds.Remove(entityId);
            }

            _trackState.CompletedPresentationMotions.Clear();

            _trackState.CompletedFlipInteractionTrackIds.Clear();
            foreach (var pair in _trackState.FlipInteractionTracks)
            {
                if (boxEntityIds.Contains(pair.Value.BoxEntityId))
                {
                    _trackState.CompletedFlipInteractionTrackIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < _trackState.CompletedFlipInteractionTrackIds.Count; i++)
            {
                if (_trackState.FlipInteractionTracks.Remove(_trackState.CompletedFlipInteractionTrackIds[i]))
                {
                    staleTrackClearedCount++;
                }
            }

            _trackState.CompletedFlipInteractionTrackIds.Clear();
            _trackState.BoxMotionTelemetry.RecordCleanup(
                reason,
                resetResult,
                staleTrackClearedCount,
                staleCompletedKeyClearedCount);
        }
    }
}
