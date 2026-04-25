using System;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [CreateAssetMenu(menuName = "Gameplay/Presentation/Enemy Presentation Archetype Asset")]
    public sealed class EnemyPresentationArchetypeAsset : ScriptableObject
    {
        [SerializeField] private EnemyUnitArchetypeId archetypeId;
        [SerializeField] private GameplayEntityView viewPrefab;

        public EnemyUnitArchetypeId ArchetypeId => archetypeId;

        public GameplayEntityView ViewPrefab => viewPrefab;

        public void ValidateConfiguration(string ownerDescription)
        {
            ArchetypeId.Validate(nameof(archetypeId));
            if (viewPrefab == null)
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} enemy presentation archetype '{ArchetypeId}' requires a non-null view prefab.");
            }

            EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(
                viewPrefab,
                $"{ownerDescription} enemy presentation archetype '{ArchetypeId}'");
        }
    }
}
