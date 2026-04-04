using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
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
                EnemyAiProfileOverride[] enemyAiProfileOverrides)
            {
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                InitialEntities = initialEntities ?? Array.Empty<EntityState>();
                InitialTerrain = initialTerrain ?? GameplayTerrainData.Empty;
                PlayerEntityId = playerEntityId;
                EnemyAiProfileOverrides = enemyAiProfileOverrides ?? Array.Empty<EnemyAiProfileOverride>();
            }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public EntityState[] InitialEntities { get; }

            public GameplayTerrainData InitialTerrain { get; }

            public int PlayerEntityId { get; }

            public EnemyAiProfileOverride[] EnemyAiProfileOverrides { get; }
        }

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private bool autoAdvanceTicks = true;
        [SerializeField] private bool autoCreateViews = true;
        [SerializeField] private bool configureMainCamera = true;
        [SerializeField] private float cellSize = 1.15f;
        [SerializeField] private bool directionChangeConsumesDelay;
        [SerializeField] private float initialMoveDelaySeconds = GameplayTimingProfile.DefaultInitialMoveDelaySeconds;
        [SerializeField] private float moveDeadzone = 0.5f;
        [SerializeField] private int playerEntityId = 10;
        [SerializeField] private PlayerControlTimingSettings playerControlTiming = PlayerControlTimingSettings.CreateDefault();
        [SerializeField, HideInInspector] private float playerMoveCooldownSeconds = -1f;
        [SerializeField] private float repeatedMoveIntervalSeconds = GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds;
        [SerializeField] private float boxSlideStepIntervalSeconds = GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds;
        [SerializeField] private float projectileStepIntervalSeconds = GameplayTimingProfile.DefaultProjectileStepIntervalSeconds;
        [SerializeField] private float moveMotionDurationSeconds = -1f;
        [SerializeField] private float pushMotionDurationSeconds = GameplayTimingProfile.DefaultPushMotionDurationSeconds;
        [SerializeField] private float flipMotionDurationSeconds = GameplayTimingProfile.DefaultFlipMotionDurationSeconds;
        [SerializeField] private float topologyMotionDurationSeconds = -1f;
        [SerializeField] private TopologyRotationVisualMapping topologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX;

        protected bool AutoCreateViews => autoCreateViews;

        protected int PlayerEntityId => playerEntityId;

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

        protected virtual IGameplayEntityViewFactory CreateViewFactory(GameplayBoardRoot boardRoot)
        {
            if (!autoCreateViews || boardRoot == null)
            {
                return null;
            }

            return new DefaultGameplayEntityViewFactory(
                boardRoot.EntityRoot,
                cellSize,
                playerEntityId,
                ResolvePlayerViewPrefab());
        }

        protected virtual EnemyAiProfile ResolveDefaultEnemyAiProfile()
        {
            return null;
        }

        protected virtual GameplayEntityView ResolvePlayerViewPrefab()
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
            return CreateConfiguration(initialState, cameraSettings, ResolveViewFactory());
        }

        private GameplaySceneHostConfiguration CreateConfiguration(
            InitialGameplayState initialState,
            GameplayCameraSettings cameraSettings,
            IGameplayEntityViewFactory viewFactory)
        {
            return new GameplaySceneHostConfiguration
            {
                Actions = actions,
                AutoAdvanceTicks = autoAdvanceTicks,
                AutoCreateViews = autoCreateViews,
                CameraSettings = cameraSettings,
                CellSize = cellSize,
                BoxSlideStepIntervalSeconds = boxSlideStepIntervalSeconds,
                DefaultEnemyAiProfile = ResolveDefaultEnemyAiProfile(),
                DirectionChangeConsumesDelay = directionChangeConsumesDelay,
                EnemyAiProfileOverrides = initialState.EnemyAiProfileOverrides,
                FlipMotionDurationSeconds = flipMotionDurationSeconds,
                InitialBoardBounds = initialState.BoardBounds,
                InitialMoveDelaySeconds = initialMoveDelaySeconds,
                InitialEntities = initialState.InitialEntities,
                InitialTerrain = initialState.InitialTerrain,
                InitialTopology = initialState.InitialTopology,
                MoveDeadzone = moveDeadzone,
                PlayerEntityId = initialState.PlayerEntityId,
                PlayerControlTiming = CreatePlayerControlTimingSettings(),
                PlayerViewPrefab = ResolvePlayerViewPrefab(),
                ProjectileStepIntervalSeconds = projectileStepIntervalSeconds,
                MoveMotionDurationSeconds = moveMotionDurationSeconds,
                PushMotionDurationSeconds = pushMotionDurationSeconds,
                RepeatedMoveIntervalSeconds = repeatedMoveIntervalSeconds,
                SnapViewCameraToTarget = configureMainCamera,
                TopologyMotionDurationSeconds = topologyMotionDurationSeconds,
                TopologyRotationVisualMapping = topologyRotationVisualMapping,
                ViewCamera = configureMainCamera ? Camera.main : null,
                ViewFactory = viewFactory,
            };
        }

        private PlayerControlTimingSettings CreatePlayerControlTimingSettings()
        {
            var resolvedSettings = playerControlTiming?.Clone() ?? PlayerControlTimingSettings.CreateDefault();

            if (resolvedSettings.MoveCooldownSeconds < 0f &&
                playerMoveCooldownSeconds >= 0f)
            {
                resolvedSettings.MoveCooldownSeconds = playerMoveCooldownSeconds;
            }

            return resolvedSettings;
        }

        private IGameplayEntityViewFactory ResolveViewFactory()
        {
            var boardRoot = GetComponentInChildren<GameplayBoardRoot>(includeInactive: true);
            return CreateViewFactory(boardRoot);
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
