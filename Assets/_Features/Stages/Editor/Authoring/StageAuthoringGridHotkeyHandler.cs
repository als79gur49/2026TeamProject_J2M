using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGridHotkeyHandler
    {
        public static StageAuthoringGridInspectorAction HandleRotateHotkeys(
            int selectedPlacementIndex,
            bool supportsSelectedFacingAuthoring)
        {
            var currentEvent = Event.current;
            if (currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode != KeyCode.R ||
                EditorGUIUtility.editingTextField ||
                GUIUtility.hotControl != 0 ||
                selectedPlacementIndex < 0 ||
                !supportsSelectedFacingAuthoring)
            {
                return StageAuthoringGridInspectorAction.None;
            }

            currentEvent.Use();
            return currentEvent.shift
                ? StageAuthoringGridInspectorAction.RotateLeft
                : StageAuthoringGridInspectorAction.RotateRight;
        }
    }
}
