using System;
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

        private readonly GameplayHudViewModel _viewModel;
        private readonly GameplayHudPresenter _presenter;
        private FeedbackSource _feedbackSource;
        private GameplayHudCommandResult? _lastCommandResult;
        private GameplayHudState _currentState;
        private string _feedbackText = string.Empty;
        private GameplayHudView _view;
        private UIBlockSnapshot _currentBlockSnapshot;

        public HUDController(GameplayHudPresenter presenter)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _viewModel = new GameplayHudViewModel();
            _presenter.StateChanged += HandlePresenterStateChanged;
            _currentState = _presenter.CurrentState;
            ApplyViewModel();
        }

        public GameplayHudViewModel ViewModel => _viewModel;

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
            if (_feedbackSource == FeedbackSource.LocalBlock && !blockSnapshot.BlocksHudInteraction)
            {
                ClearFeedback();
            }

            ApplyViewModel();
        }

        public GameplayHudCommandResult? RequestFlipRight()
        {
            if (_currentBlockSnapshot.BlocksHudInteraction)
            {
                ApplyLocalBlockFeedback();
                return null;
            }

            var result = _presenter.RequestFlipRight();
            ApplyCommandResult(result);
            return result;
        }

        public GameplayHudCommandResult? RequestMoveUp()
        {
            if (_currentBlockSnapshot.BlocksHudInteraction)
            {
                ApplyLocalBlockFeedback();
                return null;
            }

            var result = _presenter.RequestMoveUp();
            ApplyCommandResult(result);
            return result;
        }

        public void Dispose()
        {
            DetachView();
            _presenter.StateChanged -= HandlePresenterStateChanged;
            _presenter.Dispose();
        }

        private void ApplyCommandResult(GameplayHudCommandResult commandResult)
        {
            _lastCommandResult = commandResult;

            if (commandResult.Accepted)
            {
                ClearFeedback();
                return;
            }

            _feedbackSource = FeedbackSource.GameplayRejected;
            _feedbackText = MapFeedback(commandResult.FailureKind);
            ApplyViewModel();
        }

        private void ApplyLocalBlockFeedback()
        {
            _feedbackSource = FeedbackSource.LocalBlock;
            _feedbackText = LocalBlockFeedback;
            _lastCommandResult = null;
            ApplyViewModel();
        }

        private void ApplyViewModel()
        {
            _viewModel.ApplyGameplayState(_currentState);
            _viewModel.SetInteractivity(!_currentBlockSnapshot.BlocksHudInteraction);
            _viewModel.SetFeedback(_feedbackText, _lastCommandResult);
        }

        private void ClearFeedback()
        {
            _feedbackSource = FeedbackSource.None;
            _feedbackText = string.Empty;
            ApplyViewModel();
        }

        private void HandlePresenterStateChanged(GameplayHudState state)
        {
            _currentState = state;

            if ((_feedbackSource == FeedbackSource.GameplayRejected && state.CanAcceptGameplayCommands && !_currentBlockSnapshot.BlocksHudInteraction) ||
                (_feedbackSource == FeedbackSource.LocalBlock && !_currentBlockSnapshot.BlocksHudInteraction))
            {
                _feedbackSource = FeedbackSource.None;
                _feedbackText = string.Empty;
            }

            ApplyViewModel();
        }

        private static string MapFeedback(GameplayHudCommandFailureKind failureKind)
        {
            switch (failureKind)
            {
                case GameplayHudCommandFailureKind.Paused:
                    return PausedFeedback;
                case GameplayHudCommandFailureKind.Busy:
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

        private enum FeedbackSource
        {
            None = 0,
            LocalBlock = 1,
            GameplayRejected = 2,
        }
    }
}
