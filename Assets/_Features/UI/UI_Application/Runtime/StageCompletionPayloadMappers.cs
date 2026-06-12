using System;
using Game.Feature.Stages;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public static class StageCompletionStageResultPayloadMapper
    {
        public static StageResultScreenPayload Map(MinimalStageCompletionReadModel readModel)
        {
            if (readModel == null)
            {
                throw new ArgumentNullException(nameof(readModel));
            }

            var detailText = !string.IsNullOrWhiteSpace(readModel.PresentationDetail)
                ? readModel.PresentationDetail
                : BuildDefaultDetail(readModel);

            return new StageResultScreenPayload(
                string.IsNullOrWhiteSpace(readModel.PresentationTitle) ? "Stage Cleared" : readModel.PresentationTitle,
                detailText,
                string.IsNullOrWhiteSpace(readModel.ContinueLabel) ? "Continue" : readModel.ContinueLabel,
                readModel.ContinueRequest,
                readModel.RetryRequest,
                readModel.NextStageRequest);
        }

        private static string BuildDefaultDetail(MinimalStageCompletionReadModel readModel)
        {
            return readModel.Result != null
                ? $"Tick {readModel.Result.FinalTickIndex} completed."
                : string.Empty;
        }
    }
}
