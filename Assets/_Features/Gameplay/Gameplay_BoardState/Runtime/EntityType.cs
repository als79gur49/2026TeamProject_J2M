namespace Game.Feature.Gameplay.BoardState
{
    public enum EntityType
    {
        None = 0,
        Unit = 1,
        // Reserved legacy projectile entity slot. Do not instantiate.
        Projectile = 2,
        Box = 3,
    }
}
