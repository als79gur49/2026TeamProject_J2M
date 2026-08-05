using System;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class GameClearScreenView : MonoBehaviour, IScreenView, IUiNavigationTarget, IResultTransitionScreenView
    {
        private const int MainSelectionIndex = 0;

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Button _mainButton;
        [SerializeField] private TMP_Text _mainButtonLabel;
        [SerializeField] private CanvasGroup _backdropRoot;
        [SerializeField] private Image _resultBackdrop;
        [SerializeField] private Image _resultHandoffCover;
        [SerializeField] private CanvasGroup _resultHandoffCoverGroup;
        [SerializeField] private CanvasGroup _contentRoot;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();

        private GameClearScreenViewModel _viewModel;
        private bool _isVisible;
        private ResultTransitionScreenPresentation _resultTransition;

        public event Action MainRequested;

        public bool CanHandleUiNavigation => IsVisible && isActiveAndEnabled && IsInteractionReady;

        public bool IsBackdropReady => IsHandoffCoverRendered;

        public bool IsHandoffCoverRendered => _resultTransition?.IsHandoffCoverRendered ?? false;

        public bool CanBeginContentEntrance => _resultTransition?.CanBeginContentEntrance ?? false;

        public bool IsHandoffFadeComplete => _resultTransition?.IsHandoffFadeComplete ?? false;

        public bool IsContentEntranceComplete => _resultTransition?.IsContentEntranceComplete ?? false;

        public bool IsInteractionReady => _resultTransition?.IsInteractionReady ?? false;

        public Color BackdropColor => _resultTransition?.ResultBackdropColor ?? Color.clear;

        public float HandoffCoverAlpha => _resultTransition?.ResultHandoffCoverAlpha ?? 0f;

        public float ContentAlpha => _resultTransition?.ContentRootAlpha ?? 0f;

        public bool IsHandoffCoverActive => _resultTransition?.IsHandoffCoverActive ?? false;

        internal Color ResultBackdropColorForTests =>
            BackdropColor;

        internal float ResultHandoffCoverAlphaForTests =>
            HandoffCoverAlpha;

        internal float ContentRootAlphaForTests =>
            ContentAlpha;

        internal bool IsHandoffCoverActiveForTests =>
            IsHandoffCoverActive;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(GameClearScreenViewModel viewModel)
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

        public void ApplyLocalizedTypography(
            string localeCode,
            GameplayUiTypographyTheme typographyTheme)
        {
            TerminalScreenTypographyUtility.Apply(
                _titleLabel,
                localeCode,
                typographyTheme,
                TypographyStyleTag.HeaderLarge);
            TerminalScreenTypographyUtility.Apply(
                _mainButtonLabel,
                localeCode,
                typographyTheme,
                TypographyStyleTag.Button);
        }

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickMain()
        {
            if (!IsVisible || !IsInteractionReady)
            {
                return;
            }

            MainRequested?.Invoke();
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            return false;
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            _navigationGroup?.PlaySelectedSubmitFeedback();
            ClickMain();
            return true;
        }

        public bool HandleCancel()
        {
            return false;
        }

        public void OnNavigationFocusGained()
        {
            _navigationGroup?.SetSelectedIndex(MainSelectionIndex);
        }

        public void OnNavigationFocusLost()
        {
            _navigationGroup?.HideAllFrames();
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
            RebindButton(_mainButton, ClickMain);
            RefreshView();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
            UnbindButton(_mainButton, ClickMain);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_mainButton, nameof(_mainButton));
            ValidateSerializedReference(_mainButtonLabel, nameof(_mainButtonLabel));
            ValidateSerializedReference(_backdropRoot, nameof(_backdropRoot));
            ValidateSerializedReference(_resultBackdrop, nameof(_resultBackdrop));
            ValidateSerializedReference(_resultHandoffCover, nameof(_resultHandoffCover));
            ValidateSerializedReference(_resultHandoffCoverGroup, nameof(_resultHandoffCoverGroup));
            ValidateSerializedReference(_contentRoot, nameof(_contentRoot));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
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
                _root.SetActive(IsVisible);
            }

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_mainButtonLabel != null)
            {
                _mainButtonLabel.text = _viewModel.MainLabel;
            }

            _resultTransition?.RefreshPrimaryAction();
        }

        public void ConfigureDimSnapshot(
            ResultDimVisualSnapshot snapshot,
            ResultTransitionRuntimeStyle runtimeStyle)
        {
            EnsureResultTransition().Configure(snapshot, runtimeStyle);
        }

        public void PrepareOpaqueHandoff()
        {
            EnsureResultTransition().PrepareOpaqueHandoff();
        }

        public bool BeginHandoffFade()
        {
            return EnsureResultTransition().BeginHandoffFade();
        }

        public bool BeginContentEntrance()
        {
            return EnsureResultTransition().BeginContentEntrance();
        }

        public void AdvanceResultTransition(float unscaledDeltaTime)
        {
            EnsureResultTransition().Tick(unscaledDeltaTime);
        }

        public void ResetTransitionState()
        {
            EnsureResultTransition().ResetTransitionState();
        }

        private void HandleWillRenderCanvases()
        {
            _resultTransition?.ObserveCanvasRender();
        }

        private ResultTransitionScreenPresentation EnsureResultTransition()
        {
            return _resultTransition ??= new ResultTransitionScreenPresentation(
                _backdropRoot,
                _resultBackdrop,
                _resultHandoffCover,
                _resultHandoffCoverGroup,
                _contentRoot,
                _mainButton,
                () => true);
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

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(GameClearScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
