namespace Game.Feature.Flow.Audio
{
    public enum BgmTransitionMode
    {
        // The only executed transition mode in BGM flow v1.
        Immediate = 0,
        // Reserved future policy value. v1 degrades this to Immediate with one warning path.
        FadeOutIn = 1,
        // Reserved future policy value. True execution needs runtime capability beyond v1.
        Crossfade = 2,
    }
}
