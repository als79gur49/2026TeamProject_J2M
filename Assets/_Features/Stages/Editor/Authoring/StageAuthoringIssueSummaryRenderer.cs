using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringIssueSummaryRenderer
    {
        public static void Draw(
            StageAuthoringGeneratedBindingPreviewModel model,
            ref bool foldout)
        {
            foldout = EditorGUILayout.Foldout(foldout, "Validation Issues", toggleOnLabelClick: true);
            if (!foldout)
            {
                return;
            }

            if (model == null || model.RelatedIssueMessages.Length == 0)
            {
                EditorGUILayout.HelpBox("No selected placement issues in the last report.", MessageType.Info);
                return;
            }

            for (var i = 0; i < model.RelatedIssueMessages.Length; i++)
            {
                EditorGUILayout.HelpBox(model.RelatedIssueMessages[i], MessageType.Warning);
            }
        }
    }
}
