using System;
using System.Collections.Generic;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;
using Game.Shared.Audio;
using Game.Shared.Input;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public static class GameplayHostRuntimeFactory
    {
        private const string BoardRootObjectName = "GameplayBoardRoot";
        private const string WorldGuideRootObjectName = "WorldGuideRoot";
        private const string MissingGameplayPresentationAudioRuntimeInstallerMessage =
            "GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GameplayPresentationAudioConfig is assigned.";

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

            if (configuration.TerminalSessionReadModel == null)
            {
                throw new InvalidOperationException(
                    "GameplaySceneHost requires the persistent terminal-session read model.");
            }

            if (configuration.SceneEntryPresentationReadModel == null)
            {
                throw new InvalidOperationException(
                    "GameplaySceneHost requires the persistent scene-entry presentation read model.");
            }

            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                host.gameObject.scene.handle,
                host.gameObject.scene.name);

            var hostObject = host.gameObject;
            var hostTransform = host.transform;
            var inputHost = hostObject.GetComponent<GameplayInputHost>() ?? hostObject.AddComponent<GameplayInputHost>();
            var presenter = hostObject.GetComponent<GameplayTickViewPresenter>() ?? hostObject.AddComponent<GameplayTickViewPresenter>();
            var viewRegistry = hostObject.GetComponent<GameplayEntityViewRegistry>() ?? hostObject.AddComponent<GameplayEntityViewRegistry>();
            var tileFeatureVisualRegistry =
                hostObject.GetComponent<TileFeatureVisualRegistry>() ?? hostObject.AddComponent<TileFeatureVisualRegistry>();
            var presentationDependencies = DiscoverPresentationHostDependencies(hostObject);
            var presentationComposition = GameplayPresentationRuntimeCompositionFactory.Create(
                new GameplayPresentationRuntimeCompositionFactoryOptions
                {
                    DamageDeathVfxPlaybackPort = presentationDependencies.DamageDeathVfxPlaybackPort,
                });
            var presentationCoordinator = new GameplayTickPresentationCoordinator(presentationComposition);
            presenter.BindCoordinator(presentationCoordinator);

            var initialEntities = configuration.InitialEntities ?? Array.Empty<EntityState>();
            var initialTileFeatures = configuration.InitialTileFeatures ?? Array.Empty<TileFeatureState>();
            var tileFeatureDefinitions = configuration.TileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
            var moonBlockRespawnDefinitions =
                configuration.MoonBlockRespawnDefinitions ?? Array.Empty<MoonBlockRespawnDefinition>();
            var generalTimingProfile = configuration.CreateTimingProfile();
            var playerControlTiming = configuration.CreatePlayerControlTimingSnapshot();
            var unitKinematicLocomotionTiming = configuration.CreateUnitKinematicLocomotionTimingSnapshot();
            var playerContinuousLocomotion = configuration.CreatePlayerContinuousLocomotionSnapshot();
            var playerRespawnTiming = configuration.CreatePlayerRespawnTimingSnapshot();
            var enemyAiRuntime = configuration.CreateEnemyAiRuntimeSnapshot();
            var enemyPresentationArchetypeRegistry = configuration.CreateEnemyPresentationArchetypeRegistry(enemyAiRuntime);
            var faceSeamGap = configuration.ResolveFaceSeamGap();
            var playerViewPrefab = ResolvePlayerViewPrefab(configuration);
            var normalizedInitialEntities = NormalizeInitialEntitiesForRuntime(initialEntities, generalTimingProfile);
            DebugSpawnValidityPolicy.EnsureRepresentable(
                configuration.InitialBoardBounds,
                configuration.InitialTopology,
                normalizedInitialEntities);

            var worldState = GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                configuration.InitialBoardBounds,
                configuration.InitialTopology,
                initialTileFeatures);
            var initialSnapshot = GameplayCompositionRoot.CreateSnapshot(worldState);
            var presentedInitialEntities = new List<EntityState>();
            initialSnapshot.EnumerateEntitiesOrdered(presentedInitialEntities);
            var initialPresentationData = BuildInitialPresentationData(
                presentedInitialEntities,
                configuration.InitialTopology,
                initialTileFeatures);
            var initialObjectiveResult = ResolveInitialObjectiveResult(
                configuration.ObjectiveRuntimeDefinition,
                initialSnapshot);

            var inputBuffer = new TickInputBuffer();
            var demoGameplayOverrideRuntime = new DemoGameplayOverrideRuntime(DemoStageControlSettings.EnabledByDefault());
            var bootstrapper = new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    enemyAiRuntime.DefaultDefinition,
                    enemyAiRuntime.DefinitionsByEntityId,
                    enemyAiRuntime.DefinitionsByArchetypeId,
                    enemyAiRuntime.HasDefaultDefinition),
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
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming,
                playerContinuousLocomotion: playerContinuousLocomotion,
                tileFeatureDefinitions: tileFeatureDefinitions,
                moonBlockRespawnDefinitions: moonBlockRespawnDefinitions,
                demoGameplayOverrideSnapshotSource: demoGameplayOverrideRuntime);

            var boardRoot = EnsureBoardRootHierarchy(hostTransform);
            boardRoot.AttachBoardPresentationProfile(configuration.BoardPresentationProfile);
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
                        BuildStaticViewPrefabs(configuration),
                        configuration.EnemyInactiveVisualSettings)
                    : null);
            var viewBinder = new GameplayEntityViewBinder(viewRegistry, viewFactory);
            var tileFeaturePoseResolver = new BoardSurfaceCellPresentationPoseResolver(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology,
                faceSeamGap);
            var tileFeatureVisualPoseSynchronizer = new TileFeatureVisualPoseSynchronizer(
                tileFeatureVisualRegistry,
                tileFeaturePoseResolver);
            InstantiateStageTileFeatureVisuals(
                configuration.TileFeaturePresentationBindings,
                initialTileFeatures,
                tileFeatureDefinitions,
                configuration.InitialTopology,
                boardRoot.transform,
                tileFeatureVisualRegistry,
                tileFeaturePoseResolver,
                tileFeatureVisualPoseSynchronizer,
                initialSnapshot,
                initialObjectiveResult);

            presenter.ConfigureStaticWallPresentationProvenance(
                configuration.StaticWallPresentationProvenance);
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
                configuration.EnemyPresentationBindings,
                BuildTileFeatureVfxStyleBindings(configuration.TileFeaturePresentationBindings),
                configuration.EnemyInactiveVisualSettings);
            presenter.AttachTileFeatureVisualRegistry(tileFeatureVisualRegistry);
            presenter.AttachTileFeatureVisualPoseSynchronizer(tileFeatureVisualPoseSynchronizer);
            presenter.ConfigurePlayerAnchorContext(
                configuration.PlayerEntityId,
                boardRoot.transform);
            AttachPresentationExtensions(presenter, presentationDependencies.PresentationExtensions);
            AttachAudioRuntimesIfConfigured(hostObject, presenter, configuration);

            boardSurfaceRenderer.Initialize(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology,
                faceSeamGap,
                configuration.BoardSurfaceTexture,
                configuration.BoardTilePresentationCatalog,
                configuration.BoardTileStyleCatalog,
                configuration.BoardTilePaintOverrides,
                configuration.SuppressedBaseTileCells,
                configuration.BoardPresentationProfile != null
                    ? configuration.BoardPresentationProfile.ActiveFaceCoverPrefab
                    : null);

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
            KeyboardBindingSettingsService.ApplySavedSettings(
                configuration.Actions,
                configuration.KeyboardBindingStore);
            AttachWorldGuidePresenter(
                hostObject,
                configuration,
                boardSurfaceRenderer,
                tileFeaturePoseResolver,
                viewCamera,
                boardRoot.transform);

            presenter.PresentInitial(presentedInitialEntities, configuration.InitialTopology, initialPresentationData);
            inputHost.Initialize(
                inputBuffer,
                tickRunner,
                presenter,
                configuration.Actions,
                generalTimingProfile,
                configuration.PlayerEntityId,
                configuration.MoveDeadzone,
                configuration.DirectionChangeConsumesDelay,
                configuration.AutoAdvanceTicks,
                configuration.TerminalSessionReadModel,
                configuration.SceneEntryPresentationReadModel);
            var pauseService = new GameplayHostPauseService(inputHost, presenter);
            var admissionPolicy = new GameplayHostCommandAdmissionPolicy(worldState, tickRunner, inputHost, presenter, pauseService);
            var presentationBarrierTracker = new GameplayPresentationBarrierTracker();
            var presentationFeed = new GameplayHostPresentationFeed(
                inputHost,
                presenter,
                configuration.StageContentEntry,
                generalTimingProfile,
                presentationBarrierTracker,
                configuration.CampaignStageSequenceResolver);
            var uiAccess = new GameplayHostUiAccessContext(
                new GameplayHostCommandGateway(inputHost, admissionPolicy),
                new GameplayQueryFacade(
                    new GameplayHostSessionQuery(tickRunner, pauseService, admissionPolicy),
                    new GameplayHostStageQuery(configuration.StageContentEntry),
                    new GameplayHostPlayerHudQuery(
                        inputHost,
                        admissionPolicy,
                        configuration.CampaignChancesReadSource),
                    new GameplayHostObjectiveQuery(tickRunner, presentationBarrierTracker),
                    new GameplayHostSurfaceButtonRemainderQuery(admissionPolicy, tileFeatureDefinitions)),
                presentationFeed,
                pauseService,
                demoGameplayOverrideRuntime,
                new GameplayHostDemoStageControlCompletionBridge(presentationFeed),
                configuration.CampaignStageSequenceResolver);

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
                startupPlan.OutputCamera,
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

        private static void AttachWorldGuidePresenter(
            GameObject hostObject,
            GameplaySceneHostConfiguration configuration,
            GameplayBoardSurfaceRenderer boardSurfaceRenderer,
            ISurfaceCellPresentationPoseResolver poseResolver,
            Camera viewCamera,
            Transform boardRoot)
        {
            if (configuration.WorldGuideCatalog == null ||
                configuration.WorldGuideInstructions == null ||
                configuration.WorldGuideInstructions.Count == 0 ||
                boardRoot == null)
            {
                return;
            }

            var root = boardRoot.Find(WorldGuideRootObjectName);
            if (root == null)
            {
                var rootObject = new GameObject(WorldGuideRootObjectName);
                root = rootObject.transform;
                root.SetParent(boardRoot, worldPositionStays: false);
            }

            var presenter = hostObject.GetComponent<GameplayWorldGuidePresenter>() ??
                            hostObject.AddComponent<GameplayWorldGuidePresenter>();
            presenter.Initialize(
                configuration.WorldGuideCatalog,
                configuration.WorldGuideInstructions,
                boardSurfaceRenderer,
                poseResolver,
                viewCamera,
                root,
                configuration.Actions);
        }

        private static PresentationHostDependencyDiscovery DiscoverPresentationHostDependencies(GameObject hostObject)
        {
            IDamageDeathVfxPlaybackPort damageDeathVfxPlaybackPort = null;
            var presentationExtensions = new List<IGameplayTickPresentationExtension>();
            var behaviours = hostObject.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null ||
                    !behaviour.enabled ||
                    !behaviour.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (behaviour is IDamageDeathGameplayVfxPlaybackRuntime damageDeathVfxRuntime)
                {
                    if (damageDeathVfxPlaybackPort != null)
                    {
                        throw new InvalidOperationException(
                            "GameplaySceneHost requires exactly one host-local Damage/Death VFX playback runtime.");
                    }

                    damageDeathVfxPlaybackPort =
                        new DamageDeathGameplayVfxPlaybackPortAdapter(damageDeathVfxRuntime);
                }

                if (behaviour is IGameplayTickPresentationExtension extension)
                {
                    presentationExtensions.Add(extension);
                }
            }

            return new PresentationHostDependencyDiscovery(
                presentationExtensions.ToArray(),
                damageDeathVfxPlaybackPort);
        }

        private static void AttachPresentationExtensions(
            GameplayTickViewPresenter presenter,
            IReadOnlyList<IGameplayTickPresentationExtension> presentationExtensions)
        {
            if (presentationExtensions == null)
            {
                return;
            }

            for (var i = 0; i < presentationExtensions.Count; i++)
            {
                presenter.AttachPresentationExtension(presentationExtensions[i]);
            }
        }

        private sealed class PresentationHostDependencyDiscovery
        {
            public PresentationHostDependencyDiscovery(
                IReadOnlyList<IGameplayTickPresentationExtension> presentationExtensions,
                IDamageDeathVfxPlaybackPort damageDeathVfxPlaybackPort)
            {
                PresentationExtensions = presentationExtensions ?? Array.Empty<IGameplayTickPresentationExtension>();
                DamageDeathVfxPlaybackPort = damageDeathVfxPlaybackPort;
            }

            public IReadOnlyList<IGameplayTickPresentationExtension> PresentationExtensions { get; }

            public IDamageDeathVfxPlaybackPort DamageDeathVfxPlaybackPort { get; }
        }

        private static IReadOnlyDictionary<int, GameplayEntityView> BuildStaticViewPrefabs(
            GameplaySceneHostConfiguration configuration)
        {
            return StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                configuration?.StaticEntityPresentationCatalog,
                configuration?.StaticEntityPresentationBindings,
                nameof(GameplaySceneHostConfiguration));
        }

        private static IReadOnlyList<TileFeatureVfxStyleBinding> BuildTileFeatureVfxStyleBindings(
            IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0)
            {
                return Array.Empty<TileFeatureVfxStyleBinding>();
            }

            var result = new TileFeatureVfxStyleBinding[bindings.Count];
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                result[i] = new TileFeatureVfxStyleBinding(binding.TileId, binding.VfxStyleKey);
            }

            return result;
        }

        internal static InitialPresentationData BuildInitialPresentationData(
            IReadOnlyList<EntityState> initialEntities,
            CubeTopologyState initialTopology,
            IReadOnlyList<TileFeatureState> initialTileFeatures)
        {
            if (initialEntities == null || initialEntities.Count == 0)
            {
                return InitialPresentationData.Empty;
            }

            var signals = new List<EntitySpawnPresentationSignal>();
            for (var i = 0; i < initialEntities.Count; i++)
            {
                var entity = initialEntities[i];
                if (!EntityRolePolicy.IsPlayerUnit(entity))
                {
                    continue;
                }

                signals.Add(
                    new EntitySpawnPresentationSignal(
                        entity.entityId,
                        EntityPresentationKind.Player,
                        EntitySpawnPresentationReason.InitialStageStart,
                        entity.position,
                        initialTopology,
                        entity.facing,
                        EntitySpawnPresentationSourceResolver.TryResolveEntranceSource(
                            entity.position,
                            initialTileFeatures)));
            }

            return signals.Count == 0
                ? InitialPresentationData.Empty
                : new InitialPresentationData(signals);
        }

        private static void InstantiateStageTileFeatureVisuals(
            IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings,
            IReadOnlyList<TileFeatureState> initialTileFeatures,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            CubeTopologyState initialTopology,
            Transform parent,
            TileFeatureVisualRegistry registry,
            ISurfaceCellPresentationPoseResolver poseResolver = null,
            TileFeatureVisualPoseSynchronizer poseSynchronizer = null,
            WorldSnapshot initialSnapshot = null,
            StageObjectiveTickResult initialObjectiveResult = null)
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

                if (!TryGetTileFeatureState(initialTileFeatures, binding.TileId, out var tileFeature))
                {
                    UnityEngine.Debug.LogWarning($"Skipping stage TileFeature visual binding for missing TileId {binding.TileId}.");
                    continue;
                }

                var cell = tileFeature.Cell;
                var instance = UnityEngine.Object.Instantiate(binding.VisualPrefab, parent, worldPositionStays: false);
                instance.name = binding.VisualPrefab.name;

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
                if (target is TileFeatureVisualTargetView targetView)
                {
                    targetView.ConfigurePresentationRoot(instance.transform);
                }

                registry.Register(target);
                poseSynchronizer ??= poseResolver != null
                    ? new TileFeatureVisualPoseSynchronizer(registry, poseResolver)
                    : null;
                poseSynchronizer?.Refresh(target);

                if (TryResolveInitialBarricadeActive(
                        tileFeature,
                        tileFeatureDefinitions,
                        initialTopology,
                        initialSnapshot,
                        out var barricadeActive))
                {
                    if (!TryApplyInitialBarricadeActiveState(target, binding.TileId, cell, barricadeActive))
                    {
                        UnityEngine.Debug.LogWarning(
                            $"Skipping initial barricade visual state sync for TileId {binding.TileId}; target has no cue sink.");
                    }
                }
                else if (TryResolveInitialTileFeatureActive(
                             tileFeature,
                             tileFeatureDefinitions,
                             initialTopology,
                             out var tileFeatureActive))
                {
                    if (!TryApplyInitialTileFeatureActiveState(target, binding.TileId, cell, tileFeature.Kind, tileFeatureActive))
                    {
                        UnityEngine.Debug.LogWarning(
                            $"Skipping initial {tileFeature.Kind} visual state sync for TileId {binding.TileId}; target has no cue sink.");
                    }
                }
                else if (TryResolveInitialExitOpen(
                             tileFeature,
                             tileFeatureDefinitions,
                             initialTopology,
                             initialObjectiveResult,
                             out var exitOpen))
                {
                    if (!TryApplyInitialExitOpenState(target, binding.TileId, cell, exitOpen))
                    {
                        UnityEngine.Debug.LogWarning(
                            $"Skipping initial exit visual state sync for TileId {binding.TileId}; target has no cue sink.");
                    }
                }
            }

            registry.Rebuild();
        }

        private static StageObjectiveTickResult ResolveInitialObjectiveResult(
            StageObjectiveRuntimeDefinition objectiveRuntimeDefinition,
            WorldSnapshot initialSnapshot)
        {
            if (initialSnapshot == null)
            {
                return StageObjectiveTickResult.NoObjective;
            }

            var tracker = (objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled).CreateTracker();
            return tracker.Advance(initialSnapshot, StageObjectiveTickFacts.Empty);
        }

        private static bool TryGetTileFeatureState(
            IReadOnlyList<TileFeatureState> initialTileFeatures,
            int tileId,
            out TileFeatureState tileFeature)
        {
            if (initialTileFeatures != null)
            {
                for (var i = 0; i < initialTileFeatures.Count; i++)
                {
                    var candidate = initialTileFeatures[i];
                    if (candidate.TileId == tileId)
                    {
                        tileFeature = candidate;
                        return true;
                    }
                }
            }

            tileFeature = default;
            return false;
        }

        private static bool TryResolveInitialBarricadeActive(
            TileFeatureState tileFeature,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            CubeTopologyState initialTopology,
            WorldSnapshot initialSnapshot,
            out bool active)
        {
            if (tileFeature.Kind != TileFeatureKind.Barricade ||
                !TryGetTileFeatureDefinition(tileFeatureDefinitions, tileFeature.TileId, out var definition))
            {
                active = false;
                return false;
            }

            active = initialSnapshot != null
                ? BarricadeEffectiveActivationPolicy.Evaluate(
                    initialSnapshot,
                    initialTopology,
                    tileFeature,
                    definition).EffectiveActive
                : TileFeatureActivationQueries.IsActive(tileFeature, definition, initialTopology);
            return true;
        }

        private static bool TryResolveInitialTileFeatureActive(
            TileFeatureState tileFeature,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            CubeTopologyState initialTopology,
            out bool active)
        {
            if ((tileFeature.Kind != TileFeatureKind.Destroy &&
                 tileFeature.Kind != TileFeatureKind.Slide) ||
                !TryGetTileFeatureDefinition(tileFeatureDefinitions, tileFeature.TileId, out var definition))
            {
                active = false;
                return false;
            }

            active = TileFeatureActivationQueries.IsActive(tileFeature, definition, initialTopology);
            return true;
        }

        private static bool TryResolveInitialExitOpen(
            TileFeatureState tileFeature,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            CubeTopologyState initialTopology,
            StageObjectiveTickResult initialObjectiveResult,
            out bool open)
        {
            if (tileFeature.Kind != TileFeatureKind.Exit ||
                !TryGetTileFeatureDefinition(tileFeatureDefinitions, tileFeature.TileId, out var definition))
            {
                open = false;
                return false;
            }

            open = initialObjectiveResult != null &&
                   initialObjectiveResult.HasObjective &&
                   initialObjectiveResult.RequiredNonPrimaryConditionsSatisfied &&
                   TileFeatureActivationQueries.IsActive(tileFeature, definition, initialTopology);
            return true;
        }

        private static bool TryGetTileFeatureDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            if (tileFeatureDefinitions != null)
            {
                for (var i = 0; i < tileFeatureDefinitions.Count; i++)
                {
                    if (tileFeatureDefinitions[i].TileId == tileId)
                    {
                        definition = tileFeatureDefinitions[i];
                        return true;
                    }
                }
            }

            definition = default;
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

        private static ITileFeatureVisualCueSink ResolveTileFeatureCueSink(
            ITileFeatureVisualTarget target,
            TileFeatureKind featureKind)
        {
            return TileFeatureVisualCueSinkResolver.Resolve(target, featureKind);
        }

        private static bool TryApplyInitialBarricadeActiveState(
            ITileFeatureVisualTarget target,
            int tileId,
            SurfaceCell cell,
            bool active)
        {
            var sink = ResolveTileFeatureCueSink(target, TileFeatureKind.Barricade);
            if (sink != null)
            {
                return sink.TryHandle(
                    new TileFeatureVisualRequest(
                        TileFeatureVisualCueId.BarricadeActiveState,
                        tileId,
                        cell,
                        TileFeatureKind.Barricade,
                        active: active));
            }

            return false;
        }

        private static bool TryApplyInitialTileFeatureActiveState(
            ITileFeatureVisualTarget target,
            int tileId,
            SurfaceCell cell,
            TileFeatureKind featureKind,
            bool active)
        {
            var cueId = featureKind switch
            {
                TileFeatureKind.Destroy => TileFeatureVisualCueId.DestroyTileActiveState,
                TileFeatureKind.Slide => TileFeatureVisualCueId.SlideTileActiveState,
                _ => TileFeatureVisualCueId.None,
            };

            if (cueId == TileFeatureVisualCueId.None)
            {
                return false;
            }

            var sink = ResolveTileFeatureCueSink(target, featureKind);
            if (sink != null)
            {
                return sink.TryHandle(
                    new TileFeatureVisualRequest(
                        cueId,
                        tileId,
                        cell,
                        featureKind,
                        active: active));
            }

            return false;
        }

        private static bool TryApplyInitialExitOpenState(
            ITileFeatureVisualTarget target,
            int tileId,
            SurfaceCell cell,
            bool open)
        {
            var sink = ResolveTileFeatureCueSink(target, TileFeatureKind.Exit);
            if (sink != null)
            {
                return sink.TryHandle(
                    new TileFeatureVisualRequest(
                        TileFeatureVisualCueId.ExitOpenState,
                        tileId,
                        cell,
                        TileFeatureKind.Exit,
                        active: open));
            }

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
            var audioConfig = configuration?.GameplayPresentationAudioConfig;
            if (audioConfig == null)
            {
                return;
            }

            audioConfig.ValidateOrThrow();

            var audioRuntimeInstaller = hostObject.GetComponent<AudioRuntimeInstaller>();
            if (audioRuntimeInstaller == null)
            {
                throw new InvalidOperationException(MissingGameplayPresentationAudioRuntimeInstallerMessage);
            }

            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioService == null)
            {
                throw new InvalidOperationException(MissingGameplayPresentationAudioRuntimeInstallerMessage);
            }

            var playbackPort = new GameplayAudioPlaybackPortAdapter(audioRuntimeInstaller.AudioService);
            presenter.AttachGameplayAudioRuntime(playbackPort, audioConfig.GameplayAudioMap);
            presenter.AttachTileFeatureAudioRuntime(playbackPort, audioConfig.TileFeatureAudioMap);
            presenter.AttachTopologyAudioRuntime(playbackPort, audioConfig.TopologyAudioMap);
            presenter.AttachGravityFieldAudioRuntime(playbackPort, audioConfig.GravityFieldAudioMap);
            presenter.AttachBlockAudioRuntime(playbackPort, audioConfig.BlockAudioMap);
            presenter.AttachPlayerLocomotionAudioRuntime(playbackPort, audioConfig.PlayerLocomotionAudioMap);
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
