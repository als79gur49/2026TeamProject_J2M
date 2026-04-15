using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    public sealed class UIFlowCoordinator : IDisposable
    {
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly IUiFlowPauseService _pauseService;
        private readonly PopupController _popupController;
        private readonly ScreenController _screenController;
        private readonly UIBlockPolicy _uiBlockPolicy;
        private UITickEventKey? _lastStageClearedEventKey;

        public UIFlowCoordinator(
            ScreenController screenController,
            PopupController popupController,
            UIBlockPolicy uiBlockPolicy,
            IUiFlowPauseService pauseService,
            IGameplayUiPresentationSource presentationSource)
        {
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _uiBlockPolicy = uiBlockPolicy ?? throw new ArgumentNullException(nameof(uiBlockPolicy));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));

            _screenController.StateChanged += HandleFlowStateChanged;
            _screenController.ActionRequested += HandleScreenActionRequested;
            _popupController.StateChanged += HandleFlowStateChanged;
            _presentationSource.TickEventsApplied += HandleTickEventsApplied;
        }

        public UIBlockSnapshot CurrentBlockSnapshot { get; private set; }

        public void Initialize()
        {
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

            return _popupController.Push(
                new PopupRequest(PopupId.Tooltip, payload, completionCallback),
                out _);
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
            _screenController.StateChanged -= HandleFlowStateChanged;
            _screenController.ActionRequested -= HandleScreenActionRequested;
            _popupController.StateChanged -= HandleFlowStateChanged;
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

            _pauseService.Resume();
        }

        private void HandleFlowStateChanged()
        {
            RefreshBlockSnapshot();
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
                    _popupController.Push(action.PopupRequest, out _);
                    break;
            }
        }

        private bool ShowScreen(ScreenRequest request)
        {
            ClosePopupsForScreenTransition();
            return _screenController.Show(request);
        }

        private bool PushScreen(ScreenRequest request)
        {
            ClosePopupsForScreenTransition();
            return _screenController.Push(request);
        }

        private bool ReplaceScreen(ScreenRequest request)
        {
            ClosePopupsForScreenTransition();
            return _screenController.Replace(request);
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
                ClosePopupsForScreenTransition();
                _screenController.Clear();
                _screenController.SetRoot(BuildStageResultRequest(batch.TickIndex));
                return;
            }
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

        private static ScreenRequest BuildStageResultRequest(int tickIndex)
        {
            return new ScreenRequest(
                ScreenId.StageResult,
                new StageResultScreenPayload(
                    "Stage Cleared",
                    $"Tick {tickIndex} completed.",
                    "Stage 7 validates terminal screen flow without widening gameplay-to-UI contracts.",
                    "Continue"),
                ScreenId.StageResult.ToString());
        }
    }
}
