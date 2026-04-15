using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Flow
{
    public sealed class HUDController : IDisposable
    {
        private readonly ActionBarPresenter _actionBarPresenter;
        private HUDRootView _view;

        public HUDController(
            HUDRootViewModel rootViewModel,
            PlayerStatusViewModel playerStatusViewModel,
            ActionBarViewModel actionBarViewModel,
            NotificationViewModel notificationViewModel,
            ActionBarPresenter actionBarPresenter)
        {
            RootViewModel = rootViewModel ?? throw new ArgumentNullException(nameof(rootViewModel));
            PlayerStatusViewModel = playerStatusViewModel ?? throw new ArgumentNullException(nameof(playerStatusViewModel));
            ActionBarViewModel = actionBarViewModel ?? throw new ArgumentNullException(nameof(actionBarViewModel));
            NotificationViewModel = notificationViewModel ?? throw new ArgumentNullException(nameof(notificationViewModel));
            _actionBarPresenter = actionBarPresenter ?? throw new ArgumentNullException(nameof(actionBarPresenter));
        }

        public HUDRootViewModel RootViewModel { get; }

        public PlayerStatusViewModel PlayerStatusViewModel { get; }

        public ActionBarViewModel ActionBarViewModel { get; }

        public NotificationViewModel NotificationViewModel { get; }

        public void AttachView(HUDRootView view)
        {
            if (ReferenceEquals(_view, view))
            {
                return;
            }

            DetachView();

            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.Bind(RootViewModel);
            _view.PlayerStatusView.Bind(PlayerStatusViewModel);
            _view.ActionBarView.Bind(ActionBarViewModel);
            _view.NotificationView.Bind(NotificationViewModel);
            _view.ActionBarView.SlotRequested += HandleSlotRequested;
            _view.IsVisible = true;
        }

        public void Dispose()
        {
            DetachView();
        }

        private void DetachView()
        {
            if (_view == null)
            {
                return;
            }

            _view.ActionBarView.SlotRequested -= HandleSlotRequested;
            _view.NotificationView.Bind(null);
            _view.ActionBarView.Bind(null);
            _view.PlayerStatusView.Bind(null);
            _view.Bind(null);
            _view = null;
        }

        private void HandleSlotRequested(HudActionSlotId slotId)
        {
            _actionBarPresenter.RequestSlot(slotId);
        }
    }
}
