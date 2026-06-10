using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Entities
{
    public static class GameplayEntityLogicProviderFactory
    {
        public static ISnapshotEntityLogicProvider CreateDefault()
        {
            return CreateDefault(
                default,
                definitionsByEntityId: null,
                definitionsByArchetypeId: null,
                hasDefaultDefinition: false);
        }

        public static ISnapshotEntityLogicProvider CreateDefault(EnemyAiProfile enemyAiProfile)
        {
            if (enemyAiProfile == null)
            {
                throw new System.ArgumentNullException(
                    nameof(enemyAiProfile),
                    "Enemy AI profile must be explicit. Use the runtime-definition overload for intentional test/dev defaults.");
            }

            return CreateDefault(enemyAiProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        public static ISnapshotEntityLogicProvider CreateDefault(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId = null,
            bool hasDefaultDefinition = true)
        {
            return new SnapshotEntityLogicProvider(
                new IEntityLogicFactory[]
                {
                    new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId, hasDefaultDefinition),
                    new EnemyActionStateEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId, hasDefaultDefinition),
                    new EnemyCombatEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId, hasDefaultDefinition),
                    new SlidingBoxEntityLogicFactory(),
                    new ProjectileEntityLogicFactory(),
                });
        }
    }
}
