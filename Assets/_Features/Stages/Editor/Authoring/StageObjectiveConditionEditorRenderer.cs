using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Objectives;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal sealed class StageObjectiveConditionSortOrderEditState
    {
        private string stableConditionId = string.Empty;
        private StageConditionAsset condition;
        private string message = string.Empty;

        public void Set(StageObjectiveConditionEditorRow row, string validationMessage)
        {
            stableConditionId = row?.StableConditionId ?? string.Empty;
            condition = row?.Condition;
            message = validationMessage ?? string.Empty;
        }

        public void Clear()
        {
            stableConditionId = string.Empty;
            condition = null;
            message = string.Empty;
        }

        public string GetMessage(StageObjectiveConditionEditorRow row)
        {
            return row != null &&
                   string.Equals(stableConditionId, row.StableConditionId, StringComparison.Ordinal) &&
                   ReferenceEquals(condition, row.Condition)
                ? message
                : string.Empty;
        }
    }

    internal static class StageObjectiveConditionEditorRenderer
    {
        public static bool Draw(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageObjectiveConditionEditorSelection selection,
            StageObjectiveConditionEditorFeedback feedback,
            StageObjectiveConditionSortOrderEditState sortOrderEditState,
            string contextWarning,
            ref Vector2 listScroll)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Objective Conditions", EditorStyles.boldLabel);
            feedback ??= StageObjectiveConditionEditorFeedback.Unresolved;
            DrawStatusSummary(feedback);
            if (!string.IsNullOrEmpty(contextWarning))
            {
                EditorGUILayout.HelpBox(contextWarning, MessageType.Warning);
            }

            var rows = StageObjectiveConditionEditorResolver.BuildRows(serializedAuthoring, authoring);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("The current Stage has no Objective condition entries.", MessageType.Info);
                DrawGeneratedStageReference(authoring);
                return false;
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
                        DrawRow(rows[i], selection, feedback);
                    }

                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    changed = DrawSelectedDetail(
                        serializedAuthoring,
                        authoring,
                        rows,
                        selection,
                        feedback,
                        sortOrderEditState);
                }
            }

            return changed;
        }

        private static void DrawRow(
            StageObjectiveConditionEditorRow row,
            StageObjectiveConditionEditorSelection selection,
            StageObjectiveConditionEditorFeedback feedback)
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
                EditorGUILayout.LabelField($"Status: {feedback.StatusLabel}", EditorStyles.miniLabel);
            }
        }

        private static bool DrawSelectedDetail(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            IReadOnlyList<StageObjectiveConditionEditorRow> rows,
            StageObjectiveConditionEditorSelection selection,
            StageObjectiveConditionEditorFeedback feedback,
            StageObjectiveConditionSortOrderEditState sortOrderEditState)
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

            EditorGUILayout.LabelField("Status", feedback.StatusLabel, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                feedback.GetRowMessage(row),
                ToMessageType(feedback.Status));

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
            changed |= DrawSortOrder(
                serializedAuthoring,
                authoring,
                selection,
                row,
                sortOrderEditState);
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

            return changed;
        }

        internal static bool CanEditSortOrder(StageObjectiveConditionEditorRow row)
        {
            return row != null &&
                   row.Role == StageObjectiveConditionRole.SecondaryGoal &&
                   !string.IsNullOrEmpty(row.StableConditionId) &&
                   row.Condition != null;
        }

        private static bool DrawSortOrder(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageObjectiveConditionEditorSelection selection,
            StageObjectiveConditionEditorRow row,
            StageObjectiveConditionSortOrderEditState editState)
        {
            editState ??= new StageObjectiveConditionSortOrderEditState();
            if (!CanEditSortOrder(row))
            {
                DrawReadOnlyText("Sort Order", row.SortOrder.ToString());
                if (row.Role == StageObjectiveConditionRole.PrimaryGoal)
                {
                    EditorGUILayout.HelpBox(
                        "Primary Goal ordering is preserved by this editor.",
                        MessageType.Info);
                }

                return false;
            }

            EditorGUI.BeginChangeCheck();
            var nextSortOrder = EditorGUILayout.IntField(
                new GUIContent(
                    "Sort Order",
                    "Controls presentation order. Must be a positive value unique within this Stage. Gaps are allowed."),
                row.SortOrder);
            var sortOrderChanged = EditorGUI.EndChangeCheck();
            var changed = false;
            if (sortOrderChanged)
            {
                changed = StageObjectiveConditionEditorMutation.TrySetSecondarySortOrder(
                    serializedAuthoring,
                    authoring,
                    selection,
                    nextSortOrder,
                    out var validation);
                if (validation.IsValid)
                {
                    editState.Clear();
                }
                else
                {
                    editState.Set(row, validation.Message);
                }
            }

            EditorGUILayout.HelpBox(
                "Controls presentation order. Must be a positive value unique within this Stage. Gaps are allowed.",
                MessageType.Info);
            var validationMessage = editState.GetMessage(row);
            if (!string.IsNullOrEmpty(validationMessage))
            {
                EditorGUILayout.HelpBox(validationMessage, MessageType.Error);
            }

            return changed;
        }

        private static void DrawStatusSummary(StageObjectiveConditionEditorFeedback feedback)
        {
            EditorGUILayout.LabelField("Status", feedback.StatusLabel, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(feedback.Message, ToMessageType(feedback.Status));
            for (var i = 0; i < feedback.ValidationIssues.Count; i++)
            {
                var issue = feedback.ValidationIssues[i];
                EditorGUILayout.HelpBox(
                    $"{issue.Code}: {issue.Message}",
                    issue.Severity == StageValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }

            if (feedback.HasAdditionalValidationIssues)
            {
                EditorGUILayout.HelpBox(
                    "Stage has additional validation issues. Use the Stage validation report below for details.",
                    MessageType.Info);
            }
        }

        private static MessageType ToMessageType(StageObjectiveConditionEditorStatus status)
        {
            return status switch
            {
                StageObjectiveConditionEditorStatus.InSync => MessageType.Info,
                StageObjectiveConditionEditorStatus.GenerateRequired => MessageType.Warning,
                StageObjectiveConditionEditorStatus.InvalidAuthoring => MessageType.Error,
                StageObjectiveConditionEditorStatus.GeneratedOutputMissing => MessageType.Warning,
                _ => MessageType.Warning,
            };
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
