using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class StageWorldGuideCatalogEntry
    {
        [SerializeField] private string guideKey = string.Empty;
        [SerializeField] private GameObject prefab;

        public string GuideKey => StageWorldGuideInstruction.NormalizeGuideKey(guideKey);

        public GameObject Prefab => prefab;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage World Guide Catalog", fileName = "StageWorldGuideCatalog")]
    public sealed class StageWorldGuideCatalog : ScriptableObject
    {
        [SerializeField] private StageWorldGuideCatalogEntry[] entries = Array.Empty<StageWorldGuideCatalogEntry>();

        public IReadOnlyList<StageWorldGuideCatalogEntry> Entries => entries ?? Array.Empty<StageWorldGuideCatalogEntry>();

        public bool TryResolve(string guideKey, out StageWorldGuideCatalogEntry entry)
        {
            var normalized = StageWorldGuideInstruction.NormalizeGuideKey(guideKey);
            if (!string.IsNullOrEmpty(normalized))
            {
                var source = Entries;
                for (var i = 0; i < source.Count; i++)
                {
                    var candidate = source[i];
                    if (candidate != null &&
                        string.Equals(candidate.GuideKey, normalized, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }
}
