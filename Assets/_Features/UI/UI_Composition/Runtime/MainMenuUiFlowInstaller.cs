using System;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using Game.Shared.Display;
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
        private static readonly InputSystemKeyboardBridge KeyboardBridge = new();

        [SerializeField] private MainMenuScreenView _mainMenuScreenView;
        [SerializeField] private MainMenuScreenView _mainMenuScreenPrefab;
        [SerializeField] private SettingsScreenView _settingsScreenPrefab;
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private PopupLayerView _popupLayerView;
        [SerializeField] private PopupPrefabCatalog _popupPrefabCatalog;
        [SerializeField] private UiAudioCueMap _uiAudioCueMap;
        [SerializeField] private GameplayStageLaunchRouteConfig _routeConfig;
        [SerializeField] private ScriptableObjectStageCatalogProvider _stageCatalogProvider;
        [SerializeField] private CampaignStageSequenceDefinition _campaignStageSequenceDefinition;
        [SerializeField] private double _settingsPreviewTimeoutSeconds = 15d;
        [SerializeField] private bool _installOnStart = true;

        private AudioSettingsLifecycleRelay _audioSettingsLifecycleRelay;
        private IConfirmPopupPort _confirmPopupPort;
        private DisplayPreviewTimeoutRelay _displayPreviewTimeoutRelay;
        private DisplaySettingsLifecycleRelay _displaySettingsLifecycleRelay;
        private bool _isInstalled;
        private IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private bool _wasKeyboardBindingRebinding;
        private MainMenuSettingsOverlayController _settingsOverlayController;
        private IMainMenuSettingsPort _settingsPort;
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

            var isKeyboardBindingRebinding = IsKeyboardBindingRebinding();
            if (KeyboardBridge.WasEscapePressedThisFrame())
            {
                if (isKeyboardBindingRebinding || _wasKeyboardBindingRebinding)
                {
                    _wasKeyboardBindingRebinding = isKeyboardBindingRebinding;
                    return;
                }

                TryHandleBackRequested();
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

            _mainMenuScreenView.ValidateAuthoredStructureOrThrow();
            BuildPopupModule();
            BuildSettingsModule();
            BuildSaveSlotModule();
            BuildHubModule();
            BuildAudioFeedbackModule();
            _mainMenuScreenView.SetVisible(true);
            _popupLayerView.SetState(false, false, false, PopupBackdropMode.None);
            _isInstalled = true;
        }

        public bool TryHandleBackRequested()
        {
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
            PopupController = new PopupController(new GameplayPopupRuntimeFactory(_popupLayerView, _popupPrefabCatalog));
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

            var audioSettingsPort = CreateAudioSettingsPort();
            var displaySettingsPort = CreateDisplaySettingsPort();
            _keyboardBindingSettingsPort = CreateKeyboardBindingSettingsPort();
            EnsureAudioSettingsLifecycleRelay(audioSettingsPort);
            EnsureDisplayPreviewTimeoutRelay();
            EnsureDisplaySettingsLifecycleRelay();

            var accessibilitySettingsStore = new AccessibilitySettingsStore();
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
                    accessibilitySettingsStore,
                    audioSettingsPort,
                    displaySettingsPort,
                    _keyboardBindingSettingsPort,
                    PopupController,
                    displayPreviewSessionHost,
                    _displaySettingsLifecycleRelay,
                    SettingsScreenPayload.Default,
                    _settingsPreviewTimeoutSeconds));
            _settingsPort = new MainMenuSettingsPortAdapter(_settingsOverlayController);
        }

        private void BuildSaveSlotModule()
        {
            var sequenceDefinition = _campaignStageSequenceDefinition != null
                ? _campaignStageSequenceDefinition
                : CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
            var sequenceResolver = new CampaignStageSequenceResolver(sequenceDefinition);
            var saveSlotStore = new SaveSlotStore();
            var activeSlotProvider = new ActiveSlotProvider();
            var validationService = new SaveSlotValidationService(sequenceResolver, _stageCatalogProvider);
            Controller = new MainMenuController(
                saveSlotStore,
                activeSlotProvider,
                sequenceResolver,
                new ConfiguredGameplayStageLaunchRouter(_routeConfig),
                _confirmPopupPort,
                validationService);

            _mainMenuScreenView.SaveSlotPanel.SaveSlotIntentRequested += Controller.HandleIntent;
            Controller.ViewModelChanged += HandleControllerViewModelChanged;
            _mainMenuScreenView.SaveSlotPanel.Bind(Controller.BuildViewModel());
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
            var uiAudioPort = CreateUiAudioPort();
            _uiAudioFeedbackController = new MainMenuUiAudioFeedbackController(uiAudioPort);
            _uiAudioFeedbackController.Attach(
                _mainMenuScreenView,
                PopupController,
                _settingsOverlayController);
        }

        private void OnDestroy()
        {
            _uiAudioFeedbackController?.Dispose();
            _uiAudioFeedbackController = null;

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
            }

            _settingsOverlayController?.Dispose();
            _audioSettingsLifecycleRelay?.FlushNow();
            PopupController?.Dispose();
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

            var inputSystemUiModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiModuleType == null)
            {
                throw new InvalidOperationException("Unity Input System UI module is unavailable. Verify that the Input System package is installed.");
            }

            if (eventSystem.GetComponent(inputSystemUiModuleType) == null)
            {
                eventSystem.gameObject.AddComponent(inputSystemUiModuleType);
            }

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

        private IAudioSettingsPort CreateAudioSettingsPort()
        {
            var audioRuntimeInstaller = GetRequiredAudioRuntimeInstaller();
            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioSettingsService == null)
            {
                throw new InvalidOperationException(MissingAudioInstallerMessage);
            }

            return new AudioSettingsPortAdapter(audioRuntimeInstaller.AudioSettingsService);
        }

        private IUiAudioPort CreateUiAudioPort()
        {
            if (_uiAudioCueMap == null)
            {
                throw new InvalidOperationException(MissingUiAudioCueMapMessage);
            }

            var audioRuntimeInstaller = GetRequiredAudioRuntimeInstaller();
            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioService == null)
            {
                throw new InvalidOperationException(MissingAudioInstallerMessage);
            }

            return new UiAudioPortAdapter(audioRuntimeInstaller.AudioService, _uiAudioCueMap);
        }

        private IDisplaySettingsPort CreateDisplaySettingsPort()
        {
            var displayRuntimeInstaller = GetComponent<DisplayRuntimeInstaller>();
            if (displayRuntimeInstaller == null)
            {
                throw new InvalidOperationException(MissingDisplayInstallerMessage);
            }

            displayRuntimeInstaller.Install();
            if (displayRuntimeInstaller.DisplaySettingsService == null)
            {
                throw new InvalidOperationException(MissingDisplayInstallerMessage);
            }

            return new DisplaySettingsPortAdapter(displayRuntimeInstaller.DisplaySettingsService);
        }

        private IKeyboardBindingSettingsPort CreateKeyboardBindingSettingsPort()
        {
            if (_inputActions == null)
            {
                return NoOpKeyboardBindingSettingsPort.Instance;
            }

            return new KeyboardBindingSettingsPortAdapter(new KeyboardBindingSettingsService(_inputActions));
        }

        private AudioRuntimeInstaller GetRequiredAudioRuntimeInstaller()
        {
            var audioRuntimeInstaller = GetComponent<AudioRuntimeInstaller>();
            if (audioRuntimeInstaller == null)
            {
                throw new InvalidOperationException(MissingAudioInstallerMessage);
            }

            return audioRuntimeInstaller;
        }

        private void EnsureAudioSettingsLifecycleRelay(IAudioSettingsPort audioSettingsPort)
        {
            _audioSettingsLifecycleRelay = GetComponent<AudioSettingsLifecycleRelay>();
            if (_audioSettingsLifecycleRelay == null)
            {
                _audioSettingsLifecycleRelay = gameObject.AddComponent<AudioSettingsLifecycleRelay>();
            }

            _audioSettingsLifecycleRelay.Initialize(audioSettingsPort);
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

        // Resolve Input System keyboard state without relying on UnityEngine.Input.
        private sealed class InputSystemKeyboardBridge
        {
            private static readonly Type KeyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            private static readonly PropertyInfo CurrentKeyboardProperty = KeyboardType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            private static readonly PropertyInfo EscapeKeyProperty = KeyboardType?.GetProperty("escapeKey", BindingFlags.Public | BindingFlags.Instance);
            private static readonly PropertyInfo WasPressedThisFrameProperty =
                EscapeKeyProperty?.PropertyType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);

            public bool WasEscapePressedThisFrame()
            {
                if (CurrentKeyboardProperty == null || EscapeKeyProperty == null || WasPressedThisFrameProperty == null)
                {
                    return false;
                }

                var keyboard = CurrentKeyboardProperty.GetValue(null);
                if (keyboard == null)
                {
                    return false;
                }

                var escapeKey = EscapeKeyProperty.GetValue(keyboard);
                if (escapeKey == null)
                {
                    return false;
                }

                return WasPressedThisFrameProperty.GetValue(escapeKey) is bool pressed && pressed;
            }
        }
    }
}
