using System;
using System.Collections.Generic;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveHudPresenter
    {
        private const int MaxHudSubGoalRows = 2;

        public ObjectiveHudViewModel ViewModel { get; } = new();

        public void Apply(UIObjectiveSlice objective)
        {
            if (!objective.HasObjective)
            {
                ViewModel.SetState(
                    false,
                    string.Empty,
                    string.Empty,
                    Array.Empty<string>(),
                    0,
                    false);
                return;
            }

            var mainGoalText = ResolveMainGoalText(objective);
            ViewModel.SetState(
                true,
                mainGoalText,
                BuildProgressText(objective),
                BuildHudSubGoalTexts(objective, mainGoalText, out var hiddenSubGoalCount),
                hiddenSubGoalCount,
                objective.IsCleared);
        }

        private static string ResolveMainGoalText(UIObjectiveSlice objective)
        {
            if (!string.IsNullOrWhiteSpace(objective.Title))
            {
                return objective.Title;
            }

            var conditions = objective.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i].Role == UIObjectiveConditionRole.PrimaryGoal &&
                    !string.IsNullOrWhiteSpace(conditions[i].TitleText))
                {
                    return conditions[i].TitleText;
                }
            }

            if (!string.IsNullOrWhiteSpace(objective.Summary))
            {
                return objective.Summary;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i].Required &&
                    !string.IsNullOrWhiteSpace(conditions[i].TitleText))
                {
                    return conditions[i].TitleText;
                }
            }

            return string.Empty;
        }

        private static string BuildProgressText(UIObjectiveSlice objective)
        {
            var conditions = objective.Conditions;
            var totalRequired = 0;
            var satisfiedRequired = 0;

            for (var i = 0; i < conditions.Count; i++)
            {
                if (!conditions[i].Required)
                {
                    continue;
                }

                totalRequired++;
                if (conditions[i].IsSatisfied)
                {
                    satisfiedRequired++;
                }
            }

            return totalRequired > 0 ? $"{satisfiedRequired}/{totalRequired}" : string.Empty;
        }

        private static IReadOnlyList<string> BuildHudSubGoalTexts(
            UIObjectiveSlice objective,
            string mainGoalText,
            out int hiddenSubGoalCount)
        {
            var pending = new List<UIObjectiveConditionSlice>();
            var done = new List<UIObjectiveConditionSlice>();
            var conditions = objective.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (!condition.Required || string.IsNullOrWhiteSpace(condition.TitleText))
                {
                    continue;
                }

                if (ShouldHideDuplicatedPrimaryGoal(objective, condition, mainGoalText))
                {
                    continue;
                }

                if (condition.IsSatisfied)
                {
                    done.Add(condition);
                }
                else
                {
                    pending.Add(condition);
                }
            }

            var selected = new List<string>(MaxHudSubGoalRows);
            AppendSubGoalRows(selected, pending);
            AppendSubGoalRows(selected, done);

            var totalDisplayable = pending.Count + done.Count;
            hiddenSubGoalCount = Math.Max(0, totalDisplayable - selected.Count);
            return selected;
        }

        private static bool ShouldHideDuplicatedPrimaryGoal(
            UIObjectiveSlice objective,
            UIObjectiveConditionSlice condition,
            string mainGoalText)
        {
            return string.IsNullOrWhiteSpace(objective.Title) &&
                condition.Role == UIObjectiveConditionRole.PrimaryGoal &&
                string.Equals(condition.TitleText, mainGoalText, StringComparison.Ordinal);
        }

        private static void AppendSubGoalRows(
            ICollection<string> destination,
            IReadOnlyList<UIObjectiveConditionSlice> source)
        {
            for (var i = 0; i < source.Count && destination.Count < MaxHudSubGoalRows; i++)
            {
                var condition = source[i];
                destination.Add($"{(condition.IsSatisfied ? "[x]" : "[ ]")} {condition.TitleText}");
            }
        }
    }
}
