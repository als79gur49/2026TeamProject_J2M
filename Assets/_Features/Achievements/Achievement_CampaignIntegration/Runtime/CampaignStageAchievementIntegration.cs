using System;
using System.Collections.Generic;
using Game.Feature.Stages;

namespace Game.Product.Achievements.CampaignIntegration
{
    internal readonly struct NormalCampaignStageClearFact
    {
        internal NormalCampaignStageClearFact(
            StageId stageId,
            int combinedPushFlipUses)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException(
                    "Normal Campaign stage clear requires a valid StageId.",
                    nameof(stageId));
            }

            if (combinedPushFlipUses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(combinedPushFlipUses));
            }

            StageId = stageId;
            CombinedPushFlipUses = combinedPushFlipUses;
        }

        internal StageId StageId { get; }

        internal int CombinedPushFlipUses { get; }
    }

    internal sealed class CampaignStageAchievementRule
    {
        internal CampaignStageAchievementRule(
            GameAchievementId achievementId,
            StageId requiredStageId,
            int? maxCombinedPushFlipUses)
        {
            if (!achievementId.IsValid)
            {
                throw new ArgumentException(
                    "Campaign stage achievement rules require a valid achievement ID.",
                    nameof(achievementId));
            }

            if (!requiredStageId.IsValid)
            {
                throw new ArgumentException(
                    "Campaign stage achievement rules require a valid StageId.",
                    nameof(requiredStageId));
            }

            if (maxCombinedPushFlipUses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCombinedPushFlipUses));
            }

            AchievementId = achievementId;
            RequiredStageId = requiredStageId;
            MaxCombinedPushFlipUses = maxCombinedPushFlipUses;
        }

        internal GameAchievementId AchievementId { get; }

        internal StageId RequiredStageId { get; }

        internal int? MaxCombinedPushFlipUses { get; }

        internal bool IsSatisfiedBy(CampaignStagePerformanceState record)
        {
            return record.StageId.Equals(RequiredStageId) &&
                   (!MaxCombinedPushFlipUses.HasValue ||
                    record.BestCombinedPushFlipUses <= MaxCombinedPushFlipUses.Value);
        }
    }

    internal sealed class CampaignStageAchievementRuleCatalog
    {
        private readonly CampaignStageAchievementRule[] _rules;

        internal CampaignStageAchievementRuleCatalog(
            IEnumerable<CampaignStageAchievementRule> rules,
            GameAchievementCatalog productCatalog)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (productCatalog == null)
            {
                throw new ArgumentNullException(nameof(productCatalog));
            }

            var validated = new List<CampaignStageAchievementRule>();
            var ids = new HashSet<GameAchievementId>();
            foreach (var rule in rules)
            {
                if (rule == null)
                {
                    throw new ArgumentException(
                        "Campaign stage achievement rules cannot contain null.",
                        nameof(rules));
                }

                if (!productCatalog.Contains(rule.AchievementId))
                {
                    throw new ArgumentException(
                        $"Campaign stage achievement '{rule.AchievementId.Value}' is absent from the product catalog.",
                        nameof(rules));
                }

                if (!ids.Add(rule.AchievementId))
                {
                    throw new ArgumentException(
                        $"Duplicate campaign stage achievement ID '{rule.AchievementId.Value}'.",
                        nameof(rules));
                }

                validated.Add(rule);
            }

            validated.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.AchievementId.Value,
                right.AchievementId.Value));
            _rules = validated.ToArray();
        }

        internal static CampaignStageAchievementRuleCatalog Production { get; } =
            new CampaignStageAchievementRuleCatalog(
                new[]
                {
                    new CampaignStageAchievementRule(
                        GameAchievementIds.CampaignStage1_2Clear,
                        StageId.CreateOrThrow("stage-1-2"),
                        maxCombinedPushFlipUses: null),
                    new CampaignStageAchievementRule(
                        GameAchievementIds.CampaignStage1_2PushFlipWithin25,
                        StageId.CreateOrThrow("stage-1-2"),
                        maxCombinedPushFlipUses: 25),
                },
                GameAchievementCatalog.Production);

        internal IReadOnlyList<CampaignStageAchievementRule> Rules => _rules;
    }

    internal interface ICampaignStageAchievementIntegration
    {
        void TryEarnFromCommittedSlot(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver);
    }

    internal sealed class CampaignStageAchievementIntegration :
        ICampaignStageAchievementIntegration
    {
        private readonly IProductAchievementEarningSink _earningSink;
        private readonly CampaignStageAchievementRuleCatalog _rules;

        internal CampaignStageAchievementIntegration(
            IProductAchievementEarningSink earningSink,
            CampaignStageAchievementRuleCatalog rules = null)
        {
            _earningSink = earningSink ?? throw new ArgumentNullException(nameof(earningSink));
            _rules = rules ?? CampaignStageAchievementRuleCatalog.Production;
        }

        public void TryEarnFromCommittedSlot(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (committedSlot == null || sequenceResolver == null)
            {
                return;
            }

            var records = committedSlot.NormalStagePerformanceRecords;
            var earnedFromCommittedFact = new List<GameAchievementId>();
            for (var ruleIndex = 0; ruleIndex < _rules.Rules.Count; ruleIndex++)
            {
                var rule = _rules.Rules[ruleIndex];
                if (!sequenceResolver.Contains(rule.RequiredStageId))
                {
                    continue;
                }

                for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
                {
                    if (!rule.IsSatisfiedBy(records[recordIndex]))
                    {
                        continue;
                    }

                    earnedFromCommittedFact.Add(rule.AchievementId);
                    break;
                }
            }

            if (earnedFromCommittedFact.Count == 0)
            {
                return;
            }

            try
            {
                _earningSink.EarnBatch(earnedFromCommittedFact);
            }
            catch
            {
                // The durable stage record remains the startup recovery source.
            }
        }
    }

    internal sealed class UnavailableCampaignStageAchievementIntegration :
        ICampaignStageAchievementIntegration
    {
        internal static readonly UnavailableCampaignStageAchievementIntegration Instance = new();

        private UnavailableCampaignStageAchievementIntegration()
        {
        }

        public void TryEarnFromCommittedSlot(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver)
        {
        }
    }
}
