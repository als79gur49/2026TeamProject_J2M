using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    [Serializable]
    public struct StageEditorDirectPlayStageEntry
    {
        public StageId StageId;
    }

    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Stage Editor Direct Play Catalog",
        fileName = "StageEditorDirectPlayCatalog")]
    public sealed class StageEditorDirectPlayCatalog : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/_Features/Stages/Editor/StageEditorDirectPlayCatalog.asset";

        [SerializeField] private string canonicalShellScenePath = "Assets/Scenes/UIAudioScene.unity";
        [SerializeField] private StageEditorDirectPlayStageEntry[] supportedStages = Array.Empty<StageEditorDirectPlayStageEntry>();

        public string CanonicalShellScenePath => NormalizeScenePath(canonicalShellScenePath);

        public IReadOnlyList<StageEditorDirectPlayStageEntry> SupportedStages =>
            supportedStages ?? Array.Empty<StageEditorDirectPlayStageEntry>();

        public void Configure(string shellScenePath, StageEditorDirectPlayStageEntry[] stages)
        {
            canonicalShellScenePath = NormalizeScenePath(shellScenePath);
            supportedStages = stages ?? Array.Empty<StageEditorDirectPlayStageEntry>();
        }

        public bool IsCanonicalShellScenePath(string scenePath)
        {
            return string.Equals(
                NormalizeScenePath(scenePath),
                CanonicalShellScenePath,
                StringComparison.Ordinal);
        }

        public bool HasSupportedStageId(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                return false;
            }

            for (var i = 0; i < SupportedStages.Count; i++)
            {
                if (SupportedStages[i].StageId.Equals(stageId))
                {
                    return true;
                }
            }

            return false;
        }

        public static StageEditorDirectPlayCatalog LoadDefault()
        {
            return AssetDatabase.LoadAssetAtPath<StageEditorDirectPlayCatalog>(DefaultAssetPath);
        }

        public static string NormalizeScenePath(string scenePath)
        {
            return (scenePath ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
