using System;
using Game.Feature.Stages;

namespace Game.Product.Achievements.CampaignIntegration
{
    internal sealed class NormalCampaignCompletionAchievementStartupReconciler
    {
        private readonly NormalCampaignCompletionAchievementIntegration _integration;
        private bool _executionAttempted;

        public NormalCampaignCompletionAchievementStartupReconciler(
            NormalCampaignCompletionAchievementIntegration integration)
        {
            _integration = integration ?? throw new ArgumentNullException(nameof(integration));
        }

        public NormalCampaignCompletionAchievementResult Reconcile(
            ICampaignSaveSlotStore campaignSaveSlotStore,
            CampaignStageSequenceResolver sequenceResolver,
            EditorDirectPlayContext directPlayContext)
        {
            if (_executionAttempted)
            {
                return NormalCampaignCompletionAchievementResult.AlreadyReconciled;
            }

            _executionAttempted = true;
            if (directPlayContext.Mode != EditorDirectPlayMode.None)
            {
                return NormalCampaignCompletionAchievementResult.DirectPlayExcluded;
            }

            if (campaignSaveSlotStore == null || sequenceResolver == null)
            {
                return NormalCampaignCompletionAchievementResult.ProfileUnavailable;
            }

            CampaignSaveLoadResult loadResult;
            try
            {
                loadResult = campaignSaveSlotStore.LoadAllWithReport();
            }
            catch
            {
                return NormalCampaignCompletionAchievementResult.ProfileUnavailable;
            }

            switch (loadResult.Report.Status)
            {
                case CampaignSaveLoadStatus.Missing:
                    return NormalCampaignCompletionAchievementResult.NotAttempted;

                case CampaignSaveLoadStatus.Loaded:
                case CampaignSaveLoadStatus.ImportedLegacy:
                case CampaignSaveLoadStatus.BackupRecovered:
                    break;

                default:
                    return NormalCampaignCompletionAchievementResult.ProfileUnavailable;
            }

            var slots = loadResult.Slots ?? Array.Empty<SaveSlotData>();
            for (var i = 0; i < slots.Length; i++)
            {
                if (!NormalCampaignCompletionAchievementIntegration.HasEligiblePersistedReceipt(
                        slots[i],
                        sequenceResolver))
                {
                    continue;
                }

                return _integration.TryEarnFromPersistedReceipt(
                    slots[i],
                    sequenceResolver);
            }

            return NormalCampaignCompletionAchievementResult.NotAttempted;
        }
    }
}
