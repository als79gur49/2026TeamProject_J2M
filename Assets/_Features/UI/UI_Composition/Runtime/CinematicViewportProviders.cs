using System;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public interface IResolvedCinematicViewportProvider
    {
        bool TryGetAspectRatio(out float aspectRatio);

        bool TryGetViewportSize(out Vector2 size);
    }

    public interface ICinematicSelectedAspectProvider
    {
        bool TryGetAspectRatio(out float aspectRatio);
    }

    internal sealed class ResolvedCinematicViewportProvider : IResolvedCinematicViewportProvider
    {
        private const float MinimumValidDimension = 0.01f;

        private readonly RectTransform _cinematicOverlayContainer;
        private readonly RectTransform _rootCanvasRect;
        private readonly Camera _camera;
        private readonly Func<Vector2> _cameraPixelRectSizeProvider;
        private readonly Func<Vector2> _screenSizeProvider;
        private readonly Func<Vector2> _displaySizeProvider;

        internal ResolvedCinematicViewportProvider(
            RectTransform cinematicOverlayContainer,
            RectTransform rootCanvasRect,
            Camera camera,
            Func<Vector2> cameraPixelRectSizeProvider = null,
            Func<Vector2> screenSizeProvider = null,
            Func<Vector2> displaySizeProvider = null)
        {
            _cinematicOverlayContainer = cinematicOverlayContainer;
            _rootCanvasRect = rootCanvasRect;
            _camera = camera;
            _cameraPixelRectSizeProvider = cameraPixelRectSizeProvider;
            _screenSizeProvider = screenSizeProvider ?? ReadScreenSize;
            _displaySizeProvider = displaySizeProvider ?? ReadDisplaySize;
        }

        internal static ResolvedCinematicViewportProvider FromHierarchy(RectTransform cinematicOverlayContainer)
        {
            var canvas = cinematicOverlayContainer != null
                ? cinematicOverlayContainer.GetComponentInParent<Canvas>(includeInactive: true)
                : null;
            var rootCanvas = canvas != null ? canvas.rootCanvas : null;
            var rootCanvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return new ResolvedCinematicViewportProvider(
                cinematicOverlayContainer,
                rootCanvasRect,
                camera != null ? camera : Camera.main);
        }

        public bool TryGetAspectRatio(out float aspectRatio)
        {
            if (TryGetViewportSize(out var size) && TryComputeAspect(size, out aspectRatio))
            {
                return true;
            }

            aspectRatio = 0f;
            return false;
        }

        public bool TryGetViewportSize(out Vector2 size)
        {
            if (TryGetRectSize(_cinematicOverlayContainer, out size) ||
                TryGetRectSize(_rootCanvasRect, out size) ||
                TryGetCameraPixelRectSize(_camera, _cameraPixelRectSizeProvider, out size) ||
                TryGetProvidedSize(_screenSizeProvider, out size) ||
                TryGetProvidedSize(_displaySizeProvider, out size))
            {
                return true;
            }

            size = default;
            return false;
        }

        private static bool TryGetRectSize(RectTransform rectTransform, out Vector2 size)
        {
            if (rectTransform != null && IsValidSize(rectTransform.rect.size))
            {
                size = rectTransform.rect.size;
                return true;
            }

            size = default;
            return false;
        }

        private static bool TryGetCameraPixelRectSize(
            Camera camera,
            Func<Vector2> cameraPixelRectSizeProvider,
            out Vector2 size)
        {
            if (TryGetProvidedSize(cameraPixelRectSizeProvider, out size))
            {
                return true;
            }

            if (camera != null && IsValidSize(camera.pixelRect.size))
            {
                size = camera.pixelRect.size;
                return true;
            }

            size = default;
            return false;
        }

        private static bool TryGetProvidedSize(Func<Vector2> provider, out Vector2 size)
        {
            size = provider != null ? provider() : default;
            return IsValidSize(size);
        }

        private static bool TryComputeAspect(Vector2 size, out float aspectRatio)
        {
            if (IsValidSize(size))
            {
                aspectRatio = size.x / size.y;
                return aspectRatio > 0f;
            }

            aspectRatio = 0f;
            return false;
        }

        private static bool IsValidSize(Vector2 size)
        {
            return size.x > MinimumValidDimension && size.y > MinimumValidDimension;
        }

        private static Vector2 ReadScreenSize()
        {
            return new Vector2(Screen.width, Screen.height);
        }

        private static Vector2 ReadDisplaySize()
        {
            var display = Display.main;
            return display != null
                ? new Vector2(display.renderingWidth, display.renderingHeight)
                : default;
        }
    }
}
