using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayShowcaseOverlayContent
    {
        public GameplayShowcaseOverlayContent(
            string title,
            string summary,
            string controlsText,
            IEnumerable<string> highlights)
        {
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            ControlsText = controlsText ?? string.Empty;
            Highlights = CopyHighlights(highlights);
        }

        public string Title { get; }

        public string Summary { get; }

        public string ControlsText { get; }

        public IReadOnlyList<string> Highlights { get; }

        private static IReadOnlyList<string> CopyHighlights(IEnumerable<string> highlights)
        {
            if (highlights == null)
            {
                return Array.Empty<string>();
            }

            var copiedHighlights = new List<string>();
            foreach (var highlight in highlights)
            {
                if (!string.IsNullOrWhiteSpace(highlight))
                {
                    copiedHighlights.Add(highlight.Trim());
                }
            }

            return copiedHighlights.Count > 0
                ? copiedHighlights.ToArray()
                : Array.Empty<string>();
        }
    }
}
