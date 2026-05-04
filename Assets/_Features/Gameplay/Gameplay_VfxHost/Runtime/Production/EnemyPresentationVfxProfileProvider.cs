using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Authoring;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public sealed class EnemyPresentationVfxProfileProvider : IGameplayVfxProfileProvider
    {
        private readonly IReadOnlyDictionary<int, VfxProfile> profilesByEntityId;
        private readonly IReadOnlyDictionary<int, VfxProfileAsset> profileAssetsByEntityId;

        public EnemyPresentationVfxProfileProvider(
            IReadOnlyDictionary<int, VfxProfile> profilesByEntityId,
            IReadOnlyDictionary<int, VfxProfileAsset> profileAssetsByEntityId)
        {
            this.profilesByEntityId = profilesByEntityId ?? new Dictionary<int, VfxProfile>();
            this.profileAssetsByEntityId = profileAssetsByEntityId ?? new Dictionary<int, VfxProfileAsset>();
        }

        public int Count => profilesByEntityId.Count;

        public bool TryResolveProfileForRequest(
            in GameplayVfxRequest request,
            out VfxProfile profile)
        {
            if (request.SourceEntityId <= 0)
            {
                profile = null;
                return false;
            }

            return profilesByEntityId.TryGetValue(request.SourceEntityId, out profile) && profile != null;
        }

        public bool TryResolveProfileAssetForSourceEntity(
            int sourceEntityId,
            out VfxProfileAsset profileAsset)
        {
            if (sourceEntityId <= 0)
            {
                profileAsset = null;
                return false;
            }

            return profileAssetsByEntityId.TryGetValue(sourceEntityId, out profileAsset) && profileAsset != null;
        }
    }

    public static class EnemyPresentationVfxProfileMapBuilder
    {
        public static EnemyPresentationVfxProfileProvider Build(
            EnemyPresentationCatalog catalog,
            EnemyPresentationBinding[] bindings,
            string ownerDescription)
        {
            var profilesByEntityId = new Dictionary<int, VfxProfile>();
            var profileAssetsByEntityId = new Dictionary<int, VfxProfileAsset>();
            var bindingEntries = bindings ?? Array.Empty<EnemyPresentationBinding>();
            if (bindingEntries.Length == 0)
            {
                return new EnemyPresentationVfxProfileProvider(profilesByEntityId, profileAssetsByEntityId);
            }

            if (catalog == null)
            {
                return new EnemyPresentationVfxProfileProvider(profilesByEntityId, profileAssetsByEntityId);
            }

            var profileAssetsByPresentationId = BuildCatalogLookup(catalog, ownerDescription);
            for (var i = 0; i < bindingEntries.Length; i++)
            {
                var binding = bindingEntries[i];
                if (binding.EntityId <= 0)
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy VFX profile bindings require a positive entity id.");
                }

                var presentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(binding.PresentationId);
                if (string.IsNullOrEmpty(presentationId) ||
                    !profileAssetsByPresentationId.TryGetValue(presentationId, out var profileAsset) ||
                    profileAsset == null)
                {
                    continue;
                }

                var status = EnemyPresentationVfxProfileStatusResolver.Resolve(profileAsset);
                if (status.Kind != EnemyPresentationVfxProfileStatusKind.Valid)
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy presentation '{presentationId}' has invalid VFX profile '{profileAsset.name}'.");
                }

                if (!profilesByEntityId.TryAdd(binding.EntityId, profileAsset.BuildRuntimeProfile()))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} enemy VFX profile bindings cannot contain duplicate entity ids.");
                }

                profileAssetsByEntityId.Add(binding.EntityId, profileAsset);
            }

            return new EnemyPresentationVfxProfileProvider(profilesByEntityId, profileAssetsByEntityId);
        }

        private static Dictionary<string, VfxProfileAsset> BuildCatalogLookup(
            EnemyPresentationCatalog catalog,
            string ownerDescription)
        {
            var entries = catalog.Entries;
            var profilesByPresentationId = new Dictionary<string, VfxProfileAsset>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var presentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    continue;
                }

                if (!profilesByPresentationId.TryAdd(presentationId, entry.VfxProfileAsset))
                {
                    throw new InvalidOperationException(
                        $"{ownerDescription} cannot contain duplicate enemy presentation ids. Duplicate id: '{presentationId}'.");
                }
            }

            return profilesByPresentationId;
        }
    }
}
