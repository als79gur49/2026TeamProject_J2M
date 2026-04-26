using System;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    [Serializable]
    public sealed class StageBgmProfileCatalogEntry
    {
        [SerializeField] private string key;
        [SerializeField] private BgmProfile profile;

        public string Key => key ?? string.Empty;

        public BgmProfile Profile => profile;
    }

    [CreateAssetMenu(menuName = "Game/Audio/Stage BGM Profile Catalog", fileName = "StageBgmProfileCatalog")]
    public sealed class StageBgmProfileCatalog : ScriptableObject
    {
        [SerializeField] private StageBgmProfileCatalogEntry[] entries = Array.Empty<StageBgmProfileCatalogEntry>();

        public StageBgmProfileCatalogEntry[] Entries => entries ?? Array.Empty<StageBgmProfileCatalogEntry>();

        public bool TryResolve(string key, out BgmProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                var normalizedKey = key.Trim();
                var resolvedEntries = Entries;
                for (var i = 0; i < resolvedEntries.Length; i++)
                {
                    var entry = resolvedEntries[i];
                    if (entry != null && string.Equals(entry.Key, normalizedKey, StringComparison.Ordinal))
                    {
                        profile = entry.Profile;
                        return profile != null;
                    }
                }
            }

            profile = null;
            return false;
        }
    }
}
