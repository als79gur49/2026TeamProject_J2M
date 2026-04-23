using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class InventoryCatalogView : MonoBehaviour
    {
        [SerializeField] private Button _searchButton;
        [SerializeField] private Button _filterButton;
        [SerializeField] private Button _sortButton;
        [SerializeField] private TMP_Text _searchButtonLabel;
        [SerializeField] private TMP_Text _filterButtonLabel;
        [SerializeField] private TMP_Text _sortButtonLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private TMP_Text _emptyStateLabel;
        [SerializeField] private Button[] _rowButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] _rowLabelTexts = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] _rowMetaTexts = Array.Empty<TMP_Text>();

        private InventoryCatalogViewModel _viewModel;

        public event Action SearchRequested;

        public event Action FilterRequested;

        public event Action SortRequested;

        public event Action<int> RowRequested;

        public string SummaryText => _summaryLabel != null ? _summaryLabel.text : string.Empty;

        public string EmptyStateText => _emptyStateLabel != null ? _emptyStateLabel.text : string.Empty;

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

        private void OnEnable()
        {
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

        private void OnDisable()
        {
            ClearButton(_searchButton);
            ClearButton(_filterButton);
            ClearButton(_sortButton);
            for (var i = 0; i < _rowButtons.Length; i++)
            {
                ClearButton(_rowButtons[i]);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_searchButton, nameof(_searchButton));
            ValidateSerializedReference(_filterButton, nameof(_filterButton));
            ValidateSerializedReference(_sortButton, nameof(_sortButton));
            ValidateSerializedReference(_searchButtonLabel, nameof(_searchButtonLabel));
            ValidateSerializedReference(_filterButtonLabel, nameof(_filterButtonLabel));
            ValidateSerializedReference(_sortButtonLabel, nameof(_sortButtonLabel));
            ValidateSerializedReference(_summaryLabel, nameof(_summaryLabel));
            ValidateSerializedReference(_emptyStateLabel, nameof(_emptyStateLabel));

            for (var i = 0; i < _rowButtons.Length; i++)
            {
                ValidateSerializedReference(_rowButtons[i], $"{nameof(_rowButtons)}[{i}]");
            }

            for (var i = 0; i < _rowLabelTexts.Length; i++)
            {
                ValidateSerializedReference(_rowLabelTexts[i], $"{nameof(_rowLabelTexts)}[{i}]");
            }

            for (var i = 0; i < _rowMetaTexts.Length; i++)
            {
                ValidateSerializedReference(_rowMetaTexts[i], $"{nameof(_rowMetaTexts)}[{i}]");
            }
        }
#endif

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

        private static void ClearButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(InventoryCatalogView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
