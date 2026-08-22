using System;
using System.Collections.Generic;

namespace Game.Product.Achievements
{
    public static class GameAchievementIds
    {
        public static readonly GameAchievementId NormalCampaignComplete =
            GameAchievementId.Require("campaign.complete");

        public static readonly GameAchievementId CampaignStage1_2Clear =
            GameAchievementId.Require("campaign.stage-1-2.clear");

        public static readonly GameAchievementId CampaignStage1_2PushFlipWithin25 =
            GameAchievementId.Require("campaign.stage-1-2.push-flip-within-25");
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
                        GameAchievementIds.NormalCampaignComplete,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage1_2Clear,
                        GameAchievementKind.OneShot),
                    new GameAchievementDefinition(
                        GameAchievementIds.CampaignStage1_2PushFlipWithin25,
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
