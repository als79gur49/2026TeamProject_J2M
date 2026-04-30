using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGeneratedPreviewSectionRenderer
    {
        public static void Draw(
            StageAuthoringGeneratedBindingPreviewModel model,
            ref bool foldout)
        {
            foldout = EditorGUILayout.Foldout(foldout, "Generated Preview", toggleOnLabelClick: true);
            if (!foldout)
            {
                return;
            }

            if (model == null)
            {
                return;
            }

            var entityLabel = model.HasMappedEntityId
                ? model.IsPreviewEntityId
                    ? $"Preview EntityId: {model.EntityId} (not persisted)"
                    : $"EntityId: {model.EntityId}"
                : "EntityId: (unavailable)";
            EditorGUILayout.LabelField("Generated Gameplay", entityLabel);
            EditorGUILayout.LabelField("Kind", model.Kind.ToString());
            EditorGUILayout.LabelField(
                "Cell",
                $"{model.Cell.face}({model.Cell.x},{model.Cell.y})");
            EditorGUILayout.LabelField(
                "Facing",
                $"{StageAuthoringFacingDisplayUtility.ToFacingLabel(model.Facing)} {StageAuthoringFacingDisplayUtility.ToFacingArrow(model.Facing)}");

            if (!model.RequiresBinding)
            {
                EditorGUILayout.HelpBox("No presentation binding required.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Binding Kind", model.BindingKindLabel);
            EditorGUILayout.LabelField(
                "Binding",
                model.HasMappedEntityId
                    ? $"{model.EntityId} -> {(string.IsNullOrEmpty(model.PresentationId) ? "(None)" : model.PresentationId)}"
                    : "(unavailable)");
            EditorGUILayout.HelpBox(model.StatusLabel, model.StatusMessageType);
        }
    }
}
