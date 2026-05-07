using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGridRenderer
    {
        public static StageAuthoringEntityKind? DrawGrid(
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection,
            StageAuthoringEntityKind? focusedGridKind,
            bool tileFeaturePlacementMode)
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

                        if (selection.TargetCell.x == x && selection.TargetCell.y == y)
                        {
                            marker = $"[{marker}]";
                        }

                        if (DrawGridCellButton(marker, placement, nextFocusedGridKind))
                        {
                            if (tileFeaturePlacementMode)
                            {
                                selection.SelectTileFeatureCell(
                                    selection.TargetFace,
                                    new Vector2Int(x, y),
                                    authoring.TileFeatures);
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
            StageAuthoringEntityKind? focusedKind)
        {
            var previousBackgroundColor = GUI.backgroundColor;
            if (StageAuthoringGridCellStyleUtility.TryGetTint(placement, focusedKind, out var tint))
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
