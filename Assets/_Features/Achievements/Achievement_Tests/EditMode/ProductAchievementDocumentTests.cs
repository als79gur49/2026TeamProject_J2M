using System;
using NUnit.Framework;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class ProductAchievementDocumentTests
    {
        [Test]
        public void EmptyDocument_IsSchemaV1WithEmptyArrays()
        {
            var document = ProductAchievementDocument.CreateEmpty();

            Assert.That(document.SchemaVersion, Is.EqualTo(1));
            Assert.That(document.EarnedAchievementIds, Is.Empty);
            Assert.That(document.PendingAchievementPublicationIds, Is.Empty);
        }

        [Test]
        public void NullArrays_NormalizeToEmptyArrays()
        {
            var document = new ProductAchievementDocument
            {
                SchemaVersion = 1,
                EarnedAchievementIds = null,
                PendingAchievementPublicationIds = null,
            };

            var status = ProductAchievementDocumentNormalizer.TryNormalize(document, out var normalized);

            Assert.That(status, Is.EqualTo(AchievementDocumentValidationStatus.Valid));
            Assert.That(normalized.EarnedAchievementIds, Is.Empty);
            Assert.That(normalized.PendingAchievementPublicationIds, Is.Empty);
        }

        [Test]
        public void DuplicateIds_AreUniqueAndOrdinalSorted()
        {
            var document = new ProductAchievementDocument
            {
                SchemaVersion = 1,
                EarnedAchievementIds = new[] { "future.z", "campaign.level-4.clear", "future.z", "future.a" },
                PendingAchievementPublicationIds = new[] { "future.z", "campaign.level-4.clear", "future.z" },
            };

            var status = ProductAchievementDocumentNormalizer.TryNormalize(document, out var normalized);

            Assert.That(status, Is.EqualTo(AchievementDocumentValidationStatus.Valid));
            Assert.That(
                normalized.EarnedAchievementIds,
                Is.EqualTo(new[] { "campaign.level-4.clear", "future.a", "future.z" }));
            Assert.That(
                normalized.PendingAchievementPublicationIds,
                Is.EqualTo(new[] { "campaign.level-4.clear", "future.z" }));
        }

        [Test]
        public void UnknownButValidId_IsPreserved()
        {
            var document = new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { "future.valid" },
                PendingAchievementPublicationIds = Array.Empty<string>(),
            };

            var status = ProductAchievementDocumentNormalizer.TryNormalize(document, out var normalized);

            Assert.That(status, Is.EqualTo(AchievementDocumentValidationStatus.Valid));
            Assert.That(normalized.EarnedAchievementIds, Is.EqualTo(new[] { "future.valid" }));
        }

        [Test]
        public void PendingWithoutEarned_IsSchemaInvalidAndIsNotPromoted()
        {
            var document = new ProductAchievementDocument
            {
                EarnedAchievementIds = Array.Empty<string>(),
                PendingAchievementPublicationIds = new[] { "campaign.level-4.clear" },
            };

            var status = ProductAchievementDocumentNormalizer.TryNormalize(document, out var normalized);

            Assert.That(status, Is.EqualTo(AchievementDocumentValidationStatus.SchemaInvalid));
            Assert.That(normalized, Is.Null);
        }

        [TestCase("")]
        [TestCase(" campaign.level-4.clear")]
        [TestCase("CAMPAIGN.COMPLETE")]
        public void InvalidToken_IsSchemaInvalid(string token)
        {
            var document = new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { token },
                PendingAchievementPublicationIds = Array.Empty<string>(),
            };

            Assert.That(
                ProductAchievementDocumentNormalizer.TryNormalize(document, out _),
                Is.EqualTo(AchievementDocumentValidationStatus.SchemaInvalid));
        }

        [Test]
        public void UnsupportedAndNonPositiveSchemaVersions_FailClosed()
        {
            var unsupported = new ProductAchievementDocument { SchemaVersion = 2 };
            var nonPositive = new ProductAchievementDocument { SchemaVersion = 0 };

            Assert.That(
                ProductAchievementDocumentNormalizer.TryNormalize(unsupported, out _),
                Is.EqualTo(AchievementDocumentValidationStatus.UnsupportedVersion));
            Assert.That(
                ProductAchievementDocumentNormalizer.TryNormalize(nonPositive, out _),
                Is.EqualTo(AchievementDocumentValidationStatus.SchemaInvalid));
        }
    }
}
