using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PausePopupView : MonoBehaviour, IPopupView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private TMP_Text _resumeButtonLabel;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TMP_Text _settingsButtonLabel;

        private PausePopupViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private bool _hasRootRestScale;
        private float _rootRestAlpha = 1f;
        private Vector3 _rootRestScale = Vector3.one;

        public event Action<PopupCompletionKind> CompletionRequested;

        public string TitleText => _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string DescriptionText => _viewModel != null ? _viewModel.DescriptionText : string.Empty;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(PausePopupViewModel viewModel)
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
            RebindButton(_resumeButton, ClickResume);
            RebindButton(_settingsButton, ClickSettings);

            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_resumeButton, ClickResume);
            UnbindButton(_settingsButton, ClickSettings);
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

        public void ClickResume()
        {
            if (!IsVisible || _viewModel == null || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.Resumed);
        }

        public void ClickSettings()
        {
            if (!IsVisible || _viewModel == null || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.SettingsRequested);
        }

        private void OnDestroy()
        {
            StopRootEnterMotion();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindButton(_resumeButton, ClickResume);
            UnbindButton(_settingsButton, ClickSettings);
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

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = _viewModel.DescriptionText;
            }

            if (_resumeButtonLabel != null)
            {
                _resumeButtonLabel.text = _viewModel.ResumeLabel;
            }

            if (_settingsButtonLabel != null)
            {
                _settingsButtonLabel.text = _viewModel.SettingsLabel;
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
    }
}
