using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal static class StageObjectiveConditionEditorRenderer
    {
        public static bool Draw(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageObjectiveConditionEditorSelection selection,
            ref Vector2 listScroll)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Objective Conditions", EditorStyles.boldLabel);

            var rows = StageObjectiveConditionEditorResolver.BuildRows(serializedAuthoring, authoring);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("The current Stage has no Objective condition entries.", MessageType.Info);
                DrawGeneratedStageReference(authoring);
                return false;
            }

            if (!selection.HasSelection)
            {
                selection.Select(rows[0]);
            }

            var changed = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(372)))
                {
                    EditorGUILayout.LabelField($"Condition List ({rows.Count})", EditorStyles.miniBoldLabel);
                    var listHeight = Mathf.Clamp(rows.Count * 48f, 128f, 320f);
                    listScroll = EditorGUILayout.BeginScrollView(
                        listScroll,
                        EditorStyles.helpBox,
                        GUILayout.Width(368),
                        GUILayout.Height(listHeight));
                    for (var i = 0; i < rows.Count; i++)
                    {
                        DrawRow(rows[i], selection);
                    }

                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    changed = DrawSelectedDetail(serializedAuthoring, authoring, rows, selection);
                }
            }

            return changed;
        }

        public static bool HasObjectiveGeneratedDrift(StageAuthoringDefinition authoring)
        {
            if (authoring == null || authoring.GeneratedGameplayDefinition == null)
            {
                return false;
            }

            try
            {
                var allocationPlan = StageAuthoringProjection.BuildAllocationPlan(authoring);
                var issues = StageAuthoringDriftComparer.CompareGameplay(
                    StageAuthoringProjection.ProjectExpectedGameplay(authoring, allocationPlan),
                    StageAuthoringProjection.ProjectActualGameplay(authoring.GeneratedGameplayDefinition),
                    new StageAuthoringDriftContext(
                        StageValidationSeverity.Warning,
                        StageValidationTiming.EditorAuthoring,
                        authoring,
                        AssetDatabase.GetAssetPath(authoring),
                        authoring.name,
                        authoring.name,
                        authoring.GeneratedGameplayDefinition.name));
                return issues.Any(issue =>
                    issue.Code == "GameplayDrift.ObjectiveMismatch" ||
                    issue.FieldName.StartsWith("Objective.", StringComparison.Ordinal));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DrawRow(
            StageObjectiveConditionEditorRow row,
            StageObjectiveConditionEditorSelection selection)
        {
            var prefix = row.Category switch
            {
                StageObjectiveConditionEditorCategory.PrimaryGoal => "[P]",
                StageObjectiveConditionEditorCategory.ButtonObjective => "[B]",
                StageObjectiveConditionEditorCategory.OtherCondition => "[?]",
                _ => "[!]",
            };
            var label = string.IsNullOrWhiteSpace(row.AuthoringLabel)
                ? "<empty label>"
                : row.AuthoringLabel;
            var isSelected =
                selection.HasSelection &&
                string.Equals(
                    selection.StableConditionId,
                    row.StableConditionId,
                    StringComparison.Ordinal) &&
                ReferenceEquals(selection.Condition, row.Condition);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var previousBackground = GUI.backgroundColor;
                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.55f, 0.78f, 1f);
                }

                if (GUILayout.Button(
                        $"{prefix} {row.StableConditionId} — {label}",
                        EditorStyles.miniButton,
                        GUILayout.Height(22)))
                {
                    selection.Select(row);
                }

                GUI.backgroundColor = previousBackground;
                EditorGUILayout.LabelField(
                    $"{row.ConditionTypeName}  |  {row.Role}  |  Required: {row.Required}  |  Sort: {row.SortOrder}",
                    EditorStyles.miniLabel);
            }
        }

        private static bool DrawSelectedDetail(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            IReadOnlyList<StageObjectiveConditionEditorRow> rows,
            StageObjectiveConditionEditorSelection selection)
        {
            EditorGUILayout.LabelField("Selected Condition", EditorStyles.miniBoldLabel);
            var resolution = selection.Resolve(rows, out var row);
            if (resolution == StageObjectiveConditionSelectionResolution.IdentityMismatch)
            {
                EditorGUILayout.HelpBox(
                    StageObjectiveConditionEditorSelection.IdentityMismatchMessage +
                    "\nThe selected stable ID and condition reference no longer identify one canonical row.",
                    MessageType.Warning);
                DrawGeneratedStageReference(authoring);
                return false;
            }

            if (resolution != StageObjectiveConditionSelectionResolution.Resolved || row == null)
            {
                EditorGUILayout.HelpBox("Select an Objective condition row.", MessageType.Info);
                DrawGeneratedStageReference(authoring);
                return false;
            }

            EditorGUI.BeginChangeCheck();
            var nextLabel = EditorGUILayout.TextField("Authoring Label", row.AuthoringLabel);
            var labelChanged = EditorGUI.EndChangeCheck();
            var changed = false;
            if (labelChanged)
            {
                changed = StageObjectiveConditionEditorMutation.TrySetAuthoringLabel(
                    serializedAuthoring,
                    authoring,
                    selection,
                    nextLabel,
                    out var mutationError);
                if (!string.IsNullOrEmpty(mutationError))
                {
                    EditorGUILayout.HelpBox(mutationError, MessageType.Warning);
                }
            }

            EditorGUILayout.HelpBox(
                "Editor-facing label used for authoring, validation, and drift comparison. " +
                "It is not player-facing localized copy.",
                MessageType.Info);
            if (StageObjectiveAuthoringMetadataPolicy.IsAuthoringLabelMissing(
                    labelChanged ? nextLabel : row.AuthoringLabel))
            {
                EditorGUILayout.HelpBox(
                    "Objective condition authoring label is required. " +
                    "Generate validation must pass before output can be written " +
                    $"({StageObjectiveAuthoringMetadataPolicy.AuthoringLabelMissingCode}).",
                    MessageType.Error);
            }

            DrawReadOnlyText("Stable ID", row.StableConditionId);
            DrawReadOnlyText("Type", row.ConditionTypeName);
            DrawReadOnlyText("Role", row.Role.ToString());
            DrawReadOnlyText("Required", row.Required.ToString());
            DrawReadOnlyText("Sort Order", row.SortOrder.ToString());
            DrawConditionAsset(row);
            DrawTypeSpecificSummary(row);

            if (!string.IsNullOrEmpty(row.AssociationWarning))
            {
                EditorGUILayout.HelpBox(row.AssociationWarning, MessageType.Warning);
            }

            if (row.IsConditionAssetShared)
            {
                EditorGUILayout.HelpBox(
                    "Shared condition asset\nParameters are read-only in this editor.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Condition parameters are read-only in this editor.",
                    MessageType.Info);
            }

            DrawGeneratedStageReference(authoring);
            if (HasObjectiveGeneratedDrift(authoring))
            {
                EditorGUILayout.HelpBox(
                    "Generate required: Objective condition authoring differs from the generated StageDefinition.",
                    MessageType.Warning);
            }

            return changed;
        }

        private static void DrawConditionAsset(StageObjectiveConditionEditorRow row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "Condition Asset",
                        row.Condition,
                        typeof(StageConditionAsset),
                        allowSceneObjects: false);
                }

                using (new EditorGUI.DisabledScope(row.Condition == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(52)))
                    {
                        EditorGUIUtility.PingObject(row.Condition);
                    }
                }
            }
        }

        private static void DrawTypeSpecificSummary(StageObjectiveConditionEditorRow row)
        {
            switch (row.Category)
            {
                case StageObjectiveConditionEditorCategory.PrimaryGoal:
                    DrawPrimarySummary(row);
                    break;
                case StageObjectiveConditionEditorCategory.ButtonObjective:
                    DrawButtonSummary(row);
                    break;
                case StageObjectiveConditionEditorCategory.OtherCondition:
                    DrawReadOnlyText("Association", "Other Condition");
                    break;
                default:
                    DrawReadOnlyText("Association", "Invalid / Unresolved");
                    break;
            }
        }

        private static void DrawPrimarySummary(StageObjectiveConditionEditorRow row)
        {
            DrawReadOnlyText("Association", "Primary Goal");
            if (row.Condition is PlayerAtAnyZoneConditionAsset playerAtAnyZone)
            {
                var zoneSummary = playerAtAnyZone.ZoneIds.Length > 0
                    ? string.Join(", ", playerAtAnyZone.ZoneIds)
                    : "<no zones>";
                DrawReadOnlyText("Zone Association", zoneSummary);
                DrawReadOnlyText("Require Alive", playerAtAnyZone.RequireAlive.ToString());
            }
            else
            {
                DrawReadOnlyText("Condition Summary", row.ConditionTypeName);
            }
        }

        private static void DrawButtonSummary(StageObjectiveConditionEditorRow row)
        {
            DrawReadOnlyText(
                "Association",
                row.AssociationKind switch
                {
                    StageObjectiveConditionEditorAssociationKind.PushBoxButton =>
                        "Push-box Button Objective",
                    StageObjectiveConditionEditorAssociationKind.MoonBlockButton =>
                        "MoonBlock Button Objective",
                    _ => "Button Objective",
                });
            DrawReadOnlyText(
                "Button TileId",
                row.ButtonTileId.HasValue ? row.ButtonTileId.Value.ToString() : "<unresolved>");
            DrawReadOnlyText(
                "Tile Coordinate",
                row.TileCell.HasValue ? row.TileCell.Value.ToString() : "<unresolved>");
            DrawReadOnlyText(
                "Box Selector",
                row.BoxSelector.HasValue ? row.BoxSelector.Value.ToString() : "<unresolved>");
        }

        private static void DrawGeneratedStageReference(StageAuthoringDefinition authoring)
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "Generated Stage Definition",
                        authoring != null ? authoring.GeneratedGameplayDefinition : null,
                        typeof(StageDefinition),
                        allowSceneObjects: false);
                }

                using (new EditorGUI.DisabledScope(
                           authoring == null || authoring.GeneratedGameplayDefinition == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(52)))
                    {
                        EditorGUIUtility.PingObject(authoring.GeneratedGameplayDefinition);
                    }
                }
            }
        }

        private static void DrawReadOnlyText(string label, string value)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(label, value ?? string.Empty);
            }
        }
    }
}
