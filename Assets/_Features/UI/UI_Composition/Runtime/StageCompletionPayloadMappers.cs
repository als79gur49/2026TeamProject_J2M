using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    public static class StageResultPayloadMapper
    {
        public static StageResultScreenPayload Map(StageCompletionReadModel readModel)
        {
            return StageCompletionStageResultPayloadMapper.Map(readModel);
        }
    }

    public static class RewardPopupPayloadMapper
    {
        public static RewardPopupPayload Map(StageCompletionReadModel readModel)
        {
            return StageCompletionRewardPopupPayloadMapper.Map(readModel);
        }
    }
}
