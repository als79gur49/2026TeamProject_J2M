using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Flow
{
    public sealed class HUDController : IDisposable
    {
        private HUDRootView _view;

        public HUDController(
            HUDRootViewModel rootViewModel,
            StageInfoViewModel stageInfoViewModel,
            PlayerStatusViewModel playerStatusViewModel,
            ActionBarViewModel actionBarViewModel,
            NotificationViewModel notificationViewModel)
        {
            RootViewModel = rootViewModel ?? throw new ArgumentNullException(nameof(rootViewModel));
            StageInfoViewModel = stageInfoViewModel ?? throw new ArgumentNullException(nameof(stageInfoViewModel));
            PlayerStatusViewModel = playerStatusViewModel ?? throw new ArgumentNullException(nameof(playerStatusViewModel));
            ActionBarViewModel = actionBarViewModel ?? throw new ArgumentNullException(nameof(actionBarViewModel));
            NotificationViewModel = notificationViewModel ?? throw new ArgumentNullException(nameof(notificationViewModel));
        }

        public HUDRootViewModel RootViewModel { get; }

        public StageInfoViewModel StageInfoViewModel { get; }

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
            _view.BindStageInfo(StageInfoViewModel);
            _view.PlayerStatusView.Bind(PlayerStatusViewModel);
            _view.ActionBarView.Bind(ActionBarViewModel);
            _view.NotificationView.Bind(NotificationViewModel);
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

            _view.NotificationView.Bind(null);
            _view.ActionBarView.Bind(null);
            _view.PlayerStatusView.Bind(null);
            _view.BindStageInfo(null);
            _view.Bind(null);
            _view = null;
        }
    }
}
