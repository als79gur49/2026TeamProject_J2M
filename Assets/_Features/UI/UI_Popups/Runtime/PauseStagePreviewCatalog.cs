using System;
using UnityEngine;

namespace Game.Feature.UI.Popups
{
    [Serializable]
    public sealed class PauseStagePreviewEntry
    {
        [SerializeField] private string _stageKey = string.Empty;
        [SerializeField] private Sprite _previewSprite;

        public string StageKey => _stageKey ?? string.Empty;

        public Sprite PreviewSprite => _previewSprite;
    }

    [CreateAssetMenu(
        fileName = "PauseStagePreviewCatalog",
        menuName = "Game/UI/Pause Stage Preview Catalog")]
    public sealed class PauseStagePreviewCatalog : ScriptableObject
    {
        [SerializeField] private Sprite _placeholderSprite;
        [SerializeField] private PauseStagePreviewEntry[] _entries = Array.Empty<PauseStagePreviewEntry>();

        public Sprite PlaceholderSprite => _placeholderSprite;

        public PauseStagePreviewEntry[] Entries => _entries ?? Array.Empty<PauseStagePreviewEntry>();

        public Sprite ResolveOrPlaceholder(string stageKey)
        {
            if (!string.IsNullOrWhiteSpace(stageKey))
            {
                var entries = Entries;
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry != null &&
                        string.Equals(entry.StageKey, stageKey, StringComparison.Ordinal) &&
                        entry.PreviewSprite != null)
                    {
                        return entry.PreviewSprite;
                    }
                }
            }

            return _placeholderSprite;
        }
    }
}
