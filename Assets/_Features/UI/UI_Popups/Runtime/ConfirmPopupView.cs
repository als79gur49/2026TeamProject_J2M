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

        private void OnEnable()
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveListener(ClickConfirm);
                _confirmButton.onClick.AddListener(ClickConfirm);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(ClickCancel);
                _cancelButton.onClick.AddListener(ClickCancel);
            }

            RefreshView();
        }

        private void OnDisable()
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveListener(ClickConfirm);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(ClickCancel);
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

            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveListener(ClickConfirm);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(ClickCancel);
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
}
