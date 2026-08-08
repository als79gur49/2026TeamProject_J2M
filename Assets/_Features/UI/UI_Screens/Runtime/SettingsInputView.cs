using System;
using System.Collections.Generic;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
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
        [SerializeField] private TMP_Text _movementLabel;
        [SerializeField] private Button _movementSchemeButton;
        [SerializeField] private GameObject[] _wasdKeyLabels;
        [SerializeField] private GameObject[] _arrowKeyIcons;
        [SerializeField] private TMP_Text _pushLabel;
        [SerializeField] private TMP_Text _pushCurrentText;
        [SerializeField] private TMP_Text _pushKeyDisplayLabel;
        [SerializeField] private Button _pushRebindButton;
        [SerializeField] private TMP_Text _flipLabel;
        [SerializeField] private TMP_Text _flipCurrentText;
        [SerializeField] private TMP_Text _flipKeyDisplayLabel;
        [SerializeField] private Button _flipRebindButton;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _resetButton;
        [SerializeField] private TMP_Text _resetButtonLabel;

        private bool _isVisible;
        private ILocalizedTextResolver _localizedTextResolver;
        private GameplayUiTypographyTheme _typographyTheme;
        private List<LocalizedTmpTextBinding> _localizedStaticBindings;
        private SettingsInputViewModel _viewModel;

        public event Action<bool> MovementSchemeToggleRequested;

        public event Action PushRebindRequested;

        public event Action FlipRebindRequested;

        public event Action ResetRequested;

        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public bool IsMovementUsingArrowKeys => _viewModel != null && _viewModel.UseArrowKeys;

        public bool IsMovementSchemeInteractable =>
            _movementSchemeButton != null && _movementSchemeButton.interactable;

        public bool IsPushRebindInteractable => _pushRebindButton != null && _pushRebindButton.interactable;

        public bool IsFlipRebindInteractable => _flipRebindButton != null && _flipRebindButton.interactable;

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

        public void BindStaticLocalization(
            SettingsScreenPayload payload,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver,
            GameplayUiTypographyTheme typographyTheme)
        {
            UnbindStaticLocalization();
            if (payload == null)
            {
                return;
            }

            _localizedStaticBindings = new List<LocalizedTmpTextBinding>
            {
                new(
                    _movementLabel,
                    payload.MovementLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    null,
                    typographyTheme,
                    requiredThemeApplyMask: TypographyApplyMask.FontStyle),
                new(
                    _pushLabel,
                    payload.PushLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    null,
                    typographyTheme,
                    requiredThemeApplyMask: TypographyApplyMask.FontStyle),
                new(
                    _flipLabel,
                    payload.FlipLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    null,
                    typographyTheme,
                    requiredThemeApplyMask: TypographyApplyMask.FontStyle),
                new(
                    _resetButtonLabel,
                    payload.ResetInputLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    null,
                    typographyTheme,
                    requiredThemeApplyMask: TypographyApplyMask.FontStyle),
            };
            _localizedTextResolver = textResolver;
            _typographyTheme = typographyTheme;
            if (_typographyTheme != null && _localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged += HandleLocaleChanged;
            }

            RefreshTypography();
        }

        public void UnbindStaticLocalization()
        {
            if (_localizedStaticBindings == null)
            {
                return;
            }

            for (var i = 0; i < _localizedStaticBindings.Count; i++)
            {
                _localizedStaticBindings[i]?.Dispose();
            }

            _localizedStaticBindings = null;
            if (_localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged -= HandleLocaleChanged;
            }

            _localizedTextResolver = null;
            _typographyTheme = null;
        }

        public void ValidateAuthoredControlsOrThrow()
        {
            var issues = new List<string>();
            ValidateControl(_movementLabel, nameof(_movementLabel), issues);
            ValidateControl(_movementSchemeButton, nameof(_movementSchemeButton), issues);
            ValidateControls(_wasdKeyLabels, nameof(_wasdKeyLabels), 4, issues);
            ValidateControls(_arrowKeyIcons, nameof(_arrowKeyIcons), 4, issues);
            ValidateControl(_pushLabel, nameof(_pushLabel), issues);
            ValidateControl(_pushCurrentText, nameof(_pushCurrentText), issues);
            ValidateControl(_pushKeyDisplayLabel, nameof(_pushKeyDisplayLabel), issues);
            ValidateControl(_pushRebindButton, nameof(_pushRebindButton), issues);
            ValidateControl(_flipLabel, nameof(_flipLabel), issues);
            ValidateControl(_flipCurrentText, nameof(_flipCurrentText), issues);
            ValidateControl(_flipKeyDisplayLabel, nameof(_flipKeyDisplayLabel), issues);
            ValidateControl(_flipRebindButton, nameof(_flipRebindButton), issues);
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

        public void ClickMovementScheme()
        {
            if (!_isVisible || !IsMovementSchemeInteractable || IsRebinding)
            {
                return;
            }

            MovementSchemeToggleRequested?.Invoke(!IsMovementUsingArrowKeys);
        }

        public void ClickPushRebind()
        {
            if (!_isVisible || IsRebinding)
            {
                return;
            }

            PushRebindRequested?.Invoke();
        }

        public void ClickFlipRebind()
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
            ValidateSerializedReference(_movementSchemeButton, nameof(_movementSchemeButton));
            ValidateSerializedReferences(_wasdKeyLabels, nameof(_wasdKeyLabels), 4);
            ValidateSerializedReferences(_arrowKeyIcons, nameof(_arrowKeyIcons), 4);
            ValidateSerializedReference(_pushLabel, nameof(_pushLabel));
            ValidateSerializedReference(_pushCurrentText, nameof(_pushCurrentText));
            ValidateSerializedReference(_pushKeyDisplayLabel, nameof(_pushKeyDisplayLabel));
            ValidateSerializedReference(_pushRebindButton, nameof(_pushRebindButton));
            ValidateSerializedReference(_flipLabel, nameof(_flipLabel));
            ValidateSerializedReference(_flipCurrentText, nameof(_flipCurrentText));
            ValidateSerializedReference(_flipKeyDisplayLabel, nameof(_flipKeyDisplayLabel));
            ValidateSerializedReference(_flipRebindButton, nameof(_flipRebindButton));
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

            UnbindStaticLocalization();
            UnbindControls();
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void HandleLocaleChanged()
        {
            RefreshTypography();
        }

        private void RefreshControls()
        {
            if (_viewModel == null)
            {
                return;
            }

            if (!HasLocalizedStaticBindings)
            {
                SetText(_movementLabel, _viewModel.MovementLabel);
                SetText(_pushLabel, _viewModel.PushLabel);
                SetText(_flipLabel, _viewModel.FlipLabel);
                SetText(_resetButtonLabel, _viewModel.ResetLabel);
            }

            SetText(_pushCurrentText, _viewModel.PushCurrentText);
            SetKeyDisplayText(_pushKeyDisplayLabel, _viewModel.PushCurrentText);
            SetText(_flipCurrentText, _viewModel.FlipCurrentText);
            SetKeyDisplayText(_flipKeyDisplayLabel, _viewModel.FlipCurrentText);
            SetText(_statusText, _viewModel.StatusText);
            RefreshTypography();

            if (_movementSchemeButton != null)
            {
                _movementSchemeButton.interactable = _viewModel.AreControlsInteractable;
            }

            ApplyMovementSchemeDisplay(_viewModel.UseArrowKeys);

            ApplyRebindButtonState(
                _pushRebindButton,
                _viewModel.AreControlsInteractable,
                _viewModel.IsRebinding && _viewModel.RebindingAction == KeyboardBindableAction.Push);
            ApplyRebindButtonState(
                _flipRebindButton,
                _viewModel.AreControlsInteractable,
                _viewModel.IsRebinding && _viewModel.RebindingAction == KeyboardBindableAction.Flip);
            ApplyRebindButtonState(_resetButton, _viewModel.AreControlsInteractable, isHighlighted: false);
        }

        private bool IsRebinding => _viewModel != null && _viewModel.IsRebinding;

        private void RefreshView()
        {
            RebindControls();
            RefreshControls();
        }

        private bool HasLocalizedStaticBindings =>
            _localizedStaticBindings != null && _localizedStaticBindings.Count > 0;

        private void RefreshTypography()
        {
            if (_typographyTheme == null)
            {
                return;
            }

            var localeCode = _localizedTextResolver != null
                ? _localizedTextResolver.CurrentLocaleCode
                : string.Empty;
            ApplySettingsTypography(_pushCurrentText, localeCode);
            ApplySettingsTypography(_pushKeyDisplayLabel, localeCode);
            ApplySettingsTypography(_flipCurrentText, localeCode);
            ApplySettingsTypography(_flipKeyDisplayLabel, localeCode);
            ApplySettingsTypography(_statusText, localeCode);
        }

        private void ApplySettingsTypography(TMP_Text target, string localeCode)
        {
            LocalizedTmpTextApplicator.ApplyTypographyTheme(
                target,
                _typographyTheme,
                localeCode,
                requiredApplyMask: TypographyApplyMask.FontStyle);
        }

        private void RebindControls()
        {
            RebindButton(_movementSchemeButton, ClickMovementScheme);
            RebindButton(_pushRebindButton, ClickPushRebind);
            RebindButton(_flipRebindButton, ClickFlipRebind);
            RebindButton(_resetButton, ClickReset);
        }

        private void UnbindControls()
        {
            UnbindButton(_movementSchemeButton, ClickMovementScheme);
            UnbindButton(_pushRebindButton, ClickPushRebind);
            UnbindButton(_flipRebindButton, ClickFlipRebind);
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

        private void ApplyMovementSchemeDisplay(bool useArrowKeys)
        {
            ApplyDisplayObjects(_wasdKeyLabels, !useArrowKeys);
            ApplyDisplayObjects(_arrowKeyIcons, useArrowKeys);
        }

        private static void ApplyDisplayObjects(IReadOnlyList<GameObject> targets, bool isActive)
        {
            if (targets == null)
            {
                return;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                SetActiveIfChanged(targets[i], isActive);
            }
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

        private static void ValidateControls(
            IReadOnlyList<GameObject> controls,
            string fieldName,
            int expectedCount,
            ICollection<string> issues)
        {
            if (controls == null || controls.Count != expectedCount)
            {
                issues.Add($"serialized reference array '{fieldName}' must contain exactly {expectedCount} entries");
                return;
            }

            for (var i = 0; i < controls.Count; i++)
            {
                if (controls[i] == null)
                {
                    issues.Add($"serialized reference '{fieldName}[{i}]' is not assigned");
                }
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

        private void ValidateSerializedReferences(
            IReadOnlyList<GameObject> values,
            string fieldName,
            int expectedCount)
        {
            if (values == null || values.Count != expectedCount)
            {
                Debug.LogWarning(
                    $"{nameof(SettingsInputView)} on '{name}' requires exactly {expectedCount} serialized references in '{fieldName}'.",
                    this);
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                ValidateSerializedReference(values[i], $"{fieldName}[{i}]");
            }
        }
#endif
    }
}
