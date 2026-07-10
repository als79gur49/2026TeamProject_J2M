using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
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

        public static SaveSlotPanelViewModel MapRepairRequired(CampaignSaveLoadReport report)
        {
            var cards = new List<SaveSlotCardViewModel>(SaveSlotStore.SlotCount);
            for (var slotNumber = 1; slotNumber <= SaveSlotStore.SlotCount; slotNumber++)
            {
                cards.Add(new SaveSlotCardViewModel(
                    slotNumber,
                    SaveSlotCardState.Corrupted,
                    $"Slot {slotNumber}",
                    "Needs Repair",
                    string.IsNullOrWhiteSpace(report.Reason) ? "Campaign save unavailable" : report.Reason,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    SaveSlotIntentKind.None,
                    showDelete: false));
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
