using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum BoardTileVisualRole
    {
        GenericDefault = 0,
        ActiveBottom = 1,
        ActiveFront = 2,
        DecorativeTop = 3,
        DecorativeBack = 4,
    }

    [Serializable]
    public sealed class BoardTilePresentationCatalogEntry
    {
        [SerializeField] private string presentationKey = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private BoardTileVisualRole role = BoardTileVisualRole.GenericDefault;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private Material materialFallback;
        [SerializeField] private bool isDefaultForRole;

        public string PresentationKey => BoardTilePresentationCatalog.NormalizePresentationKey(presentationKey);

        public string DisplayName => displayName ?? string.Empty;

        public BoardTileVisualRole Role => role;

        public GameObject TilePrefab => tilePrefab;

        public Material MaterialFallback => materialFallback;

        public bool IsDefaultForRole => isDefaultForRole;
    }

    [CreateAssetMenu(
        fileName = "BoardTilePresentationCatalog",
        menuName = "Game/Stages/Board Tile Presentation Catalog")]
    public sealed class BoardTilePresentationCatalog : ScriptableObject
    {
        [SerializeField] private BoardTilePresentationCatalogEntry[] entries =
            Array.Empty<BoardTilePresentationCatalogEntry>();

        public IReadOnlyList<BoardTilePresentationCatalogEntry> Entries =>
            entries ?? Array.Empty<BoardTilePresentationCatalogEntry>();

        public bool TryGetEntry(
            string presentationKey,
            out BoardTilePresentationCatalogEntry entry)
        {
            var normalizedKey = NormalizePresentationKey(presentationKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                entry = null;
                return false;
            }

            BoardTilePresentationCatalogEntry match = null;
            var matchCount = 0;
            var source = Entries;
            for (var i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                if (candidate == null ||
                    !string.Equals(candidate.PresentationKey, normalizedKey, StringComparison.Ordinal))
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

        public bool TryGetDefaultEntry(
            BoardTileVisualRole role,
            out BoardTilePresentationCatalogEntry entry)
        {
            if (!Enum.IsDefined(typeof(BoardTileVisualRole), role))
            {
                entry = null;
                return false;
            }

            BoardTilePresentationCatalogEntry match = null;
            var matchCount = 0;
            var source = Entries;
            for (var i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                if (candidate == null ||
                    !candidate.IsDefaultForRole ||
                    candidate.Role != role)
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

        public static string NormalizePresentationKey(string presentationKey)
        {
            return string.IsNullOrWhiteSpace(presentationKey)
                ? string.Empty
                : presentationKey.Trim();
        }
    }
}
