using System;
using System.Collections.Generic;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal enum StageObjectiveConditionSelectionResolution
    {
        None,
        Resolved,
        IdentityMismatch,
    }

    internal sealed class StageObjectiveConditionEditorSelection
    {
        public const string IdentityMismatchMessage = "CONDITION_SELECTION_IDENTITY_MISMATCH";

        private string stableConditionId = string.Empty;
        private StageConditionAsset condition;
        private bool hasSelection;

        public bool HasSelection => hasSelection;

        public string StableConditionId => stableConditionId;

        public StageConditionAsset Condition => condition;

        public void Select(StageObjectiveConditionEditorRow row)
        {
            if (row == null)
            {
                Clear();
                return;
            }

            stableConditionId = row.StableConditionId ?? string.Empty;
            condition = row.Condition;
            hasSelection = true;
        }

        public void Select(string selectedStableConditionId, StageConditionAsset selectedCondition)
        {
            stableConditionId = selectedStableConditionId ?? string.Empty;
            condition = selectedCondition;
            hasSelection = true;
        }

        public void Clear()
        {
            stableConditionId = string.Empty;
            condition = null;
            hasSelection = false;
        }

        public StageObjectiveConditionSelectionResolution Resolve(
            IReadOnlyList<StageObjectiveConditionEditorRow> rows,
            out StageObjectiveConditionEditorRow selectedRow)
        {
            selectedRow = null;
            if (!hasSelection)
            {
                return StageObjectiveConditionSelectionResolution.None;
            }

            var stableMatchCount = 0;
            StageObjectiveConditionEditorRow canonicalMatch = null;
            var canonicalMatchCount = 0;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var stableMatches = string.Equals(
                    row.StableConditionId,
                    stableConditionId,
                    StringComparison.Ordinal);
                var conditionMatches = ReferenceEquals(row.Condition, condition);
                if (stableMatches)
                {
                    stableMatchCount++;
                }

                if (stableMatches && conditionMatches)
                {
                    canonicalMatch = row;
                    canonicalMatchCount++;
                }
            }

            if (stableMatchCount == 1 && canonicalMatchCount == 1)
            {
                selectedRow = canonicalMatch;
                return StageObjectiveConditionSelectionResolution.Resolved;
            }

            return StageObjectiveConditionSelectionResolution.IdentityMismatch;
        }
    }

    internal static class StageObjectiveConditionEditorMutation
    {
        public static bool TrySetAuthoringLabel(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageObjectiveConditionEditorSelection selection,
            string authoringLabel,
            out string error)
        {
            error = string.Empty;
            if (serializedAuthoring == null || authoring == null || selection == null)
            {
                error = StageObjectiveConditionEditorSelection.IdentityMismatchMessage;
                return false;
            }

            serializedAuthoring.Update();
            var rows = StageObjectiveConditionEditorResolver.BuildRows(serializedAuthoring, authoring);
            var resolution = selection.Resolve(rows, out var selectedRow);
            if (resolution != StageObjectiveConditionSelectionResolution.Resolved)
            {
                error = StageObjectiveConditionEditorSelection.IdentityMismatchMessage;
                return false;
            }

            var entriesProperty = serializedAuthoring
                .FindProperty("objective")
                ?.FindPropertyRelative("ConditionEntries");
            if (entriesProperty == null ||
                selectedRow.EntryIndex < 0 ||
                selectedRow.EntryIndex >= entriesProperty.arraySize)
            {
                error = StageObjectiveConditionEditorSelection.IdentityMismatchMessage;
                return false;
            }

            var element = entriesProperty.GetArrayElementAtIndex(selectedRow.EntryIndex);
            var currentStableConditionId =
                element.FindPropertyRelative("StableConditionId").stringValue ?? string.Empty;
            var currentCondition =
                element.FindPropertyRelative("Condition").objectReferenceValue as StageConditionAsset;
            if (!string.Equals(
                    currentStableConditionId,
                    selection.StableConditionId,
                    StringComparison.Ordinal) ||
                !ReferenceEquals(currentCondition, selection.Condition))
            {
                error = StageObjectiveConditionEditorSelection.IdentityMismatchMessage;
                return false;
            }

            var labelProperty = element.FindPropertyRelative("AuthoringLabel");
            var nextValue = authoringLabel ?? string.Empty;
            if (string.Equals(labelProperty.stringValue, nextValue, StringComparison.Ordinal))
            {
                return false;
            }

            labelProperty.stringValue = nextValue;
            return serializedAuthoring.ApplyModifiedProperties();
        }
    }
}
