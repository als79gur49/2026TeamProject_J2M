using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _tooltipStatusLabel;
        [SerializeField] private Text _largeTextStatusLabel;
        [SerializeField] private Button _tooltipInfoButton;
        [SerializeField] private Button _tooltipToggleButton;
        [SerializeField] private Button _largeTextToggleButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _tooltipToggleButtonLabel;
        [SerializeField] private Text _largeTextToggleButtonLabel;
        [SerializeField] private Text _backButtonLabel;

        private readonly Dictionary<AudioSettingsChannel, AudioControlWidgets> _audioControls = new();
        private DisplayControlWidgets _displayControls;
        private bool _isRefreshingDisplayControls;
        private SettingsScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action<AudioSettingsChannel, float> AudioVolumeChanged;

        public event Action<AudioSettingsChannel, bool> AudioMuteChanged;

        public event Action AudioInteractionCompleted;

        public event Action TooltipToggleRequested;

        public event Action TooltipInfoRequested;

        public event Action LargeTextToggleRequested;

        public event Action<int> DisplayResolutionChanged;

        public event Action<bool> DisplayFullscreenToggled;

        public event Action DisplayApplyRequested;

        public event Action DisplayRevertRequested;

        public event Action BackRequested;

        public string CurrentDisplayValueText =>
            _displayControls?.CurrentDisplayValue != null ? _displayControls.CurrentDisplayValue.text : string.Empty;

        public string DisplayStatusText =>
            _displayControls?.DisplayStatusLabel != null ? _displayControls.DisplayStatusLabel.text : string.Empty;

        public bool IsDisplayApplyInteractable =>
            _displayControls?.ApplyButton != null && _displayControls.ApplyButton.interactable;

        public bool IsDisplayRevertInteractable =>
            _displayControls?.RevertButton != null && _displayControls.RevertButton.interactable;

        public int SelectedDisplayResolutionIndex =>
            _displayControls?.ResolutionDropdown != null ? _displayControls.ResolutionDropdown.value : 0;

        public bool IsDisplayFullscreenOn =>
            _displayControls?.FullscreenToggle != null && _displayControls.FullscreenToggle.isOn;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(SettingsScreenViewModel viewModel)
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickTooltipToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            TooltipToggleRequested?.Invoke();
        }

        public void ClickTooltipInfo()
        {
            if (!IsVisible)
            {
                return;
            }

            TooltipInfoRequested?.Invoke();
        }

        public void ClickLargeTextToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            LargeTextToggleRequested?.Invoke();
        }

        public void SetAudioVolume(AudioSettingsChannel channel, float value)
        {
            if (!IsVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Slider != null)
            {
                widgets.Slider.value = Mathf.Clamp01(value);
            }
        }

        public void CommitAudioInteraction(AudioSettingsChannel channel)
        {
            if (!IsVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.InteractionRelay != null)
            {
                widgets.InteractionRelay.RaiseInteractionCompleted();
            }
        }

        public void SetAudioMuted(AudioSettingsChannel channel, bool isMuted)
        {
            if (!IsVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Toggle != null)
            {
                widgets.Toggle.isOn = isMuted;
            }
        }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

        public void SelectDisplayResolution(int index)
        {
            if (!IsVisible || _displayControls?.ResolutionDropdown == null)
            {
                return;
            }

            _displayControls.ResolutionDropdown.value = index;
        }

        public void SetDisplayFullscreen(bool isFullscreen)
        {
            if (!IsVisible || _displayControls?.FullscreenToggle == null)
            {
                return;
            }

            _displayControls.FullscreenToggle.isOn = isFullscreen;
        }

        public void ClickDisplayApply()
        {
            if (!IsVisible)
            {
                return;
            }

            DisplayApplyRequested?.Invoke();
        }

        public void ClickDisplayRevert()
        {
            if (!IsVisible)
            {
                return;
            }

            DisplayRevertRequested?.Invoke();
        }

        private void OnEnable()
        {
            EnsureAudioControls();
            EnsureDisplayControls();
            RebindButton(_tooltipInfoButton, ClickTooltipInfo);
            RebindButton(_tooltipToggleButton, ClickTooltipToggle);
            RebindButton(_largeTextToggleButton, ClickLargeTextToggle);
            RebindButton(_backButton, ClickBack);
            RebindAudioControls();
            RebindDisplayControls();
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindDisplayControls();
            UnbindAudioControls();
            UnbindButton(_tooltipInfoButton, ClickTooltipInfo);
            UnbindButton(_tooltipToggleButton, ClickTooltipToggle);
            UnbindButton(_largeTextToggleButton, ClickLargeTextToggle);
            UnbindButton(_backButton, ClickBack);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_tooltipStatusLabel, nameof(_tooltipStatusLabel));
            ValidateSerializedReference(_largeTextStatusLabel, nameof(_largeTextStatusLabel));
            ValidateSerializedReference(_tooltipInfoButton, nameof(_tooltipInfoButton));
            ValidateSerializedReference(_tooltipToggleButton, nameof(_tooltipToggleButton));
            ValidateSerializedReference(_largeTextToggleButton, nameof(_largeTextToggleButton));
            ValidateSerializedReference(_backButton, nameof(_backButton));
            ValidateSerializedReference(_tooltipToggleButtonLabel, nameof(_tooltipToggleButtonLabel));
            ValidateSerializedReference(_largeTextToggleButtonLabel, nameof(_largeTextToggleButtonLabel));
            ValidateSerializedReference(_backButtonLabel, nameof(_backButtonLabel));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindDisplayControls();
            UnbindAudioControls();
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            EnsureAudioControls();
            EnsureDisplayControls();
            RebindAudioControls();
            RebindDisplayControls();
            ApplySettingsLayout();

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_tooltipStatusLabel != null)
            {
                _tooltipStatusLabel.text = _viewModel.TooltipStatusText;
            }

            if (_largeTextStatusLabel != null)
            {
                _largeTextStatusLabel.text = _viewModel.LargeTextStatusText;
            }

            if (_tooltipToggleButtonLabel != null)
            {
                _tooltipToggleButtonLabel.text = _viewModel.TooltipToggleLabel;
            }

            if (_largeTextToggleButtonLabel != null)
            {
                _largeTextToggleButtonLabel.text = _viewModel.LargeTextToggleLabel;
            }

            if (_backButtonLabel != null)
            {
                _backButtonLabel.text = _viewModel.BackLabel;
            }

            RefreshAudioControl(AudioSettingsChannel.Main, _viewModel.MainAudio);
            RefreshAudioControl(AudioSettingsChannel.Bgm, _viewModel.BgmAudio);
            RefreshAudioControl(AudioSettingsChannel.Sfx, _viewModel.SfxAudio);
            RefreshDisplayControls();
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

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(SettingsScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif

        private void EnsureAudioControls()
        {
            if (_root == null)
            {
                return;
            }

            EnsureAudioControl(AudioSettingsChannel.Main, "MainAudioRow");
            EnsureAudioControl(AudioSettingsChannel.Bgm, "BgmAudioRow");
            EnsureAudioControl(AudioSettingsChannel.Sfx, "SfxAudioRow");
        }

        private void EnsureAudioControl(AudioSettingsChannel channel, string rootName)
        {
            if (_audioControls.ContainsKey(channel))
            {
                return;
            }

            var rootTransform = _root.transform.Find(rootName) as RectTransform;
            if (rootTransform == null)
            {
                rootTransform = CreateAudioRow(rootName);
            }

            _audioControls[channel] = new AudioControlWidgets(
                rootTransform,
                rootTransform.Find("Label")?.GetComponent<Text>(),
                rootTransform.Find("Value")?.GetComponent<Text>(),
                rootTransform.GetComponentInChildren<Slider>(true),
                rootTransform.GetComponentInChildren<Toggle>(true),
                rootTransform.GetComponentInChildren<SliderInteractionRelay>(true));
        }

        private RectTransform CreateAudioRow(string rootName)
        {
            var rowObject = new GameObject(rootName, typeof(RectTransform));
            rowObject.transform.SetParent(_root.transform, false);
            var rowRect = rowObject.GetComponent<RectTransform>();

            CreateLabel("Label", rowRect);
            CreateSlider(rowRect);
            CreateValueLabel(rowRect);
            CreateToggle(rowRect);
            return rowRect;
        }

        private void ApplySettingsLayout()
        {
            if (_root == null)
            {
                return;
            }

            var rootRect = _root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                var size = rootRect.sizeDelta;
                size.x = Mathf.Max(size.x, 460f);
                size.y = Mathf.Max(size.y, 600f);
                rootRect.sizeDelta = size;
            }

            LayoutRect(_titleLabel != null ? _titleLabel.rectTransform : null, new Vector2(16f, -16f), new Vector2(428f, 24f));

            LayoutAudioRow(AudioSettingsChannel.Main, new Vector2(24f, -58f));
            LayoutAudioRow(AudioSettingsChannel.Bgm, new Vector2(24f, -104f));
            LayoutAudioRow(AudioSettingsChannel.Sfx, new Vector2(24f, -150f));

            if (_displayControls != null)
            {
                LayoutRect(_displayControls.SectionTitle.rectTransform, new Vector2(24f, -194f), new Vector2(160f, 22f));
                LayoutRect(_displayControls.CurrentDisplayLabel.rectTransform, new Vector2(24f, -226f), new Vector2(120f, 22f));
                LayoutRect(_displayControls.CurrentDisplayValue.rectTransform, new Vector2(156f, -226f), new Vector2(256f, 22f));
                LayoutRect(_displayControls.ResolutionLabel.rectTransform, new Vector2(24f, -264f), new Vector2(120f, 22f));
                LayoutRect(_displayControls.ResolutionDropdown.GetComponent<RectTransform>(), new Vector2(156f, -258f), new Vector2(204f, 30f));
                LayoutRect(_displayControls.FullscreenLabel.rectTransform, new Vector2(24f, -304f), new Vector2(160f, 22f));
                LayoutRect(_displayControls.FullscreenToggle.GetComponent<RectTransform>(), new Vector2(220f, -298f), new Vector2(140f, 28f));
                LayoutRect(_displayControls.DisplayStatusLabel.rectTransform, new Vector2(24f, -346f), new Vector2(388f, 44f));
                LayoutRect(_displayControls.ApplyButton.GetComponent<RectTransform>(), new Vector2(98f, -402f), new Vector2(104f, 30f));
                LayoutRect(_displayControls.RevertButton.GetComponent<RectTransform>(), new Vector2(244f, -402f), new Vector2(104f, 30f));
            }

            LayoutRect(_tooltipStatusLabel != null ? _tooltipStatusLabel.rectTransform : null, new Vector2(24f, -458f), new Vector2(160f, 22f));
            LayoutRect(_tooltipInfoButton != null ? _tooltipInfoButton.GetComponent<RectTransform>() : null, new Vector2(188f, -452f), new Vector2(24f, 28f));
            LayoutRect(_tooltipToggleButton != null ? _tooltipToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -452f), new Vector2(140f, 28f));
            LayoutRect(_largeTextStatusLabel != null ? _largeTextStatusLabel.rectTransform : null, new Vector2(24f, -504f), new Vector2(160f, 22f));
            LayoutRect(_largeTextToggleButton != null ? _largeTextToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -498f), new Vector2(140f, 28f));
            LayoutRect(_backButton != null ? _backButton.GetComponent<RectTransform>() : null, new Vector2(181f, -554f), new Vector2(98f, 30f));
        }

        private void LayoutAudioRow(AudioSettingsChannel channel, Vector2 anchoredPosition)
        {
            if (!_audioControls.TryGetValue(channel, out var widgets))
            {
                return;
            }

            LayoutRect(widgets.RowRoot, anchoredPosition, new Vector2(412f, 32f));
            LayoutRect(widgets.Label != null ? widgets.Label.rectTransform : null, new Vector2(0f, 0f), new Vector2(116f, 24f));
            LayoutRect(
                widgets.Slider != null ? widgets.Slider.GetComponent<RectTransform>() : null,
                new Vector2(126f, -2f),
                new Vector2(172f, 20f));
            LayoutRect(widgets.Value != null ? widgets.Value.rectTransform : null, new Vector2(306f, 0f), new Vector2(54f, 24f));
            LayoutRect(
                widgets.Toggle != null ? widgets.Toggle.GetComponent<RectTransform>() : null,
                new Vector2(362f, -2f),
                new Vector2(50f, 24f));
        }

        private void RebindAudioControls()
        {
            BindAudioControl(AudioSettingsChannel.Main);
            BindAudioControl(AudioSettingsChannel.Bgm);
            BindAudioControl(AudioSettingsChannel.Sfx);
        }

        private void BindAudioControl(AudioSettingsChannel channel)
        {
            if (!_audioControls.TryGetValue(channel, out var widgets))
            {
                return;
            }

            if (widgets.Slider != null)
            {
                widgets.Slider.onValueChanged.RemoveAllListeners();
                widgets.Slider.onValueChanged.AddListener(value => HandleAudioSliderChanged(channel, value));
            }

            if (widgets.Toggle != null)
            {
                widgets.Toggle.onValueChanged.RemoveAllListeners();
                widgets.Toggle.onValueChanged.AddListener(value => HandleAudioToggleChanged(channel, value));
            }

            if (widgets.InteractionRelay != null)
            {
                widgets.InteractionRelay.InteractionCompleted -= HandleAudioInteractionCompleted;
                widgets.InteractionRelay.InteractionCompleted += HandleAudioInteractionCompleted;
            }
        }

        private void UnbindAudioControls()
        {
            foreach (var pair in _audioControls)
            {
                var widgets = pair.Value;
                if (widgets.Slider != null)
                {
                    widgets.Slider.onValueChanged.RemoveAllListeners();
                }

                if (widgets.Toggle != null)
                {
                    widgets.Toggle.onValueChanged.RemoveAllListeners();
                }

                if (widgets.InteractionRelay != null)
                {
                    widgets.InteractionRelay.InteractionCompleted -= HandleAudioInteractionCompleted;
                }
            }
        }

        private void RebindDisplayControls()
        {
            if (_displayControls == null)
            {
                return;
            }

            if (_displayControls.ResolutionDropdown != null)
            {
                _displayControls.ResolutionDropdown.onValueChanged.RemoveAllListeners();
                _displayControls.ResolutionDropdown.onValueChanged.AddListener(HandleDisplayResolutionChanged);
            }

            if (_displayControls.FullscreenToggle != null)
            {
                _displayControls.FullscreenToggle.onValueChanged.RemoveAllListeners();
                _displayControls.FullscreenToggle.onValueChanged.AddListener(HandleDisplayFullscreenChanged);
            }

            RebindButton(_displayControls.ApplyButton, ClickDisplayApply);
            RebindButton(_displayControls.RevertButton, ClickDisplayRevert);
        }

        private void UnbindDisplayControls()
        {
            if (_displayControls == null)
            {
                return;
            }

            if (_displayControls.ResolutionDropdown != null)
            {
                _displayControls.ResolutionDropdown.onValueChanged.RemoveAllListeners();
            }

            if (_displayControls.FullscreenToggle != null)
            {
                _displayControls.FullscreenToggle.onValueChanged.RemoveAllListeners();
            }

            UnbindButton(_displayControls.ApplyButton, ClickDisplayApply);
            UnbindButton(_displayControls.RevertButton, ClickDisplayRevert);
        }

        private void HandleAudioSliderChanged(AudioSettingsChannel channel, float value)
        {
            if (!IsVisible)
            {
                return;
            }

            AudioVolumeChanged?.Invoke(channel, value);
        }

        private void HandleAudioToggleChanged(AudioSettingsChannel channel, bool isMuted)
        {
            if (!IsVisible)
            {
                return;
            }

            AudioMuteChanged?.Invoke(channel, isMuted);
        }

        private void HandleAudioInteractionCompleted()
        {
            if (!IsVisible)
            {
                return;
            }

            AudioInteractionCompleted?.Invoke();
        }

        private void HandleDisplayResolutionChanged(int modeIndex)
        {
            if (!IsVisible || _isRefreshingDisplayControls)
            {
                return;
            }

            DisplayResolutionChanged?.Invoke(modeIndex);
        }

        private void HandleDisplayFullscreenChanged(bool isFullscreen)
        {
            if (!IsVisible || _isRefreshingDisplayControls)
            {
                return;
            }

            DisplayFullscreenToggled?.Invoke(isFullscreen);
        }

        private void RefreshAudioControl(AudioSettingsChannel channel, AudioSettingsRowViewModel rowViewModel)
        {
            if (!_audioControls.TryGetValue(channel, out var widgets))
            {
                return;
            }

            if (widgets.Label != null)
            {
                widgets.Label.text = rowViewModel.LabelText;
            }

            if (widgets.Value != null)
            {
                widgets.Value.text = rowViewModel.ValueText;
            }

            if (widgets.Slider != null)
            {
                widgets.Slider.SetValueWithoutNotify(rowViewModel.NormalizedValue);
            }

            if (widgets.Toggle != null)
            {
                widgets.Toggle.SetIsOnWithoutNotify(rowViewModel.IsMuted);
            }
        }

        private void RefreshDisplayControls()
        {
            if (_displayControls == null || _viewModel == null)
            {
                return;
            }

            _isRefreshingDisplayControls = true;
            try
            {
                _displayControls.SectionTitle.text = _viewModel.DisplaySectionTitle;
                _displayControls.CurrentDisplayLabel.text = _viewModel.CurrentDisplayLabel;
                _displayControls.CurrentDisplayValue.text = _viewModel.CurrentDisplayValueText;
                _displayControls.ResolutionLabel.text = _viewModel.ResolutionLabel;
                _displayControls.FullscreenLabel.text = _viewModel.FullscreenLabel;
                _displayControls.DisplayStatusLabel.text = _viewModel.DisplayStatusText;
                _displayControls.ApplyButton.interactable = _viewModel.IsDisplayApplyInteractable;
                _displayControls.RevertButton.interactable = _viewModel.IsDisplayRevertInteractable;

                if (_displayControls.ApplyButtonLabel != null)
                {
                    _displayControls.ApplyButtonLabel.text = _viewModel.DisplayApplyLabel;
                }

                if (_displayControls.RevertButtonLabel != null)
                {
                    _displayControls.RevertButtonLabel.text = _viewModel.DisplayRevertLabel;
                }

                if (_displayControls.ResolutionDropdown != null)
                {
                    _displayControls.ResolutionDropdown.ClearOptions();
                    _displayControls.ResolutionDropdown.AddOptions(new List<string>(_viewModel.ResolutionOptionTexts));
                    _displayControls.ResolutionDropdown.SetValueWithoutNotify(
                        Mathf.Clamp(_viewModel.SelectedResolutionIndex, 0, Math.Max(0, _viewModel.ResolutionOptionTexts.Count - 1)));
                    _displayControls.ResolutionDropdown.interactable = !_viewModel.IsDisplayPreviewActive;
                }

                if (_displayControls.FullscreenToggle != null)
                {
                    _displayControls.FullscreenToggle.SetIsOnWithoutNotify(_viewModel.IsFullscreenEnabled);
                    _displayControls.FullscreenToggle.interactable = !_viewModel.IsDisplayPreviewActive;
                }
            }
            finally
            {
                _isRefreshingDisplayControls = false;
            }
        }

        private void EnsureDisplayControls()
        {
            if (_root == null || _displayControls != null)
            {
                return;
            }

            var rootRect = _root.transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            var sectionTitle = FindText(rootRect, "DisplaySectionTitle") ?? CreateLabel("DisplaySectionTitle", rootRect);
            var currentDisplayLabel = FindText(rootRect, "CurrentDisplayLabel") ?? CreateLabel("CurrentDisplayLabel", rootRect);
            var currentDisplayValue = FindText(rootRect, "CurrentDisplayValue") ?? CreateLabel("CurrentDisplayValue", rootRect);
            var resolutionLabel = FindText(rootRect, "ResolutionLabel") ?? CreateLabel("ResolutionLabel", rootRect);
            var resolutionDropdown = FindDropdown(rootRect, "ResolutionDropdown") ?? CreateDropdownControl(rootRect);
            var fullscreenLabel = FindText(rootRect, "FullscreenLabel") ?? CreateLabel("FullscreenLabel", rootRect);
            var fullscreenToggle = FindToggle(rootRect, "FullscreenToggle") ?? CreateStandaloneToggle(rootRect, "FullscreenToggle", "On");
            var displayStatusLabel = FindText(rootRect, "DisplayStatus") ?? CreateStatusLabel(rootRect, "DisplayStatus");
            var applyButtonWidgets = FindButton(rootRect, "DisplayApplyButton") ?? CreateButtonControl(rootRect, "DisplayApplyButton", "Apply");
            var revertButtonWidgets = FindButton(rootRect, "DisplayRevertButton") ?? CreateButtonControl(rootRect, "DisplayRevertButton", "Revert");

            sectionTitle.fontSize = 15;
            currentDisplayLabel.fontSize = 14;
            currentDisplayValue.fontSize = 14;
            currentDisplayValue.alignment = TextAnchor.MiddleLeft;
            resolutionLabel.fontSize = 14;
            fullscreenLabel.fontSize = 14;
            displayStatusLabel.fontSize = 12;
            displayStatusLabel.alignment = TextAnchor.UpperLeft;
            displayStatusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            displayStatusLabel.verticalOverflow = VerticalWrapMode.Overflow;

            _displayControls = new DisplayControlWidgets(
                sectionTitle,
                currentDisplayLabel,
                currentDisplayValue,
                resolutionLabel,
                resolutionDropdown,
                fullscreenLabel,
                fullscreenToggle,
                displayStatusLabel,
                applyButtonWidgets.Button,
                applyButtonWidgets.Label,
                revertButtonWidgets.Button,
                revertButtonWidgets.Label);
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

        private Text CreateLabel(string name, RectTransform parent)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            LayoutRect(rectTransform, Vector2.zero, new Vector2(120f, 24f));

            var text = labelObject.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private Text CreateValueLabel(RectTransform parent)
        {
            var valueObject = new GameObject("Value", typeof(RectTransform), typeof(Text));
            valueObject.transform.SetParent(parent, false);

            var rectTransform = valueObject.GetComponent<RectTransform>();
            LayoutRect(rectTransform, new Vector2(306f, 0f), new Vector2(54f, 24f));

            var text = valueObject.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = 13;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        private Slider CreateSlider(RectTransform parent)
        {
            var sliderObject = new GameObject(
                "Slider",
                typeof(RectTransform),
                typeof(Image),
                typeof(Slider),
                typeof(SliderInteractionRelay));
            sliderObject.transform.SetParent(parent, false);

            var sliderRect = sliderObject.GetComponent<RectTransform>();
            LayoutRect(sliderRect, new Vector2(126f, -2f), new Vector2(172f, 20f));

            var sliderBackground = sliderObject.GetComponent<Image>();
            sliderBackground.color = new Color(0.12f, 0.16f, 0.21f, 1f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(8f, 6f);
            fillAreaRect.offsetMax = new Vector2(-8f, -6f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.24f, 0.68f, 0.87f, 1f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(12f, 20f);
            handle.GetComponent<Image>().color = Color.white;

            var slider = sliderObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.value = 1f;
            return slider;
        }

        private Toggle CreateToggle(RectTransform parent)
        {
            var toggleObject = new GameObject("MuteToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);

            var toggleRect = toggleObject.GetComponent<RectTransform>();
            LayoutRect(toggleRect, new Vector2(362f, -2f), new Vector2(50f, 24f));

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            LayoutRect(backgroundRect, Vector2.zero, new Vector2(18f, 18f));
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            var backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.20f, 0.25f, 0.34f, 1f);

            var checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            var checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;
            var checkmarkImage = checkmarkObject.GetComponent<Image>();
            checkmarkImage.color = new Color(0.24f, 0.68f, 0.87f, 1f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(toggleObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            LayoutRect(labelRect, new Vector2(22f, 0f), new Vector2(28f, 24f));
            var labelText = labelObject.GetComponent<Text>();
            labelText.font = ResolveFont();
            labelText.fontSize = 12;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.text = "Mute";

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            return toggle;
        }

        private Toggle CreateStandaloneToggle(RectTransform parent, string objectName, string stateLabelText)
        {
            var toggle = CreateToggle(parent);
            toggle.name = objectName;

            var label = toggle.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = stateLabelText ?? string.Empty;
                label.fontSize = 12;
            }

            return toggle;
        }

        private Text CreateStatusLabel(RectTransform parent, string objectName)
        {
            var label = CreateLabel(objectName, parent);
            label.fontSize = 12;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private ButtonWidgets CreateButtonControl(RectTransform parent, string objectName, string labelText)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            LayoutRect(buttonRect, Vector2.zero, new Vector2(104f, 30f));

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.34f, 1f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var label = CreateLabel("Label", buttonRect);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = labelText ?? string.Empty;
            label.resizeTextForBestFit = false;
            LayoutRect(label.rectTransform, Vector2.zero, new Vector2(104f, 30f));

            return new ButtonWidgets(button, label);
        }

        private Dropdown CreateDropdownControl(RectTransform parent)
        {
            var dropdownObject = new GameObject("ResolutionDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            dropdownObject.transform.SetParent(parent, false);
            var dropdownRect = dropdownObject.GetComponent<RectTransform>();
            LayoutRect(dropdownRect, Vector2.zero, new Vector2(204f, 30f));

            var backgroundImage = dropdownObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.14f, 0.18f, 0.26f, 1f);

            var caption = CreateLabel("Label", dropdownRect);
            caption.alignment = TextAnchor.MiddleLeft;
            caption.fontSize = 13;
            LayoutRect(caption.rectTransform, new Vector2(8f, -4f), new Vector2(160f, 22f));

            var arrow = CreateLabel("Arrow", dropdownRect);
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.fontSize = 14;
            arrow.text = "v";
            LayoutRect(arrow.rectTransform, new Vector2(172f, -4f), new Vector2(24f, 22f));

            var templateObject = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateObject.transform.SetParent(dropdownRect, false);
            templateObject.SetActive(false);
            var templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = new Vector2(1f, 1f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -32f);
            templateRect.sizeDelta = new Vector2(0f, 120f);
            templateObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 1f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(templateRect, false);
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 0.98f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportRect, false);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 24f);

            var itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemObject.transform.SetParent(contentRect, false);
            var itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 24f);

            var itemBackgroundObject = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBackgroundObject.transform.SetParent(itemRect, false);
            var itemBackgroundRect = itemBackgroundObject.GetComponent<RectTransform>();
            itemBackgroundRect.anchorMin = Vector2.zero;
            itemBackgroundRect.anchorMax = Vector2.one;
            itemBackgroundRect.offsetMin = Vector2.zero;
            itemBackgroundRect.offsetMax = Vector2.zero;
            var itemBackgroundImage = itemBackgroundObject.GetComponent<Image>();
            itemBackgroundImage.color = new Color(0.16f, 0.21f, 0.30f, 1f);

            var itemCheckmarkObject = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            itemCheckmarkObject.transform.SetParent(itemRect, false);
            var itemCheckmarkRect = itemCheckmarkObject.GetComponent<RectTransform>();
            itemCheckmarkRect.anchorMin = new Vector2(0f, 0.5f);
            itemCheckmarkRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckmarkRect.pivot = new Vector2(0f, 0.5f);
            itemCheckmarkRect.anchoredPosition = new Vector2(8f, 0f);
            itemCheckmarkRect.sizeDelta = new Vector2(16f, 16f);
            var itemCheckmarkImage = itemCheckmarkObject.GetComponent<Image>();
            itemCheckmarkImage.color = new Color(0.24f, 0.68f, 0.87f, 1f);

            var itemLabel = CreateLabel("Item Label", itemRect);
            itemLabel.fontSize = 13;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            LayoutRect(itemLabel.rectTransform, new Vector2(30f, -2f), new Vector2(160f, 22f));

            var itemToggle = itemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackgroundImage;
            itemToggle.graphic = itemCheckmarkImage;

            var scrollRect = templateObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.targetGraphic = backgroundImage;
            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            return dropdown;
        }

        private static Text FindText(RectTransform parent, string name)
        {
            return parent.Find(name)?.GetComponent<Text>();
        }

        private static Dropdown FindDropdown(RectTransform parent, string name)
        {
            return parent.Find(name)?.GetComponent<Dropdown>();
        }

        private static Toggle FindToggle(RectTransform parent, string name)
        {
            return parent.Find(name)?.GetComponent<Toggle>();
        }

        private static ButtonWidgets FindButton(RectTransform parent, string name)
        {
            var button = parent.Find(name)?.GetComponent<Button>();
            if (button == null)
            {
                return null;
            }

            return new ButtonWidgets(button, button.GetComponentInChildren<Text>(true));
        }

        private Font ResolveFont()
        {
            if (_titleLabel != null && _titleLabel.font != null)
            {
                return _titleLabel.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private readonly struct AudioControlWidgets
        {
            public AudioControlWidgets(
                RectTransform rowRoot,
                Text label,
                Text value,
                Slider slider,
                Toggle toggle,
                SliderInteractionRelay interactionRelay)
            {
                RowRoot = rowRoot;
                Label = label;
                Value = value;
                Slider = slider;
                Toggle = toggle;
                InteractionRelay = interactionRelay;
            }

            public RectTransform RowRoot { get; }

            public Text Label { get; }

            public Text Value { get; }

            public Slider Slider { get; }

            public Toggle Toggle { get; }

            public SliderInteractionRelay InteractionRelay { get; }
        }

        private sealed class DisplayControlWidgets
        {
            public DisplayControlWidgets(
                Text sectionTitle,
                Text currentDisplayLabel,
                Text currentDisplayValue,
                Text resolutionLabel,
                Dropdown resolutionDropdown,
                Text fullscreenLabel,
                Toggle fullscreenToggle,
                Text displayStatusLabel,
                Button applyButton,
                Text applyButtonLabel,
                Button revertButton,
                Text revertButtonLabel)
            {
                SectionTitle = sectionTitle;
                CurrentDisplayLabel = currentDisplayLabel;
                CurrentDisplayValue = currentDisplayValue;
                ResolutionLabel = resolutionLabel;
                ResolutionDropdown = resolutionDropdown;
                FullscreenLabel = fullscreenLabel;
                FullscreenToggle = fullscreenToggle;
                DisplayStatusLabel = displayStatusLabel;
                ApplyButton = applyButton;
                ApplyButtonLabel = applyButtonLabel;
                RevertButton = revertButton;
                RevertButtonLabel = revertButtonLabel;
            }

            public Text SectionTitle { get; }

            public Text CurrentDisplayLabel { get; }

            public Text CurrentDisplayValue { get; }

            public Text ResolutionLabel { get; }

            public Dropdown ResolutionDropdown { get; }

            public Text FullscreenLabel { get; }

            public Toggle FullscreenToggle { get; }

            public Text DisplayStatusLabel { get; }

            public Button ApplyButton { get; }

            public Text ApplyButtonLabel { get; }

            public Button RevertButton { get; }

            public Text RevertButtonLabel { get; }
        }

        private sealed class ButtonWidgets
        {
            public ButtonWidgets(Button button, Text label)
            {
                Button = button;
                Label = label;
            }

            public Button Button { get; }

            public Text Label { get; }
        }

        private sealed class SliderInteractionRelay : MonoBehaviour, IEndDragHandler, IPointerUpHandler
        {
            public event Action InteractionCompleted;

            public void RaiseInteractionCompleted()
            {
                InteractionCompleted?.Invoke();
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                InteractionCompleted?.Invoke();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                InteractionCompleted?.Invoke();
            }
        }
    }
}
