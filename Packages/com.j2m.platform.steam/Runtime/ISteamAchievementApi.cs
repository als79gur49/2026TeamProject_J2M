using System;

namespace Game.Platform.Steam
{
    public interface ISteamAchievementApi
    {
        uint GetNumAchievements();

        string GetAchievementName(uint index);

        bool GetAchievement(string achievementName, out bool achieved);

        bool SetAchievement(string achievementName);

        bool StoreStats();

        void RegisterAchievementStoreCallbacks(
            Action<SteamStatsStoredObservation> statsStoredObserver,
            Action<SteamAchievementStoredObservation> achievementStoredObserver);

        void DisposeAchievementStoreCallbacks();
    }
}
