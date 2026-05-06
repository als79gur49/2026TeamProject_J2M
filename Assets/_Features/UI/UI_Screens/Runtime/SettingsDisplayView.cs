using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsDisplayView : MonoBehaviour
    {
        private const string MissingControlsMessage =
            "Settings display section is missing required authored controls. Repair: open SettingsScreen.prefab and assign every SettingsDisplayView serialized reference.";

        [SerializeField] private TMP_Text _sectionTitle;
        [SerializeField] private TMP_Text _currentDisplayLabel;
        [SerializeField] private TMP_Text _currentDisplayValue;
        [SerializeField] private TMP_Text _resolutionLabel;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private RectTransform _resolutionInfoHotspot;
        [SerializeField] private SettingsHoverRelay _resolutionHoverRelay;
        [SerializeField] private RectTransform _resolutionHoverHintRoot;
        [SerializeField] private TMP_Text _resolutionHoverHintLabel;
        [SerializeField] private TMP_Text _fullscreenLabel;
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private TMP_Text _displayStatusLabel;
        [SerializeField] private RectTransform _previewCountdownRoot;
        [SerializeField] private TMP_Text _previewCountdownLabel;
        [SerializeField] private Image _previewCountdownFill;
        [SerializeField] private Button _applyButton;
        [SerializeField] private TMP_Text _applyButtonLabel;
        [SerializeField] private Button _revertButton;
        [SerializeField] private TMP_Text _revertButtonLabel;

        private bool _isRefreshingDisplayControls;
        private bool _isResolutionHoverHintVisible;
        private bool _isVisible;
        private SettingsDisplayViewModel _viewModel;

        public event Action<int> ResolutionChanged;

        public event Action<bool> FullscreenToggled;

        public event Action ApplyRequested;

        public event Action RevertRequested;

        public string CurrentDisplayValueText =>
            _currentDisplayValue != null ? _currentDisplayValue.text : string.Empty;

        public string DisplayStatusText =>
            _displayStatusLabel != null ? _displayStatusLabel.text : string.Empty;

        public bool IsDisplayApplyInteractable =>
            _applyButton != null && _applyButton.interactable;

        public bool IsDisplayRevertInteractable =>
            _revertButton != null && _revertButton.interactable;

        public int SelectedResolutionIndex =>
            _resolutionDropdown != null ? _resolutionDropdown.value : 0;

        public bool IsFullscreenOn =>
            _fullscreenToggle != null && _fullscreenToggle.isOn;

        public void Bind(SettingsDisplayViewModel viewModel)
        {
            HideResolutionHoverHint();
            RebindResolutionHoverRelay();

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
            ValidateControl(_currentDisplayLabel, nameof(_currentDisplayLabel), issues);
            ValidateControl(_currentDisplayValue, nameof(_currentDisplayValue), issues);
            ValidateControl(_resolutionLabel, nameof(_resolutionLabel), issues);
            ValidateControl(_resolutionDropdown, nameof(_resolutionDropdown), issues);
            ValidateControl(_resolutionInfoHotspot, nameof(_resolutionInfoHotspot), issues);
            ValidateControl(_resolutionHoverRelay, nameof(_resolutionHoverRelay), issues);
            ValidateControl(_resolutionHoverHintRoot, nameof(_resolutionHoverHintRoot), issues);
            ValidateControl(_resolutionHoverHintLabel, nameof(_resolutionHoverHintLabel), issues);
            ValidateControl(_fullscreenLabel, nameof(_fullscreenLabel), issues);
            ValidateControl(_fullscreenToggle, nameof(_fullscreenToggle), issues);
            ValidateControl(_displayStatusLabel, nameof(_displayStatusLabel), issues);
            ValidateControl(_previewCountdownRoot, nameof(_previewCountdownRoot), issues);
            ValidateControl(_previewCountdownLabel, nameof(_previewCountdownLabel), issues);
            ValidateControl(_previewCountdownFill, nameof(_previewCountdownFill), issues);
            ValidateControl(_applyButton, nameof(_applyButton), issues);
            ValidateControl(_applyButtonLabel, nameof(_applyButtonLabel), issues);
            ValidateControl(_revertButton, nameof(_revertButton), issues);
            ValidateControl(_revertButtonLabel, nameof(_revertButtonLabel), issues);

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(BuildValidationMessage(MissingControlsMessage, issues));
            }
        }

        public void ClickApply()
        {
            if (!_isVisible)
            {
                return;
            }

            ApplyRequested?.Invoke();
        }

        public void ClickRevert()
        {
            if (!_isVisible)
            {
                return;
            }

            RevertRequested?.Invoke();
        }

        public void SelectResolution(int index)
        {
            if (!_isVisible || _resolutionDropdown == null)
            {
                return;
            }

            _resolutionDropdown.value = index;
        }

        public void SetFullscreen(bool isFullscreen)
        {
            if (!_isVisible || _fullscreenToggle == null)
            {
                return;
            }

            _fullscreenToggle.isOn = isFullscreen;
        }

        public void SetIsVisible(bool isVisible)
        {
            _isVisible = isVisible;
            if (!isVisible)
            {
                HideResolutionHoverHint();
            }

            RefreshView();
        }

        private void OnEnable()
        {
            RebindResolutionHoverRelay();
            RefreshView();
        }

        private void OnDisable()
        {
            HideResolutionHoverHint();
            UnbindResolutionHoverRelay();
            UnbindDisplayControls();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_sectionTitle, nameof(_sectionTitle));
            ValidateSerializedReference(_currentDisplayLabel, nameof(_currentDisplayLabel));
            ValidateSerializedReference(_currentDisplayValue, nameof(_currentDisplayValue));
            ValidateSerializedReference(_resolutionLabel, nameof(_resolutionLabel));
            ValidateSerializedReference(_resolutionDropdown, nameof(_resolutionDropdown));
            ValidateSerializedReference(_resolutionInfoHotspot, nameof(_resolutionInfoHotspot));
            ValidateSerializedReference(_resolutionHoverRelay, nameof(_resolutionHoverRelay));
            ValidateSerializedReference(_resolutionHoverHintRoot, nameof(_resolutionHoverHintRoot));
            ValidateSerializedReference(_resolutionHoverHintLabel, nameof(_resolutionHoverHintLabel));
            ValidateSerializedReference(_fullscreenLabel, nameof(_fullscreenLabel));
            ValidateSerializedReference(_fullscreenToggle, nameof(_fullscreenToggle));
            ValidateSerializedReference(_displayStatusLabel, nameof(_displayStatusLabel));
            ValidateSerializedReference(_previewCountdownRoot, nameof(_previewCountdownRoot));
            ValidateSerializedReference(_previewCountdownLabel, nameof(_previewCountdownLabel));
            ValidateSerializedReference(_previewCountdownFill, nameof(_previewCountdownFill));
            ValidateSerializedReference(_applyButton, nameof(_applyButton));
            ValidateSerializedReference(_applyButtonLabel, nameof(_applyButtonLabel));
            ValidateSerializedReference(_revertButton, nameof(_revertButton));
            ValidateSerializedReference(_revertButtonLabel, nameof(_revertButtonLabel));
        }
#endif

        private void OnDestroy()
        {
            HideResolutionHoverHint();
            UnbindResolutionHoverRelay();

            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindDisplayControls();
        }

        private void HandleDisplayFullscreenChanged(bool isFullscreen)
        {
            if (!_isVisible || _isRefreshingDisplayControls)
            {
                return;
            }

            FullscreenToggled?.Invoke(isFullscreen);
        }

        private void HandleDisplayResolutionChanged(int modeIndex)
        {
            if (!_isVisible || _isRefreshingDisplayControls)
            {
                return;
            }

            ResolutionChanged?.Invoke(modeIndex);
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void HandleResolutionHoverEntered()
        {
            ShowResolutionHoverHint();
        }

        private void HandleResolutionHoverExited()
        {
            HideResolutionHoverHint();
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void RebindResolutionHoverRelay()
        {
            if (_resolutionHoverRelay == null)
            {
                return;
            }

            _resolutionHoverRelay.HoverEntered -= HandleResolutionHoverEntered;
            _resolutionHoverRelay.HoverExited -= HandleResolutionHoverExited;
            _resolutionHoverRelay.HoverEntered += HandleResolutionHoverEntered;
            _resolutionHoverRelay.HoverExited += HandleResolutionHoverExited;
        }

        private void RebindDisplayControls()
        {
            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.onValueChanged.RemoveAllListeners();
                _resolutionDropdown.onValueChanged.AddListener(HandleDisplayResolutionChanged);
            }

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.onValueChanged.RemoveAllListeners();
                _fullscreenToggle.onValueChanged.AddListener(HandleDisplayFullscreenChanged);
            }

            RebindButton(_applyButton, ClickApply);
            RebindButton(_revertButton, ClickRevert);
        }

        private void RefreshControls()
        {
            if (_viewModel == null)
            {
                if (_previewCountdownLabel != null)
                {
                    _previewCountdownLabel.text = string.Empty;
                }

                if (_previewCountdownFill != null)
                {
                    _previewCountdownFill.fillAmount = 0f;
                    ApplyPreviewCountdownFillWidth(0f);
                }

                return;
            }

            _isRefreshingDisplayControls = true;
            try
            {
                if (_currentDisplayValue != null)
                {
                    _currentDisplayValue.text = _viewModel.CurrentDisplayValueText;
                }

                if (_displayStatusLabel != null)
                {
                    _displayStatusLabel.text = _viewModel.DisplayStatusText;
                }

                if (_previewCountdownLabel != null)
                {
                    _previewCountdownLabel.text = _viewModel.PreviewCountdownText;
                }

                if (_previewCountdownFill != null)
                {
                    var normalized = Mathf.Clamp01(_viewModel.PreviewCountdownNormalized);
                    _previewCountdownFill.type = Image.Type.Simple;
                    _previewCountdownFill.fillAmount = normalized;
                    ApplyPreviewCountdownFillWidth(normalized);
                }

                if (_applyButton != null)
                {
                    _applyButton.interactable = _viewModel.IsDisplayApplyInteractable;
                }

                if (_revertButton != null)
                {
                    _revertButton.interactable = _viewModel.IsDisplayRevertInteractable;
                }

                if (_resolutionDropdown != null)
                {
                    _resolutionDropdown.ClearOptions();
                    _resolutionDropdown.AddOptions(new List<string>(_viewModel.ResolutionOptionTexts));
                    _resolutionDropdown.SetValueWithoutNotify(
                        Mathf.Clamp(_viewModel.SelectedResolutionIndex, 0, Math.Max(0, _viewModel.ResolutionOptionTexts.Count - 1)));
                    _resolutionDropdown.interactable = !_viewModel.IsDisplayPreviewActive;
                }

                if (_fullscreenToggle != null)
                {
                    _fullscreenToggle.SetIsOnWithoutNotify(_viewModel.IsFullscreenEnabled);
                    _fullscreenToggle.interactable = !_viewModel.IsDisplayPreviewActive;
                }
            }
            finally
            {
                _isRefreshingDisplayControls = false;
            }
        }

        private void RefreshView()
        {
            HideResolutionHoverHint();
            RebindDisplayControls();
            RefreshControls();
            ApplyResolutionHoverHintVisibility();
            ApplyPreviewCountdownVisibility();
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }

        private void UnbindResolutionHoverRelay()
        {
            if (_resolutionHoverRelay == null)
            {
                return;
            }

            _resolutionHoverRelay.HoverEntered -= HandleResolutionHoverEntered;
            _resolutionHoverRelay.HoverExited -= HandleResolutionHoverExited;
        }

        private void UnbindDisplayControls()
        {
            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.onValueChanged.RemoveAllListeners();
            }

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.onValueChanged.RemoveAllListeners();
            }

            UnbindButton(_applyButton, ClickApply);
            UnbindButton(_revertButton, ClickRevert);
        }

        private void ShowResolutionHoverHint()
        {
            if (!_isVisible || _viewModel == null || !HasResolutionHoverHintText())
            {
                HideResolutionHoverHint();
                return;
            }

            _isResolutionHoverHintVisible = true;
            ApplyResolutionHoverHintVisibility();
        }

        private void HideResolutionHoverHint()
        {
            _isResolutionHoverHintVisible = false;
            ApplyResolutionHoverHintVisibility();
        }

        private void ApplyResolutionHoverHintVisibility()
        {
            if (_resolutionHoverHintRoot == null)
            {
                return;
            }

            var shouldShow = _isVisible &&
                             _isResolutionHoverHintVisible &&
                             _viewModel != null &&
                             HasResolutionHoverHintText();
            _resolutionHoverHintRoot.gameObject.SetActive(shouldShow);
        }

        private bool HasResolutionHoverHintText()
        {
            return _resolutionHoverHintLabel != null &&
                   !string.IsNullOrEmpty(_resolutionHoverHintLabel.text);
        }

        private void ApplyPreviewCountdownVisibility()
        {
            if (_previewCountdownRoot == null)
            {
                return;
            }

            var shouldShow = _isVisible &&
                             _viewModel != null &&
                             _viewModel.IsDisplayPreviewActive &&
                             _viewModel.IsPreviewCountdownVisible;
            _previewCountdownRoot.gameObject.SetActive(shouldShow);
        }

        private void ApplyPreviewCountdownFillWidth(float normalized)
        {
            if (_previewCountdownFill == null)
            {
                return;
            }

            var fillRect = _previewCountdownFill.rectTransform;
            var rootWidth = _previewCountdownRoot != null && _previewCountdownRoot.rect.width > 0f
                ? _previewCountdownRoot.rect.width
                : fillRect.sizeDelta.x;

            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(rootWidth * Mathf.Clamp01(normalized), 0f);
        }

        private void ValidateControl(Component component, string fieldName, List<string> issues)
        {
            if (component == null)
            {
                issues.Add($"serialized reference '{fieldName}' is not assigned");
                return;
            }

            if (!component.transform.IsChildOf(transform))
            {
                issues.Add($"serialized reference '{fieldName}' must remain under '{name}'");
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
                Debug.LogWarning($"{nameof(SettingsDisplayView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
