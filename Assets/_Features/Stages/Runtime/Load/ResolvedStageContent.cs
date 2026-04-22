using System;

namespace Game.Feature.Stages
{
    public readonly struct ResolvedStageContent
    {
        public ResolvedStageContent(
            StageId requestedStageId,
            StageContentEntry entry,
            bool usedLaunchContext)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            RequestedStageId = requestedStageId;
            Entry = entry;
            UsedLaunchContext = usedLaunchContext;
        }

        public StageId RequestedStageId { get; }

        public StageContentEntry Entry { get; }

        public bool UsedLaunchContext { get; }
    }
}
