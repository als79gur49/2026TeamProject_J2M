using System;
using System.Text;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class GameplayScreenPresenter
    {
        public GameplayScreenViewModel ViewModel { get; } = new GameplayScreenViewModel();

        public void Apply(GameplayScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.HelpLabel,
                payload.ObjectivesLabel,
                payload.InventoryLabel,
                payload.SettingsLabel);
        }
    }

    public sealed class HelpScreenPresenter
    {
        public HelpScreenViewModel ViewModel { get; } = new HelpScreenViewModel();

        public void Apply(HelpScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(payload.TitleText, payload.DescriptionText, payload.BackLabel);
        }
    }

    public sealed class ObjectiveStatusScreenPresenter : IDisposable
    {
        private readonly ObjectiveStatusPresenter _objectiveStatusPresenter;
        private ObjectiveStatusScreenMode _mode = ObjectiveStatusScreenMode.Overview;
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

        public void ShowOverview()
        {
            _mode = ObjectiveStatusScreenMode.Overview;
            ApplyViewModel();
        }

        public void ShowSession()
        {
            _mode = ObjectiveStatusScreenMode.Session;
            ApplyViewModel();
        }

        public ObjectiveInfoPopupPayload BuildInfoPopupPayload()
        {
            if (_mode == ObjectiveStatusScreenMode.Session)
            {
                return new ObjectiveInfoPopupPayload(
                    "Session Info",
                    $"Tick {_state.NextTickIndex} | Paused: {FormatBoolean(_state.IsPaused)} | Commands: {FormatCommandStatus(_state.CanAcceptGameplayCommands)}");
            }

            if (!_state.HasObjective)
            {
                return new ObjectiveInfoPopupPayload(
                    "Objective Info",
                    "No active objective is configured for this stage.");
            }

            return new ObjectiveInfoPopupPayload(
                "Objective Info",
                $"Goal reached: {FormatBoolean(_state.GoalReached)} | All conditions: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
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
            if (_mode == ObjectiveStatusScreenMode.Session)
            {
                ViewModel.SetContent(
                    _titleText,
                    badgeText: _state.IsPaused ? "Paused" : "Session",
                    summaryText: $"Next Tick: {_state.NextTickIndex}",
                    detailText: $"Gameplay Input: {FormatCommandStatus(_state.CanAcceptGameplayCommands)}",
                    secondaryText: $"Stage Cleared: {FormatBoolean(_state.IsStageCleared)}",
                    isOverviewSelected: false,
                    isSessionSelected: true);
                return;
            }

            ViewModel.SetContent(
                _titleText,
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

        private enum ObjectiveStatusScreenMode
        {
            Overview = 0,
            Session = 1,
        }
    }

    public sealed class InventoryScreenPresenter
    {
        public InventoryScreenViewModel ViewModel { get; } = new InventoryScreenViewModel();

        public void Apply(InventoryScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var builder = new StringBuilder();
            for (var i = 0; i < payload.Items.Count; i++)
            {
                var item = payload.Items[i];
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append("- ");
                builder.Append(item.LabelText);
                builder.Append(": ");
                builder.Append(item.Amount);
            }

            ViewModel.SetContent(payload.TitleText, builder.ToString(), payload.BackLabel);
        }
    }

    public sealed class UiSessionSettingsStore
    {
        public SettingsScreenState State { get; private set; } = new SettingsScreenState(
            areTooltipsEnabled: true,
            isLargeTextEnabled: false);

        public void ToggleTooltips()
        {
            State = new SettingsScreenState(!State.AreTooltipsEnabled, State.IsLargeTextEnabled);
        }

        public void ToggleLargeText()
        {
            State = new SettingsScreenState(State.AreTooltipsEnabled, !State.IsLargeTextEnabled);
        }
    }

    public sealed class SettingsScreenPresenter
    {
        private readonly UiSessionSettingsStore _settingsStore;
        private SettingsScreenPayload _payload = SettingsScreenPayload.Default;

        public SettingsScreenPresenter(UiSessionSettingsStore settingsStore)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        }

        public SettingsScreenViewModel ViewModel { get; } = new SettingsScreenViewModel();

        public void Apply(SettingsScreenPayload payload)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Refresh();
        }

        public void ToggleTooltips()
        {
            _settingsStore.ToggleTooltips();
            Refresh();
        }

        public void ToggleLargeText()
        {
            _settingsStore.ToggleLargeText();
            Refresh();
        }

        private void Refresh()
        {
            var state = _settingsStore.State;
            ViewModel.SetContent(
                _payload.TitleText,
                state.AreTooltipsEnabled ? "Enabled" : "Disabled",
                state.IsLargeTextEnabled ? "Enabled" : "Disabled",
                _payload.TooltipToggleLabel,
                _payload.LargeTextToggleLabel,
                _payload.BackLabel);
        }
    }

    public sealed class StageResultScreenPresenter
    {
        public StageResultScreenViewModel ViewModel { get; } = new StageResultScreenViewModel();

        public void Apply(StageResultScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.SummaryText,
                payload.DetailText,
                payload.ContinueLabel);
        }
    }
}
