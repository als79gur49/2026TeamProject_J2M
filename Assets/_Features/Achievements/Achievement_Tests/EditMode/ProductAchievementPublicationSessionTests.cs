using System;
using System.Collections.Generic;
using Game.Product.Achievements.Composition;
using NUnit.Framework;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class ProductAchievementPublicationSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
        }

        [Test]
        public void Router_DefaultsUnavailableAndUsesExactAttachDetachIdentity()
        {
            var router = new SwitchableAchievementPublicationSink();
            var session = new object();
            var otherSession = new object();
            var sink = new RecordingSink(AchievementPublicationResult.Submitted);
            var otherSink = new RecordingSink(AchievementPublicationResult.Submitted);

            Assert.That(Publish(router), Is.EqualTo(AchievementPublicationResult.Unavailable));
            Assert.That(router.TryAttach(session, sink), Is.True);
            Assert.That(router.TryAttach(session, sink), Is.True);
            Assert.That(router.TryAttach(otherSession, otherSink), Is.False);
            Assert.That(router.TryDetach(otherSession, sink), Is.False);
            Assert.That(Publish(router), Is.EqualTo(AchievementPublicationResult.Submitted));
            Assert.That(router.TryDetach(session, sink), Is.True);
            Assert.That(Publish(router), Is.EqualTo(AchievementPublicationResult.Unavailable));
        }

        [TestCase(AchievementPublicationResult.Submitted, 1, 0)]
        [TestCase(AchievementPublicationResult.AlreadySatisfied, 0, 1)]
        public void AttachAfterInitialUnavailable_ReconcilesWithResultSpecificPendingPolicy(
            AchievementPublicationResult publicationResult,
            int expectedPendingCount,
            int expectedSaveCount)
        {
            var repository = new MemoryRepository(Document(pending: true));
            var router = new SwitchableAchievementPublicationSink();
            var coordinator = CreateCoordinator(repository, router);
            Assert.That(coordinator.Initialize(), Is.True);
            var sink = new RecordingSink(publicationResult);
            var controller = new ProductAchievementPublicationSessionController(
                router,
                coordinator);

            Assert.That(controller.TryAttach(new object(), sink), Is.True);

            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(
                coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.EqualTo(expectedPendingCount));
            Assert.That(repository.SaveCount, Is.EqualTo(expectedSaveCount));
        }

        [Test]
        public void Attach_ReconcilesAllEarnedEvenWhenPendingIsEmpty()
        {
            var repository = new MemoryRepository(Document(pending: false));
            var router = new SwitchableAchievementPublicationSink();
            var coordinator = CreateCoordinator(repository, router);
            Assert.That(coordinator.Initialize(), Is.True);
            var sink = new RecordingSink(AchievementPublicationResult.AlreadySatisfied);
            var controller = new ProductAchievementPublicationSessionController(
                router,
                coordinator);

            Assert.That(controller.TryAttach(new object(), sink), Is.True);

            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void SameSessionIsIdempotentAndDifferentSessionIsRejectedForApplicationLifetime()
        {
            var repository = new MemoryRepository(Document(pending: false));
            var router = new SwitchableAchievementPublicationSink();
            var coordinator = CreateCoordinator(repository, router);
            coordinator.Initialize();
            var controller = new ProductAchievementPublicationSessionController(
                router,
                coordinator);
            var firstSession = new object();
            var firstSink = new RecordingSink(AchievementPublicationResult.Unavailable);

            Assert.That(controller.TryAttach(firstSession, firstSink), Is.True);
            Assert.That(controller.TryAttach(firstSession, firstSink), Is.True);
            Assert.That(firstSink.PublishCount, Is.EqualTo(1));

            controller.Detach(firstSession, firstSink);
            var secondSink = new RecordingSink(AchievementPublicationResult.Unavailable);
            Assert.That(controller.TryAttach(new object(), secondSink), Is.False);
            Assert.That(secondSink.PublishCount, Is.Zero);
        }

        [Test]
        public void RouterPublish_UsesTargetSnapshotAcrossDetach()
        {
            var router = new SwitchableAchievementPublicationSink();
            var session = new object();
            var sink = RecordingSink.Async();
            Assert.That(router.TryAttach(session, sink), Is.True);
            var results = new List<AchievementPublicationResult>();

            router.PublishBatch(
                Batch(GameAchievementIds.NormalCampaignComplete),
                result => results.Add(result.Items[0].Result));
            Assert.That(router.TryDetach(session, sink), Is.True);
            sink.Complete(AchievementPublicationResult.Submitted);

            Assert.That(results, Is.EqualTo(new[] { AchievementPublicationResult.Submitted }));
            Assert.That(Publish(router), Is.EqualTo(AchievementPublicationResult.Unavailable));
        }

        [Test]
        public void TypedHandoff_AttachesExactlyOnceInEitherRegistrationOrder()
        {
            var productFirst = new RecordingSessionController();
            var productFirstSink = new RecordingSink(AchievementPublicationResult.Unavailable);
            var productFirstSession = new object();
            Assert.That(
                ProductAchievementPublicationSessionHandoff.TryRegisterController(productFirst),
                Is.True);
            Assert.That(
                ProductAchievementPublicationSessionHandoff.TryRegisterSteamSession(
                    productFirstSession,
                    productFirstSink),
                Is.True);
            Assert.That(productFirst.AttachCount, Is.EqualTo(1));

            ProductAchievementPublicationSessionHandoff.ResetForTests();
            var steamFirst = new RecordingSessionController();
            var steamFirstSink = new RecordingSink(AchievementPublicationResult.Unavailable);
            var steamFirstSession = new object();
            Assert.That(
                ProductAchievementPublicationSessionHandoff.TryRegisterSteamSession(
                    steamFirstSession,
                    steamFirstSink),
                Is.True);
            Assert.That(
                ProductAchievementPublicationSessionHandoff.TryRegisterController(steamFirst),
                Is.True);
            Assert.That(steamFirst.AttachCount, Is.EqualTo(1));
            Assert.That(
                ProductAchievementPublicationSessionHandoff.TryRegisterSteamSession(
                    steamFirstSession,
                    steamFirstSink),
                Is.True);
            Assert.That(steamFirst.AttachCount, Is.EqualTo(1));
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

        private static ProductAchievementDocument Document(bool pending)
        {
            return new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { GameAchievementIds.NormalCampaignComplete.Value },
                PendingAchievementPublicationIds = pending
                    ? new[] { GameAchievementIds.NormalCampaignComplete.Value }
                    : Array.Empty<string>(),
            };
        }

        private static AchievementPublicationResult Publish(
            SwitchableAchievementPublicationSink router)
        {
            var result = AchievementPublicationResult.Failed;
            router.PublishBatch(
                Batch(GameAchievementIds.NormalCampaignComplete),
                observed => result = observed.Items[0].Result);
            return result;
        }

        private static AchievementPublicationBatch Batch(GameAchievementId achievementId)
        {
            return new AchievementPublicationBatch(new[] { achievementId });
        }

        private sealed class MemoryRepository : IAchievementDocumentRepository
        {
            private ProductAchievementDocument _document;

            internal MemoryRepository(ProductAchievementDocument document)
            {
                _document = Clone(document);
            }

            internal int SaveCount { get; private set; }

            public AchievementDocumentLoadResult Load()
            {
                return new AchievementDocumentLoadResult(
                    AchievementDocumentLoadStatus.Loaded,
                    Clone(_document),
                    string.Empty);
            }

            public AchievementDocumentSaveResult Save(ProductAchievementDocument document)
            {
                SaveCount++;
                _document = Clone(document);
                return AchievementDocumentSaveResult.Saved();
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
            private readonly AchievementPublicationResult? _result;
            private AchievementPublicationBatch _batch;
            private Action<AchievementPublicationBatchResult> _completion;

            internal RecordingSink(AchievementPublicationResult result)
            {
                _result = result;
            }

            private RecordingSink()
            {
            }

            internal int PublishCount { get; private set; }

            internal static RecordingSink Async()
            {
                return new RecordingSink();
            }

            public void PublishBatch(
                AchievementPublicationBatch batch,
                Action<AchievementPublicationBatchResult> completed)
            {
                PublishCount++;
                _batch = batch;
                _completion = completed;
                if (_result.HasValue)
                {
                    completed(AchievementPublicationBatchResult.Uniform(
                        batch,
                        _result.Value));
                }
            }

            internal void Complete(AchievementPublicationResult result)
            {
                _completion(AchievementPublicationBatchResult.Uniform(_batch, result));
            }
        }

        private sealed class RecordingSessionController :
            IProductAchievementPublicationSessionController
        {
            internal int AttachCount { get; private set; }

            public bool TryAttach(
                object sessionIdentity,
                IAchievementPublicationSink publicationSink)
            {
                AttachCount++;
                return true;
            }

            public void Detach(
                object sessionIdentity,
                IAchievementPublicationSink publicationSink)
            {
            }
        }
    }
}
