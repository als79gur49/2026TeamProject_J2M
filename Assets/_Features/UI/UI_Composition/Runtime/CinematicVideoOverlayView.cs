using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class CinematicVideoOverlayView : MonoBehaviour, IPointerClickHandler
    {
        private const string UiMapName = "UI";
        private const string SubmitActionName = "Submit";
        private const string CancelActionName = "Cancel";
        private static readonly Rect FullTextureUvRect = new Rect(0f, 0f, 1f, 1f);

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _videoViewport;
        [SerializeField] private RawImage _videoImage;
        [SerializeField] private AspectRatioFitter _viewportFitter;
        [SerializeField] private AspectRatioFitter _contentFitter;
        [SerializeField] private Button _skipButton;
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private AudioSource _cinematicAudioSource;

        private Action<CinematicPlaybackCompletion> _completion;
        private IResolvedCinematicViewportProvider _viewportProvider;
        private ICinematicSelectedAspectProvider _selectedAspectProvider;
        private InputAction _cancelAction;
        private InputAction _submitAction;
        private RenderTexture _renderTexture;
        private VideoClip _currentClip;
        private SlotCinematicPlaybackOptions _currentOptions;
        private bool _completionDispatched;
        private bool _hasLoggedPlaybackDiagnostics;
        private bool _hasLoggedClipAspectFallbackWarning;
        private bool _hasLoggedViewportFallbackWarning;
        private bool _skipEnabled;

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

        public void Initialize(
            InputActionAsset inputActions,
            IResolvedCinematicViewportProvider viewportProvider = null,
            ICinematicSelectedAspectProvider selectedAspectProvider = null)
        {
            _inputActions = inputActions;
            _viewportProvider = viewportProvider;
            _selectedAspectProvider = selectedAspectProvider;
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
                image.color = Color.black;
                image.raycastTarget = true;
                _skipButton = backgroundObject.GetComponent<Button>();
                _skipButton.transition = Selectable.Transition.None;
                _skipButton.onClick.AddListener(RequestSkip);
            }

            if (_skipButton == null)
            {
                _skipButton = background.GetComponent<Button>();
            }

            EnsureVideoHierarchy();

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
            _hasLoggedPlaybackDiagnostics = false;
            _skipEnabled = options.SkipEnabled;
            IsPlaying = true;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _currentClip = clip;
            _currentOptions = options;
            EnsureRenderTexture(ResolveRenderTextureSize(clip, options));
            ApplyVideoLayout(clip, options);

            ConfigureVideoPlayer(clip);
            LogCinematicDiagnosticsIfNeeded(clip, options);
            BindSkipActions();
            _videoPlayer.Play();
        }

        public void RequestSkip()
        {
            if (!IsPlaying || !_skipEnabled)
            {
                return;
            }

            CompleteOnce(new CinematicPlaybackCompletion(CinematicPlaybackCompletionKind.Skipped));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            RequestSkip();
        }

        internal void CompleteForTesting(CinematicPlaybackCompletionKind kind)
        {
            CompleteOnce(new CinematicPlaybackCompletion(kind));
        }

        private void Update()
        {
            if (IsPlaying && _currentClip != null)
            {
                ApplyVideoLayout(_currentClip, _currentOptions);
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

        private void HandleLoopPointReached(VideoPlayer player)
        {
            CompleteOnce(new CinematicPlaybackCompletion(CinematicPlaybackCompletionKind.Completed));
        }

        private void HandleErrorReceived(VideoPlayer player, string message)
        {
            CompleteOnce(new CinematicPlaybackCompletion(CinematicPlaybackCompletionKind.Failed, message));
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
            UnbindSkipActions();

            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= HandleLoopPointReached;
                _videoPlayer.errorReceived -= HandleErrorReceived;
                _videoPlayer.Stop();
            }

            if (_cinematicAudioSource != null)
            {
                _cinematicAudioSource.Stop();
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            var callback = _completion;
            _completion = null;
            gameObject.SetActive(false);
            callback?.Invoke(completion);
        }

        private void OnDestroy()
        {
            UnbindSkipActions();
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
