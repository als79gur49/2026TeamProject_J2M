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

            return new StageResultScreenPayload(
                string.IsNullOrWhiteSpace(readModel.ContinueLabel) ? "Continue" : readModel.ContinueLabel,
                readModel.ContinueRequest,
                readModel.RetryRequest,
                readModel.NextStageRequest);
        }
    }
}
