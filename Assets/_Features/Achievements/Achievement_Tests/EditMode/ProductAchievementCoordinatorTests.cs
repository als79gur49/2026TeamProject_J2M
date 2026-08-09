using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class ProductAchievementCoordinatorTests
    {
        [Test]
        public void NewEarn_SavesEarnedAndPendingBeforePublishing()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = new RecordingSink(AchievementPublicationResult.Unavailable)
            {
                BeforeCallback = _ =>
                {
                    Assert.That(repository.SaveCount, Is.EqualTo(1));
                    Assert.That(repository.Current.EarnedAchievementIds, Does.Contain("campaign.complete"));
                    Assert.That(
                        repository.Current.PendingAchievementPublicationIds,
                        Does.Contain("campaign.complete"));
                },
            };
            var coordinator = CreateCoordinator(repository, sink);
            Assert.That(coordinator.Initialize(), Is.True);

            var result = coordinator.Earn(GameAchievementIds.NormalCampaignComplete);

            Assert.That(result, Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void SaveFailureBeforePublish_DoesNotCommitMemoryOrCallPublisher()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            repository.SaveResults.Enqueue(
                new AchievementDocumentSaveResult(AchievementDocumentSaveStatus.IoFailed, "io"));
            var sink = new RecordingSink(AchievementPublicationResult.Unavailable);
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            var result = coordinator.Earn(GameAchievementIds.NormalCampaignComplete);

            Assert.That(result, Is.EqualTo(AchievementEarnResult.PersistenceFailed));
            Assert.That(sink.PublishCount, Is.Zero);
            AssertState(coordinator, earned: false, pending: false, inFlight: 0);
        }

        [Test]
        public void DuplicateEarn_IsIdempotentAndDoesNotRepublish()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = new RecordingSink(AchievementPublicationResult.Unavailable);
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            Assert.That(
                coordinator.Earn(GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(
                coordinator.Earn(GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(AchievementEarnResult.AlreadyEarned));

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void DuplicateEarnWhilePublicationIsInFlight_DoesNotStartSecondAttempt()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            Assert.That(
                coordinator.Earn(GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(
                coordinator.Earn(GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(AchievementEarnResult.AlreadyEarned));

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 1);
        }

        [Test]
        public void SynchronousUnavailableSink_IsReentrantSafeAndRetainsOutbox()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var coordinator = CreateCoordinator(
                repository,
                new UnavailableAchievementPublicationSink());
            coordinator.Initialize();

            Assert.DoesNotThrow(() => coordinator.Earn(GameAchievementIds.NormalCampaignComplete));

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void UnavailableSink_InvokesCallbackExactlyOnceSynchronously()
        {
            var sink = new UnavailableAchievementPublicationSink();
            var callbackCount = 0;

            sink.Publish(
                GameAchievementIds.NormalCampaignComplete,
                result =>
                {
                    callbackCount++;
                    Assert.That(result, Is.EqualTo(AchievementPublicationResult.Unavailable));
                });

            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousAcceptedCallback_RemovesPendingAfterInitialDurableSave()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = new RecordingSink(AchievementPublicationResult.Accepted)
            {
                BeforeCallback = _ => Assert.That(repository.SaveCount, Is.EqualTo(1)),
            };
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            coordinator.Earn(GameAchievementIds.NormalCampaignComplete);

            Assert.That(repository.SaveCount, Is.EqualTo(2));
            AssertState(coordinator, earned: true, pending: false, inFlight: 0);
        }

        [Test]
        public void AsyncAcceptedCallback_KeepsPendingUntilCompletionThenRemovesIt()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            coordinator.Earn(GameAchievementIds.NormalCampaignComplete);
            AssertState(coordinator, earned: true, pending: true, inFlight: 1);

            sink.Complete(0, AchievementPublicationResult.Accepted);

            Assert.That(repository.SaveCount, Is.EqualTo(2));
            AssertState(coordinator, earned: true, pending: false, inFlight: 0);
        }

        [TestCase(AchievementPublicationResult.Accepted, false, 2)]
        [TestCase(AchievementPublicationResult.AlreadySatisfied, false, 2)]
        [TestCase(AchievementPublicationResult.Deferred, true, 1)]
        [TestCase(AchievementPublicationResult.Unavailable, true, 1)]
        [TestCase(AchievementPublicationResult.Rejected, true, 1)]
        [TestCase(AchievementPublicationResult.Failed, true, 1)]
        public void PublicationResultPolicy_OnlyAcceptanceRemovesPending(
            AchievementPublicationResult publicationResult,
            bool pending,
            int expectedSaves)
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var coordinator = CreateCoordinator(repository, new RecordingSink(publicationResult));
            coordinator.Initialize();

            coordinator.Earn(GameAchievementIds.NormalCampaignComplete);

            Assert.That(repository.SaveCount, Is.EqualTo(expectedSaves));
            AssertState(coordinator, earned: true, pending: pending, inFlight: 0);
        }

        [Test]
        public void AcceptedPendingRemovalSaveFailure_RetainsPendingAndAllowsNextSessionReconciliation()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            repository.SaveResults.Enqueue(AchievementDocumentSaveResult.Saved());
            repository.SaveResults.Enqueue(
                new AchievementDocumentSaveResult(AchievementDocumentSaveStatus.IoFailed, "io"));
            var firstCoordinator = CreateCoordinator(
                repository,
                new RecordingSink(AchievementPublicationResult.Accepted));
            firstCoordinator.Initialize();

            firstCoordinator.Earn(GameAchievementIds.NormalCampaignComplete);

            AssertState(firstCoordinator, earned: true, pending: true, inFlight: 0);
            Assert.That(firstCoordinator.GetSnapshot().IsUsable, Is.True);
            var nextSink = new RecordingSink(AchievementPublicationResult.AlreadySatisfied);
            var nextCoordinator = CreateCoordinator(repository, nextSink);
            Assert.That(nextCoordinator.Initialize(), Is.True);

            Assert.That(nextSink.PublishCount, Is.EqualTo(1));
            AssertState(nextCoordinator, earned: true, pending: false, inFlight: 0);
        }

        [Test]
        public void PublisherException_IsContainedAndLeavesDurableOutbox()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var coordinator = CreateCoordinator(repository, new ThrowingSink());
            coordinator.Initialize();

            Assert.DoesNotThrow(() => coordinator.Earn(GameAchievementIds.NormalCampaignComplete));

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void DuplicateCallback_FirstCompletionWinsAndSecondIsNoOp()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();
            coordinator.Earn(GameAchievementIds.NormalCampaignComplete);

            sink.Complete(0, AchievementPublicationResult.Accepted);
            sink.Complete(0, AchievementPublicationResult.Accepted);

            Assert.That(repository.SaveCount, Is.EqualTo(2));
            AssertState(coordinator, earned: true, pending: false, inFlight: 0);
        }

        [Test]
        public void CallbackAfterDispose_IsNoOp()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();
            coordinator.Earn(GameAchievementIds.NormalCampaignComplete);
            var savesBeforeDispose = repository.SaveCount;

            coordinator.Dispose();
            Assert.DoesNotThrow(() => sink.Complete(0, AchievementPublicationResult.Accepted));

            Assert.That(repository.SaveCount, Is.EqualTo(savesBeforeDispose));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void Initialize_ReconcilesAllCatalogEarnedEvenWhenPendingIsEmpty()
        {
            var repository = new RecordingRepository(
                Document(new[] { "campaign.complete" }, Array.Empty<string>()));
            var sink = new RecordingSink(AchievementPublicationResult.Unavailable);
            var coordinator = CreateCoordinator(repository, sink);

            Assert.That(coordinator.Initialize(), Is.True);

            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.Zero);
            AssertState(coordinator, earned: true, pending: false, inFlight: 0);
        }

        [Test]
        public void Initialize_ReconciliationIsBoundedOncePerCoordinatorSession()
        {
            var repository = new RecordingRepository(
                Document(new[] { "campaign.complete" }, Array.Empty<string>()));
            var sink = new RecordingSink(AchievementPublicationResult.Rejected);
            var coordinator = CreateCoordinator(repository, sink);

            Assert.That(coordinator.Initialize(), Is.True);
            Assert.That(coordinator.Initialize(), Is.True);
            Assert.That(coordinator.Initialize(), Is.True);

            Assert.That(sink.PublishCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownEarnedId_IsPreservedButExcludedFromPublication()
        {
            var repository = new RecordingRepository(
                Document(new[] { "future.valid" }, Array.Empty<string>()));
            var sink = new RecordingSink(AchievementPublicationResult.Unavailable);
            var coordinator = CreateCoordinator(repository, sink);

            Assert.That(coordinator.Initialize(), Is.True);

            Assert.That(sink.PublishCount, Is.Zero);
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds[0].Value, Is.EqualTo("future.valid"));
        }

        [Test]
        public void UnusableLoad_DisablesEarningAndPublicationWithoutSaving()
        {
            var repository = new RecordingRepository(
                ProductAchievementDocument.CreateEmpty(),
                AchievementDocumentLoadStatus.SchemaInvalid);
            var sink = new RecordingSink(AchievementPublicationResult.Unavailable);
            var coordinator = CreateCoordinator(repository, sink);

            Assert.That(coordinator.Initialize(), Is.False);
            Assert.That(
                coordinator.Earn(GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(AchievementEarnResult.UnavailableState));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(sink.PublishCount, Is.Zero);
        }

        private static ProductAchievementCoordinator CreateCoordinator(
            IAchievementDocumentRepository repository,
            IAchievementPublicationSink sink)
        {
            return new ProductAchievementCoordinator(
                repository,
                GameAchievementCatalog.Production,
                sink);
        }

        private static ProductAchievementDocument Document(string[] earned, string[] pending)
        {
            return new ProductAchievementDocument
            {
                EarnedAchievementIds = earned,
                PendingAchievementPublicationIds = pending,
            };
        }

        private static void AssertState(
            ProductAchievementCoordinator coordinator,
            bool earned,
            bool pending,
            int inFlight)
        {
            var snapshot = coordinator.GetSnapshot();
            Assert.That(Contains(snapshot.EarnedAchievementIds, GameAchievementIds.NormalCampaignComplete), Is.EqualTo(earned));
            Assert.That(
                Contains(snapshot.PendingAchievementPublicationIds, GameAchievementIds.NormalCampaignComplete),
                Is.EqualTo(pending));
            Assert.That(snapshot.InFlightCount, Is.EqualTo(inFlight));
        }

        private static bool Contains(
            IReadOnlyList<GameAchievementId> values,
            GameAchievementId expected)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == expected)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RecordingRepository : IAchievementDocumentRepository
        {
            private readonly AchievementDocumentLoadStatus _loadStatus;

            public RecordingRepository(
                ProductAchievementDocument current,
                AchievementDocumentLoadStatus loadStatus = AchievementDocumentLoadStatus.Loaded)
            {
                Current = Clone(current);
                _loadStatus = loadStatus;
            }

            public readonly Queue<AchievementDocumentSaveResult> SaveResults = new();

            public int SaveCount { get; private set; }

            public ProductAchievementDocument Current { get; private set; }

            public AchievementDocumentLoadResult Load()
            {
                return new AchievementDocumentLoadResult(
                    _loadStatus,
                    _loadStatus == AchievementDocumentLoadStatus.SchemaInvalid ? null : Clone(Current),
                    string.Empty);
            }

            public AchievementDocumentSaveResult Save(ProductAchievementDocument document)
            {
                SaveCount++;
                var result = SaveResults.Count > 0
                    ? SaveResults.Dequeue()
                    : AchievementDocumentSaveResult.Saved();
                if (result.IsSuccess)
                {
                    Current = Clone(document);
                }

                return result;
            }

            private static ProductAchievementDocument Clone(ProductAchievementDocument document)
            {
                return new ProductAchievementDocument
                {
                    SchemaVersion = document.SchemaVersion,
                    EarnedAchievementIds = (string[])document.EarnedAchievementIds.Clone(),
                    PendingAchievementPublicationIds =
                        (string[])document.PendingAchievementPublicationIds.Clone(),
                };
            }
        }

        private sealed class RecordingSink : IAchievementPublicationSink
        {
            private readonly AchievementPublicationResult? _synchronousResult;
            private readonly List<Action<AchievementPublicationResult>> _callbacks = new();

            public RecordingSink(AchievementPublicationResult synchronousResult)
            {
                _synchronousResult = synchronousResult;
            }

            private RecordingSink()
            {
            }

            public Action<GameAchievementId> BeforeCallback { get; set; }

            public int PublishCount { get; private set; }

            public static RecordingSink Async()
            {
                return new RecordingSink();
            }

            public void Publish(
                GameAchievementId achievementId,
                Action<AchievementPublicationResult> completed)
            {
                PublishCount++;
                BeforeCallback?.Invoke(achievementId);
                _callbacks.Add(completed);
                if (_synchronousResult.HasValue)
                {
                    completed(_synchronousResult.Value);
                }
            }

            public void Complete(int index, AchievementPublicationResult result)
            {
                _callbacks[index](result);
            }
        }

        private sealed class ThrowingSink : IAchievementPublicationSink
        {
            public void Publish(
                GameAchievementId achievementId,
                Action<AchievementPublicationResult> completed)
            {
                throw new InvalidOperationException("publisher failed");
            }
        }
    }
}
