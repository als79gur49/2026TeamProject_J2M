using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayExitPresentationController
    {
        private readonly HashSet<int> _exitOwnedEntityIds = new();
        private readonly List<TickEntityExitPresentationSignal> _pendingEntityExitSignals = new();
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private readonly GameplayTransientEffectPresenter _transientEffectPresenter;
        private GameplayCubeProjector _projector;
        private GameplayTimingProfile _timingProfile;

        public GameplayExitPresentationController(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayPoseResolver poseResolver,
            GameplayTransientEffectPresenter transientEffectPresenter)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _poseResolver = poseResolver ?? throw new ArgumentNullException(nameof(poseResolver));
            _transientEffectPresenter = transientEffectPresenter ?? throw new ArgumentNullException(nameof(transientEffectPresenter));
        }

        public void Configure(GameplayCubeProjector projector, GameplayTimingProfile timingProfile)
        {
            _projector = projector ?? throw new ArgumentNullException(nameof(projector));
            _timingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
        }

        public void Reset()
        {
            _exitOwnedEntityIds.Clear();
            _pendingEntityExitSignals.Clear();
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
            _pendingEntityExitSignals.Clear();

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                var signal = presentationData.EntityExitSignals[i];
                if (!_exitOwnedEntityIds.Add(signal.ExitedEntityId))
                {
                    continue;
                }

                _pendingEntityExitSignals.Add(signal);
            }
        }

        public void PlayEntityExitEffects()
        {
            for (var i = 0; i < _pendingEntityExitSignals.Count; i++)
            {
                var signal = _pendingEntityExitSignals[i];
                if (!_poseResolver.TryResolveEntityExitSignalLocalPose(_projector, signal, out var localPose))
                {
                    continue;
                }

                _stateStore.ViewsByEntityId.TryGetValue(signal.ExitedEntityId, out var sourceView);
                var hasTargetLocalPose = TryResolveExitEffectTargetLocalPose(signal, out var targetLocalPose);
                _transientEffectPresenter.PlayExitEffect(
                    signal,
                    sourceView,
                    localPose,
                    hasTargetLocalPose ? targetLocalPose : (GameplayEntityPose?)null,
                    ResolveEntityExitEffectDurationSeconds(signal.ExitCause));
            }
        }

        public void ApplyEntityExitOwnership()
        {
            foreach (var entityId in _exitOwnedEntityIds)
            {
                QueueFlipInteractionReset(entityId);
                // Exit ownership removes the authoritative entity view from presentation
                // state immediately. Any lingering visual is transient-effect-only.
                _trackState.JumpTracks.Remove(entityId);
                _trackState.LocalMotionTracks.Remove(entityId);
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

        private float ResolveEntityExitEffectDurationSeconds(TickEntityExitCause exitCause)
        {
            return exitCause switch
            {
                TickEntityExitCause.ItemConsume => _timingProfile.ItemConsumeEffectDurationSeconds,
                TickEntityExitCause.DestroyedByImpact => _timingProfile.BoxDestroyEffectDurationSeconds,
                TickEntityExitCause.Killed => _timingProfile.EnemyDeathEffectDurationSeconds,
                TickEntityExitCause.OutOfBounds => _timingProfile.ItemConsumeEffectDurationSeconds,
                _ => _timingProfile.ItemConsumeEffectDurationSeconds,
            };
        }

        private bool TryResolveExitEffectTargetLocalPose(
            TickEntityExitPresentationSignal signal,
            out GameplayEntityPose localPose)
        {
            if (signal.ExitCause == TickEntityExitCause.Killed)
            {
                if (TryResolvePlayerLocalPose(out localPose))
                {
                    return true;
                }

                if (signal.SourceActorEntityId.HasValue &&
                    _stateStore.CommittedLocalTargetPoses.TryGetValue(signal.SourceActorEntityId.Value, out localPose))
                {
                    return true;
                }
            }

            localPose = default;
            return false;
        }

        private bool TryResolvePlayerLocalPose(out GameplayEntityPose localPose)
        {
            var bestPlayerEntityId = int.MaxValue;
            localPose = default;

            foreach (var pair in _stateStore.UnitRolesByEntityId)
            {
                if (pair.Value != UnitRole.Player ||
                    !_stateStore.CommittedLocalTargetPoses.TryGetValue(pair.Key, out var candidateLocalPose) ||
                    pair.Key >= bestPlayerEntityId)
                {
                    continue;
                }

                bestPlayerEntityId = pair.Key;
                localPose = candidateLocalPose;
            }

            return bestPlayerEntityId != int.MaxValue;
        }
    }
}
