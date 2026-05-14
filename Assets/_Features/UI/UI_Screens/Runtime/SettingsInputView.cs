using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsInputView : MonoBehaviour
    {
        private const string MissingControlsMessage =
            "Settings input section is missing required authored controls. Repair: open SettingsScreen.prefab and assign every SettingsInputView serialized reference.";
        private const float ActiveDisplayAlpha = 1f;
        private const float InactiveDisplayAlpha = 0.3f;

        [SerializeField] private TMP_Text _movementLabel;
        [SerializeField] private Slider _movementSlider;
        [SerializeField] private TMP_Text _movementToggleLabel;
        [SerializeField] private TMP_Text _movementCurrentText;
        [SerializeField] private CanvasGroup _arrowKeyDisplayGroup;
        [SerializeField] private CanvasGroup _wasdKeyDisplayGroup;
        [SerializeField] private GameObject _arrowKeyActiveLight;
        [SerializeField] private GameObject _wasdKeyActiveLight;
        [SerializeField] private TMP_Text _pushLabel;
        [SerializeField] private TMP_Text _pushCurrentText;
        [SerializeField] private TMP_Text _pushKeyDisplayLabel;
        [SerializeField] private Button _pushChangeButton;
        [SerializeField] private TMP_Text _pushChangeButtonLabel;
        [SerializeField] private TMP_Text _flipLabel;
        [SerializeField] private TMP_Text _flipCurrentText;
        [SerializeField] private TMP_Text _flipKeyDisplayLabel;
        [SerializeField] private Button _flipChangeButton;
        [SerializeField] private TMP_Text _flipChangeButtonLabel;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _resetButton;
        [SerializeField] private TMP_Text _resetButtonLabel;

        private bool _isRefreshingControls;
        private bool _isVisible;
        private SettingsInputViewModel _viewModel;

        public event Action<bool> MovementSchemeToggleRequested;

        public event Action PushRebindRequested;

        public event Action FlipRebindRequested;

        public event Action ResetRequested;

        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public bool IsMovementUsingArrowKeys => _movementSlider != null && _movementSlider.value >= 0.5f;

        public bool IsMovementSliderInteractable => _movementSlider != null && _movementSlider.interactable;

        public bool IsPushChangeInteractable => _pushChangeButton != null && _pushChangeButton.interactable;

        public bool IsFlipChangeInteractable => _flipChangeButton != null && _flipChangeButton.interactable;

        public bool IsResetInteractable => _resetButton != null && _resetButton.interactable;

        public bool IsRebindingActive => IsRebinding;

        public void Bind(SettingsInputViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void ValidateAuthoredControlsOrThrow()
        {
            var issues = new List<string>();
            ValidateControl(_movementLabel, nameof(_movementLabel), issues);
            ValidateControl(_movementSlider, nameof(_movementSlider), issues);
            ValidateControl(_movementToggleLabel, nameof(_movementToggleLabel), issues);
            ValidateControl(_movementCurrentText, nameof(_movementCurrentText), issues);
            ValidateControl(_arrowKeyDisplayGroup, nameof(_arrowKeyDisplayGroup), issues);
            ValidateControl(_wasdKeyDisplayGroup, nameof(_wasdKeyDisplayGroup), issues);
            ValidateControl(_arrowKeyActiveLight, nameof(_arrowKeyActiveLight), issues);
            ValidateControl(_wasdKeyActiveLight, nameof(_wasdKeyActiveLight), issues);
            ValidateControl(_pushLabel, nameof(_pushLabel), issues);
            ValidateControl(_pushCurrentText, nameof(_pushCurrentText), issues);
            ValidateControl(_pushKeyDisplayLabel, nameof(_pushKeyDisplayLabel), issues);
            ValidateControl(_pushChangeButton, nameof(_pushChangeButton), issues);
            ValidateControl(_pushChangeButtonLabel, nameof(_pushChangeButtonLabel), issues);
            ValidateControl(_flipLabel, nameof(_flipLabel), issues);
            ValidateControl(_flipCurrentText, nameof(_flipCurrentText), issues);
            ValidateControl(_flipKeyDisplayLabel, nameof(_flipKeyDisplayLabel), issues);
            ValidateControl(_flipChangeButton, nameof(_flipChangeButton), issues);
            ValidateControl(_flipChangeButtonLabel, nameof(_flipChangeButtonLabel), issues);
            ValidateControl(_statusText, nameof(_statusText), issues);
            ValidateControl(_resetButton, nameof(_resetButton), issues);
            ValidateControl(_resetButtonLabel, nameof(_resetButtonLabel), issues);

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(BuildValidationMessage(MissingControlsMessage, issues));
            }
        }

        public void SetIsVisible(bool isVisible)
        {
            _isVisible = isVisible;
            RefreshView();
        }

        public void SetMovementUseArrowKeys(bool useArrowKeys)
        {
            if (!_isVisible || _movementSlider == null)
            {
                return;
            }

            _movementSlider.value = useArrowKeys ? 1f : 0f;
        }

        public bool AdjustMovementScheme(int delta)
        {
            if (!_isVisible || _movementSlider == null || !_movementSlider.interactable || delta == 0)
            {
                return false;
            }

            SetMovementUseArrowKeys(delta > 0);
            return true;
        }

        public void ClickPushChange()
        {
            if (!_isVisible || IsRebinding)
            {
                return;
            }

            PushRebindRequested?.Invoke();
        }

        public void ClickFlipChange()
        {
            if (!_isVisible || IsRebinding)
            {
                return;
            }

            FlipRebindRequested?.Invoke();
        }

        public void ClickReset()
        {
            if (!_isVisible || IsRebinding)
            {
                return;
            }

            ResetRequested?.Invoke();
        }

        private void OnEnable()
        {
            RebindControls();
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindControls();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_movementLabel, nameof(_movementLabel));
            ValidateSerializedReference(_movementSlider, nameof(_movementSlider));
            ValidateSerializedReference(_movementToggleLabel, nameof(_movementToggleLabel));
            ValidateSerializedReference(_movementCurrentText, nameof(_movementCurrentText));
            ValidateSerializedReference(_arrowKeyDisplayGroup, nameof(_arrowKeyDisplayGroup));
            ValidateSerializedReference(_wasdKeyDisplayGroup, nameof(_wasdKeyDisplayGroup));
            ValidateSerializedReference(_arrowKeyActiveLight, nameof(_arrowKeyActiveLight));
            ValidateSerializedReference(_wasdKeyActiveLight, nameof(_wasdKeyActiveLight));
            ValidateSerializedReference(_pushLabel, nameof(_pushLabel));
            ValidateSerializedReference(_pushCurrentText, nameof(_pushCurrentText));
            ValidateSerializedReference(_pushKeyDisplayLabel, nameof(_pushKeyDisplayLabel));
            ValidateSerializedReference(_pushChangeButton, nameof(_pushChangeButton));
            ValidateSerializedReference(_pushChangeButtonLabel, nameof(_pushChangeButtonLabel));
            ValidateSerializedReference(_flipLabel, nameof(_flipLabel));
            ValidateSerializedReference(_flipCurrentText, nameof(_flipCurrentText));
            ValidateSerializedReference(_flipKeyDisplayLabel, nameof(_flipKeyDisplayLabel));
            ValidateSerializedReference(_flipChangeButton, nameof(_flipChangeButton));
            ValidateSerializedReference(_flipChangeButtonLabel, nameof(_flipChangeButtonLabel));
            ValidateSerializedReference(_statusText, nameof(_statusText));
            ValidateSerializedReference(_resetButton, nameof(_resetButton));
            ValidateSerializedReference(_resetButtonLabel, nameof(_resetButtonLabel));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindControls();
        }

        private void HandleMovementSliderChanged(float value)
        {
            if (!_isVisible || _isRefreshingControls)
            {
                return;
            }

            MovementSchemeToggleRequested?.Invoke(value >= 0.5f);
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshControls()
        {
            if (_viewModel == null)
            {
                return;
            }

            _isRefreshingControls = true;
            try
            {
                SetText(_movementLabel, _viewModel.MovementLabel);
                SetText(_movementToggleLabel, _viewModel.UseArrowKeysLabel);
                SetText(_movementCurrentText, _viewModel.MovementCurrentText);
                SetText(_pushLabel, _viewModel.PushLabel);
                SetText(_pushCurrentText, _viewModel.PushCurrentText);
                SetKeyDisplayText(_pushKeyDisplayLabel, _viewModel.PushCurrentText);
                SetText(_pushChangeButtonLabel, _viewModel.PushChangeLabel);
                SetText(_flipLabel, _viewModel.FlipLabel);
                SetText(_flipCurrentText, _viewModel.FlipCurrentText);
                SetKeyDisplayText(_flipKeyDisplayLabel, _viewModel.FlipCurrentText);
                SetText(_flipChangeButtonLabel, _viewModel.FlipChangeLabel);
                SetText(_statusText, _viewModel.StatusText);
                SetText(_resetButtonLabel, _viewModel.ResetLabel);

                if (_movementSlider != null)
                {
                    _movementSlider.SetValueWithoutNotify(_viewModel.UseArrowKeys ? 1f : 0f);
                    _movementSlider.interactable = _viewModel.AreControlsInteractable;
                }

                ApplyMovementDisplayState(_viewModel.UseArrowKeys);

                ApplyRebindButtonState(
                    _pushChangeButton,
                    _viewModel.AreControlsInteractable,
                    _viewModel.IsRebinding && _viewModel.RebindingAction == KeyboardBindableAction.Push);
                ApplyRebindButtonState(
                    _flipChangeButton,
                    _viewModel.AreControlsInteractable,
                    _viewModel.IsRebinding && _viewModel.RebindingAction == KeyboardBindableAction.Flip);
                ApplyRebindButtonState(_resetButton, _viewModel.AreControlsInteractable, isHighlighted: false);
            }
            finally
            {
                _isRefreshingControls = false;
            }
        }

        private bool IsRebinding => _viewModel != null && _viewModel.IsRebinding;

        private void RefreshView()
        {
            RebindControls();
            RefreshControls();
        }

        private void RebindControls()
        {
            if (_movementSlider != null)
            {
                _movementSlider.onValueChanged.RemoveListener(HandleMovementSliderChanged);
                _movementSlider.onValueChanged.AddListener(HandleMovementSliderChanged);
            }

            RebindButton(_pushChangeButton, ClickPushChange);
            RebindButton(_flipChangeButton, ClickFlipChange);
            RebindButton(_resetButton, ClickReset);
        }

        private void UnbindControls()
        {
            if (_movementSlider != null)
            {
                _movementSlider.onValueChanged.RemoveListener(HandleMovementSliderChanged);
            }

            UnbindButton(_pushChangeButton, ClickPushChange);
            UnbindButton(_flipChangeButton, ClickFlipChange);
            UnbindButton(_resetButton, ClickReset);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        private static void SetKeyDisplayText(TMP_Text label, string text)
        {
            if (label == null)
            {
                return;
            }

            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = Mathf.Max(label.fontSizeMax, label.fontSize);
            label.text = text ?? string.Empty;
        }

        private void ApplyMovementDisplayState(bool useArrowKeys)
        {
            ApplyDisplayGroupState(_arrowKeyDisplayGroup, useArrowKeys);
            ApplyDisplayGroupState(_wasdKeyDisplayGroup, !useArrowKeys);
            SetActiveIfChanged(_arrowKeyActiveLight, useArrowKeys);
            SetActiveIfChanged(_wasdKeyActiveLight, !useArrowKeys);
        }

        private static void ApplyDisplayGroupState(CanvasGroup group, bool isActive)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = isActive ? ActiveDisplayAlpha : InactiveDisplayAlpha;
            group.interactable = isActive;
            group.blocksRaycasts = isActive;
        }

        private static void SetActiveIfChanged(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        private static void ApplyRebindButtonState(Button button, bool areControlsInteractable, bool isHighlighted)
        {
            if (button == null)
            {
                return;
            }

            if (isHighlighted)
            {
                button.interactable = true;
                PlayButtonAnimation(button, button.animationTriggers.highlightedTrigger);
                return;
            }

            button.interactable = areControlsInteractable;
            PlayButtonAnimation(
                button,
                areControlsInteractable
                    ? button.animationTriggers.normalTrigger
                    : button.animationTriggers.disabledTrigger);
        }

        private static void PlayButtonAnimation(Button button, string triggerName)
        {
            if (button == null || string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            var animator = button.GetComponent<Animator>();
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return;
            }

            ResetTrigger(animator, button.animationTriggers.normalTrigger);
            ResetTrigger(animator, button.animationTriggers.highlightedTrigger);
            ResetTrigger(animator, button.animationTriggers.pressedTrigger);
            ResetTrigger(animator, button.animationTriggers.selectedTrigger);
            ResetTrigger(animator, button.animationTriggers.disabledTrigger);
            animator.SetTrigger(triggerName);
        }

        private static void ResetTrigger(Animator animator, string triggerName)
        {
            if (animator != null && !string.IsNullOrEmpty(triggerName))
            {
                animator.ResetTrigger(triggerName);
            }
        }

        private static void RebindButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnbindButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }

        private static void ValidateControl(UnityEngine.Object control, string fieldName, ICollection<string> issues)
        {
            if (control == null)
            {
                issues.Add($"serialized reference '{fieldName}' is not assigned");
            }
        }

        private static string BuildValidationMessage(string baseMessage, IReadOnlyList<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return baseMessage;
            }

            return $"{baseMessage} Details: {string.Join("; ", issues)}.";
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(SettingsInputView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
