using System;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveStatusScreenPresenter : IDisposable
    {
        private readonly ObjectiveStatusPresenter _objectiveStatusPresenter;
        private ObjectiveStatusScreenState _state;
        private string _titleText = ObjectiveStatusScreenPayload.Default.TitleText;

        public ObjectiveStatusScreenPresenter(ObjectiveStatusPresenter objectiveStatusPresenter)
        {
            _objectiveStatusPresenter = objectiveStatusPresenter ?? throw new ArgumentNullException(nameof(objectiveStatusPresenter));
            _state = objectiveStatusPresenter.CurrentState;
            _objectiveStatusPresenter.StateChanged += HandleStateChanged;
            ApplyViewModel();
        }

        public ObjectiveStatusScreenViewModel ViewModel { get; } = new ObjectiveStatusScreenViewModel();

        public void ApplyPayload(ObjectiveStatusScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _titleText = payload.TitleText;
            ApplyViewModel();
        }

        public ObjectiveInfoPopupPayload BuildInfoPopupPayload()
        {
            if (!_state.HasObjective)
            {
                return new ObjectiveInfoPopupPayload(
                    "Objective Info",
                    "No active objective is configured for this stage.");
            }

            return new ObjectiveInfoPopupPayload(
                "Objective Info",
                $"{BuildObjectiveSummary(_state)} Goal reached: {FormatBoolean(_state.GoalReached)} | All conditions: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
        }

        public void Dispose()
        {
            _objectiveStatusPresenter.StateChanged -= HandleStateChanged;
            _objectiveStatusPresenter.Dispose();
        }

        private void HandleStateChanged(ObjectiveStatusScreenState state)
        {
            _state = state;
            ApplyViewModel();
        }

        private void ApplyViewModel()
        {
            ViewModel.SetContent(
                _titleText,
                badgeText: BuildObjectiveBadge(_state),
                summaryText: BuildObjectiveSummary(_state),
                detailText: BuildObjectiveDetailText(_state),
                secondaryText: $"Goal: {FormatBoolean(_state.GoalReached)} | Required: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
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

            if (!string.IsNullOrWhiteSpace(state.ObjectiveTitle) &&
                !string.IsNullOrWhiteSpace(state.ObjectiveSummary))
            {
                return $"{state.ObjectiveTitle}: {state.ObjectiveSummary}";
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveTitle))
            {
                return state.ObjectiveTitle;
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveSummary))
            {
                return state.ObjectiveSummary;
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

        private static string BuildObjectiveDetailText(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "No objective conditions are configured for display.";
            }

            return string.IsNullOrWhiteSpace(state.ConditionDetailText)
                ? "No displayable objective conditions."
                : state.ConditionDetailText;
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "Yes" : "No";
        }

    }
}
