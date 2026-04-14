using System;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.UI.Flow
{
    public sealed class UIFlowCoordinator : IDisposable
    {
        private readonly HUDController _hudController;
        private readonly IGameplayPauseService _pauseService;
        private readonly PopupController _popupController;
        private readonly ScreenController _screenController;
        private readonly UIBlockPolicy _uiBlockPolicy;

        public UIFlowCoordinator(
            ScreenController screenController,
            PopupController popupController,
            HUDController hudController,
            UIBlockPolicy uiBlockPolicy,
            IGameplayPauseService pauseService)
        {
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _hudController = hudController ?? throw new ArgumentNullException(nameof(hudController));
            _uiBlockPolicy = uiBlockPolicy ?? throw new ArgumentNullException(nameof(uiBlockPolicy));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));

            _screenController.StateChanged += HandleFlowStateChanged;
            _popupController.StateChanged += HandleFlowStateChanged;
        }

        public UIBlockSnapshot CurrentBlockSnapshot { get; private set; }

        public void Initialize()
        {
            _screenController.SetRoot(ScreenId.Gameplay);
            ApplyBlockSnapshot();
        }

        public bool OpenHelpScreen()
        {
            if (_popupController.PopupCount > 0)
            {
                return false;
            }

            return _screenController.Push(ScreenId.Help);
        }

        public bool RequestPausePopup()
        {
            if (!_popupController.Push(new PopupEntry(PopupId.Pause, isModal: true)))
            {
                return false;
            }

            _pauseService.Pause();
            return true;
        }

        public bool HandleBackRequested()
        {
            if (_popupController.CanPop)
            {
                return CloseTopPopup();
            }

            if (_screenController.CanPop)
            {
                return _screenController.Pop();
            }

            if (_screenController.CurrentScreenId == ScreenId.Gameplay)
            {
                return RequestPausePopup();
            }

            return false;
        }

        public void Dispose()
        {
            _screenController.StateChanged -= HandleFlowStateChanged;
            _popupController.StateChanged -= HandleFlowStateChanged;
        }

        private void ApplyBlockSnapshot()
        {
            CurrentBlockSnapshot = _uiBlockPolicy.Evaluate(
                _screenController.CurrentScreenId,
                _popupController.TopPopup,
                _popupController.PopupCount);
            _hudController.ApplyBlockSnapshot(CurrentBlockSnapshot);
        }

        private bool CloseTopPopup()
        {
            if (!_popupController.PopTop(out var poppedEntry))
            {
                return false;
            }

            if (poppedEntry.PopupId == PopupId.Pause)
            {
                _pauseService.Resume();
            }

            return true;
        }

        private void HandleFlowStateChanged()
        {
            ApplyBlockSnapshot();
        }
    }
}
