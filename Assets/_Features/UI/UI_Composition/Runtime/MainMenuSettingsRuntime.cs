using System;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Composition
{
    internal sealed class MainMenuSettingsRuntime : IDisposable, IUiNavigationTargetProvider
    {
        private readonly ISettingsScreenRuntime _settingsRuntime;
        private readonly SettingsScreenPayload _payload;
        private readonly PopupController _popupController;
        private bool _isDisposed;
        private bool _isOpen;

        public MainMenuSettingsRuntime(
            SettingsScreenRuntimeBuildContext buildContext,
            SettingsScreenPayload payload,
            PopupController popupController)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _settingsRuntime = new SettingsScreenRuntimeBuilder().Build(
                buildContext ?? throw new ArgumentNullException(nameof(buildContext)));
            _settingsRuntime.ActionRequested += HandleActionRequested;
        }

        public bool IsOpen => _isOpen;

        public SettingsScreenView View => _settingsRuntime.View;

        public event Action CloseRequested;

        public void Open()
        {
            ThrowIfDisposed();
            if (_isOpen)
            {
                _settingsRuntime.SetIsCurrent(true);
                return;
            }

            _settingsRuntime.ApplyPayload(_payload);
            _settingsRuntime.SetIsCurrent(true);
            _isOpen = true;
        }

        public bool TryHandleBackRequested()
        {
            return _isOpen && _settingsRuntime.TryHandleBackRequested();
        }

        public bool TryGetNavigationTarget(out IUiNavigationTarget target)
        {
            return _settingsRuntime.TryGetNavigationTarget(out target);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isOpen = false;
            _settingsRuntime.ActionRequested -= HandleActionRequested;
            _settingsRuntime.Dispose();
        }

        private void HandleActionRequested(ScreenAction action)
        {
            switch (action.ActionKind)
            {
                case ScreenActionKind.BackRequested:
                    CloseRequested?.Invoke();
                    break;

                case ScreenActionKind.RequestPopup:
                    _popupController.Push(action.PopupRequest, out _);
                    break;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MainMenuSettingsRuntime));
            }
        }
    }
}
