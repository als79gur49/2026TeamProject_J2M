using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    public sealed class UIFlowCoordinator : IDisposable
    {
        private enum PauseReturnMode
        {
            None = 0,
            RestorePausePopupAfterBack = 1,
        }

        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly IUiFlowPauseService _pauseService;
        private readonly PopupController _popupController;
        private readonly ScreenController _screenController;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly IUiAudioPort _uiAudioPort;
        private readonly UIBlockPolicy _uiBlockPolicy;
        private UITickEventKey? _lastStageClearedEventKey;
        private PauseReturnMode _pauseReturnMode;

        public UIFlowCoordinator(
            ScreenController screenController,
            PopupController popupController,
            UIBlockPolicy uiBlockPolicy,
            IUiFlowPauseService pauseService,
            IGameplayUiPresentationSource presentationSource,
            IUiAudioPort uiAudioPort,
            IStageLaunchRouter stageLaunchRouter)
        {
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _uiBlockPolicy = uiBlockPolicy ?? throw new ArgumentNullException(nameof(uiBlockPolicy));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));

            _screenController.StateChanged += HandleFlowStateChanged;
            _screenController.ActionRequested += HandleScreenActionRequested;
            _screenController.ScreenTransitioned += HandleScreenTransitioned;
            _popupController.StateChanged += HandleFlowStateChanged;
            _popupController.PopupOpened += HandlePopupOpened;
            _popupController.PopupCompleted += HandlePopupCompleted;
            _presentationSource.TickEventsApplied += HandleTickEventsApplied;
        }

        public UIBlockSnapshot CurrentBlockSnapshot { get; private set; }

        public void Initialize()
        {
            ClearPauseReturnMode();
            _screenController.SetRoot(BuildGameplayRequest());
            RefreshBlockSnapshot();
        }

        public bool OpenHelpScreen()
        {
            return PushScreen(BuildHelpRequest());
        }

        public bool OpenObjectiveStatusScreen()
        {
            return PushScreen(BuildObjectiveStatusRequest());
        }

        public bool OpenInventoryScreen()
        {
            return PushScreen(BuildInventoryRequest());
        }

        public bool OpenSettingsScreen()
        {
            return PushScreen(BuildSettingsRequest());
        }

        public bool RequestPausePopup()
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

        public bool RequestObjectiveInfoPopup(ObjectiveInfoPopupPayload payload)
        {
            if (_screenController.CurrentScreenId != ScreenId.ObjectiveStatus || payload == null)
            {
                return false;
            }

            return _popupController.Push(new PopupRequest(PopupId.ObjectiveInfo, payload), out _);
        }

        public bool RequestConfirmPopup(
            ConfirmPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            if (payload == null)
            {
                return false;
            }

            return _popupController.Push(
                new PopupRequest(PopupId.Confirm, payload, completionCallback),
                out _);
        }

        public bool RequestTooltipPopup(
            TooltipPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            if (payload == null)
            {
                return false;
            }

            return TryPushPopupRequest(new PopupRequest(PopupId.Tooltip, payload, completionCallback));
        }

        public bool RequestRewardPopup(
            RewardPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            if (payload == null)
            {
                return false;
            }

            return _popupController.Push(
                new PopupRequest(PopupId.Reward, payload, completionCallback),
                out _);
        }

        public bool HandleBackRequested()
        {
            if (_popupController.PopupCount > 0)
            {
                return _popupController.HandleBackRequested();
            }

            if (_screenController.HandleBackRequested())
            {
                if (_pauseReturnMode == PauseReturnMode.RestorePausePopupAfterBack &&
                    _screenController.CurrentScreenId == ScreenId.Gameplay &&
                    RequestPausePopup())
                {
                    ClearPauseReturnMode();
                }

                return true;
            }

            if (_screenController.CurrentScreenId == ScreenId.Gameplay)
            {
                return RequestPausePopup();
            }

            return false;
        }

        public bool HandlePopupBackdropClicked()
        {
            return _popupController.HandleBackdropClicked();
        }

        public void Dispose()
        {
            ClearPauseReturnMode();
            _screenController.StateChanged -= HandleFlowStateChanged;
            _screenController.ActionRequested -= HandleScreenActionRequested;
            _screenController.ScreenTransitioned -= HandleScreenTransitioned;
            _popupController.StateChanged -= HandleFlowStateChanged;
            _popupController.PopupOpened -= HandlePopupOpened;
            _popupController.PopupCompleted -= HandlePopupCompleted;
            _presentationSource.TickEventsApplied -= HandleTickEventsApplied;
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

            // Stage 6 default: clear the current popup stack before screen transitions.
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
                    if (!PushScreen(BuildSettingsRequest(), preservePauseReturnMode: true))
                    {
                        ClearPauseReturnMode();
                        RequestPausePopup();
                    }

                    break;

                case PopupCompletionKind.Resumed:
                case PopupCompletionKind.Closed:
                    ClearPauseReturnMode();
                    _pauseService.Resume();
                    break;

                default:
                    break;
            }
        }

        private void HandleFlowStateChanged()
        {
            RefreshBlockSnapshot();
        }

        private void HandleScreenTransitioned(ScreenTransitionedEvent transitionEvent)
        {
            switch (transitionEvent.Kind)
            {
                case ScreenTransitionKind.Show:
                case ScreenTransitionKind.Push:
                case ScreenTransitionKind.Replace:
                    _uiAudioPort.Play(UiAudioCueId.NavigateForward);
                    break;

                case ScreenTransitionKind.Pop:
                    _uiAudioPort.Play(UiAudioCueId.NavigateBack);
                    break;
            }
        }

        private void HandlePopupOpened(PopupOpenedEvent openedEvent)
        {
            _uiAudioPort.Play(UiAudioCueId.NavigateForward);
        }

        private void HandlePopupCompleted(PopupCompletedEvent completedEvent)
        {
            switch (completedEvent.Entry.PopupId)
            {
                case PopupId.Pause:
                    if (completedEvent.Completion.CompletionKind == PopupCompletionKind.Resumed)
                    {
                        _uiAudioPort.Play(UiAudioCueId.Confirm);
                    }
                    else if (completedEvent.Completion.CompletionKind == PopupCompletionKind.Closed)
                    {
                        _uiAudioPort.Play(UiAudioCueId.NavigateBack);
                    }

                    break;

                case PopupId.ObjectiveInfo:
                case PopupId.Tooltip:
                    _uiAudioPort.Play(UiAudioCueId.NavigateBack);
                    break;

                case PopupId.Confirm:
                    if (completedEvent.Completion.CompletionKind == PopupCompletionKind.Confirmed)
                    {
                        _uiAudioPort.Play(UiAudioCueId.Confirm);
                    }
                    else if (completedEvent.Completion.CompletionKind == PopupCompletionKind.Cancelled)
                    {
                        _uiAudioPort.Play(UiAudioCueId.Cancel);
                    }

                    break;

                case PopupId.Reward:
                    if (completedEvent.Completion.CompletionKind == PopupCompletionKind.Acknowledged)
                    {
                        _uiAudioPort.Play(UiAudioCueId.Confirm);
                    }

                    break;
            }
        }

        public void HandleScreenActionRequested(ScreenAction action)
        {
            switch (action.ActionKind)
            {
                case ScreenActionKind.BackRequested:
                    HandleBackRequested();
                    break;

                case ScreenActionKind.ShowScreen:
                    ShowScreen(action.ScreenRequest);
                    break;

                case ScreenActionKind.PushScreen:
                    PushScreen(action.ScreenRequest);
                    break;

                case ScreenActionKind.ReplaceScreen:
                    ReplaceScreen(action.ScreenRequest);
                    break;

                case ScreenActionKind.RequestPopup:
                    TryPushPopupRequest(action.PopupRequest);
                    break;

                case ScreenActionKind.LaunchStage:
                    LaunchStage(action.StageNavigationRequest);
                    break;
            }
        }

        private bool TryPushPopupRequest(PopupRequest request)
        {
            if (request.PopupId == PopupId.Tooltip &&
                _popupController.TopPopup.HasValue &&
                _popupController.TopPopup.Value.PopupId == PopupId.Tooltip)
            {
                return false;
            }

            return _popupController.Push(request, out _);
        }

        private bool ShowScreen(ScreenRequest request)
        {
            return ShowScreen(request, preservePauseReturnMode: false);
        }

        private bool ShowScreen(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Show(request);
        }

        private bool PushScreen(ScreenRequest request)
        {
            return PushScreen(request, preservePauseReturnMode: false);
        }

        private bool PushScreen(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Push(request);
        }

        private bool ReplaceScreen(ScreenRequest request)
        {
            return ReplaceScreen(request, preservePauseReturnMode: false);
        }

        private bool ReplaceScreen(ScreenRequest request, bool preservePauseReturnMode)
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

                _lastStageClearedEventKey = tickEvent.Key;
                ClearPauseReturnMode();
                ClosePopupsForScreenTransition();
                _screenController.Clear();
                OpenStageCompletionFlow();
                return;
            }
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

            if (readModel.RewardGrantResult != null && readModel.RewardGrantResult.AnyGranted)
            {
                _popupController.Push(
                    new PopupRequest(
                        PopupId.Reward,
                        StageCompletionRewardPopupPayloadMapper.Map(readModel)),
                    out _);
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
