using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    internal static class SettingsLayoutUtility
    {
        public static RectTransform EnsureChildRect(Transform parent, string name)
        {
            var existing = parent != null ? parent.Find(name) as RectTransform : null;
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static VerticalLayoutGroup EnsureVerticalLayout(
            GameObject target,
            RectOffset padding,
            float spacing,
            TextAnchor childAlignment = TextAnchor.UpperLeft,
            bool childControlWidth = true,
            bool childControlHeight = true,
            bool childForceExpandWidth = true,
            bool childForceExpandHeight = false)
        {
            var layout = target.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = target.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = childAlignment;
            layout.childControlWidth = childControlWidth;
            layout.childControlHeight = childControlHeight;
            layout.childForceExpandWidth = childForceExpandWidth;
            layout.childForceExpandHeight = childForceExpandHeight;
            return layout;
        }

        public static HorizontalLayoutGroup EnsureHorizontalLayout(
            GameObject target,
            RectOffset padding,
            float spacing,
            TextAnchor childAlignment = TextAnchor.MiddleLeft,
            bool childControlWidth = true,
            bool childControlHeight = true,
            bool childForceExpandWidth = false,
            bool childForceExpandHeight = false)
        {
            var layout = target.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = target.AddComponent<HorizontalLayoutGroup>();
            }

            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = childAlignment;
            layout.childControlWidth = childControlWidth;
            layout.childControlHeight = childControlHeight;
            layout.childForceExpandWidth = childForceExpandWidth;
            layout.childForceExpandHeight = childForceExpandHeight;
            return layout;
        }

        public static LayoutElement EnsureLayoutElement(
            Component target,
            float minWidth = -1f,
            float minHeight = -1f,
            float preferredWidth = -1f,
            float preferredHeight = -1f,
            float flexibleWidth = -1f,
            float flexibleHeight = -1f,
            bool ignoreLayout = false)
        {
            if (target == null)
            {
                return null;
            }

            return EnsureLayoutElement(
                target.gameObject,
                minWidth,
                minHeight,
                preferredWidth,
                preferredHeight,
                flexibleWidth,
                flexibleHeight,
                ignoreLayout);
        }

        public static LayoutElement EnsureLayoutElement(
            GameObject target,
            float minWidth = -1f,
            float minHeight = -1f,
            float preferredWidth = -1f,
            float preferredHeight = -1f,
            float flexibleWidth = -1f,
            float flexibleHeight = -1f,
            bool ignoreLayout = false)
        {
            if (target == null)
            {
                return null;
            }

            var element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.ignoreLayout = ignoreLayout;
            element.minWidth = minWidth;
            element.minHeight = minHeight;
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        public static void MoveToParent(Component child, RectTransform parent)
        {
            if (child == null || parent == null)
            {
                return;
            }

            MoveToParent(child.transform as RectTransform, parent);
        }

        public static void MoveToParent(RectTransform child, RectTransform parent)
        {
            if (child == null || parent == null || child.parent == parent)
            {
                return;
            }

            child.SetParent(parent, false);
        }

        public static void Stretch(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        public static void FillLayoutChild(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        public static void ConfigureOverlay(RectTransform rectTransform, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }
    }
}
