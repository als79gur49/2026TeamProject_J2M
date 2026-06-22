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
        public void ButtonLatch_FlipStopOnSameCell_LatchesWithSingleUpdate()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip);
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Flip) },
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
        public void TileFeatureEffectResolver_StillSeesSameOrderedFeatures()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var lowerIdButton = CreateButton(10, cell);
            var higherIdButton = CreateButton(30, cell);
            var box = CreateBox(20, cell);
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { higherIdButton, lowerIdButton }).CreateSnapshot(),
                new[] { CreateStop(20, cell, TileEffectBoxMovementFamily.Push) },
                CreateDefinition(30),
                CreateDefinition(10));

            CollectionAssert.AreEqual(
                new[] { 10, 30 },
                result.Operations.Operations.Select(operation => operation.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_FlipStopOnSameCell_EmitsMotionContactPresentationAnchor()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip);
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[]
                {
                    CreateStop(
                        20,
                        button.Cell,
                        TileEffectBoxMovementFamily.Flip,
                        MovementSemanticKind.Flip,
                        actionPlanId: 45,
                        localActionIndex: 2),
                },
                CreateDefinition(10));

            Assert.That(result.Operations.Operations.Single().State.Flags, Is.EqualTo(TileFeatureFlags.Activated));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.ButtonActivated));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.BarrierKey.Kind, Is.EqualTo(PresentationBarrierKind.ButtonActivated));
            Assert.That(tileEvent.BarrierKey.PrimaryId, Is.EqualTo(10));
            Assert.That(tileEvent.TimingAnchor.Kind, Is.EqualTo(PresentationTimingKind.MotionContact));
            Assert.That(tileEvent.TimingAnchor.SourceEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.TimingAnchor.ActionPlanId, Is.EqualTo(45));
            Assert.That(tileEvent.TimingAnchor.LocalActionIndex, Is.EqualTo(2));
            Assert.That(tileEvent.TimingAnchor.MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Flip));
            Assert.That(
                tileEvent.TimingAnchor.VisualContactNormalizedTime,
                Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
            Assert.That(
                tileEvent.TimingAnchor.VisualContactNormalizedTime,
                Is.Not.EqualTo(GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_PushStopOnSameCell_RemainsImmediatePresentation()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell);
            var result = ResolveWithStops(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[]
                {
                    CreateStop(
                        20,
                        button.Cell,
                        TileEffectBoxMovementFamily.Push,
                        MovementSemanticKind.Push,
                        actionPlanId: 45,
                        localActionIndex: 0),
                },
                CreateDefinition(10));

            Assert.That(result.Operations.Operations.Single().State.Flags, Is.EqualTo(TileFeatureFlags.Activated));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].TimingAnchor.Kind, Is.EqualTo(PresentationTimingKind.Immediate));
            Assert.That(result.TileEvents[0].BarrierKey.Kind, Is.EqualTo(PresentationBarrierKind.ButtonActivated));
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
        public void ButtonLatch_FlipLandingContactWithoutStopFact_DoesNotLatch()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var box = CreateBox(20, button.Cell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip);
            var result = Resolve(
                CreateWorldState(new[] { box }, new[] { button }).CreateSnapshot(),
                new[] { new TileEffectBoxContact(20, button.Cell, TileEffectBoxContactKind.FlipLanding) },
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
                new object[] { "Unit", new[] { CreateUnit(20, cell) } },
                new object[] { "NonBoxSolid", new[] { CreateSolid(21, cell) } },
                new object[] { "NonPushableBox", new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Flip) } },
                new object[] { "DetachedBox", new[] { CreateBox(20, cell, boardPresence: EntityBoardPresence.Detached) } },
                new object[] { "DeadBox", new[] { CreateBox(20, cell, hp: 0) } },
                new object[] { "MarkedForDeathBox", new[] { CreateBox(20, cell, markedForDeath: true) } },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var button = CreateButton(10, cell);
                var result = ResolveWithStops(
                    CreateWorldState(entities, new[] { button }).CreateSnapshot(),
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
                new object[] { "NormalPushableBox", new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Push) } },
                new object[] { "Unit", new[] { CreateUnit(20, cell) } },
                new object[] { "NonBoxSolid", new[] { CreateSolid(21, cell) } },
                new object[] { "DetachedMoonBox", new[] { CreateBox(20, cell, boxCapabilities: moonCapabilities, boardPresence: EntityBoardPresence.Detached, boxArchetype: BoxArchetype.Moon) } },
                new object[] { "DeadMoonBox", new[] { CreateBox(20, cell, hp: 0, boxCapabilities: moonCapabilities, boxArchetype: BoxArchetype.Moon) } },
                new object[] { "MarkedMoonBox", new[] { CreateBox(20, cell, boxCapabilities: moonCapabilities, markedForDeath: true, boxArchetype: BoxArchetype.Moon) } },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var button = CreateButton(10, cell);
                var result = ResolveWithStops(
                    CreateWorldState(entities, new[] { button }).CreateSnapshot(),
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
        public void ButtonLatch_FlipOnlyBoxWithFlipStop_DoesNotLatchAnyPushableButton()
        {
            var button = CreateButton(10, new SurfaceCell(FaceId.Floor, 1, 1));
            var flipOnlyBox = CreateBox(20, button.Cell, boxCapabilities: BoxCapabilities.Flip);
            var result = ResolveWithStops(
                CreateWorldState(new[] { flipOnlyBox }, new[] { button }).CreateSnapshot(),
                new[] { CreateStop(20, button.Cell, TileEffectBoxMovementFamily.Flip) },
                CreateDefinition(10, selector: TileFeatureBoxSelector.AnyPushableBox));

            Assert.That(result.IsEmpty, Is.True);
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
        public void StopFacts_FlipMoveEndingIdle_CreatesFlipStop()
        {
            var beforeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var buttonCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, beforeCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, buttonCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(20, buttonCell, CreateMovementMetadata(MovementSemanticKind.Flip));

            var stops = TickPipeline.BuildTileEffectBoxStops(beforeSnapshot, finalSnapshot, batch);

            Assert.That(stops, Has.Count.EqualTo(1));
            Assert.That(stops[0].BoxEntityId, Is.EqualTo(20));
            Assert.That(stops[0].Cell, Is.EqualTo(buttonCell));
            Assert.That(stops[0].MovementFamily, Is.EqualTo(TileEffectBoxMovementFamily.Flip));
            Assert.That(stops[0].Cause.MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Flip));
            Assert.That(stops[0].Cause.ActionPlanId, Is.EqualTo(1));
            Assert.That(stops[0].Cause.LocalActionIndex, Is.Zero);
            Assert.That(
                stops[0].Cause.VisualContactNormalizedTime,
                Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_FlipMove_PreservesMotionContactMetadata()
        {
            var beforeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var buttonCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, beforeCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, buttonCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                buttonCell,
                CreateMovementMetadata(
                    MovementSemanticKind.Flip,
                    actionPlanId: 45,
                    intentId: 9));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(beforeSnapshot, finalSnapshot, batch);

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.FlipLanding));
            Assert.That(contacts[0].MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Flip));
            Assert.That(contacts[0].ActionPlanId, Is.EqualTo(45));
            Assert.That(contacts[0].LocalActionIndex, Is.Zero);
            Assert.That(contacts[0].IntentId, Is.EqualTo(9));
            Assert.That(
                contacts[0].VisualContactNormalizedTime,
                Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_FlipImpactFollowThroughMove_PreservesMotionContactMetadata()
        {
            var beforeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var buttonCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, beforeCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, buttonCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                buttonCell,
                CreateMovementMetadata(
                    MovementSemanticKind.Flip,
                    localActionIndex: 1,
                    actionPlanId: 46,
                    intentId: 10));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(beforeSnapshot, finalSnapshot, batch);

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.ImpactFollowThrough));
            Assert.That(contacts[0].MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Flip));
            Assert.That(contacts[0].ActionPlanId, Is.EqualTo(46));
            Assert.That(contacts[0].LocalActionIndex, Is.EqualTo(1));
            Assert.That(contacts[0].IntentId, Is.EqualTo(10));
            Assert.That(
                contacts[0].VisualContactNormalizedTime,
                Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_MovingUnitOrdinaryMove_CreatesMoveEnterContact()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, fromCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, destinationCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                destinationCell,
                CreateMovementMetadata(
                    MovementSemanticKind.Move,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitOrdinaryLocomotion));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].EntityId, Is.EqualTo(20));
            Assert.That(contacts[0].EntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(contacts[0].FromCell, Is.EqualTo(fromCell));
            Assert.That(contacts[0].DestinationCell, Is.EqualTo(destinationCell));
            Assert.That(contacts[0].TileCell, Is.EqualTo(destinationCell));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.MoveEnter));
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_MovingUnitLocomotionAnchorCommit_CreatesMoveEnterContact()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, fromCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, destinationCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                destinationCell,
                CreateMovementMetadata(
                    MovementSemanticKind.Move,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].EntityId, Is.EqualTo(20));
            Assert.That(contacts[0].EntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(contacts[0].FromCell, Is.EqualTo(fromCell));
            Assert.That(contacts[0].DestinationCell, Is.EqualTo(destinationCell));
            Assert.That(contacts[0].TileCell, Is.EqualTo(destinationCell));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.MoveEnter));
            Assert.That(contacts[0].MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Move));
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_JumpLandingUnitSpecialLocomotion_CreatesMoveEnterContact()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, fromCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, destinationCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                destinationCell,
                CreateMovementMetadata(
                    MovementSemanticKind.JumpLanding,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].EntityId, Is.EqualTo(20));
            Assert.That(contacts[0].EntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(contacts[0].FromCell, Is.EqualTo(fromCell));
            Assert.That(contacts[0].DestinationCell, Is.EqualTo(destinationCell));
            Assert.That(contacts[0].TileCell, Is.EqualTo(destinationCell));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.MoveEnter));
            Assert.That(contacts[0].MovementSemanticKind, Is.EqualTo(MovementSemanticKind.JumpLanding));
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_ScriptedRelocationUnitMove_CreatesNoContact()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, fromCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, destinationCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                destinationCell,
                CreateMovementMetadata(
                    MovementSemanticKind.Move,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);

            Assert.That(contacts, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ContactFacts_SameCellLocomotionAnchorCommit_CreatesNoContact()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                cell,
                CreateMovementMetadata(
                    MovementSemanticKind.Move,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);

            Assert.That(contacts, Is.Empty);
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
        public void StopFacts_FlipImpactFollowThroughMove_CreatesFlipStop()
        {
            var beforeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var buttonCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var beforeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, beforeCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                    new[] { CreateBox(20, buttonCell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(20, buttonCell, CreateMovementMetadata(MovementSemanticKind.Flip, localActionIndex: 1));

            var stops = TickPipeline.BuildTileEffectBoxStops(beforeSnapshot, finalSnapshot, batch);

            Assert.That(stops, Has.Count.EqualTo(1));
            Assert.That(stops[0].BoxEntityId, Is.EqualTo(20));
            Assert.That(stops[0].Cell, Is.EqualTo(buttonCell));
            Assert.That(stops[0].MovementFamily, Is.EqualTo(TileEffectBoxMovementFamily.Flip));
            Assert.That(stops[0].Cause.MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Flip));
            Assert.That(stops[0].Cause.LocalActionIndex, Is.EqualTo(1));
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
            Assert.That(tileEvent.TimingAnchor.Kind, Is.EqualTo(PresentationTimingKind.Immediate));
            Assert.That(snapshot.TryGetTileFeature(10, out var storedTile), Is.True);
            Assert.That(storedTile.Kind, Is.EqualTo(TileFeatureKind.Destroy));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_ActiveBottomFace_FlipContactEmitsMotionContactTiming()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var destroyTile = CreateTileFeature(10, cell, TileFeatureKind.Destroy);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    new[] { destroyTile })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                new[]
                {
                    new TileEffectEntityContact(
                        20,
                        EntityType.Box,
                        fromCell,
                        cell,
                        cell,
                        TileEffectEntityContactKind.FlipLanding,
                        MovementSemanticKind.Flip,
                        operationOrder: 0,
                        actionPlanId: 45,
                        localActionIndex: 0,
                        intentId: 9,
                        visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime),
                },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            var tileEvent = result.TileEvents.Single();
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.TimingAnchor.Kind, Is.EqualTo(PresentationTimingKind.MotionContact));
            Assert.That(tileEvent.TimingAnchor.SourceEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.TimingAnchor.TargetEntityId, Is.Zero);
            Assert.That(tileEvent.TimingAnchor.ActionPlanId, Is.EqualTo(45));
            Assert.That(tileEvent.TimingAnchor.LocalActionIndex, Is.Zero);
            Assert.That(tileEvent.TimingAnchor.MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Flip));
            Assert.That(
                tileEvent.TimingAnchor.VisualContactNormalizedTime,
                Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_ActiveBottomFace_FlipImpactFollowThroughContactEmitsMotionContactTiming()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var destroyTile = CreateTileFeature(10, cell, TileFeatureKind.Destroy);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip) },
                    new[] { destroyTile })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                new[]
                {
                    new TileEffectEntityContact(
                        20,
                        EntityType.Box,
                        fromCell,
                        cell,
                        cell,
                        TileEffectEntityContactKind.ImpactFollowThrough,
                        MovementSemanticKind.Flip,
                        operationOrder: 0,
                        actionPlanId: 46,
                        localActionIndex: 1,
                        intentId: 10,
                        visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime),
                },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            var tileEvent = result.TileEvents.Single();
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.TimingAnchor.Kind, Is.EqualTo(PresentationTimingKind.MotionContact));
            Assert.That(tileEvent.TimingAnchor.ActionPlanId, Is.EqualTo(46));
            Assert.That(tileEvent.TimingAnchor.LocalActionIndex, Is.EqualTo(1));
            Assert.That(tileEvent.TimingAnchor.MovementSemanticKind, Is.EqualTo(MovementSemanticKind.Flip));
            Assert.That(
                tileEvent.TimingAnchor.VisualContactNormalizedTime,
                Is.EqualTo(GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime));
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
        public void DestroyTile_ActiveFaceOnly_DestroysMovingBoxOnActiveFace()
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
                CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly));

            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].EntityId, Is.EqualTo(20));
            Assert.That(result.TileEvents.Single().EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_InactiveFaceOnly_DestroysMovingBoxOnInactiveFace()
        {
            var cell = new SurfaceCell(FaceId.Back, 1, 1);
            var destroyTile = CreateTileFeature(10, cell, TileFeatureKind.Destroy);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { destroyTile })
                .CreateSnapshot();

            var result = Resolve(
                snapshot,
                new[] { new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter) },
                CreateDefinition(10, TileFeatureActivationRule.InactiveFaceOnly));

            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].EntityId, Is.EqualTo(20));
            Assert.That(result.TileEvents.Single().EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_InactiveFaceOnly_DoesNotDestroyMovingBoxOnActiveFace()
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
                CreateDefinition(10, TileFeatureActivationRule.InactiveFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void DestroyTileActivationFact_InactiveToActiveTransition_DestroysSameCellBox()
        {
            var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var previousSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var topologyBatch = new FinalizationBatch();
            topologyBatch.SetTopology(currentSnapshot.Topology);

            var facts = TickPipeline.BuildDestroyTileActivationOccupantFacts(
                previousSnapshot,
                currentSnapshot,
                new[] { CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly) },
                TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                topologyBatch);
            var result = ResolveWithActivationFacts(
                currentSnapshot,
                facts,
                CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly));

            Assert.That(facts, Has.Count.EqualTo(1));
            Assert.That(facts[0].FeatureCell, Is.EqualTo(cell));
            Assert.That(facts[0].OccupantEntityId, Is.EqualTo(20));
            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].Metadata.BoundaryReason, Is.EqualTo("DestroyTile"));
            Assert.That(result.TileEvents.Single().EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(result.TileEvents.Single().TargetEntityId, Is.EqualTo(20));
            Assert.That(result.TileEvents.Single().TimingAnchor.Kind, Is.EqualTo(PresentationTimingKind.Immediate));
        }

        [Test]
        [Category("Core")]
        public void DestroyTileActivationFact_InactiveToActiveTransition_DestroysSameCellGroundUnit()
        {
            var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var previousSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var topologyBatch = new FinalizationBatch();
            topologyBatch.SetTopology(currentSnapshot.Topology);

            var facts = TickPipeline.BuildDestroyTileActivationOccupantFacts(
                previousSnapshot,
                currentSnapshot,
                new[] { CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly) },
                TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                topologyBatch);
            var result = ResolveWithActivationFacts(
                currentSnapshot,
                facts,
                CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly));

            Assert.That(facts, Has.Count.EqualTo(1));
            Assert.That(facts[0].FeatureCell, Is.EqualTo(cell));
            Assert.That(facts[0].OccupantEntityId, Is.EqualTo(20));
            Assert.That(facts[0].OccupantType, Is.EqualTo(EntityType.Unit));
            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].Metadata.BoundaryReason, Is.EqualTo("DestroyTile"));
            Assert.That(result.TileEvents.Single().EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(result.TileEvents.Single().TargetEntityId, Is.EqualTo(20));
            Assert.That(result.TileEvents.Single().TimingAnchor.Kind, Is.EqualTo(PresentationTimingKind.Immediate));
        }

        [Test]
        [Category("Core")]
        public void DestroyTileActivationFact_NonActivationTransitions_DoNotCreateFacts()
        {
            var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var definition = CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly);
            var inactiveSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var activeSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var inactiveTopologyBatch = new FinalizationBatch();
            inactiveTopologyBatch.SetTopology(inactiveSnapshot.Topology);

            Assert.That(
                TickPipeline.BuildDestroyTileActivationOccupantFacts(
                    activeSnapshot,
                    activeSnapshot,
                    new[] { definition },
                    TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                    inactiveTopologyBatch),
                Is.Empty,
                "active to active");
            Assert.That(
                TickPipeline.BuildDestroyTileActivationOccupantFacts(
                    inactiveSnapshot,
                    inactiveSnapshot,
                    new[] { definition },
                    TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                    inactiveTopologyBatch),
                Is.Empty,
                "inactive to inactive");
            Assert.That(
                TickPipeline.BuildDestroyTileActivationOccupantFacts(
                    activeSnapshot,
                    inactiveSnapshot,
                    new[] { definition },
                    TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                    inactiveTopologyBatch),
                Is.Empty,
                "active to inactive");
            Assert.That(
                TickPipeline.BuildDestroyTileActivationOccupantFacts(
                    inactiveSnapshot,
                    activeSnapshot,
                    new[] { definition },
                    TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant),
                Is.Empty,
                "missing topology source");
        }

        [Test]
        [Category("Core")]
        public void DestroyTileActivationFact_InvalidOccupantsAndDifferentFaces_DoNotCreateFacts()
        {
            var destroyCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var definition = CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly);
            var topologyBatch = new FinalizationBatch();
            topologyBatch.SetTopology(new CubeTopologyState(FaceId.Front));
            var cases = new[]
            {
                new object[] { "AirUnit", new[] { CreateUnit(20, destroyCell, UnitMobilityKind.Air) } },
                new object[] { "DeadBox", new[] { CreateBox(22, destroyCell, hp: 0) } },
                new object[] { "DetachedBox", new[] { CreateBox(23, destroyCell, boardPresence: EntityBoardPresence.Detached) } },
                new object[] { "MarkedBox", new[] { CreateBox(24, destroyCell, markedForDeath: true) } },
                new object[] { "DifferentFaceSamePlanar", new[] { CreateBox(25, new SurfaceCell(FaceId.Floor, 1, 1)) } },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var previousSnapshot = CreateWorldState(
                        entities,
                        new[] { CreateTileFeature(10, destroyCell, TileFeatureKind.Destroy) },
                        topology: new CubeTopologyState(FaceId.Floor))
                    .CreateSnapshot();
                var currentSnapshot = CreateWorldState(
                        entities,
                        new[] { CreateTileFeature(10, destroyCell, TileFeatureKind.Destroy) },
                        topology: new CubeTopologyState(FaceId.Front))
                    .CreateSnapshot();

                Assert.That(
                    TickPipeline.BuildDestroyTileActivationOccupantFacts(
                        previousSnapshot,
                        currentSnapshot,
                        new[] { definition },
                        TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                        topologyBatch),
                    Is.Empty,
                    name);
            }
        }

        [Test]
        [Category("Core")]
        public void DestroyTileActivationFact_ExcludedSourceKinds_DoNotCreateFacts()
        {
            var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var previousSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var topologyBatch = new FinalizationBatch();
            topologyBatch.SetTopology(currentSnapshot.Topology);
            var excludedSources = new[]
            {
                TileEffectTriggerSourceKind.SpawnSettlement,
                TileEffectTriggerSourceKind.RespawnSettlement,
                TileEffectTriggerSourceKind.ScriptedRelocation,
                TileEffectTriggerSourceKind.PersistentOverlapDiagnosticOnly,
            };

            for (var i = 0; i < excludedSources.Length; i++)
            {
                Assert.That(
                    TickPipeline.BuildDestroyTileActivationOccupantFacts(
                        previousSnapshot,
                        currentSnapshot,
                        new[] { CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly) },
                        excludedSources[i],
                        topologyBatch),
                    Is.Empty,
                    excludedSources[i].ToString());
            }
        }

        [Test]
        [Category("Core")]
        public void DestroyTileActivationFact_DedupesWithMovementContact()
        {
            var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var contacts = new[]
            {
                new TileEffectBoxContact(20, cell, TileEffectBoxContactKind.PushEnter),
            };
            var facts = new[]
            {
                CreateActivationFact(10, cell, 20),
            };

            var result = Resolve(
                snapshot,
                null,
                contacts,
                facts,
                CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly));

            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void DestroyTileActivationFact_BarricadeCrushSameTick_DedupesAndDestroyTileWins()
        {
            var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var previousSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[]
                    {
                        CreateTileFeature(10, cell, TileFeatureKind.Destroy),
                        CreateTileFeature(11, cell, TileFeatureKind.Barricade),
                    },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { CreateBox(20, cell) },
                    new[]
                    {
                        CreateTileFeature(10, cell, TileFeatureKind.Destroy),
                        CreateTileFeature(11, cell, TileFeatureKind.Barricade),
                    },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                Array.Empty<TileEffectBoxContact>(),
                new[] { CreateActivationFact(10, cell, 20) },
                CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly),
                CreateDefinition(11, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Metadata.BoundaryReason, Is.EqualTo("DestroyTile"));
            Assert.That(result.EntityOperations.Operations[1].Metadata.BoundaryReason, Is.EqualTo("DestroyTile"));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_ActiveBottomFace_DestroysMovingGroundUnit()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var destroyTile = CreateTileFeature(10, cell, TileFeatureKind.Destroy);
            var snapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    new[] { destroyTile })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                new[]
                {
                    new TileEffectEntityContact(
                        20,
                        EntityType.Unit,
                        fromCell,
                        cell,
                        cell,
                        TileEffectEntityContactKind.MoveEnter,
                        MovementSemanticKind.Move,
                        operationOrder: 0),
                },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.Operations.IsEmpty, Is.True);
            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(2));
            Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(result.EntityOperations.Operations[0].BoardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(result.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(result.EntityOperations.Operations[1].EntityId, Is.EqualTo(20));
            Assert.That(result.EntityOperations.Operations[1].Metadata.DamageSourceType, Is.EqualTo(DamageSourceType.Environmental));
            Assert.That(result.EntityOperations.Operations[1].Metadata.BoundaryReason, Is.EqualTo("DestroyTile"));
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_ActiveBottomFace_DoesNotDestroyMovingAirUnit()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell, UnitMobilityKind.Air) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                new[]
                {
                    new TileEffectEntityContact(
                        20,
                        EntityType.Unit,
                        fromCell,
                        cell,
                        cell,
                        TileEffectEntityContactKind.MoveEnter,
                        MovementSemanticKind.Move,
                        operationOrder: 0),
                },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_GroundUnit_LocomotionAnchorCommitIntoActiveDestroyTile_Dies()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, fromCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                cell,
                CreateMovementMetadata(
                    MovementSemanticKind.Move,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);
            var result = ResolveEntityContacts(
                destinationSnapshot,
                contacts,
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.MoveEnter));
            Assert.That(result.EntityOperations.Operations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MarkDestroy &&
                operation.EntityId == 20), Is.True);
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void PlayerDestroyTileEffect_AirPlayerEnteringActiveDestroyTile_DoesNotKillOrTrigger()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourcePlayer = CreateUnit(20, fromCell, UnitMobilityKind.Air);
            sourcePlayer.unitRole = UnitRole.Player;
            var destinationPlayer = CreateUnit(20, cell, UnitMobilityKind.Air);
            destinationPlayer.unitRole = UnitRole.Player;
            var sourceSnapshot = CreateWorldState(
                    new[] { sourcePlayer },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { destinationPlayer },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                cell,
                CreateMovementMetadata(
                    MovementSemanticKind.Move,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);
            var result = ResolveEntityContacts(
                destinationSnapshot,
                contacts,
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.MoveEnter));
            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.TileEvents, Is.Empty);
            Assert.That(destinationSnapshot.TryGetEntity(20, out var player), Is.True);
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(player.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(player.markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_GroundUnit_JumpLandingOntoActiveDestroyTile_Dies()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, fromCell) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                cell,
                CreateMovementMetadata(
                    MovementSemanticKind.JumpLanding,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);
            var result = ResolveEntityContacts(
                destinationSnapshot,
                contacts,
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.MoveEnter));
            Assert.That(contacts[0].MovementSemanticKind, Is.EqualTo(MovementSemanticKind.JumpLanding));
            Assert.That(result.EntityOperations.Operations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MarkDestroy &&
                operation.EntityId == 20), Is.True);
            Assert.That(result.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_AirUnit_JumpLandingOntoActiveDestroyTile_Survives()
        {
            var fromCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var sourceSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, fromCell, UnitMobilityKind.Air) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var destinationSnapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell, UnitMobilityKind.Air) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();
            var batch = new FinalizationBatch();
            batch.MoveEntity(
                20,
                cell,
                CreateMovementMetadata(
                    MovementSemanticKind.JumpLanding,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.UnitSpecialLocomotion));

            var contacts = TickPipeline.BuildTileEffectEntityContacts(sourceSnapshot, destinationSnapshot, batch);
            var result = ResolveEntityContacts(
                destinationSnapshot,
                contacts,
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].ContactKind, Is.EqualTo(TileEffectEntityContactKind.MoveEnter));
            Assert.That(contacts[0].MovementSemanticKind, Is.EqualTo(MovementSemanticKind.JumpLanding));
            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.TileEvents, Is.Empty);
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
        public void DestroyTile_StationaryUnit_DoesNotDestroy()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                Array.Empty<TileEffectEntityContact>(),
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_InactiveFace_MovingUnit_DoesNotDestroy()
        {
            var fromCell = new SurfaceCell(FaceId.Front, 0, 1);
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateUnit(20, cell) },
                    new[] { CreateTileFeature(10, cell, TileFeatureKind.Destroy) })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                new[]
                {
                    new TileEffectEntityContact(
                        20,
                        EntityType.Unit,
                        fromCell,
                        cell,
                        cell,
                        TileEffectEntityContactKind.MoveEnter,
                        MovementSemanticKind.Move,
                        operationOrder: 0),
                },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_UnitOnDifferentFaceSameXY_DoesNotDestroy()
        {
            var tileCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var unitCell = new SurfaceCell(FaceId.Front, 1, 1);
            var fromCell = new SurfaceCell(FaceId.Front, 0, 1);
            var snapshot = CreateWorldState(
                    new[] { CreateUnit(20, unitCell) },
                    new[] { CreateTileFeature(10, tileCell, TileFeatureKind.Destroy) })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                new[]
                {
                    new TileEffectEntityContact(
                        20,
                        EntityType.Unit,
                        fromCell,
                        unitCell,
                        unitCell,
                        TileEffectEntityContactKind.MoveEnter,
                        MovementSemanticKind.Move,
                        operationOrder: 0),
                },
                CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_MovingBoxAndUnit_ProducesDeterministicOrder()
        {
            var boxCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var unitCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var snapshot = CreateWorldState(
                    new[]
                    {
                        CreateBox(30, boxCell),
                        CreateUnit(20, unitCell),
                    },
                    new[]
                    {
                        CreateTileFeature(300, boxCell, TileFeatureKind.Destroy),
                        CreateTileFeature(100, unitCell, TileFeatureKind.Destroy),
                    })
                .CreateSnapshot();

            var result = ResolveEntityContacts(
                snapshot,
                new[]
                {
                    new TileEffectEntityContact(30, EntityType.Box, boxCell, boxCell, boxCell, TileEffectEntityContactKind.PushEnter, MovementSemanticKind.Push, 0),
                    new TileEffectEntityContact(20, EntityType.Unit, unitCell, unitCell, unitCell, TileEffectEntityContactKind.MoveEnter, MovementSemanticKind.Move, 1),
                    new TileEffectEntityContact(20, EntityType.Unit, unitCell, unitCell, unitCell, TileEffectEntityContactKind.MoveEnter, MovementSemanticKind.Move, 2),
                },
                CreateDefinition(300, TileFeatureActivationRule.BottomFaceOnly),
                CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly));

            Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(4));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TargetEntityId).ToArray(), Is.EqualTo(new[] { 20, 30 }));
            Assert.That(result.TileEvents.Select(tileEvent => tileEvent.TileId).ToArray(), Is.EqualTo(new[] { 100, 300 }));
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_Pipeline_MovingEnemyKilledBeforeAttack_UsesEnemyDeathExit()
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var attackLogic = new AttackIfPresentLogic(30, 40);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateEnemyUnit(30, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 2, 0)),
                },
                new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(30, 100, new Vector2Int(1, 0), MovementCommandKind.Move)),
                    attackLogic,
                });

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(result.AttackPhaseResult.RawIntents.Any(intent => intent.SourceId == 30), Is.False);
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(result.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(30));
            Assert.That(result.PresentationData.EntityExitSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitedEntityId, Is.EqualTo(30));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitCause, Is.EqualTo(TickEntityExitCause.Killed));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitCause, Is.Not.EqualTo(TickEntityExitCause.OutOfBounds));
            Assert.That(result.PresentationData.EntityExitSignals[0].EntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(result.PresentationData.EntityExitSignals[0].SourceCell, Is.EqualTo(destroyCell));
            Assert.That(
                result.PresentationData.EntityExitSignals[0].Timing,
                Is.EqualTo(EntityExitPresentationTiming.AfterEntityMotion));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_Pipeline_MovingPlayerKilled_EmitsPlayerDeathSignal()
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Move)),
                });

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(10));
            Assert.That(result.PresentationData.PlayerDeathSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerDeathSignals[0].EntityId, Is.EqualTo(10));
            Assert.That(result.PresentationData.PlayerDeathSignals[0].DidDieThisTick, Is.True);
            Assert.That(result.PresentationData.EntityExitSignals, Is.Empty);
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=10"));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out _), Is.False);
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
        public void SlideTile_ImpactFollowThrough_RedirectsPushAndSlideButNotFlip()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var cases = new[]
            {
                new object[] { "PushFollowThrough", MovementSemanticKind.Push, true },
                new object[] { "SlideFollowThrough", MovementSemanticKind.Slide, true },
                new object[] { "FlipFollowThrough", MovementSemanticKind.Flip, false },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var semanticKind = (MovementSemanticKind)cases[i][1];
                var shouldRedirect = (bool)cases[i][2];
                var snapshot = CreateWorldState(
                        new[] { CreateBox(20, cell, state: EntityPhaseState.Sliding) },
                        new[] { CreateTileFeature(100, cell, TileFeatureKind.Slide) })
                    .CreateSnapshot();

                var result = ResolveEntityContacts(
                    snapshot,
                    new[]
                    {
                        new TileEffectEntityContact(
                            20,
                            EntityType.Box,
                            new SurfaceCell(FaceId.Front, 0, 1),
                            cell,
                            cell,
                            TileEffectEntityContactKind.ImpactFollowThrough,
                            semanticKind,
                            operationOrder: 0,
                            actionPlanId: 45,
                            localActionIndex: 1,
                            intentId: 100),
                    },
                    CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None));

                if (!shouldRedirect)
                {
                    Assert.That(result.IsEmpty, Is.True, name);
                    continue;
                }

                Assert.That(result.EntityOperations.Operations.Count, Is.EqualTo(1), name);
                Assert.That(result.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetFacing), name);
                Assert.That(result.EntityOperations.Operations[0].EntityId, Is.EqualTo(20), name);
                Assert.That(result.EntityOperations.Operations[0].Facing, Is.EqualTo(Direction.Up), name);
                Assert.That(result.TileEvents, Has.Count.EqualTo(1), name);
                Assert.That(result.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.SlideTileRedirected), name);
                Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20), name);
                Assert.That(result.TileEvents[0].Direction, Is.EqualTo(Direction.Up), name);
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
        public void SlideTile_Pipeline_SlidingBoxKillsEnemyOnSlideTile_RedirectsFacing()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var slideCell = new SurfaceCell(FaceId.Front, 0, 1);
            var slidingBox = CreateBox(20, sourceCell, state: EntityPhaseState.Sliding, facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, slideCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateTileFeature(100, slideCell, TileFeatureKind.Slide) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Right, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            var slideEvent = result.PresentationData.TileEvents[0];
            Assert.That(slideEvent.EventKind, Is.EqualTo(TilePresentationEventKind.SlideTileRedirected));
            Assert.That(slideEvent.TileId, Is.EqualTo(100));
            Assert.That(slideEvent.Cell, Is.EqualTo(slideCell));
            Assert.That(slideEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(slideEvent.Direction, Is.EqualTo(Direction.Right));
            Assert.That(snapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(slideCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(boxAfter.facing, Is.EqualTo(Direction.Right));
            Assert.That(snapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
        }

        [Test]
        [Category("Core")]
        public void SlideTile_Pipeline_SlidingBoxEnemySurvives_DoesNotRedirect()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var slideCell = new SurfaceCell(FaceId.Front, 0, 1);
            var slidingBox = CreateBox(20, sourceCell, state: EntityPhaseState.Sliding, facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, slideCell);
            enemy.hp = 2;
            enemy.maxHp = 2;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateTileFeature(100, slideCell, TileFeatureKind.Slide) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Right, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(
                result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.SlideTileRedirected),
                Is.False);
            Assert.That(snapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(sourceCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(boxAfter.facing, Is.EqualTo(Direction.Up));
            Assert.That(snapshot.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.hp, Is.EqualTo(1));
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
        public void BarricadeMovementBlockerQuery_ActiveFrontFaceOnlyReturnsBlockerWithoutCreatingSnapshot()
        {
            var activeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var inactiveCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var unrelatedCell = new SurfaceCell(FaceId.Front, 1, 0);
            var snapshot = CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[]
                    {
                        CreateTileFeature(100, activeCell, TileFeatureKind.Barricade),
                        CreateTileFeature(101, inactiveCell, TileFeatureKind.Barricade),
                        CreateTileFeature(102, activeCell, TileFeatureKind.Destroy),
                        CreateTileFeature(103, unrelatedCell, TileFeatureKind.Barricade),
                    })
                .CreateSnapshot();
            var definitions = new[]
            {
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                CreateDefinition(101, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
                CreateDefinition(102, TileFeatureActivationRule.FrontFaceOnly),
                CreateDefinition(103, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
            };

            SnapshotMaterializationCounts counts;
            bool activeBoxResult;
            bool activeUnitResult;
            bool inactiveUnitResult;
            bool unrelatedUnitResult;
            bool missingDefinitionResult;
            bool nonBarricadeResult;
            bool tryGetActiveResult;
            TileFeatureState activeBarricade;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                activeBoxResult = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    definitions,
                    activeCell,
                    TileFeatureBlockerSubject.Box,
                    TileFeatureMovementKind.PushStart);
                activeUnitResult = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    definitions,
                    activeCell,
                    TileFeatureBlockerSubject.Unit,
                    TileFeatureMovementKind.GroundStep);
                tryGetActiveResult = TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                    snapshot,
                    definitions,
                    activeCell,
                    TileFeatureBlockerSubject.Unit,
                    TileFeatureMovementKind.Free2DTopologyTransition,
                    out activeBarricade);
                inactiveUnitResult = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    definitions,
                    inactiveCell,
                    TileFeatureBlockerSubject.Unit,
                    TileFeatureMovementKind.GroundStep);
                unrelatedUnitResult = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    definitions,
                    new SurfaceCell(FaceId.Front, 0, 0),
                    TileFeatureBlockerSubject.Unit,
                    TileFeatureMovementKind.GroundStep);
                missingDefinitionResult = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    new[] { definitions[1], definitions[2] },
                    activeCell,
                    TileFeatureBlockerSubject.Unit,
                    TileFeatureMovementKind.GroundStep);
                nonBarricadeResult = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                    snapshot,
                    new[] { definitions[2] },
                    activeCell,
                    TileFeatureBlockerSubject.Unit,
                    TileFeatureMovementKind.GroundStep);
                counts = capture.Counts;
            }

            Assert.That(activeBoxResult, Is.True);
            Assert.That(activeUnitResult, Is.True);
            Assert.That(tryGetActiveResult, Is.True);
            Assert.That(activeBarricade.TileId, Is.EqualTo(100));
            Assert.That(inactiveUnitResult, Is.False);
            Assert.That(unrelatedUnitResult, Is.False);
            Assert.That(missingDefinitionResult, Is.False);
            Assert.That(nonBarricadeResult, Is.False);
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void BarricadeEffectiveGameplayState_ActiveWithUnit_ReturnsSuppressedByUnit()
        {
            var cell = new SurfaceCell(FaceId.Front, 2, 0);
            var snapshot = CreateWorldState(
                    new[] { CreateEnemyUnit(30, cell) },
                    new[] { CreateTileFeature(100, cell, TileFeatureKind.Barricade) })
                .CreateSnapshot();
            var definition = CreateDefinition(
                100,
                TileFeatureActivationRule.FrontFaceOnly,
                selector: TileFeatureBoxSelector.None);

            Assert.That(snapshot.TryGetTileFeature(100, out var barricade), Is.True);
            var state = BarricadeEffectiveActivationPolicy.Evaluate(
                snapshot,
                snapshot.Topology,
                barricade,
                definition);

            Assert.That(state.TopologyActive, Is.True);
            Assert.That(state.EffectiveActive, Is.False);
            Assert.That(state.GameplayStateKind, Is.EqualTo(BarricadeGameplayStateKind.ActiveSuppressedByUnit));
            Assert.That(state.BlockingUnitId, Is.EqualTo(30));
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureMovementBlockerQuery_ActiveSuppressedBarricadeWithEnemy_DoesNotPreBlockBoxImpact()
        {
            var cell = new SurfaceCell(FaceId.Front, 2, 0);
            var snapshot = CreateWorldState(
                    new[] { CreateEnemyUnit(30, cell) },
                    new[] { CreateTileFeature(100, cell, TileFeatureKind.Barricade) })
                .CreateSnapshot();
            var definitions = new[]
            {
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None),
            };

            var boxBlocked = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                snapshot,
                definitions,
                cell,
                TileFeatureBlockerSubject.Box,
                TileFeatureMovementKind.PushStart);
            var unitEntrantBlocked = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                snapshot,
                definitions,
                cell,
                TileFeatureBlockerSubject.Unit,
                TileFeatureMovementKind.GroundStep,
                existingOccupantEntityId: 0);
            var existingUnitBlocked = TileFeatureMovementBlockerQuery.HasActiveBarricadeBlocker(
                snapshot,
                definitions,
                cell,
                TileFeatureBlockerSubject.Unit,
                TileFeatureMovementKind.GroundStep,
                existingOccupantEntityId: 30);

            Assert.That(boxBlocked, Is.False);
            Assert.That(unitEntrantBlocked, Is.True);
            Assert.That(existingUnitBlocked, Is.False);
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
            Assert.That(result.PresentationData.BoxSlideStopSignals, Is.Empty);
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.PresentationData.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeBlocked));
            Assert.That(tileEvent.TileId, Is.EqualTo(100));
            Assert.That(tileEvent.Cell, Is.EqualTo(barricadeCell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(tileEvent.Direction, Is.EqualTo(Direction.Right));
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
            Assert.That(result.PresentationData.BoxSlideStopSignals, Has.Count.EqualTo(1));
            var stopSignal = result.PresentationData.BoxSlideStopSignals[0];
            Assert.That(stopSignal.BoxEntityId, Is.EqualTo(20));
            Assert.That(stopSignal.StopperKind, Is.EqualTo(BoxSlideStopperKind.Barricade));
            Assert.That(stopSignal.StopperTileId, Is.EqualTo(100));
            Assert.That(stopSignal.StopperEntityId, Is.Zero);
            Assert.That(stopSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(stopSignal.StopperCell, Is.EqualTo(barricadeCell));
            Assert.That(stopSignal.SlideDirection, Is.EqualTo(Direction.Up));
            Assert.That(stopSignal.Cause, Is.EqualTo(BoxSlideStopCause.SlidingContinuationBlocked));
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
        public void Flip_EnemyOnSuppressedBarricade_WhenEnemySurvives_DestroysSelf()
        {
            var run = RunFlipBlockedByEnemyOnBarricadeScenario(enemyHp: 2);
            var result = run.Result;
            var finalSnapshot = run.FinalSnapshot;
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 1);
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.DestroySelf));
            Assert.That(disposition.AllTargetsDestroyed, Is.False);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.False);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.None.Contains("FlipLandingBlockedByBarricade"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated").And.Contains("At=Front(2,1)"));
            CollectionAssert.AreEqual(
                new[] { (SourceId: 20, TargetId: 30, Position: landingCell, Damage: 1) },
                result.AttackPhaseResult.DrainedImpactReservations
                    .Select(reservation => (reservation.SourceId, reservation.TargetId, reservation.ImpactCell, reservation.Damage))
                    .ToArray());
            Assert.That(result.PresentationData.FlipImpactSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.FlipImpactSignals[0].Disposition, Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.position, Is.EqualTo(landingCell));
            Assert.That(enemyAfter.hp, Is.EqualTo(1));
            Assert.That(enemyAfter.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(enemyAfter.markedForDeath, Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(landingCell, out _), Is.False);
            Assert.That(finalSnapshot.HasAnyUnitAt(landingCell), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Flip_EnemyOnSuppressedBarricade_WhenEnemyDies_ReassertCrushesBox()
        {
            var run = RunFlipBlockedByEnemyOnBarricadeScenario(enemyHp: 1);
            var result = run.Result;
            var finalSnapshot = run.FinalSnapshot;
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 1);
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.BarricadeReassertCrush));
            Assert.That(disposition.BarricadeTileId, Is.EqualTo(100));
            Assert.That(disposition.BarricadeCell, Is.EqualTo(landingCell));
            Assert.That(disposition.AllTargetsDestroyed, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated").And.Contains("At=Front(2,1)"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("Reason=BarricadeReassertCrush"));
            AssertBoxReassertCrushRemovalOps(
                result.MovementPhaseResult.ResolvedOperations,
                boxEntityId: 20,
                landingCell);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty, "BarricadeCrushed is the Flip presentation signal for reassert crush.");
            var crushedEvent = result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed);
            Assert.That(crushedEvent.TileId, Is.EqualTo(100));
            Assert.That(crushedEvent.Cell, Is.EqualTo(landingCell));
            Assert.That(crushedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.HasAnyUnitAt(landingCell), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(landingCell, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Flip_CrossSurfaceBarricadeCases_AreNotApplicable()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, TestBounds.MaxInclusive.y);
            var targetCell = new SurfaceCell(FaceId.Front, 0, TestBounds.MinInclusive.y);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, playerCell),
                    CreateBox(20, targetCell, boxCapabilities: BoxCapabilities.Flip),
                },
                new[] { CreateTileFeature(100, targetCell, TileFeatureKind.Barricade) },
                topology: new CubeTopologyState(FaceId.Floor));
            // Cross-face Flip is rejected by local geometry before any landing policy exists.
            // Keep this Barricade inactive so the NOT_APPLICABLE seam test stays separate from
            // the active-Barricade + Box transition crush invariant.
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, TestBounds.MaxInclusive.y + 1), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=FlipCrossesBoundary"));
            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.None.Contains("FlipLandingBlockedByBarricade"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.MovementPhaseResult.ImpactDispositionRecords, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(10, out var playerAfter), Is.True);
            Assert.That(playerAfter.position, Is.EqualTo(playerCell));
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(targetCell));
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
        public void Barricade_BlocksUnitGroundTraversal()
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

            Assert.That(playerResult.MovementPhaseResult.RejectedReasons, Has.Count.EqualTo(1));
            Assert.That(enemyResult.MovementPhaseResult.RejectedReasons, Has.Count.EqualTo(1));
            Assert.That(playerResult.MovementPhaseResult.RejectedReasons[0], Does.Contain("LegalityBlockerKinds=TileFeature"));
            Assert.That(enemyResult.MovementPhaseResult.RejectedReasons[0], Does.Contain("LegalityBlockerKinds=TileFeature"));
            Assert.That(playerResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(enemyResult.PresentationData.TileEvents, Is.Empty);
            Assert.That(playerWorldState.CreateSnapshot().TryGetEntity(10, out var playerAfter), Is.True);
            Assert.That(enemyWorldState.CreateSnapshot().TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(playerAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(enemyAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        [Category("Core")]
        public void Barricade_StandingBoxOnAlreadyActiveBarricade_IsCrushed()
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
            Assert.That(activeResult.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(activeResult.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeCrushed));
            Assert.That(activeResult.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(30));
            Assert.That(inactiveWorldState.CreateSnapshot().TryGetEntity(20, out var inactiveBox), Is.True);
            Assert.That(activeWorldState.CreateSnapshot().TryGetEntity(30, out _), Is.False);
            Assert.That(inactiveBox.markedForDeath, Is.False);
            Assert.That(activeWorldState.CreateSnapshot().TryGetSolidSemanticAt(activeCell, out _), Is.False);
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
        public void BarricadeActivation_BoxCrushStillOccurs_WhenNoUnitOccupant()
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

            Assert.That(result.EventLogEntries, Is.Empty);
            Assert.That(result.TileEvents.Single().EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeCrushed));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void BarricadeActivation_Defer_WhenSameCellUnitOccupantExists()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var unit = CreatePlayerUnit(10, barricadeCell);
            var previousSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations, Is.Empty);
            Assert.That(result.TileEvents, Is.Empty);
            Assert.That(result.EventLogEntries.Single(), Does.Contain("BarricadeActivationDeferred"));
            Assert.That(result.EventLogEntries[0], Does.Contain("Reason=UnitOccupant"));
            Assert.That(result.EventLogEntries[0], Does.Contain("BlockingUnitId=10"));
        }

        [Test]
        [Category("Core")]
        public void BarricadeActivation_DoesNotMutateUnitOccupancy()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var unit = CreatePlayerUnit(10, barricadeCell);
            var previousSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations, Is.Empty);
            Assert.That(currentSnapshot.TryGetEntity(10, out var afterUnit), Is.True);
            Assert.That(afterUnit.position, Is.EqualTo(barricadeCell));
            Assert.That(afterUnit.hp, Is.EqualTo(unit.hp));
            Assert.That(afterUnit.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(afterUnit.markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void BarricadeActivation_DeferredStateDoesNotEnterAuthoritativeStateOrHash()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var unit = CreatePlayerUnit(10, barricadeCell);
            var previousSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(result.EventLogEntries.Single(), Does.Contain("BarricadeActivationDeferred"));
            Assert.That(currentSnapshot.TryGetTileFeature(100, out var tileFeature), Is.True);
            Assert.That(tileFeature.Flags, Is.EqualTo(TileFeatureFlags.None));
            Assert.That(
                hashBuilder.Build(7, currentSnapshot, CreateTickResultData(currentSnapshot)),
                Is.EqualTo(hashBuilder.Build(
                    7,
                    currentSnapshot,
                    CreateTickResultData(currentSnapshot, eventLogEntries: Array.Empty<string>()))));
            Assert.That(
                hashBuilder.Build(7, currentSnapshot, CreateTickResultData(currentSnapshot, eventLogEntries: result.EventLogEntries)),
                Is.Not.EqualTo(hashBuilder.Build(7, currentSnapshot, CreateTickResultData(currentSnapshot))));
        }

        [Test]
        [Category("Core")]
        public void BarricadeActivation_Defer_WhenSameCellEnemyOccupantExists()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var enemy = CreateEnemyUnit(21, barricadeCell);
            var previousSnapshot = CreateWorldState(
                    new[] { enemy },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { enemy },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations, Is.Empty);
            Assert.That(result.TileEvents, Is.Empty);
            Assert.That(result.EventLogEntries.Single(), Does.Contain("BlockingUnitId=21"));
        }

        [Test]
        [Category("Core")]
        public void BarricadeActivation_UnitDeferPrecedesBoxCrush_WhenBothAreRepresentable()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var unit = CreatePlayerUnit(10, barricadeCell);
            var box = CreateBox(20, barricadeCell);
            var barricade = CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade);
            var previousSnapshot = CreateSnapshotWithSeparateUnitAndSolidOccupancy(
                unit,
                box,
                barricade,
                new CubeTopologyState(FaceId.Floor));
            var currentSnapshot = CreateSnapshotWithSeparateUnitAndSolidOccupancy(
                unit,
                box,
                barricade,
                new CubeTopologyState(FaceId.Front));

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations, Is.Empty);
            Assert.That(result.TileEvents, Is.Empty);
            Assert.That(result.EventLogEntries.Single(), Does.Contain("BlockingUnitId=10"));
        }

        [Test]
        [Category("Core")]
        public void BarricadeTransition_AfterSuppressingUnitLeaves_CrushesRemainingBox()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var box = CreateBox(20, barricadeCell);
            var barricade = CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade);
            var previousSnapshot = CreateSnapshotWithSeparateUnitAndSolidOccupancy(
                CreatePlayerUnit(10, barricadeCell),
                box,
                barricade,
                new CubeTopologyState(FaceId.Front));
            var currentSnapshot = CreateWorldState(
                    new[] { box },
                    new[] { barricade },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.TileEvents.Single().EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeCrushed));
            Assert.That(result.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(result.EntityOperations.Operations, Has.Count.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void BarricadeActivation_DoesNotSpamDeferredFactAcrossTicks()
        {
            var barricadeCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var unit = CreatePlayerUnit(10, barricadeCell);
            var previousSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();
            var currentSnapshot = CreateWorldState(
                    new[] { unit },
                    new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                    topology: new CubeTopologyState(FaceId.Front))
                .CreateSnapshot();

            var result = Resolve(
                currentSnapshot,
                previousSnapshot,
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None));

            Assert.That(result.EntityOperations.Operations, Is.Empty);
            Assert.That(result.TileEvents, Is.Empty);
            Assert.That(result.EventLogEntries, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void BarricadeTransition_BoxOnAlreadyTopologyActiveBarricade_NoTerminalSolidOccupancy()
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

            var activeToActive = Resolve(activeSnapshot, activeSnapshot, definition);

            Assert.That(activeToActive.TileEvents, Has.Count.EqualTo(1), "active to active crush event");
            Assert.That(activeToActive.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.BarricadeCrushed));
            Assert.That(activeToActive.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(activeToActive.EntityOperations.Operations, Has.Count.EqualTo(2));
            Assert.That(activeToActive.EntityOperations.Operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(activeToActive.EntityOperations.Operations[0].BoardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(activeToActive.EntityOperations.Operations[0].Metadata.BoundaryReason, Is.EqualTo("BarricadeCrush"));
            Assert.That(activeToActive.EntityOperations.Operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(activeToActive.EntityOperations.Operations[1].Metadata.ExitCauseHint, Is.EqualTo(TickEntityExitCause.BoxDestroy));
            Assert.That(Resolve(inactiveSnapshot, inactiveSnapshot, definition).IsEmpty, Is.True, "inactive to inactive");
            Assert.That(Resolve(inactiveSnapshot, activeSnapshot, definition).IsEmpty, Is.True, "active to inactive");
            Assert.That(Resolve(activeSnapshot, definition).IsEmpty, Is.False, "missing previous snapshot active invariant");
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
                new object[] { "NonBoxSolid", new[] { CreateSolid(23, barricadeCell) } },
                new object[] { "DeadBox", new[] { CreateBox(24, barricadeCell, hp: 0) } },
                new object[] { "DetachedBox", new[] { CreateBox(25, barricadeCell, boardPresence: EntityBoardPresence.Detached) } },
                new object[] { "MarkedBox", new[] { CreateBox(26, barricadeCell, markedForDeath: true) } },
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var name = (string)cases[i][0];
                var entities = (EntityState[])cases[i][1];
                var previousSnapshot = CreateWorldState(
                        entities,
                        new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                        new CubeTopologyState(FaceId.Floor))
                    .CreateSnapshot();
                var currentSnapshot = CreateWorldState(
                        entities,
                        new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
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
        public void ButtonLatch_Pipeline_FlipLandingActivatesButton()
        {
            var buttonCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 0, 0), boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                },
                new[] { CreateButton(100, buttonCell) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 0), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(buttonCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(finalSnapshot.TryGetTileFeature(100, out var buttonAfter), Is.True);
            Assert.That((buttonAfter.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
            Assert.That(
                result.PresentationData.TileEvents.Count(tileEvent => tileEvent.EventKind == TilePresentationEventKind.ButtonActivated),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void ButtonLatch_Pipeline_FlipImpactFollowThroughLandingActivatesButton()
        {
            var buttonCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var enemy = CreateEnemyUnit(30, buttonCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 0, 0), boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                    enemy,
                },
                new[] { CreateButton(100, buttonCell) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 0), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.FollowThroughAccepted, Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(buttonCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.TryGetTileFeature(100, out var buttonAfter), Is.True);
            Assert.That((buttonAfter.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
            Assert.That(
                result.PresentationData.TileEvents.Count(tileEvent => tileEvent.EventKind == TilePresentationEventKind.ButtonActivated),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void PushSlide_BoxFollowThroughOntoDestroyTileAfterEnemyDeath_AppliesDestroyTile()
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var slidingBox = CreateBox(
                20,
                new SurfaceCell(FaceId.Floor, 0, 0),
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, destroyCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);
            var nextTick = pipeline.RunTick(new TickInput(8));

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.PushLike));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.TargetDestroyed, Is.True);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(0,1)",
                    "Facing=Up"),
                Is.True);
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.DestroyTileTriggered));
            Assert.That(result.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=20"));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(nextTick.PresentationData.EntityMotions.Any(motion => motion.EntityId == 20), Is.False);
        }

        [Test]
        [Category("Core")]
        public void PushSlide_BoxBlockedByEnemyOnDestroyTile_WhenEnemySurvives_DoesNotApplyDestroyTile()
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var slidingBox = CreateBox(
                20,
                new SurfaceCell(FaceId.Floor, 0, 0),
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, destroyCell);
            enemy.hp = 3;
            enemy.maxHp = 3;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.Stay));
            Assert.That(disposition.TargetDestroyed, Is.False);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.False);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
            Assert.That(
                result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.DestroyTileTriggered),
                Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(finalSnapshot.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.position, Is.EqualTo(destroyCell));
            Assert.That(enemyAfter.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void Flip_BoxFollowThroughOntoDestroyTileAfterEnemyDeath_MatchesEmptyLanding()
        {
            var enemyLanding = RunFlipOntoDestroyTileScenario(includeEnemy: true);
            var emptyLanding = RunFlipOntoDestroyTileScenario(includeEnemy: false);

            Assert.That(enemyLanding.Disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(enemyLanding.Disposition.FollowThroughAccepted, Is.True);
            Assert.That(enemyLanding.FinalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(emptyLanding.FinalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(
                enemyLanding.Result.PresentationData.TileEvents.Select(tileEvent => tileEvent.EventKind).ToArray(),
                Is.EqualTo(emptyLanding.Result.PresentationData.TileEvents.Select(tileEvent => tileEvent.EventKind).ToArray()));
            Assert.That(
                enemyLanding.Result.PresentationData.TileEvents.Count(tileEvent => tileEvent.EventKind == TilePresentationEventKind.DestroyTileTriggered),
                Is.EqualTo(1));
            Assert.That(enemyLanding.Result.PresentationData.TileEvents[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(enemyLanding.Result.EventLog, Does.Contain("CleanupRemoved|E=20"));
            Assert.That(enemyLanding.Result.EventLog, Does.Contain("CleanupRemoved|E=30"));
        }

        [Test]
        [Category("Extended")]
        public void PushStart_EnemyOnSuppressedBarricade_WhenEnemyDies_DestroysEnemyAndBox()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var enemy = CreateEnemyUnit(30, barricadeCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                    enemy,
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
            var finalSnapshot = worldState.CreateSnapshot();

            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.BarricadeReassertCrush));
            Assert.That(disposition.BarricadeTileId, Is.EqualTo(100));
            Assert.That(disposition.BarricadeCell, Is.EqualTo(barricadeCell));
            Assert.That(disposition.AllTargetsDestroyed, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("Reason=BarricadeReassertCrush"));
            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.None.Contains("BoxSlideBlockedByBarricade"));
            var crushedEvent = result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed);
            Assert.That(crushedEvent.TileId, Is.EqualTo(100));
            Assert.That(crushedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.HasAnyUnitAt(barricadeCell), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PushStart_EnemyOnSuppressedBarricade_WhenEnemySurvives_DamagesEnemyAndStopsBox()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var enemy = CreateEnemyUnit(30, barricadeCell);
            enemy.hp = 2;
            enemy.maxHp = 2;
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                    enemy,
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
            var finalSnapshot = worldState.CreateSnapshot();
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.Stay));
            Assert.That(disposition.AllTargetsDestroyed, Is.False);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.False);
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated"));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(finalSnapshot.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.position, Is.EqualTo(barricadeCell));
            Assert.That(enemyAfter.hp, Is.EqualTo(1));
            Assert.That(enemyAfter.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(enemyAfter.markedForDeath, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SlidingContinuation_EnemyOnSuppressedBarricade_WhenEnemyDies_RemovesBoxFromSourceAndDestination()
        {
            var firstRun = RunSlidingBlockedByEnemyOnBarricadeScenario(enemyHp: 1);
            var secondRun = RunSlidingBlockedByEnemyOnBarricadeScenario(enemyHp: 1);
            var result = firstRun.Result;
            var finalSnapshot = firstRun.FinalSnapshot;
            var barricadeCell = new SurfaceCell(FaceId.Front, 0, 1);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.BarricadeReassertCrush));
            Assert.That(disposition.BarricadeTileId, Is.EqualTo(100));
            Assert.That(disposition.BarricadeCell, Is.EqualTo(barricadeCell));
            Assert.That(disposition.AllTargetsDestroyed, Is.True);
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("Reason=BarricadeReassertCrush"));
            AssertBoxReassertCrushRemovalOps(
                result.MovementPhaseResult.ResolvedOperations,
                boxEntityId: 20,
                barricadeCell);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.None.Contains("BoxSlideBlockedByBarricade"));
            var crushedEvent = result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed);
            Assert.That(crushedEvent.TileId, Is.EqualTo(100));
            Assert.That(crushedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.HasAnyUnitAt(barricadeCell), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(new SurfaceCell(FaceId.Front, 0, 0), out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
            var hashBuilder = new DeterminismHashBuilder();
            var firstHash = hashBuilder.Build(7, firstRun.FinalSnapshot, CreateTickResultData(firstRun.FinalSnapshot));
            var secondHash = hashBuilder.Build(7, secondRun.FinalSnapshot, CreateTickResultData(secondRun.FinalSnapshot));
            Assert.That(firstHash, Is.Not.Empty);
            Assert.That(secondHash, Is.EqualTo(firstHash));
        }

        [Test]
        [Category("Extended")]
        public void SlidingContinuation_EnemyOnSuppressedBarricade_WhenEnemySurvives_StopsBoxButDoesNotCrush()
        {
            var run = RunSlidingBlockedByEnemyOnBarricadeScenario(enemyHp: 2);
            var result = run.Result;
            var finalSnapshot = run.FinalSnapshot;
            var barricadeCell = new SurfaceCell(FaceId.Front, 0, 1);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.Stay));
            Assert.That(disposition.AllTargetsDestroyed, Is.False);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.False);
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated"));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(finalSnapshot.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.position, Is.EqualTo(barricadeCell));
            Assert.That(enemyAfter.hp, Is.EqualTo(1));
            Assert.That(enemyAfter.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(enemyAfter.markedForDeath, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void BottomToFrontSlide_EnemyOnSuppressedFrontBarricade_WhenEnemyDies_RemovesBoxFromSourceAndDestination()
        {
            var result = RunBottomToFrontSlidingBlockedByEnemyOnBarricadeScenario(enemyHp: 1);
            var finalSnapshot = result.FinalSnapshot;
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var disposition = result.Result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.PushLike));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.BarricadeReassertCrush));
            Assert.That(disposition.BarricadeTileId, Is.EqualTo(100));
            Assert.That(disposition.BarricadeCell, Is.EqualTo(barricadeCell));
            Assert.That(disposition.AllTargetsDestroyed, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
            Assert.That(result.Result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated").And.Contains("At=Front(2,0)"));
            Assert.That(result.Result.MovementPhaseResult.CommitEvents, Has.Some.Contains("Reason=BarricadeReassertCrush"));
            AssertBoxReassertCrushRemovalOps(
                result.Result.MovementPhaseResult.ResolvedOperations,
                boxEntityId: 20,
                barricadeCell);
            Assert.That(result.Result.MovementPhaseResult.RejectedReasons, Has.None.Contains("BoxSlideBlockedByBarricade"));
            var crushedEvent = result.Result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed);
            Assert.That(crushedEvent.TileId, Is.EqualTo(100));
            Assert.That(crushedEvent.Cell, Is.EqualTo(barricadeCell));
            Assert.That(crushedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(result.Result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.HasAnyUnitAt(barricadeCell), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Scenario_BottomToFrontSlide_EnemyOnSuppressedFrontBarricade_WhenEnemyDies_SourceCellHasNoBoxAfterCleanup()
        {
            var run = RunBottomToFrontSlidingBlockedByEnemyOnBarricadeScenario(enemyHp: 1);
            var finalSnapshot = run.FinalSnapshot;
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var disposition = run.Result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            WriteBottomToFrontScenarioTrace(run, sourceCell, barricadeCell);
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.BarricadeReassertCrush));
            Assert.That(
                run.Result.MovementPhaseResult.ImpactDispositionRecords.Any(
                    record => record.ImpactSourceEntityId == 20 &&
                              record.DispositionKind == ImpactDispositionKind.Stay),
                Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(
                run.Result.MovementPhaseResult.ResolvedOperations.Any(
                    operation => operation.EntityId == 20 &&
                                 operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                                 operation.BoardPresence == EntityBoardPresence.Detached),
                Is.True);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Scenario_BottomToFrontSlide_EnemyOnSuppressedFrontBarricade_WhenEnemySurvives_StopsBoxButDoesNotCrush()
        {
            var run = RunBottomToFrontSlidingBlockedByEnemyOnBarricadeScenario(enemyHp: 2);
            var finalSnapshot = run.FinalSnapshot;
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var disposition = run.Result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.Stay));
            Assert.That(disposition.AllTargetsDestroyed, Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(sourceCell));
            Assert.That(boxAfter.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out var sourceSolid), Is.True);
            Assert.That(sourceSolid.Entity.entityId, Is.EqualTo(20));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
            Assert.That(run.Result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(run.Result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
        }

        [Test]
        [Category("Core")]
        public void BottomToFrontSlide_IntoFrontBarricade_NoEnemy_BlocksBeforeImpact()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var slidingBox = CreateBox(
                20,
                sourceCell,
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var worldState = CreateWorldState(
                new[] { slidingBox },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                topology: new CubeTopologyState(FaceId.Floor));
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=BoxSlideBlockedByBarricade").And.Contains("MovementKind=SlidingContinuation"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("ImpactReservationCreated"));
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            var blockedEvent = result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked);
            Assert.That(blockedEvent.TileId, Is.EqualTo(100));
            Assert.That(blockedEvent.Cell, Is.EqualTo(barricadeCell));
            Assert.That(blockedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(sourceCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out var sourceSolid), Is.True);
            Assert.That(sourceSolid.Entity.entityId, Is.EqualTo(20));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void BottomToFrontSlide_IntoInactiveFrontBarricade_AllowsNormalSlide()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var slidingBox = CreateBox(
                20,
                sourceCell,
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            var worldState = CreateWorldState(
                new[] { slidingBox },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                topology: new CubeTopologyState(FaceId.Floor));
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("ImpactReservationCreated"));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(barricadeCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out var destinationSolid), Is.True);
            Assert.That(destinationSolid.Entity.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void FrontToBottomSlide_BarricadeDestinationPolicy_IsNotApplicableForActivePolicy()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var slidingBox = CreateBox(
                20,
                sourceCell,
                state: EntityPhaseState.Sliding,
                facing: Direction.Down);
            slidingBox.stateTimer = 0;
            var worldState = CreateWorldState(
                new[] { slidingBox },
                new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) },
                topology: new CubeTopologyState(FaceId.Front));
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(result.PresentationData.TileEvents, Is.Empty, "Front-to-Bottom active Barricade policy is NOT_APPLICABLE because Barricade activation is FrontFaceOnly and the destination is Bottom/Floor.");
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(sourceCell));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out var sourceSolid), Is.True);
            Assert.That(sourceSolid.Entity.entityId, Is.EqualTo(20));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(destinationCell, out _), Is.False);
        }

        private static void WriteBottomToFrontScenarioTrace(
            PipelineScenarioRunWithoutDisposition run,
            SurfaceCell sourceCell,
            SurfaceCell barricadeCell)
        {
            var finalSnapshot = run.FinalSnapshot;

            TestContext.WriteLine("BottomToFrontTrace.Dispositions");
            foreach (var record in run.Result.MovementPhaseResult.ImpactDispositionRecords)
            {
                TestContext.WriteLine(
                    $"  Source={record.ImpactSourceEntityId}|Target={record.ImpactTargetEntityId}|Kind={record.DispositionKind}|Policy={record.PolicyKind}|AllTargetsDestroyed={record.AllTargetsDestroyed}|BarricadeTile={record.BarricadeTileId}|BarricadeCell={record.BarricadeCell}|FollowThroughAccepted={record.FollowThroughAccepted}");
            }

            TestContext.WriteLine("BottomToFrontTrace.MovementDebugEvents");
            foreach (var entry in run.Result.MovementPhaseResult.DebugEvents)
            {
                TestContext.WriteLine($"  {entry}");
            }

            TestContext.WriteLine("BottomToFrontTrace.MovementCommitEvents");
            foreach (var entry in run.Result.MovementPhaseResult.CommitEvents)
            {
                TestContext.WriteLine($"  {entry}");
            }

            TestContext.WriteLine("BottomToFrontTrace.MovementRejectedReasons");
            foreach (var entry in run.Result.MovementPhaseResult.RejectedReasons)
            {
                TestContext.WriteLine($"  {entry}");
            }

            TestContext.WriteLine("BottomToFrontTrace.BoxOperations");
            foreach (var operation in run.Result.MovementPhaseResult.ResolvedOperations.Where(operation => operation.EntityId == 20))
            {
                TestContext.WriteLine(
                    $"  Seq={operation.Sequence}|Kind={operation.Kind}|BoardPresence={operation.BoardPresence}|Destination={operation.Destination}|ExitCause={operation.Metadata.ExitCauseHint}|DamageSource={operation.Metadata.DamageSourceType}|Boundary={operation.Metadata.BoundaryReason}|PresentationTarget={operation.Metadata.PresentationTargetCell}");
            }

            TestContext.WriteLine("BottomToFrontTrace.EnemyOperations");
            foreach (var operation in run.Result.AttackPhaseResult.ResolvedOperations.Where(operation => operation.EntityId == 30))
            {
                TestContext.WriteLine(
                    $"  Seq={operation.Sequence}|Kind={operation.Kind}|BoardPresence={operation.BoardPresence}|ExitCause={operation.Metadata.ExitCauseHint}|DamageSource={operation.Metadata.DamageSourceType}|Boundary={operation.Metadata.BoundaryReason}");
            }

            TestContext.WriteLine("BottomToFrontTrace.Damage");
            foreach (var record in run.Result.AttackPhaseResult.DamageResolutions)
            {
                TestContext.WriteLine(
                    $"  Target={record.TargetId}|Amount={record.Amount}|Accepted={record.Accepted}|Reject={record.RejectReason}");
            }

            TestContext.WriteLine("BottomToFrontTrace.Cleanup");
            foreach (var entry in run.Result.EventLog.Where(entry => entry.StartsWith("CleanupRemoved|", StringComparison.Ordinal)))
            {
                TestContext.WriteLine($"  {entry}");
            }

            TestContext.WriteLine("BottomToFrontTrace.Presentation");
            foreach (var tileEvent in run.Result.PresentationData.TileEvents)
            {
                TestContext.WriteLine(
                    $"  TileEvent={tileEvent.EventKind}|Tile={tileEvent.TileId}|Cell={tileEvent.Cell}|Target={tileEvent.TargetEntityId}");
            }

            foreach (var exitSignal in run.Result.PresentationData.EntityExitSignals)
            {
                TestContext.WriteLine(
                    $"  ExitSignal=E{exitSignal.ExitedEntityId}|Cause={exitSignal.ExitCause}|Source={exitSignal.SourceCell}|Target={exitSignal.PresentationTargetCell}|Timing={exitSignal.Timing}");
            }

            TestContext.WriteLine("BottomToFrontTrace.Final");
            if (finalSnapshot.TryGetEntity(20, out var boxAfter))
            {
                TestContext.WriteLine(
                    $"  Box=exists|Cell={boxAfter.position}|Presence={boxAfter.boardPresence}|State={boxAfter.state}|Timer={boxAfter.stateTimer}|Facing={boxAfter.facing}|Hp={boxAfter.hp}|Marked={boxAfter.markedForDeath}");
            }
            else
            {
                TestContext.WriteLine("  Box=missing");
            }

            if (finalSnapshot.TryGetEntity(30, out var enemyAfter))
            {
                TestContext.WriteLine(
                    $"  Enemy=exists|Cell={enemyAfter.position}|Presence={enemyAfter.boardPresence}|Hp={enemyAfter.hp}|Marked={enemyAfter.markedForDeath}");
            }
            else
            {
                TestContext.WriteLine("  Enemy=missing");
            }

            TestContext.WriteLine(
                finalSnapshot.TryGetSolidSemanticAt(sourceCell, out var sourceSolid)
                    ? $"  SourceSolid=E{sourceSolid.Entity.entityId}"
                    : "  SourceSolid=none");
            TestContext.WriteLine(
                finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out var barricadeSolid)
                    ? $"  DestinationSolid=E{barricadeSolid.Entity.entityId}"
                    : "  DestinationSolid=none");
            TestContext.WriteLine(
                finalSnapshot.HasAnyUnitAt(barricadeCell)
                    ? "  DestinationUnit=present"
                    : "  DestinationUnit=none");
        }

        [Test]
        [Category("Extended")]
        public void PushDestroyStart_EnemyOnSuppressedBarricade_PrioritizesImpactAndReassertCrush()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, 0);
            var enemy = CreateEnemyUnit(30, barricadeCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0), boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
                    enemy,
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
            var finalSnapshot = worldState.CreateSnapshot();

            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.BarricadeReassertCrush));
            Assert.That(disposition.BarricadeTileId, Is.EqualTo(100));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.Some.Contains("Reason=BarricadeReassertCrush"));
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            var crushedEvent = result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed);
            Assert.That(crushedEvent.TileId, Is.EqualTo(100));
            Assert.That(crushedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=20"));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.HasAnyUnitAt(barricadeCell), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void PushSlide_BoxFollowThroughOntoButtonAfterEnemyDeath_MatchesSlidingPass_NoLatch()
        {
            var buttonCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var slidingBox = CreateBox(
                20,
                new SurfaceCell(FaceId.Floor, 0, 0),
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, buttonCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateButton(100, buttonCell) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.FollowThroughAccepted, Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(buttonCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(finalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(finalSnapshot.TryGetTileFeature(100, out var buttonAfter), Is.True);
            Assert.That(buttonAfter.Flags, Is.EqualTo(TileFeatureFlags.None));
            Assert.That(
                result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.ButtonActivated),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void PushSlide_BoxFollowThroughOntoSlideTileAfterEnemyDeath_RedirectsFacing()
        {
            var slideCell = new SurfaceCell(FaceId.Front, 1, 0);
            var slidingBox = CreateBox(
                20,
                new SurfaceCell(FaceId.Front, 0, 0),
                state: EntityPhaseState.Sliding,
                facing: Direction.Right);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, slideCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateTileFeature(100, slideCell, TileFeatureKind.Slide) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.FollowThroughAccepted, Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(slideCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(boxAfter.facing, Is.EqualTo(Direction.Up));
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents[0].EventKind, Is.EqualTo(TilePresentationEventKind.SlideTileRedirected));
            Assert.That(result.PresentationData.TileEvents[0].Direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Core")]
        public void Flip_BoxImpactOnSlideTile_WhenEnemyDies_DoesNotRedirect()
        {
            var slideCell = new SurfaceCell(FaceId.Front, 2, 0);
            var enemy = CreateEnemyUnit(30, slideCell);
            enemy.hp = 1;
            enemy.maxHp = 1;
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Front, 1, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 0, 0), boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                    enemy,
                },
                new[] { CreateTileFeature(100, slideCell, TileFeatureKind.Slide) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, direction: Direction2D.Up, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 0), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.FollowThroughAccepted, Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(slideCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.EventKind == TilePresentationEventKind.SlideTileRedirected), Is.False);
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
                new[] { box, CreateSolid(21, new SurfaceCell(FaceId.Floor, 2, 1)) },
                new[] { button });
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

        private static TileEffectResolutionResult ResolveEntityContacts(
            WorldSnapshot snapshot,
            IReadOnlyList<TileEffectEntityContact> contacts,
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

        private static TileEffectResolutionResult ResolveWithActivationFacts(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureActivationOccupantFact> facts,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return TileFeatureEffectResolver.Instance.Resolve(
                new TileEffectResolutionContext(
                    7,
                    snapshot,
                    definitions,
                    activationOccupantFacts: facts));
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
            IReadOnlyList<TileFeatureActivationOccupantFact> facts,
            params TileFeatureRuntimeDefinition[] definitions)
        {
            return TileFeatureEffectResolver.Instance.Resolve(
                new TileEffectResolutionContext(
                    7,
                    snapshot,
                    definitions,
                    contacts,
                    previousSnapshot,
                    activationOccupantFacts: facts));
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

        private static TileFeatureActivationOccupantFact CreateActivationFact(
            int tileId,
            SurfaceCell cell,
            int occupantEntityId)
        {
            return new TileFeatureActivationOccupantFact(
                tileId,
                cell,
                TileFeatureKind.Destroy,
                occupantEntityId,
                EntityType.Box,
                TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant,
                sourceOperationOrdinal: 0);
        }

        private static TileEffectBoxStop CreateStop(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxMovementFamily movementFamily,
            MovementSemanticKind movementSemanticKind,
            int actionPlanId,
            int localActionIndex)
        {
            return new TileEffectBoxStop(
                boxEntityId,
                cell,
                movementFamily,
                TileEffectBoxStopCause.Create(
                    boxEntityId,
                    cell,
                    movementFamily,
                    movementSemanticKind,
                    actionPlanId,
                    localActionIndex,
                    intentId: 0,
                    visualContactNormalizedTime: movementSemanticKind == MovementSemanticKind.Flip
                        ? GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime
                        : 0f));
        }

        private static FinalizationOperationMetadata CreateMovementMetadata(
            MovementSemanticKind movementSemanticKind,
            int localActionIndex = 0,
            int actionPlanId = 1,
            int intentId = 0,
            MovementExecutionBoundaryKind movementExecutionBoundaryKind = MovementExecutionBoundaryKind.Unknown)
        {
            var semanticKind = movementSemanticKind switch
            {
                MovementSemanticKind.Push => ResolvedActionSemanticKind.Push,
                MovementSemanticKind.Slide => ResolvedActionSemanticKind.Slide,
                MovementSemanticKind.Flip => ResolvedActionSemanticKind.Flip,
                MovementSemanticKind.Stop => ResolvedActionSemanticKind.Stop,
                MovementSemanticKind.JumpLanding => ResolvedActionSemanticKind.JumpLanding,
                _ => ResolvedActionSemanticKind.Move,
            };
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                semanticKind,
                sourceActorEntityId: 10,
                actionPlanId: actionPlanId,
                intentId: intentId,
                localActionIndex: localActionIndex,
                movementSemanticKind: movementSemanticKind,
                movementExecutionBoundaryKind: movementExecutionBoundaryKind);
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

            var result = pipeline.RunTick(new TickInput(7));
            var snapshot = worldState.CreateSnapshot();
            return new DeterminismHashBuilder().Build(7, snapshot, CreateTickResultData(snapshot, result.PresentationData, result.EventLog));
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

            var result = pipeline.RunTick(new TickInput(7, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();
            return new DeterminismHashBuilder().Build(7, snapshot, CreateTickResultData(snapshot, result.PresentationData, result.EventLog));
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
                Array.Empty<EntityState>(),
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

        private static PipelineScenarioRun RunFlipOntoDestroyTileScenario(bool includeEnemy)
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var entities = new List<EntityState>
            {
                CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 0, 0), boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
            };
            if (includeEnemy)
            {
                var enemy = CreateEnemyUnit(30, destroyCell);
                enemy.hp = 1;
                enemy.maxHp = 1;
                entities.Add(enemy);
            }

            var worldState = CreateWorldState(
                entities,
                new[] { CreateTileFeature(100, destroyCell, TileFeatureKind.Destroy) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 0), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords
                .FirstOrDefault(record => record.ImpactSourceEntityId == 20);
            return new PipelineScenarioRun(result, worldState.CreateSnapshot(), disposition);
        }

        private static PipelineScenarioRunWithoutDisposition RunSlidingBlockedByEnemyOnBarricadeScenario(int enemyHp)
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 0, 1);
            var slidingBox = CreateBox(
                20,
                new SurfaceCell(FaceId.Front, 0, 0),
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, barricadeCell);
            enemy.hp = enemyHp;
            enemy.maxHp = enemyHp;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            return new PipelineScenarioRunWithoutDisposition(result, worldState.CreateSnapshot());
        }

        private static PipelineScenarioRunWithoutDisposition RunBottomToFrontSlidingBlockedByEnemyOnBarricadeScenario(int enemyHp)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var slidingBox = CreateBox(
                20,
                sourceCell,
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var enemy = CreateEnemyUnit(30, barricadeCell);
            enemy.hp = enemyHp;
            enemy.maxHp = enemyHp;
            var worldState = CreateWorldState(
                new[] { slidingBox, enemy },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) },
                topology: new CubeTopologyState(FaceId.Floor));
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            return new PipelineScenarioRunWithoutDisposition(result, worldState.CreateSnapshot());
        }

        private static PipelineScenarioRunWithoutDisposition RunFlipBlockedByEnemyOnBarricadeScenario(int enemyHp)
        {
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            var enemy = CreateEnemyUnit(30, landingCell);
            enemy.hp = enemyHp;
            enemy.maxHp = enemyHp;
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Front, 1, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 0, 1), boxCapabilities: BoxCapabilities.Flip),
                    enemy,
                },
                new[] { CreateTileFeature(100, landingCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 1), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            return new PipelineScenarioRunWithoutDisposition(result, worldState.CreateSnapshot());
        }

        private static void AssertBoxReassertCrushRemovalOps(
            IReadOnlyList<FinalizationOperation> operations,
            int boxEntityId,
            SurfaceCell barricadeCell)
        {
            var boxOperations = operations
                .Where(operation => operation.EntityId == boxEntityId)
                .ToArray();
            var hasDetached = boxOperations.Any(
                operation => operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                             operation.BoardPresence == EntityBoardPresence.Detached &&
                             operation.Metadata.BoundaryReason == "BarricadeCrush" &&
                             operation.Metadata.PresentationTargetCell == barricadeCell);
            var hasDestroy = boxOperations.Any(
                operation => operation.Kind == FinalizationOperationKind.MarkDestroy &&
                             operation.Metadata.BoundaryReason == "BarricadeCrush" &&
                             operation.Metadata.PresentationTargetCell == barricadeCell);

            Assert.That(hasDetached, Is.True);
            Assert.That(hasDestroy, Is.True);
            Assert.That(
                boxOperations.Any(operation => operation.Kind != FinalizationOperationKind.ApplyStateChange),
                Is.True);
            Assert.That(
                operations.Any(
                    operation => operation.EntityId == 30 &&
                                 operation.Metadata.BoundaryReason == "BarricadeCrush"),
                Is.False);
        }

        private readonly struct PipelineScenarioRunWithoutDisposition
        {
            public PipelineScenarioRunWithoutDisposition(
                TickResult result,
                WorldSnapshot finalSnapshot)
            {
                Result = result;
                FinalSnapshot = finalSnapshot;
            }

            public TickResult Result { get; }

            public WorldSnapshot FinalSnapshot { get; }
        }

        private readonly struct PipelineScenarioRun
        {
            public PipelineScenarioRun(
                TickResult result,
                WorldSnapshot finalSnapshot,
                ImpactDispositionResolutionRecord disposition)
            {
                Result = result;
                FinalSnapshot = finalSnapshot;
                Disposition = disposition;
            }

            public TickResult Result { get; }

            public WorldSnapshot FinalSnapshot { get; }

            public ImpactDispositionResolutionRecord Disposition { get; }
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
                unitKinematicLocomotionTiming: default,
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
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(5));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(10));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(11));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(11));
        }

        private static TickResultData CreateTickResultData(
            WorldSnapshot snapshot,
            TickPresentationData presentationData = null,
            IReadOnlyList<string> eventLogEntries = null)
        {
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);
            return new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLogEntries ?? Array.Empty<string>(),
                presentationData ?? TickPresentationData.Empty);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TileFeatureState> initialTileFeatures,
            CubeTopologyState? topology = null)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                TestBounds,
                topology ?? new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
        }

        private static WorldSnapshot CreateSnapshotWithSeparateUnitAndSolidOccupancy(
            EntityState unit,
            EntityState solid,
            TileFeatureState tileFeature,
            CubeTopologyState topology)
        {
            return new WorldSnapshot(
                new Dictionary<int, EntityState>
                {
                    { unit.entityId, unit },
                    { solid.entityId, solid },
                },
                new Dictionary<SurfaceCell, SortedSet<int>>
                {
                    { unit.position, new SortedSet<int> { unit.entityId } },
                },
                new Dictionary<SurfaceCell, int>
                {
                    { solid.position, solid.entityId },
                },
                new Dictionary<int, TileFeatureState>
                {
                    { tileFeature.TileId, tileFeature },
                },
                new Dictionary<SurfaceCell, SortedSet<int>>
                {
                    { tileFeature.Cell, new SortedSet<int> { tileFeature.TileId } },
                },
                new Dictionary<int, EnemyActionRuntimeState>(),
                new Dictionary<int, PendingCellImpact>(),
                new Dictionary<int, PendingEnemyBlockedReaction>(),
                new Dictionary<int, EnemyPatrolRuntimeState>(),
                new Dictionary<int, EnemyChargeRuntimeState>(),
                new Dictionary<int, EntityExecutionLockState>(),
                new Dictionary<int, EnemyJumpRuntimeState>(),
                new Dictionary<int, EnemyGlideRuntimeState>(),
                new Dictionary<int, EnemyUtilityRuntimeState>(),
                new Dictionary<int, EnemySummonBehaviorRuntimeState>(),
                new Dictionary<int, BoxInteractionLockState>(),
                new Dictionary<int, EnemyGravityFieldAuraFieldState>(),
                new Dictionary<int, PhasedRuntimeState>(),
                new Dictionary<int, PlayerDamageState>(),
                new Dictionary<int, PlayerControlState>(),
                new Dictionary<int, SummonedEntityState>(),
                new Dictionary<int, EnemyDefinitionBindingState>(),
                new Dictionary<int, UnitKinematicRuntimeState>(),
                new Dictionary<int, UnitContinuousLocomotionState>(),
                topology,
                0,
                TestBounds);
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

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitMobilityKind = unitMobilityKind,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
        {
            var unit = CreateUnit(entityId, position);
            unit.unitRole = UnitRole.Player;
            return unit;
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            var unit = CreateUnit(entityId, position);
            unit.teamId = 2;
            unit.unitRole = UnitRole.Enemy;
            return unit;
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

        private sealed class AttackIfPresentLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _sourceId;
            private readonly int _targetId;

            public AttackIfPresentLogic(int sourceId, int targetId)
            {
                _sourceId = sourceId;
                _targetId = targetId;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (snapshot.TryGetEntity(_sourceId, out var source) &&
                    source.boardPresence == EntityBoardPresence.Occupying &&
                    source.hp > 0 &&
                    !source.markedForDeath)
                {
                    buffer.Add(new RawAttackIntent(_sourceId, priority: 100, _targetId));
                }
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
