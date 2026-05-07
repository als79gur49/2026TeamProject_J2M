using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayExitPresentationController
    {
        private readonly HashSet<int> _exitOwnedEntityIds = new();
        private readonly Dictionary<int, FlipImpactInstanceKey> _destroySelfFlipImpactKeysByEntityId = new();
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private int _refreshSequence;

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
            _destroySelfFlipImpactKeysByEntityId.Clear();
            _refreshSequence = 0;
        }

        public bool IsExitOwned(int entityId)
        {
            return _exitOwnedEntityIds.Contains(entityId);
        }

        public void RefreshEntityExitPlan(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            _exitOwnedEntityIds.Clear();
            _destroySelfFlipImpactKeysByEntityId.Clear();
            _refreshSequence++;

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                _exitOwnedEntityIds.Add(presentationData.EntityExitSignals[i].ExitedEntityId);
            }

            for (var i = 0; i < presentationData.FlipImpactSignals.Count; i++)
            {
                var signal = presentationData.FlipImpactSignals[i];
                if (signal.Disposition != FlipImpactPresentationDisposition.DestroySelf)
                {
                    continue;
                }

                var key = FlipImpactInstanceKey.Create(signal, _refreshSequence);
                if (_destroySelfFlipImpactKeysByEntityId.ContainsKey(signal.BoxEntityId))
                {
                    continue;
                }

                _destroySelfFlipImpactKeysByEntityId[signal.BoxEntityId] = key;
            }
        }

        public void ApplyEntityExitOwnership()
        {
            foreach (var entityId in _exitOwnedEntityIds)
            {
                if (!_destroySelfFlipImpactKeysByEntityId.ContainsKey(entityId))
                {
                    QueueFlipInteractionReset(entityId);
                }

                _trackState.JumpTracks.Remove(entityId);
                _trackState.LocalMotionTracks.Remove(entityId);
                _trackState.StayFlipImpactTracks.Remove(entityId);
                _trackState.VisibilityTracks.Remove(entityId);
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
