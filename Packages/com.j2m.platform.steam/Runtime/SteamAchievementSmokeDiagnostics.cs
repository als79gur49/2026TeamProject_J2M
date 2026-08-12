namespace Game.Platform.Steam
{
    public readonly struct SteamAchievementSmokeDiagnostics
    {
        internal SteamAchievementSmokeDiagnostics(
            bool achievementSmokeRequested,
            bool achievementOptInValid,
            uint observedAppId,
            uint achievementCount,
            bool targetAchievementFound,
            bool beforeReadSucceeded,
            bool targetBeforeUnlocked,
            bool mutationAttempted,
            bool setAchievementReturned,
            bool storeStatsReturned,
            int statsStoredCallbackCount,
            SteamCallbackResult statsStoredResult,
            int achievementStoredCallbackCount,
            bool targetAchievementStoredObserved,
            bool fullUnlockObserved,
            bool afterReadSucceeded,
            bool targetAfterUnlocked,
            bool callbackTimedOut,
            SteamAchievementSmokeOutcome terminalOutcome,
            SteamAchievementSmokePhase terminalPhase,
            SteamAchievementSmokeFailureKind failureKind,
            string blockedReason)
        {
            AchievementSmokeRequested = achievementSmokeRequested;
            AchievementOptInValid = achievementOptInValid;
            ObservedAppId = observedAppId;
            AchievementCount = achievementCount;
            TargetAchievementFound = targetAchievementFound;
            BeforeReadSucceeded = beforeReadSucceeded;
            TargetBeforeUnlocked = targetBeforeUnlocked;
            MutationAttempted = mutationAttempted;
            SetAchievementReturned = setAchievementReturned;
            StoreStatsReturned = storeStatsReturned;
            StatsStoredCallbackCount = statsStoredCallbackCount;
            StatsStoredResult = statsStoredResult;
            AchievementStoredCallbackCount = achievementStoredCallbackCount;
            TargetAchievementStoredObserved = targetAchievementStoredObserved;
            FullUnlockObserved = fullUnlockObserved;
            AfterReadSucceeded = afterReadSucceeded;
            TargetAfterUnlocked = targetAfterUnlocked;
            CallbackTimedOut = callbackTimedOut;
            TerminalOutcome = terminalOutcome;
            TerminalPhase = terminalPhase;
            FailureKind = failureKind;
            BlockedReason = blockedReason ?? string.Empty;
        }

        public bool AchievementSmokeRequested { get; }
        public bool AchievementOptInValid { get; }
        public uint ObservedAppId { get; }
        public uint AchievementCount { get; }
        public bool TargetAchievementFound { get; }
        public bool BeforeReadSucceeded { get; }
        public bool TargetBeforeUnlocked { get; }
        public bool MutationAttempted { get; }
        public bool SetAchievementReturned { get; }
        public bool StoreStatsReturned { get; }
        public int StatsStoredCallbackCount { get; }
        public SteamCallbackResult StatsStoredResult { get; }
        public int AchievementStoredCallbackCount { get; }
        public bool TargetAchievementStoredObserved { get; }
        public bool FullUnlockObserved { get; }
        public bool AfterReadSucceeded { get; }
        public bool TargetAfterUnlocked { get; }
        public bool CallbackTimedOut { get; }
        public SteamAchievementSmokeOutcome TerminalOutcome { get; }
        public SteamAchievementSmokePhase TerminalPhase { get; }
        public SteamAchievementSmokeFailureKind FailureKind { get; }
        public string BlockedReason { get; }
    }
}
