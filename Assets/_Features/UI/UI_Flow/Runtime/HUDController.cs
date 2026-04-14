using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Flow
{
    public sealed class HUDController : IDisposable
    {
        private const string LocalBlockFeedback = "HUD blocked";
        private const string BusyFeedback = "Busy";
        private const string PausedFeedback = "Paused";
        private const string UnavailableFeedback = "Unavailable";

        private readonly GameplayHudPresenter _presenter;
        private GameplayHudView _view;
        private UIBlockSnapshot _currentBlockSnapshot;

        public HUDController(GameplayHudPresenter presenter)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _presenter.ViewModel.SetInteractivity(true);
        }

        public GameplayHudViewModel ViewModel => _presenter.ViewModel;

        public void AttachView(GameplayHudView view)
        {
            if (ReferenceEquals(_view, view))
            {
                return;
            }

            DetachView();

            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.Bind(ViewModel);
            _view.MoveUpRequested += HandleMoveUpRequested;
            _view.FlipRightRequested += HandleFlipRightRequested;
            _view.IsVisible = true;
        }

        public void ApplyBlockSnapshot(UIBlockSnapshot blockSnapshot)
        {
            _currentBlockSnapshot = blockSnapshot;
            ViewModel.SetInteractivity(!blockSnapshot.BlocksHudInteraction);
        }

        public GameplayCommandAcceptance? RequestFlipRight()
        {
            if (_currentBlockSnapshot.BlocksHudInteraction)
            {
                ViewModel.SetFeedback(LocalBlockFeedback, null);
                return null;
            }

            var acceptance = _presenter.RequestFlip(Direction.Right);
            ApplyAcceptanceFeedback(acceptance);
            return acceptance;
        }

        public GameplayCommandAcceptance? RequestMoveUp()
        {
            if (_currentBlockSnapshot.BlocksHudInteraction)
            {
                ViewModel.SetFeedback(LocalBlockFeedback, null);
                return null;
            }

            var acceptance = _presenter.SetHeldMoveDirection(Direction.Up);
            ApplyAcceptanceFeedback(acceptance);
            return acceptance;
        }

        public void Dispose()
        {
            DetachView();
            _presenter.Dispose();
        }

        private void ApplyAcceptanceFeedback(GameplayCommandAcceptance acceptance)
        {
            if (acceptance.Accepted)
            {
                ViewModel.SetFeedback(string.Empty, acceptance);
                return;
            }

            ViewModel.SetFeedback(MapFeedback(acceptance.RejectionReason), acceptance);
        }

        private static string MapFeedback(GameplayCommandRejectionReason rejectionReason)
        {
            switch (rejectionReason)
            {
                case GameplayCommandRejectionReason.Paused:
                    return PausedFeedback;
                case GameplayCommandRejectionReason.BlockingPresentation:
                    return BusyFeedback;
                default:
                    return UnavailableFeedback;
            }
        }

        private void DetachView()
        {
            if (_view == null)
            {
                return;
            }

            _view.MoveUpRequested -= HandleMoveUpRequested;
            _view.FlipRightRequested -= HandleFlipRightRequested;
            _view.Bind(null);
            _view = null;
        }

        private void HandleMoveUpRequested()
        {
            RequestMoveUp();
        }

        private void HandleFlipRightRequested()
        {
            RequestFlipRight();
        }
    }
}
