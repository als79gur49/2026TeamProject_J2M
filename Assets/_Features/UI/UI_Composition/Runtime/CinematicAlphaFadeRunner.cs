using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class CinematicAlphaFadeRunner
    {
        private CinematicFadeSettings _settings;
        private float _from;
        private float _to;
        private float _duration;
        private float _elapsed;

        public bool IsRunning { get; private set; }

        public float CurrentAlpha { get; private set; }

        public float Progress => _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);

        public bool Begin(float from, float to, float duration, CinematicFadeSettings settings)
        {
            _settings = settings;
            _from = Mathf.Clamp01(from);
            _to = Mathf.Clamp01(to);
            _duration = Mathf.Max(0f, duration);
            _elapsed = 0f;

            if (_duration <= 0f || Mathf.Approximately(_from, _to))
            {
                CurrentAlpha = _to;
                IsRunning = false;
                return true;
            }

            CurrentAlpha = _from;
            IsRunning = true;
            return false;
        }

        public bool Advance(float deltaSeconds)
        {
            if (!IsRunning)
            {
                return true;
            }

            _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, deltaSeconds));
            var easedProgress = _settings.Evaluate(Progress);
            CurrentAlpha = Mathf.Lerp(_from, _to, easedProgress);

            if (_elapsed < _duration)
            {
                return false;
            }

            CurrentAlpha = _to;
            IsRunning = false;
            return true;
        }
    }
}
