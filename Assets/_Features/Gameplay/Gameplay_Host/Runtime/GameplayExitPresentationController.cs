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
        private readonly List<TickImpactTransientPresentationSignal> _pendingImpactTransientSignals = new();
        private readonly HashSet<int> _impactTransientEntityIds = new();
        private readonly Dictionary<int, FlipImpactInstanceKey> _destroySelfFlipImpactKeysByEntityId = new();
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;
        private readonly GameplayTransientEffectPresenter _transientEffectPresenter;
        private GameplayCubeProjector _projector;
        private GameplayTimingProfile _timingProfile;
        private int _refreshSequence;

        public GameplayExitPresentationController(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayMotionTimingResolver motionTimingResolver,
            GameplayPoseResolver poseResolver,
            GameplayTransientEffectPresenter transientEffectPresenter)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _ = motionTimingResolver ?? throw new ArgumentNullException(nameof(motionTimingResolver));
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
            _pendingImpactTransientSignals.Clear();
            _impactTransientEntityIds.Clear();
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
            _pendingEntityExitSignals.Clear();
            _pendingImpactTransientSignals.Clear();
            _impactTransientEntityIds.Clear();
            _destroySelfFlipImpactKeysByEntityId.Clear();
            _refreshSequence++;

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                var signal = presentationData.EntityExitSignals[i];
                if (!_exitOwnedEntityIds.Add(signal.ExitedEntityId))
                {
                    continue;
                }

                _pendingEntityExitSignals.Add(signal);
            }

            for (var i = 0; i < presentationData.ImpactTransientSignals.Count; i++)
            {
                var signal = presentationData.ImpactTransientSignals[i];
                if (_impactTransientEntityIds.Add(signal.EntityId))
                {
                    _pendingImpactTransientSignals.Add(signal);
                }
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

        public void PlayEntityExitEffects()
        {
            for (var i = 0; i < _pendingEntityExitSignals.Count; i++)
            {
                var signal = _pendingEntityExitSignals[i];
                if (_impactTransientEntityIds.Contains(signal.ExitedEntityId) ||
                    _destroySelfFlipImpactKeysByEntityId.ContainsKey(signal.ExitedEntityId))
                {
                    continue;
                }

                if (!ShouldPlayLegacyEntityExitEffect(signal.ExitCause))
                {
                    continue;
                }

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

        private static bool ShouldPlayLegacyEntityExitEffect(TickEntityExitCause exitCause)
        {
            _ = exitCause;
            return false;
        }

        public void ApplyEntityExitOwnership()
        {
            foreach (var entityId in _exitOwnedEntityIds)
            {
                if (!_destroySelfFlipImpactKeysByEntityId.ContainsKey(entityId))
                {
                    QueueFlipInteractionReset(entityId);
                }

                // Exit ownership removes the authoritative entity view from presentation
                // state immediately. Any lingering visual is transient-effect-only.
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

        private float ResolveImpactBreakEffectDurationSeconds()
        {
            return Math.Max(
                _timingProfile.BoxDestroyEffectDurationSeconds,
                _timingProfile.FlipMotionDurationSeconds);
        }

    }
}
