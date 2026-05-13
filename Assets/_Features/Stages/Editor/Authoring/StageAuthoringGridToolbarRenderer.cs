using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum StageAuthoringGridToolbarAction
    {
        None,
        Generate,
        Validate,
        AddPlacement,
        MoveSelectedHere,
        CopySelectedHere,
        DeleteSelected,
    }

    internal enum StageAuthoringTileFeatureCellAction
    {
        None,
        AddTileFeature,
        MoveSelectedHere,
        CopySelectedHere,
        DeleteSelected,
    }

    internal readonly struct StageAuthoringGridToolbarResult
    {
        public StageAuthoringGridToolbarResult(
            bool faceChanged,
            Game.Feature.Gameplay.BoardState.FaceId nextFace,
            StageAuthoringGridToolbarAction action)
        {
            FaceChanged = faceChanged;
            NextFace = nextFace;
            Action = action;
        }

        public bool FaceChanged { get; }

        public Game.Feature.Gameplay.BoardState.FaceId NextFace { get; }

        public StageAuthoringGridToolbarAction Action { get; }
    }

    internal static class StageAuthoringGridToolbarRenderer
    {
        public static StageAuthoringGridToolbarResult DrawHeader(
            Game.Feature.Gameplay.BoardState.FaceId targetFace)
        {
            var action = StageAuthoringGridToolbarAction.None;
            var nextFace = targetFace;
            using (new EditorGUILayout.HorizontalScope())
            {
                nextFace = (Game.Feature.Gameplay.BoardState.FaceId)EditorGUILayout.EnumPopup("Face", targetFace);

                if (GUILayout.Button("Generate", GUILayout.Width(96)))
                {
                    action = StageAuthoringGridToolbarAction.Generate;
                }

                if (GUILayout.Button("Validate", GUILayout.Width(96)))
                {
                    action = StageAuthoringGridToolbarAction.Validate;
                }
            }

            return new StageAuthoringGridToolbarResult(nextFace != targetFace, nextFace, action);
        }

        public static StageAuthoringGridToolbarAction DrawSelectedCellTools(
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection,
            int selectedPlacementIndex)
        {
            var targetPlacementCount = selection.CountPlacementsAt(
                authoring.Placements,
                selection.TargetFace,
                selection.TargetCell.x,
                selection.TargetCell.y);
            var targetOccupied = targetPlacementCount > 0;
            var targetOccupiedByOther = selection.IsTargetOccupiedByOther(authoring.Placements, selectedPlacementIndex);
            var selectedAtTarget = selection.IsSelectedPlacementAtTarget(authoring.Placements, selectedPlacementIndex);

            EditorGUILayout.LabelField(
                "Target Cell",
                $"{selection.TargetFace}({selection.TargetCell.x},{selection.TargetCell.y})");
            if (targetPlacementCount > 1)
            {
                EditorGUILayout.HelpBox(
                    "Target cell contains multiple placements. Resolve the duplicate authoring data before moving or adding.",
                    MessageType.Warning);
            }
            else if (targetOccupiedByOther)
            {
                EditorGUILayout.HelpBox("Target cell already contains a placement.", MessageType.Warning);
            }
            else if (selectedAtTarget)
            {
                EditorGUILayout.HelpBox("Selected placement is already at the target cell.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(targetOccupied))
                {
                    if (GUILayout.Button("Add Placement"))
                    {
                        return StageAuthoringGridToolbarAction.AddPlacement;
                    }
                }

                using (new EditorGUI.DisabledScope(
                           selectedPlacementIndex < 0 ||
                           targetOccupiedByOther ||
                           selectedAtTarget))
                {
                    if (GUILayout.Button("Move Selected Here"))
                    {
                        return StageAuthoringGridToolbarAction.MoveSelectedHere;
                    }
                }

                using (new EditorGUI.DisabledScope(selectedPlacementIndex < 0 || targetOccupied))
                {
                    if (GUILayout.Button("Copy Selected Here"))
                    {
                        return StageAuthoringGridToolbarAction.CopySelectedHere;
                    }
                }

                using (new EditorGUI.DisabledScope(selectedPlacementIndex < 0))
                {
                    if (GUILayout.Button("Delete Selected"))
                    {
                        return StageAuthoringGridToolbarAction.DeleteSelected;
                    }
                }
            }

            return StageAuthoringGridToolbarAction.None;
        }

        public static StageAuthoringTileFeatureCellAction DrawTileFeatureCellTools(
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection,
            int selectedTileFeatureIndex)
        {
            var targetCell = new SurfaceCell(
                selection.TargetFace,
                selection.TargetCell.x,
                selection.TargetCell.y);
            var targetTileFeatureCount = selection.CountTileFeaturesAt(
                authoring.TileFeatures,
                targetCell.face,
                targetCell.x,
                targetCell.y);
            var selectedAtTarget =
                selectedTileFeatureIndex >= 0 &&
                selectedTileFeatureIndex < authoring.TileFeatures.Count &&
                authoring.TileFeatures[selectedTileFeatureIndex].Cell == targetCell;

            EditorGUILayout.LabelField("Target Cell", targetCell.ToString());
            if (selectedAtTarget)
            {
                EditorGUILayout.HelpBox("Selected TileFeature is already at the target cell.", MessageType.Info);
            }
            else if (targetTileFeatureCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Target cell contains {targetTileFeatureCount} TileFeature(s).",
                    MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add TileFeature"))
                {
                    return StageAuthoringTileFeatureCellAction.AddTileFeature;
                }

                using (new EditorGUI.DisabledScope(selectedTileFeatureIndex < 0 || selectedAtTarget))
                {
                    if (GUILayout.Button("Move Selected Here"))
                    {
                        return StageAuthoringTileFeatureCellAction.MoveSelectedHere;
                    }
                }

                using (new EditorGUI.DisabledScope(selectedTileFeatureIndex < 0))
                {
                    if (GUILayout.Button("Copy Selected Here"))
                    {
                        return StageAuthoringTileFeatureCellAction.CopySelectedHere;
                    }
                }

                using (new EditorGUI.DisabledScope(selectedTileFeatureIndex < 0))
                {
                    if (GUILayout.Button("Delete Selected"))
                    {
                        return StageAuthoringTileFeatureCellAction.DeleteSelected;
                    }
                }
            }

            return StageAuthoringTileFeatureCellAction.None;
        }

        public static void DrawReport(StageAuthoringGenerationReport report)
        {
            if (report == null)
            {
                return;
            }

            EditorGUILayout.Space();
            var messageType = report.HasErrors ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(
                report.HasErrors
                    ? "Stage authoring generation has errors."
                    : $"Stage authoring generation completed with {report.Issues.Count} issue(s).",
                messageType);

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                var issueType = issue.Severity switch
                {
                    StageValidationSeverity.Error => MessageType.Error,
                    StageValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Message}", issueType);
            }
        }
    }
}
