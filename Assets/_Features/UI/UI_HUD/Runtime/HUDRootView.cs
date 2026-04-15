using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class HUDRootView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _shellCanvasGroup;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private PlayerStatusView _playerStatusView;
        [SerializeField] private ActionBarView _actionBarView;
        [SerializeField] private NotificationView _notificationView;

        private HUDRootViewModel _viewModel;
        private bool _isVisible = true;

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
            if (_root != null)
            {
                _root.SetActive(IsVisible && (_viewModel == null || _viewModel.IsVisible));
            }

            if (_shellCanvasGroup != null)
            {
                _shellCanvasGroup.alpha = _viewModel != null && _viewModel.IsDimmed ? 0.82f : 1f;
            }

            if (_pauseButton != null)
            {
                _pauseButton.interactable = _viewModel != null && _viewModel.IsPauseButtonEnabled;
            }
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
