using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal sealed class SceneTransitionOverlayShellView : MonoBehaviour, ISceneTransitionOverlayShellView
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private GameObject _blocker;
        [SerializeField] private Image _blockerImage;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private CanvasGroup _visualGroup;
        [SerializeField] private Transform _contentMount;

        private readonly Dictionary<SceneTransitionOverlayContentView, SceneTransitionOverlayContentView> _instancesByPrefab = new();
        private ISceneTransitionOverlayContentView _activeContent;
        private bool _opaqueTakeoverRequested;
        private Color _opaqueCoverColor = Color.clear;
        private int _opaqueRequestFrame = -1;
        private int _opaqueRenderCallbackFrame = -1;
        private bool _styledCoverFadeActive;
        private float _styledCoverFadeDuration;
        private float _styledCoverFadeElapsed;
        private TerminalIrisEasing _styledCoverFadeEasing;

        public bool IsOpaqueHandoffReady { get; private set; }

        public bool IsStyledCoverFadeComplete { get; private set; }

        internal bool HasAcknowledgedOpaqueFrame { get; private set; }

        public bool HasRenderedOpaqueFrame =>
            _opaqueTakeoverRequested &&
            _opaqueRequestFrame >= 0 &&
            _opaqueRenderCallbackFrame > _opaqueRequestFrame;

        internal float PersistentCoverOpacityForTests =>
            _blockerImage != null ? _blockerImage.color.a : 0f;

        internal Color PersistentCoverColorForTests =>
            _blockerImage != null ? _blockerImage.color : Color.clear;

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        }

        public void ShowBlockerOnly(bool blockInput)
        {
            gameObject.SetActive(true);
            SetRootGroupVisible(true, blockInput);
            SetBlockerState(blockInput, Color.clear);
            HideVisual();
        }

        public void RequestOpaqueTakeover(Color color)
        {
            color.a = 1f;
            _opaqueCoverColor = color;
            _styledCoverFadeActive = false;
            IsStyledCoverFadeComplete = true;
            gameObject.SetActive(true);
            _opaqueTakeoverRequested = true;
            HasAcknowledgedOpaqueFrame = false;
            IsOpaqueHandoffReady = false;
            _opaqueRequestFrame = Time.frameCount;
            _opaqueRenderCallbackFrame = -1;
            SetRootGroupVisible(true, true);
            SetBlockerState(blockInput: true, opaqueColor: color);
            Canvas.ForceUpdateCanvases();
        }

        public void RequestStyledCoverTakeover(
            Color color,
            float fadeDuration,
            TerminalIrisEasing easing)
        {
            if (float.IsNaN(fadeDuration) ||
                float.IsInfinity(fadeDuration) ||
                fadeDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fadeDuration),
                    fadeDuration,
                    "Styled persistent cover fade duration must be finite and greater than zero.");
            }

            if (!Enum.IsDefined(typeof(TerminalIrisEasing), easing))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(easing),
                    easing,
                    "Styled persistent cover easing must be a defined value.");
            }

            color.a = 1f;
            _opaqueCoverColor = color;
            color.a = 0f;
            gameObject.SetActive(true);
            _opaqueTakeoverRequested = true;
            HasAcknowledgedOpaqueFrame = false;
            _styledCoverFadeActive = true;
            _styledCoverFadeDuration = fadeDuration;
            _styledCoverFadeElapsed = 0f;
            _styledCoverFadeEasing = easing;
            IsStyledCoverFadeComplete = false;
            IsOpaqueHandoffReady = false;
            _opaqueRequestFrame = Time.frameCount;
            _opaqueRenderCallbackFrame = -1;
            SetRootGroupVisible(true, true);
            SetBlockerState(blockInput: true, opaqueColor: color);
            Canvas.ForceUpdateCanvases();
        }

        public void TickStyledCoverTakeover(float unscaledDeltaTime)
        {
            if (!_styledCoverFadeActive || IsStyledCoverFadeComplete)
            {
                return;
            }

            if (float.IsNaN(unscaledDeltaTime) ||
                float.IsInfinity(unscaledDeltaTime) ||
                unscaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            _styledCoverFadeElapsed += unscaledDeltaTime;
            var progress = Mathf.Clamp01(
                _styledCoverFadeElapsed / _styledCoverFadeDuration);
            var alpha = TerminalIrisEasingUtility.Evaluate(
                _styledCoverFadeEasing,
                progress);
            var color = _blockerImage.color;
            color.a = Mathf.Max(color.a, alpha);
            _blockerImage.color = color;
            if (progress >= 1f)
            {
                _styledCoverFadeActive = false;
                IsStyledCoverFadeComplete = true;
                color.a = 1f;
                _blockerImage.color = color;
            }
        }

        public void AcknowledgeOpaqueHandoffReady()
        {
            if (!IsTransitionCanvasRenderParticipant() ||
                !HasRenderedOpaqueFrame)
            {
                throw new InvalidOperationException(
                    "Persistent opaque handoff cannot be acknowledged before the canonical transition Canvas " +
                    "and its active alpha=1 blocker participate in a post-request render.");
            }

            var rect = _blockerImage.rectTransform;
            if (rect.anchorMin != Vector2.zero ||
                rect.anchorMax != Vector2.one ||
                rect.offsetMin != Vector2.zero ||
                rect.offsetMax != Vector2.zero)
            {
                throw new InvalidOperationException(
                    "Persistent opaque handoff blocker must cover the full canvas.");
            }

            Canvas.ForceUpdateCanvases();
            IsOpaqueHandoffReady = true;
            HasAcknowledgedOpaqueFrame = true;
        }

        private void HandleWillRenderCanvases()
        {
            if (!IsTransitionCanvasRenderParticipant())
            {
                return;
            }

            _opaqueRenderCallbackFrame = Time.frameCount;
        }

        internal bool IsTransitionCanvasRenderParticipant()
        {
            if (!_opaqueTakeoverRequested ||
                _canvas == null ||
                !_canvas.isActiveAndEnabled ||
                !_canvas.gameObject.activeInHierarchy ||
                _canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                _canvas.targetDisplay != 0 ||
                _canvas.sortingOrder < 5000 ||
                _rootGroup == null ||
                _rootGroup.alpha < 0.999f ||
                _blocker == null ||
                !_blocker.activeInHierarchy ||
                _blockerImage == null ||
                _blockerImage.color.a < 0.999f)
            {
                return false;
            }

            var renderer = _blockerImage.canvasRenderer;
            return renderer != null && !renderer.cull;
        }

        public void SetPersistentCoverOpacity(float opacity)
        {
            if (!_opaqueTakeoverRequested || _blockerImage == null)
            {
                return;
            }

            var color = _blockerImage.color;
            color.a = Mathf.Clamp01(opacity);
            _blockerImage.color = color;
        }

        public ISceneTransitionOverlayContentView MountContent(SceneTransitionOverlayContentView contentPrefab)
        {
            if (_activeContent != null)
            {
                _activeContent.Hide();
            }

            if (contentPrefab == null)
            {
                throw new InvalidOperationException(
                    "Scene transition overlay shell requires a catalog-authored content prefab.");
            }

            if (!_instancesByPrefab.TryGetValue(contentPrefab, out var instance) || instance == null)
            {
                instance = Instantiate(contentPrefab, _contentMount != null ? _contentMount : transform, false);
                instance.name = contentPrefab.name;
                instance.Hide();
                _instancesByPrefab[contentPrefab] = instance;
            }

            _activeContent = instance;
            return _activeContent;
        }

        public void ShowContent(SceneTransitionOverlayModel model, ISceneTransitionOverlayContentView content)
        {
            gameObject.SetActive(true);
            SetRootGroupVisible(true, model.BlockInput);
            SetBlockerState(
                model.BlockInput,
                _opaqueTakeoverRequested
                    ? _opaqueCoverColor
                    : Color.clear);
            SetActive(_visualRoot, model.OverlayKind != Game.Feature.Stages.TransitionOverlayKind.None);
            if (_visualGroup != null)
            {
                _visualGroup.alpha = 1f;
                _visualGroup.blocksRaycasts = false;
                _visualGroup.interactable = false;
            }

            _activeContent = content ?? throw new InvalidOperationException(
                "Scene transition overlay shell requires mounted catalog-authored content.");
            _activeContent.ResetView();
            _activeContent.Bind(model);
            _activeContent.Show();
        }

        public void SetProgress(float progress01)
        {
            _activeContent?.SetProgress(progress01);
        }

        public void HideVisual()
        {
            _activeContent?.Hide();
            SetActive(_visualRoot, false);
            if (_visualGroup != null)
            {
                _visualGroup.alpha = 0f;
                _visualGroup.blocksRaycasts = false;
                _visualGroup.interactable = false;
            }
        }

        public void HideAll()
        {
            HideVisual();
            SetBlockerState(false, Color.clear);
            SetRootGroupVisible(false, false);
            _opaqueTakeoverRequested = false;
            _opaqueCoverColor = Color.clear;
            _opaqueRequestFrame = -1;
            _opaqueRenderCallbackFrame = -1;
            _styledCoverFadeActive = false;
            IsStyledCoverFadeComplete = false;
            IsOpaqueHandoffReady = false;
            gameObject.SetActive(false);
        }

        internal IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>();
            AddMissing(issues, _canvas, nameof(_canvas));
            AddMissing(issues, _rootGroup, nameof(_rootGroup));
            AddMissing(issues, _blocker, nameof(_blocker));
            AddMissing(issues, _blockerImage, nameof(_blockerImage));
            AddMissing(issues, _visualRoot, nameof(_visualRoot));
            AddMissing(issues, _visualGroup, nameof(_visualGroup));
            AddMissing(issues, _contentMount, nameof(_contentMount));

            if (HasChildComponentNamed(transform, nameof(EventSystem)))
            {
                issues.Add("SceneTransitionOverlayShellView prefab must not contain an EventSystem child.");
            }

            if (HasChildComponentNamed(transform, nameof(AudioSource)))
            {
                issues.Add("SceneTransitionOverlayShellView prefab must not contain an AudioSource child.");
            }

            if (HasChildComponentNamed(transform, "AudioRuntimeRoot"))
            {
                issues.Add("SceneTransitionOverlayShellView prefab must not contain an AudioRuntimeRoot child.");
            }

            if (HasChildComponentNamed(transform, "GlobalAudioFlowRoot"))
            {
                issues.Add("SceneTransitionOverlayShellView prefab must not contain a GlobalAudioFlowRoot child.");
            }

            return issues;
        }

        private void OnValidate()
        {
            if (_canvas == null && _rootGroup == null && _visualRoot == null && _blocker == null)
            {
                return;
            }

            foreach (var issue in CollectValidationIssues())
            {
                Debug.LogWarning(issue, this);
            }
        }

        private void SetRootGroupVisible(bool visible, bool blockRaycasts)
        {
            if (_rootGroup == null)
            {
                return;
            }

            _rootGroup.alpha = visible ? 1f : 0f;
            _rootGroup.blocksRaycasts = blockRaycasts;
            _rootGroup.interactable = blockRaycasts;
        }

        private void SetBlockerState(bool blockInput, Color opaqueColor)
        {
            SetActive(_blocker, blockInput);
            if (_blockerImage == null)
            {
                return;
            }

            _blockerImage.color = opaqueColor;
            _blockerImage.raycastTarget = blockInput;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void AddMissing(ICollection<string> issues, UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                issues.Add($"SceneTransitionOverlayShellView is missing serialized field '{fieldName}'.");
            }
        }

        private static bool HasChildComponentNamed(Transform root, string typeName)
        {
            if (root == null)
            {
                return false;
            }

            var components = root.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                if (component.GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
