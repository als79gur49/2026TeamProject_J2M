using System;
using System.Collections.Generic;

namespace Game.Platform.Steam.Tests.EditMode
{
    internal sealed class FakeSteamAchievementApi : ISteamAchievementApi
    {
        internal readonly List<string> AchievementNames = new List<string>
        {
            SpacewarAchievementSmokePolicy.TargetAchievement,
        };

        internal bool BeforeReadResult { get; set; } = true;
        internal bool BeforeUnlocked { get; set; }
        internal bool AfterReadResult { get; set; } = true;
        internal bool AfterUnlocked { get; set; } = true;
        internal bool SetResult { get; set; } = true;
        internal bool StoreResult { get; set; } = true;
        internal Exception RegistrationException { get; set; }
        internal Exception DisposalException { get; set; }
        internal List<string> CallOrder { get; set; }

        internal int GetNumAchievementsCount { get; private set; }
        internal int GetAchievementNameCount { get; private set; }
        internal int GetAchievementCount { get; private set; }
        internal int SetAchievementCount { get; private set; }
        internal int StoreStatsCount { get; private set; }
        internal int RegistrationCount { get; private set; }
        internal int DisposalCount { get; private set; }

        private Action<SteamStatsStoredObservation> statsStoredObserver;
        private Action<SteamAchievementStoredObservation> achievementStoredObserver;

        public uint GetNumAchievements()
        {
            GetNumAchievementsCount++;
            return (uint)AchievementNames.Count;
        }

        public string GetAchievementName(uint index)
        {
            GetAchievementNameCount++;
            return AchievementNames[(int)index];
        }

        public bool GetAchievement(string achievementName, out bool achieved)
        {
            GetAchievementCount++;
            var isBeforeRead = GetAchievementCount == 1;
            achieved = isBeforeRead ? BeforeUnlocked : AfterUnlocked;
            return isBeforeRead ? BeforeReadResult : AfterReadResult;
        }

        public bool SetAchievement(string achievementName)
        {
            SetAchievementCount++;
            return SetResult;
        }

        public bool StoreStats()
        {
            StoreStatsCount++;
            return StoreResult;
        }

        public void RegisterAchievementStoreCallbacks(
            Action<SteamStatsStoredObservation> statsObserver,
            Action<SteamAchievementStoredObservation> achievementObserver)
        {
            RegistrationCount++;
            if (RegistrationException != null)
            {
                throw RegistrationException;
            }

            statsStoredObserver = statsObserver;
            achievementStoredObserver = achievementObserver;
        }

        public void DisposeAchievementStoreCallbacks()
        {
            DisposalCount++;
            CallOrder?.Add("achievement-dispose");
            statsStoredObserver = null;
            achievementStoredObserver = null;
            if (DisposalException != null)
            {
                throw DisposalException;
            }
        }

        internal void RaiseStatsStored(
            uint appId = SpacewarAchievementSmokePolicy.AppId,
            SteamCallbackResult result = SteamCallbackResult.Ok)
        {
            statsStoredObserver?.Invoke(new SteamStatsStoredObservation(appId, result));
        }

        internal void RaiseAchievementStored(
            uint appId = SpacewarAchievementSmokePolicy.AppId,
            string achievementName = SpacewarAchievementSmokePolicy.TargetAchievement,
            bool fullUnlock = true)
        {
            achievementStoredObserver?.Invoke(new SteamAchievementStoredObservation(
                appId,
                achievementName,
                fullUnlock));
        }
    }
}
