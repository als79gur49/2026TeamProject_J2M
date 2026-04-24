using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class HUDRootView : MonoBehaviour
    {
        private const float HudNormalAlpha = 1.0f;
        private const float HudDimmedAlpha = 0.82f;
        private const float HudDimTweenDurationSeconds = 0.12f;
        private const Ease HudDimTweenEase = Ease.OutQuad;
        private const bool HudDimUseUnscaledTime = true;

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _shellCanvasGroup;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private PlayerStatusView _playerStatusView;
        [SerializeField] private ActionBarView _actionBarView;
        [SerializeField] private NotificationView _notificationView;

        private HUDRootViewModel _viewModel;
        private bool _isVisible = true;
        private Tween _shellTween;
        private bool _lastRootVisibleState;
        private bool _hasShellAlphaTarget;
        private float _lastShellAlphaTarget = HudNormalAlpha;

        public event Action PauseRequested;

        public PlayerStatusView PlayerStatusView => _playerStatusView;

        public ActionBarView ActionBarView => _actionBarView;

        public NotificationView NotificationView => _notificationView;

        public HUDRootViewModel ViewModel => _viewModel;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(HUDRootViewModel viewModel)
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

        public void ClickPause()
        {
            if (!IsVisible || _viewModel == null || !_viewModel.IsPauseButtonEnabled)
            {
                return;
            }

            PauseRequested?.Invoke();
        }

        private void OnEnable()
        {
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(ClickPause);
                _pauseButton.onClick.AddListener(ClickPause);
            }

            RefreshView();
        }

        private void OnDisable()
        {
            KillShellTween();
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(ClickPause);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_shellCanvasGroup, nameof(_shellCanvasGroup));
            ValidateSerializedReference(_pauseButton, nameof(_pauseButton));
            ValidateSerializedReference(_playerStatusView, nameof(_playerStatusView));
            ValidateSerializedReference(_actionBarView, nameof(_actionBarView));
            ValidateSerializedReference(_notificationView, nameof(_notificationView));
        }
#endif

        private void OnDestroy()
        {
            KillShellTween();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(ClickPause);
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            var isRootVisible = IsVisible && (_viewModel == null || _viewModel.IsVisible);
            var targetShellAlpha = _viewModel != null && _viewModel.IsDimmed ? HudDimmedAlpha : HudNormalAlpha;
            var becameVisible = !_lastRootVisibleState && isRootVisible;
            var becameHidden = _lastRootVisibleState && !isRootVisible;

            if (becameVisible)
            {
                _lastRootVisibleState = true;
                _hasShellAlphaTarget = true;
                _lastShellAlphaTarget = targetShellAlpha;
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                ApplyShellAlphaImmediate(targetShellAlpha);
            }
            else
            {
                if (becameHidden || !isRootVisible)
                {
                    KillShellTween();
                    ApplyShellAlphaImmediate(targetShellAlpha);
                }

                if (_root != null)
                {
                    _root.SetActive(isRootVisible);
                }

                if (isRootVisible)
                {
                    if (!_hasShellAlphaTarget)
                    {
                        ApplyShellAlphaImmediate(targetShellAlpha);
                    }
                    else if (!Mathf.Approximately(_lastShellAlphaTarget, targetShellAlpha))
                    {
                        PlayShellAlphaTween(targetShellAlpha);
                    }

                    _hasShellAlphaTarget = true;
                    _lastShellAlphaTarget = targetShellAlpha;
                }
                else
                {
                    _hasShellAlphaTarget = true;
                    _lastShellAlphaTarget = targetShellAlpha;
                }

                _lastRootVisibleState = isRootVisible;
            }

            if (_pauseButton != null)
            {
                _pauseButton.interactable = _viewModel != null && _viewModel.IsPauseButtonEnabled;
            }
        }

        private void ApplyShellAlphaImmediate(float alpha)
        {
            if (_shellCanvasGroup == null)
            {
                return;
            }

            _shellCanvasGroup.alpha = alpha;
        }

        private void PlayShellAlphaTween(float targetAlpha)
        {
            if (_shellCanvasGroup == null)
            {
                return;
            }

            KillShellTween();
            _shellTween = _shellCanvasGroup
                .DOFade(targetAlpha, HudDimTweenDurationSeconds)
                .SetEase(HudDimTweenEase)
                .SetUpdate(HudDimUseUnscaledTime);
        }

        private void KillShellTween()
        {
            if (_shellTween == null)
            {
                return;
            }

            _shellTween.Kill();
            _shellTween = null;
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(HUDRootView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
