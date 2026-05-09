using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GravityFieldAudioPresentationController
    {
        private readonly List<GravityFieldAudioRequest> _pendingRequests = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private GravityFieldAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;

        public GravityFieldAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GravityFieldAudioMap audioMap)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            _audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            _audioMap.ValidateRequiredCuesOrThrow(GravityFieldAudioCueCatalog.RequiredOneShotV1);
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

        public void ReplacePendingPlan(IReadOnlyList<GravityFieldAudioRequest> plannedRequests)
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

        private void PlayRequest(in GravityFieldAudioRequest request)
        {
            if (!_audioMap.TryResolveOptional(request.Cue, out var binding))
            {
                return;
            }

            var attachedEntityId = request.Cue == GravityFieldAudioCue.LockedBox &&
                                   request.TargetEntityId > 0
                ? request.TargetEntityId
                : request.EmitterEntityId;
            if (binding.HasAttachmentSlot &&
                attachedEntityId > 0 &&
                TryResolveEntityView(attachedEntityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, request.Context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, request.Context);
        }

        private bool TryResolveEntityView(int entityId, out GameplayEntityView view)
        {
            view = null;
            if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out view) ||
                view == null ||
                !view.gameObject.activeInHierarchy)
            {
                view = null;
                return false;
            }

            return true;
        }
    }
}
