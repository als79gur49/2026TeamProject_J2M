using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Timing;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class GameplayShowcaseSceneInstallerBase : MonoBehaviour
    {
        protected readonly struct InitialGameplayState
        {
            public InitialGameplayState(
                BoardBounds boardBounds,
                CubeTopologyState initialTopology,
                EntityState[] initialEntities,
                GameplayTerrainData initialTerrain,
                int playerEntityId,
                EnemyAiProfileOverride[] enemyAiProfileOverrides,
                EnemyPresentationBinding[] enemyPresentationBindings,
                StaticEntityPresentationBinding[] staticEntityPresentationBindings)
            {
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                InitialEntities = initialEntities ?? Array.Empty<EntityState>();
                InitialTerrain = initialTerrain ?? GameplayTerrainData.Empty;
                PlayerEntityId = playerEntityId;
                EnemyAiProfileOverrides = enemyAiProfileOverrides ?? Array.Empty<EnemyAiProfileOverride>();
                EnemyPresentationBindings = enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
                StaticEntityPresentationBindings = staticEntityPresentationBindings ?? Array.Empty<StaticEntityPresentationBinding>();
            }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public EntityState[] InitialEntities { get; }

            public GameplayTerrainData InitialTerrain { get; }

            public int PlayerEntityId { get; }

            public EnemyAiProfileOverride[] EnemyAiProfileOverrides { get; }

            public EnemyPresentationBinding[] EnemyPresentationBindings { get; }

            public StaticEntityPresentationBinding[] StaticEntityPresentationBindings { get; }
        }

        [Header("Bootstrap")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private bool autoAdvanceTicks = true;
        [SerializeField] private bool autoCreateViews = true;
        [SerializeField] private bool configureMainCamera = true;
        [SerializeField] private float cellSize = 1.15f;
        [SerializeField] private float moveDeadzone = 0.5f;
        [SerializeField] private bool directionChangeConsumesDelay;

        [Header("Timing")]
        [SerializeField] private GameplaySimulationTimingPreset simulationTimingPreset;
        [SerializeField] private GameplayPresentationTimingPreset presentationTimingPreset;

        [Header("Presentation")]
        [SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX;
        [SerializeField] private TopologyRotationTweenSettings topologyRotationTweenSettings = TopologyRotationTweenSettings.CreateDefault();

        protected bool AutoCreateViews => autoCreateViews;

        protected float CellSize => cellSize;

        protected virtual void Awake()
        {
            if (actions == null)
            {
                throw new InvalidOperationException($"{GetType().Name} requires an InputActionAsset reference.");
            }

            var initialState = BuildInitialGameplayState();
            var cameraSettings = CreateCameraSettings();
            GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                gameObject,
                GetShowcaseOverlayContent(),
                cameraSettings);

            if (configureMainCamera)
            {
                ConfigureCamera(initialState);
            }

            var host = GetComponent<GameplaySceneHost>() ?? gameObject.AddComponent<GameplaySceneHost>();
            host.Initialize(CreateConfiguration(initialState, cameraSettings));
        }

        public GameplayShowcaseOverlayContent GetShowcaseOverlayContent()
        {
            return CreateShowcaseOverlayContent();
        }

        protected virtual GameplayCameraSettings CreateCameraSettings()
        {
            return GameplayCameraSettings.CreateShowcaseDefault();
        }

        protected virtual IGameplayEntityViewFactory CreateViewFactory(
            GameplayBoardRoot boardRoot,
            in InitialGameplayState initialState)
        {
            if (!autoCreateViews || boardRoot == null)
            {
                return null;
            }

            return new DefaultGameplayEntityViewFactory(
                boardRoot.EntityRoot,
                cellSize,
                initialState.PlayerEntityId,
                ResolvePlayerViewPrefab(),
                ResolveEnemyViewPrefabs(initialState.EnemyPresentationBindings),
                ResolveStaticEntityViewPrefabs(initialState.StaticEntityPresentationBindings));
        }

        protected virtual EnemyAiProfile ResolveDefaultEnemyAiProfile()
        {
            return null;
        }

        protected virtual GameplayEntityView ResolvePlayerViewPrefab()
        {
            return null;
        }

        protected virtual EnemyPresentationCatalog ResolveEnemyPresentationCatalog()
        {
            return null;
        }

        protected virtual StaticEntityPresentationCatalog ResolveStaticEntityPresentationCatalog()
        {
            return null;
        }

        protected abstract InitialGameplayState BuildInitialGameplayState();

        protected abstract GameplayShowcaseOverlayContent CreateShowcaseOverlayContent();

        public GameplayCameraSettings GetCameraSettings()
        {
            return CreateCameraSettings();
        }

        public void ConfigureBootstrapCamera(Camera camera)
        {
            var initialState = BuildInitialGameplayState();
            ConfigureSceneCamera(camera, initialState.BoardBounds, CreateCameraSettings(), initialState.InitialTopology);
        }

        private void ConfigureCamera(InitialGameplayState initialState)
        {
            var camera = Camera.main;
            ConfigureSceneCamera(
                camera,
                initialState.BoardBounds,
                CreateCameraSettings(),
                initialState.InitialTopology);
        }

        private GameplaySceneHostConfiguration CreateConfiguration(
            InitialGameplayState initialState,
            GameplayCameraSettings cameraSettings)
        {
            return CreateConfiguration(initialState, cameraSettings, ResolveViewFactory(initialState));
        }

        private GameplaySceneHostConfiguration CreateConfiguration(
            InitialGameplayState initialState,
            GameplayCameraSettings cameraSettings,
            IGameplayEntityViewFactory viewFactory)
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                Actions = actions,
                AutoAdvanceTicks = autoAdvanceTicks,
                AutoCreateViews = autoCreateViews,
                CameraSettings = cameraSettings,
                CellSize = cellSize,
                DefaultEnemyAiProfile = ResolveDefaultEnemyAiProfile(),
                DirectionChangeConsumesDelay = directionChangeConsumesDelay,
                EnemyAiProfileOverrides = initialState.EnemyAiProfileOverrides,
                EnemyPresentationBindings = initialState.EnemyPresentationBindings,
                EnemyPresentationCatalog = ResolveEnemyPresentationCatalog(),
                StaticEntityPresentationBindings = initialState.StaticEntityPresentationBindings,
                StaticEntityPresentationCatalog = ResolveStaticEntityPresentationCatalog(),
                InitialBoardBounds = initialState.BoardBounds,
                InitialEntities = initialState.InitialEntities,
                InitialTerrain = initialState.InitialTerrain,
                InitialTopology = initialState.InitialTopology,
                MoveDeadzone = moveDeadzone,
                PlayerEntityId = initialState.PlayerEntityId,
                PlayerViewPrefab = viewFactory == null ? ResolvePlayerViewPrefab() : null,
                SnapViewCameraToTarget = configureMainCamera,
                TopologyRotationVisualMapping = topologyRotationVisualMapping,
                TopologyRotationTween = topologyRotationTweenSettings,
                ViewCamera = configureMainCamera ? Camera.main : null,
                ViewFactory = viewFactory,
            };

            ResolveSimulationTimingPreset().ApplyTo(configuration);
            ResolvePresentationTimingPreset().ApplyTo(configuration);
            return configuration;
        }

        protected IReadOnlyDictionary<int, GameplayEntityView> ResolveEnemyViewPrefabs(
            EnemyPresentationBinding[] enemyPresentationBindings)
        {
            return EnemyPresentationCatalogResolver.BuildEnemyViewPrefabs(
                ResolveEnemyPresentationCatalog(),
                enemyPresentationBindings,
                GetType().Name);
        }

        protected IReadOnlyDictionary<int, GameplayEntityView> ResolveStaticEntityViewPrefabs(
            StaticEntityPresentationBinding[] staticEntityPresentationBindings)
        {
            return StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                ResolveStaticEntityPresentationCatalog(),
                staticEntityPresentationBindings,
                GetType().Name);
        }

        private IGameplayEntityViewFactory ResolveViewFactory(in InitialGameplayState initialState)
        {
            var boardRoot = GetComponentInChildren<GameplayBoardRoot>(includeInactive: true);
            return CreateViewFactory(boardRoot, initialState);
        }

        private GameplaySimulationTimingPreset ResolveSimulationTimingPreset()
        {
            if (simulationTimingPreset == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} on '{name}' requires a {nameof(GameplaySimulationTimingPreset)} reference.");
            }

            return simulationTimingPreset;
        }

        private GameplayPresentationTimingPreset ResolvePresentationTimingPreset()
        {
            if (presentationTimingPreset == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} on '{name}' requires a {nameof(GameplayPresentationTimingPreset)} reference.");
            }

            return presentationTimingPreset;
        }

        private void ConfigureSceneCamera(
            Camera camera,
            BoardBounds boardBounds,
            GameplayCameraSettings cameraSettings,
            CubeTopologyState topology)
        {
            if (camera == null)
            {
                return;
            }

            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            GameplayShowcaseSceneScaffold.ConfigureDefaultSceneCamera(
                camera,
                cameraSettings,
                Vector3.zero,
                projector.GetVisibleCubeBounds(topology));
        }
    }
}
