using System;
using System.Reflection;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using Game.Shared.Audio;
using Game.Shared.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameplayUiFlowInstaller : MonoBehaviour,
        IStageLaunchRouterProvider,
        ITerminalTransitionPortProvider,
        ITerminalSessionAuthorityProvider
    {
        private const string MissingAudioInstallerMessage =
            "GameplayUiFlowInstaller requires a co-located AudioRuntimeInstaller on the canonical bootstrap root for SettingsScreen audio controls.";
        private const string MissingDisplayInstallerMessage =
            "GameplayUiFlowInstaller requires a co-located DisplayRuntimeInstaller on the canonical bootstrap root for SettingsScreen display controls.";
        private const string MissingUiAudioCueMapMessage =
            "GameplayUiFlowInstaller requires a serialized UiAudioCueMap on the canonical bootstrap root for UI SFX v1.";
        private const string MissingComicSequenceOverlayPrefabMessage =
            "GameplayUiFlowInstaller requires the canonical ComicSequenceOverlayView prefab reference.";
        private const string RootShellObjectName = "GameplayUiCanvasRoot";
        private const string RootShellPrefabResourcePath = "UI/GameplayUiCanvasRootShell";
        private static readonly FieldInfo TmpDropdownLiveListField =
            typeof(TMP_Dropdown).GetField(
                "m_Dropdown",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TmpDropdownBlockerField =
            typeof(TMP_Dropdown).GetField(
                "m_Blocker",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly InputSystemKeyboardBridge KeyboardBridge = new();

        [SerializeField] private GameplaySceneHost _sceneHost;
        [SerializeField] private GameplayUiCanvasRootView _rootView;
        // Phase-local HUD prefab seam only. Do not expand this into a general feature-prefab registry.
        [SerializeField] private HUDRootView _hudPrefab;
        // Screen-prefab composition remains screen-only. Do not widen this into a cross-layer asset registry.
        [SerializeField] private ScreenPrefabCatalog _screenPrefabCatalog;
        [SerializeField] private InputActionAsset _inputActions;
        // Popup-prefab composition remains popup-only. Do not widen this into a cross-layer asset registry.
        [SerializeField] private PopupPrefabCatalog _popupPrefabCatalog;
        [SerializeField] private UiAudioCueMap _uiAudioCueMap;
        [SerializeField] private GameplayStageLaunchRouteConfig _routeConfig;
        [SerializeField] private ComicSequenceDefinition _outroComicSequence;
        [SerializeField] private ComicSequenceOverlayView _comicSequenceOverlayPrefab;
        [SerializeField] private DemoStageControlSettings _demoStageControlSettings = DemoStageControlSettings.EnabledByDefault();
        [SerializeField] private bool _installOnStart = true;

        private ComicSequenceFlowCoordinator _comicSequenceFlowCoordinator;
        private CampaignStageSequenceResolver _campaignStageSequenceResolver;
        private AudioSettingsLifecycleRelay _audioSettingsLifecycleRelay;
        private DisplayPreviewTimeoutRelay _displayPreviewTimeoutRelay;
        private DisplaySettingsLifecycleRelay _displaySettingsLifecycleRelay;
        private GameplayPauseAudioBridge _gameplayPauseAudioBridge;
        private GameplayHudLocalizationBinding _gameplayHudLocalizationBinding;
        private GameplayWorldGuideLocalizationController _gameplayWorldGuideLocalizationController;
        private GameplayWorldGuidePresenter _gameplayWorldGuidePresenter;
        private bool _isInstalled;
        private IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private ILocalizedTextResolver _localizedTextResolver;
        private UiNavigationInputRouter _navigationInputRouter;
        private IUiAudioPort _uiAudioPort;
        private StageResultAutoNextDriver _stageResultAutoNextDriver;
        private HudUiAudioFeedbackController _hudUiAudioFeedbackController;
        private IDemoStageControlCommandPort _demoStageControlCommandPort;
        private IDemoGameplayOverrideCommandPort _demoGameplayOverrideCommandPort;
        private bool _isDisposed;
        private GameplayTerminalTransitionPort _terminalTransitionPort;
        private TerminalIrisMotionProfileResolver _terminalIrisMotionResolver;
        private GameplaySceneHost _installedSceneHost;
        private bool _terminalSessionSubscribed;
        private bool _entryIrisClosedPrepared;
        private SceneEntrySessionToken _failedEntryIrisSetupToken;
        private float _entryOpeningElapsed;
        private float _entryOpeningRadius;
        private Vector2 _entryFocusCenter;
        private TerminalIrisRuntimeOpenPreset? _entryOpenPreset;
        private SceneEntrySessionToken _preparedEntryToken;
        private TerminalTransitionPlayback _gameplayEntrySourceClosePlayback;
        private SceneEntrySessionToken _gameplayEntrySourceCloseToken;
        private bool _gameplayEntrySourceCloseRenderRequested;
        private TerminalTransitionPlayback _mainMenuReturnSourceClosePlayback;
        private MainMenuEntrySessionToken _mainMenuReturnSourceCloseToken;
        private bool _mainMenuReturnSourceCloseRenderRequested;

        public GameplayUiFlowPorts Ports { get; private set; }

        public GameplayUiCanvasRootView RootView => _rootView;

        public IGameplayUiPresentationSource PresentationSource { get; private set; }

        public ScreenController ScreenController { get; private set; }

        public PopupController PopupController { get; private set; }

        public HUDController HudController { get; private set; }

        public HUDRootPresenter HudRootPresenter { get; private set; }

        public UIBlockPolicy BlockPolicy { get; private set; }

        public UIFlowCoordinator Coordinator { get; private set; }

        public HUDRootView HudView => _rootView != null ? _rootView.HudView : null;

        public ScreenLayerView ScreenLayerView => _rootView != null ? _rootView.ScreenLayerView : null;

        public SettingsScreenView SettingsScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<SettingsScreenView>() : null;

        public StageResultScreenView StageResultScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<StageResultScreenView>() : null;

        public GameClearScreenView GameClearScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<GameClearScreenView>() : null;

        internal Vector2 EntryFocusCenterForTests => _entryFocusCenter;

        internal bool IsInstalledForDiagnostics => _isInstalled;

        internal bool TryBeginGameplayEntrySourceClose(
            SceneEntrySessionToken token,
            GameplayEntryTransitionVisualSnapshot visual,
            out TerminalTransitionPlayback playback)
        {
            playback = _gameplayEntrySourceClosePlayback;
            var session = SceneEntryPresentationRegistry.Current;
            if (!_isInstalled ||
                _rootView == null ||
                _installedSceneHost == null ||
                !session.IsActive ||
                session.Token != token ||
                session.TransitionIntent != visual.Intent ||
                session.Phase != SceneEntryPresentationPhase.PersistentCoverRequested ||
                visual.SourceCloseVisualKind != GameplayEntrySourceCloseVisualKind.RetryIris ||
                (_terminalTransitionPort?.CurrentPlayback is { IsTerminal: false }) ||
                (_gameplayEntrySourceClosePlayback != null &&
                 !_gameplayEntrySourceClosePlayback.IsTerminal))
            {
                return false;
            }

            _terminalIrisMotionResolver ??=
                _rootView.RequireTerminalIrisMotionProfile().CreateResolver();
            var preset = _terminalIrisMotionResolver.ResolveRetryClose(visual.Intent);
            var focus = new TerminalFocusTarget(
                preset.FallbackCenter,
                preset.FallbackRadius,
                isFallback: true);
            var focusSource = new GameplayTerminalFocusTargetSource(
                _installedSceneHost.ViewRegistry,
                _installedSceneHost.OutputCamera);
            if (focusSource.TryCapture(_installedSceneHost.PlayerEntityId, out var captured))
            {
                focus = captured;
            }

            var irisView = _rootView.TerminalIrisOverlayView;
            irisView.ConfigureTransitionColor(visual.SourceCloseColor);
            var candidate = new TerminalTransitionPlayback(preset);
            var fullyRevealedRadius = Mathf.Max(
                irisView.CalculateFullyRevealedRadius(preset.FallbackCenter, 0f),
                irisView.CalculateFullyRevealedRadius(focus.NormalizedCenter, 0f));
            candidate.ConfigureFullyRevealedRadii(
                fullyRevealedRadius,
                fullyRevealedRadius);
            var visualOnlyToken = new TerminalSessionToken(
                TerminalSessionRegistry.Authority.AuthorityGeneration,
                token.Value);
            if (!candidate.TryBegin(
                    new TerminalTransitionRequest(
                        TerminalTransitionKind.Defeat,
                        _installedSceneHost.PlayerEntityId,
                        visualOnlyToken,
                        TerminalTransitionDestinationMode.SceneHandoff),
                    focus))
            {
                candidate.Dispose();
                return false;
            }

            _gameplayEntrySourceClosePlayback?.Dispose();
            _gameplayEntrySourceClosePlayback = candidate;
            _gameplayEntrySourceCloseToken = token;
            _gameplayEntrySourceCloseRenderRequested = false;
            irisView.Show();
            irisView.Apply(candidate);
            playback = candidate;
            return true;
        }

        internal bool TickGameplayEntrySourceClose(
            SceneEntrySessionToken token,
            TerminalTransitionPlayback playback,
            float unscaledDeltaTime)
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (playback == null ||
                !ReferenceEquals(playback, _gameplayEntrySourceClosePlayback) ||
                token != _gameplayEntrySourceCloseToken ||
                !session.IsActive ||
                session.Token != token ||
                session.Phase != SceneEntryPresentationPhase.PersistentCoverRequested)
            {
                return false;
            }

            if (playback.State != TerminalTransitionState.Black)
            {
                playback.Advance(Mathf.Max(0f, unscaledDeltaTime));
                _rootView.TerminalIrisOverlayView.Apply(playback);
            }

            if (playback.State == TerminalTransitionState.Black &&
                !_gameplayEntrySourceCloseRenderRequested)
            {
                _rootView.TerminalIrisOverlayView.RequestClosedRenderAcknowledgement();
                _gameplayEntrySourceCloseRenderRequested = true;
            }

            return playback.State == TerminalTransitionState.Black &&
                   _rootView.TerminalIrisOverlayView.HasRenderedEntryClosedFrame;
        }

        internal bool CompleteGameplayEntrySourceClose(
            SceneEntrySessionToken token,
            TerminalTransitionPlayback playback)
        {
            if (!ReferenceEquals(playback, _gameplayEntrySourceClosePlayback) ||
                token != _gameplayEntrySourceCloseToken ||
                playback.State != TerminalTransitionState.Black ||
                !_rootView.TerminalIrisOverlayView.HasRenderedEntryClosedFrame)
            {
                return false;
            }

            _rootView.TerminalIrisOverlayView.Hide();
            playback.Dispose();
            _gameplayEntrySourceClosePlayback = null;
            _gameplayEntrySourceCloseToken = default;
            _gameplayEntrySourceCloseRenderRequested = false;
            return true;
        }

        internal bool TryBeginMainMenuReturnSourceClose(
            MainMenuEntrySessionToken token,
            MainMenuTransitionVisualSnapshot visual,
            out TerminalTransitionPlayback playback)
        {
            playback = _mainMenuReturnSourceClosePlayback;
            var session = MainMenuEntryPresentationRegistry.Current;
            if (!_isInstalled ||
                _rootView == null ||
                _installedSceneHost == null ||
                !session.IsActive ||
                session.Token != token ||
                session.TransitionIntent != SceneTransitionIntent.ReturnToMainMenu ||
                session.TransitionIntent != visual.Intent ||
                session.Phase != SceneEntryPresentationPhase.PersistentCoverRequested ||
                visual.SourceCloseVisualKind !=
                MainMenuSourceCloseVisualKind.GameplayScreenCenterIris ||
                (_terminalTransitionPort?.CurrentPlayback is { IsTerminal: false }) ||
                (_mainMenuReturnSourceClosePlayback != null &&
                 !_mainMenuReturnSourceClosePlayback.IsTerminal))
            {
                return false;
            }

            _terminalIrisMotionResolver ??=
                _rootView.RequireTerminalIrisMotionProfile().CreateResolver();
            var preset = _terminalIrisMotionResolver.ResolveGameplayEntrySourceClose(
                visual.Intent);
            var focus = new TerminalFocusTarget(
                preset.FallbackCenter,
                preset.FallbackRadius,
                isFallback: true);
            var irisView = _rootView.TerminalIrisOverlayView;
            irisView.ConfigureTransitionColor(visual.SourceCloseColor);
            var candidate = new TerminalTransitionPlayback(preset);
            var fullyRevealedRadius =
                irisView.CalculateFullyRevealedRadius(preset.FallbackCenter, 0f);
            candidate.ConfigureFullyRevealedRadii(
                fullyRevealedRadius,
                fullyRevealedRadius);
            var visualOnlyToken = new TerminalSessionToken(
                TerminalSessionRegistry.Authority.AuthorityGeneration,
                token.Value);
            if (!candidate.TryBegin(
                    new TerminalTransitionRequest(
                        TerminalTransitionKind.Defeat,
                        _installedSceneHost.PlayerEntityId,
                        visualOnlyToken,
                        TerminalTransitionDestinationMode.SceneHandoff),
                    focus))
            {
                candidate.Dispose();
                return false;
            }

            _mainMenuReturnSourceClosePlayback?.Dispose();
            _mainMenuReturnSourceClosePlayback = candidate;
            _mainMenuReturnSourceCloseToken = token;
            _mainMenuReturnSourceCloseRenderRequested = false;
            irisView.Show();
            irisView.Apply(candidate);
            playback = candidate;
            return true;
        }

        internal bool TickMainMenuReturnSourceClose(
            MainMenuEntrySessionToken token,
            TerminalTransitionPlayback playback,
            float unscaledDeltaTime)
        {
            var session = MainMenuEntryPresentationRegistry.Current;
            if (playback == null ||
                !ReferenceEquals(playback, _mainMenuReturnSourceClosePlayback) ||
                token != _mainMenuReturnSourceCloseToken ||
                !session.IsActive ||
                session.Token != token ||
                session.Phase != SceneEntryPresentationPhase.PersistentCoverRequested)
            {
                return false;
            }

            var irisView = _rootView.TerminalIrisOverlayView;
            if (playback.State != TerminalTransitionState.Black)
            {
                playback.Advance(Mathf.Max(0f, unscaledDeltaTime));
                irisView.Apply(playback);
            }

            if (playback.State == TerminalTransitionState.Black &&
                !_mainMenuReturnSourceCloseRenderRequested)
            {
                irisView.RequestClosedRenderAcknowledgement();
                _mainMenuReturnSourceCloseRenderRequested = true;
            }

            return playback.State == TerminalTransitionState.Black &&
                   irisView.HasRenderedEntryClosedFrame;
        }

        internal bool CompleteMainMenuReturnSourceClose(
            MainMenuEntrySessionToken token,
            TerminalTransitionPlayback playback)
        {
            if (!ReferenceEquals(playback, _mainMenuReturnSourceClosePlayback) ||
                token != _mainMenuReturnSourceCloseToken ||
                playback.State != TerminalTransitionState.Black ||
                !_rootView.TerminalIrisOverlayView.HasRenderedEntryClosedFrame)
            {
                return false;
            }

            _rootView.TerminalIrisOverlayView.Hide();
            playback.Dispose();
            _mainMenuReturnSourceClosePlayback = null;
            _mainMenuReturnSourceCloseToken = default;
            _mainMenuReturnSourceCloseRenderRequested = false;
            return true;
        }

        public LevelFailedScreenView LevelFailedScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<LevelFailedScreenView>() : null;

        public PopupLayerView PopupLayerView => _rootView != null ? _rootView.PopupLayerView : null;

        public PausePopupView PausePopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<PausePopupView>() : null;

        public ConfirmPopupView ConfirmPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<ConfirmPopupView>() : null;

        public bool TryCreateStageLaunchRouter(string currentSceneName, out IStageLaunchRouter router)
        {
            router = new CurrentSceneStageLaunchRouter(currentSceneName);
            return true;
        }

        public bool TryGetTerminalTransitionPort(out ITerminalTransitionPort port)
        {
            if (_terminalTransitionPort == null && _sceneHost != null)
            {
                EnsureTerminalTransitionPort(_sceneHost);
            }

            port = _terminalTransitionPort;
            return port != null;
        }

        public bool TryGetTerminalSessionAuthority(
            out ITerminalSessionReadModel readModel,
            out ITerminalSessionAuthority authority)
        {
            var persistentAuthority = TerminalSessionRegistry.Authority;
            readModel = persistentAuthority;
            authority = persistentAuthority;
            return true;
        }

        internal bool TryForceClearCurrentStageForDiagnostics(out string message)
        {
            if (_demoStageControlCommandPort == null)
            {
                message = "Production Demo Stage Control command port is unavailable.";
                return false;
            }

            var result = _demoStageControlCommandPort.ForceClearCurrentStage();
            message = result.Message;
            return result.Success;
        }

        private void Start()
        {
            if (_installOnStart && _sceneHost != null)
            {
                Install(_sceneHost);
            }
        }

        private void OnEnable()
        {
            SubscribeTerminalSession();
        }

        private void OnDisable()
        {
            UnsubscribeTerminalSession();
        }

        private void Update()
        {
            _terminalTransitionPort?.Tick(Time.unscaledDeltaTime);
            TickSceneEntryPresentation(Time.unscaledDeltaTime);
            TickVictoryResultHandoff(Time.unscaledDeltaTime);

            if (_isInstalled)
            {
                _stageResultAutoNextDriver?.Tick(Time.unscaledDeltaTime);
            }

            if (!_isInstalled || _rootView == null)
            {
                return;
            }

            if (_terminalTransitionPort?.CurrentPlayback is { IsTerminal: false })
            {
                return;
            }

            if (_keyboardBindingSettingsPort != null && _keyboardBindingSettingsPort.IsRebinding)
            {
                return;
            }

            if (WasDemoStageControlOpenKeyPressed() && TryToggleDemoStageControlPanel())
            {
                return;
            }
        }

        public void Install(GameplaySceneHost sceneHost)
        {
            if (sceneHost == null)
            {
                throw new ArgumentNullException(nameof(sceneHost));
            }

            ValidateComicSequenceOverlayPrefab();

            if (sceneHost.UiAccess == null)
            {
                throw new InvalidOperationException("GameplaySceneHost must be initialized before installing UI flow.");
            }

            CaptureInstallAttemptLoadingSceneEntryOwnerIfApplicable(
                out var expectedLoadingSceneEntryOwner,
                out var expectedSceneEntryDestinationGeneration);
            var expectedSceneEntryToken = expectedLoadingSceneEntryOwner.Token;
            try
            {
                EnsureTerminalTransitionPort(sceneHost);
                _installedSceneHost = sceneHost;
                _campaignStageSequenceResolver =
                    sceneHost.UiAccess.CampaignStageSequenceResolver;
                _gameplayWorldGuidePresenter = sceneHost.GetComponent<GameplayWorldGuidePresenter>();
                RegisterSceneEntryDestinationIfApplicable();
                _demoGameplayOverrideCommandPort = sceneHost.UiAccess.DemoGameplayOverrideCommandPort;
                _demoStageControlCommandPort = CreateDemoStageControlCommandPort(sceneHost);
                var presentationSource = new GameplayUiPresentationSource(
                    sceneHost.UiAccess.QueryFacade,
                    sceneHost.UiAccess.PresentationFeed,
                    sceneHost.UiAccess.PauseService);
                Install(new GameplayUiFlowPorts(
                    sceneHost.UiAccess.CommandGateway,
                    sceneHost.UiAccess.QueryFacade,
                    presentationSource,
                    sceneHost.UiAccess.PauseService,
                    CreatePauseProgressionReadSource(
                        gameObject,
                        presentationSource,
                        _campaignStageSequenceResolver)));

                SignalTerminalDestinationReadyIfApplicable(sceneHost);
                _sceneHost = null;
            }
            catch (Exception exception)
            {
                try
                {
                    ReportCapturedLoadingSceneEntryFailureIfOwned(
                        expectedLoadingSceneEntryOwner,
                        expectedSceneEntryDestinationGeneration,
                        "DESTINATION_ENTRY_INSTALL_FAILED",
                        exception.Message);
                    ReportSceneEntryFailureIfOwned(
                        expectedSceneEntryToken,
                        expectedSceneEntryDestinationGeneration,
                        "DESTINATION_ENTRY_INSTALL_FAILED",
                        exception.Message);
                }
                catch (Exception reportException)
                {
                    AttachSecondaryException(
                        exception,
                        "SceneEntryFailureReportingFailure",
                        reportException);
                }

                try
                {
                    ReportDestinationFailureIfOwned(
                        DestinationReadinessOutcome.Failed,
                        "DESTINATION_INSTALL_FAILED",
                        exception.Message);
                }
                catch (Exception reportException)
                {
                    AttachSecondaryException(
                        exception,
                        "TerminalDestinationFailureReportingFailure",
                        reportException);
                }

                throw;
            }
        }
        private void SignalTerminalDestinationReadyIfApplicable(GameplaySceneHost sceneHost)
        {
            var session = TerminalSessionRegistry.Current;
            if (!session.IsActive ||
                session.TerminalKind != TerminalTransitionKind.Defeat ||
                session.Phase != TerminalSessionPhase.WaitingDestinationReady)
            {
                return;
            }

            if (sceneHost.UiAccess == null ||
                _rootView == null ||
                !_rootView.gameObject.activeInHierarchy ||
                Coordinator == null ||
                ScreenController == null ||
                PopupController == null ||
                HudController == null)
            {
                throw new InvalidOperationException(
                    $"Terminal destination {session.Token} cannot become ready before gameplay host, UI roots, and controllers are installed.");
            }

            var provenance = session.DestinationKind switch
            {
                TerminalDestinationKind.ReloadedGameplay =>
                    TerminalDestinationProvenance.ReloadedGameplayBootstrap,
                TerminalDestinationKind.MainMenu =>
                    TerminalDestinationProvenance.MainMenuBootstrap,
                _ => TerminalDestinationProvenance.None,
            };
            var signal = new DestinationReadinessSignal(
                session.Token,
                session.TransitionId,
                session.SourceSceneGeneration,
                session.DestinationSceneGeneration,
                session.DestinationKind,
                TerminalSessionPhase.WaitingDestinationReady,
                provenance,
                DestinationReadinessOutcome.Ready);
            if (!TerminalDestinationReadiness.Signal(signal))
            {
                throw new InvalidOperationException(
                    $"Terminal destination readiness for token {session.Token} was rejected.");
            }
        }

        public void Install(GameplayUiFlowPorts ports)
        {
            if (_isInstalled)
            {
                return;
            }

            ValidateComicSequenceOverlayPrefab();

            Ports = ports;
            EnsureRootView();
            EnsureScreenPrefabCatalog();
            EnsurePopupPrefabCatalog();
            PresentationSource = Ports.PresentationSource;
            var audioSettingsPort = UiSettingsBridgeAssembly.CreateAudioSettingsPort(gameObject, MissingAudioInstallerMessage);
            var displaySettingsPort = UiSettingsBridgeAssembly.CreateDisplaySettingsPort(gameObject, MissingDisplayInstallerMessage);
            _keyboardBindingSettingsPort = CreateKeyboardBindingSettingsPort();
            _uiAudioPort = UiSettingsBridgeAssembly.CreateUiAudioPort(
                gameObject,
                _uiAudioCueMap,
                MissingAudioInstallerMessage,
                MissingUiAudioCueMapMessage);
            var uiAudioPort = _uiAudioPort;
            var audioPauseService = CreateAudioPlaybackPauseService();
            if (UnityEngine.Application.isPlaying)
            {
                SceneTransitionCoordinator.BindUiAudioPortForCurrentScene(uiAudioPort);
            }

            _audioSettingsLifecycleRelay = UiSettingsBridgeAssembly.EnsureAudioSettingsLifecycleRelay(gameObject, audioSettingsPort);
            EnsureDisplayPreviewTimeoutRelay();
            EnsureDisplaySettingsLifecycleRelay();
            _localizedTextResolver = UiSettingsBridgeAssembly.CreatePersistentSettingsLocalizedTextResolver();
            if (UnityEngine.Application.isPlaying)
            {
                SceneTransitionCoordinator.BindLocalizedTextResolverForCurrentScene(
                    _localizedTextResolver);
            }
            _gameplayHudLocalizationBinding =
                _rootView.HudView.GetComponent<GameplayHudLocalizationBinding>();
            if (_gameplayHudLocalizationBinding == null)
            {
                throw new InvalidOperationException(
                    "GameplayHudRoot is missing GameplayHudLocalizationBinding.");
            }

            _gameplayHudLocalizationBinding.Initialize(_localizedTextResolver);
            if (_gameplayWorldGuidePresenter != null)
            {
                _gameplayWorldGuideLocalizationController =
                    new GameplayWorldGuideLocalizationController(
                        _gameplayWorldGuidePresenter,
                        _localizedTextResolver,
                        _gameplayHudLocalizationBinding.Theme);
            }

            PopupController = new PopupController(new GameplayPopupRuntimeFactory(
                _rootView.PopupLayerView,
                _popupPrefabCatalog,
                _demoStageControlCommandPort,
                _demoGameplayOverrideCommandPort,
                localizedTextResolver: _localizedTextResolver));
            _gameplayPauseAudioBridge = new GameplayPauseAudioBridge(
                Ports.GameplayPauseService,
                audioPauseService,
                PopupController);
            var displayPreviewSessionHost = new DisplayPreviewSessionHost(
                PopupController,
                _displayPreviewTimeoutRelay);

            var stageInfoPresenter = new StageInfoPresenter(_localizedTextResolver);
            var objectiveHudPresenter = new ObjectiveHudPresenter(_localizedTextResolver);
            var objectiveTypographyBinding =
                _rootView.HudView.ObjectiveHudView.GetComponent<ObjectiveHudTypographyBinding>();
            if (objectiveTypographyBinding == null)
            {
                throw new InvalidOperationException(
                    "GameplayHudRoot ObjectiveHudView is missing ObjectiveHudTypographyBinding.");
            }

            objectiveTypographyBinding.Initialize(_localizedTextResolver);
            _rootView.HudView.ObjectiveHudView.ConfigureTypography(objectiveTypographyBinding);
            var chancePanelPresenter = new ChancePanelPresenter();
            var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
            HudRootPresenter = new HUDRootPresenter(
                PresentationSource,
                stageInfoPresenter,
                objectiveHudPresenter,
                chancePanelPresenter,
                surfaceBeltIndicatorPresenter);

            ScreenController = new ScreenController(new GameplayScreenRuntimeFactory(
                screenLayerView: _rootView.ScreenLayerView,
                queryFacade: Ports.QueryFacade,
                presentationSource: PresentationSource,
                audioSettingsPort: audioSettingsPort,
                displaySettingsPort: displaySettingsPort,
                keyboardBindingSettingsPort: _keyboardBindingSettingsPort,
                uiAudioPort: uiAudioPort,
                displayPreviewSessionHost: displayPreviewSessionHost,
                displaySettingsLifecycleRelay: _displaySettingsLifecycleRelay,
                screenPrefabCatalog: _screenPrefabCatalog,
                localizedTextResolver: _localizedTextResolver));
            HudController = new HUDController(
                HudRootPresenter.ViewModel,
                stageInfoPresenter.ViewModel,
                objectiveHudPresenter.ViewModel,
                chancePanelPresenter.ViewModel,
                surfaceBeltIndicatorPresenter.ViewModel);
            _hudUiAudioFeedbackController = new HudUiAudioFeedbackController(
                uiAudioPort,
                chancePanelPresenter.ViewModel,
                objectiveHudPresenter.ViewModel);
            BlockPolicy = new UIBlockPolicy();
            Coordinator = new UIFlowCoordinator(
                ScreenController,
                PopupController,
                BlockPolicy,
                Ports.PauseService,
                PresentationSource,
                uiAudioPort,
                new CurrentSceneStageLaunchRouter(gameObject.scene.name),
                CreateMainMenuReturnRouter(),
                Ports.PauseProgressionReadSource,
                _campaignStageSequenceResolver);
            _stageResultAutoNextDriver = new StageResultAutoNextDriver(
                ScreenController,
                PopupController,
                Coordinator.TryLaunchStage);
            displayPreviewSessionHost.BindAudioIntentBoundary(Coordinator);

            HudController.AttachView(_rootView.HudView);
            WireViewEvents();
            WireControllerEvents();
            Coordinator.Initialize();
            SubscribeTerminalSession();
            EnsureNavigationInputRouter();
            SyncViews();
            _isInstalled = true;
        }

        private void OnDestroy()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _gameplayWorldGuideLocalizationController?.Dispose();
            _gameplayWorldGuideLocalizationController = null;
            _gameplayHudLocalizationBinding?.Dispose();
            _gameplayHudLocalizationBinding = null;
            // Dispose the persistent HUD presenter before any view/controller teardown can
            // encounter a partially destroyed hidden HUD hierarchy.
            HudRootPresenter?.Dispose();
            _mainMenuReturnSourceClosePlayback?.Dispose();
            _mainMenuReturnSourceClosePlayback = null;
            ReportSceneEntryFailureIfOwned(
                "DESTINATION_ENTRY_INSTALLER_DESTROYED",
                "Gameplay destination UI installer was destroyed before Entry Iris opening completed.");
            ReportDestinationFailureIfOwned(
                DestinationReadinessOutcome.Cancelled,
                "DESTINATION_INSTALLER_DESTROYED",
                "Destination UI installer was destroyed before readiness completed.");
            UnwireViewEvents();
            UnwireControllerEvents();
            _audioSettingsLifecycleRelay?.FlushNow();
            UnsubscribeTerminalSession();
            Coordinator?.Dispose();
            _stageResultAutoNextDriver?.Dispose();
            _gameplayPauseAudioBridge?.Dispose();
            ScreenController?.Dispose();
            PopupController?.Dispose();
            HudController?.Dispose();
            _hudUiAudioFeedbackController?.Dispose();
            _terminalTransitionPort?.Dispose();
            _terminalTransitionPort = null;
            _gameplayEntrySourceClosePlayback?.Dispose();
            _gameplayEntrySourceClosePlayback = null;
            _installedSceneHost = null;
            (PresentationSource as IDisposable)?.Dispose();
            if (UnityEngine.Application.isPlaying)
            {
                SceneTransitionCoordinator.UnbindLocalizedTextResolverForCurrentScene(
                    _localizedTextResolver);
            }
            (_localizedTextResolver as IDisposable)?.Dispose();
            _localizedTextResolver = null;
        }

        private static void ReportDestinationFailureIfOwned(
            DestinationReadinessOutcome outcome,
            string code,
            string message)
        {
            var authority = TerminalSessionRegistry.Authority;
            var session = authority.Current;
            if (!session.IsActive ||
                session.TerminalKind != TerminalTransitionKind.Defeat ||
                session.DestinationKind != TerminalDestinationKind.ReloadedGameplay ||
                session.DestinationSceneGeneration <= 0 ||
                authority.CurrentSceneGeneration != session.DestinationSceneGeneration ||
                session.Phase == TerminalSessionPhase.Revealing ||
                session.Phase == TerminalSessionPhase.Completed ||
                session.Phase == TerminalSessionPhase.FailedHoldingCover)
            {
                return;
            }

            var failure = new TerminalFailure(code, message);
            if (session.Phase == TerminalSessionPhase.WaitingDestinationReady)
            {
                TerminalDestinationReadiness.Signal(new DestinationReadinessSignal(
                    session.Token,
                    session.TransitionId,
                    session.SourceSceneGeneration,
                    session.DestinationSceneGeneration,
                    session.DestinationKind,
                    TerminalSessionPhase.WaitingDestinationReady,
                    TerminalDestinationProvenance.ReloadedGameplayBootstrap,
                    outcome,
                    failure.Message));
                return;
            }

            authority.TryFail(session.Token, failure);
        }

        private void EnsureRootView()
        {
            if (_rootView == null)
            {
                var rootShellPrefab = Resources.Load<GameObject>(RootShellPrefabResourcePath);
                if (rootShellPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Canonical UI root shell prefab was not found at Resources path '{RootShellPrefabResourcePath}'.");
                }

                var rootShellInstance = Instantiate(rootShellPrefab, transform, false);
                rootShellInstance.name = RootShellObjectName;
                _rootView = rootShellInstance.GetComponent<GameplayUiCanvasRootView>();
                if (_rootView == null)
                {
                    throw new InvalidOperationException("Canonical UI root shell prefab is missing GameplayUiCanvasRootView.");
                }
            }

            _rootView.EnsureHierarchy();
            EnsureHudView();
        }

        private void EnsureTerminalTransitionPort(GameplaySceneHost sceneHost)
        {
            if (_terminalTransitionPort != null)
            {
                return;
            }

            if (sceneHost == null)
            {
                throw new ArgumentNullException(nameof(sceneHost));
            }

            EnsureRootView();
            _terminalIrisMotionResolver ??=
                _rootView.RequireTerminalIrisMotionProfile().CreateResolver();
            _terminalTransitionPort = new GameplayTerminalTransitionPort(
                _rootView.TerminalIrisOverlayView,
                _terminalIrisMotionResolver,
                new GameplayTerminalFocusTargetSource(
                    sceneHost.ViewRegistry,
                    sceneHost.OutputCamera));
        }

        private void EnsureHudView()
        {
            if (_rootView.HudView != null)
            {
                return;
            }

            if (_hudPrefab == null)
            {
                throw new InvalidOperationException(
                    "GameplayUiFlowInstaller is missing the canonical HUD prefab reference. " +
                    "Assign the phase-local HUD prefab instead of reintroducing a runtime HUD builder.");
            }

            var hudView = Instantiate(_hudPrefab, _rootView.HudLayer, false);
            _rootView.AttachHudView(hudView);
        }

        private void EnsurePopupPrefabCatalog()
        {
            if (_popupPrefabCatalog != null)
            {
                return;
            }

            throw new InvalidOperationException(
                "GameplayUiFlowInstaller is missing the canonical popup prefab catalog reference. " +
                "Assign the popup-only prefab catalog instead of reintroducing runtime popup builders " +
                "or widening composition into a generic asset registry.");
        }

        private void EnsureScreenPrefabCatalog()
        {
            if (_screenPrefabCatalog != null)
            {
                return;
            }

            throw new InvalidOperationException(
                "GameplayUiFlowInstaller is missing the canonical screen prefab catalog reference. " +
                "Assign the screen-only prefab catalog instead of reintroducing runtime screen builders " +
                "or widening composition into a generic asset registry.");
        }

        private IAudioPlaybackPauseService CreateAudioPlaybackPauseService()
        {
            var audioRuntimeInstaller = GetComponent<AudioRuntimeInstaller>();
            if (audioRuntimeInstaller == null)
            {
                throw new InvalidOperationException(MissingAudioInstallerMessage);
            }

            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioPlaybackPauseService == null)
            {
                throw new InvalidOperationException(MissingAudioInstallerMessage);
            }

            return audioRuntimeInstaller.AudioPlaybackPauseService;
        }

        private IKeyboardBindingSettingsPort CreateKeyboardBindingSettingsPort()
        {
            var actions = _sceneHost != null && _sceneHost.InputHost != null
                ? _sceneHost.InputHost.Actions
                : _inputActions;
            if (actions == null)
            {
                return NoOpKeyboardBindingSettingsPort.Instance;
            }

            return new KeyboardBindingSettingsPortAdapter(new KeyboardBindingSettingsService(actions));
        }

        private IMainMenuReturnRouter CreateMainMenuReturnRouter()
        {
            IMainMenuReturnRouter inner = _routeConfig != null
                ? new ConfiguredMainMenuReturnRouter(_routeConfig)
                : NoOpMainMenuReturnRouter.Instance;
            var saveSlotStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            return new ComicOutroMainMenuReturnRouter(
                inner,
                saveSlotStore,
                saveSlotStore,
                CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(saveSlotStore),
                EnsureComicSequenceFlowCoordinator(),
                () => ScreenController != null && ScreenController.CurrentScreenId == ScreenId.GameClear);
        }

        private ComicSequenceFlowCoordinator EnsureComicSequenceFlowCoordinator()
        {
            if (_comicSequenceFlowCoordinator != null)
            {
                return _comicSequenceFlowCoordinator;
            }

            ValidateComicSequenceOverlayPrefab();
            var parent = _rootView != null ? _rootView.transform : transform;
            var overlay = Instantiate(_comicSequenceOverlayPrefab, parent, false);
            try
            {
                overlay.gameObject.SetActive(false);
                overlay.EnsureHierarchy();
                overlay.Initialize(ResolveUiInputActions());
            }
            catch
            {
                DestroyFailedComicSequenceOverlay(overlay);
                throw;
            }

            var audioFocus = GetComponent<ComicSequenceAudioFocusController>();
            if (audioFocus == null)
            {
                audioFocus = gameObject.AddComponent<ComicSequenceAudioFocusController>();
            }

            _comicSequenceFlowCoordinator = new ComicSequenceFlowCoordinator(
                null,
                _outroComicSequence,
                overlay,
                audioFocus);
            return _comicSequenceFlowCoordinator;
        }

        private void ValidateComicSequenceOverlayPrefab()
        {
            if (_comicSequenceOverlayPrefab == null)
            {
                throw new InvalidOperationException(MissingComicSequenceOverlayPrefabMessage);
            }

            _comicSequenceOverlayPrefab.EnsureHierarchy();
        }

        private static void DestroyFailedComicSequenceOverlay(
            ComicSequenceOverlayView overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(overlay.gameObject);
            }
            else
            {
                DestroyImmediate(overlay.gameObject);
            }
        }

        private void EnsureDisplayPreviewTimeoutRelay()
        {
            _displayPreviewTimeoutRelay = GetComponent<DisplayPreviewTimeoutRelay>();
            if (_displayPreviewTimeoutRelay == null)
            {
                _displayPreviewTimeoutRelay = gameObject.AddComponent<DisplayPreviewTimeoutRelay>();
            }
        }

        private void EnsureDisplaySettingsLifecycleRelay()
        {
            _displaySettingsLifecycleRelay = GetComponent<DisplaySettingsLifecycleRelay>();
            if (_displaySettingsLifecycleRelay == null)
            {
                _displaySettingsLifecycleRelay = gameObject.AddComponent<DisplaySettingsLifecycleRelay>();
            }
        }

        private void EnsureNavigationInputRouter()
        {
            _navigationInputRouter = GetComponent<UiNavigationInputRouter>();
            if (_navigationInputRouter == null)
            {
                _navigationInputRouter = gameObject.AddComponent<UiNavigationInputRouter>();
            }

            var resolver = new UiLayeredNavigationTargetResolver(
                PopupController,
                ScreenController,
                modalOverlayProvider: null,
                hudProvider: _rootView != null
                    ? _rootView.HudView as Game.Feature.UI.ViewShared.IUiNavigationTargetProvider
                    : null,
                isHudFocusEnabled: () => false);
            _navigationInputRouter.Initialize(
                ResolveUiInputActions(),
                resolver,
                () => Coordinator != null && Coordinator.HandleBackRequested(),
                () => (_comicSequenceFlowCoordinator != null && _comicSequenceFlowCoordinator.IsPresenting) ||
                      TerminalSessionRegistry.IsActive ||
                      SceneEntryPresentationRegistry.IsActive ||
                      MainMenuEntryPresentationRegistry.IsActive,
                _uiAudioPort);
        }

        private InputActionAsset ResolveUiInputActions()
        {
            return _sceneHost != null && _sceneHost.InputHost != null
                ? _sceneHost.InputHost.Actions
                : _inputActions;
        }

        private void WireViewEvents()
        {
            _rootView.HudView.PauseRequested += HandlePauseRequested;
            _rootView.PopupLayerView.BackdropClicked += HandlePopupBackdropClicked;
        }

        private void WireControllerEvents()
        {
            ScreenController.StateChanged += SyncViews;
            PopupController.StateChanged += SyncViews;
        }

        private void UnwireViewEvents()
        {
            if (_rootView == null)
            {
                return;
            }

            if (_rootView.HudView != null)
            {
                _rootView.HudView.PauseRequested -= HandlePauseRequested;
            }

            if (_rootView.PopupLayerView != null)
            {
                _rootView.PopupLayerView.BackdropClicked -= HandlePopupBackdropClicked;
            }
        }

        private void UnwireControllerEvents()
        {
            if (ScreenController != null)
            {
                ScreenController.StateChanged -= SyncViews;
            }

            if (PopupController != null)
            {
                PopupController.StateChanged -= SyncViews;
            }
        }

        private void HandlePauseRequested()
        {
            Coordinator.RequestPausePopup();
        }

        private void HandlePopupBackdropClicked()
        {
            Coordinator.HandlePopupBackdropClicked();
        }

        private void HandleTerminalSessionChanged(TerminalSessionSnapshot snapshot)
        {
            if (snapshot.IsActive &&
                snapshot.Phase == TerminalSessionPhase.WaitingDestinationReady &&
                _installedSceneHost != null)
            {
                SignalTerminalDestinationReadyIfApplicable(_installedSceneHost);
            }

            if (snapshot.IsActive)
            {
                var dropdowns = FindObjectsByType<TMP_Dropdown>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (var i = 0; i < dropdowns.Length; i++)
                {
                    ForceCloseTransientDropdown(dropdowns[i]);
                }

                _navigationInputRouter?.ClearNavigationFocus();
                SyncViews();
                var eventSystems = FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (var i = 0; i < eventSystems.Length; i++)
                {
                    eventSystems[i]?.SetSelectedGameObject(null);
                }

                return;
            }

            SyncViews();
            _navigationInputRouter?.ClearNavigationFocus();
        }

        private void TickVictoryResultHandoff(float unscaledDeltaTime)
        {
            var session = TerminalSessionRegistry.Current;
            if (!_isInstalled ||
                !session.IsActive ||
                session.TerminalKind != TerminalTransitionKind.Victory)
            {
                return;
            }

            var resultView = ResolveActiveResultTransitionView(session.DestinationKind);
            if (resultView == null)
            {
                return;
            }

            if (session.Phase == TerminalSessionPhase.WaitingSameSceneDestination)
            {
                if (!TerminalDestinationReadiness.IsReady(session.Token) ||
                    !resultView.IsHandoffCoverRendered)
                {
                    return;
                }

                TerminalRuntimeTrace.Record(session, TerminalTraceEvent.ResultBackdropReady);
                if (_terminalTransitionPort == null ||
                    !_terminalTransitionPort.CompleteToResultBackdrop(session.Token))
                {
                    throw new InvalidOperationException(
                        $"Victory result backdrop handoff rejected terminal token {session.Token}.");
                }

                if (!resultView.BeginHandoffFade())
                {
                    throw new InvalidOperationException(
                        $"Victory ResultHandoffCover fade rejected terminal token {session.Token}.");
                }

                if (!TerminalSessionRegistry.TryAdvance(
                        session.Token,
                        TerminalSessionPhase.WaitingResultInteraction))
                {
                    throw new InvalidOperationException(
                        $"Victory result interaction wait rejected terminal token {session.Token}.");
                }

                return;
            }

            if (session.Phase != TerminalSessionPhase.WaitingResultInteraction)
            {
                return;
            }

            resultView.AdvanceResultTransition(Mathf.Max(0f, unscaledDeltaTime));
            if (resultView.CanBeginContentEntrance)
            {
                if (!resultView.BeginContentEntrance())
                {
                    throw new InvalidOperationException(
                        $"Victory result content entrance rejected terminal token {session.Token}.");
                }

                TerminalRuntimeTrace.Record(
                    TerminalSessionRegistry.Current,
                    TerminalTraceEvent.ResultContentEntranceStarted);
                if (Coordinator == null ||
                    !Coordinator.NotifyResultContentEntranceStarted(
                        new ResultContentEntranceMilestone(
                            session.Token,
                            session.DestinationKind)))
                {
                    throw new InvalidOperationException(
                        $"Victory result content entrance audio milestone rejected terminal token {session.Token}.");
                }
            }

            if (!resultView.IsInteractionReady)
            {
                return;
            }

            TerminalRuntimeTrace.Record(
                TerminalSessionRegistry.Current,
                TerminalTraceEvent.ResultInteractionReady);
            if (!TerminalSessionRegistry.TryComplete(session.Token))
            {
                throw new InvalidOperationException(
                    $"Victory result interaction-ready completion rejected terminal token {session.Token}.");
            }
        }

        private void RegisterSceneEntryDestinationIfApplicable()
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (!session.IsActive || session.Phase != SceneEntryPresentationPhase.Loading)
            {
                return;
            }

            var destinationGeneration =
                TerminalSessionRegistry.Authority.CurrentSceneGeneration;
            if (!SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                    session.Token,
                    destinationGeneration))
            {
                SceneEntryPresentationRegistry.TryFailHoldingCover(
                    session.Token,
                    "Destination gameplay scene generation could not be correlated.");
                throw new InvalidOperationException(
                    $"Scene entry session {session.Token} rejected destination generation {destinationGeneration}.");
            }
        }

        private void TickSceneEntryPresentation(float unscaledDeltaTime)
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (!session.IsActive ||
                !_isInstalled ||
                _rootView == null ||
                _installedSceneHost == null)
            {
                return;
            }

            var irisView = _rootView.TerminalIrisOverlayView;
            if (session.Phase == SceneEntryPresentationPhase.WaitingRuntimeReady &&
                !_entryIrisClosedPrepared)
            {
                if (_failedEntryIrisSetupToken == session.Token)
                {
                    return;
                }

                if (!IsStrongGameplayDestinationReady(
                        session,
                        out var focus,
                        out var readinessFailure))
                {
                    SceneEntryPresentationRegistry.TryFailHoldingCover(
                        session.Token,
                        readinessFailure);
                    return;
                }

                var ready = new SceneEntryRuntimeReady(
                    session.Token,
                    session.TransitionId,
                    session.DestinationSceneGeneration,
                    _installedSceneHost.PlayerEntityId,
                    _installedSceneHost.OutputCamera,
                    SceneEntryRuntimeReadyProvenance.ProductionGameplayBootstrap);
                if (!SceneEntryPresentationRegistry.CanAcceptRuntimeReady(ready))
                {
                    SceneEntryPresentationRegistry.TryFailHoldingCover(
                        session.Token,
                        "SceneEntryRuntimeReady token, transition, generation, player, camera, or provenance was invalid.");
                    return;
                }

                PrepareGameplayDestinationIris(session, focus, irisView);
                return;
            }

            if (session.Phase == SceneEntryPresentationPhase.WaitingRuntimeReady &&
                _entryIrisClosedPrepared)
            {
                if (_preparedEntryToken != session.Token)
                {
                    return;
                }

                if (!irisView.HasRenderedEntryClosedFrame)
                {
                    return;
                }

                if (!SceneEntryPresentationRegistry.TryAdvance(
                        session.Token,
                        SceneEntryPresentationPhase.EntryIrisClosed) ||
                    !SceneTransitionCoordinator.ReleaseSceneEntryCover(session.Token) ||
                    !SceneEntryPresentationRegistry.TryAdvance(
                        session.Token,
                        SceneEntryPresentationPhase.Opening))
                {
                    SceneEntryPresentationRegistry.TryFailHoldingCover(
                        session.Token,
                        "Closed Entry Iris could not acquire the persistent-cover handoff.");
                }

                return;
            }

            if (session.Phase != SceneEntryPresentationPhase.Opening)
            {
                return;
            }

            if (!_entryOpenPreset.HasValue || _preparedEntryToken != session.Token)
            {
                throw new InvalidOperationException(
                    $"Scene entry opening token {session.Token} has no captured motion preset.");
            }

            var openPreset = _entryOpenPreset.Value;
            _entryOpeningElapsed += Mathf.Max(0f, unscaledDeltaTime);
            var openingElapsed = Mathf.Max(
                0f,
                _entryOpeningElapsed - openPreset.PreOpenHoldDuration);
            var progress = Mathf.Clamp01(openingElapsed / openPreset.OpeningDuration);
            var openingEasing = TerminalIrisEasingUtility.Evaluate(
                openPreset.OpeningEasing,
                progress);
            irisView.ApplyEntryRadius(
                Mathf.Lerp(
                    0f,
                    _entryOpeningRadius,
                    openingEasing),
                Mathf.Lerp(
                    openPreset.FinalClosedOvershootPixels,
                    0f,
                    openingEasing));
            if (progress < 1f)
            {
                return;
            }

            irisView.Hide();
            _entryIrisClosedPrepared = false;
            _failedEntryIrisSetupToken = default;
            _entryOpenPreset = null;
            _preparedEntryToken = default;
            if (!SceneEntryPresentationRegistry.TryComplete(session.Token))
            {
                throw new InvalidOperationException(
                    $"Scene entry opening completion rejected token {session.Token}.");
            }

            GameplayEntryTransitionVisualSnapshotRegistry.Clear(session.Token);
        }

        private void PrepareGameplayDestinationIris(
            SceneEntryPresentationSnapshot session,
            TerminalFocusTarget focus,
            TerminalIrisOverlayView irisView)
        {
            var expectedToken = session.Token;
            var expectedDestinationGeneration =
                session.DestinationSceneGeneration;
            var expectedTerminalDestinationOwner =
                TerminalSessionRegistry.Authority.Current;
            try
            {
                _terminalIrisMotionResolver ??=
                    _rootView.RequireTerminalIrisMotionProfile().CreateResolver();
                var entryOpenPreset =
                    _terminalIrisMotionResolver.ResolveStageEntryOpen();
                var visual = GameplayEntryTransitionVisualSnapshotRegistry.Require(
                    expectedToken,
                    session.TransitionIntent);
                irisView.ConfigureTransitionColor(visual.DestinationOpenColor);
                irisView.Show();
                irisView.ApplyClosedEntry(
                    focus.NormalizedCenter,
                    entryOpenPreset);
                var openingRadius = irisView.CalculateFullyRevealedRadius(
                    focus.NormalizedCenter,
                    entryOpenPreset.FullOpenMargin);

                _entryFocusCenter = focus.NormalizedCenter;
                _entryOpeningRadius = openingRadius;
                _entryOpeningElapsed = 0f;
                _entryOpenPreset = entryOpenPreset;
                _preparedEntryToken = expectedToken;
                _failedEntryIrisSetupToken = default;
                _entryIrisClosedPrepared = true;
            }
            catch (Exception setupException)
            {
                AbortGameplayDestinationIrisPreparation(
                    expectedToken,
                    expectedDestinationGeneration,
                    expectedTerminalDestinationOwner,
                    irisView,
                    setupException);
                throw;
            }
        }

        private void AbortGameplayDestinationIrisPreparation(
            SceneEntrySessionToken expectedToken,
            long expectedDestinationGeneration,
            TerminalSessionSnapshot expectedTerminalDestinationOwner,
            TerminalIrisOverlayView irisView,
            Exception primaryException)
        {
            _entryIrisClosedPrepared = false;
            _failedEntryIrisSetupToken = expectedToken;
            _entryOpenPreset = null;
            _preparedEntryToken = default;
            _entryOpeningElapsed = 0f;
            _entryOpeningRadius = 0f;
            _entryFocusCenter = default;
            try
            {
                irisView?.Hide();
            }
            catch (Exception cleanupException)
            {
                AttachSecondaryException(
                    primaryException,
                    "DestinationEntryIrisCleanupFailure",
                    cleanupException);
            }

            try
            {
                ReportSceneEntryIrisPreparationFailureIfOwned(
                    expectedToken,
                    expectedDestinationGeneration,
                    "DESTINATION_ENTRY_IRIS_PREPARATION_FAILED",
                    primaryException.Message);
            }
            catch (Exception reportException)
            {
                AttachSecondaryException(
                    primaryException,
                    "SceneEntryIrisFailureReportingFailure",
                    reportException);
            }

            try
            {
                ReportTerminalDestinationIrisPreparationFailureIfOwned(
                    expectedTerminalDestinationOwner,
                    "DESTINATION_IRIS_PREPARATION_FAILED",
                    primaryException.Message);
            }
            catch (Exception reportException)
            {
                AttachSecondaryException(
                    primaryException,
                    "TerminalDestinationIrisFailureReportingFailure",
                    reportException);
            }
        }

        private bool IsStrongGameplayDestinationReady(
            SceneEntryPresentationSnapshot session,
            out TerminalFocusTarget focus,
            out string failureReason)
        {
            focus = default;
            if (!_installedSceneHost.HasStrongGameplayEntryRuntime)
            {
                failureReason =
                    "Strong gameplay readiness requires the built gameplay runtime, input host, view registry, and player entity.";
                return false;
            }

            var outputCamera = _installedSceneHost.OutputCamera;
            if (outputCamera == null ||
                !outputCamera.isActiveAndEnabled ||
                !outputCamera.gameObject.activeInHierarchy)
            {
                failureReason =
                    "Strong gameplay readiness requires an active output camera.";
                return false;
            }

            if (!_rootView.gameObject.activeInHierarchy ||
                _rootView.TerminalIrisOverlayView == null ||
                Coordinator == null ||
                ScreenController == null ||
                PopupController == null ||
                HudController == null)
            {
                failureReason =
                    "Strong gameplay readiness requires installed UI flow and an Entry Iris render surface.";
                return false;
            }

            var focusSource = new GameplayTerminalFocusTargetSource(
                _installedSceneHost.ViewRegistry,
                outputCamera);
            if (!focusSource.TryCapture(
                    _installedSceneHost.PlayerEntityId,
                    out focus) ||
                focus.IsFallback)
            {
                failureReason =
                    $"Strong gameplay readiness player projection failed: {focusSource.LastCaptureDiagnostics.FailureReason}.";
                return false;
            }

            if (session.DestinationSceneGeneration !=
                TerminalSessionRegistry.Authority.CurrentSceneGeneration)
            {
                failureReason =
                    "Strong gameplay readiness rejected a stale destination scene generation.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static void ReportSceneEntryFailureIfOwned(
            string code,
            string message)
        {
            var session = SceneEntryPresentationRegistry.Current;
            ReportSceneEntryFailureIfOwned(
                session.Token,
                session.DestinationSceneGeneration,
                code,
                message);
        }

        private static void CaptureInstallAttemptLoadingSceneEntryOwnerIfApplicable(
            out SceneEntryPresentationSnapshot expectedOwner,
            out long expectedDestinationGeneration)
        {
            expectedOwner = default;
            expectedDestinationGeneration = 0;

            var session = SceneEntryPresentationRegistry.Current;
            var destinationGeneration =
                TerminalSessionRegistry.Authority.CurrentSceneGeneration;
            if (!session.IsActive ||
                !session.Token.IsValid ||
                session.Phase != SceneEntryPresentationPhase.Loading ||
                session.TransitionIntent == SceneTransitionIntent.Unknown ||
                !session.DestinationStageId.IsValid ||
                session.SourceSceneGeneration <= 0 ||
                session.DestinationSceneGeneration != 0 ||
                destinationGeneration <= 0 ||
                destinationGeneration == session.SourceSceneGeneration)
            {
                return;
            }

            expectedOwner = session;
            expectedDestinationGeneration = destinationGeneration;
        }

        private static void ReportCapturedLoadingSceneEntryFailureIfOwned(
            SceneEntryPresentationSnapshot expectedOwner,
            long expectedDestinationGeneration,
            string code,
            string message)
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (!expectedOwner.IsActive ||
                !expectedOwner.Token.IsValid ||
                expectedOwner.Phase != SceneEntryPresentationPhase.Loading ||
                expectedOwner.DestinationSceneGeneration != 0 ||
                expectedDestinationGeneration <= 0 ||
                expectedDestinationGeneration ==
                expectedOwner.SourceSceneGeneration ||
                TerminalSessionRegistry.Authority.CurrentSceneGeneration !=
                expectedDestinationGeneration ||
                !session.IsActive ||
                session.Token != expectedOwner.Token ||
                session.Phase != SceneEntryPresentationPhase.Loading ||
                session.TransitionIntent != expectedOwner.TransitionIntent ||
                !session.DestinationStageId.Equals(
                    expectedOwner.DestinationStageId) ||
                session.TransitionId != expectedOwner.TransitionId ||
                session.SourceSceneGeneration !=
                expectedOwner.SourceSceneGeneration ||
                session.DestinationSceneGeneration != 0 ||
                session.LaunchProvenance != expectedOwner.LaunchProvenance ||
                session.LaunchSlotNumber != expectedOwner.LaunchSlotNumber ||
                session.LaunchToken != expectedOwner.LaunchToken)
            {
                return;
            }

            var detail = string.IsNullOrWhiteSpace(message)
                ? "Gameplay destination UI installation failed without exception details."
                : message;
            SceneEntryPresentationRegistry.TryFailHoldingCover(
                session.Token,
                $"{code}: {detail}");
        }

        private static void ReportSceneEntryFailureIfOwned(
            SceneEntrySessionToken expectedToken,
            long expectedDestinationGeneration,
            string code,
            string message)
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (!expectedToken.IsValid ||
                expectedDestinationGeneration <= 0 ||
                !session.IsActive ||
                session.Token != expectedToken ||
                session.DestinationSceneGeneration <= 0 ||
                session.DestinationSceneGeneration != expectedDestinationGeneration ||
                session.DestinationSceneGeneration !=
                TerminalSessionRegistry.Authority.CurrentSceneGeneration ||
                session.Phase == SceneEntryPresentationPhase.Completed ||
                session.Phase == SceneEntryPresentationPhase.FailedHoldingCover)
            {
                return;
            }

            var detail = string.IsNullOrWhiteSpace(message)
                ? "Gameplay destination UI installation failed without exception details."
                : message;
            SceneEntryPresentationRegistry.TryFailHoldingCover(
                session.Token,
                $"{code}: {detail}");
        }

        private static void ReportSceneEntryIrisPreparationFailureIfOwned(
            SceneEntrySessionToken expectedToken,
            long expectedDestinationGeneration,
            string code,
            string message)
        {
            var session = SceneEntryPresentationRegistry.Current;
            if (!session.IsActive ||
                session.Token != expectedToken ||
                session.DestinationSceneGeneration !=
                expectedDestinationGeneration ||
                session.DestinationSceneGeneration !=
                TerminalSessionRegistry.Authority.CurrentSceneGeneration ||
                session.Phase !=
                SceneEntryPresentationPhase.WaitingRuntimeReady)
            {
                return;
            }

            ReportSceneEntryFailureIfOwned(
                expectedToken,
                expectedDestinationGeneration,
                code,
                message);
        }

        private static void
            ReportTerminalDestinationIrisPreparationFailureIfOwned(
                TerminalSessionSnapshot expected,
                string code,
                string message)
        {
            var authority = TerminalSessionRegistry.Authority;
            var current = authority.Current;
            if (!expected.IsActive ||
                expected.TerminalKind != TerminalTransitionKind.Defeat ||
                expected.DestinationKind !=
                TerminalDestinationKind.ReloadedGameplay ||
                expected.DestinationSceneGeneration <= 0 ||
                expected.Phase !=
                TerminalSessionPhase.WaitingDestinationReady ||
                !current.IsActive ||
                current.Token != expected.Token ||
                current.TerminalKind != expected.TerminalKind ||
                current.DestinationKind != expected.DestinationKind ||
                current.TransitionId != expected.TransitionId ||
                current.SourceSceneGeneration !=
                expected.SourceSceneGeneration ||
                current.DestinationSceneGeneration !=
                expected.DestinationSceneGeneration ||
                current.Phase != expected.Phase ||
                authority.CurrentSceneGeneration !=
                expected.DestinationSceneGeneration)
            {
                return;
            }

            var detail = string.IsNullOrWhiteSpace(message)
                ? "Gameplay destination Iris preparation failed without exception details."
                : message;
            TerminalDestinationReadiness.Signal(new DestinationReadinessSignal(
                expected.Token,
                expected.TransitionId,
                expected.SourceSceneGeneration,
                expected.DestinationSceneGeneration,
                expected.DestinationKind,
                TerminalSessionPhase.WaitingDestinationReady,
                TerminalDestinationProvenance.ReloadedGameplayBootstrap,
                DestinationReadinessOutcome.Failed,
                $"{code}: {detail}"));
        }

        private static void AttachSecondaryException(
            Exception primaryException,
            string key,
            Exception secondaryException)
        {
            if (primaryException == null || secondaryException == null)
            {
                return;
            }

            try
            {
                if (!primaryException.Data.Contains(key))
                {
                    primaryException.Data[key] = secondaryException;
                }
            }
            catch
            {
                // Keep the original installation exception primary even when
                // secondary diagnostic attachment is unavailable.
            }
        }

        private IResultTransitionScreenView ResolveActiveResultTransitionView(
            TerminalDestinationKind destinationKind)
        {
            return destinationKind switch
            {
                TerminalDestinationKind.SameSceneStageResult
                    when ScreenController?.CurrentScreenId == ScreenId.StageResult =>
                    StageResultScreenView,
                TerminalDestinationKind.SameSceneGameClear
                    when ScreenController?.CurrentScreenId == ScreenId.GameClear =>
                    GameClearScreenView,
                _ => null,
            };
        }

        private void SubscribeTerminalSession()
        {
            if (_terminalSessionSubscribed)
            {
                return;
            }

            TerminalSessionRegistry.Changed += HandleTerminalSessionChanged;
            _terminalSessionSubscribed = true;
        }

        private void UnsubscribeTerminalSession()
        {
            if (!_terminalSessionSubscribed)
            {
                return;
            }

            TerminalSessionRegistry.Changed -= HandleTerminalSessionChanged;
            _terminalSessionSubscribed = false;
        }

        private static void ForceCloseTransientDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            if (TmpDropdownLiveListField == null || TmpDropdownBlockerField == null)
            {
                throw new InvalidOperationException(
                    "Terminal input ownership requires the pinned TMP_Dropdown live-list lifecycle fields.");
            }

            var liveList = TmpDropdownLiveListField.GetValue(dropdown) as GameObject;
            var blocker = TmpDropdownBlockerField.GetValue(dropdown) as GameObject;
            liveList?.SetActive(false);
            blocker?.SetActive(false);

            if (!UnityEngine.Application.isPlaying)
            {
                TmpDropdownLiveListField.SetValue(dropdown, null);
                TmpDropdownBlockerField.SetValue(dropdown, null);
                if (liveList != null)
                {
                    DestroyImmediate(liveList);
                }

                if (blocker != null)
                {
                    DestroyImmediate(blocker);
                }

                return;
            }

            if (dropdown.enabled && dropdown.gameObject.activeInHierarchy)
            {
                dropdown.enabled = false;
                dropdown.enabled = true;
                return;
            }

            TmpDropdownLiveListField.SetValue(dropdown, null);
            TmpDropdownBlockerField.SetValue(dropdown, null);
            if (liveList != null)
            {
                Destroy(liveList);
            }

            if (blocker != null)
            {
                Destroy(blocker);
            }
        }

        private bool TryToggleDemoStageControlPanel()
        {
            if (_demoStageControlSettings == null ||
                !_demoStageControlSettings.Enabled ||
                _demoStageControlCommandPort == null ||
                PopupController == null ||
                Coordinator == null)
            {
                return false;
            }

            if (PopupController.TopPopup.HasValue)
            {
                if (PopupController.TopPopup.Value.PopupId == PopupId.DemoStageControl)
                {
                    Coordinator.HandleBackRequested();
                }

                return true;
            }

            Coordinator.RequestDemoStageControlPopup(new DemoStageControlPanelPayload(
                _demoStageControlCommandPort.GetStages(),
                _demoStageControlCommandPort.GetStatus(),
                _demoGameplayOverrideCommandPort?.GetOverrideStatus() ?? default));
            return true;
        }

        private bool WasDemoStageControlOpenKeyPressed()
        {
            var settings = _demoStageControlSettings ?? DemoStageControlSettings.EnabledByDefault();
            return settings.OpenKey == DemoStageControlOpenKey.BackQuote
                ? KeyboardBridge.WasBackQuotePressedThisFrame()
                : KeyboardBridge.WasF10PressedThisFrame();
        }

        private IDemoStageControlCommandPort CreateDemoStageControlCommandPort(GameplaySceneHost sceneHost)
        {
            if (sceneHost == null ||
                sceneHost.UiAccess == null ||
                sceneHost.UiAccess.DemoStageControlCompletionBridge == null)
            {
                return null;
            }

            var provider = FindDemoStageControlContextProvider(sceneHost.gameObject);
            if (provider == null ||
                !provider.TryCreateDemoStageControlContext(out var context) ||
                !context.IsValid)
            {
                return null;
            }

            if (_campaignStageSequenceResolver != null &&
                !ReferenceEquals(_campaignStageSequenceResolver, context.SequenceResolver))
            {
                throw new InvalidOperationException(
                    "GameplayUiFlowInstaller requires Demo Stage Control to share the gameplay composition campaign sequence resolver instance.");
            }

            var launchRouter = new CurrentSceneStageLaunchRouter(gameObject.scene.name);
            return new DemoStageControlService(
                _demoStageControlSettings ?? DemoStageControlSettings.EnabledByDefault(),
                context.StageCatalogProvider,
                context.SequenceResolver,
                context.CampaignBridge,
                new DemoStageControlLaunchBridge(launchRouter, () => launchRouter.IsLaunchInProgress),
                sceneHost.UiAccess.DemoStageControlCompletionBridge);
        }

        private static IPauseProgressionReadSource CreatePauseProgressionReadSource(
            GameObject root,
            IGameplayUiPresentationSource presentationSource,
            CampaignStageSequenceResolver compositionResolver)
        {
            if (root == null || presentationSource == null)
            {
                return EmptyPauseProgressionReadSource.Instance;
            }

            var behaviours = root.GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICampaignStageSequenceResolverProvider provider &&
                    provider.TryCreateCampaignStageSequenceResolver(out var resolver) &&
                    resolver != null)
                {
                    if (compositionResolver != null &&
                        !ReferenceEquals(compositionResolver, resolver))
                    {
                        throw new InvalidOperationException(
                            "GameplayUiFlowInstaller requires pause progression to share the gameplay composition campaign sequence resolver instance.");
                    }

                    return new CampaignPauseProgressionReadSource(resolver, presentationSource);
                }
            }

            return EmptyPauseProgressionReadSource.Instance;
        }

        private static IDemoStageControlGameplayContextProvider FindDemoStageControlContextProvider(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var parents = root.GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < parents.Length; i++)
            {
                if (parents[i] is IDemoStageControlGameplayContextProvider provider)
                {
                    return provider;
                }
            }

            var children = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] is IDemoStageControlGameplayContextProvider provider)
                {
                    return provider;
                }
            }

            return null;
        }

        private void SyncViews()
        {
            if (_rootView == null ||
                ScreenController == null ||
                PopupController == null ||
                Coordinator == null)
            {
                return;
            }

            PresentationSource?.UpdateUiGameplayInputBlocked(
                Coordinator.CurrentBlockSnapshot.BlocksUiGameplayInput);

            _rootView.HudView.IsVisible = !ScreenController.CurrentEntry.HasValue ||
                                          ScreenController.CurrentEntry.Value.Policy.HudShellMode != HudShellMode.Hidden;
            _rootView.PopupLayerView.SetState(
                PopupController.PopupCount > 0,
                Coordinator.CurrentBlockSnapshot.ShowsPopupDim,
                Coordinator.CurrentBlockSnapshot.BlocksLowerLayerPointer,
                Coordinator.CurrentBlockSnapshot.PopupBackdropMode);
        }

        // Resolve Input System keyboard state without relying on UnityEngine.Input.
        private sealed class InputSystemKeyboardBridge
        {
            private static readonly Type KeyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            private static readonly PropertyInfo CurrentKeyboardProperty = KeyboardType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            private static readonly PropertyInfo EscapeKeyProperty = KeyboardType?.GetProperty("escapeKey", BindingFlags.Public | BindingFlags.Instance);
            private static readonly PropertyInfo F10KeyProperty = KeyboardType?.GetProperty("f10Key", BindingFlags.Public | BindingFlags.Instance);
            private static readonly PropertyInfo BackQuoteKeyProperty = KeyboardType?.GetProperty("backquoteKey", BindingFlags.Public | BindingFlags.Instance);
            private static readonly PropertyInfo WasPressedThisFrameProperty =
                EscapeKeyProperty?.PropertyType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);

            public bool WasEscapePressedThisFrame()
            {
                return WasPressedThisFrame(EscapeKeyProperty);
            }

            public bool WasF10PressedThisFrame()
            {
                return WasPressedThisFrame(F10KeyProperty);
            }

            public bool WasBackQuotePressedThisFrame()
            {
                return WasPressedThisFrame(BackQuoteKeyProperty);
            }

            private static bool WasPressedThisFrame(PropertyInfo keyProperty)
            {
                if (CurrentKeyboardProperty == null || keyProperty == null || WasPressedThisFrameProperty == null)
                {
                    return false;
                }

                var keyboard = CurrentKeyboardProperty.GetValue(null);
                if (keyboard == null)
                {
                    return false;
                }

                var keyControl = keyProperty.GetValue(keyboard);
                if (keyControl == null)
                {
                    return false;
                }

                return WasPressedThisFrameProperty.GetValue(keyControl) is bool pressed && pressed;
            }
        }
    }
}
