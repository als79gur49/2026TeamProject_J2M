using System;
using System.Collections.Generic;
using Game.Product.Achievements;

namespace Game.Platform.Steam.ProductAchievements
{
    public readonly struct ExpectedSteamAchievementApiName :
        IEquatable<ExpectedSteamAchievementApiName>
    {
        private readonly string _value;

        private ExpectedSteamAchievementApiName(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsValid => IsValidValue(_value);

        public static bool TryCreate(
            string value,
            out ExpectedSteamAchievementApiName apiName)
        {
            if (!IsValidValue(value))
            {
                apiName = default;
                return false;
            }

            apiName = new ExpectedSteamAchievementApiName(value);
            return true;
        }

        public static ExpectedSteamAchievementApiName Require(string value)
        {
            if (!TryCreate(value, out var apiName))
            {
                throw new ArgumentException(
                    "A valid expected Steam achievement API Name is required.",
                    nameof(value));
            }

            return apiName;
        }

        public bool Equals(ExpectedSteamAchievementApiName other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ExpectedSteamAchievementApiName other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(
            ExpectedSteamAchievementApiName left,
            ExpectedSteamAchievementApiName right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ExpectedSteamAchievementApiName left,
            ExpectedSteamAchievementApiName right)
        {
            return !left.Equals(right);
        }

        private static bool IsValidValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                var isUppercaseAscii = character >= 'A' && character <= 'Z';
                var isDigit = character >= '0' && character <= '9';
                if (!isUppercaseAscii && !isDigit && character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class SteamAchievementMappingEntry
    {
        public SteamAchievementMappingEntry(
            GameAchievementId gameAchievementId,
            ExpectedSteamAchievementApiName expectedSteamApiName)
        {
            if (!gameAchievementId.IsValid)
            {
                throw new ArgumentException(
                    "A valid product achievement ID is required.",
                    nameof(gameAchievementId));
            }

            if (!expectedSteamApiName.IsValid)
            {
                throw new ArgumentException(
                    "A valid expected Steam API Name is required.",
                    nameof(expectedSteamApiName));
            }

            GameAchievementId = gameAchievementId;
            ExpectedSteamApiName = expectedSteamApiName;
        }

        public GameAchievementId GameAchievementId { get; }

        public ExpectedSteamAchievementApiName ExpectedSteamApiName { get; }
    }

    public sealed class SteamAchievementMapping
    {
        private readonly Dictionary<GameAchievementId, ExpectedSteamAchievementApiName>
            _byGameAchievementId;
        private readonly Dictionary<ExpectedSteamAchievementApiName, GameAchievementId>
            _byExpectedSteamApiName;
        private readonly IReadOnlyList<SteamAchievementMappingEntry> _entries;

        public SteamAchievementMapping(IEnumerable<SteamAchievementMappingEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _byGameAchievementId =
                new Dictionary<GameAchievementId, ExpectedSteamAchievementApiName>();
            _byExpectedSteamApiName =
                new Dictionary<ExpectedSteamAchievementApiName, GameAchievementId>();
            var validated = new List<SteamAchievementMappingEntry>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException(
                        "Steam achievement mapping entries cannot contain null.",
                        nameof(entries));
                }

                if (!_byGameAchievementId.TryAdd(
                    entry.GameAchievementId,
                    entry.ExpectedSteamApiName))
                {
                    throw new ArgumentException(
                        "Duplicate product achievement ID '" +
                        entry.GameAchievementId.Value + "'.",
                        nameof(entries));
                }

                if (!_byExpectedSteamApiName.TryAdd(
                    entry.ExpectedSteamApiName,
                    entry.GameAchievementId))
                {
                    throw new ArgumentException(
                        "Duplicate expected Steam API Name '" +
                        entry.ExpectedSteamApiName.Value + "'.",
                        nameof(entries));
                }

                validated.Add(entry);
            }

            validated.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.GameAchievementId.Value,
                right.GameAchievementId.Value));
            _entries = Array.AsReadOnly(validated.ToArray());
        }

        public static SteamAchievementMapping Production { get; } =
            new SteamAchievementMapping(
                new[]
                {
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignLevel0Clear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_LEVEL_0_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignLevel1Clear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_LEVEL_1_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignLevel2Clear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_LEVEL_2_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignLevel3Clear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_LEVEL_3_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignLevel4Clear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_LEVEL_4_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage0_1EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_0_1_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage0_2EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_0_2_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage0_3EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_0_3_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage1_1EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_1_1_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage1_2EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_1_2_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage2_1EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_2_1_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage2_2EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_2_2_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage3_1EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_3_1_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage3_2EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_3_2_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage3_3EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_3_3_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage4_1EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_4_1_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage4_2EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_4_2_EFFICIENT_CLEAR")),
                    new SteamAchievementMappingEntry(
                        GameAchievementIds.CampaignStage4_3EfficientClear,
                        ExpectedSteamAchievementApiName.Require(
                            "VQ_STAGE_4_3_EFFICIENT_CLEAR")),
                });

        public IReadOnlyList<SteamAchievementMappingEntry> Entries => _entries;

        public bool TryGetExpectedSteamApiName(
            GameAchievementId gameAchievementId,
            out ExpectedSteamAchievementApiName expectedSteamApiName)
        {
            return _byGameAchievementId.TryGetValue(
                gameAchievementId,
                out expectedSteamApiName);
        }

        public bool TryGetGameAchievementId(
            ExpectedSteamAchievementApiName expectedSteamApiName,
            out GameAchievementId gameAchievementId)
        {
            return _byExpectedSteamApiName.TryGetValue(
                expectedSteamApiName,
                out gameAchievementId);
        }
    }
}
