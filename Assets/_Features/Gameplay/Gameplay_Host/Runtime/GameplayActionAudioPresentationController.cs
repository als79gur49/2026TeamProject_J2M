using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.ActionAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayActionAudioPresentationController
    {
        private const int ActionAudioLaneId = 3;

        private readonly List<GameplayActionAudioRequest> _pendingRequests = new();
        private readonly List<ScheduledGameplayActionAudioRequest> _deferredRequests = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _deferredKeys = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _playedDeferredKeys = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private GameplayAudioPlaybackGateState _gateState = GameplayAudioPlaybackGateState.Open;
        private IGameplayAudioPlaybackPort _playbackPort;

        public GameplayActionAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int DeferredRequestCount => _deferredRequests.Count;

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
            ReplacePendingPlan(plannedRequests, tickIndex: 0);
        }

        public void ReplacePendingPlan(IReadOnlyList<GameplayActionAudioRequest> plannedRequests, int tickIndex)
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

        public void SetPlaybackGateState(GameplayAudioPlaybackGateState gateState)
        {
            _gateState = gateState;
        }

        public void PlayPlannedAudio()
        {
            PlayPlannedAudio(tickIndex: 0);
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
                if (!_gateState.IsBlocked)
                {
                    DrainDeferredRequests();
                }

                for (var i = 0; i < _pendingRequests.Count; i++)
                {
                    var request = _pendingRequests[i];
                    if (ShouldDefer(request))
                    {
                        DeferRequest(new ScheduledGameplayActionAudioRequest(
                            request,
                            CreateRequestKey(request, tickIndex, i)));
                        continue;
                    }

                    if (!ShouldSuppress(request))
                    {
                        PlayRequest(request);
                    }
                }
            }
            finally
            {
                _pendingRequests.Clear();
            }
        }

        public void Update()
        {
            if (_playbackPort == null)
            {
                ClearPendingPlan();
                return;
            }

            if (!_gateState.IsBlocked)
            {
                DrainDeferredRequests();
            }
        }

        public void ClearPendingPlan()
        {
            _pendingRequests.Clear();
            _deferredRequests.Clear();
            _deferredKeys.Clear();
            _playedDeferredKeys.Clear();
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
                PlayResolvedBinding(binding, ownerView, request.Context);
            }
            else
            {
                PlayResolvedBinding(binding, ownerView, request.Context);
            }
        }

        internal bool TryPlayBridgeRequest(
            in GameplayActionAudioPlaybackRequest request,
            out GameplayActionAudioPlaybackResult result)
        {
            if (_playbackPort == null)
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.PortMissing);
                return false;
            }

            if (!IsSupportedMoment(request.Moment))
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.UnsupportedMoment);
                return false;
            }

            if (!TryResolveLiveOwner(request.OwnerEntityId, out var ownerView))
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.OwnerViewMissing);
                return false;
            }

            if (!ownerView.TryGetComponent<GameplayActionAudioAuthoring>(out var authoring) ||
                authoring == null)
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.AuthoringMissing);
                return false;
            }

            if (authoring.Profile == null)
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.ProfileMissing);
                return false;
            }

            authoring.Profile.ValidateOrThrow();
            var resolveStatus = authoring.Profile.ResolveEntryStatus(
                request.Action,
                request.Moment,
                out var binding);
            switch (resolveStatus)
            {
                case GameplayActionAudioProfileResolveStatus.Resolved:
                    PlayResolvedBinding(binding, ownerView, request.Context);
                    result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.Succeeded);
                    return true;
                case GameplayActionAudioProfileResolveStatus.EntryMissing:
                    result = new GameplayActionAudioPlaybackResult(
                        GameplayActionAudioPlaybackResultKind.OptionalProfileEntryMissing);
                    return false;
                case GameplayActionAudioProfileResolveStatus.OptionalBindingMissing:
                case GameplayActionAudioProfileResolveStatus.BindingMissing:
                case GameplayActionAudioProfileResolveStatus.None:
                default:
                    result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.BindingMissing);
                    return false;
            }
        }

        private void PlayResolvedBinding(
            AudioBinding binding,
            GameplayEntityView ownerView,
            in AudioPlaybackContext context)
        {
            if (binding.HasAttachmentSlot)
            {
                _playbackPort.PlayAttached(binding.Definition, ownerView, binding.AttachmentSlot, context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, context);
        }

        private static bool IsSupportedMoment(GameplayActionAudioMoment moment)
        {
            switch (moment)
            {
                case GameplayActionAudioMoment.Windup:
                case GameplayActionAudioMoment.AssistOutOfRange:
                case GameplayActionAudioMoment.NoTarget:
                case GameplayActionAudioMoment.Invalid:
                    return true;
                default:
                    return false;
            }
        }

        private void DrainDeferredRequests()
        {
            if (_deferredRequests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _deferredRequests.Count; i++)
            {
                var scheduled = _deferredRequests[i];
                if (ShouldSuppress(scheduled.Request))
                {
                    _playedDeferredKeys.Add(scheduled.Key);
                    continue;
                }

                PlayRequest(scheduled.Request);
                _playedDeferredKeys.Add(scheduled.Key);
            }

            _deferredRequests.Clear();
            _deferredKeys.Clear();
        }

        private void DeferRequest(in ScheduledGameplayActionAudioRequest scheduled)
        {
            if (_deferredKeys.Contains(scheduled.Key) ||
                _playedDeferredKeys.Contains(scheduled.Key))
            {
                return;
            }

            _deferredRequests.Add(scheduled);
            _deferredKeys.Add(scheduled.Key);
        }

        private bool ShouldDefer(in GameplayActionAudioRequest request)
        {
            return EvaluatePlaybackPolicy(request) == GameplayAudioPlaybackDecision.DeferUntilUnlock;
        }

        private bool ShouldSuppress(in GameplayActionAudioRequest request)
        {
            return EvaluatePlaybackPolicy(request) == GameplayAudioPlaybackDecision.Suppress;
        }

        private GameplayAudioPlaybackDecision EvaluatePlaybackPolicy(in GameplayActionAudioRequest request)
        {
            return GameplayAudioPlaybackDecision.PlayNow;
        }

        private static GameplayAudioPlaybackRequestKey CreateRequestKey(
            in GameplayActionAudioRequest request,
            int tickIndex,
            int orderIndex)
        {
            return new GameplayAudioPlaybackRequestKey(
                ActionAudioLaneId,
                tickIndex,
                ((int)request.Action * 100) + (int)request.Moment,
                request.OwnerEntityId,
                orderIndex);
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

        private readonly struct ScheduledGameplayActionAudioRequest
        {
            public ScheduledGameplayActionAudioRequest(
                GameplayActionAudioRequest request,
                GameplayAudioPlaybackRequestKey key)
            {
                Request = request;
                Key = key;
            }

            public GameplayActionAudioRequest Request { get; }

            public GameplayAudioPlaybackRequestKey Key { get; }
        }
    }
}
