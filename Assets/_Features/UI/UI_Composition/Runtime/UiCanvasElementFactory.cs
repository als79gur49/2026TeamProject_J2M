using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal static class UiCanvasElementFactory
    {
        public static RectTransform CreateLayer(string name, Transform parent)
        {
            var layerObject = new GameObject(name, typeof(RectTransform));
            layerObject.transform.SetParent(parent, false);
            var rectTransform = layerObject.GetComponent<RectTransform>();
            Stretch(rectTransform);
            return rectTransform;
        }

        public static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var objectInstance = new GameObject(name, typeof(RectTransform));
            objectInstance.transform.SetParent(parent, false);
            var rectTransform = objectInstance.GetComponent<RectTransform>();
            Stretch(rectTransform);
            return rectTransform;
        }

        public static RectTransform CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            var rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = panelObject.GetComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.16f, 0.92f);
            return rectTransform;
        }

        public static Text CreateLabel(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            int fontSize = 14)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var text = labelObject.GetComponent<Text>();
            text.font = LoadDefaultFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.20f, 0.25f, 0.34f, 1f);

            var button = buttonObject.GetComponent<Button>();

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect);

            var text = labelObject.GetComponent<Text>();
            text.font = LoadDefaultFont();
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            return button;
        }

        public static Text GetButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>() : null;
        }

        public static Font LoadDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                   Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
