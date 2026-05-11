using System.Collections.Generic;
using Game.Feature.Stages;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal sealed class SceneTransitionOverlayView : MonoBehaviour, ISceneTransitionOverlayView
    {
        private const string ShowTriggerName = "Show";

        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private CanvasGroup _visualGroup;
        [SerializeField] private GameObject _blocker;
        [SerializeField] private Image _blockerImage;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private GameObject _genericLoadingGroup;
        [SerializeField] private GameObject _chanceLostGroup;
        [SerializeField] private GameObject _restartGroup;
        [SerializeField] private GameObject _stageClearGroup;
        [SerializeField] private GameObject _mainMenuReturnGroup;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _loadingText;
        [SerializeField] private TMP_Text _previousChanceText;
        [SerializeField] private TMP_Text _currentChanceText;
        [SerializeField] private TMP_Text _totalChanceText;
        [SerializeField] private TMP_Text _deathCountText;
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private GameObject _progressRoot;
        [SerializeField] private Animator _animator;

        private bool _showProgress;

        public void ShowBlockerOnly(bool blockInput)
        {
            gameObject.SetActive(true);
            SetRootGroupVisible(true, blockInput);
            SetBlockerState(blockInput);
            HideVisual();
        }

        public void ShowOverlay(SceneTransitionOverlayViewModel model)
        {
            gameObject.SetActive(true);
            _showProgress = model.ShowProgress;

            SetRootGroupVisible(true, model.BlockInput);
            SetBlockerState(model.BlockInput);
            SetText(_titleText, model.Title);
            SetText(_messageText, model.Message);
            SetText(_loadingText, string.IsNullOrWhiteSpace(model.Message) ? "Loading" : model.Message);
            SetChanceLostText(model);
            SetOverlayGroup(model.OverlayKind);

            var hasVisual = model.OverlayKind != TransitionOverlayKind.None;
            SetActive(_visualRoot, hasVisual);
            if (_visualGroup != null)
            {
                _visualGroup.alpha = hasVisual ? 1f : 0f;
                _visualGroup.blocksRaycasts = false;
                _visualGroup.interactable = false;
            }

            SetProgress(model.Progress01);
            if (hasVisual && _animator != null)
            {
                _animator.ResetTrigger(ShowTriggerName);
                _animator.SetTrigger(ShowTriggerName);
            }
        }

        public void SetProgress(float progress01)
        {
            var clamped = Mathf.Clamp01(progress01);
            if (_progressFill != null)
            {
                _progressFill.anchorMax = new Vector2(clamped, 1f);
            }

            SetActive(_progressRoot, _showProgress);
        }

        public void HideVisual()
        {
            SetActive(_visualRoot, false);
            SetAllOverlayGroups(false);
            if (_visualGroup != null)
            {
                _visualGroup.alpha = 0f;
                _visualGroup.blocksRaycasts = false;
                _visualGroup.interactable = false;
            }
        }

        public void HideAll()
        {
            _showProgress = false;
            HideVisual();
            SetProgress(0f);
            SetBlockerState(false);
            SetRootGroupVisible(false, false);
            gameObject.SetActive(false);
        }

        internal IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>();
            AddMissing(issues, _canvas, nameof(_canvas));
            AddMissing(issues, _rootGroup, nameof(_rootGroup));
            AddMissing(issues, _visualRoot, nameof(_visualRoot));
            AddMissing(issues, _blocker, nameof(_blocker));
            AddMissing(issues, _blockerImage, nameof(_blockerImage));
            AddMissing(issues, _genericLoadingGroup, nameof(_genericLoadingGroup));
            AddMissing(issues, _chanceLostGroup, nameof(_chanceLostGroup));
            AddMissing(issues, _titleText, nameof(_titleText));
            AddMissing(issues, _messageText, nameof(_messageText));
            AddMissing(issues, _loadingText, nameof(_loadingText));
            AddMissing(issues, _previousChanceText, nameof(_previousChanceText));
            AddMissing(issues, _currentChanceText, nameof(_currentChanceText));
            AddMissing(issues, _totalChanceText, nameof(_totalChanceText));
            AddMissing(issues, _deathCountText, nameof(_deathCountText));
            AddMissing(issues, _progressRoot, nameof(_progressRoot));
            if (_progressRoot != null && _progressFill == null)
            {
                issues.Add("SceneTransitionOverlayView has ProgressRoot but is missing ProgressFill.");
            }

            var root = transform;
            if (HasChildComponentNamed(root, nameof(EventSystem)))
            {
                issues.Add("SceneTransitionOverlayView prefab must not contain an EventSystem child.");
            }

            if (HasChildComponentNamed(root, "AudioSource"))
            {
                issues.Add("SceneTransitionOverlayView prefab must not contain an AudioSource child.");
            }

            if (HasChildComponentNamed(root, "AudioRuntimeRoot"))
            {
                issues.Add("SceneTransitionOverlayView prefab must not contain an AudioRuntimeRoot child.");
            }

            if (HasChildComponentNamed(root, "GlobalAudioFlowRoot"))
            {
                issues.Add("SceneTransitionOverlayView prefab must not contain a GlobalAudioFlowRoot child.");
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

        private void SetOverlayGroup(TransitionOverlayKind overlayKind)
        {
            SetAllOverlayGroups(false);
            switch (overlayKind)
            {
                case TransitionOverlayKind.GenericLoading:
                    SetActive(_genericLoadingGroup, true);
                    break;
                case TransitionOverlayKind.ChanceLost:
                    SetActive(_chanceLostGroup, true);
                    break;
                case TransitionOverlayKind.Restart:
                    SetActive(_restartGroup, true);
                    break;
                case TransitionOverlayKind.StageClear:
                    SetActive(_stageClearGroup, true);
                    break;
                case TransitionOverlayKind.MainMenuReturn:
                    SetActive(_mainMenuReturnGroup, true);
                    break;
            }
        }

        private void SetAllOverlayGroups(bool active)
        {
            SetActive(_genericLoadingGroup, active);
            SetActive(_chanceLostGroup, active);
            SetActive(_restartGroup, active);
            SetActive(_stageClearGroup, active);
            SetActive(_mainMenuReturnGroup, active);
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

        private void SetChanceLostText(SceneTransitionOverlayViewModel model)
        {
            if (!model.HasChanceLost)
            {
                SetText(_previousChanceText, string.Empty);
                SetText(_currentChanceText, string.Empty);
                SetText(_totalChanceText, string.Empty);
                SetText(_deathCountText, string.Empty);
                return;
            }

            SetText(_previousChanceText, model.PreviousRemainingChances.ToString());
            SetText(_currentChanceText, model.CurrentRemainingChances.ToString());
            SetText(_totalChanceText, $"/ {model.TotalChances}");
            SetText(_deathCountText, $"Deaths {model.DeathCount}");
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
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
                issues.Add($"SceneTransitionOverlayView is missing serialized field '{fieldName}'.");
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
