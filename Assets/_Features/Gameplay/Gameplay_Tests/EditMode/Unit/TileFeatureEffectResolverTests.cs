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
using Game.Feature.Gameplay.Movement.Expansion;
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
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Push) },
                CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_InitialIdleBoxOnSameCell_DoesNotLatch()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var result = Resolve(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                CreateDefinition(10));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_PushSlideStopOnSameCell_LatchesWithSingleUpdate()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Push) },
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
        public void ButtonLatch_ContactsWithoutStopFact_DoNotLatch()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var result = Resolve(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { new TileEffectBoxContact(20, button.Cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(10));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_SlidingBoxOnSameCellWithContact_DoesNotLatch()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell, state: EntityPhaseState.Sliding);
            var result = Resolve(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { new TileEffectBoxContact(20, button.Cell, TileEffectBoxContactKind.SlideEnter) },
                CreateDefinition(10));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_StopsWithInvalidFinalBox_DoNotLatch()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var cases = new[]
            {
                new object[] { "DeadBox", new[] { CreateBox(20, cell, hp: 0) }, CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
                new object[] { "MarkedBox", new[] { CreateBox(20, cell, markedForDeath: true) }, CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
                new object[] { "DetachedBox", new[] { CreateBox(20, cell, boardPresence: EntityBoardPresence.Detached) }, CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
                new object[] { "WrongOccupant", new[] { CreateBox(30, cell) }, CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
                new object[] { "SlidingBox", new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) }, CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var stop = (TileEffectBoxStop)cases[i][2];
                var result = ResolveWithStops(
                    CreateWorldState(entities, new[] { CreateButton(10, cell) }).CreateSnapshot(),
                    new[] { stop },
                    CreateDefinition(10));

                Assert.That(result.IsEmpty, Is.True, name);
            }
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_UnsupportedMovementFamily_DoesNotLatch()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.None) },
                CreateDefinition(10));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_SameCellDestroyTileKilledStopBox_DoesNotLatch()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[]
                    {
                        CreateButton(10, cell),
                        CreateTileFeature(100, cell, TileFeatureKind.Destroy),
                    })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                null,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                new[] { CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
                CreateDefinition(10),
                CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.Operations.IsEmpty, Is.True);
            Assert.That(result.EntityOperations.Operations.Select(operation => operation.Kind).ToArray(),
                Is.EqualTo(new[] { FinalizationOperationKind.SetBoardPresence, FinalizationOperationKind.MarkDestroy }));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_InactiveStopIsNotConsumedRetroactively()
        {
            var inactiveCell = new SurfaceCell(FaceId.Back, 1, 1);
            var activeCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var inactiveSnapshot = CreateWorldState(
                    new[] { CreateBox(20, inactiveCell) },
                    new[] { CreateButton(10, inactiveCell) })
                .CreateSnapshot();
            var activeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, activeCell) },
                    new[] { CreateButton(10, activeCell) })
                .CreateSnapshot();

            var inactiveResult = ResolveWithStops(
                inactiveSnapshot,
                new[] { CreateStop(20, inactiveCell, TileEffectBoxMovementFamily.Push) },
                CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly));
            var laterActiveResult = Resolve(
                activeSnapshot,
                CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly));

            Assert.That(inactiveResult.IsEmpty, Is.True);
            Assert.That(laterActiveResult.IsEmpty, Is.True);
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
                var result = ResolveWithStops(
                    CreateWorldState(entities, new[] { button }, terrain).CreateSnapshot(),
                    new[] { CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
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
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Push) },
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
            var stops = new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Push) };

            Assert.That(Resolve(snapshot).IsEmpty, Is.True);
            Assert.That(ResolveWithStops(snapshot, stops, CreateDefinition(10, selector: TileFeatureBoxSelector.None)).IsEmpty, Is.True);
            Assert.That(ResolveWithStops(snapshot, stops, CreateDefinition(10, selector: TileFeatureBoxSelector.FeatureCell)).IsEmpty, Is.True);
            Assert.That(ResolveWithStops(snapshot, stops, CreateDefinition(10, selector: TileFeatureBoxSelector.BoundEntity)).IsEmpty, Is.True);
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
            var result = ResolveWithStops(
                CreateWorldState(new[] { moonBox }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Slide) },
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
                var result = ResolveWithStops(
                    CreateWorldState(entities, new[] { button }, terrain).CreateSnapshot(),
                    new[] { CreateStop(20, cell, TileEffectBoxMovementFamily.Slide) },
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
            var result = ResolveWithStops(
                CreateWorldState(new[] { moonBox }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Push) },
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
                new[]
                {
                    CreateStop(130, first.Cell, TileEffectBoxMovementFamily.Push),
                    CreateStop(110, second.Cell, TileEffectBoxMovementFamily.Slide),
                },
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
        public void StopFacts_PushSlideMoveEndingIdle_CreateDeterministicStops()
        {
            var firstSourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var secondSourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var pushCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var slideCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(20, firstSourceCell),
                        CreateBox(10, secondSourceCell),
                    },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(20, pushCell),
                        CreateBox(10, slideCell),
                    },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(20, pushCell, CreateMovementMetadata(MovementSemanticKind.Push));
            batch.MoveEntity(10, slideCell, CreateMovementMetadata(MovementSemanticKind.Slide));

            var stops = TickPipeline.BuildTileEffectBoxStops(beforeSnapshot, finalSnapshot, batch);

            CollectionAssert.AreEqual(new[] { 10, 20 }, stops.Select(stop => stop.BoxEntityId).ToArray());
            CollectionAssert.AreEqual(
                new[] { TileEffectBoxMovementFamily.Slide, TileEffectBoxMovementFamily.Push },
                stops.Select(stop => stop.MovementFamily).ToArray());
        }

        [Test]
        [Category("Core")]
        public void StopFacts_MoveEndingSliding_CreatesNoStop()
        {
            var beforeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var buttonCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, beforeCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, buttonCell, state: EntityPhaseState.Sliding) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(20, buttonCell, CreateMovementMetadata(MovementSemanticKind.Push));

            var stops = TickPipeline.BuildTileEffectBoxStops(beforeSnapshot, finalSnapshot, batch);

            Assert.That(stops, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void StopFacts_SlidingToIdleStopWithoutMove_CreatesSlideStop()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, state: EntityPhaseState.Idle) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.ApplyStateChange(20, EntityPhaseState.Idle, 0, CreateMovementMetadata(MovementSemanticKind.Stop));

            var stops = TickPipeline.BuildTileEffectBoxStops(beforeSnapshot, finalSnapshot, batch);

            Assert.That(stops, Has.Count.EqualTo(1));
            Assert.That(stops[0].BoxEntityId, Is.EqualTo(20));
            Assert.That(stops[0].Cell, Is.EqualTo(cell));
            Assert.That(stops[0].MovementFamily, Is.EqualTo(TileEffectBoxMovementFamily.Slide));
        }

        [Test]
        [Category("Core")]
        public void StopFacts_ImpactFollowThroughMove_CreatesNoStop()
        {
            var beforeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var buttonCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, beforeCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, buttonCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(20, buttonCell, CreateMovementMetadata(MovementSemanticKind.Push, localActionIndex: 1));

            var stops = TickPipeline.BuildTileEffectBoxStops(beforeSnapshot, finalSnapshot, batch);

            Assert.That(stops, Is.Empty);
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
            Assert.That(result.EntityOperations.Operations[0].Metadata.ExitPresentationTiming, Is.EqualTo(EntityExitPresentationTiming.AfterEntityMotion));
            Assert.That(result.EntityOperations.Operations[1].Metadata.ExitPresentationTiming, Is.EqualTo(EntityExitPresentationTiming.AfterEntityMotion));
            Assert.That(result.EntityOperations.Operations[0].Metadata.HasPresentationTargetCell, Is.True);
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
        public void DestroyTile_ActiveFrontFace_DestroysMovingBoxOnceAndKeepsTile()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var destroyTile = CreateTileFeature(10, cell, TileFeatureKind.Destroy);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { destroyTile })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly));

            Assert.That(result.Operations.IsEmpty, Is.True);
            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].EntityId, Is.EqualTo(20));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(result.TileEvents[0].TileId, Is.EqualTo(10));
            Assert.That(result.TileEvents[0].Cell, Is.EqualTo(cell));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(snapshot.TryGetTileFeature(10, out var storedTile), Is.True);
            Assert.That(storedTile.Kind, Is.EqualTo(TileFeatureKind.Destroy));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_FrontOnlyOnBottomFace_DoesNotDestroy()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
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
            Assert.That(result.TileEvents, Has.Count.EqualTo(2));
            Assert.That(
                result.TileEvents.Select(tileEvent => tileEvent.EventKind).ToArray(),
                Is.EqualTo(new[]
                {
                    TilePresentationEventKind.SlideTileRedirected,
                    TilePresentationEventKind.SlideTileRedirected,
                }));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TileId).ToArray(), Is.EqualTo(new[] { 100, 200 }));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TargetEntityId).ToArray(), Is.EqualTo(new[] { 20, 30 }));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.Direction).ToArray(), Is.EqualTo(new[] { Direction.Up, Direction.Left }));
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
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.SlideTileRedirected));
            Assert.That(tileEvent.TileId, Is.EqualTo(100));
            Assert.That(tileEvent.Cell, Is.EqualTo(cell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Slide));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.Direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_AlreadyFacingRedirectDirection_CreatesNoOperationOrEvent()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding, facing: Direction.Up) },
                    new[] { CreateTileFeature(100, cell, TileFeatureKind.Slide) })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void SlideTile_MultipleRedirects_ProduceDeterministicEventOrder()
        {
            var firstCell = new SurfaceCell(FaceId.Front, 0, 1);
            var secondCell = new SurfaceCell(FaceId.Front, 1, 1);
            var snapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(20, secondCell, state: EntityPhaseState.Sliding),
                        CreateBox(30, firstCell, state: EntityPhaseState.Sliding),
                    },
                    new[]
                    {
                        CreateTileFeature(200, secondCell, TileFeatureKind.Slide),
                        CreateTileFeature(100, firstCell, TileFeatureKind.Slide),
                    })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[]
                {
                    new TileEffectBoxContact(20, secondCell, TileEffectBoxContactKind.PushEnter),
                    new TileEffectBoxContact(30, firstCell, TileEffectBoxContactKind.PushEnter),
                },
                CreateDefinition(200, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Left, selector: TileFeatureBoxSelector.None),
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None));

            Assert.That(result.TileEvents, Has.Count.EqualTo(2));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TileId).ToArray(), Is.EqualTo(new[] { 100, 200 }));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TargetEntityId).ToArray(), Is.EqualTo(new[] { 30, 20 }));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.Direction).ToArray(), Is.EqualTo(new[] { Direction.Up, Direction.Left }));
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

            var firstResult = pipeline.RunTick(new TickInput(1));
            var firstFinalSnapshot = worldState.CreateSnapshot();
            var firstAttackReadSnapshot = attackLogic.CapturedSnapshots.Last();

            Assert.That(firstResult.PresentationData.TileEvents, Has.Count.EqualTo(1));
            var slideEvent = firstResult.PresentationData.TileEvents[0];
            Assert.That(slideEvent.EventKind, Is.EqualTo(TilePresentationEventKind.SlideTileRedirected));
            Assert.That(slideEvent.TileId, Is.EqualTo(100));
            Assert.That(slideEvent.Cell, Is.EqualTo(slideCell));
            Assert.That(slideEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Slide));
            Assert.That(slideEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(slideEvent.Direction, Is.EqualTo(Direction.Up));
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
        public void BarricadeQuery_ActiveFrontFaceOnlyReturnsBlockerWithoutCreatingSnapshot()
        {
            var activeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var inactiveCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[]
                    {
                        CreateTileFeature(100, activeCell, TileFeatureKind.Barricade),
                        CreateTileFeature(101, inactiveCell, TileFeatureKind.Barricade),
                        CreateTileFeature(102, activeCell, TileFeatureKind.Destroy),
                    })
                .CreateSnapshot();
            var definitions = new[]
            {
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                CreateDefinition(101, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                CreateDefinition(102, TileFeatureActivationRule.FrontFaceOnly),
            };

            SnapshotMaterializationCounts counts;
            bool activeResult;
            bool inactiveResult;
            bool missingDefinitionResult;
            bool nonBarricadeResult;
            bool tryGetActiveResult;
            TileFeatureState activeBarricade;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                activeResult = TileFeatureBoxBlockerQuery.HasActiveBarricadeBlocker(snapshot, definitions, activeCell);
                tryGetActiveResult = TileFeatureBoxBlockerQuery.TryGetActiveBarricadeBlocker(
                    snapshot,
                    definitions,
                    activeCell,
                    out activeBarricade);
                inactiveResult = TileFeatureBoxBlockerQuery.HasActiveBarricadeBlocker(snapshot, definitions, inactiveCell);
                missingDefinitionResult = TileFeatureBoxBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    new[] { definitions[1], definitions[2] },
                    activeCell);
                nonBarricadeResult = TileFeatureBoxBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    new[] { definitions[2] },
                    activeCell);
                counts = capture.Counts;
            }

            Assert.That(activeResult, Is.True);
            Assert.That(tryGetActiveResult, Is.True);
            Assert.That(activeBarricade.TileId, Is.EqualTo(100));
            Assert.That(inactiveResult, Is.False);
            Assert.That(missingDefinitionResult, Is.False);
            Assert.That(nonBarricadeResult, Is.False);
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void Barricade_PushFirstStepIntoActiveBlocker_RejectsWithBlockedTileEvent()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var snapshotAfter = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=BoxSlideBlockedByBarricade").And.Contains("MovementKind=PushStart"));
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.PresentationData.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeBlocked));
            Assert.That(tileEvent.TileId, Is.EqualTo(100));
            Assert.That(tileEvent.Cell, Is.EqualTo(barricadeCell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.Direction, Is.EqualTo(Direction.Right));
            Assert.That(result.PresentationData.FrontFaceShieldBlocks, Is.Empty);
            Assert.That(snapshotAfter.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
        }

        [Test]
        [Category("Core")]
        public void Barricade_PushDestroyFirstStepIntoActiveBlocker_UsesExistingDestroyFallback()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0), boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeBlocked));
            Assert.That(result.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.TileEvents[0].Direction, Is.EqualTo(Direction.Right));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=20"));
        }

        [Test]
        [Category("Core")]
        public void Barricade_SlidingContinuationIntoActiveBlocker_StopsWithBlockedTileEvent()
        {
            var slidingBox = CreateBox(
                20,
                new SurfaceCell(FaceId.Front, 0, 0),
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var barricadeCell = new SurfaceCell(FaceId.Front, 0, 1);
            var worldState = CreateWorldState(
                new[] { slidingBox },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var snapshotAfter = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=BoxSlideBlockedByBarricade").And.Contains("MovementKind=SlidingContinuation"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("ImpactReservationCreated"));
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeBlocked));
            Assert.That(result.PresentationData.TileEvents[0].TileId, Is.EqualTo(100));
            Assert.That(result.PresentationData.TileEvents[0].Cell, Is.EqualTo(barricadeCell));
            Assert.That(result.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.TileEvents[0].Direction, Is.EqualTo(Direction.Up));
            Assert.That(snapshotAfter.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
        }

        [Test]
        [Category("Core")]
        public void Barricade_FlipLandingIntoActiveBlocker_RejectsWithBlockedTileEventBeforeImpact()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 1, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 0, 1), boxCapabilities: BoxCapabilities.Flip),
                    CreateEnemyUnit(30, barricadeCell),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 1), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var snapshotAfter = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=FlipLandingBlockedByBarricade"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("ImpactReservationCreated"));
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.PresentationData.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeBlocked));
            Assert.That(tileEvent.TileId, Is.EqualTo(100));
            Assert.That(tileEvent.Cell, Is.EqualTo(barricadeCell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.Direction, Is.EqualTo(Direction.Right));
            Assert.That(
                result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed),
                Is.False);
            Assert.That(snapshotAfter.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
            Assert.That(snapshotAfter.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.position, Is.EqualTo(barricadeCell));
            Assert.That(enemyAfter.hp, Is.EqualTo(3));
            Assert.That(enemyAfter.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(enemyAfter.markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Barricade_InactiveDoesNotBlockFlipLanding()
        {
            var barricadeCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 0, 1), boxCapabilities: BoxCapabilities.Flip),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 1), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var snapshotAfter = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(snapshotAfter.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(barricadeCell));
        }

        [Test]
        [Category("Core")]
        public void Barricade_InactiveDoesNotBlockPushOrSlide()
        {
            var inactiveCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var pushWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                new[] { CreateTileFeature(100, inactiveCell, TileFeatureKind.Barricade) });
            var pushPipeline = CreatePipeline(
                pushWorldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            var pushResult = pushPipeline.RunTick(new TickInput(7));

            Assert.That(pushResult.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(pushResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(pushWorldState.CreateSnapshot().TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.position, Is.EqualTo(inactiveCell));
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Sliding));

            var slidingBox = CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 0), state: EntityPhaseState.Sliding, facing: Direction.Right);
            slidingBox.stateTimer = 0;
            var slideWorldState = CreateWorldState(
                new[] { slidingBox },
                new[] { CreateTileFeature(101, inactiveCell, TileFeatureKind.Barricade) });
            var slidePipeline = CreatePipeline(
                slideWorldState,
                new[] { CreateDefinition(101, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var slideResult = slidePipeline.RunTick(new TickInput(7));

            Assert.That(slideResult.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(slideResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(slideWorldState.CreateSnapshot().TryGetEntity(30, out var slidBox), Is.True);
            Assert.That(slidBox.position, Is.EqualTo(inactiveCell));
            Assert.That(slidBox.state, Is.EqualTo(EntityPhaseState.Sliding));
        }

        [Test]
        [Category("Core")]
        public void Barricade_DoesNotAffectUnitEnemyOrProjectileMovement()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 1, 0);
            var playerWorldState = CreateWorldState(
                new[] { CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)) },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var playerResult = CreatePipeline(
                    playerWorldState,
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                    new IEntityLogic[]
                    {
                        new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Move)),
                    })
                .RunTick(new TickInput(7));

            var enemy = CreateUnit(30, new SurfaceCell(FaceId.Front, 0, 0));
            enemy.teamId = 2;
            var enemyWorldState = CreateWorldState(
                new[] { enemy },
                new[] { CreateTileFeature(101, barricadeCell, TileFeatureKind.Barricade) });
            var enemyResult = CreatePipeline(
                    enemyWorldState,
                    new[] { CreateDefinition(101, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                    new IEntityLogic[]
                    {
                        new ScriptedMovementLogic(new RawMovementIntent(30, 100, new Vector2Int(1, 0), MovementCommandKind.Move)),
                    })
                .RunTick(new TickInput(7));

            var projectileWorldState = CreateWorldState(
                new[] { CreateProjectile(40, new SurfaceCell(FaceId.Front, 0, 0)) },
                new[] { CreateTileFeature(102, barricadeCell, TileFeatureKind.Barricade) });
            var projectileResult = CreatePipeline(
                    projectileWorldState,
                    new[] { CreateDefinition(102, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                    new IEntityLogic[]
                    {
                        new ScriptedMovementLogic(new RawMovementIntent(40, 100, new Vector2Int(1, 0), MovementCommandKind.Move)),
                    })
                .RunTick(new TickInput(7));

            Assert.That(playerResult.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(enemyResult.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(projectileResult.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(playerResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(enemyResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(projectileResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(playerWorldState.CreateSnapshot().TryGetEntity(10, out var playerAfter), Is.True);
            Assert.That(enemyWorldState.CreateSnapshot().TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(projectileWorldState.CreateSnapshot().TryGetEntity(40, out var projectileAfter), Is.True);
            Assert.That(playerAfter.position, Is.EqualTo(barricadeCell));
            Assert.That(enemyAfter.position, Is.EqualTo(barricadeCell));
            Assert.That(projectileAfter.position, Is.EqualTo(barricadeCell));
        }

        [Test]
        [Category("Core")]
        public void Barricade_StandingBoxIsNotRetroactivelyDestroyed()
        {
            var inactiveCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var inactiveWorldState = CreateWorldState(
                new[] { CreateBox(20, inactiveCell) },
                new[] { CreateTileFeature(100, inactiveCell, TileFeatureKind.Barricade) });
            var inactiveResult = CreatePipeline(
                    inactiveWorldState,
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                    Array.Empty<IEntityLogic>())
                .RunTick(new TickInput(7));

            var activeCell = new SurfaceCell(FaceId.Front, 1, 0);
            var activeWorldState = CreateWorldState(
                new[] { CreateBox(30, activeCell) },
                new[] { CreateTileFeature(101, activeCell, TileFeatureKind.Barricade) });
            var activeResult = CreatePipeline(
                    activeWorldState,
                    new[] { CreateDefinition(101, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                    Array.Empty<IEntityLogic>())
                .RunTick(new TickInput(7));

            Assert.That(inactiveResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(activeResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(inactiveWorldState.CreateSnapshot().TryGetEntity(20, out var inactiveBox), Is.True);
            Assert.That(activeWorldState.CreateSnapshot().TryGetEntity(30, out var activeBox), Is.True);
            Assert.That(inactiveBox.markedForDeath, Is.False);
            Assert.That(activeBox.markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_InactiveToActiveTransition_DestroysBoxWithCrushedTileEvent()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var previousSnapshot = CreateWorldState(
                    new[] { CreateBox(20, barricadeCell) },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { CreateBox(20, barricadeCell) },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.Operations.IsEmpty, Is.True);
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeCrushed));
            Assert.That(result.TileEvents[0].TileId, Is.EqualTo(100));
            Assert.That(result.TileEvents[0].Cell, Is.EqualTo(barricadeCell));
            Assert.That(result.TileEvents[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(result.TileEvents[0].Direction, Is.EqualTo(Direction.None));
            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(result.EntityOperations.Operations[0].EntityId, Is.EqualTo(20));
            Assert.That(result.EntityOperations.Operations[0].BoardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(result.EntityOperations.Operations[0].Metadata.BoundaryReason, Is.EqualTo("BarricadeCrush"));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].Metadata.ExitCauseHint, Is.EqualTo(TickEntityExitCause.BoxDestroy));
            Assert.That(result.EntityOperations.Operations[1].Metadata.ExitPresentationTiming, Is.EqualTo(EntityExitPresentationTiming.Immediate));
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_NonActivationTransitions_DoNotScanAndDoNotCrush()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var definition = CreateDefinition(
                100,
                TileFeatureActivationRule.FrontFaceOnly,
                selector: TileFeatureBoxSelector.None);
            var activeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, barricadeCell) },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var inactiveSnapshot = CreateWorldState(
                    new[] { CreateBox(20, barricadeCell) },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();

            Assert.That(Resolve(activeSnapshot, activeSnapshot, definition).IsEmpty, Is.True, "active to active");
            Assert.That(Resolve(inactiveSnapshot, inactiveSnapshot, definition).IsEmpty, Is.True, "inactive to inactive");
            Assert.That(Resolve(inactiveSnapshot, activeSnapshot, definition).IsEmpty, Is.True, "active to inactive");
            Assert.That(Resolve(activeSnapshot, definition).IsEmpty, Is.True, "missing previous snapshot");
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_IgnoresNonBoxInvalidAndAlreadyDestroyedOccupants()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var definition = CreateDefinition(
                100,
                TileFeatureActivationRule.FrontFaceOnly,
                selector: TileFeatureBoxSelector.None);
            var cases = new[]
            {
                new object[] { "Unit", new[] { CreateUnit(20, barricadeCell) }, GameplayTerrainData.Empty },
                new object[] { "EnemyUnit", new[] { CreateEnemyUnit(21, barricadeCell) }, GameplayTerrainData.Empty },
                new object[] { "Projectile", new[] { CreateProjectile(22, barricadeCell) }, GameplayTerrainData.Empty },
                new object[] { "WallLikeTerrain", Array.Empty<EntityState>(), new GameplayTerrainData(new[] { CreateWallLikeTerrain(barricadeCell) }) },
                new object[] { "NonBoxSolid", new[] { CreateSolid(23, barricadeCell) }, GameplayTerrainData.Empty },
                new object[] { "DeadBox", new[] { CreateBox(24, barricadeCell, hp: 0) }, GameplayTerrainData.Empty },
                new object[] { "DetachedBox", new[] { CreateBox(25, barricadeCell, boardPresence: EntityBoardPresence.Detached) }, GameplayTerrainData.Empty },
                new object[] { "MarkedBox", new[] { CreateBox(26, barricadeCell, markedForDeath: true) }, GameplayTerrainData.Empty },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var terrain = (GameplayTerrainData)cases[i][2];
                var previousSnapshot = CreateWorldState(
                        entities,
                        new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                        terrain,
                        new CubeTopologyState(FaceId.Floor))
                    .CreateSnapshot();
                var currentSnapshot = CreateWorldState(
                        entities,
                        new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                        terrain,
                        new CubeTopologyState(FaceId.Front))
                    .CreateSnapshot();

                Assert.That(Resolve(currentSnapshot, previousSnapshot, definition).IsEmpty, Is.True, name);
            }
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_MoonBlockIsCrushed()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var moonBox = CreateBox(
                20,
                barricadeCell,
                boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype: BoxArchetype.Moon);
            var previousSnapshot = CreateWorldState(
                    new[] { moonBox },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { moonBox },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_DestroyTileSameTick_DedupesAndDestroyTileWins()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var previousSnapshot = CreateWorldState(
                    new[] { CreateBox(20, barricadeCell) },
                    new[]
                    {
                        CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade),
                        CreateTileFeature(200, barricadeCell, TileFeatureKind.Destroy),
                    },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { CreateBox(20, barricadeCell) },
                    new[]
                    {
                        CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade),
                        CreateTileFeature(200, barricadeCell, TileFeatureKind.Destroy),
                    },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var contacts = new[]
            {
                new TileEffectBoxContact(20, barricadeCell, TileEffectBoxContactKind.PushEnter),
            };

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                contacts,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                CreateDefinition(200, TileFeatureActivationRule.FrontFaceOnly));

            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Metadata.BoundaryReason, Is.EqualTo("DestroyTile"));
            Assert.That(result.EntityOperations.Operations[1].Metadata.BoundaryReason, Is.EqualTo("DestroyTile"));
            Assert.That(result.EntityOperations.Operations[0].Metadata.ExitPresentationTiming, Is.EqualTo(EntityExitPresentationTiming.AfterEntityMotion));
            Assert.That(result.EntityOperations.Operations[1].Metadata.ExitPresentationTiming, Is.EqualTo(EntityExitPresentationTiming.AfterEntityMotion));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_Pipeline_DetachesBeforeFinalAttackReadAndAuthoritativeCleanup()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var attackLogic = new CapturingAttackLogic();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 4)),
                    CreateBox(20, barricadeCell),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    attackLogic,
                });

            var result = pipeline.RunTick(new TickInput(7, PlayerTickCommand.Move(Direction.Up)));

            Assert.That(attackLogic.CapturedSnapshots, Has.Count.EqualTo(2));
            Assert.That(attackLogic.CapturedSnapshots[1].TryGetEntity(20, out var attackReadBox), Is.True);
            Assert.That(attackReadBox.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(attackReadBox.markedForDeath, Is.True);
            var crushedEvent = result.PresentationData.TileEvents.Single(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.BarricadeCrushed);
            Assert.That(crushedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=20"));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_DeterminismHash_ChangesAndReplaysStably()
        {
            var withoutCrush = RunBarricadeCrushHashScenario(crushEnabled: false);
            var withCrushFirst = RunBarricadeCrushHashScenario(crushEnabled: true);
            var withCrushSecond = RunBarricadeCrushHashScenario(crushEnabled: true);

            Assert.That(withCrushFirst, Is.EqualTo(withCrushSecond));
            Assert.That(withCrushFirst, Is.Not.EqualTo(withoutCrush));
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_NoTransitionPath_PreservesPinnedSnapshotBudget()
        {
            var counts = RunBarricadeNoTransitionBudgetScenario();

            AssertPinnedEmptyBudget(counts);
        }

        [Test]
        [Category("Core")]
        public void BarricadeCrush_CrushPathAddsOnlyPostTileEffectSnapshot()
        {
            var withoutCrushCounts = RunBarricadeCrushBudgetScenario(crushEnabled: false);
            var withCrushCounts = RunBarricadeCrushBudgetScenario(crushEnabled: true);

            Assert.That(
                withCrushCounts.ProjectedWorldMaterializedSnapshotCount,
                Is.EqualTo(withoutCrushCounts.ProjectedWorldMaterializedSnapshotCount + 1));
            Assert.That(
                withCrushCounts.ProjectedWorldCacheHitCount,
                Is.EqualTo(withoutCrushCounts.ProjectedWorldCacheHitCount));
        }

        [Test]
        [Category("Core")]
        public void Barricade_ActiveBlocksBeforeDestroyTileContact_InactiveAllowsDestroyTile()
        {
            var activeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var activeWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new[]
                {
                    CreateTileFeature(100, activeCell, TileFeatureKind.Barricade),
                    CreateTileFeature(200, activeCell, TileFeatureKind.Destroy),
                });
            var activeResult = CreatePipeline(
                    activeWorldState,
                    new[]
                    {
                        CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                        CreateDefinition(200, TileFeatureActivationRule.FrontFaceOnly),
                    },
                    new IEntityLogic[]
                    {
                        new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                    })
                .RunTick(new TickInput(7));

            var inactiveCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var inactiveWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(11, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(21, new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                new[]
                {
                    CreateTileFeature(101, inactiveCell, TileFeatureKind.Barricade),
                    CreateTileFeature(201, inactiveCell, TileFeatureKind.Destroy),
                });
            var inactiveResult = CreatePipeline(
                    inactiveWorldState,
                    new[]
                    {
                        CreateDefinition(101, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                        CreateDefinition(201, TileFeatureActivationRule.BottomFaceOnly),
                    },
                    new IEntityLogic[]
                    {
                        new ScriptedMovementLogic(new RawMovementIntent(11, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                    })
                .RunTick(new TickInput(7));

            Assert.That(activeResult.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=BoxSlideBlockedByBarricade"));
            Assert.That(activeResult.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(activeResult.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeBlocked));
            Assert.That(activeWorldState.CreateSnapshot().TryGetEntity(20, out var activeBox), Is.True);
            Assert.That(activeBox.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));

            Assert.That(inactiveResult.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(inactiveResult.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(inactiveWorldState.CreateSnapshot().TryGetEntity(21, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Barricade_ActiveBlocksBeforeSlideTileRedirect_InactiveAllowsSlideTile()
        {
            var activeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var activeWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new[]
                {
                    CreateTileFeature(100, activeCell, TileFeatureKind.Barricade),
                    CreateTileFeature(200, activeCell, TileFeatureKind.Slide),
                });
            var activeResult = CreatePipeline(
                    activeWorldState,
                    new[]
                    {
                        CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                        CreateDefinition(200, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None),
                    },
                    new IEntityLogic[]
                    {
                        new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                    })
                .RunTick(new TickInput(7));

            var inactiveWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(11, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(21, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new[]
                {
                    CreateTileFeature(101, activeCell, TileFeatureKind.Barricade),
                    CreateTileFeature(201, activeCell, TileFeatureKind.Slide),
                });
            var inactiveResult = CreatePipeline(
                    inactiveWorldState,
                    new[]
                    {
                        CreateDefinition(101, TileFeatureActivationRule.BottomFaceOnly, selector: TileFeatureBoxSelector.None),
                        CreateDefinition(201, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None),
                    },
                    new IEntityLogic[]
                    {
                        new ScriptedMovementLogic(new RawMovementIntent(11, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                    })
                .RunTick(new TickInput(7));

            Assert.That(activeResult.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=BoxSlideBlockedByBarricade"));
            Assert.That(activeResult.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(activeResult.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeBlocked));
            Assert.That(activeWorldState.CreateSnapshot().TryGetEntity(20, out var activeBox), Is.True);
            Assert.That(activeBox.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(activeBox.facing, Is.EqualTo(Direction.Right));

            Assert.That(inactiveResult.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(inactiveResult.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.SlideTileRedirected));
            Assert.That(inactiveWorldState.CreateSnapshot().TryGetEntity(21, out var redirectedBox), Is.True);
            Assert.That(redirectedBox.position, Is.EqualTo(activeCell));
            Assert.That(redirectedBox.facing, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Core")]
        public void Barricade_DeterminismHash_ChangesAndReplaysStably()
        {
            var withoutBarricade = RunBarricadeHashScenario(includeBarricade: false);
            var withBarricadeFirst = RunBarricadeHashScenario(includeBarricade: true);
            var withBarricadeSecond = RunBarricadeHashScenario(includeBarricade: true);

            Assert.That(withBarricadeFirst, Is.EqualTo(withBarricadeSecond));
            Assert.That(withBarricadeFirst, Is.Not.EqualTo(withoutBarricade));
        }

        [Test]
        [Category("Core")]
        public void Barricade_BlockedPath_PreservesPinnedSnapshotBudget()
        {
            var counts = RunBarricadeBlockedBudgetScenario();

            AssertPinnedEmptyBudget(counts);
        }

        [Test]
        [Category("Core")]
        public void TilePresentationEvents_DoNotEnterDeterminismHash()
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
                    new TilePresentationEvent(
                        TilePresentationEventKind.SlideTileRedirected,
                        200,
                        cell,
                        TileFeatureKind.Slide,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0,
                        targetEntityId: 30,
                        direction: Direction.Up),
                    new TilePresentationEvent(
                        TilePresentationEventKind.BarricadeBlocked,
                        300,
                        cell,
                        TileFeatureKind.Barricade,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0,
                        targetEntityId: 40,
                        direction: Direction.Right),
                    new TilePresentationEvent(
                        TilePresentationEventKind.BarricadeCrushed,
                        400,
                        cell,
                        TileFeatureKind.Barricade,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0,
                        targetEntityId: 50),
                });

            Assert.That(
                hashBuilder.Build(3, snapshot, CreateTickResultData(snapshot, TickPresentationData.Empty)),
                Is.EqualTo(hashBuilder.Build(3, snapshot, CreateTickResultData(snapshot, presentationData))));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_Pipeline_InitialIdleBoxDoesNotActivate()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var worldState = CreateWorldState(new[] { box }, new[] { button });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(10) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetTileFeature(10, out var storedButton), Is.True);
            Assert.That(storedButton.Flags, Is.EqualTo(TileFeatureFlags.None));
            Assert.That(
                result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.ButtonActivated),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_Pipeline_PushEnteringButtonAsSlidingDoesNotActivate()
        {
            var buttonCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                new[] { CreateButton(100, buttonCell) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(buttonCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(finalSnapshot.TryGetTileFeature(100, out var buttonAfter), Is.True);
            Assert.That(buttonAfter.Flags, Is.EqualTo(TileFeatureFlags.None));
            Assert.That(
                result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.ButtonActivated),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_Pipeline_SlidingBoxPassesThroughButtonDoesNotActivate()
        {
            var buttonCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var slidingBox = CreateBox(
                20,
                new SurfaceCell(FaceId.Floor, 1, 0),
                state: EntityPhaseState.Sliding,
                facing: Direction.Right);
            slidingBox.stateTimer = 0;
            var worldState = CreateWorldState(
                new[] { slidingBox },
                new[] { CreateButton(100, buttonCell) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100) },
                Array.Empty<IEntityLogic>());

            TickResult result = null;
            for (var tick = 7; tick <= 20; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick));
            }

            var finalSnapshot = worldState.CreateSnapshot();
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.Not.EqualTo(buttonCell));
            Assert.That(finalSnapshot.TryGetTileFeature(100, out var buttonAfter), Is.True);
            Assert.That(buttonAfter.Flags, Is.EqualTo(TileFeatureFlags.None));
            Assert.That(
                result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.ButtonActivated),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_IsVisibleInFinalAttackReadSnapshot_AndAppliedToAuthoritativeWorld()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell, state: EntityPhaseState.Sliding, facing: Direction.Right);
            box.stateTimer = 0;
            var attackLogic = new CapturingAttackLogic();
            var worldState = CreateWorldState(
                new[] { box },
                new[] { button },
                new GameplayTerrainData(new[] { CreateWallLikeTerrain(new SurfaceCell(FaceId.Floor, 2, 1)) }));
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(10) },
                new[] { attackLogic });

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(attackLogic.CapturedSnapshots.Count, Is.EqualTo(2));
            Assert.That(attackLogic.CapturedSnapshots[1].TryGetTileFeature(10, out var attackReadButton), Is.True);
            Assert.That((attackReadButton.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
            Assert.That(
                result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.ButtonActivated),
                Is.True);

            var finalSnapshot = worldState.CreateSnapshot();
            Assert.That(finalSnapshot.TryGetEntity(20, out var finalBox), Is.True);
            Assert.That(finalBox.position, Is.EqualTo(button.Cell));
            Assert.That(finalBox.state, Is.EqualTo(EntityPhaseState.Idle));
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

        private static TileEffectResolutionResult Resolve(
            WorldSnapshot snapshot,
            IReadOnlyList<TileEffectBoxStop> stops,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return ResolveWithStops(snapshot, stops, definitions);
        }

        private static TileEffectResolutionResult ResolveWithStops(
            WorldSnapshot snapshot,
            IReadOnlyList<TileEffectBoxStop> stops,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return TileFeatureEffectResolver.Instance.Resolve(
                new TileEffectResolutionContext(7, snapshot, definitions, boxStops: stops));
        }

        private static TileEffectResolutionResult Resolve(
            WorldSnapshot snapshot,
            WorldSnapshot previousSnapshot,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return Resolve(snapshot, previousSnapshot, Array.Empty<TileEffectBoxContact>(), definitions);
        }

        private static TileEffectResolutionResult Resolve(
            WorldSnapshot snapshot,
            WorldSnapshot previousSnapshot,
            IReadOnlyList<TileEffectBoxContact> contacts,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return TileFeatureEffectResolver.Instance.Resolve(
                new TileEffectResolutionContext(7, snapshot, definitions, contacts, previousSnapshot));
        }

        private static TileEffectResolutionResult Resolve(
            WorldSnapshot snapshot,
            WorldSnapshot previousSnapshot,
            IReadOnlyList<TileEffectBoxContact> contacts,
            IReadOnlyList<TileEffectBoxStop> stops,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return TileFeatureEffectResolver.Instance.Resolve(
                new TileEffectResolutionContext(7, snapshot, definitions, contacts, previousSnapshot, stops));
        }

        private static TileEffectBoxStop CreateStop(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxMovementFamily movementFamily)
        {
            return new TileEffectBoxStop(boxEntityId, cell, movementFamily);
        }

        private static FinalizationOperationMetadata CreateMovementMetadata(
            MovementSemanticKind movementSemanticKind,
            int localActionIndex = 0)
        {
            var semanticKind = movementSemanticKind switch
            {
                MovementSemanticKind.Push => ResolvedActionSemanticKind.Push,
                MovementSemanticKind.Slide => ResolvedActionSemanticKind.Slide,
                MovementSemanticKind.Flip => ResolvedActionSemanticKind.Flip,
                MovementSemanticKind.Stop => ResolvedActionSemanticKind.Stop,
                _ => ResolvedActionSemanticKind.Move,
            };
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                semanticKind,
                sourceActorEntityId: 10,
                actionPlanId: 1,
                localActionIndex: localActionIndex,
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

        private static string RunBarricadeHashScenario(bool includeBarricade)
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var tileFeatures = includeBarricade
                ? new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) }
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
                includeBarricade
                    ? new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) }
                    : Array.Empty<TileFeatureRuntimeDefinition>(),
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            return pipeline.RunTick(new TickInput(7)).DeterminismHash;
        }

        private static string RunBarricadeCrushHashScenario(bool crushEnabled)
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 4)),
                    CreateBox(20, barricadeCell),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[]
                {
                    CreateDefinition(
                        100,
                        crushEnabled ? TileFeatureActivationRule.FrontFaceOnly : TileFeatureActivationRule.BottomFaceOnly,
                        selector: TileFeatureBoxSelector.None),
                },
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            return pipeline.RunTick(new TickInput(7, PlayerTickCommand.Move(Direction.Up))).DeterminismHash;
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

        private static SnapshotMaterializationCounts RunBarricadeBlockedBudgetScenario()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)),
                });

            using var capture = SnapshotMaterializationDiagnostics.BeginCapture();
            pipeline.RunTick(new TickInput(7));
            return capture.Counts;
        }

        private static SnapshotMaterializationCounts RunBarricadeNoTransitionBudgetScenario()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 1, 1);
            var worldState = CreateWorldState(
                new[] { CreateBox(20, barricadeCell) },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            using var capture = SnapshotMaterializationDiagnostics.BeginCapture();
            pipeline.RunTick(new TickInput(7));
            return capture.Counts;
        }

        private static SnapshotMaterializationCounts RunBarricadeCrushBudgetScenario(bool crushEnabled)
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 4)),
                    CreateBox(20, barricadeCell),
                },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[]
                {
                    CreateDefinition(
                        100,
                        crushEnabled ? TileFeatureActivationRule.FrontFaceOnly : TileFeatureActivationRule.BottomFaceOnly,
                        selector: TileFeatureBoxSelector.None),
                },
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            using var capture = SnapshotMaterializationDiagnostics.BeginCapture();
            pipeline.RunTick(new TickInput(7, PlayerTickCommand.Move(Direction.Up)));
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
            GameplayTerrainData terrainData = null,
            CubeTopologyState? topology = null)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                TestBounds,
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor),
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

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            var unit = CreateUnit(entityId, position);
            unit.teamId = 2;
            return unit;
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
