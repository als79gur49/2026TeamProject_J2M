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

    public enum GameplayObjectivePresentationKind
    {
        None = 0,
        ReachExit = 1,
        ActivateButton = 2,
        ActivateMoonButton = 3,
    }

    public readonly struct GameplayObjectiveConditionReadModel
    {
        public GameplayObjectiveConditionReadModel(
            string stableId,
            GameplayObjectivePresentationKind presentationKind,
            string stableGroupKey,
            GameplayObjectiveConditionRole role,
            bool required,
            bool isSatisfied,
            int completedCount,
            int requiredCount,
            int sortOrder)
        {
            StableId = stableId ?? string.Empty;
            PresentationKind = presentationKind;
            StableGroupKey = stableGroupKey ?? string.Empty;
            Role = role;
            Required = required;
            IsSatisfied = isSatisfied;
            CompletedCount = Math.Max(0, completedCount);
            RequiredCount = Math.Max(0, requiredCount);
            SortOrder = sortOrder;
        }

        public string StableId { get; }

        public GameplayObjectivePresentationKind PresentationKind { get; }

        public string StableGroupKey { get; }

        public GameplayObjectiveConditionRole Role { get; }

        public bool Required { get; }

        public bool IsSatisfied { get; }

        public int CompletedCount { get; }

        public int RequiredCount { get; }

        public int SortOrder { get; }
    }

    public readonly struct GameplayObjectiveReadModel
    {
        public static readonly GameplayObjectiveReadModel NoObjective = new(
            false,
            false,
            false,
            false,
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
                Array.Empty<GameplayObjectiveConditionReadModel>())
        {
        }

        public GameplayObjectiveReadModel(
            bool hasObjective,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared,
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
            Conditions = conditions ?? Array.Empty<GameplayObjectiveConditionReadModel>();
        }

        public bool HasObjective { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }

        public bool SemanticGoalReached { get; }

        public bool SemanticAllConditionsSatisfied { get; }

        public bool SemanticIsCleared { get; }

        public IReadOnlyList<GameplayObjectiveConditionReadModel> Conditions { get; }
    }
}
