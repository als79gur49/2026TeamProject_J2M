namespace Game.Platform.Steam
{
    public enum SteamPlatformFailureReason
    {
        None = 0,
        PacksizeMismatch = 1,
        DllMissing = 2,
        BadImageFormat = 3,
        EntryPointMissing = 4,
        SteamClientUnavailable = 5,
        AppIdUnavailable = 6,
        InitializationReturnedFalse = 7,
        InitializationException = 8,
        CallbackException = 9,
        ShutdownException = 10,
    }
}
