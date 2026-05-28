using System;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.DemoStageControl.UI
{
    public sealed class DemoStageControlPanelView : MonoBehaviour, IPopupView, IUiNavigationTarget
    {
        private Button _closeButton;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _campaignText;
        private TextMeshProUGUI _currentText;
        private Button _forceClearButton;
        private TextMeshProUGUI _lastResultText;
        private Button _nextButton;
        private Button _playerInvincibleButton;
        private TextMeshProUGUI _playerInvincibleButtonLabel;
        private Button _previousButton;
        private RectTransform _root;
        private TextMeshProUGUI _selectedStageText;
        private Button _startButton;
        private DemoStageControlPanelViewModel _viewModel;

        public event Action<PopupCompletionKind> CompletionRequested;

        public event Action<StageId> SelectedStageChanged;

        public event Action<StageId> StartStageClicked;

        public event Action ForceClearClicked;

        public event Action<bool> PlayerInvincibleToggled;

        public bool CanHandleUiNavigation => IsVisible && isActiveAndEnabled && _canvasGroup != null && _canvasGroup.interactable;

        public bool IsVisible
        {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }

        public static DemoStageControlPanelView CreateRuntime(Transform parent)
        {
            var rootObject = new GameObject(nameof(DemoStageControlPanelView), typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(parent, false);
            var view = rootObject.AddComponent<DemoStageControlPanelView>();
            view.EnsureHierarchy();
            return view;
        }

        public void Bind(DemoStageControlPanelViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= Refresh;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += Refresh;
            }

            Refresh();
        }

        public void SetIsTopmost(bool isTopmost)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.interactable = isTopmost;
            _canvasGroup.blocksRaycasts = isTopmost;
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            return false;
        }

        public bool HandleSubmit()
        {
            return false;
        }

        public bool HandleCancel()
        {
            CompletionRequested?.Invoke(PopupCompletionKind.Closed);
            return true;
        }

        public void OnNavigationFocusGained()
        {
        }

        public void OnNavigationFocusLost()
        {
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= Refresh;
            }
        }

        private void BuildHierarchy()
        {
            _root = (RectTransform)transform;
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(620f, 520f);

            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            var background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.07f, 0.96f);

            var layout = gameObject.GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            AddText("Demo Stage Control", 28, FontStyles.Bold, TextAlignmentOptions.Left);
            _currentText = AddText(string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Left);
            _campaignText = AddText(string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Left);
            _selectedStageText = AddText(string.Empty, 18, FontStyles.Bold, TextAlignmentOptions.Left);

            var selectorRow = AddRow("SelectorRow");
            _previousButton = AddButton(selectorRow, "Prev");
            _nextButton = AddButton(selectorRow, "Next");

            _startButton = AddButton(transform, "Start Selected Stage");
            _forceClearButton = AddButton(transform, "Force Clear Current Stage");
            _playerInvincibleButton = AddToggleButton(
                transform,
                "Player Invincible: OFF",
                out _playerInvincibleButtonLabel);
            _lastResultText = AddText(string.Empty, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            _closeButton = AddButton(transform, "Close");

            _previousButton.onClick.AddListener(HandlePreviousClicked);
            _nextButton.onClick.AddListener(HandleNextClicked);
            _startButton.onClick.AddListener(HandleStartClicked);
            _forceClearButton.onClick.AddListener(() => ForceClearClicked?.Invoke());
            _playerInvincibleButton.onClick.AddListener(HandlePlayerInvincibleButtonClicked);
            _closeButton.onClick.AddListener(() => CompletionRequested?.Invoke(PopupCompletionKind.Closed));
        }

        private void EnsureHierarchy()
        {
            if (_root == null)
            {
                BuildHierarchy();
            }
        }

        private Transform AddRow(string name)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(transform, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44f;
            return row.transform;
        }

        private TextMeshProUGUI AddText(
            string text,
            int fontSize,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            var textComponent = textObject.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = style;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            textComponent.enableWordWrapping = true;
            var layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = fontSize + 10f;
            return textComponent;
        }

        private Button AddButton(Transform parent, string label)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.25f, 1f);
            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.26f, 0.31f, 0.35f, 1f);
            colors.pressedColor = new Color(0.12f, 0.15f, 0.18f, 1f);
            colors.disabledColor = new Color(0.10f, 0.11f, 0.12f, 0.6f);
            button.colors = colors;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 4f);
            labelRect.offsetMax = new Vector2(-12f, -4f);
            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 17f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = false;

            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44f;
            layoutElement.minHeight = 38f;
            return button;
        }

        private Button AddToggleButton(
            Transform parent,
            string label,
            out TextMeshProUGUI labelText)
        {
            var buttonObject = new GameObject("Player Invincible", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.25f, 1f);
            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.26f, 0.31f, 0.35f, 1f);
            colors.pressedColor = new Color(0.12f, 0.15f, 0.18f, 1f);
            colors.disabledColor = new Color(0.10f, 0.11f, 0.12f, 0.6f);
            button.colors = colors;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 4f);
            labelRect.offsetMax = new Vector2(-12f, -4f);
            labelText = labelObject.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 17f;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.enableWordWrapping = false;

            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44f;
            layoutElement.minHeight = 38f;
            return button;
        }

        private void Refresh()
        {
            if (_viewModel == null || _currentText == null)
            {
                return;
            }

            _currentText.text = _viewModel.CurrentStageText;
            _campaignText.text = _viewModel.CampaignActiveStageText;
            _selectedStageText.text = _viewModel.SelectedStageText;
            _lastResultText.text = string.IsNullOrWhiteSpace(_viewModel.LastResultText)
                ? "Last result: none"
                : $"Last result: {_viewModel.LastResultText}";
            if (_playerInvincibleButton != null)
            {
                _playerInvincibleButtonLabel.text = _viewModel.PlayerInvincibleText;
                _playerInvincibleButton.interactable = true;
                RefreshPlayerInvincibleButtonColors();
            }

            _startButton.interactable = _viewModel.CanStartSelectedStage;
            _forceClearButton.interactable = _viewModel.CanForceClearCurrentStage;
            _previousButton.interactable = _viewModel.Stages.Count > 1;
            _nextButton.interactable = _viewModel.Stages.Count > 1;
        }

        private void HandlePreviousClicked()
        {
            if (_viewModel == null || _viewModel.Stages.Count == 0)
            {
                return;
            }

            var nextIndex = _viewModel.SelectedStageIndex <= 0
                ? _viewModel.Stages.Count - 1
                : _viewModel.SelectedStageIndex - 1;
            _viewModel.SelectIndex(nextIndex);
            SelectedStageChanged?.Invoke(_viewModel.SelectedStageId);
        }

        private void HandlePlayerInvincibleButtonClicked()
        {
            if (_viewModel == null)
            {
                return;
            }

            PlayerInvincibleToggled?.Invoke(!_viewModel.PlayerInvincible);
        }

        private void RefreshPlayerInvincibleButtonColors()
        {
            var normalColor = _viewModel.PlayerInvincible
                ? new Color(0.18f, 0.42f, 0.34f, 1f)
                : new Color(0.18f, 0.22f, 0.25f, 1f);
            var colors = _playerInvincibleButton.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = _viewModel.PlayerInvincible
                ? new Color(0.23f, 0.50f, 0.41f, 1f)
                : new Color(0.26f, 0.31f, 0.35f, 1f);
            colors.pressedColor = _viewModel.PlayerInvincible
                ? new Color(0.13f, 0.30f, 0.24f, 1f)
                : new Color(0.12f, 0.15f, 0.18f, 1f);
            _playerInvincibleButton.colors = colors;
            if (_playerInvincibleButton.targetGraphic is Image image)
            {
                image.color = normalColor;
            }
        }

        private void HandleNextClicked()
        {
            if (_viewModel == null || _viewModel.Stages.Count == 0)
            {
                return;
            }

            var nextIndex = _viewModel.SelectedStageIndex >= _viewModel.Stages.Count - 1
                ? 0
                : _viewModel.SelectedStageIndex + 1;
            _viewModel.SelectIndex(nextIndex);
            SelectedStageChanged?.Invoke(_viewModel.SelectedStageId);
        }

        private void HandleStartClicked()
        {
            if (_viewModel == null || !_viewModel.SelectedStageId.IsValid)
            {
                return;
            }

            StartStageClicked?.Invoke(_viewModel.SelectedStageId);
        }
    }
}
