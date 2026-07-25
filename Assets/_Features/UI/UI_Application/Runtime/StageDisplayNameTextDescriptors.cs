using Game.Feature.Stages;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public static class StageDisplayNameTextDescriptors
    {
        public static LocalizedTextDescriptor Create(string displayNameKey)
        {
            return new LocalizedTextDescriptor(
                StageDisplayNameKeys.Table,
                StageDisplayNameKeys.Normalize(displayNameKey),
                LocalizedTextRole.Label);
        }

        public static LocalizedTextDescriptor ForStage(StageId stageId)
        {
            return Create(StageDisplayNameKeys.ForStage(stageId));
        }
    }
}
