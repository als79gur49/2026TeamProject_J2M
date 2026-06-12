using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum TileFeatureVisualBindingStatusKind
    {
        NoPresentationDefinition,
        MissingTileFeature,
        MissingBinding,
        Bound,
        DuplicateBinding,
        InvalidPrefab,
    }

    internal readonly struct TileFeatureVisualBindingStatus
    {
        public TileFeatureVisualBindingStatus(
            TileFeatureVisualBindingStatusKind kind,
            int tileId,
            GameObject visualPrefab,
            int bindingCount,
            string message)
        {
            Kind = kind;
            TileId = tileId;
            VisualPrefab = visualPrefab;
            BindingCount = bindingCount;
            Message = message ?? string.Empty;
        }

        public TileFeatureVisualBindingStatusKind Kind { get; }

        public int TileId { get; }

        public GameObject VisualPrefab { get; }

        public int BindingCount { get; }

        public string Message { get; }
    }

    internal enum BoardTilePresentationOverrideStatusKind
    {
        NoPresentationDefinition,
        CatalogMissing,
        MissingOverride,
        Resolved,
        MissingKey,
        DuplicateOverride,
        InvalidCell,
        OutsideBounds,
    }

    internal readonly struct BoardTilePresentationOverrideStatus
    {
        public BoardTilePresentationOverrideStatus(
            BoardTilePresentationOverrideStatusKind kind,
            SurfaceCell cell,
            string presentationKey,
            BoardTilePresentationCatalogEntry catalogEntry,
            int overrideCount,
            string message,
            int suppressingTileId = 0)
        {
            Kind = kind;
            Cell = cell;
            PresentationKey = presentationKey ?? string.Empty;
            CatalogEntry = catalogEntry;
            OverrideCount = overrideCount;
            Message = message ?? string.Empty;
            SuppressingTileId = suppressingTileId;
        }

        public BoardTilePresentationOverrideStatusKind Kind { get; }

        public SurfaceCell Cell { get; }

        public string PresentationKey { get; }

        public BoardTilePresentationCatalogEntry CatalogEntry { get; }

        public int OverrideCount { get; }

        public string Message { get; }

        public int SuppressingTileId { get; }

        public bool IsBaseTileSuppressed => SuppressingTileId > 0;
    }

    internal readonly struct BoardTilePresentationCatalogOption
    {
        public BoardTilePresentationCatalogOption(
            string presentationKey,
            string label,
            BoardTilePresentationCatalogEntry entry)
        {
            PresentationKey = presentationKey ?? string.Empty;
            Label = label ?? string.Empty;
            Entry = entry;
        }

        public string PresentationKey { get; }

        public string Label { get; }

        public BoardTilePresentationCatalogEntry Entry { get; }
    }

    internal enum BoardTilePaintOverrideStatusKind
    {
        NoPresentationDefinition,
        CatalogMissing,
        MissingOverride,
        Resolved,
        MissingKey,
        DuplicateOverride,
        InvalidCell,
        OutsideBounds,
    }

    internal readonly struct BoardTilePaintOverrideStatus
    {
        public BoardTilePaintOverrideStatus(
            BoardTilePaintOverrideStatusKind kind,
            SurfaceCell cell,
            string styleKey,
            BoardTileStyleCatalogEntry catalogEntry,
            int overrideCount,
            string message)
        {
            Kind = kind;
            Cell = cell;
            StyleKey = styleKey ?? string.Empty;
            CatalogEntry = catalogEntry;
            OverrideCount = overrideCount;
            Message = message ?? string.Empty;
        }

        public BoardTilePaintOverrideStatusKind Kind { get; }

        public SurfaceCell Cell { get; }

        public string StyleKey { get; }

        public BoardTileStyleCatalogEntry CatalogEntry { get; }

        public int OverrideCount { get; }

        public string Message { get; }
    }

    internal readonly struct BoardTileStyleCatalogOption
    {
        public BoardTileStyleCatalogOption(
            string styleKey,
            string label,
            BoardTileStyleCatalogEntry entry)
        {
            StyleKey = styleKey ?? string.Empty;
            Label = label ?? string.Empty;
            Entry = entry;
        }

        public string StyleKey { get; }

        public string Label { get; }

        public BoardTileStyleCatalogEntry Entry { get; }
    }

    internal static class StageAuthoringPresentationBindingCommands
    {
        public static BoardTilePresentationCatalogOption[] BuildBoardTilePresentationOptions(
            StagePresentationDefinition presentation)
        {
            var catalog = presentation != null ? presentation.BoardTilePresentationCatalog : null;
            if (catalog == null)
            {
                return Array.Empty<BoardTilePresentationCatalogOption>();
            }

            return catalog.Entries
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.PresentationKey))
                .Select(entry => new BoardTilePresentationCatalogOption(
                    entry.PresentationKey,
                    FormatBoardTileCatalogOption(entry),
                    entry))
                .OrderBy(option => option.Label, StringComparer.Ordinal)
                .ToArray();
        }

        public static BoardTileStyleCatalogOption[] BuildBoardTileStyleOptions(
            StagePresentationDefinition presentation)
        {
            var catalog = presentation != null ? presentation.BoardTileStyleCatalog : null;
            if (catalog == null)
            {
                return Array.Empty<BoardTileStyleCatalogOption>();
            }

            return catalog.Entries
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.StyleKey))
                .Select(entry => new BoardTileStyleCatalogOption(
                    entry.StyleKey,
                    FormatBoardTileStyleCatalogOption(entry),
                    entry))
                .OrderBy(option => option.Label, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool TrySetBoardTilePresentationOverride(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            string presentationKey,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBoardTileOverrideTarget(presentation, authoring, cell, out error))
            {
                return false;
            }

            var normalizedKey = BoardTilePresentationCatalog.NormalizePresentationKey(presentationKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                error = "Board tile presentation override requires a non-empty PresentationKey.";
                return false;
            }

            var catalog = presentation.BoardTilePresentationCatalog;
            if (catalog == null)
            {
                error = "Board tile presentation override requires a BoardTilePresentationCatalog.";
                return false;
            }

            if (!catalog.TryGetEntry(normalizedKey, out _))
            {
                error = $"Board tile PresentationKey '{normalizedKey}' was not found in BoardTilePresentationCatalog '{catalog.name}'.";
                return false;
            }

            var next = presentation.BoardTilePresentationOverrides
                .Where(entry => entry != null && !entry.Cell.Equals(cell))
                .Select(entry => new BoardTilePresentationOverride(entry.Cell, entry.PresentationKey))
                .ToList();
            next.Add(new BoardTilePresentationOverride(cell, normalizedKey));
            WriteBoardTileOverrides(presentation, next, "Set Board Tile Presentation Override");
            return true;
        }

        public static bool TryClearBoardTilePresentationOverride(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBoardTileOverrideTarget(presentation, authoring, cell, out error, requireCatalog: false))
            {
                return false;
            }

            var removedCount = 0;
            var next = new List<BoardTilePresentationOverride>();
            var overrides = presentation.BoardTilePresentationOverrides;
            for (var i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Cell.Equals(cell))
                {
                    removedCount++;
                    continue;
                }

                next.Add(new BoardTilePresentationOverride(entry.Cell, entry.PresentationKey));
            }

            if (removedCount <= 0)
            {
                error = $"Board tile presentation override for cell {cell} was not found.";
                return false;
            }

            WriteBoardTileOverrides(presentation, next, "Clear Board Tile Presentation Override");
            return true;
        }

        public static bool TrySetBoardTilePaintOverride(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            string styleKey,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBoardTilePaintTarget(presentation, authoring, cell, out error))
            {
                return false;
            }

            var normalizedKey = BoardTileStyleCatalog.NormalizeStyleKey(styleKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                error = "Board tile paint override requires a non-empty StyleKey.";
                return false;
            }

            var catalog = presentation.BoardTileStyleCatalog;
            if (catalog == null)
            {
                error = "Board tile paint override requires a BoardTileStyleCatalog.";
                return false;
            }

            if (!catalog.TryGetEntry(normalizedKey, out _))
            {
                error = $"Board tile StyleKey '{normalizedKey}' was not found in BoardTileStyleCatalog '{catalog.name}'.";
                return false;
            }

            var next = presentation.BoardTilePaintOverrides
                .Where(entry => entry != null && !entry.Cell.Equals(cell))
                .Select(entry => new BoardTilePaintOverride(entry.Cell, entry.StyleKey))
                .ToList();
            next.Add(new BoardTilePaintOverride(cell, normalizedKey));
            WriteBoardTilePaintOverrides(presentation, next, "Set Board Tile Paint Override");
            return true;
        }

        public static bool TryClearBoardTilePaintOverride(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBoardTilePaintTarget(presentation, authoring, cell, out error, requireCatalog: false))
            {
                return false;
            }

            var removedCount = 0;
            var next = new List<BoardTilePaintOverride>();
            var overrides = presentation.BoardTilePaintOverrides;
            for (var i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Cell.Equals(cell))
                {
                    removedCount++;
                    continue;
                }

                next.Add(new BoardTilePaintOverride(entry.Cell, entry.StyleKey));
            }

            if (removedCount <= 0)
            {
                error = $"Board tile paint override for cell {cell} was not found.";
                return false;
            }

            WriteBoardTilePaintOverrides(presentation, next, "Clear Board Tile Paint Override");
            return true;
        }

        public static bool TryGetBoardTilePresentationOverrideStatus(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out BoardTilePresentationOverrideStatus status)
        {
            if (presentation == null)
            {
                status = WithBaseTileSuppression(
                    new BoardTilePresentationOverrideStatus(
                        BoardTilePresentationOverrideStatusKind.NoPresentationDefinition,
                        cell,
                        string.Empty,
                        null,
                        0,
                        "No StagePresentationDefinition assigned; board tile override editing disabled."),
                    presentation,
                    authoring,
                    cell);
                return false;
            }

            if (!IsValidBoardTileCell(authoring, cell, out var cellError))
            {
                status = WithBaseTileSuppression(
                    new BoardTilePresentationOverrideStatus(
                        cellError == InvalidBoardTileCellReason.InvalidFace
                            ? BoardTilePresentationOverrideStatusKind.InvalidCell
                            : BoardTilePresentationOverrideStatusKind.OutsideBounds,
                        cell,
                        string.Empty,
                        null,
                        0,
                        cellError == InvalidBoardTileCellReason.InvalidFace
                            ? $"Board tile override cell {cell} has an invalid face value."
                            : $"Board tile override cell {cell} is outside board bounds."),
                    presentation,
                    authoring,
                    cell);
                return false;
            }

            var matches = presentation.BoardTilePresentationOverrides
                .Where(entry => entry != null && entry.Cell.Equals(cell))
                .ToArray();
            var catalog = presentation.BoardTilePresentationCatalog;
            if (catalog == null)
            {
                status = WithBaseTileSuppression(
                    new BoardTilePresentationOverrideStatus(
                        BoardTilePresentationOverrideStatusKind.CatalogMissing,
                        cell,
                        matches.Length > 0 ? matches[0].PresentationKey : string.Empty,
                        null,
                        matches.Length,
                        "No BoardTilePresentationCatalog assigned; board tile override editing disabled."),
                    presentation,
                    authoring,
                    cell);
                return false;
            }

            if (matches.Length == 0)
            {
                status = WithBaseTileSuppression(
                    new BoardTilePresentationOverrideStatus(
                        BoardTilePresentationOverrideStatusKind.MissingOverride,
                        cell,
                        string.Empty,
                        null,
                        0,
                        "No board tile presentation override is set for this cell."),
                    presentation,
                    authoring,
                    cell);
                return true;
            }

            var key = matches[0].PresentationKey;
            if (matches.Length > 1)
            {
                status = WithBaseTileSuppression(
                    new BoardTilePresentationOverrideStatus(
                        BoardTilePresentationOverrideStatusKind.DuplicateOverride,
                        cell,
                        key,
                        null,
                        matches.Length,
                        $"Board tile presentation override is duplicated for {cell} ({matches.Length})."),
                    presentation,
                    authoring,
                    cell);
                return true;
            }

            if (!catalog.TryGetEntry(key, out var entry))
            {
                status = WithBaseTileSuppression(
                    new BoardTilePresentationOverrideStatus(
                        BoardTilePresentationOverrideStatusKind.MissingKey,
                        cell,
                        key,
                        null,
                        1,
                        $"Board tile PresentationKey '{key}' is missing from BoardTilePresentationCatalog '{catalog.name}'."),
                    presentation,
                    authoring,
                    cell);
                return true;
            }

            status = WithBaseTileSuppression(
                new BoardTilePresentationOverrideStatus(
                    BoardTilePresentationOverrideStatusKind.Resolved,
                    cell,
                    key,
                    entry,
                    1,
                    $"Resolved: {FormatBoardTileCatalogOption(entry)}"),
                presentation,
                authoring,
                cell);
            return true;
        }

        public static bool TryGetBoardTilePaintOverrideStatus(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out BoardTilePaintOverrideStatus status)
        {
            if (presentation == null)
            {
                status = new BoardTilePaintOverrideStatus(
                    BoardTilePaintOverrideStatusKind.NoPresentationDefinition,
                    cell,
                    string.Empty,
                    null,
                    0,
                    "No StagePresentationDefinition assigned; board tile paint editing disabled.");
                return false;
            }

            if (!IsValidBoardTileCell(authoring, cell, out var cellError))
            {
                status = new BoardTilePaintOverrideStatus(
                    cellError == InvalidBoardTileCellReason.InvalidFace
                        ? BoardTilePaintOverrideStatusKind.InvalidCell
                        : BoardTilePaintOverrideStatusKind.OutsideBounds,
                    cell,
                    string.Empty,
                    null,
                    0,
                    cellError == InvalidBoardTileCellReason.InvalidFace
                        ? $"Board tile paint cell {cell} has an invalid face value."
                        : $"Board tile paint cell {cell} is outside board bounds.");
                return false;
            }

            var matches = presentation.BoardTilePaintOverrides
                .Where(entry => entry != null && entry.Cell.Equals(cell))
                .ToArray();
            var catalog = presentation.BoardTileStyleCatalog;
            if (catalog == null)
            {
                status = new BoardTilePaintOverrideStatus(
                    BoardTilePaintOverrideStatusKind.CatalogMissing,
                    cell,
                    matches.Length > 0 ? matches[0].StyleKey : string.Empty,
                    null,
                    matches.Length,
                    "No BoardPresentationProfile/BoardTileStyleCatalog assigned; board tile paint editing disabled.");
                return false;
            }

            if (matches.Length == 0)
            {
                status = new BoardTilePaintOverrideStatus(
                    BoardTilePaintOverrideStatusKind.MissingOverride,
                    cell,
                    string.Empty,
                    null,
                    0,
                    "No board tile paint override is set for this cell.");
                return true;
            }

            var key = matches[0].StyleKey;
            if (matches.Length > 1)
            {
                status = new BoardTilePaintOverrideStatus(
                    BoardTilePaintOverrideStatusKind.DuplicateOverride,
                    cell,
                    key,
                    null,
                    matches.Length,
                    $"Board tile paint override is duplicated for {cell} ({matches.Length}).");
                return true;
            }

            if (!catalog.TryGetEntry(key, out var entry))
            {
                status = new BoardTilePaintOverrideStatus(
                    BoardTilePaintOverrideStatusKind.MissingKey,
                    cell,
                    key,
                    null,
                    1,
                    $"Board tile StyleKey '{key}' is missing from BoardTileStyleCatalog '{catalog.name}'.");
                return true;
            }

            status = new BoardTilePaintOverrideStatus(
                BoardTilePaintOverrideStatusKind.Resolved,
                cell,
                key,
                entry,
                1,
                $"Resolved: {FormatBoardTileStyleCatalogOption(entry)}");
            return true;
        }

        public static bool TryResolveBaseTileSuppressionForCell(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out int tileId)
        {
            tileId = 0;
            if (presentation == null || authoring == null)
            {
                return false;
            }

            var features = authoring.TileFeatures;
            for (var i = 0; i < features.Count; i++)
            {
                var feature = features[i];
                if (ResolvesReplaceBaseTileWithVisual(presentation, feature, out var resolvedKind) &&
                    ContainsSuppressedBaseTileCell(feature, resolvedKind, authoring.Board, cell))
                {
                    tileId = feature.TileId;
                    return true;
                }
            }

            return false;
        }

        public static int CountReplaceBaseTileSuppressorsForCell(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell)
        {
            if (presentation == null || authoring == null)
            {
                return 0;
            }

            var count = 0;
            var features = authoring.TileFeatures;
            for (var i = 0; i < features.Count; i++)
            {
                var feature = features[i];
                if (ResolvesReplaceBaseTileWithVisual(presentation, feature, out var resolvedKind) &&
                    ContainsSuppressedBaseTileCell(feature, resolvedKind, authoring.Board, cell))
                {
                    count++;
                }
            }

            return count;
        }

        public static bool ResolvesTileFeatureVisual(
            StagePresentationDefinition presentation,
            StageTileFeatureDefinition feature)
        {
            return ResolvesReplaceBaseTileWithVisual(presentation, feature, out _);
        }

        public static bool TrySetTileFeatureVisualBinding(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            GameObject visualPrefab,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBindingTarget(presentation, authoring, tileId, out error))
            {
                return false;
            }

            if (visualPrefab == null)
            {
                error = "TileFeature visual binding requires a visual prefab.";
                return false;
            }

            if (!PrefabHasConfigurableTileFeatureVisualTarget(visualPrefab))
            {
                error = $"Prefab '{visualPrefab.name}' must provide a configurable TileFeature visual target.";
                return false;
            }

            var next = presentation.TileFeaturePresentationBindings
                .Where(binding => binding != null && binding.TileId != tileId)
                .Select(CloneBinding)
                .ToList();
            next.Add(new TileFeaturePresentationBinding
            {
                TileId = tileId,
                VisualPrefab = visualPrefab,
            });
            WriteBindings(presentation, next, "Set TileFeature Visual Binding");
            return true;
        }

        public static bool TryRemoveTileFeatureVisualBinding(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBindingTarget(presentation, authoring, tileId, out error))
            {
                return false;
            }

            var removedCount = 0;
            var next = new List<TileFeaturePresentationBinding>();
            var bindings = presentation.TileFeaturePresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                if (binding.TileId == tileId)
                {
                    removedCount++;
                    continue;
                }

                next.Add(CloneBinding(binding));
            }

            if (removedCount <= 0)
            {
                error = $"TileFeature visual binding for TileId {tileId} was not found.";
                return false;
            }

            WriteBindings(presentation, next, "Remove TileFeature Visual Binding");
            return true;
        }

        public static bool TryGetTileFeatureVisualBindingStatus(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            out TileFeatureVisualBindingStatus status)
        {
            if (presentation == null)
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.NoPresentationDefinition,
                    tileId,
                    null,
                    0,
                    "No StagePresentationDefinition assigned; visual binding editing disabled.");
                return false;
            }

            if (authoring == null ||
                tileId <= 0 ||
                !ContainsTileFeature(authoring, tileId))
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.MissingTileFeature,
                    tileId,
                    null,
                    0,
                    $"TileFeature TileId {tileId} is not present in the selected authoring definition.");
                return false;
            }

            var matched = presentation.TileFeaturePresentationBindings
                .Where(binding => binding != null && binding.TileId == tileId)
                .ToArray();
            if (matched.Length == 0)
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.MissingBinding,
                    tileId,
                    null,
                    0,
                    "TileFeature visual binding is missing.");
                return true;
            }

            var prefab = matched[0].VisualPrefab;
            if (matched.Length > 1)
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.DuplicateBinding,
                    tileId,
                    prefab,
                    matched.Length,
                    $"TileFeature visual binding is duplicated ({matched.Length}).");
                return true;
            }

            if (!PrefabHasConfigurableTileFeatureVisualTarget(prefab))
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.InvalidPrefab,
                    tileId,
                    prefab,
                    1,
                    prefab == null
                        ? "TileFeature visual binding has no visual prefab."
                        : $"TileFeature visual binding prefab '{prefab.name}' is invalid.");
                return true;
            }

            status = new TileFeatureVisualBindingStatus(
                TileFeatureVisualBindingStatusKind.Bound,
                tileId,
                prefab,
                1,
                $"Bound: {prefab.name}");
            return true;
        }

        public static bool PrefabHasConfigurableTileFeatureVisualTarget(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            var behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour is TileFeatureVisualTargetView ||
                    behaviour is ITileFeatureVisualTarget &&
                    behaviour is ITileFeatureVisualTargetConfigurator)
                {
                    return true;
                }
            }

            return false;
        }

        private static BoardTilePresentationOverrideStatus WithBaseTileSuppression(
            BoardTilePresentationOverrideStatus status,
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell)
        {
            if (!TryResolveBaseTileSuppressionForCell(presentation, authoring, cell, out var tileId))
            {
                return status;
            }

            var message = $"{status.Message} Base tile suppressed by TileFeature: TileId {tileId}; board tile overrides will not be visible while suppressed.";
            return new BoardTilePresentationOverrideStatus(
                status.Kind,
                status.Cell,
                status.PresentationKey,
                status.CatalogEntry,
                status.OverrideCount,
                message,
                tileId);
        }

        private static bool ResolvesReplaceBaseTileWithVisual(
            StagePresentationDefinition presentation,
            StageTileFeatureDefinition feature,
            out TileFeatureKind resolvedKind)
        {
            resolvedKind = TileFeatureKind.Unknown;
            if (feature.TileId <= 0)
            {
                return false;
            }

            var directBinding = FindDirectTileFeatureBinding(presentation, feature.TileId);
            if (directBinding != null)
            {
                if (directBinding.VisualPrefab == null)
                {
                    return false;
                }

                TryResolveCatalogKeyKind(
                    presentation,
                    feature.PresentationKey,
                    out resolvedKind);
                return true;
            }

            return TryResolveCatalogEntryForFeature(
                       presentation,
                       feature,
                       out var entry) &&
                   entry.VisualPrefab != null &&
                   TrySetResolvedKind(entry, out resolvedKind);
        }

        private static TileFeaturePresentationBinding FindDirectTileFeatureBinding(
            StagePresentationDefinition presentation,
            int tileId)
        {
            if (presentation == null)
            {
                return null;
            }

            var bindings = presentation.TileFeaturePresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding != null &&
                    binding.TileId == tileId)
                {
                    return binding;
                }
            }

            return null;
        }

        private static bool TrySetResolvedKind(
            TileFeaturePresentationCatalogEntry entry,
            out TileFeatureKind resolvedKind)
        {
            resolvedKind = entry != null
                ? entry.Kind
                : TileFeatureKind.Unknown;
            return entry != null;
        }

        private static bool ContainsSuppressedBaseTileCell(
            StageTileFeatureDefinition feature,
            TileFeatureKind resolvedKind,
            StageBoardDefinition board,
            SurfaceCell cell)
        {
            if (feature.Cell.face != cell.face)
            {
                return false;
            }

            if (board.MaxInclusive.x >= board.MinInclusive.x &&
                board.MaxInclusive.y >= board.MinInclusive.y &&
                (cell.x < board.MinInclusive.x ||
                 cell.x > board.MaxInclusive.x ||
                 cell.y < board.MinInclusive.y ||
                 cell.y > board.MaxInclusive.y))
            {
                return false;
            }

            if (RequiresThreeByThreeBaseTileSuppression(resolvedKind))
            {
                return Math.Abs(cell.x - feature.Cell.x) <= 1 &&
                       Math.Abs(cell.y - feature.Cell.y) <= 1;
            }

            return feature.Cell.Equals(cell);
        }

        private static bool RequiresThreeByThreeBaseTileSuppression(TileFeatureKind kind)
        {
            return kind == TileFeatureKind.Exit;
        }

        private static bool TryResolveCatalogKeyKind(
            StagePresentationDefinition presentation,
            string presentationKey,
            out TileFeatureKind kind)
        {
            kind = TileFeatureKind.Unknown;
            var normalizedKey = TileFeaturePresentationCatalog.NormalizePresentationKey(presentationKey);
            var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
            if (catalog == null ||
                string.IsNullOrEmpty(normalizedKey) ||
                !catalog.TryGetEntry(normalizedKey, out var entry))
            {
                return false;
            }

            kind = entry.Kind;
            return true;
        }

        private static bool TryResolveCatalogEntryForFeature(
            StagePresentationDefinition presentation,
            StageTileFeatureDefinition feature,
            out TileFeaturePresentationCatalogEntry entry)
        {
            entry = null;
            var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
            if (catalog == null)
            {
                return false;
            }

            var normalizedKey = TileFeaturePresentationCatalog.NormalizePresentationKey(feature.PresentationKey);
            if (!string.IsNullOrEmpty(normalizedKey) &&
                catalog.TryGetEntry(normalizedKey, out entry))
            {
                return true;
            }

            return catalog.TryGetDefaultEntry(feature.Kind, out entry);
        }

        private static bool ValidateBindingTarget(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            out string error)
        {
            error = string.Empty;
            if (presentation == null)
            {
                error = "StagePresentationDefinition is missing.";
                return false;
            }

            if (authoring == null)
            {
                error = "StageAuthoringDefinition is missing.";
                return false;
            }

            if (tileId <= 0)
            {
                error = "TileFeature visual binding requires a positive TileId.";
                return false;
            }

            if (!ContainsTileFeature(authoring, tileId))
            {
                error = $"TileFeature TileId {tileId} was not found in the authoring definition.";
                return false;
            }

            return true;
        }

        private static bool ContainsTileFeature(StageAuthoringDefinition authoring, int tileId)
        {
            if (authoring == null)
            {
                return false;
            }

            var features = authoring.TileFeatures;
            for (var i = 0; i < features.Count; i++)
            {
                if (features[i].TileId == tileId)
                {
                    return true;
                }
            }

            return false;
        }

        private static TileFeaturePresentationBinding CloneBinding(TileFeaturePresentationBinding binding)
        {
            return new TileFeaturePresentationBinding
            {
                TileId = binding.TileId,
                VisualPrefab = binding.VisualPrefab,
            };
        }

        private static void WriteBindings(
            StagePresentationDefinition presentation,
            IReadOnlyList<TileFeaturePresentationBinding> bindings,
            string undoName)
        {
            Undo.RecordObject(presentation, undoName);
            var ordered = bindings
                .Where(binding => binding != null)
                .OrderBy(binding => binding.TileId)
                .ToArray();
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("tileFeaturePresentationBindings");
            property.arraySize = ordered.Length;
            for (var i = 0; i < ordered.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("TileId").intValue = ordered[i].TileId;
                element.FindPropertyRelative("VisualPrefab").objectReferenceValue = ordered[i].VisualPrefab;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presentation);
        }

        private static bool ValidateBoardTileOverrideTarget(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out string error,
            bool requireCatalog = true)
        {
            error = string.Empty;
            if (presentation == null)
            {
                error = "StagePresentationDefinition is missing.";
                return false;
            }

            if (requireCatalog && presentation.BoardTilePresentationCatalog == null)
            {
                error = "BoardTilePresentationCatalog is missing.";
                return false;
            }

            if (!IsValidBoardTileCell(authoring, cell, out var reason))
            {
                error = reason == InvalidBoardTileCellReason.InvalidFace
                    ? $"Board tile override cell {cell} has an invalid face value."
                    : $"Board tile override cell {cell} is outside board bounds.";
                return false;
            }

            return true;
        }

        private static bool ValidateBoardTilePaintTarget(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out string error,
            bool requireCatalog = true)
        {
            error = string.Empty;
            if (presentation == null)
            {
                error = "StagePresentationDefinition is missing.";
                return false;
            }

            if (requireCatalog && presentation.BoardTileStyleCatalog == null)
            {
                error = "BoardTileStyleCatalog is missing.";
                return false;
            }

            if (!IsValidBoardTileCell(authoring, cell, out var reason))
            {
                error = reason == InvalidBoardTileCellReason.InvalidFace
                    ? $"Board tile paint cell {cell} has an invalid face value."
                    : $"Board tile paint cell {cell} is outside board bounds.";
                return false;
            }

            return true;
        }

        private static bool IsValidBoardTileCell(
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out InvalidBoardTileCellReason reason)
        {
            if (!Enum.IsDefined(typeof(FaceId), cell.face))
            {
                reason = InvalidBoardTileCellReason.InvalidFace;
                return false;
            }

            if (authoring != null)
            {
                var board = authoring.Board;
                if (cell.x < board.MinInclusive.x ||
                    cell.x > board.MaxInclusive.x ||
                    cell.y < board.MinInclusive.y ||
                    cell.y > board.MaxInclusive.y)
                {
                    reason = InvalidBoardTileCellReason.OutsideBounds;
                    return false;
                }
            }

            reason = InvalidBoardTileCellReason.None;
            return true;
        }

        private static void WriteBoardTileOverrides(
            StagePresentationDefinition presentation,
            IReadOnlyList<BoardTilePresentationOverride> overrides,
            string undoName)
        {
            Undo.RecordObject(presentation, undoName);
            var ordered = overrides
                .Where(entry => entry != null)
                .OrderBy(entry => (int)entry.Cell.face)
                .ThenBy(entry => entry.Cell.x)
                .ThenBy(entry => entry.Cell.y)
                .ToArray();
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("boardTilePresentationOverrides");
            property.arraySize = ordered.Length;
            for (var i = 0; i < ordered.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                SetSurfaceCell(element.FindPropertyRelative("cell"), ordered[i].Cell);
                element.FindPropertyRelative("presentationKey").stringValue = ordered[i].PresentationKey;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presentation);
        }

        private static void WriteBoardTilePaintOverrides(
            StagePresentationDefinition presentation,
            IReadOnlyList<BoardTilePaintOverride> overrides,
            string undoName)
        {
            Undo.RecordObject(presentation, undoName);
            var ordered = overrides
                .Where(entry => entry != null)
                .OrderBy(entry => (int)entry.Cell.face)
                .ThenBy(entry => entry.Cell.x)
                .ThenBy(entry => entry.Cell.y)
                .ToArray();
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("boardTilePaintOverrides");
            property.arraySize = ordered.Length;
            for (var i = 0; i < ordered.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                SetSurfaceCell(element.FindPropertyRelative("cell"), ordered[i].Cell);
                element.FindPropertyRelative("styleKey").stringValue = ordered[i].StyleKey;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presentation);
        }

        private static void SetSurfaceCell(SerializedProperty property, SurfaceCell cell)
        {
            property.FindPropertyRelative("face").intValue = (int)cell.face;
            property.FindPropertyRelative("x").intValue = cell.x;
            property.FindPropertyRelative("y").intValue = cell.y;
        }

        private static string FormatBoardTileCatalogOption(BoardTilePresentationCatalogEntry entry)
        {
            if (entry == null)
            {
                return "(Missing)";
            }

            var displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.PresentationKey
                : entry.DisplayName;
            return $"{entry.Role} / {displayName} ({entry.PresentationKey})";
        }

        private static string FormatBoardTileStyleCatalogOption(BoardTileStyleCatalogEntry entry)
        {
            if (entry == null)
            {
                return "(Missing)";
            }

            var displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.StyleKey
                : entry.DisplayName;
            return $"{displayName} ({entry.StyleKey})";
        }

        private enum InvalidBoardTileCellReason
        {
            None,
            InvalidFace,
            OutsideBounds,
        }
    }
}
