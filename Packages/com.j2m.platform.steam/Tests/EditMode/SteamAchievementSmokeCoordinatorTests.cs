using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Platform.Steam.Tests.EditMode
{
    public sealed class SteamAchievementSmokeCoordinatorTests
    {
        private double now;
        private FakeSteamAchievementApi api;
        private SteamAchievementSmokeCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            now = 100d;
            api = new FakeSteamAchievementApi();
            coordinator = CreateCoordinator(
                api,
                baseSmokeRequested: true,
                achievementSmokeRequested: true);
        }

        [TestCase(false, false, SteamAchievementSmokeOutcome.NotRequested)]
        [TestCase(true, false, SteamAchievementSmokeOutcome.NotRequested)]
        [TestCase(false, true, SteamAchievementSmokeOutcome.Blocked)]
        public void OptInMatrix_WithoutTripleOptIn_PerformsNoMutation(
            bool baseSmokeRequested,
            bool achievementSmokeRequested,
            SteamAchievementSmokeOutcome expectedOutcome)
        {
            coordinator = CreateCoordinator(
                api,
                baseSmokeRequested,
                achievementSmokeRequested);

            BeginValidSession();

            Assert.That(coordinator.Diagnostics.TerminalOutcome, Is.EqualTo(expectedOutcome));
            Assert.That(api.SetAchievementCount, Is.Zero);
            Assert.That(api.StoreStatsCount, Is.Zero);
            if (achievementSmokeRequested)
            {
                Assert.That(coordinator.Diagnostics.BlockedReason,
                    Is.EqualTo("InvalidOptInCombination"));
                Assert.That(coordinator.Diagnostics.AchievementOptInValid, Is.False);
            }
        }

        [TestCase(0u)]
        [TestCase(123456u)]
        public void NonSpacewarAppId_BlocksBeforeCapabilityUse(uint appId)
        {
            coordinator.BeginSession(true, appId, true, true);

            AssertBlockedWithoutMutation("UnexpectedAppId");
            Assert.That(api.RegistrationCount, Is.Zero);
            Assert.That(api.GetNumAchievementsCount, Is.Zero);
        }

        [TestCase(false, true, "InvalidSteamId")]
        [TestCase(true, false, "LoggedOff")]
        public void IdentityAndLoginGates_BlockBeforeMutation(
            bool steamIdValid,
            bool loggedOn,
            string reason)
        {
            coordinator.BeginSession(
                true,
                SpacewarAchievementSmokePolicy.AppId,
                steamIdValid,
                loggedOn);

            AssertBlockedWithoutMutation(reason);
            Assert.That(api.RegistrationCount, Is.Zero);
        }

        [Test]
        public void MissingOrCaseMismatchedTarget_BlocksBeforeMutation()
        {
            api.AchievementNames.Clear();
            api.AchievementNames.Add("ach_win_one_game");

            BeginValidSession();

            AssertBlockedWithoutMutation("TargetMissing");
            Assert.That(coordinator.Diagnostics.AchievementCount, Is.EqualTo(1));
            Assert.That(coordinator.Diagnostics.TargetAchievementFound, Is.False);
            Assert.That(api.GetAchievementNameCount, Is.EqualTo(1));
        }

        [Test]
        public void BeforeReadReturnedFalse_PerformsNoMutation()
        {
            api.BeforeReadResult = false;

            BeginValidSession();

            Assert.That(coordinator.Diagnostics.TerminalOutcome,
                Is.EqualTo(SteamAchievementSmokeOutcome.Failed));
            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.ReturnedFalse));
            AssertNoMutation();
        }

        [Test]
        public void AlreadyUnlocked_IsNonFailureAndPerformsNoMutation()
        {
            api.BeforeUnlocked = true;

            BeginValidSession();

            Assert.That(coordinator.Diagnostics.TerminalOutcome,
                Is.EqualTo(SteamAchievementSmokeOutcome.AlreadyUnlocked));
            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.None));
            Assert.That(coordinator.Diagnostics.BlockedReason,
                Is.EqualTo("TestPreconditionNotReset"));
            AssertNoMutation();
        }

        [Test]
        public void SetReturnedFalse_DoesNotStore()
        {
            api.SetResult = false;

            BeginValidSession();

            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.Zero);
            Assert.That(coordinator.Diagnostics.MutationAttempted, Is.True);
            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.ReturnedFalse));
        }

        [Test]
        public void StoreReturnedFalse_DoesNotRetryAndPreservesMutationAttempt()
        {
            api.StoreResult = false;

            BeginValidSession();
            coordinator.Tick();
            coordinator.Tick();

            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(coordinator.Diagnostics.MutationAttempted, Is.True);
            Assert.That(coordinator.Diagnostics.StoreStatsReturned, Is.False);
            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.ReturnedFalse));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RequiredCallbacks_InEitherOrder_ThenAfterRead_Succeeds(
            bool achievementFirst)
        {
            BeginValidSession();

            if (achievementFirst)
            {
                api.RaiseAchievementStored();
                api.RaiseStatsStored();
            }
            else
            {
                api.RaiseStatsStored();
                api.RaiseAchievementStored();
            }

            coordinator.Tick();

            Assert.That(coordinator.Diagnostics.TerminalOutcome,
                Is.EqualTo(SteamAchievementSmokeOutcome.Succeeded));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
            Assert.That(coordinator.Diagnostics.TargetAfterUnlocked, Is.True);
        }

        [Test]
        public void ForeignAndWrongAchievementCallbacks_AreNotSufficient()
        {
            BeginValidSession();
            api.RaiseStatsStored(appId: 999u);
            api.RaiseAchievementStored(appId: 999u);
            api.RaiseStatsStored();
            api.RaiseAchievementStored(achievementName: "ACH_TRAVEL_FAR_ACCUM");

            coordinator.Tick();

            Assert.That(coordinator.Diagnostics.TerminalOutcome,
                Is.EqualTo(default(SteamAchievementSmokeOutcome)));
            Assert.That(coordinator.Diagnostics.StatsStoredCallbackCount, Is.EqualTo(1));
            Assert.That(coordinator.Diagnostics.AchievementStoredCallbackCount, Is.EqualTo(1));
            Assert.That(coordinator.Diagnostics.TargetAchievementStoredObserved, Is.False);
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void StatsStoredError_FailsWithoutRetry()
        {
            BeginValidSession();
            api.RaiseStatsStored(result: SteamCallbackResult.Failure);

            coordinator.Tick();

            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.CallbackError));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [Test]
        public void TargetProgressCallback_IsNotAcceptedAsFullUnlock()
        {
            BeginValidSession();
            api.RaiseAchievementStored(fullUnlock: false);

            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.CallbackError));
            Assert.That(coordinator.Diagnostics.TargetAchievementStoredObserved, Is.True);
            Assert.That(coordinator.Diagnostics.FullUnlockObserved, Is.False);
        }

        [Test]
        public void CallbackWait_UsesThirtySecondMonotonicTimeoutWithoutRetry()
        {
            BeginValidSession();
            now += SpacewarAchievementSmokePolicy.CallbackTimeoutSeconds - 0.001d;
            coordinator.Tick();
            Assert.That(coordinator.Diagnostics.CallbackTimedOut, Is.False);

            now += 0.001d;
            coordinator.Tick();

            Assert.That(coordinator.Diagnostics.CallbackTimedOut, Is.True);
            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.Timeout));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void AfterReadMismatch_FailsVerification(
            bool readReturned,
            bool unlocked)
        {
            api.AfterReadResult = readReturned;
            api.AfterUnlocked = unlocked;
            BeginValidSession();
            api.RaiseStatsStored();
            api.RaiseAchievementStored();

            coordinator.Tick();

            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.VerificationMismatch));
            Assert.That(coordinator.Diagnostics.TerminalOutcome,
                Is.EqualTo(SteamAchievementSmokeOutcome.Failed));
        }

        [Test]
        public void ShutdownWhileWaiting_IsIncompleteAndDisposesCallbacksOnce()
        {
            var logs = new List<string>();
            coordinator = new SteamAchievementSmokeCoordinator(
                api,
                baseSmokeRequested: true,
                achievementSmokeRequested: true,
                () => now,
                logs.Add);
            BeginValidSession();

            coordinator.Shutdown();
            coordinator.Shutdown();

            Assert.That(coordinator.Diagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.IncompleteAtShutdown));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0], Does.StartWith(
                SteamAchievementSmokeCoordinator.ResultPrefix + " "));
            Assert.That(logs[0], Does.Not.Contain("steamId").IgnoreCase);
            Assert.That(logs[0], Does.Not.Contain("persona").IgnoreCase);
            Assert.That(logs[0], Does.Not.Contain("account").IgnoreCase);
        }

        private SteamAchievementSmokeCoordinator CreateCoordinator(
            ISteamAchievementApi achievementApi,
            bool baseSmokeRequested,
            bool achievementSmokeRequested)
        {
            return new SteamAchievementSmokeCoordinator(
                achievementApi,
                baseSmokeRequested,
                achievementSmokeRequested,
                () => now,
                logger: _ => { });
        }

        private void BeginValidSession()
        {
            coordinator.BeginSession(
                initializationSucceeded: true,
                SpacewarAchievementSmokePolicy.AppId,
                steamIdentityValid: true,
                loggedOn: true);
        }

        private void AssertBlockedWithoutMutation(string reason)
        {
            Assert.That(coordinator.Diagnostics.TerminalOutcome,
                Is.EqualTo(SteamAchievementSmokeOutcome.Blocked));
            Assert.That(coordinator.Diagnostics.BlockedReason, Is.EqualTo(reason));
            AssertNoMutation();
        }

        private void AssertNoMutation()
        {
            Assert.That(api.SetAchievementCount, Is.Zero);
            Assert.That(api.StoreStatsCount, Is.Zero);
            Assert.That(coordinator.Diagnostics.MutationAttempted, Is.False);
        }
    }
}
