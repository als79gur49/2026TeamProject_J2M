using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsDisplayView : MonoBehaviour
    {
        private const string MissingControlsMessage =
            "Settings display section is missing required authored controls. Repair: open SettingsScreen.prefab and assign every SettingsDisplayView serialized reference.";
        private const int KeyboardListVisibleItemCount = 4;

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
        [SerializeField] private TMP_Text _languageLabel;
        [SerializeField] private Button _languageCycleButton;
        [SerializeField] private TMP_Text _languageCycleButtonLabel;
        [SerializeField] private TMP_Text _displayStatusLabel;
        [SerializeField] private RectTransform _previewCountdownRoot;
        [SerializeField] private TMP_Text _previewCountdownLabel;
        [SerializeField] private Slider _previewCountdownSlider;
        [SerializeField] private Button _applyButton;
        [SerializeField] private TMP_Text _applyButtonLabel;
        [SerializeField] private Button _revertButton;
        [SerializeField] private TMP_Text _revertButtonLabel;

        private bool _isRefreshingDisplayControls;
        private bool _isResolutionHoverHintVisible;
        private bool _isVisible;
        private int _resolutionKeyboardHighlightedIndex = -1;
        private SettingsDisplayViewModel _viewModel;

        public event Action<int> ResolutionChanged;

        public event Action<bool> FullscreenToggled;

        public event Action ApplyRequested;

        public event Action RevertRequested;

        public event Action LanguageCycleRequested;

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

        public int ResolutionOptionCount =>
            _resolutionDropdown != null && _resolutionDropdown.options != null ? _resolutionDropdown.options.Count : 0;

        public int CurrentResolutionIndex => SelectedResolutionIndex;

        public int ResolutionKeyboardHighlightedIndex => _resolutionKeyboardHighlightedIndex;

        public bool IsResolutionKeyboardListOpen =>
            TryGetNativeResolutionDropdownList(out var nativeList) && nativeList.gameObject.activeSelf;

        public bool IsFullscreenOn =>
            _fullscreenToggle != null && _fullscreenToggle.isOn;

        public string LanguageLabelText =>
            _languageLabel != null ? _languageLabel.text : string.Empty;

        public string CurrentLanguageText =>
            _languageCycleButtonLabel != null ? _languageCycleButtonLabel.text : string.Empty;

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
            ValidateControl(_previewCountdownSlider, nameof(_previewCountdownSlider), issues);
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

        public void ClickLanguageCycle()
        {
            if (!_isVisible ||
                _viewModel == null ||
                !_viewModel.IsLanguageSelectionAvailable)
            {
                return;
            }

            LanguageCycleRequested?.Invoke();
        }

        public void SelectResolution(int index)
        {
            if (!_isVisible ||
                _resolutionDropdown == null ||
                _resolutionDropdown.options == null ||
                index < 0 ||
                index >= _resolutionDropdown.options.Count ||
                _resolutionDropdown.value == index)
            {
                return;
            }

            _resolutionDropdown.value = index;
        }

        public bool TryGetResolutionOptionLabel(int index, out string label)
        {
            label = string.Empty;
            if (_resolutionDropdown == null ||
                _resolutionDropdown.options == null ||
                index < 0 ||
                index >= _resolutionDropdown.options.Count)
            {
                return false;
            }

            label = _resolutionDropdown.options[index].text ?? string.Empty;
            return true;
        }

        public bool OpenResolutionKeyboardList(int highlightedIndex)
        {
            if (!_isVisible || _resolutionDropdown == null || ResolutionOptionCount <= 0)
            {
                return false;
            }

            // Keyboard opens TMP's visual list, but option navigation/commit still stays in UiFocusGraph.
            _resolutionDropdown.Show();
            if (!TryGetNativeResolutionDropdownList(out _))
            {
                return false;
            }

            SetResolutionKeyboardHighlight(Mathf.Clamp(highlightedIndex, 0, ResolutionOptionCount - 1));
            return true;
        }

        public void CloseResolutionKeyboardList()
        {
            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.Hide();
            }

            _resolutionKeyboardHighlightedIndex = -1;
        }

        public void SetResolutionKeyboardHighlight(int index)
        {
            if (ResolutionOptionCount <= 0)
            {
                _resolutionKeyboardHighlightedIndex = -1;
                return;
            }

            _resolutionKeyboardHighlightedIndex = Mathf.Clamp(index, 0, ResolutionOptionCount - 1);
            RefreshNativeResolutionKeyboardHighlight();
            EnsureResolutionKeyboardHighlightVisible(_resolutionKeyboardHighlightedIndex);
        }

        public void EnsureResolutionKeyboardHighlightVisible(int index)
        {
            if (TryGetNativeResolutionDropdownList(out var nativeList))
            {
                var nativeScrollRect = nativeList.GetComponentInChildren<ScrollRect>(true);
                if (nativeScrollRect != null && ResolutionOptionCount > KeyboardListVisibleItemCount)
                {
                    var nativeMaxTopIndex = Mathf.Max(0, ResolutionOptionCount - KeyboardListVisibleItemCount);
                    var nativeTopIndex = Mathf.Clamp(index - KeyboardListVisibleItemCount + 1, 0, nativeMaxTopIndex);
                    nativeScrollRect.verticalNormalizedPosition = nativeMaxTopIndex <= 0
                        ? 1f
                        : 1f - nativeTopIndex / (float)nativeMaxTopIndex;
                }

                return;
            }
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
                CloseResolutionKeyboardList();
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
            CloseResolutionKeyboardList();
            UnbindResolutionHoverRelay();
            UnbindDisplayControls();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
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
            ValidateSerializedReference(_previewCountdownSlider, nameof(_previewCountdownSlider));
            ValidateSerializedReference(_applyButton, nameof(_applyButton));
            ValidateSerializedReference(_applyButtonLabel, nameof(_applyButtonLabel));
            ValidateSerializedReference(_revertButton, nameof(_revertButton));
            ValidateSerializedReference(_revertButtonLabel, nameof(_revertButtonLabel));
        }
#endif

        private void OnDestroy()
        {
            HideResolutionHoverHint();
            CloseResolutionKeyboardList();
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
                EnsureResolutionPointerRelay();
            }

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.onValueChanged.RemoveAllListeners();
                _fullscreenToggle.onValueChanged.AddListener(HandleDisplayFullscreenChanged);
            }

            RebindButton(_applyButton, ClickApply);
            RebindButton(_revertButton, ClickRevert);
            RebindButton(_languageCycleButton, ClickLanguageCycle);
        }

        private void RefreshControls()
        {
            if (_viewModel == null)
            {
                if (_displayStatusLabel != null)
                {
                    _displayStatusLabel.text = string.Empty;
                    _displayStatusLabel.gameObject.SetActive(false);
                }

                if (_previewCountdownLabel != null)
                {
                    _previewCountdownLabel.text = string.Empty;
                }

                if (_previewCountdownSlider != null)
                {
                    ApplyPreviewCountdownSlider(0f);
                }

                if (_languageLabel != null)
                {
                    _languageLabel.text = string.Empty;
                }

                if (_languageCycleButtonLabel != null)
                {
                    _languageCycleButtonLabel.text = string.Empty;
                }

                if (_languageCycleButton != null)
                {
                    _languageCycleButton.interactable = false;
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
                    _displayStatusLabel.gameObject.SetActive(_viewModel.IsDisplayStatusVisible);
                }

                if (_previewCountdownLabel != null)
                {
                    _previewCountdownLabel.text = _viewModel.PreviewCountdownText;
                }

                if (_previewCountdownSlider != null)
                {
                    var normalized = Mathf.Clamp01(_viewModel.PreviewCountdownNormalized);
                    ApplyPreviewCountdownSlider(normalized);
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
                    if (IsResolutionKeyboardListOpen)
                    {
                        SetResolutionKeyboardHighlight(Mathf.Clamp(
                            _resolutionKeyboardHighlightedIndex,
                            0,
                            Math.Max(0, ResolutionOptionCount - 1)));
                    }
                }

                if (_fullscreenToggle != null)
                {
                    _fullscreenToggle.SetIsOnWithoutNotify(_viewModel.IsFullscreenEnabled);
                    _fullscreenToggle.interactable = !_viewModel.IsDisplayPreviewActive;
                }

                if (_languageLabel != null)
                {
                    _languageLabel.text = _viewModel.LanguageLabelText;
                }

                if (_languageCycleButtonLabel != null)
                {
                    _languageCycleButtonLabel.text = _viewModel.CurrentLanguageText;
                }

                if (_languageCycleButton != null)
                {
                    _languageCycleButton.interactable = _viewModel.IsLanguageSelectionAvailable;
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
            UnbindButton(_languageCycleButton, ClickLanguageCycle);
        }

        private void ShowResolutionHoverHint()
        {
            if (!_isVisible || _viewModel == null || !HasResolutionHoverHintCopy())
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

        private void RefreshNativeResolutionKeyboardHighlight()
        {
            if (!TryGetNativeResolutionDropdownList(out var nativeList))
            {
                return;
            }

            var toggles = nativeList.GetComponentsInChildren<Toggle>(false);
            for (var i = 0; i < toggles.Length; i++)
            {
                var targetGraphic = toggles[i] != null ? toggles[i].targetGraphic as Graphic : null;
                if (targetGraphic == null)
                {
                    continue;
                }

                targetGraphic.color = i == _resolutionKeyboardHighlightedIndex
                    ? new Color(0.95f, 0.82f, 0.28f, 0.42f)
                    : new Color(1f, 1f, 1f, 0.08f);
            }
        }

        private bool TryGetNativeResolutionDropdownList(out RectTransform dropdownList)
        {
            dropdownList = null;
            if (_resolutionDropdown == null)
            {
                return false;
            }

            var found = _resolutionDropdown.transform.Find("Dropdown List") as RectTransform;
            if (found == null)
            {
                return false;
            }

            var popupCanvas = found.GetComponent<Canvas>();
            if (popupCanvas == null || !popupCanvas.overrideSorting)
            {
                return false;
            }

            dropdownList = found;
            return true;
        }

        private void EnsureResolutionPointerRelay()
        {
            if (_resolutionDropdown == null)
            {
                return;
            }

            var relay = _resolutionDropdown.GetComponent<ResolutionKeyboardPointerRelay>();
            if (relay == null)
            {
                relay = _resolutionDropdown.gameObject.AddComponent<ResolutionKeyboardPointerRelay>();
            }

            relay.PointerDown = CloseResolutionKeyboardList;
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
                             HasResolutionHoverHintCopy();
            _resolutionHoverHintRoot.gameObject.SetActive(shouldShow);
        }

        private bool HasResolutionHoverHintCopy()
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

        private void ApplyPreviewCountdownSlider(float normalized)
        {
            if (_previewCountdownSlider == null)
            {
                return;
            }

            _previewCountdownSlider.minValue = 0f;
            _previewCountdownSlider.maxValue = 1f;
            _previewCountdownSlider.wholeNumbers = false;
            _previewCountdownSlider.interactable = false;
            _previewCountdownSlider.SetValueWithoutNotify(Mathf.Clamp01(normalized));
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

        private sealed class ResolutionKeyboardPointerRelay : MonoBehaviour, IPointerDownHandler
        {
            public Action PointerDown { get; set; }

            public void OnPointerDown(PointerEventData eventData)
            {
                PointerDown?.Invoke();
            }
        }
    }
}
