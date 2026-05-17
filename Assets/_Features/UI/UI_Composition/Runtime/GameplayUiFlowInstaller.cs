using System;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using Game.Shared.Display;
using Game.Shared.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameplayUiFlowInstaller : MonoBehaviour, IStageLaunchRouterProvider
    {
        private const string MissingAudioInstallerMessage =
            "GameplayUiFlowInstaller requires a co-located AudioRuntimeInstaller on the canonical bootstrap root for SettingsScreen audio controls.";
        private const string MissingDisplayInstallerMessage =
            "GameplayUiFlowInstaller requires a co-located DisplayRuntimeInstaller on the canonical bootstrap root for SettingsScreen display controls.";
        private const string MissingUiAudioCueMapMessage =
            "GameplayUiFlowInstaller requires a serialized UiAudioCueMap on the canonical bootstrap root for UI SFX v1.";
        private const string RootShellObjectName = "GameplayUiCanvasRoot";
        private const string RootShellPrefabResourcePath = "UI/GameplayUiCanvasRootShell";
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
        [SerializeField] private bool _installOnStart = true;

        private UiArchitectureDiagnosticsTracker _diagnosticsTracker;
        private AudioSettingsLifecycleRelay _audioSettingsLifecycleRelay;
        private DisplayPreviewTimeoutRelay _displayPreviewTimeoutRelay;
        private DisplaySettingsLifecycleRelay _displaySettingsLifecycleRelay;
        private bool _isInstalled;
        private IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private UiNavigationInputRouter _navigationInputRouter;
        private StageResultAutoNextDriver _stageResultAutoNextDriver;
        private HudUiAudioFeedbackController _hudUiAudioFeedbackController;

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

        public ObjectiveStatusScreenView ObjectiveStatusScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<ObjectiveStatusScreenView>() : null;

        public SettingsScreenView SettingsScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<SettingsScreenView>() : null;

        public StageResultScreenView StageResultScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<StageResultScreenView>() : null;

        public LevelFailedScreenView LevelFailedScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<LevelFailedScreenView>() : null;

        public PopupLayerView PopupLayerView => _rootView != null ? _rootView.PopupLayerView : null;

        public PausePopupView PausePopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<PausePopupView>() : null;

        public ObjectiveInfoPopupView ObjectiveInfoPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<ObjectiveInfoPopupView>() : null;

        public ConfirmPopupView ConfirmPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<ConfirmPopupView>() : null;

        public TooltipPopupView TooltipPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<TooltipPopupView>() : null;

        public RewardPopupView RewardPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<RewardPopupView>() : null;

        public bool TryCreateStageLaunchRouter(string currentSceneName, out IStageLaunchRouter router)
        {
            router = new CurrentSceneStageLaunchRouter(currentSceneName);
            return true;
        }

        private void Start()
        {
            if (_installOnStart && _sceneHost != null)
            {
                Install(_sceneHost);
            }
        }

        private void Update()
        {
            if (_isInstalled)
            {
                _stageResultAutoNextDriver?.Tick(Time.unscaledDeltaTime);
            }

            if (!_isInstalled || _rootView == null || _rootView.DiagnosticsOverlayView == null)
            {
                return;
            }

            if (_keyboardBindingSettingsPort != null && _keyboardBindingSettingsPort.IsRebinding)
            {
                return;
            }

            if (KeyboardBridge.WasF3PressedThisFrame())
            {
                _rootView.DiagnosticsOverlayView.ToggleVisibility();
            }

            if (KeyboardBridge.WasF4PressedThisFrame())
            {
                _rootView.DiagnosticsOverlayView.ToggleExpanded();
            }
        }

        public void Install(GameplaySceneHost sceneHost)
        {
            if (sceneHost == null)
            {
                throw new ArgumentNullException(nameof(sceneHost));
            }

            if (sceneHost.UiAccess == null)
            {
                throw new InvalidOperationException("GameplaySceneHost must be initialized before installing UI flow.");
            }

            Install(new GameplayUiFlowPorts(
                sceneHost.UiAccess.CommandGateway,
                sceneHost.UiAccess.QueryFacade,
                new GameplayUiPresentationSource(
                    sceneHost.UiAccess.QueryFacade,
                    sceneHost.UiAccess.PresentationFeed,
                    sceneHost.UiAccess.PauseService),
                sceneHost.UiAccess.PauseService));

            _sceneHost = null;
        }

        public void Install(GameplayUiFlowPorts ports)
        {
            if (_isInstalled)
            {
                return;
            }

            Ports = ports;
            EnsureRootView();
            EnsureScreenPrefabCatalog();
            EnsurePopupPrefabCatalog();
            PresentationSource = Ports.PresentationSource;
            var audioSettingsPort = CreateAudioSettingsPort();
            var displaySettingsPort = CreateDisplaySettingsPort();
            _keyboardBindingSettingsPort = CreateKeyboardBindingSettingsPort();
            var uiAudioPort = CreateUiAudioPort();
            EnsureAudioSettingsLifecycleRelay(audioSettingsPort);
            EnsureDisplayPreviewTimeoutRelay();
            EnsureDisplaySettingsLifecycleRelay();

            PopupController = new PopupController(new GameplayPopupRuntimeFactory(
                _rootView.PopupLayerView,
                _popupPrefabCatalog));
            var displayPreviewSessionHost = new DisplayPreviewSessionHost(
                PopupController,
                _displayPreviewTimeoutRelay);

            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var objectiveHudPresenter = new ObjectiveHudPresenter();
            var chancePanelPresenter = new ChancePanelPresenter();
            var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
            var notificationPresenter = new NotificationPresenter();
            HudRootPresenter = new HUDRootPresenter(
                PresentationSource,
                stageInfoPresenter,
                objectiveHudPresenter,
                chancePanelPresenter,
                surfaceBeltIndicatorPresenter,
                playerStatusPresenter,
                notificationPresenter);

            var accessibilitySettingsStore = new AccessibilitySettingsStore();
            ScreenController = new ScreenController(new GameplayScreenRuntimeFactory(
                _rootView.ScreenLayerView,
                Ports.QueryFacade,
                PresentationSource,
                accessibilitySettingsStore,
                audioSettingsPort,
                displaySettingsPort,
                _keyboardBindingSettingsPort,
                uiAudioPort,
                displayPreviewSessionHost,
                _displaySettingsLifecycleRelay,
                _screenPrefabCatalog));
            HudController = new HUDController(
                HudRootPresenter.ViewModel,
                stageInfoPresenter.ViewModel,
                objectiveHudPresenter.ViewModel,
                chancePanelPresenter.ViewModel,
                surfaceBeltIndicatorPresenter.ViewModel,
                playerStatusPresenter.ViewModel,
                notificationPresenter.ViewModel);
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
                CreateMainMenuReturnRouter());
            _stageResultAutoNextDriver = new StageResultAutoNextDriver(
                ScreenController,
                PopupController,
                new CurrentSceneStageLaunchRouter(gameObject.scene.name));
            displayPreviewSessionHost.BindAudioIntentBoundary(Coordinator);

            HudController.AttachView(_rootView.HudView);
            WireViewEvents();
            WireControllerEvents();
            Coordinator.Initialize();
            EnsureNavigationInputRouter();
            SyncViews();
            SetupDiagnostics();

            _isInstalled = true;
        }

        private void OnDestroy()
        {
            UnwireViewEvents();
            UnwireControllerEvents();
            _audioSettingsLifecycleRelay?.FlushNow();
            _diagnosticsTracker?.Dispose();
            Coordinator?.Dispose();
            _stageResultAutoNextDriver?.Dispose();
            ScreenController?.Dispose();
            PopupController?.Dispose();
            HudController?.Dispose();
            _hudUiAudioFeedbackController?.Dispose();
            HudRootPresenter?.Dispose();
            (PresentationSource as IDisposable)?.Dispose();
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
            var actions = _sceneHost != null && _sceneHost.InputHost != null
                ? _sceneHost.InputHost.Actions
                : _inputActions;
            if (actions == null)
            {
                return NoOpKeyboardBindingSettingsPort.Instance;
            }

            return new KeyboardBindingSettingsPortAdapter(new KeyboardBindingSettingsService(actions));
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

        private IMainMenuReturnRouter CreateMainMenuReturnRouter()
        {
            return _routeConfig != null
                ? new ConfiguredMainMenuReturnRouter(_routeConfig)
                : NoOpMainMenuReturnRouter.Instance;
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
                () => false);
        }

        private InputActionAsset ResolveUiInputActions()
        {
            return _sceneHost != null && _sceneHost.InputHost != null
                ? _sceneHost.InputHost.Actions
                : _inputActions;
        }

        private void SetupDiagnostics()
        {
            if (_rootView == null || _rootView.DiagnosticsOverlayView == null)
            {
                return;
            }

            _rootView.DiagnosticsOverlayView.SetSupported(UiArchitectureDiagnosticsTracker.IsRuntimeSupported);
            if (!UiArchitectureDiagnosticsTracker.IsRuntimeSupported)
            {
                return;
            }

            _diagnosticsTracker = new UiArchitectureDiagnosticsTracker(
                PresentationSource,
                Coordinator,
                ScreenController,
                PopupController,
                isHudVisible: () => _rootView.HudView != null && _rootView.HudView.IsVisible,
                isHudReadOnly: () => HudController != null && HudController.IsGameplayReadOnly);
            _rootView.DiagnosticsOverlayView.Bind(_diagnosticsTracker);
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
            private static readonly PropertyInfo F3KeyProperty = KeyboardType?.GetProperty("f3Key", BindingFlags.Public | BindingFlags.Instance);
            private static readonly PropertyInfo F4KeyProperty = KeyboardType?.GetProperty("f4Key", BindingFlags.Public | BindingFlags.Instance);
            private static readonly PropertyInfo WasPressedThisFrameProperty =
                EscapeKeyProperty?.PropertyType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);

            public bool WasEscapePressedThisFrame()
            {
                return WasPressedThisFrame(EscapeKeyProperty);
            }

            public bool WasF3PressedThisFrame()
            {
                return WasPressedThisFrame(F3KeyProperty);
            }

            public bool WasF4PressedThisFrame()
            {
                return WasPressedThisFrame(F4KeyProperty);
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
