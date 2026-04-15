using System;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameplayUiFlowInstaller : MonoBehaviour
    {
        private const string RootShellObjectName = "GameplayUiCanvasRoot";
        private const string RootShellPrefabResourcePath = "UI/GameplayUiCanvasRootShell";
        private static readonly InputSystemKeyboardBridge KeyboardBridge = new();

        [SerializeField] private GameplaySceneHost _sceneHost;
        [SerializeField] private GameplayUiCanvasRootView _rootView;
        // Phase-local HUD prefab seam only. Do not expand this into a general feature-prefab registry.
        [SerializeField] private HUDRootView _hudPrefab;
        [SerializeField] private bool _installOnStart = true;

        private UiArchitectureDiagnosticsTracker _diagnosticsTracker;
        private bool _isInstalled;

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

        public GameplayScreenView GameplayScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<GameplayScreenView>() : null;

        public HelpScreenView HelpScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<HelpScreenView>() : null;

        public ObjectiveStatusScreenView ObjectiveStatusScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<ObjectiveStatusScreenView>() : null;

        public InventoryScreenView InventoryScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<InventoryScreenView>() : null;

        public SettingsScreenView SettingsScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<SettingsScreenView>() : null;

        public StageResultScreenView StageResultScreenView => ScreenLayerView != null ? ScreenLayerView.FindScreenView<StageResultScreenView>() : null;

        public PopupLayerView PopupLayerView => _rootView != null ? _rootView.PopupLayerView : null;

        public PausePopupView PausePopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<PausePopupView>() : null;

        public ObjectiveInfoPopupView ObjectiveInfoPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<ObjectiveInfoPopupView>() : null;

        public ConfirmPopupView ConfirmPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<ConfirmPopupView>() : null;

        public TooltipPopupView TooltipPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<TooltipPopupView>() : null;

        public RewardPopupView RewardPopupView => PopupLayerView != null ? PopupLayerView.FindPopupView<RewardPopupView>() : null;

        private void Start()
        {
            if (_installOnStart && _sceneHost != null)
            {
                Install(_sceneHost);
            }
        }

        private void Update()
        {
            if (_isInstalled && KeyboardBridge.WasEscapePressedThisFrame())
            {
                Coordinator.HandleBackRequested();
            }

            if (!_isInstalled || _rootView == null || _rootView.DiagnosticsOverlayView == null)
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
            PresentationSource = Ports.PresentationSource;

            var playerStatusPresenter = new PlayerStatusPresenter();
            var actionBarPresenter = new ActionBarPresenter(Ports.CommandGateway);
            var notificationPresenter = new NotificationPresenter();
            HudRootPresenter = new HUDRootPresenter(
                PresentationSource,
                playerStatusPresenter,
                actionBarPresenter,
                notificationPresenter);

            var sessionSettingsStore = new UiSessionSettingsStore();
            ScreenController = new ScreenController(new GameplayScreenRuntimeFactory(
                _rootView.ScreenLayerView,
                Ports.QueryFacade,
                PresentationSource,
                sessionSettingsStore));
            PopupController = new PopupController(new GameplayPopupRuntimeFactory(_rootView.PopupLayerView));
            HudController = new HUDController(
                HudRootPresenter.ViewModel,
                playerStatusPresenter.ViewModel,
                actionBarPresenter.ViewModel,
                notificationPresenter.ViewModel,
                actionBarPresenter);
            BlockPolicy = new UIBlockPolicy();
            Coordinator = new UIFlowCoordinator(
                ScreenController,
                PopupController,
                BlockPolicy,
                Ports.PauseService,
                PresentationSource);

            HudController.AttachView(_rootView.HudView);
            WireViewEvents();
            WireControllerEvents();
            Coordinator.Initialize();
            SyncViews();
            SetupDiagnostics();

            _isInstalled = true;
        }

        private void OnDestroy()
        {
            UnwireViewEvents();
            UnwireControllerEvents();
            _diagnosticsTracker?.Dispose();
            Coordinator?.Dispose();
            PopupController?.Dispose();
            ScreenController?.Dispose();
            HudController?.Dispose();
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
                isHudReadOnly: () => HudController != null && !HudController.ActionBarViewModel.IsInteractive,
                inventoryViewAccessor: () => ScreenLayerView != null ? ScreenLayerView.FindScreenView<InventoryScreenView>() : null);
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
