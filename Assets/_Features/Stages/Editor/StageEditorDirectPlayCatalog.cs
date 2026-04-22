using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    [Serializable]
    public struct StageEditorDirectPlayCatalogEntry
    {
        public string ScenePath;
        public StageId StageId;
    }

    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Stage Editor Direct Play Catalog",
        fileName = "StageEditorDirectPlayCatalog")]
    public sealed class StageEditorDirectPlayCatalog : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/_Features/Stages/Editor/StageEditorDirectPlayCatalog.asset";

        [SerializeField] private StageEditorDirectPlayCatalogEntry[] entries = Array.Empty<StageEditorDirectPlayCatalogEntry>();

        public IReadOnlyList<StageEditorDirectPlayCatalogEntry> Entries => entries ?? Array.Empty<StageEditorDirectPlayCatalogEntry>();

        public void SetEntries(StageEditorDirectPlayCatalogEntry[] value)
        {
            entries = value ?? Array.Empty<StageEditorDirectPlayCatalogEntry>();
        }

        public bool TryResolveScenePath(string scenePath, out StageId stageId)
        {
            var normalizedScenePath = NormalizeScenePath(scenePath);
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (!entry.StageId.IsValid)
                {
                    continue;
                }

                if (string.Equals(NormalizeScenePath(entry.ScenePath), normalizedScenePath, StringComparison.Ordinal))
                {
                    stageId = entry.StageId;
                    return true;
                }
            }

            stageId = StageId.None;
            return false;
        }

        public bool HasScenePath(string scenePath)
        {
            return TryResolveScenePath(scenePath, out _);
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
