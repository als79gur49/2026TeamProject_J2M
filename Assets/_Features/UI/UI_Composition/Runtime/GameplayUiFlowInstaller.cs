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

        private bool _isInstalled;

        public GameplayUiFlowPorts Ports { get; private set; }

        public GameplayUiCanvasRootView RootView => _rootView;

        public ScreenController ScreenController { get; private set; }

        public PopupController PopupController { get; private set; }

        public HUDController HudController { get; private set; }

        public ObjectiveStatusScreenController ObjectiveStatusScreenController { get; private set; }

        public UIBlockPolicy BlockPolicy { get; private set; }

        public UIFlowCoordinator Coordinator { get; private set; }

        public GameplayHudView HudView => _rootView != null ? _rootView.HudView : null;

        public GameplayScreenView GameplayScreenView => _rootView != null ? _rootView.GameplayScreenView : null;

        public HelpScreenView HelpScreenView => _rootView != null ? _rootView.HelpScreenView : null;

        public ObjectiveStatusScreenView ObjectiveStatusScreenView => _rootView != null ? _rootView.ObjectiveStatusScreenView : null;

        public PausePopupView PausePopupView => _rootView != null ? _rootView.PausePopupView : null;

        public ObjectiveInfoPopupView ObjectiveInfoPopupView => _rootView != null ? _rootView.ObjectiveInfoPopupView : null;

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
                sceneHost.UiAccess.PresentationFeed,
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

            var hudPresenter = new GameplayHudPresenter(
                Ports.QueryFacade,
                Ports.CommandGateway,
                Ports.PresentationFeed,
                Ports.GameplayPauseService);
            var objectivePresenter = new ObjectiveStatusPresenter(
                Ports.QueryFacade,
                Ports.PresentationFeed,
                Ports.GameplayPauseService);

            ScreenController = new ScreenController();
            PopupController = new PopupController();
            HudController = new HUDController(hudPresenter);
            ObjectiveStatusScreenController = new ObjectiveStatusScreenController(objectivePresenter);
            BlockPolicy = new UIBlockPolicy();
            Coordinator = new UIFlowCoordinator(
                ScreenController,
                PopupController,
                HudController,
                BlockPolicy,
                Ports.PauseService);

            HudController.AttachView(_rootView.HudView);
            ObjectiveStatusScreenController.AttachView(_rootView.ObjectiveStatusScreenView);
            WireViewEvents();
            WireControllerEvents();
            Coordinator.Initialize();
            SyncViews();

            _isInstalled = true;
        }

        private void OnDestroy()
        {
            UnwireViewEvents();
            UnwireControllerEvents();
            Coordinator?.Dispose();
            HudController?.Dispose();
            ObjectiveStatusScreenController?.Dispose();
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

        private void WireViewEvents()
        {
            _rootView.HudView.PauseRequested += HandlePauseRequested;
            _rootView.GameplayScreenView.HelpRequested += HandleHelpRequested;
            _rootView.GameplayScreenView.ObjectivesRequested += HandleObjectiveStatusRequested;
            _rootView.HelpScreenView.BackRequested += HandleBackRequested;
            _rootView.PausePopupView.ResumeRequested += HandleResumeRequested;
            _rootView.ObjectiveInfoPopupView.CloseRequested += HandleBackRequested;
            ObjectiveStatusScreenController.BackRequested += HandleBackRequested;
            ObjectiveStatusScreenController.InfoRequested += HandleObjectiveInfoRequested;
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

            if (_rootView.GameplayScreenView != null)
            {
                _rootView.GameplayScreenView.HelpRequested -= HandleHelpRequested;
                _rootView.GameplayScreenView.ObjectivesRequested -= HandleObjectiveStatusRequested;
            }

            if (_rootView.HelpScreenView != null)
            {
                _rootView.HelpScreenView.BackRequested -= HandleBackRequested;
            }

            if (_rootView.PausePopupView != null)
            {
                _rootView.PausePopupView.ResumeRequested -= HandleResumeRequested;
            }

            if (_rootView.ObjectiveInfoPopupView != null)
            {
                _rootView.ObjectiveInfoPopupView.CloseRequested -= HandleBackRequested;
            }

            if (ObjectiveStatusScreenController != null)
            {
                ObjectiveStatusScreenController.BackRequested -= HandleBackRequested;
                ObjectiveStatusScreenController.InfoRequested -= HandleObjectiveInfoRequested;
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

        private void HandleHelpRequested()
        {
            Coordinator.OpenHelpScreen();
        }

        private void HandleObjectiveStatusRequested()
        {
            Coordinator.OpenObjectiveStatusScreen();
        }

        private void HandleObjectiveInfoRequested()
        {
            var content = ObjectiveStatusScreenController.BuildInfoContent();
            _rootView.ObjectiveInfoPopupView.SetContent(content.Title, content.Body);
            Coordinator.RequestObjectiveInfoPopup();
        }

        private void HandleBackRequested()
        {
            Coordinator.HandleBackRequested();
        }

        private void HandleResumeRequested()
        {
            Coordinator.HandleBackRequested();
        }

        private void SyncViews()
        {
            if (_rootView == null || ScreenController == null || PopupController == null)
            {
                return;
            }

            _rootView.HudView.IsVisible = true;
            _rootView.GameplayScreenView.IsVisible = ScreenController.CurrentScreenId == ScreenId.Gameplay;
            _rootView.HelpScreenView.IsVisible = ScreenController.CurrentScreenId == ScreenId.Help;
            ObjectiveStatusScreenController.SetVisible(ScreenController.CurrentScreenId == ScreenId.ObjectiveStatus);
            _rootView.PausePopupView.IsVisible = PopupController.Contains(PopupId.Pause);
            _rootView.ObjectiveInfoPopupView.IsVisible = PopupController.Contains(PopupId.ObjectiveInfo);
        }
    }
}
