using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class TileFeatureAudioPresentationController
    {
        private readonly List<TileFeatureAudioRequest> _pendingRequests = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private TileFeatureAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;

        public TileFeatureAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TileFeatureAudioMap audioMap)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            _audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            _audioMap.ValidateRequiredCuesOrThrow(TileFeatureAudioCueCatalog.RequiredOneShotV1);
            ClearPendingPlan();
        }

        public void DetachRuntime()
        {
            ClearPendingPlan();
            _audioMap = null;
            _playbackPort = null;
        }

        public void ResetSession()
        {
            ClearPendingPlan();
        }

        public void ReplacePendingPlan(IReadOnlyList<TileFeatureAudioRequest> plannedRequests)
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
            if (_audioMap == null || _playbackPort == null)
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

        private void PlayRequest(in TileFeatureAudioRequest request)
        {
            var binding = _audioMap.ResolveOrThrow(request.Cue);
            if (binding.HasAttachmentSlot &&
                request.OwnerEntityId > 0 &&
                TryResolveOwner(request.OwnerEntityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, request.Context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, request.Context);
        }

        private bool TryResolveOwner(int ownerEntityId, out GameplayEntityView owner)
        {
            owner = null;
            if (!_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId, out owner) ||
                owner == null ||
                !owner.gameObject.activeInHierarchy)
            {
                owner = null;
                return false;
            }

            return true;
        }
    }
}
