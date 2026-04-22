using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.ActionAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayActionAudioPresentationController
    {
        private readonly List<GameplayActionAudioRequest> _pendingRequests = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private IGameplayAudioPlaybackPort _playbackPort;

        public GameplayActionAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            ClearPendingPlan();
        }

        public void DetachRuntime()
        {
            ClearPendingPlan();
            _playbackPort = null;
        }

        public void ResetSession()
        {
            ClearPendingPlan();
        }

        public void ReplacePendingPlan(IReadOnlyList<GameplayActionAudioRequest> plannedRequests)
        {
            if (plannedRequests == null)
            {
                throw new ArgumentNullException(nameof(plannedRequests));
            }

            ClearPendingPlan();
            for (var i = 0; i < plannedRequests.Count; i++)
            {
                _pendingRequests.Add(plannedRequests[i]);
            }
        }

        public void PlayPlannedAudio()
        {
            if (_playbackPort == null)
            {
                ClearPendingPlan();
                return;
            }

            try
            {
                for (var i = 0; i < _pendingRequests.Count; i++)
                {
                    PlayRequest(_pendingRequests[i]);
                }
            }
            finally
            {
                ClearPendingPlan();
            }
        }

        public void ClearPendingPlan()
        {
            _pendingRequests.Clear();
        }

        private void PlayRequest(in GameplayActionAudioRequest request)
        {
            if (!TryResolveLiveOwner(request.OwnerEntityId, out var ownerView))
            {
                return;
            }

            var authoring = GameplayActionAudioAuthoring.GetOptionalValidatedAuthoring(ownerView);
            if (authoring == null ||
                !authoring.Profile.TryResolve(request.Action, request.Moment, out var binding))
            {
                return;
            }

            if (binding.HasAttachmentSlot)
            {
                _playbackPort.PlayAttached(binding.Definition, ownerView, binding.AttachmentSlot, request.Context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, request.Context);
        }

        private bool TryResolveLiveOwner(int ownerEntityId, out GameplayEntityView ownerView)
        {
            ownerView = null;
            if (!_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId, out ownerView) ||
                ownerView == null ||
                !ownerView.gameObject.activeInHierarchy)
            {
                ownerView = null;
                return false;
            }

            return true;
        }
    }
}
