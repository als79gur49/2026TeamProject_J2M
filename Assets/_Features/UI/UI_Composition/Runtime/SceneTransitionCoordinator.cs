using System;
using System.Collections;
using System.Globalization;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.ViewShared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.UI.Composition
{
    internal interface ISceneTransitionLoadOperation
    {
        float Progress { get; }

        bool IsDone { get; }

        bool AllowSceneActivation { get; set; }
    }

    internal sealed class UnitySceneTransitionLoadOperation : ISceneTransitionLoadOperation
    {
        private readonly AsyncOperation _operation;

        public UnitySceneTransitionLoadOperation(AsyncOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public float Progress => _operation.progress;

        public bool IsDone => _operation.isDone;

        public bool AllowSceneActivation
        {
            get => _operation.allowSceneActivation;
            set => _operation.allowSceneActivation = value;
        }
    }

    internal sealed class SceneTransitionCoordinator : MonoBehaviour
    {
        private const string RootName = "[SceneTransitionCoordinator]";
        private const string ShellPrefabResourcePath = "UI/Transitions/SceneTransitionOverlayShell";
        private const string ContentCatalogResourcePath = "UI/Transitions/SceneTransitionOverlayContentCatalog";
        private static SceneTransitionCoordinator _instance;
        private static Func<SceneTransitionOverlayShellView> _shellResourceLoaderForTests;
        private static Func<SceneTransitionOverlayContentCatalog> _contentCatalogResourceLoaderForTests;
        private static Func<string, ISceneTransitionLoadOperation> _sceneLoaderForTests;
        private static IUiAudioPort _pendingUiAudioPort;
        private static ILocalizedTextResolver _pendingLocalizedTextResolver;

        private readonly StageTransitionLaunchGuard _guard = new();
        private readonly StageTransitionProfileResolver _profileResolver = new();
        private readonly SceneTransitionOverlayContentResolver _contentResolver = new();
        [SerializeField] private SceneTransitionOverlayShellView _overlayShellPrefab;
        [SerializeField] private SceneTransitionOverlayContentCatalog _contentCatalog;
        private ISceneTransitionOverlayShellView _overlayShell;
        private IUiAudioPort _uiAudioPort;
        private ILocalizedTextResolver _localizedTextResolver;
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
                    _instance.BindLocalizedTextResolver(_pendingLocalizedTextResolver);
                    return _instance;
                }

                var root = new GameObject(RootName);
                _instance = root.AddComponent<SceneTransitionCoordinator>();
                return _instance;
            }
        }

        public bool IsTransitionInProgress => _guard.IsTransitionInProgress;

        internal int AcceptedTransitionCount { get; private set; }

        internal SceneTransitionRoutePolicy? LastResolvedRoutePolicy { get; private set; }

        public bool TryStartStageTransition(
            StageNavigationRequest request,
            string targetSceneName,
            Guid? campaignLaunchToken = null)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Scene transition requires a valid stage navigation request.", nameof(request));
            }

            var routePolicy = SceneTransitionRoutePolicyCatalog.RequireDestination(
                SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent),
                SceneTransitionDestinationKind.Gameplay);
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
                routePolicy,
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

        public bool TryStartMainMenuReturn(
            string targetSceneName,
            SceneTransitionIntent transitionIntent)
        {
            var routePolicy = SceneTransitionRoutePolicyCatalog.RequireDestination(
                SceneTransitionRoutePolicyCatalog.ResolveProduction(transitionIntent),
                SceneTransitionDestinationKind.MainMenu);
            return TryStartTransition(
                new StageNavigationRequest(
                    StageId.None,
                    StageNavigationKind.None,
                    "gameplay-to-main",
                    StageTransitionHint.ForKind(StageTransitionKind.GameplayToMain),
                    transitionIntent),
                targetSceneName,
                routePolicy,
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
            _localizedTextResolver = _pendingLocalizedTextResolver;
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
            SceneTransitionRoutePolicy routePolicy,
            Action beforeLoad,
            Guid? campaignLaunchToken,
            StageLaunchContext launchContext)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                throw new InvalidOperationException("Scene transition requires a target scene name.");
            }

            var fromSceneName = SceneManager.GetActiveScene().name;
            var profile = _profileResolver.Resolve(
                routePolicy,
                request,
                fromSceneName,
                targetSceneName);
            LastResolvedRoutePolicy = routePolicy;
            Debug.Log(
                SceneTransitionRouteDiagnostic.Format(
                    routePolicy,
                    request.Source,
                    profile.Kind),
                this);
            if (!_guard.TryBegin(out var transitionId))
            {
                Debug.LogWarning(
                    $"Ignoring scene transition to '{targetSceneName}' because transition {_guard.CurrentTransitionId} is already in progress.",
                    this);
                return false;
            }

            if (routePolicy.Intent == SceneTransitionIntent.GameplayEntry)
            {
                var mainMenuSource =
                    FindFirstObjectByType<MainMenuUiFlowInstaller>();
                if (mainMenuSource == null)
                {
                    _guard.Complete(transitionId);
                    throw new InvalidOperationException(
                        "GameplayEntry requires the production Main Menu source Iris owner before session claim.");
                }

                try
                {
                    mainMenuSource.RequireGameplayEntrySourceReady();
                }
                catch
                {
                    _guard.Complete(transitionId);
                    throw;
                }
            }

            if (routePolicy.Intent == SceneTransitionIntent.GameplayEntry &&
                StageLaunchContextStore.TryPeek(out _))
            {
                _guard.Complete(transitionId);
                throw new InvalidOperationException(
                    "GameplayEntry launch context is already owned; source Iris was not started.");
            }

            var cinematicHandoff = default(CinematicOpaqueHandoffSnapshot);
            if (routePolicy.TargetRequiresOpaqueOwnerTransfer)
            {
                try
                {
                    cinematicHandoff = RequireCinematicOpaqueHandoff(
                        routePolicy.Intent);
                }
                catch
                {
                    _guard.Complete(transitionId);
                    throw;
                }
            }

            var entrySession = SceneEntryPresentationRegistry.Current;
            var mainMenuEntrySession = MainMenuEntryPresentationRegistry.Current;
            var gameplayVisualCaptured = false;
            var gameplayTransitionBound = false;
            var mainMenuVisualCaptured = false;
            var mainMenuTransitionBound = false;
            _currentCampaignLaunchToken = campaignLaunchToken;
            _currentLaunchContext = null;
            try
            {
                if (routePolicy.ImplementsSceneTransitionSession &&
                    routePolicy.DestinationKind ==
                    SceneTransitionDestinationKind.Gameplay)
                {
                    if (routePolicy.Intent == SceneTransitionIntent.StageAdvance)
                    {
                        if (!entrySession.IsActive ||
                            entrySession.TransitionIntent != routePolicy.Intent ||
                            !entrySession.DestinationStageId.Equals(request.StageId))
                        {
                            throw new InvalidOperationException(
                                "Next-stage transition requires the matching persistent SceneEntryPresentationSession claim.");
                        }
                    }
                    else if (!entrySession.IsActive)
                    {
                        if (!SceneEntryPresentationRegistry.TryClaim(
                                routePolicy.Intent,
                                request.StageId,
                                TerminalSessionRegistry.Authority
                                    .CurrentSceneGeneration,
                                launchContext?.Source ?? request.Source,
                                launchContext?.SlotNumber ?? 0,
                                launchContext?.Token ?? Guid.Empty,
                                out _))
                        {
                            throw new InvalidOperationException(
                                $"Retry route {routePolicy.Intent} could not claim gameplay entry session.");
                        }

                        entrySession = SceneEntryPresentationRegistry.Current;
                    }

                    if (entrySession.TransitionIntent != routePolicy.Intent ||
                        !entrySession.DestinationStageId.Equals(request.StageId))
                    {
                        throw new InvalidOperationException(
                            $"Gameplay entry route {routePolicy.Intent} requires its matching current session.");
                    }

                    GameplayEntryTransitionVisualSnapshotRegistry.Capture(
                        entrySession.Token,
                        routePolicy,
                        routePolicy.Intent == SceneTransitionIntent.StageAdvance
                            ? ResultTransitionVisualSnapshotRegistry
                                .RequireCurrent()
                                .Dim
                                .OpaqueColor
                            : null,
                        routePolicy.Intent ==
                        SceneTransitionIntent.CinematicToGameplay
                            ? cinematicHandoff.OpaqueColor
                            : null);
                    gameplayVisualCaptured = true;
                    if (!SceneEntryPresentationRegistry.TryBindTransition(
                            entrySession.Token,
                            transitionId))
                    {
                        throw new InvalidOperationException(
                            $"Gameplay entry route {routePolicy.Intent} requires its matching current session.");
                    }

                    gameplayTransitionBound = true;
                }

                if (routePolicy.ImplementsSceneTransitionSession &&
                    routePolicy.DestinationKind ==
                    SceneTransitionDestinationKind.MainMenu)
                {
                    if (!mainMenuEntrySession.IsActive)
                    {
                        if (!MainMenuEntryPresentationRegistry.TryClaim(
                                routePolicy.Intent,
                                TerminalSessionRegistry.Authority
                                    .CurrentSceneGeneration,
                                request.Source,
                                out _))
                        {
                            throw new InvalidOperationException(
                                $"Main Menu route {routePolicy.Intent} could not claim its destination session.");
                        }

                        mainMenuEntrySession =
                            MainMenuEntryPresentationRegistry.Current;
                    }

                    if (mainMenuEntrySession.TransitionIntent !=
                        routePolicy.Intent)
                    {
                        throw new InvalidOperationException(
                            $"Main Menu route {routePolicy.Intent} requires its matching current session.");
                    }

                    MainMenuTransitionVisualPolicy.Capture(
                        mainMenuEntrySession.Token,
                        routePolicy,
                        routePolicy.Intent ==
                        SceneTransitionIntent.CinematicToMainMenu
                            ? cinematicHandoff.OpaqueColor
                            : null);
                    mainMenuVisualCaptured = true;
                    if (!MainMenuEntryPresentationRegistry.TryBindTransition(
                            mainMenuEntrySession.Token,
                            transitionId))
                    {
                        throw new InvalidOperationException(
                            $"Main Menu route {routePolicy.Intent} requires its matching current session.");
                    }

                    mainMenuTransitionBound = true;
                }

                var terminalToken = request.TransitionHint.TerminalToken;
                if (terminalToken.IsValid &&
                    !TerminalSessionRegistry.Authority.TryBindTransition(
                        terminalToken,
                        transitionId,
                        TerminalSessionRegistry.Current.DestinationKind))
                {
                    throw new InvalidOperationException(
                        $"Scene transition {transitionId} could not bind terminal token {terminalToken}.");
                }

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
                    routePolicy,
                    campaignLaunchToken,
                    launchContext));
                AcceptedTransitionCount++;
                return true;
            }
            catch
            {
                ClearFailedCampaignLaunch(launchContext, campaignLaunchToken);
                TryRestoreDirectPlayContextAfterFailure(request);
                const string failureReason =
                    "Scene transition failed before its scene load routine started.";
                var holdingCover = HandleSceneEntryPreCoroutineFailure(
                                       entrySession,
                                       transitionId,
                                       gameplayVisualCaptured,
                                       gameplayTransitionBound,
                                       failureReason) ||
                                   HandleMainMenuEntryPreCoroutineFailure(
                                       mainMenuEntrySession,
                                       transitionId,
                                       mainMenuVisualCaptured,
                                       mainMenuTransitionBound,
                                       failureReason);
                if (!holdingCover)
                {
                    _guard.Complete(transitionId);
                }

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
            SceneTransitionRoutePolicy routePolicy,
            Guid? campaignLaunchToken,
            StageLaunchContext launchContext)
        {
            var state = new TransitionExecutionState
            {
                Phase = SceneTransitionLifecycleState.Claimed,
                TerminalToken = request.TransitionHint.TerminalToken,
            };
            var routine = RunTransitionCore(
                transitionId,
                request,
                targetSceneName,
                profile,
                routePolicy,
                state);
            Exception failure = null;
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
                    catch (Exception exception)
                    {
                        failure = exception;
                        break;
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

                if (failure != null ||
                    state.Phase != SceneTransitionLifecycleState.Completed)
                {
                    TryRestoreDirectPlayContextAfterFailure(request);
                }

                if (failure != null && profile.RequiresExplicitContentCompletion)
                {
                    state.Phase = SceneTransitionLifecycleState.FailedHoldingCover;
                    if (state.TerminalToken.IsValid)
                    {
                        TerminalSessionRegistry.TryFailHoldingCover(
                            state.TerminalToken,
                            failure.Message);
                    }

                    TryHoldSceneEntryCoverOnFailure(request, failure.Message);

                    Debug.LogError(
                        $"Terminal scene transition {transitionId} entered FailedHoldingCover. " +
                        $"token={state.TerminalToken}, phase={state.Phase}, failure={failure.Message}. " +
                        "The launch guard remains owned and scene activation remains blocked.",
                        this);
                }
                else if (failure != null)
                {
                    ClearFailedCampaignLaunch(launchContext, campaignLaunchToken);
                    var holdingCover = TryHoldSceneEntryCoverOnFailure(
                                           request,
                                           failure.Message) ||
                                       TryHoldMainMenuEntryCoverOnFailure(
                                           request,
                                           failure.Message);
                    if (holdingCover)
                    {
                        state.Phase = SceneTransitionLifecycleState.FailedHoldingCover;
                        Debug.LogError(
                            $"Scene transition {transitionId} entered FailedHoldingCover. " +
                            $"intent={routePolicy.Intent}, failure={failure.Message}. " +
                            "The launch guard remains owned and destination interaction remains blocked.",
                            this);
                    }
                    else
                    {
                        _guard.Complete(transitionId);
                        Debug.LogException(failure, this);
                    }
                }
                else if (state.Phase != SceneTransitionLifecycleState.Completed)
                {
                    var incompleteFailure = new InvalidOperationException(
                        $"Scene transition {transitionId} stopped without reaching Completed (phase={state.Phase}).");
                    if (profile.RequiresExplicitContentCompletion)
                    {
                        state.Phase = SceneTransitionLifecycleState.FailedHoldingCover;
                        TerminalSessionRegistry.TryFailHoldingCover(
                            state.TerminalToken,
                            incompleteFailure.Message);
                        TryHoldSceneEntryCoverOnFailure(
                            request,
                            incompleteFailure.Message);
                        Debug.LogException(incompleteFailure, this);
                    }
                    else
                    {
                        ClearFailedCampaignLaunch(launchContext, campaignLaunchToken);
                        var holdingCover = TryHoldSceneEntryCoverOnFailure(
                                               request,
                                               incompleteFailure.Message) ||
                                           TryHoldMainMenuEntryCoverOnFailure(
                                               request,
                                               incompleteFailure.Message);
                        if (holdingCover)
                        {
                            state.Phase = SceneTransitionLifecycleState.FailedHoldingCover;
                        }
                        else
                        {
                            _guard.Complete(transitionId);
                        }

                        Debug.LogException(incompleteFailure, this);
                    }
                }
                else
                {
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
        }

        private IEnumerator RunTransitionCore(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile,
            SceneTransitionRoutePolicy routePolicy,
            TransitionExecutionState state)
        {
            if (routePolicy.DestinationKind ==
                    SceneTransitionDestinationKind.MainMenu &&
                routePolicy.ImplementsSceneTransitionSession)
            {
                var mainMenuRoutine = RunMainMenuDestinationTransition(
                    transitionId,
                    request,
                    targetSceneName,
                    routePolicy,
                    state);
                while (mainMenuRoutine.MoveNext())
                {
                    yield return mainMenuRoutine.Current;
                }

                yield break;
            }

            if (routePolicy.Intent == SceneTransitionIntent.DeathRetry)
            {
                if (!profile.StartAsyncLoadBeforeOverlay ||
                    !profile.RequiresExplicitContentCompletion ||
                    !profile.RequiresOpaqueTakeover)
                {
                    throw new InvalidOperationException(
                        "DeathRetry requires its exact explicit-content opaque lifecycle profile.");
                }

                state.Diagnostics = BeginDiagnostics(transitionId, request, targetSceneName);
                state.Operation = BeginLoad(targetSceneName);
                ObserveDiagnostics(
                    state,
                    ResolvePreActivationDiagnosticPhase(state.Operation.Progress));
                var explicitRoutine = RunExplicitContentTransition(
                    transitionId,
                    request,
                    targetSceneName,
                    profile,
                    state);
                while (explicitRoutine.MoveNext())
                {
                    yield return explicitRoutine.Current;
                }

                yield break;
            }

            if (routePolicy.Intent == SceneTransitionIntent.StageAdvance)
            {
                var stageAdvanceRoutine = RunStageAdvanceTransition(
                    transitionId,
                    request,
                    targetSceneName,
                    profile,
                    state);
                while (stageAdvanceRoutine.MoveNext())
                {
                    yield return stageAdvanceRoutine.Current;
                }

                yield break;
            }

            if (UsesGameplayEntryIrisSourceLifecycle(routePolicy.Intent))
            {
                if (profile.RequiresExplicitContentCompletion ||
                    profile.StartAsyncLoadBeforeOverlay)
                {
                    throw new InvalidOperationException(
                        $"Gameplay entry route {routePolicy.Intent} cannot use the explicit-content loading lifecycle.");
                }

                var gameplayEntryRoutine = RunGameplayEntryIrisTransition(
                    transitionId,
                    request,
                    targetSceneName,
                    routePolicy,
                    state);
                while (gameplayEntryRoutine.MoveNext())
                {
                    yield return gameplayEntryRoutine.Current;
                }

                yield break;
            }

            throw new InvalidOperationException(
                $"Scene transition intent {routePolicy.Intent} has no canonical executor.");
        }

        private IEnumerator RunStageAdvanceTransition(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile,
            TransitionExecutionState state)
        {
            var entrySession = SceneEntryPresentationRegistry.Current;
            if (!entrySession.IsActive ||
                entrySession.TransitionIntent != SceneTransitionIntent.StageAdvance ||
                entrySession.TransitionId != transitionId ||
                !entrySession.DestinationStageId.Equals(request.StageId))
            {
                throw new InvalidOperationException(
                    $"StageAdvance transition {transitionId} has no correlated gameplay entry session.");
            }

            var preOverlayDelaySeconds = Math.Max(0f, profile.PreOverlayDelaySeconds);
            var preOverlayStartedAt = Time.unscaledTime;
            while (Time.unscaledTime - preOverlayStartedAt < preOverlayDelaySeconds)
            {
                if (state.Operation != null)
                {
                    ObserveDiagnostics(state, ResolvePreActivationDiagnosticPhase(state.Operation.Progress));
                }

                yield return null;
            }

            state.Phase = SceneTransitionLifecycleState.Presenting;
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
                state.Phase = SceneTransitionLifecycleState.Loading;
                ObserveDiagnostics(state, ResolvePreActivationDiagnosticPhase(state.Operation.Progress));
            }

            var minimumVisibleSeconds = Math.Max(0f, profile.MinimumVisibleSeconds);
            var loadReady = false;
            var minimumElapsed = !profile.HoldSceneActivationUntilMinimumElapsed;
            while (!loadReady || !minimumElapsed)
            {
                loadReady = state.Operation.Progress >= 0.9f;
                ObserveDiagnostics(
                    state,
                    loadReady
                        ? SceneTransitionDiagnosticPhase.ActivationPending
                        : SceneTransitionDiagnosticPhase.WaitingForReadiness);
                overlay.SetProgress(loadReady ? 1f : NormalizeProgress(state.Operation.Progress));
                minimumElapsed = IsMinimumVisibleElapsedForActivation(
                    profile,
                    overlayShownAt,
                    Time.unscaledTime);
                yield return null;
            }

            var completedTerminal = TerminalSessionRegistry.Current;
            var visualSnapshot =
                ResultTransitionVisualSnapshotRegistry.RequireCurrent();
            if (!completedTerminal.Token.IsValid ||
                completedTerminal.Token != visualSnapshot.TerminalToken ||
                completedTerminal.TerminalKind != TerminalTransitionKind.Victory ||
                completedTerminal.DestinationKind !=
                TerminalDestinationKind.SameSceneStageResult ||
                completedTerminal.Phase != TerminalSessionPhase.Completed ||
                entrySession.SourceSceneGeneration !=
                completedTerminal.SourceSceneGeneration ||
                (request.TransitionHint.TerminalToken.IsValid &&
                 request.TransitionHint.TerminalToken != visualSnapshot.TerminalToken))
            {
                throw new InvalidOperationException(
                    "Next-stage transition requires a destination-correlated entry session, the completed " +
                    "StageResult Victory token/generation, and its matching immutable Result visual snapshot.");
            }

            overlay.RequestStyledCoverTakeover(
                visualSnapshot.Dim.OpaqueColor,
                visualSnapshot.RuntimeStyle.ResultExitCoverFadeDuration,
                visualSnapshot.RuntimeStyle.ResultExitCoverEasing);
            while (!overlay.IsStyledCoverFadeComplete)
            {
                overlay.TickStyledCoverTakeover(Time.unscaledDeltaTime);
                yield return null;
            }

            while (!overlay.HasRenderedOpaqueFrame)
            {
                yield return null;
            }

            overlay.AcknowledgeOpaqueHandoffReady();
            state.PersistentCoverRendered = true;
            overlay.HideVisual();
            if (!SceneEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.PersistentCoverReady) ||
                !SceneEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.Loading))
            {
                throw new InvalidOperationException(
                    $"Next-stage transition {transitionId} could not advance its persistent cover lifecycle.");
            }

            overlay.SetProgress(1f);
            ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.ActivationPending);
            state.Phase = SceneTransitionLifecycleState.ReadyToActivate;
            state.Operation.AllowSceneActivation = true;
            state.Phase = SceneTransitionLifecycleState.Activating;
            ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.WaitingForCompletion);
            while (!state.Operation.IsDone)
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

            state.Phase = SceneTransitionLifecycleState.WaitingDestinationReady;
            while (SceneEntryPresentationRegistry.IsActive)
            {
                var current = SceneEntryPresentationRegistry.Current;
                if (current.Token != entrySession.Token)
                {
                    throw new InvalidOperationException(
                        $"StageAdvance destination entry token changed from {entrySession.Token} to {current.Token}.");
                }

                if (current.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    throw new InvalidOperationException(
                        $"StageAdvance destination failed while holding opaque cover: {current.FailureReason}");
                }

                yield return null;
            }

            state.Phase = SceneTransitionLifecycleState.Completed;
        }

        private IEnumerator RunExplicitContentTransition(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            StageTransitionProfile profile,
            TransitionExecutionState state)
        {
            if (!profile.RequiresOpaqueTakeover)
            {
                throw new InvalidOperationException(
                    $"Explicit transition {transitionId} requires persistent opaque takeover.");
            }

            var token = request.TransitionHint.TerminalToken;
            if (!token.IsValid ||
                !TerminalSessionRegistry.IsActive ||
                TerminalSessionRegistry.Current.Token != token ||
                TerminalSessionRegistry.Current.TerminalKind != TerminalTransitionKind.Defeat)
            {
                throw new InvalidOperationException(
                    $"Explicit transition {transitionId} requires the active correlated Defeat terminal token. token={token}.");
            }

            var terminalPlayback = TerminalTransitionRegistry.Current;
            while (terminalPlayback == null ||
                   terminalPlayback.Request.Token != token ||
                   terminalPlayback.Request.Kind != TerminalTransitionKind.Defeat ||
                   terminalPlayback.State != TerminalTransitionState.Black)
            {
                terminalPlayback = TerminalTransitionRegistry.Current;
                if (terminalPlayback != null &&
                    terminalPlayback.Request.Token == token &&
                    (terminalPlayback.State == TerminalTransitionState.Cancelled ||
                     terminalPlayback.State == TerminalTransitionState.Disposed))
                {
                    throw new InvalidOperationException(
                        $"Transition {transitionId} Terminal Iris for token {token} was cancelled before black.");
                }

                ObserveDiagnostics(
                    state,
                    state.Operation != null
                        ? ResolvePreActivationDiagnosticPhase(state.Operation.Progress)
                        : SceneTransitionDiagnosticPhase.WaitingForReadiness);
                yield return null;
            }

            var overlay = EnsureOverlayShell();
            TerminalSessionRegistry.TryAdvance(token, TerminalSessionPhase.OpaqueHandoff);
            var entrySession = SceneEntryPresentationRegistry.Current;
            var visual = GameplayEntryTransitionVisualSnapshotRegistry.Require(
                entrySession.Token,
                SceneTransitionIntent.DeathRetry);
            overlay.RequestOpaqueTakeover(visual.HoldColor);
            while (!overlay.HasRenderedOpaqueFrame)
            {
                yield return null;
            }
            overlay.AcknowledgeOpaqueHandoffReady();
            state.PersistentCoverRendered = true;
            if (!overlay.IsOpaqueHandoffReady)
            {
                throw new InvalidOperationException(
                    $"Transition {transitionId} persistent opaque handoff was not acknowledged.");
            }

            if (!terminalPlayback.CompleteHandoff(token))
            {
                throw new InvalidOperationException(
                    $"Transition {transitionId} could not release scene-local Iris after opaque handoff.");
            }

            if (!SceneEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.PersistentCoverReady) ||
                !SceneEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.Loading))
            {
                throw new InvalidOperationException(
                    $"DeathRetry transition {transitionId} could not enter gameplay destination loading.");
            }

            var viewModel = CreateViewModel(profile, request.TransitionHint, 0f);
            var contentPrefab = ResolveContentPrefab(viewModel);
            var content = overlay.MountContent(contentPrefab);
            overlay.ShowContent(viewModel, content);
            if (content is not ITransitionContentPlaybackProvider playbackProvider ||
                playbackProvider.Playback == null)
            {
                throw new InvalidOperationException(
                    $"Transition {transitionId} requires explicit content completion, but content '{contentPrefab.name}' does not provide it.");
            }

            var contentPlayback = playbackProvider.Playback;
            var contentCompleted = contentPlayback.IsCompleted;
            var contentCancelled = contentPlayback.IsCancelled;
            var contentFailed = contentPlayback.IsFailed;
            void HandleCompleted()
            {
                contentCompleted = true;
            }

            void HandleCancelled()
            {
                contentCancelled = true;
            }

            void HandleFailed()
            {
                contentFailed = true;
            }

            contentPlayback.Completed += HandleCompleted;
            contentPlayback.Cancelled += HandleCancelled;
            contentPlayback.Failed += HandleFailed;
            try
            {
                state.Phase = SceneTransitionLifecycleState.Presenting;
                TerminalSessionRegistry.TryAdvance(token, TerminalSessionPhase.Presenting);
                PlayTransitionAudio(viewModel);
                contentCompleted = contentPlayback.IsCompleted;
                contentCancelled = contentPlayback.IsCancelled;
                contentFailed = contentPlayback.IsFailed;

                if (state.Operation == null)
                {
                    state.Diagnostics = BeginDiagnostics(transitionId, request, targetSceneName);
                    state.Operation = BeginLoad(targetSceneName);
                }

                state.Phase = SceneTransitionLifecycleState.Loading;
                TerminalSessionRegistry.TryAdvance(token, TerminalSessionPhase.Loading);
                while (true)
                {
                    var asyncLoadReady = state.Operation.Progress >= 0.9f;
                    var opaqueHandoffReady = overlay.IsOpaqueHandoffReady;
                    overlay.SetProgress(asyncLoadReady ? 1f : NormalizeProgress(state.Operation.Progress));
                    ObserveDiagnostics(
                        state,
                        CanActivateExplicitTransition(
                            opaqueHandoffReady,
                            contentCompleted,
                            asyncLoadReady,
                            contentCancelled || contentFailed)
                            ? SceneTransitionDiagnosticPhase.ActivationPending
                            : SceneTransitionDiagnosticPhase.WaitingForReadiness);

                    if (CanActivateExplicitTransition(
                            opaqueHandoffReady,
                            contentCompleted,
                            asyncLoadReady,
                            contentCancelled || contentFailed))
                    {
                        break;
                    }

                    if (contentCancelled || contentFailed)
                    {
                        throw new InvalidOperationException(
                            $"Transition {transitionId} content playback ended without completion. " +
                            $"outcome={contentPlayback.Outcome}, " +
                            $"opaqueHandoffReady={opaqueHandoffReady}, " +
                            $"chanceLostCompleted={contentCompleted}, " +
                            $"asyncLoadReady={asyncLoadReady}, " +
                            $"operationProgress={state.Operation.Progress.ToString("0.000", CultureInfo.InvariantCulture)}.");
                    }

                    yield return null;
                }

                overlay.SetProgress(1f);
                state.Phase = SceneTransitionLifecycleState.ReadyToActivate;
                TerminalSessionRegistry.TryAdvance(token, TerminalSessionPhase.ReadyToActivate);
                state.Phase = SceneTransitionLifecycleState.Activating;
                TerminalSessionRegistry.TryAdvance(token, TerminalSessionPhase.Activating);
                state.Operation.AllowSceneActivation = true;
                ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.WaitingForCompletion);
                while (!state.Operation.IsDone)
                {
                    yield return null;
                    ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.WaitingForCompletion);
                }

                EndDiagnostics(transitionId, state.Diagnostics);
                state.Phase = SceneTransitionLifecycleState.WaitingDestinationReady;
                TerminalSessionRegistry.TryAdvance(token, TerminalSessionPhase.WaitingDestinationReady);
                while (!TerminalDestinationReadiness.IsReady(token))
                {
                    var terminalSession = TerminalSessionRegistry.Current;
                    if (terminalSession.Token == token &&
                        terminalSession.Phase == TerminalSessionPhase.FailedHoldingCover)
                    {
                        throw new InvalidOperationException(
                            $"Destination readiness failed for terminal token {token}: " +
                            terminalSession.FailureReason);
                    }

                    yield return null;
                }

                while (SceneEntryPresentationRegistry.IsActive)
                {
                    var currentEntry = SceneEntryPresentationRegistry.Current;
                    if (currentEntry.Token != entrySession.Token)
                    {
                        throw new InvalidOperationException(
                            $"DeathRetry destination entry token changed from {entrySession.Token} to {currentEntry.Token}.");
                    }

                    if (currentEntry.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
                    {
                        throw new InvalidOperationException(
                            $"DeathRetry destination failed while holding black: {currentEntry.FailureReason}");
                    }

                    yield return null;
                }

                state.Phase = SceneTransitionLifecycleState.Revealing;
                TerminalSessionRegistry.TryAdvance(token, TerminalSessionPhase.Revealing);
                if (!TerminalSessionRegistry.TryComplete(token))
                {
                    throw new InvalidOperationException(
                        $"Transition {transitionId} could not complete TerminalSession token {token} after reveal.");
                }

                state.Phase = SceneTransitionLifecycleState.Completed;
            }
            finally
            {
                contentPlayback.Completed -= HandleCompleted;
                contentPlayback.Cancelled -= HandleCancelled;
                contentPlayback.Failed -= HandleFailed;
            }
        }

        private IEnumerator RunMainMenuDestinationTransition(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            SceneTransitionRoutePolicy routePolicy,
            TransitionExecutionState state)
        {
            var entrySession = MainMenuEntryPresentationRegistry.Current;
            if (!entrySession.IsActive ||
                entrySession.TransitionIntent != routePolicy.Intent ||
                entrySession.TransitionId != transitionId)
            {
                throw new InvalidOperationException(
                    $"Main Menu transition {transitionId} has no correlated destination session.");
            }

            var visual = MainMenuTransitionVisualPolicy.Require(
                entrySession.Token,
                routePolicy.Intent);
            var usesCinematicOpaqueOwner =
                routePolicy.Intent ==
                SceneTransitionIntent.CinematicToMainMenu;
            TerminalTransitionPlayback sourceClose = null;
            GameplayUiFlowInstaller gameplaySourceInstaller = null;
            if (!usesCinematicOpaqueOwner)
            {
                gameplaySourceInstaller =
                    FindFirstObjectByType<GameplayUiFlowInstaller>();
                if (gameplaySourceInstaller == null ||
                    !gameplaySourceInstaller.TryBeginMainMenuReturnSourceClose(
                        entrySession.Token,
                        visual,
                        out sourceClose))
                {
                    throw new InvalidOperationException(
                        $"ReturnToMainMenu transition {transitionId} could not start its screen-center source Iris.");
                }

                while (!gameplaySourceInstaller.TickMainMenuReturnSourceClose(
                           entrySession.Token,
                           sourceClose,
                           Time.unscaledDeltaTime))
                {
                    yield return null;
                }
            }

            var overlay = EnsureOverlay();
            overlay.RequestOpaqueTakeover(visual.HoldColor);
            while (!overlay.HasRenderedOpaqueFrame)
            {
                yield return null;
            }

            overlay.AcknowledgeOpaqueHandoffReady();
            state.PersistentCoverRendered = true;
            if (usesCinematicOpaqueOwner)
            {
                var handoff = RequireCinematicOpaqueHandoff(
                    routePolicy.Intent);
                if (!CinematicOpaqueHandoffRegistry
                        .TryTransferToPersistentCover(handoff.Token))
                {
                    throw new InvalidOperationException(
                        $"CinematicToMainMenu transition {transitionId} could not transfer rendered opaque ownership.");
                }
            }
            else if (!gameplaySourceInstaller.CompleteMainMenuReturnSourceClose(
                         entrySession.Token,
                         sourceClose))
            {
                throw new InvalidOperationException(
                    $"ReturnToMainMenu transition {transitionId} could not transfer source Iris ownership.");
            }

            overlay.HideVisual();
            if (!MainMenuEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.PersistentCoverReady) ||
                !MainMenuEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.Loading))
            {
                throw new InvalidOperationException(
                    $"Main Menu transition {transitionId} could not enter destination loading.");
            }

            state.Diagnostics = BeginDiagnostics(
                transitionId,
                request,
                targetSceneName);
            state.Operation = BeginLoad(targetSceneName);
            state.Phase = SceneTransitionLifecycleState.Loading;
            while (state.Operation.Progress < 0.9f)
            {
                ObserveDiagnostics(
                    state,
                    SceneTransitionDiagnosticPhase.WaitingForReadiness);
                yield return null;
            }

            state.Phase = SceneTransitionLifecycleState.ReadyToActivate;
            state.Operation.AllowSceneActivation = true;
            ObserveDiagnostics(
                state,
                SceneTransitionDiagnosticPhase.WaitingForCompletion);
            while (!state.Operation.IsDone)
            {
                yield return null;
                ObserveDiagnostics(
                    state,
                    SceneTransitionDiagnosticPhase.WaitingForCompletion);
            }

            EndDiagnostics(transitionId, state.Diagnostics);
            state.Phase =
                SceneTransitionLifecycleState.WaitingDestinationReady;
            while (MainMenuEntryPresentationRegistry.IsActive)
            {
                var current = MainMenuEntryPresentationRegistry.Current;
                if (current.Token != entrySession.Token)
                {
                    throw new InvalidOperationException(
                        $"Main Menu destination token changed from {entrySession.Token} to {current.Token}.");
                }

                if (current.Phase ==
                    SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    throw new InvalidOperationException(
                        $"Main Menu destination failed while holding opaque cover: {current.FailureReason}");
                }

                yield return null;
            }

            state.Phase = SceneTransitionLifecycleState.Completed;
        }

        private IEnumerator RunGameplayEntryIrisTransition(
            int transitionId,
            StageNavigationRequest request,
            string targetSceneName,
            SceneTransitionRoutePolicy routePolicy,
            TransitionExecutionState state)
        {
            var entrySession = SceneEntryPresentationRegistry.Current;
            if (!entrySession.IsActive ||
                entrySession.TransitionIntent != routePolicy.Intent ||
                entrySession.TransitionId != transitionId)
            {
                throw new InvalidOperationException(
                    $"Retry transition {transitionId} has no correlated gameplay entry session.");
            }

            var visual = GameplayEntryTransitionVisualSnapshotRegistry.Require(
                entrySession.Token,
                routePolicy.Intent);
            var usesCinematicOpaqueOwner =
                routePolicy.Intent ==
                SceneTransitionIntent.CinematicToGameplay;
            var gameplaySourceInstaller =
                routePolicy.Intent == SceneTransitionIntent.GameplayEntry ||
                usesCinematicOpaqueOwner
                    ? null
                    : FindFirstObjectByType<GameplayUiFlowInstaller>();
            var mainMenuSourceInstaller =
                routePolicy.Intent == SceneTransitionIntent.GameplayEntry
                    ? FindFirstObjectByType<MainMenuUiFlowInstaller>()
                    : null;
            TerminalTransitionPlayback sourceClose = null;
            if (!usesCinematicOpaqueOwner)
            {
                var sourceStarted = mainMenuSourceInstaller != null
                    ? mainMenuSourceInstaller.TryBeginGameplayEntrySourceClose(
                        entrySession.Token,
                        visual,
                        out sourceClose)
                    : gameplaySourceInstaller != null &&
                      gameplaySourceInstaller.TryBeginGameplayEntrySourceClose(
                          entrySession.Token,
                          visual,
                          out sourceClose);
                if (!sourceStarted)
                {
                    throw new InvalidOperationException(
                        $"Gameplay entry transition {transitionId} could not start its authored source Iris.");
                }

                while (!(mainMenuSourceInstaller != null
                           ? mainMenuSourceInstaller.TickGameplayEntrySourceClose(
                               entrySession.Token,
                               sourceClose,
                               Time.unscaledDeltaTime)
                           : gameplaySourceInstaller.TickGameplayEntrySourceClose(
                               entrySession.Token,
                               sourceClose,
                               Time.unscaledDeltaTime)))
                {
                    yield return null;
                }
            }

            var overlay = EnsureOverlay();
            overlay.RequestOpaqueTakeover(visual.HoldColor);
            while (!overlay.HasRenderedOpaqueFrame)
            {
                yield return null;
            }

            overlay.AcknowledgeOpaqueHandoffReady();
            state.PersistentCoverRendered = true;
            if (usesCinematicOpaqueOwner)
            {
                var handoff = RequireCinematicOpaqueHandoff(
                    routePolicy.Intent);
                if (!CinematicOpaqueHandoffRegistry
                        .TryTransferToPersistentCover(handoff.Token))
                {
                    throw new InvalidOperationException(
                        $"CinematicToGameplay transition {transitionId} could not transfer rendered opaque ownership.");
                }
            }
            else
            {
                var sourceCompleted = mainMenuSourceInstaller != null
                    ? mainMenuSourceInstaller.CompleteGameplayEntrySourceClose(
                        entrySession.Token,
                        sourceClose)
                    : gameplaySourceInstaller.CompleteGameplayEntrySourceClose(
                        entrySession.Token,
                        sourceClose);
                if (!sourceCompleted)
                {
                    throw new InvalidOperationException(
                        $"Gameplay entry transition {transitionId} could not hand source Iris ownership to the persistent cover.");
                }
            }

            overlay.HideVisual();
            if (!SceneEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.PersistentCoverReady) ||
                !SceneEntryPresentationRegistry.TryAdvance(
                    entrySession.Token,
                    SceneEntryPresentationPhase.Loading))
            {
                throw new InvalidOperationException(
                    $"Retry transition {transitionId} could not enter destination loading.");
            }

            state.Diagnostics = BeginDiagnostics(transitionId, request, targetSceneName);
            state.Operation = BeginLoad(targetSceneName);
            state.Phase = SceneTransitionLifecycleState.Loading;
            while (state.Operation.Progress < 0.9f)
            {
                ObserveDiagnostics(
                    state,
                    SceneTransitionDiagnosticPhase.WaitingForReadiness);
                yield return null;
            }

            state.Phase = SceneTransitionLifecycleState.ReadyToActivate;
            state.Operation.AllowSceneActivation = true;
            ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.WaitingForCompletion);
            while (!state.Operation.IsDone)
            {
                yield return null;
                ObserveDiagnostics(state, SceneTransitionDiagnosticPhase.WaitingForCompletion);
            }

            EndDiagnostics(transitionId, state.Diagnostics);
            state.Phase = SceneTransitionLifecycleState.WaitingDestinationReady;
            while (SceneEntryPresentationRegistry.IsActive)
            {
                var current = SceneEntryPresentationRegistry.Current;
                if (current.Token != entrySession.Token)
                {
                    throw new InvalidOperationException(
                        $"Retry destination entry token changed from {entrySession.Token} to {current.Token}.");
                }

                if (current.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
                {
                    throw new InvalidOperationException(
                        $"Retry destination failed while holding opaque cover: {current.FailureReason}");
                }

                yield return null;
            }

            if (string.Equals(request.Source, "pause-retry", StringComparison.Ordinal))
            {
                Time.timeScale = 1f;
            }

            state.Phase = SceneTransitionLifecycleState.Completed;
        }

        private enum SceneTransitionLifecycleState
        {
            Idle = 0,
            Claimed = 1,
            Loading = 2,
            Presenting = 3,
            ReadyToActivate = 4,
            Activating = 5,
            WaitingDestinationReady = 6,
            Revealing = 7,
            Completed = 8,
            FailedHoldingCover = 9,
        }

        private sealed class TransitionExecutionState
        {
            public ISceneTransitionLoadOperation Operation;
            public SceneTransitionDiagnosticsMonitor Diagnostics;
            public SceneTransitionLifecycleState Phase;
            public TerminalSessionToken TerminalToken;
            public bool PersistentCoverRendered;
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
                state.Operation.Progress,
                state.Operation.IsDone,
                state.Operation.AllowSceneActivation);
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

        internal static void TryRestoreDirectPlayContextAfterFailure(
            StageNavigationRequest request)
        {
            var context = request.EditorDirectPlayContext;
            if (context.Mode == EditorDirectPlayMode.None ||
                EditorDirectPlayContextStore.GetCurrentOrNone().Mode !=
                EditorDirectPlayMode.None)
            {
                return;
            }

            EditorDirectPlayContextStore.SetCurrent(context);
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            EndCurrentDiagnostics();
            if (!_guard.IsTransitionInProgress ||
                TerminalSessionRegistry.Current.Phase != TerminalSessionPhase.FailedHoldingCover)
            {
                ClearFailedCampaignLaunch(_currentLaunchContext, _currentCampaignLaunchToken);
            }

            _currentLaunchContext = null;
            _currentCampaignLaunchToken = null;
            _instance = null;
        }

        private static ISceneTransitionLoadOperation BeginLoad(string targetSceneName)
        {
            if (_sceneLoaderForTests != null)
            {
                return _sceneLoaderForTests(targetSceneName) ??
                       throw new InvalidOperationException(
                           $"Test scene loader returned null for scene '{targetSceneName}'.");
            }

            var operation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException($"LoadSceneAsync returned null for scene '{targetSceneName}'.");
            }

            operation.allowSceneActivation = false;
            return new UnitySceneTransitionLoadOperation(operation);
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

        private bool HandleSceneEntryPreCoroutineFailure(
            SceneEntryPresentationSnapshot expected,
            long transitionId,
            bool visualCaptured,
            bool transitionBound,
            string failureReason)
        {
            if (!expected.IsActive)
            {
                return false;
            }

            var current = SceneEntryPresentationRegistry.Current;
            if (!current.IsActive ||
                current.Token != expected.Token ||
                current.TransitionIntent != expected.TransitionIntent ||
                !current.DestinationStageId.Equals(expected.DestinationStageId))
            {
                if (visualCaptured)
                {
                    GameplayEntryTransitionVisualSnapshotRegistry.Clear(
                        expected.Token);
                }

                return false;
            }

            if (current.Phase == SceneEntryPresentationPhase.Claimed)
            {
                if (visualCaptured)
                {
                    GameplayEntryTransitionVisualSnapshotRegistry.Clear(
                        expected.Token);
                }

                SceneEntryPresentationRegistry.TryCancelClaim(expected.Token);
                return false;
            }

            if (!transitionBound || current.TransitionId != transitionId)
            {
                return false;
            }

            if (current.Phase !=
                SceneEntryPresentationPhase.PersistentCoverRequested)
            {
                return true;
            }

            if (visualCaptured)
            {
                try
                {
                    var overlay = EnsureOverlay();
                    overlay.RequestOpaqueTakeover(
                        GameplayEntryTransitionVisualSnapshotRegistry
                            .Require(expected.Token, expected.TransitionIntent)
                            .HoldColor);
                    overlay.HideVisual();
                }
                catch (Exception overlayFailure)
                {
                    Debug.LogException(overlayFailure, this);
                }
            }

            return SceneEntryPresentationRegistry.TryFailHoldingCover(
                expected.Token,
                failureReason);
        }

        private bool HandleMainMenuEntryPreCoroutineFailure(
            MainMenuEntryPresentationSnapshot expected,
            long transitionId,
            bool visualCaptured,
            bool transitionBound,
            string failureReason)
        {
            if (!expected.IsActive)
            {
                return false;
            }

            var current = MainMenuEntryPresentationRegistry.Current;
            if (!current.IsActive ||
                current.Token != expected.Token ||
                current.TransitionIntent != expected.TransitionIntent)
            {
                if (visualCaptured)
                {
                    MainMenuTransitionVisualPolicy.Clear(expected.Token);
                }

                return false;
            }

            if (current.Phase == SceneEntryPresentationPhase.Claimed)
            {
                if (visualCaptured)
                {
                    MainMenuTransitionVisualPolicy.Clear(expected.Token);
                }

                MainMenuEntryPresentationRegistry.TryCancelClaim(expected.Token);
                return false;
            }

            if (!transitionBound || current.TransitionId != transitionId)
            {
                return false;
            }

            if (current.Phase !=
                SceneEntryPresentationPhase.PersistentCoverRequested)
            {
                return true;
            }

            if (visualCaptured)
            {
                try
                {
                    var overlay = EnsureOverlay();
                    overlay.RequestOpaqueTakeover(
                        MainMenuTransitionVisualPolicy
                            .Require(expected.Token, expected.TransitionIntent)
                            .HoldColor);
                    overlay.HideVisual();
                }
                catch (Exception overlayFailure)
                {
                    Debug.LogException(overlayFailure, this);
                }
            }

            return MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                expected.Token,
                failureReason);
        }

        private bool TryHoldSceneEntryCoverOnFailure(
            StageNavigationRequest request,
            string failureReason)
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (!session.IsActive ||
                session.TransitionIntent != request.TransitionIntent)
            {
                return false;
            }

            try
            {
                var overlay = EnsureOverlay();
                overlay.RequestOpaqueTakeover(
                    GameplayEntryTransitionVisualSnapshotRegistry
                        .Require(session.Token, session.TransitionIntent)
                        .HoldColor);
                overlay.HideVisual();
                SceneEntryPresentationRegistry.TryFailHoldingCover(
                    session.Token,
                    string.IsNullOrWhiteSpace(failureReason)
                        ? "Next-stage transition failed while the persistent cover was active."
                        : failureReason);
                return true;
            }
            catch (Exception overlayFailure)
            {
                Debug.LogException(overlayFailure, this);
                return false;
            }
        }

        private bool TryHoldMainMenuEntryCoverOnFailure(
            StageNavigationRequest request,
            string failureReason)
        {
            var session = MainMenuEntryPresentationRegistry.Current;
            if (!session.IsActive ||
                session.TransitionIntent != request.TransitionIntent)
            {
                return false;
            }

            try
            {
                var overlay = EnsureOverlay();
                overlay.RequestOpaqueTakeover(
                    MainMenuTransitionVisualPolicy
                        .Require(session.Token, session.TransitionIntent)
                        .HoldColor);
                overlay.HideVisual();
                MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                    session.Token,
                    failureReason);
                return true;
            }
            catch (Exception overlayFailure)
            {
                Debug.LogException(overlayFailure, this);
                return false;
            }
        }

        private static CinematicOpaqueHandoffSnapshot
            RequireCinematicOpaqueHandoff(SceneTransitionIntent intent)
        {
            var handoff = CinematicOpaqueHandoffRegistry.Current;
            if (!handoff.IsActive ||
                handoff.Intent != intent ||
                handoff.Phase !=
                CinematicOpaqueHandoffPhase.CinematicOpaqueRendered ||
                handoff.SourceSceneGeneration !=
                TerminalSessionRegistry.Authority.CurrentSceneGeneration)
            {
                throw new InvalidOperationException(
                    $"Cinematic route {intent} requires its current exact-opaque rendered owner.");
            }

            return handoff;
        }

        private static float NormalizeProgress(float progress)
        {
            return Mathf.Clamp01(progress / 0.9f);
        }

        private static bool UsesGameplayEntryIrisSourceLifecycle(
            SceneTransitionIntent intent)
        {
            return intent == SceneTransitionIntent.GameplayEntry ||
                   intent == SceneTransitionIntent.ManualRetry ||
                   intent == SceneTransitionIntent.DemoStageRelaunch ||
                   intent == SceneTransitionIntent.CinematicToGameplay;
        }

        internal static void SetOverlayShellResourceLoaderForTests(Func<SceneTransitionOverlayShellView> loader)
        {
            _shellResourceLoaderForTests = loader;
        }

        internal static void SetContentCatalogResourceLoaderForTests(Func<SceneTransitionOverlayContentCatalog> loader)
        {
            _contentCatalogResourceLoaderForTests = loader;
        }

        internal static void SetSceneLoaderForTests(
            Func<string, ISceneTransitionLoadOperation> loader)
        {
            _sceneLoaderForTests = loader;
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

        internal void BindLocalizedTextResolver(ILocalizedTextResolver localizedTextResolver)
        {
            _localizedTextResolver = localizedTextResolver;
        }

        internal static void BindLocalizedTextResolverForCurrentScene(
            ILocalizedTextResolver localizedTextResolver)
        {
            _pendingLocalizedTextResolver = localizedTextResolver;
            _instance?.BindLocalizedTextResolver(localizedTextResolver);
        }

        internal static void UnbindLocalizedTextResolverForCurrentScene(
            ILocalizedTextResolver localizedTextResolver)
        {
            if (!ReferenceEquals(_pendingLocalizedTextResolver, localizedTextResolver))
            {
                return;
            }

            _pendingLocalizedTextResolver = null;
            if (_instance != null &&
                ReferenceEquals(_instance._localizedTextResolver, localizedTextResolver))
            {
                _instance._localizedTextResolver = null;
            }
        }

        internal static bool ReleaseSceneEntryCover(SceneEntrySessionToken token)
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (_instance == null ||
                !session.IsActive ||
                session.Token != token ||
                session.Phase != SceneEntryPresentationPhase.EntryIrisClosed)
            {
                return false;
            }

            _instance.EnsureOverlay().HideAll();
            return true;
        }

        internal static bool ReleaseMainMenuEntryCover(
            MainMenuEntrySessionToken token)
        {
            var session = MainMenuEntryPresentationRegistry.Current;
            if (_instance == null ||
                !session.IsActive ||
                session.Token != token ||
                session.Phase != SceneEntryPresentationPhase.EntryIrisClosed)
            {
                return false;
            }

            _instance.EnsureOverlay().HideAll();
            return true;
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

        private SceneTransitionOverlayModel CreateViewModel(
            StageTransitionProfile profile,
            StageTransitionHint hint,
            float progress01)
        {
            var transitionKind = ResolveTransitionKind(profile, hint);
            var hasChanceLost = transitionKind == StageTransitionKind.DeathRetryChanceLost &&
                                profile.OverlayKind == TransitionOverlayKind.ChanceLost &&
                                hint.HasChanceLostPayload;
            var payload = hasChanceLost ? hint.ChanceLostPayload : default;
            var hasLocalizedLoadingText =
                transitionKind == StageTransitionKind.StageClearNext ||
                transitionKind == StageTransitionKind.DeathRetryChanceLost;
            var text = hasLocalizedLoadingText
                ? CreateTransitionTextSnapshot(
                    includeRemainingChances:
                        transitionKind == StageTransitionKind.DeathRetryChanceLost)
                : default;
            return new SceneTransitionOverlayModel(
                transitionKind,
                profile.OverlayKind,
                profile.BlockInput,
                profile.ShowProgress,
                progress01,
                hasChanceLost,
                hasChanceLost ? payload.PreviousRemainingChances : 0,
                hasChanceLost ? payload.CurrentRemainingChances : 0,
                hasChanceLost ? payload.TotalChances : 0,
                hasChanceLost ? payload.DeathCount : 0,
                hint.TerminalClaimId,
                text);
        }

        private SceneTransitionOverlayTextSnapshot CreateTransitionTextSnapshot(
            bool includeRemainingChances)
        {
            return new SceneTransitionOverlayTextSnapshot(
                _localizedTextResolver?.CurrentLocaleCode ?? UnityStringTableTextResolver.DefaultLocaleCode,
                includeRemainingChances
                    ? ResolveTransitionText(
                        SceneTransitionTextDescriptors.RemainingChances,
                        SceneTransitionLocalizationEntryId.RemainingChances)
                    : string.Empty,
                ResolveTransitionText(
                    SceneTransitionTextDescriptors.Loading,
                    SceneTransitionLocalizationEntryId.Loading));
        }

        private string ResolveTransitionText(
            LocalizedTextDescriptor descriptor,
            SceneTransitionLocalizationEntryId entryId)
        {
            if (_localizedTextResolver != null)
            {
                var resolved = _localizedTextResolver.Resolve(descriptor);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            foreach (var entry in SceneTransitionLocalizationContract.Entries)
            {
                if (entry.Id == entryId)
                {
                    return entry.English;
                }
            }

            return string.Empty;
        }

        private static StageTransitionKind ResolveTransitionKind(StageTransitionProfile profile, StageTransitionHint hint)
        {
            if (profile != null && profile.Kind != StageTransitionKind.Unknown)
            {
                return profile.Kind;
            }

            return hint.Kind;
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

        internal static bool CanActivateExplicitTransition(
            bool opaqueHandoffReady,
            bool contentCompleted,
            bool asyncLoadReady,
            bool contentCancelled)
        {
            return opaqueHandoffReady &&
                   contentCompleted &&
                   asyncLoadReady &&
                   !contentCancelled;
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
