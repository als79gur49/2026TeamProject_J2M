using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class ConfirmPopupView : MonoBehaviour, IPopupView, IUiNavigationTarget
    {
        private const int ConfirmSelectionIndex = 0;
        private const int CancelSelectionIndex = 1;

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _confirmButtonLabel;
        [SerializeField] private TMP_Text _cancelButtonLabel;
        [SerializeField] private Image _confirmButtonImage;
        [SerializeField] private UiSelectableButtonGroup _actionNavigationGroup = new UiSelectableButtonGroup();

        private ConfirmPopupViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private bool _hasRootRestScale;
        private float _rootRestAlpha = 1f;
        private Vector3 _rootRestScale = Vector3.one;

        public event Action<PopupCompletionKind> CompletionRequested;

        public string TitleText => _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string BodyText => _viewModel != null ? _viewModel.BodyText : string.Empty;

        public bool CanHandleUiNavigation => IsVisible && isActiveAndEnabled && _canvasGroup != null && _canvasGroup.interactable;

        public int SelectedActionIndex => _actionNavigationGroup != null ? _actionNavigationGroup.SelectedIndex : 0;

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

            ResetDefaultSelection();
            RefreshView();
        }

        public IReadOnlyList<TMP_Text> CreateTypographyTargets()
        {
            return new[]
            {
                _titleLabel,
                _bodyLabel,
                _confirmButtonLabel,
                _cancelButtonLabel,
            };
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

            ResetDefaultSelection();
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
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
            if (!isTopmost)
            {
                OnNavigationFocusLost();
            }
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (!CanHandleUiNavigation || _actionNavigationGroup == null)
            {
                return false;
            }

            switch (command)
            {
                case UiNavigationCommand.Left:
                    return _actionNavigationGroup.SetSelectedIndex(CancelSelectionIndex);

                case UiNavigationCommand.Right:
                    return _actionNavigationGroup.SetSelectedIndex(ConfirmSelectionIndex);

                default:
                    return false;
            }
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation || _actionNavigationGroup == null)
            {
                return false;
            }

            if (_actionNavigationGroup.SelectedIndex == CancelSelectionIndex)
            {
                _actionNavigationGroup.PlaySelectedSubmitFeedback();
                ClickCancel();
                return true;
            }

            _actionNavigationGroup.PlaySelectedSubmitFeedback();
            ClickConfirm();
            return true;
        }

        public bool HandleCancel()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            ClickCancel();
            return true;
        }

        public void OnNavigationFocusGained()
        {
            _actionNavigationGroup?.RefreshVisuals();
        }

        public void OnNavigationFocusLost()
        {
            _actionNavigationGroup?.HideAllFrames();
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
            StopRootEnterMotion();
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
            ResetDefaultSelection();
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

        private void ResetDefaultSelection()
        {
            if (_actionNavigationGroup == null)
            {
                return;
            }

            _actionNavigationGroup.SetSelectedIndexSilently(CancelSelectionIndex);
            _actionNavigationGroup.HideAllFrames();
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
