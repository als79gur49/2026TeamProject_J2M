using System;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal sealed class MainMenuSettingsOverlayController : IDisposable, IUiNavigationTargetProvider
    {
        private const string OverlayLayerName = "MainMenuSettingsOverlayLayer";
        private const string BlockerName = "SettingsBlocker";
        private const string ContentRootName = "SettingsContentRoot";

        private readonly Transform parentRoot;
        private readonly Transform popupLayerTransform;
        private readonly Func<RectTransform, MainMenuSettingsRuntime> runtimeFactory;
        private RectTransform overlayLayer;
        private RectTransform contentRoot;
        private CanvasGroup blockerCanvasGroup;
        private Image blockerImage;
        private MainMenuSettingsRuntime runtime;
        private bool isDisposed;

        public MainMenuSettingsOverlayController(
            Transform parentRoot,
            Transform popupLayerTransform,
            Func<RectTransform, MainMenuSettingsRuntime> runtimeFactory)
        {
            this.parentRoot = parentRoot ?? throw new ArgumentNullException(nameof(parentRoot));
            this.popupLayerTransform = popupLayerTransform;
            this.runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
        }

        public bool IsOpen => runtime != null;

        public event Action Opened;

        public event Action Closed;

        public event Action<SettingsSectionId> SectionChanged;

        public RectTransform ContentRoot
        {
            get
            {
                EnsureOverlayLayer();
                return contentRoot;
            }
        }

        public RectTransform OverlayLayer
        {
            get
            {
                EnsureOverlayLayer();
                return overlayLayer;
            }
        }

        public void Open()
        {
            ThrowIfDisposed();
            EnsureOverlayLayer();
            Focus();

            if (runtime != null)
            {
                SetOverlayVisible(true);
                return;
            }

            var createdRuntime = runtimeFactory(contentRoot);
            runtime = createdRuntime ?? throw new InvalidOperationException("MainMenu settings runtime factory returned null.");
            runtime.CloseRequested += HandleRuntimeCloseRequested;
            runtime.SectionChanged += HandleRuntimeSectionChanged;

            try
            {
                SetOverlayVisible(true);
                runtime.Open();
                Opened?.Invoke();
            }
            catch
            {
                runtime.CloseRequested -= HandleRuntimeCloseRequested;
                runtime.SectionChanged -= HandleRuntimeSectionChanged;
                runtime.Dispose();
                runtime = null;
                SetOverlayVisible(false);
                throw;
            }
        }

        public void Close()
        {
            if (runtime == null)
            {
                SetOverlayVisible(false);
                return;
            }

            var closingRuntime = runtime;
            runtime = null;
            closingRuntime.CloseRequested -= HandleRuntimeCloseRequested;
            closingRuntime.SectionChanged -= HandleRuntimeSectionChanged;
            closingRuntime.Dispose();
            SetOverlayVisible(false);
            Closed?.Invoke();
        }

        public bool TryHandleBackRequested()
        {
            return runtime != null && runtime.TryHandleBackRequested();
        }

        public bool TryGetNavigationTarget(out IUiNavigationTarget target)
        {
            target = runtime != null ? runtime.View as IUiNavigationTarget : null;
            return target != null;
        }

        public void Focus()
        {
            if (overlayLayer != null)
            {
                overlayLayer.SetAsLastSibling();
            }

            if (popupLayerTransform != null)
            {
                popupLayerTransform.SetAsLastSibling();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Close();
            DestroyObject(overlayLayer != null ? overlayLayer.gameObject : null);
            overlayLayer = null;
            contentRoot = null;
            blockerCanvasGroup = null;
            blockerImage = null;
        }

        private void HandleRuntimeCloseRequested()
        {
            Close();
        }

        private void HandleRuntimeSectionChanged(SettingsSectionId sectionId)
        {
            SectionChanged?.Invoke(sectionId);
        }

        private void EnsureOverlayLayer()
        {
            if (overlayLayer != null)
            {
                return;
            }

            var overlayObject = new GameObject(OverlayLayerName, typeof(RectTransform));
            overlayObject.transform.SetParent(parentRoot, false);
            overlayLayer = (RectTransform)overlayObject.transform;
            StretchToParent(overlayLayer);

            var blockerObject = new GameObject(BlockerName, typeof(RectTransform));
            blockerObject.transform.SetParent(overlayLayer, false);
            var blockerRect = (RectTransform)blockerObject.transform;
            StretchToParent(blockerRect);
            blockerCanvasGroup = blockerObject.AddComponent<CanvasGroup>();
            blockerImage = blockerObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.48f);

            var contentObject = new GameObject(ContentRootName, typeof(RectTransform));
            contentObject.transform.SetParent(overlayLayer, false);
            contentRoot = (RectTransform)contentObject.transform;
            StretchToParent(contentRoot);

            SetOverlayVisible(false);
        }

        private void SetOverlayVisible(bool isVisible)
        {
            if (overlayLayer != null)
            {
                overlayLayer.gameObject.SetActive(isVisible);
            }

            if (blockerCanvasGroup != null)
            {
                blockerCanvasGroup.alpha = isVisible ? 1f : 0f;
                blockerCanvasGroup.interactable = isVisible;
                blockerCanvasGroup.blocksRaycasts = isVisible;
            }

            if (blockerImage != null)
            {
                blockerImage.raycastTarget = isVisible;
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(MainMenuSettingsOverlayController));
            }
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void DestroyObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(unityObject);
                return;
            }

            UnityEngine.Object.DestroyImmediate(unityObject);
        }
    }
}
