using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class EnemyAudioPresentationController
    {
        private readonly EnemyMoveCadenceGate _moveCadenceGate = new();
        private readonly List<ScheduledEnemyAudioRequest> _pendingRequests = new();
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

            RemoveImmediatePendingRequests();
            for (var i = 0; i < plannedRequests.Count; i++)
            {
                var request = plannedRequests[i];
                _pendingRequests.Add(new ScheduledEnemyAudioRequest(request, request.DelaySeconds));
            }
        }

        public void PlayPlannedAudio(int tickIndex)
        {
            PlayReadyAudio(tickIndex, 0f);
        }

        public void Update(int tickIndex, float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            PlayReadyAudio(tickIndex, deltaTime);
        }

        private void PlayReadyAudio(int tickIndex, float deltaTime)
        {
            if (_playbackPort == null)
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

                PlayRequest(scheduled.Request, tickIndex);
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

        private readonly struct ScheduledEnemyAudioRequest
        {
            public ScheduledEnemyAudioRequest(EnemyAudioRequest request, float remainingSeconds)
            {
                Request = request;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
            }

            public EnemyAudioRequest Request { get; }

            public float RemainingSeconds { get; }

            public ScheduledEnemyAudioRequest Advance(float deltaTime)
            {
                return new ScheduledEnemyAudioRequest(Request, RemainingSeconds - Math.Max(0f, deltaTime));
            }
        }
    }
}
