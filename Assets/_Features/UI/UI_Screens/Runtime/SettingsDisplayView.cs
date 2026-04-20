using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsDisplayView : MonoBehaviour
    {
        private DisplayControlWidgets _displayControls;
        private bool _isRefreshingDisplayControls;
        private bool _isVisible;
        private SettingsDisplayViewModel _viewModel;

        public event Action<int> ResolutionChanged;

        public event Action<bool> FullscreenToggled;

        public event Action ApplyRequested;

        public event Action RevertRequested;

        public string CurrentDisplayValueText =>
            _displayControls?.CurrentDisplayValue != null ? _displayControls.CurrentDisplayValue.text : string.Empty;

        public string DisplayStatusText =>
            _displayControls?.DisplayStatusLabel != null ? _displayControls.DisplayStatusLabel.text : string.Empty;

        public bool IsDisplayApplyInteractable =>
            _displayControls?.ApplyButton != null && _displayControls.ApplyButton.interactable;

        public bool IsDisplayRevertInteractable =>
            _displayControls?.RevertButton != null && _displayControls.RevertButton.interactable;

        public int SelectedResolutionIndex =>
            _displayControls?.ResolutionDropdown != null ? _displayControls.ResolutionDropdown.value : 0;

        public bool IsFullscreenOn =>
            _displayControls?.FullscreenToggle != null && _displayControls.FullscreenToggle.isOn;

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
            if (!_isVisible || _displayControls?.ResolutionDropdown == null)
            {
                return;
            }

            _displayControls.ResolutionDropdown.value = index;
        }

        public void SetFullscreen(bool isFullscreen)
        {
            if (!_isVisible || _displayControls?.FullscreenToggle == null)
            {
                return;
            }

            _displayControls.FullscreenToggle.isOn = isFullscreen;
        }

        public void SetIsVisible(bool isVisible)
        {
            _isVisible = isVisible;
            RefreshView();
        }

        private void OnEnable()
        {
            EnsureDisplayControls();
            RebindDisplayControls();
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindDisplayControls();
        }

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindDisplayControls();
        }

        private ButtonWidgets CreateButtonControl(string objectName, string labelText)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            LayoutRect(buttonRect, Vector2.zero, new Vector2(104f, 30f));

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.34f, 1f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var label = CreateLabel("Label");
            label.transform.SetParent(buttonRect, false);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = labelText ?? string.Empty;
            label.resizeTextForBestFit = false;
            LayoutRect(label.rectTransform, Vector2.zero, new Vector2(104f, 30f));

            return new ButtonWidgets(button, label);
        }

        private Dropdown CreateDropdownControl()
        {
            var dropdownObject = new GameObject("ResolutionDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            dropdownObject.transform.SetParent(transform, false);
            var dropdownRect = dropdownObject.GetComponent<RectTransform>();
            LayoutRect(dropdownRect, Vector2.zero, new Vector2(204f, 30f));

            var backgroundImage = dropdownObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.14f, 0.18f, 0.26f, 1f);

            var caption = CreateLabel("Label");
            caption.transform.SetParent(dropdownRect, false);
            caption.alignment = TextAnchor.MiddleLeft;
            caption.fontSize = 13;
            LayoutRect(caption.rectTransform, new Vector2(8f, -4f), new Vector2(160f, 22f));

            var arrow = CreateLabel("Arrow");
            arrow.transform.SetParent(dropdownRect, false);
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

            var itemLabel = CreateLabel("Item Label");
            itemLabel.transform.SetParent(itemRect, false);
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

        private Text CreateLabel(string name)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));

            var text = labelObject.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private Text CreateStatusLabel(string objectName)
        {
            var label = CreateLabel(objectName);
            label.fontSize = 12;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private Toggle CreateStandaloneToggle(string objectName, string stateLabelText)
        {
            var toggleObject = new GameObject(objectName, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(transform, false);

            var toggleRect = toggleObject.GetComponent<RectTransform>();
            LayoutRect(toggleRect, new Vector2(220f, -78f), new Vector2(140f, 28f));

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

            var label = CreateLabel("Label");
            label.transform.SetParent(toggleObject.transform, false);
            label.text = stateLabelText ?? string.Empty;
            label.fontSize = 12;
            LayoutRect(label.rectTransform, new Vector2(22f, 0f), new Vector2(98f, 24f));

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            return toggle;
        }

        private void EnsureDisplayControls()
        {
            if (_displayControls != null)
            {
                return;
            }

            var sectionTitle = FindText("DisplaySectionTitle") ?? CreateChildLabel("DisplaySectionTitle");
            var currentDisplayLabel = FindText("CurrentDisplayLabel") ?? CreateChildLabel("CurrentDisplayLabel");
            var currentDisplayValue = FindText("CurrentDisplayValue") ?? CreateChildLabel("CurrentDisplayValue");
            var resolutionLabel = FindText("ResolutionLabel") ?? CreateChildLabel("ResolutionLabel");
            var resolutionDropdown = FindDropdown("ResolutionDropdown") ?? CreateDropdownControl();
            var fullscreenLabel = FindText("FullscreenLabel") ?? CreateChildLabel("FullscreenLabel");
            var fullscreenToggle = FindToggle("FullscreenToggle") ?? CreateStandaloneToggle("FullscreenToggle", "On");
            var displayStatusLabel = FindText("DisplayStatus") ?? CreateChildStatusLabel("DisplayStatus");
            var applyButtonWidgets = FindButton("DisplayApplyButton") ?? CreateButtonControl("DisplayApplyButton", "Apply");
            var revertButtonWidgets = FindButton("DisplayRevertButton") ?? CreateButtonControl("DisplayRevertButton", "Revert");

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
            if (_displayControls == null)
            {
                return;
            }

            LayoutRect(_displayControls.SectionTitle.rectTransform, new Vector2(0f, 0f), new Vector2(160f, 22f));
            LayoutRect(_displayControls.CurrentDisplayLabel.rectTransform, new Vector2(0f, -32f), new Vector2(120f, 22f));
            LayoutRect(_displayControls.CurrentDisplayValue.rectTransform, new Vector2(132f, -32f), new Vector2(256f, 22f));
            LayoutRect(_displayControls.ResolutionLabel.rectTransform, new Vector2(0f, -70f), new Vector2(120f, 22f));
            LayoutRect(_displayControls.ResolutionDropdown.GetComponent<RectTransform>(), new Vector2(132f, -64f), new Vector2(204f, 30f));
            LayoutRect(_displayControls.FullscreenLabel.rectTransform, new Vector2(0f, -110f), new Vector2(160f, 22f));
            LayoutRect(_displayControls.FullscreenToggle.GetComponent<RectTransform>(), new Vector2(196f, -104f), new Vector2(140f, 28f));
            LayoutRect(_displayControls.DisplayStatusLabel.rectTransform, new Vector2(0f, -152f), new Vector2(388f, 44f));
            LayoutRect(_displayControls.ApplyButton.GetComponent<RectTransform>(), new Vector2(74f, -208f), new Vector2(104f, 30f));
            LayoutRect(_displayControls.RevertButton.GetComponent<RectTransform>(), new Vector2(220f, -208f), new Vector2(104f, 30f));
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

        private Text CreateChildLabel(string objectName)
        {
            var label = CreateLabel(objectName);
            label.transform.SetParent(transform, false);
            return label;
        }

        private Text CreateChildStatusLabel(string objectName)
        {
            var label = CreateStatusLabel(objectName);
            label.transform.SetParent(transform, false);
            return label;
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

            RebindButton(_displayControls.ApplyButton, ClickApply);
            RebindButton(_displayControls.RevertButton, ClickRevert);
        }

        private void RefreshControls()
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

        private void RefreshView()
        {
            EnsureDisplayControls();
            RebindDisplayControls();
            LayoutControls();
            RefreshControls();
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

        private Text FindText(string name)
        {
            return transform.Find(name)?.GetComponent<Text>();
        }

        private Dropdown FindDropdown(string name)
        {
            return transform.Find(name)?.GetComponent<Dropdown>();
        }

        private Toggle FindToggle(string name)
        {
            return transform.Find(name)?.GetComponent<Toggle>();
        }

        private ButtonWidgets FindButton(string name)
        {
            var button = transform.Find(name)?.GetComponent<Button>();
            if (button == null)
            {
                return null;
            }

            return new ButtonWidgets(button, button.GetComponentInChildren<Text>(true));
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

            UnbindButton(_displayControls.ApplyButton, ClickApply);
            UnbindButton(_displayControls.RevertButton, ClickRevert);
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
    }
}
