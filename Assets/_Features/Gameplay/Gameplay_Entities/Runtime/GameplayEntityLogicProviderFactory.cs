namespace Game.Feature.Gameplay.Entities
{
    using System.Collections.Generic;

    public static class GameplayEntityLogicProviderFactory
    {
        public static ISnapshotEntityLogicProvider CreateDefault()
        {
            return CreateDefault(null);
        }

        public static ISnapshotEntityLogicProvider CreateDefault(EnemyAiProfile enemyAiProfile)
        {
            return CreateDefault(enemyAiProfile, null);
        }

        public static ISnapshotEntityLogicProvider CreateDefault(
            EnemyAiProfile enemyAiProfile,
            IReadOnlyDictionary<int, EnemyAiProfile> profilesByEntityId)
        {
            return new SnapshotEntityLogicProvider(
                new IEntityLogicFactory[]
                {
                    new EnemyEntityLogicFactory(enemyAiProfile ?? EnemyAiProfile.CreateRuntimeDefault(), profilesByEntityId),
                    new SlidingBoxEntityLogicFactory(),
                    new ProjectileEntityLogicFactory(),
                });
        }
    }
}
