using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
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
            var timingProfile = configuration.CreateTimingProfile();
            var playerControlTiming = configuration.CreatePlayerControlTimingSnapshot();
            var playerViewPrefab = ResolvePlayerViewPrefab(configuration);
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(initialEntities, timingProfile);

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
                    configuration.DefaultEnemyAiProfile,
                    BuildEnemyAiProfileOverrides(configuration)));
            var tickRunner = bootstrapper.CreateTickRunner(
                worldState,
                BuildStaticEntityLogics(configuration, playerControlTiming),
                inputBuffer,
                timingProfile,
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
                        BuildEnemyViewPrefabs(configuration))
                    : null);
            var viewBinder = new GameplayEntityViewBinder(viewRegistry, viewFactory);

            presenter.Initialize(
                viewBinder,
                configuration.InitialBoardBounds,
                configuration.InitialTopology,
                configuration.CellSize,
                timingProfile,
                boardRoot,
                boardSurfaceRenderer,
                configuration.TopologyRotationVisualMapping);

            boardSurfaceRenderer.Initialize(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology);

            var viewCamera = ResolveViewCamera(configuration);
            var viewCameraTarget = boardRoot.CameraTargetRoot;
            var viewCameraRig = ConfigureViewCameraRig(
                hostObject,
                configuration,
                viewCamera,
                viewCameraTarget,
                presenter.VisibleCubeBounds);

            presenter.PresentInitial(presentedInitialEntities, configuration.InitialTopology);
            inputHost.Initialize(
                inputBuffer,
                tickRunner,
                presenter,
                configuration.Actions,
                timingProfile,
                configuration.MoveDeadzone,
                configuration.DirectionChangeConsumesDelay,
                configuration.AutoAdvanceTicks);

            return new GameplayHostRuntimeContext(
                boardRoot,
                boardSurfaceRenderer,
                inputBuffer,
                inputHost,
                presenter,
                timingProfile,
                tickRunner,
                viewRegistry,
                viewCameraTarget,
                worldState,
                viewCamera,
                viewCameraRig,
                presentedInitialEntities);
        }

        private static IReadOnlyDictionary<int, EnemyAiProfile> BuildEnemyAiProfileOverrides(
            GameplaySceneHostConfiguration configuration)
        {
            if (configuration?.EnemyAiProfileOverrides == null ||
                configuration.EnemyAiProfileOverrides.Length == 0)
            {
                return null;
            }

            var profilesByEntityId = new Dictionary<int, EnemyAiProfile>();
            for (var i = 0; i < configuration.EnemyAiProfileOverrides.Length; i++)
            {
                var overrideEntry = configuration.EnemyAiProfileOverrides[i];
                if (overrideEntry.EntityId <= 0)
                {
                    throw new ArgumentException("Enemy AI profile overrides require a positive entity ID.", nameof(configuration));
                }

                if (overrideEntry.Profile == null)
                {
                    throw new ArgumentException("Enemy AI profile overrides require a non-null profile.", nameof(configuration));
                }

                if (profilesByEntityId.ContainsKey(overrideEntry.EntityId))
                {
                    throw new ArgumentException("Enemy AI profile overrides cannot contain duplicate entity IDs.", nameof(configuration));
                }

                profilesByEntityId.Add(overrideEntry.EntityId, overrideEntry.Profile);
            }

            return profilesByEntityId;
        }

        private static IReadOnlyDictionary<int, GameplayEntityView> BuildEnemyViewPrefabs(
            GameplaySceneHostConfiguration configuration)
        {
            return EnemyPresentationCatalogResolver.BuildEnemyViewPrefabs(
                configuration?.EnemyPresentationCatalog,
                configuration?.EnemyPresentationBindings,
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
            Transform viewCameraTarget,
            Bounds visibleCubeBounds)
        {
            if (!configuration.SnapViewCameraToTarget || viewCamera == null || viewCameraTarget == null)
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
            cameraRig.ApplySettings(configuration.CameraSettings ?? GameplayCameraSettings.CreateRuntimeDefault());
            cameraRig.Initialize(viewCamera, viewCameraTarget, visibleCubeBounds);
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
