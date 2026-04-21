using System;

namespace Game.Feature.Stages
{
    public sealed class StageLoadStrategyFactory
    {
        private static readonly CatalogResolvedStageIdStrategy CatalogResolvedStageIdStrategy = new();
        private static readonly SerializedStageContentEntryStrategy SerializedStageContentEntryStrategy = new();
        private static readonly LegacyStageDefinitionStrategy LegacyStageDefinitionStrategy = new();

        public IStageLoadStrategy Create(StageLoadSourceMode sourceMode)
        {
            switch (sourceMode)
            {
                case StageLoadSourceMode.CatalogResolvedStageId:
                    return CatalogResolvedStageIdStrategy;
                case StageLoadSourceMode.SerializedStageContentEntry:
                    return SerializedStageContentEntryStrategy;
                case StageLoadSourceMode.LegacyStageDefinition:
                    return LegacyStageDefinitionStrategy;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, "Unsupported stage load source mode.");
            }
        }
    }
}
