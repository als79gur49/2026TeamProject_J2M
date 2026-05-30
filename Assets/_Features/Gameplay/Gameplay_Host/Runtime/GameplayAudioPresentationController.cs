using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal enum GameplayAudioPlaybackBlockReason
    {
        None = 0,
        TopologyPresentationLock = 1,
    }

    internal enum GameplayAudioPlaybackDecision
    {
        PlayNow = 0,
        DeferUntilUnlock = 1,
        Suppress = 2,
    }

    internal readonly struct GameplayAudioPlaybackGateState
    {
        public GameplayAudioPlaybackGateState(
            bool isBlocked,
            GameplayAudioPlaybackBlockReason reason)
        {
            IsBlocked = isBlocked;
            Reason = isBlocked ? reason : GameplayAudioPlaybackBlockReason.None;
        }

        public bool IsBlocked { get; }

        public GameplayAudioPlaybackBlockReason Reason { get; }

        public static GameplayAudioPlaybackGateState Open =>
            new(false, GameplayAudioPlaybackBlockReason.None);

        public static GameplayAudioPlaybackGateState TopologyLocked =>
            new(true, GameplayAudioPlaybackBlockReason.TopologyPresentationLock);
    }

    internal readonly struct GameplayAudioPlaybackRequestKey : IEquatable<GameplayAudioPlaybackRequestKey>
    {
        public GameplayAudioPlaybackRequestKey(
            int laneId,
            int tickIndex,
            int semanticId,
            int ownerEntityId,
            int orderIndex,
            int targetFace = 0,
            int targetX = 0,
            int targetY = 0,
            int eventTick = 0,
            int eventId = 0,
            int presentationKey = 0)
        {
            LaneId = laneId;
            TickIndex = tickIndex;
            SemanticId = semanticId;
            OwnerEntityId = ownerEntityId;
            OrderIndex = orderIndex;
            TargetFace = targetFace;
            TargetX = targetX;
            TargetY = targetY;
            EventTick = eventTick;
            EventId = eventId;
            PresentationKey = presentationKey;
        }

        public int LaneId { get; }

        public int TickIndex { get; }

        public int SemanticId { get; }

        public int OwnerEntityId { get; }

        public int OrderIndex { get; }

        public int TargetFace { get; }

        public int TargetX { get; }

        public int TargetY { get; }

        public int EventTick { get; }

        public int EventId { get; }

        public int PresentationKey { get; }

        public bool Equals(GameplayAudioPlaybackRequestKey other)
        {
            return LaneId == other.LaneId &&
                   TickIndex == other.TickIndex &&
                   SemanticId == other.SemanticId &&
                   OwnerEntityId == other.OwnerEntityId &&
                   OrderIndex == other.OrderIndex &&
                   TargetFace == other.TargetFace &&
                   TargetX == other.TargetX &&
                   TargetY == other.TargetY &&
                   EventTick == other.EventTick &&
                   EventId == other.EventId &&
                   PresentationKey == other.PresentationKey;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayAudioPlaybackRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = LaneId;
                hash = (hash * 397) ^ TickIndex;
                hash = (hash * 397) ^ SemanticId;
                hash = (hash * 397) ^ OwnerEntityId;
                hash = (hash * 397) ^ OrderIndex;
                hash = (hash * 397) ^ TargetFace;
                hash = (hash * 397) ^ TargetX;
                hash = (hash * 397) ^ TargetY;
                hash = (hash * 397) ^ EventTick;
                hash = (hash * 397) ^ EventId;
                hash = (hash * 397) ^ PresentationKey;
                return hash;
            }
        }
    }

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
        private const int CoreGameplayAudioLaneId = 1;

        private readonly List<ScheduledGameplayAudioRequest> _pendingRequests = new();
        private readonly List<ScheduledGameplayAudioRequest> _deferredRequests = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _deferredKeys = new();
        private readonly HashSet<GameplayAudioPlaybackRequestKey> _playedDeferredKeys = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private GameplayAudioMap _audioMap;
        private GameplayAudioPlaybackGateState _gateState = GameplayAudioPlaybackGateState.Open;
        private IGameplayAudioPlaybackPort _playbackPort;

        public GameplayAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int PendingRequestCount => _pendingRequests.Count;

        internal int DeferredRequestCount => _deferredRequests.Count;

        internal GameplayAudioPlaybackGateState PlaybackGateState => _gateState;

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
            ReplacePendingPlan(plannedRequests, tickIndex: 0);
        }

        public void ReplacePendingPlan(IReadOnlyList<GameplayAudioRequest> plannedRequests, int tickIndex)
        {
            if (plannedRequests == null)
            {
                throw new ArgumentNullException(nameof(plannedRequests));
            }

            RemoveImmediatePendingRequests();
            for (var i = 0; i < plannedRequests.Count; i++)
            {
                var request = plannedRequests[i];
                _pendingRequests.Add(new ScheduledGameplayAudioRequest(
                    request,
                    request.DelaySeconds,
                    CreateRequestKey(request, tickIndex, i)));
            }
        }

        public void SetPlaybackGateState(GameplayAudioPlaybackGateState gateState)
        {
            _gateState = gateState;
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

            if (!_gateState.IsBlocked)
            {
                DrainDeferredRequests();
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
                    PlayRequest(scheduled.Request);
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

        private void DeferRequest(in ScheduledGameplayAudioRequest scheduled)
        {
            if (_deferredKeys.Contains(scheduled.Key) ||
                _playedDeferredKeys.Contains(scheduled.Key))
            {
                return;
            }

            _deferredRequests.Add(scheduled);
            _deferredKeys.Add(scheduled.Key);
        }

        private bool ShouldFreezeTimer(in GameplayAudioRequest request)
        {
            return _gateState.IsBlocked &&
                   IsTopologyLockSensitive(request.SemanticId);
        }

        private bool ShouldDefer(in GameplayAudioRequest request)
        {
            return EvaluatePlaybackPolicy(request) == GameplayAudioPlaybackDecision.DeferUntilUnlock;
        }

        private bool ShouldSuppress(in GameplayAudioRequest request)
        {
            return EvaluatePlaybackPolicy(request) == GameplayAudioPlaybackDecision.Suppress;
        }

        private GameplayAudioPlaybackDecision EvaluatePlaybackPolicy(in GameplayAudioRequest request)
        {
            if (!_gateState.IsBlocked)
            {
                return GameplayAudioPlaybackDecision.PlayNow;
            }

            if (_gateState.Reason == GameplayAudioPlaybackBlockReason.TopologyPresentationLock &&
                IsTopologyLockSensitive(request.SemanticId))
            {
                return GameplayAudioPlaybackDecision.DeferUntilUnlock;
            }

            return GameplayAudioPlaybackDecision.PlayNow;
        }

        private static bool IsTopologyLockSensitive(GameplayAudioSemanticId semanticId)
        {
            return semanticId == GameplayAudioSemanticId.PlayerDamage ||
                   semanticId == GameplayAudioSemanticId.EnemyDamage;
        }

        private static GameplayAudioPlaybackRequestKey CreateRequestKey(
            in GameplayAudioRequest request,
            int tickIndex,
            int orderIndex)
        {
            return new GameplayAudioPlaybackRequestKey(
                CoreGameplayAudioLaneId,
                tickIndex,
                (int)request.SemanticId,
                request.OwnerEntityId ?? 0,
                orderIndex);
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
            public ScheduledGameplayAudioRequest(
                GameplayAudioRequest request,
                float remainingSeconds,
                GameplayAudioPlaybackRequestKey key)
            {
                Request = request;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
                Key = key;
            }

            public GameplayAudioRequest Request { get; }

            public float RemainingSeconds { get; }

            public GameplayAudioPlaybackRequestKey Key { get; }

            public ScheduledGameplayAudioRequest Advance(float deltaTime)
            {
                return new ScheduledGameplayAudioRequest(
                    Request,
                    RemainingSeconds - Math.Max(0f, deltaTime),
                    Key);
            }
        }
    }
}
