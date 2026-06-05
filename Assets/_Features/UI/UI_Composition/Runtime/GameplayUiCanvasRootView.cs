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
        [SerializeField] private HUDRootView _hudView;
        [SerializeField] private ScreenLayerView _screenLayerView;
        [SerializeField] private PopupLayerView _popupLayerView;

        public HUDRootView HudView => _hudView;

        internal RectTransform HudLayer => _hudLayer;

        internal RectTransform ScreenLayer => _screenLayer;

        internal RectTransform PopupLayer => _popupLayer;

        public ScreenLayerView ScreenLayerView => _screenLayerView;

        public PopupLayerView PopupLayerView => _popupLayerView;

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

    }
}
