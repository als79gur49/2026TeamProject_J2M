using System;
using Game.Feature.Stages;

namespace Game.Product.Achievements.CampaignIntegration
{
    public enum NormalCampaignCompletionAchievementResult
    {
        NotAttempted = 0,
        EarnedNew = 1,
        AlreadyEarned = 2,
        ProductUnavailable = 3,
        PersistenceFailed = 4,
        InvalidReceipt = 5,
        InvalidAchievement = 6,
        ProfileUnavailable = 7,
        DirectPlayExcluded = 8,
        AlreadyReconciled = 9,
        ExceptionContained = 10,
    }

    internal interface INormalCampaignCompletionAchievementIntegration
    {
        NormalCampaignCompletionAchievementResult TryEarnAfterCommittedCompletion(
            EditorDirectPlayContext directPlayContext,
            MinimalStageCompletionResult completionResult,
            StageId completedStageId,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotData committedSlot);
    }

    internal sealed class NormalCampaignCompletionAchievementIntegration :
        INormalCampaignCompletionAchievementIntegration
    {
        private readonly IProductAchievementEarningSink _earningSink;

        public NormalCampaignCompletionAchievementIntegration(
            IProductAchievementEarningSink earningSink)
        {
            _earningSink = earningSink ?? throw new ArgumentNullException(nameof(earningSink));
        }

        public NormalCampaignCompletionAchievementResult TryEarnAfterCommittedCompletion(
            EditorDirectPlayContext directPlayContext,
            MinimalStageCompletionResult completionResult,
            StageId completedStageId,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotData committedSlot)
        {
            var currentEligibility = NormalCampaignCompletionReceiptPolicy.Evaluate(
                directPlayContext,
                completionResult,
                completedStageId,
                sequenceResolver);
            if (!currentEligibility.IsEligible)
            {
                return NormalCampaignCompletionAchievementResult.NotAttempted;
            }

            return TryEarnFromPersistedReceipt(committedSlot, sequenceResolver);
        }

        internal NormalCampaignCompletionAchievementResult TryEarnFromPersistedReceipt(
            SaveSlotData committedSlot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (!HasEligiblePersistedReceipt(committedSlot, sequenceResolver))
            {
                return NormalCampaignCompletionAchievementResult.InvalidReceipt;
            }

            try
            {
                return Map(_earningSink.Earn(GameAchievementIds.NormalCampaignComplete));
            }
            catch
            {
                return NormalCampaignCompletionAchievementResult.ExceptionContained;
            }
        }

        internal static bool HasEligiblePersistedReceipt(
            SaveSlotData slot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            return slot != null &&
                   slot.CampaignCompleted &&
                   slot.HasNormalCampaignCompletionReceipt &&
                   slot.NormalCampaignCompletionReceipt != null &&
                   NormalCampaignCompletionReceiptPolicy.IsEligiblePersistedReceipt(
                       slot.NormalCampaignCompletionReceipt,
                       sequenceResolver);
        }

        private static NormalCampaignCompletionAchievementResult Map(
            AchievementEarnResult result)
        {
            return result switch
            {
                AchievementEarnResult.EarnedNew =>
                    NormalCampaignCompletionAchievementResult.EarnedNew,
                AchievementEarnResult.AlreadyEarned =>
                    NormalCampaignCompletionAchievementResult.AlreadyEarned,
                AchievementEarnResult.PersistenceFailed =>
                    NormalCampaignCompletionAchievementResult.PersistenceFailed,
                AchievementEarnResult.UnavailableState =>
                    NormalCampaignCompletionAchievementResult.ProductUnavailable,
                AchievementEarnResult.InvalidAchievement =>
                    NormalCampaignCompletionAchievementResult.InvalidAchievement,
                _ => NormalCampaignCompletionAchievementResult.InvalidAchievement,
            };
        }
    }

    internal sealed class UnavailableNormalCampaignCompletionAchievementIntegration :
        INormalCampaignCompletionAchievementIntegration
    {
        public static readonly UnavailableNormalCampaignCompletionAchievementIntegration Instance =
            new();

        private UnavailableNormalCampaignCompletionAchievementIntegration()
        {
        }

        public NormalCampaignCompletionAchievementResult TryEarnAfterCommittedCompletion(
            EditorDirectPlayContext directPlayContext,
            MinimalStageCompletionResult completionResult,
            StageId completedStageId,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotData committedSlot)
        {
            return NormalCampaignCompletionAchievementResult.ProductUnavailable;
        }
    }
}
