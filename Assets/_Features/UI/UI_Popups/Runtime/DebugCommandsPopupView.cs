using System;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class DebugCommandsPopupView : MonoBehaviour, IPopupView, IUiNavigationTarget
    {
        private const int NextStageSelectionIndex = 0;
        private const int ForceClearSelectionIndex = 1;
        private const int CloseSelectionIndex = 2;

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _lastResultLabel;
        [SerializeField] private Button _nextStageButton;
        [SerializeField] private Button _forceClearResultOnlyButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _nextStageButtonLabel;
        [SerializeField] private TMP_Text _forceClearResultOnlyButtonLabel;
        [SerializeField] private TMP_Text _closeButtonLabel;

        private DebugCommandsPopupViewModel _viewModel;
        private bool _isVisible;
        private int _selectedIndex;

        public event Action<PopupCompletionKind> CompletionRequested;

        public event Action NextStageRequested;

        public event Action ForceClearResultOnlyRequested;

        public bool CanHandleUiNavigation => IsVisible && isActiveAndEnabled && _canvasGroup != null && _canvasGroup.interactable;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(DebugCommandsPopupViewModel viewModel)
        {
            EnsureHierarchy();
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

        public void SetIsTopmost(bool isTopmost)
        {
            EnsureHierarchy();
            _canvasGroup.interactable = isTopmost;
            _canvasGroup.blocksRaycasts = isTopmost;
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            switch (command)
            {
                case UiNavigationCommand.Up:
                    _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
                    return true;

                case UiNavigationCommand.Down:
                    _selectedIndex = Mathf.Min(CloseSelectionIndex, _selectedIndex + 1);
                    return true;

                default:
                    return false;
            }
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            switch (_selectedIndex)
            {
                case NextStageSelectionIndex:
                    ClickNextStage();
                    return true;

                case ForceClearSelectionIndex:
                    ClickForceClearResultOnly();
                    return true;

                default:
                    ClickClose();
                    return true;
            }
        }

        public bool HandleCancel()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            ClickClose();
            return true;
        }

        public void OnNavigationFocusGained()
        {
        }

        public void OnNavigationFocusLost()
        {
        }

        public void ClickNextStage()
        {
            if (!CanEmit() || _viewModel == null || !_viewModel.CanGoNextStage)
            {
                return;
            }

            NextStageRequested?.Invoke();
        }

        public void ClickForceClearResultOnly()
        {
            if (!CanEmit() || _viewModel == null || !_viewModel.CanForceClearResultOnly)
            {
                return;
            }

            ForceClearResultOnlyRequested?.Invoke();
        }

        public void ClickClose()
        {
            if (!CanEmit())
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Closed);
        }

        private void OnEnable()
        {
            EnsureHierarchy();
            RebindButton(_nextStageButton, ClickNextStage);
            RebindButton(_forceClearResultOnlyButton, ClickForceClearResultOnly);
            RebindButton(_closeButton, ClickClose);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_nextStageButton, ClickNextStage);
            UnbindButton(_forceClearResultOnlyButton, ClickForceClearResultOnly);
            UnbindButton(_closeButton, ClickClose);
        }

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private bool CanEmit()
        {
            return IsVisible && _canvasGroup != null && _canvasGroup.interactable;
        }

        private void RefreshView()
        {
            EnsureHierarchy();
            _root.SetActive(IsVisible);
            if (_viewModel == null)
            {
                return;
            }

            _titleLabel.text = _viewModel.TitleText;
            _statusLabel.text = string.Join(
                "\n",
                _viewModel.DebugBuildStatusText,
                _viewModel.CurrentStageText,
                _viewModel.NextStageText,
                _viewModel.LockStatusText,
                _viewModel.ForceClearAvailabilityText,
                "Result Only mode: NO SAVE / NO REWARD");
            _lastResultLabel.text = string.IsNullOrWhiteSpace(_viewModel.LastCommandMessage)
                ? "Last command: none"
                : $"Last command: {_viewModel.LastCommandMessage}";
            _nextStageButtonLabel.text = _viewModel.NextStageLabel;
            _forceClearResultOnlyButtonLabel.text = _viewModel.ForceClearResultOnlyLabel;
            _closeButtonLabel.text = _viewModel.CloseLabel;
            _nextStageButton.interactable = _viewModel.CanGoNextStage;
            _forceClearResultOnlyButton.interactable = _viewModel.CanForceClearResultOnly;
        }

        private void EnsureHierarchy()
        {
            if (_root != null)
            {
                return;
            }

            var root = new GameObject("DebugCommandsPopupRoot", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(transform, false);
            _root = root;
            _canvasGroup = root.GetComponent<CanvasGroup>();
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 430f);

            var image = root.GetComponent<Image>();
            image.color = new Color(0.07f, 0.08f, 0.09f, 0.96f);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _titleLabel = CreateLabel("Title", root.transform, 30, FontStyles.Bold, TextAlignmentOptions.Left);
            _statusLabel = CreateLabel("Status", root.transform, 20, FontStyles.Normal, TextAlignmentOptions.Left);
            _lastResultLabel = CreateLabel("LastResult", root.transform, 18, FontStyles.Normal, TextAlignmentOptions.Left);
            (_nextStageButton, _nextStageButtonLabel) = CreateButton("NextStageButton", root.transform);
            (_forceClearResultOnlyButton, _forceClearResultOnlyButtonLabel) = CreateButton("ForceClearResultOnlyButton", root.transform);
            (_closeButton, _closeButtonLabel) = CreateButton("CloseButton", root.transform);
        }

        private static TMP_Text CreateLabel(
            string objectName,
            Transform parent,
            int fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = Color.white;
            label.enableWordWrapping = true;
            labelObject.GetComponent<LayoutElement>().preferredHeight = fontSize * 1.8f;
            return label;
        }

        private static (Button Button, TMP_Text Label) CreateButton(string objectName, Transform parent)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.26f, 1f);
            buttonObject.GetComponent<LayoutElement>().preferredHeight = 48f;
            var text = CreateLabel("Label", buttonObject.transform, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);
            return (buttonObject.GetComponent<Button>(), text);
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
    }
}
