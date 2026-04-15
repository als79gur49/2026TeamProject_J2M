using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
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

        private void OnEnable()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(ClickAcknowledge);
                _closeButton.onClick.AddListener(ClickAcknowledge);
            }

            RefreshView();
        }

        private void OnDisable()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(ClickAcknowledge);
            }
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

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(ClickAcknowledge);
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

            if (_firstItemLabel != null)
            {
                _firstItemLabel.text = _viewModel.ItemLines.Length > 0 ? _viewModel.ItemLines[0] : string.Empty;
            }

            if (_secondItemLabel != null)
            {
                _secondItemLabel.text = _viewModel.ItemLines.Length > 1 ? _viewModel.ItemLines[1] : string.Empty;
            }

            if (_thirdItemLabel != null)
            {
                _thirdItemLabel.text = _viewModel.ItemLines.Length > 2 ? _viewModel.ItemLines[2] : string.Empty;
            }
        }
    }
}
