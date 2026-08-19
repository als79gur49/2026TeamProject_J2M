using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    public enum ComicCinematicPresentationState
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
    }

    internal interface IComicCinematicPlaybackOverlay
    {
        bool IsPlaying { get; }
        AudioSource CinematicAudioSource { get; }
        void EnsureHierarchy();
        void SetAudioFocusController(CinematicAudioFocusController audioFocusController);
        void Play(
            ComicCinematicSequenceDefinition definition,
            CinematicOpaqueHandoffToken opaqueHandoffToken,
            Action<CinematicPlaybackCompletion> completion);
        bool AbortSetupAfterFailure(CinematicOpaqueHandoffToken expectedToken);
        void ReleaseOpaqueHandoff(CinematicOpaqueHandoffToken token);
    }

    [DisallowMultipleComponent]
    public sealed class ComicCinematicOverlayView :
        MonoBehaviour,
        IPointerClickHandler,
        IComicCinematicPlaybackOverlay
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
        }

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _pageViewport;
        [SerializeField] private AspectRatioFitter _pageFitter;
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private RectTransform _finalViewport;
        [SerializeField] private AspectRatioFitter _finalFitter;
        [SerializeField] private Image _finalImage;
        [SerializeField] private Image _blackFadeImage;
        [SerializeField] private AudioSource _cinematicAudioSource;

        private readonly CinematicAlphaFadeRunner _fadeRunner = new();
        private readonly List<Image> _panelImages = new();
        private Action<CinematicPlaybackCompletion> _completion;
        private CinematicAudioFocusController _audioFocusController;
        private ComicCinematicSequenceDefinition _definition;
        private ComicCinematicTimingSettings _timing;
        private FadeOperation _fadeOperation;
        private Image _panelBeingRevealed;
        private InputAction _submitAction;
        private bool _submitActionWasEnabled;
        private CinematicOpaqueHandoffToken _opaqueHandoffToken;
        private CinematicPlaybackCompletion _pendingCompletion;
        private int _currentPageIndex = -1;
        private int _visiblePanelCount;
        private int _opaqueRenderRequestFrame = -1;
        private float _finalBlackHoldRemaining;
        private bool _awaitingOpaqueRender;
        private bool _completionDispatched;
        private bool _finalBeforeDisplayed;
        private bool _finalAfterDisplayed;

        public bool IsPlaying { get; private set; }

        public AudioSource CinematicAudioSource
        {
            get
            {
                EnsureHierarchy();
                return _cinematicAudioSource;
            }
        }

        public ComicCinematicPresentationState CurrentPresentationState { get; private set; } =
            ComicCinematicPresentationState.Idle;

        internal int CurrentPageIndex => _currentPageIndex;
        internal int VisiblePanelCount => _visiblePanelCount;
        internal bool IsFinalBeforeDisplayed => _finalBeforeDisplayed;
        internal bool IsFinalAfterDisplayed => _finalAfterDisplayed;
        internal float CurrentFadeAlpha =>
            _blackFadeImage != null ? _blackFadeImage.color.a : 0f;
        internal IReadOnlyList<Image> PanelImages => _panelImages;

        public void Initialize(InputActionAsset inputActions)
        {
            _inputActions = inputActions;
        }

        public void SetAudioFocusController(CinematicAudioFocusController audioFocusController)
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

            _cinematicAudioSource = _cinematicAudioSource != null
                ? _cinematicAudioSource
                : GetComponent<AudioSource>();
            if (_cinematicAudioSource == null)
            {
                _cinematicAudioSource = gameObject.AddComponent<AudioSource>();
            }

            _cinematicAudioSource.playOnAwake = false;
            _cinematicAudioSource.loop = false;
            _cinematicAudioSource.spatialBlend = 0f;
        }

        internal void Play(
            ComicCinematicSequenceDefinition definition,
            CinematicOpaqueHandoffToken opaqueHandoffToken,
            Action<CinematicPlaybackCompletion> completion)
        {
            if (definition == null)
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    "Comic cinematic definition is missing."));
                return;
            }

            if (!definition.TryValidate(out var failureReason))
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    failureReason));
                return;
            }

            if (IsPlaying)
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    "A comic cinematic is already playing."));
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
            _pendingCompletion = new CinematicPlaybackCompletion(
                CinematicPlaybackCompletionKind.Completed);
            IsPlaying = true;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            ApplyBlackFadeAlpha(0f);
            BindAdvanceInput();
            CurrentPresentationState = ComicCinematicPresentationState.Entering;
            BeginFade(
                FadeOperation.EnterToBlack,
                0f,
                1f,
                _timing.EnterFadeDuration);
        }

        void IComicCinematicPlaybackOverlay.Play(
            ComicCinematicSequenceDefinition definition,
            CinematicOpaqueHandoffToken opaqueHandoffToken,
            Action<CinematicPlaybackCompletion> completion) =>
            Play(definition, opaqueHandoffToken, completion);

        public void RequestAdvance()
        {
            if (!IsPlaying ||
                _completionDispatched ||
                CurrentPresentationState != ComicCinematicPresentationState.AwaitingAdvance)
            {
                return;
            }

            if (_currentPageIndex >= 0)
            {
                var currentPage = _definition.Pages[_currentPageIndex];
                if (_visiblePanelCount < currentPage.Panels.Length)
                {
                    RevealNextPanel(currentPage);
                    return;
                }

                if (_currentPageIndex + 1 < _definition.Pages.Length)
                {
                    CurrentPresentationState = ComicCinematicPresentationState.PageTransition;
                    BeginFade(
                        FadeOperation.PageToBlack,
                        CurrentFadeAlpha,
                        1f,
                        _timing.PageFadeOutDuration);
                    return;
                }

                if (_definition.HasFinalShots)
                {
                    CurrentPresentationState = ComicCinematicPresentationState.FinalTransition;
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

            if (_finalBeforeDisplayed)
            {
                CurrentPresentationState = ComicCinematicPresentationState.FinalTransition;
                BeginFade(
                    FadeOperation.FinalSwapToBlack,
                    CurrentFadeAlpha,
                    1f,
                    _timing.FinalSwapFadeOutDuration);
                return;
            }

            if (_finalAfterDisplayed)
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
                !CinematicOpaqueHandoffRegistry.TryAcknowledgeCinematicOpaqueRendered(
                    _opaqueHandoffToken))
            {
                return false;
            }

            _awaitingOpaqueRender = false;
            CompleteOnce(_pendingCompletion);
            return true;
        }

        internal bool AbortSetupAfterFailure(CinematicOpaqueHandoffToken expectedToken)
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

        bool IComicCinematicPlaybackOverlay.AbortSetupAfterFailure(
            CinematicOpaqueHandoffToken expectedToken) =>
            AbortSetupAfterFailure(expectedToken);

        internal void ReleaseOpaqueHandoff(CinematicOpaqueHandoffToken token)
        {
            if (!token.IsValid ||
                token != _opaqueHandoffToken ||
                !_completionDispatched ||
                CurrentPresentationState != ComicCinematicPresentationState.Completed ||
                CurrentFadeAlpha < 0.9999f)
            {
                throw new InvalidOperationException(
                    $"Comic cinematic opaque owner release rejected token {token}.");
            }

            _opaqueHandoffToken = default;
            _awaitingOpaqueRender = false;
            SetInactive();
        }

        void IComicCinematicPlaybackOverlay.ReleaseOpaqueHandoff(
            CinematicOpaqueHandoffToken token) =>
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

            ApplyBlackFadeAlpha(_fadeRunner.CurrentAlpha);
            if (_fadeOperation == FadeOperation.ExitToBlack &&
                _definition != null &&
                _definition.AudioClip != null)
            {
                _audioFocusController?.SetCinematicFadeGain(1f - _fadeRunner.Progress);
            }
        }

        private void CompleteFadeOperation()
        {
            var completedOperation = _fadeOperation;
            _fadeOperation = FadeOperation.None;
            switch (completedOperation)
            {
                case FadeOperation.EnterToBlack:
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
                        ComicCinematicPresentationState.AwaitingAdvance;
                    break;

                case FadeOperation.PanelReveal:
                    SetImageAlpha(_panelBeingRevealed, 1f);
                    _panelBeingRevealed = null;
                    CurrentPresentationState =
                        ComicCinematicPresentationState.AwaitingAdvance;
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
                    ConfigureFinalShot(showAfter: false);
                    BeginFade(
                        FadeOperation.FinalBeforeFromBlack,
                        1f,
                        0f,
                        _timing.PageFadeInDuration);
                    break;

                case FadeOperation.FinalSwapToBlack:
                    ConfigureFinalShot(showAfter: true);
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
                ConfigureFinalShot(showAfter: false);
            }
        }

        private void ConfigurePage(int pageIndex)
        {
            _currentPageIndex = pageIndex;
            _visiblePanelCount = 0;
            _finalBeforeDisplayed = false;
            _finalAfterDisplayed = false;
            _pageViewport.gameObject.SetActive(true);
            _finalViewport.gameObject.SetActive(false);

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

        private void RevealNextPanel(ComicCinematicPageDefinition page)
        {
            var image = _panelImages[_visiblePanelCount];
            image.gameObject.SetActive(true);
            SetImageAlpha(image, 0f);
            _panelBeingRevealed = image;
            _visiblePanelCount++;
            CurrentPresentationState = ComicCinematicPresentationState.Revealing;
            BeginFade(
                FadeOperation.PanelReveal,
                0f,
                1f,
                _timing.PanelRevealDuration);
        }

        private void ConfigureFinalShot(bool showAfter)
        {
            _currentPageIndex = -1;
            _visiblePanelCount = 0;
            _pageViewport.gameObject.SetActive(false);
            _finalViewport.gameObject.SetActive(true);
            _finalImage.sprite = showAfter
                ? _definition.FinalAfterSprite
                : _definition.FinalBeforeSprite;
            _finalImage.preserveAspect = true;
            _finalImage.raycastTarget = false;
            _finalBeforeDisplayed = !showAfter;
            _finalAfterDisplayed = showAfter;
        }

        private void BeginExit()
        {
            CurrentPresentationState = ComicCinematicPresentationState.Exiting;
            _pendingCompletion = new CinematicPlaybackCompletion(
                CinematicPlaybackCompletionKind.Completed);
            BeginFade(
                FadeOperation.ExitToBlack,
                CurrentFadeAlpha,
                1f,
                _timing.ExitFadeDuration);
        }

        private void CompleteExitFade()
        {
            ApplyBlackFadeAlpha(1f);
            _audioFocusController?.SetCinematicFadeGain(0f);
            if (_opaqueHandoffToken.IsValid)
            {
                CurrentPresentationState =
                    ComicCinematicPresentationState.AwaitingOpaqueRender;
                _opaqueRenderRequestFrame = Time.frameCount;
                _awaitingOpaqueRender = true;
                Canvas.ForceUpdateCanvases();
                return;
            }

            CompleteOnce(_pendingCompletion);
        }

        private void CompleteOnce(CinematicPlaybackCompletion completion)
        {
            if (_completionDispatched)
            {
                return;
            }

            _completionDispatched = true;
            IsPlaying = false;
            UnbindAdvanceInput();
            if (_cinematicAudioSource != null)
            {
                _cinematicAudioSource.Stop();
                _cinematicAudioSource.clip = null;
            }

            var callback = _completion;
            _completion = null;
            CurrentPresentationState = ComicCinematicPresentationState.Completed;
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

            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = Color.black;
            backgroundImage.raycastTarget = true;
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
            _finalViewport = transform.Find("FinalShotViewport") as RectTransform;
            if (_finalViewport == null)
            {
                var finalObject = new GameObject(
                    "FinalShotViewport",
                    typeof(RectTransform),
                    typeof(AspectRatioFitter));
                finalObject.transform.SetParent(transform, false);
                _finalViewport = (RectTransform)finalObject.transform;
                UiCanvasElementFactory.Stretch(_finalViewport);
            }

            _finalFitter = _finalViewport.GetComponent<AspectRatioFitter>();
            _finalFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _finalFitter.aspectRatio = ComicCinematicSequenceDefinition.FinalShotAspectRatio;

            var finalImageRect = _finalViewport.Find("FinalShot") as RectTransform;
            if (finalImageRect == null)
            {
                var finalImageObject = new GameObject(
                    "FinalShot",
                    typeof(RectTransform),
                    typeof(Image));
                finalImageObject.transform.SetParent(_finalViewport, false);
                finalImageRect = (RectTransform)finalImageObject.transform;
                UiCanvasElementFactory.Stretch(finalImageRect);
            }

            _finalImage = finalImageRect.GetComponent<Image>();
            _finalImage.color = Color.white;
            _finalImage.raycastTarget = false;
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
            ComicCinematicSequenceDefinition definition)
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
            var reference = ComicCinematicSequenceDefinition.ReferenceResolution;
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

            _cinematicAudioSource.clip = _definition.AudioClip;
            _cinematicAudioSource.Play();
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
            _finalBeforeDisplayed = false;
            _finalAfterDisplayed = false;
            _finalBlackHoldRemaining = 0f;
            _panelBeingRevealed = null;
            _fadeOperation = FadeOperation.None;
            _fadeRunner.Reset();

            if (_pageViewport != null)
            {
                _pageViewport.gameObject.SetActive(false);
            }

            if (_finalViewport != null)
            {
                _finalViewport.gameObject.SetActive(false);
            }
        }

        private void StopAndResetRuntime()
        {
            UnbindAdvanceInput();
            if (_cinematicAudioSource != null)
            {
                _cinematicAudioSource.Stop();
                _cinematicAudioSource.clip = null;
            }

            _completion = null;
            _completionDispatched = true;
            _awaitingOpaqueRender = false;
            _opaqueRenderRequestFrame = -1;
            _opaqueHandoffToken = default;
            _definition = null;
            IsPlaying = false;
            CurrentPresentationState = ComicCinematicPresentationState.Idle;
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
            CancelPlaybackBecauseDisabled();
        }

        private void CancelPlaybackBecauseDisabled()
        {
            if (!IsPlaying || _completionDispatched)
            {
                return;
            }

            var callback = _completion;
            var token = _opaqueHandoffToken;
            StopAndResetRuntime();
            CinematicOpaqueHandoffRegistry.TryReleaseAbandonedOwner(token);
            callback?.Invoke(new CinematicPlaybackCompletion(
                CinematicPlaybackCompletionKind.Cancelled,
                "Comic cinematic overlay was disabled before playback completed."));
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
            if (!CinematicOpaqueHandoffRegistry.TryAcknowledgeCinematicOpaqueRendered(
                    _opaqueHandoffToken))
            {
                CinematicOpaqueHandoffRegistry.TryFailHoldingOpaque(
                    _opaqueHandoffToken,
                    "The comic cinematic exact-opaque render acknowledgement was stale.");
                return;
            }

            CompleteOnce(_pendingCompletion);
        }

        private void OnDestroy()
        {
            UnbindAdvanceInput();
            if (IsPlaying && !_completionDispatched)
            {
                _completionDispatched = true;
                IsPlaying = false;
                var callback = _completion;
                _completion = null;
                callback?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Cancelled,
                    "Comic cinematic overlay was destroyed before playback completed."));
            }

            if (_opaqueHandoffToken.IsValid)
            {
                CinematicOpaqueHandoffRegistry.TryFailHoldingOpaque(
                    _opaqueHandoffToken,
                    "Comic cinematic overlay was destroyed before ownership transfer.");
            }
        }
    }
}
