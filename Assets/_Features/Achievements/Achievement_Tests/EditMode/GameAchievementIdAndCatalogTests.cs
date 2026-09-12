using System;
using NUnit.Framework;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class GameAchievementIdAndCatalogTests
    {
        [Test]
        public void CanonicalToken_IsValidAndRoundTripsWithoutNormalization()
        {
            Assert.That(GameAchievementId.TryCreate("campaign.level-4.clear", out var achievementId), Is.True);
            Assert.That(achievementId.IsValid, Is.True);
            Assert.That(achievementId.Value, Is.EqualTo("campaign.level-4.clear"));
            Assert.That(achievementId.ToString(), Is.EqualTo("campaign.level-4.clear"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" campaign.level-4.clear")]
        [TestCase("campaign.level-4.clear ")]
        [TestCase("Campaign.complete")]
        [TestCase("campaign/complete")]
        public void InvalidToken_IsRejectedWithoutTrimmingOrCaseConversion(string value)
        {
            Assert.That(GameAchievementId.TryCreate(value, out var achievementId), Is.False);
            Assert.That(achievementId.IsValid, Is.False);
        }

        [Test]
        public void DefaultId_IsInvalid()
        {
            Assert.That(default(GameAchievementId).IsValid, Is.False);
            Assert.That(default(GameAchievementId).Value, Is.Empty);
        }

        [Test]
        public void EqualityAndHash_AreOrdinalAndCaseSensitive()
        {
            var first = GameAchievementId.Require("campaign.level-4.clear");
            var same = GameAchievementId.Require("campaign.level-4.clear");
            var different = GameAchievementId.Require("campaign-complete");

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(GameAchievementId.TryCreate("Campaign.complete", out _), Is.False);
        }

        [Test]
        public void ProductionCatalog_ContainsExactlyFiveLevelClearAchievements()
        {
            var catalog = GameAchievementCatalog.Production;
            Assert.That(catalog.Definitions.Count, Is.EqualTo(5));
            for (var level = 0; level < 5; level++)
            {
                var id = GameAchievementId.Require($"campaign.level-{level}.clear");
                Assert.That(catalog.TryGet(id, out var definition), Is.True);
                Assert.That(definition.Kind, Is.EqualTo(GameAchievementKind.OneShot));
            }
        }

        [Test]
        public void Catalog_RejectsNullDefinitionInvalidIdAndDuplicateId()
        {
            var canonical = new GameAchievementDefinition(
                GameAchievementIds.CampaignLevel4Clear,
                GameAchievementKind.OneShot);

            Assert.Throws<ArgumentException>(() =>
                new GameAchievementCatalog(new GameAchievementDefinition[] { null }));
            Assert.Throws<ArgumentException>(() =>
                new GameAchievementCatalog(
                    new[] { new GameAchievementDefinition(default, GameAchievementKind.OneShot) }));
            Assert.Throws<ArgumentException>(() =>
                new GameAchievementCatalog(new[] { canonical, canonical }));
        }

        [Test]
        public void CatalogLookup_IsExactAndDoesNotAcceptCaseMismatch()
        {
            var catalog = GameAchievementCatalog.Production;

            Assert.That(catalog.TryGet(GameAchievementIds.CampaignLevel4Clear, out _), Is.True);
            Assert.That(GameAchievementId.TryCreate("CAMPAIGN.COMPLETE", out var mismatch), Is.False);
            Assert.That(catalog.TryGet(mismatch, out _), Is.False);
        }
    }
}
