using System;

namespace Game.Feature.UI.Screens
{
    public readonly struct ObjectiveStatusScreenState
    {
        public ObjectiveStatusScreenState(
            bool hasObjective,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared,
            string objectiveTitle,
            string objectiveSummary,
            string conditionDetailText)
        {
            HasObjective = hasObjective;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            IsCleared = isCleared;
            ObjectiveTitle = objectiveTitle ?? string.Empty;
            ObjectiveSummary = objectiveSummary ?? string.Empty;
            ConditionDetailText = conditionDetailText ?? string.Empty;
        }

        public bool HasObjective { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }

        public string ObjectiveTitle { get; }

        public string ObjectiveSummary { get; }

        public string ConditionDetailText { get; }
    }

    public sealed class ObjectiveStatusScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = "Objective Status";

        public string BadgeText { get; private set; } = string.Empty;

        public string SummaryText { get; private set; } = string.Empty;

        public string DetailText { get; private set; } = string.Empty;

        public string SecondaryText { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string badgeText,
            string summaryText,
            string detailText,
            string secondaryText)
        {
            TitleText = titleText ?? string.Empty;
            BadgeText = badgeText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            SecondaryText = secondaryText ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
