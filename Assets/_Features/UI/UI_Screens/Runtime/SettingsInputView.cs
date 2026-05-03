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

        [SerializeField] private TMP_Text _sectionTitle;
        [SerializeField] private TMP_Text _movementLabel;
        [SerializeField] private Toggle _movementToggle;
        [SerializeField] private TMP_Text _movementToggleLabel;
        [SerializeField] private TMP_Text _movementCurrentText;
        [SerializeField] private TMP_Text _pushLabel;
        [SerializeField] private TMP_Text _pushCurrentText;
        [SerializeField] private Button _pushChangeButton;
        [SerializeField] private TMP_Text _pushChangeButtonLabel;
        [SerializeField] private TMP_Text _flipLabel;
        [SerializeField] private TMP_Text _flipCurrentText;
        [SerializeField] private Button _flipChangeButton;
        [SerializeField] private TMP_Text _flipChangeButtonLabel;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _resetButton;
        [SerializeField] private TMP_Text _resetButtonLabel;

        private bool _isRefreshingControls;
        private bool _isVisible;
        private SettingsInputViewModel _viewModel;
        private RectTransform _movementRowRoot;
        private RectTransform _pushRowRoot;
        private RectTransform _flipRowRoot;
        private RectTransform _resetRowRoot;

        public event Action<bool> MovementSchemeToggleRequested;

        public event Action PushRebindRequested;

        public event Action FlipRebindRequested;

        public event Action ResetRequested;

        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public bool IsMovementToggleOn => _movementToggle != null && _movementToggle.isOn;

        public bool IsPushChangeInteractable => _pushChangeButton != null && _pushChangeButton.interactable;

        public bool IsFlipChangeInteractable => _flipChangeButton != null && _flipChangeButton.interactable;

        public bool IsResetInteractable => _resetButton != null && _resetButton.interactable;

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
            ValidateControl(_sectionTitle, nameof(_sectionTitle), issues);
            ValidateControl(_movementLabel, nameof(_movementLabel), issues);
            ValidateControl(_movementToggle, nameof(_movementToggle), issues);
            ValidateControl(_movementToggleLabel, nameof(_movementToggleLabel), issues);
            ValidateControl(_movementCurrentText, nameof(_movementCurrentText), issues);
            ValidateControl(_pushLabel, nameof(_pushLabel), issues);
            ValidateControl(_pushCurrentText, nameof(_pushCurrentText), issues);
            ValidateControl(_pushChangeButton, nameof(_pushChangeButton), issues);
            ValidateControl(_pushChangeButtonLabel, nameof(_pushChangeButtonLabel), issues);
            ValidateControl(_flipLabel, nameof(_flipLabel), issues);
            ValidateControl(_flipCurrentText, nameof(_flipCurrentText), issues);
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
            if (!_isVisible || _movementToggle == null)
            {
                return;
            }

            _movementToggle.isOn = useArrowKeys;
        }

        public void ClickPushChange()
        {
            if (!_isVisible)
            {
                return;
            }

            PushRebindRequested?.Invoke();
        }

        public void ClickFlipChange()
        {
            if (!_isVisible)
            {
                return;
            }

            FlipRebindRequested?.Invoke();
        }

        public void ClickReset()
        {
            if (!_isVisible)
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
            ValidateSerializedReference(_sectionTitle, nameof(_sectionTitle));
            ValidateSerializedReference(_movementLabel, nameof(_movementLabel));
            ValidateSerializedReference(_movementToggle, nameof(_movementToggle));
            ValidateSerializedReference(_movementToggleLabel, nameof(_movementToggleLabel));
            ValidateSerializedReference(_movementCurrentText, nameof(_movementCurrentText));
            ValidateSerializedReference(_pushLabel, nameof(_pushLabel));
            ValidateSerializedReference(_pushCurrentText, nameof(_pushCurrentText));
            ValidateSerializedReference(_pushChangeButton, nameof(_pushChangeButton));
            ValidateSerializedReference(_pushChangeButtonLabel, nameof(_pushChangeButtonLabel));
            ValidateSerializedReference(_flipLabel, nameof(_flipLabel));
            ValidateSerializedReference(_flipCurrentText, nameof(_flipCurrentText));
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

        private void HandleMovementToggleChanged(bool useArrowKeys)
        {
            if (!_isVisible || _isRefreshingControls)
            {
                return;
            }

            MovementSchemeToggleRequested?.Invoke(useArrowKeys);
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void LayoutControls()
        {
            SettingsLayoutUtility.EnsureVerticalLayout(
                gameObject,
                new RectOffset(0, 0, 0, 0),
                14f,
                TextAnchor.UpperLeft);

            _movementRowRoot = SettingsLayoutUtility.EnsureChildRect(transform, "MovementInputRow");
            _pushRowRoot = SettingsLayoutUtility.EnsureChildRect(transform, "PushInputRow");
            _flipRowRoot = SettingsLayoutUtility.EnsureChildRect(transform, "FlipInputRow");
            _resetRowRoot = SettingsLayoutUtility.EnsureChildRect(transform, "InputResetRow");

            LayoutSectionTitle();
            LayoutMovementRow();
            LayoutBindingRow(_pushRowRoot, _pushLabel, _pushCurrentText, _pushChangeButton);
            LayoutBindingRow(_flipRowRoot, _flipLabel, _flipCurrentText, _flipChangeButton);
            LayoutStatusAndReset();
            ApplyLayoutOrder();
        }

        private void LayoutSectionTitle()
        {
            SettingsLayoutUtility.MoveToParent(_sectionTitle != null ? _sectionTitle.rectTransform : null, transform as RectTransform);
            SettingsLayoutUtility.EnsureLayoutElement(_sectionTitle, preferredHeight: 24f, flexibleWidth: 1f);
        }

        private void LayoutMovementRow()
        {
            ConfigureRow(_movementRowRoot, 32f);
            SettingsLayoutUtility.MoveToParent(_movementLabel != null ? _movementLabel.rectTransform : null, _movementRowRoot);
            SettingsLayoutUtility.MoveToParent(_movementToggle, _movementRowRoot);
            SettingsLayoutUtility.MoveToParent(_movementCurrentText != null ? _movementCurrentText.rectTransform : null, _movementRowRoot);
            SettingsLayoutUtility.EnsureLayoutElement(_movementLabel, preferredWidth: 132f, preferredHeight: 24f);
            SettingsLayoutUtility.EnsureLayoutElement(_movementToggle, preferredWidth: 150f, preferredHeight: 28f);
            SettingsLayoutUtility.EnsureLayoutElement(_movementCurrentText, preferredHeight: 24f, flexibleWidth: 1f);
        }

        private void LayoutBindingRow(RectTransform rowRoot, TMP_Text label, TMP_Text currentText, Button changeButton)
        {
            ConfigureRow(rowRoot, 32f);
            SettingsLayoutUtility.MoveToParent(label != null ? label.rectTransform : null, rowRoot);
            SettingsLayoutUtility.MoveToParent(currentText != null ? currentText.rectTransform : null, rowRoot);
            SettingsLayoutUtility.MoveToParent(changeButton, rowRoot);
            SettingsLayoutUtility.EnsureLayoutElement(label, preferredWidth: 132f, preferredHeight: 24f);
            SettingsLayoutUtility.EnsureLayoutElement(currentText, preferredHeight: 24f, flexibleWidth: 1f);
            SettingsLayoutUtility.EnsureLayoutElement(changeButton, preferredWidth: 116f, preferredHeight: 30f);
        }

        private void LayoutStatusAndReset()
        {
            SettingsLayoutUtility.MoveToParent(_statusText != null ? _statusText.rectTransform : null, transform as RectTransform);
            SettingsLayoutUtility.EnsureLayoutElement(_statusText, preferredHeight: 28f, flexibleWidth: 1f);

            SettingsLayoutUtility.EnsureHorizontalLayout(
                _resetRowRoot.gameObject,
                new RectOffset(0, 0, 0, 0),
                0f,
                TextAnchor.MiddleCenter);
            SettingsLayoutUtility.EnsureLayoutElement(_resetRowRoot, preferredHeight: 34f, flexibleWidth: 1f);
            SettingsLayoutUtility.FillLayoutChild(_resetRowRoot);
            SettingsLayoutUtility.MoveToParent(_resetButton, _resetRowRoot);
            SettingsLayoutUtility.EnsureLayoutElement(_resetButton, preferredWidth: 156f, preferredHeight: 32f);
        }

        private static void ConfigureRow(RectTransform rowRoot, float preferredHeight)
        {
            SettingsLayoutUtility.EnsureHorizontalLayout(
                rowRoot.gameObject,
                new RectOffset(0, 0, 0, 0),
                10f,
                TextAnchor.MiddleLeft);
            SettingsLayoutUtility.EnsureLayoutElement(rowRoot, preferredHeight: preferredHeight, flexibleWidth: 1f);
            SettingsLayoutUtility.FillLayoutChild(rowRoot);
        }

        private void ApplyLayoutOrder()
        {
            if (_sectionTitle != null)
            {
                _sectionTitle.transform.SetSiblingIndex(0);
            }

            _movementRowRoot.SetSiblingIndex(1);
            _pushRowRoot.SetSiblingIndex(2);
            _flipRowRoot.SetSiblingIndex(3);
            if (_statusText != null)
            {
                _statusText.transform.SetSiblingIndex(4);
            }

            _resetRowRoot.SetSiblingIndex(5);
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
                SetText(_sectionTitle, _viewModel.SectionTitle);
                SetText(_movementLabel, _viewModel.MovementLabel);
                SetText(_movementToggleLabel, _viewModel.UseArrowKeysLabel);
                SetText(_movementCurrentText, _viewModel.MovementCurrentText);
                SetText(_pushLabel, _viewModel.PushLabel);
                SetText(_pushCurrentText, _viewModel.PushCurrentText);
                SetText(_pushChangeButtonLabel, _viewModel.PushChangeLabel);
                SetText(_flipLabel, _viewModel.FlipLabel);
                SetText(_flipCurrentText, _viewModel.FlipCurrentText);
                SetText(_flipChangeButtonLabel, _viewModel.FlipChangeLabel);
                SetText(_statusText, _viewModel.StatusText);
                SetText(_resetButtonLabel, _viewModel.ResetLabel);

                if (_movementToggle != null)
                {
                    _movementToggle.SetIsOnWithoutNotify(_viewModel.UseArrowKeys);
                    _movementToggle.interactable = _viewModel.AreControlsInteractable;
                }

                if (_pushChangeButton != null)
                {
                    _pushChangeButton.interactable = _viewModel.AreControlsInteractable;
                }

                if (_flipChangeButton != null)
                {
                    _flipChangeButton.interactable = _viewModel.AreControlsInteractable;
                }

                if (_resetButton != null)
                {
                    _resetButton.interactable = _viewModel.AreControlsInteractable;
                }
            }
            finally
            {
                _isRefreshingControls = false;
            }
        }

        private void RefreshView()
        {
            RebindControls();
            LayoutControls();
            RefreshControls();
        }

        private void RebindControls()
        {
            if (_movementToggle != null)
            {
                _movementToggle.onValueChanged.RemoveListener(HandleMovementToggleChanged);
                _movementToggle.onValueChanged.AddListener(HandleMovementToggleChanged);
            }

            RebindButton(_pushChangeButton, ClickPushChange);
            RebindButton(_flipChangeButton, ClickFlipChange);
            RebindButton(_resetButton, ClickReset);
        }

        private void UnbindControls()
        {
            if (_movementToggle != null)
            {
                _movementToggle.onValueChanged.RemoveListener(HandleMovementToggleChanged);
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
