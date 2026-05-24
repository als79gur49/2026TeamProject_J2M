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

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RawImage _videoImage;
        [SerializeField] private AspectRatioFitter _aspectFitter;
        [SerializeField] private Button _skipButton;
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private AudioSource _cinematicAudioSource;

        private Action<CinematicPlaybackCompletion> _completion;
        private InputAction _cancelAction;
        private InputAction _submitAction;
        private RenderTexture _renderTexture;
        private bool _completionDispatched;
        private bool _skipEnabled;

        public bool IsPlaying { get; private set; }

        public AudioSource CinematicAudioSource
        {
            get
            {
                EnsureHierarchy(new SlotCinematicPlaybackOptions(true, SlotCinematicAspectPolicy.FitInside, 1920, 1080));
                return _cinematicAudioSource;
            }
        }

        internal VideoAudioOutputMode ConfiguredAudioOutputMode =>
            _videoPlayer != null ? _videoPlayer.audioOutputMode : VideoAudioOutputMode.None;

        public void Initialize(InputActionAsset inputActions)
        {
            _inputActions = inputActions;
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

            if (_videoImage == null)
            {
                var videoObject = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                videoObject.transform.SetParent(transform, false);
                var videoRect = (RectTransform)videoObject.transform;
                UiCanvasElementFactory.Stretch(videoRect);
                _videoImage = videoObject.GetComponent<RawImage>();
                _videoImage.raycastTarget = false;
                _aspectFitter = videoObject.GetComponent<AspectRatioFitter>();
                _aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }

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

            EnsureRenderTexture(options.RenderTextureWidth, options.RenderTextureHeight);
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
            _skipEnabled = options.SkipEnabled;
            IsPlaying = true;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            if (_aspectFitter != null)
            {
                var width = clip.width > 0 ? clip.width : (uint)options.RenderTextureWidth;
                var height = clip.height > 0 ? clip.height : (uint)options.RenderTextureHeight;
                _aspectFitter.aspectRatio = height > 0 ? width / (float)height : 16f / 9f;
            }

            ConfigureVideoPlayer(clip);
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

        private void ConfigureVideoPlayer(VideoClip clip)
        {
            _videoPlayer.Stop();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.skipOnDrop = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
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

        private void EnsureRenderTexture(int width, int height)
        {
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
    }
}
