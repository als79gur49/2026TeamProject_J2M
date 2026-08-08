using System;

namespace Game.Platform.Steam
{
    internal sealed class SteamAchievementSmokeCoordinator
    {
        internal const string ResultPrefix = "J2M_STEAM_ACHIEVEMENT_SMOKE_RESULT";

        private readonly ISteamAchievementApi achievementApi;
        private readonly bool baseSmokeRequested;
        private readonly bool achievementSmokeRequested;
        private readonly Func<double> monotonicSeconds;
        private readonly Action<string> logger;

        private bool sessionStarted;
        private bool callbacksRegistered;
        private bool shutdown;
        private bool resultEmitted;
        private bool terminal;
        private bool achievementOptInValid;
        private uint observedAppId;
        private uint achievementCount;
        private bool targetAchievementFound;
        private bool beforeReadSucceeded;
        private bool targetBeforeUnlocked;
        private bool mutationAttempted;
        private bool setAchievementReturned;
        private bool storeStatsReturned;
        private int statsStoredCallbackCount;
        private SteamCallbackResult statsStoredResult;
        private int achievementStoredCallbackCount;
        private bool targetAchievementStoredObserved;
        private bool fullUnlockObserved;
        private bool afterReadSucceeded;
        private bool targetAfterUnlocked;
        private bool callbackTimedOut;
        private SteamAchievementSmokeOutcome terminalOutcome;
        private SteamAchievementSmokePhase phase = SteamAchievementSmokePhase.OptIn;
        private SteamAchievementSmokePhase terminalPhase = SteamAchievementSmokePhase.OptIn;
        private SteamAchievementSmokeFailureKind failureKind;
        private string blockedReason = string.Empty;
        private double callbackWaitStartedAt;

        internal SteamAchievementSmokeCoordinator(
            ISteamAchievementApi achievementApi,
            bool baseSmokeRequested,
            bool achievementSmokeRequested,
            Func<double> monotonicSeconds,
            Action<string> logger)
        {
            this.achievementApi = achievementApi;
            this.baseSmokeRequested = baseSmokeRequested;
            this.achievementSmokeRequested = achievementSmokeRequested;
            this.monotonicSeconds = monotonicSeconds ??
                (() => UnityEngine.Time.realtimeSinceStartupAsDouble);
            this.logger = logger;
        }

        internal SteamAchievementSmokeDiagnostics Diagnostics =>
            new SteamAchievementSmokeDiagnostics(
                achievementSmokeRequested,
                achievementOptInValid,
                observedAppId,
                achievementCount,
                targetAchievementFound,
                beforeReadSucceeded,
                targetBeforeUnlocked,
                mutationAttempted,
                setAchievementReturned,
                storeStatsReturned,
                statsStoredCallbackCount,
                statsStoredResult,
                achievementStoredCallbackCount,
                targetAchievementStoredObserved,
                fullUnlockObserved,
                afterReadSucceeded,
                targetAfterUnlocked,
                callbackTimedOut,
                terminalOutcome,
                terminalPhase,
                failureKind,
                blockedReason);

        internal void BeginSession(
            bool initializationSucceeded,
            uint appId,
            bool steamIdentityValid,
            bool loggedOn)
        {
            if (sessionStarted || shutdown)
            {
                return;
            }

            sessionStarted = true;
            observedAppId = appId;

            if (!achievementSmokeRequested)
            {
                Complete(
                    SteamAchievementSmokeOutcome.NotRequested,
                    SteamAchievementSmokePhase.OptIn,
                    SteamAchievementSmokeFailureKind.None,
                    string.Empty);
                return;
            }

            if (!baseSmokeRequested)
            {
                Complete(
                    SteamAchievementSmokeOutcome.Blocked,
                    SteamAchievementSmokePhase.OptIn,
                    SteamAchievementSmokeFailureKind.Prerequisite,
                    "InvalidOptInCombination");
                return;
            }

            achievementOptInValid = true;
            phase = SteamAchievementSmokePhase.Session;
            if (!initializationSucceeded)
            {
                Block("SteamInitializationUnavailable");
                return;
            }

            if (appId != SpacewarAchievementSmokePolicy.AppId)
            {
                Block("UnexpectedAppId");
                return;
            }

            if (!steamIdentityValid)
            {
                Block("InvalidSteamId");
                return;
            }

            if (!loggedOn)
            {
                Block("LoggedOff");
                return;
            }

            if (achievementApi == null)
            {
                Block("AchievementCapabilityUnavailable");
                return;
            }

            try
            {
                phase = SteamAchievementSmokePhase.CallbackRegistration;
                achievementApi.RegisterAchievementStoreCallbacks(
                    ObserveStatsStored,
                    ObserveAchievementStored);
                callbacksRegistered = true;

                phase = SteamAchievementSmokePhase.Schema;
                achievementCount = achievementApi.GetNumAchievements();
                for (uint index = 0; index < achievementCount; index++)
                {
                    if (string.Equals(
                        achievementApi.GetAchievementName(index),
                        SpacewarAchievementSmokePolicy.TargetAchievement,
                        StringComparison.Ordinal))
                    {
                        targetAchievementFound = true;
                    }
                }

                if (!targetAchievementFound)
                {
                    Block("TargetMissing");
                    return;
                }

                phase = SteamAchievementSmokePhase.BeforeRead;
                beforeReadSucceeded = achievementApi.GetAchievement(
                    SpacewarAchievementSmokePolicy.TargetAchievement,
                    out targetBeforeUnlocked);
                if (!beforeReadSucceeded)
                {
                    FailReturnedFalse("BeforeReadReturnedFalse");
                    return;
                }

                if (targetBeforeUnlocked)
                {
                    Complete(
                        SteamAchievementSmokeOutcome.AlreadyUnlocked,
                        SteamAchievementSmokePhase.BeforeRead,
                        SteamAchievementSmokeFailureKind.None,
                        "TestPreconditionNotReset");
                    return;
                }

                phase = SteamAchievementSmokePhase.Set;
                mutationAttempted = true;
                setAchievementReturned = achievementApi.SetAchievement(
                    SpacewarAchievementSmokePolicy.TargetAchievement);
                if (!setAchievementReturned)
                {
                    FailReturnedFalse("SetAchievementReturnedFalse");
                    return;
                }

                phase = SteamAchievementSmokePhase.Store;
                storeStatsReturned = achievementApi.StoreStats();
                if (!storeStatsReturned)
                {
                    FailReturnedFalse("StoreStatsReturnedFalse");
                    return;
                }

                if (terminal)
                {
                    return;
                }

                callbackWaitStartedAt = monotonicSeconds();
                phase = SteamAchievementSmokePhase.CallbackWait;
            }
            catch (Exception exception)
            {
                Complete(
                    SteamAchievementSmokeOutcome.Failed,
                    phase,
                    SteamAchievementSmokeFailureKind.Exception,
                    exception.GetType().Name);
            }
        }

        internal void Tick()
        {
            if (terminal || shutdown || phase != SteamAchievementSmokePhase.CallbackWait)
            {
                return;
            }

            if (statsStoredResult == SteamCallbackResult.Ok &&
                targetAchievementStoredObserved &&
                fullUnlockObserved)
            {
                VerifyAfterStore();
                return;
            }

            if (monotonicSeconds() - callbackWaitStartedAt <
                SpacewarAchievementSmokePolicy.CallbackTimeoutSeconds)
            {
                return;
            }

            callbackTimedOut = true;
            Complete(
                SteamAchievementSmokeOutcome.Failed,
                SteamAchievementSmokePhase.CallbackWait,
                SteamAchievementSmokeFailureKind.Timeout,
                "CallbackTimeout");
        }

        internal void Shutdown()
        {
            if (shutdown)
            {
                return;
            }

            shutdown = true;
            if (!sessionStarted)
            {
                BeginUnavailableSessionForShutdown();
            }
            else if (!terminal)
            {
                Complete(
                    SteamAchievementSmokeOutcome.Failed,
                    phase,
                    SteamAchievementSmokeFailureKind.IncompleteAtShutdown,
                    "IncompleteAtShutdown");
            }

            DisposeCallbacks();
            EmitResultOnce();
        }

        private void ObserveStatsStored(SteamStatsStoredObservation observation)
        {
            if (terminal || observation.AppId != SpacewarAchievementSmokePolicy.AppId)
            {
                return;
            }

            statsStoredCallbackCount++;
            statsStoredResult = observation.Result;
            if (observation.Result != SteamCallbackResult.Ok)
            {
                Complete(
                    SteamAchievementSmokeOutcome.Failed,
                    SteamAchievementSmokePhase.CallbackWait,
                    SteamAchievementSmokeFailureKind.CallbackError,
                    "StatsStoredResultNotOk");
            }
        }

        private void ObserveAchievementStored(
            SteamAchievementStoredObservation observation)
        {
            if (terminal || observation.AppId != SpacewarAchievementSmokePolicy.AppId)
            {
                return;
            }

            achievementStoredCallbackCount++;
            if (!string.Equals(
                observation.AchievementName,
                SpacewarAchievementSmokePolicy.TargetAchievement,
                StringComparison.Ordinal))
            {
                return;
            }

            targetAchievementStoredObserved = true;
            fullUnlockObserved = observation.IsFullUnlock;
            if (!observation.IsFullUnlock)
            {
                Complete(
                    SteamAchievementSmokeOutcome.Failed,
                    SteamAchievementSmokePhase.CallbackWait,
                    SteamAchievementSmokeFailureKind.CallbackError,
                    "TargetStoredWasNotFullUnlock");
            }
        }

        private void VerifyAfterStore()
        {
            try
            {
                phase = SteamAchievementSmokePhase.AfterRead;
                afterReadSucceeded = achievementApi.GetAchievement(
                    SpacewarAchievementSmokePolicy.TargetAchievement,
                    out targetAfterUnlocked);
                if (!afterReadSucceeded || !targetAfterUnlocked)
                {
                    Complete(
                        SteamAchievementSmokeOutcome.Failed,
                        SteamAchievementSmokePhase.AfterRead,
                        SteamAchievementSmokeFailureKind.VerificationMismatch,
                        "AfterReadVerificationMismatch");
                    return;
                }

                Complete(
                    SteamAchievementSmokeOutcome.Succeeded,
                    SteamAchievementSmokePhase.AfterRead,
                    SteamAchievementSmokeFailureKind.None,
                    string.Empty);
            }
            catch (Exception exception)
            {
                Complete(
                    SteamAchievementSmokeOutcome.Failed,
                    SteamAchievementSmokePhase.AfterRead,
                    SteamAchievementSmokeFailureKind.Exception,
                    exception.GetType().Name);
            }
        }

        private void Block(string reason)
        {
            Complete(
                SteamAchievementSmokeOutcome.Blocked,
                phase,
                SteamAchievementSmokeFailureKind.Prerequisite,
                reason);
        }

        private void FailReturnedFalse(string reason)
        {
            Complete(
                SteamAchievementSmokeOutcome.Failed,
                phase,
                SteamAchievementSmokeFailureKind.ReturnedFalse,
                reason);
        }

        private void Complete(
            SteamAchievementSmokeOutcome outcome,
            SteamAchievementSmokePhase completedAt,
            SteamAchievementSmokeFailureKind kind,
            string reason)
        {
            if (terminal)
            {
                return;
            }

            terminal = true;
            terminalOutcome = outcome;
            terminalPhase = completedAt;
            failureKind = kind;
            blockedReason = reason ?? string.Empty;
            phase = SteamAchievementSmokePhase.Completed;
        }

        private void BeginUnavailableSessionForShutdown()
        {
            sessionStarted = true;
            if (!achievementSmokeRequested)
            {
                Complete(
                    SteamAchievementSmokeOutcome.NotRequested,
                    SteamAchievementSmokePhase.OptIn,
                    SteamAchievementSmokeFailureKind.None,
                    string.Empty);
                return;
            }

            if (!baseSmokeRequested)
            {
                Complete(
                    SteamAchievementSmokeOutcome.Blocked,
                    SteamAchievementSmokePhase.OptIn,
                    SteamAchievementSmokeFailureKind.Prerequisite,
                    "InvalidOptInCombination");
                return;
            }

            achievementOptInValid = true;
            Complete(
                SteamAchievementSmokeOutcome.Blocked,
                SteamAchievementSmokePhase.Session,
                SteamAchievementSmokeFailureKind.Prerequisite,
                "SteamInitializationUnavailable");
        }

        private void DisposeCallbacks()
        {
            if (!callbacksRegistered)
            {
                return;
            }

            callbacksRegistered = false;
            try
            {
                achievementApi.DisposeAchievementStoreCallbacks();
            }
            catch (Exception exception)
            {
                terminal = false;
                Complete(
                    SteamAchievementSmokeOutcome.Failed,
                    SteamAchievementSmokePhase.Completed,
                    SteamAchievementSmokeFailureKind.Exception,
                    exception.GetType().Name);
            }
        }

        private void EmitResultOnce()
        {
            if (!achievementSmokeRequested || resultEmitted)
            {
                return;
            }

            resultEmitted = true;
            var diagnostics = Diagnostics;
            var result = ResultPrefix + " {" +
                "\"achievementSmokeRequested\":true," +
                "\"achievementOptInValid\":" + Json(diagnostics.AchievementOptInValid) + "," +
                "\"observedAppId\":" + diagnostics.ObservedAppId + "," +
                "\"achievementCount\":" + diagnostics.AchievementCount + "," +
                "\"targetAchievementFound\":" + Json(diagnostics.TargetAchievementFound) + "," +
                "\"beforeReadSucceeded\":" + Json(diagnostics.BeforeReadSucceeded) + "," +
                "\"targetBeforeUnlocked\":" + Json(diagnostics.TargetBeforeUnlocked) + "," +
                "\"mutationAttempted\":" + Json(diagnostics.MutationAttempted) + "," +
                "\"setAchievementReturned\":" + Json(diagnostics.SetAchievementReturned) + "," +
                "\"storeStatsReturned\":" + Json(diagnostics.StoreStatsReturned) + "," +
                "\"statsStoredCallbackCount\":" + diagnostics.StatsStoredCallbackCount + "," +
                "\"statsStoredResult\":\"" + diagnostics.StatsStoredResult + "\"," +
                "\"achievementStoredCallbackCount\":" + diagnostics.AchievementStoredCallbackCount + "," +
                "\"targetAchievementStoredObserved\":" + Json(diagnostics.TargetAchievementStoredObserved) + "," +
                "\"fullUnlockObserved\":" + Json(diagnostics.FullUnlockObserved) + "," +
                "\"afterReadSucceeded\":" + Json(diagnostics.AfterReadSucceeded) + "," +
                "\"targetAfterUnlocked\":" + Json(diagnostics.TargetAfterUnlocked) + "," +
                "\"callbackTimedOut\":" + Json(diagnostics.CallbackTimedOut) + "," +
                "\"terminalOutcome\":\"" + diagnostics.TerminalOutcome + "\"," +
                "\"terminalPhase\":\"" + diagnostics.TerminalPhase + "\"," +
                "\"failureKind\":\"" + diagnostics.FailureKind + "\"," +
                "\"blockedReason\":\"" + diagnostics.BlockedReason + "\"" +
                "}";
            try
            {
                (logger ?? UnityEngine.Debug.Log)(result);
            }
            catch
            {
                // Smoke diagnostics must never interrupt owned Steam cleanup.
            }
        }

        private static string Json(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
