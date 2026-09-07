using System;
using System.IO;
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
                EarnedAchievementIds = new[] { "level-clear-0" },
                PendingAchievementPublicationIds = new[] { "level-clear-0" },
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

        private static void AssertEmpty(AchievementDocumentLoadResult loaded)
        {
            Assert.That(loaded.IsUsable, Is.True);
            Assert.That(loaded.Document.EarnedAchievementIds, Is.Empty);
            Assert.That(loaded.Document.PendingAchievementPublicationIds, Is.Empty);
        }
    }
}
