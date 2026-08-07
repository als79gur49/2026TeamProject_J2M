using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public static class TerminalResultTextDescriptors
    {
        public static readonly LocalizedTextDescriptor Continue =
            Create(TerminalResultLocalizationEntryId.Continue);

        public static readonly LocalizedTextDescriptor StageClearTitle =
            Create(TerminalResultLocalizationEntryId.StageClearTitle);

        public static readonly LocalizedTextDescriptor LevelFailedTitle =
            Create(TerminalResultLocalizationEntryId.LevelFailedTitle);

        public static readonly LocalizedTextDescriptor ChancesExhaustedDetail =
            Create(TerminalResultLocalizationEntryId.ChancesExhaustedDetail);

        public static readonly LocalizedTextDescriptor RestartStage =
            Create(TerminalResultLocalizationEntryId.RestartStage);

        public static readonly LocalizedTextDescriptor MainMenu =
            Create(TerminalResultLocalizationEntryId.MainMenu);

        public static readonly LocalizedTextDescriptor GameClearTitle =
            Create(TerminalResultLocalizationEntryId.GameClearTitle);

        public static LocalizedTextDescriptor DetailFor(GameplayLevelFailureReason reason)
        {
            return reason == GameplayLevelFailureReason.ChancesExhausted
                ? ChancesExhaustedDetail
                : default;
        }

        private static LocalizedTextDescriptor Create(TerminalResultLocalizationEntryId id)
        {
            foreach (var entry in TerminalResultLocalizationContract.Entries)
            {
                if (entry.Id == id)
                {
                    return new LocalizedTextDescriptor(
                        entry.Table,
                        entry.Key,
                        entry.Role,
                        entry.Weight);
                }
            }

            return default;
        }
    }

    public static class SceneTransitionTextDescriptors
    {
        public static readonly LocalizedTextDescriptor RemainingChances =
            Create(SceneTransitionLocalizationEntryId.RemainingChances);

        public static readonly LocalizedTextDescriptor Loading =
            Create(SceneTransitionLocalizationEntryId.Loading);

        private static LocalizedTextDescriptor Create(SceneTransitionLocalizationEntryId id)
        {
            foreach (var entry in SceneTransitionLocalizationContract.Entries)
            {
                if (entry.Id == id)
                {
                    return new LocalizedTextDescriptor(
                        entry.Table,
                        entry.Key,
                        entry.Role,
                        entry.Weight);
                }
            }

            return default;
        }
    }
}
