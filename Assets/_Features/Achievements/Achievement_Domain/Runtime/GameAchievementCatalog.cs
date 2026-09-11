using System;
using System.Collections.Generic;

namespace Game.Product.Achievements
{
    public static class GameAchievementIds
    {
        public static readonly GameAchievementId CampaignLevel0Clear =
            GameAchievementId.Require("campaign.level-0.clear");

        public static readonly GameAchievementId CampaignLevel1Clear =
            GameAchievementId.Require("campaign.level-1.clear");

        public static readonly GameAchievementId CampaignLevel2Clear =
            GameAchievementId.Require("campaign.level-2.clear");

        public static readonly GameAchievementId CampaignLevel3Clear =
            GameAchievementId.Require("campaign.level-3.clear");

        public static readonly GameAchievementId CampaignLevel4Clear =
            GameAchievementId.Require("campaign.level-4.clear");

        public static readonly GameAchievementId CampaignStage0_1EfficientClear =
            GameAchievementId.Require("campaign.stage-0-1.efficient-clear");

        public static readonly GameAchievementId CampaignStage0_2EfficientClear =
            GameAchievementId.Require("campaign.stage-0-2.efficient-clear");

        public static readonly GameAchievementId CampaignStage0_3EfficientClear =
            GameAchievementId.Require("campaign.stage-0-3.efficient-clear");

        public static readonly GameAchievementId CampaignStage1_1EfficientClear =
            GameAchievementId.Require("campaign.stage-1-1.efficient-clear");

        public static readonly GameAchievementId CampaignStage1_2EfficientClear =
            GameAchievementId.Require("campaign.stage-1-2.efficient-clear");

        public static readonly GameAchievementId CampaignStage2_1EfficientClear =
            GameAchievementId.Require("campaign.stage-2-1.efficient-clear");

        public static readonly GameAchievementId CampaignStage2_2EfficientClear =
            GameAchievementId.Require("campaign.stage-2-2.efficient-clear");

        public static readonly GameAchievementId CampaignStage3_1EfficientClear =
            GameAchievementId.Require("campaign.stage-3-1.efficient-clear");

        public static readonly GameAchievementId CampaignStage3_2EfficientClear =
            GameAchievementId.Require("campaign.stage-3-2.efficient-clear");

        public static readonly GameAchievementId CampaignStage3_3EfficientClear =
            GameAchievementId.Require("campaign.stage-3-3.efficient-clear");

        public static readonly GameAchievementId CampaignStage4_1EfficientClear =
            GameAchievementId.Require("campaign.stage-4-1.efficient-clear");

        public static readonly GameAchievementId CampaignStage4_2EfficientClear =
            GameAchievementId.Require("campaign.stage-4-2.efficient-clear");

        public static readonly GameAchievementId CampaignStage4_3EfficientClear =
            GameAchievementId.Require("campaign.stage-4-3.efficient-clear");

    }

    public enum GameAchievementKind
    {
        OneShot = 0,
    }

    public sealed class GameAchievementDefinition
    {
        public GameAchievementDefinition(GameAchievementId id, GameAchievementKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public GameAchievementId Id { get; }

        public GameAchievementKind Kind { get; }
    }

    public sealed class GameAchievementCatalog
    {
        private readonly Dictionary<GameAchievementId, GameAchievementDefinition> _byId;
        private readonly GameAchievementDefinition[] _definitions;

        public GameAchievementCatalog(IEnumerable<GameAchievementDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _byId = new Dictionary<GameAchievementId, GameAchievementDefinition>();
            var validated = new List<GameAchievementDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Achievement definitions cannot contain null.", nameof(definitions));
                }

                if (!definition.Id.IsValid)
                {
                    throw new ArgumentException("Achievement definitions must use valid IDs.", nameof(definitions));
                }

                if (!Enum.IsDefined(typeof(GameAchievementKind), definition.Kind))
                {
                    throw new ArgumentException("Achievement definitions must use a supported kind.", nameof(definitions));
                }

                if (!_byId.TryAdd(definition.Id, definition))
                {
                    throw new ArgumentException(
                        $"Duplicate achievement ID '{definition.Id.Value}'.",
                        nameof(definitions));
                }

                validated.Add(definition);
            }

            validated.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.Id.Value, right.Id.Value));
            _definitions = validated.ToArray();
        }

        public static GameAchievementCatalog Production { get; } =
            new GameAchievementCatalog(
                new[]
                {
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignLevel0Clear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignLevel1Clear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignLevel2Clear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignLevel3Clear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignLevel4Clear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage0_1EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage0_2EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage0_3EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage1_1EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage1_2EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage2_1EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage2_2EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage3_1EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage3_2EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage3_3EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage4_1EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage4_2EfficientClear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage4_3EfficientClear,
                        GameAchievementKind.OneShot),
                });

        public IReadOnlyList<GameAchievementDefinition> Definitions => _definitions;

        public bool TryGet(GameAchievementId id, out GameAchievementDefinition definition)
        {
            return _byId.TryGetValue(id, out definition);
        }

        public bool Contains(GameAchievementId id)
        {
            return _byId.ContainsKey(id);
        }
    }
}
