using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Composition
{
    public static class StageResultPayloadMapper
    {
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

            return new StageResultScreenPayload(
                string.IsNullOrWhiteSpace(readModel.ResultTitle) ? "Stage Cleared" : readModel.ResultTitle,
                summaryText,
                detailText,
                string.IsNullOrWhiteSpace(readModel.ResultContinueLabel) ? "Continue" : readModel.ResultContinueLabel);
        }

        private static string BuildDefaultSummary(StageCompletionReadModel readModel)
        {
            if (readModel.ClearEvaluationResult == null)
            {
                return readModel.DisplayName;
            }

            return $"{readModel.DisplayName} | Score {readModel.ClearEvaluationResult.Score} | Stars {readModel.ClearEvaluationResult.StarsEarned}";
        }

        private static string BuildDefaultDetail(StageCompletionReadModel readModel)
        {
            if (readModel.ClearResult == null || readModel.ClearEvaluationResult == null)
            {
                return string.Empty;
            }

            return $"Tick {readModel.ClearResult.FinalTickIndex} completed. Rank {readModel.ClearEvaluationResult.RankId}.";
        }
    }

    public static class RewardPopupPayloadMapper
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
