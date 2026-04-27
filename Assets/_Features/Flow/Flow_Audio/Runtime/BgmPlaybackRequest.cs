using System;
using Game.Shared.Audio;

namespace Game.Feature.Flow.Audio
{
    public enum BgmExecutedTransitionMode
    {
        Immediate = 0,
        FadeOutIn = 1,
    }

    public readonly struct BgmPlaybackRequest
    {
        public BgmPlaybackRequest(AudioDefinition definition, BgmPlaybackTransition transition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Transition = transition;
        }

        public AudioDefinition Definition { get; }

        public BgmPlaybackTransition Transition { get; }
    }

    public readonly struct BgmStopRequest
    {
        public BgmStopRequest(BgmPlaybackTransition transition)
        {
            Transition = transition;
        }

        public BgmPlaybackTransition Transition { get; }
    }

    public readonly struct BgmPlaybackTransition
    {
        public BgmPlaybackTransition(
            BgmExecutedTransitionMode mode,
            float fadeOutSeconds = 0f,
            float fadeInSeconds = 0f)
        {
            ValidateDuration(fadeOutSeconds, nameof(fadeOutSeconds));
            ValidateDuration(fadeInSeconds, nameof(fadeInSeconds));

            if (!Enum.IsDefined(typeof(BgmExecutedTransitionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported BGM transition mode.");
            }

            Mode = mode;
            FadeOutSeconds = mode == BgmExecutedTransitionMode.Immediate ? 0f : fadeOutSeconds;
            FadeInSeconds = mode == BgmExecutedTransitionMode.Immediate ? 0f : fadeInSeconds;
        }

        public static BgmPlaybackTransition Immediate => new(BgmExecutedTransitionMode.Immediate);

        public static BgmPlaybackTransition FadeOutIn(float fadeOutSeconds, float fadeInSeconds)
        {
            return new BgmPlaybackTransition(BgmExecutedTransitionMode.FadeOutIn, fadeOutSeconds, fadeInSeconds);
        }

        public BgmExecutedTransitionMode Mode { get; }

        public float FadeOutSeconds { get; }

        public float FadeInSeconds { get; }

        public bool HasAnyFadeDuration => FadeOutSeconds > 0f || FadeInSeconds > 0f;

        private static void ValidateDuration(float seconds, string parameterName)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    seconds,
                    "BGM fade duration must be finite and greater than or equal to zero.");
            }
        }
    }
}
