using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsAudioView : MonoBehaviour
    {
        private readonly Dictionary<AudioSettingsChannel, AudioControlWidgets> _audioControls = new();
        private bool _isVisible;
        private SettingsAudioViewModel _viewModel;

        public event Action<AudioSettingsChannel, float> VolumeChanged;

        public event Action<AudioSettingsChannel, bool> MuteChanged;

        public event Action InteractionCompleted;

        public void Bind(SettingsAudioViewModel viewModel)
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

        public void CommitInteraction(AudioSettingsChannel channel)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.InteractionRelay != null)
            {
                widgets.InteractionRelay.RaiseInteractionCompleted();
            }
        }

        public void SetIsVisible(bool isVisible)
        {
            _isVisible = isVisible;
            RefreshView();
        }

        public void SetMuted(AudioSettingsChannel channel, bool isMuted)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Toggle != null)
            {
                widgets.Toggle.isOn = isMuted;
            }
        }

        public void SetVolume(AudioSettingsChannel channel, float value)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Slider != null)
            {
                widgets.Slider.value = Mathf.Clamp01(value);
            }
        }

        private void OnEnable()
        {
            EnsureAudioControls();
            RebindAudioControls();
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindAudioControls();
        }

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindAudioControls();
        }

        private void ApplyLayout()
        {
            LayoutAudioRow(AudioSettingsChannel.Main, new Vector2(0f, 0f));
            LayoutAudioRow(AudioSettingsChannel.Bgm, new Vector2(0f, -46f));
            LayoutAudioRow(AudioSettingsChannel.Sfx, new Vector2(0f, -92f));
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

        private RectTransform CreateAudioRow(string rootName)
        {
            var rowObject = new GameObject(rootName, typeof(RectTransform));
            rowObject.transform.SetParent(transform, false);
            var rowRect = rowObject.GetComponent<RectTransform>();

            CreateLabel("Label", rowRect);
            CreateSlider(rowRect);
            CreateValueLabel(rowRect);
            CreateToggle(rowRect);
            return rowRect;
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

        private void EnsureAudioControl(AudioSettingsChannel channel, string rootName)
        {
            if (_audioControls.ContainsKey(channel))
            {
                return;
            }

            var rootTransform = transform.Find(rootName) as RectTransform;
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

        private void EnsureAudioControls()
        {
            EnsureAudioControl(AudioSettingsChannel.Main, "MainAudioRow");
            EnsureAudioControl(AudioSettingsChannel.Bgm, "BgmAudioRow");
            EnsureAudioControl(AudioSettingsChannel.Sfx, "SfxAudioRow");
        }

        private void HandleAudioInteractionCompleted()
        {
            if (!_isVisible)
            {
                return;
            }

            InteractionCompleted?.Invoke();
        }

        private void HandleAudioSliderChanged(AudioSettingsChannel channel, float value)
        {
            if (!_isVisible)
            {
                return;
            }

            VolumeChanged?.Invoke(channel, value);
        }

        private void HandleAudioToggleChanged(AudioSettingsChannel channel, bool isMuted)
        {
            if (!_isVisible)
            {
                return;
            }

            MuteChanged?.Invoke(channel, isMuted);
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
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

        private void RefreshView()
        {
            EnsureAudioControls();
            RebindAudioControls();
            ApplyLayout();

            if (_viewModel == null)
            {
                return;
            }

            RefreshAudioControl(AudioSettingsChannel.Main, _viewModel.MainAudio);
            RefreshAudioControl(AudioSettingsChannel.Bgm, _viewModel.BgmAudio);
            RefreshAudioControl(AudioSettingsChannel.Sfx, _viewModel.SfxAudio);
        }

        private Font ResolveFont()
        {
            var textComponents = GetComponentsInParent<Text>(true);
            for (var i = 0; i < textComponents.Length; i++)
            {
                if (textComponents[i] != null && textComponents[i].font != null)
                {
                    return textComponents[i].font;
                }
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            public void OnEndDrag(PointerEventData eventData)
            {
                InteractionCompleted?.Invoke();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                InteractionCompleted?.Invoke();
            }

            public void RaiseInteractionCompleted()
            {
                InteractionCompleted?.Invoke();
            }
        }
    }
}
