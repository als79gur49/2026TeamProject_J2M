using System;
using Game.Feature.Stages;

namespace Game.Product.Achievements.CampaignIntegration
{
    public enum CampaignStageAchievementReconciliationResult
    {
        NotAttempted,
        Completed,
        ProductUnavailable,
        ProfileUnavailable,
        DirectPlayExcluded,
        AlreadyReconciled,
        ExceptionContained,
    }

    internal sealed class CampaignStageAchievementStartupReconciler
    {
        private readonly ICampaignStageAchievementIntegration _stageIntegration;
        private bool _executionAttempted;

        public CampaignStageAchievementStartupReconciler(
            ICampaignStageAchievementIntegration stageIntegration)
        {
            _stageIntegration = stageIntegration ?? throw new ArgumentNullException(nameof(stageIntegration));
        }

        public CampaignStageAchievementReconciliationResult Reconcile(
            ICampaignSaveQuery campaignSaveSlotStore,
            CampaignStageSequenceResolver sequenceResolver,
            EditorDirectPlayContext directPlayContext)
        {
            if (_executionAttempted)
            {
                return CampaignStageAchievementReconciliationResult.AlreadyReconciled;
            }

            _executionAttempted = true;
            if (directPlayContext.Mode != EditorDirectPlayMode.None)
            {
                return CampaignStageAchievementReconciliationResult.DirectPlayExcluded;
            }

            if (campaignSaveSlotStore == null || sequenceResolver == null)
            {
                return CampaignStageAchievementReconciliationResult.ProfileUnavailable;
            }

            CampaignSaveLoadResult loadResult;
            try
            {
                loadResult = campaignSaveSlotStore.LoadAllWithReport();
            }
            catch
            {
                return CampaignStageAchievementReconciliationResult.ProfileUnavailable;
            }

            switch (loadResult.Report.Status)
            {
                case CampaignSaveLoadStatus.Missing:
                    return CampaignStageAchievementReconciliationResult.NotAttempted;

                case CampaignSaveLoadStatus.Loaded:
                case CampaignSaveLoadStatus.BackupRecovered:
                    break;

                default:
                    return CampaignStageAchievementReconciliationResult.ProfileUnavailable;
            }

            var slots = loadResult.Slots ?? Array.Empty<CampaignSlotEntry>();
            for (var i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    _stageIntegration.TryEarnFromCommittedSlot(
                        slots[i].State,
                        sequenceResolver);
                }
            }

            return CampaignStageAchievementReconciliationResult.Completed;
        }
    }
}
