using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class PlayerLocomotionAudioPresentationController
    {
        private const float MinimumStepIntervalSeconds = 0.01f;
        private const double TopologyTransitionBlockedCooldownSeconds = 0.3d;

        private readonly Dictionary<int, ActiveWalkLoopState> _activeWalkLoopsByEntityId = new();
        private readonly Dictionary<TopologyTransitionBlockedCooldownKey, double> _nextAllowedTimeByKey = new();
        private readonly HashSet<int> _pendingSequenceIds = new();
        private readonly List<ScheduledPlayerLocomotionAudioRequest> _pendingRequests = new();
        private readonly HashSet<int> _playedSequenceIds = new();
        private readonly PlayerLocomotionAudioRequestPlanner _requestPlanner = new();
        private readonly HashSet<int> _refreshedActiveEntityIds = new();
        private readonly HashSet<int> _stageClearSuppressedEntityIds = new();
        private readonly HashSet<int> _terminalEntityIdsThisTick = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private PlayerLocomotionAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;
        private float _stepIntervalSeconds = MinimumStepIntervalSeconds;
        private int _playedSequenceScopeTick = int.MinValue;
        private readonly Func<double> _timeProvider;

        public PlayerLocomotionAudioPresentationController(
            GameplayPresentationStateStore stateStore,
            Func<double> timeProvider = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _timeProvider = timeProvider ?? (() => UnityEngine.Time.unscaledTimeAsDouble);
        }

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            PlayerLocomotionAudioMap audioMap)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            _audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            _audioMap.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1);
            ClearActiveLoops();
            ClearPendingPlan();
            ClearOneShotPlaybackState();
            ClearTopologyTransitionBlockedCooldowns();
            ClearTerminalSuppression();
        }

        public void DetachRuntime()
        {
            ClearActiveLoops();
            ClearPendingPlan();
            ClearOneShotPlaybackState();
            ClearTopologyTransitionBlockedCooldowns();
            ClearTerminalSuppression();
            _audioMap = null;
            _playbackPort = null;
        }

        public void ResetSession()
        {
            ClearActiveLoops();
            ClearPendingPlan();
            ClearTerminalSuppression();
            ClearOneShotPlaybackState();
            ClearTopologyTransitionBlockedCooldowns();
        }

        public void RefreshSignals(
            TickResult result,
            float stepIntervalSeconds)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            RefreshTerminalSuppression(result);
            ReplacePendingPlan(_requestPlanner.BuildRequests(result), result.TickIndex);
            RefreshSignals(result.PresentationData.PlayerLocomotionSignals, stepIntervalSeconds);
        }

        public void ReplacePendingPlan(
            IReadOnlyList<PlayerLocomotionAudioRequest> plannedRequests,
            int tickIndex)
        {
            if (plannedRequests == null)
            {
                throw new ArgumentNullException(nameof(plannedRequests));
            }

            RefreshPlayedSequenceScope(tickIndex);
            var now = _timeProvider();
            PruneExpiredTopologyTransitionBlockedCooldowns(now);

            for (var i = 0; i < plannedRequests.Count; i++)
            {
                var request = plannedRequests[i];
                if (_playedSequenceIds.Contains(request.SequenceId) ||
                    IsSuppressedByTopologyTransitionBlockedCooldown(request, now))
                {
                    continue;
                }

                if (!_pendingSequenceIds.Add(request.SequenceId))
                {
                    continue;
                }

                _pendingRequests.Add(
                    new ScheduledPlayerLocomotionAudioRequest(
                        request,
                        request.DelaySeconds,
                        tickIndex));
            }
        }

        public void RefreshSignals(
            IReadOnlyList<TickPlayerLocomotionPresentationSignal> signals,
            float stepIntervalSeconds)
        {
            if (signals == null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            _stepIntervalSeconds = Math.Max(MinimumStepIntervalSeconds, stepIntervalSeconds);
            _refreshedActiveEntityIds.Clear();

            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    !signal.ShouldPlayWalkLoop ||
                    IsLocomotionSuppressed(signal.EntityId))
                {
                    continue;
                }

                _refreshedActiveEntityIds.Add(signal.EntityId);
                if (!_activeWalkLoopsByEntityId.ContainsKey(signal.EntityId))
                {
                    _activeWalkLoopsByEntityId.Add(signal.EntityId, new ActiveWalkLoopState(remainingSeconds: 0f));
                }
            }

            var activeEntityIds = new List<int>(_activeWalkLoopsByEntityId.Keys);
            for (var i = 0; i < activeEntityIds.Count; i++)
            {
                if (!_refreshedActiveEntityIds.Contains(activeEntityIds[i]))
                {
                    _activeWalkLoopsByEntityId.Remove(activeEntityIds[i]);
                }
            }

            _refreshedActiveEntityIds.Clear();
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
                ClearActiveLoops();
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
                RefreshPlayedSequenceScope(scheduled.TickIndex);
                if (_playedSequenceIds.Add(scheduled.Request.SequenceId))
                {
                    PlayRequest(scheduled.Request);
                    RegisterTopologyTransitionBlockedCooldown(scheduled.Request, _timeProvider());
                }

                _pendingRequests.RemoveAt(i);
            }

            var activeEntityIds = new List<int>(_activeWalkLoopsByEntityId.Keys);
            for (var i = 0; i < activeEntityIds.Count; i++)
            {
                var entityId = activeEntityIds[i];
                var state = _activeWalkLoopsByEntityId[entityId];
                state.RemainingSeconds -= deltaTime;
                if (state.RemainingSeconds > 0f)
                {
                    _activeWalkLoopsByEntityId[entityId] = state;
                    continue;
                }

                PlayWalkStep(entityId);
                state.RemainingSeconds = _stepIntervalSeconds;
                _activeWalkLoopsByEntityId[entityId] = state;
            }
        }

        private void PlayWalkStep(int entityId)
        {
            var binding = _audioMap.ResolveOrThrow(PlayerLocomotionAudioCue.WalkStep);
            var context = new AudioPlaybackContext(
                ownerEntityId: entityId,
                debugTag: PlayerLocomotionAudioCueCatalog.Format(PlayerLocomotionAudioCue.WalkStep));

            if (binding.HasAttachmentSlot &&
                TryResolveOwner(entityId, out var owner))
            {
                _playbackPort.PlayAttached(binding.Definition, owner, binding.AttachmentSlot, context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, context);
        }

        private void PlayRequest(in PlayerLocomotionAudioRequest request)
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

        private void ClearActiveLoops()
        {
            _activeWalkLoopsByEntityId.Clear();
            _refreshedActiveEntityIds.Clear();
        }

        private void ClearPendingPlan()
        {
            _pendingRequests.Clear();
            _pendingSequenceIds.Clear();
        }

        private void ClearOneShotPlaybackState()
        {
            _playedSequenceIds.Clear();
            _playedSequenceScopeTick = int.MinValue;
        }

        private void RefreshPlayedSequenceScope(int tickIndex)
        {
            if (_playedSequenceScopeTick == tickIndex)
            {
                return;
            }

            _playedSequenceIds.Clear();
            _playedSequenceScopeTick = tickIndex;
        }

        private void ClearTopologyTransitionBlockedCooldowns()
        {
            _nextAllowedTimeByKey.Clear();
        }

        private bool IsSuppressedByTopologyTransitionBlockedCooldown(
            in PlayerLocomotionAudioRequest request,
            double now)
        {
            if (request.Cue != PlayerLocomotionAudioCue.TopologyTransitionBlocked)
            {
                return false;
            }

            var key = new TopologyTransitionBlockedCooldownKey(request);
            if (!_nextAllowedTimeByKey.TryGetValue(key, out var nextAllowedTime))
            {
                return false;
            }

            if (now < nextAllowedTime)
            {
                return true;
            }

            _nextAllowedTimeByKey.Remove(key);
            return false;
        }

        private void RegisterTopologyTransitionBlockedCooldown(
            in PlayerLocomotionAudioRequest request,
            double now)
        {
            if (request.Cue == PlayerLocomotionAudioCue.TopologyTransitionBlocked)
            {
                _nextAllowedTimeByKey[new TopologyTransitionBlockedCooldownKey(request)] =
                    now + TopologyTransitionBlockedCooldownSeconds;
                PruneExpiredTopologyTransitionBlockedCooldowns(now);
            }
        }

        private void PruneExpiredTopologyTransitionBlockedCooldowns(double now)
        {
            if (_nextAllowedTimeByKey.Count == 0)
            {
                return;
            }

            List<TopologyTransitionBlockedCooldownKey> expiredKeys = null;
            foreach (var entry in _nextAllowedTimeByKey)
            {
                if (entry.Value > now)
                {
                    continue;
                }

                expiredKeys ??= new List<TopologyTransitionBlockedCooldownKey>();
                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
            {
                return;
            }

            for (var i = 0; i < expiredKeys.Count; i++)
            {
                _nextAllowedTimeByKey.Remove(expiredKeys[i]);
            }
        }

        private void RefreshTerminalSuppression(TickResult result)
        {
            _terminalEntityIdsThisTick.Clear();

            var presentationData = result.PresentationData;
            var playerOutcomeSignals = presentationData.PlayerOutcomeSignals;
            for (var i = 0; i < playerOutcomeSignals.Count; i++)
            {
                var signal = playerOutcomeSignals[i];
                if (signal.EntityId <= 0 ||
                    signal.OutcomeKind != TickPlayerOutcomePresentationKind.StageClearVictory)
                {
                    continue;
                }

                _stageClearSuppressedEntityIds.Add(signal.EntityId);
                _terminalEntityIdsThisTick.Add(signal.EntityId);
            }

            var playerDeathSignals = presentationData.PlayerDeathSignals;
            for (var i = 0; i < playerDeathSignals.Count; i++)
            {
                AddTerminalEntityThisTick(playerDeathSignals[i].EntityId);
            }

            var playerDeathHoldSignals = presentationData.PlayerDeathHoldSignals;
            for (var i = 0; i < playerDeathHoldSignals.Count; i++)
            {
                AddTerminalEntityThisTick(playerDeathHoldSignals[i].EntityId);
            }

            var entityExitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < entityExitSignals.Count; i++)
            {
                AddTerminalEntityThisTick(entityExitSignals[i].ExitedEntityId);
            }

            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind == TickVisibilityChangeKind.Remove)
                {
                    AddTerminalEntityThisTick(change.EntityId);
                }
            }

            var finalEntities = result.FinalEntities;
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (entity.unitRole == UnitRole.Player &&
                    (entity.hp <= 0 || entity.markedForDeath))
                {
                    AddTerminalEntityThisTick(entity.entityId);
                }
            }

            foreach (var entityId in _terminalEntityIdsThisTick)
            {
                _activeWalkLoopsByEntityId.Remove(entityId);
                _refreshedActiveEntityIds.Remove(entityId);
            }
        }

        private void AddTerminalEntityThisTick(int entityId)
        {
            if (entityId > 0)
            {
                _terminalEntityIdsThisTick.Add(entityId);
            }
        }

        private bool IsLocomotionSuppressed(int entityId)
        {
            return _stageClearSuppressedEntityIds.Contains(entityId) ||
                   _terminalEntityIdsThisTick.Contains(entityId);
        }

        private void ClearTerminalSuppression()
        {
            _stageClearSuppressedEntityIds.Clear();
            _terminalEntityIdsThisTick.Clear();
        }

        private struct ActiveWalkLoopState
        {
            public ActiveWalkLoopState(float remainingSeconds)
            {
                RemainingSeconds = remainingSeconds;
            }

            public float RemainingSeconds;
        }

        private struct ScheduledPlayerLocomotionAudioRequest
        {
            public ScheduledPlayerLocomotionAudioRequest(
                PlayerLocomotionAudioRequest request,
                float remainingSeconds,
                int tickIndex)
            {
                Request = request;
                RemainingSeconds = remainingSeconds;
                TickIndex = tickIndex;
            }

            public PlayerLocomotionAudioRequest Request { get; }

            public int TickIndex { get; }

            public float RemainingSeconds;
        }

        private readonly struct TopologyTransitionBlockedCooldownKey :
            IEquatable<TopologyTransitionBlockedCooldownKey>
        {
            public TopologyTransitionBlockedCooldownKey(in PlayerLocomotionAudioRequest request)
            {
                EntityId = request.OwnerEntityId;
                Direction = request.GateDirection;
                CandidateCell = request.GateCandidateCell;
            }

            private int EntityId { get; }

            private Direction Direction { get; }

            private SurfaceCell CandidateCell { get; }

            public bool Equals(TopologyTransitionBlockedCooldownKey other)
            {
                return EntityId == other.EntityId &&
                       Direction == other.Direction &&
                       CandidateCell.Equals(other.CandidateCell);
            }

            public override bool Equals(object obj)
            {
                return obj is TopologyTransitionBlockedCooldownKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = EntityId;
                    hash = (hash * 397) ^ (int)Direction;
                    hash = (hash * 397) ^ CandidateCell.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
