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
        [SerializeField] private TMP_Text _warningLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _confirmButtonLabel;
        [SerializeField] private TMP_Text _cancelButtonLabel;
        [SerializeField] private Image _confirmButtonImage;
        [SerializeField] private bool _preserveAuthoredConfirmColor;
        [SerializeField] private bool _confirmActionOnLeft;
        [SerializeField] private CanvasGroup _modeDescriptionPanel;
        [SerializeField] private UiSelectableButtonGroup _actionNavigationGroup = new UiSelectableButtonGroup();

        private bool _confirmEnabled = true;
        private bool _cancelEnabled = true;
        private bool _consumeBack;
        private bool _secondaryIsAlternative;
        private bool _navigationFocusVisible;
        private int _hoveredModeIndex = -1;
        public void ConfigureAlternativeAction(bool enabled) => _secondaryIsAlternative = enabled;
        public void ConfigureActions(bool confirmEnabled, bool cancelEnabled, bool consumeBack)
        {
            _confirmEnabled = confirmEnabled;
            _cancelEnabled = cancelEnabled;
            _consumeBack = consumeBack;
            if (_confirmButton != null) _confirmButton.interactable = confirmEnabled;
            if (_cancelButton != null) _cancelButton.interactable = cancelEnabled;
        }

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

        public string WarningText => _viewModel != null ? _viewModel.WarningText : string.Empty;

        public bool CanHandleUiNavigation => IsVisible && isActiveAndEnabled && _canvasGroup != null && _canvasGroup.interactable;

        public int SelectedActionIndex => _actionNavigationGroup != null ? _actionNavigationGroup.SelectedIndex : 0;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
                WireModeHover(value);
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
                _warningLabel,
                _confirmButtonLabel,
                _cancelButtonLabel,
            };
        }

        private void OnEnable()
        {
            WireModeHover(true);
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
            WireModeHover(false);
            _hoveredModeIndex = -1;
            _navigationFocusVisible = false;
            RefreshModeDescription();
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

            bool changed;
            switch (command)
            {
                case UiNavigationCommand.Left:
                    changed = _actionNavigationGroup.SetSelectedIndex(
                        _confirmActionOnLeft ? ConfirmSelectionIndex : CancelSelectionIndex);
                    break;

                case UiNavigationCommand.Right:
                    changed = _actionNavigationGroup.SetSelectedIndex(
                        _confirmActionOnLeft ? CancelSelectionIndex : ConfirmSelectionIndex);
                    break;

                default:
                    return false;
            }

            _hoveredModeIndex = -1;
            RefreshModeDescription();
            return changed;
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
            if (_consumeBack) return true;
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            if (CanEmit()) CompletionRequested?.Invoke(PopupCompletionKind.Cancelled);
            return true;
        }

        public void OnNavigationFocusGained()
        {
            _navigationFocusVisible = true;
            _actionNavigationGroup?.RefreshVisuals();
            RefreshModeDescription();
        }

        public void OnNavigationFocusLost()
        {
            _navigationFocusVisible = false;
            _actionNavigationGroup?.HideAllFrames();
            RefreshModeDescription();
        }

        public void ClickConfirm()
        {
            if (!_confirmEnabled || !CanEmit())
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Confirmed);
        }

        public void ClickCancel()
        {
            if (!_cancelEnabled || !CanEmit())
            {
                return;
            }

            CompletionRequested?.Invoke(_secondaryIsAlternative ? PopupCompletionKind.AlternativeSelected : PopupCompletionKind.Cancelled);
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

            var warningText = _viewModel != null ? _viewModel.WarningText : string.Empty;
            if (_warningLabel != null)
            {
                _warningLabel.text = warningText;
                _warningLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(warningText));
            }

            if (_viewModel == null)
            {
                RefreshModeDescription();
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

            if (_confirmButtonImage != null && !_preserveAuthoredConfirmColor)
            {
                _confirmButtonImage.color = _viewModel.IsConfirmDestructive
                    ? new Color(0.62f, 0.21f, 0.21f, 1f)
                    : new Color(0.20f, 0.25f, 0.34f, 1f);
            }

            RefreshModeDescription();
        }

        private void ResetDefaultSelection()
        {
            _hoveredModeIndex = -1;
            _navigationFocusVisible = false;
            if (_actionNavigationGroup == null)
            {
                return;
            }

            _actionNavigationGroup.SetSelectedIndexSilently(
                _confirmActionOnLeft ? ConfirmSelectionIndex : CancelSelectionIndex);
            _actionNavigationGroup.HideAllFrames();
        }

        private void WireModeHover(bool subscribe)
        {
            if (_modeDescriptionPanel == null)
            {
                return;
            }

            WireModeHover(_confirmButton, subscribe);
            WireModeHover(_cancelButton, subscribe);
        }

        private void WireModeHover(Button button, bool subscribe)
        {
            var relay = button != null ? button.GetComponent<ModeOptionHoverRelay>() : null;
            if (relay == null)
            {
                return;
            }

            relay.HoverEntered -= HandleModeHoverEntered;
            relay.HoverExited -= HandleModeHoverExited;
            if (subscribe)
            {
                relay.HoverEntered += HandleModeHoverEntered;
                relay.HoverExited += HandleModeHoverExited;
            }
        }

        private void HandleModeHoverEntered(int index)
        {
            if (!CanHandleUiNavigation || _modeDescriptionPanel == null)
            {
                return;
            }

            _hoveredModeIndex = index;
            _actionNavigationGroup?.SetSelectedIndexSilently(index);
            if (_navigationFocusVisible)
            {
                _actionNavigationGroup?.RefreshVisuals();
            }

            RefreshModeDescription();
        }

        private void HandleModeHoverExited(int index)
        {
            if (_hoveredModeIndex != index)
            {
                return;
            }

            _hoveredModeIndex = -1;
            RefreshModeDescription();
        }

        private void RefreshModeDescription()
        {
            if (_modeDescriptionPanel == null)
            {
                return;
            }

            var show = _viewModel != null && CanHandleUiNavigation &&
                (_hoveredModeIndex >= 0 || _navigationFocusVisible);
            var index = _hoveredModeIndex >= 0
                ? _hoveredModeIndex
                : _actionNavigationGroup != null ? _actionNavigationGroup.SelectedIndex : ConfirmSelectionIndex;
            _modeDescriptionPanel.alpha = show ? 1f : 0f;
            _modeDescriptionPanel.blocksRaycasts = false;
            _modeDescriptionPanel.interactable = false;
            if (_bodyLabel != null)
            {
                _bodyLabel.gameObject.SetActive(show && index == ConfirmSelectionIndex);
            }

            if (_warningLabel != null)
            {
                _warningLabel.gameObject.SetActive(show && index == CancelSelectionIndex &&
                    !string.IsNullOrWhiteSpace(_warningLabel.text));
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
