using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public sealed class StageEditorDirectPlayWindow : EditorWindow
    {
        private readonly List<StageId> _stageIds = new();
        private readonly List<string> _stageLabels = new();
        private EditorDirectPlayMode _mode = EditorDirectPlayMode.NonCampaign;
        private int _productionSlot = 1;
        private int _remainingChances = SaveSlotStore.DefaultRemainingChances;
        private int _selectedStageIndex;

        [MenuItem("Tools/Stages/Direct Play/Launch Stage...")]
        public static void Open()
        {
            GetWindow<StageEditorDirectPlayWindow>("Stage Direct Play");
        }

        private void OnEnable()
        {
            RefreshStages();
        }

        private void OnGUI()
        {
            if (_stageIds.Count == 0)
            {
                EditorGUILayout.HelpBox("No stage catalog entries are available.", MessageType.Warning);
                if (GUILayout.Button("Refresh"))
                {
                    RefreshStages();
                }

                return;
            }

            _selectedStageIndex = EditorGUILayout.Popup("StageId", _selectedStageIndex, _stageLabels.ToArray());
            _mode = (EditorDirectPlayMode)EditorGUILayout.EnumPopup("Editor Direct-Play Context", _mode);
            EditorGUILayout.HelpBox(
                $"{StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.EditorDirectPlaySupport)}. Editor-only launch context injection before editor play; not a production load path.",
                MessageType.Info);
            _remainingChances = EditorGUILayout.IntPopup(
                "Remaining Chances",
                Mathf.Clamp(_remainingChances, 1, SaveSlotStore.DefaultRemainingChances),
                new[] { "3", "2", "1" },
                new[] { 3, 2, 1 });

            if (_mode == EditorDirectPlayMode.CampaignProductionSlot)
            {
                _productionSlot = EditorGUILayout.IntPopup(
                    "Production Slot",
                    _productionSlot,
                    new[] { "1", "2", "3" },
                    new[] { 1, 2, 3 });
                EditorGUILayout.HelpBox("Production slot mode overwrites real save data after confirmation.", MessageType.Warning);

                if (GUILayout.Button("Export Standalone Save Seed..."))
                {
                    StageEditorDirectPlayLauncher.ExportStandaloneCampaignSaveSeedWithSavePanel(
                        _stageIds[_selectedStageIndex],
                        _remainingChances,
                        _productionSlot);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Launch"))
                {
                    StageEditorDirectPlayLauncher.LaunchStage(
                        _stageIds[_selectedStageIndex],
                        _mode,
                        _remainingChances,
                        _productionSlot);
                }

                if (GUILayout.Button("Clear Temp Direct Play Save"))
                {
                    StageEditorDirectPlayLauncher.ClearTempDirectPlaySave();
                }
            }

            if (GUILayout.Button("Refresh Stages"))
            {
                RefreshStages();
            }
        }

        private void RefreshStages()
        {
            _stageIds.Clear();
            _stageLabels.Clear();
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            if (provider == null)
            {
                return;
            }

            var entries = provider.LoadEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.StageId.IsValid)
                {
                    continue;
                }

                _stageIds.Add(entry.StageId);
                _stageLabels.Add(entry.StageId.Value);
            }

            _stageLabels.Sort(StringComparer.Ordinal);
            _stageIds.Sort((left, right) => string.Compare(left.Value, right.Value, StringComparison.Ordinal));
            _selectedStageIndex = Mathf.Clamp(_selectedStageIndex, 0, Math.Max(0, _stageIds.Count - 1));
        }
    }
}
