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
            Assert.That(GameAchievementId.TryCreate("campaign.complete", out var achievementId), Is.True);
            Assert.That(achievementId.IsValid, Is.True);
            Assert.That(achievementId.Value, Is.EqualTo("campaign.complete"));
            Assert.That(achievementId.ToString(), Is.EqualTo("campaign.complete"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" campaign.complete")]
        [TestCase("campaign.complete ")]
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
            var first = GameAchievementId.Require("campaign.complete");
            var same = GameAchievementId.Require("campaign.complete");
            var different = GameAchievementId.Require("campaign-complete");

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(GameAchievementId.TryCreate("Campaign.complete", out _), Is.False);
        }

        [Test]
        public void ProductionCatalog_ContainsCanonicalCampaignAchievements()
        {
            var catalog = GameAchievementCatalog.Production;

            Assert.That(
                catalog.TryGet(GameAchievementIds.NormalCampaignComplete, out var definition),
                Is.True);
            Assert.That(definition.Id.Value, Is.EqualTo("campaign.complete"));
            Assert.That(definition.Kind, Is.EqualTo(GameAchievementKind.OneShot));
            Assert.That(
                catalog.TryGet(GameAchievementIds.CampaignStage1_2Clear, out var clear),
                Is.True);
            Assert.That(clear.Id.Value, Is.EqualTo("campaign.stage-1-2.clear"));
            Assert.That(clear.Kind, Is.EqualTo(GameAchievementKind.OneShot));
            Assert.That(
                catalog.TryGet(
                    GameAchievementIds.CampaignStage1_2PushFlipWithin25,
                    out var efficient),
                Is.True);
            Assert.That(
                efficient.Id.Value,
                Is.EqualTo("campaign.stage-1-2.push-flip-within-25"));
            Assert.That(efficient.Kind, Is.EqualTo(GameAchievementKind.OneShot));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(3));
        }

        [Test]
        public void Catalog_RejectsNullDefinitionInvalidIdAndDuplicateId()
        {
            var canonical = new GameAchievementDefinition(
                GameAchievementIds.NormalCampaignComplete,
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

            Assert.That(catalog.TryGet(GameAchievementIds.NormalCampaignComplete, out _), Is.True);
            Assert.That(GameAchievementId.TryCreate("CAMPAIGN.COMPLETE", out var mismatch), Is.False);
            Assert.That(catalog.TryGet(mismatch, out _), Is.False);
        }
    }
}
