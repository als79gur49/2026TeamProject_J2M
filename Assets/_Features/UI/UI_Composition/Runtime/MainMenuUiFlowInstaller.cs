using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using Game.Shared.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class MainMenuUiFlowInstaller : MonoBehaviour
    {
        private const string GameplayUiRootShellResourcePath =
            "UI/GameplayUiCanvasRootShell";
        private const string MissingRouteConfigMessage =
            "MainMenuUiFlowInstaller requires a GameplayStageLaunchRouteConfig reference.";
        private const string MissingStageCatalogProviderMessage =
            "MainMenuUiFlowInstaller requires a ScriptableObjectStageCatalogProvider reference.";
        private const string MissingCampaignStageSequenceDefinitionMessage =
            "MainMenuUiFlowInstaller requires the authoritative serialized CampaignStageSequenceDefinition for the Main Menu composition.";
        private const string MissingMainMenuScreenPrefabMessage =
            "MainMenuUiFlowInstaller requires a MainMenuScreenView prefab reference.";
        private const string MissingPopupPrefabCatalogMessage =
            "MainMenuUiFlowInstaller requires a PopupPrefabCatalog reference.";
        private const string MissingScreenPrefabCatalogMessage =
            "MainMenuUiFlowInstaller requires the production ScreenPrefabCatalog reference.";
        private const string MissingSettingsScreenPrefabMessage =
            "MainMenuUiFlowInstaller ScreenPrefabCatalog requires a SettingsScreenView prefab reference.";
        private const string MissingSettingsTypographyThemeMessage =
            "MainMenuUiFlowInstaller ScreenPrefabCatalog requires the production Settings typography theme.";
        private const string MissingAudioInstallerMessage =
            "MainMenuUiFlowInstaller requires a same-root AudioRuntimeInstaller with audio runtime services.";
        private const string MissingDisplayInstallerMessage =
            "MainMenuUiFlowInstaller requires a same-root DisplayRuntimeInstaller with a DisplaySettingsService.";
        private const string MissingUiAudioCueMapMessage =
            "MainMenuUiFlowInstaller requires a serialized UiAudioCueMap for MainMenu UI SFX.";
        [SerializeField] private MainMenuScreenView _mainMenuScreenView;
        [SerializeField] private MainMenuScreenView _mainMenuScreenPrefab;
        [SerializeField] private ScreenPrefabCatalog _screenPrefabCatalog;
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private PopupLayerView _popupLayerView;
        [SerializeField] private PopupPrefabCatalog _popupPrefabCatalog;
        [SerializeField] private UiAudioCueMap _uiAudioCueMap;
        [SerializeField] private MainMenuCameraPresentationController _cameraPresentationController;
        [SerializeField] private GameplayStageLaunchRouteConfig _routeConfig;
        [SerializeField] private ScriptableObjectStageCatalogProvider _stageCatalogProvider;
        [SerializeField] private CampaignStageSequenceDefinition _campaignStageSequenceDefinition;
        [SerializeField] private SlotCinematicDefinition _slotCinematicDefinition;
        [SerializeField] private double _settingsPreviewTimeoutSeconds = 15d;
        [SerializeField] private bool _installOnStart = true;

        private AudioSettingsLifecycleRelay _audioSettingsLifecycleRelay;
        private CinematicFlowCoordinator _cinematicFlowCoordinator;
        private CampaignStageSequenceResolver _campaignStageSequenceResolver;
        private IConfirmPopupPort _confirmPopupPort;
        private DisplayPreviewTimeoutRelay _displayPreviewTimeoutRelay;
        private DisplaySettingsLifecycleRelay _displaySettingsLifecycleRelay;
        private bool _isInstalled;
        private IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private ILocalizedTextResolver _localizedTextResolver;
        private UiNavigationInputRouter _navigationInputRouter;
        private bool _wasKeyboardBindingRebinding;
        private MainMenuSettingsOverlayController _settingsOverlayController;
        private IMainMenuSettingsPort _settingsPort;
        private IUiAudioPort _uiAudioPort;
        private MainMenuUiAudioFeedbackController _uiAudioFeedbackController;
        private GameplayUiCanvasRootView _gameplayEntrySourceRoot;
        private TerminalIrisMotionProfileResolver _gameplayEntryMotionResolver;
        private TerminalTransitionPlayback _gameplayEntrySourcePlayback;
        private SceneEntrySessionToken _gameplayEntrySourceToken;
        private bool _gameplayEntrySourceRenderRequested;
        private long _sourceSceneGeneration;
        private GameplayUiCanvasRootView _mainMenuDestinationRoot;
        private TerminalIrisMotionProfileResolver _mainMenuDestinationMotionResolver;
        private TerminalIrisRuntimeOpenPreset? _mainMenuDestinationOpenPreset;
        private MainMenuEntrySessionToken _preparedMainMenuEntryToken;
        private MainMenuEntrySessionToken _failedMainMenuIrisSetupToken;
        private bool _mainMenuDestinationClosedPrepared;
        private float _mainMenuDestinationOpeningElapsed;
        private float _mainMenuDestinationOpeningRadius;
        private Vector2 _mainMenuDestinationCenter;

        public MainMenuController Controller { get; private set; }

        public MainMenuHubController HubController { get; private set; }

        public MainMenuScreenView MainMenuScreenView => _mainMenuScreenView;

        public PopupController PopupController { get; private set; }

        internal bool IsGameplayEntryInteractionBlocked { get; private set; }

        internal CampaignStageSequenceResolver CampaignStageSequenceResolverForDiagnostics =>
            _campaignStageSequenceResolver;

        internal long SourceSceneGenerationForTests => _sourceSceneGeneration;

        internal void RequireGameplayEntrySourceReady()
        {
            if (!_isInstalled || _mainMenuScreenView == null)
            {
                throw new InvalidOperationException(
                    "Main Menu GameplayEntry source requires the installed production Main Menu UI.");
            }

            EnsureGameplayEntrySourceRoot();
        }

        internal bool TryBeginGameplayEntrySourceClose(
            SceneEntrySessionToken token,
            GameplayEntryTransitionVisualSnapshot visual,
            out TerminalTransitionPlayback playback)
        {
            playback = _gameplayEntrySourcePlayback;
            var session = SceneEntryPresentationRegistry.Current;
            if (!_isInstalled ||
                _mainMenuScreenView == null ||
                !session.IsActive ||
                session.Token != token ||
                session.TransitionIntent != SceneTransitionIntent.GameplayEntry ||
                session.TransitionIntent != visual.Intent ||
                session.Phase != SceneEntryPresentationPhase.PersistentCoverRequested ||
                visual.SourceCloseVisualKind != GameplayEntrySourceCloseVisualKind.MainMenuIris ||
                (_gameplayEntrySourcePlayback != null &&
                 !_gameplayEntrySourcePlayback.IsTerminal))
            {
                return false;
            }

            var sourceRoot = EnsureGameplayEntrySourceRoot();
            _gameplayEntryMotionResolver ??=
                sourceRoot.RequireTerminalIrisMotionProfile().CreateResolver();
            var preset = _gameplayEntryMotionResolver.ResolveGameplayEntrySourceClose(
                visual.Intent);
            var focus = new TerminalFocusTarget(
                preset.FallbackCenter,
                preset.FallbackRadius,
                isFallback: true);
            var irisView = sourceRoot.TerminalIrisOverlayView;
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
                        focusEntityId: 1,
                        visualOnlyToken,
                        TerminalTransitionDestinationMode.SceneHandoff),
                    focus))
            {
                candidate.Dispose();
                return false;
            }

            SetGameplayEntryInteractionBlocked(true);
            _gameplayEntrySourcePlayback?.Dispose();
            _gameplayEntrySourcePlayback = candidate;
            _gameplayEntrySourceToken = token;
            _gameplayEntrySourceRenderRequested = false;
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
                !ReferenceEquals(playback, _gameplayEntrySourcePlayback) ||
                token != _gameplayEntrySourceToken ||
                !session.IsActive ||
                session.Token != token ||
                session.Phase != SceneEntryPresentationPhase.PersistentCoverRequested)
            {
                return false;
            }

            var irisView = _gameplayEntrySourceRoot.TerminalIrisOverlayView;
            if (playback.State != TerminalTransitionState.Black)
            {
                playback.Advance(Mathf.Max(0f, unscaledDeltaTime));
                irisView.Apply(playback);
            }

            if (playback.State == TerminalTransitionState.Black &&
                !_gameplayEntrySourceRenderRequested)
            {
                irisView.RequestClosedRenderAcknowledgement();
                _gameplayEntrySourceRenderRequested = true;
            }

            return playback.State == TerminalTransitionState.Black &&
                   irisView.HasRenderedEntryClosedFrame;
        }

        internal bool CompleteGameplayEntrySourceClose(
            SceneEntrySessionToken token,
            TerminalTransitionPlayback playback)
        {
            if (!ReferenceEquals(playback, _gameplayEntrySourcePlayback) ||
                token != _gameplayEntrySourceToken ||
                playback.State != TerminalTransitionState.Black ||
                _gameplayEntrySourceRoot == null ||
                !_gameplayEntrySourceRoot.TerminalIrisOverlayView
                    .HasRenderedEntryClosedFrame)
            {
                return false;
            }

            _gameplayEntrySourceRoot.TerminalIrisOverlayView.Hide();
            playback.Dispose();
            _gameplayEntrySourcePlayback = null;
            _gameplayEntrySourceToken = default;
            _gameplayEntrySourceRenderRequested = false;
            return true;
        }

        private void Start()
        {
            if (_installOnStart)
            {
                try
                {
                    Install();
                }
                catch (Exception exception)
                {
                    ReportMainMenuEntryFailureIfOwned(
                        $"MAIN_MENU_DESTINATION_INSTALL_FAILED: {exception.Message}");
                    throw;
                }
            }
        }

        private void Update()
        {
            TickMainMenuEntryPresentation(Time.unscaledDeltaTime);
            if (!_isInstalled)
            {
                return;
            }

            _wasKeyboardBindingRebinding = IsKeyboardBindingRebinding();
        }

        public void Install()
        {
            if (_isInstalled)
            {
                return;
            }

            if (_routeConfig == null)
            {
                throw new InvalidOperationException(MissingRouteConfigMessage);
            }

            if (_stageCatalogProvider == null)
            {
                throw new InvalidOperationException(MissingStageCatalogProviderMessage);
            }

            if (_campaignStageSequenceDefinition == null)
            {
                throw new InvalidOperationException(MissingCampaignStageSequenceDefinitionMessage);
            }

            _campaignStageSequenceResolver ??=
                new CampaignStageSequenceResolver(_campaignStageSequenceDefinition);

            if (_popupPrefabCatalog == null)
            {
                throw new InvalidOperationException(MissingPopupPrefabCatalogMessage);
            }

            _sourceSceneGeneration =
                TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                    gameObject.scene.handle,
                    gameObject.scene.name);
            EnsureCanvasRoot();
            EnsureEventSystem();
            EnsureMainMenuScreenView();
            EnsurePopupLayerView();
            _localizedTextResolver = UiSettingsBridgeAssembly.CreatePersistentSettingsLocalizedTextResolver();

            _mainMenuScreenView.ValidateAuthoredStructureOrThrow();
            _mainMenuScreenView.BindStaticLocalization(
                MainMenuStaticTextPayload.Default,
                _localizedTextResolver,
                DefaultLocalizedTypographyResolver.Instance,
                null,
                _popupPrefabCatalog.TypographyTheme);
            BuildPopupModule();
            BuildSettingsModule();
            BuildAudioFeedbackModule();
            BuildSaveSlotModule();
            BuildHubModule();
            BuildCameraPresentationModule();
            EnsureNavigationInputRouter();
            _mainMenuScreenView.SetVisible(true);
            _popupLayerView.SetState(false, false, false, PopupBackdropMode.None);
            _isInstalled = true;
            RegisterMainMenuEntryDestinationIfApplicable();
            if (MainMenuEntryPresentationRegistry.IsActive)
            {
                SetGameplayEntryInteractionBlocked(true);
            }
        }

        public bool TryHandleBackRequested()
        {
            if (IsGameplayEntryInteractionBlocked ||
                MainMenuEntryPresentationRegistry.IsActive ||
                (SceneEntryPresentationRegistry.IsActive &&
                 SceneEntryPresentationRegistry.Current.TransitionIntent ==
                 SceneTransitionIntent.GameplayEntry))
            {
                return true;
            }

            if (_cinematicFlowCoordinator != null && _cinematicFlowCoordinator.IsPlaying)
            {
                _cinematicFlowCoordinator.RequestSkip();
                return true;
            }

            if (IsKeyboardBindingRebinding())
            {
                return true;
            }

            if (PopupController != null && PopupController.PopupCount > 0)
            {
                return PopupController.HandleBackRequested();
            }

            if (_settingsOverlayController != null && _settingsOverlayController.IsOpen)
            {
                return _settingsOverlayController.TryHandleBackRequested();
            }

            if (_mainMenuScreenView != null && _mainMenuScreenView.TryCloseActiveSection())
            {
                return true;
            }

            return false;
        }

        private bool IsKeyboardBindingRebinding()
        {
            return _keyboardBindingSettingsPort != null && _keyboardBindingSettingsPort.IsRebinding;
        }

        private void BuildPopupModule()
        {
            PopupController = new PopupController(new GameplayPopupRuntimeFactory(
                _popupLayerView,
                _popupPrefabCatalog,
                localizedTextResolver: _localizedTextResolver));
            PopupController.StateChanged += SyncPopupLayer;
            _popupLayerView.BackdropClicked += HandlePopupBackdropClicked;
            _confirmPopupPort = new ConfirmPopupPortAdapter(PopupController);
        }

        private void BuildSettingsModule()
        {
            if (_screenPrefabCatalog == null)
            {
                throw new InvalidOperationException(MissingScreenPrefabCatalogMessage);
            }

            if (_screenPrefabCatalog.SettingsPrefab == null)
            {
                throw new InvalidOperationException(MissingSettingsScreenPrefabMessage);
            }

            if (_screenPrefabCatalog.SettingsTypographyTheme == null)
            {
                throw new InvalidOperationException(MissingSettingsTypographyThemeMessage);
            }

            var audioSettingsPort = UiSettingsBridgeAssembly.CreateAudioSettingsPort(gameObject, MissingAudioInstallerMessage);
            var displaySettingsPort = UiSettingsBridgeAssembly.CreateDisplaySettingsPort(gameObject, MissingDisplayInstallerMessage);
            _keyboardBindingSettingsPort = CreateKeyboardBindingSettingsPort();
            _audioSettingsLifecycleRelay = UiSettingsBridgeAssembly.EnsureAudioSettingsLifecycleRelay(gameObject, audioSettingsPort);
            EnsureDisplayPreviewTimeoutRelay();
            EnsureDisplaySettingsLifecycleRelay();

            var uiAudioPort = EnsureUiAudioPort();
            var displayPreviewSessionHost = new DisplayPreviewSessionHost(
                PopupController,
                _displayPreviewTimeoutRelay,
                _settingsPreviewTimeoutSeconds);

            _settingsOverlayController = new MainMenuSettingsOverlayController(
                transform,
                _popupLayerView != null ? _popupLayerView.transform : null,
                contentRoot => new MainMenuSettingsRuntime(
                    new SettingsScreenRuntimeBuildContext(
                        parent: contentRoot,
                        prefab: _screenPrefabCatalog.SettingsPrefab,
                        audioSettingsPort: audioSettingsPort,
                        displaySettingsPort: displaySettingsPort,
                        keyboardBindingSettingsPort: _keyboardBindingSettingsPort,
                        uiAudioPort: uiAudioPort,
                        displayPreviewSessionHost: displayPreviewSessionHost,
                        displaySettingsLifecycleRelay: _displaySettingsLifecycleRelay,
                        typographyTheme: _screenPrefabCatalog.SettingsTypographyTheme,
                        localizedTextResolver: _localizedTextResolver),
                    SettingsScreenPayload.Default,
                    PopupController));
            _settingsPort = new MainMenuSettingsPortAdapter(_settingsOverlayController);
        }

        private void BuildSaveSlotModule()
        {
            var sequenceResolver = _campaignStageSequenceResolver ??
                throw new InvalidOperationException(
                    "MainMenuUiFlowInstaller campaign sequence resolver was not created during composition bootstrap.");
            var saveSlotStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            var activeSlotProvider = CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(saveSlotStore);
            ImportStandaloneCampaignSaveSeed(saveSlotStore, activeSlotProvider, sequenceResolver);
            var launchHandoffStore = CampaignLaunchHandoffSessionStore.Instance;
            var validationService = new SaveSlotValidationService(sequenceResolver, _stageCatalogProvider);
            IStageLaunchRouter stageLaunchRouter = new ConfiguredGameplayStageLaunchRouter(_routeConfig);
            stageLaunchRouter = new CinematicStageLaunchRouter(
                stageLaunchRouter,
                saveSlotStore,
                launchHandoffStore,
                EnsureCinematicFlowCoordinator());
            Controller = new MainMenuController(
                saveSlotStore,
                launchHandoffStore,
                sequenceResolver,
                stageLaunchRouter,
                _confirmPopupPort,
                validationService,
                _localizedTextResolver,
                new UnityMainMenuSaveDiagnosticPort());

            _mainMenuScreenView.SaveSlotPanel.SaveSlotIntentRequested += Controller.HandleIntent;
            Controller.ViewModelChanged += HandleControllerViewModelChanged;
            _mainMenuScreenView.SaveSlotPanel.Bind(Controller.BuildViewModel());
        }

        private void ImportStandaloneCampaignSaveSeed(
            ICampaignSaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (!StandaloneCampaignSaveSeedImporter.TryImportDefaultSeed(
                    saveSlotStore,
                    activeSlotProvider,
                    sequenceResolver,
                    _stageCatalogProvider,
                    out var importResult))
            {
                if (importResult.Status != StandaloneCampaignSaveSeedImportStatus.FileNotFound &&
                    importResult.Status != StandaloneCampaignSaveSeedImportStatus.EditorRuntimeSkipped)
                {
                    Debug.LogWarning($"Standalone campaign save seed import skipped: {importResult.Message}");
                }

                return;
            }

            Debug.Log(
                $"Standalone campaign save seed imported: slot={importResult.SlotNumber}, stage={importResult.StageId.Value}, path={importResult.SeedPath}");
        }

        private void BuildHubModule()
        {
            HubController = new MainMenuHubController(
                _settingsPort ?? NoOpMainMenuSettingsPort.Instance,
                new UnityApplicationQuitPort(),
                _confirmPopupPort,
                _mainMenuScreenView.ShowSection);

            _mainMenuScreenView.CommandRequested += HubController.HandleCommand;
            _mainMenuScreenView.NavigationRequested += HubController.HandleNavigation;
            _mainMenuScreenView.ShowSection(MainMenuSectionId.None);
        }

        private void BuildAudioFeedbackModule()
        {
            _uiAudioFeedbackController = new MainMenuUiAudioFeedbackController(EnsureUiAudioPort());
            _uiAudioFeedbackController.Attach(
                _mainMenuScreenView,
                PopupController,
                _settingsOverlayController);
        }

        private void BuildCameraPresentationModule()
        {
            _cameraPresentationController?.Attach(_mainMenuScreenView, _settingsOverlayController);
        }

        private void EnsureNavigationInputRouter()
        {
            _navigationInputRouter = GetComponent<UiNavigationInputRouter>();
            if (_navigationInputRouter == null)
            {
                _navigationInputRouter = gameObject.AddComponent<UiNavigationInputRouter>();
            }

            ConfigureNavigationInputRouter();
            if (PopupController != null)
            {
                PopupController.StateChanged -= HandlePopupNavigationStateChanged;
                PopupController.StateChanged += HandlePopupNavigationStateChanged;
            }
        }

        private void HandlePopupNavigationStateChanged()
        {
            ConfigureNavigationInputRouter();
        }

        private void ConfigureNavigationInputRouter()
        {
            if (_navigationInputRouter == null)
            {
                return;
            }

            _navigationInputRouter.Initialize(
                _inputActions,
                new UiLayeredNavigationTargetResolver(
                    PopupController,
                    screenController: null,
                    screenProvider: new SingleUiNavigationTargetProvider(_mainMenuScreenView),
                    modalOverlayProvider: _settingsOverlayController),
                TryHandleBackRequested,
                () => IsKeyboardBindingRebinding() ||
                      _wasKeyboardBindingRebinding ||
                      (_cinematicFlowCoordinator != null && _cinematicFlowCoordinator.IsPlaying) ||
                      MainMenuEntryPresentationRegistry.IsActive,
                EnsureUiAudioPort());
        }

        private void OnDestroy()
        {
            _gameplayEntrySourcePlayback?.Dispose();
            _gameplayEntrySourcePlayback = null;
            ReportMainMenuEntryFailureIfOwned(
                "MAIN_MENU_DESTINATION_INSTALLER_DESTROYED: Main Menu installer was destroyed before opening completed.");
            _uiAudioFeedbackController?.Dispose();
            _uiAudioFeedbackController = null;
            _cameraPresentationController?.Detach();

            if (Controller != null)
            {
                Controller.ViewModelChanged -= HandleControllerViewModelChanged;
            }

            if (_mainMenuScreenView != null && _mainMenuScreenView.SaveSlotPanel != null && Controller != null)
            {
                _mainMenuScreenView.SaveSlotPanel.SaveSlotIntentRequested -= Controller.HandleIntent;
            }

            if (_mainMenuScreenView != null && HubController != null)
            {
                _mainMenuScreenView.CommandRequested -= HubController.HandleCommand;
                _mainMenuScreenView.NavigationRequested -= HubController.HandleNavigation;
            }

            if (_popupLayerView != null)
            {
                _popupLayerView.BackdropClicked -= HandlePopupBackdropClicked;
            }

            if (PopupController != null)
            {
                PopupController.StateChanged -= SyncPopupLayer;
                PopupController.StateChanged -= HandlePopupNavigationStateChanged;
            }

            _settingsOverlayController?.Dispose();
            _audioSettingsLifecycleRelay?.FlushNow();
            Controller?.Dispose();
            PopupController?.Dispose();
            (_localizedTextResolver as IDisposable)?.Dispose();
        }

        private void RegisterMainMenuEntryDestinationIfApplicable()
        {
            var session = MainMenuEntryPresentationRegistry.Current;
            if (!session.IsActive ||
                session.Phase != SceneEntryPresentationPhase.Loading)
            {
                return;
            }

            if (!MainMenuEntryPresentationRegistry.TryRegisterDestinationScene(
                    session.Token,
                    _sourceSceneGeneration))
            {
                MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                    session.Token,
                    "Main Menu destination scene generation could not be correlated.");
                throw new InvalidOperationException(
                    $"Main Menu entry session {session.Token} rejected destination generation {_sourceSceneGeneration}.");
            }
        }

        private void TickMainMenuEntryPresentation(float unscaledDeltaTime)
        {
            var session = MainMenuEntryPresentationRegistry.Current;
            if (!session.IsActive || !_isInstalled)
            {
                return;
            }

            if (session.Phase == SceneEntryPresentationPhase.WaitingRuntimeReady &&
                !_mainMenuDestinationClosedPrepared)
            {
                if (_failedMainMenuIrisSetupToken == session.Token)
                {
                    return;
                }

                if (!IsStrongMainMenuDestinationReady(session))
                {
                    return;
                }

                var ready = new MainMenuDestinationReady(
                    session.Token,
                    session.TransitionId,
                    session.DestinationSceneGeneration,
                    MainMenuDestinationReadyProvenance.ProductionMainMenuComposition);
                if (!MainMenuEntryPresentationRegistry.CanAcceptRuntimeReady(ready))
                {
                    MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                        session.Token,
                        "Main Menu destination readiness token, transition, generation, or provenance was invalid.");
                    return;
                }

                PrepareMainMenuDestinationIris(session);
                return;
            }

            if (session.Phase == SceneEntryPresentationPhase.WaitingRuntimeReady &&
                _mainMenuDestinationClosedPrepared)
            {
                if (_preparedMainMenuEntryToken != session.Token ||
                    !_mainMenuDestinationRoot.TerminalIrisOverlayView
                        .HasRenderedEntryClosedFrame)
                {
                    return;
                }

                if (!MainMenuEntryPresentationRegistry.TryAdvance(
                        session.Token,
                        SceneEntryPresentationPhase.EntryIrisClosed) ||
                    !SceneTransitionCoordinator.ReleaseMainMenuEntryCover(
                        session.Token) ||
                    !MainMenuEntryPresentationRegistry.TryAdvance(
                        session.Token,
                        SceneEntryPresentationPhase.Opening))
                {
                    MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                        session.Token,
                        "Rendered closed Main Menu Iris could not acquire the persistent-cover handoff.");
                }

                return;
            }

            if (session.Phase != SceneEntryPresentationPhase.Opening)
            {
                return;
            }

            if (!_mainMenuDestinationOpenPreset.HasValue ||
                _preparedMainMenuEntryToken != session.Token)
            {
                MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                    session.Token,
                    "Main Menu opening has no correlated authored motion preset.");
                return;
            }

            var openPreset = _mainMenuDestinationOpenPreset.Value;
            _mainMenuDestinationOpeningElapsed +=
                Mathf.Max(0f, unscaledDeltaTime);
            var openingElapsed = Mathf.Max(
                0f,
                _mainMenuDestinationOpeningElapsed -
                openPreset.PreOpenHoldDuration);
            var progress = Mathf.Clamp01(
                openingElapsed / openPreset.OpeningDuration);
            var easing = TerminalIrisEasingUtility.Evaluate(
                openPreset.OpeningEasing,
                progress);
            _mainMenuDestinationRoot.TerminalIrisOverlayView.ApplyEntryRadius(
                Mathf.Lerp(0f, _mainMenuDestinationOpeningRadius, easing),
                Mathf.Lerp(
                    openPreset.FinalClosedOvershootPixels,
                    0f,
                    easing));
            if (progress < 1f)
            {
                return;
            }

            _mainMenuDestinationRoot.TerminalIrisOverlayView.Hide();
            _mainMenuDestinationClosedPrepared = false;
            _failedMainMenuIrisSetupToken = default;
            _mainMenuDestinationOpenPreset = null;
            _preparedMainMenuEntryToken = default;
            if (!MainMenuEntryPresentationRegistry.TryComplete(session.Token))
            {
                throw new InvalidOperationException(
                    $"Main Menu opening completion rejected token {session.Token}.");
            }

            MainMenuTransitionVisualPolicy.Clear(session.Token);
            Time.timeScale = 1f;
            SetGameplayEntryInteractionBlocked(false);
        }

        private bool IsStrongMainMenuDestinationReady(
            MainMenuEntryPresentationSnapshot session)
        {
            var canvas = GetComponentInParent<Canvas>();
            var eventSystem = EventSystem.current != null
                ? EventSystem.current
                : UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            return session.DestinationSceneGeneration == _sourceSceneGeneration &&
                   _sourceSceneGeneration ==
                   TerminalSessionRegistry.Authority.CurrentSceneGeneration &&
                   _mainMenuScreenView != null &&
                   _mainMenuScreenView.isActiveAndEnabled &&
                   _mainMenuScreenView.gameObject.activeInHierarchy &&
                   _mainMenuScreenView.SaveSlotPanel != null &&
                   Controller != null &&
                   HubController != null &&
                   PopupController != null &&
                   _localizedTextResolver != null &&
                   _navigationInputRouter != null &&
                   canvas != null &&
                   canvas.isActiveAndEnabled &&
                   canvas.gameObject.activeInHierarchy &&
                   eventSystem != null &&
                   eventSystem.isActiveAndEnabled &&
                   eventSystem.currentInputModule != null;
        }

        private void PrepareMainMenuDestinationIris(
            MainMenuEntryPresentationSnapshot session)
        {
            var expectedToken = session.Token;
            var expectedDestinationGeneration =
                session.DestinationSceneGeneration;
            TerminalIrisOverlayView irisView = null;
            try
            {
                var destinationRoot = EnsureMainMenuDestinationRoot();
                _mainMenuDestinationMotionResolver ??=
                    destinationRoot.RequireTerminalIrisMotionProfile()
                        .CreateResolver();
                var closePreset =
                    _mainMenuDestinationMotionResolver
                        .ResolveGameplayEntryClose();
                var preparedOpenPreset =
                    _mainMenuDestinationMotionResolver.ResolveStageEntryOpen();
                var visual = MainMenuTransitionVisualPolicy.Require(
                    expectedToken,
                    session.TransitionIntent);
                irisView = destinationRoot.TerminalIrisOverlayView;
                var center = closePreset.FallbackCenter;
                irisView.ConfigureTransitionColor(
                    visual.DestinationOpenColor);
                irisView.Show();
                irisView.ApplyClosedEntry(center, preparedOpenPreset);
                var openingRadius = irisView.CalculateFullyRevealedRadius(
                    center,
                    preparedOpenPreset.FullOpenMargin);

                _mainMenuDestinationCenter = center;
                _mainMenuDestinationOpeningRadius = openingRadius;
                _mainMenuDestinationOpeningElapsed = 0f;
                _mainMenuDestinationOpenPreset = preparedOpenPreset;
                _preparedMainMenuEntryToken = expectedToken;
                _failedMainMenuIrisSetupToken = default;
                _mainMenuDestinationClosedPrepared = true;
            }
            catch (Exception setupException)
            {
                AbortMainMenuDestinationIrisPreparation(
                    expectedToken,
                    expectedDestinationGeneration,
                    irisView,
                    setupException);
                throw;
            }
        }

        private void AbortMainMenuDestinationIrisPreparation(
            MainMenuEntrySessionToken expectedToken,
            long expectedDestinationGeneration,
            TerminalIrisOverlayView irisView,
            Exception primaryException)
        {
            _mainMenuDestinationClosedPrepared = false;
            _failedMainMenuIrisSetupToken = expectedToken;
            _mainMenuDestinationOpenPreset = null;
            _preparedMainMenuEntryToken = default;
            _mainMenuDestinationOpeningElapsed = 0f;
            _mainMenuDestinationOpeningRadius = 0f;
            _mainMenuDestinationCenter = default;
            try
            {
                irisView?.Hide();
            }
            catch (Exception cleanupException)
            {
                AttachSecondaryException(
                    primaryException,
                    "MainMenuDestinationIrisCleanupFailure",
                    cleanupException);
            }

            try
            {
                ReportMainMenuEntryIrisPreparationFailureIfOwned(
                    expectedToken,
                    expectedDestinationGeneration,
                    "MAIN_MENU_DESTINATION_IRIS_PREPARATION_FAILED",
                    primaryException.Message);
            }
            catch (Exception reportException)
            {
                AttachSecondaryException(
                    primaryException,
                    "MainMenuDestinationIrisFailureReportingFailure",
                    reportException);
            }
        }

        private GameplayUiCanvasRootView EnsureMainMenuDestinationRoot()
        {
            if (_mainMenuDestinationRoot != null)
            {
                return _mainMenuDestinationRoot;
            }

            var prefab = Resources.Load<GameplayUiCanvasRootView>(
                GameplayUiRootShellResourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Main Menu destination Iris requires Resources/{GameplayUiRootShellResourcePath}.");
            }

            _mainMenuDestinationRoot = Instantiate(prefab, transform, false);
            _mainMenuDestinationRoot.name = "MainMenuDestinationIris";
            _mainMenuDestinationRoot.PrepareTransitionOnlyPresentation();
            _mainMenuDestinationRoot.transform.SetAsLastSibling();
            return _mainMenuDestinationRoot;
        }

        private static void ReportMainMenuEntryFailureIfOwned(string failureReason)
        {
            var session = MainMenuEntryPresentationRegistry.Current;
            if (session.IsActive &&
                session.Phase != SceneEntryPresentationPhase.FailedHoldingCover)
            {
                MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                    session.Token,
                    failureReason);
            }
        }

        private static void ReportMainMenuEntryIrisPreparationFailureIfOwned(
            MainMenuEntrySessionToken expectedToken,
            long expectedDestinationGeneration,
            string code,
            string message)
        {
            var session = MainMenuEntryPresentationRegistry.Current;
            if (!expectedToken.IsValid ||
                expectedDestinationGeneration <= 0 ||
                !session.IsActive ||
                session.Token != expectedToken ||
                session.DestinationSceneGeneration !=
                expectedDestinationGeneration ||
                expectedDestinationGeneration !=
                TerminalSessionRegistry.Authority.CurrentSceneGeneration ||
                session.Phase !=
                SceneEntryPresentationPhase.WaitingRuntimeReady)
            {
                return;
            }

            var detail = string.IsNullOrWhiteSpace(message)
                ? "Main Menu destination Iris preparation failed without exception details."
                : message;
            MainMenuEntryPresentationRegistry.TryFailHoldingCover(
                expectedToken,
                $"{code}: {detail}");
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
                // Keep the original setup exception primary.
            }
        }

        private GameplayUiCanvasRootView EnsureGameplayEntrySourceRoot()
        {
            if (_gameplayEntrySourceRoot != null)
            {
                return _gameplayEntrySourceRoot;
            }

            var prefab = Resources.Load<GameplayUiCanvasRootView>(
                GameplayUiRootShellResourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Main Menu GameplayEntry source Iris requires Resources/{GameplayUiRootShellResourcePath}.");
            }

            _gameplayEntrySourceRoot = Instantiate(prefab, transform, false);
            _gameplayEntrySourceRoot.name = "MainMenuGameplayEntryIrisSource";
            _gameplayEntrySourceRoot.PrepareTransitionOnlyPresentation();
            _gameplayEntrySourceRoot.transform.SetAsLastSibling();
            return _gameplayEntrySourceRoot;
        }

        private void SetGameplayEntryInteractionBlocked(bool blocked)
        {
            IsGameplayEntryInteractionBlocked = blocked;
            _mainMenuScreenView?.SetLaunchInteractionBlocked(blocked);
            if (blocked)
            {
                _navigationInputRouter?.ClearNavigationFocus();
            }
        }

        private void HandleControllerViewModelChanged(SaveSlotPanelViewModel viewModel)
        {
            if (_mainMenuScreenView != null && _mainMenuScreenView.SaveSlotPanel != null)
            {
                _mainMenuScreenView.SaveSlotPanel.Bind(viewModel);
            }
        }

        private void HandlePopupBackdropClicked()
        {
            PopupController?.HandleBackdropClicked();
        }

        private void SyncPopupLayer()
        {
            if (_popupLayerView == null || PopupController == null)
            {
                return;
            }

            var topPopup = PopupController.TopPopup;
            if (!topPopup.HasValue)
            {
                _popupLayerView.SetState(false, false, false, PopupBackdropMode.None);
                return;
            }

            var policy = topPopup.Value.Policy;
            _popupLayerView.SetState(
                true,
                policy.ShowsDim,
                policy.BlocksLowerLayers || policy.BackdropMode != PopupBackdropMode.None,
                policy.BackdropMode);
        }

        private void EnsureCanvasRoot()
        {
            if (GetComponentInParent<Canvas>() != null)
            {
                return;
            }

            UiOverlayCanvasConfigurator.ConfigureOverlayCanvas(gameObject);
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            var inputSystemUiModuleType = UiEventSystemNavigationActionUtility.RequireInputSystemUiModuleType();

            if (eventSystem.GetComponent(inputSystemUiModuleType) == null)
            {
                eventSystem.gameObject.AddComponent(inputSystemUiModuleType);
            }

            UiEventSystemNavigationActionUtility.DisableNavigationActions(eventSystem, inputSystemUiModuleType);

            var legacyModules = eventSystem.GetComponents<StandaloneInputModule>();
            foreach (var legacyModule in legacyModules)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(legacyModule);
                }
                else
                {
                    DestroyImmediate(legacyModule);
                }
            }
        }

        private void EnsureMainMenuScreenView()
        {
            if (_mainMenuScreenView != null)
            {
                return;
            }

            if (_mainMenuScreenPrefab == null)
            {
                throw new InvalidOperationException(MissingMainMenuScreenPrefabMessage);
            }

            _mainMenuScreenView = Instantiate(_mainMenuScreenPrefab, transform, false);
            _mainMenuScreenView.name = _mainMenuScreenPrefab.name;
        }

        private void EnsurePopupLayerView()
        {
            if (_popupLayerView != null && _popupLayerView.ContentRoot != null)
            {
                _popupLayerView.transform.SetAsLastSibling();
                return;
            }

            var layerRoot = new GameObject("MainMenuPopupLayer", typeof(RectTransform));
            layerRoot.transform.SetParent(transform, false);
            layerRoot.transform.SetAsLastSibling();
            var layerRect = (RectTransform)layerRoot.transform;
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;

            _popupLayerView = layerRoot.AddComponent<PopupLayerView>();

            var backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(layerRoot.transform, false);
            var backdropRect = (RectTransform)backdrop.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropCanvasGroup = backdrop.AddComponent<CanvasGroup>();
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = Color.black;
            var backdropButton = backdrop.AddComponent<Button>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(layerRoot.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            _popupLayerView.Configure(layerRoot, backdropCanvasGroup, backdropImage, backdropButton, contentRect);
        }

        private IUiAudioPort EnsureUiAudioPort()
        {
            return _uiAudioPort ??= UiSettingsBridgeAssembly.CreateUiAudioPort(
                gameObject,
                _uiAudioCueMap,
                MissingAudioInstallerMessage,
                MissingUiAudioCueMapMessage);
        }

        private IKeyboardBindingSettingsPort CreateKeyboardBindingSettingsPort()
        {
            if (_inputActions == null)
            {
                return NoOpKeyboardBindingSettingsPort.Instance;
            }

            return new KeyboardBindingSettingsPortAdapter(new KeyboardBindingSettingsService(_inputActions));
        }

        private CinematicFlowCoordinator EnsureCinematicFlowCoordinator()
        {
            if (_cinematicFlowCoordinator != null)
            {
                return _cinematicFlowCoordinator;
            }

            var overlay = GetComponentInChildren<CinematicVideoOverlayView>(includeInactive: true);
            if (overlay == null)
            {
                var overlayObject = new GameObject("CinematicVideoOverlay", typeof(RectTransform));
                overlayObject.transform.SetParent(transform, false);
                overlay = overlayObject.AddComponent<CinematicVideoOverlayView>();
                overlayObject.SetActive(false);
            }

            overlay.Initialize(_inputActions);
            var audioFocus = GetComponent<CinematicAudioFocusController>();
            if (audioFocus == null)
            {
                audioFocus = gameObject.AddComponent<CinematicAudioFocusController>();
            }

            _cinematicFlowCoordinator = new CinematicFlowCoordinator(
                _slotCinematicDefinition,
                overlay,
                audioFocus);
            return _cinematicFlowCoordinator;
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

    }

    internal sealed class UnityMainMenuSaveDiagnosticPort : IMainMenuSaveDiagnosticPort
    {
        public void Report(SaveSlotFailureDiagnostic diagnostic)
        {
            var slot = diagnostic.SlotNumber > 0
                ? diagnostic.SlotNumber.ToString()
                : "all";
            var reason = (diagnostic.Reason ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            Debug.LogWarning(
                "[CampaignSaveUI] " +
                $"operation={diagnostic.Operation} " +
                $"slot={slot} " +
                $"failure={diagnostic.FailureKind} " +
                $"status={diagnostic.LoadStatus} " +
                $"reason={reason}");
        }
    }
}
