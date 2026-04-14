using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class ObjectiveStatusScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _badgeLabel;
        [SerializeField] private Text _summaryLabel;
        [SerializeField] private Text _detailLabel;
        [SerializeField] private Text _secondaryLabel;
        [SerializeField] private Button _overviewButton;
        [SerializeField] private Button _sessionButton;
        [SerializeField] private Button _infoButton;
        [SerializeField] private Button _backButton;

        private ObjectiveStatusScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action OverviewRequested;

        public event Action SessionRequested;

        public event Action InfoRequested;

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

        public void Configure(
            GameObject root,
            Text titleLabel,
            Text badgeLabel,
            Text summaryLabel,
            Text detailLabel,
            Text secondaryLabel,
            Button overviewButton,
            Button sessionButton,
            Button infoButton,
            Button backButton)
        {
            _root = root;
            _titleLabel = titleLabel;
            _badgeLabel = badgeLabel;
            _summaryLabel = summaryLabel;
            _detailLabel = detailLabel;
            _secondaryLabel = secondaryLabel;
            _overviewButton = overviewButton;
            _sessionButton = sessionButton;
            _infoButton = infoButton;
            _backButton = backButton;

            _overviewButton.onClick.RemoveListener(ClickOverview);
            _sessionButton.onClick.RemoveListener(ClickSession);
            _infoButton.onClick.RemoveListener(ClickInfo);
            _backButton.onClick.RemoveListener(ClickBack);
            _overviewButton.onClick.AddListener(ClickOverview);
            _sessionButton.onClick.AddListener(ClickSession);
            _infoButton.onClick.AddListener(ClickInfo);
            _backButton.onClick.AddListener(ClickBack);

            RefreshView();
        }

        public void Bind(ObjectiveStatusScreenViewModel viewModel)
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

        public void ClickOverview()
        {
            if (!IsVisible)
            {
                return;
            }

            OverviewRequested?.Invoke();
        }

        public void ClickSession()
        {
            if (!IsVisible)
            {
                return;
            }

            SessionRequested?.Invoke();
        }

        public void ClickInfo()
        {
            if (!IsVisible)
            {
                return;
            }

            InfoRequested?.Invoke();
        }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
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

            if (_badgeLabel != null)
            {
                _badgeLabel.text = _viewModel.BadgeText;
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = _viewModel.SummaryText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_secondaryLabel != null)
            {
                _secondaryLabel.text = _viewModel.SecondaryText;
            }

            if (_overviewButton != null)
            {
                _overviewButton.interactable = !_viewModel.IsOverviewSelected;
            }

            if (_sessionButton != null)
            {
                _sessionButton.interactable = !_viewModel.IsSessionSelected;
            }
        }
    }
}
