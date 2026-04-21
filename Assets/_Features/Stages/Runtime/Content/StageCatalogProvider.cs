using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    public interface IStageCatalogProvider
    {
        IReadOnlyList<StageContentEntry> LoadEntries();

        StageIdAliasTable AliasTable { get; }
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/ScriptableObject Stage Catalog Provider", fileName = "StageCatalogProvider")]
    public sealed class ScriptableObjectStageCatalogProvider : ScriptableObject, IStageCatalogProvider
    {
        [SerializeField] private StageCatalog catalog;

        public StageIdAliasTable AliasTable => catalog != null ? catalog.StageIdAliasTable : null;

        public IReadOnlyList<StageContentEntry> LoadEntries()
        {
            return catalog != null ? catalog.Entries : Array.Empty<StageContentEntry>();
        }
    }

    public sealed class StageCatalogResolver
    {
        private readonly StageIdAliasTable aliasTable;
        private readonly Dictionary<StageId, StageContentEntry> entriesByStageId;

        public StageCatalogResolver(IStageCatalogProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            aliasTable = provider.AliasTable;
            entriesByStageId = BuildLookup(provider.LoadEntries());
        }

        public bool TryResolve(StageId stageId, out StageContentEntry entry)
        {
            return entriesByStageId.TryGetValue(stageId, out entry);
        }

        public bool TryResolve(string rawStageId, out StageContentEntry entry)
        {
            if (StageId.TryCreate(rawStageId, out var stageId) &&
                entriesByStageId.TryGetValue(stageId, out entry))
            {
                return true;
            }

            if (aliasTable != null)
            {
                var aliasedStageId = aliasTable.Resolve(rawStageId);
                if (aliasedStageId.IsValid &&
                    entriesByStageId.TryGetValue(aliasedStageId, out entry))
                {
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public StageContentEntry ResolveOrThrow(string rawStageId)
        {
            if (!TryResolve(rawStageId, out var entry))
            {
                throw new KeyNotFoundException($"No stage content entry resolves from '{rawStageId}'.");
            }

            return entry;
        }

        private static Dictionary<StageId, StageContentEntry> BuildLookup(IReadOnlyList<StageContentEntry> entries)
        {
            var lookup = new Dictionary<StageId, StageContentEntry>();
            if (entries == null)
            {
                return lookup;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!entry.StageId.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Stage content entry '{entry.name}' has no valid canonical stage id.");
                }

                if (!lookup.TryAdd(entry.StageId, entry))
                {
                    throw new InvalidOperationException(
                        $"Stage catalog provider returned duplicate stage id '{entry.StageId.Value}'.");
                }
            }

            return lookup;
        }
    }
}
