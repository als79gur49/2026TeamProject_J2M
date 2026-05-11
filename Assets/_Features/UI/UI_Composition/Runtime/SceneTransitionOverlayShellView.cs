using System.Collections.Generic;
using TMPro;
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
        private GeneratedContentView _generatedContent;

        public void ShowBlockerOnly(bool blockInput)
        {
            gameObject.SetActive(true);
            SetRootGroupVisible(true, blockInput);
            SetBlockerState(blockInput);
            HideVisual();
        }

        public ISceneTransitionOverlayContentView MountContent(SceneTransitionOverlayContentView contentPrefab)
        {
            if (_activeContent != null)
            {
                _activeContent.Hide();
            }

            if (contentPrefab == null)
            {
                _activeContent = EnsureGeneratedContent();
                return _activeContent;
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

        public void ShowContent(SceneTransitionOverlayViewModel model, ISceneTransitionOverlayContentView content)
        {
            gameObject.SetActive(true);
            SetRootGroupVisible(true, model.BlockInput);
            SetBlockerState(model.BlockInput);
            SetActive(_visualRoot, model.OverlayKind != Game.Feature.Stages.TransitionOverlayKind.None);
            if (_visualGroup != null)
            {
                _visualGroup.alpha = 1f;
                _visualGroup.blocksRaycasts = false;
                _visualGroup.interactable = false;
            }

            _activeContent = content ?? _activeContent ?? EnsureGeneratedContent();
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
            SetBlockerState(false);
            SetRootGroupVisible(false, false);
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

        private GeneratedContentView EnsureGeneratedContent()
        {
            if (_generatedContent != null)
            {
                return _generatedContent;
            }

            var root = new GameObject("GeneratedGenericTransitionContent", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(_contentMount != null ? _contentMount : transform, false);
            UiCanvasElementFactory.Stretch(root.GetComponent<RectTransform>());
            _generatedContent = new GeneratedContentView(root);
            _generatedContent.Hide();
            return _generatedContent;
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

        private void SetBlockerState(bool blockInput)
        {
            SetActive(_blocker, blockInput);
            if (_blockerImage == null)
            {
                return;
            }

            _blockerImage.color = Color.clear;
            _blockerImage.raycastTarget = blockInput;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void AddMissing(ICollection<string> issues, Object value, string fieldName)
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

        private sealed class GeneratedContentView : ISceneTransitionOverlayContentView
        {
            private readonly CanvasGroup _canvasGroup;
            private readonly GameObject _root;
            private readonly TMP_Text _titleText;
            private readonly TMP_Text _messageText;
            private readonly TMP_Text _progressText;
            private readonly RectTransform _progressFill;
            private bool _showProgress;

            public GeneratedContentView(GameObject root)
            {
                _root = root;
                _canvasGroup = root.GetComponent<CanvasGroup>();

                var panel = UiCanvasElementFactory.CreatePanel(
                    "StatusPanel",
                    root.transform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(620f, 250f),
                    Vector2.zero);
                panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.96f);
                _titleText = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(36f, -30f), new Vector2(548f, 44f), TextAnchor.MiddleCenter, 28);
                _messageText = UiCanvasElementFactory.CreateLabel("Message", panel, new Vector2(36f, -82f), new Vector2(548f, 58f), TextAnchor.MiddleCenter, 17);

                var progressBack = new GameObject("ProgressBack", typeof(RectTransform), typeof(Image));
                progressBack.transform.SetParent(panel, false);
                var progressBackRect = progressBack.GetComponent<RectTransform>();
                progressBackRect.anchorMin = new Vector2(0.5f, 0f);
                progressBackRect.anchorMax = new Vector2(0.5f, 0f);
                progressBackRect.pivot = new Vector2(0.5f, 0f);
                progressBackRect.sizeDelta = new Vector2(500f, 12f);
                progressBackRect.anchoredPosition = new Vector2(0f, 42f);
                progressBack.GetComponent<Image>().color = new Color(0.21f, 0.24f, 0.29f, 1f);

                var progressFillObject = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
                progressFillObject.transform.SetParent(progressBack.transform, false);
                _progressFill = progressFillObject.GetComponent<RectTransform>();
                _progressFill.anchorMin = Vector2.zero;
                _progressFill.anchorMax = new Vector2(0f, 1f);
                _progressFill.pivot = new Vector2(0f, 0.5f);
                _progressFill.sizeDelta = Vector2.zero;
                _progressFill.anchoredPosition = Vector2.zero;
                progressFillObject.GetComponent<Image>().color = new Color(0.66f, 0.86f, 0.95f, 1f);

                _progressText = UiCanvasElementFactory.CreateLabel("ProgressText", panel, new Vector2(36f, -206f), new Vector2(548f, 26f), TextAnchor.MiddleCenter, 14);
            }

            public void Bind(SceneTransitionOverlayViewModel model)
            {
                _showProgress = model.ShowProgress;
                _titleText.text = model.Title;
                _messageText.text = model.Message;
                SetProgress(model.Progress01);
            }

            public void SetProgress(float progress01)
            {
                var clamped = Mathf.Clamp01(progress01);
                _progressFill.anchorMax = new Vector2(clamped, 1f);
                _progressText.text = _showProgress ? $"{Mathf.RoundToInt(clamped * 100f)}%" : string.Empty;
                _progressFill.parent.gameObject.SetActive(_showProgress);
                _progressText.gameObject.SetActive(_showProgress);
            }

            public void Show()
            {
                _canvasGroup.alpha = 1f;
                _root.SetActive(true);
            }

            public void Hide()
            {
                _canvasGroup.alpha = 0f;
                _root.SetActive(false);
            }

            public void ResetView()
            {
                _showProgress = false;
                _titleText.text = string.Empty;
                _messageText.text = string.Empty;
                SetProgress(0f);
            }
        }
    }
}
