using System;
using Game.Feature.Stages;

namespace Game.Product.Achievements.CampaignIntegration
{
    internal readonly struct NormalCampaignCompletionFact
    {
        internal NormalCampaignCompletionFact(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException(
                    "Normal Campaign completion requires a valid StageId.",
                    nameof(stageId));
            }

            StageId = stageId;
        }

        internal StageId StageId { get; }
    }

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
            NormalCampaignCompletionFact completion,
            CampaignStageSequenceResolver sequenceResolver,
            CampaignSlotState committedSlot);
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
            NormalCampaignCompletionFact completion,
            CampaignStageSequenceResolver sequenceResolver,
            CampaignSlotState committedSlot)
        {
            if (sequenceResolver == null ||
                !sequenceResolver.Contains(completion.StageId) ||
                !sequenceResolver.IsFinal(completion.StageId))
            {
                return NormalCampaignCompletionAchievementResult.NotAttempted;
            }

            if (committedSlot?.Receipt?.Presence !=
                    CampaignReceiptPresence.PresentWithPayload ||
                !committedSlot.Receipt.Payload.CompletedStageId.Equals(completion.StageId))
            {
                return NormalCampaignCompletionAchievementResult.InvalidReceipt;
            }

            return TryEarnFromPersistedReceipt(committedSlot, sequenceResolver);
        }

        internal NormalCampaignCompletionAchievementResult TryEarnFromPersistedReceipt(
            CampaignSlotState committedSlot,
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
            CampaignSlotState slot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            return slot != null &&
                   slot.CampaignCompleted &&
                   slot.Receipt.Presence == CampaignReceiptPresence.PresentWithPayload &&
                   sequenceResolver != null &&
                   sequenceResolver.Contains(slot.Receipt.Payload.CompletedStageId) &&
                   sequenceResolver.IsFinal(slot.Receipt.Payload.CompletedStageId);
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
            NormalCampaignCompletionFact completion,
            CampaignStageSequenceResolver sequenceResolver,
            CampaignSlotState committedSlot)
        {
            return NormalCampaignCompletionAchievementResult.ProductUnavailable;
        }
    }
}
