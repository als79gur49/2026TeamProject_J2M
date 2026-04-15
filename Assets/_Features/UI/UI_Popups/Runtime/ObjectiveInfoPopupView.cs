using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class ObjectiveInfoPopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _bodyLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _closeButtonLabel;

        private ObjectiveInfoPopupViewModel _viewModel;
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

        public string TitleText => _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string BodyText => _viewModel != null ? _viewModel.BodyText : string.Empty;

        public void Bind(ObjectiveInfoPopupViewModel viewModel)
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
                _closeButton.onClick.RemoveListener(ClickClose);
                _closeButton.onClick.AddListener(ClickClose);
            }

            RefreshView();
        }

        private void OnDisable()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(ClickClose);
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

        public void ClickClose()
        {
            if (!IsVisible || _viewModel == null || _canvasGroup == null || !_canvasGroup.interactable)
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
                _closeButton.onClick.RemoveListener(ClickClose);
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

            if (_closeButtonLabel != null)
            {
                _closeButtonLabel.text = _viewModel.CloseLabel;
            }
        }
    }
}
