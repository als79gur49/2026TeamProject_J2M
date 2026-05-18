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
        private Action<string> _diagnosticSink;

        public TileFeatureAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        public void SetDiagnosticSink(Action<string> diagnosticSink)
        {
            _diagnosticSink = diagnosticSink;
        }

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
            if (!TryResolveBinding(request, out var binding))
            {
                return;
            }

            if (binding.HasAttachmentSlot &&
                request.OwnerEntityId > 0 &&
                TryResolveOwner(request.OwnerEntityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, request.Context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, request.Context);
        }

        private bool TryResolveBinding(TileFeatureAudioCue cue, out AudioBinding binding)
        {
            if (cue == TileFeatureAudioCue.ButtonActivated)
            {
                binding = _audioMap.ResolveOrThrow(cue);
                return true;
            }

            return _audioMap.TryResolveOptional(cue, out binding);
        }

        private bool TryResolveBinding(in TileFeatureAudioRequest request, out AudioBinding binding)
        {
            if (request.BurstKind == TileFeatureAudioBurstKind.On)
            {
                if (_audioMap.TryResolveOptional(TileFeatureAudioCue.TileFeatureOnBurst, out binding))
                {
                    return true;
                }

                _diagnosticSink?.Invoke(
                    "TileFeature On burst binding is missing. Falling back to one representative single request.");
                return TryResolveRepresentativeSingleBinding(request, out binding);
            }

            if (request.BurstKind == TileFeatureAudioBurstKind.Off)
            {
                if (_audioMap.TryResolveOptional(TileFeatureAudioCue.TileFeatureOffBurst, out binding))
                {
                    return true;
                }

                _diagnosticSink?.Invoke(
                    "TileFeature Off burst binding is missing. Falling back to one representative single request.");
                return TryResolveRepresentativeSingleBinding(request, out binding);
            }

            if (request.Cue == TileFeatureAudioCue.MoonBlockGeneratorBlocked)
            {
                return _audioMap.TryResolveMoonBlockGeneratorBlocked(
                    request.MoonBlockGeneratorBlockedPayload,
                    out binding);
            }

            return TryResolveBinding(request.Cue, out binding);
        }

        private bool TryResolveRepresentativeSingleBinding(
            in TileFeatureAudioRequest request,
            out AudioBinding binding)
        {
            var representativeCue = request.RepresentativeCue == TileFeatureAudioCue.None
                ? request.Cue
                : request.RepresentativeCue;
            if (representativeCue == request.Cue &&
                (request.Cue == TileFeatureAudioCue.TileFeatureOnBurst ||
                 request.Cue == TileFeatureAudioCue.TileFeatureOffBurst))
            {
                binding = null;
                return false;
            }

            return TryResolveBinding(representativeCue, out binding);
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
