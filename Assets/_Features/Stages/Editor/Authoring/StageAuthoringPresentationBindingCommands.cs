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

            var sourceMatches = authoring.TileFeaturePresentationSelections
                .Where(binding => binding != null && binding.TileId == tileId)
                .ToArray();
            if (sourceMatches.Length != 1)
            {
                error = $"TileFeature TileId {tileId} must have exactly one presentation selection.";
                return false;
            }

            var next = authoring.TileFeaturePresentationSelections
                .Where(binding => binding != null && binding.TileId != tileId)
                .Select(CloneBinding)
                .ToList();
            next.Add(new TileFeaturePresentationBinding
            {
                TileId = tileId,
                PresentationKey = sourceMatches[0].PresentationKey,
                VisualPrefab = visualPrefab,
            });
            WriteAuthoringSelections(authoring, next, "Set TileFeature Visual Override");
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

            var matches = authoring.TileFeaturePresentationSelections
                .Where(binding => binding != null && binding.TileId == tileId)
                .ToArray();
            if (matches.Length != 1 || matches[0].VisualPrefab == null)
            {
                error = $"TileFeature direct visual override for TileId {tileId} was not found.";
                return false;
            }

            var next = authoring.TileFeaturePresentationSelections
                .Where(binding => binding != null)
                .Select(binding => new TileFeaturePresentationBinding
                {
                    TileId = binding.TileId,
                    PresentationKey = binding.PresentationKey,
                    VisualPrefab = binding.TileId == tileId ? null : binding.VisualPrefab,
                })
                .ToList();
            WriteAuthoringSelections(authoring, next, "Remove TileFeature Visual Override");
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

            var matched = authoring.TileFeaturePresentationSelections
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

            if (prefab == null)
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.MissingBinding,
                    tileId,
                    null,
                    1,
                    "TileFeature has no direct visual override.");
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

            var selection = FindTileFeaturePresentationSelection(presentation, feature.TileId);
            if (selection == null)
            {
                return false;
            }

            if (selection.VisualPrefab != null)
            {
                TryResolveCatalogKeyKind(
                    presentation,
                    selection.PresentationKey,
                    out resolvedKind);
                return true;
            }

            return TryResolveCatalogEntryForFeature(
                       presentation,
                       feature,
                       selection.PresentationKey,
                       out var entry) &&
                   entry.VisualPrefab != null &&
                   TrySetResolvedKind(entry, out resolvedKind);
        }

        private static TileFeaturePresentationBinding FindTileFeaturePresentationSelection(
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
            string presentationKey,
            out TileFeaturePresentationCatalogEntry entry)
        {
            entry = null;
            var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
            if (catalog == null)
            {
                return false;
            }

            var normalizedKey = TileFeaturePresentationCatalog.NormalizePresentationKey(presentationKey);
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
                PresentationKey = binding.PresentationKey,
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
                element.FindPropertyRelative("PresentationKey").stringValue =
                    TileFeaturePresentationCatalog.NormalizePresentationKey(ordered[i].PresentationKey);
                element.FindPropertyRelative("VisualPrefab").objectReferenceValue = ordered[i].VisualPrefab;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presentation);
        }

        private static void WriteAuthoringSelections(
            StageAuthoringDefinition authoring,
            IReadOnlyList<TileFeaturePresentationBinding> bindings,
            string undoName)
        {
            Undo.RecordObject(authoring, undoName);
            authoring.SetTileFeaturePresentationSelections(
                bindings
                    .Where(binding => binding != null)
                    .OrderBy(binding => binding.TileId)
                    .Select(CloneBinding));
            EditorUtility.SetDirty(authoring);
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
