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

    public sealed class MainMenuScreenViewModel
    {
        public MainMenuScreenViewModel(IReadOnlyList<SaveSlotCardViewModel> slotCards)
        {
            SlotCards = slotCards ?? Array.Empty<SaveSlotCardViewModel>();
        }

        public IReadOnlyList<SaveSlotCardViewModel> SlotCards { get; }
    }

    public static class MainMenuSlotViewModelMapper
    {
        public static MainMenuScreenViewModel Map(
            IReadOnlyList<SaveSlotData> slots,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var cards = new List<SaveSlotCardViewModel>(SaveSlotStore.SlotCount);
            for (var slotNumber = 1; slotNumber <= SaveSlotStore.SlotCount; slotNumber++)
            {
                var slot = ResolveSlot(slots, slotNumber);
                cards.Add(MapSlot(slot, sequenceResolver));
            }

            return new MainMenuScreenViewModel(cards);
        }

        public static SaveSlotCardViewModel MapSlot(
            SaveSlotData slot,
            CampaignStageSequenceResolver sequenceResolver)
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

            var displayStage = sequenceResolver != null
                ? sequenceResolver.GetDisplayName(slot.CurrentStageId)
                : slot.CurrentStageId.Value;
            if (string.IsNullOrWhiteSpace(displayStage) && slot.CurrentStageId.IsValid)
            {
                displayStage = slot.CurrentStageId.Value;
            }

            if (slot.CampaignCompleted)
            {
                return new SaveSlotCardViewModel(
                    slot.SlotNumber,
                    SaveSlotCardState.Completed,
                    title,
                    "Completed",
                    "Stage 5-1",
                    $"Chances {slot.RemainingChances}",
                    $"Deaths {slot.TotalDeaths}",
                    slot.LastPlayedAt,
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
                slot.LastPlayedAt,
                "Continue",
                SaveSlotIntentKind.Continue,
                showRestart: false,
                showDelete: true);
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
