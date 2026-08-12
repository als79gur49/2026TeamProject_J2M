namespace Game.Platform.Steam
{
    public enum SteamAchievementSmokeFailureKind
    {
        None = 0,
        Prerequisite = 1,
        ReturnedFalse = 2,
        CallbackError = 3,
        Timeout = 4,
        Exception = 5,
        VerificationMismatch = 6,
        IncompleteAtShutdown = 7,
    }
}
