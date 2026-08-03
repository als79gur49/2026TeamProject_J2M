using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal enum StageObjectiveConditionEditorCategory
    {
        PrimaryGoal,
        ButtonObjective,
        OtherCondition,
        InvalidOrUnresolved,
    }

    internal enum StageObjectiveConditionEditorAssociationKind
    {
        PrimaryGoal,
        PushBoxButton,
        MoonBlockButton,
        Button,
        Other,
        InvalidOrUnresolved,
    }

    internal sealed class StageObjectiveConditionEditorRow
    {
        public StageObjectiveConditionEditorRow(
            int entryIndex,
            string stableConditionId,
            string authoringLabel,
            StageObjectiveConditionRole role,
            bool required,
            int sortOrder,
            StageConditionAsset condition,
            string conditionTypeName,
            StageObjectiveConditionEditorCategory category,
            StageObjectiveConditionEditorAssociationKind associationKind,
            int? buttonTileId,
            SurfaceCell? tileCell,
            TileFeatureBoxSelector? boxSelector,
            bool isConditionAssetShared,
            string associationWarning)
        {
            EntryIndex = entryIndex;
            StableConditionId = stableConditionId ?? string.Empty;
            AuthoringLabel = authoringLabel ?? string.Empty;
            Role = role;
            Required = required;
            SortOrder = sortOrder;
            Condition = condition;
            ConditionTypeName = conditionTypeName ?? string.Empty;
            Category = category;
            AssociationKind = associationKind;
            ButtonTileId = buttonTileId;
            TileCell = tileCell;
            BoxSelector = boxSelector;
            IsConditionAssetShared = isConditionAssetShared;
            AssociationWarning = associationWarning ?? string.Empty;
        }

        public int EntryIndex { get; }

        public string StableConditionId { get; }

        public string AuthoringLabel { get; }

        public StageObjectiveConditionRole Role { get; }

        public bool Required { get; }

        public int SortOrder { get; }

        public StageConditionAsset Condition { get; }

        public string ConditionTypeName { get; }

        public StageObjectiveConditionEditorCategory Category { get; }

        public StageObjectiveConditionEditorAssociationKind AssociationKind { get; }

        public int? ButtonTileId { get; }

        public SurfaceCell? TileCell { get; }

        public TileFeatureBoxSelector? BoxSelector { get; }

        public bool IsConditionAssetShared { get; }

        public string AssociationWarning { get; }

        public bool IsAuthoringLabelMissing =>
            StageObjectiveAuthoringMetadataPolicy.IsAuthoringLabelMissing(AuthoringLabel);
    }

    internal static class StageObjectiveConditionEditorResolver
    {
        private const string ObjectivePropertyName = "objective";
        private const string ConditionEntriesPropertyName = "ConditionEntries";

        public static IReadOnlyList<StageObjectiveConditionEditorRow> BuildRows(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring)
        {
            if (serializedAuthoring == null || authoring == null)
            {
                return Array.Empty<StageObjectiveConditionEditorRow>();
            }

            var objectiveProperty = serializedAuthoring.FindProperty(ObjectivePropertyName);
            var entriesProperty = objectiveProperty?.FindPropertyRelative(ConditionEntriesPropertyName);
            if (entriesProperty == null || !entriesProperty.isArray)
            {
                return Array.Empty<StageObjectiveConditionEditorRow>();
            }

            var conditionReferenceCounts = CountConditionReferences(entriesProperty);
            var rows = new List<StageObjectiveConditionEditorRow>(entriesProperty.arraySize);
            for (var i = 0; i < entriesProperty.arraySize; i++)
            {
                rows.Add(BuildRow(entriesProperty.GetArrayElementAtIndex(i), i, authoring, conditionReferenceCounts));
            }

            return rows;
        }

        public static bool TryParseButtonStableTileId(string stableConditionId, out int tileId)
        {
            const string prefix = "button-";
            tileId = 0;
            if (string.IsNullOrEmpty(stableConditionId) ||
                !stableConditionId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(
                       stableConditionId.Substring(prefix.Length),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out tileId) &&
                   tileId > 0;
        }

        private static StageObjectiveConditionEditorRow BuildRow(
            SerializedProperty element,
            int entryIndex,
            StageAuthoringDefinition authoring,
            IReadOnlyDictionary<StageConditionAsset, int> conditionReferenceCounts)
        {
            var condition = element.FindPropertyRelative("Condition").objectReferenceValue as StageConditionAsset;
            var role = (StageObjectiveConditionRole)element.FindPropertyRelative("Role").intValue;
            var stableConditionId = element.FindPropertyRelative("StableConditionId").stringValue ?? string.Empty;
            var category = ResolveCategory(condition, role, stableConditionId);
            var associationKind = ResolveBaseAssociationKind(category);
            int? buttonTileId = null;
            SurfaceCell? tileCell = null;
            TileFeatureBoxSelector? boxSelector = null;
            var associationWarning = string.Empty;

            if (condition is ButtonActivatedConditionAsset buttonCondition)
            {
                buttonTileId = buttonCondition.TileId;
                var matchingFeatures = authoring.TileFeatures
                    .Where(feature =>
                        feature.TileId == buttonCondition.TileId &&
                        feature.Kind == TileFeatureKind.Button)
                    .ToArray();
                if (matchingFeatures.Length == 1)
                {
                    var feature = matchingFeatures[0];
                    tileCell = feature.Cell;
                    boxSelector = feature.BoxSelector;
                    associationKind = feature.BoxSelector switch
                    {
                        TileFeatureBoxSelector.AnyPushableBox =>
                            StageObjectiveConditionEditorAssociationKind.PushBoxButton,
                        TileFeatureBoxSelector.MoonBlockOnly =>
                            StageObjectiveConditionEditorAssociationKind.MoonBlockButton,
                        _ => StageObjectiveConditionEditorAssociationKind.Button,
                    };
                }
                else
                {
                    associationWarning = "Button TileFeature association was not resolved.";
                }

                var expectedStableConditionId = $"button-{buttonCondition.TileId}";
                if (!string.Equals(stableConditionId, expectedStableConditionId, StringComparison.Ordinal))
                {
                    associationWarning = string.IsNullOrEmpty(associationWarning)
                        ? $"StableConditionId must match Button TileId ({expectedStableConditionId})."
                        : $"{associationWarning} StableConditionId must match Button TileId ({expectedStableConditionId}).";
                }
            }

            return new StageObjectiveConditionEditorRow(
                entryIndex,
                stableConditionId,
                element.FindPropertyRelative("AuthoringLabel").stringValue ?? string.Empty,
                role,
                element.FindPropertyRelative("Required").boolValue,
                element.FindPropertyRelative("SortOrder").intValue,
                condition,
                condition != null ? condition.GetType().Name : "<unresolved>",
                category,
                associationKind,
                buttonTileId,
                tileCell,
                boxSelector,
                condition != null &&
                    conditionReferenceCounts.TryGetValue(condition, out var referenceCount) &&
                    referenceCount > 1,
                associationWarning);
        }

        private static Dictionary<StageConditionAsset, int> CountConditionReferences(SerializedProperty entriesProperty)
        {
            var counts = new Dictionary<StageConditionAsset, int>();
            for (var i = 0; i < entriesProperty.arraySize; i++)
            {
                var condition = entriesProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("Condition")
                    .objectReferenceValue as StageConditionAsset;
                if (condition == null)
                {
                    continue;
                }

                counts.TryGetValue(condition, out var count);
                counts[condition] = count + 1;
            }

            return counts;
        }

        private static StageObjectiveConditionEditorCategory ResolveCategory(
            StageConditionAsset condition,
            StageObjectiveConditionRole role,
            string stableConditionId)
        {
            if (condition == null)
            {
                return StageObjectiveConditionEditorCategory.InvalidOrUnresolved;
            }

            if (role == StageObjectiveConditionRole.PrimaryGoal ||
                string.Equals(stableConditionId, "primary-goal", StringComparison.Ordinal))
            {
                return StageObjectiveConditionEditorCategory.PrimaryGoal;
            }

            if (condition is ButtonActivatedConditionAsset)
            {
                return StageObjectiveConditionEditorCategory.ButtonObjective;
            }

            return StageObjectiveConditionEditorCategory.OtherCondition;
        }

        private static StageObjectiveConditionEditorAssociationKind ResolveBaseAssociationKind(
            StageObjectiveConditionEditorCategory category)
        {
            return category switch
            {
                StageObjectiveConditionEditorCategory.PrimaryGoal =>
                    StageObjectiveConditionEditorAssociationKind.PrimaryGoal,
                StageObjectiveConditionEditorCategory.ButtonObjective =>
                    StageObjectiveConditionEditorAssociationKind.Button,
                StageObjectiveConditionEditorCategory.OtherCondition =>
                    StageObjectiveConditionEditorAssociationKind.Other,
                _ => StageObjectiveConditionEditorAssociationKind.InvalidOrUnresolved,
            };
        }
    }
}
