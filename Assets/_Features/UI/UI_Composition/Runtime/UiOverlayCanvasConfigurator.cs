using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal static class UiOverlayCanvasConfigurator
    {
        internal static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        internal const float MatchWidthOrHeight = 1.0f;

        internal static Canvas ConfigureOverlayCanvas(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = root.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = root.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;

            if (root.GetComponent<GraphicRaycaster>() == null)
            {
                root.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }
    }
}
