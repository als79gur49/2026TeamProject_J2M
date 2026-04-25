using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy Unit Archetype", fileName = "EnemyUnitArchetype")]
    public sealed class EnemyUnitArchetypeAsset : ScriptableObject
    {
        [SerializeField] private EnemyUnitArchetypeId archetypeId;
        [SerializeField] private EnemyAiProfile aiProfile;
        [SerializeField] private EnemyUnitSpawnDefaults spawnDefaults = EnemyUnitSpawnDefaults.CreateDefault();

        public EnemyUnitArchetypeId ArchetypeId => archetypeId;

        public EnemyAiProfile AiProfile => aiProfile;

        public EnemyUnitSpawnDefaults SpawnDefaults => spawnDefaults;

        internal void ValidateConfiguration(string paramName)
        {
            archetypeId.Validate(paramName);
            if (aiProfile == null)
            {
                throw new ArgumentException("Enemy unit archetype assets require a non-null AI profile.", paramName);
            }

            spawnDefaults.Validate(paramName);
        }
    }
}
