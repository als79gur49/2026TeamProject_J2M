using System;
using System.Collections.Generic;
using Game.Feature.Stages;

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
            bool showRestart,
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
            ShowRestart = showRestart;
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

        public bool ShowRestart { get; }

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

    public static class MainMenuSlotViewModelMapper
    {
        public static SaveSlotPanelViewModel Map(
            IReadOnlyList<SaveSlotData> slots,
            CampaignStageSequenceResolver sequenceResolver)
        {
            return Map(slots, sequenceResolver, validationService: null);
        }

        public static SaveSlotPanelViewModel Map(
            IReadOnlyList<SaveSlotData> slots,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotValidationService validationService)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var cards = new List<SaveSlotCardViewModel>(SaveSlotStore.SlotCount);
            for (var slotNumber = 1; slotNumber <= SaveSlotStore.SlotCount; slotNumber++)
            {
                var slot = ResolveSlot(slots, slotNumber);
                var validation = validationService != null
                    ? validationService.Validate(slot)
                    : default;
                cards.Add(MapSlot(slot, sequenceResolver, validationService != null ? validation : (SaveSlotValidationResult?)null));
            }

            return new SaveSlotPanelViewModel(cards);
        }

        public static SaveSlotCardViewModel MapSlot(
            SaveSlotData slot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            return MapSlot(slot, sequenceResolver, null);
        }

        public static SaveSlotCardViewModel MapSlot(
            SaveSlotData slot,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotValidationResult? validationResult)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            var title = $"Slot {slot.SlotNumber}";
            if (slot.IsEmpty)
            {
                return new SaveSlotCardViewModel(
                    slot.SlotNumber,
                    SaveSlotCardState.Empty,
                    title,
                    "Empty",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "New Game",
                    SaveSlotIntentKind.NewGame,
                    showRestart: false,
                    showDelete: false);
            }

            var validation = validationResult ?? new SaveSlotValidationResult(
                slot,
                slot.CampaignCompleted ? SaveSlotValidationStatus.Completed : SaveSlotValidationStatus.Valid,
                sequenceResolver != null ? sequenceResolver.GetLevelGroupId(slot.CurrentStageId) : slot.CurrentLevelGroupId,
                levelGroupWasSynced: false);
            var displayStage = sequenceResolver != null
                ? sequenceResolver.GetDisplayName(slot.CurrentStageId)
                : slot.CurrentStageId.Value;
            if (string.IsNullOrWhiteSpace(displayStage) && slot.CurrentStageId.IsValid)
            {
                displayStage = slot.CurrentStageId.Value;
            }

            if (validation.Status == SaveSlotValidationStatus.Completed)
            {
                return new SaveSlotCardViewModel(
                    slot.SlotNumber,
                    SaveSlotCardState.Completed,
                    title,
                    "Completed",
                    string.IsNullOrWhiteSpace(displayStage) ? string.Empty : $"Stage {displayStage}",
                    $"Chances {slot.RemainingChances}",
                    $"Deaths {slot.TotalDeaths}",
                    FormatLastPlayedText(slot.LastPlayedAt),
                    "Restart",
                    SaveSlotIntentKind.Restart,
                    showRestart: true,
                    showDelete: true);
            }

            if (!validation.CanContinue)
            {
                var isUnsupported = validation.Status == SaveSlotValidationStatus.UnsupportedVersion;
                return new SaveSlotCardViewModel(
                    slot.SlotNumber,
                    isUnsupported ? SaveSlotCardState.Unsupported : SaveSlotCardState.Corrupted,
                    title,
                    isUnsupported ? "Unsupported" : "Needs Repair",
                    ResolveInvalidStageText(slot, displayStage, validation.Status),
                    string.Empty,
                    $"Deaths {slot.TotalDeaths}",
                    FormatLastPlayedText(slot.LastPlayedAt),
                    "Restart",
                    SaveSlotIntentKind.Restart,
                    showRestart: true,
                    showDelete: true);
            }

            return new SaveSlotCardViewModel(
                slot.SlotNumber,
                SaveSlotCardState.Existing,
                title,
                "Continue",
                string.IsNullOrWhiteSpace(displayStage) ? string.Empty : $"Stage {displayStage}",
                $"Chances {slot.RemainingChances}",
                $"Deaths {slot.TotalDeaths}",
                FormatLastPlayedText(slot.LastPlayedAt),
                "Continue",
                SaveSlotIntentKind.Continue,
                showRestart: false,
                showDelete: true);
        }

        private static string FormatLastPlayedText(string lastPlayedAt)
        {
            if (string.IsNullOrWhiteSpace(lastPlayedAt))
            {
                return string.Empty;
            }

            if (DateTimeOffset.TryParse(lastPlayedAt, out var playedAt))
            {
                return $"Played {playedAt.LocalDateTime:yyyy-MM-dd HH:mm}";
            }

            return lastPlayedAt;
        }

        private static string ResolveInvalidStageText(
            SaveSlotData slot,
            string displayStage,
            SaveSlotValidationStatus status)
        {
            if (!slot.CurrentStageId.IsValid)
            {
                return "Invalid stage";
            }

            if (!string.IsNullOrWhiteSpace(displayStage))
            {
                return $"Stage {displayStage}";
            }

            return status == SaveSlotValidationStatus.StageMissingFromCatalog ||
                   status == SaveSlotValidationStatus.StageMissingFromSequence
                ? $"Stage {slot.CurrentStageId.Value}"
                : "Invalid stage";
        }

        private static SaveSlotData ResolveSlot(IReadOnlyList<SaveSlotData> slots, int slotNumber)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].SlotNumber == slotNumber)
                {
                    return slots[i];
                }
            }

            return SaveSlotData.CreateEmpty(slotNumber);
        }
    }
}
