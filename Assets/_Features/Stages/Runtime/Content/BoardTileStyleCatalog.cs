using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class BoardTileStyleCatalogEntry
    {
        [SerializeField] private string styleKey = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Color tint = Color.white;

        public string StyleKey => BoardTileStyleCatalog.NormalizeStyleKey(styleKey);

        public string DisplayName => displayName ?? string.Empty;

        public Color Tint => tint;
    }

    [CreateAssetMenu(
        fileName = "BoardTileStyleCatalog",
        menuName = "Game/Stages/Board Tile Style Catalog")]
    public sealed class BoardTileStyleCatalog : ScriptableObject
    {
        [SerializeField] private BoardTileStyleCatalogEntry[] entries =
            Array.Empty<BoardTileStyleCatalogEntry>();

        public IReadOnlyList<BoardTileStyleCatalogEntry> Entries =>
            entries ?? Array.Empty<BoardTileStyleCatalogEntry>();

        public bool TryGetEntry(
            string styleKey,
            out BoardTileStyleCatalogEntry entry)
        {
            var normalizedKey = NormalizeStyleKey(styleKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                entry = null;
                return false;
            }

            BoardTileStyleCatalogEntry match = null;
            var matchCount = 0;
            var source = Entries;
            for (var i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                if (candidate == null ||
                    !string.Equals(candidate.StyleKey, normalizedKey, StringComparison.Ordinal))
                {
                    continue;
                }

                match = candidate;
                matchCount++;
                if (matchCount > 1)
                {
                    entry = null;
                    return false;
                }
            }

            entry = matchCount == 1 ? match : null;
            return entry != null;
        }

        public static string NormalizeStyleKey(string styleKey)
        {
            return string.IsNullOrWhiteSpace(styleKey)
                ? string.Empty
                : styleKey.Trim();
        }
    }
}
