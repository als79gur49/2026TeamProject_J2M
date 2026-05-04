using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public struct EnemyPresentationCatalogEntry
    {
        public string PresentationId;
        public GameplayEntityView ViewPrefab;
        public VfxProfileAsset VfxProfileAsset;
    }

    [CreateAssetMenu(menuName = "Gameplay/Presentation/Enemy Presentation Catalog")]
    public sealed class EnemyPresentationCatalog : ScriptableObject
    {
        [SerializeField] private EnemyPresentationCatalogEntry[] entries = Array.Empty<EnemyPresentationCatalogEntry>();

        public EnemyPresentationCatalogEntry[] Entries => entries ?? Array.Empty<EnemyPresentationCatalogEntry>();
    }

    public static class EnemyPresentationCatalogResolver
    {
        public static IReadOnlyDictionary<int, GameplayEntityView> BuildEnemyViewPrefabs(
            EnemyPresentationCatalog catalog,
            EnemyPresentationBinding[] bindings,
            string ownerDescription)
        {
            if (bindings == null || bindings.Length == 0)
            {
                return null;
            }

            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} requires an {nameof(EnemyPresentationCatalog)} when enemy presentation bindings are configured.");
            }

            var prefabsByPresentationId = BuildCatalogLookup(catalog, ownerDescription);
            var prefabsByEntityId = new Dictionary<int, GameplayEntityView>();

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.EntityId <= 0)
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation bindings require a positive entity id.");
                }

                var presentationId = NormalizePresentationId(binding.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation bindings require a non-empty presentation id.");
                }

                if (!prefabsByPresentationId.TryGetValue(presentationId, out var viewPrefab))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} could not resolve enemy presentation id '{presentationId}' for entity {binding.EntityId}.");
                }

                if (!prefabsByEntityId.TryAdd(binding.EntityId, viewPrefab))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation bindings cannot contain duplicate entity ids.");
                }

                EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(
                    viewPrefab,
                    $"{ownerDescription} enemy presentation '{presentationId}'");
            }

            return prefabsByEntityId;
        }

        public static string NormalizePresentationId(string presentationId)
        {
            return string.IsNullOrWhiteSpace(presentationId)
                ? string.Empty
                : presentationId.Trim();
        }

        private static Dictionary<string, GameplayEntityView> BuildCatalogLookup(
            EnemyPresentationCatalog catalog,
            string ownerDescription)
        {
            var entries = catalog.Entries;
            var prefabsByPresentationId = new Dictionary<string, GameplayEntityView>(StringComparer.Ordinal);

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var presentationId = NormalizePresentationId(entry.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} contains an enemy presentation catalog entry with an empty presentation id.");
                }

                if (entry.ViewPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation '{presentationId}' requires a non-null view prefab.");
                }

                if (!prefabsByPresentationId.TryAdd(presentationId, entry.ViewPrefab))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} cannot contain duplicate enemy presentation ids. Duplicate id: '{presentationId}'.");
                }
            }

            return prefabsByPresentationId;
        }
    }
}
