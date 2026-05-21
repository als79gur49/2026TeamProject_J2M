using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum BoardTileOverlayLayer
    {
        Guide = 0,
        Objective = 1,
        Danger = 2,
        Selection = 3,
        Hover = 4,
    }

    [Serializable]
    public sealed class BoardTileOverlayCatalogEntry
    {
        [SerializeField] private string overlayKey = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private BoardTileOverlayLayer layer = BoardTileOverlayLayer.Guide;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private float alpha = 1f;
        [SerializeField] private int order;

        public string OverlayKey => BoardTileOverlayCatalog.NormalizeOverlayKey(overlayKey);

        public string DisplayName => displayName ?? string.Empty;

        public BoardTileOverlayLayer Layer => layer;

        public Color Tint => tint;

        public float Alpha => alpha;

        public int Order => order;
    }

    [CreateAssetMenu(
        fileName = "BoardTileOverlayCatalog",
        menuName = "Game/Stages/Board Tile Overlay Catalog")]
    public sealed class BoardTileOverlayCatalog : ScriptableObject
    {
        [SerializeField] private BoardTileOverlayCatalogEntry[] entries =
            Array.Empty<BoardTileOverlayCatalogEntry>();

        public IReadOnlyList<BoardTileOverlayCatalogEntry> Entries =>
            entries ?? Array.Empty<BoardTileOverlayCatalogEntry>();

        public bool TryGetEntry(
            string overlayKey,
            out BoardTileOverlayCatalogEntry entry)
        {
            var normalizedKey = NormalizeOverlayKey(overlayKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                entry = null;
                return false;
            }

            BoardTileOverlayCatalogEntry match = null;
            var matchCount = 0;
            var source = Entries;
            for (var i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                if (candidate == null ||
                    !string.Equals(candidate.OverlayKey, normalizedKey, StringComparison.Ordinal))
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

        public static string NormalizeOverlayKey(string overlayKey)
        {
            return string.IsNullOrWhiteSpace(overlayKey)
                ? string.Empty
                : overlayKey.Trim();
        }
    }

    [Serializable]
    public sealed class BoardTileOverlayOverride
    {
        [SerializeField] private SurfaceCell cell;
        [SerializeField] private string overlayKey = string.Empty;

        public BoardTileOverlayOverride()
        {
        }

        public BoardTileOverlayOverride(SurfaceCell cell, string overlayKey)
        {
            this.cell = cell;
            this.overlayKey = overlayKey ?? string.Empty;
        }

        public SurfaceCell Cell => cell;

        public string OverlayKey => BoardTileOverlayCatalog.NormalizeOverlayKey(overlayKey);
    }
}
