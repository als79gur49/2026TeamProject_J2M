using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BlockAudio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class BlockAudioPresentationController
    {
        private readonly List<ScheduledBlockAudioRequest> _pendingRequests = new();
        private readonly HashSet<int> _pendingSequenceIds = new();
        private readonly HashSet<int> _playedSequenceIds = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private BlockAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;

        public BlockAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            BlockAudioMap audioMap)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            _audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            _audioMap.ValidateRequiredCuesOrThrow(BlockAudioCueCatalog.RequiredOneShotV1);
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
            _playedSequenceIds.Clear();
        }

        public void ReplacePendingPlan(IReadOnlyList<BlockAudioRequest> plannedRequests)
        {
            if (plannedRequests == null)
            {
                throw new ArgumentNullException(nameof(plannedRequests));
            }

            for (var i = 0; i < plannedRequests.Count; i++)
            {
                var request = plannedRequests[i];
                if (_playedSequenceIds.Contains(request.SequenceId) ||
                    !_pendingSequenceIds.Add(request.SequenceId))
                {
                    continue;
                }

                _pendingRequests.Add(new ScheduledBlockAudioRequest(request, request.DelaySeconds));
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

        public void ClearPendingPlan()
        {
            _pendingRequests.Clear();
            _pendingSequenceIds.Clear();
        }

        private void PlayReadyAudio(float deltaTime)
        {
            if (_audioMap == null || _playbackPort == null)
            {
                ClearPendingPlan();
                return;
            }

            for (var i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                var scheduled = _pendingRequests[i];
                scheduled.RemainingSeconds -= deltaTime;
                if (scheduled.RemainingSeconds > 0f)
                {
                    _pendingRequests[i] = scheduled;
                    continue;
                }

                _pendingSequenceIds.Remove(scheduled.Request.SequenceId);
                if (_playedSequenceIds.Add(scheduled.Request.SequenceId))
                {
                    PlayRequest(scheduled.Request);
                }

                _pendingRequests.RemoveAt(i);
            }
        }

        private void PlayRequest(in BlockAudioRequest request)
        {
            var binding = _audioMap.ResolveOrThrow(request.Cue);
            if (binding.HasAttachmentSlot &&
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
            if (ownerEntityId <= 0 ||
                !_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId, out owner) ||
                owner == null ||
                !owner.gameObject.activeInHierarchy)
            {
                owner = null;
                return false;
            }

            return true;
        }

        private struct ScheduledBlockAudioRequest
        {
            public ScheduledBlockAudioRequest(BlockAudioRequest request, float remainingSeconds)
            {
                Request = request;
                RemainingSeconds = remainingSeconds;
            }

            public BlockAudioRequest Request { get; }

            public float RemainingSeconds;
        }
    }
}
