using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PopupLayerView : MonoBehaviour
    {
        private const float BackdropDimAlpha = 0.58f;
        private const float BackdropOpenFadeDurationSeconds = 0.15f;
        private const Ease BackdropOpenFadeEase = Ease.OutQuad;
        private const bool BackdropOpenUseUnscaledTime = true;

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _backdropCanvasGroup;
        [SerializeField] private Image _backdropImage;
        [SerializeField] private Button _backdropButton;
        [SerializeField] private RectTransform _contentRoot;

        private bool _isVisible;
        private Tween _backdropTween;
        private bool _lastVisibleState;

        public event Action BackdropClicked;

        public RectTransform ContentRoot => _contentRoot;

        public bool IsDimVisible { get; private set; }

        public bool BlocksLowerLayerPointer { get; private set; }

        public PopupBackdropMode BackdropMode { get; private set; }

        public void Configure(
            GameObject root,
            CanvasGroup backdropCanvasGroup,
            Image backdropImage,
            Button backdropButton,
            RectTransform contentRoot)
        {
            _root = root;
            _backdropCanvasGroup = backdropCanvasGroup;
            _backdropImage = backdropImage;
            _backdropButton = backdropButton;
            _contentRoot = contentRoot;

            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(HandleBackdropClicked);
                _backdropButton.onClick.AddListener(HandleBackdropClicked);
            }

            RefreshView();
        }

        private void OnEnable()
        {
            RefreshView();
        }

        private void OnDisable()
        {
            StopBackdropTween();
        }

        private void OnDestroy()
        {
            StopBackdropTween();
            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(HandleBackdropClicked);
            }
        }

        public T FindPopupView<T>() where T : Component
        {
            if (_contentRoot == null)
            {
                return null;
            }

            return _contentRoot.GetComponentInChildren<T>(true);
        }

        public void SetState(
            bool isVisible,
            bool showDim,
            bool blocksLowerLayerPointer,
            PopupBackdropMode backdropMode)
        {
            _isVisible = isVisible;
            IsDimVisible = showDim;
            BlocksLowerLayerPointer = blocksLowerLayerPointer;
            BackdropMode = backdropMode;
            RefreshView();
        }

        private void HandleBackdropClicked()
        {
            if (BackdropMode == PopupBackdropMode.None)
            {
                return;
            }

            BackdropClicked?.Invoke();
        }

        private void RefreshView()
        {
            var targetAlpha = IsDimVisible ? BackdropDimAlpha : 0f;
            var becameVisible = !_lastVisibleState && _isVisible;
            var becameHidden = _lastVisibleState && !_isVisible;

            if (becameVisible)
            {
                _lastVisibleState = true;
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                ApplyBackdropInteractionState();
                if (targetAlpha > 0f)
                {
                    PlayBackdropOpenFade(targetAlpha);
                }
                else
                {
                    StopBackdropTween();
                    ApplyBackdropVisualAlpha(0f);
                }

                return;
            }

            if (becameHidden || !_isVisible)
            {
                StopBackdropTween();
            }
            else if (_isVisible)
            {
                StopBackdropTween();
            }

            if (_root != null)
            {
                _root.SetActive(_isVisible);
            }

            ApplyBackdropInteractionState();
            ApplyBackdropVisualAlpha(_isVisible ? targetAlpha : 0f);
            _lastVisibleState = _isVisible;
        }

        private void ApplyBackdropInteractionState()
        {
            if (_backdropImage != null)
            {
                _backdropImage.raycastTarget = BlocksLowerLayerPointer || BackdropMode != PopupBackdropMode.None;
            }

            if (_backdropCanvasGroup != null)
            {
                _backdropCanvasGroup.alpha = IsDimVisible ? 1f : 0f;
                _backdropCanvasGroup.interactable = BackdropMode != PopupBackdropMode.None;
                _backdropCanvasGroup.blocksRaycasts = BlocksLowerLayerPointer || BackdropMode != PopupBackdropMode.None;
            }
        }

        private void ApplyBackdropVisualAlpha(float alpha)
        {
            if (_backdropImage == null)
            {
                return;
            }

            var color = _backdropImage.color;
            color.a = alpha;
            _backdropImage.color = color;
        }

        private void PlayBackdropOpenFade(float targetAlpha)
        {
            StopBackdropTween();
            ApplyBackdropVisualAlpha(0f);
            _backdropTween = DOTween
                .To(GetBackdropVisualAlpha, ApplyBackdropVisualAlpha, targetAlpha, BackdropOpenFadeDurationSeconds)
                .SetEase(BackdropOpenFadeEase)
                .SetUpdate(BackdropOpenUseUnscaledTime);
        }

        private float GetBackdropVisualAlpha()
        {
            return _backdropImage != null ? _backdropImage.color.a : 0f;
        }

        private void StopBackdropTween()
        {
            if (_backdropTween == null)
            {
                return;
            }

            _backdropTween.Kill();
            _backdropTween = null;
        }
    }
}
