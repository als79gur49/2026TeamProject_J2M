using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    public sealed class UIFlowCoordinator : IDisposable, IUiFlowAudioIntentBoundary
    {
        private enum PauseReturnMode
        {
            None = 0,
            RestorePausePopupAfterBack = 1,
        }

        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly IMainMenuReturnRouter _mainMenuReturnRouter;
        private readonly IUiFlowPauseService _pauseService;
        private readonly PopupController _popupController;
        private readonly ScreenController _screenController;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly IUiAudioPort _uiAudioPort;
        private readonly UIBlockPolicy _uiBlockPolicy;
        private UiFlowAudioTransaction _activeAudioTransaction;
        private PopupController.PopupCompletionDispatchEvent? _activePopupCompletionDispatch;
        private UITickEventKey? _lastStageClearedEventKey;
        private PauseReturnMode _pauseReturnMode;

        public UIFlowCoordinator(
            ScreenController screenController,
            PopupController popupController,
            UIBlockPolicy uiBlockPolicy,
            IUiFlowPauseService pauseService,
            IGameplayUiPresentationSource presentationSource,
            IUiAudioPort uiAudioPort,
            IStageLaunchRouter stageLaunchRouter,
            IMainMenuReturnRouter mainMenuReturnRouter = null)
        {
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _uiBlockPolicy = uiBlockPolicy ?? throw new ArgumentNullException(nameof(uiBlockPolicy));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _mainMenuReturnRouter = mainMenuReturnRouter ?? NoOpMainMenuReturnRouter.Instance;

            _screenController.StateChanged += HandleFlowStateChanged;
            _screenController.ActionRequested += HandleScreenActionRequested;
            _screenController.ScreenTransitioned += HandleScreenTransitioned;
            _popupController.StateChanged += HandleFlowStateChanged;
            _popupController.PopupOpened += HandlePopupOpened;
            _popupController.PopupCompleted += HandlePopupCompleted;
            _popupController.PopupCompletionDispatching += HandlePopupCompletionDispatching;
            _popupController.PopupCompletionDispatched += HandlePopupCompletionDispatched;
            _presentationSource.TickEventsApplied += HandleTickEventsApplied;
            _presentationSource.LevelFailedCommitted += HandleLevelFailedCommitted;
        }

        public UIBlockSnapshot CurrentBlockSnapshot { get; private set; }

        internal UiFlowAudioTrace LastFlowAudioTrace { get; private set; }

        public void Initialize()
        {
            ClearPauseReturnMode();
            _screenController.SetRoot(BuildGameplayRequest());
            RefreshBlockSnapshot();
        }

        public bool OpenHelpScreen()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => PushScreenCore(BuildHelpRequest()));
        }

        public bool OpenObjectiveStatusScreen()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => PushScreenCore(BuildObjectiveStatusRequest()));
        }

        public bool OpenInventoryScreen()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => PushScreenCore(BuildInventoryRequest()));
        }

        public bool OpenSettingsScreen()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => PushScreenCore(BuildSettingsRequest()));
        }

        public bool RequestPausePopup()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, RequestPausePopupCore);
        }

        public bool RequestObjectiveInfoPopup(ObjectiveInfoPopupPayload payload)
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => RequestObjectiveInfoPopupCore(payload));
        }

        public bool RequestConfirmPopup(
            ConfirmPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            return ExecuteIntent(
                UiFlowAudioIntentKind.OpenForward,
                () => RequestConfirmPopupCore(payload, completionCallback));
        }

        public bool RequestTooltipPopup(
            TooltipPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            return ExecuteIntent(
                UiFlowAudioIntentKind.OpenForward,
                () => RequestTooltipPopupCore(payload, completionCallback));
        }

        public bool RequestRewardPopup(
            RewardPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            return ExecuteIntent(
                UiFlowAudioIntentKind.OpenForward,
                () => RequestRewardPopupCore(payload, completionCallback));
        }

        public bool HandleBackRequested()
        {
            return ExecuteIntent(ResolveBackIntent(), HandleBackRequestedCore);
        }

        public bool HandlePopupBackdropClicked()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.Back, HandlePopupBackdropClickedCore);
        }

        public void Dispose()
        {
            AbortActiveTransaction(UiFlowAudioSilenceReason.Cleanup);
            ClearPauseReturnMode();
            _screenController.StateChanged -= HandleFlowStateChanged;
            _screenController.ActionRequested -= HandleScreenActionRequested;
            _screenController.ScreenTransitioned -= HandleScreenTransitioned;
            _popupController.StateChanged -= HandleFlowStateChanged;
            _popupController.PopupOpened -= HandlePopupOpened;
            _popupController.PopupCompleted -= HandlePopupCompleted;
            _popupController.PopupCompletionDispatching -= HandlePopupCompletionDispatching;
            _popupController.PopupCompletionDispatched -= HandlePopupCompletionDispatched;
            _presentationSource.TickEventsApplied -= HandleTickEventsApplied;
            _presentationSource.LevelFailedCommitted -= HandleLevelFailedCommitted;
        }

        bool IUiFlowAudioIntentBoundary.ExecuteOpenForwardBoundary(Func<bool> action)
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, action);
        }

        private void RefreshBlockSnapshot()
        {
            CurrentBlockSnapshot = _uiBlockPolicy.Evaluate(
                new UIFlowStateSnapshot(
                    _screenController.CurrentEntry,
                    _popupController.TopPopup,
                    _popupController.PopupCount));
        }

        private void ClosePopupsForScreenTransition()
        {
            if (_popupController.PopupCount == 0)
            {
                return;
            }

            _popupController.CloseAll(PopupCloseReason.ScreenTransition);
        }

        private void HandlePausePopupCompletion(PopupCompletion completion)
        {
            if (completion.PopupId != PopupId.Pause)
            {
                return;
            }

            switch (completion.CompletionKind)
            {
                case PopupCompletionKind.SettingsRequested:
                    SetPauseReturnMode(PauseReturnMode.RestorePausePopupAfterBack);
                    if (!PushScreenCore(BuildSettingsRequest(), preservePauseReturnMode: true))
                    {
                        ClearPauseReturnMode();
                        RequestPausePopupCore();
                    }

                    break;

                case PopupCompletionKind.Resumed:
                case PopupCompletionKind.Closed:
                    ClearPauseReturnMode();
                    _pauseService.Resume();
                    break;
            }
        }

        private void HandleFlowStateChanged()
        {
            RefreshBlockSnapshot();
        }

        private void HandleScreenTransitioned(ScreenTransitionedEvent transitionEvent)
        {
            RecordDelta(UiFlowAudioDelta.FromScreenTransition(transitionEvent));
        }

        private void HandlePopupOpened(PopupOpenedEvent openedEvent)
        {
            RecordDelta(UiFlowAudioDelta.FromPopupOpened(openedEvent));
        }

        private void HandlePopupCompleted(PopupCompletedEvent completedEvent)
        {
            RecordDelta(UiFlowAudioDelta.FromPopupCompleted(completedEvent));
        }

        private void HandlePopupCompletionDispatching(PopupController.PopupCompletionDispatchEvent dispatchEvent)
        {
            if (_activeAudioTransaction != null)
            {
                return;
            }

            var intent = ResolvePopupCompletionIntent(dispatchEvent.Completion);
            if (intent == UiFlowAudioIntentKind.None)
            {
                return;
            }

            _activeAudioTransaction = new UiFlowAudioTransaction(intent);
            _activePopupCompletionDispatch = dispatchEvent;
        }

        private void HandlePopupCompletionDispatched(PopupController.PopupCompletionDispatchEvent dispatchEvent)
        {
            if (!_activePopupCompletionDispatch.HasValue)
            {
                return;
            }

            var expected = _activePopupCompletionDispatch.Value.Completion;
            if (!expected.InstanceId.Equals(dispatchEvent.Completion.InstanceId))
            {
                return;
            }

            _activeAudioTransaction?.Leave();
            _activePopupCompletionDispatch = null;
            TryFinalizeActiveTransaction();
        }

        public void HandleScreenActionRequested(ScreenAction action)
        {
            switch (action.ActionKind)
            {
                case ScreenActionKind.BackRequested:
                    HandleBackRequested();
                    break;

                case ScreenActionKind.ShowScreen:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => ShowScreenCore(action.ScreenRequest));
                    break;

                case ScreenActionKind.PushScreen:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => PushScreenCore(action.ScreenRequest));
                    break;

                case ScreenActionKind.ReplaceScreen:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => ReplaceScreenCore(action.ScreenRequest));
                    break;

                case ScreenActionKind.RequestPopup:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => TryPushPopupRequestCore(action.PopupRequest));
                    break;

                case ScreenActionKind.LaunchStage:
                    LaunchStage(action.StageNavigationRequest);
                    break;

                case ScreenActionKind.ReturnToMainMenu:
                    ReturnToMainMenu();
                    break;
            }
        }

        private bool ExecuteIntent(UiFlowAudioIntentKind intent, Func<bool> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var createdRoot = BeginTransaction(intent);

            try
            {
                var result = action();
                if (createdRoot && !result)
                {
                    _activeAudioTransaction?.Abort(UiFlowAudioSilenceReason.FailedOperation);
                }

                return result;
            }
            catch
            {
                if (createdRoot)
                {
                    _activeAudioTransaction?.Abort(UiFlowAudioSilenceReason.Aborted);
                }

                throw;
            }
            finally
            {
                EndTransaction(createdRoot);
            }
        }

        private bool BeginTransaction(UiFlowAudioIntentKind intent)
        {
            if (_activeAudioTransaction != null)
            {
                _activeAudioTransaction.Join();
                return false;
            }

            _activeAudioTransaction = new UiFlowAudioTransaction(intent);
            return true;
        }

        private void EndTransaction(bool createdRoot)
        {
            if (_activeAudioTransaction == null)
            {
                return;
            }

            _activeAudioTransaction.Leave();
            if (createdRoot)
            {
                TryFinalizeActiveTransaction();
            }
        }

        private void TryFinalizeActiveTransaction()
        {
            if (_activeAudioTransaction == null || _activeAudioTransaction.Depth > 0)
            {
                return;
            }

            var completedTransaction = _activeAudioTransaction;
            _activeAudioTransaction = null;
            var trace = completedTransaction.FinalizeTrace();
            LastFlowAudioTrace = trace;

            if (trace.EmittedCueId.HasValue)
            {
                _uiAudioPort.Play(trace.EmittedCueId.Value);
            }
        }

        private void AbortActiveTransaction(UiFlowAudioSilenceReason reason)
        {
            _activeAudioTransaction?.Abort(reason);
            _activePopupCompletionDispatch = null;
        }

        private void RecordDelta(UiFlowAudioDelta delta)
        {
            _activeAudioTransaction?.Record(delta);
        }

        private bool HandleBackRequestedCore()
        {
            if (_popupController.PopupCount > 0)
            {
                return _popupController.HandleBackRequested();
            }

            if (_screenController.HandleBackRequested())
            {
                if (_pauseReturnMode == PauseReturnMode.RestorePausePopupAfterBack &&
                    _screenController.CurrentScreenId == ScreenId.Gameplay &&
                    RequestPausePopupCore())
                {
                    ClearPauseReturnMode();
                }

                return true;
            }

            if (_screenController.CurrentScreenId == ScreenId.Gameplay)
            {
                return RequestPausePopupCore();
            }

            return false;
        }

        private bool HandlePopupBackdropClickedCore()
        {
            return _popupController.HandleBackdropClicked();
        }

        private bool RequestPausePopupCore()
        {
            if (_popupController.Contains(PopupId.Pause))
            {
                return false;
            }

            if (!_popupController.Push(
                    new PopupRequest(
                        PopupId.Pause,
                        PausePopupPayload.Default,
                        HandlePausePopupCompletion),
                    out _))
            {
                return false;
            }

            _pauseService.Pause();
            return true;
        }

        private bool RequestObjectiveInfoPopupCore(ObjectiveInfoPopupPayload payload)
        {
            if (_screenController.CurrentScreenId != ScreenId.ObjectiveStatus || payload == null)
            {
                return false;
            }

            return _popupController.Push(new PopupRequest(PopupId.ObjectiveInfo, payload), out _);
        }

        private bool RequestConfirmPopupCore(
            ConfirmPopupPayload payload,
            Action<PopupCompletion> completionCallback)
        {
            if (payload == null)
            {
                return false;
            }

            return _popupController.Push(
                new PopupRequest(PopupId.Confirm, payload, completionCallback),
                out _);
        }

        private bool RequestTooltipPopupCore(
            TooltipPopupPayload payload,
            Action<PopupCompletion> completionCallback)
        {
            if (payload == null)
            {
                return false;
            }

            return TryPushPopupRequestCore(new PopupRequest(PopupId.Tooltip, payload, completionCallback));
        }

        private bool RequestRewardPopupCore(
            RewardPopupPayload payload,
            Action<PopupCompletion> completionCallback)
        {
            if (payload == null)
            {
                return false;
            }

            return _popupController.Push(
                new PopupRequest(PopupId.Reward, payload, completionCallback),
                out _);
        }

        private bool TryPushPopupRequestCore(PopupRequest request)
        {
            if (request.PopupId == PopupId.Tooltip &&
                _popupController.TopPopup.HasValue &&
                _popupController.TopPopup.Value.PopupId == PopupId.Tooltip)
            {
                return false;
            }

            return _popupController.Push(request, out _);
        }

        private bool ShowScreenCore(ScreenRequest request)
        {
            return ShowScreenCore(request, preservePauseReturnMode: false);
        }

        private bool ShowScreenCore(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Show(request);
        }

        private bool PushScreenCore(ScreenRequest request)
        {
            return PushScreenCore(request, preservePauseReturnMode: false);
        }

        private bool PushScreenCore(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Push(request);
        }

        private bool ReplaceScreenCore(ScreenRequest request)
        {
            return ReplaceScreenCore(request, preservePauseReturnMode: false);
        }

        private bool ReplaceScreenCore(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Replace(request);
        }

        private void LaunchStage(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new InvalidOperationException("Stage launch actions require a valid StageNavigationRequest.");
            }

            ClosePopupsForScreenTransition();
            _stageLaunchRouter.Launch(request);
        }

        private void ReturnToMainMenu()
        {
            ClosePopupsForScreenTransition();
            _mainMenuReturnRouter.ReturnToMainMenu();
        }

        private void HandleTickEventsApplied(UITickEventBatch batch)
        {
            if (!batch.HasAnyEvents)
            {
                return;
            }

            for (var i = 0; i < batch.Events.Count; i++)
            {
                var tickEvent = batch.Events[i];
                if (tickEvent.EventKind != UITickEventKind.StageCleared)
                {
                    continue;
                }

                if (_lastStageClearedEventKey.HasValue && _lastStageClearedEventKey.Value.Equals(tickEvent.Key))
                {
                    return;
                }

                ExecuteIntent(UiFlowAudioIntentKind.SystemPresentation, () =>
                {
                    _lastStageClearedEventKey = tickEvent.Key;
                    ClearPauseReturnMode();
                    ClosePopupsForScreenTransition();
                    _screenController.Clear();
                    OpenStageCompletionFlow();
                    return true;
                });
                return;
            }
        }

        private void HandleLevelFailedCommitted(LevelFailedScreenPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            if (_screenController.CurrentScreenId == ScreenId.LevelFailed)
            {
                return;
            }

            ExecuteIntent(UiFlowAudioIntentKind.SystemPresentation, () =>
            {
                ClearPauseReturnMode();
                ClosePopupsForScreenTransition();
                _screenController.Clear();
                _screenController.SetRoot(new ScreenRequest(
                    ScreenId.LevelFailed,
                    payload,
                    ScreenId.LevelFailed.ToString()));
                RecordDelta(UiFlowAudioDelta.FromRootScreenSet(ScreenId.LevelFailed));
                return true;
            });
        }

        private void OpenStageCompletionFlow()
        {
            var readModel = _presentationSource.CurrentStageCompletion;
            if (readModel == null)
            {
                throw new InvalidOperationException(
                    "Stage clear tick events require a completion read model before UI flow transition.");
            }

            _screenController.SetRoot(new ScreenRequest(
                ScreenId.StageResult,
                StageCompletionStageResultPayloadMapper.Map(readModel),
                ScreenId.StageResult.ToString()));
            RecordDelta(UiFlowAudioDelta.FromRootScreenSet(ScreenId.StageResult));

            if (readModel.RewardGrantResult != null && readModel.RewardGrantResult.AnyGranted)
            {
                _popupController.Push(
                    new PopupRequest(
                        PopupId.Reward,
                        StageCompletionRewardPopupPayloadMapper.Map(readModel)),
                    out _);
            }
        }

        private UiFlowAudioIntentKind ResolveBackIntent()
        {
            if (_popupController.TopPopup.HasValue &&
                _popupController.TopPopup.Value.Policy.BackAction == PopupBackAction.Cancel)
            {
                return UiFlowAudioIntentKind.Cancel;
            }

            return UiFlowAudioIntentKind.Back;
        }

        private static UiFlowAudioIntentKind ResolvePopupCompletionIntent(PopupCompletion completion)
        {
            if (!IsUserVisibleCompletion(completion.CloseReason))
            {
                return UiFlowAudioIntentKind.None;
            }

            switch (completion.PopupId)
            {
                case PopupId.Pause:
                    return completion.CompletionKind switch
                    {
                        PopupCompletionKind.SettingsRequested => UiFlowAudioIntentKind.OpenForward,
                        PopupCompletionKind.Resumed => UiFlowAudioIntentKind.Confirm,
                        PopupCompletionKind.Closed => UiFlowAudioIntentKind.Back,
                        _ => UiFlowAudioIntentKind.None,
                    };

                case PopupId.ObjectiveInfo:
                    return completion.CompletionKind switch
                    {
                        PopupCompletionKind.Acknowledged => UiFlowAudioIntentKind.Back,
                        PopupCompletionKind.Closed => UiFlowAudioIntentKind.Back,
                        _ => UiFlowAudioIntentKind.None,
                    };

                case PopupId.Tooltip:
                    return completion.CompletionKind switch
                    {
                        PopupCompletionKind.Closed => UiFlowAudioIntentKind.Back,
                        PopupCompletionKind.Acknowledged => UiFlowAudioIntentKind.Back,
                        _ => UiFlowAudioIntentKind.None,
                    };

                case PopupId.Confirm:
                    return completion.CompletionKind switch
                    {
                        PopupCompletionKind.Confirmed => UiFlowAudioIntentKind.Confirm,
                        PopupCompletionKind.Cancelled => UiFlowAudioIntentKind.Cancel,
                        _ => UiFlowAudioIntentKind.None,
                    };

                case PopupId.Reward:
                    return completion.CompletionKind == PopupCompletionKind.Acknowledged
                        ? UiFlowAudioIntentKind.Confirm
                        : UiFlowAudioIntentKind.None;

                default:
                    return UiFlowAudioIntentKind.None;
            }
        }

        private static bool IsUserVisibleCompletion(PopupCloseReason closeReason)
        {
            switch (closeReason)
            {
                case PopupCloseReason.UserAction:
                case PopupCloseReason.Back:
                case PopupCloseReason.BackdropClick:
                    return true;

                default:
                    return false;
            }
        }

        private void SetPauseReturnMode(PauseReturnMode mode)
        {
            _pauseReturnMode = mode;
        }

        private void ClearPauseReturnMode()
        {
            _pauseReturnMode = PauseReturnMode.None;
        }

        private static ScreenRequest BuildGameplayRequest()
        {
            return new ScreenRequest(ScreenId.Gameplay, GameplayScreenPayload.Default, ScreenId.Gameplay.ToString());
        }

        private static ScreenRequest BuildHelpRequest()
        {
            return new ScreenRequest(ScreenId.Help, HelpScreenPayload.Default, ScreenId.Help.ToString());
        }

        private static ScreenRequest BuildObjectiveStatusRequest()
        {
            return new ScreenRequest(
                ScreenId.ObjectiveStatus,
                ObjectiveStatusScreenPayload.Default,
                ScreenId.ObjectiveStatus.ToString());
        }

        private static ScreenRequest BuildInventoryRequest()
        {
            return new ScreenRequest(ScreenId.Inventory, InventoryScreenPayload.Default, ScreenId.Inventory.ToString());
        }

        private static ScreenRequest BuildSettingsRequest()
        {
            return new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString());
        }
    }
}
