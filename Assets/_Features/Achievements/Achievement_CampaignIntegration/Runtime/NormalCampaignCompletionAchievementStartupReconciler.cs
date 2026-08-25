using System;
using Game.Feature.Stages;

namespace Game.Product.Achievements.CampaignIntegration
{
    internal sealed class NormalCampaignCompletionAchievementStartupReconciler
    {
        private readonly NormalCampaignCompletionAchievementIntegration _integration;
        private readonly ICampaignStageAchievementIntegration _stageIntegration;
        private bool _executionAttempted;

        public NormalCampaignCompletionAchievementStartupReconciler(
            NormalCampaignCompletionAchievementIntegration integration,
            ICampaignStageAchievementIntegration stageIntegration = null)
        {
            _integration = integration ?? throw new ArgumentNullException(nameof(integration));
            _stageIntegration = stageIntegration ??
                UnavailableCampaignStageAchievementIntegration.Instance;
        }

        public NormalCampaignCompletionAchievementResult Reconcile(
            ICampaignSaveQuery campaignSaveSlotStore,
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
                case CampaignSaveLoadStatus.BackupRecovered:
                    break;

                default:
                    return NormalCampaignCompletionAchievementResult.ProfileUnavailable;
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

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty ||
                    !NormalCampaignCompletionAchievementIntegration.HasEligiblePersistedReceipt(
                        slots[i].State,
                        sequenceResolver))
                {
                    continue;
                }

                return _integration.TryEarnFromPersistedReceipt(
                    slots[i].State,
                    sequenceResolver);
            }

            return NormalCampaignCompletionAchievementResult.NotAttempted;
        }
    }
}
