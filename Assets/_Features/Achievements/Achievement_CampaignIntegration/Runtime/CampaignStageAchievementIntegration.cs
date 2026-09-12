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
        void TryEarnFromCommittedClear(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver,
            NormalCampaignStageClearFact currentClear);

        void TryEarnFromCommittedSlot(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver);
    }

    internal sealed class CampaignStageAchievementIntegration :
        ICampaignStageAchievementIntegration
    {
        private static readonly IReadOnlyDictionary<StageId, (GameAchievementId Id, int Limit)>
            EfficientClearRules = new System.Collections.ObjectModel.ReadOnlyDictionary<
                StageId, (GameAchievementId Id, int Limit)>(
                new Dictionary<StageId, (GameAchievementId Id, int Limit)>
                {
                    [StageId.CreateOrThrow("stage-0-1")] =
                        (GameAchievementIds.CampaignStage0_1EfficientClear, 25),
                    [StageId.CreateOrThrow("stage-0-2")] =
                        (GameAchievementIds.CampaignStage0_2EfficientClear, 25),
                    [StageId.CreateOrThrow("stage-0-3")] =
                        (GameAchievementIds.CampaignStage0_3EfficientClear, 12),
                    [StageId.CreateOrThrow("stage-1-1")] =
                        (GameAchievementIds.CampaignStage1_1EfficientClear, 8),
                    [StageId.CreateOrThrow("stage-1-2")] =
                        (GameAchievementIds.CampaignStage1_2EfficientClear, 16),
                    [StageId.CreateOrThrow("stage-2-1")] =
                        (GameAchievementIds.CampaignStage2_1EfficientClear, 20),
                    [StageId.CreateOrThrow("stage-2-2")] =
                        (GameAchievementIds.CampaignStage2_2EfficientClear, 30),
                    [StageId.CreateOrThrow("stage-3-1")] =
                        (GameAchievementIds.CampaignStage3_1EfficientClear, 22),
                    [StageId.CreateOrThrow("stage-3-2")] =
                        (GameAchievementIds.CampaignStage3_2EfficientClear, 35),
                    [StageId.CreateOrThrow("stage-3-3")] =
                        (GameAchievementIds.CampaignStage3_3EfficientClear, 45),
                    [StageId.CreateOrThrow("stage-4-1")] =
                        (GameAchievementIds.CampaignStage4_1EfficientClear, 35),
                    [StageId.CreateOrThrow("stage-4-2")] =
                        (GameAchievementIds.CampaignStage4_2EfficientClear, 45),
                    [StageId.CreateOrThrow("stage-4-3")] =
                        (GameAchievementIds.CampaignStage4_3EfficientClear, 40),
                });

        private readonly IProductAchievementEarningSink _earningSink;

        internal CampaignStageAchievementIntegration(
            IProductAchievementEarningSink earningSink)
        {
            _earningSink = earningSink ?? throw new ArgumentNullException(nameof(earningSink));
        }

        public void TryEarnFromCommittedClear(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver,
            NormalCampaignStageClearFact currentClear)
        {
            if (committedSlot == null || sequenceResolver == null ||
                !currentClear.StageId.IsValid || !sequenceResolver.Contains(currentClear.StageId))
            {
                return;
            }

            // Only the host's accepted clear, after its campaign commit, enters this path.
            // A historical best is never used to qualify a new efficient-clear achievement.
            var candidates = CollectLevelAchievements(committedSlot, sequenceResolver);
            if (EfficientClearRules.TryGetValue(currentClear.StageId, out var rule) &&
                currentClear.CombinedPushFlipUses <= rule.Limit)
            {
                candidates.Add(rule.Id);
            }

            EarnCandidates(candidates);
        }

        public void TryEarnFromCommittedSlot(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (committedSlot == null || sequenceResolver == null)
            {
                return;
            }

            // Startup recovery remains limited to the original level-clear achievements.
            EarnCandidates(CollectLevelAchievements(committedSlot, sequenceResolver));
        }

        private static List<GameAchievementId> CollectLevelAchievements(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver)
        {
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

            return earnedFromCommittedFact;
        }

        private void EarnCandidates(List<GameAchievementId> candidates)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            try
            {
                _earningSink.EarnBatch(candidates);
            }
            catch
            {
                // Level clears can recover from records; efficient clears require another qualifying clear.
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

        public void TryEarnFromCommittedClear(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver,
            NormalCampaignStageClearFact currentClear)
        {
        }

        public void TryEarnFromCommittedSlot(
            CampaignSlotState committedSlot,
            CampaignStageSequenceResolver sequenceResolver)
        {
        }
    }
}
