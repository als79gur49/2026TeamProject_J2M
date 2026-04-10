using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public static class GameplayHostRuntimeFactory
    {
        private const string BoardRootObjectName = "GameplayBoardRoot";

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
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(initialEntities, generalTimingProfile);

            var worldState = GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                configuration.InitialBoardBounds,
                initialTerrain,
                configuration.InitialTopology);
            var initialSnapshot = SnapshotBuilder.Create(worldState);
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

            boardSurfaceRenderer.Initialize(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology,
                faceSeamGap,
                configuration.BoardSurfaceTexture);

            var viewCamera = ResolveViewCamera(configuration);
            var outputCamera = ResolveOutputCamera(configuration);
            var outputCameraBrain = ResolveOutputCameraBrain(outputCamera);
            presenter.AttachOutputCamera(outputCamera);
            var viewCameraTarget = boardRoot.CameraTargetRoot;
            var viewCameraRig = ConfigureViewCameraRig(
                hostObject,
                configuration,
                viewCamera,
                outputCameraBrain,
                viewCameraTarget,
                presenter.VisibleCubeBounds);
            presenter.AttachCameraRuntime(viewCameraRig, outputCameraBrain);

            var topologyTransitionPostFxController =
                hostObject.GetComponent<TopologyTransitionPostFxController>() ??
                hostObject.AddComponent<TopologyTransitionPostFxController>();
            topologyTransitionPostFxController.Initialize(configuration.TopologyTransitionPostFxProfile, outputCamera);
            presenter.AttachTopologyTransitionPostFxController(topologyTransitionPostFxController);

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
                viewCamera,
                viewCameraRig,
                presentedInitialEntities);
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

        private static IReadOnlyList<IEntityLogic> BuildStaticEntityLogics(
            GameplaySceneHostConfiguration configuration,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            var entityLogics = new List<IEntityLogic>
            {
                new PlayerLogic(
                    configuration.PlayerEntityId,
                    playerControlTiming.PushContactThresholdTicks,
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

        private static GameplayCameraRig ConfigureViewCameraRig(
            GameObject hostObject,
            GameplaySceneHostConfiguration configuration,
            Camera viewCamera,
            CinemachineBrain outputCameraBrain,
            Transform viewCameraTarget,
            Bounds visibleCubeBounds)
        {
            if (viewCameraTarget == null)
            {
                var existingRig = hostObject.GetComponent<GameplayCameraRig>();
                if (existingRig != null)
                {
                    existingRig.enabled = false;
                }

                return existingRig;
            }

            var cameraRig = hostObject.GetComponent<GameplayCameraRig>() ?? hostObject.AddComponent<GameplayCameraRig>();
            cameraRig.enabled = true;
            cameraRig.ConfigureTopologyTransitionCameraShake(configuration.TopologyTransitionCameraShakeProfile);
            var resolvedCameraSettings = cameraRig.ResolveConfiguredSettings(
                configuration.CameraSettings ?? GameplayCameraSettings.CreateRuntimeDefault(),
                viewCameraTarget.position,
                configuration.InitialTopology,
                configuration.TopologyRotationVisualMapping);
            cameraRig.ApplySettings(resolvedCameraSettings);
            cameraRig.Initialize(
                outputCameraBrain == null ? viewCamera : null,
                viewCameraTarget,
                visibleCubeBounds);
            return cameraRig;
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

        private static Camera ResolveViewCamera(GameplaySceneHostConfiguration configuration)
        {
            return configuration.ViewCamera ?? (configuration.SnapViewCameraToTarget ? Camera.main : null);
        }

        private static Camera ResolveOutputCamera(GameplaySceneHostConfiguration configuration)
        {
            return configuration.ViewCamera ?? Camera.main;
        }

        private static CinemachineBrain ResolveOutputCameraBrain(Camera outputCamera)
        {
            return outputCamera != null
                ? outputCamera.GetComponent<CinemachineBrain>()
                : null;
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
