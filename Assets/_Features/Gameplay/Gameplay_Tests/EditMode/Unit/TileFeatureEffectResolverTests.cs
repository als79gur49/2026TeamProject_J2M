using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
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
        public void ContactFacts_MovingBoxOperations_CreateDeterministicContacts()
        {
            var firstCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var secondCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var snapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(20, firstCell),
                        CreateBox(10, secondCell),
                    },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(20, firstCell, CreateMovementMetadata(MovementSemanticKind.Push));
            batch.MoveEntity(10, secondCell, CreateMovementMetadata(MovementSemanticKind.Slide));

            var contacts = TickPipeline.BuildTileEffectBoxContacts(snapshot, batch);

            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                contacts.Select(contact => contact.BoxEntityId).ToArray());
            CollectionAssert.AreEqual(
                new[] { TileEffectBoxContactKind.SlideEnter, TileEffectBoxContactKind.PushEnter },
                contacts.Select(contact => contact.Kind).ToArray());
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_StationaryBoxOnDestroyTile_CreatesNoContact()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();

            var contacts = TickPipeline.BuildTileEffectBoxContacts(snapshot, new FinalizationBatch());

            Assert.That(contacts, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_ActiveBottomFace_DestroysMovingBoxOnceAndKeepsTile()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var destroyTile = CreateTileFeature(10, cell, TileFeatureKind.Destroy);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { destroyTile })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[]
                {
                    new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter),
                    new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.SlideEnter),
                },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.Operations.IsEmpty, Is.True);
            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].EntityId, Is.EqualTo(20));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(tileEvent.TileId, Is.EqualTo(10));
            Assert.That(tileEvent.Cell, Is.EqualTo(cell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Destroy));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(snapshot.TryGetTileFeature(10, out var storedTile), Is.True);
            Assert.That(storedTile.Kind, Is.EqualTo(TileFeatureKind.Destroy));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_InactiveOrStationaryOrNonBoxTargets_DoNotDestroy()
        {
            var floorCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var frontCell = new SurfaceCell(FaceId.Front, 1, 1);
            var cases = new[]
            {
                new object[]
                {
                    "InactiveFrontFace",
                    CreateWorldState(
                        new[] { CreateBox(20, frontCell) },
                        new[] { CreateTileFeature(10, frontCell, TileFeatureKind.Destroy) }).CreateSnapshot(),
                    new[] { new TileEffectBoxContact(20, frontCell, TileEffectBoxContactKind.PushEnter) },
                },
                new object[]
                {
                    "StationaryBox",
                    CreateWorldState(
                        new[] { CreateBox(20, floorCell) },
                        new[] { CreateTileFeature(10, floorCell, TileFeatureKind.Destroy) }).CreateSnapshot(),
                    Array.Empty<TileEffectBoxContact>(),
                },
                new object[]
                {
                    "Unit",
                    CreateWorldState(
                        new[] { CreateUnit(20, floorCell) },
                        new[] { CreateTileFeature(10, floorCell, TileFeatureKind.Destroy) }).CreateSnapshot(),
                    new[] { new TileEffectBoxContact(20, floorCell, TileEffectBoxContactKind.PushEnter) },
                },
                new object[]
                {
                    "Projectile",
                    CreateWorldState(
                        new[] { CreateProjectile(20, floorCell) },
                        new[] { CreateTileFeature(10, floorCell, TileFeatureKind.Destroy) }).CreateSnapshot(),
                    new[] { new TileEffectBoxContact(20, floorCell, TileEffectBoxContactKind.PushEnter) },
                },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var snapshot = (WorldSnapshot)cases[i][1];
                var contacts = (TileEffectBoxContact[])cases[i][2];

                var result = Resolve(
                    snapshot,
                    contacts,
                    CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

                Assert.That(result.IsEmpty, Is.True, name);
            }
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_MovingMoonBlock_DestroysLikeBox()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var moonBox = CreateBox(
                20,
                cell,
                boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype: BoxArchetype.Moon);
            var snapshot = CreateWorldState(
                    new[] { moonBox },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_MultipleMovingBoxes_ProducesDeterministicPresentationEvents()
        {
            var firstCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var secondCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var snapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(30, firstCell),
                        CreateBox(20, secondCell),
                    },
                    new[]
                    {
                        CreateTileFeature(300, firstCell, TileFeatureKind.Destroy),
                        CreateTileFeature(100, secondCell, TileFeatureKind.Destroy),
                    })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[]
                {
                    new TileEffectBoxContact(30, firstCell, TileEffectBoxContactKind.PushEnter),
                    new TileEffectBoxContact(20, secondCell, TileEffectBoxContactKind.PushEnter),
                    new TileEffectBoxContact(20, secondCell, TileEffectBoxContactKind.SlideEnter),
                },
                CreateDefinition(300, TileFeatureActivationRule.BottomFaceOnly),
                CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(4));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TargetEntityId).ToArray(), Is.EqualTo(new[] { 20, 30 }));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TileId).ToArray(), Is.EqualTo(new[] { 100, 300 }));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_ActiveFrontFace_RedirectsPushEnterAndSlideEnterBoxes()
        {
            var pushCell = new SurfaceCell(FaceId.Front, 1, 1);
            var slideCell = new SurfaceCell(FaceId.Front, 2, 1);
            var snapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(20, pushCell, state: EntityPhaseState.Sliding),
                        CreateBox(30, slideCell, state: EntityPhaseState.Sliding),
                    },
                    new[]
                    {
                        CreateTileFeature(100, pushCell, TileFeatureKind.Slide),
                        CreateTileFeature(200, slideCell, TileFeatureKind.Slide),
                    })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[]
                {
                    new TileEffectBoxContact(20, pushCell, TileEffectBoxContactKind.PushEnter),
                    new TileEffectBoxContact(30, slideCell, TileEffectBoxContactKind.SlideEnter),
                },
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None),
                CreateDefinition(200, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Left, selector: TileFeatureBoxSelector.None));

            Assert.That(result.Operations.IsEmpty, Is.True);
            Assert.That(result.TileEvents, Is.Empty);
            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetFacing));
            Assert.That(result.EntityOperations.Operations[0].EntityId, Is.EqualTo(20));
            Assert.That(result.EntityOperations.Operations[0].Facing, Is.EqualTo(Direction.Up));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.SetFacing));
            Assert.That(result.EntityOperations.Operations[1].EntityId, Is.EqualTo(30));
            Assert.That(result.EntityOperations.Operations[1].Facing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_InactiveOrNonFrontOrStationaryTargets_DoNotRedirect()
        {
            var floorCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var frontCell = new SurfaceCell(FaceId.Front, 1, 1);
            var cases = new[]
            {
                new object[]
                {
                    "InactiveFloorFace",
                    CreateWorldState(
                        new[] { CreateBox(20, floorCell, state: EntityPhaseState.Sliding) },
                        new[] { CreateTileFeature(100, floorCell, TileFeatureKind.Slide) }).CreateSnapshot(),
                    new[] { new TileEffectBoxContact(20, floorCell, TileEffectBoxContactKind.PushEnter) },
                    CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None),
                },
                new object[]
                {
                    "StationaryBox",
                    CreateWorldState(
                        new[] { CreateBox(20, frontCell, state: EntityPhaseState.Idle) },
                        new[] { CreateTileFeature(100, frontCell, TileFeatureKind.Slide) }).CreateSnapshot(),
                    new[] { new TileEffectBoxContact(20, frontCell, TileEffectBoxContactKind.PushEnter) },
                    CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None),
                },
                new object[]
                {
                    "MissingContact",
                    CreateWorldState(
                        new[] { CreateBox(20, frontCell, state: EntityPhaseState.Sliding) },
                        new[] { CreateTileFeature(100, frontCell, TileFeatureKind.Slide) }).CreateSnapshot(),
                    Array.Empty<TileEffectBoxContact>(),
                    CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None),
                },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var snapshot = (WorldSnapshot)cases[i][1];
                var contacts = (TileEffectBoxContact[])cases[i][2];
                var definition = (TileFeatureRuntimeDefinition)cases[i][3];

                var result = Resolve(snapshot, contacts, definition);

                Assert.That(result.IsEmpty, Is.True, name);
            }
        }

        [Test]
        [Category("Core")]
        public void SlideTile_InvalidTargetsAndUnsupportedContacts_DoNotRedirect()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var cases = new[]
            {
                new object[] { "Unit", new[] { CreateUnit(20, cell) }, TileEffectBoxContactKind.PushEnter },
                new object[] { "Projectile", new[] { CreateProjectile(20, cell) }, TileEffectBoxContactKind.PushEnter },
                new object[] { "NonBoxSolid", new[] { CreateSolid(20, cell) }, TileEffectBoxContactKind.PushEnter },
                new object[] { "DeadBox", new[] { CreateBox(20, cell, hp: 0, state: EntityPhaseState.Sliding) }, TileEffectBoxContactKind.PushEnter },
                new object[] { "DetachedBox", new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding, boardPresence: EntityBoardPresence.Detached) }, TileEffectBoxContactKind.PushEnter },
                new object[] { "MarkedBox", new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding, markedForDeath: true) }, TileEffectBoxContactKind.PushEnter },
                new object[] { "FlipLanding", new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) }, TileEffectBoxContactKind.FlipLanding },
                new object[] { "ImpactFollowThrough", new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) }, TileEffectBoxContactKind.ImpactFollowThrough },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var contactKind = (TileEffectBoxContactKind)cases[i][2];
                var snapshot = CreateWorldState(
                        entities,
                        new[] { CreateTileFeature(100, cell, TileFeatureKind.Slide) })
                    .CreateSnapshot();

                var result = Resolve(
                    snapshot,
                    new[] { new TileEffectBoxContact(20, cell, contactKind) },
                    CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None));

                Assert.That(result.IsEmpty, Is.True, name);
            }
        }

        [Test]
        [Category("Core")]
        public void SlideTile_DuplicateContacts_CreateSingleRedirect()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) },
                    new[] { CreateTileFeature(100, cell, TileFeatureKind.Slide) })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[]
                {
                    new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter),
                    new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.SlideEnter),
                },
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(1));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetFacing));
            Assert.That(result.EntityOperations.Operations[0].EntityId, Is.EqualTo(20));
            Assert.That(result.EntityOperations.Operations[0].Facing, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_SameCellDestroyTile_DestroysAndDoesNotRedirect()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) },
                    new[]
                    {
                        CreateTileFeature(100, cell, TileFeatureKind.Destroy),
                        CreateTileFeature(200, cell, TileFeatureKind.Slide),
                    })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                CreateDefinition(200, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations.Select(operation => operation.Kind).ToArray(),
                Is.EqualTo(new[] { FinalizationOperationKind.SetBoardPresence, FinalizationOperationKind.MarkDestroy }));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_RedirectKeepsTileStateUnchanged()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var slideTile = CreateTileFeature(100, cell, TileFeatureKind.Slide);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) },
                    new[] { slideTile })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None));

            Assert.That(result.Operations.IsEmpty, Is.True);
            Assert.That(snapshot.TryGetTileFeature(100, out var storedTile), Is.True);
            Assert.That(storedTile, Is.EqualTo(slideTile));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_Pipeline_RemovesMovedBoxBeforeFinalAttackRead()
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) });
            var attackLogic = new CapturingAttackLogic();
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                    attackLogic,
                });

            var result = pipeline.RunTick(new TickInput(7));
            var attackReadSnapshot = attackLogic.CapturedSnapshots.Last();

            Assert.That(attackReadSnapshot.TryGetEntity(20, out var attackReadBox), Is.True);
            Assert.That(attackReadBox.markedForDeath, Is.True);
            Assert.That(attackReadBox.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(attackReadSnapshot.TryGetSolidSemanticAt(destroyCell, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=20"));
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(result.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_Pipeline_RedirectsFacingBeforeFinalAttackReadAndNextSlideContinuation()
        {
            var slideCell = new SurfaceCell(FaceId.Front, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new[] { CreateTileFeature(100, slideCell, TileFeatureKind.Slide) });
            var attackLogic = new CapturingAttackLogic();
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(
                        new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push),
                        executeTick: 1),
                    attackLogic,
                });

            pipeline.RunTick(new TickInput(1));
            var firstFinalSnapshot = worldState.CreateSnapshot();
            var firstAttackReadSnapshot = attackLogic.CapturedSnapshots.Last();

            Assert.That(firstFinalSnapshot.TryGetEntity(20, out var firstFinalBox), Is.True);
            Assert.That(firstFinalBox.position, Is.EqualTo(slideCell));
            Assert.That(firstFinalBox.facing, Is.EqualTo(Direction.Up));
            Assert.That(firstFinalBox.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(firstAttackReadSnapshot.TryGetEntity(20, out var attackReadBox), Is.True);
            Assert.That(attackReadBox.facing, Is.EqualTo(Direction.Up));

            for (var tick = 2; tick <= 12; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
            }

            pipeline.RunTick(new TickInput(13));
            var secondFinalSnapshot = worldState.CreateSnapshot();

            Assert.That(secondFinalSnapshot.TryGetEntity(20, out var secondFinalBox), Is.True);
            Assert.That(secondFinalBox.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 2, 1)));
            Assert.That(secondFinalBox.facing, Is.EqualTo(Direction.Up));
            Assert.That(secondFinalBox.state, Is.EqualTo(EntityPhaseState.Sliding));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_Pipeline_DestroyPathAddsOnlyPostTileEffectSnapshot()
        {
            var withoutDestroyCounts = RunDestroyTileBudgetScenario(includeDestroyTile: false);
            var withDestroyCounts = RunDestroyTileBudgetScenario(includeDestroyTile: true);

            Assert.That(
                withDestroyCounts.ProjectedWorldMaterializedSnapshotCount,
                Is.EqualTo(withoutDestroyCounts.ProjectedWorldMaterializedSnapshotCount + 1));
            Assert.That(
                withDestroyCounts.ProjectedWorldCacheHitCount,
                Is.EqualTo(withoutDestroyCounts.ProjectedWorldCacheHitCount));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_Pipeline_RedirectPathAddsOnlyPostTileEffectSnapshot()
        {
            var withoutSlideCounts = RunSlideTileBudgetScenario(includeSlideTile: false);
            var withSlideCounts = RunSlideTileBudgetScenario(includeSlideTile: true);

            Assert.That(
                withSlideCounts.ProjectedWorldMaterializedSnapshotCount,
                Is.EqualTo(withoutSlideCounts.ProjectedWorldMaterializedSnapshotCount + 1));
            Assert.That(
                withSlideCounts.ProjectedWorldCacheHitCount,
                Is.EqualTo(withoutSlideCounts.ProjectedWorldCacheHitCount));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_DeterminismHash_ChangesAndReplaysStably()
        {
            var withoutDestroy = RunDestroyTileHashScenario(includeDestroyTile: false);
            var withDestroyFirst = RunDestroyTileHashScenario(includeDestroyTile: true);
            var withDestroySecond = RunDestroyTileHashScenario(includeDestroyTile: true);

            Assert.That(withDestroyFirst, Is.EqualTo(withDestroySecond));
            Assert.That(withDestroyFirst, Is.Not.EqualTo(withoutDestroy));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_DeterminismHash_ChangesAndReplaysStably()
        {
            var withoutSlide = RunSlideTileHashScenario(includeSlideTile: false);
            var withSlideFirst = RunSlideTileHashScenario(includeSlideTile: true);
            var withSlideSecond = RunSlideTileHashScenario(includeSlideTile: true);

            Assert.That(withSlideFirst, Is.EqualTo(withSlideSecond));
            Assert.That(withSlideFirst, Is.Not.EqualTo(withoutSlide));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_PresentationEvent_DoesNotEnterDeterminismHash()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var snapshot = CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[] { CreateTileFeature(100, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();
            var hashBuilder = new DeterminismHashBuilder();
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: new[]
                {
                    new TilePresentationEvent(
                        TilePresentationEventKind.DestroyTileTriggered,
                        100,
                        cell,
                        TileFeatureKind.Destroy,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0,
                        targetEntityId: 20),
                });

            Assert.That(
                hashBuilder.Build(3, snapshot, CreateTickResultData(snapshot, TickPresentationData.Empty)),
                Is.EqualTo(hashBuilder.Build(3, snapshot, CreateTickResultData(snapshot, presentationData))));
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
            return Resolve(snapshot, Array.Empty<TileEffectBoxContact>(), definitions);
        }

        private static TileEffectResolutionResult Resolve(
            WorldSnapshot snapshot,
            IReadOnlyList<TileEffectBoxContact> contacts,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return TileFeatureEffectResolver.Instance.Resolve(
                new TileEffectResolutionContext(7, snapshot, definitions, contacts));
        }

        private static FinalizationOperationMetadata CreateMovementMetadata(MovementSemanticKind movementSemanticKind)
        {
            var semanticKind = movementSemanticKind switch
            {
                MovementSemanticKind.Push => ResolvedActionSemanticKind.Push,
                MovementSemanticKind.Slide => ResolvedActionSemanticKind.Slide,
                MovementSemanticKind.Flip => ResolvedActionSemanticKind.Flip,
                _ => ResolvedActionSemanticKind.Move,
            };
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                semanticKind,
                sourceActorEntityId: 10,
                actionPlanId: 1,
                movementSemanticKind: movementSemanticKind);
        }

        private static string RunDestroyTileHashScenario(bool includeDestroyTile)
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var tileFeatures = includeDestroyTile
                ? new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) }
                : Array.Empty<TileFeatureState>();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                tileFeatures);
            var pipeline = CreatePipeline(
                worldState,
                includeDestroyTile
                    ? new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) }
                    : Array.Empty<TileFeatureRuntimeDefinition>(),
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            return pipeline.RunTick(new TickInput(7)).DeterminismHash;
        }

        private static string RunSlideTileHashScenario(bool includeSlideTile)
        {
            var slideCell = new SurfaceCell(FaceId.Front, 2, 0);
            var tileFeatures = includeSlideTile
                ? new[] { CreateTileFeature(100, slideCell, TileFeatureKind.Slide) }
                : Array.Empty<TileFeatureState>();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                tileFeatures);
            var pipeline = CreatePipeline(
                worldState,
                includeSlideTile
                    ? new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None) }
                    : Array.Empty<TileFeatureRuntimeDefinition>(),
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            return pipeline.RunTick(new TickInput(7)).DeterminismHash;
        }

        private static SnapshotMaterializationCounts RunDestroyTileBudgetScenario(bool includeDestroyTile)
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var tileFeatures = includeDestroyTile
                ? new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) }
                : Array.Empty<TileFeatureState>();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                tileFeatures);
            var pipeline = CreatePipeline(
                worldState,
                includeDestroyTile
                    ? new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) }
                    : Array.Empty<TileFeatureRuntimeDefinition>(),
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            using var capture = SnapshotMaterializationDiagnostics.BeginCapture();
            pipeline.RunTick(new TickInput(7));
            return capture.Counts;
        }

        private static SnapshotMaterializationCounts RunSlideTileBudgetScenario(bool includeSlideTile)
        {
            var slideCell = new SurfaceCell(FaceId.Front, 2, 0);
            var tileFeatures = includeSlideTile
                ? new[] { CreateTileFeature(100, slideCell, TileFeatureKind.Slide) }
                : Array.Empty<TileFeatureState>();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                tileFeatures);
            var pipeline = CreatePipeline(
                worldState,
                includeSlideTile
                    ? new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None) }
                    : Array.Empty<TileFeatureRuntimeDefinition>(),
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            using var capture = SnapshotMaterializationDiagnostics.BeginCapture();
            pipeline.RunTick(new TickInput(7));
            return capture.Counts;
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

        private static TickResultData CreateTickResultData(
            WorldSnapshot snapshot,
            TickPresentationData presentationData = null)
        {
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);
            return new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                presentationData ?? TickPresentationData.Empty);
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
            TileFeatureFlags flags = TileFeatureFlags.None,
            int sourceEntityId = 0,
            int ownerEntityId = 0,
            int teamId = 0)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                flags,
                sourceEntityId,
                ownerEntityId,
                teamId,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(
            int tileId,
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.Always,
            Direction2D direction = Direction2D.None,
            TileFeatureBoxSelector selector = TileFeatureBoxSelector.AnyPushableBox)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                direction,
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

        private static EntityState CreateSolid(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
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
            BoxArchetype boxArchetype = BoxArchetype.Normal,
            EntityPhaseState state = EntityPhaseState.Idle,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = Math.Max(1, hp),
                teamId = 0,
                type = EntityType.Box,
                state = state,
                facing = facing,
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

        private sealed class ScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent _movementIntent;
            private readonly int? _executeTick;

            public ScriptedMovementLogic(RawMovementIntent movementIntent, int? executeTick = null)
            {
                _movementIntent = movementIntent;
                _executeTick = executeTick;
            }

            public int ControlledEntityId => _movementIntent.SourceId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_executeTick.HasValue &&
                    input.TickIndex != _executeTick.Value)
                {
                    return;
                }

                buffer.Add(_movementIntent);
            }
        }
    }
}
