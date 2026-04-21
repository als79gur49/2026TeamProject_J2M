using System;

namespace Game.Feature.Stages
{
    public readonly struct ResolvedStageContent
    {
        public ResolvedStageContent(
            StageLoadSourceMode sourceMode,
            StageId requestedStageId,
            StageContentEntry entry,
            bool usedLaunchContext,
            bool usedDefaultStageIdFallback)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            SourceMode = sourceMode;
            RequestedStageId = requestedStageId;
            Entry = entry;
            UsedLaunchContext = usedLaunchContext;
            UsedDefaultStageIdFallback = usedDefaultStageIdFallback;
        }

        public StageLoadSourceMode SourceMode { get; }

        public StageId RequestedStageId { get; }

        public StageContentEntry Entry { get; }

        public bool UsedLaunchContext { get; }

        public bool UsedDefaultStageIdFallback { get; }
    }
}
