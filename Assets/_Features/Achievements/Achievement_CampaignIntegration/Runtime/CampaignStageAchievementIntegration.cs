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

        internal CampaignStageAchievementIntegration(
            IProductAchievementEarningSink earningSink)
        {
            _earningSink = earningSink ?? throw new ArgumentNullException(nameof(earningSink));
        }

        public void TryEarnFromCommittedSlot(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (committedSlot == null || sequenceResolver == null)
            {
                return;
            }

            // The authored sequence, not stage naming or numeric suffixes, owns finality.
            // Scan all entries so even an unvalidated noncontiguous group cannot earn early.
            var lastStages = new Dictionary<string, StageId>(StringComparer.Ordinal);
            foreach (var entry in sequenceResolver.Entries)
            {
                lastStages[entry.LevelGroupId] = entry.StageId;
            }

            var clearedStages = new HashSet<StageId>();
            foreach (var record in committedSlot.NormalStagePerformanceRecords)
            {
                clearedStages.Add(record.StageId);
            }

            var earnedFromCommittedFact = new List<GameAchievementId>();
            AddIfCleared("level-0", GameAchievementIds.CampaignLevel0Clear);
            AddIfCleared("level-1", GameAchievementIds.CampaignLevel1Clear);
            AddIfCleared("level-2", GameAchievementIds.CampaignLevel2Clear);
            AddIfCleared("level-3", GameAchievementIds.CampaignLevel3Clear);
            AddIfCleared("level-4", GameAchievementIds.CampaignLevel4Clear);

            void AddIfCleared(string levelGroupId, GameAchievementId achievementId)
            {
                if (lastStages.TryGetValue(levelGroupId, out var stageId) &&
                    clearedStages.Contains(stageId))
                {
                    earnedFromCommittedFact.Add(achievementId);
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
