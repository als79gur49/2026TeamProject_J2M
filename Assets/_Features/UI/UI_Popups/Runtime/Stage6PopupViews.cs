using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class ConfirmPopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _bodyLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Text _confirmButtonLabel;
        [SerializeField] private Text _cancelButtonLabel;
        [SerializeField] private Image _confirmButtonImage;

        private ConfirmPopupViewModel _viewModel;
        private bool _isVisible;

        public event Action<PopupCompletionKind> CompletionRequested;

        public string TitleText => _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string BodyText => _viewModel != null ? _viewModel.BodyText : string.Empty;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Configure(
            GameObject root,
            CanvasGroup canvasGroup,
            Text titleLabel,
            Text bodyLabel,
            Button confirmButton,
            Button cancelButton,
            Text confirmButtonLabel,
            Text cancelButtonLabel,
            Image confirmButtonImage)
        {
            _root = root;
            _canvasGroup = canvasGroup;
            _titleLabel = titleLabel;
            _bodyLabel = bodyLabel;
            _confirmButton = confirmButton;
            _cancelButton = cancelButton;
            _confirmButtonLabel = confirmButtonLabel;
            _cancelButtonLabel = cancelButtonLabel;
            _confirmButtonImage = confirmButtonImage;

            _confirmButton.onClick.RemoveListener(ClickConfirm);
            _cancelButton.onClick.RemoveListener(ClickCancel);
            _confirmButton.onClick.AddListener(ClickConfirm);
            _cancelButton.onClick.AddListener(ClickCancel);

            RefreshView();
        }

        public void Bind(ConfirmPopupViewModel viewModel)
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

        public void SetIsTopmost(bool isTopmost)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.interactable = isTopmost;
            _canvasGroup.blocksRaycasts = isTopmost;
        }

        public void ClickConfirm()
        {
            if (!CanEmit())
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Confirmed);
        }

        public void ClickCancel()
        {
            if (!CanEmit())
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Cancelled);
        }

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }
        }

        private bool CanEmit()
        {
            return IsVisible && _canvasGroup != null && _canvasGroup.interactable;
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

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = _viewModel.BodyText;
            }

            if (_confirmButtonLabel != null)
            {
                _confirmButtonLabel.text = _viewModel.ConfirmLabel;
            }

            if (_cancelButtonLabel != null)
            {
                _cancelButtonLabel.text = _viewModel.CancelLabel;
            }

            if (_confirmButtonImage != null)
            {
                _confirmButtonImage.color = _viewModel.IsConfirmDestructive
                    ? new Color(0.62f, 0.21f, 0.21f, 1f)
                    : new Color(0.20f, 0.25f, 0.34f, 1f);
            }
        }
    }

    public sealed class TooltipPopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _bodyLabel;
        [SerializeField] private Button _dismissButton;

        private TooltipPopupViewModel _viewModel;
        private bool _isVisible;

        public event Action<PopupCompletionKind> CompletionRequested;

        public string TitleText => _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string BodyText => _viewModel != null ? _viewModel.BodyText : string.Empty;

        public TooltipPopupAnchorPreset AnchorPreset => _viewModel != null
            ? _viewModel.AnchorPreset
            : TooltipPopupAnchorPreset.Center;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Configure(
            GameObject root,
            CanvasGroup canvasGroup,
            RectTransform panelRect,
            Text titleLabel,
            Text bodyLabel,
            Button dismissButton)
        {
            _root = root;
            _canvasGroup = canvasGroup;
            _panelRect = panelRect;
            _titleLabel = titleLabel;
            _bodyLabel = bodyLabel;
            _dismissButton = dismissButton;

            _dismissButton.onClick.RemoveListener(ClickDismiss);
            _dismissButton.onClick.AddListener(ClickDismiss);

            RefreshView();
        }

        public void Bind(TooltipPopupViewModel viewModel)
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

        public void SetIsTopmost(bool isTopmost)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.interactable = isTopmost;
            _canvasGroup.blocksRaycasts = isTopmost;
        }

        public void ClickDismiss()
        {
            if (!IsVisible || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Closed);
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

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = _viewModel.BodyText;
            }

            ApplyAnchor(_viewModel.AnchorPreset);
        }

        private void ApplyAnchor(TooltipPopupAnchorPreset anchorPreset)
        {
            if (_panelRect == null)
            {
                return;
            }

            switch (anchorPreset)
            {
                case TooltipPopupAnchorPreset.UpperRight:
                    _panelRect.anchorMin = new Vector2(1f, 1f);
                    _panelRect.anchorMax = new Vector2(1f, 1f);
                    _panelRect.pivot = new Vector2(1f, 1f);
                    _panelRect.anchoredPosition = new Vector2(-24f, -24f);
                    break;

                case TooltipPopupAnchorPreset.LowerLeft:
                    _panelRect.anchorMin = new Vector2(0f, 0f);
                    _panelRect.anchorMax = new Vector2(0f, 0f);
                    _panelRect.pivot = new Vector2(0f, 0f);
                    _panelRect.anchoredPosition = new Vector2(24f, 24f);
                    break;

                default:
                    _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    _panelRect.pivot = new Vector2(0.5f, 0.5f);
                    _panelRect.anchoredPosition = new Vector2(0f, 160f);
                    break;
            }
        }
    }

    public sealed class RewardPopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _firstItemLabel;
        [SerializeField] private Text _secondItemLabel;
        [SerializeField] private Text _thirdItemLabel;
        [SerializeField] private Text _summaryLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _closeButtonLabel;

        private RewardPopupViewModel _viewModel;
        private bool _isVisible;

        public event Action<PopupCompletionKind> CompletionRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Configure(
            GameObject root,
            CanvasGroup canvasGroup,
            Text titleLabel,
            Text firstItemLabel,
            Text secondItemLabel,
            Text thirdItemLabel,
            Text summaryLabel,
            Button closeButton,
            Text closeButtonLabel)
        {
            _root = root;
            _canvasGroup = canvasGroup;
            _titleLabel = titleLabel;
            _firstItemLabel = firstItemLabel;
            _secondItemLabel = secondItemLabel;
            _thirdItemLabel = thirdItemLabel;
            _summaryLabel = summaryLabel;
            _closeButton = closeButton;
            _closeButtonLabel = closeButtonLabel;

            _closeButton.onClick.RemoveListener(ClickAcknowledge);
            _closeButton.onClick.AddListener(ClickAcknowledge);

            RefreshView();
        }

        public void Bind(RewardPopupViewModel viewModel)
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

        public void SetIsTopmost(bool isTopmost)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.interactable = isTopmost;
            _canvasGroup.blocksRaycasts = isTopmost;
        }

        public void ClickAcknowledge()
        {
            if (!IsVisible || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Acknowledged);
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

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = _viewModel.SummaryText;
            }

            if (_closeButtonLabel != null)
            {
                _closeButtonLabel.text = _viewModel.CloseLabel;
            }

            SetItemText(_firstItemLabel, 0);
            SetItemText(_secondItemLabel, 1);
            SetItemText(_thirdItemLabel, 2);
        }

        private void SetItemText(Text label, int index)
        {
            if (label == null)
            {
                return;
            }

            label.text = _viewModel.ItemLines.Length > index
                ? _viewModel.ItemLines[index]
                : string.Empty;
        }
    }
}
