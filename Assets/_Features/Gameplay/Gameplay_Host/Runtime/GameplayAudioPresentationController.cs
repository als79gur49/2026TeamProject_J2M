using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.EnemyAudio;
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

    internal interface IGameplayAudioLoopPlaybackPort
    {
        AudioPlaybackHandle PlayAttachedLoop(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context);
    }

    internal sealed class GameplayAudioPlaybackPortAdapter : IGameplayAudioPlaybackPort, IGameplayAudioLoopPlaybackPort
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

        public AudioPlaybackHandle PlayAttachedLoop(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context)
        {
            return _audioService.PlayAttached(definition, owner, slot, context);
        }
    }

    internal sealed class GameplayAudioPresentationController
    {
        private readonly List<ScheduledGameplayAudioRequest> _pendingRequests = new();
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

        public void ReplacePendingPlan(IReadOnlyList<GameplayAudioRequest> plannedRequests)
        {
            if (plannedRequests == null)
            {
                throw new ArgumentNullException(nameof(plannedRequests));
            }

            RemoveImmediatePendingRequests();
            for (var i = 0; i < plannedRequests.Count; i++)
            {
                var request = plannedRequests[i];
                _pendingRequests.Add(new ScheduledGameplayAudioRequest(request, request.DelaySeconds));
            }
        }

        public void PlayPlannedAudio()
        {
            PlayReadyAudio(0f);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            PlayReadyAudio(deltaTime);
        }

        private void PlayReadyAudio(float deltaTime)
        {
            if (_audioMap == null || _playbackPort == null)
            {
                ClearPendingPlan();
                return;
            }

            var retainedCount = 0;
            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                var scheduled = _pendingRequests[i].Advance(deltaTime);
                if (scheduled.RemainingSeconds > 0f)
                {
                    _pendingRequests[retainedCount++] = scheduled;
                    continue;
                }

                PlayRequest(scheduled.Request);
            }

            if (retainedCount < _pendingRequests.Count)
            {
                _pendingRequests.RemoveRange(retainedCount, _pendingRequests.Count - retainedCount);
            }
        }

        public void ClearPendingPlan()
        {
            _pendingRequests.Clear();
        }

        private void RemoveImmediatePendingRequests()
        {
            for (var i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                if (_pendingRequests[i].RemainingSeconds <= 0f)
                {
                    _pendingRequests.RemoveAt(i);
                }
            }
        }

        private void PlayRequest(in GameplayAudioRequest request)
        {
            if (ShouldSuppressGenericEnemyDeath(request))
            {
                return;
            }

            var binding = _audioMap.ResolveOrThrow(request.SemanticId);
            if (binding.HasAttachmentSlot &&
                TryResolveOwner(request.OwnerEntityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, request.Context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, request.Context);
        }

        private bool ShouldSuppressGenericEnemyDeath(in GameplayAudioRequest request)
        {
            if (request.SemanticId != GameplayAudioSemanticId.EntityExitEnemyDeath ||
                !request.OwnerEntityId.HasValue ||
                !TryResolveOwner(request.OwnerEntityId, out var owner))
            {
                return false;
            }

            var authoring = EnemyAudioAuthoring.GetOptionalValidatedAuthoring(owner);
            return authoring != null &&
                   authoring.Profile.HasCue(EnemyAudioCue.Death);
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

        private readonly struct ScheduledGameplayAudioRequest
        {
            public ScheduledGameplayAudioRequest(GameplayAudioRequest request, float remainingSeconds)
            {
                Request = request;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
            }

            public GameplayAudioRequest Request { get; }

            public float RemainingSeconds { get; }

            public ScheduledGameplayAudioRequest Advance(float deltaTime)
            {
                return new ScheduledGameplayAudioRequest(Request, RemainingSeconds - Math.Max(0f, deltaTime));
            }
        }
    }
}
