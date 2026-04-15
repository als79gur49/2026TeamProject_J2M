using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PausePopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Button _resumeButton;

        private PausePopupViewModel _viewModel;
        private bool _isVisible;

        public event Action<PopupCompletionKind> CompletionRequested;

        public string TitleText => _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string DescriptionText => _viewModel != null ? _viewModel.DescriptionText : string.Empty;

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
            Text descriptionLabel,
            Button resumeButton)
        {
            _root = root;
            _canvasGroup = canvasGroup;
            _titleLabel = titleLabel;
            _descriptionLabel = descriptionLabel;
            _resumeButton = resumeButton;

            _resumeButton.onClick.RemoveListener(ClickResume);
            _resumeButton.onClick.AddListener(ClickResume);

            RefreshView();
        }

        public void Bind(PausePopupViewModel viewModel)
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

        public void ClickResume()
        {
            if (!IsVisible || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Resumed);
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

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = _viewModel.DescriptionText;
            }

            var buttonLabel = _resumeButton != null
                ? _resumeButton.GetComponentInChildren<Text>()
                : null;
            if (buttonLabel != null)
            {
                buttonLabel.text = _viewModel.ResumeLabel;
            }
        }
    }
}
