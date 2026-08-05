using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Objectives;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal enum StageObjectiveConditionSortOrderValidationError
    {
        None,
        IdentityMismatch,
        NotSecondaryGoal,
        NotPositive,
        Duplicate,
    }

    internal readonly struct StageObjectiveConditionSortOrderValidationResult
    {
        private StageObjectiveConditionSortOrderValidationResult(
            StageObjectiveConditionSortOrderValidationError error,
            string message,
            int conflictingEntryIndex = -1,
            string conflictingStableConditionId = "",
            StageObjectiveConditionRole conflictingRole = default,
            int conflictingSortOrder = 0)
        {
            Error = error;
            Message = message ?? string.Empty;
            ConflictingEntryIndex = conflictingEntryIndex;
            ConflictingStableConditionId = conflictingStableConditionId ?? string.Empty;
            ConflictingRole = conflictingRole;
            ConflictingSortOrder = conflictingSortOrder;
        }

        public StageObjectiveConditionSortOrderValidationError Error { get; }

        public string Message { get; }

        public int ConflictingEntryIndex { get; }

        public string ConflictingStableConditionId { get; }

        public StageObjectiveConditionRole ConflictingRole { get; }

        public int ConflictingSortOrder { get; }

        public bool IsValid => Error == StageObjectiveConditionSortOrderValidationError.None;

        public static StageObjectiveConditionSortOrderValidationResult Valid()
        {
            return new StageObjectiveConditionSortOrderValidationResult(
                StageObjectiveConditionSortOrderValidationError.None,
                string.Empty);
        }

        public static StageObjectiveConditionSortOrderValidationResult IdentityMismatch()
        {
            return new StageObjectiveConditionSortOrderValidationResult(
                StageObjectiveConditionSortOrderValidationError.IdentityMismatch,
                StageObjectiveConditionEditorSelection.IdentityMismatchMessage);
        }

        public static StageObjectiveConditionSortOrderValidationResult NotSecondaryGoal()
        {
            return new StageObjectiveConditionSortOrderValidationResult(
                StageObjectiveConditionSortOrderValidationError.NotSecondaryGoal,
                "Only resolved Secondary Goal Sort Order values can be edited.");
        }

        public static StageObjectiveConditionSortOrderValidationResult NotPositive()
        {
            return new StageObjectiveConditionSortOrderValidationResult(
                StageObjectiveConditionSortOrderValidationError.NotPositive,
                "Secondary Objective Sort Order must be greater than zero.");
        }

        public static StageObjectiveConditionSortOrderValidationResult Duplicate(
            int entryIndex,
            string stableConditionId,
            StageObjectiveConditionRole role,
            int sortOrder)
        {
            var conflictName = string.IsNullOrEmpty(stableConditionId)
                ? $"entry[{entryIndex}]"
                : stableConditionId;
            return new StageObjectiveConditionSortOrderValidationResult(
                StageObjectiveConditionSortOrderValidationError.Duplicate,
                $"Sort Order {sortOrder} is already used by {conflictName}.",
                entryIndex,
                stableConditionId,
                role,
                sortOrder);
        }
    }

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

        public static bool TrySetSecondarySortOrder(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageObjectiveConditionEditorSelection selection,
            int sortOrder,
            out StageObjectiveConditionSortOrderValidationResult validation)
        {
            validation = StageObjectiveConditionSortOrderValidationResult.IdentityMismatch();
            if (serializedAuthoring == null || authoring == null || selection == null)
            {
                return false;
            }

            serializedAuthoring.Update();
            var rows = StageObjectiveConditionEditorResolver.BuildRows(serializedAuthoring, authoring);
            var resolution = selection.Resolve(rows, out var selectedRow);
            if (resolution != StageObjectiveConditionSelectionResolution.Resolved ||
                selectedRow == null ||
                string.IsNullOrEmpty(selectedRow.StableConditionId) ||
                selectedRow.Condition == null)
            {
                return false;
            }

            var entriesProperty = serializedAuthoring
                .FindProperty("objective")
                ?.FindPropertyRelative("ConditionEntries");
            if (entriesProperty == null ||
                selectedRow.EntryIndex < 0 ||
                selectedRow.EntryIndex >= entriesProperty.arraySize)
            {
                return false;
            }

            var element = entriesProperty.GetArrayElementAtIndex(selectedRow.EntryIndex);
            var stableConditionIdProperty = element.FindPropertyRelative("StableConditionId");
            var conditionProperty = element.FindPropertyRelative("Condition");
            var roleProperty = element.FindPropertyRelative("Role");
            var sortOrderProperty = element.FindPropertyRelative("SortOrder");
            if (stableConditionIdProperty == null ||
                conditionProperty == null ||
                roleProperty == null ||
                sortOrderProperty == null)
            {
                return false;
            }

            var currentStableConditionId = stableConditionIdProperty.stringValue ?? string.Empty;
            var currentCondition = conditionProperty.objectReferenceValue as StageConditionAsset;
            if (!string.Equals(
                    currentStableConditionId,
                    selection.StableConditionId,
                    StringComparison.Ordinal) ||
                !ReferenceEquals(currentCondition, selection.Condition))
            {
                return false;
            }

            var role = (StageObjectiveConditionRole)roleProperty.intValue;
            if (role != StageObjectiveConditionRole.SecondaryGoal)
            {
                validation = StageObjectiveConditionSortOrderValidationResult.NotSecondaryGoal();
                return false;
            }

            if (sortOrder <= 0)
            {
                validation = StageObjectiveConditionSortOrderValidationResult.NotPositive();
                return false;
            }

            for (var i = 0; i < entriesProperty.arraySize; i++)
            {
                if (i == selectedRow.EntryIndex)
                {
                    continue;
                }

                var other = entriesProperty.GetArrayElementAtIndex(i);
                var otherSortOrder = other.FindPropertyRelative("SortOrder").intValue;
                if (otherSortOrder != sortOrder)
                {
                    continue;
                }

                validation = StageObjectiveConditionSortOrderValidationResult.Duplicate(
                    i,
                    other.FindPropertyRelative("StableConditionId").stringValue ?? string.Empty,
                    (StageObjectiveConditionRole)other.FindPropertyRelative("Role").intValue,
                    otherSortOrder);
                return false;
            }

            validation = StageObjectiveConditionSortOrderValidationResult.Valid();
            if (sortOrderProperty.intValue == sortOrder)
            {
                return false;
            }

            sortOrderProperty.intValue = sortOrder;
            return serializedAuthoring.ApplyModifiedProperties();
        }
    }
}
