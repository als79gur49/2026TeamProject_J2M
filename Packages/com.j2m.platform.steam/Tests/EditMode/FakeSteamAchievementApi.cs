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
        internal Exception GetNumAchievementsException { get; set; }
        internal Exception GetAchievementNameException { get; set; }
        internal Exception GetAchievementException { get; set; }
        internal Exception SetAchievementException { get; set; }
        internal Exception StoreStatsException { get; set; }
        internal List<string> CallOrder { get; set; }
        internal List<string> RequestedAchievementNames { get; } = new List<string>();
        internal Queue<bool> ReadResultOverrides { get; } = new Queue<bool>();
        internal Queue<bool> UnlockedOverrides { get; } = new Queue<bool>();

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
            if (GetNumAchievementsException != null)
            {
                throw GetNumAchievementsException;
            }

            return (uint)AchievementNames.Count;
        }

        public string GetAchievementName(uint index)
        {
            GetAchievementNameCount++;
            if (GetAchievementNameException != null)
            {
                throw GetAchievementNameException;
            }

            return AchievementNames[(int)index];
        }

        public bool GetAchievement(string achievementName, out bool achieved)
        {
            GetAchievementCount++;
            RequestedAchievementNames.Add(achievementName);
            if (GetAchievementException != null)
            {
                throw GetAchievementException;
            }

            if (ReadResultOverrides.Count > 0 && UnlockedOverrides.Count > 0)
            {
                achieved = UnlockedOverrides.Dequeue();
                return ReadResultOverrides.Dequeue();
            }

            var isBeforeRead = GetAchievementCount == 1;
            achieved = isBeforeRead ? BeforeUnlocked : AfterUnlocked;
            return isBeforeRead ? BeforeReadResult : AfterReadResult;
        }

        public bool SetAchievement(string achievementName)
        {
            SetAchievementCount++;
            RequestedAchievementNames.Add(achievementName);
            if (SetAchievementException != null)
            {
                throw SetAchievementException;
            }

            return SetResult;
        }

        public bool StoreStats()
        {
            StoreStatsCount++;
            if (StoreStatsException != null)
            {
                throw StoreStatsException;
            }

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
