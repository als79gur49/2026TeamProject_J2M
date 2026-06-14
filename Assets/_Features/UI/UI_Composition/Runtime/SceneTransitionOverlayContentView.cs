using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal class SceneTransitionOverlayContentView : MonoBehaviour, ISceneTransitionOverlayContentView
    {
        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private TMP_Text _progressText;

        private bool _showProgress;

        public virtual void Bind(SceneTransitionOverlayModel model)
        {
            _showProgress = model.ShowProgress;
            SetProgress(model.Progress01);
        }

        public virtual void SetProgress(float progress01)
        {
            var clamped = Mathf.Clamp01(progress01);
            if (_progressText != null)
            {
                _progressText.text = _showProgress ? $"{Mathf.RoundToInt(clamped * 100f)}%" : string.Empty;
            }
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
            SetProgress(0f);
        }

        internal virtual IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>();
            AddMissing(issues, _rootGroup, nameof(_rootGroup));
            AddMissing(issues, _progressText, nameof(_progressText));

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
