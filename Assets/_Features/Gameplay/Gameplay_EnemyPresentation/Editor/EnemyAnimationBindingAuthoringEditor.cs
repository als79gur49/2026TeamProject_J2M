using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    [CustomEditor(typeof(EnemyAnimationBindingAuthoring))]
    internal sealed class EnemyAnimationBindingAuthoringEditor : UnityEditor.Editor
    {
        private SerializedProperty _crossFade;
        private SerializedProperty _bindings;

        private void OnEnable()
        {
            _crossFade = serializedObject.FindProperty("defaultStateCrossFadeDurationSeconds");
            _bindings = serializedObject.FindProperty("bindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Cue Bindings", EditorStyles.boldLabel);
            var bindingsChanged = false;
            for (var index = 0; index < _bindings.arraySize; index++)
            {
                bindingsChanged |= DrawBinding(index);
            }

            if (GUILayout.Button("Add Cue Binding"))
            {
                var index = _bindings.arraySize;
                _bindings.arraySize++;
                InitializeNewBinding(_bindings.GetArrayElementAtIndex(index));
                bindingsChanged = true;
            }

            if (bindingsChanged)
            {
                NormalizeBindings(_bindings, _crossFade);
            }

            if (HasPrimaryStateBinding(_bindings))
            {
                EditorGUILayout.PropertyField(_crossFade, new GUIContent("State Cross-Fade Seconds"));
            }

            serializedObject.ApplyModifiedProperties();
            DrawDiagnostics();
        }

        private bool DrawBinding(int index)
        {
            var row = _bindings.GetArrayElementAtIndex(index);
            var cueProperty = row.FindPropertyRelative("cue");
            var modeProperty = row.FindPropertyRelative("primaryDispatchMode");
            var cue = (EnemyAnimationCue)cueProperty.intValue;
            var mode = (EnemyAnimationDispatchMode)modeProperty.intValue;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            cue = (EnemyAnimationCue)EditorGUILayout.EnumPopup("Cue", cue);
            cueProperty.intValue = (int)cue;
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                _bindings.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUI.EndChangeCheck();
                return true;
            }

            EditorGUILayout.EndHorizontal();

            mode = (EnemyAnimationDispatchMode)EditorGUILayout.EnumPopup("Dispatch", mode);
            modeProperty.intValue = (int)mode;
            EditorGUILayout.PropertyField(row.FindPropertyRelative("targetName"), new GUIContent("Target"));

            if (ShouldShowSustainedState(cue, mode))
            {
                EditorGUILayout.PropertyField(
                    row.FindPropertyRelative("sustainedStateName"),
                    new GUIContent("Sustained State"));
            }

            if (ShouldShowReplacementState(cue, mode))
            {
                EditorGUILayout.PropertyField(
                    row.FindPropertyRelative("replacementStateName"),
                    new GUIContent("Replacement State (Optional)"));
            }

            if (ShouldShowTiming(cue))
            {
                EditorGUILayout.PropertyField(
                    row.FindPropertyRelative("animatorDurationSeconds"),
                    new GUIContent("Animator Duration Seconds"));
                EditorGUILayout.PropertyField(
                    row.FindPropertyRelative("referenceClip"),
                    new GUIContent("Reference Clip"));
            }

            EditorGUILayout.EndVertical();
            return EditorGUI.EndChangeCheck();
        }

        private void DrawDiagnostics()
        {
            var authoring = (EnemyAnimationBindingAuthoring)target;
            var animator = ResolveAnimatorForDiagnostics(authoring);
            foreach (var diagnostic in EnemyAnimationControllerBindingValidator.Validate(authoring, animator))
            {
                EditorGUILayout.HelpBox(
                    FormatDiagnostic(diagnostic),
                    diagnostic.Severity == EnemyAnimationBindingDiagnosticSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }

        internal static string FormatDiagnostic(EnemyAnimationBindingDiagnostic diagnostic)
        {
            return $"[{diagnostic.Code}] {diagnostic.Message}";
        }

        internal static Animator ResolveAnimatorForDiagnostics(EnemyAnimationBindingAuthoring authoring)
        {
            var driver = authoring != null
                ? authoring.GetComponentInParent<EnemyAnimatorDriver>(includeInactive: true)
                : null;
            return driver != null
                ? driver.ResolveAnimatorForBindingValidation()
                : null;
        }

        internal static bool ShouldShowSustainedState(
            EnemyAnimationCue cue,
            EnemyAnimationDispatchMode mode)
        {
            return cue == EnemyAnimationCue.JumpAirborne &&
                   mode == EnemyAnimationDispatchMode.Trigger;
        }

        internal static bool ShouldShowReplacementState(
            EnemyAnimationCue cue,
            EnemyAnimationDispatchMode mode)
        {
            return EnemyAnimationBindingSnapshot.AllowsReplacementState(cue, mode);
        }

        internal static bool ShouldShowTiming(EnemyAnimationCue cue)
        {
            return EnemyAnimationCueCatalog.TryGet(cue, out var metadata) && metadata.SupportsTiming;
        }

        internal static void InitializeNewBinding(SerializedProperty row)
        {
            row.FindPropertyRelative("cue").intValue = (int)EnemyAnimationCue.None;
            row.FindPropertyRelative("primaryDispatchMode").intValue = (int)EnemyAnimationDispatchMode.None;
            row.FindPropertyRelative("targetName").stringValue = string.Empty;
            row.FindPropertyRelative("sustainedStateName").stringValue = string.Empty;
            row.FindPropertyRelative("replacementStateName").stringValue = string.Empty;
            row.FindPropertyRelative("animatorDurationSeconds").floatValue =
                EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
            row.FindPropertyRelative("referenceClip").objectReferenceValue = null;
        }

        internal static void NormalizeBindings(
            SerializedProperty bindings,
            SerializedProperty crossFade)
        {
            var hasPrimaryStateBinding = false;
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var row = bindings.GetArrayElementAtIndex(index);
                var cue = (EnemyAnimationCue)row.FindPropertyRelative("cue").intValue;
                var mode = (EnemyAnimationDispatchMode)row.FindPropertyRelative("primaryDispatchMode").intValue;
                hasPrimaryStateBinding |= mode == EnemyAnimationDispatchMode.State;

                if (!ShouldShowSustainedState(cue, mode))
                {
                    row.FindPropertyRelative("sustainedStateName").stringValue = string.Empty;
                }

                if (!ShouldShowReplacementState(cue, mode))
                {
                    row.FindPropertyRelative("replacementStateName").stringValue = string.Empty;
                }

                if (!ShouldShowTiming(cue))
                {
                    row.FindPropertyRelative("animatorDurationSeconds").floatValue =
                        EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
                    row.FindPropertyRelative("referenceClip").objectReferenceValue = null;
                }
            }

            if (!hasPrimaryStateBinding)
            {
                crossFade.floatValue = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
            }
            else if (crossFade.floatValue == EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel)
            {
                crossFade.floatValue = 0f;
            }
        }

        private static bool HasPrimaryStateBinding(SerializedProperty bindings)
        {
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var row = bindings.GetArrayElementAtIndex(index);
                if ((EnemyAnimationDispatchMode)row.FindPropertyRelative("primaryDispatchMode").intValue ==
                    EnemyAnimationDispatchMode.State)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
