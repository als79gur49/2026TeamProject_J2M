using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Game.Feature.Gameplay.Timing;
using Game.Feature.Stages;
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
                TileFeatureState[] initialTileFeatures,
                TileFeatureRuntimeDefinition[] tileFeatureDefinitions,
                MoonBlockRespawnDefinition[] moonBlockRespawnDefinitions,
                int playerEntityId,
                StageObjectiveRuntimeDefinition objectiveRuntimeDefinition,
                EnemyAiProfileOverride[] enemyAiProfileOverrides,
                StageContentEntry stageContentEntry,
                EnemyUnitArchetypeCatalog enemyUnitArchetypeCatalog,
                EnemyPresentationCatalog enemyPresentationCatalog,
                EnemyPresentationArchetypeCatalog enemyPresentationArchetypeCatalog,
                EnemyPresentationBinding[] enemyPresentationBindings,
                StaticEntityPresentationCatalog staticEntityPresentationCatalog,
                StaticEntityPresentationBinding[] staticEntityPresentationBindings,
                BoardPresentationProfile boardPresentationProfile,
                BoardTilePresentationCatalog boardTilePresentationCatalog,
                BoardTileStyleCatalog boardTileStyleCatalog,
                BoardTileOverlayCatalog boardTileOverlayCatalog,
                IReadOnlyList<TileFeaturePresentationResolvedBinding> tileFeaturePresentationBindings,
                StageWorldGuideCatalog worldGuideCatalog = null,
                IReadOnlyList<StageWorldGuideInstructionResolved> worldGuideInstructions = null,
                IReadOnlyList<BoardTilePresentationOverride> boardTilePresentationOverrides = null,
                IReadOnlyList<BoardTilePaintOverride> boardTilePaintOverrides = null,
                IReadOnlyList<BoardTileOverlayOverride> boardTileOverlayOverrides = null,
                IReadOnlyList<SurfaceCell> suppressedBaseTileCells = null)
            {
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                InitialEntities = initialEntities ?? Array.Empty<EntityState>();
                InitialTerrain = initialTerrain ?? GameplayTerrainData.Empty;
                InitialTileFeatures = initialTileFeatures ?? Array.Empty<TileFeatureState>();
                TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
                MoonBlockRespawnDefinitions = moonBlockRespawnDefinitions ?? Array.Empty<MoonBlockRespawnDefinition>();
                PlayerEntityId = playerEntityId;
                ObjectiveRuntimeDefinition = objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
                EnemyAiProfileOverrides = enemyAiProfileOverrides ?? Array.Empty<EnemyAiProfileOverride>();
                StageContentEntry = stageContentEntry;
                EnemyUnitArchetypeCatalog = enemyUnitArchetypeCatalog;
                EnemyPresentationCatalog = enemyPresentationCatalog;
                EnemyPresentationArchetypeCatalog = enemyPresentationArchetypeCatalog;
                EnemyPresentationBindings = enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
                StaticEntityPresentationCatalog = staticEntityPresentationCatalog;
                StaticEntityPresentationBindings = staticEntityPresentationBindings ?? Array.Empty<StaticEntityPresentationBinding>();
                BoardPresentationProfile = boardPresentationProfile;
                BoardTilePresentationCatalog = boardTilePresentationCatalog;
                BoardTileStyleCatalog = boardTileStyleCatalog;
                BoardTileOverlayCatalog = boardTileOverlayCatalog;
                BoardTilePresentationOverrides =
                    boardTilePresentationOverrides ?? Array.Empty<BoardTilePresentationOverride>();
                BoardTilePaintOverrides =
                    boardTilePaintOverrides ?? Array.Empty<BoardTilePaintOverride>();
                BoardTileOverlayOverrides =
                    boardTileOverlayOverrides ?? Array.Empty<BoardTileOverlayOverride>();
                TileFeaturePresentationBindings =
                    tileFeaturePresentationBindings ?? Array.Empty<TileFeaturePresentationResolvedBinding>();
                WorldGuideCatalog = worldGuideCatalog;
                WorldGuideInstructions = worldGuideInstructions ?? Array.Empty<StageWorldGuideInstructionResolved>();
                SuppressedBaseTileCells = suppressedBaseTileCells ?? Array.Empty<SurfaceCell>();
            }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public EntityState[] InitialEntities { get; }

            public GameplayTerrainData InitialTerrain { get; }

            public TileFeatureState[] InitialTileFeatures { get; }

            public TileFeatureRuntimeDefinition[] TileFeatureDefinitions { get; }

            public MoonBlockRespawnDefinition[] MoonBlockRespawnDefinitions { get; }

            public int PlayerEntityId { get; }

            public StageObjectiveRuntimeDefinition ObjectiveRuntimeDefinition { get; }

            public EnemyAiProfileOverride[] EnemyAiProfileOverrides { get; }

            public StageContentEntry StageContentEntry { get; }

            public EnemyUnitArchetypeCatalog EnemyUnitArchetypeCatalog { get; }

            public EnemyPresentationCatalog EnemyPresentationCatalog { get; }

            public EnemyPresentationArchetypeCatalog EnemyPresentationArchetypeCatalog { get; }

            public EnemyPresentationBinding[] EnemyPresentationBindings { get; }

            public StaticEntityPresentationCatalog StaticEntityPresentationCatalog { get; }

            public StaticEntityPresentationBinding[] StaticEntityPresentationBindings { get; }

            public BoardPresentationProfile BoardPresentationProfile { get; }

            public BoardTilePresentationCatalog BoardTilePresentationCatalog { get; }

            public BoardTileStyleCatalog BoardTileStyleCatalog { get; }

            public BoardTileOverlayCatalog BoardTileOverlayCatalog { get; }

            public IReadOnlyList<BoardTilePresentationOverride> BoardTilePresentationOverrides { get; }

            public IReadOnlyList<BoardTilePaintOverride> BoardTilePaintOverrides { get; }

            public IReadOnlyList<BoardTileOverlayOverride> BoardTileOverlayOverrides { get; }

            public IReadOnlyList<TileFeaturePresentationResolvedBinding> TileFeaturePresentationBindings { get; }

            public StageWorldGuideCatalog WorldGuideCatalog { get; }

            public IReadOnlyList<StageWorldGuideInstructionResolved> WorldGuideInstructions { get; }

            public IReadOnlyList<SurfaceCell> SuppressedBaseTileCells { get; }
        }

        [Header("Bootstrap")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private bool autoAdvanceTicks = true;
        [SerializeField] private bool autoCreateViews = true;
        [SerializeField] private float cellSize = 1.15f;
        [SerializeField] private float moveDeadzone = 0.5f;
        [SerializeField] private bool directionChangeConsumesDelay;

        [Header("Timing")]
        [SerializeField] private GameplaySimulationTimingPreset simulationTimingPreset;
        [SerializeField] private GameplayPresentationTimingPreset presentationTimingPreset;

        [Header("Presentation")]
        [SerializeField] private Texture2D boardSurfaceTexture;
        [SerializeField] private GameplayAudioMap gameplayAudioMap;
        [SerializeField] private TileFeatureAudioMap tileFeatureAudioMap;
        [SerializeField] private TopologyAudioMap topologyAudioMap;
        [SerializeField] private GravityFieldAudioMap gravityFieldAudioMap;
        [SerializeField] private BlockAudioMap blockAudioMap;
        [SerializeField] private PlayerLocomotionAudioMap playerLocomotionAudioMap;
        [SerializeField] private float faceSeamGap = -1f;

        protected bool AutoCreateViews => autoCreateViews;

        protected float CellSize => cellSize;

        protected float FaceSeamGap => ResolveFaceSeamGap();

        protected virtual void Awake()
        {
            if (actions == null)
            {
                throw new InvalidOperationException($"{GetType().Name} requires an InputActionAsset reference.");
            }

            InstallBootstrapServices(gameObject);
            ValidateBootstrapReadiness(gameObject);
            var initialState = BuildInitialGameplayState();
            var cameraTopologyAuthoring = ResolveCameraTopologyAuthoringSnapshot();
            var sharedTuning = cameraTopologyAuthoring.SharedTuning;
            var baseCameraSettings = sharedTuning.CameraSettings?.Clone() ??
                                     GameplayCameraSettings.CreateShowcaseDefault();
            var baselineAuthoringPolicy = cameraTopologyAuthoring.BaselineAuthoringPolicy;
            GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                gameObject,
                baseCameraSettings,
                baselineAuthoringPolicy,
                sharedTuning.TopologyTransitionCameraShakeProfile);
            var host = GetComponent<GameplaySceneHost>() ?? gameObject.AddComponent<GameplaySceneHost>();
            host.Initialize(CreateConfiguration(initialState, baseCameraSettings));
            OnHostInitialized(host, initialState);
        }

        private static void InstallBootstrapServices(GameObject bootstrapRoot)
        {
            var behaviours = bootstrapRoot.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (IsActiveEnabledBehaviour(behaviour) &&
                    behaviour is IGameplayBootstrapInstaller installer)
                {
                    installer.Install();
                }
            }
        }

        private static void ValidateBootstrapReadiness(GameObject bootstrapRoot)
        {
            var behaviours = bootstrapRoot.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (!IsActiveEnabledBehaviour(behaviour) ||
                    behaviour is not IGameplayBootstrapReadiness readiness ||
                    readiness.IsReady)
                {
                    continue;
                }

                var description = readiness.DescribeReadiness();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(description)
                    ? $"{behaviour.GetType().Name} is not ready for gameplay host initialization."
                    : description);
            }
        }

        private static bool IsActiveEnabledBehaviour(MonoBehaviour behaviour)
        {
            return behaviour != null &&
                   behaviour.enabled &&
                   behaviour.gameObject.activeInHierarchy;
        }

        protected virtual GameplayCameraSettings CreateCameraSettings()
        {
            return ResolveCameraTopologyAuthoring().GetCameraSettings();
        }

        protected virtual GameplayCameraBaselineAuthoringPolicy CreateBaselineAuthoringPolicy()
        {
            return ResolveCameraTopologyAuthoring().GetBaselineAuthoringPolicy();
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
                ResolveEnemyViewPrefabs(
                    ResolveConfiguredEnemyPresentationCatalog(initialState),
                    initialState.EnemyPresentationBindings),
                ResolveStaticEntityViewPrefabs(
                    ResolveConfiguredStaticEntityPresentationCatalog(initialState),
                    initialState.StaticEntityPresentationBindings));
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

        protected virtual EnemyUnitArchetypeCatalog ResolveEnemyUnitArchetypeCatalog()
        {
            return null;
        }

        protected virtual EnemyPresentationArchetypeCatalog ResolveEnemyPresentationArchetypeCatalog()
        {
            return null;
        }

        protected virtual StaticEntityPresentationCatalog ResolveStaticEntityPresentationCatalog()
        {
            return null;
        }

        protected virtual GameplayAudioMap ResolveGameplayAudioMap()
        {
            return gameplayAudioMap;
        }

        protected virtual TileFeatureAudioMap ResolveTileFeatureAudioMap()
        {
            return tileFeatureAudioMap;
        }

        protected virtual TopologyAudioMap ResolveTopologyAudioMap()
        {
            return topologyAudioMap;
        }

        protected virtual GravityFieldAudioMap ResolveGravityFieldAudioMap()
        {
            return gravityFieldAudioMap;
        }

        protected virtual BlockAudioMap ResolveBlockAudioMap()
        {
            return blockAudioMap;
        }

        protected virtual PlayerLocomotionAudioMap ResolvePlayerLocomotionAudioMap()
        {
            return playerLocomotionAudioMap;
        }

        protected abstract InitialGameplayState BuildInitialGameplayState();

        protected virtual void ConfigureRuntimeConfiguration(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
        }

        protected virtual void ConfigureObjectiveRuntimeDefinition(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
        }

        protected virtual void OnHostInitialized(
            GameplaySceneHost host,
            in InitialGameplayState initialState)
        {
        }

        public GameplayCameraSettings GetCameraSettings()
        {
            return CreateCameraSettings();
        }

        public GameplayCameraBaselineAuthoringPolicy GetBaselineAuthoringPolicy()
        {
            return CreateBaselineAuthoringPolicy();
        }

        public TopologyTransitionCameraShakeProfile GetTopologyTransitionCameraShakeProfile()
        {
            return ResolveCameraTopologyAuthoring().GetTopologyTransitionCameraShakeProfile();
        }

        public TopologyTransitionPostFxProfile GetTopologyTransitionPostFxProfile()
        {
            return ResolveCameraTopologyAuthoring().GetTopologyTransitionPostFxProfile();
        }

        public void ConfigureBootstrapCamera(Camera camera)
        {
            var initialState = BuildInitialGameplayState();
            var cameraTopologyAuthoring = ResolveCameraTopologyAuthoringSnapshot();
            var sharedTuning = cameraTopologyAuthoring.SharedTuning;
            ConfigureSceneCamera(
                camera,
                initialState.BoardBounds,
                ResolveEffectiveCameraSettings(
                    initialState,
                    sharedTuning.CameraSettings,
                    cameraTopologyAuthoring.BaselineAuthoringPolicy,
                    sharedTuning.TopologyRotationVisualMapping),
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
            var cameraTopologyAuthoring = ResolveCameraTopologyAuthoringSnapshot();
            var configuration = new GameplaySceneHostConfiguration
            {
                Actions = actions,
                AutoAdvanceTicks = autoAdvanceTicks,
                AutoCreateViews = autoCreateViews,
                BoardSurfaceTexture = boardSurfaceTexture,
                CellSize = cellSize,
                FaceSeamGap = ResolveFaceSeamGap(),
                DefaultEnemyAiProfile = ResolveDefaultEnemyAiProfile(),
                DirectionChangeConsumesDelay = directionChangeConsumesDelay,
                EnemyAiProfileOverrides = initialState.EnemyAiProfileOverrides,
                EnemyUnitArchetypeCatalog = ResolveConfiguredEnemyUnitArchetypeCatalog(initialState),
                EnemyPresentationArchetypeCatalog = ResolveConfiguredEnemyPresentationArchetypeCatalog(initialState),
                StageContentEntry = initialState.StageContentEntry,
                EnemyPresentationBindings = initialState.EnemyPresentationBindings,
                EnemyPresentationCatalog = ResolveConfiguredEnemyPresentationCatalog(initialState),
                StaticEntityPresentationBindings = initialState.StaticEntityPresentationBindings,
                StaticEntityPresentationCatalog = ResolveConfiguredStaticEntityPresentationCatalog(initialState),
                BoardPresentationProfile = initialState.BoardPresentationProfile,
                BoardTilePresentationCatalog = initialState.BoardTilePresentationCatalog,
                BoardTileStyleCatalog = initialState.BoardTileStyleCatalog,
                BoardTileOverlayCatalog = initialState.BoardTileOverlayCatalog,
                BoardTilePresentationOverrides = initialState.BoardTilePresentationOverrides,
                BoardTilePaintOverrides = initialState.BoardTilePaintOverrides,
                BoardTileOverlayOverrides = initialState.BoardTileOverlayOverrides,
                TileFeaturePresentationBindings = initialState.TileFeaturePresentationBindings,
                WorldGuideCatalog = initialState.WorldGuideCatalog,
                WorldGuideInstructions = initialState.WorldGuideInstructions,
                SuppressedBaseTileCells = initialState.SuppressedBaseTileCells,
                InitialBoardBounds = initialState.BoardBounds,
                InitialEntities = initialState.InitialEntities,
                InitialTerrain = initialState.InitialTerrain,
                InitialTileFeatures = initialState.InitialTileFeatures,
                TileFeatureDefinitions = initialState.TileFeatureDefinitions,
                MoonBlockRespawnDefinitions = initialState.MoonBlockRespawnDefinitions,
                InitialTopology = initialState.InitialTopology,
                MoveDeadzone = moveDeadzone,
                ObjectiveRuntimeDefinition = initialState.ObjectiveRuntimeDefinition,
                PlayerEntityId = initialState.PlayerEntityId,
                PlayerViewPrefab = viewFactory == null ? ResolvePlayerViewPrefab() : null,
                GameplayAudioMap = ResolveGameplayAudioMap(),
                TileFeatureAudioMap = ResolveTileFeatureAudioMap(),
                TopologyAudioMap = ResolveTopologyAudioMap(),
                GravityFieldAudioMap = ResolveGravityFieldAudioMap(),
                BlockAudioMap = ResolveBlockAudioMap(),
                PlayerLocomotionAudioMap = ResolvePlayerLocomotionAudioMap(),
                ViewFactory = viewFactory,
            };

            ConfigureRuntimeConfiguration(configuration, initialState);
            ResolveSimulationTimingPreset().ApplyTo(configuration);
            ResolvePresentationTimingPreset().ApplyTo(configuration);
            ConfigureObjectiveRuntimeDefinition(configuration, initialState);
            GameplayCameraTopologyConfigurationComposer.ApplyTo(
                configuration,
                cameraTopologyAuthoring,
                cameraSettings,
                cameraTopologyAuthoring.ConfigureMainCamera ? Camera.main : null);
            return configuration;
        }

        protected IReadOnlyDictionary<int, GameplayEntityView> ResolveEnemyViewPrefabs(
            EnemyPresentationBinding[] enemyPresentationBindings)
        {
            return ResolveEnemyViewPrefabs(
                ResolveEnemyPresentationCatalog(),
                enemyPresentationBindings);
        }

        protected IReadOnlyDictionary<int, GameplayEntityView> ResolveEnemyViewPrefabs(
            EnemyPresentationCatalog enemyPresentationCatalog,
            EnemyPresentationBinding[] enemyPresentationBindings)
        {
            return EnemyPresentationCatalogResolver.BuildEnemyViewPrefabs(
                enemyPresentationCatalog,
                enemyPresentationBindings,
                GetType().Name);
        }

        protected IReadOnlyDictionary<int, GameplayEntityView> ResolveStaticEntityViewPrefabs(
            StaticEntityPresentationBinding[] staticEntityPresentationBindings)
        {
            return ResolveStaticEntityViewPrefabs(
                ResolveStaticEntityPresentationCatalog(),
                staticEntityPresentationBindings);
        }

        protected IReadOnlyDictionary<int, GameplayEntityView> ResolveStaticEntityViewPrefabs(
            StaticEntityPresentationCatalog staticEntityPresentationCatalog,
            StaticEntityPresentationBinding[] staticEntityPresentationBindings)
        {
            return StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                staticEntityPresentationCatalog,
                staticEntityPresentationBindings,
                GetType().Name);
        }

        private IGameplayEntityViewFactory ResolveViewFactory(in InitialGameplayState initialState)
        {
            var boardRoot = GetComponentInChildren<GameplayBoardRoot>(includeInactive: true);
            return CreateViewFactory(boardRoot, initialState);
        }

        private EnemyUnitArchetypeCatalog ResolveConfiguredEnemyUnitArchetypeCatalog(
            in InitialGameplayState initialState)
        {
            return initialState.EnemyUnitArchetypeCatalog ??
                   (AllowsLegacySceneCatalogFallback(initialState) ? ResolveEnemyUnitArchetypeCatalog() : null);
        }

        private EnemyPresentationArchetypeCatalog ResolveConfiguredEnemyPresentationArchetypeCatalog(
            in InitialGameplayState initialState)
        {
            return initialState.EnemyPresentationArchetypeCatalog ??
                   (AllowsLegacySceneCatalogFallback(initialState) ? ResolveEnemyPresentationArchetypeCatalog() : null);
        }

        private EnemyPresentationCatalog ResolveConfiguredEnemyPresentationCatalog(
            in InitialGameplayState initialState)
        {
            return initialState.EnemyPresentationCatalog ??
                   (AllowsLegacySceneCatalogFallback(initialState) ? ResolveEnemyPresentationCatalog() : null);
        }

        private StaticEntityPresentationCatalog ResolveConfiguredStaticEntityPresentationCatalog(
            in InitialGameplayState initialState)
        {
            return initialState.StaticEntityPresentationCatalog ??
                   (AllowsLegacySceneCatalogFallback(initialState) ? ResolveStaticEntityPresentationCatalog() : null);
        }

        private static bool AllowsLegacySceneCatalogFallback(in InitialGameplayState initialState)
        {
            return initialState.StageContentEntry == null;
        }

        private GameplayCameraSettings ResolveEffectiveCameraSettings(
            InitialGameplayState initialState,
            GameplayCameraSettings baseCameraSettings,
            GameplayCameraBaselineAuthoringPolicy baselineAuthoringPolicy,
            TopologyRotationVisualMapping topologyRotationVisualMapping)
        {
            var rig = GetComponent<GameplayCameraRig>();
            if (rig == null)
            {
                return baseCameraSettings != null
                    ? baseCameraSettings.Clone()
                    : GameplayCameraSettings.CreateShowcaseDefault();
            }

            var boardRoot = GetComponentInChildren<GameplayBoardRoot>(includeInactive: true);
            var projector = new GameplayCubeProjector(initialState.BoardBounds, cellSize, ResolveFaceSeamGap());
            var cubeCenterLocal = projector.GetCubeCenter();
            var cubeCenterWorld = boardRoot != null
                ? boardRoot.transform.TransformPoint(cubeCenterLocal)
                : transform.TransformPoint(cubeCenterLocal);

            return rig.ResolveConfiguredSettings(
                baseCameraSettings,
                baselineAuthoringPolicy,
                cubeCenterWorld,
                initialState.InitialTopology,
                topologyRotationVisualMapping);
        }

        private GameplayCameraTopologyAuthoring ResolveCameraTopologyAuthoring()
        {
            return GameplayCameraTopologyAuthoring.GetRequiredValidated(this);
        }

        private GameplayCameraTopologyAuthoringSnapshot ResolveCameraTopologyAuthoringSnapshot()
        {
            return ResolveCameraTopologyAuthoring().CreateSnapshot();
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

            var projector = new GameplayCubeProjector(boardBounds, cellSize, ResolveFaceSeamGap());
            GameplayShowcaseSceneScaffold.ConfigureDefaultSceneCamera(
                camera,
                cameraSettings,
                Vector3.zero,
                projector.GetVisibleCubeBounds(topology));
        }

        private float ResolveFaceSeamGap()
        {
            if (cellSize <= 0f)
            {
                throw new InvalidOperationException($"{GetType().Name} requires a positive cell size.");
            }

            return faceSeamGap >= 0f ? faceSeamGap : cellSize;
        }
    }
}
