using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class CinematicVideoOverlayView :
        MonoBehaviour,
        IPointerClickHandler,
        ICinematicPlaybackOverlay
    {
        private const string UiMapName = "UI";
        private const string SubmitActionName = "Submit";
        private const string CancelActionName = "Cancel";
        private static readonly Rect FullTextureUvRect = new Rect(0f, 0f, 1f, 1f);

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _videoViewport;
        [SerializeField] private RawImage _videoImage;
        [SerializeField] private Image _blackFadeImage;
        [SerializeField] private AspectRatioFitter _viewportFitter;
        [SerializeField] private AspectRatioFitter _contentFitter;
        [SerializeField] private Button _skipButton;
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private AudioSource _cinematicAudioSource;

        private readonly CinematicAlphaFadeRunner _fadeRunner = new();
        private Action<CinematicPlaybackCompletion> _completion;
        private CinematicAudioFocusController _audioFocusController;
        private IResolvedCinematicViewportProvider _viewportProvider;
        private ICinematicSelectedAspectProvider _selectedAspectProvider;
        private InputAction _cancelAction;
        private InputAction _submitAction;
        private RenderTexture _renderTexture;
        private VideoClip _currentClip;
        private SlotCinematicPlaybackOptions _currentOptions;
        private CinematicFadeSettings _fadeSettings;
        private CinematicPlaybackCompletion _pendingCompletion;
        private bool _completionDispatched;
        private bool _exitFadeRequested;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _hasLoggedPlaybackDiagnostics;
#endif
        private bool _hasLoggedClipAspectFallbackWarning;
        private bool _hasLoggedViewportFallbackWarning;
        private bool _queuedSkip;
        private bool _skipEnabled;
        private float _audioFadeGain = 1f;
        private CinematicOpaqueHandoffToken _opaqueHandoffToken;
        private int _opaqueRenderRequestFrame = -1;
        private bool _awaitingOpaqueRender;

        public bool IsPlaying { get; private set; }

        public AudioSource CinematicAudioSource
        {
            get
            {
                EnsureHierarchy(new SlotCinematicPlaybackOptions(
                    true,
                    CinematicAspectSource.AutoResolvedViewport,
                    CinematicScaleMode.CropToFillViewport,
                    16f / 9f,
                    1920,
                    1080));
                return _cinematicAudioSource;
            }
        }

        internal VideoAudioOutputMode ConfiguredAudioOutputMode =>
            _videoPlayer != null ? _videoPlayer.audioOutputMode : VideoAudioOutputMode.None;

        internal float ConfiguredPresentationAspectRatio { get; private set; }

        internal float ConfiguredContentAspectRatio { get; private set; }

        internal CinematicScaleMode ConfiguredScaleMode { get; private set; }

        internal bool IsViewportAspectFitterEnabled =>
            _viewportFitter != null && _viewportFitter.enabled;

        internal bool IsContentAspectFitterEnabled =>
            _contentFitter != null && _contentFitter.enabled;

        internal AspectRatioFitter.AspectMode ConfiguredContentAspectMode =>
            _contentFitter != null ? _contentFitter.aspectMode : AspectRatioFitter.AspectMode.None;

        internal Rect ConfiguredVideoUvRect =>
            _videoImage != null ? _videoImage.uvRect : default;

        internal int ConfiguredRenderTextureWidth =>
            _renderTexture != null ? _renderTexture.width : 0;

        internal int ConfiguredRenderTextureHeight =>
            _renderTexture != null ? _renderTexture.height : 0;

        internal float ConfiguredRenderTextureAspectRatio =>
            _renderTexture != null && _renderTexture.height > 0
                ? _renderTexture.width / (float)_renderTexture.height
                : 0f;

        internal VideoAspectRatio ConfiguredVideoPlayerAspectRatio =>
            _videoPlayer != null ? _videoPlayer.aspectRatio : default;

        internal CinematicPresentationState CurrentPresentationState { get; private set; } =
            CinematicPresentationState.Idle;

        internal float CurrentFadeAlpha =>
            _blackFadeImage != null ? _blackFadeImage.color.a : 0f;

        internal float CurrentAudioFadeGain => _audioFadeGain;

        public void Initialize(
            InputActionAsset inputActions,
            IResolvedCinematicViewportProvider viewportProvider = null,
            ICinematicSelectedAspectProvider selectedAspectProvider = null)
        {
            _inputActions = inputActions;
            _viewportProvider = viewportProvider;
            _selectedAspectProvider = selectedAspectProvider;
        }

        public void SetAudioFocusController(CinematicAudioFocusController audioFocusController)
        {
            _audioFocusController = audioFocusController;
            ApplyAudioFadeGain(_audioFadeGain);
        }

        public void EnsureHierarchy(SlotCinematicPlaybackOptions options)
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            UiCanvasElementFactory.Stretch(rectTransform);

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            var background = transform.Find("Background") as RectTransform;
            if (background == null)
            {
                var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(Button));
                backgroundObject.transform.SetParent(transform, false);
                background = (RectTransform)backgroundObject.transform;
                UiCanvasElementFactory.Stretch(background);
                var image = backgroundObject.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
                _skipButton = backgroundObject.GetComponent<Button>();
                _skipButton.transition = Selectable.Transition.None;
                _skipButton.onClick.AddListener(RequestSkip);
            }

            if (_skipButton == null)
            {
                _skipButton = background.GetComponent<Button>();
            }

            var backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.clear;
                backgroundImage.raycastTarget = true;
            }

            EnsureVideoHierarchy();
            EnsureBlackFadeLayer();

            if (_videoPlayer == null)
            {
                _videoPlayer = GetComponent<VideoPlayer>();
            }

            if (_videoPlayer == null)
            {
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            if (_cinematicAudioSource == null)
            {
                _cinematicAudioSource = GetComponent<AudioSource>();
            }

            if (_cinematicAudioSource == null)
            {
                _cinematicAudioSource = gameObject.AddComponent<AudioSource>();
            }

            _cinematicAudioSource.playOnAwake = false;
            _cinematicAudioSource.loop = false;
            _cinematicAudioSource.spatialBlend = 0f;
        }

        public void Play(
            VideoClip clip,
            SlotCinematicPlaybackOptions options,
            Action<CinematicPlaybackCompletion> completion)
        {
            Play(clip, options, default, completion);
        }

        internal void Play(
            VideoClip clip,
            SlotCinematicPlaybackOptions options,
            CinematicOpaqueHandoffToken opaqueHandoffToken,
            Action<CinematicPlaybackCompletion> completion)
        {
            if (clip == null)
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    "Cinematic clip is missing."));
                return;
            }

            EnsureHierarchy(options);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _completion = completion;
            _completionDispatched = false;
            _exitFadeRequested = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _hasLoggedPlaybackDiagnostics = false;
#endif
            _queuedSkip = false;
            _skipEnabled = options.SkipEnabled;
            _fadeSettings = options.FadeSettings;
            _opaqueHandoffToken = opaqueHandoffToken;
            _opaqueRenderRequestFrame = -1;
            _awaitingOpaqueRender = false;
            IsPlaying = true;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _currentClip = clip;
            _currentOptions = options;
            _pendingCompletion = new CinematicPlaybackCompletion(CinematicPlaybackCompletionKind.Completed);
            ApplyAudioFadeGain(1f);
            ApplyFadeAlpha(0f);
            SetVideoImageVisible(false);
            EnsureRenderTexture(ResolveRenderTextureSize(clip, options));
            ApplyVideoLayout(clip, options);

            ConfigureVideoPlayer(clip);
            LogCinematicDiagnosticsIfNeeded(clip, options);
            BindSkipActions();
            BeginEnterFadeToBlack();
        }

        void ICinematicPlaybackOverlay.Play(
            VideoClip clip,
            SlotCinematicPlaybackOptions options,
            CinematicOpaqueHandoffToken opaqueHandoffToken,
            Action<CinematicPlaybackCompletion> completion)
        {
            Play(clip, options, opaqueHandoffToken, completion);
        }

        internal bool AbortSetupAfterFailure(
            CinematicOpaqueHandoffToken expectedToken)
        {
            if ((expectedToken.IsValid && expectedToken != _opaqueHandoffToken) ||
                (!expectedToken.IsValid && _opaqueHandoffToken.IsValid))
            {
                return false;
            }

            _completion = null;
            _completionDispatched = true;
            _exitFadeRequested = false;
            _pendingCompletion = default;
            _queuedSkip = false;
            _skipEnabled = false;
            _awaitingOpaqueRender = false;
            _opaqueRenderRequestFrame = -1;
            _opaqueHandoffToken = default;
            _currentClip = null;
            _currentOptions = default;
            IsPlaying = false;
            _fadeRunner.Reset();
            UnbindSkipActions();

            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= HandlePrepareCompleted;
                _videoPlayer.loopPointReached -= HandleLoopPointReached;
                _videoPlayer.errorReceived -= HandleErrorReceived;
                _videoPlayer.Stop();
                _videoPlayer.clip = null;
                _videoPlayer.targetTexture = null;
            }

            if (_cinematicAudioSource != null)
            {
                _cinematicAudioSource.Stop();
            }

            SetVideoImageVisible(false);
            ReleaseRenderTexture();
            ApplyAudioFadeGain(1f);
            ApplyFadeAlpha(0f);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            CurrentPresentationState = CinematicPresentationState.Idle;
            gameObject.SetActive(false);
            return true;
        }

        bool ICinematicPlaybackOverlay.AbortSetupAfterFailure(
            CinematicOpaqueHandoffToken expectedToken)
        {
            return AbortSetupAfterFailure(expectedToken);
        }

        public void RequestSkip()
        {
            if (!IsPlaying || !_skipEnabled)
            {
                return;
            }

            if (CurrentPresentationState != CinematicPresentationState.Playing)
            {
                if (_fadeSettings.SkipDuringFadePolicy == CinematicSkipDuringFadePolicy.QueueUntilPlaying)
                {
                    _queuedSkip = true;
                }

                return;
            }

            RequestExitFade(CinematicExitReason.Skipped);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            RequestSkip();
        }

        internal void CompleteForTesting(CinematicPlaybackCompletionKind kind)
        {
            switch (kind)
            {
                case CinematicPlaybackCompletionKind.Skipped:
                    RequestExitFade(CinematicExitReason.Skipped, string.Empty, force: true);
                    break;
                case CinematicPlaybackCompletionKind.Failed:
                    RequestExitFade(CinematicExitReason.Failed, string.Empty, force: true);
                    break;
                case CinematicPlaybackCompletionKind.Completed:
                default:
                    RequestExitFade(CinematicExitReason.NaturalEnd, string.Empty, force: true);
                    break;
            }
        }

        internal void NotifyPreparedFirstFrame()
        {
            if (CurrentPresentationState != CinematicPresentationState.PreparingVideo)
            {
                return;
            }

            SetVideoImageVisible(true);
            BeginRevealFadeFromBlack();
        }

        internal void RequestExitFade(CinematicExitReason reason, string message = "")
        {
            RequestExitFade(reason, message, force: false);
        }

        internal void AdvanceFadeForTesting(float deltaSeconds)
        {
            AdvancePresentation(deltaSeconds);
        }

        internal bool AcknowledgeOpaqueRenderForTesting()
        {
            if (!_awaitingOpaqueRender ||
                _completionDispatched ||
                !_opaqueHandoffToken.IsValid ||
                CurrentFadeAlpha < 0.9999f ||
                !CinematicOpaqueHandoffRegistry
                    .TryAcknowledgeCinematicOpaqueRendered(
                        _opaqueHandoffToken))
            {
                return false;
            }

            _awaitingOpaqueRender = false;
            CompleteOnce(_pendingCompletion);
            return true;
        }

        private void Update()
        {
            if (IsPlaying && _currentClip != null)
            {
                ApplyVideoLayout(_currentClip, _currentOptions);
            }

            AdvancePresentation(Time.unscaledDeltaTime);
        }

        private void BeginEnterFadeToBlack()
        {
            CurrentPresentationState = CinematicPresentationState.EnterFadeToBlack;
            var completed = _fadeRunner.Begin(0f, 1f, _fadeSettings.EnterFadeDuration, _fadeSettings);
            ApplyFadeAlpha(_fadeRunner.CurrentAlpha);
            if (completed)
            {
                CompleteEnterFade();
            }
        }

        private void CompleteEnterFade()
        {
            CurrentPresentationState = CinematicPresentationState.PreparingVideo;
            ApplyFadeAlpha(1f);
            PrepareVideo();
        }

        private void PrepareVideo()
        {
            if (_videoPlayer == null)
            {
                RequestExitFade(CinematicExitReason.Failed, "Cinematic VideoPlayer is missing.", force: true);
                return;
            }

            _videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            _videoPlayer.prepareCompleted += HandlePrepareCompleted;
            _videoPlayer.Prepare();
            if (_videoPlayer.isPrepared)
            {
                NotifyPreparedFirstFrame();
            }
        }

        private void BeginRevealFadeFromBlack()
        {
            CurrentPresentationState = CinematicPresentationState.RevealFadeFromBlack;
            if (_fadeSettings.PlaybackStartPolicy == CinematicPlaybackStartPolicy.WithRevealFade)
            {
                StartPlayback();
            }

            var completed = _fadeRunner.Begin(1f, 0f, _fadeSettings.RevealFadeDuration, _fadeSettings);
            ApplyFadeAlpha(_fadeRunner.CurrentAlpha);
            if (completed)
            {
                CompleteRevealFade();
            }
        }

        private void CompleteRevealFade()
        {
            ApplyFadeAlpha(0f);
            if (_fadeSettings.PlaybackStartPolicy == CinematicPlaybackStartPolicy.AfterRevealFade)
            {
                StartPlayback();
            }

            CurrentPresentationState = CinematicPresentationState.Playing;
            if (_queuedSkip)
            {
                _queuedSkip = false;
                RequestExitFade(CinematicExitReason.Skipped);
            }
        }

        private void StartPlayback()
        {
            if (_videoPlayer != null && !_videoPlayer.isPlaying)
            {
                _videoPlayer.Play();
            }
        }

        private void RequestExitFade(CinematicExitReason reason, string message, bool force)
        {
            if (!IsPlaying || _completionDispatched || _exitFadeRequested)
            {
                return;
            }

            if (!force && CurrentPresentationState != CinematicPresentationState.Playing)
            {
                if (_fadeSettings.SkipDuringFadePolicy == CinematicSkipDuringFadePolicy.QueueUntilPlaying &&
                    reason == CinematicExitReason.Skipped)
                {
                    _queuedSkip = true;
                }

                return;
            }

            _exitFadeRequested = true;
            _pendingCompletion = CreateCompletion(reason, message);
            CurrentPresentationState = CinematicPresentationState.ExitFadeToBlack;
            var completed = _fadeRunner.Begin(CurrentFadeAlpha, 1f, _fadeSettings.ExitFadeDuration, _fadeSettings);
            ApplyFadeAlpha(_fadeRunner.CurrentAlpha);
            ApplyExitAudioFade();
            if (completed)
            {
                CompleteExitFade();
            }
        }

        private static CinematicPlaybackCompletion CreateCompletion(CinematicExitReason reason, string message)
        {
            switch (reason)
            {
                case CinematicExitReason.Skipped:
                    return new CinematicPlaybackCompletion(CinematicPlaybackCompletionKind.Skipped);
                case CinematicExitReason.Failed:
                    return new CinematicPlaybackCompletion(CinematicPlaybackCompletionKind.Failed, message);
                case CinematicExitReason.NaturalEnd:
                default:
                    return new CinematicPlaybackCompletion(CinematicPlaybackCompletionKind.Completed);
            }
        }

        private void AdvancePresentation(float deltaSeconds)
        {
            if (!_fadeRunner.IsRunning)
            {
                return;
            }

            var completed = _fadeRunner.Advance(deltaSeconds);
            ApplyFadeAlpha(_fadeRunner.CurrentAlpha);
            if (CurrentPresentationState == CinematicPresentationState.ExitFadeToBlack)
            {
                ApplyExitAudioFade();
            }

            if (!completed)
            {
                return;
            }

            switch (CurrentPresentationState)
            {
                case CinematicPresentationState.EnterFadeToBlack:
                    CompleteEnterFade();
                    break;
                case CinematicPresentationState.RevealFadeFromBlack:
                    CompleteRevealFade();
                    break;
                case CinematicPresentationState.ExitFadeToBlack:
                    CompleteExitFade();
                    break;
            }
        }

        private void CompleteExitFade()
        {
            ApplyFadeAlpha(1f);
            if (_fadeSettings.AudioFadeOutWithExit)
            {
                ApplyAudioFadeGain(0f);
            }

            if (_opaqueHandoffToken.IsValid)
            {
                _opaqueRenderRequestFrame = Time.frameCount;
                _awaitingOpaqueRender = true;
                Canvas.ForceUpdateCanvases();
            }
            else
            {
                CompleteOnce(_pendingCompletion);
            }
        }

        private void ApplyExitAudioFade()
        {
            if (!_fadeSettings.AudioFadeOutWithExit)
            {
                ApplyAudioFadeGain(1f);
                return;
            }

            ApplyAudioFadeGain(1f - Mathf.Clamp01(_fadeRunner.Progress));
        }

        private void ApplyFadeAlpha(float alpha)
        {
            EnsureBlackFadeLayer();
            var color = _fadeSettings.FadeColor;
            color.a = Mathf.Clamp01(alpha);
            _blackFadeImage.color = color;
        }

        private void ApplyAudioFadeGain(float gain)
        {
            _audioFadeGain = Mathf.Clamp01(gain);
            if (_audioFocusController != null)
            {
                _audioFocusController.SetCinematicFadeGain(_audioFadeGain);
            }
        }

        private void SetVideoImageVisible(bool visible)
        {
            if (_videoImage != null)
            {
                _videoImage.enabled = visible;
            }
        }

        private void EnsureVideoHierarchy()
        {
            if (_videoViewport == null)
            {
                var existing = transform.Find("Video") as RectTransform;
                if (existing == null)
                {
                    var viewportObject = new GameObject("Video", typeof(RectTransform), typeof(RectMask2D), typeof(AspectRatioFitter));
                    viewportObject.transform.SetParent(transform, false);
                    existing = (RectTransform)viewportObject.transform;
                }

                _videoViewport = existing;
            }

            UiCanvasElementFactory.Stretch(_videoViewport);

            if (_videoViewport.GetComponent<RectMask2D>() == null)
            {
                _videoViewport.gameObject.AddComponent<RectMask2D>();
            }

            if (_viewportFitter == null)
            {
                _viewportFitter = _videoViewport.GetComponent<AspectRatioFitter>();
            }

            if (_viewportFitter == null)
            {
                _viewportFitter = _videoViewport.gameObject.AddComponent<AspectRatioFitter>();
            }

            if (_videoImage == null || ReferenceEquals(_videoImage.transform, _videoViewport))
            {
                _videoImage = _videoViewport.GetComponentInChildren<RawImage>(includeInactive: true);
                if (_videoImage == null || ReferenceEquals(_videoImage.transform, _videoViewport))
                {
                    var imageObject = new GameObject("VideoImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                    imageObject.transform.SetParent(_videoViewport, false);
                    _videoImage = imageObject.GetComponent<RawImage>();
                }
            }

            var imageRect = _videoImage.transform as RectTransform;
            if (imageRect != null)
            {
                UiCanvasElementFactory.Stretch(imageRect);
            }

            _videoImage.raycastTarget = false;

            if (_contentFitter == null)
            {
                _contentFitter = _videoImage.GetComponent<AspectRatioFitter>();
            }

            if (_contentFitter == null)
            {
                _contentFitter = _videoImage.gameObject.AddComponent<AspectRatioFitter>();
            }
        }

        private void EnsureBlackFadeLayer()
        {
            if (_blackFadeImage == null)
            {
                var existing = transform.Find("BlackFade") as RectTransform;
                if (existing != null)
                {
                    _blackFadeImage = existing.GetComponent<Image>();
                }

                if (_blackFadeImage == null)
                {
                    var fadeObject = new GameObject("BlackFade", typeof(RectTransform), typeof(Image));
                    fadeObject.transform.SetParent(transform, false);
                    existing = (RectTransform)fadeObject.transform;
                    _blackFadeImage = fadeObject.GetComponent<Image>();
                }

                UiCanvasElementFactory.Stretch(existing);
            }
            else if (_blackFadeImage.transform is RectTransform fadeRect)
            {
                UiCanvasElementFactory.Stretch(fadeRect);
            }

            _blackFadeImage.raycastTarget = false;
            _blackFadeImage.transform.SetAsLastSibling();
            if (!IsPlaying && CurrentPresentationState == CinematicPresentationState.Idle)
            {
                _blackFadeImage.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void ApplyVideoLayout(VideoClip clip, SlotCinematicPlaybackOptions options)
        {
            if (_videoViewport == null || _videoImage == null)
            {
                return;
            }

            var presentationAspect = ResolvePresentationAspect(clip, options, out var useResolvedViewportRect);
            var contentAspect = ResolveClipAspect(clip, options);
            ConfiguredPresentationAspectRatio = presentationAspect;
            ConfiguredContentAspectRatio = contentAspect;
            ConfiguredScaleMode = options.ScaleMode;

            ConfigureViewportFrame(presentationAspect, useResolvedViewportRect);
            ConfigureContentImage(presentationAspect, contentAspect, options.ScaleMode);
        }

        private float ResolvePresentationAspect(
            VideoClip clip,
            SlotCinematicPlaybackOptions options,
            out bool useResolvedViewportRect)
        {
            useResolvedViewportRect = false;
            switch (options.AspectSource)
            {
                case CinematicAspectSource.SettingsSelectedAspect:
                    if (_selectedAspectProvider != null &&
                        _selectedAspectProvider.TryGetAspectRatio(out var selectedAspect) &&
                        IsValidAspect(selectedAspect))
                    {
                        return selectedAspect;
                    }

                    return ResolveAutoViewportAspect(out useResolvedViewportRect);

                case CinematicAspectSource.FixedAspect:
                    if (IsValidAspect(options.FixedAspectRatio))
                    {
                        return options.FixedAspectRatio;
                    }

                    return ResolveAutoViewportAspect(out useResolvedViewportRect);

                case CinematicAspectSource.VideoClipAspect:
                    return ResolveClipAspect(clip, options);

                case CinematicAspectSource.AutoResolvedViewport:
                default:
                    return ResolveAutoViewportAspect(out useResolvedViewportRect);
            }
        }

        private float ResolveAutoViewportAspect(out bool useResolvedViewportRect)
        {
            useResolvedViewportRect = true;
            var provider = _viewportProvider ?? ResolveDefaultViewportProvider();
            if (provider != null &&
                provider.TryGetAspectRatio(out var viewportAspect) &&
                IsValidAspect(viewportAspect))
            {
                return viewportAspect;
            }

            if (!_hasLoggedViewportFallbackWarning)
            {
                Debug.LogWarning("Cinematic viewport aspect could not be resolved. Falling back to 16:9.");
                _hasLoggedViewportFallbackWarning = true;
            }

            return 16f / 9f;
        }

        private IResolvedCinematicViewportProvider ResolveDefaultViewportProvider()
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return null;
            }

            _viewportProvider = ResolvedCinematicViewportProvider.FromHierarchy(rectTransform);
            return _viewportProvider;
        }

        private static float ResolveClipAspect(VideoClip clip, SlotCinematicPlaybackOptions options)
        {
            var width = clip != null && clip.width > 0 ? clip.width : (uint)options.RenderTextureWidth;
            var height = clip != null && clip.height > 0 ? clip.height : (uint)options.RenderTextureHeight;
            return height > 0 ? width / (float)height : 16f / 9f;
        }

        private static Vector2Int ResolveRenderTextureSize(VideoClip clip, SlotCinematicPlaybackOptions options)
        {
            var fallbackWidth = Mathf.Max(16, options.RenderTextureWidth);
            var fallbackHeight = Mathf.Max(16, options.RenderTextureHeight);
            if (clip == null || clip.width == 0 || clip.height == 0)
            {
                return new Vector2Int(fallbackWidth, fallbackHeight);
            }

            var contentAspect = clip.width / (float)clip.height;
            var budgetAspect = fallbackWidth / (float)fallbackHeight;
            if (!IsValidAspect(contentAspect) || !IsValidAspect(budgetAspect))
            {
                return new Vector2Int(fallbackWidth, fallbackHeight);
            }

            var width = fallbackWidth;
            var height = fallbackHeight;
            if (contentAspect > budgetAspect)
            {
                height = Mathf.RoundToInt(fallbackWidth / contentAspect);
            }
            else
            {
                width = Mathf.RoundToInt(fallbackHeight * contentAspect);
            }

            return new Vector2Int(Mathf.Max(16, width), Mathf.Max(16, height));
        }

        private void ConfigureViewportFrame(float aspectRatio, bool useResolvedViewportRect)
        {
            if (_viewportFitter == null)
            {
                return;
            }

            if (useResolvedViewportRect)
            {
                _viewportFitter.enabled = false;
                UiCanvasElementFactory.Stretch(_videoViewport);
                return;
            }

            _viewportFitter.enabled = true;
            _viewportFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _viewportFitter.aspectRatio = aspectRatio;
        }

        private void ConfigureContentImage(
            float presentationAspectRatio,
            float contentAspectRatio,
            CinematicScaleMode scaleMode)
        {
            if (_videoImage == null)
            {
                return;
            }

            var imageRect = _videoImage.transform as RectTransform;
            _videoImage.uvRect = FullTextureUvRect;

            if (scaleMode == CinematicScaleMode.StretchToViewport)
            {
                DisableContentFitter();
                StretchImageToViewport(imageRect);
                return;
            }

            if (!IsValidAspect(contentAspectRatio))
            {
                contentAspectRatio = 16f / 9f;
            }

            if (scaleMode == CinematicScaleMode.FitInsideViewport)
            {
                DisableContentFitter();
                FitImageInsideViewport(imageRect, contentAspectRatio);
                return;
            }

            DisableContentFitter();
            StretchImageToViewport(imageRect);
            _videoImage.uvRect = ComputeCropToFillUvRect(presentationAspectRatio, contentAspectRatio);
        }

        private static bool IsValidAspect(float aspectRatio)
        {
            return aspectRatio > 0f && !float.IsNaN(aspectRatio) && !float.IsInfinity(aspectRatio);
        }

        private void DisableContentFitter()
        {
            if (_contentFitter != null)
            {
                _contentFitter.enabled = false;
            }
        }

        private static void StretchImageToViewport(RectTransform imageRect)
        {
            if (imageRect == null)
            {
                return;
            }

            UiCanvasElementFactory.Stretch(imageRect);
        }

        private void FitImageInsideViewport(RectTransform imageRect, float contentAspectRatio)
        {
            if (imageRect == null)
            {
                return;
            }

            var viewportSize = _videoViewport != null ? _videoViewport.rect.size : Vector2.zero;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                StretchImageToViewport(imageRect);
                return;
            }

            var targetAspectRatio = viewportSize.x / viewportSize.y;
            var fittedSize = contentAspectRatio > targetAspectRatio
                ? new Vector2(viewportSize.x, viewportSize.x / contentAspectRatio)
                : new Vector2(viewportSize.y * contentAspectRatio, viewportSize.y);

            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = fittedSize;
            imageRect.localScale = Vector3.one;
        }

        private static Rect ComputeCropToFillUvRect(float targetAspectRatio, float contentAspectRatio)
        {
            if (!IsValidAspect(targetAspectRatio) || !IsValidAspect(contentAspectRatio))
            {
                return FullTextureUvRect;
            }

            if (Mathf.Approximately(targetAspectRatio, contentAspectRatio))
            {
                return FullTextureUvRect;
            }

            if (contentAspectRatio < targetAspectRatio)
            {
                var visibleHeight = Mathf.Clamp01(contentAspectRatio / targetAspectRatio);
                return new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight);
            }

            var visibleWidth = Mathf.Clamp01(targetAspectRatio / contentAspectRatio);
            return new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
        }

        private void ConfigureVideoPlayer(VideoClip clip)
        {
            _videoPlayer.Stop();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.skipOnDrop = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip = clip;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayer.controlledAudioTrackCount = 1;
            _videoPlayer.EnableAudioTrack(0, true);
            _videoPlayer.SetTargetAudioSource(0, _cinematicAudioSource);
            _videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            _videoPlayer.loopPointReached -= HandleLoopPointReached;
            _videoPlayer.loopPointReached += HandleLoopPointReached;
            _videoPlayer.errorReceived -= HandleErrorReceived;
            _videoPlayer.errorReceived += HandleErrorReceived;
        }

        private void EnsureRenderTexture(Vector2Int size)
        {
            var width = Mathf.Max(16, size.x);
            var height = Mathf.Max(16, size.y);
            if (_renderTexture != null &&
                _renderTexture.width == width &&
                _renderTexture.height == height)
            {
                return;
            }

            ReleaseRenderTexture();
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "SlotCinematicRenderTexture",
            };
            _renderTexture.Create();
            if (_videoImage != null)
            {
                _videoImage.texture = _renderTexture;
            }
        }

        private void BindSkipActions()
        {
            UnbindSkipActions();
            if (_inputActions == null)
            {
                return;
            }

            var uiMap = _inputActions.FindActionMap(UiMapName, throwIfNotFound: false);
            if (uiMap == null)
            {
                return;
            }

            _submitAction = uiMap.FindAction(SubmitActionName, throwIfNotFound: false);
            _cancelAction = uiMap.FindAction(CancelActionName, throwIfNotFound: false);
            if (_submitAction != null)
            {
                _submitAction.performed += HandleSkipActionPerformed;
                _submitAction.Enable();
            }

            if (_cancelAction != null)
            {
                _cancelAction.performed += HandleSkipActionPerformed;
                _cancelAction.Enable();
            }
        }

        private void UnbindSkipActions()
        {
            if (_submitAction != null)
            {
                _submitAction.performed -= HandleSkipActionPerformed;
            }

            if (_cancelAction != null)
            {
                _cancelAction.performed -= HandleSkipActionPerformed;
            }

            _submitAction = null;
            _cancelAction = null;
        }

        private void HandleSkipActionPerformed(InputAction.CallbackContext context)
        {
            RequestSkip();
        }

        private void HandlePrepareCompleted(VideoPlayer player)
        {
            NotifyPreparedFirstFrame();
        }

        private void HandleLoopPointReached(VideoPlayer player)
        {
            RequestExitFade(CinematicExitReason.NaturalEnd);
        }

        private void HandleErrorReceived(VideoPlayer player, string message)
        {
            RequestExitFade(CinematicExitReason.Failed, message, force: true);
        }

        private void CompleteOnce(CinematicPlaybackCompletion completion)
        {
            if (_completionDispatched)
            {
                return;
            }

            _completionDispatched = true;
            IsPlaying = false;
            _currentClip = null;
            _queuedSkip = false;
            UnbindSkipActions();

            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= HandlePrepareCompleted;
                _videoPlayer.loopPointReached -= HandleLoopPointReached;
                _videoPlayer.errorReceived -= HandleErrorReceived;
                _videoPlayer.Stop();
            }

            if (_cinematicAudioSource != null)
            {
                _cinematicAudioSource.Stop();
            }

            SetVideoImageVisible(false);
            ReleaseRenderTexture();

            if (!_opaqueHandoffToken.IsValid && _canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                gameObject.SetActive(false);
            }

            var callback = _completion;
            _completion = null;
            CurrentPresentationState = CinematicPresentationState.Completed;
            callback?.Invoke(completion);
        }

        internal void ReleaseOpaqueHandoff(CinematicOpaqueHandoffToken token)
        {
            if (!token.IsValid ||
                token != _opaqueHandoffToken ||
                !_completionDispatched ||
                CurrentPresentationState != CinematicPresentationState.Completed ||
                CurrentFadeAlpha < 0.9999f)
            {
                throw new InvalidOperationException(
                    $"Cinematic opaque owner release rejected token {token}.");
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            _opaqueHandoffToken = default;
            _awaitingOpaqueRender = false;
            gameObject.SetActive(false);
        }

        void ICinematicPlaybackOverlay.ReleaseOpaqueHandoff(
            CinematicOpaqueHandoffToken token)
        {
            ReleaseOpaqueHandoff(token);
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
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
                    "The cinematic exact-opaque render acknowledgement was stale.");
                return;
            }

            CompleteOnce(_pendingCompletion);
        }

        private void OnDestroy()
        {
            UnbindSkipActions();
            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= HandlePrepareCompleted;
                _videoPlayer.loopPointReached -= HandleLoopPointReached;
                _videoPlayer.errorReceived -= HandleErrorReceived;
            }

            if (IsPlaying && !_completionDispatched)
            {
                _completionDispatched = true;
                IsPlaying = false;
                var callback = _completion;
                _completion = null;
                callback?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Cancelled,
                    "Cinematic overlay was destroyed before playback completed."));
            }

            if (_opaqueHandoffToken.IsValid)
            {
                CinematicOpaqueHandoffRegistry.TryFailHoldingOpaque(
                    _opaqueHandoffToken,
                    "Cinematic overlay was destroyed before persistent-cover ownership transfer.");
            }

            ReleaseRenderTexture();
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null)
            {
                return;
            }

            if (_videoImage != null && ReferenceEquals(_videoImage.texture, _renderTexture))
            {
                _videoImage.texture = null;
            }

            _renderTexture.Release();
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(_renderTexture);
            }
            else
            {
                DestroyImmediate(_renderTexture);
            }

            _renderTexture = null;
        }

        private void LogCinematicDiagnosticsIfNeeded(VideoClip clip, SlotCinematicPlaybackOptions options)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_hasLoggedPlaybackDiagnostics)
            {
                return;
            }

            _hasLoggedPlaybackDiagnostics = true;
            var contentAspect = ConfiguredContentAspectRatio;
            var renderTextureAspect = ConfiguredRenderTextureAspectRatio;
            if ((clip == null || clip.width == 0 || clip.height == 0) && !_hasLoggedClipAspectFallbackWarning)
            {
                Debug.LogWarning(
                    "Cinematic clip aspect metadata is unavailable. " +
                    $"Falling back to render texture size {ConfiguredRenderTextureWidth}x{ConfiguredRenderTextureHeight}.");
                _hasLoggedClipAspectFallbackWarning = true;
            }

            if (options.ScaleMode == CinematicScaleMode.CropToFillViewport &&
                IsValidAspect(contentAspect) &&
                IsValidAspect(renderTextureAspect) &&
                Mathf.Abs(renderTextureAspect - contentAspect) > 0.005f)
            {
                Debug.LogWarning(
                    "Cinematic CropToFillViewport render texture aspect differs from content aspect. " +
                    $"clip={clip?.name}, clipSize={clip?.width}x{clip?.height}, " +
                    $"contentAspect={contentAspect:0.#####}, renderTexture={ConfiguredRenderTextureWidth}x{ConfiguredRenderTextureHeight}, " +
                    $"renderTextureAspect={renderTextureAspect:0.#####}, targetAspect={ConfiguredPresentationAspectRatio:0.#####}, " +
                    $"aspectSource={options.AspectSource}, scaleMode={options.ScaleMode}, uvRect={ConfiguredVideoUvRect}, " +
                    $"videoPlayerAspect={ConfiguredVideoPlayerAspectRatio}");
            }
#endif
        }
    }
}
