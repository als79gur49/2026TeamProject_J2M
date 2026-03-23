namespace Game.Feature.Gameplay.Model.Actions
{
    public readonly struct DelayedAttackAction
    {
        public DelayedAttackAction(int targetId, int damage)
        {
            TargetId = targetId;
            Damage = damage;
        }

        public int TargetId { get; }

        public int Damage { get; }
    }
}
