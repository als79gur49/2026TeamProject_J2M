using System;

namespace Game.Shared.Audio
{
    public enum AudioBgmTransitionMode
    {
        Immediate = 0,
        FadeOutIn = 1,
    }

    public readonly struct AudioBgmPlaybackRequest
    {
        public AudioBgmPlaybackRequest(AudioDefinition definition, AudioBgmTransition transition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Transition = transition;
        }

        public AudioDefinition Definition { get; }

        public AudioBgmTransition Transition { get; }
    }

    public readonly struct AudioBgmStopRequest
    {
        public AudioBgmStopRequest(AudioBgmTransition transition)
        {
            Transition = transition;
        }

        public AudioBgmTransition Transition { get; }
    }

    public readonly struct AudioBgmTransition
    {
        public AudioBgmTransition(
            AudioBgmTransitionMode mode,
            float fadeOutSeconds = 0f,
            float fadeInSeconds = 0f)
        {
            ValidateDuration(fadeOutSeconds, nameof(fadeOutSeconds));
            ValidateDuration(fadeInSeconds, nameof(fadeInSeconds));

            if (!Enum.IsDefined(typeof(AudioBgmTransitionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported audio BGM transition mode.");
            }

            Mode = mode;
            FadeOutSeconds = mode == AudioBgmTransitionMode.Immediate ? 0f : fadeOutSeconds;
            FadeInSeconds = mode == AudioBgmTransitionMode.Immediate ? 0f : fadeInSeconds;
        }

        public static AudioBgmTransition Immediate => new(AudioBgmTransitionMode.Immediate);

        public static AudioBgmTransition FadeOutIn(float fadeOutSeconds, float fadeInSeconds)
        {
            return new AudioBgmTransition(AudioBgmTransitionMode.FadeOutIn, fadeOutSeconds, fadeInSeconds);
        }

        public AudioBgmTransitionMode Mode { get; }

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
                    "Audio BGM fade duration must be finite and greater than or equal to zero.");
            }
        }
    }
}
