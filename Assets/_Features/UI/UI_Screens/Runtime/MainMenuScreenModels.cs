using System;
using System.Collections.Generic;
using Game.Feature.UI.ViewShared;

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

    public enum SaveSlotFailurePresentationKind
    {
        None = 0,
        UnsupportedVersion = 1,
        CorruptedData = 2,
        PermissionDenied = 3,
        LoadFailed = 4,
        NeedsRepair = 5,
        RecoveryPending = 6,
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

    public sealed class MainMenuStaticTextPayload
    {
        public static readonly MainMenuStaticTextPayload Default = new();

        public MainMenuStaticTextPayload(
            LocalizedTextDescriptor startLabelDescriptor = default,
            LocalizedTextDescriptor settingsLabelDescriptor = default,
            LocalizedTextDescriptor quitLabelDescriptor = default)
        {
            StartLabelDescriptor = OrDefault(startLabelDescriptor, MainMenuStaticTextDescriptors.Start);
            SettingsLabelDescriptor = OrDefault(settingsLabelDescriptor, MainMenuStaticTextDescriptors.Settings);
            QuitLabelDescriptor = OrDefault(quitLabelDescriptor, MainMenuStaticTextDescriptors.Quit);
        }

        public LocalizedTextDescriptor StartLabelDescriptor { get; }

        public LocalizedTextDescriptor SettingsLabelDescriptor { get; }

        public LocalizedTextDescriptor QuitLabelDescriptor { get; }

        private static LocalizedTextDescriptor OrDefault(
            LocalizedTextDescriptor descriptor,
            LocalizedTextDescriptor fallback)
        {
            return string.IsNullOrEmpty(descriptor.Table) && string.IsNullOrEmpty(descriptor.Key)
                ? fallback
                : descriptor;
        }
    }

    public static class MainMenuStaticTextDescriptors
    {
        public const string Table = "UI";

        public static readonly LocalizedTextDescriptor Start = new(
            Table,
            "ui.main_menu.start",
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Settings = new(
            Table,
            "ui.common.settings",
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Quit = new(
            Table,
            "ui.main_menu.quit",
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);
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
            bool showDelete,
            string deleteActionText = "",
            SaveSlotFailurePresentationKind failureKind = SaveSlotFailurePresentationKind.None)
        {
            SlotNumber = slotNumber;
            State = state;
            FailureKind = failureKind;
            TitleText = titleText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            StageText = stageText ?? string.Empty;
            ChancesText = chancesText ?? string.Empty;
            DeathsText = deathsText ?? string.Empty;
            LastPlayedText = lastPlayedText ?? string.Empty;
            PrimaryActionText = primaryActionText ?? string.Empty;
            PrimaryIntentKind = primaryIntentKind;
            ShowDelete = showDelete;
            DeleteActionText = deleteActionText ?? string.Empty;
        }

        public int SlotNumber { get; }

        public SaveSlotCardState State { get; }

        public SaveSlotFailurePresentationKind FailureKind { get; }

        public string TitleText { get; }

        public string StatusText { get; }

        public string StageText { get; }

        public string ChancesText { get; }

        public string DeathsText { get; }

        public string LastPlayedText { get; }

        public string PrimaryActionText { get; }

        public SaveSlotIntentKind PrimaryIntentKind { get; }

        public bool ShowDelete { get; }

        public string DeleteActionText { get; }
    }

    public sealed class SaveSlotPanelViewModel
    {
        public SaveSlotPanelViewModel(
            IReadOnlyList<SaveSlotCardViewModel> slotCards,
            CampaignSaveBlockedViewModel blockedState = null)
        {
            SlotCards = slotCards ?? Array.Empty<SaveSlotCardViewModel>();
            BlockedState = blockedState;
        }

        public IReadOnlyList<SaveSlotCardViewModel> SlotCards { get; }

        public CampaignSaveBlockedViewModel BlockedState { get; }

        public bool IsBlocked => BlockedState != null;
    }

    public sealed class CampaignSaveBlockedViewModel
    {
        public CampaignSaveBlockedViewModel(
            SaveSlotFailurePresentationKind failureKind,
            string titleText,
            string detailText,
            bool showRetry,
            bool showResetProfile,
            string retryActionText,
            string resetProfileActionText)
        {
            FailureKind = failureKind;
            TitleText = titleText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            ShowRetry = showRetry;
            ShowResetProfile = showResetProfile;
            RetryActionText = retryActionText ?? string.Empty;
            ResetProfileActionText = resetProfileActionText ?? string.Empty;
        }

        public SaveSlotFailurePresentationKind FailureKind { get; }

        public string TitleText { get; }

        public string DetailText { get; }

        public bool ShowRetry { get; }

        public bool ShowResetProfile { get; }

        public string RetryActionText { get; }

        public string ResetProfileActionText { get; }
    }
}
