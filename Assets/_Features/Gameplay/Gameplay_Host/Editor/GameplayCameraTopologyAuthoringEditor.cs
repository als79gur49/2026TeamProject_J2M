using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    [CustomEditor(typeof(GameplayCameraTopologyAuthoring))]
    internal sealed class GameplayCameraTopologyAuthoringEditor : UnityEditor.Editor
    {
        private SerializedProperty _sourceModeProperty;
        private SerializedProperty _presetProperty;
        private SerializedProperty _configureMainCameraProperty;
        private SerializedProperty _baselineAuthoringPolicyProperty;
        private SerializedProperty _inlineSharedTuningProperty;

        private void OnEnable()
        {
            _sourceModeProperty = serializedObject.FindProperty("sourceMode");
            _presetProperty = serializedObject.FindProperty("preset");
            _configureMainCameraProperty = serializedObject.FindProperty("configureMainCamera");
            _baselineAuthoringPolicyProperty = serializedObject.FindProperty("baselineAuthoringPolicy");
            _inlineSharedTuningProperty = serializedObject.FindProperty("inlineSharedTuning");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_sourceModeProperty);
            EditorGUILayout.PropertyField(_presetProperty);
            EditorGUILayout.PropertyField(_configureMainCameraProperty);
            EditorGUILayout.PropertyField(_baselineAuthoringPolicyProperty);

            EditorGUILayout.Space();

            if ((GameplayCameraTopologySourceMode)_sourceModeProperty.enumValueIndex ==
                GameplayCameraTopologySourceMode.Inline)
            {
                EditorGUILayout.PropertyField(_inlineSharedTuningProperty, includeChildren: true);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Preset mode resolves shared tuning only from the referenced GameplayCameraTopologyPreset. Inline shared tuning is hidden because it is not authoritative in Preset mode.",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
