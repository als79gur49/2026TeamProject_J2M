using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Shared.Audio;

namespace Game.Feature.UI.Composition
{
    internal sealed class GameplayPauseAudioBridge : IDisposable
    {
        private readonly IAudioPlaybackPauseService _audioPauseService;
        private readonly IGameplayPauseService _pauseService;
        private readonly PopupController _popupController;
        private bool _suppressNextResume;

        public GameplayPauseAudioBridge(
            IGameplayPauseService pauseService,
            IAudioPlaybackPauseService audioPauseService,
            PopupController popupController)
        {
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            _audioPauseService = audioPauseService ?? throw new ArgumentNullException(nameof(audioPauseService));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));

            _pauseService.PauseChanged += HandlePauseChanged;
            _popupController.PopupCompletionDispatching += HandlePopupCompletionDispatching;
            ApplyCurrentPauseState();
        }

        public void Dispose()
        {
            _pauseService.PauseChanged -= HandlePauseChanged;
            _popupController.PopupCompletionDispatching -= HandlePopupCompletionDispatching;
        }

        private void HandlePopupCompletionDispatching(PopupController.PopupCompletionDispatchEvent dispatchEvent)
        {
            if (dispatchEvent.Completion.PopupId != PopupId.Pause ||
                dispatchEvent.Completion.CloseReason != PopupCloseReason.UserAction)
            {
                return;
            }

            if (dispatchEvent.Completion.CompletionKind == PopupCompletionKind.RetryRequested ||
                dispatchEvent.Completion.CompletionKind == PopupCompletionKind.MainMenuRequested)
            {
                _suppressNextResume = true;
            }
        }

        private void HandlePauseChanged(bool isPaused)
        {
            if (isPaused)
            {
                _suppressNextResume = false;
                PauseGameplayPresentation();
                return;
            }

            if (_suppressNextResume)
            {
                _suppressNextResume = false;
                return;
            }

            ResumeGameplayPresentation();
        }

        private void ApplyCurrentPauseState()
        {
            if (_pauseService.IsPaused)
            {
                PauseGameplayPresentation();
                return;
            }

            ResumeGameplayPresentation();
        }

        private void PauseGameplayPresentation()
        {
            _audioPauseService.PauseGroup(
                AudioPlaybackPauseGroup.GameplayPresentation,
                AudioPauseReason.GameplayPause);
        }

        private void ResumeGameplayPresentation()
        {
            _audioPauseService.ResumeGroup(
                AudioPlaybackPauseGroup.GameplayPresentation,
                AudioPauseReason.GameplayPause);
        }
    }
}
