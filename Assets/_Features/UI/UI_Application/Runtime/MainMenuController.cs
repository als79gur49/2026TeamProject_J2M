using System;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public interface IConfirmPopupPort
    {
        void Request(ConfirmPopupPayload payload, Action<bool> completion);
    }

    public sealed class MainMenuController : IDisposable
    {
        private readonly IConfirmPopupPort _confirmPopupPort;
        private readonly ICampaignLaunchHandoffStore _launchHandoffStore;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly ICampaignSaveSlotStore _saveSlotStore;
        private readonly SaveSlotValidationService _saveSlotValidationService;
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private LaunchConfirmationOperation _currentLaunchConfirmation;
        private bool _isDisposed;

        public MainMenuController(
            ICampaignSaveSlotStore saveSlotStore,
            ICampaignLaunchHandoffStore launchHandoffStore,
            CampaignStageSequenceResolver sequenceResolver,
            IStageLaunchRouter stageLaunchRouter,
            IConfirmPopupPort confirmPopupPort,
            SaveSlotValidationService saveSlotValidationService = null,
            ILocalizedTextResolver localizedTextResolver = null)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _launchHandoffStore = launchHandoffStore ??
                throw new ArgumentNullException(nameof(launchHandoffStore));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _confirmPopupPort = confirmPopupPort ?? throw new ArgumentNullException(nameof(confirmPopupPort));
            _saveSlotValidationService = saveSlotValidationService;
            _localizedTextResolver = localizedTextResolver ??
                InvariantSettingsLocalizedTextResolver.Instance;
            _localizedTextResolver.LocaleChanged += HandleLocaleChanged;
        }

        public event Action<SaveSlotPanelViewModel> ViewModelChanged;

        public SaveSlotPanelViewModel BuildViewModel()
        {
            var loadResult = _saveSlotStore.LoadAllWithReport();
            if (loadResult.Report.BlocksCampaignAccess)
            {
                return MainMenuSlotViewModelMapper.MapCampaignAccessBlocked(
                    loadResult.Report,
                    _localizedTextResolver);
            }

            return MainMenuSlotViewModelMapper.Map(
                loadResult.Slots,
                _sequenceResolver,
                _saveSlotValidationService,
                _localizedTextResolver);
        }

        public void HandleIntent(SaveSlotIntent intent)
        {
            switch (intent.IntentKind)
            {
                case SaveSlotIntentKind.NewGame:
                    StartNewGame(
                        intent.SlotNumber,
                        MainMenuLaunchOperationKind.NewGame,
                        confirmIfOccupied: true);
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

            var validation = Validate(slotNumber);
            if (validation.Status == SaveSlotValidationStatus.Empty)
            {
                StartNewGame(
                    slotNumber,
                    MainMenuLaunchOperationKind.EmptyContinue,
                    confirmIfOccupied: false);
                return;
            }

            if (!validation.CanContinue)
            {
                RefreshViewModel();
                return;
            }

            if (!TryReserveLaunch(
                    slotNumber,
                    validation.Slot.CurrentStageId,
                    StageNavigationKind.Continue,
                    "main-menu-continue",
                    out var handoff))
            {
                return;
            }

            try
            {
                if (validation.RequiresSaveSync)
                {
                    _saveSlotStore.SaveSlot(validation.Slot);
                }

                TryRouteOwnedLaunch(handoff);
            }
            catch
            {
                _launchHandoffStore.TryClear(handoff.Token);
                throw;
            }
        }

        public void RequestRestart(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            StartNewGame(
                slotNumber,
                MainMenuLaunchOperationKind.Restart,
                confirmIfOccupied: true);
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
                MainMenuLocalization.CreateConfirmationPayload(
                    MainMenuConfirmationKind.DeleteSlot,
                    slotNumber),
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

        private void StartNewGame(
            int slotNumber,
            MainMenuLaunchOperationKind operationKind,
            bool confirmIfOccupied)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            var existingValidation = Validate(slotNumber);
            if (operationKind == MainMenuLaunchOperationKind.Restart &&
                !existingValidation.CanRestart)
            {
                RefreshViewModel();
                return;
            }

            var candidate = SaveSlotData.CreateNewGame(
                slotNumber,
                _sequenceResolver,
                string.Empty);
            var candidateValidation = Validate(candidate);
            if (!candidateValidation.CanContinue)
            {
                RefreshViewModel();
                return;
            }

            if (!TryReserveLaunch(
                    slotNumber,
                    candidateValidation.Slot.CurrentStageId,
                    StageNavigationKind.Continue,
                    "main-menu-new-game",
                    out var handoff))
            {
                return;
            }

            var requiresConfirmation =
                operationKind == MainMenuLaunchOperationKind.Restart ||
                (confirmIfOccupied &&
                 existingValidation.Status != SaveSlotValidationStatus.Empty);
            if (requiresConfirmation)
            {
                RequestLaunchConfirmation(handoff, operationKind);
                return;
            }

            InitializeAndRouteNewGame(handoff);
        }

        private void RequestLaunchConfirmation(
            CampaignLaunchHandoff handoff,
            MainMenuLaunchOperationKind operationKind)
        {
            var operation = new LaunchConfirmationOperation(handoff, operationKind);
            _currentLaunchConfirmation = operation;
            var payload = operationKind == MainMenuLaunchOperationKind.Restart
                ? MainMenuLocalization.CreateConfirmationPayload(
                    MainMenuConfirmationKind.RestartSlot,
                    handoff.SlotNumber)
                : MainMenuLocalization.CreateConfirmationPayload(
                    MainMenuConfirmationKind.OverwriteSlot,
                    handoff.SlotNumber);

            try
            {
                _confirmPopupPort.Request(
                    payload,
                    confirmed => CompleteLaunchConfirmation(operation, operationKind, confirmed));
            }
            catch
            {
                if (ReferenceEquals(_currentLaunchConfirmation, operation))
                {
                    _currentLaunchConfirmation = null;
                }

                _launchHandoffStore.TryClear(handoff.Token);
                RefreshViewModel();
                throw;
            }
        }

        private void CompleteLaunchConfirmation(
            LaunchConfirmationOperation operation,
            MainMenuLaunchOperationKind expectedKind,
            bool confirmed)
        {
            if (!ReferenceEquals(_currentLaunchConfirmation, operation))
            {
                return;
            }

            _currentLaunchConfirmation = null;
            if (operation.Kind != expectedKind ||
                operation.SlotNumber != operation.Handoff.SlotNumber ||
                !IsCurrentHandoff(operation.Handoff))
            {
                return;
            }

            if (!confirmed)
            {
                _launchHandoffStore.TryClear(operation.Handoff.Token);
                RefreshViewModel();
                return;
            }

            InitializeAndRouteNewGame(operation.Handoff);
        }

        private void InitializeAndRouteNewGame(CampaignLaunchHandoff handoff)
        {
            if (!IsCurrentHandoff(handoff))
            {
                return;
            }

            try
            {
                _saveSlotStore.InitializeNewGame(
                    handoff.SlotNumber,
                    _sequenceResolver,
                    DateTimeOffset.UtcNow.ToString("O"));
                var validation = Validate(handoff.SlotNumber);
                if (!validation.CanContinue ||
                    validation.Slot.SlotNumber != handoff.SlotNumber ||
                    !validation.Slot.CurrentStageId.Equals(handoff.StageId))
                {
                    _launchHandoffStore.TryClear(handoff.Token);
                    return;
                }

                TryRouteOwnedLaunch(handoff);
            }
            catch
            {
                _launchHandoffStore.TryClear(handoff.Token);
                throw;
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

        private bool TryReserveLaunch(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            out CampaignLaunchHandoff handoff)
        {
            if (!_launchHandoffStore.TryBegin(
                    slotNumber,
                    stageId,
                    navigationKind,
                    source,
                    out handoff))
            {
                RefreshViewModel();
                return false;
            }

            return true;
        }

        private bool TryRouteOwnedLaunch(CampaignLaunchHandoff handoff)
        {
            if (!IsCurrentHandoff(handoff))
            {
                return false;
            }

            CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
            {
                Source = handoff.Source,
                RequestedStageId = handoff.StageId.Value,
                HasLaunchHandoff = true,
                HandoffSlotNumber = handoff.SlotNumber,
                HandoffToken = handoff.Token.ToString("N"),
                SaveSlotStoreKey = _saveSlotStore.DiagnosticsKey,
            });
            try
            {
                _stageLaunchRouter.Launch(new StageNavigationRequest(
                    handoff.StageId,
                    handoff.NavigationKind,
                    handoff.Source,
                    StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay)));
                return true;
            }
            catch
            {
                _launchHandoffStore.TryClear(handoff.Token);
                throw;
            }
        }

        private bool IsCurrentHandoff(CampaignLaunchHandoff expected)
        {
            return expected != null &&
                   _launchHandoffStore.TryPeek(out var current) &&
                   expected.Matches(current);
        }

        private SaveSlotValidationResult Validate(int slotNumber)
        {
            return Validate(_saveSlotStore.LoadSlot(slotNumber));
        }

        private SaveSlotValidationResult Validate(SaveSlotData slot)
        {
            return _saveSlotValidationService != null
                ? _saveSlotValidationService.Validate(slot)
                : new SaveSlotValidationResult(
                    slot,
                    slot.IsEmpty
                        ? SaveSlotValidationStatus.Empty
                        : slot.CampaignCompleted
                            ? SaveSlotValidationStatus.Completed
                            : SaveSlotValidationStatus.Valid,
                    string.Empty,
                    levelGroupWasSynced: false);
        }

        private void RefreshViewModel()
        {
            ViewModelChanged?.Invoke(BuildViewModel());
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _localizedTextResolver.LocaleChanged -= HandleLocaleChanged;
            _isDisposed = true;
        }

        private void HandleLocaleChanged()
        {
            if (!_isDisposed)
            {
                RefreshViewModel();
            }
        }

        private enum MainMenuLaunchOperationKind
        {
            NewGame = 1,
            Restart = 2,
            EmptyContinue = 3,
        }

        private sealed class LaunchConfirmationOperation
        {
            public LaunchConfirmationOperation(
                CampaignLaunchHandoff handoff,
                MainMenuLaunchOperationKind kind)
            {
                Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
                Kind = kind;
                SlotNumber = handoff.SlotNumber;
            }

            public CampaignLaunchHandoff Handoff { get; }

            public MainMenuLaunchOperationKind Kind { get; }

            public int SlotNumber { get; }
        }
    }
}
