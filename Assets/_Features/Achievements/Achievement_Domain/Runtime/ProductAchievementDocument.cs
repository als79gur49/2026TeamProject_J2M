using System;
using System.Collections.Generic;

namespace Game.Product.Achievements
{
    [Serializable]
    public sealed class ProductAchievementDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string[] EarnedAchievementIds = Array.Empty<string>();
        public string[] PendingAchievementPublicationIds = Array.Empty<string>();

        public static ProductAchievementDocument CreateEmpty()
        {
            return new ProductAchievementDocument();
        }
    }

    public enum AchievementDocumentValidationStatus
    {
        Valid = 0,
        SchemaInvalid = 1,
        UnsupportedVersion = 2,
    }

    public static class ProductAchievementDocumentNormalizer
    {
        public static AchievementDocumentValidationStatus TryNormalize(
            ProductAchievementDocument document,
            out ProductAchievementDocument normalized)
        {
            normalized = null;
            if (document == null || document.SchemaVersion <= 0)
            {
                return AchievementDocumentValidationStatus.SchemaInvalid;
            }

            if (document.SchemaVersion > ProductAchievementDocument.CurrentSchemaVersion)
            {
                return AchievementDocumentValidationStatus.UnsupportedVersion;
            }

            if (!TryNormalizeIds(document.EarnedAchievementIds, out var earned) ||
                !TryNormalizeIds(document.PendingAchievementPublicationIds, out var pending))
            {
                return AchievementDocumentValidationStatus.SchemaInvalid;
            }

            var earnedSet = new HashSet<string>(earned, StringComparer.Ordinal);
            for (var i = 0; i < pending.Length; i++)
            {
                if (!earnedSet.Contains(pending[i]))
                {
                    return AchievementDocumentValidationStatus.SchemaInvalid;
                }
            }

            normalized = new ProductAchievementDocument
            {
                SchemaVersion = ProductAchievementDocument.CurrentSchemaVersion,
                EarnedAchievementIds = earned,
                PendingAchievementPublicationIds = pending,
            };
            return AchievementDocumentValidationStatus.Valid;
        }

        private static bool TryNormalizeIds(string[] values, out string[] normalized)
        {
            values ??= Array.Empty<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Length; i++)
            {
                if (!GameAchievementId.TryCreate(values[i], out var achievementId))
                {
                    normalized = null;
                    return false;
                }

                unique.Add(achievementId.Value);
            }

            normalized = new string[unique.Count];
            unique.CopyTo(normalized);
            Array.Sort(normalized, StringComparer.Ordinal);
            return true;
        }
    }
}
