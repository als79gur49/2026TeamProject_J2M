using System;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using NUnit.Framework;

namespace Game.Platform.Steam.Tests.EditMode
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class SteamAchievementMappingTests
    {
        [Test]
        public void ProductionMapping_MapsCampaignCompleteToCanonicalExpectedNameExactly()
        {
            Assert.That(
                SteamAchievementMapping.Production.TryGetExpectedSteamApiName(
                    GameAchievementIds.NormalCampaignComplete,
                    out var expectedName),
                Is.True);
            Assert.That(expectedName.Value, Is.EqualTo("VQ_CAMPAIGN_COMPLETE"));
            Assert.That(
                SteamAchievementMapping.Production.TryGetGameAchievementId(
                    expectedName,
                    out var gameAchievementId),
                Is.True);
            Assert.That(gameAchievementId, Is.EqualTo(GameAchievementIds.NormalCampaignComplete));
        }

        [TestCase("clear", "VQ_STAGE_1_2_CLEAR")]
        [TestCase("efficient", "VQ_STAGE_1_2_PUSH_FLIP_LE_25")]
        public void ProductionMapping_MapsStage1_2AchievementsExactly(
            string kind,
            string expectedApiName)
        {
            var achievementId = kind == "clear"
                ? GameAchievementIds.CampaignStage1_2Clear
                : GameAchievementIds.CampaignStage1_2PushFlipWithin25;

            Assert.That(
                SteamAchievementMapping.Production.TryGetExpectedSteamApiName(
                    achievementId,
                    out var expectedName),
                Is.True);
            Assert.That(expectedName.Value, Is.EqualTo(expectedApiName));
            Assert.That(
                SteamAchievementMapping.Production.TryGetGameAchievementId(
                    expectedName,
                    out var reverseId),
                Is.True);
            Assert.That(reverseId, Is.EqualTo(achievementId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" VQ_CAMPAIGN_COMPLETE")]
        [TestCase("VQ_CAMPAIGN_COMPLETE ")]
        [TestCase("vq_campaign_complete")]
        [TestCase("VQ-CAMPAIGN-COMPLETE")]
        public void ExpectedName_InvalidOrNonCanonicalValueIsRejected(string value)
        {
            Assert.That(
                ExpectedSteamAchievementApiName.TryCreate(value, out _),
                Is.False);
        }

        [Test]
        public void DuplicateGameAchievementId_IsRejected()
        {
            var id = GameAchievementIds.NormalCampaignComplete;

            Assert.Throws<ArgumentException>(() => new SteamAchievementMapping(
                new[]
                {
                    Entry(id, "VQ_FIRST"),
                    Entry(id, "VQ_SECOND"),
                }));
        }

        [Test]
        public void DuplicateExpectedSteamApiName_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new SteamAchievementMapping(
                new[]
                {
                    Entry(GameAchievementIds.NormalCampaignComplete, "VQ_DUPLICATE"),
                    Entry(GameAchievementId.Require("future.valid"), "VQ_DUPLICATE"),
                }));
        }

        [Test]
        public void UnknownAndCaseMismatchedLookupsFail()
        {
            Assert.That(
                SteamAchievementMapping.Production.TryGetExpectedSteamApiName(
                    GameAchievementId.Require("future.valid"),
                    out _),
                Is.False);
            Assert.That(
                ExpectedSteamAchievementApiName.TryCreate(
                    "vq_campaign_complete",
                    out _),
                Is.False);
            Assert.That(
                SteamAchievementMapping.Production.TryGetGameAchievementId(
                    ExpectedSteamAchievementApiName.Require("VQ_CAMPAIGN_COMPLETEX"),
                    out _),
                Is.False);
        }

        private static SteamAchievementMappingEntry Entry(
            GameAchievementId id,
            string expectedName)
        {
            return new SteamAchievementMappingEntry(
                id,
                ExpectedSteamAchievementApiName.Require(expectedName));
        }
    }
}
