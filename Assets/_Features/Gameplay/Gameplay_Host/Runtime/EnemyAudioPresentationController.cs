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
        private readonly List<ScheduledEnemyAudioRequest> _pendingRequests = new();
        private readonly List<ScheduledEnemyAudioRequest> _deferredRequests = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _deferredKeys = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _playedDeferredKeys = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private GameplayAudioPlaybackGateState _gateState = GameplayAudioPlaybackGateState.Open;
        private IGameplayAudioPlaybackPort _playbackPort;

        public EnemyAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        internal int DeferredRequestCount => _deferredRequests.Count;

        public void ConfigureMoveCadence(int simulationTicksPerSecond)
        {
            _moveCadenceGate.Configure(simulationTicksPerSecond);
            _stationaryActiveCadenceGate.Configure(simulationTicksPerSecond);
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            ClearPendingPlan();
            _moveCadenceGate.ResetState();
            _stationaryActiveCadenceGate.ResetState();
        }

        public void DetachRuntime()
        {
            ClearPendingPlan();
            _moveCadenceGate.ResetState();
            _stationaryActiveCadenceGate.ResetState();
            _playbackPort = null;
        }

        public void ResetSession()
        {
            ClearPendingPlan();
            _moveCadenceGate.ResetState();
            _stationaryActiveCadenceGate.ResetState();
        }

        public void ReplacePendingPlan(IReadOnlyList<EnemyAudioRequest> plannedRequests)
        {
            ReplacePendingPlan(plannedRequests, tickIndex: 0);
        }

        public void ReplacePendingPlan(IReadOnlyList<EnemyAudioRequest> plannedRequests, int tickIndex)
        {
            if (plannedRequests == null)
            {
                throw new ArgumentNullException(nameof(plannedRequests));
            }

            RemoveImmediatePendingRequests();
            for (var i = 0; i < plannedRequests.Count; i++)
            {
                var request = plannedRequests[i];
                _pendingRequests.Add(new ScheduledEnemyAudioRequest(
                    request,
                    request.DelaySeconds,
                    CreateRequestKey(request, tickIndex, i)));
            }
        }

        public void SetPlaybackGateState(GameplayAudioPlaybackGateState gateState)
        {
            _gateState = gateState;
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

            if (!_gateState.IsBlocked)
            {
                DrainDeferredRequests(tickIndex);
            }

            var retainedCount = 0;
            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                var pending = _pendingRequests[i];
                var scheduled = ShouldFreezeTimer(pending.Request)
                    ? pending
                    : pending.Advance(deltaTime);
                if (scheduled.RemainingSeconds > 0f)
                {
                    _pendingRequests[retainedCount++] = scheduled;
                    continue;
                }

                if (ShouldDefer(scheduled.Request))
                {
                    DeferRequest(scheduled);
                    continue;
                }

                if (!ShouldSuppress(scheduled.Request))
                {
                    PlayRequest(scheduled.Request, tickIndex);
                }
            }

            if (retainedCount < _pendingRequests.Count)
            {
                _pendingRequests.RemoveRange(retainedCount, _pendingRequests.Count - retainedCount);
            }
        }

        public void ClearPendingPlan()
        {
            _pendingRequests.Clear();
            _deferredRequests.Clear();
            _deferredKeys.Clear();
            _playedDeferredKeys.Clear();
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

            if (request.Cue == EnemyAudioCue.StationaryActive &&
                ShouldSuppressStationaryActive(request.OwnerEntityId))
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

            if (request.Cue == EnemyAudioCue.StationaryActive &&
                !_stationaryActiveCadenceGate.ShouldPlayStationaryActive(request.OwnerEntityId, tickIndex))
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

        private void DrainDeferredRequests(int tickIndex)
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

                PlayRequest(scheduled.Request, tickIndex);
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

        private bool ShouldFreezeTimer(in EnemyAudioRequest request)
        {
            return _gateState.IsBlocked &&
                   IsTopologyLockSensitive(request.Cue);
        }

        private bool ShouldDefer(in EnemyAudioRequest request)
        {
            return EvaluatePlaybackPolicy(request) == GameplayAudioPlaybackDecision.DeferUntilUnlock;
        }

        private bool ShouldSuppress(in EnemyAudioRequest request)
        {
            return EvaluatePlaybackPolicy(request) == GameplayAudioPlaybackDecision.Suppress;
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

        private bool ShouldSuppressStationaryActive(int ownerEntityId)
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

            return true;
        }

        private readonly struct ScheduledEnemyAudioRequest
        {
            public ScheduledEnemyAudioRequest(
                EnemyAudioRequest request,
                float remainingSeconds,
                GameplayAudioPlaybackRequestKey key)
            {
                Request = request;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
                Key = key;
            }

            public EnemyAudioRequest Request { get; }

            public float RemainingSeconds { get; }

            public GameplayAudioPlaybackRequestKey Key { get; }

            public ScheduledEnemyAudioRequest Advance(float deltaTime)
            {
                return new ScheduledEnemyAudioRequest(
                    Request,
                    RemainingSeconds - Math.Max(0f, deltaTime),
                    Key);
            }
        }
    }
}
