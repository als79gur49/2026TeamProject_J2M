using System;

namespace Game.Feature.Stages
{
    public readonly struct CampaignStageResultNavigationPlan
    {
        public CampaignStageResultNavigationPlan(
            StageId completedStageId,
            StageNavigationRequest nextStageRequest,
            bool campaignCompleted)
        {
            CompletedStageId = completedStageId;
            NextStageRequest = nextStageRequest;
            CampaignCompleted = campaignCompleted;
        }

        public StageId CompletedStageId { get; }

        public StageNavigationRequest NextStageRequest { get; }

        public bool CampaignCompleted { get; }
    }

    public static class CampaignStageResultNavigationStore
    {
        private static CampaignStageResultNavigationPlan _currentPlan;
        private static bool _hasCurrentPlan;

        public static void Set(CampaignStageResultNavigationPlan plan)
        {
            if (!plan.CompletedStageId.IsValid)
            {
                throw new ArgumentException("Campaign stage result navigation requires a completed stage id.", nameof(plan));
            }

            _currentPlan = plan;
            _hasCurrentPlan = true;
        }

        public static bool TryGet(StageId completedStageId, out CampaignStageResultNavigationPlan plan)
        {
            if (_hasCurrentPlan && _currentPlan.CompletedStageId.Equals(completedStageId))
            {
                plan = _currentPlan;
                return true;
            }

            plan = default;
            return false;
        }

        public static void Clear()
        {
            _currentPlan = default;
            _hasCurrentPlan = false;
        }
    }
}
