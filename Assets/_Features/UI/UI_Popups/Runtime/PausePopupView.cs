using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PausePopupView : MonoBehaviour, IPopupView, IUiNavigationTarget
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private TMP_Text _resumeButtonLabel;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TMP_Text _settingsButtonLabel;
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _retryButtonLabel;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private TMP_Text _mainMenuButtonLabel;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();

        private PausePopupViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private bool _hasRootRestScale;
        private List<PausePopupLocalizedTmpTextBinding> _localizedStaticBindings;
        private bool _hasExternalStaticLocalization;
        private float _rootRestAlpha = 1f;
        private Vector3 _rootRestScale = Vector3.one;

        public event Action<PopupCompletionKind> CompletionRequested;

        public bool CanHandleUiNavigation => IsVisible && isActiveAndEnabled && _canvasGroup != null && _canvasGroup.interactable;

        public string TitleText => _titleLabel != null ? _titleLabel.text : _viewModel != null ? _viewModel.TitleText : string.Empty;

        public string DescriptionText => _descriptionLabel != null ? _descriptionLabel.text : _viewModel != null ? _viewModel.DescriptionText : string.Empty;

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

        public void BindStaticLocalization(
            PausePopupPayload payload,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver)
        {
            UnbindStaticLocalization();
            if (payload == null)
            {
                return;
            }

            _localizedStaticBindings = new List<PausePopupLocalizedTmpTextBinding>
            {
                new(_titleLabel, payload.TitleTextDescriptor, textResolver, typographyResolver),
                new(_descriptionLabel, payload.DescriptionTextDescriptor, textResolver, typographyResolver),
                new(_resumeButtonLabel, payload.ResumeLabelDescriptor, textResolver, typographyResolver),
                new(_settingsButtonLabel, payload.SettingsLabelDescriptor, textResolver, typographyResolver),
                new(_retryButtonLabel, payload.RetryLabelDescriptor, textResolver, typographyResolver),
                new(_mainMenuButtonLabel, payload.MainMenuLabelDescriptor, textResolver, typographyResolver),
            };
        }

        public IReadOnlyList<PausePopupLocalizedTextTarget> CreateStaticLocalizationTargets(PausePopupPayload payload)
        {
            if (payload == null)
            {
                return Array.Empty<PausePopupLocalizedTextTarget>();
            }

            return new[]
            {
                new PausePopupLocalizedTextTarget(_titleLabel, payload.TitleTextDescriptor),
                new PausePopupLocalizedTextTarget(_descriptionLabel, payload.DescriptionTextDescriptor),
                new PausePopupLocalizedTextTarget(_resumeButtonLabel, payload.ResumeLabelDescriptor),
                new PausePopupLocalizedTextTarget(_settingsButtonLabel, payload.SettingsLabelDescriptor),
                new PausePopupLocalizedTextTarget(_retryButtonLabel, payload.RetryLabelDescriptor),
                new PausePopupLocalizedTextTarget(_mainMenuButtonLabel, payload.MainMenuLabelDescriptor),
            };
        }

        public void BindExternalStaticLocalization()
        {
            DisposeLocalizedStaticBindings();
            _hasExternalStaticLocalization = true;
            RefreshView();
        }

        public void UnbindStaticLocalization()
        {
            _hasExternalStaticLocalization = false;
            DisposeLocalizedStaticBindings();
            RefreshView();
        }

        private void OnEnable()
        {
            RebindButton(_resumeButton, ClickResume);
            RebindButton(_settingsButton, ClickSettings);
            RebindButton(_retryButton, ClickRetry);
            RebindButton(_mainMenuButton, ClickMainMenu);

            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_resumeButton, ClickResume);
            UnbindButton(_settingsButton, ClickSettings);
            UnbindButton(_retryButton, ClickRetry);
            UnbindButton(_mainMenuButton, ClickMainMenu);
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

        public void ClickRetry()
        {
            if (!IsVisible || _viewModel == null || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.RetryRequested);
        }

        public void ClickMainMenu()
        {
            if (!IsVisible || _viewModel == null || _canvasGroup == null || !_canvasGroup.interactable)
            {
                return;
            }

            CompletionRequested?.Invoke(PopupCompletionKind.MainMenuRequested);
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (!CanHandleUiNavigation || _navigationGroup == null)
            {
                return false;
            }

            switch (command)
            {
                case UiNavigationCommand.Up:
                    return _navigationGroup.TryMove(-1);

                case UiNavigationCommand.Down:
                    return _navigationGroup.TryMove(1);

                default:
                    return false;
            }
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            var selected = _navigationGroup != null ? _navigationGroup.GetSelectedButton() : null;
            if (selected == _resumeButton || selected == null)
            {
                _navigationGroup?.PlaySelectedSubmitFeedback();
                ClickResume();
            }
            else if (selected == _settingsButton)
            {
                _navigationGroup?.PlaySelectedSubmitFeedback();
                ClickSettings();
            }
            else if (selected == _retryButton)
            {
                _navigationGroup?.PlaySelectedSubmitFeedback();
                ClickRetry();
            }
            else if (selected == _mainMenuButton)
            {
                _navigationGroup?.PlaySelectedSubmitFeedback();
                ClickMainMenu();
            }
            else
            {
                _navigationGroup?.PlaySelectedSubmitFeedback();
                selected.onClick.Invoke();
            }

            return true;
        }

        public bool HandleCancel()
        {
            return false;
        }

        public void OnNavigationFocusGained()
        {
            _navigationGroup?.SetSelectedIndex(0);
        }

        public void OnNavigationFocusLost()
        {
            _navigationGroup?.HideAllFrames();
        }

        private void OnDestroy()
        {
            StopRootEnterMotion();
            DisposeLocalizedStaticBindings();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindButton(_resumeButton, ClickResume);
            UnbindButton(_settingsButton, ClickSettings);
            UnbindButton(_retryButton, ClickRetry);
            UnbindButton(_mainMenuButton, ClickMainMenu);
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

            if (HasStaticLocalization)
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

            if (_retryButtonLabel != null)
            {
                _retryButtonLabel.text = _viewModel.RetryLabel;
            }

            if (_mainMenuButtonLabel != null)
            {
                _mainMenuButtonLabel.text = _viewModel.MainMenuLabel;
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

        private bool HasStaticLocalization =>
            _hasExternalStaticLocalization ||
            (_localizedStaticBindings != null && _localizedStaticBindings.Count > 0);

        private void DisposeLocalizedStaticBindings()
        {
            if (_localizedStaticBindings == null)
            {
                return;
            }

            for (var i = 0; i < _localizedStaticBindings.Count; i++)
            {
                _localizedStaticBindings[i]?.Dispose();
            }

            _localizedStaticBindings = null;
        }
    }

    public readonly struct PausePopupLocalizedTextTarget
    {
        public PausePopupLocalizedTextTarget(TMP_Text target, LocalizedTextDescriptor descriptor)
        {
            Target = target;
            Descriptor = descriptor;
        }

        public TMP_Text Target { get; }

        public LocalizedTextDescriptor Descriptor { get; }
    }

    internal sealed class PausePopupLocalizedTmpTextBinding : IDisposable
    {
        private readonly TMP_Text _target;
        private readonly LocalizedTextDescriptor _descriptor;
        private readonly ILocalizedTextResolver _textResolver;
        private readonly ILocalizedTypographyResolver _typographyResolver;
        private bool _isDisposed;

        public PausePopupLocalizedTmpTextBinding(
            TMP_Text target,
            LocalizedTextDescriptor descriptor,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver)
        {
            _target = target;
            _descriptor = descriptor;
            _textResolver = textResolver;
            _typographyResolver = typographyResolver ?? DefaultLocalizedTypographyResolver.Instance;

            if (_textResolver != null)
            {
                _textResolver.LocaleChanged += HandleLocaleChanged;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (_isDisposed || _target == null)
            {
                return;
            }

            _target.text = ResolveText();
            var localeCode = _textResolver != null ? _textResolver.CurrentLocaleCode : string.Empty;
            ApplyTypography(
                _target,
                _typographyResolver.Resolve(
                    localeCode,
                    _descriptor.Role,
                    _descriptor.Weight));
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_textResolver != null)
            {
                _textResolver.LocaleChanged -= HandleLocaleChanged;
            }

            _isDisposed = true;
        }

        private string ResolveText()
        {
            if (_textResolver == null)
            {
                return _descriptor.Key;
            }

            return _textResolver.Resolve(_descriptor) ?? string.Empty;
        }

        private static void ApplyTypography(TMP_Text target, LocalizedTypographyStyle style)
        {
            if (target == null)
            {
                return;
            }

            target.fontSize = style.FontSize;
            target.lineSpacing = style.LineSpacing;
            target.fontStyle = style.Bold
                ? target.fontStyle | FontStyles.Bold
                : target.fontStyle & ~FontStyles.Bold;
        }

        private void HandleLocaleChanged()
        {
            Refresh();
        }
    }
}
