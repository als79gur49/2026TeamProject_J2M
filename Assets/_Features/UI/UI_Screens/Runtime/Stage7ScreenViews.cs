using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class InventoryScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _itemsLabel;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _backButtonLabel;

        private InventoryScreenViewModel _viewModel;
        private bool _isVisible;

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
            Text itemsLabel,
            Button backButton,
            Text backButtonLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _itemsLabel = itemsLabel;
            _backButton = backButton;
            _backButtonLabel = backButtonLabel;

            _backButton.onClick.RemoveListener(ClickBack);
            _backButton.onClick.AddListener(ClickBack);

            RefreshView();
        }

        public void Bind(InventoryScreenViewModel viewModel)
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
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

            if (_itemsLabel != null)
            {
                _itemsLabel.text = _viewModel.ItemsText;
            }

            if (_backButtonLabel != null)
            {
                _backButtonLabel.text = _viewModel.BackLabel;
            }
        }
    }

    public sealed class SettingsScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _tooltipStatusLabel;
        [SerializeField] private Text _largeTextStatusLabel;
        [SerializeField] private Button _tooltipToggleButton;
        [SerializeField] private Button _largeTextToggleButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _tooltipToggleButtonLabel;
        [SerializeField] private Text _largeTextToggleButtonLabel;
        [SerializeField] private Text _backButtonLabel;

        private SettingsScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action TooltipToggleRequested;

        public event Action LargeTextToggleRequested;

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
            Text tooltipStatusLabel,
            Text largeTextStatusLabel,
            Button tooltipToggleButton,
            Button largeTextToggleButton,
            Button backButton,
            Text tooltipToggleButtonLabel,
            Text largeTextToggleButtonLabel,
            Text backButtonLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _tooltipStatusLabel = tooltipStatusLabel;
            _largeTextStatusLabel = largeTextStatusLabel;
            _tooltipToggleButton = tooltipToggleButton;
            _largeTextToggleButton = largeTextToggleButton;
            _backButton = backButton;
            _tooltipToggleButtonLabel = tooltipToggleButtonLabel;
            _largeTextToggleButtonLabel = largeTextToggleButtonLabel;
            _backButtonLabel = backButtonLabel;

            _tooltipToggleButton.onClick.RemoveListener(ClickTooltipToggle);
            _largeTextToggleButton.onClick.RemoveListener(ClickLargeTextToggle);
            _backButton.onClick.RemoveListener(ClickBack);
            _tooltipToggleButton.onClick.AddListener(ClickTooltipToggle);
            _largeTextToggleButton.onClick.AddListener(ClickLargeTextToggle);
            _backButton.onClick.AddListener(ClickBack);

            RefreshView();
        }

        public void Bind(SettingsScreenViewModel viewModel)
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickTooltipToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            TooltipToggleRequested?.Invoke();
        }

        public void ClickLargeTextToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            LargeTextToggleRequested?.Invoke();
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

            if (_tooltipStatusLabel != null)
            {
                _tooltipStatusLabel.text = _viewModel.TooltipStatusText;
            }

            if (_largeTextStatusLabel != null)
            {
                _largeTextStatusLabel.text = _viewModel.LargeTextStatusText;
            }

            if (_tooltipToggleButtonLabel != null)
            {
                _tooltipToggleButtonLabel.text = _viewModel.TooltipToggleLabel;
            }

            if (_largeTextToggleButtonLabel != null)
            {
                _largeTextToggleButtonLabel.text = _viewModel.LargeTextToggleLabel;
            }

            if (_backButtonLabel != null)
            {
                _backButtonLabel.text = _viewModel.BackLabel;
            }
        }
    }

    public sealed class StageResultScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _summaryLabel;
        [SerializeField] private Text _detailLabel;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Text _continueButtonLabel;

        private StageResultScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action ContinueRequested;

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
            Text summaryLabel,
            Text detailLabel,
            Button continueButton,
            Text continueButtonLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _summaryLabel = summaryLabel;
            _detailLabel = detailLabel;
            _continueButton = continueButton;
            _continueButtonLabel = continueButtonLabel;

            _continueButton.onClick.RemoveListener(ClickContinue);
            _continueButton.onClick.AddListener(ClickContinue);

            RefreshView();
        }

        public void Bind(StageResultScreenViewModel viewModel)
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickContinue()
        {
            if (!IsVisible)
            {
                return;
            }

            ContinueRequested?.Invoke();
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

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_continueButtonLabel != null)
            {
                _continueButtonLabel.text = _viewModel.ContinueLabel;
            }
        }
    }
}
