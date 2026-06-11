using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum TileFeaturePresentationCatalogStatusKind
    {
        NoPresentationDefinition,
        CatalogMissing,
        EmptyKeyResolvedByDefault,
        EmptyKeyUnresolved,
        KeyResolved,
        KeyMissing,
        KindMismatch,
        DirectionHintMismatch,
        DirectOverrideActive,
    }

    internal readonly struct TileFeaturePresentationCatalogStatus
    {
        public TileFeaturePresentationCatalogStatus(
            TileFeaturePresentationCatalogStatusKind kind,
            string presentationKey,
            string message,
            TileFeaturePresentationCatalogEntry entry = null)
        {
            Kind = kind;
            PresentationKey = presentationKey ?? string.Empty;
            Message = message ?? string.Empty;
            Entry = entry;
        }

        public TileFeaturePresentationCatalogStatusKind Kind { get; }

        public string PresentationKey { get; }

        public string Message { get; }

        public TileFeaturePresentationCatalogEntry Entry { get; }
    }

    internal readonly struct TileFeaturePresentationCatalogOption
    {
        public TileFeaturePresentationCatalogOption(
            string presentationKey,
            string label,
            TileFeaturePresentationCatalogEntry entry,
            bool invalidPrefab)
        {
            PresentationKey = presentationKey ?? string.Empty;
            Label = label ?? string.Empty;
            Entry = entry;
            InvalidPrefab = invalidPrefab;
        }

        public string PresentationKey { get; }

        public string Label { get; }

        public TileFeaturePresentationCatalogEntry Entry { get; }

        public bool InvalidPrefab { get; }
    }

    internal static class StageAuthoringTileFeaturePresentationCatalogCommands
    {
        public static TileFeaturePresentationCatalogOption[] BuildOptions(
            StagePresentationDefinition presentation,
            TileFeatureKind kind)
        {
            var options = new List<TileFeaturePresentationCatalogOption>
            {
                new(string.Empty, "None", null, false),
            };

            var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
            if (catalog == null)
            {
                return options.ToArray();
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null ||
                    entry.Kind != kind)
                {
                    continue;
                }

                var missingPrefab = entry.VisualPrefab == null;
                var invalidPrefab = string.IsNullOrEmpty(entry.PresentationKey) ||
                                    missingPrefab ||
                                    !StageAuthoringPresentationBindingCommands
                                        .PrefabHasConfigurableTileFeatureVisualTarget(entry.VisualPrefab);
                options.Add(new TileFeaturePresentationCatalogOption(
                    entry.PresentationKey,
                    BuildLabel(entry, missingPrefab, invalidPrefab),
                    entry,
                    invalidPrefab));
            }

            return options.ToArray();
        }

        public static TileFeaturePresentationCatalogStatus ResolveStatus(
            StagePresentationDefinition presentation,
            StageTileFeatureDefinition feature,
            bool directOverrideActive)
        {
            var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(feature.PresentationKey);
            if (directOverrideActive)
            {
                if (TryResolveCatalogKeyEntry(presentation, presentationKey, out var directEntry))
                {
                    return new TileFeaturePresentationCatalogStatus(
                        TileFeaturePresentationCatalogStatusKind.DirectOverrideActive,
                        presentationKey,
                        $"Direct TileId visual override is active; visual prefab uses the direct override and footprint uses catalog key '{presentationKey}'.",
                        directEntry);
                }

                return new TileFeaturePresentationCatalogStatus(
                    TileFeaturePresentationCatalogStatusKind.DirectOverrideActive,
                    presentationKey,
                    string.IsNullOrEmpty(presentationKey)
                        ? "Direct TileId visual override is active; visual prefab uses the direct override and SingleCell footprint."
                        : $"Direct TileId visual override is active; visual prefab uses the direct override and unresolved catalog key '{presentationKey}' uses SingleCell footprint.");
            }

            if (presentation == null)
            {
                return new TileFeaturePresentationCatalogStatus(
                    TileFeaturePresentationCatalogStatusKind.NoPresentationDefinition,
                    presentationKey,
                    "No StagePresentationDefinition assigned; catalog visual selection is disabled.");
            }

            var catalog = presentation.TileFeaturePresentationCatalog;
            if (catalog == null)
            {
                return new TileFeaturePresentationCatalogStatus(
                    TileFeaturePresentationCatalogStatusKind.CatalogMissing,
                    presentationKey,
                    "No TileFeaturePresentationCatalog assigned; direct override remains available in Advanced.");
            }

            if (!string.IsNullOrEmpty(presentationKey))
            {
                if (!catalog.TryGetEntry(presentationKey, out var keyedEntry))
                {
                    return new TileFeaturePresentationCatalogStatus(
                        TileFeaturePresentationCatalogStatusKind.KeyMissing,
                        presentationKey,
                        $"PresentationKey '{presentationKey}' is not present in the TileFeature catalog.");
                }

                if (keyedEntry.Kind != feature.Kind)
                {
                    return new TileFeaturePresentationCatalogStatus(
                        TileFeaturePresentationCatalogStatusKind.KindMismatch,
                        presentationKey,
                        $"PresentationKey '{presentationKey}' is cataloged for {keyedEntry.Kind}, not {feature.Kind}.",
                        keyedEntry);
                }

                if (HasDirectionHintMismatch(feature, keyedEntry))
                {
                    return new TileFeaturePresentationCatalogStatus(
                        TileFeaturePresentationCatalogStatusKind.DirectionHintMismatch,
                        presentationKey,
                        $"PresentationKey '{presentationKey}' direction hint {keyedEntry.DirectionHint} does not match {feature.Direction}.",
                        keyedEntry);
                }

                return new TileFeaturePresentationCatalogStatus(
                    TileFeaturePresentationCatalogStatusKind.KeyResolved,
                    presentationKey,
                    $"Resolved catalog visual: {ResolveDisplayName(keyedEntry)}.",
                    keyedEntry);
            }

            if (catalog.TryGetDefaultEntry(feature.Kind, out var defaultEntry))
            {
                return new TileFeaturePresentationCatalogStatus(
                    TileFeaturePresentationCatalogStatusKind.EmptyKeyResolvedByDefault,
                    string.Empty,
                    $"No PresentationKey set; default {feature.Kind} visual will be used.",
                    defaultEntry);
            }

            return new TileFeaturePresentationCatalogStatus(
                TileFeaturePresentationCatalogStatusKind.EmptyKeyUnresolved,
                string.Empty,
                "No PresentationKey set and no default visual exists for this TileFeature kind.");
        }

        public static bool HasDirectionHintMismatch(
            StageTileFeatureDefinition feature,
            TileFeaturePresentationCatalogEntry entry)
        {
            if (entry == null ||
                entry.DirectionHint == Direction2D.None)
            {
                return false;
            }

            return feature.Kind == TileFeatureKind.Slide &&
                   entry.DirectionHint != feature.Direction;
        }

        private static string BuildLabel(
            TileFeaturePresentationCatalogEntry entry,
            bool missingPrefab,
            bool invalidPrefab)
        {
            var displayName = ResolveDisplayName(entry);
            var label = $"{entry.Kind} / {displayName} ({entry.PresentationKey})";
            if (entry.IsDefaultForKind)
            {
                label += " [Default]";
            }

            if (missingPrefab)
            {
                label += " [Missing Prefab]";
            }
            else if (invalidPrefab)
            {
                label += " [Invalid]";
            }

            return label;
        }

        private static string ResolveDisplayName(TileFeaturePresentationCatalogEntry entry)
        {
            var displayName = entry.DisplayName;
            return string.IsNullOrWhiteSpace(displayName)
                ? entry.PresentationKey
                : displayName;
        }

        private static bool TryResolveCatalogKeyEntry(
            StagePresentationDefinition presentation,
            string presentationKey,
            out TileFeaturePresentationCatalogEntry entry)
        {
            entry = null;
            var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
            return catalog != null &&
                   !string.IsNullOrEmpty(presentationKey) &&
                   catalog.TryGetEntry(presentationKey, out entry);
        }
    }
}
