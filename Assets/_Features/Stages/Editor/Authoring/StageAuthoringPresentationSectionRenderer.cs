using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringPresentationSectionRenderer
    {
        public static bool DrawDropdown(
            SerializedProperty placementProperty,
            StagePresentationDefinition generatedPresentationDefinition)
        {
            var kind = (StageAuthoringEntityKind)placementProperty.FindPropertyRelative("Kind").intValue;
            if (kind == StageAuthoringEntityKind.Player)
            {
                return false;
            }

            var presentationProperty = placementProperty.FindPropertyRelative("PresentationId");
            var model = StageAuthoringPresentationOptionModel.Build(
                kind,
                presentationProperty.stringValue,
                generatedPresentationDefinition);
            for (var i = 0; i < model.WarningMessages.Length; i++)
            {
                EditorGUILayout.HelpBox(model.WarningMessages[i], MessageType.Warning);
            }

            if (model.PopupLabels.Length == 0)
            {
                return false;
            }

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUILayout.Popup(
                "Presentation",
                model.SelectedPopupIndex,
                model.PopupLabels);
            if (!EditorGUI.EndChangeCheck())
            {
                return false;
            }

            presentationProperty.stringValue = model.ResolvePresentationId(nextIndex);
            return true;
        }

        public static void DrawPreview(
            StageAuthoringPresentationPreviewModel model,
            ref bool foldout)
        {
            foldout = EditorGUILayout.Foldout(foldout, "Presentation Preview", toggleOnLabelClick: true);
            if (!foldout)
            {
                return;
            }

            if (model == null || !model.RequiresPresentation)
            {
                EditorGUILayout.HelpBox("Player presentation is not authored here.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Type", model.PresentationKindLabel);
            EditorGUILayout.LabelField("PresentationId", model.HasPresentationId ? model.PresentationId : "(None)");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Catalog", model.CatalogAsset, typeof(Object), allowSceneObjects: false);
                EditorGUILayout.ObjectField("ViewPrefab", model.ViewPrefab, typeof(Object), allowSceneObjects: false);
                if (model.PresentationKindLabel == "Enemy")
                {
                    EditorGUILayout.ObjectField("VFX Profile", model.VfxProfileAsset, typeof(Object), allowSceneObjects: false);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(model.CatalogAsset == null))
                {
                    if (GUILayout.Button("Ping Catalog"))
                    {
                        EditorGUIUtility.PingObject(model.CatalogAsset);
                    }

                    if (GUILayout.Button("Select Catalog"))
                    {
                        Selection.activeObject = model.CatalogAsset;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(model.ViewPrefab == null))
                {
                    if (GUILayout.Button("Ping Prefab"))
                    {
                        EditorGUIUtility.PingObject(model.ViewPrefab);
                    }

                    if (GUILayout.Button("Select Prefab"))
                    {
                        Selection.activeObject = model.ViewPrefab;
                    }
                }
            }

            EditorGUILayout.HelpBox(model.StatusLabel, model.StatusMessageType);
            if (model.PresentationKindLabel == "Enemy" &&
                !string.IsNullOrEmpty(model.VfxProfileStatusLabel))
            {
                EditorGUILayout.HelpBox(model.VfxProfileStatusLabel, model.VfxProfileStatusMessageType);
            }

            for (var i = 0; i < model.WarningMessages.Length; i++)
            {
                if (model.WarningMessages[i] != model.StatusLabel)
                {
                    EditorGUILayout.HelpBox(model.WarningMessages[i], MessageType.Warning);
                }
            }
        }
    }
}
