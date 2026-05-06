using Game.Feature.Gameplay.BoardState;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringPlacementCommands
    {
        public static StageAuthoringCommandResult AddPlacement(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection)
        {
            if (serializedAuthoring == null ||
                authoring == null ||
                selection == null ||
                selection.CountPlacementsAt(
                    authoring.Placements,
                    selection.TargetFace,
                    selection.TargetCell.x,
                    selection.TargetCell.y) > 0)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            var placementsProperty = serializedAuthoring.FindProperty("placements");
            placementsProperty.InsertArrayElementAtIndex(placementsProperty.arraySize);
            var addedIndex = placementsProperty.arraySize - 1;
            var element = placementsProperty.GetArrayElementAtIndex(addedIndex);
            element.FindPropertyRelative("StableGuid").stringValue = System.Guid.NewGuid().ToString("N");
            element.FindPropertyRelative("DisplayName").stringValue = "Placement";
            element.FindPropertyRelative("Kind").intValue = (int)StageAuthoringEntityKind.Box;
            var cellProperty = element.FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)selection.TargetFace;
            cellProperty.FindPropertyRelative("x").intValue = selection.TargetCell.x;
            cellProperty.FindPropertyRelative("y").intValue = selection.TargetCell.y;
            element.FindPropertyRelative("Facing").intValue = (int)Direction.None;
            element.FindPropertyRelative("Hp").intValue = 1;
            element.FindPropertyRelative("UnitStackGroup").stringValue = string.Empty;
            element.FindPropertyRelative("BoxCapabilities").intValue = (int)BoxCapabilities.None;
            element.FindPropertyRelative("BoxArchetype").intValue = (int)BoxArchetype.Normal;
            element.FindPropertyRelative("EnemyAiMode").intValue = 0;
            element.FindPropertyRelative("EnemyAiStateTimer").intValue = 0;
            element.FindPropertyRelative("EnemyAiProfileOverride").objectReferenceValue = null;
            element.FindPropertyRelative("PresentationId").stringValue = string.Empty;
            return StageAuthoringCommandResult.ChangedResult(selectPlacementIndex: addedIndex);
        }

        public static StageAuthoringCommandResult MoveSelectedHere(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection,
            int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                authoring == null ||
                selection == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize ||
                selection.IsTargetOccupiedByOther(authoring.Placements, selectedPlacementIndex) ||
                selection.IsSelectedPlacementAtTarget(authoring.Placements, selectedPlacementIndex))
            {
                return StageAuthoringCommandResult.NoOp();
            }

            var cellProperty = placementsProperty
                .GetArrayElementAtIndex(selectedPlacementIndex)
                .FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)selection.TargetFace;
            cellProperty.FindPropertyRelative("x").intValue = selection.TargetCell.x;
            cellProperty.FindPropertyRelative("y").intValue = selection.TargetCell.y;
            return StageAuthoringCommandResult.ChangedResult();
        }

        public static StageAuthoringCommandResult DeleteSelected(
            SerializedObject serializedAuthoring,
            int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            placementsProperty.DeleteArrayElementAtIndex(selectedPlacementIndex);
            return StageAuthoringCommandResult.ChangedResult(clearSelectedPlacement: true);
        }

        public static StageAuthoringCommandResult ClearSelection()
        {
            return StageAuthoringCommandResult.ChangedResult(
                requiresApply: false,
                requiresUpdate: false,
                shouldRepaint: true,
                clearSelectedPlacement: true);
        }

        public static StageAuthoringCommandResult RotateSelectedClockwise(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            int selectedPlacementIndex)
        {
            return RotateSelectedFacing(serializedAuthoring, authoring, selectedPlacementIndex, clockwise: true);
        }

        public static StageAuthoringCommandResult RotateSelectedCounterClockwise(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            int selectedPlacementIndex)
        {
            return RotateSelectedFacing(serializedAuthoring, authoring, selectedPlacementIndex, clockwise: false);
        }

        public static bool SupportsSelectedFacingAuthoring(
            SerializedObject serializedAuthoring,
            int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return false;
            }

            var kind = (StageAuthoringEntityKind)placementsProperty
                .GetArrayElementAtIndex(selectedPlacementIndex)
                .FindPropertyRelative("Kind")
                .intValue;
            return StageAuthoringKindRegistry.TryGet(kind, out var descriptor) &&
                   descriptor.SupportsFacingAuthoring;
        }

        private static StageAuthoringCommandResult RotateSelectedFacing(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            int selectedPlacementIndex,
            bool clockwise)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                authoring == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize ||
                !SupportsSelectedFacingAuthoring(serializedAuthoring, selectedPlacementIndex))
            {
                return StageAuthoringCommandResult.NoOp();
            }

            var element = placementsProperty.GetArrayElementAtIndex(selectedPlacementIndex);
            var facingProperty = element.FindPropertyRelative("Facing");
            var current = (Direction)facingProperty.intValue;
            var next = clockwise
                ? StageAuthoringFacingDisplayUtility.RotateClockwise(current)
                : StageAuthoringFacingDisplayUtility.RotateCounterClockwise(current);
            if (next == current)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            Undo.RecordObject(authoring, "Rotate Stage Placement Facing");
            facingProperty.intValue = (int)next;
            return StageAuthoringCommandResult.ChangedResult(
                requiresSetDirty: true,
                shouldRepaint: true);
        }
    }
}
