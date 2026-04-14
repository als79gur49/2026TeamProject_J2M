namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplayObjectiveReadModel
    {
        public GameplayObjectiveReadModel(
            bool hasObjective,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared)
        {
            HasObjective = hasObjective;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            IsCleared = isCleared;
        }

        public bool HasObjective { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }
    }
}
