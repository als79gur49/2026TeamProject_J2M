namespace Game.Feature.Gameplay.Attack
{
    public enum AttackCommandKind
    {
        Attack = 0,
        // Reserved legacy projectile entity command. Do not emit.
        FireProjectile = 1,
        ImpactReservation = 2,
        DelayedEffect = 3,
    }
}
