using System;
using System.Collections.Generic;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using NUnit.Framework;

namespace Game.Platform.Steam.Tests.EditMode
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class SteamAchievementPublisherTests
    {
        private const uint SessionAppId = 4242;

        [TestCase(false, 4242u, true, true)]
        [TestCase(true, 0u, true, true)]
        [TestCase(true, 4242u, false, true)]
        [TestCase(true, 4242u, true, false)]
        public void UnavailableSession_ReturnsUnavailableWithoutSteamMutation(
            bool initialized,
            uint appId,
            bool steamIdValid,
            bool loggedOn)
        {
            var api = ProductApi();
            using var publisher = CreatePublisher(api, out _);
            Assert.That(
                publisher.BeginSession(new SteamAchievementSessionPrerequisites(
                    initialized,
                    appId,
                    steamIdValid,
                    loggedOn)),
                Is.False);

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            AssertNoMutation(api);
            Assert.That(api.RegistrationCount, Is.Zero);
        }

        [Test]
        public void UnknownMapping_ReturnsRejectedWithoutSteamApiCalls()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementId.Require("future.valid"));

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Rejected }));
            Assert.That(api.GetNumAchievementsCount, Is.Zero);
            Assert.That(api.GetAchievementCount, Is.Zero);
            AssertNoMutation(api);
        }

        [TestCase("app-id")]
        [TestCase("steam-id")]
        [TestCase("logged-on")]
        public void ReadinessLostAfterAttach_ReturnsUnavailableBeforeAchievementApi(
            string lostPrerequisite)
        {
            var lifecycle = ProductLifecycle();
            var api = ProductApi();
            using var publisher = ReadyPublisher(lifecycle, api, out _);
            switch (lostPrerequisite)
            {
                case "app-id":
                    lifecycle.AppId = 0;
                    break;
                case "steam-id":
                    lifecycle.SteamIdValid = false;
                    break;
                case "logged-on":
                    lifecycle.LoggedOn = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(lostPrerequisite));
            }

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.GetNumAchievementsCount, Is.Zero);
            AssertNoMutation(api);
        }

        [TestCase("OTHER_ACHIEVEMENT")]
        [TestCase("vq_campaign_complete")]
        public void ExactSchemaTargetMissing_ReturnsRejectedWithoutMutation(string schemaName)
        {
            var api = ProductApi(schemaName);
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Rejected }));
            AssertNoMutation(api);
        }

        [Test]
        public void MissingSchema_IsCachedForTheSteamSession()
        {
            var api = ProductApi("OTHER_ACHIEVEMENT");
            using var publisher = ReadyPublisher(api, out _);

            Assert.That(
                Publish(publisher, GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(new[] { AchievementPublicationResult.Rejected }));
            api.AchievementNames.Clear();
            api.AchievementNames.Add(ExpectedName());
            Assert.That(
                Publish(publisher, GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(new[] { AchievementPublicationResult.Rejected }));

            Assert.That(api.GetNumAchievementsCount, Is.EqualTo(1));
            Assert.That(api.GetAchievementNameCount, Is.EqualTo(1));
            AssertNoMutation(api);
        }

        [Test]
        public void PreReadAlreadyUnlocked_ReturnsAlreadySatisfiedWithoutMutation()
        {
            var api = ProductApi();
            api.BeforeUnlocked = true;
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.AlreadySatisfied }));
            AssertNoMutation(api);
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void PreReadFailure_ReturnsFailedWithoutMutation()
        {
            var api = ProductApi();
            api.BeforeReadResult = false;
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            AssertNoMutation(api);
        }

        [Test]
        public void SetFalse_ReturnsFailedWithoutStore()
        {
            var api = ProductApi();
            api.SetResult = false;
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.Zero);
        }

        [Test]
        public void StoreFalse_ReturnsFailedWithoutRetry()
        {
            var api = ProductApi();
            api.StoreResult = false;
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RequiredCallbacks_InEitherOrder_PostReadAndAcceptExactlyOnce(
            bool statsFirst)
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            RaiseSuccessCallbacks(api, statsFirst);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [Test]
        public void ForeignWrongAndPartialCallbacks_DoNotCompleteTarget()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out var clock);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            api.RaiseStatsStored(SessionAppId + 1);
            api.RaiseAchievementStored(SessionAppId, "OTHER_ACHIEVEMENT");
            api.RaiseAchievementStored(SessionAppId, ExpectedName(), fullUnlock: false);
            Assert.That(results, Is.Empty);

            clock.Seconds = SteamAchievementPublisher.CallbackTimeoutSeconds;
            publisher.Tick();
            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void StatsStoredError_QuarantinesSessionAndDoesNotStartQueuedOperations()
        {
            var api = ProductApi();
            var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.NormalCampaignComplete);
            var second = Publish(publisher, GameAchievementIds.CampaignStage1_2Clear);
            var third = Publish(publisher, GameAchievementIds.CampaignStage1_2PushFlipWithin25);

            api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(third, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));

            api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure);
            api.RaiseAchievementStored(SessionAppId, ExpectedName());
            var afterFailure = Publish(
                publisher,
                GameAchievementIds.CampaignStage1_2Clear);
            publisher.Dispose();
            publisher.Dispose();

            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(second, Has.Count.EqualTo(1));
            Assert.That(third, Has.Count.EqualTo(1));
            Assert.That(afterFailure, Is.EqualTo(new[]
            {
                AchievementPublicationResult.Unavailable,
            }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [Test]
        public void StatsStoredError_CleanupAndCompletionExceptionsRemainContained()
        {
            var api = ProductApi();
            api.DisposalException = new InvalidOperationException("callback cleanup failed");
            var publisher = ReadyPublisher(api, out _);
            var activeCompletionCount = 0;
            var queued = new List<AchievementPublicationResult>();
            publisher.Publish(
                GameAchievementIds.NormalCampaignComplete,
                _ =>
                {
                    activeCompletionCount++;
                    throw new InvalidOperationException("completion failed");
                });
            publisher.Publish(
                GameAchievementIds.CampaignStage1_2Clear,
                queued.Add);

            Assert.DoesNotThrow(() =>
                api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure));

            Assert.That(activeCompletionCount, Is.EqualTo(1));
            Assert.That(queued, Is.EqualTo(new[]
            {
                AchievementPublicationResult.Unavailable,
            }));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignStage1_2PushFlipWithin25),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            publisher.Dispose();
        }

        [Test]
        public void Timeout_IsBoundedAtThirtySecondsWithoutRetry()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out var clock);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            clock.Seconds = SteamAchievementPublisher.CallbackTimeoutSeconds - 0.001d;
            publisher.Tick();
            Assert.That(results, Is.Empty);

            clock.Seconds = SteamAchievementPublisher.CallbackTimeoutSeconds;
            publisher.Tick();
            publisher.Tick();

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [Test]
        public void Timeout_QuarantinesSessionAndCompletesQueuedOperationsUnavailable()
        {
            var api = ProductApi();
            var publisher = ReadyPublisher(api, out var clock);
            var first = Publish(publisher, GameAchievementIds.NormalCampaignComplete);
            var second = Publish(publisher, GameAchievementIds.CampaignStage1_2Clear);
            var third = Publish(publisher, GameAchievementIds.CampaignStage1_2PushFlipWithin25);

            clock.Seconds = SteamAchievementPublisher.CallbackTimeoutSeconds;
            publisher.Tick();

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(third, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));

            api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure);
            api.RaiseAchievementStored(SessionAppId, ExpectedName());
            var afterTimeout = Publish(
                publisher,
                GameAchievementIds.CampaignStage1_2Clear);
            publisher.Dispose();
            publisher.Dispose();

            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(second, Has.Count.EqualTo(1));
            Assert.That(third, Has.Count.EqualTo(1));
            Assert.That(afterTimeout, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [Test]
        public void Timeout_CompletionReentryCannotRestartQuarantinedSession()
        {
            var api = ProductApi();
            var publisher = ReadyPublisher(api, out var clock);
            var first = new List<AchievementPublicationResult>();
            var reentrant = new List<AchievementPublicationResult>();
            publisher.Publish(
                GameAchievementIds.NormalCampaignComplete,
                result =>
                {
                    first.Add(result);
                    publisher.Publish(
                        GameAchievementIds.CampaignStage1_2Clear,
                        reentrant.Add);
                });

            clock.Seconds = SteamAchievementPublisher.CallbackTimeoutSeconds;
            publisher.Tick();
            publisher.Dispose();

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(reentrant, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [Test]
        public void SingleFlight_QueuesSecondOperationInFifoOrder()
        {
            var api = ProductApi();
            api.ReadResultOverrides.Enqueue(true);
            api.UnlockedOverrides.Enqueue(false);
            api.ReadResultOverrides.Enqueue(true);
            api.UnlockedOverrides.Enqueue(true);
            api.ReadResultOverrides.Enqueue(true);
            api.UnlockedOverrides.Enqueue(false);
            api.ReadResultOverrides.Enqueue(true);
            api.UnlockedOverrides.Enqueue(true);
            using var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.NormalCampaignComplete);
            var second = Publish(publisher, GameAchievementIds.CampaignStage1_2Clear);

            Assert.That(first, Is.Empty);
            Assert.That(second, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.NormalCampaignComplete,
                statsFirst: true);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
            Assert.That(second, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(api.StoreStatsCount, Is.EqualTo(2));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignStage1_2Clear,
                statsFirst: false);

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
        }

        [Test]
        public void SuccessfulCompletionReentry_PreservesReservedFifoOrder()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 3);
            using var publisher = ReadyPublisher(api, out _);
            var first = new List<AchievementPublicationResult>();
            var second = new List<AchievementPublicationResult>();
            var third = new List<AchievementPublicationResult>();
            publisher.Publish(
                GameAchievementIds.NormalCampaignComplete,
                result =>
                {
                    first.Add(result);
                    publisher.Publish(
                        GameAchievementIds.CampaignStage1_2PushFlipWithin25,
                        third.Add);
                });
            publisher.Publish(
                GameAchievementIds.CampaignStage1_2Clear,
                second.Add);

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.NormalCampaignComplete,
                statsFirst: true);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
            Assert.That(second, Is.Empty);
            Assert.That(third, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(
                api.RequestedAchievementNames[api.RequestedAchievementNames.Count - 1],
                Is.EqualTo(ExpectedName(GameAchievementIds.CampaignStage1_2Clear)));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignStage1_2Clear,
                statsFirst: false);

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
            Assert.That(third, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(3));
            Assert.That(
                api.RequestedAchievementNames[api.RequestedAchievementNames.Count - 1],
                Is.EqualTo(ExpectedName(
                    GameAchievementIds.CampaignStage1_2PushFlipWithin25)));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignStage1_2PushFlipWithin25,
                statsFirst: true);

            Assert.That(third, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
        }

        [Test]
        public void CorrelatedPostReadFailure_StillStartsNextQueuedOperation()
        {
            var api = ProductApi();
            QueuePublicationReads(api, readSucceeded: true, unlocked: false);
            QueuePublicationReads(api, readSucceeded: true, unlocked: false);
            QueueSuccessfulPublicationReads(api, publicationCount: 1);
            using var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.NormalCampaignComplete);
            var second = Publish(publisher, GameAchievementIds.CampaignStage1_2Clear);

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.NormalCampaignComplete,
                statsFirst: true);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(second, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(api.StoreStatsCount, Is.EqualTo(2));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignStage1_2Clear,
                statsFirst: false);

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
        }

        [Test]
        public void DuplicateCallbacks_CompleteAndPostReadOnce()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            api.RaiseStatsStored(SessionAppId);
            api.RaiseStatsStored(SessionAppId);
            api.RaiseAchievementStored(SessionAppId, ExpectedName());
            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Accepted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
        }

        [Test]
        public void PostReadMismatch_ReturnsFailed()
        {
            var api = ProductApi();
            api.AfterUnlocked = false;
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            RaiseSuccessCallbacks(api, statsFirst: true);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
        }

        [Test]
        public void PostReadReturnedFalse_ReturnsFailed()
        {
            var api = ProductApi();
            api.AfterReadResult = false;
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            RaiseSuccessCallbacks(api, statsFirst: true);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
        }

        [Test]
        public void PostReadException_IsContainedAsFailed()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);
            api.GetAchievementException = new InvalidOperationException("post-read");

            RaiseSuccessCallbacks(api, statsFirst: false);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
        }

        [Test]
        public void DisposeInFlight_CompletesUnavailableDisposesCallbacksAndIgnoresLateCallbacks()
        {
            var api = ProductApi();
            var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            publisher.Dispose();
            publisher.Dispose();
            api.RaiseStatsStored(SessionAppId);
            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void DisposeWithQueuedOperations_CompletesAllUnavailableWithoutStartingQueue()
        {
            var api = ProductApi();
            var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.NormalCampaignComplete);
            var second = Publish(publisher, GameAchievementIds.CampaignStage1_2Clear);

            publisher.Dispose();

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [TestCase(nameof(FakeSteamAchievementApi.GetNumAchievementsException))]
        [TestCase(nameof(FakeSteamAchievementApi.GetAchievementNameException))]
        [TestCase(nameof(FakeSteamAchievementApi.GetAchievementException))]
        [TestCase(nameof(FakeSteamAchievementApi.SetAchievementException))]
        [TestCase(nameof(FakeSteamAchievementApi.StoreStatsException))]
        public void SteamApiException_IsContainedAsFailed(string exceptionProperty)
        {
            var api = ProductApi();
            SetException(api, exceptionProperty);
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.NormalCampaignComplete);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
        }

        private static SteamAchievementPublisher ReadyPublisher(
            FakeSteamAchievementApi api,
            out FakeClock clock)
        {
            return ReadyPublisher(ProductLifecycle(), api, out clock);
        }

        private static SteamAchievementPublisher ReadyPublisher(
            FakeSteamNativeApi lifecycle,
            FakeSteamAchievementApi api,
            out FakeClock clock)
        {
            var publisher = CreatePublisher(lifecycle, api, out clock);
            Assert.That(
                publisher.BeginSession(new SteamAchievementSessionPrerequisites(
                    initializationSucceeded: true,
                    observedAppId: SessionAppId,
                    steamIdValid: true,
                    loggedOn: true)),
                Is.True);
            return publisher;
        }

        private static SteamAchievementPublisher CreatePublisher(
            FakeSteamAchievementApi api,
            out FakeClock clock)
        {
            return CreatePublisher(ProductLifecycle(), api, out clock);
        }

        private static SteamAchievementPublisher CreatePublisher(
            FakeSteamNativeApi lifecycle,
            FakeSteamAchievementApi api,
            out FakeClock clock)
        {
            var localClock = new FakeClock();
            clock = localClock;
            return new SteamAchievementPublisher(
                lifecycle,
                api,
                SteamAchievementMapping.Production,
                () => localClock.Seconds);
        }

        private static FakeSteamAchievementApi ProductApi(string schemaName = null)
        {
            var api = new FakeSteamAchievementApi();
            api.AchievementNames.Clear();
            if (schemaName != null)
            {
                api.AchievementNames.Add(schemaName);
                return api;
            }

            for (var i = 0; i < GameAchievementCatalog.Production.Definitions.Count; i++)
            {
                var id = GameAchievementCatalog.Production.Definitions[i].Id;
                api.AchievementNames.Add(ExpectedName(id));
            }

            return api;
        }

        private static FakeSteamNativeApi ProductLifecycle()
        {
            return new FakeSteamNativeApi { AppId = SessionAppId };
        }

        private static string ExpectedName()
        {
            return ExpectedName(GameAchievementIds.NormalCampaignComplete);
        }

        private static string ExpectedName(GameAchievementId achievementId)
        {
            SteamAchievementMapping.Production.TryGetExpectedSteamApiName(
                achievementId,
                out var expectedName);
            return expectedName.Value;
        }

        private static List<AchievementPublicationResult> Publish(
            SteamAchievementPublisher publisher,
            GameAchievementId achievementId)
        {
            var results = new List<AchievementPublicationResult>();
            publisher.Publish(achievementId, results.Add);
            return results;
        }

        private static void QueueSuccessfulPublicationReads(
            FakeSteamAchievementApi api,
            int publicationCount)
        {
            for (var i = 0; i < publicationCount; i++)
            {
                QueuePublicationReads(api, readSucceeded: true, unlocked: false);
                QueuePublicationReads(api, readSucceeded: true, unlocked: true);
            }
        }

        private static void QueuePublicationReads(
            FakeSteamAchievementApi api,
            bool readSucceeded,
            bool unlocked)
        {
            api.ReadResultOverrides.Enqueue(readSucceeded);
            api.UnlockedOverrides.Enqueue(unlocked);
        }

        private static void RaiseSuccessCallbacks(
            FakeSteamAchievementApi api,
            bool statsFirst)
        {
            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.NormalCampaignComplete,
                statsFirst);
        }

        private static void RaiseSuccessCallbacks(
            FakeSteamAchievementApi api,
            GameAchievementId achievementId,
            bool statsFirst)
        {
            var expectedName = ExpectedName(achievementId);
            if (statsFirst)
            {
                api.RaiseStatsStored(SessionAppId);
                api.RaiseAchievementStored(SessionAppId, expectedName);
            }
            else
            {
                api.RaiseAchievementStored(SessionAppId, expectedName);
                api.RaiseStatsStored(SessionAppId);
            }
        }

        private static void AssertNoMutation(FakeSteamAchievementApi api)
        {
            Assert.That(api.SetAchievementCount, Is.Zero);
            Assert.That(api.StoreStatsCount, Is.Zero);
        }

        private static void SetException(
            FakeSteamAchievementApi api,
            string exceptionProperty)
        {
            var exception = new InvalidOperationException("contained");
            switch (exceptionProperty)
            {
                case nameof(FakeSteamAchievementApi.GetNumAchievementsException):
                    api.GetNumAchievementsException = exception;
                    break;
                case nameof(FakeSteamAchievementApi.GetAchievementException):
                    api.GetAchievementException = exception;
                    break;
                case nameof(FakeSteamAchievementApi.GetAchievementNameException):
                    api.GetAchievementNameException = exception;
                    break;
                case nameof(FakeSteamAchievementApi.SetAchievementException):
                    api.SetAchievementException = exception;
                    break;
                case nameof(FakeSteamAchievementApi.StoreStatsException):
                    api.StoreStatsException = exception;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(exceptionProperty));
            }
        }

        private sealed class FakeClock
        {
            internal double Seconds { get; set; }
        }
    }
}
