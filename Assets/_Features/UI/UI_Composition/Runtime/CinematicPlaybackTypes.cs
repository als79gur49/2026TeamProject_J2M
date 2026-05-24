namespace Game.Feature.UI.Composition
{
    public enum CinematicPlaybackCompletionKind
    {
        Completed = 0,
        Skipped = 1,
        Failed = 2,
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
            SlotCinematicAspectPolicy aspectPolicy,
            int renderTextureWidth,
            int renderTextureHeight)
        {
            SkipEnabled = skipEnabled;
            AspectPolicy = aspectPolicy;
            RenderTextureWidth = renderTextureWidth > 0 ? renderTextureWidth : 1920;
            RenderTextureHeight = renderTextureHeight > 0 ? renderTextureHeight : 1080;
        }

        public bool SkipEnabled { get; }

        public SlotCinematicAspectPolicy AspectPolicy { get; }

        public int RenderTextureWidth { get; }

        public int RenderTextureHeight { get; }
    }
}
