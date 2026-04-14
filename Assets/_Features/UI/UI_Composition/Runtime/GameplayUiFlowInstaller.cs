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
        [SerializeField] private bool _installOnStart = true;

        private GameplayHudView _hudView;
        private HelpScreenView _helpScreenView;
        private PausePopupView _pausePopupView;
        private GameplayScreenView _gameplayScreenView;
        private bool _isInstalled;

        public GameplayUiFlowPorts Ports { get; private set; }

        public ScreenController ScreenController { get; private set; }

        public PopupController PopupController { get; private set; }

        public HUDController HudController { get; private set; }

        public UIBlockPolicy BlockPolicy { get; private set; }

        public UIFlowCoordinator Coordinator { get; private set; }

        public GameplayHudView HudView => _hudView;

        public GameplayScreenView GameplayScreenView => _gameplayScreenView;

        public HelpScreenView HelpScreenView => _helpScreenView;

        public PausePopupView PausePopupView => _pausePopupView;

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
            if (_isInstalled)
            {
                return;
            }

            if (sceneHost == null)
            {
                throw new ArgumentNullException(nameof(sceneHost));
            }

            if (sceneHost.UiAccess == null)
            {
                throw new InvalidOperationException("GameplaySceneHost must be initialized before installing UI flow.");
            }

            Ports = new GameplayUiFlowPorts(
                sceneHost.UiAccess.CommandGateway,
                sceneHost.UiAccess.QueryFacade,
                sceneHost.UiAccess.PresentationFeed,
                sceneHost.UiAccess.PauseService);

            EnsureViews();

            var presenter = new GameplayHudPresenter(
                Ports.QueryFacade,
                Ports.CommandGateway,
                Ports.PresentationFeed,
                Ports.PauseService);

            ScreenController = new ScreenController();
            PopupController = new PopupController();
            HudController = new HUDController(presenter);
            BlockPolicy = new UIBlockPolicy();
            Coordinator = new UIFlowCoordinator(
                ScreenController,
                PopupController,
                HudController,
                BlockPolicy,
                Ports.PauseService);

            HudController.AttachView(_hudView);
            WireViewEvents();
            WireControllerEvents();
            Coordinator.Initialize();
            SyncViews();

            _sceneHost = null;
            _isInstalled = true;
        }

        private void OnDestroy()
        {
            UnwireViewEvents();
            UnwireControllerEvents();
            Coordinator?.Dispose();
            HudController?.Dispose();
        }

        private void EnsureViews()
        {
            _hudView = EnsureChildComponent<GameplayHudView>("GameplayHudView");
            _gameplayScreenView = EnsureChildComponent<GameplayScreenView>("GameplayScreenView");
            _helpScreenView = EnsureChildComponent<HelpScreenView>("HelpScreenView");
            _pausePopupView = EnsureChildComponent<PausePopupView>("PausePopupView");
        }

        private T EnsureChildComponent<T>(string childName)
            where T : Component
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                var childObject = new GameObject(childName);
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }

        private void WireViewEvents()
        {
            _hudView.PauseRequested += HandlePauseRequested;
            _gameplayScreenView.HelpRequested += HandleHelpRequested;
            _helpScreenView.BackRequested += HandleBackRequested;
            _pausePopupView.ResumeRequested += HandleResumeRequested;
        }

        private void WireControllerEvents()
        {
            ScreenController.StateChanged += SyncViews;
            PopupController.StateChanged += SyncViews;
        }

        private void UnwireViewEvents()
        {
            if (_hudView != null)
            {
                _hudView.PauseRequested -= HandlePauseRequested;
            }

            if (_gameplayScreenView != null)
            {
                _gameplayScreenView.HelpRequested -= HandleHelpRequested;
            }

            if (_helpScreenView != null)
            {
                _helpScreenView.BackRequested -= HandleBackRequested;
            }

            if (_pausePopupView != null)
            {
                _pausePopupView.ResumeRequested -= HandleResumeRequested;
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
            if (ScreenController == null || PopupController == null)
            {
                return;
            }

            _hudView.IsVisible = true;
            _gameplayScreenView.IsVisible = ScreenController.CurrentScreenId == ScreenId.Gameplay;
            _helpScreenView.IsVisible = ScreenController.CurrentScreenId == ScreenId.Help;
            _pausePopupView.IsVisible = PopupController.Contains(PopupId.Pause);
        }
    }
}
