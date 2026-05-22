using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public enum GameplayObjectiveConditionRole
    {
        None = 0,
        PrimaryGoal = 1,
        SecondaryGoal = 2,
        Challenge = 3,
    }

    public readonly struct GameplayObjectiveConditionReadModel
    {
        public GameplayObjectiveConditionReadModel(
            string stableId,
            GameplayObjectiveConditionRole role,
            bool required,
            bool isSatisfied,
            string titleText,
            string progressText,
            int sortOrder)
        {
            StableId = stableId ?? string.Empty;
            Role = role;
            Required = required;
            IsSatisfied = isSatisfied;
            TitleText = titleText ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            SortOrder = sortOrder;
        }

        public string StableId { get; }

        public GameplayObjectiveConditionRole Role { get; }

        public bool Required { get; }

        public bool IsSatisfied { get; }

        public string TitleText { get; }

        public string ProgressText { get; }

        public int SortOrder { get; }
    }

    public readonly struct GameplayObjectiveReadModel
    {
        public static readonly GameplayObjectiveReadModel NoObjective = new(
            false,
            false,
            false,
            false,
            string.Empty,
            string.Empty,
            Array.Empty<GameplayObjectiveConditionReadModel>());

        public GameplayObjectiveReadModel(
            bool hasObjective,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared)
            : this(
                hasObjective,
                goalReached,
                allConditionsSatisfied,
                isCleared,
                string.Empty,
                string.Empty,
                Array.Empty<GameplayObjectiveConditionReadModel>())
        {
        }

        public GameplayObjectiveReadModel(
            bool hasObjective,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared,
            string objectiveTitle,
            string objectiveSummary,
            IReadOnlyList<GameplayObjectiveConditionReadModel> conditions,
            bool? semanticGoalReached = null,
            bool? semanticAllConditionsSatisfied = null,
            bool? semanticIsCleared = null)
        {
            HasObjective = hasObjective;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            IsCleared = isCleared;
            SemanticGoalReached = semanticGoalReached ?? goalReached;
            SemanticAllConditionsSatisfied = semanticAllConditionsSatisfied ?? allConditionsSatisfied;
            SemanticIsCleared = semanticIsCleared ?? isCleared;
            ObjectiveTitle = objectiveTitle ?? string.Empty;
            ObjectiveSummary = objectiveSummary ?? string.Empty;
            Conditions = conditions ?? Array.Empty<GameplayObjectiveConditionReadModel>();
        }

        public bool HasObjective { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }

        public bool SemanticGoalReached { get; }

        public bool SemanticAllConditionsSatisfied { get; }

        public bool SemanticIsCleared { get; }

        public string ObjectiveTitle { get; }

        public string ObjectiveSummary { get; }

        public IReadOnlyList<GameplayObjectiveConditionReadModel> Conditions { get; }
    }
}
