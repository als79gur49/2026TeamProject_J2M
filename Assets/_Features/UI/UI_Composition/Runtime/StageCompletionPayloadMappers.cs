using Game.Feature.UI.Application;
using Game.Feature.UI.Screens;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    public static class StageResultPayloadMapper
    {
        public static StageResultScreenPayload Map(MinimalStageCompletionReadModel readModel)
        {
            return StageCompletionStageResultPayloadMapper.Map(readModel);
        }
    }
}
