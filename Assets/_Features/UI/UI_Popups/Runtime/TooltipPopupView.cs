using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class TooltipPopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField] private Button _dismissButton;

        private TooltipPopupViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;

        public event Action<PopupCompletionKind> CompletionRequested;

        public string TitleText => _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string BodyText => _viewModel != null ? _viewModel.BodyText : string.Empty;

        public TooltipPopupAnchorPreset AnchorPreset => _viewModel != null
            ? _viewModel.AnchorPreset
            : TooltipPopupAnchorPreset.Center;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(TooltipPopupViewModel viewModel)
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
            if (_dismissButton != null)
            {
                _dismissButton.onClick.RemoveListener(ClickDismiss);
                _dismissButton.onClick.AddListener(ClickDismiss);
            }

            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            if (_dismissButton != null)
            {
                _dismissButton.onClick.RemoveListener(ClickDismiss);
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

        public void ClickDismiss()
        {
            if (!IsVisible || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Closed);
        }

        private void OnDestroy()
        {
            StopRootEnterMotion();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            if (_dismissButton != null)
            {
                _dismissButton.onClick.RemoveListener(ClickDismiss);
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

            if (_bodyLabel != null)
            {
                _bodyLabel.text = _viewModel.BodyText;
            }

            ApplyAnchor(_viewModel.AnchorPreset);
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
            _enterTween = PopupEnterTweenUtility.PlayTooltipEnter(_canvasGroup, out _rootRestAlpha);
            _hasRootRestAlpha = _canvasGroup != null;
        }

        private void StopRootEnterMotion()
        {
            PopupEnterTweenUtility.Kill(ref _enterTween);
            if (_hasRootRestAlpha)
            {
                PopupEnterTweenUtility.RestoreAlpha(_canvasGroup, _rootRestAlpha);
            }
        }

        private void ApplyAnchor(TooltipPopupAnchorPreset anchorPreset)
        {
            if (_panelRect == null)
            {
                return;
            }

            switch (anchorPreset)
            {
                case TooltipPopupAnchorPreset.UpperRight:
                    _panelRect.anchorMin = new Vector2(1f, 1f);
                    _panelRect.anchorMax = new Vector2(1f, 1f);
                    _panelRect.pivot = new Vector2(1f, 1f);
                    _panelRect.anchoredPosition = new Vector2(-24f, -24f);
                    break;

                case TooltipPopupAnchorPreset.LowerLeft:
                    _panelRect.anchorMin = new Vector2(0f, 0f);
                    _panelRect.anchorMax = new Vector2(0f, 0f);
                    _panelRect.pivot = new Vector2(0f, 0f);
                    _panelRect.anchoredPosition = new Vector2(24f, 24f);
                    break;

                default:
                    _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    _panelRect.pivot = new Vector2(0.5f, 0.5f);
                    _panelRect.anchoredPosition = new Vector2(0f, 160f);
                    break;
            }
        }
    }
}
