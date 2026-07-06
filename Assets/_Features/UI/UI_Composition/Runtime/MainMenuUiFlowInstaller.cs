using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using Game.Shared.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class MainMenuUiFlowInstaller : MonoBehaviour
    {
        private const string MissingRouteConfigMessage =
            "MainMenuUiFlowInstaller requires a GameplayStageLaunchRouteConfig reference.";
        private const string MissingStageCatalogProviderMessage =
            "MainMenuUiFlowInstaller requires a ScriptableObjectStageCatalogProvider reference.";
        private const string MissingMainMenuScreenPrefabMessage =
            "MainMenuUiFlowInstaller requires a MainMenuScreenView prefab reference.";
        private const string MissingPopupPrefabCatalogMessage =
            "MainMenuUiFlowInstaller requires a PopupPrefabCatalog reference.";
        private const string MissingSettingsScreenPrefabMessage =
            "MainMenuUiFlowInstaller requires a SettingsScreenView prefab reference.";
        private const string MissingAudioInstallerMessage =
            "MainMenuUiFlowInstaller requires a same-root AudioRuntimeInstaller with audio runtime services.";
        private const string MissingDisplayInstallerMessage =
            "MainMenuUiFlowInstaller requires a same-root DisplayRuntimeInstaller with a DisplaySettingsService.";
        private const string MissingUiAudioCueMapMessage =
            "MainMenuUiFlowInstaller requires a serialized UiAudioCueMap for MainMenu UI SFX.";
        [SerializeField] private MainMenuScreenView _mainMenuScreenView;
        [SerializeField] private MainMenuScreenView _mainMenuScreenPrefab;
        [SerializeField] private SettingsScreenView _settingsScreenPrefab;
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private PopupLayerView _popupLayerView;
        [SerializeField] private PopupPrefabCatalog _popupPrefabCatalog;
        [SerializeField] private UiAudioCueMap _uiAudioCueMap;
        [SerializeField] private TMP_FontAsset _koreanSettingsFont;
        [SerializeField] private MainMenuCameraPresentationController _cameraPresentationController;
        [SerializeField] private GameplayStageLaunchRouteConfig _routeConfig;
        [SerializeField] private ScriptableObjectStageCatalogProvider _stageCatalogProvider;
        [SerializeField] private CampaignStageSequenceDefinition _campaignStageSequenceDefinition;
        [SerializeField] private SlotCinematicDefinition _slotCinematicDefinition;
        [SerializeField] private double _settingsPreviewTimeoutSeconds = 15d;
        [SerializeField] private bool _installOnStart = true;

        private AudioSettingsLifecycleRelay _audioSettingsLifecycleRelay;
        private CinematicFlowCoordinator _cinematicFlowCoordinator;
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

        public MainMenuController Controller { get; private set; }

        public MainMenuHubController HubController { get; private set; }

        public MainMenuScreenView MainMenuScreenView => _mainMenuScreenView;

        public PopupController PopupController { get; private set; }

        private void Start()
        {
            if (_installOnStart)
            {
                Install();
            }
        }

        private void Update()
        {
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

            if (_popupPrefabCatalog == null)
            {
                throw new InvalidOperationException(MissingPopupPrefabCatalogMessage);
            }

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
                _koreanSettingsFont != null ? new DefaultLocalizedTmpFontResolver(_koreanSettingsFont) : null);
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
        }

        public bool TryHandleBackRequested()
        {
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
            if (_settingsScreenPrefab == null)
            {
                throw new InvalidOperationException(MissingSettingsScreenPrefabMessage);
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
                    _settingsScreenPrefab,
                    contentRoot,
                    audioSettingsPort,
                    displaySettingsPort,
                    _keyboardBindingSettingsPort,
                    PopupController,
                    displayPreviewSessionHost,
                    _displaySettingsLifecycleRelay,
                    SettingsScreenPayload.Default,
                    _settingsPreviewTimeoutSeconds,
                    uiAudioPort: uiAudioPort,
                    localizedTextResolver: _localizedTextResolver,
                    localizedTmpFontResolver: _koreanSettingsFont != null
                        ? new DefaultLocalizedTmpFontResolver(_koreanSettingsFont)
                        : null));
            _settingsPort = new MainMenuSettingsPortAdapter(_settingsOverlayController);
        }

        private void BuildSaveSlotModule()
        {
            var sequenceDefinition = _campaignStageSequenceDefinition != null
                ? _campaignStageSequenceDefinition
                : CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
            var sequenceResolver = new CampaignStageSequenceResolver(sequenceDefinition);
            ImportStandaloneCampaignSaveSeed(sequenceResolver);
            var saveSlotStore = new SaveSlotStore();
            var activeSlotProvider = new ActiveSlotProvider();
            var validationService = new SaveSlotValidationService(sequenceResolver, _stageCatalogProvider);
            IStageLaunchRouter stageLaunchRouter = new ConfiguredGameplayStageLaunchRouter(_routeConfig);
            stageLaunchRouter = new CinematicStageLaunchRouter(
                stageLaunchRouter,
                saveSlotStore,
                activeSlotProvider,
                EnsureCinematicFlowCoordinator());
            Controller = new MainMenuController(
                saveSlotStore,
                activeSlotProvider,
                sequenceResolver,
                stageLaunchRouter,
                _confirmPopupPort,
                validationService);

            _mainMenuScreenView.SaveSlotPanel.SaveSlotIntentRequested += Controller.HandleIntent;
            Controller.ViewModelChanged += HandleControllerViewModelChanged;
            _mainMenuScreenView.SaveSlotPanel.Bind(Controller.BuildViewModel());
        }

        private void ImportStandaloneCampaignSaveSeed(CampaignStageSequenceResolver sequenceResolver)
        {
            if (!StandaloneCampaignSaveSeedImporter.TryImportDefaultSeed(
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
                      (_cinematicFlowCoordinator != null && _cinematicFlowCoordinator.IsPlaying),
                EnsureUiAudioPort());
        }

        private void OnDestroy()
        {
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
            PopupController?.Dispose();
            (_localizedTextResolver as IDisposable)?.Dispose();
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
}
