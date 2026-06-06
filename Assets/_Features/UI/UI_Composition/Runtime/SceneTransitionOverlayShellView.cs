using System;
using System.Collections.Generic;
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
            SetBlockerState(model.BlockInput);
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
