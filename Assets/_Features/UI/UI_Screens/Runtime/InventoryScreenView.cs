using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class InventoryScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private InventoryCatalogView _catalogView;
        [SerializeField] private InventoryDetailView _detailView;
        [SerializeField] private InventoryActionView _actionView;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _backButtonLabel;

        private InventoryScreenViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;

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

        public InventoryCatalogView CatalogView => _catalogView;

        public InventoryDetailView DetailView => _detailView;

        public InventoryActionView ActionView => _actionView;

        public string CatalogSummaryText => _catalogView != null ? _catalogView.SummaryText : string.Empty;

        public string CatalogEmptyStateText => _catalogView != null ? _catalogView.EmptyStateText : string.Empty;

        public string DetailTitleText => _detailView != null ? _detailView.TitleText : string.Empty;

        public string ActionFeedbackText => _actionView != null ? _actionView.FeedbackText : string.Empty;

        public string PrimaryActionStateText => _actionView != null ? _actionView.PrimaryStateText : string.Empty;

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

        public string GetCatalogRowLabel(int visibleIndex)
        {
            return _catalogView != null ? _catalogView.GetRowLabel(visibleIndex) : string.Empty;
        }

        private void OnEnable()
        {
            RebindButton(_backButton, ClickBack);
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_backButton, ClickBack);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_catalogView, nameof(_catalogView));
            ValidateSerializedReference(_detailView, nameof(_detailView));
            ValidateSerializedReference(_actionView, nameof(_actionView));
            ValidateSerializedReference(_backButton, nameof(_backButton));
            ValidateSerializedReference(_backButtonLabel, nameof(_backButtonLabel));
        }
#endif

        private void OnDestroy()
        {
            StopRootEnterMotion();
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
            ApplyRootVisibility();

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

        private void ApplyRootVisibility()
        {
            var becameVisible = !_lastVisibleState && IsVisible;
            var becameHidden = _lastVisibleState && !IsVisible;

            if (becameVisible)
            {
                _lastVisibleState = true;
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                PlayRootEnterMotion();
                return;
            }

            if (becameHidden || !IsVisible)
            {
                StopRootEnterMotion();
            }

            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            _lastVisibleState = IsVisible;
        }

        private void PlayRootEnterMotion()
        {
            _rootCanvasGroup = ScreenEnterTweenUtility.EnsureCanvasGroup(_root, _rootCanvasGroup);
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            _enterTween = ScreenEnterTweenUtility.PlayEnterFade(_rootCanvasGroup, out _rootRestAlpha);
            _hasRootRestAlpha = _rootCanvasGroup != null;
        }

        private void StopRootEnterMotion()
        {
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            if (_hasRootRestAlpha)
            {
                ScreenEnterTweenUtility.RestoreAlpha(_rootCanvasGroup, _rootRestAlpha);
            }
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

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(InventoryScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
