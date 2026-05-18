using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class EnemyChargeLoopAudioPresentationController
    {
        private readonly Dictionary<int, ActiveChargeLoopState> _activeLoopsByEntityId = new();
        private readonly HashSet<int> _refreshedActiveEntityIds = new();
        private readonly GameplayPresentationStateStore _stateStore;

        private IGameplayAudioLoopPlaybackPort _playbackPort;

        public EnemyChargeLoopAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        internal int ActiveLoopCount => _activeLoopsByEntityId.Count;

        public void AttachRuntime(IGameplayAudioLoopPlaybackPort playbackPort)
        {
            StopAllLoops();
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
        }

        public void DetachRuntime()
        {
            StopAllLoops();
            _playbackPort = null;
        }

        public void ResetSession()
        {
            StopAllLoops();
        }

        public void RefreshSignals(IReadOnlyList<TickEnemyChargePresentationSignal> signals)
        {
            if (signals == null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            if (_playbackPort == null)
            {
                StopAllLoops();
                return;
            }

            _refreshedActiveEntityIds.Clear();
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    signal.Phase != EnemyChargePhase.Active)
                {
                    continue;
                }

                _refreshedActiveEntityIds.Add(signal.EntityId);
                RefreshActiveLoop(signal.EntityId, signal.Sequence);
            }

            StopStaleLoops();
            _refreshedActiveEntityIds.Clear();
        }

        private void RefreshActiveLoop(int entityId, int sequence)
        {
            if (_activeLoopsByEntityId.TryGetValue(entityId, out var activeLoop))
            {
                if (activeLoop.Sequence == sequence &&
                    activeLoop.Handle.IsValid &&
                    TryResolveLiveOwner(entityId, out _))
                {
                    return;
                }

                StopLoop(entityId);
            }

            TryStartLoop(entityId, sequence);
        }

        private void TryStartLoop(int entityId, int sequence)
        {
            if (!TryResolveLiveOwner(entityId, out var ownerView))
            {
                return;
            }

            var authoring = EnemyAudioAuthoring.GetOptionalValidatedAuthoring(ownerView);
            if (authoring == null ||
                !authoring.Profile.TryResolve(EnemyAudioCue.ChargeActiveLoop, out var binding))
            {
                return;
            }

            var context = new AudioPlaybackContext(
                ownerEntityId: entityId,
                debugTag: EnemyAudioCueCatalog.Format(EnemyAudioCue.ChargeActiveLoop));
            var handle = _playbackPort.PlayAttachedLoop(binding.Definition, ownerView, binding.AttachmentSlot, context);
            if (handle == null ||
                !handle.IsValid)
            {
                return;
            }

            _activeLoopsByEntityId[entityId] = new ActiveChargeLoopState(sequence, handle);
        }

        private void StopStaleLoops()
        {
            var activeEntityIds = new List<int>(_activeLoopsByEntityId.Keys);
            for (var i = 0; i < activeEntityIds.Count; i++)
            {
                if (!_refreshedActiveEntityIds.Contains(activeEntityIds[i]))
                {
                    StopLoop(activeEntityIds[i]);
                }
            }
        }

        private void StopAllLoops()
        {
            var activeEntityIds = new List<int>(_activeLoopsByEntityId.Keys);
            for (var i = 0; i < activeEntityIds.Count; i++)
            {
                StopLoop(activeEntityIds[i]);
            }

            _refreshedActiveEntityIds.Clear();
        }

        private void StopLoop(int entityId)
        {
            if (!_activeLoopsByEntityId.TryGetValue(entityId, out var activeLoop))
            {
                return;
            }

            _activeLoopsByEntityId.Remove(entityId);
            activeLoop.Handle.Stop();
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

        private readonly struct ActiveChargeLoopState
        {
            public ActiveChargeLoopState(int sequence, AudioPlaybackHandle handle)
            {
                Sequence = sequence;
                Handle = handle ?? AudioPlaybackHandle.Invalid;
            }

            public int Sequence { get; }

            public AudioPlaybackHandle Handle { get; }
        }
    }
}
