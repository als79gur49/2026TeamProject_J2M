namespace Game.Feature.Gameplay.Attack.Actions
{
    public readonly struct DamageAction
    {
        public DamageAction(int targetId, int amount)
        {
            TargetId = targetId;
            Amount = amount;
        }

        public int TargetId { get; }

        public int Amount { get; }
    }
}
