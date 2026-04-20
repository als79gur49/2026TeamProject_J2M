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
        private SettingsScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action<AudioSettingsChannel, float> AudioVolumeChanged;

        public event Action<AudioSettingsChannel, bool> AudioMuteChanged;

        public event Action AudioInteractionCompleted;

        public event Action TooltipToggleRequested;

        public event Action TooltipInfoRequested;

        public event Action LargeTextToggleRequested;

        public event Action BackRequested;

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

        private void OnEnable()
        {
            EnsureAudioControls();
            RebindButton(_tooltipInfoButton, ClickTooltipInfo);
            RebindButton(_tooltipToggleButton, ClickTooltipToggle);
            RebindButton(_largeTextToggleButton, ClickLargeTextToggle);
            RebindButton(_backButton, ClickBack);
            RebindAudioControls();
            RefreshView();
        }

        private void OnDisable()
        {
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
            RebindAudioControls();
            ApplyAudioFirstLayout();

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

        private void ApplyAudioFirstLayout()
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
                size.y = Mathf.Max(size.y, 360f);
                rootRect.sizeDelta = size;
            }

            LayoutRect(_titleLabel != null ? _titleLabel.rectTransform : null, new Vector2(16f, -16f), new Vector2(428f, 24f));

            LayoutAudioRow(AudioSettingsChannel.Main, new Vector2(24f, -58f));
            LayoutAudioRow(AudioSettingsChannel.Bgm, new Vector2(24f, -104f));
            LayoutAudioRow(AudioSettingsChannel.Sfx, new Vector2(24f, -150f));

            LayoutRect(_tooltipStatusLabel != null ? _tooltipStatusLabel.rectTransform : null, new Vector2(24f, -214f), new Vector2(160f, 22f));
            LayoutRect(_tooltipInfoButton != null ? _tooltipInfoButton.GetComponent<RectTransform>() : null, new Vector2(188f, -208f), new Vector2(24f, 28f));
            LayoutRect(_tooltipToggleButton != null ? _tooltipToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -208f), new Vector2(140f, 28f));
            LayoutRect(_largeTextStatusLabel != null ? _largeTextStatusLabel.rectTransform : null, new Vector2(24f, -260f), new Vector2(160f, 22f));
            LayoutRect(_largeTextToggleButton != null ? _largeTextToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -254f), new Vector2(140f, 28f));
            LayoutRect(_backButton != null ? _backButton.GetComponent<RectTransform>() : null, new Vector2(181f, -316f), new Vector2(98f, 30f));
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
