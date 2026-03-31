namespace Game.Feature.Gameplay.Entities
{
    public static class GameplayEntityLogicProviderFactory
    {
        public static ISnapshotEntityLogicProvider CreateDefault()
        {
            return new SnapshotEntityLogicProvider(
                new IEntityLogicFactory[]
                {
                    new EnemyEntityLogicFactory(),
                    new SlidingBoxEntityLogicFactory(),
                    new ProjectileEntityLogicFactory(),
                });
        }
    }
}
