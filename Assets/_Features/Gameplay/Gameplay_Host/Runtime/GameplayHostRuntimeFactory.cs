using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;
using Game.Shared.Audio;
using Game.Shared.Input;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public static class GameplayHostRuntimeFactory
    {
        private const string BoardRootObjectName = "GameplayBoardRoot";
        private const string MissingGameplayAudioRuntimeInstallerMessage =
            "GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GameplayAudioMap is assigned.";
        private const string MissingTileFeatureAudioRuntimeInstallerMessage =
            "GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when TileFeatureAudioMap is assigned.";
        private const string MissingGravityFieldAudioRuntimeInstallerMessage =
            "GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GravityFieldAudioMap is assigned.";
        private const string MissingBlockAudioRuntimeInstallerMessage =
            "GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when BlockAudioMap is assigned.";

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
            var tileFeatureVisualRegistry =
                hostObject.GetComponent<TileFeatureVisualRegistry>() ?? hostObject.AddComponent<TileFeatureVisualRegistry>();

            var initialEntities = configuration.InitialEntities ?? Array.Empty<EntityState>();
            var initialTerrain = configuration.InitialTerrain ?? GameplayTerrainData.Empty;
            var initialTileFeatures = configuration.InitialTileFeatures ?? Array.Empty<TileFeatureState>();
            var tileFeatureDefinitions = configuration.TileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
            var moonBlockRespawnDefinitions =
                configuration.MoonBlockRespawnDefinitions ?? Array.Empty<MoonBlockRespawnDefinition>();
            var generalTimingProfile = configuration.CreateTimingProfile();
            var playerControlTiming = configuration.CreatePlayerControlTimingSnapshot();
            var playerKinematicLocomotionTiming = configuration.CreatePlayerKinematicLocomotionTimingSnapshot();
            var playerContinuousLocomotion = configuration.CreatePlayerContinuousLocomotionSnapshot();
            var playerRespawnTiming = configuration.CreatePlayerRespawnTimingSnapshot();
            var enemyAiRuntime = configuration.CreateEnemyAiRuntimeSnapshot();
            var enemyPresentationArchetypeRegistry = configuration.CreateEnemyPresentationArchetypeRegistry(enemyAiRuntime);
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
                configuration.InitialTopology,
                initialTileFeatures);
            var initialSnapshot = GameplayCompositionRoot.CreateSnapshot(worldState);
            var presentedInitialEntities = new List<EntityState>();
            initialSnapshot.EnumerateEntitiesOrdered(presentedInitialEntities);

            var inputBuffer = new TickInputBuffer();
            var bootstrapper = new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    enemyAiRuntime.DefaultDefinition,
                    enemyAiRuntime.DefinitionsByEntityId,
                    enemyAiRuntime.DefinitionsByArchetypeId),
                enemyAiRuntime.SpawnDefaultsByArchetypeId);
            var tickRunner = bootstrapper.CreateTickRunner(
                worldState,
                BuildStaticEntityLogics(configuration, playerControlTiming),
                inputBuffer,
                generalTimingProfile,
                playerControlTiming,
                playerRespawnTiming.RespawnDelayTicks,
                configuration.ObjectiveRuntimeDefinition,
                startTickIndex: 1,
                allowPlayerRespawn: !configuration.DisablePlayerRespawn,
                runtimeFeatureFlags: configuration.CreateRuntimeFeatureFlags(),
                playerKinematicLocomotionTiming: playerKinematicLocomotionTiming,
                playerContinuousLocomotion: playerContinuousLocomotion,
                tileFeatureDefinitions: tileFeatureDefinitions,
                moonBlockRespawnDefinitions: moonBlockRespawnDefinitions);

            var boardRoot = EnsureBoardRootHierarchy(hostTransform);
            var boardSurfaceRenderer = boardRoot.EnsureBoardSurfaceRenderer();
            viewRegistry.ConfigureSearchRoot(boardRoot.EntityRoot);
            tileFeatureVisualRegistry.ConfigureSearchRoot(boardRoot.transform);

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
            var tileFeaturePoseResolver = new BoardSurfaceCellPresentationPoseResolver(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology,
                faceSeamGap);
            InstantiateStageTileFeatureVisuals(
                configuration.TileFeaturePresentationBindings,
                initialTileFeatures,
                boardRoot.transform,
                tileFeatureVisualRegistry,
                tileFeaturePoseResolver);

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
                faceSeamGap,
                enemyPresentationArchetypeRegistry,
                configuration.EnemyPresentationCatalog,
                configuration.EnemyPresentationBindings);
            presenter.AttachTileFeatureVisualRegistry(tileFeatureVisualRegistry);
            AttachPresentationExtensions(hostObject, presenter);
            AttachAudioRuntimesIfConfigured(hostObject, presenter, configuration);

            boardSurfaceRenderer.Initialize(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology,
                faceSeamGap,
                configuration.BoardSurfaceTexture,
                configuration.BoardTilePresentationCatalog,
                configuration.BoardTilePresentationOverrides,
                configuration.SuppressedBaseTileCells);

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
            KeyboardBindingSettingsService.ApplySavedSettings(configuration.Actions);
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
                    new GameplayHostStageQuery(configuration.StageContentEntry),
                    new GameplayHostPlayerHudQuery(
                        tickRunner,
                        inputHost,
                        admissionPolicy,
                        configuration.CampaignChancesReadSource),
                    new GameplayHostObjectiveQuery(tickRunner)),
                new GameplayHostPresentationFeed(
                    inputHost,
                    presenter,
                    configuration.StageContentEntry,
                    configuration.StageCompletionProfileStore),
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
                tileFeatureVisualRegistry,
                viewCameraTarget,
                worldState,
                configuration.ObjectiveRuntimeDefinition,
                viewCamera,
                viewCameraRig,
                presentedInitialEntities,
                uiAccess,
                playerRespawnTiming.RespawnDelayTicks);
        }

        private static IReadOnlyDictionary<int, GameplayEntityView> BuildEnemyViewPrefabs(
            GameplaySceneHostConfiguration configuration)
        {
            return EnemyPresentationCatalogResolver.BuildEnemyViewPrefabs(
                configuration?.EnemyPresentationCatalog,
                configuration?.EnemyPresentationBindings,
                nameof(GameplaySceneHostConfiguration));
        }

        private static void AttachPresentationExtensions(GameObject hostObject, GameplayTickViewPresenter presenter)
        {
            var behaviours = hostObject.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour != null &&
                    behaviour.enabled &&
                    behaviour.gameObject.activeInHierarchy &&
                    behaviour is IGameplayTickPresentationExtension extension)
                {
                    presenter.AttachPresentationExtension(extension);
                }
            }
        }

        private static IReadOnlyDictionary<int, GameplayEntityView> BuildStaticViewPrefabs(
            GameplaySceneHostConfiguration configuration)
        {
            return StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                configuration?.StaticEntityPresentationCatalog,
                configuration?.StaticEntityPresentationBindings,
                nameof(GameplaySceneHostConfiguration));
        }

        private static void InstantiateStageTileFeatureVisuals(
            IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings,
            IReadOnlyList<TileFeatureState> initialTileFeatures,
            Transform parent,
            TileFeatureVisualRegistry registry,
            ISurfaceCellPresentationPoseResolver poseResolver = null)
        {
            if (bindings == null || bindings.Count == 0)
            {
                return;
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.TileId <= 0 ||
                    binding.VisualPrefab == null)
                {
                    UnityEngine.Debug.LogWarning($"Skipping invalid stage TileFeature visual binding at index {i}.");
                    continue;
                }

                if (!TryGetTileFeatureCell(initialTileFeatures, binding.TileId, out var cell))
                {
                    UnityEngine.Debug.LogWarning($"Skipping stage TileFeature visual binding for missing TileId {binding.TileId}.");
                    continue;
                }

                var hasResolvedPose = false;
                var resolvedPose = default(SurfaceCellPresentationPose);
                if (poseResolver != null)
                {
                    if (!poseResolver.TryResolvePose(cell, out resolvedPose))
                    {
                        UnityEngine.Debug.LogWarning(
                            $"Skipping stage TileFeature visual binding for TileId {binding.TileId}; cell '{cell}' could not resolve a presentation pose.");
                        continue;
                    }

                    hasResolvedPose = true;
                }

                var instance = UnityEngine.Object.Instantiate(binding.VisualPrefab, parent, worldPositionStays: false);
                instance.name = binding.VisualPrefab.name;
                if (hasResolvedPose)
                {
                    instance.transform.localPosition = resolvedPose.LocalPosition;
                    instance.transform.localRotation = resolvedPose.LocalRotation;
                    instance.transform.localScale = resolvedPose.LocalScale;
                }

                if (!TryGetConfigurableTileFeatureVisualTarget(
                        instance,
                        out var target,
                        out var configurator))
                {
                    UnityEngine.Debug.LogWarning(
                        $"Skipping stage TileFeature visual binding for TileId {binding.TileId}; prefab '{binding.VisualPrefab.name}' has no configurable tile visual target.");
                    UnityEngine.Object.Destroy(instance);
                    continue;
                }

                configurator.ConfigureTileFeature(binding.TileId, cell);
                registry.Register(target);
            }

            registry.Rebuild();
        }

        private static bool TryGetTileFeatureCell(
            IReadOnlyList<TileFeatureState> initialTileFeatures,
            int tileId,
            out SurfaceCell cell)
        {
            if (initialTileFeatures != null)
            {
                for (var i = 0; i < initialTileFeatures.Count; i++)
                {
                    var tileFeature = initialTileFeatures[i];
                    if (tileFeature.TileId == tileId)
                    {
                        cell = tileFeature.Cell;
                        return true;
                    }
                }
            }

            cell = default;
            return false;
        }

        private static bool TryGetConfigurableTileFeatureVisualTarget(
            GameObject instance,
            out ITileFeatureVisualTarget target,
            out ITileFeatureVisualTargetConfigurator configurator)
        {
            var targetView = instance.GetComponentInChildren<TileFeatureVisualTargetView>(includeInactive: true);
            if (targetView != null)
            {
                target = targetView;
                configurator = targetView;
                return true;
            }

            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITileFeatureVisualTarget candidateTarget &&
                    behaviours[i] is ITileFeatureVisualTargetConfigurator candidateConfigurator)
                {
                    target = candidateTarget;
                    configurator = candidateConfigurator;
                    return true;
                }
            }

            target = null;
            configurator = null;
            return false;
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

        private static void AttachAudioRuntimesIfConfigured(
            GameObject hostObject,
            GameplayTickViewPresenter presenter,
            GameplaySceneHostConfiguration configuration)
        {
            var hasGameplayAudioMap = configuration?.GameplayAudioMap != null;
            var hasTileFeatureAudioMap = configuration?.TileFeatureAudioMap != null;
            var hasGravityFieldAudioMap = configuration?.GravityFieldAudioMap != null;
            var hasBlockAudioMap = configuration?.BlockAudioMap != null;
            if (!hasGameplayAudioMap &&
                !hasTileFeatureAudioMap &&
                !hasGravityFieldAudioMap &&
                !hasBlockAudioMap)
            {
                return;
            }

            var audioRuntimeInstaller = hostObject.GetComponent<AudioRuntimeInstaller>();
            if (audioRuntimeInstaller == null)
            {
                throw new InvalidOperationException(ResolveMissingAudioRuntimeInstallerMessage(
                    hasGameplayAudioMap,
                    hasTileFeatureAudioMap,
                    hasGravityFieldAudioMap,
                    hasBlockAudioMap));
            }

            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioService == null)
            {
                throw new InvalidOperationException(ResolveMissingAudioRuntimeInstallerMessage(
                    hasGameplayAudioMap,
                    hasTileFeatureAudioMap,
                    hasGravityFieldAudioMap,
                    hasBlockAudioMap));
            }

            var playbackPort = new GameplayAudioPlaybackPortAdapter(audioRuntimeInstaller.AudioService);
            if (hasGameplayAudioMap)
            {
                presenter.AttachGameplayAudioRuntime(playbackPort, configuration.GameplayAudioMap);
            }

            if (hasTileFeatureAudioMap)
            {
                presenter.AttachTileFeatureAudioRuntime(playbackPort, configuration.TileFeatureAudioMap);
            }

            if (hasGravityFieldAudioMap)
            {
                presenter.AttachGravityFieldAudioRuntime(playbackPort, configuration.GravityFieldAudioMap);
            }

            if (hasBlockAudioMap)
            {
                presenter.AttachBlockAudioRuntime(playbackPort, configuration.BlockAudioMap);
            }
        }

        private static string ResolveMissingAudioRuntimeInstallerMessage(
            bool hasGameplayAudioMap,
            bool hasTileFeatureAudioMap,
            bool hasGravityFieldAudioMap,
            bool hasBlockAudioMap)
        {
            if (hasGameplayAudioMap)
            {
                return MissingGameplayAudioRuntimeInstallerMessage;
            }

            if (hasTileFeatureAudioMap)
            {
                return MissingTileFeatureAudioRuntimeInstallerMessage;
            }

            if (hasGravityFieldAudioMap)
            {
                return MissingGravityFieldAudioRuntimeInstallerMessage;
            }

            return hasBlockAudioMap
                ? MissingBlockAudioRuntimeInstallerMessage
                : MissingGameplayAudioRuntimeInstallerMessage;
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
