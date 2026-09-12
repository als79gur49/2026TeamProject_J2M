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

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            AssertNoMutation(api);
            Assert.That(api.RegistrationCount, Is.Zero);
        }

        [Test]
        public void FailedSecondRegistration_DoesNotReleaseFirstPublisherCallbacks()
        {
            var lifecycle = ProductLifecycle();
            var api = ProductApi();
            using var owner = ReadyPublisher(lifecycle, api, out _);
            using var contender = CreatePublisher(lifecycle, api, out _);

            Assert.That(
                contender.BeginSession(new SteamAchievementSessionPrerequisites(
                    initializationSucceeded: true,
                    observedAppId: SessionAppId,
                    steamIdValid: true,
                    loggedOn: true)),
                Is.False);
            Assert.That(api.RegistrationCount, Is.EqualTo(2));
            Assert.That(api.DisposalCount, Is.Zero);

            var results = Publish(
                owner,
                GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));

            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(
                results,
                Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
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

        [Test]
        public void SuccessfulBatch_ChecksReadinessExactlyOnceAtMutationAdmission()
        {
            var lifecycle = ProductLifecycle();
            var api = ProductApi();
            using var publisher = ReadyPublisher(lifecycle, api, out _);

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(lifecycle.AppIdCount, Is.EqualTo(1));
            Assert.That(lifecycle.IdentityCount, Is.EqualTo(1));
            Assert.That(lifecycle.LoggedOnCount, Is.EqualTo(1));

            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(lifecycle.AppIdCount, Is.EqualTo(1));
            Assert.That(lifecycle.IdentityCount, Is.EqualTo(1));
            Assert.That(lifecycle.LoggedOnCount, Is.EqualTo(1));
        }

        [Test]
        public void EachStartedQueuedBatch_ChecksReadinessExactlyOnce()
        {
            var lifecycle = ProductLifecycle();
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 2);
            using var publisher = ReadyPublisher(lifecycle, api, out _);

            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);

            Assert.That(second, Is.Empty);
            Assert.That(lifecycle.AppIdCount, Is.EqualTo(1));
            Assert.That(lifecycle.IdentityCount, Is.EqualTo(1));
            Assert.That(lifecycle.LoggedOnCount, Is.EqualTo(1));

            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(second, Is.Empty);
            Assert.That(lifecycle.AppIdCount, Is.EqualTo(2));
            Assert.That(lifecycle.IdentityCount, Is.EqualTo(2));
            Assert.That(lifecycle.LoggedOnCount, Is.EqualTo(2));

            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel1Clear));

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(lifecycle.AppIdCount, Is.EqualTo(2));
            Assert.That(lifecycle.IdentityCount, Is.EqualTo(2));
            Assert.That(lifecycle.LoggedOnCount, Is.EqualTo(2));
        }

        [Test]
        public void ChangedNonZeroAppId_QuarantinesBeforeAchievementApi()
        {
            var lifecycle = ProductLifecycle();
            var api = ProductApi();
            using var publisher = ReadyPublisher(lifecycle, api, out _);
            lifecycle.AppId = SessionAppId + 1;

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(lifecycle.AppIdCount, Is.EqualTo(1));
            Assert.That(lifecycle.IdentityCount, Is.Zero);
            Assert.That(lifecycle.LoggedOnCount, Is.Zero);
            Assert.That(api.GetNumAchievementsCount, Is.Zero);
            AssertNoMutation(api);
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel1Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
        }

        [TestCase("app-id")]
        [TestCase("steam-id")]
        [TestCase("logged-on")]
        public void ReadinessLostAfterAttach_QuarantinesBeforeAchievementApi(
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

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.GetNumAchievementsCount, Is.Zero);
            AssertNoMutation(api);
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel1Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
        }

        [TestCase("app-id")]
        [TestCase("steam-id")]
        [TestCase("logged-on")]
        public void ReadinessException_QuarantinesAsFailed(
            string throwingPrerequisite)
        {
            var lifecycle = ProductLifecycle();
            var api = ProductApi();
            using var publisher = ReadyPublisher(lifecycle, api, out _);
            SetReadinessException(lifecycle, throwingPrerequisite);

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.GetNumAchievementsCount, Is.Zero);
            AssertNoMutation(api);
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel1Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
        }

        [Test]
        public void QueuedReadinessLoss_UsesSameQuarantinePolicyAsImmediateBatch()
        {
            var lifecycle = ProductLifecycle();
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 2);
            using var publisher = ReadyPublisher(lifecycle, api, out _);
            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);
            lifecycle.LoggedOn = false;

            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [TestCase("OTHER_ACHIEVEMENT")]
        [TestCase("vq_level_4_clear")]
        public void ExactSchemaTargetMissing_ReturnsRejectedWithoutMutation(string schemaName)
        {
            var api = ProductApi(schemaName);
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Rejected }));
            AssertNoMutation(api);
        }

        [Test]
        public void MissingSchema_IsCachedForTheSteamSession()
        {
            var api = ProductApi("OTHER_ACHIEVEMENT");
            using var publisher = ReadyPublisher(api, out _);

            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel4Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Rejected }));
            api.AchievementNames.Clear();
            api.AchievementNames.Add(ExpectedName());
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel4Clear),
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

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

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

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            AssertNoMutation(api);
        }

        [Test]
        public void SetFalse_ReturnsFailedWithoutStore()
        {
            var api = ProductApi();
            api.SetResult = false;
            using var publisher = ReadyPublisher(api, out _);

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

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

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NamedCallback_SubmitsExactlyOnceRegardlessOfStatsOrder(
            bool statsFirst)
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            RaiseSuccessCallbacks(api, statsFirst);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [Test]
        public void Batch_SetsAllCandidatesStoresOnceAndWaitsForEveryExactNamedCallback()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 2);
            using var publisher = ReadyPublisher(api, out _);
            AchievementPublicationBatchResult result = null;

            publisher.PublishBatch(
                new AchievementPublicationBatch(new[]
                {
                    GameAchievementIds.CampaignLevel1Clear,
                    GameAchievementIds.CampaignLevel2Clear,
                }),
                observed => result = observed);

            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(result, Is.Null);

            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel1Clear));
            api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure);
            Assert.That(result, Is.Null);

            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel2Clear));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items, Has.Count.EqualTo(2));
            Assert.That(result.Items[0].Result, Is.EqualTo(AchievementPublicationResult.Submitted));
            Assert.That(result.Items[1].Result, Is.EqualTo(AchievementPublicationResult.Submitted));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [Test]
        public void BatchTimeout_PreservesNamedSuccessFailsMissingItemAndQuarantinesSession()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 2);
            var publisher = ReadyPublisher(api, out var clock);
            AchievementPublicationBatchResult result = null;
            publisher.PublishBatch(
                new AchievementPublicationBatch(new[]
                {
                    GameAchievementIds.CampaignLevel1Clear,
                    GameAchievementIds.CampaignLevel2Clear,
                }),
                observed => result = observed);

            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel1Clear));
            clock.Seconds = SteamAchievementPublisher.CallbackTimeoutSeconds;
            publisher.Tick();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items[0].Result, Is.EqualTo(AchievementPublicationResult.Submitted));
            Assert.That(result.Items[1].Result, Is.EqualTo(AchievementPublicationResult.Failed));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel4Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            publisher.Dispose();
        }

        [Test]
        public void StatsStoredOkAlone_DoesNotCompleteAndEventuallyTimesOut()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out var clock);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            api.RaiseStatsStored(SessionAppId);

            Assert.That(results, Is.Empty);
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));

            clock.Seconds = SteamAchievementPublisher.CallbackTimeoutSeconds;
            publisher.Tick();

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [Test]
        public void DelayedPriorSuccessCallbacks_CannotCompleteNextNamedOperation()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 2);
            using var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);

            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(second, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));

            api.RaiseStatsStored(SessionAppId);
            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(second, Is.Empty);
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));

            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel1Clear));

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
        }

        [Test]
        public void DelayedPriorStatsError_DoesNotContaminateNamedOperations()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 3);
            var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);
            var third = Publish(
                publisher,
                GameAchievementIds.CampaignLevel2Clear);

            api.RaiseAchievementStored(SessionAppId, ExpectedName());
            api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(second, Is.Empty);
            Assert.That(third, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(api.StoreStatsCount, Is.EqualTo(2));

            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel1Clear));
            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel2Clear));

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(third, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.DisposalCount, Is.Zero);
            publisher.Dispose();
        }

        [Test]
        public void ForeignWrongAndPartialCallbacks_DoNotCompleteTarget()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out var clock);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

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
        public void StatsStoredError_DoesNotCompleteOrStopQueuedNamedOperations()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 3);
            var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);
            var third = Publish(publisher, GameAchievementIds.CampaignLevel2Clear);

            api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure);

            Assert.That(first, Is.Empty);
            Assert.That(second, Is.Empty);
            Assert.That(third, Is.Empty);
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            api.RaiseAchievementStored(SessionAppId, ExpectedName());
            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel1Clear));
            api.RaiseAchievementStored(
                SessionAppId,
                ExpectedName(GameAchievementIds.CampaignLevel2Clear));

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(third, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(3));
            Assert.That(api.SetAchievementCount, Is.EqualTo(3));
            Assert.That(api.StoreStatsCount, Is.EqualTo(3));
            Assert.That(api.DisposalCount, Is.Zero);
            publisher.Dispose();
        }

        [Test]
        public void StatsStoredError_DoesNotCompleteAndDisposeExceptionsRemainContained()
        {
            var api = ProductApi();
            api.DisposalException = new InvalidOperationException("callback cleanup failed");
            var publisher = ReadyPublisher(api, out _);
            var activeCompletionCount = 0;
            var queued = new List<AchievementPublicationResult>();
            Publish(
                publisher,
                GameAchievementIds.CampaignLevel4Clear,
                _ =>
                {
                    activeCompletionCount++;
                    throw new InvalidOperationException("completion failed");
                });
            Publish(
                publisher,
                GameAchievementIds.CampaignLevel1Clear,
                queued.Add);

            Assert.DoesNotThrow(() =>
                api.RaiseStatsStored(SessionAppId, SteamCallbackResult.Failure));

            Assert.That(activeCompletionCount, Is.Zero);
            Assert.That(queued, Is.Empty);
            Assert.That(api.DisposalCount, Is.Zero);
            Assert.DoesNotThrow(() => publisher.Dispose());
            Assert.That(activeCompletionCount, Is.EqualTo(1));
            Assert.That(queued, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [Test]
        public void Timeout_IsBoundedAtThirtySecondsWithoutRetry()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out var clock);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

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
            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);
            var third = Publish(publisher, GameAchievementIds.CampaignLevel2Clear);

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
                GameAchievementIds.CampaignLevel1Clear);
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
            Publish(
                publisher,
                GameAchievementIds.CampaignLevel4Clear,
                result =>
                {
                    first.Add(result);
                    Publish(
                        publisher,
                        GameAchievementIds.CampaignLevel1Clear,
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
            api.UnlockedOverrides.Enqueue(false);
            using var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);

            Assert.That(first, Is.Empty);
            Assert.That(second, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignLevel4Clear,
                statsFirst: true);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(second, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(api.StoreStatsCount, Is.EqualTo(2));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignLevel1Clear,
                statsFirst: false);

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
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
            Publish(
                publisher,
                GameAchievementIds.CampaignLevel4Clear,
                result =>
                {
                    first.Add(result);
                    Publish(
                        publisher,
                        GameAchievementIds.CampaignLevel2Clear,
                        third.Add);
                });
            Publish(
                publisher,
                GameAchievementIds.CampaignLevel1Clear,
                second.Add);

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignLevel4Clear,
                statsFirst: true);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(second, Is.Empty);
            Assert.That(third, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(
                api.RequestedAchievementNames[api.RequestedAchievementNames.Count - 1],
                Is.EqualTo(ExpectedName(GameAchievementIds.CampaignLevel1Clear)));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignLevel1Clear,
                statsFirst: false);

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(third, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(3));
            Assert.That(
                api.RequestedAchievementNames[api.RequestedAchievementNames.Count - 1],
                Is.EqualTo(ExpectedName(
                    GameAchievementIds.CampaignLevel2Clear)));

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignLevel2Clear,
                statsFirst: true);

            Assert.That(third, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
        }

        [Test]
        public void StoreFalse_CompletesFailedAndStillStartsNextQueuedOperation()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 2);
            api.StoreResultOverrides.Enqueue(false);
            api.StoreResultOverrides.Enqueue(true);
            using var publisher = ReadyPublisher(api, out _);
            var first = new List<AchievementPublicationResult>();
            var second = new List<AchievementPublicationResult>();
            api.StoreStatsAction = () =>
            {
                api.StoreStatsAction = null;
                Publish(
                    publisher,
                    GameAchievementIds.CampaignLevel1Clear,
                    second.Add);
            };

            Publish(
                publisher,
                GameAchievementIds.CampaignLevel4Clear,
                first.Add);

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Failed }));
            Assert.That(second, Is.Empty);
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(api.StoreStatsCount, Is.EqualTo(2));
            Assert.That(api.DisposalCount, Is.Zero);

            RaiseSuccessCallbacks(
                api,
                GameAchievementIds.CampaignLevel1Clear,
                statsFirst: false);

            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
        }

        [Test]
        public void DuplicateCallbacks_CompleteExactlyOnce()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            api.RaiseStatsStored(SessionAppId);
            api.RaiseStatsStored(SessionAppId);
            api.RaiseAchievementStored(SessionAppId, ExpectedName());
            api.RaiseAchievementStored(SessionAppId, ExpectedName());

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void NamedCallback_DoesNotRequirePostReadConfirmation()
        {
            var api = ProductApi();
            api.AfterUnlocked = false;
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            RaiseSuccessCallbacks(api, statsFirst: true);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void NamedCallback_DoesNotPerformSecondRead()
        {
            var api = ProductApi();
            api.AfterReadResult = false;
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            RaiseSuccessCallbacks(api, statsFirst: true);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void NamedCallback_IgnoresUnusedPostReadException()
        {
            var api = ProductApi();
            using var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            api.GetAchievementException = new InvalidOperationException("post-read");

            RaiseSuccessCallbacks(api, statsFirst: false);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
        }

        [Test]
        public void DisposeInFlight_CompletesUniformUnavailableAndIgnoresCapturedLateCallbacks()
        {
            var api = ProductApi();
            var publisher = ReadyPublisher(api, out _);
            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

            publisher.Dispose();
            publisher.Dispose();
            api.RaiseStatsStored(SessionAppId);
            api.RaiseAchievementStored(SessionAppId, ExpectedName());
            api.RaiseCapturedStatsStored(SessionAppId);
            api.RaiseCapturedAchievementStored(SessionAppId, ExpectedName());
            api.RaiseCapturedStatsStored(SessionAppId);
            api.RaiseCapturedAchievementStored(SessionAppId, ExpectedName());

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(api.GetAchievementCount, Is.EqualTo(1));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel1Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
        }

        [Test]
        public void DisposeWithQueuedOperations_CompletesAllUnavailableWithoutStartingQueue()
        {
            var api = ProductApi();
            var publisher = ReadyPublisher(api, out _);
            var first = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);
            var second = Publish(publisher, GameAchievementIds.CampaignLevel1Clear);

            publisher.Dispose();

            Assert.That(first, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(second, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
        }

        [Test]
        public void DisposePartialBatch_OverwritesResolvedAndUnresolvedItemsAsUnavailable()
        {
            var api = ProductApi();
            QueuePublicationReads(api, readSucceeded: true, unlocked: true);
            QueuePublicationReads(api, readSucceeded: true, unlocked: false);
            var publisher = ReadyPublisher(api, out _);
            AchievementPublicationBatchResult result = null;
            publisher.PublishBatch(
                new AchievementPublicationBatch(new[]
                {
                    GameAchievementIds.CampaignLevel1Clear,
                    GameAchievementIds.CampaignLevel2Clear,
                }),
                observed => result = observed);

            Assert.That(result, Is.Null);
            Assert.That(api.SetAchievementCount, Is.EqualTo(1));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));

            publisher.Dispose();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items, Has.Count.EqualTo(2));
            Assert.That(result.Items[0].Result, Is.EqualTo(AchievementPublicationResult.Unavailable));
            Assert.That(result.Items[1].Result, Is.EqualTo(AchievementPublicationResult.Unavailable));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
        }

        [Test]
        public void AchievementApiException_PreservesKnownItemAndFailsUnresolvedItem()
        {
            var api = ProductApi();
            QueuePublicationReads(api, readSucceeded: true, unlocked: true);
            api.GetAchievementExceptionsByName.Add(
                ExpectedName(GameAchievementIds.CampaignLevel2Clear),
                new InvalidOperationException("second pre-read failed"));
            using var publisher = ReadyPublisher(api, out _);
            AchievementPublicationBatchResult result = null;

            publisher.PublishBatch(
                new AchievementPublicationBatch(new[]
                {
                    GameAchievementIds.CampaignLevel1Clear,
                    GameAchievementIds.CampaignLevel2Clear,
                }),
                observed => result = observed);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items, Has.Count.EqualTo(2));
            Assert.That(
                result.Items[0].AchievementId,
                Is.EqualTo(GameAchievementIds.CampaignLevel1Clear));
            Assert.That(
                result.Items[0].Result,
                Is.EqualTo(AchievementPublicationResult.AlreadySatisfied));
            Assert.That(
                result.Items[1].AchievementId,
                Is.EqualTo(GameAchievementIds.CampaignLevel2Clear));
            Assert.That(
                result.Items[1].Result,
                Is.EqualTo(AchievementPublicationResult.Failed));
            Assert.That(api.GetAchievementCount, Is.EqualTo(2));
            AssertNoMutation(api);
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel4Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
        }

        [Test]
        public void StoreStatsException_AfterMultipleSetsFailsActiveAndQueuedBatch()
        {
            var api = ProductApi();
            QueueSuccessfulPublicationReads(api, publicationCount: 2);
            api.StoreStatsException = new InvalidOperationException("store failed");
            var publisher = ReadyPublisher(api, out _);
            var queued = new List<AchievementPublicationResult>();
            api.SetAchievementAction = _ =>
            {
                api.SetAchievementAction = null;
                Publish(
                    publisher,
                    GameAchievementIds.CampaignLevel4Clear,
                    queued.Add);
            };
            AchievementPublicationBatchResult active = null;

            publisher.PublishBatch(
                new AchievementPublicationBatch(new[]
                {
                    GameAchievementIds.CampaignLevel1Clear,
                    GameAchievementIds.CampaignLevel2Clear,
                }),
                observed => active = observed);

            Assert.That(active, Is.Not.Null);
            Assert.That(active.Items, Has.Count.EqualTo(2));
            Assert.That(
                active.Items[0].Result,
                Is.EqualTo(AchievementPublicationResult.Failed));
            Assert.That(
                active.Items[1].Result,
                Is.EqualTo(AchievementPublicationResult.Failed));
            Assert.That(queued, Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            Assert.That(api.SetAchievementCount, Is.EqualTo(2));
            Assert.That(api.StoreStatsCount, Is.EqualTo(1));
            Assert.That(api.DisposalCount, Is.EqualTo(1));
            Assert.That(
                Publish(publisher, GameAchievementIds.CampaignLevel4Clear),
                Is.EqualTo(new[] { AchievementPublicationResult.Unavailable }));
            publisher.Dispose();
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

            var results = Publish(publisher, GameAchievementIds.CampaignLevel4Clear);

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
            return ExpectedName(GameAchievementIds.CampaignLevel4Clear);
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
            Publish(publisher, achievementId, results.Add);
            return results;
        }

        private static void Publish(
            SteamAchievementPublisher publisher,
            GameAchievementId achievementId,
            Action<AchievementPublicationResult> completed)
        {
            publisher.PublishBatch(
                new AchievementPublicationBatch(new[] { achievementId }),
                result => completed(result.Items[0].Result));
        }

        private static void QueueSuccessfulPublicationReads(
            FakeSteamAchievementApi api,
            int publicationCount)
        {
            for (var i = 0; i < publicationCount; i++)
            {
                QueuePublicationReads(api, readSucceeded: true, unlocked: false);
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
                GameAchievementIds.CampaignLevel4Clear,
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

        private static void SetReadinessException(
            FakeSteamNativeApi lifecycle,
            string throwingPrerequisite)
        {
            var exception = new InvalidOperationException("readiness failed");
            switch (throwingPrerequisite)
            {
                case "app-id":
                    lifecycle.AppIdException = exception;
                    break;
                case "steam-id":
                    lifecycle.SteamIdValidException = exception;
                    break;
                case "logged-on":
                    lifecycle.LoggedOnException = exception;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(throwingPrerequisite));
            }
        }

        private sealed class FakeClock
        {
            internal double Seconds { get; set; }
        }
    }
}
