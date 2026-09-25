using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public interface ICampaignModeSelectionPort
    {
        void RequestMode(Action<GameMode?> completion);
    }

    public interface IConfirmPopupPort
    {
        void Request(ConfirmPopupPayload payload, Action<bool> completion);
    }

    public sealed class MainMenuController : IDisposable
    {
        private readonly IConfirmPopupPort _confirmPopupPort;
        private readonly ICampaignModeSelectionPort _modeSelectionPort;
        private readonly ICampaignLaunchHandoffStore _launchHandoffStore;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly ICampaignSaveQuery _saveSlotStore;
        private readonly ICampaignSlotLifecyclePort _slotLifecyclePort;
        private readonly ICampaignContinuePreparationPort _continuePreparationPort;
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly CampaignSlotLaunchEvaluator _slotLaunchEvaluator;
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private readonly IMainMenuSaveDiagnosticPort _saveDiagnosticPort;
        private readonly ICampaignSaveRecoveryPort _saveRecoveryPort;
        private LaunchConfirmationOperation _currentLaunchConfirmation;
        private int _confirmationGeneration;
        private bool _isDisposed;

        public MainMenuController(
            ICampaignSaveQuery saveSlotStore,
            ICampaignSlotLifecyclePort slotLifecyclePort,
            ICampaignContinuePreparationPort continuePreparationPort,
            ICampaignLaunchHandoffStore launchHandoffStore,
            CampaignStageSequenceResolver sequenceResolver,
            CampaignSlotLaunchEvaluator slotLaunchEvaluator,
            IStageLaunchRouter stageLaunchRouter,
            IConfirmPopupPort confirmPopupPort,
            ILocalizedTextResolver localizedTextResolver = null,
            IMainMenuSaveDiagnosticPort saveDiagnosticPort = null,
            ICampaignSaveRecoveryPort saveRecoveryPort = null,
            ICampaignModeSelectionPort modeSelectionPort = null)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _slotLifecyclePort = slotLifecyclePort ??
                throw new ArgumentNullException(nameof(slotLifecyclePort));
            _continuePreparationPort = continuePreparationPort ??
                throw new ArgumentNullException(nameof(continuePreparationPort));
            _launchHandoffStore = launchHandoffStore ??
                throw new ArgumentNullException(nameof(launchHandoffStore));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _slotLaunchEvaluator = slotLaunchEvaluator ??
                throw new ArgumentNullException(nameof(slotLaunchEvaluator));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _confirmPopupPort = confirmPopupPort ?? throw new ArgumentNullException(nameof(confirmPopupPort));
            _modeSelectionPort = modeSelectionPort ?? confirmPopupPort as ICampaignModeSelectionPort;
            _localizedTextResolver = localizedTextResolver ??
                InvariantSettingsLocalizedTextResolver.Instance;
            _saveDiagnosticPort = saveDiagnosticPort ??
                NoOpMainMenuSaveDiagnosticPort.Instance;
            _saveRecoveryPort = saveRecoveryPort;
            _localizedTextResolver.LocaleChanged += HandleLocaleChanged;
        }

        public event Action<SaveSlotPanelViewModel> ViewModelChanged;

        public SaveSlotPanelViewModel BuildViewModel()
        {
            var loadResult = _saveSlotStore.LoadAllWithReport();
            if (loadResult.Report.BlocksCampaignAccess)
            {
                _saveDiagnosticPort.Report(new SaveSlotFailureDiagnostic(
                    MainMenuSlotViewModelMapper.MapFailureKind(loadResult.Report.Status),
                    loadResult.Report.Status,
                    loadResult.Report.Reason,
                    slotNumber: 0,
                    operation: SaveSlotRepositoryOperation.LoadAllWithReport));
                return MainMenuSlotViewModelMapper.MapCampaignAccessBlocked(
                    loadResult.Report,
                    _localizedTextResolver);
            }

            return MainMenuSlotViewModelMapper.Map(
                BuildPresentationInputs(loadResult.Slots),
                _localizedTextResolver);
        }

        public void HandleIntent(SaveSlotIntent intent)
        {
            if (_isDisposed)
            {
                return;
            }

            switch (intent.IntentKind)
            {
                case SaveSlotIntentKind.NewGame:
                    RequestNewGame(intent.SlotNumber);
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
            if (_isDisposed)
            {
                return;
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!TryBeginCommand())
            {
                return;
            }

            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            var entry = _saveSlotStore.LoadSlot(slotNumber);
            var evaluation = _slotLaunchEvaluator.Evaluate(entry);
            var actionPolicy = CampaignSlotActionPolicy.Evaluate(evaluation);
            if (entry.IsEmpty)
            {
                StartNewGame(
                    slotNumber,
                    MainMenuLaunchOperationKind.EmptyContinue,
                    confirmIfOccupied: false);
                return;
            }

            if (!actionPolicy.CanContinue || evaluation.State == null)
            {
                RefreshViewModel();
                return;
            }

            if (!TryReserveLaunch(
                    slotNumber,
                    evaluation.ResolvedStageId,
                    StageNavigationKind.Continue,
                    "main-menu-continue",
                    out var handoff))
            {
                return;
            }

            try
            {
                var preparation = _continuePreparationPort.PrepareContinue(
                    new CampaignContinuePreparationCommand(
                        slotNumber,
                        evaluation.State.CurrentStageId,
                        evaluation.State.CurrentLevelGroupId,
                        evaluation.ResolvedLevelGroupId));
                var committedState = preparation.CommittedState;
                if (!preparation.Succeeded ||
                    committedState == null ||
                    committedState.CampaignCompleted ||
                    committedState.SlotNumber != handoff.SlotNumber ||
                    !committedState.CurrentStageId.Equals(handoff.StageId) ||
                    !string.Equals(
                        committedState.CurrentLevelGroupId,
                        evaluation.ResolvedLevelGroupId,
                        StringComparison.Ordinal))
                {
                    _launchHandoffStore.TryClear(handoff.Token);
                    RefreshViewModel();
                    return;
                }

                TryRouteOwnedLaunch(handoff);
            }
            catch
            {
                _launchHandoffStore.TryClear(handoff.Token);
                RefreshViewModel();
                throw;
            }
        }

        public void RequestRestart(int slotNumber)
        {
            if (_isDisposed)
            {
                return;
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!TryBeginCommand())
            {
                return;
            }

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
            if (_isDisposed)
            {
                return;
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!TryBeginCommand())
            {
                return;
            }

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

            var confirmationGeneration = BeginConfirmation();
            _confirmPopupPort.Request(
                MainMenuLocalization.CreateConfirmationPayload(
                    MainMenuConfirmationKind.DeleteSlot,
                    slotNumber),
                confirmed =>
                {
                    if (!TryClaimConfirmation(confirmationGeneration))
                    {
                        return;
                    }

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

                        if (_launchHandoffStore.TryPeek(out _))
                        {
                            return;
                        }

                        _slotLifecyclePort.DeleteSlot(slotNumber);
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

        private void RequestNewGame(int slotNumber)
        {
            if (_isDisposed)
            {
                return;
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!TryBeginCommand())
            {
                return;
            }

            StartNewGame(
                slotNumber,
                MainMenuLaunchOperationKind.NewGame,
                confirmIfOccupied: true);
        }

        public void RetryBlockedSave()
        {
            if (!TryBeginCommand())
            {
                return;
            }

            if (_saveRecoveryPort?.HasPendingReset == true)
            {
                _saveRecoveryPort.RetryPendingReset();
            }

            RefreshViewModel();
        }

        public void RequestResetBlockedSave()
        {
            if (!TryBeginCommand())
            {
                return;
            }

            var report = _saveSlotStore.LoadAllWithReport().Report;
            var actions = CampaignSaveRecoveryPolicy.GetActions(report.Status);
            if (_saveRecoveryPort == null ||
                (actions & CampaignSaveRecoveryActions.ResetProfile) == 0)
            {
                RefreshViewModel();
                return;
            }

            var confirmationGeneration = BeginConfirmation();
            _confirmPopupPort.Request(
                MainMenuLocalization.CreateConfirmationPayload(
                    MainMenuConfirmationKind.ResetBlockedProfile),
                confirmed =>
                {
                    if (!TryClaimConfirmation(confirmationGeneration))
                    {
                        return;
                    }

                    try
                    {
                        if (!confirmed)
                        {
                            return;
                        }

                        _saveRecoveryPort.ResetBlockedProfile(report.Status);
                    }
                    finally
                    {
                        RefreshViewModel();
                    }
                });
        }

        private void StartNewGame(int slotNumber, MainMenuLaunchOperationKind operationKind, bool confirmIfOccupied)
        {
            if (_isDisposed || IsCampaignAccessBlocked()) return;
            // Headless consumers may omit the UI port; production always supplies the popup adapter.
            if (_modeSelectionPort == null)
            {
                StartNewGameWithMode(slotNumber, operationKind, confirmIfOccupied, GameMode.Hardcore);
                return;
            }
            var generation = BeginConfirmation();
            _modeSelectionPort.RequestMode(mode =>
            {
                if (!TryClaimConfirmation(generation)) return;
                if (mode != GameMode.Casual && mode != GameMode.Hardcore) return;
                StartNewGameWithMode(slotNumber, operationKind, confirmIfOccupied, mode.Value);
            });
        }

        private void StartNewGameWithMode(
            int slotNumber,
            MainMenuLaunchOperationKind operationKind,
            bool confirmIfOccupied, GameMode gameMode)
        {
            if (_isDisposed)
            {
                return;
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (IsCampaignAccessBlocked())
            {
                RefreshViewModel();
                return;
            }

            var existingEntry = _saveSlotStore.LoadSlot(slotNumber);
            var existingEvaluation = _slotLaunchEvaluator.Evaluate(existingEntry);
            var existingActionPolicy = CampaignSlotActionPolicy.Evaluate(existingEvaluation);
            if (operationKind == MainMenuLaunchOperationKind.Restart &&
                !existingActionPolicy.CanRestart)
            {
                RefreshViewModel();
                return;
            }

            var candidate = CampaignSlotStateFactory.CreateNewGame(
                slotNumber,
                _sequenceResolver,
                string.Empty, gameMode);
            var candidateEvaluation = _slotLaunchEvaluator.Evaluate(candidate);
            var candidateActionPolicy = CampaignSlotActionPolicy.Evaluate(candidateEvaluation);
            if (!candidateActionPolicy.CanContinue)
            {
                RefreshViewModel();
                return;
            }

            if (!TryReserveLaunch(
                    slotNumber,
                    candidateEvaluation.ResolvedStageId,
                    StageNavigationKind.Continue,
                    ResolveLaunchSource(operationKind),
                    out var handoff))
            {
                return;
            }

            var requiresConfirmation =
                operationKind == MainMenuLaunchOperationKind.Restart ||
                (confirmIfOccupied && !existingEntry.IsEmpty);
            if (requiresConfirmation)
            {
                RequestLaunchConfirmation(handoff, operationKind, gameMode);
                return;
            }

            InitializeAndRouteNewGame(handoff, gameMode);
        }

        private void RequestLaunchConfirmation(
            CampaignLaunchHandoff handoff,
            MainMenuLaunchOperationKind operationKind, GameMode gameMode)
        {
            var confirmationGeneration = BeginConfirmation();
            var operation = new LaunchConfirmationOperation(handoff, operationKind, gameMode);
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
                    confirmed => CompleteLaunchConfirmation(
                        operation,
                        operationKind,
                        confirmationGeneration,
                        confirmed));
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
            int confirmationGeneration,
            bool confirmed)
        {
            if (!ReferenceEquals(_currentLaunchConfirmation, operation) ||
                !TryClaimConfirmation(confirmationGeneration))
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

            InitializeAndRouteNewGame(operation.Handoff, operation.GameMode);
        }

        private void InitializeAndRouteNewGame(CampaignLaunchHandoff handoff, GameMode gameMode)
        {
            if (_isDisposed || !IsCurrentHandoff(handoff))
            {
                return;
            }

            try
            {
                _slotLifecyclePort.InitializeNewGame(
                    handoff.SlotNumber,
                    _sequenceResolver,
                    DateTimeOffset.UtcNow.ToString("O"), gameMode);
                var entry = _saveSlotStore.LoadSlot(handoff.SlotNumber);
                var evaluation = _slotLaunchEvaluator.Evaluate(entry);
                var actionPolicy = CampaignSlotActionPolicy.Evaluate(evaluation);
                if (!actionPolicy.CanContinue ||
                    evaluation.State == null ||
                    evaluation.State.SlotNumber != handoff.SlotNumber ||
                    !evaluation.ResolvedStageId.Equals(handoff.StageId))
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
                SaveStoreDiagnosticsKey = _saveSlotStore.DiagnosticsKey,
            });
            try
            {
                _stageLaunchRouter.Launch(new StageNavigationRequest(
                    handoff.StageId,
                    handoff.NavigationKind,
                    handoff.Source,
                    StageTransitionHint.ForKind(StageTransitionKind.MainToGameplay),
                    SceneTransitionIntent.GameplayEntry));
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

        private static string ResolveLaunchSource(MainMenuLaunchOperationKind operationKind)
        {
            return operationKind switch
            {
                MainMenuLaunchOperationKind.NewGame => "main-menu-new-game",
                MainMenuLaunchOperationKind.EmptyContinue => "main-menu-empty-continue",
                MainMenuLaunchOperationKind.Restart => "main-menu-completed-restart",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(operationKind),
                    operationKind,
                    "Main Menu launch operation has no governed provenance."),
            };
        }

        private IReadOnlyList<MainMenuSlotPresentationInput> BuildPresentationInputs(
            IReadOnlyList<CampaignSlotEntry> entries)
        {
            var inputs = new List<MainMenuSlotPresentationInput>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var evaluation = _slotLaunchEvaluator.Evaluate(entry);
                inputs.Add(new MainMenuSlotPresentationInput(
                    entry,
                    evaluation,
                    CampaignSlotActionPolicy.Evaluate(evaluation)));
            }

            return inputs;
        }

        private void RefreshViewModel()
        {
            if (_isDisposed)
            {
                return;
            }

            ViewModelChanged?.Invoke(BuildViewModel());
        }

        private int BeginConfirmation()
        {
            _confirmationGeneration++;
            if (_currentLaunchConfirmation != null)
            {
                _launchHandoffStore.TryClear(_currentLaunchConfirmation.Handoff.Token);
                _currentLaunchConfirmation = null;
            }

            return _confirmationGeneration;
        }

        private bool TryBeginCommand()
        {
            if (_isDisposed)
            {
                return false;
            }

            BeginConfirmation();
            return true;
        }

        private bool TryClaimConfirmation(int confirmationGeneration)
        {
            if (_isDisposed || confirmationGeneration != _confirmationGeneration)
            {
                return false;
            }

            _confirmationGeneration++;
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _confirmationGeneration++;
            if (_currentLaunchConfirmation != null)
            {
                _launchHandoffStore.TryClear(_currentLaunchConfirmation.Handoff.Token);
                _currentLaunchConfirmation = null;
            }

            _localizedTextResolver.LocaleChanged -= HandleLocaleChanged;
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
                MainMenuLaunchOperationKind kind, GameMode gameMode)
            {
                Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
                Kind = kind;
                GameMode = gameMode;
                SlotNumber = handoff.SlotNumber;
            }

            public CampaignLaunchHandoff Handoff { get; }

            public MainMenuLaunchOperationKind Kind { get; }

            public GameMode GameMode { get; }

            public int SlotNumber { get; }
        }
    }
}
