using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Composition
{
    internal sealed class MainMenuUiAudioFeedbackController : IDisposable
    {
        private readonly IUiAudioPort _uiAudioPort;
        private MainMenuScreenView _mainMenuScreenView;
        private PopupController _popupController;
        private MainMenuSettingsOverlayController _settingsOverlayController;
        private SaveSlotPanelView _saveSlotPanelView;
        private bool _suppressNextConfirmPopupOpenedCue;
        private bool _suppressNextSettingsOpenedCue;

        public MainMenuUiAudioFeedbackController(IUiAudioPort uiAudioPort)
        {
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
        }

        public void Attach(
            MainMenuScreenView mainMenuScreenView,
            PopupController popupController,
            MainMenuSettingsOverlayController settingsOverlayController)
        {
            Detach();

            _mainMenuScreenView = mainMenuScreenView ?? throw new ArgumentNullException(nameof(mainMenuScreenView));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _settingsOverlayController = settingsOverlayController;
            _saveSlotPanelView = _mainMenuScreenView.SaveSlotPanel;

            _mainMenuScreenView.CommandRequested += HandleCommandRequested;
            _mainMenuScreenView.NavigationRequested += HandleNavigationRequested;

            if (_saveSlotPanelView != null)
            {
                _saveSlotPanelView.SaveSlotIntentRequested += HandleSaveSlotIntentRequested;
            }

            _popupController.PopupOpened += HandlePopupOpened;
            _popupController.PopupCompleted += HandlePopupCompleted;

            if (_settingsOverlayController != null)
            {
                _settingsOverlayController.Opened += HandleSettingsOpened;
                _settingsOverlayController.Closed += HandleSettingsClosed;
            }
        }

        public void Dispose()
        {
            Detach();
        }

        internal void HandleCommandRequested(MainMenuCommandIntent intent)
        {
            switch (intent.CommandKind)
            {
                case MainMenuCommandKind.OpenSettings:
                    Play(UiAudioCueId.PrimaryMenuCommand);
                    _suppressNextSettingsOpenedCue = true;
                    break;

                case MainMenuCommandKind.Quit:
                    Play(UiAudioCueId.PrimaryMenuCommand);
                    _suppressNextConfirmPopupOpenedCue = true;
                    break;
            }
        }

        internal void HandleNavigationRequested(MainMenuNavigationIntent intent)
        {
            if (intent.SectionId == MainMenuSectionId.None)
            {
                return;
            }

            Play(intent.SectionId == MainMenuSectionId.SaveSlots
                ? UiAudioCueId.PrimaryMenuCommand
                : UiAudioCueId.Select);
        }

        internal void HandleSaveSlotIntentRequested(SaveSlotIntent intent)
        {
            if (intent.IntentKind != SaveSlotIntentKind.Continue)
            {
                return;
            }

            if (_popupController != null && _popupController.Contains(PopupId.Confirm))
            {
                return;
            }

            Play(UiAudioCueId.StageLaunch);
        }

        internal void HandlePopupOpened(PopupOpenedEvent openedEvent)
        {
            if (openedEvent.Entry.PopupId != PopupId.Confirm)
            {
                return;
            }

            if (_suppressNextConfirmPopupOpenedCue)
            {
                _suppressNextConfirmPopupOpenedCue = false;
                return;
            }

            Play(UiAudioCueId.NavigateForward);
        }

        internal void HandlePopupCompleted(PopupCompletedEvent completedEvent)
        {
            if (completedEvent.Entry.PopupId != PopupId.Confirm)
            {
                return;
            }

            if (completedEvent.Entry.Payload is ConfirmPopupPayload modePayload &&
                modePayload.IsCampaignModeSelection)
            {
                switch (completedEvent.Completion.CompletionKind)
                {
                    case PopupCompletionKind.Confirmed:
                    case PopupCompletionKind.AlternativeSelected:
                        Play(UiAudioCueId.StageLaunch);
                        break;
                    case PopupCompletionKind.Cancelled:
                        Play(UiAudioCueId.Cancel);
                        break;
                }
                return;
            }

            switch (completedEvent.Completion.CompletionKind)
            {
                case PopupCompletionKind.Confirmed:
                    Play(UiAudioCueId.Confirm);
                    break;

                case PopupCompletionKind.Cancelled:
                    Play(UiAudioCueId.Cancel);
                    break;
            }
        }

        internal void HandleSettingsOpened()
        {
            if (_suppressNextSettingsOpenedCue)
            {
                _suppressNextSettingsOpenedCue = false;
                return;
            }

            Play(UiAudioCueId.NavigateForward);
        }

        internal void HandleSettingsClosed()
        {
            Play(UiAudioCueId.NavigateBack);
        }

        private void Detach()
        {
            if (_mainMenuScreenView != null)
            {
                _mainMenuScreenView.CommandRequested -= HandleCommandRequested;
                _mainMenuScreenView.NavigationRequested -= HandleNavigationRequested;
            }

            if (_saveSlotPanelView != null)
            {
                _saveSlotPanelView.SaveSlotIntentRequested -= HandleSaveSlotIntentRequested;
            }

            if (_popupController != null)
            {
                _popupController.PopupOpened -= HandlePopupOpened;
                _popupController.PopupCompleted -= HandlePopupCompleted;
            }

            if (_settingsOverlayController != null)
            {
                _settingsOverlayController.Opened -= HandleSettingsOpened;
                _settingsOverlayController.Closed -= HandleSettingsClosed;
            }

            _mainMenuScreenView = null;
            _saveSlotPanelView = null;
            _popupController = null;
            _settingsOverlayController = null;
            _suppressNextConfirmPopupOpenedCue = false;
            _suppressNextSettingsOpenedCue = false;
        }

        private void Play(UiAudioCueId cueId)
        {
            _uiAudioPort.Play(cueId);
        }
    }
}
