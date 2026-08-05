namespace Game.Platform.Steam
{
    public enum SteamPlatformRuntimeState
    {
        NotInitialized = 0,
        Initializing = 1,
        Available = 2,
        Unavailable = 3,
        ShuttingDown = 4,
        Shutdown = 5,
        Faulted = 6,
    }
}
