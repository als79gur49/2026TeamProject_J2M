using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class MoonBlockGeneratorProductionWorldVsViewTests
    {
        private const string MechanicsShowcaseEntryPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/Levels/level-01/Stages/stage-3-1/stage-3-1_Entry.asset";

        private const string MechanicsShowcasePresentationPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/Levels/level-01/Stages/stage-3-1/stage-3-1_Presentation.asset";

        private const int PlayerEntityId = 10;
        private const int MoonBlockEntityId = 240;
        private const int GeneratorTileId = 10;

        private static readonly SurfaceCell InitialMoonCell = new(FaceId.Floor, 5, 4);
        private static readonly SurfaceCell GeneratorRespawnCell = new(FaceId.Floor, 5, 4);
        private static readonly SurfaceCell PlayerCell = new(FaceId.Floor, 7, 1);

        [Test]
        [Category("Core")]
        public void MechanicsShowcase_DestroyedMoonBlockRespawnsWorldStateAndRestoresViewBinding()
        {
            var fixture = LoadFixture();
            var worldState = GameplayCompositionRoot.CreateWorldState(
                fixture.Build.InitialEntities,
                fixture.Build.BoardBounds,
                fixture.Build.InitialTopology,
                fixture.Build.InitialTileFeatures);
            var pipeline = CreatePipeline(worldState, fixture.Build);

            var before = GameplayCompositionRoot.CreateSnapshot(worldState);
            Assert.That(before.Topology.IsFaceActive(FaceId.Floor), Is.True);
            Assert.That(before.TryGetEntity(PlayerEntityId, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(PlayerCell));
            Assert.That(before.TryGetEntity(MoonBlockEntityId, out var initialMoon), Is.True);
            Assert.That(initialMoon.position, Is.EqualTo(InitialMoonCell));
            Assert.That(before.TryGetTileFeature(GeneratorTileId, out var generator), Is.True);
            Assert.That(generator.Kind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));
            Assert.That(generator.Cell, Is.EqualTo(GeneratorRespawnCell));

            var writeContext = worldState.CreateWriteContext();
            writeContext.SetBoardPresence(MoonBlockEntityId, EntityBoardPresence.Detached);
            ((IAttackCommitContext)writeContext).MarkDestroy(MoonBlockEntityId);

            var result = pipeline.RunTick(new TickInput(1));
            var after = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(result.EventLog, Does.Contain($"CleanupRemoved|E={MoonBlockEntityId}"));
            Assert.That(
                result.EventLog,
                Does.Contain("MoonBlockGeneratorRespawnCommitted|TileId=10|E=240|Pos=(5,4)|Face=Floor|Tick=1"));
            Assert.That(result.EventLog, Has.None.Contains("MoonBlockGeneratorRespawnDeferred"));
            Assert.That(after.TryGetEntity(MoonBlockEntityId, out var respawnedMoon), Is.True);
            Assert.That(respawnedMoon.entityId, Is.EqualTo(MoonBlockEntityId));
            Assert.That(respawnedMoon.type, Is.EqualTo(EntityType.Box));
            Assert.That(respawnedMoon.boxArchetype, Is.EqualTo(BoxArchetype.Moon));
            Assert.That(respawnedMoon.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(respawnedMoon.hp, Is.GreaterThan(0));
            Assert.That(respawnedMoon.markedForDeath, Is.False);
            Assert.That(respawnedMoon.position, Is.EqualTo(GeneratorRespawnCell));
            Assert.That(after.TryGetSolidOccupantAt(GeneratorRespawnCell, out var solidOccupant), Is.True);
            Assert.That(solidOccupant.entityId, Is.EqualTo(MoonBlockEntityId));

            var generatedEvent = result.PresentationData.TileEvents.Single(evt =>
                evt.EventKind == TilePresentationEventKind.MoonBlockGenerated);
            Assert.That(generatedEvent.TileId, Is.EqualTo(GeneratorTileId));
            Assert.That(generatedEvent.Cell, Is.EqualTo(GeneratorRespawnCell));
            Assert.That(generatedEvent.TargetEntityId, Is.EqualTo(MoonBlockEntityId));
            Assert.That(generatedEvent.SpawnTick, Is.EqualTo(result.TickIndex));

            var requests = new TilePresentationRequestPlanner().BuildRequests(result.PresentationData);
            var generatedRequest = requests.Single(request =>
                request.RequestKind == TilePresentationRequestKind.MoonBlockGenerated);
            Assert.That(generatedRequest.TileId, Is.EqualTo(GeneratorTileId));
            Assert.That(generatedRequest.Cell, Is.EqualTo(GeneratorRespawnCell));
            Assert.That(generatedRequest.TargetEntityId, Is.EqualTo(MoonBlockEntityId));

            AssertEntityViewBindingRestoresForRespawn(fixture, result);
        }

        private static void AssertEntityViewBindingRestoresForRespawn(
            ProductionFixture fixture,
            TickResult result)
        {
            var root = new GameObject(nameof(AssertEntityViewBindingRestoresForRespawn));

            try
            {
                var registry = root.AddComponent<GameplayEntityViewRegistry>();
                var staticViewPrefabs = StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                    fixture.Presentation.StaticEntityPresentationCatalog,
                    fixture.Presentation.StaticEntityPresentationBindings,
                    "mechanics-showcase production view binding");
                var factory = new DefaultGameplayEntityViewFactory(
                    root.transform,
                    cellSize: 1f,
                    playerEntityId: fixture.Build.PlayerEntityId,
                    staticViewPrefabsByEntityId: staticViewPrefabs);
                var binder = new GameplayEntityViewBinder(registry, factory);
                var presenter = root.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);

                presenter.Initialize(
                    binder,
                    fixture.Build.BoardBounds,
                    fixture.Build.InitialTopology,
                    cellSize: 1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(fixture.Build.InitialEntities, fixture.Build.InitialTopology);
                Assert.That(registry.TryGetView(MoonBlockEntityId, out var initialView), Is.True);
                Assert.That(initialView.gameObject.activeSelf, Is.True);

                presenter.Present(result);

                Assert.That(registry.TryGetView(MoonBlockEntityId, out var respawnedView), Is.True);
                Assert.That(respawnedView.EntityId, Is.EqualTo(MoonBlockEntityId));
                Assert.That(respawnedView.gameObject.activeSelf, Is.True);
                Assert.That(respawnedView.gameObject.activeInHierarchy, Is.True);
                Assert.That(respawnedView.GetComponentInChildren<Renderer>(includeInactive: false), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ProductionFixture LoadFixture()
        {
            var entry = AssetDatabase.LoadAssetAtPath<StageContentEntry>(MechanicsShowcaseEntryPath);
            var presentation = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(MechanicsShowcasePresentationPath);
            Assert.That(entry, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(entry.StageId.Value, Is.EqualTo("stage-3-1"));

            var build = StageRuntimeBuilder.Build(entry.GameplayDefinition);
            var definition = build.MoonBlockRespawnDefinitions.Single(def =>
                def.GeneratorTileId == GeneratorTileId);
            Assert.That(definition.MoonBlockEntityId, Is.EqualTo(MoonBlockEntityId));
            Assert.That(definition.SpawnCell, Is.EqualTo(GeneratorRespawnCell));
            Assert.That(definition.Template.position, Is.EqualTo(InitialMoonCell));

            return new ProductionFixture(build, presentation);
        }

        private static TickPipeline CreatePipeline(WorldState worldState, StageRuntimeBuildResult build)
        {
            var timing = GameplayTimingProfile.CreateDefault();
            return new GameplayBootstrapper(EmptyEntityLogicProvider.Instance)
                .CreateTickPipeline(
                    worldState,
                    Array.Empty<IEntityLogic>(),
                    timing,
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        timing.SimulationTicksPerSecond,
                        timing.RepeatedMoveIntervalSeconds),
                    playerRespawnDelayTicks: 1,
                    objectiveDefinition: build.ObjectiveRuntimeDefinition,
                    allowPlayerRespawn: true,
                    runtimeFeatureFlags: default,
                    playerKinematicLocomotionTiming: default,
                    playerContinuousLocomotion: default,
                    tileFeatureDefinitions: build.TileFeatureDefinitions,
                    moonBlockRespawnDefinitions: build.MoonBlockRespawnDefinitions);
        }

        private readonly struct ProductionFixture
        {
            public ProductionFixture(StageRuntimeBuildResult build, StagePresentationDefinition presentation)
            {
                Build = build;
                Presentation = presentation;
            }

            public StageRuntimeBuildResult Build { get; }

            public StagePresentationDefinition Presentation { get; }
        }

        private sealed class EmptyEntityLogicProvider : ISnapshotEntityLogicProvider
        {
            public static readonly EmptyEntityLogicProvider Instance = new();

            public EntityLogicSet Build(
                WorldSnapshot snapshot,
                IReadOnlyList<IEntityLogic> staticEntityLogics)
            {
                return new EntityLogicSet(
                    Array.Empty<IPreMovementStateLogic>(),
                    Array.Empty<IEnemyAiStateLogic>(),
                    Array.Empty<IEnemyActionStateLogic>(),
                    Array.Empty<IMovementEntityLogic>(),
                    Array.Empty<IAttackEntityLogic>());
            }
        }
    }
}
