using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal enum PlayerDeathDisplacementTrackState
    {
        Animating = 1,
        Settled = 2,
        Cleared = 3,
    }

    internal sealed class PlayerDeathDisplacementTrack
    {
        private const float CompletionEpsilon = 0.0001f;

        private readonly float _durationSeconds;
        private readonly Vector3 _targetOffsetLocal;
        private float _elapsedSeconds;

        public PlayerDeathDisplacementTrack(Vector3 targetOffsetLocal, float durationSeconds)
        {
            _targetOffsetLocal = targetOffsetLocal;
            _durationSeconds = Mathf.Max(CompletionEpsilon, durationSeconds);
            State = PlayerDeathDisplacementTrackState.Animating;
        }

        public Vector3 CurrentOffset => State switch
        {
            PlayerDeathDisplacementTrackState.Cleared => Vector3.zero,
            PlayerDeathDisplacementTrackState.Settled => _targetOffsetLocal,
            _ => _targetOffsetLocal * EvaluateEaseOutCubic(NormalizedTime),
        };

        public bool IsAnimating => State == PlayerDeathDisplacementTrackState.Animating;

        public PlayerDeathDisplacementTrackState State { get; private set; }

        private float NormalizedTime => Mathf.Clamp01(_elapsedSeconds / _durationSeconds);

        public void Advance(float deltaTime)
        {
            if (State != PlayerDeathDisplacementTrackState.Animating ||
                deltaTime <= 0f)
            {
                return;
            }

            _elapsedSeconds = Mathf.Min(_durationSeconds, _elapsedSeconds + deltaTime);
            if (_elapsedSeconds >= _durationSeconds - CompletionEpsilon)
            {
                _elapsedSeconds = _durationSeconds;
                State = PlayerDeathDisplacementTrackState.Settled;
            }
        }

        public void Clear()
        {
            State = PlayerDeathDisplacementTrackState.Cleared;
            _elapsedSeconds = _durationSeconds;
        }

        private static float EvaluateEaseOutCubic(float normalizedTime)
        {
            var inverse = 1f - Mathf.Clamp01(normalizedTime);
            return 1f - (inverse * inverse * inverse);
        }
    }
}
