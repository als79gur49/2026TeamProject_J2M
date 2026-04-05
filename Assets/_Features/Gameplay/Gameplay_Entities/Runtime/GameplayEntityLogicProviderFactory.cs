using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Entities
{
    public static class GameplayEntityLogicProviderFactory
    {
        public static ISnapshotEntityLogicProvider CreateDefault()
        {
            return CreateDefault(EnemyAiRuntimeDefinition.CreateDefaultMelee());
        }

        public static ISnapshotEntityLogicProvider CreateDefault(EnemyAiProfile enemyAiProfile)
        {
            var defaultDefinition = (enemyAiProfile ?? EnemyAiProfile.CreateRuntimeDefault())
                .CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            return CreateDefault(defaultDefinition);
        }

        public static ISnapshotEntityLogicProvider CreateDefault(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null)
        {
            return new SnapshotEntityLogicProvider(
                new IEntityLogicFactory[]
                {
                    new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId),
                    new EnemyActionStateEntityLogicFactory(defaultDefinition, definitionsByEntityId),
                    new SlidingBoxEntityLogicFactory(),
                    new ProjectileEntityLogicFactory(),
                });
        }
    }
}
