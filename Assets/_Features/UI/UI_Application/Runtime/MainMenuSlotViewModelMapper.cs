using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public sealed class MainMenuSlotPresentationInput
    {
        public MainMenuSlotPresentationInput(
            CampaignSlotEntry entry,
            CampaignSlotLaunchEvaluation launchEvaluation,
            CampaignSlotActionPolicy actionPolicy)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            var expectedActionPolicy = CampaignSlotActionPolicy.Evaluate(launchEvaluation);
            if (expectedActionPolicy.CanContinue != actionPolicy.CanContinue ||
                expectedActionPolicy.CanRestart != actionPolicy.CanRestart ||
                expectedActionPolicy.CanDelete != actionPolicy.CanDelete)
            {
                throw new ArgumentException(
                    "The slot action policy must be derived from the supplied launch evaluation.",
                    nameof(actionPolicy));
            }

            if (entry.IsEmpty)
            {
                if (launchEvaluation.Status != CampaignSlotLaunchStatus.Empty ||
                    launchEvaluation.State != null)
                {
                    throw new ArgumentException(
                        "An empty slot entry requires an empty launch evaluation.",
                        nameof(launchEvaluation));
                }
            }
            else if (launchEvaluation.Status == CampaignSlotLaunchStatus.Empty ||
                     !ReferenceEquals(entry.State, launchEvaluation.State))
            {
                throw new ArgumentException(
                    "An occupied slot entry and launch evaluation must carry the same immutable state.",
                    nameof(launchEvaluation));
            }

            LaunchEvaluation = launchEvaluation;
            ActionPolicy = actionPolicy;
        }

        public CampaignSlotEntry Entry { get; }

        public CampaignSlotLaunchEvaluation LaunchEvaluation { get; }

        public CampaignSlotActionPolicy ActionPolicy { get; }
    }

    public static class MainMenuSlotViewModelMapper
    {
        public static SaveSlotPanelViewModel Map(
            IReadOnlyList<MainMenuSlotPresentationInput> slots)
        {
            return Map(
                slots,
                InvariantSettingsLocalizedTextResolver.Instance);
        }

        public static SaveSlotPanelViewModel Map(
            IReadOnlyList<MainMenuSlotPresentationInput> slots,
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
                cards.Add(MapSlot(
                    slot.Entry,
                    slot.LaunchEvaluation,
                    slot.ActionPolicy,
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
            CampaignSlotEntry slot,
            CampaignSlotLaunchEvaluation launchEvaluation,
            CampaignSlotActionPolicy actionPolicy)
        {
            return MapSlot(
                slot,
                launchEvaluation,
                actionPolicy,
                InvariantSettingsLocalizedTextResolver.Instance);
        }

        public static SaveSlotCardViewModel MapSlot(
            CampaignSlotEntry slot,
            CampaignSlotLaunchEvaluation launchEvaluation,
            CampaignSlotActionPolicy actionPolicy,
            ILocalizedTextResolver localizedTextResolver)
        {
            var input = new MainMenuSlotPresentationInput(
                slot,
                launchEvaluation,
                actionPolicy);

            if (localizedTextResolver == null)
            {
                throw new ArgumentNullException(nameof(localizedTextResolver));
            }

            var entry = input.Entry;
            var title = MainMenuLocalization.Resolve(
                localizedTextResolver,
                MainMenuLocalizationEntryId.SlotLabel,
                entry.SlotNumber);
            var deleteAction = MainMenuLocalization.Resolve(
                localizedTextResolver,
                MainMenuLocalizationEntryId.SlotDelete);
            if (entry.IsEmpty)
            {
                return new SaveSlotCardViewModel(
                    entry.SlotNumber,
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

            var evaluation = input.LaunchEvaluation;
            var action = input.ActionPolicy;
            var state = evaluation.State;
            var displayStage = evaluation.ResolvedStageId.IsValid
                ? localizedTextResolver.Resolve(StageDisplayNameTextDescriptors.ForStage(evaluation.ResolvedStageId))
                : string.Empty;

            if (state.CampaignCompleted)
            {
                return new SaveSlotCardViewModel(
                    entry.SlotNumber,
                    SaveSlotCardState.Completed,
                    title,
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotCompleted),
                    FormatStageText(localizedTextResolver, displayStage),
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotChances, state.RemainingChances),
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotDeaths, state.TotalDeaths),
                    FormatLastPlayedText(state.LastPlayedAt, localizedTextResolver),
                    MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotRestart),
                    SaveSlotIntentKind.Restart,
                    showDelete: action.CanDelete,
                    deleteActionText: deleteAction);
            }

            if (!action.CanContinue)
            {
                var failureKind = MapFailureKind(evaluation.Status);
                if (failureKind == SaveSlotFailurePresentationKind.None)
                {
                    failureKind = SaveSlotFailurePresentationKind.NeedsRepair;
                }

                var failureText = MainMenuLocalization.FailureDescriptor(failureKind);
                return new SaveSlotCardViewModel(
                    entry.SlotNumber,
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
                    showDelete: action.CanDelete,
                    deleteActionText: deleteAction,
                    failureKind: failureKind);
            }

            return new SaveSlotCardViewModel(
                entry.SlotNumber,
                SaveSlotCardState.Existing,
                title,
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotContinue),
                FormatStageText(localizedTextResolver, displayStage),
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotChances, state.RemainingChances),
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotDeaths, state.TotalDeaths),
                FormatLastPlayedText(state.LastPlayedAt, localizedTextResolver),
                MainMenuLocalization.Resolve(localizedTextResolver, MainMenuLocalizationEntryId.SlotContinue),
                SaveSlotIntentKind.Continue,
                showDelete: action.CanDelete,
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
            CampaignSlotLaunchStatus status)
        {
            switch (status)
            {
                case CampaignSlotLaunchStatus.StageMissingFromSequence:
                case CampaignSlotLaunchStatus.StageMissingFromCatalog:
                    return SaveSlotFailurePresentationKind.NeedsRepair;
                default:
                    return SaveSlotFailurePresentationKind.None;
            }
        }

        private static MainMenuSlotPresentationInput ResolveSlot(
            IReadOnlyList<MainMenuSlotPresentationInput> slots,
            int slotNumber)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Entry.SlotNumber == slotNumber)
                {
                    return slots[i];
                }
            }

            throw new ArgumentException(
                $"Slot presentation input {slotNumber} is missing.",
                nameof(slots));
        }
    }
}
