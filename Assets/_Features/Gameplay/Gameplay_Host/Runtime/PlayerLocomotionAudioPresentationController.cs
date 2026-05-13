using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class PlayerLocomotionAudioPresentationController
    {
        private const float MinimumStepIntervalSeconds = 0.01f;

        private readonly Dictionary<int, ActiveWalkLoopState> _activeWalkLoopsByEntityId = new();
        private readonly HashSet<int> _refreshedActiveEntityIds = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private PlayerLocomotionAudioMap _audioMap;
        private IGameplayAudioPlaybackPort _playbackPort;
        private float _stepIntervalSeconds = MinimumStepIntervalSeconds;

        public PlayerLocomotionAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            PlayerLocomotionAudioMap audioMap)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
            _audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            _audioMap.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1);
            ClearActiveLoops();
        }

        public void DetachRuntime()
        {
            ClearActiveLoops();
            _audioMap = null;
            _playbackPort = null;
        }

        public void ResetSession()
        {
            ClearActiveLoops();
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
                    !signal.ShouldPlayWalkLoop)
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
                return;
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

        private struct ActiveWalkLoopState
        {
            public ActiveWalkLoopState(float remainingSeconds)
            {
                RemainingSeconds = remainingSeconds;
            }

            public float RemainingSeconds;
        }
    }
}
