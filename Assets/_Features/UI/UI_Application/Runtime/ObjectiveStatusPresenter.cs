using System;
using System.Text;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveStatusPresenter : IDisposable
    {
        private readonly IGameplayUiPresentationSource _presentationSource;

        public ObjectiveStatusPresenter(IGameplayUiPresentationSource presentationSource)
        {
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));

            _presentationSource.SnapshotChanged += HandleSnapshotChanged;

            Refresh();
        }

        public event Action<ObjectiveStatusScreenState> StateChanged;

        public ObjectiveStatusScreenState CurrentState { get; private set; }

        public void Dispose()
        {
            _presentationSource.SnapshotChanged -= HandleSnapshotChanged;
        }

        public void Refresh()
        {
            var objective = _presentationSource.CurrentSnapshot.Objective;

            CurrentState = new ObjectiveStatusScreenState(
                objective.HasObjective,
                objective.GoalReached,
                objective.AllConditionsSatisfied,
                objective.IsCleared,
                objective.Title,
                objective.Summary,
                BuildConditionDetailText(objective));
            StateChanged?.Invoke(CurrentState);
        }

        private void HandleSnapshotChanged(UIPresentationSnapshot _)
        {
            Refresh();
        }

        private static string BuildConditionDetailText(UIObjectiveSlice objective)
        {
            if (!objective.HasObjective)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendGroup(
                builder,
                "Primary",
                objective,
                condition => condition.Role == UIObjectiveConditionRole.PrimaryGoal);
            AppendGroup(
                builder,
                "Required",
                objective,
                condition => condition.Required && condition.Role != UIObjectiveConditionRole.PrimaryGoal);
            AppendGroup(
                builder,
                "Optional",
                objective,
                condition => !condition.Required && condition.Role != UIObjectiveConditionRole.Challenge);
            AppendGroup(
                builder,
                "Challenges",
                objective,
                condition => condition.Role == UIObjectiveConditionRole.Challenge);

            return builder.Length == 0
                ? "No displayable objective conditions."
                : builder.ToString().TrimEnd();
        }

        private static void AppendGroup(
            StringBuilder builder,
            string heading,
            UIObjectiveSlice objective,
            Func<UIObjectiveConditionSlice, bool> predicate)
        {
            var appendedHeading = false;
            var conditions = objective.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (!predicate(condition) || string.IsNullOrWhiteSpace(condition.TitleText))
                {
                    continue;
                }

                if (!appendedHeading)
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine($"{heading}:");
                    appendedHeading = true;
                }

                builder.Append("- ");
                builder.Append(condition.IsSatisfied ? "Done" : "Pending");
                builder.Append(": ");
                builder.Append(condition.TitleText);
                if (!string.IsNullOrWhiteSpace(condition.ProgressText))
                {
                    builder.Append(" (");
                    builder.Append(condition.ProgressText);
                    builder.Append(')');
                }

                builder.AppendLine();
            }
        }
    }
}
