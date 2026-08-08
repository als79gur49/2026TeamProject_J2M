namespace Game.Platform.Steam
{
    public readonly struct SteamAchievementStoredObservation
    {
        public SteamAchievementStoredObservation(
            uint appId,
            string achievementName,
            bool isFullUnlock)
        {
            AppId = appId;
            AchievementName = achievementName ?? string.Empty;
            IsFullUnlock = isFullUnlock;
        }

        public uint AppId { get; }

        public string AchievementName { get; }

        public bool IsFullUnlock { get; }
    }
}
