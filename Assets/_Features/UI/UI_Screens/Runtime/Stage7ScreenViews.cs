using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class InventoryScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private InventoryCatalogView _catalogView;
        [SerializeField] private InventoryDetailView _detailView;
        [SerializeField] private InventoryActionView _actionView;
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
            InventoryCatalogView catalogView,
            InventoryDetailView detailView,
            InventoryActionView actionView,
            Button backButton,
            Text backButtonLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _catalogView = catalogView;
            _detailView = detailView;
            _actionView = actionView;
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

        public InventoryCatalogView CatalogView => _catalogView;

        public InventoryDetailView DetailView => _detailView;

        public InventoryActionView ActionView => _actionView;

        public void ClickSearch()
        {
            if (!IsVisible || _catalogView == null)
            {
                return;
            }

            _catalogView.ClickSearch();
        }

        public void ClickFilter()
        {
            if (!IsVisible || _catalogView == null)
            {
                return;
            }

            _catalogView.ClickFilter();
        }

        public void ClickSort()
        {
            if (!IsVisible || _catalogView == null)
            {
                return;
            }

            _catalogView.ClickSort();
        }

        public void ClickItemRow(int visibleIndex)
        {
            if (!IsVisible || _catalogView == null)
            {
                return;
            }

            _catalogView.ClickRow(visibleIndex);
        }

        public void ClickPrimaryAction()
        {
            if (!IsVisible || _actionView == null)
            {
                return;
            }

            _actionView.ClickPrimaryAction();
        }

        public void ClickSecondaryAction()
        {
            if (!IsVisible || _actionView == null)
            {
                return;
            }

            _actionView.ClickSecondaryAction();
        }

        public string CatalogSummaryText => _catalogView != null ? _catalogView.SummaryText : string.Empty;

        public string CatalogEmptyStateText => _catalogView != null ? _catalogView.EmptyStateText : string.Empty;

        public string DetailTitleText => _detailView != null ? _detailView.TitleText : string.Empty;

        public string ActionFeedbackText => _actionView != null ? _actionView.FeedbackText : string.Empty;

        public string PrimaryActionStateText => _actionView != null ? _actionView.PrimaryStateText : string.Empty;

        public string GetCatalogRowLabel(int visibleIndex)
        {
            return _catalogView != null ? _catalogView.GetRowLabel(visibleIndex) : string.Empty;
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

            if (_backButtonLabel != null)
            {
                _backButtonLabel.text = _viewModel.BackLabel;
            }
        }
    }

    public sealed class InventoryCatalogView : MonoBehaviour
    {
        [SerializeField] private Button _searchButton;
        [SerializeField] private Button _filterButton;
        [SerializeField] private Button _sortButton;
        [SerializeField] private Text _searchButtonLabel;
        [SerializeField] private Text _filterButtonLabel;
        [SerializeField] private Text _sortButtonLabel;
        [SerializeField] private Text _summaryLabel;
        [SerializeField] private Text _emptyStateLabel;
        [SerializeField] private Button[] _rowButtons;
        [SerializeField] private Text[] _rowLabelTexts;
        [SerializeField] private Text[] _rowMetaTexts;

        private InventoryCatalogViewModel _viewModel;

        public event Action SearchRequested;

        public event Action FilterRequested;

        public event Action SortRequested;

        public event Action<int> RowRequested;

        public string SummaryText => _summaryLabel != null ? _summaryLabel.text : string.Empty;

        public string EmptyStateText => _emptyStateLabel != null ? _emptyStateLabel.text : string.Empty;

        public void Configure(
            Button searchButton,
            Button filterButton,
            Button sortButton,
            Text searchButtonLabel,
            Text filterButtonLabel,
            Text sortButtonLabel,
            Text summaryLabel,
            Text emptyStateLabel,
            Button[] rowButtons,
            Text[] rowLabelTexts,
            Text[] rowMetaTexts)
        {
            _searchButton = searchButton;
            _filterButton = filterButton;
            _sortButton = sortButton;
            _searchButtonLabel = searchButtonLabel;
            _filterButtonLabel = filterButtonLabel;
            _sortButtonLabel = sortButtonLabel;
            _summaryLabel = summaryLabel;
            _emptyStateLabel = emptyStateLabel;
            _rowButtons = rowButtons ?? Array.Empty<Button>();
            _rowLabelTexts = rowLabelTexts ?? Array.Empty<Text>();
            _rowMetaTexts = rowMetaTexts ?? Array.Empty<Text>();

            RebindButton(_searchButton, ClickSearch);
            RebindButton(_filterButton, ClickFilter);
            RebindButton(_sortButton, ClickSort);
            for (var i = 0; i < _rowButtons.Length; i++)
            {
                var index = i;
                RebindButton(_rowButtons[i], () => ClickRow(index));
            }

            RefreshView();
        }

        public void Bind(InventoryCatalogViewModel viewModel)
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

        public void ClickSearch()
        {
            SearchRequested?.Invoke();
        }

        public void ClickFilter()
        {
            FilterRequested?.Invoke();
        }

        public void ClickSort()
        {
            SortRequested?.Invoke();
        }

        public void ClickRow(int visibleIndex)
        {
            RowRequested?.Invoke(visibleIndex);
        }

        public string GetRowLabel(int visibleIndex)
        {
            return visibleIndex >= 0 && visibleIndex < _rowLabelTexts.Length && _rowLabelTexts[visibleIndex] != null
                ? _rowLabelTexts[visibleIndex].text
                : string.Empty;
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
            if (_viewModel == null)
            {
                return;
            }

            if (_searchButtonLabel != null)
            {
                _searchButtonLabel.text = _viewModel.SearchLabelText;
            }

            if (_filterButtonLabel != null)
            {
                _filterButtonLabel.text = _viewModel.FilterLabelText;
            }

            if (_sortButtonLabel != null)
            {
                _sortButtonLabel.text = _viewModel.SortLabelText;
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = _viewModel.SummaryText;
            }

            if (_emptyStateLabel != null)
            {
                _emptyStateLabel.text = _viewModel.EmptyStateText;
            }

            for (var i = 0; i < _rowButtons.Length; i++)
            {
                var row = i < _viewModel.Rows.Count
                    ? _viewModel.Rows[i]
                    : new InventoryCatalogRowViewModel(string.Empty, string.Empty, isSelected: false, isVisible: false);
                if (_rowButtons[i] != null)
                {
                    _rowButtons[i].gameObject.SetActive(row.IsVisible);
                    _rowButtons[i].interactable = row.IsVisible && !row.IsSelected;
                }

                if (i < _rowLabelTexts.Length && _rowLabelTexts[i] != null)
                {
                    _rowLabelTexts[i].text = row.LabelText;
                }

                if (i < _rowMetaTexts.Length && _rowMetaTexts[i] != null)
                {
                    _rowMetaTexts[i].text = row.MetaText;
                }
            }
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    public sealed class InventoryDetailView : MonoBehaviour
    {
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _badgeLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Text _detailLabel;

        private InventoryDetailViewModel _viewModel;

        public string TitleText => _titleLabel != null ? _titleLabel.text : string.Empty;

        public void Configure(
            Text titleLabel,
            Text badgeLabel,
            Text descriptionLabel,
            Text detailLabel)
        {
            _titleLabel = titleLabel;
            _badgeLabel = badgeLabel;
            _descriptionLabel = descriptionLabel;
            _detailLabel = detailLabel;
            RefreshView();
        }

        public void Bind(InventoryDetailViewModel viewModel)
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

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = _viewModel.DescriptionText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }
        }
    }

    public sealed class InventoryActionView : MonoBehaviour
    {
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private Text _primaryButtonLabel;
        [SerializeField] private Text _secondaryButtonLabel;
        [SerializeField] private Text _primaryStateLabel;
        [SerializeField] private Text _secondaryStateLabel;
        [SerializeField] private Text _feedbackLabel;

        private InventoryActionViewModel _viewModel;

        public event Action PrimaryActionRequested;

        public event Action SecondaryActionRequested;

        public string FeedbackText => _feedbackLabel != null ? _feedbackLabel.text : string.Empty;

        public string PrimaryStateText => _primaryStateLabel != null ? _primaryStateLabel.text : string.Empty;

        public void Configure(
            Button primaryButton,
            Button secondaryButton,
            Text primaryButtonLabel,
            Text secondaryButtonLabel,
            Text primaryStateLabel,
            Text secondaryStateLabel,
            Text feedbackLabel)
        {
            _primaryButton = primaryButton;
            _secondaryButton = secondaryButton;
            _primaryButtonLabel = primaryButtonLabel;
            _secondaryButtonLabel = secondaryButtonLabel;
            _primaryStateLabel = primaryStateLabel;
            _secondaryStateLabel = secondaryStateLabel;
            _feedbackLabel = feedbackLabel;

            RebindButton(_primaryButton, ClickPrimaryAction);
            RebindButton(_secondaryButton, ClickSecondaryAction);
            RefreshView();
        }

        public void Bind(InventoryActionViewModel viewModel)
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

        public void ClickPrimaryAction()
        {
            PrimaryActionRequested?.Invoke();
        }

        public void ClickSecondaryAction()
        {
            SecondaryActionRequested?.Invoke();
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
            if (_viewModel == null)
            {
                return;
            }

            if (_primaryButton != null)
            {
                _primaryButton.gameObject.SetActive(_viewModel.IsPrimaryVisible);
                _primaryButton.interactable = _viewModel.IsPrimaryEnabled;
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.gameObject.SetActive(_viewModel.IsSecondaryVisible);
                _secondaryButton.interactable = _viewModel.IsSecondaryEnabled;
            }

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = _viewModel.PrimaryLabelText;
            }

            if (_secondaryButtonLabel != null)
            {
                _secondaryButtonLabel.text = _viewModel.SecondaryLabelText;
            }

            if (_primaryStateLabel != null)
            {
                _primaryStateLabel.text = _viewModel.PrimaryStateText;
            }

            if (_secondaryStateLabel != null)
            {
                _secondaryStateLabel.text = _viewModel.SecondaryStateText;
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = _viewModel.FeedbackText;
            }
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
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
