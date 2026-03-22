namespace Game.Feature.Gameplay.Attack.Collection
{
    public readonly struct RawAttackIntent
    {
        public RawAttackIntent(int sourceId, int priority)
            : this(sourceId, priority, 0)
        {
        }

        public RawAttackIntent(int sourceId, int priority, int targetId)
        {
            SourceId = sourceId;
            Priority = priority;
            TargetId = targetId;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public int TargetId { get; }
    }
}
