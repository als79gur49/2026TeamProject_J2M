using System;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [Serializable]
    public sealed class ComicCinematicPanelDefinition
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Rect _referenceRect;

        public Sprite Sprite => _sprite;

        public Rect ReferenceRect => _referenceRect;

        internal bool TryValidate(out string failureReason)
        {
            if (_sprite == null)
            {
                failureReason = "A comic cinematic panel sprite is missing.";
                return false;
            }

            if (_referenceRect.width <= 0f || _referenceRect.height <= 0f)
            {
                failureReason = "A comic cinematic panel reference rect must have a positive size.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ComicCinematicPageDefinition
    {
        [SerializeField] private ComicCinematicPanelDefinition[] _panels =
            Array.Empty<ComicCinematicPanelDefinition>();

        public ComicCinematicPanelDefinition[] Panels =>
            _panels ?? Array.Empty<ComicCinematicPanelDefinition>();

        internal bool TryValidate(int pageIndex, out string failureReason)
        {
            var panels = Panels;
            if (panels.Length == 0)
            {
                failureReason = $"Comic cinematic page {pageIndex + 1} has no panels.";
                return false;
            }

            for (var panelIndex = 0; panelIndex < panels.Length; panelIndex++)
            {
                var panel = panels[panelIndex];
                if (panel == null)
                {
                    failureReason =
                        $"Comic cinematic page {pageIndex + 1}, panel {panelIndex + 1} is missing.";
                    return false;
                }

                if (!panel.TryValidate(out failureReason))
                {
                    failureReason =
                        $"Comic cinematic page {pageIndex + 1}, panel {panelIndex + 1} is invalid. " +
                        failureReason;
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public struct ComicCinematicTimingSettings
    {
        [SerializeField] [Min(0f)] private float _enterFadeDuration;
        [SerializeField] [Min(0f)] private float _panelRevealDuration;
        [SerializeField] [Min(0f)] private float _pageFadeOutDuration;
        [SerializeField] [Min(0f)] private float _pageFadeInDuration;
        [SerializeField] [Min(0f)] private float _finalSwapFadeOutDuration;
        [SerializeField] [Min(0f)] private float _finalSwapBlackHoldDuration;
        [SerializeField] [Min(0f)] private float _finalSwapFadeInDuration;
        [SerializeField] [Min(0f)] private float _exitFadeDuration;
        [SerializeField] private Color _fadeColor;
        [SerializeField] private CinematicFadeEase _fadeEase;

        public ComicCinematicTimingSettings(
            float enterFadeDuration,
            float panelRevealDuration,
            float pageFadeOutDuration,
            float pageFadeInDuration,
            float finalSwapFadeOutDuration,
            float finalSwapBlackHoldDuration,
            float finalSwapFadeInDuration,
            float exitFadeDuration,
            Color fadeColor,
            CinematicFadeEase fadeEase)
        {
            _enterFadeDuration = Mathf.Max(0f, enterFadeDuration);
            _panelRevealDuration = Mathf.Max(0f, panelRevealDuration);
            _pageFadeOutDuration = Mathf.Max(0f, pageFadeOutDuration);
            _pageFadeInDuration = Mathf.Max(0f, pageFadeInDuration);
            _finalSwapFadeOutDuration = Mathf.Max(0f, finalSwapFadeOutDuration);
            _finalSwapBlackHoldDuration = Mathf.Max(0f, finalSwapBlackHoldDuration);
            _finalSwapFadeInDuration = Mathf.Max(0f, finalSwapFadeInDuration);
            _exitFadeDuration = Mathf.Max(0f, exitFadeDuration);
            _fadeColor = fadeColor;
            _fadeEase = fadeEase;
        }

        public static ComicCinematicTimingSettings Default => new(
            0.25f,
            0.18f,
            0.30f,
            0.25f,
            0.35f,
            0.10f,
            0.50f,
            0.30f,
            Color.black,
            CinematicFadeEase.SmoothStep);

        public float EnterFadeDuration => Mathf.Max(0f, _enterFadeDuration);
        public float PanelRevealDuration => Mathf.Max(0f, _panelRevealDuration);
        public float PageFadeOutDuration => Mathf.Max(0f, _pageFadeOutDuration);
        public float PageFadeInDuration => Mathf.Max(0f, _pageFadeInDuration);
        public float FinalSwapFadeOutDuration => Mathf.Max(0f, _finalSwapFadeOutDuration);
        public float FinalSwapBlackHoldDuration => Mathf.Max(0f, _finalSwapBlackHoldDuration);
        public float FinalSwapFadeInDuration => Mathf.Max(0f, _finalSwapFadeInDuration);
        public float ExitFadeDuration => Mathf.Max(0f, _exitFadeDuration);
        public Color FadeColor => _fadeColor;
        public CinematicFadeEase FadeEase => _fadeEase;

    }

    [CreateAssetMenu(
        fileName = "ComicCinematicSequenceDefinition",
        menuName = "Game/UI/Comic Cinematic Sequence Definition")]
    public sealed class ComicCinematicSequenceDefinition : ScriptableObject
    {
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        public const float FinalShotAspectRatio = 2.244f;

        [SerializeField] private ComicCinematicPageDefinition[] _pages =
            Array.Empty<ComicCinematicPageDefinition>();
        [SerializeField] private Sprite _finalBeforeSprite;
        [SerializeField] private Sprite _finalAfterSprite;
        [SerializeField] private AudioClip _audioClip;
        [SerializeField] private ComicCinematicTimingSettings _timing =
            ComicCinematicTimingSettings.Default;

        public ComicCinematicPageDefinition[] Pages =>
            _pages ?? Array.Empty<ComicCinematicPageDefinition>();

        public Sprite FinalBeforeSprite => _finalBeforeSprite;
        public Sprite FinalAfterSprite => _finalAfterSprite;
        public AudioClip AudioClip => _audioClip;
        public ComicCinematicTimingSettings Timing => _timing;
        public bool HasFinalShots => _finalBeforeSprite != null && _finalAfterSprite != null;
        public bool HasContent => Pages.Length > 0 || HasFinalShots;

        public bool TryValidate(out string failureReason)
        {
            if (!HasContent)
            {
                failureReason = "Comic cinematic sequence has no pages or final shots.";
                return false;
            }

            var pages = Pages;
            for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
            {
                var page = pages[pageIndex];
                if (page == null)
                {
                    failureReason = $"Comic cinematic page {pageIndex + 1} is missing.";
                    return false;
                }

                if (!page.TryValidate(pageIndex, out failureReason))
                {
                    return false;
                }
            }

            if ((_finalBeforeSprite == null) != (_finalAfterSprite == null))
            {
                failureReason =
                    "Comic cinematic final shots must provide both before and after sprites.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }
}
