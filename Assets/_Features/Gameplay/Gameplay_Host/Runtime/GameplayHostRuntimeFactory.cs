using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Shared.Audio;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public static class GameplayHostRuntimeFactory
    {
        private const string BoardRootObjectName = "GameplayBoardRoot";
        private const string MissingGameplayAudioRuntimeInstallerMessage =
            "GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GameplayAudioMap is assigned.";

        public static GameplayHostRuntimeContext Create(
            GameplaySceneHost host,
            GameplaySceneHostConfiguration configuration)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.PlayerEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration), "Player entity ID must be positive.");
            }

            if (!configuration.InitialBoardBounds.IsBounded)
            {
                throw new InvalidOperationException(
                    "GameplaySceneHost requires bounded InitialBoardBounds. Unbounded boards are not supported by runtime scene hosts.");
            }

            var hostObject = host.gameObject;
            var hostTransform = host.transform;
            var inputHost = hostObject.GetComponent<GameplayInputHost>() ?? hostObject.AddComponent<GameplayInputHost>();
            var presenter = hostObject.GetComponent<GameplayTickViewPresenter>() ?? hostObject.AddComponent<GameplayTickViewPresenter>();
            var viewRegistry = hostObject.GetComponent<GameplayEntityViewRegistry>() ?? hostObject.AddComponent<GameplayEntityViewRegistry>();

            var initialEntities = configuration.InitialEntities ?? Array.Empty<EntityState>();
            var initialTerrain = configuration.InitialTerrain ?? GameplayTerrainData.Empty;
            var generalTimingProfile = configuration.CreateTimingProfile();
            var playerControlTiming = configuration.CreatePlayerControlTimingSnapshot();
            var playerRespawnTiming = configuration.CreatePlayerRespawnTimingSnapshot();
            var enemyAiRuntime = configuration.CreateEnemyAiRuntimeSnapshot();
            var faceSeamGap = configuration.ResolveFaceSeamGap();
            var playerViewPrefab = ResolvePlayerViewPrefab(configuration);
            var normalizedInitialEntities = NormalizeInitialEntitiesForRuntime(initialEntities, generalTimingProfile);
            DebugSpawnValidityPolicy.EnsureRepresentable(
                configuration.InitialBoardBounds,
                initialTerrain,
                normalizedInitialEntities);

            var worldState = GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                configuration.InitialBoardBounds,
                initialTerrain,
                configuration.InitialTopology);
            var initialSnapshot = GameplayCompositionRoot.CreateSnapshot(worldState);
            var presentedInitialEntities = new List<EntityState>();
            initialSnapshot.EnumerateEntitiesOrdered(presentedInitialEntities);

            var inputBuffer = new TickInputBuffer();
            var bootstrapper = new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    enemyAiRuntime.DefaultDefinition,
                    enemyAiRuntime.DefinitionsByEntityId));
            var tickRunner = bootstrapper.CreateTickRunner(
                worldState,
                BuildStaticEntityLogics(configuration, playerControlTiming),
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnTiming.RespawnDelayTicks,
                configuration.ObjectiveRuntimeDefinition,
                startTickIndex: 1);

            var boardRoot = EnsureBoardRootHierarchy(hostTransform);
            var boardSurfaceRenderer = boardRoot.EnsureBoardSurfaceRenderer();
            viewRegistry.ConfigureSearchRoot(boardRoot.EntityRoot);

            var viewFactory = configuration.ViewFactory ??
                (configuration.AutoCreateViews
                    ? new DefaultGameplayEntityViewFactory(
                        boardRoot.EntityRoot,
                        configuration.CellSize,
                        configuration.PlayerEntityId,
                        playerViewPrefab,
                        BuildEnemyViewPrefabs(configuration),
                        BuildStaticViewPrefabs(configuration))
                    : null);
            var viewBinder = new GameplayEntityViewBinder(viewRegistry, viewFactory);

            presenter.Initialize(
                viewBinder,
                configuration.InitialBoardBounds,
                configuration.InitialTopology,
                configuration.CellSize,
                generalTimingProfile,
                boardRoot,
                boardSurfaceRenderer,
                configuration.TopologyRotationVisualMapping,
                configuration.TopologyRotationTween,
                faceSeamGap);
            AttachGameplayAudioRuntimeIfConfigured(hostObject, presenter, configuration);

            boardSurfaceRenderer.Initialize(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology,
                faceSeamGap,
                configuration.BoardSurfaceTexture);

            var viewCameraTarget = boardRoot.CameraTargetRoot;
            var startupPlan = GameplayCameraStartupPlanComposer.Compose(
                hostObject,
                configuration,
                viewCameraTarget);
            GameplayShowcaseSceneCameraBootstrap.ApplyResolvedStartupLens(hostObject.scene, startupPlan);
            var visualRuntime = GameplayHostTopologyVisualRuntimeBootstrap.Attach(
                hostObject,
                startupPlan,
                presenter,
                viewCameraTarget,
                presenter.VisibleCubeBounds);
            var viewCamera = visualRuntime.ViewCamera;
            var viewCameraRig = visualRuntime.ViewCameraRig;

            presenter.PresentInitial(presentedInitialEntities, configuration.InitialTopology);
            inputHost.Initialize(
                inputBuffer,
                tickRunner,
                presenter,
                configuration.Actions,
                generalTimingProfile,
                configuration.PlayerEntityId,
                configuration.MoveDeadzone,
                configuration.DirectionChangeConsumesDelay,
                configuration.AutoAdvanceTicks);
            var pauseService = new GameplayHostPauseService(inputHost);
            var admissionPolicy = new GameplayHostCommandAdmissionPolicy(worldState, tickRunner, inputHost, presenter, pauseService);
            var uiAccess = new GameplayHostUiAccessContext(
                new GameplayHostCommandGateway(inputHost, admissionPolicy),
                new GameplayQueryFacade(
                    new GameplayHostSessionQuery(tickRunner, pauseService, admissionPolicy),
                    new GameplayHostPlayerHudQuery(tickRunner, inputHost, admissionPolicy),
                    new GameplayHostObjectiveQuery(tickRunner)),
                new GameplayHostPresentationFeed(inputHost, presenter, configuration.StageContentEntry),
                pauseService);

            return new GameplayHostRuntimeContext(
                boardRoot,
                boardSurfaceRenderer,
                inputBuffer,
                inputHost,
                presenter,
                generalTimingProfile,
                tickRunner,
                viewRegistry,
                viewCameraTarget,
                worldState,
                configuration.ObjectiveRuntimeDefinition,
                viewCamera,
                viewCameraRig,
                presentedInitialEntities,
                uiAccess);
        }

        private static IReadOnlyDictionary<int, GameplayEntityView> BuildEnemyViewPrefabs(
            GameplaySceneHostConfiguration configuration)
        {
            return EnemyPresentationCatalogResolver.BuildEnemyViewPrefabs(
                configuration?.EnemyPresentationCatalog,
                configuration?.EnemyPresentationBindings,
                nameof(GameplaySceneHostConfiguration));
        }

        private static IReadOnlyDictionary<int, GameplayEntityView> BuildStaticViewPrefabs(
            GameplaySceneHostConfiguration configuration)
        {
            return StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                configuration?.StaticEntityPresentationCatalog,
                configuration?.StaticEntityPresentationBindings,
                nameof(GameplaySceneHostConfiguration));
        }

        private static List<EntityState> NormalizeInitialEntitiesForRuntime(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            var normalizedEntities = new List<EntityState>();

            foreach (var entity in initialEntities)
            {
                var normalizedEntity = entity;

                if (normalizedEntity.type == EntityType.Projectile &&
                    normalizedEntity.spawnTick == 0 &&
                    normalizedEntity.stateTimer == 0 &&
                    normalizedEntity.hp > 0 &&
                    !normalizedEntity.markedForDeath)
                {
                    normalizedEntity.stateTimer = timingProfile.ProjectileStepIntervalTicks;
                }

                normalizedEntities.Add(normalizedEntity);
            }

            return normalizedEntities;
        }

        private static IReadOnlyList<IEntityLogic> BuildStaticEntityLogics(
            GameplaySceneHostConfiguration configuration,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            var entityLogics = new List<IEntityLogic>
            {
                new PlayerLogic(
                    configuration.PlayerEntityId,
                    playerControlTiming.PushWindupTicks,
                    playerControlTiming.PushRecoveryTicks,
                    playerControlTiming.FlipWindupTicks,
                    playerControlTiming.FlipRecoveryTicks),
            };

            if (configuration.StaticEntityLogics == null)
            {
                return entityLogics;
            }

            for (var i = 0; i < configuration.StaticEntityLogics.Length; i++)
            {
                var entityLogic = configuration.StaticEntityLogics[i];
                if (entityLogic == null)
                {
                    throw new ArgumentException("Static entity logic collections cannot contain null entries.", nameof(configuration));
                }

                entityLogics.Add(entityLogic);
            }

            return entityLogics;
        }

        private static GameplayBoardRoot EnsureBoardRootHierarchy(Transform hostTransform)
        {
            var boardRoot = FindExistingBoardRoot(hostTransform);
            if (boardRoot == null)
            {
                var boardRootObject = new GameObject(BoardRootObjectName);
                boardRootObject.transform.SetParent(hostTransform, worldPositionStays: false);
                boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
            }
            else
            {
                boardRoot.transform.SetParent(hostTransform, worldPositionStays: false);
                boardRoot.gameObject.name = BoardRootObjectName;
            }

            boardRoot.EnsureHierarchy();
            return boardRoot;
        }

        private static GameplayBoardRoot FindExistingBoardRoot(Transform hostTransform)
        {
            for (var i = 0; i < hostTransform.childCount; i++)
            {
                var child = hostTransform.GetChild(i);
                if (child.TryGetComponent<GameplayBoardRoot>(out var boardRoot))
                {
                    return boardRoot;
                }
            }

            var namedChild = hostTransform.Find(BoardRootObjectName);
            if (namedChild != null)
            {
                return namedChild.GetComponent<GameplayBoardRoot>() ??
                       namedChild.gameObject.AddComponent<GameplayBoardRoot>();
            }

            return null;
        }

        private static void AttachGameplayAudioRuntimeIfConfigured(
            GameObject hostObject,
            GameplayTickViewPresenter presenter,
            GameplaySceneHostConfiguration configuration)
        {
            if (configuration?.GameplayAudioMap == null)
            {
                return;
            }

            var audioRuntimeInstaller = hostObject.GetComponent<AudioRuntimeInstaller>();
            if (audioRuntimeInstaller == null)
            {
                throw new InvalidOperationException(MissingGameplayAudioRuntimeInstallerMessage);
            }

            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioService == null)
            {
                throw new InvalidOperationException(MissingGameplayAudioRuntimeInstallerMessage);
            }

            presenter.AttachGameplayAudioRuntime(
                new GameplayAudioPlaybackPortAdapter(audioRuntimeInstaller.AudioService),
                configuration.GameplayAudioMap);
        }

        private static GameplayEntityView ResolvePlayerViewPrefab(GameplaySceneHostConfiguration configuration)
        {
            if (configuration.PlayerViewPrefab != null)
            {
                PlayerViewPrefabRequirements.ValidatePlayerViewPrefab(
                    configuration.PlayerViewPrefab,
                    nameof(GameplaySceneHostConfiguration.PlayerViewPrefab));
                return configuration.PlayerViewPrefab;
            }

            if (configuration.ViewFactory is IPlayerViewPrefabSource prefabSource &&
                prefabSource.PlayerViewPrefab != null)
            {
                PlayerViewPrefabRequirements.ValidatePlayerViewPrefab(
                    prefabSource.PlayerViewPrefab,
                    $"{configuration.ViewFactory.GetType().Name}.{nameof(IPlayerViewPrefabSource.PlayerViewPrefab)}");
                return prefabSource.PlayerViewPrefab;
            }

            return null;
        }
    }
}
