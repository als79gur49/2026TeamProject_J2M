using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum TileFeatureVisualPlacementMode
    {
        Overlay = 0,
        ReplaceBaseTile = 1,
    }

    public enum TileFeatureVisualFootprintMode
    {
        SingleCell = 0,
        ThreeByThreeSameFace = 1,
    }

    [Serializable]
    public sealed class TileFeaturePresentationCatalogEntry
    {
        [SerializeField] private string presentationKey = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private TileFeatureKind kind = TileFeatureKind.Unknown;
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private TileFeatureVisualPlacementMode placementMode;
        [SerializeField] private TileFeatureVisualFootprintMode footprintMode;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool isDefaultForKind;
        [SerializeField] private Direction2D directionHint = Direction2D.None;

        public string PresentationKey => TileFeaturePresentationCatalog.NormalizePresentationKey(presentationKey);

        public string DisplayName => displayName ?? string.Empty;

        public TileFeatureKind Kind => kind;

        public GameObject VisualPrefab => visualPrefab;

        public TileFeatureVisualPlacementMode PlacementMode => placementMode;

        public TileFeatureVisualFootprintMode FootprintMode => footprintMode;

        public Sprite Icon => icon;

        public bool IsDefaultForKind => isDefaultForKind;

        public Direction2D DirectionHint => directionHint;
    }

    [CreateAssetMenu(
        fileName = "TileFeaturePresentationCatalog",
        menuName = "Game/Stages/Tile Feature Presentation Catalog")]
    public sealed class TileFeaturePresentationCatalog : ScriptableObject
    {
        [SerializeField] private TileFeaturePresentationCatalogEntry[] entries =
            Array.Empty<TileFeaturePresentationCatalogEntry>();

        public IReadOnlyList<TileFeaturePresentationCatalogEntry> Entries =>
            entries ?? Array.Empty<TileFeaturePresentationCatalogEntry>();

        public bool TryGetEntry(
            string presentationKey,
            out TileFeaturePresentationCatalogEntry entry)
        {
            var normalizedKey = NormalizePresentationKey(presentationKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                entry = null;
                return false;
            }

            TileFeaturePresentationCatalogEntry match = null;
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
            TileFeatureKind kind,
            out TileFeaturePresentationCatalogEntry entry)
        {
            if (kind == TileFeatureKind.Unknown ||
                !Enum.IsDefined(typeof(TileFeatureKind), kind))
            {
                entry = null;
                return false;
            }

            TileFeaturePresentationCatalogEntry match = null;
            var matchCount = 0;
            var source = Entries;
            for (var i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                if (candidate == null ||
                    !candidate.IsDefaultForKind ||
                    candidate.Kind != kind)
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
