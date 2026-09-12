using System;
using DG.Tweening;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    [DisallowMultipleComponent]
    public sealed class PauseStagePreviewOverlayView : MonoBehaviour, IUiNavigationTarget
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _stageNameLabel;
        [SerializeField] private Image _previewImage;
        [SerializeField] private Button _closeButton;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();
        [SerializeField, Min(0f)] private float _openDurationSeconds = 0.18f;
        [SerializeField, Min(0f)] private float _closeDurationSeconds = 0.12f;
        [SerializeField, Range(0.1f, 1f)] private float _closedScale = 0.85f;

        private Tween _transitionTween;
        private bool _isOpen;
        private bool _isClosing;
        private bool _isNavigationFocusRevealed;

        public event Action CloseRequested;

        public event Action Closed;

        public bool IsOpen => _isOpen;

        public TMP_Text StageNameLabel => _stageNameLabel;

        public bool CanHandleUiNavigation =>
            _isOpen &&
            !_isClosing &&
            isActiveAndEnabled &&
            _canvasGroup != null &&
            _canvasGroup.interactable;

        private void OnEnable()
        {
            BindCloseButton();
        }

        private void OnDisable()
        {
            StopTransition();
            OnNavigationFocusLost();
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        private void OnDestroy()
        {
            StopTransition();
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        public void Open(PauseStagePreviewSelection selection)
        {
            StopTransition();
            OnNavigationFocusLost();
            BindCloseButton();
            _isOpen = true;
            _isClosing = false;

            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_previewImage != null)
            {
                _previewImage.sprite = selection.PreviewSprite;
                _previewImage.preserveAspect = true;
                _previewImage.rectTransform.localScale = Vector3.one * _closedScale;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (!isActiveAndEnabled || _openDurationSeconds <= 0f)
            {
                ApplyOpenRestState();
                return;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (_canvasGroup != null)
            {
                sequence.Join(_canvasGroup.DOFade(1f, _openDurationSeconds));
            }

            if (_previewImage != null)
            {
                sequence.Join(_previewImage.rectTransform.DOScale(1f, _openDurationSeconds));
            }

            _transitionTween = sequence
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    _transitionTween = null;
                    ApplyOpenRestState();
                });
        }

        public void Close(bool immediate = false)
        {
            if (!_isOpen || _isClosing)
            {
                return;
            }

            StopTransition();
            OnNavigationFocusLost();
            _isClosing = true;
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = true;
            }

            if (immediate || !isActiveAndEnabled || _closeDurationSeconds <= 0f)
            {
                ApplyClosedState();
                return;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (_canvasGroup != null)
            {
                sequence.Join(_canvasGroup.DOFade(0f, _closeDurationSeconds));
            }

            if (_previewImage != null)
            {
                sequence.Join(_previewImage.rectTransform.DOScale(_closedScale, _closeDurationSeconds));
            }

            _transitionTween = sequence
                .SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    _transitionTween = null;
                    ApplyClosedState();
                });
        }

        public void ResetClosed()
        {
            StopTransition();
            ApplyClosedState();
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            if (!_isNavigationFocusRevealed)
            {
                OnNavigationFocusGained();
            }

            return false;
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            if (!_isNavigationFocusRevealed)
            {
                OnNavigationFocusGained();
                return true;
            }

            _navigationGroup?.PlaySelectedSubmitFeedback();
            RequestClose();
            return true;
        }

        public bool HandleCancel()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            RequestClose();
            return true;
        }

        public void OnNavigationFocusGained()
        {
            if (CanHandleUiNavigation)
            {
                _isNavigationFocusRevealed = true;
                _navigationGroup?.SetSelectedIndex(0);
            }
        }

        public void OnNavigationFocusLost()
        {
            _isNavigationFocusRevealed = false;
            _navigationGroup?.HideAllFrames();
        }

        private void HandleCloseClicked()
        {
            RequestClose();
        }

        private void RequestClose()
        {
            if (CanHandleUiNavigation)
            {
                CloseRequested?.Invoke();
            }
        }

        private void BindCloseButton()
        {
            if (_closeButton == null)
            {
                return;
            }

            _closeButton.onClick.RemoveListener(HandleCloseClicked);
            _closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void ApplyOpenRestState()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_previewImage != null)
            {
                _previewImage.rectTransform.localScale = Vector3.one;
            }
        }

        private void ApplyClosedState()
        {
            var wasOpen = _isOpen;
            _isOpen = false;
            _isClosing = false;
            OnNavigationFocusLost();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_previewImage != null)
            {
                _previewImage.sprite = null;
                _previewImage.rectTransform.localScale = Vector3.one * _closedScale;
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }

            if (wasOpen)
            {
                Closed?.Invoke();
            }
        }

        private void StopTransition()
        {
            _transitionTween?.Kill();
            _transitionTween = null;
        }
    }
}
