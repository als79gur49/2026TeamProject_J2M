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
            var defaultDefinition = enemyAiProfile != null
                ? enemyAiProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                : EnemyAiRuntimeDefinition.CreateDefaultMelee();
            return CreateDefault(defaultDefinition);
        }

        public static ISnapshotEntityLogicProvider CreateDefault(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId = null)
        {
            return new SnapshotEntityLogicProvider(
                new IEntityLogicFactory[]
                {
                    new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId),
                    new EnemyActionStateEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId),
                    new EnemyCombatEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId),
                    new SlidingBoxEntityLogicFactory(),
                    new ProjectileEntityLogicFactory(),
                });
        }
    }
}
