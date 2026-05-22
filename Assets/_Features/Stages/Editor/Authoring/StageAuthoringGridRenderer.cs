using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal sealed class BoardTilePaintGridPreview
    {
        public static readonly BoardTilePaintGridPreview Disabled = new(false, null);

        private readonly Dictionary<SurfaceCell, Color> tintByCell;

        public BoardTilePaintGridPreview(
            bool enabled,
            IReadOnlyDictionary<SurfaceCell, Color> tintByCell)
        {
            Enabled = enabled;
            this.tintByCell = tintByCell != null
                ? new Dictionary<SurfaceCell, Color>(tintByCell)
                : new Dictionary<SurfaceCell, Color>();
        }

        public bool Enabled { get; }

        public bool TryGetTint(SurfaceCell cell, out Color tint)
        {
            if (!Enabled)
            {
                tint = Color.white;
                return false;
            }

            return tintByCell.TryGetValue(cell, out tint);
        }
    }

    internal static class StageAuthoringGridRenderer
    {
        private const float PaintTintBlendStrength = 0.35f;

        public static StageAuthoringEntityKind? DrawGrid(
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection,
            StageAuthoringEntityKind? focusedGridKind,
            StageAuthoringGridEditMode editMode,
            BoardTilePaintGridPreview paintPreview)
        {
            var board = authoring.Board;
            EditorGUILayout.LabelField(
                $"Board {board.MinInclusive.x},{board.MinInclusive.y} to {board.MaxInclusive.x},{board.MaxInclusive.y}");
            if (board.MaxInclusive.x < board.MinInclusive.x ||
                board.MaxInclusive.y < board.MinInclusive.y)
            {
                EditorGUILayout.HelpBox("Board bounds are invalid.", MessageType.Error);
                return focusedGridKind;
            }

            var nextFocusedGridKind = DrawGridLegend(focusedGridKind);
            for (var y = board.MaxInclusive.y; y >= board.MinInclusive.y; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var x = board.MinInclusive.x; x <= board.MaxInclusive.x; x++)
                    {
                        var index = selection.FindPlacementAt(authoring.Placements, selection.TargetFace, x, y);
                        var placementCount = selection.CountPlacementsAt(authoring.Placements, selection.TargetFace, x, y);
                        var placement = index >= 0 ? authoring.Placements[index] : null;
                        var marker = index >= 0
                            ? GetMarker(placement)
                            : ".";
                        if (placementCount > 1)
                        {
                            marker = StageAuthoringGridMarkerBuilder.Build(placement, duplicateCell: true);
                        }

                        var tileFeatureCount = selection.CountTileFeaturesAt(
                            authoring.TileFeatures,
                            selection.TargetFace,
                            x,
                            y);
                        if (tileFeatureCount > 0)
                        {
                            var tileFeatureIndex = selection.FindTileFeatureAt(
                                authoring.TileFeatures,
                                selection.TargetFace,
                                x,
                                y);
                            var tileFeatureMarker = StageAuthoringGridMarkerBuilder.BuildTileFeatureBadge(
                                authoring.TileFeatures[tileFeatureIndex]);
                            if (tileFeatureCount > 1)
                            {
                                tileFeatureMarker += $"+{tileFeatureCount - 1}";
                            }

                            marker = marker == "."
                                ? tileFeatureMarker
                                : $"{marker}/{tileFeatureMarker}";
                        }

                        var zoneCount = selection.CountZonesAt(
                            authoring.Zones,
                            selection.TargetFace,
                            x,
                            y);
                        var selectedZoneAtCell = false;
                        if (editMode == StageAuthoringGridEditMode.ZoneEditing && zoneCount > 0)
                        {
                            var zoneIndex = selection.FindZoneAt(
                                authoring.Zones,
                                selection.TargetFace,
                                x,
                                y);
                            var zoneMarker = BuildZoneBadge(authoring.Zones[zoneIndex]);
                            if (zoneCount > 1)
                            {
                                zoneMarker += $"+{zoneCount - 1}";
                            }

                            marker = marker == "."
                                ? zoneMarker
                                : $"{marker}/{zoneMarker}";

                            var zoneIndices = selection.FindZoneIndicesAt(
                                authoring.Zones,
                                selection.TargetFace,
                                x,
                                y);
                            for (var zoneIndexAtCell = 0; zoneIndexAtCell < zoneIndices.Length; zoneIndexAtCell++)
                            {
                                if (zoneIndices[zoneIndexAtCell] == selection.SelectedZoneIndexHint)
                                {
                                    selectedZoneAtCell = true;
                                    break;
                                }
                            }
                        }

                        if (selection.TargetCell.x == x && selection.TargetCell.y == y)
                        {
                            marker = $"[{marker}]";
                        }

                        if (DrawGridCellButton(
                                marker,
                                placement,
                                nextFocusedGridKind,
                                selectedZoneAtCell,
                                editMode == StageAuthoringGridEditMode.ZoneEditing && zoneCount > 0,
                                editMode,
                                paintPreview,
                                new SurfaceCell(selection.TargetFace, x, y)))
                        {
                            if (editMode == StageAuthoringGridEditMode.TileFeaturePlacement)
                            {
                                selection.SelectTileFeatureCell(
                                    selection.TargetFace,
                                    new Vector2Int(x, y),
                                    authoring.TileFeatures);
                            }
                            else if (editMode == StageAuthoringGridEditMode.ZoneEditing)
                            {
                                selection.SelectZoneCell(
                                    selection.TargetFace,
                                    new Vector2Int(x, y),
                                    authoring.Zones);
                            }
                            else
                            {
                                selection.SelectCell(selection.TargetFace, new Vector2Int(x, y), authoring.Placements);
                            }
                        }
                    }
                }
            }

            return nextFocusedGridKind;
        }

        public static string GetMarker(StagePlacedEntityAuthoring placement)
        {
            return StageAuthoringGridMarkerBuilder.Build(placement);
        }

        public static bool IsOnFace(StagePlacedEntityAuthoring placement, FaceId face)
        {
            return placement != null && placement.Cell.face == face;
        }

        private static bool DrawGridCellButton(
            string marker,
            StagePlacedEntityAuthoring placement,
            StageAuthoringEntityKind? focusedKind,
            bool selectedZoneHighlight,
            bool zoneCell,
            StageAuthoringGridEditMode editMode,
            BoardTilePaintGridPreview paintPreview,
            SurfaceCell cell)
        {
            var previousBackgroundColor = GUI.backgroundColor;
            if (TryResolveCellBackgroundColor(
                    previousBackgroundColor,
                    placement,
                    focusedKind,
                    selectedZoneHighlight,
                    zoneCell,
                    editMode,
                    paintPreview,
                    cell,
                    out var tint))
            {
                GUI.backgroundColor = tint;
            }

            try
            {
                return GUILayout.Button(marker, GUILayout.Width(72), GUILayout.Height(28));
            }
            finally
            {
                GUI.backgroundColor = previousBackgroundColor;
            }
        }

        internal static bool TryResolveCellBackgroundColor(
            Color defaultColor,
            StagePlacedEntityAuthoring placement,
            StageAuthoringEntityKind? focusedKind,
            bool selectedZoneHighlight,
            bool zoneCell,
            StageAuthoringGridEditMode editMode,
            BoardTilePaintGridPreview paintPreview,
            SurfaceCell cell,
            out Color tint)
        {
            var hasCustomTint = false;
            tint = defaultColor;
            if (selectedZoneHighlight)
            {
                tint = new Color(0.5f, 0.85f, 0.45f, 1f);
                hasCustomTint = true;
            }
            else if (zoneCell)
            {
                tint = new Color(0.45f, 0.7f, 0.9f, 1f);
                hasCustomTint = true;
            }
            else if (StageAuthoringGridCellStyleUtility.TryGetTint(placement, focusedKind, out var entityTint))
            {
                tint = entityTint;
                hasCustomTint = true;
            }

            if (editMode == StageAuthoringGridEditMode.BoardTilePaint &&
                paintPreview != null &&
                paintPreview.TryGetTint(cell, out var paintTint))
            {
                tint = BlendPaintTint(tint, paintTint);
                return true;
            }

            return hasCustomTint;
        }

        internal static Color BlendPaintTint(Color baseColor, Color paintTint)
        {
            var opaquePaintTint = new Color(paintTint.r, paintTint.g, paintTint.b, 1f);
            var blendStrength = Mathf.Clamp01(PaintTintBlendStrength * paintTint.a);
            var blended = Color.Lerp(baseColor, opaquePaintTint, blendStrength);
            blended.a = baseColor.a;
            return blended;
        }

        private static string BuildZoneBadge(StageZoneDefinition zone)
        {
            var zoneId = zone.ZoneId?.Trim();
            if (string.IsNullOrEmpty(zoneId))
            {
                return "Z:?";
            }

            return zoneId.Length <= 8
                ? $"Z:{zoneId}"
                : $"Z:{zoneId.Substring(0, 8)}";
        }

        private static StageAuthoringEntityKind? DrawGridLegend(StageAuthoringEntityKind? focusedGridKind)
        {
            var nextFocusedGridKind = focusedGridKind;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Legend", GUILayout.Width(52));
                if (GUILayout.Button(nextFocusedGridKind.HasValue ? "All" : "[All]", GUILayout.Width(48), GUILayout.Height(20)))
                {
                    nextFocusedGridKind = null;
                }

                var descriptors = StageAuthoringKindRegistry.Descriptors;
                for (var i = 0; i < descriptors.Count; i++)
                {
                    var descriptor = descriptors[i];
                    nextFocusedGridKind = DrawLegendItem(
                        descriptor.Kind,
                        $"{descriptor.Marker} {descriptor.DisplayName}",
                        nextFocusedGridKind);
                }
            }

            return nextFocusedGridKind;
        }

        private static StageAuthoringEntityKind? DrawLegendItem(
            StageAuthoringEntityKind kind,
            string label,
            StageAuthoringEntityKind? focusedGridKind)
        {
            var nextFocusedGridKind = focusedGridKind;
            var previousBackgroundColor = GUI.backgroundColor;
            if (StageAuthoringGridCellStyleUtility.TryGetTint(kind, out var tint))
            {
                if (focusedGridKind.HasValue && !StageAuthoringGridCellStyleUtility.IsFocusedKind(kind, focusedGridKind))
                {
                    tint = StageAuthoringGridCellStyleUtility.DimTint(tint);
                }

                GUI.backgroundColor = tint;
            }

            try
            {
                var buttonLabel = StageAuthoringGridCellStyleUtility.IsFocusedKind(kind, focusedGridKind)
                    ? $"[{label}]"
                    : label;
                if (GUILayout.Button(buttonLabel, GUILayout.Width(72), GUILayout.Height(20)))
                {
                    nextFocusedGridKind = StageAuthoringGridCellStyleUtility.IsFocusedKind(kind, focusedGridKind)
                        ? null
                        : kind;
                }
            }
            finally
            {
                GUI.backgroundColor = previousBackgroundColor;
            }

            return nextFocusedGridKind;
        }
    }
}
