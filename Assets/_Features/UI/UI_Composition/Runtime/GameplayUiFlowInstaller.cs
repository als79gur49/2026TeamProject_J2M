using System;
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
        [SerializeField] private GameplaySceneHost _sceneHost;
        [SerializeField] private GameplayUiCanvasRootView _rootView;
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
            if (_isInstalled && Input.GetKeyDown(KeyCode.Escape))
            {
                Coordinator.HandleBackRequested();
            }

            if (!_isInstalled || _rootView == null || _rootView.DiagnosticsOverlayView == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                _rootView.DiagnosticsOverlayView.ToggleVisibility();
            }

            if (Input.GetKeyDown(KeyCode.F4))
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
                var child = transform.Find("GameplayUiCanvasRoot");
                if (child == null)
                {
                    var childObject = new GameObject("GameplayUiCanvasRoot", typeof(RectTransform));
                    childObject.transform.SetParent(transform, false);
                    child = childObject.transform;
                }

                _rootView = child.GetComponent<GameplayUiCanvasRootView>() ??
                            child.gameObject.AddComponent<GameplayUiCanvasRootView>();
            }

            _rootView.EnsureHierarchy();
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
    }
}
