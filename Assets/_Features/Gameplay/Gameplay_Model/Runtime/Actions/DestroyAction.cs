namespace Game.Feature.Gameplay.Model.Actions
{
    public enum DestroyCondition
    {
        WhenHpDepleted = 0,
        AlwaysMark = 1,
    }

    public readonly struct DestroyAction
    {
        public DestroyAction(int targetId, DestroyCondition condition = DestroyCondition.WhenHpDepleted)
        {
            TargetId = targetId;
            Condition = condition;
        }

        public int TargetId { get; }

        public DestroyCondition Condition { get; }
    }
}
