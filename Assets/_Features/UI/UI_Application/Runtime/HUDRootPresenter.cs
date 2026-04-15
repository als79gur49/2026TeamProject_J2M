using System;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class HUDRootPresenter : IDisposable
    {
        private readonly ActionBarPresenter _actionBarPresenter;
        private readonly PlayerStatusPresenter _playerStatusPresenter;
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly NotificationPresenter _notificationPresenter;

        public HUDRootPresenter(
            IGameplayUiPresentationSource presentationSource,
            PlayerStatusPresenter playerStatusPresenter,
            ActionBarPresenter actionBarPresenter,
            NotificationPresenter notificationPresenter)
        {
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _playerStatusPresenter = playerStatusPresenter ?? throw new ArgumentNullException(nameof(playerStatusPresenter));
            _actionBarPresenter = actionBarPresenter ?? throw new ArgumentNullException(nameof(actionBarPresenter));
            _notificationPresenter = notificationPresenter ?? throw new ArgumentNullException(nameof(notificationPresenter));

            ViewModel = new HUDRootViewModel();
            _presentationSource.SnapshotChanged += HandleSnapshotChanged;
            _presentationSource.TickEventsApplied += HandleTickEventsApplied;

            ApplySnapshot(_presentationSource.CurrentSnapshot);
        }

        public HUDRootViewModel ViewModel { get; }

        public void Dispose()
        {
            _presentationSource.SnapshotChanged -= HandleSnapshotChanged;
            _presentationSource.TickEventsApplied -= HandleTickEventsApplied;
        }

        private void HandleSnapshotChanged(UIPresentationSnapshot snapshot)
        {
            ApplySnapshot(snapshot);
        }

        private void HandleTickEventsApplied(UITickEventBatch batch)
        {
            _actionBarPresenter.HandleTickEvents(batch);
        }

        private void ApplySnapshot(UIPresentationSnapshot snapshot)
        {
            ViewModel.SetShellState(
                isVisible: true,
                isDimmed: snapshot.Interaction.IsPaused ||
                          snapshot.Interaction.IsUiGameplayInputBlocked ||
                          snapshot.Interaction.HasBlockingGameplayPresentation,
                isPauseButtonEnabled: !snapshot.Interaction.IsPaused &&
                                      !snapshot.Interaction.IsUiGameplayInputBlocked);

            _playerStatusPresenter.Apply(snapshot.Tick, snapshot.Interaction, snapshot.Player);
            _actionBarPresenter.Apply(snapshot.Tick, snapshot.Interaction, snapshot.Player);
            _notificationPresenter.Apply(snapshot.Notifications);
        }
    }
}
