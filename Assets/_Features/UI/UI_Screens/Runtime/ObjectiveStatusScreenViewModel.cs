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
            int nextTickIndex,
            bool isPaused,
            bool canAcceptGameplayCommands,
            bool isStageCleared)
        {
            HasObjective = hasObjective;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            IsCleared = isCleared;
            NextTickIndex = nextTickIndex;
            IsPaused = isPaused;
            CanAcceptGameplayCommands = canAcceptGameplayCommands;
            IsStageCleared = isStageCleared;
        }

        public bool HasObjective { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }

        public int NextTickIndex { get; }

        public bool IsPaused { get; }

        public bool CanAcceptGameplayCommands { get; }

        public bool IsStageCleared { get; }
    }

    public readonly struct ObjectiveInfoPopupContent
    {
        public ObjectiveInfoPopupContent(string title, string body)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public string Title { get; }

        public string Body { get; }
    }

    public sealed class ObjectiveStatusScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = "Objective Status";

        public string BadgeText { get; private set; } = string.Empty;

        public string SummaryText { get; private set; } = string.Empty;

        public string DetailText { get; private set; } = string.Empty;

        public string SecondaryText { get; private set; } = string.Empty;

        public bool IsOverviewSelected { get; private set; }

        public bool IsSessionSelected { get; private set; }

        public void SetContent(
            string badgeText,
            string summaryText,
            string detailText,
            string secondaryText,
            bool isOverviewSelected,
            bool isSessionSelected)
        {
            BadgeText = badgeText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            SecondaryText = secondaryText ?? string.Empty;
            IsOverviewSelected = isOverviewSelected;
            IsSessionSelected = isSessionSelected;
            Changed?.Invoke();
        }
    }
}
