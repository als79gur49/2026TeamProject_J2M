using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    public sealed class ObjectiveStatusScreenController : IDisposable
    {
        private readonly ObjectiveStatusPresenter _presenter;
        private readonly ObjectiveStatusScreenViewModel _viewModel;
        private ObjectiveStatusScreenMode _mode = ObjectiveStatusScreenMode.Overview;
        private ObjectiveStatusScreenState _state;
        private ObjectiveStatusScreenView _view;

        public ObjectiveStatusScreenController(ObjectiveStatusPresenter presenter)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _viewModel = new ObjectiveStatusScreenViewModel();
            _state = _presenter.CurrentState;
            _presenter.StateChanged += HandleStateChanged;
            ApplyViewModel();
        }

        public event Action BackRequested;

        public event Action InfoRequested;

        public ObjectiveStatusScreenViewModel ViewModel => _viewModel;

        public void AttachView(ObjectiveStatusScreenView view)
        {
            if (ReferenceEquals(_view, view))
            {
                return;
            }

            DetachView();

            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.Bind(_viewModel);
            _view.OverviewRequested += HandleOverviewRequested;
            _view.SessionRequested += HandleSessionRequested;
            _view.InfoRequested += HandleInfoRequested;
            _view.BackRequested += HandleBackRequested;
        }

        public void SetVisible(bool isVisible)
        {
            if (_view == null)
            {
                return;
            }

            _view.IsVisible = isVisible;
        }

        public ObjectiveInfoPopupContent BuildInfoContent()
        {
            if (_mode == ObjectiveStatusScreenMode.Session)
            {
                return new ObjectiveInfoPopupContent(
                    "Session Info",
                    $"Tick {_state.NextTickIndex} | Paused: {FormatBoolean(_state.IsPaused)} | Commands: {FormatCommandStatus(_state.CanAcceptGameplayCommands)}");
            }

            if (!_state.HasObjective)
            {
                return new ObjectiveInfoPopupContent(
                    "Objective Info",
                    "No active objective is configured for this stage.");
            }

            return new ObjectiveInfoPopupContent(
                "Objective Info",
                $"Goal reached: {FormatBoolean(_state.GoalReached)} | All conditions: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
        }

        public void Dispose()
        {
            DetachView();
            _presenter.StateChanged -= HandleStateChanged;
            _presenter.Dispose();
        }

        private void ApplyViewModel()
        {
            if (_mode == ObjectiveStatusScreenMode.Session)
            {
                _viewModel.SetContent(
                    badgeText: _state.IsPaused ? "Paused" : "Session",
                    summaryText: $"Next Tick: {_state.NextTickIndex}",
                    detailText: $"Gameplay Input: {FormatCommandStatus(_state.CanAcceptGameplayCommands)}",
                    secondaryText: $"Stage Cleared: {FormatBoolean(_state.IsStageCleared)}",
                    isOverviewSelected: false,
                    isSessionSelected: true);
                return;
            }

            _viewModel.SetContent(
                badgeText: BuildObjectiveBadge(_state),
                summaryText: BuildObjectiveSummary(_state),
                detailText: $"Goal Reached: {FormatBoolean(_state.GoalReached)}",
                secondaryText: $"All Conditions: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}",
                isOverviewSelected: true,
                isSessionSelected: false);
        }

        private static string BuildObjectiveBadge(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "No Objective";
            }

            if (state.IsCleared)
            {
                return "Cleared";
            }

            if (state.AllConditionsSatisfied)
            {
                return "Ready";
            }

            if (state.GoalReached)
            {
                return "Goal Reached";
            }

            return "Pending";
        }

        private static string BuildObjectiveSummary(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "This stage currently has no active objective.";
            }

            if (state.IsCleared)
            {
                return "The objective chain is fully cleared.";
            }

            if (state.AllConditionsSatisfied)
            {
                return "All objective conditions are currently satisfied.";
            }

            if (state.GoalReached)
            {
                return "Primary goal reached. Waiting on remaining conditions.";
            }

            return "Primary goal is still in progress.";
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "Yes" : "No";
        }

        private static string FormatCommandStatus(bool canAcceptGameplayCommands)
        {
            return canAcceptGameplayCommands ? "Ready" : "Blocked";
        }

        private void DetachView()
        {
            if (_view == null)
            {
                return;
            }

            _view.OverviewRequested -= HandleOverviewRequested;
            _view.SessionRequested -= HandleSessionRequested;
            _view.InfoRequested -= HandleInfoRequested;
            _view.BackRequested -= HandleBackRequested;
            _view.Bind(null);
            _view = null;
        }

        private void HandleBackRequested()
        {
            BackRequested?.Invoke();
        }

        private void HandleInfoRequested()
        {
            InfoRequested?.Invoke();
        }

        private void HandleOverviewRequested()
        {
            _mode = ObjectiveStatusScreenMode.Overview;
            ApplyViewModel();
        }

        private void HandleSessionRequested()
        {
            _mode = ObjectiveStatusScreenMode.Session;
            ApplyViewModel();
        }

        private void HandleStateChanged(ObjectiveStatusScreenState state)
        {
            _state = state;
            ApplyViewModel();
        }

        private enum ObjectiveStatusScreenMode
        {
            Overview = 0,
            Session = 1,
        }
    }
}
