using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class RotationTrack
    {
        private readonly List<RotationClip> _clips = new();

        public bool HasClips => _clips.Count > 0;

        public Quaternion TailEndValue => _clips[_clips.Count - 1].EndValue;

        public void Append(RotationClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            _clips.Add(clip);
        }

        public void Clear()
        {
            _clips.Clear();
        }

        public Quaternion SampleAndAdvance(float deltaTime, Quaternion fallbackValue)
        {
            if (_clips.Count == 0)
            {
                return fallbackValue;
            }

            var remainingDeltaTime = deltaTime;
            while (_clips.Count > 0)
            {
                var clip = _clips[0];
                remainingDeltaTime = clip.Advance(remainingDeltaTime);
                var value = clip.IsComplete
                    ? clip.EndValue
                    : clip.Sample();
                if (!clip.IsComplete)
                {
                    return value;
                }

                _clips.RemoveAt(0);
                if (_clips.Count == 0)
                {
                    return value;
                }

                if (remainingDeltaTime <= 0f)
                {
                    return value;
                }
            }

            return fallbackValue;
        }
    }

    public sealed class RotationClip
    {
        private RotationClip(Quaternion startValue, Quaternion endValue, float durationSeconds)
        {
            StartValue = startValue;
            EndValue = endValue;
            DurationSeconds = durationSeconds;
            ElapsedSeconds = 0f;
        }

        public Quaternion EndValue { get; }

        public float DurationSeconds { get; }

        public float ElapsedSeconds { get; private set; }

        public bool IsComplete => RemainingSeconds <= 0.0001f;

        public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

        public Quaternion StartValue { get; }

        public static RotationClip Create(Quaternion startValue, Quaternion endValue, float durationSeconds)
        {
            return new RotationClip(startValue, endValue, Mathf.Max(durationSeconds, 0.0001f));
        }

        public float Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            var consumedTime = Mathf.Min(RemainingSeconds, deltaTime);
            ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
            return Mathf.Max(0f, deltaTime - consumedTime);
        }

        public Quaternion Sample()
        {
            var t = DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(ElapsedSeconds / DurationSeconds);

            return Quaternion.SlerpUnclamped(StartValue, EndValue, EaseOutQuad(t));
        }

        private static float EaseOutQuad(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - (inverse * inverse);
        }
    }
}
