using System;
using System.Collections;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.UI.Composition
{
    internal sealed class SceneTransitionCoordinator : MonoBehaviour
    {
        private const string RootName = "[SceneTransitionCoordinator]";
        private const string ShellPrefabResourcePath = "UI/Transitions/SceneTransitionOverlayShell";
        private const string ContentCatalogResourcePath = "UI/Transitions/SceneTransitionOverlayContentCatalog";
        private static SceneTransitionCoordinator _instance;
        private static Func<SceneTransitionOverlayShellView> _shellResourceLoaderForTests;
        private static Func<SceneTransitionOverlayContentCatalog> _contentCatalogResourceLoaderForTests;
        private static IUiAudioPort _pendingUiAudioPort;

        private readonly StageTransitionLaunchGuard _guard = new();
        private readonly StageTransitionProfileResolver _profileResolver = new();
        private readonly SceneTransitionOverlayContentResolver _contentResolver = new();
        [SerializeField] private SceneTransitionOverlayShellView _overlayShellPrefab;
        [SerializeField] private SceneTransitionOverlayContentCatalog _contentCatalog;
        private ISceneTransitionOverlayShellView _overlayShell;
        private IUiAudioPort _uiAudioPort;
        private Guid? _currentCampaignLaunchToken;

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
                    _instance.BindUiAudioPort(_pendingUiAudioPort);
                    return _instance;
                }

                var root = new GameObject(RootName);
                _instance = root.AddComponent<SceneTransitionCoordinator>();
                return _instance;
            }
        }

        public bool IsTransitionInProgress => _guard.IsTransitionInProgress;

        public bool TryStartStageTransition(
            StageNavigationRequest request,
            string targetSceneName,
            Guid? campaignLaunchToken = null)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Scene transition requires a valid stage navigation request.", nameof(request));
            }

            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            if (launchHandoffStore.TryPeek(out var pendingHandoff))
            {
                if (!campaignLaunchToken.HasValue ||
                    pendingHandoff.Token != campaignLaunchToken.Value)
                {
                    return false;
                }

                if (!pendingHandoff.Matches(request))
                {
                    launchHandoffStore.TryClear(campaignLaunchToken.Value);
                    return false;
                }
            }
            else if (campaignLaunchToken.HasValue)
            {
                return false;
            }

            return TryStartTransition(
                request,
                targetSceneName,
                beforeLoad: () =>
                {
                    StageLaunchContextStore.SetCurrent(request.StageId);
                    CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
                    {
                        SceneName = targetSceneName,
                        Source = request.Source,
                        RequestedStageId = request.StageId.Value,
                        LaunchStageId = request.StageId.Value,
                        EditorDirectPlayMode = EditorDirectPlayContextStore.GetCurrentOrNone().Mode,
                    });
                },
                campaignLaunchToken);
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
                beforeLoad: StageLaunchContextStore.Clear,
                campaignLaunchToken: null);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _uiAudioPort = _pendingUiAudioPort;
            DontDestroyOnLoad(gameObject);
            EnsureOverlayShell();
        }

        private bool TryStartTransition(
            StageNavigationRequest request,
            string targetSceneName,
            Action beforeLoad,
            Guid? campaignLaunchToken)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                throw new InvalidOperationException("Scene transition requires a target scene name.");
            }

            var fromSceneName = SceneManager.GetActiveScene().name;
            var profile = _profileResolver.Resolve(request, fromSceneName, targetSceneName);
            if (!_guard.TryBegin(out var transitionId))
            {
                if (campaignLaunchToken.HasValue &&
                    _currentCampaignLaunchToken != campaignLaunchToken)
                {
                    CampaignLaunchHandoffSessionStore.Instance.TryClear(campaignLaunchToken.Value);
                }

                Debug.LogWarning(
                    $"Ignoring scene transition to '{targetSceneName}' because transition {_guard.CurrentTransitionId} is already in progress.",
                    this);
                return false;
            }

            _currentCampaignLaunchToken = campaignLaunchToken;
            try
            {
                beforeLoad?.Invoke();
                var shell = EnsureOverlayShell();
                shell.HideAll();
                if (profile.BlockInputDuringPreOverlayDelay)
                {
                    shell.ShowBlockerOnly(true);
                }

                StartCoroutine(RunTransition(
                    transitionId,
                    request,
                    targetSceneName,
                    profile,
                    campaignLaunchToken));
                return true;
            }
            catch
            {
                ClearFailedCampaignLaunch(request.StageId, campaignLaunchToken);
                TryHideOverlay();
                _guard.Complete(transitionId);
                _currentCampaignLaunchToken = null;
                throw;
            }
        }

        private IEnumerator RunTransition(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile,
            Guid? campaignLaunchToken)
        {
            var state = new TransitionExecutionState();
            var routine = RunTransitionCore(request, targetSceneName, profile, state);
            try
            {
                while (true)
                {
                    bool hasNext;
                    object current;
                    try
                    {
                        hasNext = routine.MoveNext();
                        current = hasNext ? routine.Current : null;
                    }
                    catch
                    {
                        ClearFailedCampaignLaunch(request.StageId, campaignLaunchToken);
                        throw;
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    yield return current;
                }
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
                if (state.Operation != null && !state.Operation.allowSceneActivation)
                {
                    state.Operation.allowSceneActivation = true;
                }

                TryHideOverlay();
                _guard.Complete(transitionId);
                if (_currentCampaignLaunchToken == campaignLaunchToken)
                {
                    _currentCampaignLaunchToken = null;
                }
            }
        }

        private IEnumerator RunTransitionCore(
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile,
            TransitionExecutionState state)
        {
            if (profile.StartAsyncLoadBeforeOverlay)
            {
                state.Operation = BeginLoad(targetSceneName);
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
            PlayTransitionAudio(viewModel);
            var overlayShownAt = Time.unscaledTime;

            if (state.Operation == null)
            {
                state.Operation = BeginLoad(targetSceneName);
            }

            var minimumVisibleSeconds = Math.Max(0f, profile.MinimumVisibleSeconds);
            var loadReady = false;
            var minimumElapsed = !profile.HoldSceneActivationUntilMinimumElapsed;
            while (!loadReady || !minimumElapsed)
            {
                loadReady = state.Operation.progress >= 0.9f;
                overlay.SetProgress(loadReady ? 1f : NormalizeProgress(state.Operation.progress));
                minimumElapsed = IsMinimumVisibleElapsedForActivation(
                    profile,
                    overlayShownAt,
                    Time.unscaledTime);
                yield return null;
            }

            overlay.SetProgress(1f);
            state.Operation.allowSceneActivation = true;
            while (!state.Operation.isDone)
            {
                yield return null;
            }

            while (!profile.HoldSceneActivationUntilMinimumElapsed &&
                   Time.unscaledTime - overlayShownAt < minimumVisibleSeconds)
            {
                yield return null;
            }
        }

        private sealed class TransitionExecutionState
        {
            public AsyncOperation Operation;
        }

        private static void ClearFailedCampaignLaunch(
            StageId stageId,
            Guid? campaignLaunchToken)
        {
            StageLaunchContextStore.TryClearCurrent(stageId);
            if (campaignLaunchToken.HasValue)
            {
                CampaignLaunchHandoffSessionStore.Instance.TryClear(campaignLaunchToken.Value);
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
            if (shellPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Scene transition overlay shell prefab was not found at Resources path '{ShellPrefabResourcePath}'. " +
                    "Configure the canonical scene transition overlay shell.");
            }

            var instance = Instantiate(shellPrefab, transform, false);
            instance.name = "SceneTransitionOverlayShell";
            instance.HideAll();
            _overlayShell = instance;
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

        internal static void SetOverlayShellResourceLoaderForTests(Func<SceneTransitionOverlayShellView> loader)
        {
            _shellResourceLoaderForTests = loader;
        }

        internal static void SetContentCatalogResourceLoaderForTests(Func<SceneTransitionOverlayContentCatalog> loader)
        {
            _contentCatalogResourceLoaderForTests = loader;
        }

        internal void BindUiAudioPort(IUiAudioPort uiAudioPort)
        {
            _uiAudioPort = uiAudioPort;
        }

        internal static void BindUiAudioPortForCurrentScene(IUiAudioPort uiAudioPort)
        {
            _pendingUiAudioPort = uiAudioPort;
            _instance?.BindUiAudioPort(uiAudioPort);
        }

        internal void PlayTransitionAudio(SceneTransitionOverlayModel model)
        {
            var cueId = ResolveTransitionAudioCue(model.TransitionKind);
            if (!cueId.HasValue)
            {
                return;
            }

            _uiAudioPort?.Play(cueId.Value);
        }

        internal static UiAudioCueId? ResolveTransitionAudioCue(StageTransitionKind transitionKind)
        {
            return transitionKind == StageTransitionKind.DeathRetryChanceLost
                ? UiAudioCueId.ChanceLoss
                : null;
        }

        private static SceneTransitionOverlayShellView LoadShellPrefabFromResources()
        {
            return _shellResourceLoaderForTests != null
                ? _shellResourceLoaderForTests()
                : Resources.Load<SceneTransitionOverlayShellView>(ShellPrefabResourcePath);
        }

        private SceneTransitionOverlayContentView ResolveContentPrefab(SceneTransitionOverlayModel model)
        {
            var catalog = _contentCatalog != null ? _contentCatalog : LoadContentCatalogFromResources();
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"Scene transition overlay content catalog was not found at Resources path '{ContentCatalogResourcePath}'. " +
                    "Configure the canonical scene transition content catalog.");
            }

            var content = _contentResolver.Resolve(model, catalog);
            if (content == null)
            {
                throw new InvalidOperationException(
                    "Scene transition overlay content catalog did not resolve content " +
                    $"for transition kind '{model.TransitionKind}' and overlay kind '{model.OverlayKind}'. " +
                    "Configure the canonical scene transition content catalog.");
            }

            return content;
        }

        private static SceneTransitionOverlayContentCatalog LoadContentCatalogFromResources()
        {
            return _contentCatalogResourceLoaderForTests != null
                ? _contentCatalogResourceLoaderForTests()
                : Resources.Load<SceneTransitionOverlayContentCatalog>(ContentCatalogResourcePath);
        }

        private static SceneTransitionOverlayModel CreateViewModel(
            StageTransitionProfile profile,
            StageTransitionHint hint,
            float progress01)
        {
            var transitionKind = ResolveTransitionKind(profile, hint);
            var hasChanceLost = transitionKind == StageTransitionKind.DeathRetryChanceLost &&
                                profile.OverlayKind == TransitionOverlayKind.ChanceLost &&
                                hint.HasChanceLostPayload;
            var payload = hasChanceLost ? hint.ChanceLostPayload : default;
            return new SceneTransitionOverlayModel(
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

    }
}
