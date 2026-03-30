using System.Text;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayShowcaseOverlay : MonoBehaviour
    {
        private const float MaxPanelWidth = 520f;
        private const float ScreenMargin = 24f;

        [SerializeField] private string title;
        [SerializeField] [TextArea(2, 4)] private string summary;
        [SerializeField] private string controlsText;
        [SerializeField] [TextArea(3, 10)] private string highlightsText;
        [SerializeField] private bool visible = true;

        private GUIStyle _bodyStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;

        public string Title => title;

        public string Summary => summary;

        public string ControlsText => controlsText;

        public string HighlightsText => highlightsText;

        public void Configure(GameplayShowcaseOverlayContent content)
        {
            title = content.Title;
            summary = content.Summary;
            controlsText = content.ControlsText;
            highlightsText = BuildHighlightsText(content.Highlights);
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyles();

            var panelWidth = Mathf.Min(MaxPanelWidth, Mathf.Max(320f, Screen.width - (ScreenMargin * 2f)));
            var bodyText = BuildBodyText();
            var contentWidth = panelWidth - 40f;
            var titleHeight = _titleStyle.CalcHeight(new GUIContent(title), contentWidth);
            var bodyHeight = _bodyStyle.CalcHeight(new GUIContent(bodyText), contentWidth);
            var panelHeight = Mathf.Min(
                Mathf.Max(144f, titleHeight + bodyHeight + 36f),
                Mathf.Max(144f, Screen.height - (ScreenMargin * 2f)));

            var panelRect = new Rect(ScreenMargin, ScreenMargin, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, _panelStyle);

            var titleRect = new Rect(panelRect.x + 18f, panelRect.y + 14f, contentWidth, titleHeight);
            GUI.Label(titleRect, title, _titleStyle);

            var bodyRect = new Rect(titleRect.x, titleRect.yMax + 10f, contentWidth, panelRect.height - titleHeight - 26f);
            GUI.Label(bodyRect, bodyText, _bodyStyle);
        }

        private string BuildBodyText()
        {
            var builder = new StringBuilder(256);
            AppendSection(builder, summary);
            AppendSection(builder, string.IsNullOrWhiteSpace(controlsText) ? string.Empty : $"Controls\n{controlsText}");
            AppendSection(builder, string.IsNullOrWhiteSpace(highlightsText) ? string.Empty : $"Highlights\n{highlightsText}");
            return builder.ToString();
        }

        private void EnsureStyles()
        {
            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(16, 16, 16, 16),
                };

                _panelStyle.normal.background = Texture2D.whiteTexture;
                _panelStyle.normal.textColor = new Color(0.12f, 0.15f, 0.2f);
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 19,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.12f, 0.15f, 0.2f) },
                    wordWrap = true,
                };
            }

            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    normal = { textColor = new Color(0.18f, 0.2f, 0.25f) },
                    wordWrap = true,
                };
            }
        }

        private static void AppendSection(StringBuilder builder, string sectionText)
        {
            if (string.IsNullOrWhiteSpace(sectionText))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(sectionText.Trim());
        }

        private static string BuildHighlightsText(System.Collections.Generic.IReadOnlyList<string> highlights)
        {
            if (highlights == null || highlights.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(highlights.Count * 48);
            for (var i = 0; i < highlights.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("- ").Append(highlights[i]);
            }

            return builder.ToString();
        }
    }
}
