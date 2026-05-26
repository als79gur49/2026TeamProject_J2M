using System;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public enum CinematicPlaybackStartPolicy
    {
        AfterRevealFade = 0,
        WithRevealFade = 1,
    }

    public enum CinematicSkipDuringFadePolicy
    {
        IgnoreUntilPlaying = 0,
        QueueUntilPlaying = 1,
    }

    public enum CinematicFadeEase
    {
        SmoothStep = 0,
        Linear = 1,
    }

    [Serializable]
    public struct CinematicFadeSettings
    {
        [SerializeField] [Min(0f)] private float _enterFadeDuration;
        [SerializeField] [Min(0f)] private float _revealFadeDuration;
        [SerializeField] [Min(0f)] private float _exitFadeDuration;
        [SerializeField] private Color _fadeColor;
        [SerializeField] private CinematicFadeEase _fadeEase;
        [SerializeField] private bool _audioFadeOutWithExit;
        [SerializeField] private CinematicPlaybackStartPolicy _playbackStartPolicy;
        [SerializeField] private CinematicSkipDuringFadePolicy _skipDuringFadePolicy;

        public CinematicFadeSettings(
            float enterFadeDuration,
            float revealFadeDuration,
            float exitFadeDuration,
            Color fadeColor,
            CinematicFadeEase fadeEase,
            bool audioFadeOutWithExit,
            CinematicPlaybackStartPolicy playbackStartPolicy,
            CinematicSkipDuringFadePolicy skipDuringFadePolicy)
        {
            _enterFadeDuration = Mathf.Max(0f, enterFadeDuration);
            _revealFadeDuration = Mathf.Max(0f, revealFadeDuration);
            _exitFadeDuration = Mathf.Max(0f, exitFadeDuration);
            _fadeColor = fadeColor;
            _fadeEase = fadeEase;
            _audioFadeOutWithExit = audioFadeOutWithExit;
            _playbackStartPolicy = playbackStartPolicy;
            _skipDuringFadePolicy = skipDuringFadePolicy;
        }

        public static CinematicFadeSettings Default => new(
            0.25f,
            0.25f,
            0.30f,
            Color.black,
            CinematicFadeEase.SmoothStep,
            true,
            CinematicPlaybackStartPolicy.AfterRevealFade,
            CinematicSkipDuringFadePolicy.IgnoreUntilPlaying);

        public float EnterFadeDuration => Mathf.Max(0f, _enterFadeDuration);

        public float RevealFadeDuration => Mathf.Max(0f, _revealFadeDuration);

        public float ExitFadeDuration => Mathf.Max(0f, _exitFadeDuration);

        public Color FadeColor => _fadeColor;

        public CinematicFadeEase FadeEase => _fadeEase;

        public bool AudioFadeOutWithExit => _audioFadeOutWithExit;

        public CinematicPlaybackStartPolicy PlaybackStartPolicy => _playbackStartPolicy;

        public CinematicSkipDuringFadePolicy SkipDuringFadePolicy => _skipDuringFadePolicy;

        public float Evaluate(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return _fadeEase == CinematicFadeEase.Linear
                ? progress
                : progress * progress * (3f - 2f * progress);
        }
    }
}
