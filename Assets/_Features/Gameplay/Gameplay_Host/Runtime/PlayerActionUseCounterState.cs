using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class PlayerActionUseCounterState
    {
        private readonly HashSet<PlayerActionUseKey> _consumedActions = new();
        private float _elapsedSeconds;
        private int _playerEntityId;

        public int PlayerEntityId => _playerEntityId;

        public int Count { get; private set; }

        public int VisibleCount { get; private set; }

        public bool IsVisible { get; private set; }

        public float Alpha { get; private set; }

        public void ConfigurePlayerEntityId(int playerEntityId)
        {
            if (playerEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerEntityId));
            }

            if (_playerEntityId == playerEntityId)
            {
                return;
            }

            _playerEntityId = playerEntityId;
            Reset();
        }

        public bool TryConsume(in TickPlayerActionPresentationSignal signal)
        {
            if (!IsCountable(signal))
            {
                return false;
            }

            var key = new PlayerActionUseKey(signal.EntityId, signal.ActiveActionSequence);
            if (!_consumedActions.Add(key))
            {
                return false;
            }

            Count++;
            return true;
        }

        public bool RevealCount(int count)
        {
            if (count <= VisibleCount || count > Count)
            {
                return false;
            }

            VisibleCount = count;
            _elapsedSeconds = 0f;
            Alpha = 1f;
            IsVisible = true;
            return true;
        }

        public bool TryReset(in TickPlayerDeathPresentationSignal signal)
        {
            if (signal.EntityId != _playerEntityId || !signal.DidDieThisTick)
            {
                return false;
            }

            Reset();
            return true;
        }

        public bool TryReset(in EntitySpawnPresentationSignal signal)
        {
            if (signal.EntityId != _playerEntityId ||
                signal.EntityKind != EntityPresentationKind.Player ||
                signal.Reason != EntitySpawnPresentationReason.PlayerRespawn)
            {
                return false;
            }

            Reset();
            return true;
        }

        public void Advance(float deltaTime, float opaqueDurationSeconds, float fadeDurationSeconds)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (opaqueDurationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(opaqueDurationSeconds));
            }

            if (fadeDurationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fadeDurationSeconds));
            }

            if (!IsVisible || deltaTime <= 0f)
            {
                return;
            }

            _elapsedSeconds += deltaTime;
            if (_elapsedSeconds <= opaqueDurationSeconds)
            {
                Alpha = 1f;
                return;
            }

            var fadeElapsed = _elapsedSeconds - opaqueDurationSeconds;
            if (fadeElapsed >= fadeDurationSeconds)
            {
                Alpha = 0f;
                IsVisible = false;
                return;
            }

            Alpha = 1f - (fadeElapsed / fadeDurationSeconds);
        }

        public void Reset()
        {
            _consumedActions.Clear();
            Count = 0;
            VisibleCount = 0;
            _elapsedSeconds = 0f;
            Alpha = 0f;
            IsVisible = false;
        }

        internal bool IsCountable(in TickPlayerActionPresentationSignal signal)
        {
            return CombinedPushFlipUsePolicy.IsCountable(_playerEntityId, signal);
        }

        private readonly struct PlayerActionUseKey : IEquatable<PlayerActionUseKey>
        {
            public PlayerActionUseKey(int entityId, int actionSequence)
            {
                EntityId = entityId;
                ActionSequence = actionSequence;
            }

            private int EntityId { get; }

            private int ActionSequence { get; }

            public bool Equals(PlayerActionUseKey other)
            {
                return EntityId == other.EntityId && ActionSequence == other.ActionSequence;
            }

            public override bool Equals(object obj)
            {
                return obj is PlayerActionUseKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId * 397) ^ ActionSequence;
                }
            }
        }
    }

    internal static class CombinedPushFlipUsePolicy
    {
        internal static bool IsCountable(
            int playerEntityId,
            in TickPlayerActionPresentationSignal signal)
        {
            if (playerEntityId <= 0 ||
                signal.EntityId != playerEntityId ||
                signal.ActiveActionSequence <= 0 ||
                !signal.ExecutedThisTick ||
                signal.CanceledThisTick)
            {
                return false;
            }

            if (signal.ActiveActionKind != PlayerActionKind.Push &&
                signal.ActiveActionKind != PlayerActionKind.Flip)
            {
                return false;
            }

            return signal.ResolutionKind == TickPlayerActionResolutionKind.Success ||
                   signal.ResolutionKind == TickPlayerActionResolutionKind.Impact;
        }
    }

    internal readonly struct StageAttemptMetricsSnapshot
    {
        internal StageAttemptMetricsSnapshot(int combinedPushFlipUses)
        {
            CombinedPushFlipUses = combinedPushFlipUses;
        }

        internal int CombinedPushFlipUses { get; }
    }

    internal sealed class StageAttemptPushFlipTracker
    {
        private readonly HashSet<ActionUseKey> _consumedActions = new();
        private int _playerEntityId;

        internal StageAttemptMetricsSnapshot Snapshot => new(CombinedPushFlipUses);

        internal int CombinedPushFlipUses { get; private set; }

        internal void Observe(TickResult result, int playerEntityId)
        {
            if (result == null || playerEntityId <= 0)
            {
                return;
            }

            if (_playerEntityId != playerEntityId)
            {
                _playerEntityId = playerEntityId;
                Reset();
            }

            var presentationData = result.PresentationData;
            var deathSignals = presentationData.PlayerDeathSignals;
            for (var i = 0; i < deathSignals.Count; i++)
            {
                if (deathSignals[i].EntityId == playerEntityId &&
                    deathSignals[i].DidDieThisTick)
                {
                    Reset();
                    return;
                }
            }

            var spawnSignals = presentationData.EntitySpawnSignals;
            for (var i = 0; i < spawnSignals.Count; i++)
            {
                if (spawnSignals[i].EntityId == playerEntityId &&
                    spawnSignals[i].EntityKind == EntityPresentationKind.Player &&
                    spawnSignals[i].Reason == EntitySpawnPresentationReason.PlayerRespawn)
                {
                    Reset();
                    return;
                }
            }

            var actionSignals = presentationData.PlayerActionSignals;
            for (var i = 0; i < actionSignals.Count; i++)
            {
                var signal = actionSignals[i];
                if (!CombinedPushFlipUsePolicy.IsCountable(playerEntityId, signal) ||
                    !_consumedActions.Add(new ActionUseKey(
                        signal.EntityId,
                        signal.ActiveActionSequence)))
                {
                    continue;
                }

                CombinedPushFlipUses++;
            }
        }

        internal void Reset()
        {
            _consumedActions.Clear();
            CombinedPushFlipUses = 0;
        }

        private readonly struct ActionUseKey : IEquatable<ActionUseKey>
        {
            internal ActionUseKey(int entityId, int sequence)
            {
                EntityId = entityId;
                Sequence = sequence;
            }

            private int EntityId { get; }

            private int Sequence { get; }

            public bool Equals(ActionUseKey other)
            {
                return EntityId == other.EntityId && Sequence == other.Sequence;
            }

            public override bool Equals(object obj)
            {
                return obj is ActionUseKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId * 397) ^ Sequence;
                }
            }
        }
    }
}
