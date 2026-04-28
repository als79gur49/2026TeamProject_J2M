using System;
using System.Text;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveStatusPresenter : IDisposable
    {
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly IGameplayQueryFacade _queryFacade;

        public ObjectiveStatusPresenter(
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource)
        {
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
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
            var session = _queryFacade.Session.Read();
            var objective = _presentationSource.CurrentSnapshot.Objective;

            CurrentState = new ObjectiveStatusScreenState(
                objective.HasObjective,
                objective.GoalReached,
                objective.AllConditionsSatisfied,
                objective.IsCleared,
                objective.Title,
                objective.Summary,
                BuildConditionDetailText(objective),
                session.NextTickIndex,
                session.IsPaused,
                session.CanAcceptGameplayCommands,
                session.IsStageCleared);
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
            AppendGroup(builder, "Primary", objective, ConditionIsPrimary);
            AppendGroup(builder, "Required", objective, ConditionIsRequiredNonPrimary);
            AppendGroup(builder, "Optional", objective, ConditionIsOptional);
            AppendGroup(builder, "Challenges", objective, ConditionIsChallenge);

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
            var wroteHeading = false;
            var conditions = objective.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (!predicate(condition) ||
                    string.IsNullOrWhiteSpace(condition.TitleText))
                {
                    continue;
                }

                if (!wroteHeading)
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine(heading);
                    wroteHeading = true;
                }

                builder.Append("- ")
                    .Append(condition.IsSatisfied ? "Done: " : "Pending: ")
                    .Append(condition.TitleText);
                if (!string.IsNullOrWhiteSpace(condition.ProgressText))
                {
                    builder.Append(" (")
                        .Append(condition.ProgressText)
                        .Append(')');
                }

                builder.AppendLine();
            }
        }

        private static bool ConditionIsPrimary(UIObjectiveConditionSlice condition)
        {
            return condition.Role == UIObjectiveConditionRole.PrimaryGoal;
        }

        private static bool ConditionIsRequiredNonPrimary(UIObjectiveConditionSlice condition)
        {
            return condition.Required &&
                   condition.Role != UIObjectiveConditionRole.PrimaryGoal &&
                   condition.Role != UIObjectiveConditionRole.Challenge;
        }

        private static bool ConditionIsOptional(UIObjectiveConditionSlice condition)
        {
            return !condition.Required &&
                   condition.Role != UIObjectiveConditionRole.Challenge;
        }

        private static bool ConditionIsChallenge(UIObjectiveConditionSlice condition)
        {
            return condition.Role == UIObjectiveConditionRole.Challenge;
        }
    }
}
