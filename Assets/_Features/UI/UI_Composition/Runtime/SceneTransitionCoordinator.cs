using System;
using System.Collections;
using System.Globalization;
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
        private ISceneTransitionActiveClock _diagnosticClock;
        private SceneTransitionDiagnosticsMonitor _currentDiagnostics;
        private Guid? _currentCampaignLaunchToken;
        private StageLaunchContext _currentLaunchContext;

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
            StageLaunchContext launchContext;
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

                launchContext = StageLaunchContext.FromHandoff(pendingHandoff);
            }
            else if (campaignLaunchToken.HasValue)
            {
                return false;
            }
            else
            {
                if (!CampaignPendinglessLaunchPolicy.IsAllowed(request))
                {
                    return false;
                }

                launchContext = StageLaunchContext.CreatePendinglessReload(request);
            }

            return TryStartTransition(
                request,
                targetSceneName,
                beforeLoad: () =>
                {
                    if (!StageLaunchContextStore.TrySetCurrent(launchContext))
                    {
                        throw new InvalidOperationException(
                            "A different stage launch operation already owns the context.");
                    }

                    CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageLaunch)
                    {
                        SceneName = targetSceneName,
                        Source = request.Source,
                        RequestedStageId = request.StageId.Value,
                        LaunchStageId = request.StageId.Value,
                        EditorDirectPlayMode = EditorDirectPlayContextStore.GetCurrentOrNone().Mode,
                    });
                },
                campaignLaunchToken,
                launchContext);
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
                campaignLaunchToken: null,
                launchContext: null);
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
            _diagnosticClock = new SceneTransitionActiveClock();
            DontDestroyOnLoad(gameObject);
            EnsureOverlayShell();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _diagnosticClock?.SetPaused(pauseStatus);
        }

        private bool TryStartTransition(
            StageNavigationRequest request,
            string targetSceneName,
            Action beforeLoad,
            Guid? campaignLaunchToken,
            StageLaunchContext launchContext)
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

            _currentCampaignLaunchToken = campaignLaunchToken;
            _currentLaunchContext = null;
            try
            {
                beforeLoad?.Invoke();
                _currentLaunchContext = launchContext;
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
                    campaignLaunchToken,
                    launchContext));
                return true;
            }
            catch
            {
                ClearFailedCampaignLaunch(_currentLaunchContext, campaignLaunchToken);
                TryHideOverlay();
                _guard.Complete(transitionId);
                _currentCampaignLaunchToken = null;
                _currentLaunchContext = null;
                throw;
            }
        }

        private IEnumerator RunTransition(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile,
            Guid? campaignLaunchToken,
            StageLaunchContext launchContext)
        {
            var state = new TransitionExecutionState();
            var routine = RunTransitionCore(transitionId, request, targetSceneName, profile, state);
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
                        if (!state.TerminalClaimed)
                        {
                            state.TerminalClaimed = true;
                            ClearFailedCampaignLaunch(launchContext, campaignLaunchToken);
                        }

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
                EndDiagnostics(transitionId, state.Diagnostics);
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

                if (_currentLaunchContext != null &&
                    launchContext != null &&
                    _currentLaunchContext.Equals(launchContext))
                {
                    _currentLaunchContext = null;
                }
            }
        }

        private IEnumerator RunTransitionCore(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile,
            TransitionExecutionState state)
        {
            if (profile.StartAsyncLoadBeforeOverlay)
            {
                state.Diagnostics = BeginDiagnostics(transitionId, request, targetSceneName);
                state.Operation = BeginLoad(targetSceneName);
                ObserveDiagnostics(state, ResolvePreActivationDiagnosticPhase(state.Operation.progress));
            }

            var preOverlayDelaySeconds = Math.Max(0f, profile.PreOverlayDelaySeconds);
            var preOverlayStartedAt = Time.unscaledTime;
            while (Time.unscaledTime - preOverlayStartedAt < preOverlayDelaySeconds)
            {
                if (state.Operation != null)
                {
                    ObserveDiagnostics(state, ResolvePreActivationDiagnosticPhase(state.Operation.progress));
                }

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
                state.Diagnostics = BeginDiagnostics(transitionId, request, targetSceneName);
                state.Operation = BeginLoad(targetSceneName);
                ObserveDiagnostics(state, ResolvePreActivationDiagnosticPhase(state.Operation.progress));
            }

            var minimumVisibleSeconds = Math.Max(0f, profile.MinimumVisibleSeconds);
            var loadReady = false;
            var minimumElapsed = !profile.HoldSceneActivationUntilMinimumElapsed;
            while (!loadReady || !minimumElapsed)
            {
                loadReady = state.Operation.progress >= 0.9f;
                ObserveDiagnostics(
                    state,
                    loadReady
                        ? SceneTransitionDiagnosticPhase.ActivationPending
                        : SceneTransitionDiagnosticPhase.WaitingForReadiness);
                overlay.SetProgress(loadReady ? 1f : NormalizeProgress(state.Operation.progress));
                minimumElapsed = IsMinimumVisibleElapsedForActivation(
                    profile,
                    overlayShownAt,
                    Time.unscaledTime);
                yield return null;
            }

            overlay.SetProgress(1f);
            ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.ActivationPending);
            state.Operation.allowSceneActivation = true;
            ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.WaitingForCompletion);
            while (!state.Operation.isDone)
            {
                yield return null;
                ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.WaitingForCompletion);
            }

            EndDiagnostics(transitionId, state.Diagnostics);
            while (!profile.HoldSceneActivationUntilMinimumElapsed &&
                   Time.unscaledTime - overlayShownAt < minimumVisibleSeconds)
            {
                yield return null;
            }
        }

        private sealed class TransitionExecutionState
        {
            public AsyncOperation Operation;
            public SceneTransitionDiagnosticsMonitor Diagnostics;
            public bool TerminalClaimed;
        }

        private SceneTransitionDiagnosticsMonitor BeginDiagnostics(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName)
        {
            var diagnostics = new SceneTransitionDiagnosticsMonitor(
                new SceneTransitionDiagnosticIdentity(
                    transitionId,
                    targetSceneName,
                    request.NavigationKind,
                    request.Source),
                SceneTransitionDiagnosticsSettings.ConservativeProductionDefault,
                _diagnosticClock ??= new SceneTransitionActiveClock(),
                new UnitySceneTransitionDiagnosticLogger(this));
            _currentDiagnostics = diagnostics;
            return diagnostics;
        }

        private static void ObserveDiagnostics(
            TransitionExecutionState state,
            SceneTransitionDiagnosticPhase phase)
        {
            if (state.Diagnostics == null || state.Operation == null)
            {
                return;
            }

            state.Diagnostics.Observe(
                phase,
                state.Operation.progress,
                state.Operation.isDone,
                state.Operation.allowSceneActivation);
        }

        private static SceneTransitionDiagnosticPhase ResolvePreActivationDiagnosticPhase(float progress)
        {
            return progress >= 0.9f
                ? SceneTransitionDiagnosticPhase.ActivationPending
                : SceneTransitionDiagnosticPhase.WaitingForReadiness;
        }

        private void EndDiagnostics(
            int transitionId,
            SceneTransitionDiagnosticsMonitor diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.End();
            if (ReferenceEquals(_currentDiagnostics, diagnostics) &&
                diagnostics.Identity.TransitionId == transitionId)
            {
                _currentDiagnostics = null;
            }
        }

        private void EndCurrentDiagnostics()
        {
            _currentDiagnostics?.End();
            _currentDiagnostics = null;
        }

        private static void ClearFailedCampaignLaunch(
            StageLaunchContext launchContext,
            Guid? campaignLaunchToken)
        {
            if (launchContext != null)
            {
                StageLaunchContextStore.TryClear(launchContext);
            }

            if (campaignLaunchToken.HasValue)
            {
                var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
                if (handoffStore.TryPeek(out var currentHandoff) &&
                    launchContext != null &&
                    launchContext.Matches(currentHandoff))
                {
                    handoffStore.TryClear(campaignLaunchToken.Value);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            EndCurrentDiagnostics();
            ClearFailedCampaignLaunch(_currentLaunchContext, _currentCampaignLaunchToken);
            if (_guard.IsTransitionInProgress)
            {
                _guard.Complete(_guard.CurrentTransitionId);
            }

            _currentLaunchContext = null;
            _currentCampaignLaunchToken = null;
            _instance = null;
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

    internal enum SceneTransitionDiagnosticPhase
    {
        LoadRequested = 0,
        WaitingForReadiness = 1,
        ActivationPending = 2,
        WaitingForCompletion = 3,
    }

    internal interface ISceneTransitionActiveClock
    {
        double ActiveTimeSeconds { get; }

        void SetPaused(bool paused);
    }

    internal sealed class SceneTransitionActiveClock : ISceneTransitionActiveClock
    {
        private readonly Func<double> _wallTimeSeconds;
        private double _activeTimeSeconds;
        private double _lastWallTimeSeconds;
        private bool _paused;

        public SceneTransitionActiveClock()
            : this(() => Time.realtimeSinceStartupAsDouble)
        {
        }

        internal SceneTransitionActiveClock(Func<double> wallTimeSeconds)
        {
            _wallTimeSeconds = wallTimeSeconds ?? throw new ArgumentNullException(nameof(wallTimeSeconds));
            _lastWallTimeSeconds = _wallTimeSeconds();
        }

        public double ActiveTimeSeconds
        {
            get
            {
                AccumulateUntil(_wallTimeSeconds());
                return _activeTimeSeconds;
            }
        }

        public void SetPaused(bool paused)
        {
            if (_paused == paused)
            {
                return;
            }

            var now = _wallTimeSeconds();
            AccumulateUntil(now);
            _paused = paused;
            _lastWallTimeSeconds = now;
        }

        private void AccumulateUntil(double now)
        {
            if (!_paused)
            {
                _activeTimeSeconds += Math.Max(0d, now - _lastWallTimeSeconds);
            }

            _lastWallTimeSeconds = now;
        }
    }

    internal readonly struct SceneTransitionDiagnosticsSettings
    {
        public SceneTransitionDiagnosticsSettings(
            double totalActiveWarningThresholdSeconds,
            double progressStallWarningThresholdSeconds,
            float progressEpsilon)
        {
            if (totalActiveWarningThresholdSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(totalActiveWarningThresholdSeconds));
            }

            if (progressStallWarningThresholdSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(progressStallWarningThresholdSeconds));
            }

            if (progressEpsilon <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(progressEpsilon));
            }

            TotalActiveWarningThresholdSeconds = totalActiveWarningThresholdSeconds;
            ProgressStallWarningThresholdSeconds = progressStallWarningThresholdSeconds;
            ProgressEpsilon = progressEpsilon;
        }

        public double TotalActiveWarningThresholdSeconds { get; }

        public double ProgressStallWarningThresholdSeconds { get; }

        public float ProgressEpsilon { get; }

        public static SceneTransitionDiagnosticsSettings ConservativeProductionDefault { get; } =
            new(
                totalActiveWarningThresholdSeconds: 120d,
                progressStallWarningThresholdSeconds: 30d,
                progressEpsilon: 0.01f);
    }

    internal readonly struct SceneTransitionDiagnosticIdentity
    {
        public SceneTransitionDiagnosticIdentity(
            int transitionId,
            string targetSceneName,
            StageNavigationKind navigationKind,
            string source)
        {
            TransitionId = transitionId;
            TargetSceneName = string.IsNullOrWhiteSpace(targetSceneName)
                ? "<unavailable>"
                : targetSceneName.Trim();
            NavigationKind = navigationKind;
            Source = string.IsNullOrWhiteSpace(source) ? "<unavailable>" : source.Trim();
        }

        public int TransitionId { get; }

        public string TargetSceneName { get; }

        public StageNavigationKind NavigationKind { get; }

        public string Source { get; }

        public string NavigationLabel =>
            NavigationKind == StageNavigationKind.None
                ? "<unavailable>"
                : NavigationKind.ToString();
    }

    internal readonly struct SceneTransitionDiagnosticSnapshot
    {
        public SceneTransitionDiagnosticSnapshot(
            bool isActive,
            SceneTransitionDiagnosticPhase phase,
            float progress,
            bool isDone,
            bool allowSceneActivation,
            double activeElapsedSeconds,
            double phaseElapsedSeconds,
            double progressStallElapsedSeconds,
            float lastMeaningfulProgress,
            bool totalDurationWarningEmitted,
            bool currentPhaseProgressStallWarningEmitted)
        {
            IsActive = isActive;
            Phase = phase;
            Progress = progress;
            IsDone = isDone;
            AllowSceneActivation = allowSceneActivation;
            ActiveElapsedSeconds = activeElapsedSeconds;
            PhaseElapsedSeconds = phaseElapsedSeconds;
            ProgressStallElapsedSeconds = progressStallElapsedSeconds;
            LastMeaningfulProgress = lastMeaningfulProgress;
            TotalDurationWarningEmitted = totalDurationWarningEmitted;
            CurrentPhaseProgressStallWarningEmitted = currentPhaseProgressStallWarningEmitted;
        }

        public bool IsActive { get; }

        public SceneTransitionDiagnosticPhase Phase { get; }

        public float Progress { get; }

        public bool IsDone { get; }

        public bool AllowSceneActivation { get; }

        public double ActiveElapsedSeconds { get; }

        public double PhaseElapsedSeconds { get; }

        public double ProgressStallElapsedSeconds { get; }

        public float LastMeaningfulProgress { get; }

        public bool TotalDurationWarningEmitted { get; }

        public bool CurrentPhaseProgressStallWarningEmitted { get; }

        public bool AnyWarningEmitted =>
            TotalDurationWarningEmitted || CurrentPhaseProgressStallWarningEmitted;
    }

    internal interface ISceneTransitionDiagnosticLogger
    {
        void LogWarning(string message);
    }

    internal sealed class UnitySceneTransitionDiagnosticLogger : ISceneTransitionDiagnosticLogger
    {
        private readonly UnityEngine.Object _context;

        public UnitySceneTransitionDiagnosticLogger(UnityEngine.Object context)
        {
            _context = context;
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(message, _context);
        }
    }

    internal sealed class SceneTransitionDiagnosticsMonitor
    {
        private readonly SceneTransitionDiagnosticIdentity _identity;
        private readonly SceneTransitionDiagnosticsSettings _settings;
        private readonly ISceneTransitionActiveClock _clock;
        private readonly ISceneTransitionDiagnosticLogger _logger;
        private readonly double _startedAtActiveTime;

        private SceneTransitionDiagnosticPhase _phase;
        private double _phaseEnteredAtActiveTime;
        private double _lastMeaningfulProgressAtActiveTime;
        private float _lastMeaningfulProgress;
        private float _progress;
        private bool _isDone;
        private bool _observedAllowSceneActivation;
        private bool _active = true;
        private bool _totalDurationWarningEmitted;
        private bool _currentPhaseProgressStallWarningEmitted;

        public SceneTransitionDiagnosticsMonitor(
            SceneTransitionDiagnosticIdentity identity,
            SceneTransitionDiagnosticsSettings settings,
            ISceneTransitionActiveClock clock,
            ISceneTransitionDiagnosticLogger logger)
        {
            _identity = identity;
            _settings = settings;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _startedAtActiveTime = _clock.ActiveTimeSeconds;
            _phase = SceneTransitionDiagnosticPhase.LoadRequested;
            _phaseEnteredAtActiveTime = _startedAtActiveTime;
            _lastMeaningfulProgressAtActiveTime = _startedAtActiveTime;
        }

        public SceneTransitionDiagnosticIdentity Identity => _identity;

        public SceneTransitionDiagnosticSnapshot Snapshot => CaptureDiagnosticState(_clock.ActiveTimeSeconds);

        public void Observe(
            SceneTransitionDiagnosticPhase phase,
            float progress,
            bool isDone,
            bool allowSceneActivation)
        {
            if (!_active)
            {
                return;
            }

            var now = _clock.ActiveTimeSeconds;
            if (_phase != phase)
            {
                _phase = phase;
                _phaseEnteredAtActiveTime = now;
                _currentPhaseProgressStallWarningEmitted = false;
            }

            _progress = progress;
            _isDone = isDone;
            _observedAllowSceneActivation = allowSceneActivation;
            if (progress >= _lastMeaningfulProgress + _settings.ProgressEpsilon)
            {
                _lastMeaningfulProgress = progress;
                _lastMeaningfulProgressAtActiveTime = now;
            }

            if (isDone)
            {
                return;
            }

            var snapshot = CaptureDiagnosticState(now);
            if (!_totalDurationWarningEmitted &&
                snapshot.ActiveElapsedSeconds >= _settings.TotalActiveWarningThresholdSeconds)
            {
                _totalDurationWarningEmitted = true;
                EmitWarning("TotalActiveDuration", CaptureDiagnosticState(now));
            }

            if (phase != SceneTransitionDiagnosticPhase.LoadRequested &&
                !_currentPhaseProgressStallWarningEmitted &&
                snapshot.ProgressStallElapsedSeconds >= _settings.ProgressStallWarningThresholdSeconds)
            {
                _currentPhaseProgressStallWarningEmitted = true;
                EmitWarning("ProgressStall", CaptureDiagnosticState(now));
            }
        }

        public void End()
        {
            _active = false;
        }

        private SceneTransitionDiagnosticSnapshot CaptureDiagnosticState(double now)
        {
            return new SceneTransitionDiagnosticSnapshot(
                _active,
                _phase,
                _progress,
                _isDone,
                _observedAllowSceneActivation,
                Math.Max(0d, now - _startedAtActiveTime),
                Math.Max(0d, now - _phaseEnteredAtActiveTime),
                Math.Max(0d, now - _lastMeaningfulProgressAtActiveTime),
                _lastMeaningfulProgress,
                _totalDurationWarningEmitted,
                _currentPhaseProgressStallWarningEmitted);
        }

        private void EmitWarning(string warningType, SceneTransitionDiagnosticSnapshot snapshot)
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "Scene transition load diagnostics warning. " +
                "WarningType={0} TransitionId={1} Scene={2} Navigation={3} Source={4} " +
                "Phase={5} Progress={6:0.###} IsDone={7} AllowSceneActivation={8} " +
                "ActiveElapsed={9:0.###}s PhaseElapsed={10:0.###}s " +
                "ProgressStallElapsed={11:0.###}s LastMeaningfulProgress={12:0.###} " +
                "TotalDurationWarningEmitted={13} PhaseProgressStallWarningEmitted={14}",
                warningType,
                _identity.TransitionId,
                _identity.TargetSceneName,
                _identity.NavigationLabel,
                _identity.Source,
                snapshot.Phase,
                snapshot.Progress,
                snapshot.IsDone,
                snapshot.AllowSceneActivation,
                snapshot.ActiveElapsedSeconds,
                snapshot.PhaseElapsedSeconds,
                snapshot.ProgressStallElapsedSeconds,
                snapshot.LastMeaningfulProgress,
                snapshot.TotalDurationWarningEmitted,
                snapshot.CurrentPhaseProgressStallWarningEmitted);

            try
            {
                _logger.LogWarning(message);
            }
            catch
            {
                // Diagnostics must never alter the scene transition terminal path.
            }
        }
    }
}
