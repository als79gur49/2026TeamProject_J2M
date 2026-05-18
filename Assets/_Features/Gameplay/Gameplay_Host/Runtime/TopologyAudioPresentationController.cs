using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.TopologyAudio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class TopologyAudioPresentationController
    {
        private readonly List<TopologyAudioRequest> _pendingRequests = new();
        private readonly HashSet<int> _pendingSequenceIds = new();
        private readonly HashSet<int> _playedSequenceIds = new();

        private TopologyAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;

        internal int PendingRequestCount => _pendingRequests.Count;

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            TopologyAudioMap audioMap)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            _audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            _audioMap.ValidateRequiredCuesOrThrow(TopologyAudioCueCatalog.RequiredOneShotV1);
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

        public void ReplacePendingPlan(IReadOnlyList<TopologyAudioRequest> plannedRequests)
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

                _pendingRequests.Add(request);
            }
        }

        public void PlayPlannedAudio()
        {
            if (_audioMap == null || _playbackPort == null)
            {
                ClearPendingPlan();
                return;
            }

            for (var i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                var request = _pendingRequests[i];
                _pendingSequenceIds.Remove(request.SequenceId);
                if (_playedSequenceIds.Add(request.SequenceId))
                {
                    PlayRequest(request);
                }

                _pendingRequests.RemoveAt(i);
            }
        }

        public void ClearPendingPlan()
        {
            _pendingRequests.Clear();
            _pendingSequenceIds.Clear();
        }

        private void PlayRequest(in TopologyAudioRequest request)
        {
            var binding = _audioMap.ResolveOrThrow(request.Cue);
            _playbackPort.Play2D(binding.Definition, request.Context);
        }
    }
}
