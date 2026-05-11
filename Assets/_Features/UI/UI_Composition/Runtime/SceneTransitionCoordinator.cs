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
        private const string ShellPrefabResourcePath = "UI/Transitions/SceneTransitionOverlayShell";
        private const string ContentCatalogResourcePath = "UI/Transitions/SceneTransitionOverlayContentCatalog";
        private const string OverlayPrefabResourcePath = "UI/SceneTransitionOverlayView";
        private static SceneTransitionCoordinator _instance;
        private static Func<SceneTransitionOverlayShellView> _shellResourceLoaderForTests;
        private static Func<SceneTransitionOverlayContentCatalog> _contentCatalogResourceLoaderForTests;
        private static Func<string, SceneTransitionOverlayContentView> _contentResourceLoaderForTests;
        private static Func<SceneTransitionOverlayView> _overlayResourceLoaderForTests;

        private readonly StageTransitionLaunchGuard _guard = new();
        private readonly StageTransitionProfileResolver _profileResolver = new();
        private readonly SceneTransitionOverlayContentResolver _contentResolver = new();
        [SerializeField] private SceneTransitionOverlayShellView _overlayShellPrefab;
        [SerializeField] private SceneTransitionOverlayContentCatalog _contentCatalog;
        [SerializeField] private SceneTransitionOverlayView _overlayPrefab;
        private ISceneTransitionOverlayShellView _overlayShell;
        private bool _generatedFallbackWarningLogged;
        private bool _contentFallbackWarningLogged;

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
            EnsureOverlayShell();
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
                var shell = EnsureOverlayShell();
                shell.HideAll();
                if (profile.BlockInputDuringPreOverlayDelay)
                {
                    shell.ShowBlockerOnly(true);
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

                var overlay = EnsureOverlayShell();
                var viewModel = CreateViewModel(profile, request.TransitionHint, 0f);
                var contentPrefab = ResolveContentPrefab(viewModel);
                var content = overlay.MountContent(contentPrefab);
                overlay.ShowContent(viewModel, content);
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
                    overlay.SetProgress(loadReady ? 1f : NormalizeProgress(operation.progress));
                    minimumElapsed = IsMinimumVisibleElapsedForActivation(
                        profile,
                        overlayShownAt,
                        Time.unscaledTime);
                    yield return null;
                }

                overlay.SetProgress(1f);
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

        private ISceneTransitionOverlayShellView EnsureOverlay()
        {
            return EnsureOverlayShell();
        }

        private ISceneTransitionOverlayShellView EnsureOverlayShell()
        {
            if (_overlayShell != null)
            {
                return _overlayShell;
            }

            var shellPrefab = _overlayShellPrefab != null ? _overlayShellPrefab : LoadShellPrefabFromResources();
            if (shellPrefab != null)
            {
                var instance = Instantiate(shellPrefab, transform, false);
                instance.name = "SceneTransitionOverlayShell";
                instance.HideAll();
                _overlayShell = instance;
                return _overlayShell;
            }

            var legacyPrefab = _overlayPrefab != null ? _overlayPrefab : LoadOverlayPrefabFromResources();
            if (legacyPrefab != null)
            {
                var instance = Instantiate(legacyPrefab, transform, false);
                instance.name = "SceneTransitionOverlayView";
                instance.HideAll();
                _overlayShell = new LegacyOverlayShellAdapter(instance, false);
                return _overlayShell;
            }

            if (!_generatedFallbackWarningLogged)
            {
                Debug.LogWarning(
                    $"Scene transition overlay shell prefab was not found at Resources path '{ShellPrefabResourcePath}' " +
                    $"and legacy overlay prefab was not found at Resources path '{OverlayPrefabResourcePath}'. " +
                    "Using the generated fallback overlay.",
                    this);
                _generatedFallbackWarningLogged = true;
            }

            _overlayShell = new LegacyOverlayShellAdapter(new GeneratedSceneTransitionOverlayView(transform), true);
            return _overlayShell;
        }

        private void TryHideOverlay()
        {
            try
            {
                EnsureOverlay().HideAll();
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

        internal static void SetOverlayResourceLoaderForTests(Func<SceneTransitionOverlayView> loader)
        {
            _overlayResourceLoaderForTests = loader;
        }

        internal static void SetOverlayShellResourceLoaderForTests(Func<SceneTransitionOverlayShellView> loader)
        {
            _shellResourceLoaderForTests = loader;
        }

        internal static void SetContentCatalogResourceLoaderForTests(Func<SceneTransitionOverlayContentCatalog> loader)
        {
            _contentCatalogResourceLoaderForTests = loader;
        }

        internal static void SetContentResourceLoaderForTests(Func<string, SceneTransitionOverlayContentView> loader)
        {
            _contentResourceLoaderForTests = loader;
        }

        internal bool IsUsingGeneratedOverlayForTests =>
            _overlayShell is LegacyOverlayShellAdapter { IsGenerated: true };

        private static SceneTransitionOverlayShellView LoadShellPrefabFromResources()
        {
            return _shellResourceLoaderForTests != null
                ? _shellResourceLoaderForTests()
                : Resources.Load<SceneTransitionOverlayShellView>(ShellPrefabResourcePath);
        }

        private static SceneTransitionOverlayView LoadOverlayPrefabFromResources()
        {
            return _overlayResourceLoaderForTests != null
                ? _overlayResourceLoaderForTests()
                : Resources.Load<SceneTransitionOverlayView>(OverlayPrefabResourcePath);
        }

        private SceneTransitionOverlayContentView ResolveContentPrefab(SceneTransitionOverlayViewModel model)
        {
            var catalog = _contentCatalog != null ? _contentCatalog : LoadContentCatalogFromResources();
            var content = _contentResolver.Resolve(
                model,
                catalog,
                LoadContentPrefabForTransitionKind,
                LoadContentPrefabForOverlayKind,
                LoadGenericContentPrefab);
            if (content == null && !_contentFallbackWarningLogged)
            {
                Debug.LogWarning(
                    "Scene transition overlay content prefab was not found. " +
                    "Using a generated generic transition content view.",
                    this);
                _contentFallbackWarningLogged = true;
            }

            return content;
        }

        private static SceneTransitionOverlayContentCatalog LoadContentCatalogFromResources()
        {
            return _contentCatalogResourceLoaderForTests != null
                ? _contentCatalogResourceLoaderForTests()
                : Resources.Load<SceneTransitionOverlayContentCatalog>(ContentCatalogResourcePath);
        }

        private static SceneTransitionOverlayContentView LoadContentPrefabForTransitionKind(StageTransitionKind transitionKind)
        {
            return transitionKind switch
            {
                StageTransitionKind.MainToGameplay => LoadContentPrefab("UI/Transitions/Contents/GenericLoadingOverlayContent"),
                StageTransitionKind.GameplayToMain => LoadContentPrefab("UI/Transitions/Contents/MainMenuReturnOverlayContent"),
                StageTransitionKind.StageClearNext => LoadContentPrefab("UI/Transitions/Contents/StageClearOverlayContent"),
                StageTransitionKind.StageRetryManual => LoadContentPrefab("UI/Transitions/Contents/ManualRestartOverlayContent"),
                StageTransitionKind.DeathRetryChanceLost => LoadContentPrefab("UI/Transitions/Contents/ChanceLostOverlayContent"),
                StageTransitionKind.LevelFailedRestart => LoadContentPrefab("UI/Transitions/Contents/LevelFailedRestartOverlayContent"),
                _ => null,
            };
        }

        private static SceneTransitionOverlayContentView LoadContentPrefabForOverlayKind(TransitionOverlayKind overlayKind)
        {
            return overlayKind switch
            {
                TransitionOverlayKind.GenericLoading => LoadContentPrefab("UI/Transitions/Contents/GenericLoadingOverlayContent"),
                TransitionOverlayKind.ChanceLost => LoadContentPrefab("UI/Transitions/Contents/ChanceLostOverlayContent"),
                TransitionOverlayKind.StageClear => LoadContentPrefab("UI/Transitions/Contents/StageClearOverlayContent"),
                TransitionOverlayKind.Restart => LoadContentPrefab("UI/Transitions/Contents/ManualRestartOverlayContent"),
                TransitionOverlayKind.MainMenuReturn => LoadContentPrefab("UI/Transitions/Contents/MainMenuReturnOverlayContent"),
                _ => null,
            };
        }

        private static SceneTransitionOverlayContentView LoadGenericContentPrefab()
        {
            return LoadContentPrefab("UI/Transitions/Contents/GenericLoadingOverlayContent");
        }

        private static SceneTransitionOverlayContentView LoadContentPrefab(string resourcePath)
        {
            return _contentResourceLoaderForTests != null
                ? _contentResourceLoaderForTests(resourcePath)
                : Resources.Load<SceneTransitionOverlayContentView>(resourcePath);
        }

        private static SceneTransitionOverlayViewModel CreateViewModel(
            StageTransitionProfile profile,
            StageTransitionHint hint,
            float progress01)
        {
            var transitionKind = ResolveTransitionKind(profile, hint);
            var hasChanceLost = transitionKind == StageTransitionKind.DeathRetryChanceLost &&
                                profile.OverlayKind == TransitionOverlayKind.ChanceLost &&
                                hint.HasChanceLostPayload;
            var payload = hasChanceLost ? hint.ChanceLostPayload : default;
            return new SceneTransitionOverlayViewModel(
                transitionKind,
                profile.OverlayKind,
                ResolveTitle(profile, hint, transitionKind),
                ResolveMessage(profile, hint, transitionKind),
                profile.BlockInput,
                profile.ShowProgress,
                progress01,
                hasChanceLost,
                hasChanceLost ? payload.PreviousRemainingChances : 0,
                hasChanceLost ? payload.CurrentRemainingChances : 0,
                hasChanceLost ? payload.TotalChances : 0,
                hasChanceLost ? payload.DeathCount : 0);
        }

        private static StageTransitionKind ResolveTransitionKind(StageTransitionProfile profile, StageTransitionHint hint)
        {
            if (profile != null && profile.Kind != StageTransitionKind.Unknown)
            {
                return profile.Kind;
            }

            return hint.Kind;
        }

        private static string ResolveTitle(
            StageTransitionProfile profile,
            StageTransitionHint hint,
            StageTransitionKind transitionKind)
        {
            if (hint.HasChanceLostPayload && !string.IsNullOrWhiteSpace(hint.ChanceLostPayload.Title))
            {
                return hint.ChanceLostPayload.Title;
            }

            switch (transitionKind)
            {
                case StageTransitionKind.MainToGameplay:
                    return "Loading";
                case StageTransitionKind.GameplayToMain:
                    return "Returning to Main";
                case StageTransitionKind.StageClearNext:
                    return "Loading Next Stage";
                case StageTransitionKind.StageRetryManual:
                    return "Retrying Stage";
                case StageTransitionKind.DeathRetryChanceLost:
                    return "Chance Lost";
                case StageTransitionKind.LevelFailedRestart:
                    return "Restarting Level";
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

        private static string ResolveMessage(
            StageTransitionProfile profile,
            StageTransitionHint hint,
            StageTransitionKind transitionKind)
        {
            if (hint.HasChanceLostPayload && !string.IsNullOrWhiteSpace(hint.ChanceLostPayload.Message))
            {
                return hint.ChanceLostPayload.Message;
            }

            switch (transitionKind)
            {
                case StageTransitionKind.MainToGameplay:
                    return "Preparing the stage.";
                case StageTransitionKind.GameplayToMain:
                    return "Preparing the main menu.";
                case StageTransitionKind.StageClearNext:
                    return "Preparing the next stage.";
                case StageTransitionKind.StageRetryManual:
                    return "Restarting the current stage.";
                case StageTransitionKind.DeathRetryChanceLost:
                    return "Retrying from your current stage.";
                case StageTransitionKind.LevelFailedRestart:
                    return "Returning to the first stage in this level.";
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

        private sealed class LegacyOverlayShellAdapter : ISceneTransitionOverlayShellView
        {
            private readonly ISceneTransitionOverlayView _legacyView;

            public LegacyOverlayShellAdapter(ISceneTransitionOverlayView legacyView, bool isGenerated)
            {
                _legacyView = legacyView ?? throw new ArgumentNullException(nameof(legacyView));
                IsGenerated = isGenerated;
            }

            public bool IsGenerated { get; }

            public void ShowBlockerOnly(bool blockInput)
            {
                _legacyView.ShowBlockerOnly(blockInput);
            }

            public ISceneTransitionOverlayContentView MountContent(SceneTransitionOverlayContentView contentPrefab)
            {
                return null;
            }

            public void ShowContent(
                SceneTransitionOverlayViewModel model,
                ISceneTransitionOverlayContentView content)
            {
                _legacyView.ShowOverlay(model);
            }

            public void SetProgress(float progress01)
            {
                _legacyView.SetProgress(progress01);
            }

            public void HideVisual()
            {
                _legacyView.HideVisual();
            }

            public void HideAll()
            {
                _legacyView.HideAll();
            }
        }

        private sealed class GeneratedSceneTransitionOverlayView : ISceneTransitionOverlayView
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
            private bool _showProgress;

            public GeneratedSceneTransitionOverlayView(Transform parent)
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

                HideAll();
            }

            public void ShowBlockerOnly(bool blockInput)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = blockInput;
                _canvasGroup.interactable = blockInput;
                _blocker.color = Color.clear;
                _blocker.raycastTarget = blockInput;
                _panel.gameObject.SetActive(false);
                _root.SetActive(true);
            }

            public void ShowOverlay(SceneTransitionOverlayViewModel model)
            {
                _titleText.text = model.Title;
                _messageText.text = model.Message;
                _chanceText.text = ResolveChanceText(model);
                _chanceText.gameObject.SetActive(
                    model.TransitionKind == StageTransitionKind.DeathRetryChanceLost && model.HasChanceLost);
                _showProgress = model.ShowProgress;
                SetProgress(model.Progress01);
                _blocker.raycastTarget = model.BlockInput;
                _blocker.color = model.OverlayKind == TransitionOverlayKind.None
                    ? Color.clear
                    : new Color(0.03f, 0.04f, 0.06f, 0.92f);
                _panel.gameObject.SetActive(model.OverlayKind != TransitionOverlayKind.None);
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = model.BlockInput;
                _canvasGroup.interactable = model.BlockInput;
                _root.SetActive(true);
            }

            public void SetProgress(float progress)
            {
                _progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                _progressText.text = _showProgress ? $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%" : string.Empty;
                _progressFill.parent.gameObject.SetActive(_showProgress);
                _progressText.gameObject.SetActive(_showProgress);
            }

            public void HideVisual()
            {
                _panel.gameObject.SetActive(false);
                _chanceText.gameObject.SetActive(false);
            }

            public void HideAll()
            {
                _showProgress = false;
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                _blocker.color = Color.clear;
                _blocker.raycastTarget = false;
                HideVisual();
                SetProgress(0f);
                _root.SetActive(false);
            }

            private static string ResolveChanceText(SceneTransitionOverlayViewModel model)
            {
                if (model.TransitionKind != StageTransitionKind.DeathRetryChanceLost || !model.HasChanceLost)
                {
                    return string.Empty;
                }

                return $"Chance {model.PreviousRemainingChances} -> {model.CurrentRemainingChances} / {model.TotalChances}  |  Deaths {model.DeathCount}";
            }
        }
    }
}
