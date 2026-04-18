using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal interface IGameplayAudioPlaybackPort
    {
        void Play2D(AudioDefinition definition, in AudioPlaybackContext context);

        void PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context);
    }

    internal sealed class GameplayAudioPlaybackPortAdapter : IGameplayAudioPlaybackPort
    {
        private readonly IAudioService _audioService;

        public GameplayAudioPlaybackPortAdapter(IAudioService audioService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
        {
            _audioService.Play2D(definition, context);
        }

        public void PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context)
        {
            _audioService.PlayAttached(definition, owner, slot, context);
        }
    }

    internal sealed class GameplayAudioPresentationController
    {
        private readonly List<GameplayAudioRequest> _pendingRequests = new();
        private readonly GameplayAudioRequestPlanner _planner = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private GameplayAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;

        public GameplayAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap audioMap)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            _audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            _audioMap.ValidateRequiredSemanticsOrThrow(GameplayAudioSemanticCatalog.RequiredOneShotV1);
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

        public void RefreshAudioPlan(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            ClearPendingPlan();
            if (_audioMap == null || _playbackPort == null)
            {
                return;
            }

            var plannedRequests = _planner.BuildRequests(result);
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

            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                PlayRequest(_pendingRequests[i]);
            }

            ClearPendingPlan();
        }

        public void ClearPendingPlan()
        {
            _pendingRequests.Clear();
        }

        private void PlayRequest(in GameplayAudioRequest request)
        {
            var binding = _audioMap.ResolveOrThrow(request.SemanticId);
            if (binding.HasAttachmentSlot &&
                TryResolveOwner(request.OwnerEntityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, request.Context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, request.Context);
        }

        private bool TryResolveOwner(int? ownerEntityId, out GameplayEntityView owner)
        {
            owner = null;
            if (!ownerEntityId.HasValue ||
                !_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId.Value, out owner) ||
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
