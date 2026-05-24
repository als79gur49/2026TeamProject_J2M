using UnityEngine;
using UnityEngine.Video;

namespace Game.Feature.UI.Composition
{
    public enum SlotCinematicKind
    {
        Intro = 0,
        Outro = 1,
    }

    public enum SlotCinematicAspectPolicy
    {
        FitInside = 0,
    }

    [CreateAssetMenu(
        fileName = "SlotCinematicDefinition",
        menuName = "Game/UI/Slot Cinematic Definition")]
    public sealed class SlotCinematicDefinition : ScriptableObject
    {
        [SerializeField] private VideoClip _introClip;
        [SerializeField] private VideoClip _outroClip;
        [SerializeField] private bool _skipEnabled = true;
        [SerializeField] private SlotCinematicAspectPolicy _aspectPolicy = SlotCinematicAspectPolicy.FitInside;
        [SerializeField] [Min(16)] private int _renderTextureWidth = 1920;
        [SerializeField] [Min(16)] private int _renderTextureHeight = 1080;

        public VideoClip IntroClip => _introClip;

        public VideoClip OutroClip => _outroClip;

        public bool SkipEnabled => _skipEnabled;

        public SlotCinematicAspectPolicy AspectPolicy => _aspectPolicy;

        public int RenderTextureWidth => Mathf.Max(16, _renderTextureWidth);

        public int RenderTextureHeight => Mathf.Max(16, _renderTextureHeight);

        public VideoClip GetClip(SlotCinematicKind kind)
        {
            return kind == SlotCinematicKind.Outro ? _outroClip : _introClip;
        }

        public SlotCinematicPlaybackOptions CreatePlaybackOptions()
        {
            return new SlotCinematicPlaybackOptions(
                _skipEnabled,
                _aspectPolicy,
                RenderTextureWidth,
                RenderTextureHeight);
        }
    }
}
