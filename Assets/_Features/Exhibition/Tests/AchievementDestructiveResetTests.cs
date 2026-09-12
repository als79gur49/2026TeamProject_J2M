using System;
using System.IO;
using System.Linq;
using Game.Exhibition.Integration;
using Game.Feature.Stages;
using Game.Product.Achievements;
using Game.Product.Achievements.Composition;
using Game.Product.Achievements.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Game.Exhibition.Tests
{
    [Category("Full")]
    public sealed class AchievementDestructiveResetTests
    {
        private string _root;
        private string Canonical => Path.Combine(_root, FileProductAchievementRepository.AchievementFileName);
        private FileProductAchievementRepository Repository => new FileProductAchievementRepository(
            new StageAtomicAchievementTextStoreAdapter(new AtomicTextFileStore(_root), _root));
        [SetUp] public void SetUp() { _root = Path.Combine(Path.GetTempPath(), "j2m-achievement-reset-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root); }
        [TearDown] public void TearDown() { Directory.Delete(_root, true); }

        [TestCase(false)] [TestCase(true)]
        public void ResetRemovesOldEarnedAndPendingFromEveryRecoverySource(bool removeCanonical)
        {
            var old = JsonUtility.ToJson(new ProductAchievementDocument
            {
                EarnedAchievementIds = GameAchievementCatalog.Production.Definitions.Select(d => d.Id.Value).ToArray(),
                PendingAchievementPublicationIds = GameAchievementCatalog.Production.Definitions.Select(d => d.Id.Value).ToArray(),
            });
            File.WriteAllText(Canonical, old);
            File.WriteAllText(Canonical + ".bak", old);
            File.WriteAllText(Canonical + ".rollback", old);
            File.WriteAllText(Canonical + ".bak.rollback", old);
            if (removeCanonical) File.Delete(Canonical);
            Repository.Reset(); Repository.Reset();
            AssertEmpty(Repository.Load());
            File.Delete(Canonical);
            AssertEmpty(Repository.Load());
            Assert.That(File.Exists(Canonical + ".rollback"), Is.False);
            Assert.That(File.Exists(Canonical + ".bak.rollback"), Is.False);
        }

        [Test]
        public void ResetKeepsSettingsAndDiagnosticFiles()
        {
            var settings = Path.Combine(_root, "settings.json");
            var diagnostic = Canonical + ".corrupt.evidence";
            File.WriteAllText(settings, "operator settings"); File.WriteAllText(diagnostic, "diagnostics");
            Repository.Reset();
            Assert.That(File.ReadAllText(settings), Is.EqualTo("operator settings"));
            Assert.That(File.ReadAllText(diagnostic), Is.EqualTo("diagnostics"));
        }

        [Test]
        public void FreshCoordinatorAfterResetDoesNotRepublishAndCanEarnEfficientAchievementAgain()
        {
            var names = GameAchievementCatalog.Production.Definitions.Select(d => d.Id.Value).ToArray();
            Assert.That(names.Length, Is.EqualTo(18));
            var old = JsonUtility.ToJson(new ProductAchievementDocument
            {
                EarnedAchievementIds = names, PendingAchievementPublicationIds = names,
            });
            File.WriteAllText(Canonical, old);
            File.WriteAllText(Canonical + ".bak", old);
            File.WriteAllText(Canonical + ".rollback", old);
            new ParticipantProgressResetAdapter(new Paths(_root)).Reset();
            AssertEmpty(Repository.Load());
            var sink = new PublicationSpy();
            using (var coordinator = new ProductAchievementCoordinator(Repository, GameAchievementCatalog.Production, sink))
            {
                Assert.That(coordinator.Initialize(), Is.True);
                Assert.That(sink.Calls, Is.Zero);
                var id = GameAchievementIds.CampaignStage1_2EfficientClear;
                Assert.That(coordinator.Earn(id), Is.EqualTo(AchievementEarnResult.EarnedNew));
                Assert.That(coordinator.Earn(id), Is.EqualTo(AchievementEarnResult.AlreadyEarned));
                Assert.That(sink.Calls, Is.EqualTo(1));
                Assert.That(sink.Last.AchievementIds, Is.EqualTo(new[] { id }));
                Assert.That(Repository.Load().Document.EarnedAchievementIds, Is.EqualTo(new[] { id.Value }));
            }
        }

        private sealed class Paths : SavePathProviderBase
        {
            public Paths(string root) : base(root) { }
        }

        private sealed class PublicationSpy : IAchievementPublicationSink
        {
            public int Calls;
            public AchievementPublicationBatch Last;
            public void PublishBatch(AchievementPublicationBatch batch, Action<AchievementPublicationBatchResult> completed)
            {
                Calls++; Last = batch;
                completed(AchievementPublicationBatchResult.Uniform(batch, AchievementPublicationResult.Submitted));
            }
        }

        private static void AssertEmpty(AchievementDocumentLoadResult loaded)
        {
            Assert.That(loaded.IsUsable, Is.True);
            Assert.That(loaded.Document.EarnedAchievementIds, Is.Empty);
            Assert.That(loaded.Document.PendingAchievementPublicationIds, Is.Empty);
        }
    }
}
