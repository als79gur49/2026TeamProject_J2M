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
                    Assert.That(repository.Current.EarnedAchievementIds, Does.Contain("campaign.level-4.clear"));
                    Assert.That(
                        repository.Current.PendingAchievementPublicationIds,
                        Does.Contain("campaign.level-4.clear"));
                },
            };
            var coordinator = CreateCoordinator(repository, sink);
            Assert.That(coordinator.Initialize(), Is.True);

            var result = coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);

            Assert.That(result, Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void EarnBatch_SavesAndPublishesAllNewAchievementsAtomically()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            var result = coordinator.EarnBatch(new[]
            {
                GameAchievementIds.CampaignLevel1Clear,
                GameAchievementIds.CampaignLevel2Clear,
            });

            Assert.That(result.Result, Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(result.NewlyEarnedAchievementIds, Has.Count.EqualTo(2));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.Current.EarnedAchievementIds, Has.Length.EqualTo(2));
            Assert.That(repository.Current.PendingAchievementPublicationIds, Has.Length.EqualTo(2));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(sink.LastBatch.AchievementIds, Has.Count.EqualTo(2));
            Assert.That(coordinator.GetSnapshot().InFlightCount, Is.EqualTo(2));
        }

        [Test]
        public void BatchCompletion_RemovesAllAlreadySatisfiedPendingItemsInOneSave()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();
            coordinator.EarnBatch(new[]
            {
                GameAchievementIds.CampaignLevel1Clear,
                GameAchievementIds.CampaignLevel2Clear,
            });

            sink.CompleteBatch(0, new[]
            {
                new AchievementPublicationItemResult(
                    GameAchievementIds.CampaignLevel1Clear,
                    AchievementPublicationResult.AlreadySatisfied),
                new AchievementPublicationItemResult(
                    GameAchievementIds.CampaignLevel2Clear,
                    AchievementPublicationResult.AlreadySatisfied),
            });

            Assert.That(repository.SaveCount, Is.EqualTo(2));
            Assert.That(repository.Current.PendingAchievementPublicationIds, Is.Empty);
            Assert.That(coordinator.GetSnapshot().InFlightCount, Is.Zero);
        }

        [Test]
        public void MalformedBatchResult_IgnoresUnknownAndDuplicateItemsAndRetainsMissingPending()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();
            coordinator.EarnBatch(new[]
            {
                GameAchievementIds.CampaignLevel1Clear,
                GameAchievementIds.CampaignLevel2Clear,
            });

            sink.CompleteBatch(0, new[]
            {
                new AchievementPublicationItemResult(
                    GameAchievementIds.CampaignLevel1Clear,
                    AchievementPublicationResult.AlreadySatisfied),
                new AchievementPublicationItemResult(
                    GameAchievementId.Require("future.valid"),
                    AchievementPublicationResult.AlreadySatisfied),
                new AchievementPublicationItemResult(
                    GameAchievementIds.CampaignLevel1Clear,
                    AchievementPublicationResult.AlreadySatisfied),
            });

            Assert.That(repository.SaveCount, Is.EqualTo(2));
            Assert.That(
                repository.Current.PendingAchievementPublicationIds,
                Is.EqualTo(new[]
                {
                    GameAchievementIds.CampaignLevel2Clear.Value,
                }));
            Assert.That(coordinator.GetSnapshot().InFlightCount, Is.Zero);
        }

        [Test]
        public void EarnBatch_DuplicateInputFailsBeforeSaveOrPublication()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            var result = coordinator.EarnBatch(new[]
            {
                GameAchievementIds.CampaignLevel1Clear,
                GameAchievementIds.CampaignLevel1Clear,
            });

            Assert.That(result.Result, Is.EqualTo(AchievementEarnResult.InvalidAchievement));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(sink.PublishCount, Is.Zero);
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

            var result = coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);

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
                coordinator.Earn(GameAchievementIds.CampaignLevel4Clear),
                Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(
                coordinator.Earn(GameAchievementIds.CampaignLevel4Clear),
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
                coordinator.Earn(GameAchievementIds.CampaignLevel4Clear),
                Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(
                coordinator.Earn(GameAchievementIds.CampaignLevel4Clear),
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

            Assert.DoesNotThrow(() => coordinator.Earn(GameAchievementIds.CampaignLevel4Clear));

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void UnavailableSink_InvokesCallbackExactlyOnceSynchronously()
        {
            var sink = new UnavailableAchievementPublicationSink();
            var callbackCount = 0;

            sink.PublishBatch(
                new AchievementPublicationBatch(new[]
                {
                    GameAchievementIds.CampaignLevel4Clear,
                }),
                result =>
                {
                    callbackCount++;
                    Assert.That(
                        result.Items[0].Result,
                        Is.EqualTo(AchievementPublicationResult.Unavailable));
                });

            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousSubmittedCallback_KeepsPendingForNextApplicationLifetime()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = new RecordingSink(AchievementPublicationResult.Submitted)
            {
                BeforeCallback = _ => Assert.That(repository.SaveCount, Is.EqualTo(1)),
            };
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void AsyncSubmittedCallback_CompletesInFlightButKeepsPending()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();

            coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);
            AssertState(coordinator, earned: true, pending: true, inFlight: 1);

            sink.Complete(0, AchievementPublicationResult.Submitted);

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [TestCase(AchievementPublicationResult.Submitted, true, 1)]
        [TestCase(AchievementPublicationResult.AlreadySatisfied, false, 2)]
        [TestCase(AchievementPublicationResult.Deferred, true, 1)]
        [TestCase(AchievementPublicationResult.Unavailable, true, 1)]
        [TestCase(AchievementPublicationResult.Rejected, true, 1)]
        [TestCase(AchievementPublicationResult.Failed, true, 1)]
        public void PublicationResultPolicy_OnlyAlreadySatisfiedRemovesPending(
            AchievementPublicationResult publicationResult,
            bool pending,
            int expectedSaves)
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var coordinator = CreateCoordinator(repository, new RecordingSink(publicationResult));
            coordinator.Initialize();

            coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);

            Assert.That(repository.SaveCount, Is.EqualTo(expectedSaves));
            AssertState(coordinator, earned: true, pending: pending, inFlight: 0);
        }

        [Test]
        public void AlreadySatisfiedPendingRemovalSaveFailure_RetainsPendingForNextApplicationLifetime()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            repository.SaveResults.Enqueue(AchievementDocumentSaveResult.Saved());
            repository.SaveResults.Enqueue(
                new AchievementDocumentSaveResult(AchievementDocumentSaveStatus.IoFailed, "io"));
            var firstCoordinator = CreateCoordinator(
                repository,
                new RecordingSink(AchievementPublicationResult.AlreadySatisfied));
            firstCoordinator.Initialize();

            firstCoordinator.Earn(GameAchievementIds.CampaignLevel4Clear);

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

            Assert.DoesNotThrow(() => coordinator.Earn(GameAchievementIds.CampaignLevel4Clear));

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
            coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);

            sink.Complete(0, AchievementPublicationResult.Submitted);
            sink.Complete(0, AchievementPublicationResult.Submitted);

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void CallbackAfterDispose_IsNoOp()
        {
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            var sink = RecordingSink.Async();
            var coordinator = CreateCoordinator(repository, sink);
            coordinator.Initialize();
            coordinator.Earn(GameAchievementIds.CampaignLevel4Clear);
            var savesBeforeDispose = repository.SaveCount;

            coordinator.Dispose();
            Assert.DoesNotThrow(() => sink.Complete(0, AchievementPublicationResult.Submitted));

            Assert.That(repository.SaveCount, Is.EqualTo(savesBeforeDispose));
            AssertState(coordinator, earned: true, pending: true, inFlight: 0);
        }

        [Test]
        public void Initialize_ReconcilesAllCatalogEarnedEvenWhenPendingIsEmpty()
        {
            var repository = new RecordingRepository(
                Document(new[] { "campaign.level-4.clear" }, Array.Empty<string>()));
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
                Document(new[] { "campaign.level-4.clear" }, Array.Empty<string>()));
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
                coordinator.Earn(GameAchievementIds.CampaignLevel4Clear),
                Is.EqualTo(AchievementEarnResult.UnavailableState));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(sink.PublishCount, Is.Zero);
        }

        [Test]
        public void EfficientClear_OfflineEarnRemainsPendingAndConfirmsOnNextApplication()
        {
            var id = GameAchievementIds.CampaignStage1_2EfficientClear;
            var repository = new RecordingRepository(ProductAchievementDocument.CreateEmpty());
            using (var first = CreateCoordinator(repository, new UnavailableAchievementPublicationSink()))
            {
                first.Initialize();
                Assert.That(first.Earn(id), Is.EqualTo(AchievementEarnResult.EarnedNew));
                Assert.That(first.Earn(id), Is.EqualTo(AchievementEarnResult.AlreadyEarned));
                Assert.That(repository.Current.PendingAchievementPublicationIds, Is.EqualTo(new[] { id.Value }));
            }

            var sink = new RecordingSink(AchievementPublicationResult.AlreadySatisfied);
            using var next = CreateCoordinator(repository, sink);
            next.Initialize();
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(sink.LastBatch.AchievementIds, Is.EqualTo(new[] { id }));
            Assert.That(repository.Current.EarnedAchievementIds, Is.EqualTo(new[] { id.Value }));
            Assert.That(repository.Current.PendingAchievementPublicationIds, Is.Empty);
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
            Assert.That(Contains(snapshot.EarnedAchievementIds, GameAchievementIds.CampaignLevel4Clear), Is.EqualTo(earned));
            Assert.That(
                Contains(snapshot.PendingAchievementPublicationIds, GameAchievementIds.CampaignLevel4Clear),
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

        [Test]
        public void RetiredPreReleaseIds_ArePreservedWithoutWritingPublishingOrGrantingReplacements()
        {
            var retired = new[] { "campaign.complete", "campaign.stage-1-2.clear",
                "campaign.stage-1-2.push-flip-within-25" };
            var repository = new RecordingRepository(Document(retired, retired));
            repository.SaveResults.Enqueue(new AchievementDocumentSaveResult(
                AchievementDocumentSaveStatus.IoFailed, "initialization must not write"));
            var sink = RecordingSink.Async();
            using var coordinator = CreateCoordinator(repository, sink);
            Assert.That(coordinator.Initialize(), Is.True);
            Assert.That(coordinator.ReconcileAllEarnedForNewPublicationSession(), Is.True);
            foreach (var id in retired)
            {
                Assert.That(coordinator.Earn(GameAchievementId.Require(id)),
                    Is.EqualTo(AchievementEarnResult.InvalidAchievement));
            }
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(sink.PublishCount, Is.Zero);
            Assert.That(repository.Current.EarnedAchievementIds, Is.EqualTo(retired));
            Assert.That(repository.Current.PendingAchievementPublicationIds, Is.EqualTo(retired));
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds.Count, Is.EqualTo(3));
        }

        [Test]
        public void MixedOldAndNewRecords_PublishOnlyActiveLevelAchievements()
        {
            var ids = new[] { "campaign.complete", "campaign.stage-1-2.clear",
                "campaign.stage-1-2.push-flip-within-25", "campaign.level-0.clear", "future.valid" };
            var repository = new RecordingRepository(Document(ids, ids));
            var sink = new RecordingSink(AchievementPublicationResult.Unavailable);
            using var coordinator = CreateCoordinator(repository, sink);
            Assert.That(coordinator.Initialize(), Is.True);
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(sink.LastBatch.AchievementIds,
                Is.EqualTo(new[] { GameAchievementIds.CampaignLevel0Clear }));
            Assert.That(coordinator.ReconcileAllEarnedForNewPublicationSession(), Is.True);
            Assert.That(sink.PublishCount, Is.EqualTo(2));
            Assert.That(sink.LastBatch.AchievementIds,
                Is.EqualTo(new[] { GameAchievementIds.CampaignLevel0Clear }));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.Current.EarnedAchievementIds, Is.EqualTo(ids));
            Assert.That(repository.Current.PendingAchievementPublicationIds, Is.EqualTo(ids));
        }

        [Test]
        public void NewLevelEarn_PreservesInactiveOldRecordsWithoutConvertingThem()
        {
            var retired = new[] { "campaign.complete", "campaign.stage-1-2.clear",
                "campaign.stage-1-2.push-flip-within-25" };
            var repository = new RecordingRepository(Document(retired, retired));
            var sink = RecordingSink.Async();
            using var coordinator = CreateCoordinator(repository, sink);
            Assert.That(coordinator.Initialize(), Is.True);
            Assert.That(coordinator.Earn(GameAchievementIds.CampaignLevel1Clear),
                Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(sink.LastBatch.AchievementIds,
                Is.EqualTo(new[] { GameAchievementIds.CampaignLevel1Clear }));
            var expected = new[] { "campaign.complete", "campaign.stage-1-2.clear",
                "campaign.stage-1-2.push-flip-within-25", "campaign.level-1.clear" };
            Assert.That(repository.Current.EarnedAchievementIds, Is.EquivalentTo(expected));
            Assert.That(repository.Current.PendingAchievementPublicationIds, Is.EquivalentTo(expected));
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
            private readonly List<AchievementPublicationBatch> _batches = new();
            private readonly List<Action<AchievementPublicationBatchResult>> _callbacks = new();

            public RecordingSink(AchievementPublicationResult synchronousResult)
            {
                _synchronousResult = synchronousResult;
            }

            private RecordingSink()
            {
            }

            public Action<GameAchievementId> BeforeCallback { get; set; }

            public int PublishCount { get; private set; }

            public AchievementPublicationBatch LastBatch =>
                _batches.Count == 0 ? null : _batches[_batches.Count - 1];

            public static RecordingSink Async()
            {
                return new RecordingSink();
            }

            public void PublishBatch(
                AchievementPublicationBatch batch,
                Action<AchievementPublicationBatchResult> completed)
            {
                PublishCount++;
                BeforeCallback?.Invoke(batch.AchievementIds[0]);
                _batches.Add(batch);
                _callbacks.Add(completed);
                if (_synchronousResult.HasValue)
                {
                    completed(AchievementPublicationBatchResult.Uniform(
                        batch,
                        _synchronousResult.Value));
                }
            }

            public void Complete(int index, AchievementPublicationResult result)
            {
                _callbacks[index](AchievementPublicationBatchResult.Uniform(
                    _batches[index],
                    result));
            }

            public void CompleteBatch(
                int index,
                IEnumerable<AchievementPublicationItemResult> items)
            {
                _callbacks[index](new AchievementPublicationBatchResult(items));
            }
        }

        private sealed class ThrowingSink : IAchievementPublicationSink
        {
            public void PublishBatch(
                AchievementPublicationBatch batch,
                Action<AchievementPublicationBatchResult> completed)
            {
                throw new InvalidOperationException("publisher failed");
            }
        }
    }
}
