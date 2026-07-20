namespace Game.Feature.UI.Composition
{
    public enum CinematicPlaybackCompletionKind
    {
        Completed = 0,
        Skipped = 1,
        Failed = 2,
        Cancelled = 3,
    }

    public readonly struct CinematicPlaybackCompletion
    {
        public CinematicPlaybackCompletion(CinematicPlaybackCompletionKind kind, string message = "")
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public CinematicPlaybackCompletionKind Kind { get; }

        public string Message { get; }
    }

    public readonly struct SlotCinematicPlaybackOptions
    {
        public SlotCinematicPlaybackOptions(
            bool skipEnabled,
            CinematicAspectSource aspectSource,
            CinematicScaleMode scaleMode,
            float fixedAspectRatio,
            int renderTextureWidth,
            int renderTextureHeight)
            : this(
                skipEnabled,
                aspectSource,
                scaleMode,
                fixedAspectRatio,
                renderTextureWidth,
                renderTextureHeight,
                CinematicFadeSettings.Default)
        {
        }

        public SlotCinematicPlaybackOptions(
            bool skipEnabled,
            CinematicAspectSource aspectSource,
            CinematicScaleMode scaleMode,
            float fixedAspectRatio,
            int renderTextureWidth,
            int renderTextureHeight,
            CinematicFadeSettings fadeSettings)
        {
            SkipEnabled = skipEnabled;
            AspectSource = aspectSource;
            ScaleMode = scaleMode;
            FixedAspectRatio = fixedAspectRatio > 0f ? fixedAspectRatio : 16f / 9f;
            RenderTextureWidth = renderTextureWidth > 0 ? renderTextureWidth : 1920;
            RenderTextureHeight = renderTextureHeight > 0 ? renderTextureHeight : 1080;
            FadeSettings = fadeSettings;
        }

        public bool SkipEnabled { get; }

        public CinematicAspectSource AspectSource { get; }

        public CinematicScaleMode ScaleMode { get; }

        public float FixedAspectRatio { get; }

        public int RenderTextureWidth { get; }

        public int RenderTextureHeight { get; }

        public CinematicFadeSettings FadeSettings { get; }
    }
}
