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

        /// <summary>
        /// Registers the single achievement callback pair atomically.
        /// A failed registration must leave any existing registration unchanged.
        /// </summary>
        void RegisterAchievementStoreCallbacks(
            Action<SteamStatsStoredObservation> statsStoredObserver,
            Action<SteamAchievementStoredObservation> achievementStoredObserver);

        /// <summary>
        /// Disposes the callback pair acquired by a successful registration.
        /// Callers that failed to register do not own callbacks to dispose.
        /// </summary>
        void DisposeAchievementStoreCallbacks();
    }
}
