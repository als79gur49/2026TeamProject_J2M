namespace Game.Feature.Gameplay.UIAccess.Models
{
    public enum GameplayCommandRejectionReason
    {
        None = 0,
        Paused = 1,
        BlockingPresentation = 2,
        GameplayInputUnavailable = 3,
        NoControllableActor = 4,
        InvalidRequest = 5,
        Other = 6,
    }
}
