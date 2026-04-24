using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class RewardPopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _firstItemLabel;
        [SerializeField] private TMP_Text _secondItemLabel;
        [SerializeField] private TMP_Text _thirdItemLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _closeButtonLabel;

        private RewardPopupViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private bool _hasRootRestScale;
        private float _rootRestAlpha = 1f;
        private Vector3 _rootRestScale = Vector3.one;

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
            StopRootEnterMotion();
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
            StopRootEnterMotion();
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
            ApplyRootVisibility();

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
            PopupEnterTweenUtility.Kill(ref _enterTween);
            _enterTween = PopupEnterTweenUtility.PlayModalEnter(
                _canvasGroup,
                _root != null ? _root.transform : null,
                out _rootRestAlpha,
                out _rootRestScale);
            _hasRootRestAlpha = _canvasGroup != null;
            _hasRootRestScale = _root != null;
        }

        private void StopRootEnterMotion()
        {
            PopupEnterTweenUtility.Kill(ref _enterTween);
            if (_hasRootRestAlpha)
            {
                PopupEnterTweenUtility.RestoreAlpha(_canvasGroup, _rootRestAlpha);
            }

            if (_hasRootRestScale)
            {
                PopupEnterTweenUtility.RestoreScale(_root != null ? _root.transform : null, _rootRestScale);
            }
        }
    }
}
