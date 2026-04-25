using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy Unit Archetype Catalog", fileName = "EnemyUnitArchetypeCatalog")]
    public sealed class EnemyUnitArchetypeCatalog : ScriptableObject
    {
        [SerializeField] private EnemyUnitArchetypeAsset[] entries = Array.Empty<EnemyUnitArchetypeAsset>();

        public IReadOnlyList<EnemyUnitArchetypeAsset> Entries => entries ?? Array.Empty<EnemyUnitArchetypeAsset>();
    }
}
