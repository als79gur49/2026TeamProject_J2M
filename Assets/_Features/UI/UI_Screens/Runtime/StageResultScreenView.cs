using System;
using DG.Tweening;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class StageResultScreenView : MonoBehaviour, IScreenView, IUiNavigationTarget, IResultTransitionScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TMP_Text _continueButtonLabel;
        [SerializeField] private CanvasGroup _backdropRoot;
        [SerializeField] private Image _resultBackdrop;
        [SerializeField] private Image _resultHandoffCover;
        [SerializeField] private CanvasGroup _resultHandoffCoverGroup;
        [SerializeField] private CanvasGroup _contentRoot;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();

        private StageResultScreenViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;
        private ResultTransitionScreenPresentation _resultTransition;

        public event Action ContinueRequested;

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

        public void Bind(StageResultScreenViewModel viewModel)
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
                _continueButtonLabel,
                localeCode,
                typographyTheme,
                TypographyStyleTag.Button);
        }

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickContinue()
        {
            if (!IsVisible || !IsInteractionReady || (_viewModel != null && !_viewModel.IsContinueEnabled))
            {
                return;
            }

            ContinueRequested?.Invoke();
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
            ClickContinue();
            return true;
        }

        public bool HandleCancel()
        {
            return false;
        }

        public void OnNavigationFocusGained()
        {
            _navigationGroup?.SetSelectedIndex(0);
            if (_continueButton != null &&
                _continueButton.IsActive() &&
                _continueButton.IsInteractable())
            {
                _continueButton.Select();
            }
        }

        public void OnNavigationFocusLost()
        {
            _navigationGroup?.HideAllFrames();
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
            RebindButton(_continueButton, ClickContinue);
            RefreshView();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
            StopRootEnterMotion();
            UnbindButton(_continueButton, ClickContinue);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_continueButton, nameof(_continueButton));
            ValidateSerializedReference(_continueButtonLabel, nameof(_continueButtonLabel));
            ValidateSerializedReference(_backdropRoot, nameof(_backdropRoot));
            ValidateSerializedReference(_resultBackdrop, nameof(_resultBackdrop));
            ValidateSerializedReference(_resultHandoffCover, nameof(_resultHandoffCover));
            ValidateSerializedReference(_resultHandoffCoverGroup, nameof(_resultHandoffCoverGroup));
            ValidateSerializedReference(_contentRoot, nameof(_contentRoot));
        }
#endif

        private void OnDestroy()
        {
            StopRootEnterMotion();
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
            ApplyRootVisibility();

            if (_viewModel == null)
            {
                return;
            }

            if (_continueButton != null)
            {
                EnsureResultTransition().RefreshPrimaryAction();
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_continueButtonLabel != null)
            {
                _continueButtonLabel.text = _viewModel.ContinueLabel;
            }
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
            StopRootEnterMotion();
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
                _continueButton,
                () => _viewModel == null || _viewModel.IsContinueEnabled);
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
            if (_resultTransition != null && !_resultTransition.IsInteractionReady)
            {
                return;
            }

            _rootCanvasGroup = ScreenEnterTweenUtility.EnsureCanvasGroup(_root, _rootCanvasGroup);
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            _enterTween = ScreenEnterTweenUtility.PlayEnterFade(_rootCanvasGroup, out _rootRestAlpha);
            _hasRootRestAlpha = _rootCanvasGroup != null;
        }

        private void StopRootEnterMotion()
        {
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            if (_hasRootRestAlpha)
            {
                ScreenEnterTweenUtility.RestoreAlpha(_rootCanvasGroup, _rootRestAlpha);
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

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(StageResultScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }

    public interface IResultTransitionScreenView
    {
        bool IsBackdropReady { get; }
        bool IsHandoffCoverRendered { get; }
        bool CanBeginContentEntrance { get; }
        bool IsHandoffFadeComplete { get; }
        bool IsContentEntranceComplete { get; }
        bool IsInteractionReady { get; }
        Color BackdropColor { get; }
        float HandoffCoverAlpha { get; }
        float ContentAlpha { get; }
        bool IsHandoffCoverActive { get; }
        void ConfigureDimSnapshot(ResultDimVisualSnapshot snapshot, ResultTransitionRuntimeStyle runtimeStyle);
        void PrepareOpaqueHandoff();
        bool BeginHandoffFade();
        bool BeginContentEntrance();
        void AdvanceResultTransition(float unscaledDeltaTime);
        void ResetTransitionState();
    }

    internal sealed class ResultTransitionScreenPresentation
    {
        private readonly CanvasGroup _backdropRoot;
        private readonly Image _resultBackdrop;
        private readonly Image _resultHandoffCover;
        private readonly CanvasGroup _resultHandoffCoverGroup;
        private readonly CanvasGroup _contentRoot;
        private readonly Selectable _primaryAction;
        private readonly Func<bool> _primaryActionEnabled;
        private ResultDimVisualSnapshot _dimSnapshot;
        private ResultTransitionRuntimeStyle _runtimeStyle;
        private float _handoffElapsed;
        private float _entranceElapsed;
        private int _handoffRequestFrame = -1;
        private bool _configured;
        private bool _prepared;
        private bool _handoffStarted;
        private bool _entranceStarted;

        public ResultTransitionScreenPresentation(
            CanvasGroup backdropRoot,
            Image resultBackdrop,
            Image resultHandoffCover,
            CanvasGroup resultHandoffCoverGroup,
            CanvasGroup contentRoot,
            Selectable primaryAction,
            Func<bool> primaryActionEnabled)
        {
            _backdropRoot = backdropRoot != null ? backdropRoot : throw new ArgumentNullException(nameof(backdropRoot));
            _resultBackdrop = resultBackdrop != null ? resultBackdrop : throw new ArgumentNullException(nameof(resultBackdrop));
            _resultHandoffCover = resultHandoffCover != null
                ? resultHandoffCover
                : throw new ArgumentNullException(nameof(resultHandoffCover));
            _resultHandoffCoverGroup = resultHandoffCoverGroup != null
                ? resultHandoffCoverGroup
                : throw new ArgumentNullException(nameof(resultHandoffCoverGroup));
            _contentRoot = contentRoot != null ? contentRoot : throw new ArgumentNullException(nameof(contentRoot));
            _primaryAction = primaryAction != null ? primaryAction : throw new ArgumentNullException(nameof(primaryAction));
            _primaryActionEnabled = primaryActionEnabled ?? throw new ArgumentNullException(nameof(primaryActionEnabled));
        }

        public bool IsHandoffCoverRendered { get; private set; }
        public bool CanBeginContentEntrance =>
            _prepared &&
            _handoffStarted &&
            !_entranceStarted &&
            _handoffElapsed >= _runtimeStyle.ResultContentEntranceDelay;
        public bool IsHandoffFadeComplete { get; private set; }
        public bool IsContentEntranceComplete { get; private set; }
        public bool IsInteractionReady { get; private set; }

        internal Color ResultBackdropColor => _resultBackdrop.color;

        internal float ResultHandoffCoverAlpha => _resultHandoffCoverGroup.alpha;

        internal float ContentRootAlpha => _contentRoot.alpha;

        internal bool IsHandoffCoverActive => _resultHandoffCover.gameObject.activeInHierarchy;

        public void Configure(
            ResultDimVisualSnapshot snapshot,
            ResultTransitionRuntimeStyle runtimeStyle)
        {
            if (!snapshot.IsFullStretch || snapshot.RaycastTarget)
            {
                throw new InvalidOperationException(
                    "Result transition requires the canonical full-stretch visual-only Dim snapshot.");
            }

            ValidateVisualContract();
            _dimSnapshot = snapshot;
            _runtimeStyle = runtimeStyle;
            _configured = true;
            SetBackdropFinalColor();
            ResetTransitionState();
        }

        public void PrepareOpaqueHandoff()
        {
            RequireConfigured();
            ResetTransitionState();
            _prepared = true;
            IsHandoffCoverRendered = false;
            IsHandoffFadeComplete = false;
            IsContentEntranceComplete = false;
            IsInteractionReady = false;
            _handoffRequestFrame = Time.frameCount;
            _resultHandoffCover.gameObject.SetActive(true);
            _resultHandoffCover.color = _dimSnapshot.OpaqueColor;
            _resultHandoffCover.raycastTarget = false;
            _resultHandoffCoverGroup.alpha = 1f;
            _resultHandoffCoverGroup.interactable = false;
            _resultHandoffCoverGroup.blocksRaycasts = false;
            _resultHandoffCoverGroup.ignoreParentGroups = true;
            SetContent(0f, false);
            Canvas.ForceUpdateCanvases();
        }

        public void ObserveCanvasRender()
        {
            if (!_prepared ||
                Time.frameCount <= _handoffRequestFrame ||
                !_resultHandoffCover.isActiveAndEnabled ||
                !_resultHandoffCover.gameObject.activeInHierarchy ||
                _resultHandoffCoverGroup.alpha < 0.999f ||
                _resultHandoffCover.color.a < 0.999f ||
                _resultHandoffCover.canvas == null ||
                !_resultHandoffCover.canvas.isActiveAndEnabled ||
                _resultHandoffCover.canvasRenderer == null ||
                _resultHandoffCover.canvasRenderer.cull)
            {
                return;
            }

            var rect = _resultHandoffCover.rectTransform;
            if (rect.anchorMin == Vector2.zero &&
                rect.anchorMax == Vector2.one &&
                rect.offsetMin == Vector2.zero &&
                rect.offsetMax == Vector2.zero)
            {
                IsHandoffCoverRendered = true;
            }
        }

        public bool BeginHandoffFade()
        {
            if (!_prepared || !IsHandoffCoverRendered || _handoffStarted)
            {
                return false;
            }

            _handoffStarted = true;
            _handoffElapsed = 0f;
            _resultHandoffCoverGroup.alpha = 1f;
            return true;
        }

        public bool BeginContentEntrance()
        {
            if (!CanBeginContentEntrance)
            {
                return false;
            }

            _entranceStarted = true;
            _entranceElapsed = 0f;
            return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime < 0f ||
                float.IsNaN(unscaledDeltaTime) ||
                float.IsInfinity(unscaledDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (!_prepared || IsInteractionReady)
            {
                return;
            }

            if (_handoffStarted && !IsHandoffFadeComplete)
            {
                _handoffElapsed += unscaledDeltaTime;
                var fadeElapsed = Mathf.Max(
                    0f,
                    _handoffElapsed - _runtimeStyle.ResultHandoffHoldDuration);
                var progress = Mathf.Clamp01(
                    fadeElapsed / _runtimeStyle.ResultHandoffFadeDuration);
                var eased = _runtimeStyle.EvaluateResultHandoff(progress);
                _resultHandoffCoverGroup.alpha =
                    Mathf.Min(_resultHandoffCoverGroup.alpha, 1f - eased);
                if (progress >= 1f)
                {
                    IsHandoffFadeComplete = true;
                    _resultHandoffCoverGroup.alpha = 0f;
                    _resultHandoffCoverGroup.interactable = false;
                    _resultHandoffCoverGroup.blocksRaycasts = false;
                    _resultHandoffCover.gameObject.SetActive(false);
                }
            }

            if (_entranceStarted && !IsContentEntranceComplete)
            {
                _entranceElapsed += unscaledDeltaTime;
                var progress = Mathf.Clamp01(
                    _entranceElapsed / _runtimeStyle.ContentEntranceDuration);
                var eased = _runtimeStyle.EvaluateResultContentEntrance(progress);
                _contentRoot.alpha = Mathf.Max(_contentRoot.alpha, eased);
                if (progress >= 1f)
                {
                    IsContentEntranceComplete = true;
                    SetContent(1f, false);
                }
            }

            TryCompleteInteraction();
        }

        public void RefreshPrimaryAction()
        {
            _primaryAction.interactable = IsInteractionReady && _primaryActionEnabled();
        }

        public void ResetTransitionState()
        {
            RequireConfigured();
            _prepared = false;
            _handoffStarted = false;
            _entranceStarted = false;
            _handoffElapsed = 0f;
            _entranceElapsed = 0f;
            _handoffRequestFrame = -1;
            IsHandoffCoverRendered = false;
            IsHandoffFadeComplete = true;
            IsContentEntranceComplete = true;
            IsInteractionReady = true;
            _resultHandoffCoverGroup.alpha = 0f;
            _resultHandoffCoverGroup.interactable = false;
            _resultHandoffCoverGroup.blocksRaycasts = false;
            _resultHandoffCover.gameObject.SetActive(false);
            SetBackdropFinalColor();
            SetContent(1f, true);
        }

        private void SetBackdropFinalColor()
        {
            _backdropRoot.alpha = 1f;
            _backdropRoot.interactable = false;
            _backdropRoot.blocksRaycasts = false;
            _backdropRoot.ignoreParentGroups = true;
            _resultBackdrop.color = _dimSnapshot.FinalColor;
            _resultBackdrop.raycastTarget = false;
        }

        private void SetContent(float alpha, bool interactive)
        {
            _contentRoot.alpha = alpha;
            _contentRoot.interactable = interactive;
            _contentRoot.blocksRaycasts = interactive;
            RefreshPrimaryAction();
        }

        private void TryCompleteInteraction()
        {
            if (!IsHandoffFadeComplete ||
                !IsContentEntranceComplete ||
                IsInteractionReady)
            {
                return;
            }

            IsInteractionReady = true;
            SetContent(1f, true);
            if (_primaryAction.IsActive() && _primaryAction.IsInteractable())
            {
                _primaryAction.Select();
            }
        }

        private void ValidateVisualContract()
        {
            if (_resultBackdrop.sprite != null ||
                HasCustomMaterial(_resultBackdrop) ||
                _resultBackdrop.raycastTarget ||
                _resultHandoffCover.sprite != null ||
                HasCustomMaterial(_resultHandoffCover) ||
                _resultHandoffCover.raycastTarget)
            {
                throw new InvalidOperationException(
                    "ResultBackdrop and ResultHandoffCover must be no-sprite, default-material, visual-only Images.");
            }

            ValidateFullStretch(_resultBackdrop.rectTransform, nameof(_resultBackdrop));
            ValidateFullStretch(_resultHandoffCover.rectTransform, nameof(_resultHandoffCover));
        }

        private static bool HasCustomMaterial(Graphic graphic)
        {
            return graphic.material != null &&
                   graphic.material != graphic.defaultMaterial;
        }

        private static void ValidateFullStretch(RectTransform rect, string fieldName)
        {
            if (rect.anchorMin != Vector2.zero ||
                rect.anchorMax != Vector2.one ||
                rect.offsetMin != Vector2.zero ||
                rect.offsetMax != Vector2.zero)
            {
                throw new InvalidOperationException(
                    $"{fieldName} must be full-stretch with zero offsets.");
            }
        }

        private void RequireConfigured()
        {
            if (!_configured)
            {
                throw new InvalidOperationException(
                    "Result transition must be configured from an immutable snapshot before use.");
            }
        }
    }

}
