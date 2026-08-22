using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public static class MainMenuSlotViewModelMapper
    {
        public static SaveSlotPanelViewModel Map(
            IReadOnlyList<SaveSlotData> slots,
            CampaignStageSequenceResolver sequenceResolver)
        {
            return Map(
                slots,
                sequenceResolver,
                validationService: null,
                InvariantSettingsLocalizedTextResolver.Instance);
        }

        public static SaveSlotPanelViewModel Map(
            IReadOnlyList<SaveSlotData> slots,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotValidationService validationService)
        {
            return Map(
                slots,
                sequenceResolver,
                validationService,
                InvariantSettingsLocalizedTextResolver.Instance);
        }

        public static SaveSlotPanelViewModel Map(
            IReadOnlyList<SaveSlotData> slots,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotValidationService validationService,
            ILocalizedTextResolver localizedTextResolver)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            if (localizedTextResolver == null)
            {
                throw new ArgumentNullException(nameof(localizedTextResolver));
            }

            var cards = new List<SaveSlotCardViewModel>(CampaignSaveSlotPolicy.SlotCount);
            for (var slotNumber = 1; slotNumber <= CampaignSaveSlotPolicy.SlotCount; slotNumber++)
            {
                var slot = ResolveSlot(slots, slotNumber);
                var validation = validationService != null
                    ? validationService.Validate(slot)
                    : default;
                cards.Add(MapSlot(
                    slot,
                    sequenceResolver,
                    validationService != null ? validation : (SaveSlotValidationResult?)null,
                    localizedTextResolver));
            }

            return new SaveSlotPanelViewModel(cards);
        }

        public static SaveSlotPanelViewModel MapCampaignAccessBlocked(CampaignSaveLoadReport report)
        {
            return MapCampaignAccessBlocked(
                report,
                InvariantSettingsLocalizedTextResolver.Instance);
        }

        public static SaveSlotPanelViewModel MapCampaignAccessBlocked(
            CampaignSaveLoadReport report,
            ILocalizedTextResolver localizedTextResolver)
        {
            if (localizedTextResolver == null)
            {
                throw new ArgumentNullException(nameof(localizedTextResolver));
            }

            var failureKind = MapFailureKind(report.Status);
            if (failureKind == SaveSlotFailurePresentationKind.None)
            {
                failureKind = SaveSlotFailurePresentationKind.NeedsRepair;
            }

            var failureText = MainMenuLocalization.FailureDescriptor(failureKind);
            var failureTitle = localizedTextResolver.Resolve(failureText.Title);
            var failureDetail = localizedTextResolver.Resolve(failureText.Detail);
            var recoveryActions = CampaignSaveRecoveryPolicy.GetActions(report.Status);
            return new SaveSlotPanelViewModel(
                Array.Empty<SaveSlotCardViewModel>(),
                new CampaignSaveBlockedViewModel(
                    failureKind,
                    failureTitle,
                    failureDetail,
                    showRetry: (recoveryActions & CampaignSaveRecoveryActions.Retry) != 0,
                    showResetProfile: (recoveryActions & CampaignSaveRecoveryActions.ResetProfile) != 0,
                    retryActionText: MainMenuLocalization.Resolve(
                        localizedTextResolver,
                        MainMenuLocalizationEntryId.SaveRecoveryRetry),
                    resetProfileActionText: MainMenuLocalization.Resolve(
                        localizedTextResolver,
                        MainMenuLocalizationEntryId.SaveRecoveryReset)));
        }

        public static SaveSlotPanelViewModel MapRepairRequired(CampaignSaveLoadReport report)
        {
            return MapCampaignAccessBlocked(report);
        }

        public static SaveSlotCardViewModel MapSlot(
            SaveSlotData slot,
            CampaignStageSequenceResolver sequenceResolver)
        {
            return MapSlot(
                slot,
                sequenceResolver,
                null,
                InvariantSettingsLocalizedTextResolver.Instance);
        }

        public static SaveSlotCardViewModel MapSlot(
            SaveSlotData slot,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotValidationResult? validationResult)
        {
            return MapSlot(
                slot,
                sequenceResolver,
                validationResult,
                InvariantSettingsLocalizedTextResolver.Instance);
        }

        public static SaveSlotCardViewModel MapSlot(
            SaveSlotData slot,
            CampaignStageSequenceResolver sequenceResolver,
            SaveSlotValidationResult? validationResult,
            ILocalizedTextResolver localizedTextResolver)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (localizedTextResolver == null)
            {
                throw new ArgumentNullException(nameof(localizedTextResolver));
            }

            var title = MainMenuLocalization.Resolve(
                localizedTextResolver,
                MainMenuLocalizationEntryId.SlotLabel,
                slot.SlotNumber);
            var deleteAction = MainMenuLocalization.Resolve(
                localizedTextResolver,
                MainMenuLocalizationEntryId.SlotDelete);
            if (slot.IsEmpty)
            {
                return new SaveSlotCardViewModel(
                    slot.SlotNumber,
                    SaveSlotCardState.Empty,
                    title,
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotEmpty),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotNewGame),
                    SaveSlotIntentKind.NewGame,
                    showDelete: false,
                    deleteActionText: deleteAction);
            }

            var validation = validationResult ?? new SaveSlotValidationResult(
                slot,
                slot.CampaignCompleted ? SaveSlotValidationStatus.Completed : SaveSlotValidationStatus.Valid,
                sequenceResolver != null ? sequenceResolver.GetLevelGroupId(slot.CurrentStageId) : slot.CurrentLevelGroupId,
                levelGroupWasSynced: false);
            var displayStage = slot.CurrentStageId.IsValid
                ? localizedTextResolver.Resolve(StageDisplayNameTextDescriptors.ForStage(slot.CurrentStageId))
                : string.Empty;

            if (validation.Status == SaveSlotValidationStatus.Completed)
            {
                return new SaveSlotCardViewModel(
                    slot.SlotNumber,
                    SaveSlotCardState.Completed,
                    title,
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotCompleted),
                    FormatStageText(localizedTextResolver, displayStage),
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotChances, slot.RemainingChances),
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotDeaths, slot.TotalDeaths),
                    FormatLastPlayedText(slot.LastPlayedAt, localizedTextResolver),
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotRestart),
                    SaveSlotIntentKind.Restart,
                    showDelete: true,
                    deleteActionText: deleteAction);
            }

            if (!validation.CanContinue)
            {
                var failureKind = MapFailureKind(validation.Status);
                if (failureKind == SaveSlotFailurePresentationKind.None)
                {
                    failureKind = SaveSlotFailurePresentationKind.NeedsRepair;
                }

                var failureText = MainMenuLocalization.FailureDescriptor(failureKind);
                return new SaveSlotCardViewModel(
                    slot.SlotNumber,
                    failureKind == SaveSlotFailurePresentationKind.UnsupportedVersion
                        ? SaveSlotCardState.Unsupported
                        : SaveSlotCardState.Corrupted,
                    title,
                    localizedTextResolver.Resolve(failureText.Title),
                    localizedTextResolver.Resolve(failureText.Detail),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotRestart),
                    SaveSlotIntentKind.Restart,
                    showDelete: true,
                    deleteActionText: deleteAction,
                    failureKind: failureKind);
            }

            return new SaveSlotCardViewModel(
                slot.SlotNumber,
                SaveSlotCardState.Existing,
                title,
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotContinue),
                FormatStageText(localizedTextResolver, displayStage),
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotChances, slot.RemainingChances),
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotDeaths, slot.TotalDeaths),
                FormatLastPlayedText(slot.LastPlayedAt, localizedTextResolver),
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotContinue),
                SaveSlotIntentKind.Continue,
                showDelete: true,
                deleteActionText: deleteAction);
        }

        private static string FormatStageText(
            ILocalizedTextResolver localizedTextResolver,
            string displayStage)
        {
            return string.IsNullOrWhiteSpace(displayStage)
                ? string.Empty
                : MainMenuLocalization.Resolve(
                    localizedTextResolver,
                    MainMenuLocalizationEntryId.SlotStage,
                    displayStage);
        }

        private static string FormatLastPlayedText(
            string lastPlayedAt,
            ILocalizedTextResolver localizedTextResolver)
        {
            var formattedDate = MainMenuLocalization.FormatPlayedDate(
                lastPlayedAt,
                localizedTextResolver.CurrentLocaleCode);
            if (string.IsNullOrWhiteSpace(formattedDate))
            {
                return string.Empty;
            }

            return MainMenuLocalization.Resolve(
                localizedTextResolver,
                MainMenuLocalizationEntryId.SlotPlayed,
                formattedDate);
        }

        public static SaveSlotFailurePresentationKind MapFailureKind(
            CampaignSaveLoadStatus status)
        {
            switch (status)
            {
                case CampaignSaveLoadStatus.CorruptRepairRequired:
                    return SaveSlotFailurePresentationKind.CorruptedData;
                case CampaignSaveLoadStatus.SchemaInvalidRepairRequired:
                    return SaveSlotFailurePresentationKind.UnsupportedVersion;
                case CampaignSaveLoadStatus.Unauthorized:
                    return SaveSlotFailurePresentationKind.PermissionDenied;
                case CampaignSaveLoadStatus.IoFailed:
                    return SaveSlotFailurePresentationKind.LoadFailed;
                case CampaignSaveLoadStatus.RecoveryPending:
                    return SaveSlotFailurePresentationKind.RecoveryPending;
                default:
                    return SaveSlotFailurePresentationKind.None;
            }
        }

        public static SaveSlotFailurePresentationKind MapFailureKind(
            SaveSlotValidationStatus status)
        {
            switch (status)
            {
                case SaveSlotValidationStatus.UnsupportedVersion:
                    return SaveSlotFailurePresentationKind.UnsupportedVersion;
                case SaveSlotValidationStatus.Corrupted:
                    return SaveSlotFailurePresentationKind.CorruptedData;
                case SaveSlotValidationStatus.StageMissingFromSequence:
                case SaveSlotValidationStatus.StageMissingFromCatalog:
                    return SaveSlotFailurePresentationKind.NeedsRepair;
                default:
                    return SaveSlotFailurePresentationKind.None;
            }
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
