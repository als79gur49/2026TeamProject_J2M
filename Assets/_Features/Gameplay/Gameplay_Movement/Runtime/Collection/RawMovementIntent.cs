namespace Game.Feature.Gameplay.Movement.Collection
{
    public readonly struct RawMovementIntent
    {
        public RawMovementIntent(int sourceId, int priority)
        {
            SourceId = sourceId;
            Priority = priority;
        }

        public int SourceId { get; }

        public int Priority { get; }
    }
}
