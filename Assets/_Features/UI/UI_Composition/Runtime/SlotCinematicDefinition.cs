using UnityEngine;
using UnityEngine.Video;

namespace Game.Feature.UI.Composition
{
    public enum SlotCinematicKind
    {
        Intro = 0,
        Outro = 1,
    }

    public enum CinematicAspectSource
    {
        AutoResolvedViewport = 0,
        SettingsSelectedAspect = 1,
        FixedAspect = 2,
        VideoClipAspect = 3,
    }

    public enum CinematicScaleMode
    {
        CropToFillViewport = 0,
        StretchToViewport = 1,
        FitInsideViewport = 2,
    }

    [CreateAssetMenu(
        fileName = "SlotCinematicDefinition",
        menuName = "Game/UI/Slot Cinematic Definition")]
    public sealed class SlotCinematicDefinition : ScriptableObject
    {
        [SerializeField] private VideoClip _introClip;
        [SerializeField] private VideoClip _outroClip;
        [SerializeField] private bool _skipEnabled = true;
        [SerializeField] private CinematicAspectSource _aspectSource = CinematicAspectSource.AutoResolvedViewport;
        [SerializeField] private CinematicScaleMode _scaleMode = CinematicScaleMode.FitInsideViewport;
        [SerializeField] [Min(0.01f)] private float _fixedAspectRatio = 16f / 9f;
        [SerializeField] [Min(16)] private int _renderTextureWidth = 1920;
        [SerializeField] [Min(16)] private int _renderTextureHeight = 1080;
        [SerializeField] private CinematicFadeSettings _fadeSettings = CinematicFadeSettings.Default;

        public VideoClip IntroClip => _introClip;

        public VideoClip OutroClip => _outroClip;

        public bool SkipEnabled => _skipEnabled;

        public CinematicAspectSource AspectSource => _aspectSource;

        public CinematicScaleMode ScaleMode => _scaleMode;

        public float FixedAspectRatio => _fixedAspectRatio > 0f ? _fixedAspectRatio : 16f / 9f;

        public int RenderTextureWidth => Mathf.Max(16, _renderTextureWidth);

        public int RenderTextureHeight => Mathf.Max(16, _renderTextureHeight);

        public CinematicFadeSettings FadeSettings => _fadeSettings;

        public VideoClip GetClip(SlotCinematicKind kind)
        {
            return kind == SlotCinematicKind.Outro ? _outroClip : _introClip;
        }

        public SlotCinematicPlaybackOptions CreatePlaybackOptions()
        {
            return new SlotCinematicPlaybackOptions(
                _skipEnabled,
                _aspectSource,
                _scaleMode,
                FixedAspectRatio,
                RenderTextureWidth,
                RenderTextureHeight,
                FadeSettings);
        }
    }
}
