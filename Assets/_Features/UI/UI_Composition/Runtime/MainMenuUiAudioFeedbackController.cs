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
            // Command-owned visible deltas are emitted by settings overlay and popup lifecycle events.
        }

        internal void HandleNavigationRequested(MainMenuNavigationIntent intent)
        {
            if (intent.SectionId == MainMenuSectionId.None)
            {
                return;
            }

            Play(UiAudioCueId.Select);
        }

        internal void HandleSaveSlotIntentRequested(SaveSlotIntent intent)
        {
            if (!IsNormalSaveSlotIntent(intent))
            {
                return;
            }

            if (_popupController != null && _popupController.Contains(PopupId.Confirm))
            {
                return;
            }

            Play(UiAudioCueId.Select);
        }

        internal void HandlePopupOpened(PopupOpenedEvent openedEvent)
        {
            if (openedEvent.Entry.PopupId != PopupId.Confirm)
            {
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
        }

        private static bool IsNormalSaveSlotIntent(SaveSlotIntent intent)
        {
            switch (intent.IntentKind)
            {
                case SaveSlotIntentKind.NewGame:
                case SaveSlotIntentKind.Continue:
                    return true;

                default:
                    return false;
            }
        }

        private void Play(UiAudioCueId cueId)
        {
            _uiAudioPort.Play(cueId);
        }
    }
}
