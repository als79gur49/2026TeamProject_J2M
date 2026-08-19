namespace Game.Feature.UI.Composition
{
    public enum CinematicPlaybackCompletionKind
    {
        Completed = 0,
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

}
