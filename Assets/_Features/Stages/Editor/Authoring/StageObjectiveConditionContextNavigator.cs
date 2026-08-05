using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;

namespace Game.Feature.Stages.Editor
{
    internal enum StageObjectiveConditionContextResolution
    {
        NotApplicable,
        Resolved,
        Unresolved,
    }

    internal static class StageObjectiveConditionContextNavigator
    {
        public const string PrimarySelectionUnresolved = "PRIMARY_OBJECTIVE_SELECTION_UNRESOLVED";
        public const string ButtonSelectionUnresolved = "BUTTON_OBJECTIVE_SELECTION_UNRESOLVED";

        public static StageObjectiveConditionContextResolution SelectForTileFeature(
            IReadOnlyList<StageObjectiveConditionEditorRow> rows,
            StageTileFeatureDefinition feature,
            StageConditionAsset expectedCondition,
            StageObjectiveConditionEditorSelection selection,
            out string warning)
        {
            warning = string.Empty;
            if (selection == null)
            {
                warning = StageObjectiveConditionEditorSelection.IdentityMismatchMessage;
                return StageObjectiveConditionContextResolution.Unresolved;
            }

            return feature.Kind switch
            {
                TileFeatureKind.Exit => SelectPrimary(rows, expectedCondition, selection, out warning),
                TileFeatureKind.Button => SelectButton(rows, feature.TileId, expectedCondition, selection, out warning),
                _ => ClearNotApplicable(selection, out warning),
            };
        }

        private static StageObjectiveConditionContextResolution ClearNotApplicable(
            StageObjectiveConditionEditorSelection selection,
            out string warning)
        {
            selection.Clear();
            warning = string.Empty;
            return StageObjectiveConditionContextResolution.NotApplicable;
        }

        public static StageObjectiveConditionContextResolution SelectPrimary(
            IReadOnlyList<StageObjectiveConditionEditorRow> rows,
            StageConditionAsset expectedCondition,
            StageObjectiveConditionEditorSelection selection,
            out string warning)
        {
            warning = string.Empty;
            rows ??= Array.Empty<StageObjectiveConditionEditorRow>();
            var primaryGoalAssociations = rows
                .Where(row => row.Role == StageObjectiveConditionRole.PrimaryGoal)
                .ToArray();
            var exactMatches = primaryGoalAssociations
                .Where(row =>
                    string.Equals(row.StableConditionId, "primary-goal", StringComparison.Ordinal) &&
                    row.Required &&
                    row.Condition is PlayerAtAnyZoneConditionAsset &&
                    (expectedCondition == null || ReferenceEquals(row.Condition, expectedCondition)))
                .ToArray();

            if (primaryGoalAssociations.Length == 1 && exactMatches.Length == 1)
            {
                selection.Select(exactMatches.Single());
                return StageObjectiveConditionContextResolution.Resolved;
            }

            selection.Clear();
            warning = PrimarySelectionUnresolved;
            return StageObjectiveConditionContextResolution.Unresolved;
        }

        public static StageObjectiveConditionContextResolution SelectButton(
            IReadOnlyList<StageObjectiveConditionEditorRow> rows,
            int tileId,
            StageConditionAsset expectedCondition,
            StageObjectiveConditionEditorSelection selection,
            out string warning)
        {
            warning = string.Empty;
            rows ??= Array.Empty<StageObjectiveConditionEditorRow>();
            if (expectedCondition is not ButtonActivatedConditionAsset)
            {
                selection.Clear();
                warning = ButtonSelectionUnresolved;
                return StageObjectiveConditionContextResolution.Unresolved;
            }

            var expectedStableConditionId = $"button-{tileId}";
            var matchingAssociations = rows
                .Where(row =>
                    string.Equals(
                        StageAuthoringButtonObjectiveHelperCommands.NormalizeStableConditionId(
                            row.StableConditionId),
                        expectedStableConditionId,
                        StringComparison.Ordinal) ||
                    row.ButtonTileId == tileId)
                .ToArray();
            var exactMatches = matchingAssociations
                .Where(row =>
                    string.Equals(
                        row.StableConditionId,
                        expectedStableConditionId,
                        StringComparison.Ordinal) &&
                    row.Required &&
                    row.Role == StageObjectiveConditionRole.SecondaryGoal &&
                    row.Condition is ButtonActivatedConditionAsset buttonCondition &&
                    buttonCondition.TileId == tileId &&
                    row.ButtonTileId == tileId &&
                    ReferenceEquals(row.Condition, expectedCondition))
                .ToArray();

            if (matchingAssociations.Length == 1 && exactMatches.Length == 1)
            {
                selection.Select(exactMatches.Single());
                return StageObjectiveConditionContextResolution.Resolved;
            }

            selection.Clear();
            warning = ButtonSelectionUnresolved;
            return StageObjectiveConditionContextResolution.Unresolved;
        }
    }
}
