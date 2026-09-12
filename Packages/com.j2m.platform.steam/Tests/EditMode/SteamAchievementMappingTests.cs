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
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ProductionMapping_MapsLevelClearsBidirectionally(int level)
        {
            var mapping = SteamAchievementMapping.Production;
            Assert.That(mapping.Entries.Count, Is.EqualTo(18));
            var id = GameAchievementId.Require($"campaign.level-{level}.clear");
            Assert.That(mapping.TryGetExpectedSteamApiName(id, out var name), Is.True);
            Assert.That(name.Value, Is.EqualTo($"VQ_LEVEL_{level}_CLEAR"));
            Assert.That(mapping.TryGetGameAchievementId(name, out var reverseId), Is.True);
            Assert.That(reverseId, Is.EqualTo(id));
        }

        [TestCase("0-1", "VQ_STAGE_0_1_EFFICIENT_CLEAR")]
        [TestCase("0-2", "VQ_STAGE_0_2_EFFICIENT_CLEAR")]
        [TestCase("0-3", "VQ_STAGE_0_3_EFFICIENT_CLEAR")]
        [TestCase("1-1", "VQ_STAGE_1_1_EFFICIENT_CLEAR")]
        [TestCase("1-2", "VQ_STAGE_1_2_EFFICIENT_CLEAR")]
        [TestCase("2-1", "VQ_STAGE_2_1_EFFICIENT_CLEAR")]
        [TestCase("2-2", "VQ_STAGE_2_2_EFFICIENT_CLEAR")]
        [TestCase("3-1", "VQ_STAGE_3_1_EFFICIENT_CLEAR")]
        [TestCase("3-2", "VQ_STAGE_3_2_EFFICIENT_CLEAR")]
        [TestCase("3-3", "VQ_STAGE_3_3_EFFICIENT_CLEAR")]
        [TestCase("4-1", "VQ_STAGE_4_1_EFFICIENT_CLEAR")]
        [TestCase("4-2", "VQ_STAGE_4_2_EFFICIENT_CLEAR")]
        [TestCase("4-3", "VQ_STAGE_4_3_EFFICIENT_CLEAR")]
        public void ProductionMapping_MapsEfficientClearsBidirectionally(string stage, string expected)
        {
            var mapping = SteamAchievementMapping.Production;
            var id = GameAchievementId.Require($"campaign.stage-{stage}.efficient-clear");
            Assert.That(mapping.TryGetExpectedSteamApiName(id, out var name), Is.True);
            Assert.That(name.Value, Is.EqualTo(expected));
            Assert.That(mapping.TryGetGameAchievementId(name, out var reverse), Is.True);
            Assert.That(reverse, Is.EqualTo(id));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" VQ_LEVEL_4_CLEAR")]
        [TestCase("VQ_LEVEL_4_CLEAR ")]
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
            var id = GameAchievementIds.CampaignLevel4Clear;

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
                    Entry(GameAchievementIds.CampaignLevel4Clear, "VQ_DUPLICATE"),
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
                    ExpectedSteamAchievementApiName.Require("VQ_LEVEL_4_CLEARX"),
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
