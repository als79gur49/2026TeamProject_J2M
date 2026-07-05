using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class EnemyAudioPresentationController
    {
        private const int EnemyAudioLaneId = 2;

        private readonly EnemyMoveCadenceGate _moveCadenceGate = new();
        private readonly EnemyStationaryActiveCadenceGate _stationaryActiveCadenceGate = new();
        private readonly List<ScheduledEnemyAudioRequest> _scheduledRequests = new();
        private readonly List<ScheduledEnemyAudioRequest> _deferredRequests = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _deferredKeys = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _playedDeferredKeys = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private GameplayAudioPlaybackGateState _gateState = GameplayAudioPlaybackGateState.Open;
        private IGameplayAudioPlaybackPort _playbackPort;
        private GameplayTimingProfile _timingProfile = GameplayTimingProfile.CreateDefault();

        public EnemyAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int ScheduledRequestCount => _scheduledRequests.Count;

        internal int DeferredRequestCount => _deferredRequests.Count;

        public void ConfigureMoveCadence(int simulationTicksPerSecond)
        {
            _moveCadenceGate.Configure(simulationTicksPerSecond);
            _stationaryActiveCadenceGate.Configure(simulationTicksPerSecond);
        }

        public void ConfigureTiming(GameplayTimingProfile timingProfile)
        {
            _timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            ConfigureMoveCadence(_timingProfile.SimulationTicksPerSecond);
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            ClearScheduledRequests();
            _moveCadenceGate.ResetState();
            _stationaryActiveCadenceGate.ResetState();
        }

        public void DetachRuntime()
        {
            ClearScheduledRequests();
            _moveCadenceGate.ResetState();
            _stationaryActiveCadenceGate.ResetState();
            _playbackPort = null;
        }

        public void ResetSession()
        {
            ClearScheduledRequests();
            _moveCadenceGate.ResetState();
            _stationaryActiveCadenceGate.ResetState();
        }

        public void SetPlaybackGateState(GameplayAudioPlaybackGateState gateState)
        {
            _gateState = gateState;
        }

        public void Update(int tickIndex, float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            PlayScheduledAudio(tickIndex, deltaTime);
        }

        internal bool TryPlayBridgeRequest(
            in GameplayEnemyAudioPlaybackRequest request,
            out GameplayEnemyAudioPlaybackResult result)
        {
            if (_playbackPort == null)
            {
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.PortMissing);
                return false;
            }

            if (request.Cue == EnemyAudioCue.ChargeActiveLoop)
            {
                result = new GameplayEnemyAudioPlaybackResult(
                    GameplayEnemyAudioPlaybackResultKind.UnsupportedLoopSemantic);
                return false;
            }

            if (!IsSupportedOneShotCue(request.Cue))
            {
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.UnsupportedSemantic);
                return false;
            }

            var enemyRequest = CreateEnemyAudioRequest(request);
            var requestKey = CreateRequestKey(enemyRequest, request.TickIndex, request.OwnershipKey.OrderIndex);
            if (enemyRequest.DelaySeconds > 0f)
            {
                _scheduledRequests.Add(new ScheduledEnemyAudioRequest(
                    enemyRequest,
                    enemyRequest.DelaySeconds,
                    requestKey,
                    request.TickIndex));
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.Requested);
                return true;
            }

            if (EvaluatePlaybackPolicy(enemyRequest) == GameplayAudioPlaybackDecision.DeferUntilUnlock)
            {
                DeferRequest(new ScheduledEnemyAudioRequest(enemyRequest, 0f, requestKey, request.TickIndex));
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.Requested);
                return true;
            }

            return TryPlayRequest(enemyRequest, request.TickIndex, out result);
        }

        private void PlayScheduledAudio(int tickIndex, float deltaTime)
        {
            if (_playbackPort == null)
            {
                ClearScheduledRequests();
                return;
            }

            if (!_gateState.IsBlocked)
            {
                DrainDeferredRequests();
            }

            var retainedCount = 0;
            for (var i = 0; i < _scheduledRequests.Count; i++)
            {
                var scheduled = ShouldFreezeTimer(scheduledRequest: _scheduledRequests[i].Request)
                    ? _scheduledRequests[i]
                    : _scheduledRequests[i].Advance(deltaTime);
                if (scheduled.RemainingSeconds > 0f)
                {
                    _scheduledRequests[retainedCount++] = scheduled;
                    continue;
                }

                if (EvaluatePlaybackPolicy(scheduled.Request) == GameplayAudioPlaybackDecision.DeferUntilUnlock)
                {
                    DeferRequest(scheduled);
                    continue;
                }

                TryPlayRequest(scheduled.Request, scheduled.TickIndex, out _);
            }

            if (retainedCount < _scheduledRequests.Count)
            {
                _scheduledRequests.RemoveRange(retainedCount, _scheduledRequests.Count - retainedCount);
            }
        }

        private void ClearScheduledRequests()
        {
            _scheduledRequests.Clear();
            _deferredRequests.Clear();
            _deferredKeys.Clear();
            _playedDeferredKeys.Clear();
        }

        private bool TryPlayRequest(
            in EnemyAudioRequest request,
            int tickIndex,
            out GameplayEnemyAudioPlaybackResult result)
        {
            if (!TryResolveLiveOwner(request.OwnerEntityId, out var ownerView))
            {
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.OwnerViewMissing);
                return false;
            }

            if (request.Cue == EnemyAudioCue.StationaryActive &&
                IsStationaryActiveBlocked(request.OwnerEntityId))
            {
                result = new GameplayEnemyAudioPlaybackResult(
                    GameplayEnemyAudioPlaybackResultKind.OptionalProfileEntryMissing);
                return false;
            }

            if (!ownerView.TryGetComponent<EnemyAudioAuthoring>(out var authoring) ||
                authoring == null)
            {
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.AuthoringMissing);
                return false;
            }

            if (authoring.Profile == null)
            {
                result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.ProfileMissing);
                return false;
            }

            authoring.Profile.ValidateOrThrow();
            var resolveStatus = authoring.Profile.ResolveEntryStatus(request.Cue, out var binding);
            switch (resolveStatus)
            {
                case EnemyAudioProfileResolveStatus.Resolved:
                    break;
                case EnemyAudioProfileResolveStatus.EntryMissing:
                case EnemyAudioProfileResolveStatus.OptionalBindingMissing:
                    result = new GameplayEnemyAudioPlaybackResult(
                        GameplayEnemyAudioPlaybackResultKind.OptionalProfileEntryMissing);
                    return false;
                case EnemyAudioProfileResolveStatus.BindingMissing:
                case EnemyAudioProfileResolveStatus.None:
                default:
                    result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.BindingMissing);
                    return false;
            }

            if (request.Cue == EnemyAudioCue.Move &&
                !_moveCadenceGate.ShouldPlayMove(request.OwnerEntityId, tickIndex))
            {
                result = new GameplayEnemyAudioPlaybackResult(
                    GameplayEnemyAudioPlaybackResultKind.OptionalProfileEntryMissing);
                return false;
            }

            if (request.Cue == EnemyAudioCue.StationaryActive &&
                !_stationaryActiveCadenceGate.ShouldPlayStationaryActive(
                    request.OwnerEntityId,
                    tickIndex))
            {
                result = new GameplayEnemyAudioPlaybackResult(
                    GameplayEnemyAudioPlaybackResultKind.OptionalProfileEntryMissing);
                return false;
            }

            PlayResolvedBinding(binding, ownerView, request.Context);
            result = new GameplayEnemyAudioPlaybackResult(GameplayEnemyAudioPlaybackResultKind.Succeeded);
            return true;
        }

        private EnemyAudioRequest CreateEnemyAudioRequest(in GameplayEnemyAudioPlaybackRequest request)
        {
            var resolvedDelaySeconds = request.DelaySeconds;
            if (resolvedDelaySeconds <= 0f &&
                request.EnemyAudioPayload.Timing == (int)EntityExitPresentationTiming.AtContactTime)
            {
                resolvedDelaySeconds =
                    _timingProfile.FlipMotionDurationSeconds *
                    request.EnemyAudioPayload.VisualContactNormalizedTime;
            }

            var baseRequest = request.ToEnemyAudioRequest();
            return new EnemyAudioRequest(
                baseRequest.OwnerEntityId,
                baseRequest.Cue,
                baseRequest.Context,
                resolvedDelaySeconds,
                baseRequest.Identity,
                baseRequest.SemanticEvent);
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

        private static bool IsSupportedOneShotCue(EnemyAudioCue cue)
        {
            switch (cue)
            {
                case EnemyAudioCue.Move:
                case EnemyAudioCue.Death:
                case EnemyAudioCue.Windup:
                case EnemyAudioCue.Landing:
                case EnemyAudioCue.Active:
                case EnemyAudioCue.Recover:
                case EnemyAudioCue.ForwardCellImpact:
                case EnemyAudioCue.StationaryActive:
                case EnemyAudioCue.PassiveContact:
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
                TryPlayRequest(scheduled.Request, scheduled.TickIndex, out _);
                _playedDeferredKeys.Add(scheduled.Key);
            }

            _deferredRequests.Clear();
            _deferredKeys.Clear();
        }

        private void DeferRequest(in ScheduledEnemyAudioRequest scheduled)
        {
            if (_deferredKeys.Contains(scheduled.Key) ||
                _playedDeferredKeys.Contains(scheduled.Key))
            {
                return;
            }

            _deferredRequests.Add(scheduled);
            _deferredKeys.Add(scheduled.Key);
        }

        private bool ShouldFreezeTimer(in EnemyAudioRequest scheduledRequest)
        {
            return _gateState.IsBlocked &&
                   IsTopologyLockSensitive(scheduledRequest.Cue);
        }

        private GameplayAudioPlaybackDecision EvaluatePlaybackPolicy(in EnemyAudioRequest request)
        {
            if (!_gateState.IsBlocked)
            {
                return GameplayAudioPlaybackDecision.PlayNow;
            }

            if (_gateState.Reason == GameplayAudioPlaybackBlockReason.TopologyPresentationLock &&
                IsTopologyLockSensitive(request.Cue))
            {
                return GameplayAudioPlaybackDecision.DeferUntilUnlock;
            }

            return GameplayAudioPlaybackDecision.PlayNow;
        }

        private static bool IsTopologyLockSensitive(EnemyAudioCue cue)
        {
            return cue == EnemyAudioCue.ForwardCellImpact;
        }

        private static GameplayAudioPlaybackRequestKey CreateRequestKey(
            in EnemyAudioRequest request,
            int tickIndex,
            int orderIndex)
        {
            if (request.Identity.IsValid)
            {
                var identity = request.Identity;
                return new GameplayAudioPlaybackRequestKey(
                    EnemyAudioLaneId,
                    tickIndex,
                    (int)request.Cue,
                    request.OwnerEntityId,
                    orderIndex: 0,
                    targetFace: (int)identity.TargetCell.face,
                    targetX: identity.TargetCell.x,
                    targetY: identity.TargetCell.y,
                    eventTick: identity.ImpactTick,
                    eventId: identity.ImpactId,
                    presentationKey: identity.PresentationKey);
            }

            return new GameplayAudioPlaybackRequestKey(
                EnemyAudioLaneId,
                tickIndex,
                (int)request.Cue,
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

        private bool IsStationaryActiveBlocked(int ownerEntityId)
        {
            if (ownerEntityId <= 0)
            {
                return true;
            }

            if (_stateStore.CommittedFacesByEntityId.TryGetValue(ownerEntityId, out var committedFace))
            {
                return committedFace != _stateStore.CommittedTopology.BottomFace;
            }

            if (_stateStore.EnemyVisualFactsByEntityId.TryGetValue(ownerEntityId, out var facts) &&
                facts.IsGameplayAutonomySuppressed)
            {
                return true;
            }

            if (_stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(ownerEntityId, out var semanticState) &&
                semanticState.ActivityState == EnemyVisualActivityState.FrontFaceInactive)
            {
                return true;
            }

            return false;
        }

        private readonly struct ScheduledEnemyAudioRequest
        {
            public ScheduledEnemyAudioRequest(
                EnemyAudioRequest request,
                float remainingSeconds,
                GameplayAudioPlaybackRequestKey key,
                int tickIndex)
            {
                Request = request;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
                Key = key;
                TickIndex = Math.Max(0, tickIndex);
            }

            public EnemyAudioRequest Request { get; }

            public float RemainingSeconds { get; }

            public GameplayAudioPlaybackRequestKey Key { get; }

            public int TickIndex { get; }

            public ScheduledEnemyAudioRequest Advance(float deltaTime)
            {
                return new ScheduledEnemyAudioRequest(
                    Request,
                    RemainingSeconds - Math.Max(0f, deltaTime),
                    Key,
                    TickIndex);
            }
        }
    }
}
