using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
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
                _transientEffectPresenter.PlayExitEffect(
                    signal,
                    sourceView,
                    localPose,
                    ResolveEntityExitEffectDurationSeconds(signal.ExitCause));
            }
        }

        public void ApplyEntityExitOwnership()
        {
            foreach (var entityId in _exitOwnedEntityIds)
            {
                // Exit ownership removes the authoritative entity view from presentation
                // state immediately. Any lingering visual is transient-effect-only.
                _trackState.JumpTracks.Remove(entityId);
                _trackState.LocalMotionTracks.Remove(entityId);
                _trackState.VisibilityTracks.Remove(entityId);
                _stateStore.CommittedLocalTargetPoses.Remove(entityId);
                _stateStore.CommittedProjectedSlotsByEntityId.Remove(entityId);
                _stateStore.EnemyAiModesByEntityId.Remove(entityId);
                _stateStore.EnemyVisualFactsByEntityId.Remove(entityId);
                _stateStore.EnemyVisualSemanticStatesByEntityId.Remove(entityId);
                _stateStore.JumpDetachedVisibilityStates.Remove(entityId);
                _stateStore.RetainedLocalTargetPoses.Remove(entityId);
                _stateStore.TransitionVisibilityStates.Remove(entityId);
                _stateStore.EntityTypesByEntityId.Remove(entityId);

                if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                    view != null)
                {
                    view.SetVisible(false);
                }
            }
        }

        private float ResolveEntityExitEffectDurationSeconds(TickEntityExitCause exitCause)
        {
            return exitCause switch
            {
                TickEntityExitCause.ItemConsume => _timingProfile.ItemConsumeEffectDurationSeconds,
                TickEntityExitCause.BoxDestroy => _timingProfile.BoxDestroyEffectDurationSeconds,
                _ => _timingProfile.ItemConsumeEffectDurationSeconds,
            };
        }
    }
}
