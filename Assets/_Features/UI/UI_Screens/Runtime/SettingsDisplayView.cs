using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsDisplayView : MonoBehaviour
    {
        private const string MissingControlsMessage =
            "Settings display section is missing required authored controls. Repair: open SettingsScreen.prefab and assign every SettingsDisplayView serialized reference.";

        [SerializeField] private Text _sectionTitle;
        [SerializeField] private Text _currentDisplayLabel;
        [SerializeField] private Text _currentDisplayValue;
        [SerializeField] private Text _resolutionLabel;
        [SerializeField] private Dropdown _resolutionDropdown;
        [SerializeField] private Text _fullscreenLabel;
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private Text _displayStatusLabel;
        [SerializeField] private Button _applyButton;
        [SerializeField] private Text _applyButtonLabel;
        [SerializeField] private Button _revertButton;
        [SerializeField] private Text _revertButtonLabel;

        private bool _isRefreshingDisplayControls;
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
            ValidateControl(_fullscreenLabel, nameof(_fullscreenLabel), issues);
            ValidateControl(_fullscreenToggle, nameof(_fullscreenToggle), issues);
            ValidateControl(_displayStatusLabel, nameof(_displayStatusLabel), issues);
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
            RefreshView();
        }

        private void OnEnable()
        {
            RebindDisplayControls();
            RefreshView();
        }

        private void OnDisable()
        {
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
            ValidateSerializedReference(_fullscreenLabel, nameof(_fullscreenLabel));
            ValidateSerializedReference(_fullscreenToggle, nameof(_fullscreenToggle));
            ValidateSerializedReference(_displayStatusLabel, nameof(_displayStatusLabel));
            ValidateSerializedReference(_applyButton, nameof(_applyButton));
            ValidateSerializedReference(_applyButtonLabel, nameof(_applyButtonLabel));
            ValidateSerializedReference(_revertButton, nameof(_revertButton));
            ValidateSerializedReference(_revertButtonLabel, nameof(_revertButtonLabel));
        }
#endif

        private void OnDestroy()
        {
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

        private void LayoutControls()
        {
            LayoutRect(_sectionTitle != null ? _sectionTitle.rectTransform : null, new Vector2(0f, 0f), new Vector2(160f, 22f));
            LayoutRect(_currentDisplayLabel != null ? _currentDisplayLabel.rectTransform : null, new Vector2(0f, -32f), new Vector2(120f, 22f));
            LayoutRect(_currentDisplayValue != null ? _currentDisplayValue.rectTransform : null, new Vector2(132f, -32f), new Vector2(256f, 22f));
            LayoutRect(_resolutionLabel != null ? _resolutionLabel.rectTransform : null, new Vector2(0f, -70f), new Vector2(120f, 22f));
            LayoutRect(_resolutionDropdown != null ? _resolutionDropdown.GetComponent<RectTransform>() : null, new Vector2(132f, -64f), new Vector2(204f, 30f));
            LayoutRect(_fullscreenLabel != null ? _fullscreenLabel.rectTransform : null, new Vector2(0f, -110f), new Vector2(160f, 22f));
            LayoutRect(_fullscreenToggle != null ? _fullscreenToggle.GetComponent<RectTransform>() : null, new Vector2(196f, -104f), new Vector2(140f, 28f));
            LayoutRect(_displayStatusLabel != null ? _displayStatusLabel.rectTransform : null, new Vector2(0f, -152f), new Vector2(388f, 44f));
            LayoutRect(_applyButton != null ? _applyButton.GetComponent<RectTransform>() : null, new Vector2(74f, -208f), new Vector2(104f, 30f));
            LayoutRect(_revertButton != null ? _revertButton.GetComponent<RectTransform>() : null, new Vector2(220f, -208f), new Vector2(104f, 30f));
        }

        private static void LayoutRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
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
                return;
            }

            _isRefreshingDisplayControls = true;
            try
            {
                if (_sectionTitle != null)
                {
                    _sectionTitle.text = _viewModel.DisplaySectionTitle;
                }

                if (_currentDisplayLabel != null)
                {
                    _currentDisplayLabel.text = _viewModel.CurrentDisplayLabel;
                }

                if (_currentDisplayValue != null)
                {
                    _currentDisplayValue.text = _viewModel.CurrentDisplayValueText;
                }

                if (_resolutionLabel != null)
                {
                    _resolutionLabel.text = _viewModel.ResolutionLabel;
                }

                if (_fullscreenLabel != null)
                {
                    _fullscreenLabel.text = _viewModel.FullscreenLabel;
                }

                if (_displayStatusLabel != null)
                {
                    _displayStatusLabel.text = _viewModel.DisplayStatusText;
                }

                if (_applyButton != null)
                {
                    _applyButton.interactable = _viewModel.IsDisplayApplyInteractable;
                }

                if (_revertButton != null)
                {
                    _revertButton.interactable = _viewModel.IsDisplayRevertInteractable;
                }

                if (_applyButtonLabel != null)
                {
                    _applyButtonLabel.text = _viewModel.DisplayApplyLabel;
                }

                if (_revertButtonLabel != null)
                {
                    _revertButtonLabel.text = _viewModel.DisplayRevertLabel;
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
            RebindDisplayControls();
            LayoutControls();
            RefreshControls();
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
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
