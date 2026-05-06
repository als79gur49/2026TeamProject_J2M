using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureEffectResolverTests
    {
        private static readonly BoardBounds TestBounds = new(
            Vector2Int.zero,
            new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void ButtonLatch_InactiveActivationRule_DoesNotLatch()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Back, 1, 1));
            var box = CreateBox(20, button.Cell);
            var result = Resolve(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_ActiveAnyPushableBoxOnSameCell_LatchesWithSingleUpdate()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var result = Resolve(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                CreateDefinition(10));

            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.Operations.Operations.Count, Is.EqualTo(1));
            var operation = result.Operations.Operations[0];
            Assert.That(operation.Kind, Is.EqualTo(TileFeatureOperationKind.Update));
            Assert.That(operation.TileId, Is.EqualTo(10));
            Assert.That(operation.State.Flags, Is.EqualTo(TileFeatureFlags.Activated));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_NonAcceptedOccupants_DoNotLatch()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var cases = new[]
            {
                new object[] { "Unit", new[] { CreateUnit(20, cell) }, GameplayTerrainData.Empty },
                new object[] { "Projectile", new[] { CreateProjectile(20, cell) }, GameplayTerrainData.Empty },
                new object[] { "WallLikeTerrain", Array.Empty<EntityState>(), new GameplayTerrainData(new[] { CreateWallLikeTerrain(cell) }) },
                new object[] { "NonPushableBox", new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Flip) }, GameplayTerrainData.Empty },
                new object[] { "DetachedBox", new[] { CreateBox(20, cell, boardPresence: EntityBoardPresence.Detached) }, GameplayTerrainData.Empty },
                new object[] { "DeadBox", new[] { CreateBox(20, cell, hp: 0) }, GameplayTerrainData.Empty },
                new object[] { "MarkedForDeathBox", new[] { CreateBox(20, cell, markedForDeath: true) }, GameplayTerrainData.Empty },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var terrain = (GameplayTerrainData)cases[i][2];
                var button = CreateButton(10, cell);
                var result = Resolve(
                    CreateWorldState(entities, new[] { button }, terrain).CreateSnapshot(),
                    CreateDefinition(10));

                Assert.That(result.IsEmpty, Is.True, name);
            }
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_AlreadyActivated_DoesNotRelatch()
        {
            var button = CreateButton(
                10,
                new SurfaceCell(FaceId.Floor, 1, 1),
                TileFeatureFlags.Activated);
            var box = CreateBox(20, button.Cell);
            var result = Resolve(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                CreateDefinition(10));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_MissingDefinitionAndUnsupportedSelectors_DoNotLatch()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var snapshot = CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot();

            Assert.That(Resolve(snapshot).IsEmpty, Is.True);
            Assert.That(Resolve(snapshot, CreateDefinition(10, selector: TileFeatureBoxSelector.None)).IsEmpty, Is.True);
            Assert.That(Resolve(snapshot, CreateDefinition(10, selector: TileFeatureBoxSelector.FeatureCell)).IsEmpty, Is.True);
            Assert.That(Resolve(snapshot, CreateDefinition(10, selector: TileFeatureBoxSelector.BoundEntity)).IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_MoonBlockOnly_LatchesMoonBoxOnSameCell()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var moonBox = CreateBox(
                20,
                button.Cell,
                boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype: BoxArchetype.Moon);
            var result = Resolve(
                CreateWorldState(new[] { moonBox }, new[] { button }).CreateSnapshot(),
                CreateDefinition(10, selector: TileFeatureBoxSelector.MoonBlockOnly));

            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.Operations.Operations.Count, Is.EqualTo(1));
            Assert.That(result.Operations.Operations[0].State.Flags, Is.EqualTo(TileFeatureFlags.Activated));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_MoonBlockOnly_RejectsNonMoonOrInvalidOccupants()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var moonCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy;
            var cases = new[]
            {
                new object[] { "NormalPushableBox", new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Push) }, GameplayTerrainData.Empty },
                new object[] { "Unit", new[] { CreateUnit(20, cell) }, GameplayTerrainData.Empty },
                new object[] { "Projectile", new[] { CreateProjectile(20, cell) }, GameplayTerrainData.Empty },
                new object[] { "WallLikeTerrain", Array.Empty<EntityState>(), new GameplayTerrainData(new[] { CreateWallLikeTerrain(cell) }) },
                new object[] { "DetachedMoonBox", new[] { CreateBox(20, cell, boxCapabilities: moonCapabilities, boardPresence: EntityBoardPresence.Detached, boxArchetype: BoxArchetype.Moon) }, GameplayTerrainData.Empty },
                new object[] { "DeadMoonBox", new[] { CreateBox(20, cell, hp: 0, boxCapabilities: moonCapabilities, boxArchetype: BoxArchetype.Moon) }, GameplayTerrainData.Empty },
                new object[] { "MarkedMoonBox", new[] { CreateBox(20, cell, boxCapabilities: moonCapabilities, markedForDeath: true, boxArchetype: BoxArchetype.Moon) }, GameplayTerrainData.Empty },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var terrain = (GameplayTerrainData)cases[i][2];
                var button = CreateButton(10, cell);
                var result = Resolve(
                    CreateWorldState(entities, new[] { button }, terrain).CreateSnapshot(),
                    CreateDefinition(10, selector: TileFeatureBoxSelector.MoonBlockOnly));

                Assert.That(result.IsEmpty, Is.True, name);
            }
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_AnyPushableBox_LatchesMoonBoxWithPushCapability()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var moonBox = CreateBox(
                20,
                button.Cell,
                boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype: BoxArchetype.Moon);
            var result = Resolve(
                CreateWorldState(new[] { moonBox }, new[] { button }).CreateSnapshot(),
                CreateDefinition(10, selector: TileFeatureBoxSelector.AnyPushableBox));

            Assert.That(result.IsEmpty, Is.False);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_MultipleButtons_ProduceDeterministicOrderedUpdatesWithoutDuplicates()
        {
            var first = CreateButton(30, new SurfaceCell(FaceId.Floor, 1, 0));
            var second = CreateButton(10, new SurfaceCell(FaceId.Floor, 0, 0));
            var third = CreateButton(20, new SurfaceCell(FaceId.Floor, 0, 0));
            var snapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(130, first.Cell),
                        CreateBox(110, second.Cell),
                    },
                    new[] { first, second, third })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                CreateDefinition(30),
                CreateDefinition(10),
                CreateDefinition(20));

            var tileIds = result.Operations.Operations.Select(operation => operation.TileId).ToArray();
            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, tileIds);
            Assert.That(tileIds.Distinct().Count(), Is.EqualTo(tileIds.Length));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_IsVisibleInFinalAttackReadSnapshot_AndAppliedToAuthoritativeWorld()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var attackLogic = new CapturingAttackLogic();
            var worldState = CreateWorldState(new[] { box }, new[] { button });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(10) },
                new[] { attackLogic });

            pipeline.RunTick(new TickInput(7));

            Assert.That(attackLogic.CapturedSnapshots.Count, Is.EqualTo(2));
            Assert.That(attackLogic.CapturedSnapshots[1].TryGetTileFeature(10, out var attackReadButton), Is.True);
            Assert.That((attackReadButton.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));

            var finalSnapshot = worldState.CreateSnapshot();
            Assert.That(finalSnapshot.TryGetTileFeature(10, out var storedButton), Is.True);
            Assert.That((storedButton.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_ActivatedFlagChangesDeterminismHash()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var inactiveSnapshot = CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[] { CreateButton(10, cell) })
                .CreateSnapshot();
            var activatedSnapshot = CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[] { CreateButton(10, cell, TileFeatureFlags.Activated) })
                .CreateSnapshot();
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                hashBuilder.Build(3, inactiveSnapshot, CreateTickResultData(inactiveSnapshot)),
                Is.Not.EqualTo(hashBuilder.Build(3, activatedSnapshot, CreateTickResultData(activatedSnapshot))));
        }

        [Test]
        [Category("Core")]
        public void BoxArchetype_ChangesDeterminismHash()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var normalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var moonSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy, boxArchetype: BoxArchetype.Moon) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                hashBuilder.Build(3, normalSnapshot, CreateTickResultData(normalSnapshot)),
                Is.Not.EqualTo(hashBuilder.Build(3, moonSnapshot, CreateTickResultData(moonSnapshot))));
        }

        [Test]
        [Category("Core")]
        public void DefaultResolver_NoButtonStage_PreservesSnapshotBudget()
        {
            var worldState = CreateWorldState(
                Array.Empty<EntityState>(),
                new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Exit) });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                pipeline.RunTick(new TickInput(7));
                counts = capture.Counts;
            }

            AssertPinnedEmptyBudget(counts);
        }

        [Test]
        [Category("Core")]
        public void DefaultResolver_InactiveTopologyButton_DoesNotProduceOperationOrIncreaseSnapshotBudget()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Back, 1, 1));
            var box = CreateBox(20, button.Cell);
            var worldState = CreateWorldState(new[] { box }, new[] { button });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly) },
                Array.Empty<IEntityLogic>());

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                pipeline.RunTick(new TickInput(7));
                counts = capture.Counts;
            }

            AssertPinnedEmptyBudget(counts);
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetTileFeature(10, out var storedButton), Is.True);
            Assert.That(storedButton.Flags, Is.EqualTo(TileFeatureFlags.None));
        }

        private static TileEffectResolutionResult Resolve(
            WorldSnapshot snapshot,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return TileFeatureEffectResolver.Instance.Resolve(
                new TileEffectResolutionContext(7, snapshot, definitions));
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IReadOnlyList<IEntityLogic> entityLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new TickPipeline(
                worldState,
                entityLogics,
                GameplayEntityLogicProviderFactory.CreateDefault(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                playerRespawnDelayTicks: 1,
                objectiveDefinition: null,
                enemySpawnDefaultsByArchetypeId: null,
                allowPlayerRespawn: true,
                runtimeFeatureFlags: default,
                playerKinematicLocomotionTiming: default,
                playerContinuousLocomotion: default,
                tileFeatureDefinitions: tileFeatureDefinitions,
                tileEffectResolver: null);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile)
        {
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static void AssertPinnedEmptyBudget(SnapshotMaterializationCounts counts)
        {
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(15));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(11));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(2));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(12));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(12));
        }

        private static TickResultData CreateTickResultData(WorldSnapshot snapshot)
        {
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);
            return new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>());
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TileFeatureState> initialTileFeatures,
            GameplayTerrainData terrainData = null)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                TestBounds,
                terrainData ?? GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
        }

        private static TileFeatureState CreateButton(
            int tileId,
            SurfaceCell cell,
            TileFeatureFlags flags = TileFeatureFlags.None)
        {
            return CreateTileFeature(tileId, cell, TileFeatureKind.Button, flags);
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureFlags flags = TileFeatureFlags.None)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                flags,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(
            int tileId,
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.Always,
            TileFeatureBoxSelector selector = TileFeatureBoxSelector.AnyPushableBox)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                selector,
                boundEntityId: 0,
                presentationKey: string.Empty);
        }

        private static TerrainCellState CreateWallLikeTerrain(SurfaceCell cell)
        {
            return new TerrainCellState(cell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal);
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateProjectile(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Projectile,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            int hp = 1,
            BoxCapabilities boxCapabilities = BoxCapabilities.Push,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            bool markedForDeath = false,
            BoxArchetype boxArchetype = BoxArchetype.Normal)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = Math.Max(1, hp),
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = boxCapabilities,
                boxArchetype = boxArchetype,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
            };
        }

        private sealed class CapturingAttackLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            public int ControlledEntityId => 0;

            public List<WorldSnapshot> CapturedSnapshots { get; } = new();

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                CapturedSnapshots.Add(snapshot);
            }
        }
    }
}
