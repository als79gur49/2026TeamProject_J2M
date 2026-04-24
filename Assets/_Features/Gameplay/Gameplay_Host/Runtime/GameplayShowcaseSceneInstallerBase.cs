using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Objectives;
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
                int playerEntityId,
                StageObjectiveRuntimeDefinition objectiveRuntimeDefinition,
                EnemyAiProfileOverride[] enemyAiProfileOverrides,
                StageContentEntry stageContentEntry,
                EnemyPresentationCatalog enemyPresentationCatalog,
                EnemyPresentationBinding[] enemyPresentationBindings,
                StaticEntityPresentationCatalog staticEntityPresentationCatalog,
                StaticEntityPresentationBinding[] staticEntityPresentationBindings)
            {
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                InitialEntities = initialEntities ?? Array.Empty<EntityState>();
                InitialTerrain = initialTerrain ?? GameplayTerrainData.Empty;
                PlayerEntityId = playerEntityId;
                ObjectiveRuntimeDefinition = objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
                EnemyAiProfileOverrides = enemyAiProfileOverrides ?? Array.Empty<EnemyAiProfileOverride>();
                StageContentEntry = stageContentEntry;
                EnemyPresentationCatalog = enemyPresentationCatalog;
                EnemyPresentationBindings = enemyPresentationBindings ?? Array.Empty<EnemyPresentationBinding>();
                StaticEntityPresentationCatalog = staticEntityPresentationCatalog;
                StaticEntityPresentationBindings = staticEntityPresentationBindings ?? Array.Empty<StaticEntityPresentationBinding>();
            }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public EntityState[] InitialEntities { get; }

            public GameplayTerrainData InitialTerrain { get; }

            public int PlayerEntityId { get; }

            public StageObjectiveRuntimeDefinition ObjectiveRuntimeDefinition { get; }

            public EnemyAiProfileOverride[] EnemyAiProfileOverrides { get; }

            public StageContentEntry StageContentEntry { get; }

            public EnemyPresentationCatalog EnemyPresentationCatalog { get; }

            public EnemyPresentationBinding[] EnemyPresentationBindings { get; }

            public StaticEntityPresentationCatalog StaticEntityPresentationCatalog { get; }

            public StaticEntityPresentationBinding[] StaticEntityPresentationBindings { get; }
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

            var initialState = BuildInitialGameplayState();
            var cameraTopologyAuthoring = ResolveCameraTopologyAuthoringSnapshot();
            var baseCameraSettings = cameraTopologyAuthoring.CameraSettings?.Clone() ??
                                     GameplayCameraSettings.CreateShowcaseDefault();
            var baselineAuthoringPolicy = cameraTopologyAuthoring.BaselineAuthoringPolicy;
            GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                gameObject,
                baseCameraSettings,
                baselineAuthoringPolicy,
                cameraTopologyAuthoring.TopologyTransitionCameraShakeProfile);
            var resolvedCameraSettings = ResolveEffectiveCameraSettings(
                initialState,
                baseCameraSettings,
                baselineAuthoringPolicy,
                cameraTopologyAuthoring.TopologyRotationVisualMapping);
            var rig = GetComponent<GameplayCameraRig>();
            rig?.ApplySettings(resolvedCameraSettings);

            if (cameraTopologyAuthoring.ConfigureMainCamera)
            {
                ConfigureCamera(initialState, resolvedCameraSettings);
            }

            var host = GetComponent<GameplaySceneHost>() ?? gameObject.AddComponent<GameplaySceneHost>();
            host.Initialize(CreateConfiguration(initialState, resolvedCameraSettings));
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
                    initialState.EnemyPresentationCatalog ?? ResolveEnemyPresentationCatalog(),
                    initialState.EnemyPresentationBindings),
                ResolveStaticEntityViewPrefabs(
                    initialState.StaticEntityPresentationCatalog ?? ResolveStaticEntityPresentationCatalog(),
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

        protected virtual StaticEntityPresentationCatalog ResolveStaticEntityPresentationCatalog()
        {
            return null;
        }

        protected virtual GameplayAudioMap ResolveGameplayAudioMap()
        {
            return gameplayAudioMap;
        }

        protected abstract InitialGameplayState BuildInitialGameplayState();

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
            ConfigureSceneCamera(
                camera,
                initialState.BoardBounds,
                ResolveEffectiveCameraSettings(
                    initialState,
                    cameraTopologyAuthoring.CameraSettings,
                    cameraTopologyAuthoring.BaselineAuthoringPolicy,
                    cameraTopologyAuthoring.TopologyRotationVisualMapping),
                initialState.InitialTopology);
        }

        private void ConfigureCamera(
            InitialGameplayState initialState,
            GameplayCameraSettings resolvedCameraSettings)
        {
            var camera = Camera.main;
            ConfigureSceneCamera(
                camera,
                initialState.BoardBounds,
                resolvedCameraSettings,
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
                StageContentEntry = initialState.StageContentEntry,
                EnemyPresentationBindings = initialState.EnemyPresentationBindings,
                EnemyPresentationCatalog = initialState.EnemyPresentationCatalog ?? ResolveEnemyPresentationCatalog(),
                StaticEntityPresentationBindings = initialState.StaticEntityPresentationBindings,
                StaticEntityPresentationCatalog = initialState.StaticEntityPresentationCatalog ?? ResolveStaticEntityPresentationCatalog(),
                InitialBoardBounds = initialState.BoardBounds,
                InitialEntities = initialState.InitialEntities,
                InitialTerrain = initialState.InitialTerrain,
                InitialTopology = initialState.InitialTopology,
                MoveDeadzone = moveDeadzone,
                ObjectiveRuntimeDefinition = initialState.ObjectiveRuntimeDefinition,
                PlayerEntityId = initialState.PlayerEntityId,
                PlayerViewPrefab = viewFactory == null ? ResolvePlayerViewPrefab() : null,
                GameplayAudioMap = ResolveGameplayAudioMap(),
                ViewFactory = viewFactory,
            };

            ResolveSimulationTimingPreset().ApplyTo(configuration);
            ResolvePresentationTimingPreset().ApplyTo(configuration);
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
