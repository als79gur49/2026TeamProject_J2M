using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum StageAuthoringGridInspectorAction
    {
        None,
        RotateLeft,
        RotateRight,
        ClearSelection,
    }

    internal readonly struct StageAuthoringGridInspectorResult
    {
        public StageAuthoringGridInspectorResult(
            bool hasSelection,
            bool serializedFieldsChanged,
            StageAuthoringGridInspectorAction action)
        {
            HasSelection = hasSelection;
            SerializedFieldsChanged = serializedFieldsChanged;
            Action = action;
        }

        public bool HasSelection { get; }

        public bool SerializedFieldsChanged { get; }

        public StageAuthoringGridInspectorAction Action { get; }
    }

    internal static class StageAuthoringGridInspectorRenderer
    {
        public static StageAuthoringGridInspectorResult DrawFields(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            int selectedPlacementIndex,
            bool supportsSelectedFacingAuthoring)
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                EditorGUILayout.HelpBox("No placement selected.", MessageType.Info);
                return new StageAuthoringGridInspectorResult(
                    hasSelection: false,
                    serializedFieldsChanged: false,
                    StageAuthoringGridInspectorAction.None);
            }

            var element = placementsProperty.GetArrayElementAtIndex(selectedPlacementIndex);
            DrawSelectedPlacementHeader(element, authoring.Placements[selectedPlacementIndex]);
            var action = DrawRotateControls(supportsSelectedFacingAuthoring);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(element, includeChildren: true);
            var changed = EditorGUI.EndChangeCheck();
            changed |= StageAuthoringPresentationSectionRenderer.DrawDropdown(
                element,
                authoring.GeneratedPresentationDefinition);

            return new StageAuthoringGridInspectorResult(
                hasSelection: true,
                changed,
                action);
        }

        public static StageAuthoringGridInspectorAction DrawClearSelectionButton()
        {
            return GUILayout.Button("Clear Selection", GUILayout.Width(128))
                ? StageAuthoringGridInspectorAction.ClearSelection
                : StageAuthoringGridInspectorAction.None;
        }

        private static void DrawSelectedPlacementHeader(
            SerializedProperty placementProperty,
            StagePlacedEntityAuthoring placement)
        {
            var displayName = placementProperty.FindPropertyRelative("DisplayName").stringValue;
            var kind = (StageAuthoringEntityKind)placementProperty.FindPropertyRelative("Kind").intValue;
            var cellProperty = placementProperty.FindPropertyRelative("Cell");
            var face = (FaceId)cellProperty.FindPropertyRelative("face").intValue;
            var x = cellProperty.FindPropertyRelative("x").intValue;
            var y = cellProperty.FindPropertyRelative("y").intValue;
            var facing = placement != null ? placement.Facing : Direction.None;
            var facingLabel = StageAuthoringFacingDisplayUtility.ToFacingLabel(facing);
            var facingArrow = StageAuthoringFacingDisplayUtility.ToFacingArrow(facing);
            EditorGUILayout.LabelField(
                "Selected Placement",
                $"{displayName} / {kind} / {face}({x},{y}) / Facing: {facingLabel} {facingArrow}");
        }

        private static StageAuthoringGridInspectorAction DrawRotateControls(bool supportsSelectedFacingAuthoring)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!supportsSelectedFacingAuthoring))
                {
                    if (GUILayout.Button("Rotate Left", GUILayout.Width(112)))
                    {
                        return StageAuthoringGridInspectorAction.RotateLeft;
                    }

                    if (GUILayout.Button("Rotate Right", GUILayout.Width(112)))
                    {
                        return StageAuthoringGridInspectorAction.RotateRight;
                    }
                }
            }

            return StageAuthoringGridInspectorAction.None;
        }
    }
}
