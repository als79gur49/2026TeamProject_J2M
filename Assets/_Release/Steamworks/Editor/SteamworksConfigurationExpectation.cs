using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using UnityEditor;
using UnityEngine;

namespace Game.Release.Steamworks.Editor
{
    public static class SteamworksExpectationVocabulary
    {
        public const string Classification =
            "STEAMWORKS_CONFIGURATION_EXPECTATION";
        public const string ExpectedNotPublished = "EXPECTED_NOT_PUBLISHED";
        public const string ActualIdentityNotConfigured =
            "ACTUAL_IDENTITY_NOT_CONFIGURED";
    }

    [Serializable]
    public sealed class SteamworksConfigurationExpectation
    {
        public int schemaVersion;
        public string classification;
        public string publicationStatus;
        public string actualIdentityStatus;
        public SteamworksExpectationSource source;
        public SteamworksExpectationProduct product;
        public SteamworksExpectationDistribution distribution;
        public SteamworksExpectationAchievement[] achievements;
    }

    [Serializable]
    public sealed class SteamworksExpectationSource
    {
        public string head;
        public string tree;
    }

    [Serializable]
    public sealed class SteamworksExpectationProduct
    {
        public string companyName;
        public string productName;
    }

    [Serializable]
    public sealed class SteamworksExpectationDistribution
    {
        public string targetId;
        public string executable;
        public string expectedProvider;
        public string[] arguments;
        public string[] requiredArtifacts;
        public string[] forbiddenArtifacts;
    }

    [Serializable]
    public sealed class SteamworksExpectationAchievement
    {
        public string gameAchievementId;
        public string expectedSteamApiName;
        public string publicationStatus;
    }

    public static class SteamworksConfigurationExpectationBuilder
    {
        public static SteamworksConfigurationExpectation Build(
            string sourceHead,
            string sourceTree)
        {
            RequireGitObjectId(sourceHead, nameof(sourceHead));
            RequireGitObjectId(sourceTree, nameof(sourceTree));
            ValidateProductionAchievementCompleteness();

            var distribution = WindowsDistributionTargetPolicy.SteamWindows;
            if (WindowsDistributionTargetPolicy.ValidateConfiguration(distribution) !=
                WindowsDistributionValidationFailure.None)
            {
                throw new InvalidOperationException(
                    "The canonical SteamWindows distribution contract is invalid.");
            }

            var achievements = SteamAchievementMapping.Production.Entries
                .OrderBy(entry => entry.GameAchievementId.Value, StringComparer.Ordinal)
                .Select(entry => new SteamworksExpectationAchievement
                {
                    gameAchievementId = entry.GameAchievementId.Value,
                    expectedSteamApiName = entry.ExpectedSteamApiName.Value,
                    publicationStatus =
                        SteamworksExpectationVocabulary.ExpectedNotPublished,
                })
                .ToArray();

            return new SteamworksConfigurationExpectation
            {
                schemaVersion = 1,
                classification = SteamworksExpectationVocabulary.Classification,
                publicationStatus =
                    SteamworksExpectationVocabulary.ExpectedNotPublished,
                actualIdentityStatus =
                    SteamworksExpectationVocabulary.ActualIdentityNotConfigured,
                source = new SteamworksExpectationSource
                {
                    head = sourceHead,
                    tree = sourceTree,
                },
                product = new SteamworksExpectationProduct
                {
                    companyName = PlayerSettings.companyName,
                    productName = PlayerSettings.productName,
                },
                distribution = new SteamworksExpectationDistribution
                {
                    targetId = distribution.TargetId,
                    executable = WindowsDistributionTargetPolicy.ExecutableName,
                    expectedProvider = distribution.ExpectedProviderId,
                    arguments = distribution.CopyExpectedLaunchArguments(),
                    requiredArtifacts = distribution.CopyRequiredArtifacts(),
                    forbiddenArtifacts = distribution.CopyForbiddenArtifacts(),
                },
                achievements = achievements,
            };
        }

        public static void ValidateProductionAchievementCompleteness()
        {
            var catalog = GameAchievementCatalog.Production;
            var mapping = SteamAchievementMapping.Production;
            var mappedProductIds = new HashSet<GameAchievementId>();
            var mappedApiNames = new HashSet<ExpectedSteamAchievementApiName>();

            foreach (var entry in mapping.Entries)
            {
                if (!mappedProductIds.Add(entry.GameAchievementId))
                {
                    throw new InvalidOperationException(
                        "The production Steam mapping contains a duplicate product ID.");
                }

                if (!mappedApiNames.Add(entry.ExpectedSteamApiName))
                {
                    throw new InvalidOperationException(
                        "The production Steam mapping contains a duplicate API Name.");
                }

                if (!catalog.Contains(entry.GameAchievementId))
                {
                    throw new InvalidOperationException(
                        "The production Steam mapping contains an unknown reverse mapping.");
                }

                if (!mapping.TryGetGameAchievementId(
                        entry.ExpectedSteamApiName,
                        out var reverseId) ||
                    reverseId != entry.GameAchievementId)
                {
                    throw new InvalidOperationException(
                        "The production Steam reverse mapping is inconsistent.");
                }
            }

            foreach (var definition in catalog.Definitions)
            {
                if (!mapping.TryGetExpectedSteamApiName(
                        definition.Id,
                        out var expectedName) ||
                    !expectedName.IsValid)
                {
                    throw new InvalidOperationException(
                        "A production product achievement is missing its Steam mapping.");
                }
            }

            if (mapping.Entries.Count != catalog.Definitions.Count)
            {
                throw new InvalidOperationException(
                    "The production product catalog and Steam mapping are incomplete.");
            }
        }

        private static void RequireGitObjectId(string value, string argumentName)
        {
            if ((value == null || value.Length != 40) &&
                (value == null || value.Length != 64))
            {
                throw new ArgumentException(
                    "A full Git object ID is required.", argumentName);
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                var hexadecimal = character >= '0' && character <= '9' ||
                                  character >= 'a' && character <= 'f';
                if (!hexadecimal)
                {
                    throw new ArgumentException(
                        "A lowercase hexadecimal Git object ID is required.",
                        argumentName);
                }
            }
        }
    }

    public static class SteamworksConfigurationExpectationSerializer
    {
        public static string Serialize(SteamworksConfigurationExpectation expectation)
        {
            if (expectation == null)
            {
                throw new ArgumentNullException(nameof(expectation));
            }

            var json = JsonUtility.ToJson(expectation, true)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
            return json.TrimEnd('\n') + "\n";
        }

        public static string ComputeSha256(string canonicalJson)
        {
            if (canonicalJson == null)
            {
                throw new ArgumentNullException(nameof(canonicalJson));
            }

            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalJson));
            var builder = new StringBuilder(digest.Length * 2);
            foreach (var value in digest)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
