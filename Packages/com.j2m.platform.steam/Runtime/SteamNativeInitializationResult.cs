namespace Game.Platform.Steam
{
    public enum SteamNativeInitializationResult
    {
        NotAttempted = 0,
        Succeeded = 1,
        ReturnedFalse = 2,
        Threw = 3,
    }
}
