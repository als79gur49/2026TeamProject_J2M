namespace Game.Platform.Steam
{
    public readonly struct SteamStatsStoredObservation
    {
        public SteamStatsStoredObservation(uint appId, SteamCallbackResult result)
        {
            AppId = appId;
            Result = result;
        }

        public uint AppId { get; }

        public SteamCallbackResult Result { get; }
    }
}
