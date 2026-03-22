namespace Game.Feature.Gameplay.Attack.Actions
{
    public readonly struct DestroyAction
    {
        public DestroyAction(int targetId)
        {
            TargetId = targetId;
        }

        public int TargetId { get; }
    }
}
