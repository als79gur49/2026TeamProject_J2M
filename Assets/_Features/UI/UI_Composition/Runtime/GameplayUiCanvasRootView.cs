using System;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameplayUiCanvasRootView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _hudLayer;
        [SerializeField] private RectTransform _screenLayer;
        [SerializeField] private RectTransform _popupLayer;
        [SerializeField] private RectTransform _diagnosticsLayer;
        [SerializeField] private HUDRootView _hudView;
        [SerializeField] private ScreenLayerView _screenLayerView;
        [SerializeField] private PopupLayerView _popupLayerView;
        [SerializeField] private UiArchitectureDiagnosticsOverlayView _diagnosticsOverlayView;

        public HUDRootView HudView => _hudView;

        internal RectTransform HudLayer => _hudLayer;

        internal RectTransform ScreenLayer => _screenLayer;

        internal RectTransform PopupLayer => _popupLayer;

        internal RectTransform DiagnosticsLayer => _diagnosticsLayer;

        public ScreenLayerView ScreenLayerView => _screenLayerView;

        public PopupLayerView PopupLayerView => _popupLayerView;

        internal UiArchitectureDiagnosticsOverlayView DiagnosticsOverlayView => _diagnosticsOverlayView;

        public void EnsureHierarchy()
        {
            EnsureCanvas();
            EnsureEventSystem();
            ResolveShellReferences();
            GameplayUiRootShellValidator.Validate(this);

            if (_screenLayerView == null)
            {
                _screenLayerView = CreateScreenLayerView(_screenLayer);
            }

            if (_popupLayerView == null)
            {
                _popupLayerView = CreatePopupLayerView(_popupLayer);
            }

            if (_diagnosticsOverlayView == null)
            {
                _diagnosticsOverlayView = CreateDiagnosticsOverlayView(_diagnosticsLayer);
            }
        }

        internal void AttachHudView(HUDRootView hudView)
        {
            if (hudView == null)
            {
                throw new ArgumentNullException(nameof(hudView));
            }

            if (_hudLayer == null)
            {
                throw new InvalidOperationException("Canonical HUD host anchor is unavailable.");
            }

            if (hudView.transform.parent != _hudLayer)
            {
                throw new InvalidOperationException("Canonical HUD prefab must mount directly beneath HudLayer.");
            }

            _hudView = hudView;
        }

        private void EnsureCanvas()
        {
            _canvas = UiOverlayCanvasConfigurator.ConfigureOverlayCanvas(gameObject);
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            var inputSystemUiModuleType = UiEventSystemNavigationActionUtility.RequireInputSystemUiModuleType();

            if (eventSystem.GetComponent(inputSystemUiModuleType) == null)
            {
                eventSystem.gameObject.AddComponent(inputSystemUiModuleType);
            }

            UiEventSystemNavigationActionUtility.DisableNavigationActions(eventSystem, inputSystemUiModuleType);

            var legacyModules = eventSystem.GetComponents<StandaloneInputModule>();
            foreach (var legacyModule in legacyModules)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(legacyModule);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(legacyModule);
                }
            }
        }

        private void ResolveShellReferences()
        {
            _hudLayer = ResolveLayer(_hudLayer, GameplayUiRootShellValidator.HudLayerName);
            _screenLayer = ResolveLayer(_screenLayer, GameplayUiRootShellValidator.ScreenLayerName);
            _popupLayer = ResolveLayer(_popupLayer, GameplayUiRootShellValidator.PopupLayerName);
            _diagnosticsLayer = ResolveLayer(_diagnosticsLayer, GameplayUiRootShellValidator.DiagnosticsLayerName);

            if (_hudView == null && _hudLayer != null)
            {
                _hudView = _hudLayer.GetComponentInChildren<HUDRootView>(true);
            }

            if (_screenLayerView == null && _screenLayer != null)
            {
                _screenLayerView = _screenLayer.GetComponentInChildren<ScreenLayerView>(true);
            }

            if (_popupLayerView == null && _popupLayer != null)
            {
                _popupLayerView = _popupLayer.GetComponentInChildren<PopupLayerView>(true);
            }

            if (_diagnosticsOverlayView == null && _diagnosticsLayer != null)
            {
                _diagnosticsOverlayView = _diagnosticsLayer.GetComponentInChildren<UiArchitectureDiagnosticsOverlayView>(true);
            }
        }

        private RectTransform ResolveLayer(RectTransform current, string expectedName)
        {
            if (current != null)
            {
                return current;
            }

            var child = transform.Find(expectedName);
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private static ScreenLayerView CreateScreenLayerView(Transform parent)
        {
            var root = UiCanvasElementFactory.CreateStretchRect("ScreenLayerRoot", parent);
            var view = root.gameObject.AddComponent<ScreenLayerView>();
            var contentRoot = UiCanvasElementFactory.CreateStretchRect("ScreenContentRoot", root);
            view.Configure(root.gameObject, contentRoot);
            return view;
        }

        private static PopupLayerView CreatePopupLayerView(Transform parent)
        {
            var root = UiCanvasElementFactory.CreateStretchRect("PopupLayerRoot", parent);
            var view = root.gameObject.AddComponent<PopupLayerView>();

            var backdropObject = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            backdropObject.transform.SetParent(root, false);
            var backdropRect = backdropObject.GetComponent<RectTransform>();
            UiCanvasElementFactory.Stretch(backdropRect);
            var backdropImage = backdropObject.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0f);
            var backdropButton = backdropObject.GetComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            var backdropCanvasGroup = backdropObject.GetComponent<CanvasGroup>();

            var contentRoot = UiCanvasElementFactory.CreateStretchRect("PopupContentRoot", root);
            view.Configure(root.gameObject, backdropCanvasGroup, backdropImage, backdropButton, contentRoot);
            view.SetState(false, false, false, PopupBackdropMode.None);
            return view;
        }

        private static UiArchitectureDiagnosticsOverlayView CreateDiagnosticsOverlayView(Transform parent)
        {
            var panel = UiCanvasElementFactory.CreatePanel(
                "UiDiagnosticsOverlay",
                parent,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(392f, 336f),
                new Vector2(-16f, -16f));
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.07f, 0.10f, 0.14f, 0.94f);
            }

            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var view = panel.gameObject.AddComponent<UiArchitectureDiagnosticsOverlayView>();
            var title = UiCanvasElementFactory.CreateLabel(
                "Title",
                panel,
                new Vector2(12f, -12f),
                new Vector2(364f, 22f),
                TextAnchor.MiddleLeft,
                16);
            var summary = UiCanvasElementFactory.CreateLabel(
                "Summary",
                panel,
                new Vector2(12f, -40f),
                new Vector2(364f, 170f),
                TextAnchor.UpperLeft,
                13);
            var detailPanel = UiCanvasElementFactory.CreatePanel(
                "DetailPanel",
                panel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(364f, 104f),
                new Vector2(12f, -218f));
            var detailImage = detailPanel.GetComponent<Image>();
            if (detailImage != null)
            {
                detailImage.color = new Color(0.12f, 0.16f, 0.22f, 0.96f);
            }

            var detail = UiCanvasElementFactory.CreateLabel(
                "Details",
                detailPanel,
                new Vector2(8f, -8f),
                new Vector2(348f, 88f),
                TextAnchor.UpperLeft,
                12);
            view.Configure(panel.gameObject, canvasGroup, title, summary, detailPanel.gameObject, detail);
            view.SetSupported(false);
            return view;
        }
    }
}
