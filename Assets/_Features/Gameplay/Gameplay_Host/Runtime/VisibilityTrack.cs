using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class VisibilityTrack
    {
        private readonly VisibilityClip _clip;

        private VisibilityTrack(VisibilityClip clip)
        {
            _clip = clip ?? throw new ArgumentNullException(nameof(clip));
        }

        public bool IsActive => !_clip.IsComplete;

        public bool IsComplete => _clip.IsComplete;

        public bool TargetVisibility => _clip.FinalVisibility;

        public static VisibilityTrack CreateHide(float durationSeconds)
        {
            return new VisibilityTrack(VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds,
                transitionThreshold: 1f));
        }

        public static VisibilityTrack CreateShow()
        {
            return new VisibilityTrack(VisibilityClip.Create(
                initialVisibility: false,
                finalVisibility: true,
                durationSeconds: 0.0001f,
                transitionThreshold: 0f));
        }

        public bool SampleAndAdvance(float deltaTime, bool fallbackVisibility)
        {
            return _clip.SampleAndAdvance(deltaTime, fallbackVisibility);
        }
    }

    public sealed class VisibilityClip
    {
        private VisibilityClip(
            bool initialVisibility,
            bool finalVisibility,
            float durationSeconds,
            float transitionThreshold)
        {
            InitialVisibility = initialVisibility;
            FinalVisibility = finalVisibility;
            DurationSeconds = Mathf.Max(durationSeconds, 0.0001f);
            TransitionThreshold = Mathf.Clamp01(transitionThreshold);
            ElapsedSeconds = 0f;
        }

        public float DurationSeconds { get; }

        public float ElapsedSeconds { get; private set; }

        public bool FinalVisibility { get; }

        public bool InitialVisibility { get; }

        public bool IsComplete => RemainingSeconds <= 0.0001f;

        public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

        public float TransitionThreshold { get; }

        public static VisibilityClip Create(
            bool initialVisibility,
            bool finalVisibility,
            float durationSeconds,
            float transitionThreshold)
        {
            return new VisibilityClip(
                initialVisibility,
                finalVisibility,
                durationSeconds,
                transitionThreshold);
        }

        public bool SampleAndAdvance(float deltaTime, bool fallbackVisibility)
        {
            if (deltaTime > 0f)
            {
                ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
            }

            if (DurationSeconds <= 0f)
            {
                return FinalVisibility;
            }

            var normalizedTime = Mathf.Clamp01(ElapsedSeconds / DurationSeconds);
            return normalizedTime >= TransitionThreshold
                ? FinalVisibility
                : InitialVisibility;
        }
    }
}
