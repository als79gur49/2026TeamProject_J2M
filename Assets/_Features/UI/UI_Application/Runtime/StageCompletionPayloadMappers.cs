using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public static class StageCompletionStageResultPayloadMapper
    {
        private static readonly Lazy<CampaignStageSequenceResolver> CanonicalCampaignResolver =
            new(() => new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()));

        public static StageResultScreenPayload Map(MinimalStageCompletionReadModel readModel)
        {
            if (readModel == null)
            {
                throw new ArgumentNullException(nameof(readModel));
            }

            var summaryText = !string.IsNullOrWhiteSpace(readModel.PresentationSummary)
                ? readModel.PresentationSummary
                : readModel.DisplayName;
            var detailText = !string.IsNullOrWhiteSpace(readModel.PresentationDetail)
                ? readModel.PresentationDetail
                : BuildDefaultDetail(readModel);

            return new StageResultScreenPayload(
                string.IsNullOrWhiteSpace(readModel.PresentationTitle) ? "Stage Cleared" : readModel.PresentationTitle,
                summaryText,
                detailText,
                string.IsNullOrWhiteSpace(readModel.ContinueLabel) ? "Continue" : readModel.ContinueLabel,
                readModel.ContinueRequest,
                readModel.RetryRequest,
                readModel.NextStageRequest);
        }

        public static StageResultScreenPayload Map(StageCompletionReadModel readModel)
        {
            if (readModel == null)
            {
                throw new ArgumentNullException(nameof(readModel));
            }

            var summaryText = !string.IsNullOrWhiteSpace(readModel.ResultSummaryText)
                ? readModel.ResultSummaryText
                : BuildDefaultSummary(readModel);
            var detailText = !string.IsNullOrWhiteSpace(readModel.ResultDetailText)
                ? readModel.ResultDetailText
                : BuildDefaultDetail(readModel);

            var nextStageRequest = StageNavigationRequest.None;
            if (CampaignStageResultNavigationStore.TryGet(readModel.StageId, out var campaignNavigationPlan))
            {
                nextStageRequest = campaignNavigationPlan.NextStageRequest;
                CampaignStageResultNavigationStore.Clear();
            }
            else
            {
                nextStageRequest = ResolveCanonicalCampaignNextRequest(readModel.StageId);
            }

            var continueRequest = nextStageRequest.IsValid
                ? nextStageRequest.WithTransitionHint(StageTransitionHint.ForKind(StageTransitionKind.StageClearNext))
                : new StageNavigationRequest(
                    readModel.StageId,
                    StageNavigationKind.Continue,
                    "stage-result-continue",
                    StageTransitionHint.ForKind(StageTransitionKind.StageClearNext));

            var retryRequest = new StageNavigationRequest(
                readModel.StageId,
                StageNavigationKind.Retry,
                "stage-result-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual));

            var typedNextStageRequest = nextStageRequest.IsValid
                ? nextStageRequest.WithTransitionHint(StageTransitionHint.ForKind(StageTransitionKind.StageClearNext))
                : StageNavigationRequest.None;

            return new StageResultScreenPayload(
                string.IsNullOrWhiteSpace(readModel.ResultTitle) ? "Stage Cleared" : readModel.ResultTitle,
                summaryText,
                detailText,
                string.IsNullOrWhiteSpace(readModel.ResultContinueLabel) ? "Continue" : readModel.ResultContinueLabel,
                continueRequest,
                retryRequest,
                typedNextStageRequest);
        }

        private static StageNavigationRequest ResolveCanonicalCampaignNextRequest(StageId stageId)
        {
            var resolver = CanonicalCampaignResolver.Value;
            if (!resolver.Contains(stageId) ||
                resolver.IsFinal(stageId) ||
                !resolver.TryGetNext(stageId, out var nextStageId))
            {
                return StageNavigationRequest.None;
            }

            return new StageNavigationRequest(
                nextStageId,
                StageNavigationKind.NextStage,
                "campaign-auto-next",
                StageTransitionHint.ForKind(StageTransitionKind.StageClearNext));
        }

        private static string BuildDefaultSummary(StageCompletionReadModel readModel)
        {
            return readModel.DisplayName;
        }

        private static string BuildDefaultDetail(StageCompletionReadModel readModel)
        {
            if (readModel.ClearResult == null)
            {
                return string.Empty;
            }

            return $"Tick {readModel.ClearResult.FinalTickIndex} completed.";
        }

        private static string BuildDefaultDetail(MinimalStageCompletionReadModel readModel)
        {
            return readModel.Result != null
                ? $"Tick {readModel.Result.FinalTickIndex} completed."
                : string.Empty;
        }
    }

    public static class StageCompletionRewardPopupPayloadMapper
    {
        public static RewardPopupPayload Map(StageCompletionReadModel readModel)
        {
            if (readModel == null)
            {
                throw new ArgumentNullException(nameof(readModel));
            }

            var items = new List<RewardPopupItemPayload>();
            if (readModel.RewardGrantResult != null)
            {
                for (var i = 0; i < readModel.RewardGrantResult.GrantedRewards.Count; i++)
                {
                    var grant = readModel.RewardGrantResult.GrantedRewards[i];
                    items.Add(new RewardPopupItemPayload(
                        string.IsNullOrWhiteSpace(grant.Reward.RewardId) ? "Reward" : grant.Reward.RewardId,
                        grant.Reward.Amount));
                }
            }

            return new RewardPopupPayload(
                "Rewards",
                items,
                BuildSummary(readModel),
                "Close");
        }

        private static string BuildSummary(StageCompletionReadModel readModel)
        {
            if (readModel.RewardGrantResult == null || !readModel.RewardGrantResult.AnyGranted)
            {
                return "No rewards granted.";
            }

            return readModel.RewardGrantResult.WasFirstClear
                ? "First-clear rewards granted."
                : "Rewards granted for this completion.";
        }
    }
}
