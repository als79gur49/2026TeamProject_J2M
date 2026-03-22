namespace Game.Feature.Gameplay.Attack.Collection
{
    public readonly struct RawAttackIntent
    {
        public RawAttackIntent(int sourceId, int priority)
        {
            SourceId = sourceId;
            Priority = priority;
        }

        public int SourceId { get; }

        public int Priority { get; }
    }
}
