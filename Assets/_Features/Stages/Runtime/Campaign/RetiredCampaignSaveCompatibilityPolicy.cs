using System;

namespace Game.Feature.Stages
{
    public static class RetiredCampaignSaveCompatibilityPolicy
    {
        public const string RetiredCompletedStageId = "stage-5-1";

        public static bool IsRetiredCompletedStageId(StageId stageId)
        {
            return stageId.IsValid &&
                   string.Equals(stageId.Value, RetiredCompletedStageId, StringComparison.Ordinal);
        }
    }
}
