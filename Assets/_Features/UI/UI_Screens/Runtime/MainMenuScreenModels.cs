using System;
using System.Collections.Generic;

namespace Game.Feature.UI.Screens
{
    public enum SaveSlotCardState
    {
        Empty = 0,
        Existing = 1,
        Completed = 2,
        Corrupted = 3,
        Unsupported = 4,
    }

    public enum SaveSlotIntentKind
    {
        None = 0,
        NewGame = 1,
        Continue = 2,
        Restart = 3,
        Delete = 4,
    }

    public readonly struct SaveSlotIntent
    {
        public SaveSlotIntent(int slotNumber, SaveSlotIntentKind intentKind)
        {
            SlotNumber = slotNumber;
            IntentKind = intentKind;
        }

        public int SlotNumber { get; }

        public SaveSlotIntentKind IntentKind { get; }
    }

    public enum MainMenuSectionId
    {
        None = 0,
        SaveSlots = 1,
        History = 2,
        Friends = 3,
        Profile = 4,
        Achievements = 5,
        Collection = 6,
        Credits = 7,
    }

    public readonly struct MainMenuNavigationIntent
    {
        public MainMenuNavigationIntent(MainMenuSectionId sectionId)
        {
            SectionId = sectionId;
        }

        public MainMenuSectionId SectionId { get; }
    }

    public enum MainMenuCommandKind
    {
        None = 0,
        OpenSettings = 1,
        Quit = 2,
    }

    public readonly struct MainMenuCommandIntent
    {
        public MainMenuCommandIntent(MainMenuCommandKind commandKind)
        {
            CommandKind = commandKind;
        }

        public MainMenuCommandKind CommandKind { get; }
    }

    public sealed class SaveSlotCardViewModel
    {
        public SaveSlotCardViewModel(
            int slotNumber,
            SaveSlotCardState state,
            string titleText,
            string statusText,
            string stageText,
            string chancesText,
            string deathsText,
            string lastPlayedText,
            string primaryActionText,
            SaveSlotIntentKind primaryIntentKind,
            bool showDelete)
        {
            SlotNumber = slotNumber;
            State = state;
            TitleText = titleText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            StageText = stageText ?? string.Empty;
            ChancesText = chancesText ?? string.Empty;
            DeathsText = deathsText ?? string.Empty;
            LastPlayedText = lastPlayedText ?? string.Empty;
            PrimaryActionText = primaryActionText ?? string.Empty;
            PrimaryIntentKind = primaryIntentKind;
            ShowDelete = showDelete;
        }

        public int SlotNumber { get; }

        public SaveSlotCardState State { get; }

        public string TitleText { get; }

        public string StatusText { get; }

        public string StageText { get; }

        public string ChancesText { get; }

        public string DeathsText { get; }

        public string LastPlayedText { get; }

        public string PrimaryActionText { get; }

        public SaveSlotIntentKind PrimaryIntentKind { get; }

        public bool ShowDelete { get; }
    }

    public sealed class SaveSlotPanelViewModel
    {
        public SaveSlotPanelViewModel(IReadOnlyList<SaveSlotCardViewModel> slotCards)
        {
            SlotCards = slotCards ?? Array.Empty<SaveSlotCardViewModel>();
        }

        public IReadOnlyList<SaveSlotCardViewModel> SlotCards { get; }
    }
}
