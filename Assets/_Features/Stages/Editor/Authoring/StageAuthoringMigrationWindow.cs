using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public sealed class StageAuthoringMigrationWindow : EditorWindow
    {
        private StageContentEntry entry;

        [MenuItem("Tools/Stages/Authoring/Migrate Selected Stage Content Entry")]
        private static void MigrateSelected()
        {
            var selectedEntry = Selection.activeObject as StageContentEntry;
            if (selectedEntry == null)
            {
                Debug.LogError("Select a StageContentEntry asset before running authoring migration.");
                return;
            }

            var authoring = StageAuthoringMigrationTool.CreateForEntry(selectedEntry);
            selectedEntry.AssignAuthoringDefinition(authoring);
            EditorUtility.SetDirty(selectedEntry);
            AssetDatabase.SaveAssets();
            Selection.activeObject = authoring;
        }

        [MenuItem("Tools/Stages/Authoring/Migration Window")]
        private static void Open()
        {
            GetWindow<StageAuthoringMigrationWindow>("Stage Authoring Migration").Show();
        }

        private void OnGUI()
        {
            entry = (StageContentEntry)EditorGUILayout.ObjectField("Entry", entry, typeof(StageContentEntry), false);
            using (new EditorGUI.DisabledScope(entry == null))
            {
                if (GUILayout.Button("Create / Refresh Authoring Asset"))
                {
                    var authoring = StageAuthoringMigrationTool.CreateForEntry(entry);
                    entry.AssignAuthoringDefinition(authoring);
                    EditorUtility.SetDirty(entry);
                    AssetDatabase.SaveAssets();
                    Selection.activeObject = authoring;
                }
            }
        }
    }
}
