namespace Game.Feature.UI.Composition
{
    public enum CinematicPresentationState
    {
        Idle = 0,
        EnterFadeToBlack = 1,
        PreparingVideo = 2,
        RevealFadeFromBlack = 3,
        Playing = 4,
        ExitFadeToBlack = 5,
        Completed = 6,
    }

    public enum CinematicExitReason
    {
        Skipped = 0,
        NaturalEnd = 1,
        Failed = 2,
    }
}
