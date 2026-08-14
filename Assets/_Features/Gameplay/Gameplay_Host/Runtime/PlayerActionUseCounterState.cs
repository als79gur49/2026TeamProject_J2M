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
            if (_playerEntityId <= 0 ||
                signal.EntityId != _playerEntityId ||
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
}
