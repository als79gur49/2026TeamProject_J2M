using System;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public interface IConfirmPopupPort
    {
        void Request(ConfirmPopupPayload payload, Action<bool> completion);
    }

    public sealed class MainMenuController
    {
        private readonly IConfirmPopupPort _confirmPopupPort;
        private readonly ICampaignLaunchHandoffStore _launchHandoffStore;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly ICampaignSaveSlotStore _saveSlotStore;
        private readonly SaveSlotValidationService _saveSlotValidationService;
        private readonly CampaignStageSequenceResolver _sequenceResolver;

        public MainMenuController(
            ICampaignSaveSlotStore saveSlotStore,
            ICampaignLaunchHandoffStore launchHandoffStore,
            CampaignStageSequenceResolver sequenceResolver,
            IStageLaunchRouter stageLaunchRouter,
            IConfirmPopupPort confirmPopupPort,
            SaveSlotValidationService saveSlotValidationService = null)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _launchHandoffStore = launchHandoffStore ??
                throw new ArgumentNullException(nameof(launchHandoffStore));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _confirmPopupPort = confirmPopupPort ?? throw new ArgumentNullException(nameof(confirmPopupPort));
            _saveSlotValidationService = saveSlotValidationService;
        }

        public event Action<SaveSlotPanelViewModel> ViewModelChanged;

        public SaveSlotPanelViewModel BuildViewModel()
        {
            var loadResult = _saveSlotStore.LoadAllWithReport();
            if (loadResult.Report.BlocksCampaignAccess)
            {
                return MainMenuSlotViewModelMapper.MapCampaignAccessBlocked(loadResult.Report);
            }

            return MainMenuSlotViewModelMapper.Map(
                loadResult.Slots,
                _sequenceResolver,
                _saveSlotValidationService);
        }

        public void HandleIntent(SaveSlotIntent intent)
        {
            switch (intent.IntentKind)
            {
                case SaveSlotIntentKind.NewGame:
                    StartNewGame(intent.SlotNumber, confirmIfOccupied: true);
                    break;

                case SaveSlotIntentKind.Continue:
                    Continue(intent.SlotNumber);
                    break;

                case SaveSlotIntentKind.Restart:
                    RequestRestart(intent.SlotNumber);
                    break;

                case SaveSlotIntentKind.Delete:
                    RequestDelete(intent.SlotNumber);
                    break;
            }
        }

        public void Continue(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            var validation = ValidateAndSync(slotNumber);
            if (validation.Status == SaveSlotValidationStatus.Empty)
            {
                StartNewGame(slotNumber, confirmIfOccupied: false);
                return;
            }

            if (!validation.CanContinue)
            {
                RefreshViewModel();
                return;
            }

            BeginLaunch(
                slotNumber,
                validation.Slot.CurrentStageId,
                StageNavigationKind.Continue,
                "main-menu-continue");
        }

        public void RequestRestart(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            _confirmPopupPort.Request(
                new ConfirmPopupPayload(
                    "Restart Slot",
                    $"Restart slot {slotNumber}? Existing campaign progress will be overwritten.",
                    "Restart",
                    "Cancel",
                    true),
                confirmed =>
                {
                    if (confirmed)
                    {
                        StartNewGame(slotNumber, confirmIfOccupied: false);
                    }
                });
        }

        public void RequestDelete(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            _confirmPopupPort.Request(
                new ConfirmPopupPayload(
                    "Delete Slot",
                    $"Delete slot {slotNumber}? This cannot be undone.",
                    "Delete",
                    "Cancel",
                    true),
                confirmed =>
                {
                    try
                    {
                        if (!confirmed)
                        {
                            return;
                        }

                        if (IsCampaignAccessBlocked())
                        {
                            return;
                        }

                        _saveSlotStore.DeleteSlot(slotNumber);
                        if (_launchHandoffStore.TryPeek(out var pendingHandoff) &&
                            pendingHandoff.SlotNumber == slotNumber)
                        {
                            _launchHandoffStore.TryClear(pendingHandoff.Token);
                        }
                    }
                    finally
                    {
                        RefreshViewModel();
                    }
                });
        }

        private void StartNewGame(int slotNumber, bool confirmIfOccupied)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            if (_launchHandoffStore.TryPeek(out _))
            {
                RefreshViewModel();
                return;
            }

            var existingValidation = ValidateAndSync(slotNumber);
            if (confirmIfOccupied && existingValidation.Status != SaveSlotValidationStatus.Empty)
            {
                _confirmPopupPort.Request(
                    new ConfirmPopupPayload(
                        "Overwrite Slot",
                        $"Overwrite slot {slotNumber}? Existing campaign progress will be replaced.",
                        "Overwrite",
                        "Cancel",
                        true),
                    confirmed =>
                    {
                        if (confirmed)
                        {
                            StartNewGame(slotNumber, confirmIfOccupied: false);
                        }
                    });
                return;
            }

            try
            {
                _saveSlotStore.InitializeNewGame(
                    slotNumber,
                    _sequenceResolver,
                    DateTimeOffset.UtcNow.ToString("O"));
                var validation = ValidateAndSync(slotNumber);
                if (!validation.CanContinue)
                {
                    return;
                }

                BeginLaunch(
                    slotNumber,
                    validation.Slot.CurrentStageId,
                    StageNavigationKind.Continue,
                    "main-menu-new-game");
            }
            finally
            {
                RefreshViewModel();
            }
        }

        private bool IsCampaignAccessBlocked()
        {
            if (_saveSlotStore.LastCampaignLoadReport.BlocksCampaignAccess)
            {
                return true;
            }

            return _saveSlotStore.LoadAllWithReport().Report.BlocksCampaignAccess;
        }

        private void BeginLaunch(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source)
        {
            if (!_launchHandoffStore.TryBegin(
                    slotNumber,
                    stageId,
                    navigationKind,
                    source,
                    out var handoff))
            {
                RefreshViewModel();
                return;
            }

            CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
            {
                Source = source,
                RequestedStageId = stageId.IsValid ? stageId.Value : string.Empty,
                HasLaunchHandoff = true,
                HandoffSlotNumber = handoff.SlotNumber,
                HandoffToken = handoff.Token.ToString("N"),
                SaveSlotStoreKey = _saveSlotStore.DiagnosticsKey,
            });
            try
            {
                _stageLaunchRouter.Launch(new StageNavigationRequest(
                    stageId,
                    navigationKind,
                    source,
                    StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay)));
            }
            catch
            {
                _launchHandoffStore.TryClear(handoff.Token);
                throw;
            }
        }

        private SaveSlotValidationResult ValidateAndSync(int slotNumber)
        {
            return _saveSlotValidationService != null
                ? _saveSlotValidationService.ValidateAndSync(_saveSlotStore, slotNumber)
                : new SaveSlotValidationResult(
                    _saveSlotStore.LoadSlot(slotNumber),
                    SaveSlotValidationStatus.Valid,
                    string.Empty,
                    levelGroupWasSynced: false);
        }

        private void RefreshViewModel()
        {
            ViewModelChanged?.Invoke(BuildViewModel());
        }
    }
}
