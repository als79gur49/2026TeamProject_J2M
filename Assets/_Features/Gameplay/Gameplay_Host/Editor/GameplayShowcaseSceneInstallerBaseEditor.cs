using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Timing;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Host.Editor
{
    [CustomEditor(typeof(GameplayShowcaseSceneInstallerBase), true)]
    [CanEditMultipleObjects]
    public sealed class GameplayShowcaseSceneInstallerBaseEditor : UnityEditor.Editor
    {
        private const float UseConfigurationFallbackSentinel = -1f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            if (targets.Length > 1)
            {
                return;
            }

            DrawTopologyTimingSection();
        }

        private void DrawTopologyTimingSection()
        {
            var presentationTimingPresetProperty = serializedObject.FindProperty("presentationTimingPreset");
            var presentationTimingPreset =
                presentationTimingPresetProperty?.objectReferenceValue as GameplayPresentationTimingPreset;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Topology Rotation Duration", EditorStyles.boldLabel);

            if (presentationTimingPreset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Presentation Timing Preset to author the topology rotation duration. The installer does not own a scene-local override.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Topology rotation duration is owned by the referenced Presentation Timing Preset asset. Editing the field below updates that asset directly instead of serializing a scene-local float.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Source Preset", presentationTimingPreset, typeof(GameplayPresentationTimingPreset), false);
                }

                if (GUILayout.Button("Ping", GUILayout.Width(52f)))
                {
                    EditorGUIUtility.PingObject(presentationTimingPreset);
                }
            }

            var presetSerializedObject = new SerializedObject(presentationTimingPreset);
            presetSerializedObject.Update();

            var topologyMotionDurationProperty =
                presetSerializedObject.FindProperty("topologyMotionDurationSeconds");
            var pushMotionDurationProperty =
                presetSerializedObject.FindProperty("pushMotionDurationSeconds");

            if (topologyMotionDurationProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "The assigned Presentation Timing Preset is missing topologyMotionDurationSeconds serialization.",
                    MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                topologyMotionDurationProperty,
                new GUIContent(
                    "Topology Motion Duration Seconds",
                    "Presentation-only topology rotation duration. Set -1 to preserve the preset/configuration fallback to push motion duration."));

            if (EditorGUI.EndChangeCheck())
            {
                presetSerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(presentationTimingPreset);
            }

            if (topologyMotionDurationProperty.floatValue == UseConfigurationFallbackSentinel)
            {
                var fallbackMessage = pushMotionDurationProperty != null &&
                                      pushMotionDurationProperty.floatValue > 0f
                    ? $"Current preset fallback resolves to Push Motion Duration Seconds ({pushMotionDurationProperty.floatValue:0.###}s)."
                    : "Current preset fallback resolves to the existing configuration push motion duration.";
                EditorGUILayout.HelpBox(fallbackMessage, MessageType.Info);
            }
        }
    }
}
