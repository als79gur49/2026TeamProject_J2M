using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    public enum ComicSequencePresentationState
    {
        Idle = 0,
        Entering = 1,
        Revealing = 2,
        AwaitingAdvance = 3,
        PageTransition = 4,
        FinalTransition = 5,
        Exiting = 6,
        AwaitingOpaqueRender = 7,
        Completed = 8,
        PanelSwap = 9,
    }

    internal interface IComicSequenceOverlay
    {
        bool IsPresenting { get; }
        AudioSource ComicSequenceAudioSource { get; }
        void EnsureHierarchy();
        void SetAudioFocusController(ComicSequenceAudioFocusController audioFocusController);
        void Present(
            ComicSequenceDefinition definition,
            ComicSequenceOpaqueHandoffToken opaqueHandoffToken,
            Action<ComicSequenceResult> completion);
        bool AbortSetupAfterFailure(ComicSequenceOpaqueHandoffToken expectedToken);
        void ReleaseOpaqueHandoff(ComicSequenceOpaqueHandoffToken token);
    }

    [DisallowMultipleComponent]
    public sealed class ComicSequenceOverlayView :
        MonoBehaviour,
        IPointerClickHandler,
        IComicSequenceOverlay
    {
        private const string UiMapName = "UI";
        private const string SubmitActionName = "Submit";

        private enum FadeOperation
        {
            None = 0,
            EnterToBlack = 1,
            InitialReveal = 2,
            PanelReveal = 3,
            PageToBlack = 4,
            PageFromBlack = 5,
            FinalBeforeToBlack = 6,
            FinalBeforeFromBlack = 7,
            FinalSwapToBlack = 8,
            FinalSwapFromBlack = 9,
            ExitToBlack = 10,
            PanelSwapFadeOut = 11,
            PanelSwapFadeIn = 12,
        }

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _pageViewport;
        [SerializeField] private AspectRatioFitter _pageFitter;
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private RectTransform _finalTransitionViewport;
        [SerializeField] private AspectRatioFitter _finalTransitionFitter;
        [SerializeField] private Image _finalTransitionImage;
        [SerializeField] private Image _blackFadeImage;
        [SerializeField] private AudioSource _comicSequenceAudioSource;

        private readonly ComicSequenceAlphaFadeRunner _fadeRunner = new();
        private readonly List<Image> _panelImages = new();
        private Image _backgroundImage;
        private Action<ComicSequenceResult> _completion;
        private ComicSequenceAudioFocusController _audioFocusController;
        private ComicSequenceDefinition _definition;
        private ComicSequenceTimingSettings _timing;
        private FadeOperation _fadeOperation;
        private Image _panelBeingRevealed;
        private Image _panelBeingSwapped;
        private InputAction _submitAction;
        private bool _submitActionWasEnabled;
        private ComicSequenceOpaqueHandoffToken _opaqueHandoffToken;
        private ComicSequenceResult _pendingCompletion;
        private int _currentPageIndex = -1;
        private int _visiblePanelCount;
        private int _opaqueRenderRequestFrame = -1;
        private float _finalBlackHoldRemaining;
        private bool _currentPanelReplacementDisplayed;
        private bool _awaitingOpaqueRender;
        private bool _completionDispatched;
        private bool _finalTransitionBeforeDisplayed;
        private bool _finalTransitionAfterDisplayed;

        public bool IsPresenting { get; private set; }

        public AudioSource ComicSequenceAudioSource
        {
            get
            {
                EnsureHierarchy();
                return _comicSequenceAudioSource;
            }
        }

        public ComicSequencePresentationState CurrentPresentationState { get; private set; } =
            ComicSequencePresentationState.Idle;

        internal int CurrentPageIndex => _currentPageIndex;
        internal int VisiblePanelCount => _visiblePanelCount;
        internal bool IsFinalTransitionBeforeDisplayed => _finalTransitionBeforeDisplayed;
        internal bool IsFinalTransitionAfterDisplayed => _finalTransitionAfterDisplayed;
        internal float CurrentFadeAlpha =>
            _blackFadeImage != null ? _blackFadeImage.color.a : 0f;
        internal IReadOnlyList<Image> PanelImages => _panelImages;

        public void Initialize(InputActionAsset inputActions)
        {
            _inputActions = inputActions;
        }

        public void SetAudioFocusController(ComicSequenceAudioFocusController audioFocusController)
        {
            _audioFocusController = audioFocusController;
        }

        public void EnsureHierarchy()
        {
            var rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            UiCanvasElementFactory.Stretch(rootRect);

            _canvasGroup = _canvasGroup != null ? _canvasGroup : GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            EnsureBackground();
            EnsurePageViewport();
            EnsureFinalViewport();
            EnsureBlackFadeLayer();

            _comicSequenceAudioSource = _comicSequenceAudioSource != null
                ? _comicSequenceAudioSource
                : GetComponent<AudioSource>();
            if (_comicSequenceAudioSource == null)
            {
                _comicSequenceAudioSource = gameObject.AddComponent<AudioSource>();
            }

            _comicSequenceAudioSource.playOnAwake = false;
            _comicSequenceAudioSource.loop = false;
            _comicSequenceAudioSource.spatialBlend = 0f;
        }

        internal void Present(
            ComicSequenceDefinition definition,
            ComicSequenceOpaqueHandoffToken opaqueHandoffToken,
            Action<ComicSequenceResult> completion)
        {
            if (definition == null)
            {
                completion?.Invoke(new ComicSequenceResult(
                    ComicSequenceResultKind.Failed,
                    "Comic sequence definition is missing."));
                return;
            }

            if (!definition.TryValidate(out var failureReason))
            {
                completion?.Invoke(new ComicSequenceResult(
                    ComicSequenceResultKind.Failed,
                    failureReason));
                return;
            }

            if (IsPresenting)
            {
                completion?.Invoke(new ComicSequenceResult(
                    ComicSequenceResultKind.Failed,
                    "A comic sequence is already being presented."));
                return;
            }

            EnsureHierarchy();
            EnsurePanelPool(ResolveMaximumPanelCount(definition));
            ResetPresentationContent();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            _definition = definition;
            _timing = definition.Timing;
            _completion = completion;
            _opaqueHandoffToken = opaqueHandoffToken;
            _completionDispatched = false;
            _awaitingOpaqueRender = false;
            _opaqueRenderRequestFrame = -1;
            _pendingCompletion = new ComicSequenceResult(
                ComicSequenceResultKind.Completed);
            IsPresenting = true;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            SetImageAlpha(_backgroundImage, 0f);
            ApplyBlackFadeAlpha(0f);
            BindAdvanceInput();
            CurrentPresentationState = ComicSequencePresentationState.Entering;
            BeginFade(
                FadeOperation.EnterToBlack,
                0f,
                1f,
                _timing.EnterFadeDuration);
        }

        void IComicSequenceOverlay.Present(
            ComicSequenceDefinition definition,
            ComicSequenceOpaqueHandoffToken opaqueHandoffToken,
            Action<ComicSequenceResult> completion) =>
            Present(definition, opaqueHandoffToken, completion);

        public void RequestAdvance()
        {
            if (!IsPresenting ||
                _completionDispatched ||
                CurrentPresentationState != ComicSequencePresentationState.AwaitingAdvance)
            {
                return;
            }

            if (_currentPageIndex >= 0)
            {
                var currentPage = _definition.Pages[_currentPageIndex];
                var currentPanel = currentPage.Panels[_visiblePanelCount - 1];
                if (!_currentPanelReplacementDisplayed && currentPanel.HasReplacement)
                {
                    _panelBeingSwapped = _panelImages[_visiblePanelCount - 1];
                    CurrentPresentationState = ComicSequencePresentationState.PanelSwap;
                    BeginFade(
                        FadeOperation.PanelSwapFadeOut,
                        1f,
                        0f,
                        _timing.FinalSwapFadeOutDuration);
                    return;
                }

                if (_visiblePanelCount < currentPage.Panels.Length)
                {
                    RevealNextPanel(currentPage);
                    return;
                }

                if (_currentPageIndex + 1 < _definition.Pages.Length)
                {
                    CurrentPresentationState = ComicSequencePresentationState.PageTransition;
                    BeginFade(
                        FadeOperation.PageToBlack,
                        CurrentFadeAlpha,
                        1f,
                        _timing.PageFadeOutDuration);
                    return;
                }

                if (_definition.HasFinalTransition)
                {
                    CurrentPresentationState = ComicSequencePresentationState.FinalTransition;
                    BeginFade(
                        FadeOperation.FinalBeforeToBlack,
                        CurrentFadeAlpha,
                        1f,
                        _timing.PageFadeOutDuration);
                    return;
                }

                BeginExit();
                return;
            }

            if (_finalTransitionBeforeDisplayed)
            {
                CurrentPresentationState = ComicSequencePresentationState.FinalTransition;
                BeginFade(
                    FadeOperation.FinalSwapToBlack,
                    CurrentFadeAlpha,
                    1f,
                    _timing.FinalSwapFadeOutDuration);
                return;
            }

            if (_finalTransitionAfterDisplayed)
            {
                BeginExit();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null &&
                eventData.button == PointerEventData.InputButton.Left)
            {
                RequestAdvance();
            }
        }

        internal void AdvanceForTesting(float deltaSeconds)
        {
            AdvancePresentation(deltaSeconds);
        }

        internal bool AcknowledgeOpaqueRenderForTesting()
        {
            if (!_awaitingOpaqueRender ||
                _completionDispatched ||
                !_opaqueHandoffToken.IsValid ||
                CurrentFadeAlpha < 0.9999f ||
                !ComicSequenceOpaqueHandoffRegistry.TryAcknowledgeComicSequenceOpaqueRendered(
                    _opaqueHandoffToken))
            {
                return false;
            }

            _awaitingOpaqueRender = false;
            CompleteOnce(_pendingCompletion);
            return true;
        }

        internal bool AbortSetupAfterFailure(ComicSequenceOpaqueHandoffToken expectedToken)
        {
            if ((expectedToken.IsValid && expectedToken != _opaqueHandoffToken) ||
                (!expectedToken.IsValid && _opaqueHandoffToken.IsValid))
            {
                return false;
            }

            StopAndResetRuntime();
            SetInactive();
            return true;
        }

        bool IComicSequenceOverlay.AbortSetupAfterFailure(
            ComicSequenceOpaqueHandoffToken expectedToken) =>
            AbortSetupAfterFailure(expectedToken);

        internal void ReleaseOpaqueHandoff(ComicSequenceOpaqueHandoffToken token)
        {
            if (!token.IsValid ||
                token != _opaqueHandoffToken ||
                !_completionDispatched ||
                CurrentPresentationState != ComicSequencePresentationState.Completed ||
                CurrentFadeAlpha < 0.9999f)
            {
                throw new InvalidOperationException(
                    $"Comic sequence opaque owner release rejected token {token}.");
            }

            _opaqueHandoffToken = default;
            _awaitingOpaqueRender = false;
            SetInactive();
        }

        void IComicSequenceOverlay.ReleaseOpaqueHandoff(
            ComicSequenceOpaqueHandoffToken token) =>
            ReleaseOpaqueHandoff(token);

        private void Update()
        {
            AdvancePresentation(Time.unscaledDeltaTime);
        }

        private void AdvancePresentation(float deltaSeconds)
        {
            if (_finalBlackHoldRemaining > 0f)
            {
                _finalBlackHoldRemaining = Mathf.Max(
                    0f,
                    _finalBlackHoldRemaining - Mathf.Max(0f, deltaSeconds));
                if (_finalBlackHoldRemaining <= 0f)
                {
                    BeginFade(
                        FadeOperation.FinalSwapFromBlack,
                        1f,
                        0f,
                        _timing.FinalSwapFadeInDuration);
                }

                return;
            }

            if (!_fadeRunner.IsRunning)
            {
                return;
            }

            var completed = _fadeRunner.Advance(deltaSeconds);
            ApplyCurrentFadeValue();
            if (completed)
            {
                CompleteFadeOperation();
            }
        }

        private void BeginFade(
            FadeOperation operation,
            float from,
            float to,
            float duration)
        {
            _fadeOperation = operation;
            var completed = _fadeRunner.Begin(
                from,
                to,
                duration,
                _timing.FadeEase);
            ApplyCurrentFadeValue();
            if (completed)
            {
                CompleteFadeOperation();
            }
        }

        private void ApplyCurrentFadeValue()
        {
            if (_fadeOperation == FadeOperation.PanelReveal)
            {
                SetImageAlpha(_panelBeingRevealed, _fadeRunner.CurrentAlpha);
                return;
            }

            if (_fadeOperation == FadeOperation.PanelSwapFadeOut ||
                _fadeOperation == FadeOperation.PanelSwapFadeIn)
            {
                SetImageAlpha(_panelBeingSwapped, _fadeRunner.CurrentAlpha);
                return;
            }

            ApplyBlackFadeAlpha(_fadeRunner.CurrentAlpha);
            if (_fadeOperation == FadeOperation.ExitToBlack &&
                _definition != null &&
                _definition.AudioClip != null)
            {
                _audioFocusController?.SetComicSequenceFadeGain(1f - _fadeRunner.Progress);
            }
        }

        private void CompleteFadeOperation()
        {
            var completedOperation = _fadeOperation;
            _fadeOperation = FadeOperation.None;
            switch (completedOperation)
            {
                case FadeOperation.EnterToBlack:
                    SetImageAlpha(_backgroundImage, 1f);
                    ConfigureInitialContent();
                    StartSequenceAudio();
                    BeginFade(
                        FadeOperation.InitialReveal,
                        1f,
                        0f,
                        _timing.PageFadeInDuration);
                    break;

                case FadeOperation.InitialReveal:
                case FadeOperation.PageFromBlack:
                case FadeOperation.FinalBeforeFromBlack:
                case FadeOperation.FinalSwapFromBlack:
                    CurrentPresentationState =
                        ComicSequencePresentationState.AwaitingAdvance;
                    break;

                case FadeOperation.PanelReveal:
                    SetImageAlpha(_panelBeingRevealed, 1f);
                    _panelBeingRevealed = null;
                    CurrentPresentationState =
                        ComicSequencePresentationState.AwaitingAdvance;
                    break;

                case FadeOperation.PageToBlack:
                    ConfigurePage(_currentPageIndex + 1);
                    BeginFade(
                        FadeOperation.PageFromBlack,
                        1f,
                        0f,
                        _timing.PageFadeInDuration);
                    break;

                case FadeOperation.FinalBeforeToBlack:
                    ConfigureFinalTransition(showAfter: false);
                    BeginFade(
                        FadeOperation.FinalBeforeFromBlack,
                        1f,
                        0f,
                        _timing.PageFadeInDuration);
                    break;

                case FadeOperation.FinalSwapToBlack:
                    ConfigureFinalTransition(showAfter: true);
                    _finalBlackHoldRemaining = _timing.FinalSwapBlackHoldDuration;
                    if (_finalBlackHoldRemaining <= 0f)
                    {
                        BeginFade(
                            FadeOperation.FinalSwapFromBlack,
                            1f,
                            0f,
                            _timing.FinalSwapFadeInDuration);
                    }
                    break;

                case FadeOperation.PanelSwapFadeOut:
                    SetImageAlpha(_panelBeingSwapped, 0f);
                    ConfigureCurrentPanelReplacement();
                    BeginFade(
                        FadeOperation.PanelSwapFadeIn,
                        0f,
                        1f,
                        _timing.FinalSwapFadeInDuration);
                    break;

                case FadeOperation.PanelSwapFadeIn:
                    SetImageAlpha(_panelBeingSwapped, 1f);
                    _panelBeingSwapped = null;
                    CurrentPresentationState =
                        ComicSequencePresentationState.AwaitingAdvance;
                    break;

                case FadeOperation.ExitToBlack:
                    CompleteExitFade();
                    break;
            }
        }

        private void ConfigureInitialContent()
        {
            if (_definition.Pages.Length > 0)
            {
                ConfigurePage(0);
            }
            else
            {
                ConfigureFinalTransition(showAfter: false);
            }
        }

        private void ConfigurePage(int pageIndex)
        {
            _currentPageIndex = pageIndex;
            _visiblePanelCount = 0;
            _currentPanelReplacementDisplayed = false;
            _finalTransitionBeforeDisplayed = false;
            _finalTransitionAfterDisplayed = false;
            _pageViewport.gameObject.SetActive(true);
            _finalTransitionViewport.gameObject.SetActive(false);

            for (var index = 0; index < _panelImages.Count; index++)
            {
                _panelImages[index].gameObject.SetActive(false);
            }

            var panels = _definition.Pages[pageIndex].Panels;
            for (var panelIndex = 0; panelIndex < panels.Length; panelIndex++)
            {
                var image = _panelImages[panelIndex];
                var panel = panels[panelIndex];
                image.sprite = panel.Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                ApplyReferenceRect(image.rectTransform, panel.ReferenceRect);
            }

            var firstPanel = _panelImages[0];
            firstPanel.gameObject.SetActive(true);
            SetImageAlpha(firstPanel, 1f);
            _visiblePanelCount = 1;
        }

        private void RevealNextPanel(ComicPageDefinition page)
        {
            var image = _panelImages[_visiblePanelCount];
            image.gameObject.SetActive(true);
            SetImageAlpha(image, 0f);
            _panelBeingRevealed = image;
            _visiblePanelCount++;
            _currentPanelReplacementDisplayed = false;
            CurrentPresentationState = ComicSequencePresentationState.Revealing;
            BeginFade(
                FadeOperation.PanelReveal,
                0f,
                1f,
                _timing.PanelRevealDuration);
        }

        private void ConfigureCurrentPanelReplacement()
        {
            var panelIndex = _visiblePanelCount - 1;
            var panel = _definition.Pages[_currentPageIndex].Panels[panelIndex];
            _panelBeingSwapped.sprite = panel.ReplacementSprite;
            _currentPanelReplacementDisplayed = true;
        }

        private void ConfigureFinalTransition(bool showAfter)
        {
            _currentPageIndex = -1;
            _visiblePanelCount = 0;
            _currentPanelReplacementDisplayed = false;
            _pageViewport.gameObject.SetActive(false);
            _finalTransitionViewport.gameObject.SetActive(true);
            _finalTransitionImage.sprite = showAfter
                ? _definition.FinalTransitionAfterSprite
                : _definition.FinalTransitionBeforeSprite;
            _finalTransitionImage.preserveAspect = true;
            _finalTransitionImage.raycastTarget = false;
            _finalTransitionBeforeDisplayed = !showAfter;
            _finalTransitionAfterDisplayed = showAfter;
        }

        private void BeginExit()
        {
            CurrentPresentationState = ComicSequencePresentationState.Exiting;
            _pendingCompletion = new ComicSequenceResult(
                ComicSequenceResultKind.Completed);
            BeginFade(
                FadeOperation.ExitToBlack,
                CurrentFadeAlpha,
                1f,
                _timing.ExitFadeDuration);
        }

        private void CompleteExitFade()
        {
            ApplyBlackFadeAlpha(1f);
            _audioFocusController?.SetComicSequenceFadeGain(0f);
            if (_opaqueHandoffToken.IsValid)
            {
                CurrentPresentationState =
                    ComicSequencePresentationState.AwaitingOpaqueRender;
                _opaqueRenderRequestFrame = Time.frameCount;
                _awaitingOpaqueRender = true;
                Canvas.ForceUpdateCanvases();
                return;
            }

            CompleteOnce(_pendingCompletion);
        }

        private void CompleteOnce(ComicSequenceResult completion)
        {
            if (_completionDispatched)
            {
                return;
            }

            _completionDispatched = true;
            IsPresenting = false;
            UnbindAdvanceInput();
            if (_comicSequenceAudioSource != null)
            {
                _comicSequenceAudioSource.Stop();
                _comicSequenceAudioSource.clip = null;
            }

            var callback = _completion;
            _completion = null;
            CurrentPresentationState = ComicSequencePresentationState.Completed;
            if (!_opaqueHandoffToken.IsValid)
            {
                SetInactive();
            }

            callback?.Invoke(completion);
        }

        private void EnsureBackground()
        {
            var background = transform.Find("Background") as RectTransform;
            if (background == null)
            {
                var backgroundObject = new GameObject(
                    "Background",
                    typeof(RectTransform),
                    typeof(Image));
                backgroundObject.transform.SetParent(transform, false);
                background = (RectTransform)backgroundObject.transform;
                UiCanvasElementFactory.Stretch(background);
            }

            _backgroundImage = background.GetComponent<Image>();
            _backgroundImage.color = Color.black;
            _backgroundImage.raycastTarget = true;
        }

        private void EnsurePageViewport()
        {
            _pageViewport = transform.Find("PageViewport") as RectTransform;
            if (_pageViewport == null)
            {
                var pageObject = new GameObject(
                    "PageViewport",
                    typeof(RectTransform),
                    typeof(AspectRatioFitter));
                pageObject.transform.SetParent(transform, false);
                _pageViewport = (RectTransform)pageObject.transform;
                UiCanvasElementFactory.Stretch(_pageViewport);
            }

            _pageFitter = _pageViewport.GetComponent<AspectRatioFitter>();
            _pageFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _pageFitter.aspectRatio = 16f / 9f;

            _panelRoot = _pageViewport.Find("Panels") as RectTransform;
            if (_panelRoot == null)
            {
                _panelRoot = UiCanvasElementFactory.CreateStretchRect(
                    "Panels",
                    _pageViewport);
            }
        }

        private void EnsureFinalViewport()
        {
            _finalTransitionViewport = transform.Find("FinalTransitionViewport") as RectTransform;
            if (_finalTransitionViewport == null)
            {
                var finalObject = new GameObject(
                    "FinalTransitionViewport",
                    typeof(RectTransform),
                    typeof(AspectRatioFitter));
                finalObject.transform.SetParent(transform, false);
                _finalTransitionViewport = (RectTransform)finalObject.transform;
                UiCanvasElementFactory.Stretch(_finalTransitionViewport);
            }

            _finalTransitionFitter = _finalTransitionViewport.GetComponent<AspectRatioFitter>();
            _finalTransitionFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _finalTransitionFitter.aspectRatio = ComicSequenceDefinition.FinalTransitionAspectRatio;

            var finalImageRect = _finalTransitionViewport.Find("FinalTransition") as RectTransform;
            if (finalImageRect == null)
            {
                var finalImageObject = new GameObject(
                    "FinalTransition",
                    typeof(RectTransform),
                    typeof(Image));
                finalImageObject.transform.SetParent(_finalTransitionViewport, false);
                finalImageRect = (RectTransform)finalImageObject.transform;
                UiCanvasElementFactory.Stretch(finalImageRect);
            }

            _finalTransitionImage = finalImageRect.GetComponent<Image>();
            _finalTransitionImage.color = Color.white;
            _finalTransitionImage.raycastTarget = false;
        }

        private void EnsureBlackFadeLayer()
        {
            var fadeRect = transform.Find("BlackFade") as RectTransform;
            if (fadeRect == null)
            {
                var fadeObject = new GameObject(
                    "BlackFade",
                    typeof(RectTransform),
                    typeof(Image));
                fadeObject.transform.SetParent(transform, false);
                fadeRect = (RectTransform)fadeObject.transform;
                UiCanvasElementFactory.Stretch(fadeRect);
            }

            _blackFadeImage = fadeRect.GetComponent<Image>();
            _blackFadeImage.raycastTarget = false;
            _blackFadeImage.transform.SetAsLastSibling();
        }

        private void EnsurePanelPool(int count)
        {
            while (_panelImages.Count < count)
            {
                var panelObject = new GameObject(
                    $"Panel{_panelImages.Count + 1:00}",
                    typeof(RectTransform),
                    typeof(Image));
                panelObject.transform.SetParent(_panelRoot, false);
                var image = panelObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                panelObject.SetActive(false);
                _panelImages.Add(image);
            }
        }

        private static int ResolveMaximumPanelCount(
            ComicSequenceDefinition definition)
        {
            var maximum = 0;
            var pages = definition.Pages;
            for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
            {
                maximum = Mathf.Max(maximum, pages[pageIndex].Panels.Length);
            }

            return maximum;
        }

        private static void ApplyReferenceRect(RectTransform target, Rect referenceRect)
        {
            var reference = ComicSequenceDefinition.ReferenceResolution;
            target.anchorMin = new Vector2(
                referenceRect.xMin / reference.x,
                1f - referenceRect.yMax / reference.y);
            target.anchorMax = new Vector2(
                referenceRect.xMax / reference.x,
                1f - referenceRect.yMin / reference.y);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;
            target.sizeDelta = Vector2.zero;
            target.localScale = Vector3.one;
        }

        private void StartSequenceAudio()
        {
            if (_definition == null || _definition.AudioClip == null)
            {
                return;
            }

            _comicSequenceAudioSource.clip = _definition.AudioClip;
            _comicSequenceAudioSource.Play();
        }

        private void BindAdvanceInput()
        {
            UnbindAdvanceInput();
            var uiMap = _inputActions != null
                ? _inputActions.FindActionMap(UiMapName, throwIfNotFound: false)
                : null;
            _submitAction = uiMap != null
                ? uiMap.FindAction(SubmitActionName, throwIfNotFound: false)
                : null;
            if (_submitAction == null)
            {
                return;
            }

            _submitActionWasEnabled = _submitAction.enabled;
            _submitAction.performed += HandleSubmitPerformed;
            if (!_submitActionWasEnabled)
            {
                _submitAction.Enable();
            }
        }

        private void UnbindAdvanceInput()
        {
            if (_submitAction != null)
            {
                _submitAction.performed -= HandleSubmitPerformed;
                if (!_submitActionWasEnabled)
                {
                    _submitAction.Disable();
                }
            }

            _submitAction = null;
            _submitActionWasEnabled = false;
        }

        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            RequestAdvance();
        }

        private void ApplyBlackFadeAlpha(float alpha)
        {
            EnsureBlackFadeLayer();
            var color = _timing.FadeColor;
            color.a = Mathf.Clamp01(alpha);
            _blackFadeImage.color = color;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private void ResetPresentationContent()
        {
            _currentPageIndex = -1;
            _visiblePanelCount = 0;
            _finalTransitionBeforeDisplayed = false;
            _finalTransitionAfterDisplayed = false;
            _finalBlackHoldRemaining = 0f;
            _currentPanelReplacementDisplayed = false;
            _panelBeingRevealed = null;
            _panelBeingSwapped = null;
            _fadeOperation = FadeOperation.None;
            _fadeRunner.Reset();

            if (_pageViewport != null)
            {
                _pageViewport.gameObject.SetActive(false);
            }

            if (_finalTransitionViewport != null)
            {
                _finalTransitionViewport.gameObject.SetActive(false);
            }
        }

        private void StopAndResetRuntime()
        {
            UnbindAdvanceInput();
            if (_comicSequenceAudioSource != null)
            {
                _comicSequenceAudioSource.Stop();
                _comicSequenceAudioSource.clip = null;
            }

            _completion = null;
            _completionDispatched = true;
            _awaitingOpaqueRender = false;
            _opaqueRenderRequestFrame = -1;
            _opaqueHandoffToken = default;
            _definition = null;
            IsPresenting = false;
            CurrentPresentationState = ComicSequencePresentationState.Idle;
            ResetPresentationContent();
        }

        private void SetInactive()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
            CancelPresentationBecauseDisabled();
        }

        private void CancelPresentationBecauseDisabled()
        {
            if (!IsPresenting || _completionDispatched)
            {
                return;
            }

            var callback = _completion;
            var token = _opaqueHandoffToken;
            StopAndResetRuntime();
            ComicSequenceOpaqueHandoffRegistry.TryReleaseAbandonedOwner(token);
            callback?.Invoke(new ComicSequenceResult(
                ComicSequenceResultKind.Cancelled,
                "Comic sequence overlay was disabled before presentation completed."));
        }

        private void HandleWillRenderCanvases()
        {
            if (!_awaitingOpaqueRender ||
                _completionDispatched ||
                !_opaqueHandoffToken.IsValid ||
                Time.frameCount <= _opaqueRenderRequestFrame ||
                CurrentFadeAlpha < 0.9999f ||
                _blackFadeImage == null ||
                _blackFadeImage.canvasRenderer == null ||
                _blackFadeImage.canvasRenderer.cull)
            {
                return;
            }

            _awaitingOpaqueRender = false;
            if (!ComicSequenceOpaqueHandoffRegistry.TryAcknowledgeComicSequenceOpaqueRendered(
                    _opaqueHandoffToken))
            {
                ComicSequenceOpaqueHandoffRegistry.TryFailHoldingOpaque(
                    _opaqueHandoffToken,
                    "The comic sequence exact-opaque render acknowledgement was stale.");
                return;
            }

            CompleteOnce(_pendingCompletion);
        }

        private void OnDestroy()
        {
            UnbindAdvanceInput();
            if (IsPresenting && !_completionDispatched)
            {
                _completionDispatched = true;
                IsPresenting = false;
                var callback = _completion;
                _completion = null;
                callback?.Invoke(new ComicSequenceResult(
                    ComicSequenceResultKind.Cancelled,
                    "Comic sequence overlay was destroyed before presentation completed."));
            }

            if (_opaqueHandoffToken.IsValid)
            {
                ComicSequenceOpaqueHandoffRegistry.TryFailHoldingOpaque(
                    _opaqueHandoffToken,
                    "Comic sequence overlay was destroyed before ownership transfer.");
            }
        }
    }
}
