using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [SerializeField] private MainMenuScreenView _mainMenuScreenView;
        [SerializeField] private MainMenuScreenView _mainMenuScreenPrefab;
        [SerializeField] private PopupLayerView _popupLayerView;
        [SerializeField] private PopupPrefabCatalog _popupPrefabCatalog;
        [SerializeField] private GameplayStageLaunchRouteConfig _routeConfig;
        [SerializeField] private ScriptableObjectStageCatalogProvider _stageCatalogProvider;
        [SerializeField] private CampaignStageSequenceDefinition _campaignStageSequenceDefinition;
        [SerializeField] private bool _installOnStart = true;

        private IConfirmPopupPort _confirmPopupPort;
        private bool _isInstalled;

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
            BuildSaveSlotModule();
            BuildHubModule();
            _mainMenuScreenView.SetVisible(true);
            _popupLayerView.SetState(false, false, false, PopupBackdropMode.None);
            _isInstalled = true;
        }

        private void BuildPopupModule()
        {
            PopupController = new PopupController(new GameplayPopupRuntimeFactory(_popupLayerView, _popupPrefabCatalog));
            PopupController.StateChanged += SyncPopupLayer;
            _popupLayerView.BackdropClicked += HandlePopupBackdropClicked;
            _confirmPopupPort = new ConfirmPopupPortAdapter(PopupController);
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
                NoOpMainMenuSettingsPort.Instance,
                new UnityApplicationQuitPort(),
                _confirmPopupPort,
                _mainMenuScreenView.ShowSection);

            _mainMenuScreenView.CommandRequested += HubController.HandleCommand;
            _mainMenuScreenView.NavigationRequested += HubController.HandleNavigation;
            _mainMenuScreenView.ShowSection(MainMenuSectionId.SaveSlots);
        }

        private void OnDestroy()
        {
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

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
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
    }
}
