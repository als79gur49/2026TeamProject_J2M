using System;
using System.Collections;
using Game.Feature.Stages;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal sealed class SceneTransitionCoordinator : MonoBehaviour
    {
        private const string RootName = "[SceneTransitionCoordinator]";
        private static SceneTransitionCoordinator _instance;

        private readonly StageTransitionLaunchGuard _guard = new();
        private readonly StageTransitionProfileResolver _profileResolver = new();
        private SceneTransitionOverlay _overlay;

        public static SceneTransitionCoordinator Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                var existing = FindAnyObjectByType<SceneTransitionCoordinator>();
                if (existing != null)
                {
                    _instance = existing;
                    return _instance;
                }

                var root = new GameObject(RootName);
                _instance = root.AddComponent<SceneTransitionCoordinator>();
                return _instance;
            }
        }

        public bool IsTransitionInProgress => _guard.IsTransitionInProgress;

        public bool TryStartStageTransition(StageNavigationRequest request, string targetSceneName)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Scene transition requires a valid stage navigation request.", nameof(request));
            }

            return TryStartTransition(
                request,
                targetSceneName,
                beforeLoad: () => StageLaunchContextStore.SetCurrent(request.StageId));
        }

        public bool TryStartMainMenuReturn(string targetSceneName)
        {
            return TryStartTransition(
                new StageNavigationRequest(
                    StageId.None,
                    StageNavigationKind.None,
                    "gameplay-to-main",
                    StageTransitionHint.ForKind(StageTransitionKind.GameplayToMain)),
                targetSceneName,
                beforeLoad: StageLaunchContextStore.Clear);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureOverlay();
        }

        private bool TryStartTransition(
            StageNavigationRequest request,
            string targetSceneName,
            Action beforeLoad)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                throw new InvalidOperationException("Scene transition requires a target scene name.");
            }

            var fromSceneName = SceneManager.GetActiveScene().name;
            var profile = _profileResolver.Resolve(request, fromSceneName, targetSceneName);
            if (!_guard.TryBegin(out var transitionId))
            {
                Debug.LogWarning(
                    $"Ignoring scene transition to '{targetSceneName}' because transition {_guard.CurrentTransitionId} is already in progress.",
                    this);
                return false;
            }

            try
            {
                beforeLoad?.Invoke();
                var overlay = EnsureOverlay();
                overlay.PrepareHidden();
                if (profile.BlockInputDuringPreOverlayDelay)
                {
                    overlay.ShowInputBlocker();
                }

                StartCoroutine(RunTransition(transitionId, request, targetSceneName, profile));
                return true;
            }
            catch
            {
                TryHideOverlay();
                _guard.Complete(transitionId);
                throw;
            }
        }

        private IEnumerator RunTransition(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile)
        {
            AsyncOperation operation = null;
            try
            {
                if (profile.StartAsyncLoadBeforeOverlay)
                {
                    operation = BeginLoad(targetSceneName);
                }

                var preOverlayDelaySeconds = Math.Max(0f, profile.PreOverlayDelaySeconds);
                var preOverlayStartedAt = Time.unscaledTime;
                while (Time.unscaledTime - preOverlayStartedAt < preOverlayDelaySeconds)
                {
                    yield return null;
                }

                var overlay = EnsureOverlay();
                overlay.Show(profile, request.TransitionHint);
                var overlayShownAt = Time.unscaledTime;

                if (operation == null)
                {
                    operation = BeginLoad(targetSceneName);
                }

                var minimumVisibleSeconds = Math.Max(0f, profile.MinimumVisibleSeconds);
                var loadReady = false;
                var minimumElapsed = !profile.HoldSceneActivationUntilMinimumElapsed;
                while (!loadReady || !minimumElapsed)
                {
                    loadReady = operation.progress >= 0.9f;
                    overlay.SetProgress(
                        loadReady ? 1f : NormalizeProgress(operation.progress),
                        profile.ShowProgress);
                    minimumElapsed = IsMinimumVisibleElapsedForActivation(
                        profile,
                        overlayShownAt,
                        Time.unscaledTime);
                    yield return null;
                }

                overlay.SetProgress(1f, profile.ShowProgress);
                operation.allowSceneActivation = true;
                while (!operation.isDone)
                {
                    yield return null;
                }

                while (!profile.HoldSceneActivationUntilMinimumElapsed &&
                       Time.unscaledTime - overlayShownAt < minimumVisibleSeconds)
                {
                    yield return null;
                }
            }
            finally
            {
                if (operation != null && !operation.allowSceneActivation)
                {
                    operation.allowSceneActivation = true;
                }

                TryHideOverlay();
                _guard.Complete(transitionId);
            }
        }

        private static AsyncOperation BeginLoad(string targetSceneName)
        {
            var operation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException($"LoadSceneAsync returned null for scene '{targetSceneName}'.");
            }

            operation.allowSceneActivation = false;
            return operation;
        }

        private SceneTransitionOverlay EnsureOverlay()
        {
            if (_overlay == null)
            {
                _overlay = new SceneTransitionOverlay(transform);
            }

            return _overlay;
        }

        private void TryHideOverlay()
        {
            try
            {
                EnsureOverlay().Hide();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private static float NormalizeProgress(float progress)
        {
            return Mathf.Clamp01(progress / 0.9f);
        }

        internal static bool IsMinimumVisibleElapsedForActivation(
            StageTransitionProfile profile,
            float overlayShownAt,
            float now)
        {
            if (profile == null || !profile.HoldSceneActivationUntilMinimumElapsed)
            {
                return true;
            }

            return now - overlayShownAt >= Math.Max(0f, profile.MinimumVisibleSeconds);
        }

        private sealed class SceneTransitionOverlay
        {
            private readonly CanvasGroup _canvasGroup;
            private readonly GameObject _root;
            private readonly Image _blocker;
            private readonly Transform _panel;
            private readonly TMP_Text _titleText;
            private readonly TMP_Text _messageText;
            private readonly TMP_Text _chanceText;
            private readonly TMP_Text _progressText;
            private readonly RectTransform _progressFill;

            public SceneTransitionOverlay(Transform parent)
            {
                _root = new GameObject("SceneTransitionOverlay", typeof(RectTransform));
                _root.transform.SetParent(parent, false);

                var rectTransform = _root.GetComponent<RectTransform>();
                UiCanvasElementFactory.Stretch(rectTransform);
                var canvas = UiOverlayCanvasConfigurator.ConfigureOverlayCanvas(_root);
                canvas.sortingOrder = 5000;

                _canvasGroup = _root.AddComponent<CanvasGroup>();
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;

                var blockerObject = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image));
                blockerObject.transform.SetParent(_root.transform, false);
                var blockerRect = blockerObject.GetComponent<RectTransform>();
                UiCanvasElementFactory.Stretch(blockerRect);
                _blocker = blockerObject.GetComponent<Image>();
                _blocker.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);
                _blocker.raycastTarget = true;

                var panel = UiCanvasElementFactory.CreatePanel(
                    "StatusPanel",
                    _root.transform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(620f, 250f),
                    Vector2.zero);
                _panel = panel;
                panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.96f);

                _titleText = UiCanvasElementFactory.CreateLabel(
                    "Title",
                    panel,
                    new Vector2(36f, -30f),
                    new Vector2(548f, 44f),
                    TextAnchor.MiddleCenter,
                    28);
                _messageText = UiCanvasElementFactory.CreateLabel(
                    "Message",
                    panel,
                    new Vector2(36f, -82f),
                    new Vector2(548f, 58f),
                    TextAnchor.MiddleCenter,
                    17);
                _chanceText = UiCanvasElementFactory.CreateLabel(
                    "ChanceText",
                    panel,
                    new Vector2(36f, -142f),
                    new Vector2(548f, 34f),
                    TextAnchor.MiddleCenter,
                    20);

                var progressBack = new GameObject("ProgressBack", typeof(RectTransform), typeof(Image));
                progressBack.transform.SetParent(panel, false);
                var progressBackRect = progressBack.GetComponent<RectTransform>();
                progressBackRect.anchorMin = new Vector2(0.5f, 0f);
                progressBackRect.anchorMax = new Vector2(0.5f, 0f);
                progressBackRect.pivot = new Vector2(0.5f, 0f);
                progressBackRect.sizeDelta = new Vector2(500f, 12f);
                progressBackRect.anchoredPosition = new Vector2(0f, 42f);
                progressBack.GetComponent<Image>().color = new Color(0.21f, 0.24f, 0.29f, 1f);

                var progressFillObject = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
                progressFillObject.transform.SetParent(progressBack.transform, false);
                _progressFill = progressFillObject.GetComponent<RectTransform>();
                _progressFill.anchorMin = Vector2.zero;
                _progressFill.anchorMax = new Vector2(0f, 1f);
                _progressFill.pivot = new Vector2(0f, 0.5f);
                _progressFill.sizeDelta = Vector2.zero;
                _progressFill.anchoredPosition = Vector2.zero;
                progressFillObject.GetComponent<Image>().color = new Color(0.66f, 0.86f, 0.95f, 1f);

                _progressText = UiCanvasElementFactory.CreateLabel(
                    "ProgressText",
                    panel,
                    new Vector2(36f, -206f),
                    new Vector2(548f, 26f),
                    TextAnchor.MiddleCenter,
                    14);

                Hide();
            }

            public void PrepareHidden()
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                _blocker.color = Color.clear;
                _blocker.raycastTarget = false;
                _panel.gameObject.SetActive(false);
                _root.SetActive(true);
            }

            public void ShowInputBlocker()
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
                _blocker.color = Color.clear;
                _blocker.raycastTarget = true;
                _panel.gameObject.SetActive(false);
                _root.SetActive(true);
            }

            public void Show(StageTransitionProfile profile, StageTransitionHint hint)
            {
                var title = ResolveTitle(profile, hint);
                var message = ResolveMessage(profile, hint);
                _titleText.text = title;
                _messageText.text = message;
                _chanceText.text = ResolveChanceText(profile, hint);
                _chanceText.gameObject.SetActive(profile.OverlayKind == TransitionOverlayKind.ChanceLost);
                SetProgress(0f, profile.ShowProgress);
                _blocker.raycastTarget = profile.BlockInput;
                _blocker.color = profile.OverlayKind == TransitionOverlayKind.None
                    ? Color.clear
                    : new Color(0.03f, 0.04f, 0.06f, 0.92f);
                _panel.gameObject.SetActive(profile.OverlayKind != TransitionOverlayKind.None);
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = profile.BlockInput;
                _canvasGroup.interactable = profile.BlockInput;
                _root.SetActive(true);
            }

            public void SetProgress(float progress, bool visible)
            {
                _progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                _progressText.text = visible ? $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%" : string.Empty;
                _progressFill.parent.gameObject.SetActive(visible);
                _progressText.gameObject.SetActive(visible);
            }

            public void Hide()
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                _blocker.color = Color.clear;
                _blocker.raycastTarget = false;
                _panel.gameObject.SetActive(false);
                _root.SetActive(false);
            }

            private static string ResolveTitle(StageTransitionProfile profile, StageTransitionHint hint)
            {
                if (hint.HasChanceLostPayload && !string.IsNullOrWhiteSpace(hint.ChanceLostPayload.Title))
                {
                    return hint.ChanceLostPayload.Title;
                }

                return profile.OverlayKind switch
                {
                    TransitionOverlayKind.ChanceLost => "Chance Lost",
                    TransitionOverlayKind.StageClear => "Loading Next Stage",
                    TransitionOverlayKind.Restart => "Restarting",
                    TransitionOverlayKind.MainMenuReturn => "Returning to Main",
                    TransitionOverlayKind.None => string.Empty,
                    _ => "Loading",
                };
            }

            private static string ResolveMessage(StageTransitionProfile profile, StageTransitionHint hint)
            {
                if (hint.HasChanceLostPayload && !string.IsNullOrWhiteSpace(hint.ChanceLostPayload.Message))
                {
                    return hint.ChanceLostPayload.Message;
                }

                return profile.OverlayKind switch
                {
                    TransitionOverlayKind.ChanceLost => "Retrying from your current stage.",
                    TransitionOverlayKind.StageClear => "Preparing the next stage.",
                    TransitionOverlayKind.Restart => "Preparing the stage.",
                    TransitionOverlayKind.MainMenuReturn => "Preparing the main menu.",
                    TransitionOverlayKind.None => string.Empty,
                    _ => "Preparing the scene.",
                };
            }

            private static string ResolveChanceText(StageTransitionProfile profile, StageTransitionHint hint)
            {
                if (profile.OverlayKind != TransitionOverlayKind.ChanceLost || !hint.HasChanceLostPayload)
                {
                    return string.Empty;
                }

                var payload = hint.ChanceLostPayload;
                return $"Chance {payload.PreviousRemainingChances} -> {payload.CurrentRemainingChances} / {payload.TotalChances}  |  Deaths {payload.DeathCount}";
            }
        }
    }
}
