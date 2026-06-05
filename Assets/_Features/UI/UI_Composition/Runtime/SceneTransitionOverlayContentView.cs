using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal class SceneTransitionOverlayContentView : MonoBehaviour, ISceneTransitionOverlayContentView
    {
        private const string ShowTriggerName = "Show";

        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private GameObject _progressRoot;
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private Animator _animator;

        private bool _showProgress;

        public virtual void Bind(SceneTransitionOverlayModel model)
        {
            _showProgress = model.ShowProgress;
            SetText(_titleText, model.Title);
            SetText(_messageText, model.Message);
            SetProgress(model.Progress01);
        }

        public virtual void SetProgress(float progress01)
        {
            var clamped = Mathf.Clamp01(progress01);
            if (_progressFill != null)
            {
                _progressFill.anchorMax = new Vector2(clamped, 1f);
            }

            if (_progressText != null)
            {
                _progressText.text = _showProgress ? $"{Mathf.RoundToInt(clamped * 100f)}%" : string.Empty;
            }

            SetActive(_progressRoot, _showProgress);
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            if (_rootGroup != null)
            {
                _rootGroup.alpha = 1f;
                _rootGroup.blocksRaycasts = false;
                _rootGroup.interactable = false;
            }

            if (_animator != null)
            {
                _animator.ResetTrigger(ShowTriggerName);
                _animator.SetTrigger(ShowTriggerName);
            }
        }

        public virtual void Hide()
        {
            if (_rootGroup != null)
            {
                _rootGroup.alpha = 0f;
                _rootGroup.blocksRaycasts = false;
                _rootGroup.interactable = false;
            }

            gameObject.SetActive(false);
        }

        public virtual void ResetView()
        {
            _showProgress = false;
            SetText(_titleText, string.Empty);
            SetText(_messageText, string.Empty);
            SetProgress(0f);
        }

        internal virtual IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>();
            AddMissing(issues, _rootGroup, nameof(_rootGroup));
            AddMissing(issues, _titleText, nameof(_titleText));
            AddMissing(issues, _messageText, nameof(_messageText));
            if (_progressRoot != null && _progressFill == null)
            {
                issues.Add($"{GetType().Name} has ProgressRoot but is missing ProgressFill.");
            }

            if (HasChildComponentNamed(transform, nameof(EventSystem)))
            {
                issues.Add($"{GetType().Name} prefab must not contain an EventSystem child.");
            }

            if (HasChildComponentNamed(transform, nameof(AudioSource)))
            {
                issues.Add($"{GetType().Name} prefab must not contain an AudioSource child.");
            }

            if (HasChildComponentNamed(transform, "AudioRuntimeRoot"))
            {
                issues.Add($"{GetType().Name} prefab must not contain an AudioRuntimeRoot child.");
            }

            if (HasChildComponentNamed(transform, "GlobalAudioFlowRoot"))
            {
                issues.Add($"{GetType().Name} prefab must not contain a GlobalAudioFlowRoot child.");
            }

            return issues;
        }

        protected static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        protected static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        protected static void AddMissing(ICollection<string> issues, Object value, string fieldName)
        {
            if (value == null)
            {
                issues.Add($"Scene transition content view is missing serialized field '{fieldName}'.");
            }
        }

        protected static bool HasChildComponentNamed(Transform root, string typeName)
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
