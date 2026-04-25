using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [CreateAssetMenu(menuName = "Gameplay/Presentation/Enemy Presentation Archetype Catalog")]
    public sealed class EnemyPresentationArchetypeCatalog : ScriptableObject
    {
        [SerializeField] private EnemyPresentationArchetypeAsset[] entries = Array.Empty<EnemyPresentationArchetypeAsset>();

        public EnemyPresentationArchetypeAsset[] Entries => entries ?? Array.Empty<EnemyPresentationArchetypeAsset>();
    }

    public readonly struct EnemyPresentationArchetypeRuntime
    {
        public EnemyPresentationArchetypeRuntime(
            EnemyUnitArchetypeId archetypeId,
            GameplayEntityView viewPrefab)
        {
            ArchetypeId = archetypeId;
            ViewPrefab = viewPrefab != null ? viewPrefab : throw new ArgumentNullException(nameof(viewPrefab));
        }

        public EnemyUnitArchetypeId ArchetypeId { get; }

        public GameplayEntityView ViewPrefab { get; }
    }

    public sealed class EnemyPresentationArchetypeRegistry
    {
        public EnemyPresentationArchetypeRegistry(
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyPresentationArchetypeRuntime> entries)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyPresentationArchetypeRuntime> Entries { get; }

        public bool TryGetRuntime(
            EnemyUnitArchetypeId archetypeId,
            out EnemyPresentationArchetypeRuntime runtime)
        {
            return Entries.TryGetValue(archetypeId, out runtime);
        }
    }

    public static class EnemyPresentationArchetypeCatalogResolver
    {
        public static EnemyPresentationArchetypeRegistry BuildRegistry(
            EnemyPresentationArchetypeCatalog catalog,
            IReadOnlyList<EnemyUnitArchetypeId> requiredSummonArchetypeIds,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> gameplayDefinitionsByArchetypeId,
            string ownerDescription)
        {
            var hasRequiredSummonArchetypes = requiredSummonArchetypeIds != null &&
                                              requiredSummonArchetypeIds.Count > 0;
            if (catalog == null)
            {
                if (hasRequiredSummonArchetypes)
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} requires an {nameof(EnemyPresentationArchetypeCatalog)} when summon archetype presentations are configured.");
                }

                return null;
            }

            var orderedEntries = new List<EnemyPresentationArchetypeAsset>(catalog.Entries.Length);
            for (var i = 0; i < catalog.Entries.Length; i++)
            {
                var entry = catalog.Entries[i];
                if (entry == null)
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation archetype catalogs cannot contain null entries.");
                }

                orderedEntries.Add(entry);
            }

            orderedEntries.Sort((left, right) => EnemyUnitArchetypeId.OrderingComparer.Compare(left.ArchetypeId, right.ArchetypeId));

            var runtimesByArchetypeId =
                new Dictionary<EnemyUnitArchetypeId, EnemyPresentationArchetypeRuntime>(EnemyUnitArchetypeId.EqualityComparer);
            for (var i = 0; i < orderedEntries.Count; i++)
            {
                var entry = orderedEntries[i];
                entry.ValidateConfiguration(ownerDescription);

                var archetypeId = entry.ArchetypeId;
                if (!runtimesByArchetypeId.TryAdd(
                        archetypeId,
                        new EnemyPresentationArchetypeRuntime(archetypeId, entry.ViewPrefab)))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation archetype catalog cannot contain duplicate archetype ids ('{archetypeId}').");
                }

                if (gameplayDefinitionsByArchetypeId == null ||
                    !gameplayDefinitionsByArchetypeId.ContainsKey(archetypeId))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation archetype '{archetypeId}' is not present in the gameplay archetype registry.");
                }
            }

            if (hasRequiredSummonArchetypes)
            {
                for (var i = 0; i < requiredSummonArchetypeIds.Count; i++)
                {
                    var requiredArchetypeId = requiredSummonArchetypeIds[i];
                    if (!runtimesByArchetypeId.ContainsKey(requiredArchetypeId))
                    {
                        throw new InvalidOperationException(
                            $"{ownerDescription} is missing summoned enemy presentation mapping for archetype '{requiredArchetypeId}'.");
                    }
                }
            }

            return runtimesByArchetypeId.Count > 0
                ? new EnemyPresentationArchetypeRegistry(runtimesByArchetypeId)
                : null;
        }
    }
}
