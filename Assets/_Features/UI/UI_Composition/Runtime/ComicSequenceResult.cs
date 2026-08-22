namespace Game.Feature.UI.Composition
{
    public enum ComicSequenceResultKind
    {
        Completed = 0,
        Failed = 2,
        Cancelled = 3,
    }

    public readonly struct ComicSequenceResult
    {
        public ComicSequenceResult(ComicSequenceResultKind kind, string message = "")
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public ComicSequenceResultKind Kind { get; }

        public string Message { get; }
    }

}
