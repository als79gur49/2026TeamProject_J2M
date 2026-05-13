using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class EnemyAudioPresentationController
    {
        private readonly EnemyMoveCadenceGate _moveCadenceGate = new();
        private readonly List<EnemyAudioRequest> _pendingRequests = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private IGameplayAudioPlaybackPort _playbackPort;

        public EnemyAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        public void ConfigureMoveCadence(int simulationTicksPerSecond)
        {
            _moveCadenceGate.Configure(simulationTicksPerSecond);
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            ClearPendingPlan();
            _moveCadenceGate.ResetState();
        }

        public void DetachRuntime()
        {
            ClearPendingPlan();
            _moveCadenceGate.ResetState();
            _playbackPort = null;
        }

        public void ResetSession()
        {
            ClearPendingPlan();
            _moveCadenceGate.ResetState();
        }

        public void ReplacePendingPlan(IReadOnlyList<EnemyAudioRequest> plannedRequests)
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

        public void PlayPlannedAudio(int tickIndex)
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
                    PlayRequest(_pendingRequests[i], tickIndex);
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

        private void PlayRequest(in EnemyAudioRequest request, int tickIndex)
        {
            if (!TryResolveLiveOwner(request.OwnerEntityId, out var ownerView))
            {
                return;
            }

            var authoring = EnemyAudioAuthoring.GetOptionalValidatedAuthoring(ownerView);
            if (authoring == null ||
                !authoring.Profile.TryResolve(request.Cue, out var binding))
            {
                return;
            }

            if (request.Cue == EnemyAudioCue.Move &&
                !_moveCadenceGate.ShouldPlayMove(request.OwnerEntityId, tickIndex))
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
