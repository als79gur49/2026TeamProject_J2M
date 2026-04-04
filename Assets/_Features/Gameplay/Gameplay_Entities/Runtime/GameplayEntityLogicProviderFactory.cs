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
            var defaultProfile = enemyAiProfile ?? EnemyAiProfile.CreateRuntimeDefault();

            return new SnapshotEntityLogicProvider(
                new IEntityLogicFactory[]
                {
                    new EnemyEntityLogicFactory(defaultProfile, profilesByEntityId),
                    new EnemyActionStateEntityLogicFactory(defaultProfile, profilesByEntityId),
                    new SlidingBoxEntityLogicFactory(),
                    new ProjectileEntityLogicFactory(),
                });
        }
    }
}
