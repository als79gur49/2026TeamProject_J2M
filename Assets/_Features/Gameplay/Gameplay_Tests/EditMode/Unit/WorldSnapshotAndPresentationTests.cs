using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Resolution;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class WorldSnapshotAndPresentationTests
    {
        [Test]
        [Category("Extended")]
        public void TickPresentationData_EmptyAndLegacyConstructorsExposeEmptyFrontFaceShieldSignals()
        {
            Assert.That(TickPresentationData.Empty.FrontFaceShieldSources, Is.Empty);
            Assert.That(TickPresentationData.Empty.FrontFaceShieldBlocks, Is.Empty);
            Assert.That(TickPresentationData.Empty.SummonWindupWarnings, Is.Empty);
            Assert.That(TickPresentationData.Empty.FrontFaceShieldWindupWarnings, Is.Empty);
            Assert.That(TickPresentationData.Empty.TileEvents, Is.Empty);
            Assert.That(TickPresentationData.Empty.GravityFieldVisualStates, Is.Empty);

            var presentationData = new TickPresentationData(Array.Empty<TickEntityMotion>());

            Assert.That(presentationData.FrontFaceShieldSources, Is.Empty);
            Assert.That(presentationData.FrontFaceShieldBlocks, Is.Empty);
            Assert.That(presentationData.SummonWindupWarnings, Is.Empty);
            Assert.That(presentationData.FrontFaceShieldWindupWarnings, Is.Empty);
            Assert.That(presentationData.TileEvents, Is.Empty);
            Assert.That(presentationData.GravityFieldVisualStates, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_GravityFieldVisualStates_UsesFinalAuthoritativeEmitterState()
        {
            var chargingCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var activeCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateGravityFieldBox(40, activeCell, GravityFieldPhase.Charging, timerTicks: 10),
                CreateEntity(20, EntityType.Box, new SurfaceCell(FaceId.Floor, 2, 0), Direction.None),
                CreateGravityFieldBox(30, chargingCell, GravityFieldPhase.Charging, timerTicks: 10),
            });
            var writeContext = CreateWriteContext(worldState);
            writeContext.SetGravityFieldState(30, GravityFieldPhase.Charging, timerTicks: 6);
            writeContext.SetGravityFieldState(40, GravityFieldPhase.Active, timerTicks: 2);
            var finalSnapshot = CreateSnapshot(worldState);

            var presentationData = BuildPresentationDataForFinalSnapshot(
                finalSnapshot,
                gravityFieldChargeDurationTicks: 10,
                gravityFieldActiveDurationTicks: 5);

            Assert.That(
                presentationData.GravityFieldVisualStates.Select(state => state.EmitterEntityId).ToArray(),
                Is.EqualTo(new[] { 30, 40 }));

            var charging = presentationData.GravityFieldVisualStates[0];
            Assert.That(charging.Cell, Is.EqualTo(chargingCell));
            Assert.That(charging.Phase, Is.EqualTo(GravityFieldPhase.Charging));
            Assert.That(charging.TimerTicks, Is.EqualTo(6));
            Assert.That(charging.DurationTicks, Is.EqualTo(10));
            Assert.That(charging.Progress01, Is.EqualTo(0.4f).Within(0.0001f));

            var active = presentationData.GravityFieldVisualStates[1];
            Assert.That(active.Cell, Is.EqualTo(activeCell));
            Assert.That(active.Phase, Is.EqualTo(GravityFieldPhase.Active));
            Assert.That(active.TimerTicks, Is.EqualTo(2));
            Assert.That(active.DurationTicks, Is.EqualTo(5));
            Assert.That(active.Progress01, Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_GravityFieldVisualStates_ExcludesIneligibleEmitters()
        {
            var dead = CreateGravityFieldBox(30, new SurfaceCell(FaceId.Floor, 0, 0), GravityFieldPhase.Active, timerTicks: 1);
            dead.hp = 0;
            var detached = CreateGravityFieldBox(31, new SurfaceCell(FaceId.Floor, 1, 0), GravityFieldPhase.Active, timerTicks: 1);
            detached.boardPresence = EntityBoardPresence.Detached;
            var marked = CreateGravityFieldBox(32, new SurfaceCell(FaceId.Floor, 2, 0), GravityFieldPhase.Active, timerTicks: 1);
            marked.markedForDeath = true;
            var finalSnapshot = CreateSnapshot(CreateWorldState(new[]
            {
                dead,
                detached,
                marked,
                CreateEntity(33, EntityType.Box, new SurfaceCell(FaceId.Floor, 3, 0), Direction.None),
            }));

            var presentationData = BuildPresentationDataForFinalSnapshot(finalSnapshot);

            Assert.That(presentationData.GravityFieldVisualStates, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_GravityFieldVisualStates_ClampsNegativeTimerProgress()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateGravityFieldBox(30, new SurfaceCell(FaceId.Floor, 0, 0), GravityFieldPhase.Charging, timerTicks: 10),
            });
            CreateWriteContext(worldState).SetGravityFieldState(30, GravityFieldPhase.Active, timerTicks: -2);
            var finalSnapshot = CreateSnapshot(worldState);

            var presentationData = BuildPresentationDataForFinalSnapshot(
                finalSnapshot,
                gravityFieldChargeDurationTicks: 10,
                gravityFieldActiveDurationTicks: 5);

            Assert.That(presentationData.GravityFieldVisualStates, Has.Count.EqualTo(1));
            Assert.That(presentationData.GravityFieldVisualStates[0].DurationTicks, Is.EqualTo(5));
            Assert.That(presentationData.GravityFieldVisualStates[0].TimerTicks, Is.Zero);
            Assert.That(presentationData.GravityFieldVisualStates[0].Progress01, Is.EqualTo(1f));
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_GravityFieldVisualStates_ActiveIncludesDeterministicThreeByThreeArea()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateGravityFieldBox(30, new SurfaceCell(FaceId.Floor, 1, 1), GravityFieldPhase.Active, timerTicks: 2),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                GameplayTerrainData.Empty);
            var presentationData = BuildPresentationDataForFinalSnapshot(CreateSnapshot(worldState));

            Assert.That(presentationData.GravityFieldVisualStates, Has.Count.EqualTo(1));
            var state = presentationData.GravityFieldVisualStates[0];
            Assert.That(
                state.AreaCells.ToArray(),
                Is.EqualTo(new[]
                {
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new SurfaceCell(FaceId.Floor, 2, 1),
                    new SurfaceCell(FaceId.Floor, 0, 2),
                    new SurfaceCell(FaceId.Floor, 1, 2),
                    new SurfaceCell(FaceId.Floor, 2, 2),
                }));
            Assert.That(state.AreaFootprint.SlotVisibilityMask, Is.EqualTo(0x1FF));
            Assert.That(state.AreaFootprint.IsSlotVisible(4), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_GravityFieldVisualStates_BoundedEdgeExcludesOutOfBoundsArea()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateGravityFieldBox(30, new SurfaceCell(FaceId.Floor, 0, 0), GravityFieldPhase.Active, timerTicks: 2),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var presentationData = BuildPresentationDataForFinalSnapshot(CreateSnapshot(worldState));

            var state = presentationData.GravityFieldVisualStates.Single();
            Assert.That(
                state.AreaCells.ToArray(),
                Is.EqualTo(new[]
                {
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    new SurfaceCell(FaceId.Floor, 1, 1),
                }));
            Assert.That(state.AreaFootprint.IsSlotVisible(4), Is.True);
            Assert.That(state.AreaFootprint.IsSlotVisible(5), Is.True);
            Assert.That(state.AreaFootprint.IsSlotVisible(7), Is.True);
            Assert.That(state.AreaFootprint.IsSlotVisible(8), Is.True);
            Assert.That(state.AreaFootprint.IsSlotVisible(0), Is.False);
            Assert.That(state.AreaFootprint.IsSlotVisible(3), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_GravityFieldVisualStates_ChargingHasEmptyArea()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateGravityFieldBox(30, new SurfaceCell(FaceId.Floor, 1, 1), GravityFieldPhase.Charging, timerTicks: 2),
            });
            var presentationData = BuildPresentationDataForFinalSnapshot(CreateSnapshot(worldState));

            var state = presentationData.GravityFieldVisualStates.Single();
            Assert.That(state.AreaCells, Is.Empty);
            Assert.That(state.AreaFootprint.SlotVisibilityMask, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void GravityFieldAreaFootprint_DefensivelyCopiesAreaCells()
        {
            var cells = new List<SurfaceCell>
            {
                new SurfaceCell(FaceId.Floor, 1, 1),
            };
            var footprint = new GravityFieldAreaFootprint(cells, slotVisibilityMask: 1 << 4);
            cells.Add(new SurfaceCell(FaceId.Floor, 2, 2));

            Assert.That(footprint.AreaCells, Has.Count.EqualTo(1));
            Assert.That(footprint.AreaCells[0], Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            var list = (IList<SurfaceCell>)footprint.AreaCells;
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list.Add(new SurfaceCell(FaceId.Floor, 3, 3)));
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_ButtonActivatedTileEvent_UsesFinalAuthoritativeTileFact()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var preButton = CreateTileFeature(
                tileId: 100,
                cell,
                TileFeatureKind.Button,
                TileFeatureFlags.None,
                sourceEntityId: 30,
                ownerEntityId: 40,
                teamId: 2);
            var finalButton = CreateTileFeature(
                tileId: 100,
                cell,
                TileFeatureKind.Button,
                TileFeatureFlags.Activated,
                sourceEntityId: 31,
                ownerEntityId: 41,
                teamId: 3);
            var preSnapshot = CreateTileFeatureSnapshot(preButton);
            var finalSnapshot = CreateTileFeatureSnapshot(finalButton);

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    MovementPhaseResult.Empty,
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            Assert.That(presentationData.TileEvents.Count, Is.EqualTo(1));
            var tileEvent = presentationData.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.ButtonActivated));
            Assert.That(tileEvent.TileId, Is.EqualTo(100));
            Assert.That(tileEvent.Cell, Is.EqualTo(cell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Button));
            Assert.That(tileEvent.SourceEntityId, Is.EqualTo(31));
            Assert.That(tileEvent.OwnerEntityId, Is.EqualTo(41));
            Assert.That(tileEvent.TeamId, Is.EqualTo(3));
            Assert.That(tileEvent.TargetEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_TileEvents_MergesAndSortsResolverAndButtonEvents()
        {
            var buttonCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destroyCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var preButton = CreateTileFeature(100, buttonCell, TileFeatureKind.Button, TileFeatureFlags.None);
            var finalButton = CreateTileFeature(100, buttonCell, TileFeatureKind.Button, TileFeatureFlags.Activated);
            var preSnapshot = CreateTileFeatureSnapshot(preButton);
            var finalSnapshot = CreateTileFeatureSnapshot(finalButton);
            var destroyEvent = new TilePresentationEvent(
                TilePresentationEventKind.DestroyTileTriggered,
                200,
                destroyCell,
                TileFeatureKind.Destroy,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                targetEntityId: 30);
            var slideEvent = new TilePresentationEvent(
                TilePresentationEventKind.SlideTileRedirected,
                300,
                destroyCell,
                TileFeatureKind.Slide,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                targetEntityId: 40,
                direction: Direction.Up);

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    MovementPhaseResult.Empty,
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    tileEvents: new[] { slideEvent, destroyEvent }));

            Assert.That(presentationData.TileEvents, Has.Count.EqualTo(3));
            Assert.That(
                presentationData.TileEvents.Select(tileEvent => tileEvent.EventKind).ToArray(),
                Is.EqualTo(new[]
                {
                    TilePresentationEventKind.DestroyTileTriggered,
                    TilePresentationEventKind.SlideTileRedirected,
                    TilePresentationEventKind.ButtonActivated,
                }));
            Assert.That(presentationData.TileEvents[0].TargetEntityId, Is.EqualTo(30));
            Assert.That(presentationData.TileEvents[1].TargetEntityId, Is.EqualTo(40));
            Assert.That(presentationData.TileEvents[1].Direction, Is.EqualTo(Direction.Up));
            Assert.That(presentationData.TileEvents[2].TargetEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_ButtonActivatedTileEvent_DoesNotRepeatForAlreadyActivatedButton()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var button = CreateTileFeature(
                tileId: 100,
                cell,
                TileFeatureKind.Button,
                TileFeatureFlags.Activated);
            var snapshot = CreateTileFeatureSnapshot(button);

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    snapshot,
                    snapshot,
                    snapshot,
                    snapshot,
                    MovementPhaseResult.Empty,
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            Assert.That(presentationData.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_WithTimingProfile_NormalizesPreExistingProjectileCadence()
        {
            var timingProfile = new GameplayTimingProfile(
                simulationTicksPerSecond: 120,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8);
            var worldState = CreateWorldState(
                new[]
                {
                    new EntityState
                    {
                        entityId = 10,
                        position = new Vector2Int(0, 0),
                        hp = 1,
                        maxHp = 1,
                        teamId = 1,
                        type = EntityType.Projectile,
                        facing = Direction.Right,
                    },
                },
                timingProfile);
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetProjectileAt(new Vector2Int(0, 0), out var projectile), Is.True);
            Assert.That(projectile.stateTimer, Is.EqualTo(timingProfile.ProjectileStepIntervalTicks));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_EnumeratesEntitiesInEntityIdOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 30,
                    position = new Vector2Int(3, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
                new EntityState
                {
                    entityId = 20,
                    position = new Vector2Int(2, 0),
                    hp = 1,
                    maxHp = 1,
                    teamId = 2,
                    type = EntityType.Projectile,
                },
            });
            var snapshot = CreateSnapshot(worldState);
            var orderedEntities = new List<EntityState>();

            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, orderedEntities.Select(entity => entity.entityId).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_EnumeratesOccupancyLayersInCellOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 30,
                    position = new Vector2Int(3, 1),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(0, 2),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 25,
                    position = new Vector2Int(0, 2),
                    hp = 2,
                    maxHp = 2,
                    teamId = 2,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 15,
                    position = new Vector2Int(0, 1),
                    hp = 1,
                    maxHp = 1,
                    teamId = 0,
                    type = EntityType.Box,
                },
                new EntityState
                {
                    entityId = 40,
                    position = new Vector2Int(2, 1),
                    hp = 1,
                    maxHp = 1,
                    teamId = 2,
                    type = EntityType.Projectile,
                },
                new EntityState
                {
                    entityId = 20,
                    position = new Vector2Int(1, 1),
                    hp = 1,
                    maxHp = 1,
                    teamId = 2,
                    type = EntityType.Projectile,
                },
            });
            var snapshot = CreateSnapshot(worldState);
            var orderedUnits = new List<SnapshotOccupancyEntry>();
            var orderedSolids = new List<SnapshotOccupancyEntry>();
            var orderedProjectiles = new List<SnapshotOccupancyEntry>();

            snapshot.EnumerateUnitOccupancyOrdered(orderedUnits);
            snapshot.EnumerateSolidOccupancyOrdered(orderedSolids);
            snapshot.EnumerateProjectileOccupancyOrdered(orderedProjectiles);

            CollectionAssert.AreEqual(
                new[]
                {
                    (X: 0, Y: 2, EntityId: 10),
                    (X: 0, Y: 2, EntityId: 25),
                    (X: 3, Y: 1, EntityId: 30),
                },
                orderedUnits.Select(entry => (entry.Cell.x, entry.Cell.y, entry.EntityId)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (X: 0, Y: 1, EntityId: 15),
                },
                orderedSolids.Select(entry => (entry.Cell.x, entry.Cell.y, entry.EntityId)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (X: 1, Y: 1, EntityId: 20),
                    (X: 2, Y: 1, EntityId: 40),
                },
                orderedProjectiles.Select(entry => (entry.Cell.x, entry.Cell.y, entry.EntityId)).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_MarkedForDeathUnit_DoesNotBlockUnitPlacementButStillBlocksMovementUntilCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
            });
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetUnitTraversalBlocker(new Vector2Int(1, 0), out _), Is.False);
            Assert.That(snapshot.TryPickImpactTargetAt(new Vector2Int(1, 0), sourceTeamId: 1, out var impactTarget), Is.True);
            Assert.That(impactTarget.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_IsBlockedForUnit_ConsidersBoardBoundsTerrainAndIgnoresProjectileAndUnitLayers()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    new EntityState
                    {
                        entityId = 10,
                        position = new Vector2Int(1, 0),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                    },
                    new EntityState
                    {
                        entityId = 15,
                        position = new Vector2Int(1, 0),
                        hp = 2,
                        maxHp = 2,
                        teamId = 2,
                        type = EntityType.Unit,
                    },
                    new EntityState
                    {
                        entityId = 20,
                        position = new Vector2Int(2, 0),
                        hp = 1,
                        maxHp = 1,
                        teamId = 1,
                        type = EntityType.Projectile,
                    },
                    new EntityState
                    {
                        entityId = 30,
                        position = new Vector2Int(3, 0),
                        hp = 1,
                        maxHp = 1,
                        teamId = 0,
                        type = EntityType.None,
                    },
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                new GameplayTerrainData(new[] { new Vector2Int(0, 1) }));
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.IsInsideBoard(new Vector2Int(0, 0)), Is.True);
            Assert.That(snapshot.IsInsideBoard(new Vector2Int(-1, 0)), Is.False);
            Assert.That(snapshot.TryGetUnitTraversalBlocker(new Vector2Int(-1, 0), out var boardEdgeBlocker), Is.True);
            Assert.That(boardEdgeBlocker.Kind, Is.EqualTo(SlideStopperKind.BoardEdge));
            Assert.That(snapshot.TryGetUnitTraversalBlocker(new Vector2Int(0, 1), out var terrainBlocker), Is.True);
            Assert.That(terrainBlocker.Kind, Is.EqualTo(SlideStopperKind.Terrain));
            Assert.That(snapshot.TryGetUnitTraversalBlocker(new Vector2Int(1, 0), out _), Is.False);
            Assert.That(snapshot.TryGetUnitTraversalBlocker(new Vector2Int(2, 0), out _), Is.False);
            Assert.That(snapshot.TryGetUnitTraversalBlocker(new Vector2Int(3, 0), out var solidBlocker), Is.True);
            Assert.That(solidBlocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(solidBlocker.EntityId, Is.EqualTo(30));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryGetPlacementBlocker_AppliesGameplayFacePresenceAndEntityTypeRules()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    new EntityState
                    {
                        entityId = 10,
                        position = new SurfaceCell(FaceId.Ceiling, 1, 0),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                    },
                    new EntityState
                    {
                        entityId = 20,
                        position = new SurfaceCell(FaceId.Floor, 2, 0),
                        hp = 1,
                        maxHp = 1,
                        teamId = 1,
                        type = EntityType.Unit,
                        boardPresence = EntityBoardPresence.Detached,
                    },
                    new EntityState
                    {
                        entityId = 30,
                        position = new SurfaceCell(FaceId.Floor, 3, 0),
                        hp = 0,
                        maxHp = 1,
                        teamId = 2,
                        type = EntityType.Unit,
                        markedForDeath = true,
                    },
                    new EntityState
                    {
                        entityId = 40,
                        position = new SurfaceCell(FaceId.Floor, 1, 1),
                        hp = 1,
                        maxHp = 1,
                        teamId = 0,
                        type = EntityType.None,
                    },
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 1)),
                new GameplayTerrainData(new[] { new Vector2Int(0, 1) }));
            var snapshot = CreateSnapshot(worldState);

            Assert.That(
                snapshot.TryGetPlacementBlocker(EntityType.Unit, new SurfaceCell(FaceId.Ceiling, 0, 1), ignoredEntityId: 0, out _),
                Is.False);
            Assert.That(
                snapshot.TryGetPlacementBlocker(EntityType.Unit, new SurfaceCell(FaceId.Ceiling, 1, 0), ignoredEntityId: 0, out _),
                Is.False);
            Assert.That(
                snapshot.TryGetPlacementBlocker(EntityType.Unit, new SurfaceCell(FaceId.Floor, 2, 0), ignoredEntityId: 0, out _),
                Is.False);

            Assert.That(
                snapshot.TryGetPlacementBlocker(EntityType.Unit, new SurfaceCell(FaceId.Floor, 3, 0), ignoredEntityId: 0, out _),
                Is.False);
            Assert.That(
                snapshot.TryGetPlacementBlocker(EntityType.Projectile, new SurfaceCell(FaceId.Floor, 3, 0), ignoredEntityId: 0, out _),
                Is.False);
            Assert.That(
                snapshot.TryGetPlacementBlocker(EntityType.Box, new SurfaceCell(FaceId.Floor, 3, 0), ignoredEntityId: 0, out var blocker),
                Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(blocker.EntityId, Is.EqualTo(30));

            Assert.That(
                snapshot.TryGetPlacementBlocker(EntityType.Projectile, new SurfaceCell(FaceId.Floor, 1, 1), ignoredEntityId: 0, out var solidBlocker),
                Is.True);
            Assert.That(solidBlocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(solidBlocker.EntityId, Is.EqualTo(40));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_CanBeTargetedForNewSelection_FollowsMarkedForDeathPolicy()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
            });
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.CanBeTargetedForNewSelection(10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_IsImmutable_AfterWorldMutation()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 30,
                    position = new Vector2Int(3, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
                new EntityState
                {
                    entityId = 20,
                    position = new Vector2Int(2, 0),
                    hp = 1,
                    maxHp = 1,
                    teamId = 2,
                    type = EntityType.Projectile,
                },
            });
            var snapshot = CreateSnapshot(worldState);
            var unitsAtMarkedCell = new List<EntityState>();
            var unitsAtOriginalCell = new List<EntityState>();
            var unitsAtMovedCell = new List<EntityState>();

            snapshot.EnumerateUnitsAt(new Vector2Int(1, 0), unitsAtMarkedCell);
            CollectionAssert.AreEqual(new[] { 10 }, unitsAtMarkedCell.Select(entity => entity.entityId).ToArray());
            Assert.That(snapshot.TryGetProjectileAt(new Vector2Int(2, 0), out var projectile), Is.True);
            Assert.That(projectile.entityId, Is.EqualTo(20));

            CreateWriteContext(worldState).MoveEntity(30, new Vector2Int(5, 0));

            snapshot.EnumerateUnitsAt(new Vector2Int(3, 0), unitsAtOriginalCell);
            CollectionAssert.AreEqual(new[] { 30 }, unitsAtOriginalCell.Select(entity => entity.entityId).ToArray());
            snapshot.EnumerateUnitsAt(new Vector2Int(5, 0), unitsAtMovedCell);
            Assert.That(unitsAtMovedCell, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void Movement_EdgeReservation_RejectsLaterCandidateThatSharesUndirectedEdge()
        {
            var resolver = new MovementResolver();
            var sortedCandidates = new List<ActionGroup>
            {
                CreateMoveGroup(
                    groupId: 1,
                    intentId: 1,
                    sourceId: 10,
                    priority: 10,
                    new MoveAction(entityId: 10, source: new Vector2Int(0, 0), destination: new Vector2Int(1, 0), facing: Direction.Right)),
                CreateMoveGroup(
                    groupId: 2,
                    intentId: 2,
                    sourceId: 20,
                    priority: 5,
                    new MoveAction(entityId: 20, source: new Vector2Int(1, 0), destination: new Vector2Int(0, 0), facing: Direction.Left)),
            };
            var selectedGroups = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            resolver.Resolve(sortedCandidates, selectedGroups, rejectedReasons);

            CollectionAssert.AreEqual(new[] { 1 }, selectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=EdgeReserved|From=(0,0)|To=(1,0)",
                },
                rejectedReasons);
        }

        [Test]
        [Category("Extended")]
        public void Movement_EdgeReservation_StillRejectsDestinationConflict()
        {
            var resolver = new MovementResolver();
            var sortedCandidates = new List<ActionGroup>
            {
                CreateMoveGroup(
                    groupId: 1,
                    intentId: 1,
                    sourceId: 10,
                    priority: 10,
                    new MoveAction(entityId: 10, source: new Vector2Int(0, 0), destination: new Vector2Int(1, 0), facing: Direction.Right)),
                CreateMoveGroup(
                    groupId: 2,
                    intentId: 2,
                    sourceId: 20,
                    priority: 5,
                    new MoveAction(entityId: 20, source: new Vector2Int(2, 0), destination: new Vector2Int(1, 0), facing: Direction.Left)),
            };
            var selectedGroups = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            resolver.Resolve(sortedCandidates, selectedGroups, rejectedReasons);

            CollectionAssert.AreEqual(new[] { 1 }, selectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=DestinationReserved|Cell=(1,0)",
                },
                rejectedReasons);
        }

        [Test]
        [Category("Extended")]
        public void Movement_UnitSharedMove_RejectsLaterCandidateWhenPushAlreadyReservedDestination()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 20,
                    position = new Vector2Int(2, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 30,
                    position = new Vector2Int(0, 0),
                    hp = 1,
                    maxHp = 1,
                    teamId = 0,
                    type = EntityType.Box,
                },
            });
            var snapshot = CreateSnapshot(worldState);
            var resolver = new MovementResolver();
            var sortedCandidates = new List<ActionGroup>
            {
                CreatePushGroup(
                    groupId: 1,
                    intentId: 1,
                    sourceId: 40,
                    priority: 10,
                    new MoveAction(entityId: 30, source: new Vector2Int(0, 0), destination: new Vector2Int(1, 0), facing: Direction.Right)),
                CreateMoveGroup(
                    groupId: 2,
                    intentId: 2,
                    sourceId: 20,
                    priority: 5,
                    new MoveAction(entityId: 20, source: new Vector2Int(2, 0), destination: new Vector2Int(1, 0), facing: Direction.Left)),
            };
            var selectedGroups = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            resolver.Resolve(snapshot, sortedCandidates, selectedGroups, rejectedReasons);

            CollectionAssert.AreEqual(new[] { 1 }, selectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=DestinationReserved|Cell=(1,0)",
                },
                rejectedReasons);
        }

        [Test]
        [Category("Extended")]
        public void Movement_UnitSharedMove_RejectsLaterPushCandidateThatSharesUndirectedEdge()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(0, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 30,
                    position = new Vector2Int(1, 0),
                    hp = 1,
                    maxHp = 1,
                    teamId = 0,
                    type = EntityType.Box,
                },
            });
            var snapshot = CreateSnapshot(worldState);
            var resolver = new MovementResolver();
            var sortedCandidates = new List<ActionGroup>
            {
                CreateMoveGroup(
                    groupId: 1,
                    intentId: 1,
                    sourceId: 10,
                    priority: 10,
                    new MoveAction(entityId: 10, source: new Vector2Int(0, 0), destination: new Vector2Int(1, 0), facing: Direction.Right)),
                CreatePushGroup(
                    groupId: 2,
                    intentId: 2,
                    sourceId: 40,
                    priority: 5,
                    new MoveAction(entityId: 30, source: new Vector2Int(1, 0), destination: new Vector2Int(0, 0), facing: Direction.Left)),
            };
            var selectedGroups = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            resolver.Resolve(snapshot, sortedCandidates, selectedGroups, rejectedReasons);

            CollectionAssert.AreEqual(new[] { 1 }, selectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=40|Reason=EdgeReserved|From=(0,0)|To=(1,0)",
                },
                rejectedReasons);
        }

        [Test]
        [Category("Extended")]
        public void Movement_TopologyReservation_RejectsLaterCandidateThatAlsoChangesTopology()
        {
            var resolver = new MovementResolver();
            var sortedCandidates = new List<ActionGroup>
            {
                CreateRotateGroup(
                    groupId: 1,
                    intentId: 1,
                    sourceId: 10,
                    priority: 10,
                    source: new SurfaceCell(FaceId.Floor, 0, 1),
                    destination: new SurfaceCell(FaceId.Front, 0, 0),
                    rotationKind: CubeRotationKind.Forward,
                    updatedTopology: new CubeTopologyState(FaceId.Front)),
                CreateRotateGroup(
                    groupId: 2,
                    intentId: 2,
                    sourceId: 20,
                    priority: 5,
                    source: new SurfaceCell(FaceId.Floor, 1, 0),
                    destination: new SurfaceCell(FaceId.Back, 1, 1),
                    rotationKind: CubeRotationKind.Backward,
                    updatedTopology: new CubeTopologyState(FaceId.Back)),
            };
            var selectedGroups = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            resolver.Resolve(sortedCandidates, selectedGroups, rejectedReasons);

            CollectionAssert.AreEqual(new[] { 1 }, selectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=TopologyExclusive|BlockedBy=1|BlockingKind=Move|BlockingTopologyChange=True",
                },
                rejectedReasons);
        }

        [Test]
        [Category("Extended")]
        public void Movement_TopologyExclusive_RejectsLaterOrdinaryCandidateAfterTopologySelected()
        {
            var resolver = new MovementResolver();
            var sortedCandidates = new List<ActionGroup>
            {
                CreateRotateGroup(
                    groupId: 1,
                    intentId: 1,
                    sourceId: 10,
                    priority: 10,
                    source: new SurfaceCell(FaceId.Floor, 0, 1),
                    destination: new SurfaceCell(FaceId.Front, 0, 0),
                    rotationKind: CubeRotationKind.Forward,
                    updatedTopology: new CubeTopologyState(FaceId.Front)),
                CreateMoveGroup(
                    groupId: 2,
                    intentId: 2,
                    sourceId: 20,
                    priority: 5,
                    new MoveAction(entityId: 20, source: new Vector2Int(2, 0), destination: new Vector2Int(1, 0), facing: Direction.Left)),
            };
            var selectedGroups = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            resolver.Resolve(sortedCandidates, selectedGroups, rejectedReasons);

            CollectionAssert.AreEqual(new[] { 1 }, selectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=TopologyExclusive|BlockedBy=1|BlockingKind=Move|BlockingTopologyChange=True",
                },
                rejectedReasons);
        }

        [Test]
        [Category("Extended")]
        public void Movement_TopologyExclusive_RejectsTopologyCandidateWhenOrdinaryGroupAlreadySelected()
        {
            var resolver = new MovementResolver();
            var sortedCandidates = new List<ActionGroup>
            {
                CreateMoveGroup(
                    groupId: 1,
                    intentId: 1,
                    sourceId: 20,
                    priority: 10,
                    new MoveAction(entityId: 20, source: new Vector2Int(2, 0), destination: new Vector2Int(1, 0), facing: Direction.Left)),
                CreateRotateGroup(
                    groupId: 2,
                    intentId: 2,
                    sourceId: 10,
                    priority: 5,
                    source: new SurfaceCell(FaceId.Floor, 0, 1),
                    destination: new SurfaceCell(FaceId.Front, 0, 0),
                    rotationKind: CubeRotationKind.Forward,
                    updatedTopology: new CubeTopologyState(FaceId.Front)),
            };
            var selectedGroups = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            resolver.Resolve(sortedCandidates, selectedGroups, rejectedReasons);

            CollectionAssert.AreEqual(new[] { 1 }, selectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=10|Reason=TopologyExclusive|BlockedBy=1|BlockingKind=Move|BlockingTopologyChange=False",
                },
                rejectedReasons);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsMoveMotionForUnitMove()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, sourceCell, Direction.Up),
                }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, destinationCell, Direction.Right),
                }).CreateSnapshot();
            var actionGroup = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.Move);
            actionGroup.AssignGroupId(1);
            actionGroup.Moves.Add(new MoveAction(10, sourceCell, destinationCell, Direction.Right));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResult(actionGroup, ResolvedActionSemanticKind.Move),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 10, Kind: TickEntityMotionKind.Move, Source: sourceCell, Destination: destinationCell),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_GenericMovePresentation_Retained()
        {
            TickPresentationDataBuilder_BuildsMoveMotionForUnitMove();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_RuntimeBuilder_UsesMoveForActiveChargeMoveSemantic()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var preMovementEnemy = CreateEntity(40, EntityType.Unit, sourceCell, Direction.Right);
            preMovementEnemy.aiMode = EnemyAiMode.Charge;
            var postMovementEnemy = CreateEntity(40, EntityType.Unit, destinationCell, Direction.Right);
            postMovementEnemy.aiMode = EnemyAiMode.Charge;
            var preMovementWorld = CreateWorldState(
                new[]
                {
                    preMovementEnemy,
                });
            CreateWriteContext(preMovementWorld).SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 2,
                    lockedDirection = Direction.Right,
                    windupEndTick = 1,
                    remainingActiveSteps = 1,
                    recoverRemainingTicks = 0,
                });

            var postMovementWorld = CreateWorldState(
                new[]
                {
                    postMovementEnemy,
                });
            CreateWriteContext(postMovementWorld).SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 2,
                    lockedDirection = Direction.Right,
                    windupEndTick = 1,
                    remainingActiveSteps = 0,
                    recoverRemainingTicks = 0,
                });

            var preMovementSnapshot = preMovementWorld.CreateSnapshot();
            var postMovementSnapshot = postMovementWorld.CreateSnapshot();
            var actionGroup = new ActionGroup(intentId: 1, sourceId: 40, priority: 5, ActionGroupKind.Move);
            actionGroup.AssignGroupId(1);
            actionGroup.Moves.Add(new MoveAction(40, sourceCell, destinationCell, Direction.Right));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResult(actionGroup, ResolvedActionSemanticKind.Move),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 40, Kind: TickEntityMotionKind.Move, Source: sourceCell, Destination: destinationCell),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsProjectileMoveMotionForProjectileMove()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(11, EntityType.Projectile, sourceCell, Direction.Right),
                }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(11, EntityType.Projectile, destinationCell, Direction.Right),
                }).CreateSnapshot();
            var actionGroup = new ActionGroup(intentId: 1, sourceId: 11, priority: 5, ActionGroupKind.Move);
            actionGroup.AssignGroupId(1);
            actionGroup.Moves.Add(new MoveAction(11, sourceCell, destinationCell, Direction.Right));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResult(actionGroup, ResolvedActionSemanticKind.ProjectileMove),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 11, Kind: TickEntityMotionKind.ProjectileMove, Source: sourceCell, Destination: destinationCell),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsBoxSlideMotionForSlidingPushBox()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);

            var sourceEntity = CreateEntity(30, EntityType.Box, sourceCell, Direction.Right);
            sourceEntity.boxCapabilities = BoxCapabilities.Push;

            var destinationEntity = CreateEntity(30, EntityType.Box, destinationCell, Direction.Right);
            destinationEntity.boxCapabilities = BoxCapabilities.Push;
            destinationEntity.state = EntityPhaseState.Sliding;
            destinationEntity.stateTimer = 11;

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    sourceEntity,
                }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    destinationEntity,
                }).CreateSnapshot();
            var actionGroup = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.Push);
            actionGroup.AssignGroupId(1);
            actionGroup.Moves.Add(new MoveAction(30, sourceCell, destinationCell, Direction.Right));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResult(actionGroup),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.BoxSlide, Source: sourceCell, Destination: destinationCell),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
        }

        [Test]
        [Category("Core")]
        public void TickResultBuilder_BoxActionMovement_DoesNotSuppressLegacyMotion()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);

            var sourceEntity = CreateEntity(30, EntityType.Box, sourceCell, Direction.Right);
            sourceEntity.boxCapabilities = BoxCapabilities.Push;

            var destinationEntity = CreateEntity(30, EntityType.Box, destinationCell, Direction.Right);
            destinationEntity.boxCapabilities = BoxCapabilities.Push;
            destinationEntity.state = EntityPhaseState.Sliding;
            destinationEntity.stateTimer = 11;

            var preMovementSnapshot = CreateWorldState(new[] { sourceEntity }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(new[] { destinationEntity }).CreateSnapshot();
            var metadata = new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.Slide,
                sourceActorEntityId: 10,
                actionPlanId: 1,
                movementSemanticKind: MovementSemanticKind.Slide,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.BoxActionMovement,
                boundaryReason: "BoxActionMovement");

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResultWithOperations(
                        FinalizationOperation.MoveEntity(1, 30, destinationCell, metadata)),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.BoxSlide, Source: sourceCell, Destination: destinationCell),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
        }

        [Test]
        [Category("Core")]
        public void TickResultBuilder_BoxActionMovement_DoesNotSuppressLegacyMotion_Regression()
        {
            TickResultBuilder_BoxActionMovement_DoesNotSuppressLegacyMotion();
        }

        [Test]
        [Category("Core")]
        public void TickPresentationDataBuilder_DestroyTileFinalization_EmitsAfterMotionBoxDestroyExitWithoutCleanupRemove()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var contactCell = new SurfaceCell(FaceId.Floor, 0, 0);
            const int boxId = 30;

            var preMovementBox = CreateEntity(boxId, EntityType.Box, sourceCell, Direction.Down);
            preMovementBox.boxCapabilities = BoxCapabilities.Push;
            var postMovementBox = CreateEntity(boxId, EntityType.Box, contactCell, Direction.Down);
            postMovementBox.boxCapabilities = BoxCapabilities.Push;
            var postAttackBox = postMovementBox;
            postAttackBox.boardPresence = EntityBoardPresence.Detached;
            postAttackBox.markedForDeath = true;

            var preMovementSnapshot = CreateWorldState(new[] { preMovementBox }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(new[] { postMovementBox }).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(new[] { postAttackBox }).CreateSnapshot();
            var finalSnapshot = CreateWorldState(Array.Empty<EntityState>()).CreateSnapshot();
            var moveMetadata = new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.Push,
                sourceActorEntityId: 10,
                actionPlanId: 1,
                movementSemanticKind: MovementSemanticKind.Push);
            var destroyMetadata = new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.None,
                sourceActorEntityId: boxId,
                actionPlanId: 1,
                exitCauseHint: TickEntityExitCause.BoxDestroy,
                damageSourceType: DamageSourceType.Environmental,
                presentationTargetCell: contactCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation,
                boundaryReason: "DestroyTile",
                exitPresentationTiming: EntityExitPresentationTiming.AfterEntityMotion,
                hasPresentationTargetCell: true);
            var finalizationBatch = new FinalizationBatch();
            finalizationBatch.MoveEntity(boxId, contactCell, moveMetadata);
            finalizationBatch.SetBoardPresence(boxId, EntityBoardPresence.Detached, destroyMetadata);
            finalizationBatch.MarkDestroy(boxId, destroyMetadata);

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResultWithOperations(
                        FinalizationOperation.MoveEntity(1, boxId, contactCell, moveMetadata)),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.RemovedEntities(boxId),
                    finalizationBatch: finalizationBatch));

            Assert.That(presentationData.EntityMotions.Select(motion => motion.EntityId), Does.Contain(boxId));
            Assert.That(
                presentationData.VisibilityChanges.Any(
                    change => change.EntityId == boxId &&
                              change.ChangeKind == TickVisibilityChangeKind.Remove),
                Is.False);
            var exitSignal = presentationData.EntityExitSignals.Single();
            Assert.That(exitSignal.ExitedEntityId, Is.EqualTo(boxId));
            Assert.That(exitSignal.ExitCause, Is.EqualTo(TickEntityExitCause.BoxDestroy));
            Assert.That(exitSignal.SourceCell, Is.EqualTo(contactCell));
            Assert.That(exitSignal.Timing, Is.EqualTo(EntityExitPresentationTiming.AfterEntityMotion));
            Assert.That(exitSignal.EntityType, Is.EqualTo(EntityType.Box));
        }

        [Test]
        [Category("Core")]
        public void TickResultBuilder_LocomotionAnchorCommit_SuppressesLegacyMotion()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, sourceCell, Direction.Right),
                }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, destinationCell, Direction.Right),
                }).CreateSnapshot();
            var metadata = new FinalizationOperationMetadata(
                TickPhase.Plan,
                ResolvedActionSemanticKind.Move,
                sourceActorEntityId: 10,
                actionPlanId: 0,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                boundaryReason: "TestAnchorCommit");

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResultWithOperations(
                        FinalizationOperation.MoveEntity(1, 10, destinationCell, metadata)),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            Assert.That(presentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void TickResultBuilder_LocomotionAnchorCommit_SuppressesLegacyMotion_Regression()
        {
            TickResultBuilder_LocomotionAnchorCommit_SuppressesLegacyMotion();
        }

        [Test]
        [Category("Core")]
        public void TickResultBuilder_Free2DTopologyTransition_MetadataSynthesizesTopologyMotionWhenSnapshotsAlreadyMatch()
        {
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var expectedSourceTopology = destinationTopology.Rotate(CubeRotationKind.Backward);
            var destinationCell = new SurfaceCell(FaceId.Front, 0, 0);
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var snapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, destinationCell, Direction.Up),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                destinationTopology).CreateSnapshot();
            var metadata = new FinalizationOperationMetadata(
                TickPhase.Plan,
                ResolvedActionSemanticKind.Move,
                sourceActorEntityId: 10,
                actionPlanId: 0,
                intentId: 1,
                rotationKind: CubeRotationKind.Forward,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.Free2DTopologyTransition,
                boundaryReason: "Free2DTopologyNativeTransition");

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    snapshot,
                    snapshot,
                    snapshot,
                    snapshot,
                    CreateMovementPhaseResultWithOperations(
                        FinalizationOperation.MoveEntity(1, 10, destinationCell, metadata)),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            Assert.That(presentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(presentationData.TopologyMotion.Value.SourceTopology, Is.EqualTo(expectedSourceTopology));
            Assert.That(presentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(destinationTopology));
            Assert.That(presentationData.TopologyMotion.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(presentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_SuppressionBoundary_IsCurrent()
        {
            var cases = new[]
            {
                (BoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit, Suppressed: true),
                (BoundaryKind: MovementExecutionBoundaryKind.UnitOrdinaryLocomotion, Suppressed: true),
                (BoundaryKind: MovementExecutionBoundaryKind.Free2DTopologyTransition, Suppressed: true),
                (BoundaryKind: MovementExecutionBoundaryKind.BoxActionMovement, Suppressed: false),
                (BoundaryKind: MovementExecutionBoundaryKind.TopologyMaterialization, Suppressed: false),
                (BoundaryKind: MovementExecutionBoundaryKind.SpawnRespawnPlacement, Suppressed: false),
                (BoundaryKind: MovementExecutionBoundaryKind.CleanupRemoval, Suppressed: false),
                (BoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation, Suppressed: false),
                (BoundaryKind: MovementExecutionBoundaryKind.LegacyFallback, Suppressed: false),
                (BoundaryKind: MovementExecutionBoundaryKind.Unknown, Suppressed: false),
            };
            var preEntities = new List<EntityState>();
            var postEntities = new List<EntityState>();
            var operations = new List<FinalizationOperation>();

            for (var i = 0; i < cases.Length; i++)
            {
                var entityId = 100 + i;
                var sourceCell = new SurfaceCell(FaceId.Floor, i, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, i, 1);
                preEntities.Add(CreateEntity(entityId, EntityType.Unit, sourceCell, Direction.Up));
                postEntities.Add(CreateEntity(entityId, EntityType.Unit, destinationCell, Direction.Right));

                var metadata = new FinalizationOperationMetadata(
                    TickPhase.Resolve,
                    ResolvedActionSemanticKind.Move,
                    sourceActorEntityId: entityId,
                    actionPlanId: i + 1,
                    movementSemanticKind: MovementSemanticKind.Move,
                    movementExecutionBoundaryKind: cases[i].BoundaryKind,
                    boundaryReason: cases[i].BoundaryKind.ToString());
                operations.Add(FinalizationOperation.MoveEntity(i + 1, entityId, destinationCell, metadata));
            }

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    CreateWorldState(preEntities).CreateSnapshot(),
                    CreateWorldState(postEntities).CreateSnapshot(),
                    CreateWorldState(postEntities).CreateSnapshot(),
                    CreateWorldState(postEntities).CreateSnapshot(),
                    CreateMovementPhaseResultWithOperations(operations.ToArray()),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            for (var i = 0; i < cases.Length; i++)
            {
                var entityId = 100 + i;
                var hasMove = presentationData.EntityMotions.Any(motion =>
                    motion.EntityId == entityId &&
                    motion.MotionKind == TickEntityMotionKind.Move);
                Assert.That(hasMove, Is.EqualTo(!cases[i].Suppressed), cases[i].BoundaryKind.ToString());
            }
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_UsesPostMovementSnapshotForFollowThroughMotion_AndPostAttackSnapshotForEnemyDeath()
        {
            const int tickIndex = 11;
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);

            var sourceBox = CreateEntity(20, EntityType.Box, sourceCell, Direction.Right);
            sourceBox.boxCapabilities = BoxCapabilities.Push;

            var movedBox = sourceBox;
            movedBox.position = destinationCell;
            movedBox.state = EntityPhaseState.Sliding;
            movedBox.stateTimer = 11;

            var enemyBeforeImpact = CreateEnemyEntity(40, destinationCell, EnemyAiMode.Attack, Direction.Left);
            var enemyLocalVacated = enemyBeforeImpact;
            enemyLocalVacated.boardPresence = EntityBoardPresence.Detached;
            var enemyMarkedDead = enemyLocalVacated;
            enemyMarkedDead.hp = 0;
            enemyMarkedDead.markedForDeath = true;
            enemyMarkedDead.aiMode = EnemyAiMode.Recover;

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    sourceBox,
                    enemyBeforeImpact,
                }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    movedBox,
                    enemyLocalVacated,
                }).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(
                new[]
                {
                    movedBox,
                    enemyMarkedDead,
                }).CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                new[]
                {
                    movedBox,
                }).CreateSnapshot();

            var movementGroup = new ActionGroup(intentId: 1, sourceId: 20, priority: 5, ActionGroupKind.Push);
            movementGroup.AssignGroupId(1);
            movementGroup.BoardPresenceChanges.Add(new BoardPresenceChangeAction(40, EntityBoardPresence.Detached));
            movementGroup.Moves.Add(new MoveAction(20, sourceCell, destinationCell, Direction.Right));

            var attackGroup = new ActionGroup(intentId: 2, sourceId: 20, priority: 5, ActionGroupKind.Attack);
            attackGroup.AssignGroupId(2);
            attackGroup.Destroys.Add(new DestroyAction(40));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResult(movementGroup, ResolvedActionSemanticKind.Push),
                    CreateAttackPhaseResult(attackGroup),
                    CleanupFixtureFactory.RemovedEntities(40),
                    currentTickIndex: tickIndex));

            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 20, Kind: TickEntityMotionKind.Push, Source: sourceCell, Destination: destinationCell),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
            Assert.That(presentationData.EntityExitSignals.Count, Is.EqualTo(1));
            var signal = presentationData.EntityExitSignals[0];
            Assert.That(signal.ExitedEntityId, Is.EqualTo(40));
            Assert.That(signal.ExitCause, Is.EqualTo(TickEntityExitCause.EnemyDeath));
            Assert.That(signal.SourceActorEntityId, Is.EqualTo(20));
            Assert.That(signal.SourceCell, Is.EqualTo(destinationCell));
            Assert.That(signal.Topology, Is.EqualTo(postAttackSnapshot.Topology));
            Assert.That(signal.PresentationSeed, Is.EqualTo(BuildExpectedPresentationSeed(tickIndex, 40, 20, TickEntityExitCause.EnemyDeath)));
            Assert.That(presentationData.VisibilityChanges.Any(change => change.EntityId == 40 && change.ChangeKind == TickVisibilityChangeKind.Remove), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsTopologyAndVisibilityPresentationRecords()
        {
            var initialTopology = new CubeTopologyState(FaceId.Floor);
            var rotatedTopology = new CubeTopologyState(FaceId.Front);
            var actorSourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var actorDestinationCell = new SurfaceCell(FaceId.Front, 0, 0);
            var itemCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var projectileCell = new SurfaceCell(FaceId.Front, 0, 1);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1));

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, actorSourceCell, Direction.Up),
                    CreateEntity(20, EntityType.Box, itemCell, Direction.Up),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                initialTopology).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, actorDestinationCell, Direction.Up),
                    CreateEntity(20, EntityType.Box, itemCell, Direction.Up, boardPresence: EntityBoardPresence.Detached, markedForDeath: true),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                rotatedTopology).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, actorDestinationCell, Direction.Up),
                    CreateEntity(20, EntityType.Box, itemCell, Direction.Up, boardPresence: EntityBoardPresence.Detached, markedForDeath: true),
                    CreateEntity(30, EntityType.Projectile, projectileCell, Direction.Up),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                rotatedTopology).CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, actorDestinationCell, Direction.Up),
                    CreateEntity(30, EntityType.Projectile, projectileCell, Direction.Up),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                rotatedTopology).CreateSnapshot();

            var movementGroup = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.Item);
            movementGroup.AssignGroupId(1);
            movementGroup.Moves.Add(new MoveAction(10, actorSourceCell, actorDestinationCell, Direction.Up));
            movementGroup.BoardPresenceChanges.Add(new BoardPresenceChangeAction(20, EntityBoardPresence.Detached));
            movementGroup.TopologyChanges.Add(new TopologyChangeAction(CubeRotationKind.Forward, rotatedTopology));

            var spawnedProjectile = CreateEntity(30, EntityType.Projectile, projectileCell, Direction.Up);
            spawnedProjectile.spawnTick = 1;
            var attackGroup = new ActionGroup(intentId: 2, sourceId: 10, priority: 5, ActionGroupKind.Attack);
            attackGroup.AssignGroupId(2);
            attackGroup.Spawns.Add(new SpawnAction(1, spawnedProjectile));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResult(movementGroup),
                    CreateAttackPhaseResult(attackGroup),
                    CleanupFixtureFactory.RemovedEntities(20)));

            Assert.That(presentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(presentationData.TopologyMotion.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(presentationData.TopologyMotion.Value.SourceTopology, Is.EqualTo(initialTopology));
            Assert.That(presentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(rotatedTopology));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickVisibilityChangeKind.Spawn),
                },
                presentationData.VisibilityChanges
                    .Select(change => (change.EntityId, change.ChangeKind))
                    .ToArray());
            Assert.That(presentationData.EntityExitSignals.Count, Is.EqualTo(1));
            Assert.That(presentationData.EntityExitSignals[0].ExitedEntityId, Is.EqualTo(20));
            Assert.That(presentationData.EntityExitSignals[0].ExitCause, Is.EqualTo(TickEntityExitCause.ItemConsume));
            Assert.That(presentationData.EntityExitSignals[0].SourceActorEntityId, Is.EqualTo(10));
            Assert.That(presentationData.EntityExitSignals[0].SourceCell, Is.EqualTo(itemCell));
            Assert.That(presentationData.EntityExitSignals[0].Topology, Is.EqualTo(initialTopology));
            Assert.That(presentationData.EntityExitSignals[0].EntityType, Is.EqualTo(EntityType.Box));
            Assert.That(presentationData.TransitionVisibilityChanges, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void RespawnTopologyReset_EmitsTopologyMotionBeforeSpawnVisibility()
        {
            var sourceTopology = new CubeTopologyState(FaceId.Back);
            var destinationTopology = new CubeTopologyState(FaceId.Floor);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2));
            var sourceSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(20, EntityType.Box, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Left),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                sourceTopology).CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(20, EntityType.Box, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Left),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                destinationTopology).CreateSnapshot();
            var respawnPhaseResult = new RespawnPhaseResult(
                Array.Empty<EntityState>(),
                new[]
                {
                    "RespawnDeferred|E=10|Reason=TopologyResetRequired|TargetFace=Front|Tick=9",
                    "RespawnTopologyResetRequested|E=10|From=Back|To=Floor|Rotation=Forward|TargetFace=Front|Tick=9",
                },
                new RespawnTopologyResetRequest(
                    entityId: 10,
                    targetFace: FaceId.Front,
                    sourceTopology,
                    destinationTopology,
                    CubeRotationKind.Forward));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    sourceSnapshot,
                    sourceSnapshot,
                    sourceSnapshot,
                    finalSnapshot,
                    MovementPhaseResult.Empty,
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    respawnPhaseResult,
                    currentTickIndex: 9));

            Assert.That(presentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(presentationData.TopologyMotion.Value.SourceTopology, Is.EqualTo(sourceTopology));
            Assert.That(presentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(destinationTopology));
            Assert.That(presentationData.TopologyMotion.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(
                presentationData.VisibilityChanges.Any(change =>
                    change.EntityId == 10 &&
                    change.ChangeKind == TickVisibilityChangeKind.Spawn),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_FlipDestroySelf_SeparatesLogicalNoMoveFromFlipImpactSignal()
        {
            const int tickIndex = 7;
            var sourceCell = new SurfaceCell(FaceId.Floor, -1, 0);
            var impactCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var enemyBeforeAttack = CreateEntity(40, EntityType.Unit, impactCell, Direction.Right);
            enemyBeforeAttack.hp = 3;
            enemyBeforeAttack.maxHp = 3;
            var enemyAfterAttack = CreateEntity(40, EntityType.Unit, impactCell, Direction.Right);
            enemyAfterAttack.hp = 2;
            enemyAfterAttack.maxHp = 3;
            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(20, EntityType.Box, sourceCell, Direction.Left),
                    enemyBeforeAttack,
                }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(20, EntityType.Box, sourceCell, Direction.Left, boardPresence: EntityBoardPresence.Detached, markedForDeath: true),
                    enemyBeforeAttack,
                }).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(20, EntityType.Box, sourceCell, Direction.Left, boardPresence: EntityBoardPresence.Detached, markedForDeath: true),
                    enemyAfterAttack,
                }).CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                new[]
                {
                    enemyAfterAttack,
                }).CreateSnapshot();

            var movementGroup = new ActionGroup(intentId: 1, sourceId: 20, priority: 5, ActionGroupKind.Flip);
            movementGroup.AssignGroupId(1);
            movementGroup.BoardPresenceChanges.Add(new BoardPresenceChangeAction(20, EntityBoardPresence.Detached));
            movementGroup.Destroys.Add(new DestroyAction(20));

            var attackGroup = new ActionGroup(intentId: 2, sourceId: 20, priority: 5, ActionGroupKind.Attack);
            attackGroup.AssignGroupId(2);
            attackGroup.Damages.Add(new DamageAction(40, 1));

            var impactDispositionRecord = new ImpactDispositionResolutionRecord(
                actionPlanId: 1,
                impactSourceEntityId: 20,
                impactTargetEntityId: 40,
                impactCell,
                policyKind: ImpactDispositionPolicyKind.Flip,
                dispositionKind: ImpactDispositionKind.DestroySelf,
                targetDestroyed: false,
                followThroughLegalityChecked: false,
                followThroughAccepted: false);
            var movementPhaseResult = CanonicalPhaseResultFactory.CreateMovementPhaseResult(
                Array.Empty<RawMovementIntent>(),
                Array.Empty<MoveIntent>(),
                new[] { movementGroup },
                Array.Empty<string>(),
                Array.Empty<string>(),
                _ => ResolvedActionSemanticKind.Flip,
                new[] { impactDispositionRecord });

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    finalSnapshot,
                    movementPhaseResult,
                    CreateAttackPhaseResult(attackGroup),
                    CleanupFixtureFactory.RemovedEntities(20),
                    currentTickIndex: tickIndex));

            Assert.That(presentationData.EntityMotions, Is.Empty);
            Assert.That(presentationData.EntityExitSignals.Count, Is.EqualTo(1));
            Assert.That(presentationData.EntityExitSignals[0].ExitedEntityId, Is.EqualTo(20));
            Assert.That(presentationData.ImpactTransientSignals, Is.Empty);
            Assert.That(presentationData.FlipImpactSignals.Count, Is.EqualTo(1));
            var signal = presentationData.FlipImpactSignals[0];
            Assert.That(signal.SourceActionPlanId, Is.EqualTo(1));
            Assert.That(signal.BoxEntityId, Is.EqualTo(20));
            Assert.That(signal.ImpactTargetEntityId, Is.EqualTo(40));
            Assert.That(signal.ActorEntityId, Is.EqualTo(20));
            Assert.That(signal.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(signal.ImpactCell, Is.EqualTo(impactCell));
            Assert.That(signal.SourceFacing, Is.EqualTo(Direction.Left));
            Assert.That(signal.ImpactFacing, Is.EqualTo(Direction.Right));
            Assert.That(signal.HasLandingCell, Is.False);
            Assert.That(signal.Disposition, Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsTransitionVisibilityPresentationRecordsForTopologyPassengers()
        {
            var initialTopology = new CubeTopologyState(FaceId.Floor);
            var rotatedTopology = new CubeTopologyState(FaceId.Front);
            var actorSourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var actorDestinationCell = new SurfaceCell(FaceId.Front, 0, 0);
            var retainedCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var shownCell = new SurfaceCell(FaceId.Ceiling, 2, 1);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1));

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, actorSourceCell, Direction.Up),
                    CreateEntity(20, EntityType.Box, retainedCell, Direction.Left),
                    CreateEntity(30, EntityType.Box, shownCell, Direction.Right),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                initialTopology).CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                new[]
                {
                    CreateEntity(10, EntityType.Unit, actorDestinationCell, Direction.Up),
                    CreateEntity(20, EntityType.Box, retainedCell, Direction.Left),
                    CreateEntity(30, EntityType.Box, shownCell, Direction.Right),
                },
                boardBounds,
                GameplayTerrainData.Empty,
                rotatedTopology).CreateSnapshot();

            var movementGroup = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.Move);
            movementGroup.AssignGroupId(1);
            movementGroup.Moves.Add(new MoveAction(10, actorSourceCell, actorDestinationCell, Direction.Up));
            movementGroup.TopologyChanges.Add(new TopologyChangeAction(CubeRotationKind.Forward, rotatedTopology));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResult(movementGroup),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None()));

            CollectionAssert.AreEqual(
                new[]
                {
                    (
                        EntityId: 30,
                        Mode: TickTransitionVisibilityMode.ShowAtTransitionStart,
                        Cell: shownCell,
                        Topology: rotatedTopology,
                        Facing: Direction.Right),
                },
                presentationData.TransitionVisibilityChanges
                    .Select(change => (change.EntityId, change.Mode, change.Cell, change.Topology, change.Facing))
                    .ToArray());
            Assert.That(presentationData.VisibilityChanges, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsEnemyDeathExitSignalForAttackKilledEnemy_WithoutGenericCleanupRemove()
        {
            const int tickIndex = 17;
            var topology = new CubeTopologyState(FaceId.Floor);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 2));
            var playerCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var enemyCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var player = CreateEntity(10, EntityType.Unit, playerCell, Direction.Right);
            player.unitRole = UnitRole.Player;
            var enemyAlive = CreateEnemyEntity(40, enemyCell, EnemyAiMode.Attack, Direction.Left);
            enemyAlive.unitRole = UnitRole.Enemy;
            var enemyDead = enemyAlive;
            enemyDead.hp = 0;
            enemyDead.markedForDeath = true;
            enemyDead.aiMode = EnemyAiMode.Recover;

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    player,
                    enemyAlive,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    player,
                    enemyAlive,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(
                new[]
                {
                    player,
                    enemyDead,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                new[]
                {
                    player,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();

            var attackGroup = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.Attack);
            attackGroup.AssignGroupId(1);
            attackGroup.Destroys.Add(new DestroyAction(40));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResult(),
                    CreateAttackPhaseResult(attackGroup),
                    CleanupFixtureFactory.RemovedEntities(40),
                    currentTickIndex: tickIndex));

            Assert.That(presentationData.EntityExitSignals.Count, Is.EqualTo(1));
            var signal = presentationData.EntityExitSignals[0];
            Assert.That(signal.ExitedEntityId, Is.EqualTo(40));
            Assert.That(signal.ExitCause, Is.EqualTo(TickEntityExitCause.EnemyDeath));
            Assert.That(signal.SourceActorEntityId, Is.EqualTo(10));
            Assert.That(signal.EntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(signal.SourceCell, Is.EqualTo(enemyCell));
            Assert.That(signal.Topology, Is.EqualTo(topology));
            Assert.That(signal.PresentationSeed, Is.EqualTo(BuildExpectedPresentationSeed(tickIndex, 40, 10, TickEntityExitCause.EnemyDeath)));
            Assert.That(presentationData.VisibilityChanges.Any(change => change.EntityId == 40 && change.ChangeKind == TickVisibilityChangeKind.Remove), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_PlayerDeathSignal_UsesCanonicalFirstFatalAcceptedDamageSource()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var attackerOneCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var attackerTwoCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var playerAlive = CreateEntity(10, EntityType.Unit, playerCell, Direction.Right);
            playerAlive.unitRole = UnitRole.Player;
            playerAlive.hp = 3;
            playerAlive.maxHp = 3;
            var playerDead = playerAlive;
            playerDead.hp = 0;
            playerDead.markedForDeath = true;
            var attackerOne = CreateEnemyEntity(20, attackerOneCell, EnemyAiMode.Attack, Direction.Left);
            var attackerTwo = CreateEnemyEntity(30, attackerTwoCell, EnemyAiMode.Attack, Direction.Left);

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    playerAlive,
                    attackerOne,
                    attackerTwo,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    playerAlive,
                    attackerOne,
                    attackerTwo,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(
                new[]
                {
                    playerDead,
                    attackerOne,
                    attackerTwo,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    postAttackSnapshot,
                    MovementPhaseResult.Empty,
                    CreateAttackPhaseResult(
                        new DamageResolutionRecord(1, 1, 20, AttackSourceKind.Combat, 10, 1, true, DamageRejectReason.None),
                        new DamageResolutionRecord(2, 2, 30, AttackSourceKind.Combat, 10, 2, true, DamageRejectReason.None)),
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 11));

            Assert.That(presentationData.PlayerDeathSignals.Count, Is.EqualTo(1));
            var signal = presentationData.PlayerDeathSignals[0];
            Assert.That(signal.EntityId, Is.EqualTo(10));
            Assert.That(signal.DidDieThisTick, Is.True);
            Assert.That(signal.SourceEntityId, Is.EqualTo(30));
            Assert.That(signal.ResolvedDamageSourceAvailable, Is.True);
            Assert.That(signal.DamageAmountAtFatalHit, Is.EqualTo(2));
            Assert.That(signal.DeathDirectionHintKind, Is.EqualTo(DeathDirectionHintKind.AttackerReverse));
            Assert.That(signal.FallbackFacing, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_PlayerDeathSignal_DoesNotChangeAfterCanonicalFatalResolution()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0));
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var playerAlive = CreateEntity(10, EntityType.Unit, playerCell, Direction.Right);
            playerAlive.unitRole = UnitRole.Player;
            playerAlive.hp = 3;
            playerAlive.maxHp = 3;
            var playerDead = playerAlive;
            playerDead.hp = 0;
            playerDead.markedForDeath = true;
            var attackerOne = CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 0), EnemyAiMode.Attack, Direction.Left);
            var attackerTwo = CreateEnemyEntity(30, new SurfaceCell(FaceId.Floor, 2, 0), EnemyAiMode.Attack, Direction.Left);
            var attackerThree = CreateEnemyEntity(40, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.Attack, Direction.Left);

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    playerAlive,
                    attackerOne,
                    attackerTwo,
                    attackerThree,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    playerAlive,
                    attackerOne,
                    attackerTwo,
                    attackerThree,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(
                new[]
                {
                    playerDead,
                    attackerOne,
                    attackerTwo,
                    attackerThree,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    postAttackSnapshot,
                    MovementPhaseResult.Empty,
                    CreateAttackPhaseResult(
                        new DamageResolutionRecord(1, 1, 20, AttackSourceKind.Combat, 10, 1, true, DamageRejectReason.None),
                        new DamageResolutionRecord(2, 2, 30, AttackSourceKind.Combat, 10, 2, true, DamageRejectReason.None),
                        new DamageResolutionRecord(3, 3, 40, AttackSourceKind.Combat, 10, 5, true, DamageRejectReason.None)),
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 12));

            Assert.That(presentationData.PlayerDeathSignals.Count, Is.EqualTo(1));
            var signal = presentationData.PlayerDeathSignals[0];
            Assert.That(signal.SourceEntityId, Is.EqualTo(30));
            Assert.That(signal.DamageAmountAtFatalHit, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_PlayerDeathSignal_DoesNotEmitWithoutFatalAcceptedDamage()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var playerAlive = CreateEntity(10, EntityType.Unit, playerCell, Direction.Right);
            playerAlive.unitRole = UnitRole.Player;
            playerAlive.hp = 3;
            playerAlive.maxHp = 3;
            var attacker = CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 0), EnemyAiMode.Attack, Direction.Left);

            var preMovementSnapshot = CreateWorldState(
                new[]
                {
                    playerAlive,
                    attacker,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(
                new[]
                {
                    playerAlive,
                    attacker,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var postAttackSnapshot = CreateWorldState(
                new[]
                {
                    playerAlive,
                    attacker,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();
            var finalSnapshot = CreateWorldState(
                new[]
                {
                    attacker,
                },
                boardBounds,
                GameplayTerrainData.Empty,
                topology).CreateSnapshot();

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    finalSnapshot,
                    MovementPhaseResult.Empty,
                    CreateAttackPhaseResult(
                        new DamageResolutionRecord(1, 1, 20, AttackSourceKind.Combat, 10, 3, false, DamageRejectReason.ReceiverCooldown)),
                    CleanupFixtureFactory.RemovedEntities(10),
                    currentTickIndex: 13));

            Assert.That(presentationData.PlayerDeathSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsEnemyActionSignalForOngoingWindup()
        {
            const int enemyId = 40;
            const int targetId = 10;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var activeAction = CreateEnemyActionState(
                EnemyActionKind.Melee,
                sequence: 3,
                lockedTargetEntityId: targetId,
                direction: Direction.Right,
                startTick: 4,
                executeTick: 6);

            var preMovementSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Attack, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                },
                new EnemyActionStateSeed(enemyId, activeAction));
            var postMovementSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Attack, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                },
                new EnemyActionStateSeed(enemyId, activeAction));
            var finalSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Attack, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                },
                new EnemyActionStateSeed(enemyId, activeAction));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 5));

            var signal = presentationData.EnemyActionSignals.Single();
            Assert.That(signal.EntityId, Is.EqualTo(enemyId));
            Assert.That(signal.ActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
            Assert.That(signal.ActiveActionSequence, Is.EqualTo(3));
            Assert.That(signal.StartedThisTick, Is.False);
            Assert.That(signal.CanceledThisTick, Is.False);
            Assert.That(signal.ExecutedThisTick, Is.False);
            Assert.That(signal.StartedRecoveryThisTick, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsEnemyActionSignalForStartExecuteAndRecovery()
        {
            const int enemyId = 40;
            const int targetId = 10;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var startedAction = CreateEnemyActionState(
                EnemyActionKind.Melee,
                sequence: 1,
                lockedTargetEntityId: targetId,
                direction: Direction.Right,
                startTick: 7,
                executeTick: 7);
            var executedAction = startedAction;
            executedAction.executionAttempted = true;

            var preMovementSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Chase, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                });
            var postMovementSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Attack, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                },
                new EnemyActionStateSeed(enemyId, startedAction));
            var postAttackSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Recover, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                },
                new EnemyActionStateSeed(enemyId, executedAction));
            var attackGroup = new ActionGroup(intentId: 1, sourceId: enemyId, priority: 5, ActionGroupKind.Attack);
            attackGroup.AssignGroupId(1);

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postAttackSnapshot,
                    postAttackSnapshot,
                    CreateMovementPhaseResult(),
                    CreateAttackPhaseResult(attackGroup),
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 7));

            var signal = presentationData.EnemyActionSignals.Single();
            Assert.That(signal.EntityId, Is.EqualTo(enemyId));
            Assert.That(signal.ActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
            Assert.That(signal.ActiveActionSequence, Is.EqualTo(1));
            Assert.That(signal.StartedThisTick, Is.True);
            Assert.That(signal.CanceledThisTick, Is.False);
            Assert.That(signal.ExecutedThisTick, Is.True);
            Assert.That(signal.StartedRecoveryThisTick, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsEnemyActionCancelSignalWhenWindupClears()
        {
            const int enemyId = 40;
            const int targetId = 10;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var activeAction = CreateEnemyActionState(
                EnemyActionKind.Melee,
                sequence: 2,
                lockedTargetEntityId: targetId,
                direction: Direction.Right,
                startTick: 3,
                executeTick: 5);
            var clearedAction = EnemyActionQueries.Clear(activeAction);

            var preMovementSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Attack, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                },
                new EnemyActionStateSeed(enemyId, activeAction));
            var postMovementSnapshot = CreateSnapshotWithEnemyActionStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Chase, Direction.Right),
                    CreateEntity(targetId, EntityType.Unit, targetCell, Direction.Left),
                },
                new EnemyActionStateSeed(enemyId, clearedAction));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 4));

            var signal = presentationData.EnemyActionSignals.Single();
            Assert.That(signal.EntityId, Is.EqualTo(enemyId));
            Assert.That(signal.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
            Assert.That(signal.ActiveActionSequence, Is.EqualTo(2));
            Assert.That(signal.StartedThisTick, Is.False);
            Assert.That(signal.CanceledThisTick, Is.True);
            Assert.That(signal.ExecutedThisTick, Is.False);
            Assert.That(signal.StartedRecoveryThisTick, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForWindupStart()
        {
            const int enemyId = 40;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var jumpState = CreateEnemyJumpState(
                EnemyJumpPhase.Windup,
                sequence: 3,
                sourceCell: enemyCell,
                lockedTargetCell: new SurfaceCell(FaceId.Floor, 3, 1));

            var preMovementSnapshot = CreateSnapshotWithEnemyJumpStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Patrol, Direction.Right),
                });
            var postMovementSnapshot = CreateSnapshotWithEnemyJumpStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Patrol, Direction.Right),
                },
                new EnemyJumpStateSeed(enemyId, jumpState));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 5,
                    jumpBaselineSnapshot: preMovementSnapshot));

            var signal = presentationData.EnemyJumpSignals.Single();
            Assert.That(signal.EntityId, Is.EqualTo(enemyId));
            Assert.That(signal.Sequence, Is.EqualTo(3));
            Assert.That(signal.Phase, Is.EqualTo(EnemyJumpPhase.Windup));
            Assert.That(signal.StartedWindupThisTick, Is.True);
            Assert.That(signal.StartedAirborneThisTick, Is.False);
            Assert.That(signal.LandedThisTick, Is.False);
            Assert.That(signal.RetryThisTick, Is.False);
            Assert.That(signal.Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.WindupStarted));
            Assert.That(signal.SourceCell, Is.EqualTo(enemyCell));
            Assert.That(signal.LockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
            Assert.That(signal.PresentationTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
            Assert.That(signal.Facing, Is.EqualTo(Direction.Right));
            Assert.That(signal.LandingTick, Is.EqualTo(7));
            Assert.That(signal.RemainingAirborneTicks, Is.Zero);
            Assert.That(signal.RetryCount, Is.Zero);
            Assert.That(presentationData.EnemyActionSignals, Is.Empty);
        }

        [Test]
        [Category("Full")]
        public void TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForAirborneStartAndRetry()
        {
            const int enemyId = 40;
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var windupState = CreateEnemyJumpState(
                EnemyJumpPhase.Windup,
                sequence: 4,
                sourceCell,
                targetCell);
            var airborneState = CreateEnemyJumpState(
                EnemyJumpPhase.Airborne,
                sequence: 4,
                sourceCell,
                targetCell);
            var retryState = CreateEnemyJumpState(
                EnemyJumpPhase.Airborne,
                sequence: 4,
                sourceCell,
                targetCell,
                retryCount: 1);

            var airborneStartPresentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, airborneState)),
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, airborneState)),
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, airborneState)),
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, airborneState)),
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 6,
                    jumpBaselineSnapshot: CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right),
                        },
                        new EnemyJumpStateSeed(enemyId, windupState))));

            var startSignal = airborneStartPresentationData.EnemyJumpSignals.Single();
            Assert.That(startSignal.Phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(startSignal.StartedWindupThisTick, Is.False);
            Assert.That(startSignal.StartedAirborneThisTick, Is.True);
            Assert.That(startSignal.LandedThisTick, Is.False);
            Assert.That(startSignal.RetryThisTick, Is.False);
            Assert.That(startSignal.Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.AirborneStarted));
            Assert.That(startSignal.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(startSignal.LockedTargetCell, Is.EqualTo(targetCell));
            Assert.That(startSignal.PresentationTargetCell, Is.EqualTo(targetCell));
            Assert.That(startSignal.Facing, Is.EqualTo(Direction.Right));
            Assert.That(startSignal.LandingTick, Is.EqualTo(7));
            Assert.That(startSignal.RemainingAirborneTicks, Is.EqualTo(1));
            Assert.That(startSignal.RetryCount, Is.Zero);

            var retryPresentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, retryState)),
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, retryState)),
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, retryState)),
                    CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, retryState)),
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 7,
                    jumpBaselineSnapshot: CreateSnapshotWithEnemyJumpStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                        },
                        new EnemyJumpStateSeed(enemyId, airborneState))));

            var retrySignal = retryPresentationData.EnemyJumpSignals.Single();
            Assert.That(retrySignal.Phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(retrySignal.StartedWindupThisTick, Is.False);
            Assert.That(retrySignal.StartedAirborneThisTick, Is.False);
            Assert.That(retrySignal.LandedThisTick, Is.False);
            Assert.That(retrySignal.RetryThisTick, Is.True);
            Assert.That(retrySignal.Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Retried));
            Assert.That(retrySignal.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(retrySignal.LockedTargetCell, Is.EqualTo(targetCell));
            Assert.That(retrySignal.PresentationTargetCell, Is.EqualTo(targetCell));
            Assert.That(retrySignal.Facing, Is.EqualTo(Direction.Right));
            Assert.That(retrySignal.LandingTick, Is.EqualTo(8));
            Assert.That(retrySignal.RemainingAirborneTicks, Is.EqualTo(1));
            Assert.That(retrySignal.RetryCount, Is.EqualTo(1));
            Assert.That(retryPresentationData.EnemyActionSignals, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForCrushedBoxAndLanded()
        {
            const int enemyId = 40;
            const int boxId = 20;
            const int actionPlanId = 700;
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var airborneState = CreateEnemyJumpState(
                EnemyJumpPhase.Airborne,
                sequence: 5,
                sourceCell,
                targetCell);
            var cooldownState = CreateEnemyJumpState(
                EnemyJumpPhase.Cooldown,
                sequence: 5,
                sourceCell,
                targetCell);
            var preMovementBox = CreateEntity(boxId, EntityType.Box, targetCell, Direction.None);
            preMovementBox.boxCapabilities = BoxCapabilities.JumpCrushable;
            var boxExitMetadata = new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.JumpLanding,
                enemyId,
                actionPlanId,
                contestId: actionPlanId,
                exitCauseHint: TickEntityExitCause.BoxDestroy,
                movementSemanticKind: MovementSemanticKind.JumpLanding,
                presentationTargetCell: targetCell);
            var jumpMetadata = new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.JumpLanding,
                enemyId,
                actionPlanId,
                contestId: actionPlanId,
                movementSemanticKind: MovementSemanticKind.JumpLanding,
                jumpPresentationKind: JumpPresentationKind.CrushedBoxAndLanded,
                presentationTargetCell: targetCell);
            var movementPhaseResult = CreateMovementPhaseResultWithOperations(
                FinalizationOperation.SetBoardPresence(1, boxId, EntityBoardPresence.Detached, boxExitMetadata),
                FinalizationOperation.MarkDestroy(2, boxId, boxExitMetadata),
                FinalizationOperation.MoveEntity(3, enemyId, targetCell, jumpMetadata),
                FinalizationOperation.SetBoardPresence(4, enemyId, EntityBoardPresence.Occupying, jumpMetadata),
                FinalizationOperation.SetEnemyJumpState(5, enemyId, cooldownState, jumpMetadata));

            var preMovementSnapshot = CreateSnapshotWithEnemyJumpStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, sourceCell, EnemyAiMode.Patrol, Direction.Right, EntityBoardPresence.Detached),
                    preMovementBox,
                },
                new EnemyJumpStateSeed(enemyId, airborneState));
            var postMovementSnapshot = CreateSnapshotWithEnemyJumpStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, targetCell, EnemyAiMode.Patrol, Direction.Right),
                },
                new EnemyJumpStateSeed(enemyId, cooldownState));
            var finalSnapshot = CreateSnapshotWithEnemyJumpStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, targetCell, EnemyAiMode.Patrol, Direction.Right),
                },
                new EnemyJumpStateSeed(enemyId, cooldownState));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    finalSnapshot,
                    movementPhaseResult,
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.RemovedEntities(boxId),
                    currentTickIndex: 7,
                    jumpBaselineSnapshot: preMovementSnapshot));

            var jumpSignal = presentationData.EnemyJumpSignals.Single();
            Assert.That(jumpSignal.EntityId, Is.EqualTo(enemyId));
            Assert.That(jumpSignal.Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded));
            Assert.That(jumpSignal.LandedThisTick, Is.True);
            Assert.That(jumpSignal.RetryThisTick, Is.False);
            Assert.That(jumpSignal.PresentationTargetCell, Is.EqualTo(targetCell));

            var exitSignal = presentationData.EntityExitSignals.Single();
            Assert.That(exitSignal.ExitedEntityId, Is.EqualTo(boxId));
            Assert.That(exitSignal.ExitCause, Is.EqualTo(TickEntityExitCause.BoxDestroy));
            Assert.That(exitSignal.SourceActorEntityId, Is.EqualTo(enemyId));
            Assert.That(exitSignal.SourceCell, Is.EqualTo(targetCell));
            Assert.That(exitSignal.Timing, Is.EqualTo(EntityExitPresentationTiming.Immediate));
            Assert.That(exitSignal.EntityType, Is.EqualTo(EntityType.Box));
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationDataBuilder_BuildsEnemyChargeSignal_ForWindupActiveAndRecoverTransitions()
        {
            const int enemyId = 40;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var windupState = CreateEnemyChargeState(
                EnemyChargePhase.Windup,
                sequence: 2,
                lockedDirection: Direction.Right,
                windupEndTick: 6);
            var activeState = CreateEnemyChargeState(
                EnemyChargePhase.Active,
                sequence: 2,
                lockedDirection: Direction.Right,
                windupEndTick: 6);
            var recoverState = CreateEnemyChargeState(
                EnemyChargePhase.Recover,
                sequence: 2,
                lockedDirection: Direction.Right,
                windupEndTick: 6);

            var windupPresentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        }),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, windupState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, windupState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, windupState)),
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 5));

            var windupSignal = windupPresentationData.EnemyChargeSignals.Single();
            Assert.That(windupSignal.EntityId, Is.EqualTo(enemyId));
            Assert.That(windupSignal.Sequence, Is.EqualTo(2));
            Assert.That(windupSignal.Phase, Is.EqualTo(EnemyChargePhase.Windup));
            Assert.That(windupSignal.StartedWindupThisTick, Is.True);
            Assert.That(windupSignal.StartedActiveThisTick, Is.False);
            Assert.That(windupSignal.StartedRecoverThisTick, Is.False);
            Assert.That(windupSignal.LockedDirection, Is.EqualTo(Direction.Right));

            var activePresentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, windupState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, activeState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, activeState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Charge, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, activeState)),
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 6));

            var activeSignal = activePresentationData.EnemyChargeSignals.Single();
            Assert.That(activeSignal.Phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(activeSignal.StartedWindupThisTick, Is.False);
            Assert.That(activeSignal.StartedActiveThisTick, Is.True);
            Assert.That(activeSignal.StartedRecoverThisTick, Is.False);
            Assert.That(activeSignal.LockedDirection, Is.EqualTo(Direction.Right));

            var recoverPresentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Recover, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, activeState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Recover, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, recoverState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Recover, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, recoverState)),
                    CreateSnapshotWithEnemyChargeStates(
                        new[]
                        {
                            CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Recover, Direction.Right),
                        },
                        new EnemyChargeStateSeed(enemyId, recoverState)),
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 7));

            var recoverSignal = recoverPresentationData.EnemyChargeSignals.Single();
            Assert.That(recoverSignal.Phase, Is.EqualTo(EnemyChargePhase.Recover));
            Assert.That(recoverSignal.StartedWindupThisTick, Is.False);
            Assert.That(recoverSignal.StartedActiveThisTick, Is.False);
            Assert.That(recoverSignal.StartedRecoverThisTick, Is.True);
            Assert.That(recoverSignal.LockedDirection, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_Windup_EmitsPositiveLiftHeight()
        {
            const int enemyId = 40;
            var enemyCell = new SurfaceCell(FaceId.Front, 1, 1);
            var windupState = CreateEnemyGlideState(
                EnemyGlidePhase.Windup,
                sequence: 3,
                windupUntilTickExclusive: 10,
                windupTicks: 4);
            var snapshot = CreateSnapshotWithEnemyGlideStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Chase, Direction.Right),
                },
                new EnemyGlideStateSeed(enemyId, windupState));

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    snapshot,
                    snapshot,
                    snapshot,
                    snapshot,
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 8));

            var signal = presentationData.EnemyGlideSignals.Single();
            Assert.That(signal.EntityId, Is.EqualTo(enemyId));
            Assert.That(signal.AnchorCell, Is.EqualTo(enemyCell));
            Assert.That(signal.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
            Assert.That(signal.Sequence, Is.EqualTo(3));
            Assert.That(signal.PhaseElapsedTicks, Is.EqualTo(2));
            Assert.That(signal.PhaseTotalTicks, Is.EqualTo(4));
            Assert.That(signal.NormalizedPhaseProgress, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(signal.LiftHeightUnits, Is.EqualTo(KinematicFixed.UnitsPerCell / 4));
            Assert.That(signal.CurrentHeightUnits, Is.EqualTo(KinematicFixed.UnitsPerCell / 8));
            Assert.That(signal.IsTerminalZero, Is.False);
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_ActiveAndLandingPending_HoldLiftHeight()
        {
            const int enemyId = 40;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var activeState = CreateEnemyGlideState(
                EnemyGlidePhase.Active,
                sequence: 4,
                activeUntilTickExclusive: 12,
                durationTicks: 5);
            var landingPendingState = CreateEnemyGlideState(
                EnemyGlidePhase.LandingPending,
                sequence: 4,
                activeUntilTickExclusive: 12,
                durationTicks: 5,
                landingPendingCell: new SurfaceCell(FaceId.Floor, 2, 1));

            var activeSignal = BuildSingleGlideSignal(enemyId, enemyCell, activeState, currentTickIndex: 9);
            var landingPendingSignal = BuildSingleGlideSignal(enemyId, enemyCell, landingPendingState, currentTickIndex: 12);

            Assert.That(activeSignal.Phase, Is.EqualTo(EnemyGlidePhase.Active));
            Assert.That(activeSignal.CurrentHeightUnits, Is.EqualTo(KinematicFixed.UnitsPerCell / 4));
            Assert.That(activeSignal.IsLandingPending, Is.False);
            Assert.That(landingPendingSignal.Phase, Is.EqualTo(EnemyGlidePhase.LandingPending));
            Assert.That(landingPendingSignal.CurrentHeightUnits, Is.EqualTo(KinematicFixed.UnitsPerCell / 4));
            Assert.That(landingPendingSignal.IsLandingPending, Is.True);
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_Recovery_DescendsAndSupportsDip()
        {
            const int enemyId = 40;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var recoveryState = CreateEnemyGlideState(
                EnemyGlidePhase.Recovery,
                sequence: 5,
                recoveryUntilTickExclusive: 14,
                recoveryTicks: 4);

            var defaultSignal = BuildSingleGlideSignal(enemyId, enemyCell, recoveryState, currentTickIndex: 12);
            var dipSignal = BuildSingleGlideSignal(
                enemyId,
                enemyCell,
                recoveryState,
                currentTickIndex: 12,
                new FixedEnemyGlidePresentationSettingsResolver(
                    new EnemyGlidePresentationSettings(
                        KinematicFixed.UnitsPerCell / 4,
                        KinematicFixed.UnitsPerCell / 16)));

            Assert.That(defaultSignal.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
            Assert.That(defaultSignal.CurrentHeightUnits, Is.EqualTo(KinematicFixed.UnitsPerCell / 8));
            Assert.That(defaultSignal.RecoveryDipHeightUnits, Is.Zero);
            Assert.That(dipSignal.RecoveryDipHeightUnits, Is.EqualTo(KinematicFixed.UnitsPerCell / 16));
            Assert.That(dipSignal.CurrentHeightUnits, Is.EqualTo(-(KinematicFixed.UnitsPerCell / 16)));
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_Clear_DoesNotLeaveStaleHoverSignal()
        {
            const int enemyId = 40;
            var enemyCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var activeState = CreateEnemyGlideState(
                EnemyGlidePhase.Active,
                sequence: 6,
                activeUntilTickExclusive: 12,
                durationTicks: 5);
            var preMovementSnapshot = CreateSnapshotWithEnemyGlideStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Chase, Direction.Right),
                },
                new EnemyGlideStateSeed(enemyId, activeState));
            var finalSnapshot = CreateSnapshotWithEnemyGlideStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Chase, Direction.Right),
                });

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 12));

            var signal = presentationData.EnemyGlideSignals.Single();
            Assert.That(signal.IsTerminalZero, Is.True);
            Assert.That(signal.CurrentHeightUnits, Is.Zero);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot()
        {
            var generalTimingProfile = GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond,
                generalTimingProfile.RepeatedMoveIntervalSeconds);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, timingProfile);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, terrainData);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, terrainData, topology);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            var createSnapshotMethod = typeof(WorldState).GetMethod(
                "CreateSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createSnapshotMethod, Is.Not.Null);

            return (WorldSnapshot)createSnapshotMethod.Invoke(worldState, null);
        }

        private static WorldSnapshot CreateTileFeatureSnapshot(params TileFeatureState[] tileFeatures)
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                tileFeatures);

            return CreateSnapshot(worldState);
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureFlags flags,
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

        private static IWorldWriteContext CreateWriteContext(WorldState worldState)
        {
            var createWriteContextMethod = typeof(WorldState).GetMethod(
                "CreateWriteContext",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createWriteContextMethod, Is.Not.Null);

            return (IWorldWriteContext)createWriteContextMethod.Invoke(worldState, null);
        }

        private static WorldSnapshot CreateSnapshotWithEnemyActionStates(
            IEnumerable<EntityState> initialEntities,
            params EnemyActionStateSeed[] actionStates)
        {
            var worldState = CreateWorldState(initialEntities);
            if (actionStates != null && actionStates.Length > 0)
            {
                var writeContext = CreateWriteContext(worldState);
                for (var i = 0; i < actionStates.Length; i++)
                {
                    writeContext.SetEnemyActionState(actionStates[i].EntityId, actionStates[i].State);
                }
            }

            return CreateSnapshot(worldState);
        }

        private static WorldSnapshot CreateSnapshotWithEnemyJumpStates(
            IEnumerable<EntityState> initialEntities,
            params EnemyJumpStateSeed[] jumpStates)
        {
            var worldState = CreateWorldState(initialEntities);
            if (jumpStates != null && jumpStates.Length > 0)
            {
                var writeContext = CreateWriteContext(worldState);
                for (var i = 0; i < jumpStates.Length; i++)
                {
                    writeContext.SetEnemyJumpState(jumpStates[i].EntityId, jumpStates[i].State);
                }
            }

            return CreateSnapshot(worldState);
        }

        private static WorldSnapshot CreateSnapshotWithEnemyChargeStates(
            IEnumerable<EntityState> initialEntities,
            params EnemyChargeStateSeed[] chargeStates)
        {
            var worldState = CreateWorldState(initialEntities);
            if (chargeStates != null && chargeStates.Length > 0)
            {
                var writeContext = CreateWriteContext(worldState);
                for (var i = 0; i < chargeStates.Length; i++)
                {
                    writeContext.SetEnemyChargeState(chargeStates[i].EntityId, chargeStates[i].State);
                }
            }

            return CreateSnapshot(worldState);
        }

        private static WorldSnapshot CreateSnapshotWithEnemyGlideStates(
            IEnumerable<EntityState> initialEntities,
            params EnemyGlideStateSeed[] glideStates)
        {
            var worldState = CreateWorldState(initialEntities);
            if (glideStates != null && glideStates.Length > 0)
            {
                var writeContext = CreateWriteContext(worldState);
                for (var i = 0; i < glideStates.Length; i++)
                {
                    writeContext.SetEnemyGlideState(glideStates[i].EntityId, glideStates[i].State);
                }
            }

            return CreateSnapshot(worldState);
        }

        private static TickEnemyGlidePresentationSignal BuildSingleGlideSignal(
            int enemyId,
            SurfaceCell enemyCell,
            EnemyGlideRuntimeState glideState,
            int currentTickIndex,
            IEnemyGlidePresentationSettingsResolver settingsResolver = null)
        {
            var snapshot = CreateSnapshotWithEnemyGlideStates(
                new[]
                {
                    CreateEnemyEntity(enemyId, enemyCell, EnemyAiMode.Chase, Direction.Right),
                },
                new EnemyGlideStateSeed(enemyId, glideState));

            return new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    snapshot,
                    snapshot,
                    snapshot,
                    snapshot,
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: currentTickIndex,
                    enemyGlidePresentationSettingsResolver: settingsResolver)).EnemyGlideSignals.Single();
        }

        private static TickPresentationData BuildPresentationDataForFinalSnapshot(
            WorldSnapshot finalSnapshot,
            int gravityFieldChargeDurationTicks = 10,
            int gravityFieldActiveDurationTicks = 5)
        {
            return new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    gravityFieldChargeDurationTicks: gravityFieldChargeDurationTicks,
                    gravityFieldActiveDurationTicks: gravityFieldActiveDurationTicks));
        }

        private static ActionGroup CreateActionGroup(int intentId, int sourceId, int priority, int groupId)
        {
            var actionGroup = new ActionGroup(intentId, sourceId, priority, ActionGroupKind.Move);
            actionGroup.AssignGroupId(groupId);
            return actionGroup;
        }

        private static ActionGroup CreateMoveGroup(
            int groupId,
            int intentId,
            int sourceId,
            int priority,
            params MoveAction[] moves)
        {
            return CreateMovementGroup(groupId, intentId, sourceId, priority, ActionGroupKind.Move, moves);
        }

        private static ActionGroup CreatePushGroup(
            int groupId,
            int intentId,
            int sourceId,
            int priority,
            params MoveAction[] moves)
        {
            return CreateMovementGroup(groupId, intentId, sourceId, priority, ActionGroupKind.Push, moves);
        }

        private static ActionGroup CreateMovementGroup(
            int groupId,
            int intentId,
            int sourceId,
            int priority,
            ActionGroupKind groupKind,
            params MoveAction[] moves)
        {
            var actionGroup = new ActionGroup(intentId, sourceId, priority, groupKind);
            actionGroup.AssignGroupId(groupId);

            for (var i = 0; i < moves.Length; i++)
            {
                actionGroup.Moves.Add(moves[i]);
            }

            return actionGroup;
        }

        private static ActionGroup CreateRotateGroup(
            int groupId,
            int intentId,
            int sourceId,
            int priority,
            SurfaceCell source,
            SurfaceCell destination,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology)
        {
            var actionGroup = CreateMoveGroup(
                groupId,
                intentId,
                sourceId,
                priority,
                new MoveAction(sourceId, source, destination, Direction.Up));
            actionGroup.TopologyChanges.Add(new TopologyChangeAction(rotationKind, updatedTopology));
            return actionGroup;
        }

        private static MovementPhaseResult CreateMovementPhaseResult(params ActionGroup[] selectedGroups)
        {
            return CanonicalPhaseResultFactory.CreateMovementPhaseResult(selectedGroups);
        }

        private static MovementPhaseResult CreateMovementPhaseResult(
            ActionGroup selectedGroup,
            ResolvedActionSemanticKind semanticKind)
        {
            return CanonicalPhaseResultFactory.CreateMovementPhaseResult(
                Array.Empty<RawMovementIntent>(),
                Array.Empty<MoveIntent>(),
                new[] { selectedGroup },
                Array.Empty<string>(),
                Array.Empty<string>(),
                _ => semanticKind);
        }

        private static MovementPhaseResult CreateMovementPhaseResultWithOperations(
            params FinalizationOperation[] operations)
        {
            return new MovementPhaseResult(
                Array.Empty<RawMovementIntent>(),
                Array.Empty<MoveIntent>(),
                Array.Empty<ResolutionRecord>(),
                Array.Empty<ImpactDispositionResolutionRecord>(),
                operations ?? Array.Empty<FinalizationOperation>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static AttackPhaseResult CreateAttackPhaseResult(params ActionGroup[] selectedGroups)
        {
            return CanonicalPhaseResultFactory.CreateAttackPhaseResult(selectedGroups);
        }

        private static AttackPhaseResult CreateAttackPhaseResult(params DamageResolutionRecord[] damageResolutions)
        {
            return new AttackPhaseResult(
                Array.Empty<RawAttackIntent>(),
                Array.Empty<ImpactReservation>(),
                Array.Empty<DelayedAttackEffectRecord>(),
                damageResolutions,
                Array.Empty<ResolutionRecord>(),
                Array.Empty<FinalizationOperation>(),
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static EnemyActionRuntimeState CreateEnemyActionState(
            EnemyActionKind kind,
            int sequence,
            int lockedTargetEntityId,
            Direction direction,
            int startTick,
            int executeTick,
            bool executionAttempted = false)
        {
            return new EnemyActionRuntimeState
            {
                kind = kind,
                sequence = sequence,
                lockedTargetEntityId = lockedTargetEntityId,
                direction = direction,
                startTick = startTick,
                executeTick = executeTick,
                executionAttempted = executionAttempted,
            };
        }

        private static EnemyJumpRuntimeState CreateEnemyJumpState(
            EnemyJumpPhase phase,
            int sequence,
            SurfaceCell sourceCell,
            SurfaceCell lockedTargetCell,
            int retryCount = 0)
        {
            return new EnemyJumpRuntimeState
            {
                phase = phase,
                sequence = sequence,
                sourceCell = sourceCell,
                lockedTargetCell = lockedTargetCell,
                windupEndTick = 5,
                landingTick = 7 + retryCount,
                retryCount = retryCount,
            };
        }

        private static EnemyChargeRuntimeState CreateEnemyChargeState(
            EnemyChargePhase phase,
            int sequence,
            Direction lockedDirection,
            int windupEndTick,
            int remainingActiveSteps = 0,
            int recoverRemainingTicks = 0)
        {
            return new EnemyChargeRuntimeState
            {
                phase = phase,
                sequence = sequence,
                lockedDirection = lockedDirection,
                windupEndTick = windupEndTick,
                remainingActiveSteps = remainingActiveSteps,
                recoverRemainingTicks = recoverRemainingTicks,
            };
        }

        private static EnemyGlideRuntimeState CreateEnemyGlideState(
            EnemyGlidePhase phase,
            int sequence,
            int windupUntilTickExclusive = 0,
            int activeUntilTickExclusive = 0,
            int recoveryUntilTickExclusive = 0,
            int cooldownUntilTickExclusive = 0,
            int windupTicks = 0,
            int durationTicks = 0,
            int recoveryTicks = 0,
            int cooldownTicks = 0,
            int lastExitedTick = 0,
            SurfaceCell landingPendingCell = default)
        {
            return EnemyGlideRuntimeState.Create(
                phase,
                sequence,
                windupUntilTickExclusive,
                activeUntilTickExclusive,
                recoveryUntilTickExclusive,
                cooldownUntilTickExclusive,
                windupTicks,
                durationTicks,
                recoveryTicks,
                cooldownTicks,
                lastExitedTick,
                landingPendingCell);
        }

        private static EntityState CreateEnemyEntity(
            int entityId,
            SurfaceCell position,
            EnemyAiMode aiMode,
            Direction facing,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            var entity = CreateEntity(entityId, EntityType.Unit, position, facing, boardPresence);
            entity.aiMode = aiMode;
            entity.teamId = 2;
            entity.unitRole = UnitRole.Enemy;
            return entity;
        }

        private static EntityState CreateEntity(
            int entityId,
            EntityType entityType,
            SurfaceCell position,
            Direction facing,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            bool markedForDeath = false)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = entityType,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
            };
        }

        private static EntityState CreateGravityFieldBox(
            int entityId,
            SurfaceCell position,
            GravityFieldPhase phase,
            int timerTicks)
        {
            var entity = CreateEntity(entityId, EntityType.Box, position, Direction.None);
            entity.boxArchetype = BoxArchetype.GravityField;
            entity.gravityFieldPhase = phase;
            entity.gravityFieldTimerTicks = timerTicks;
            return entity;
        }

        private static int BuildExpectedPresentationSeed(
            int currentTickIndex,
            int exitedEntityId,
            int sourceActorEntityId,
            TickEntityExitCause exitCause)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = (hash ^ (uint)currentTickIndex) * 16777619u;
                hash = (hash ^ (uint)exitedEntityId) * 16777619u;
                hash = (hash ^ (uint)sourceActorEntityId) * 16777619u;
                hash = (hash ^ (uint)exitCause) * 16777619u;
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static int BuildExpectedImpactPresentationSeed(
            int currentTickIndex,
            int sourceEntityId,
            int targetEntityId)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = (hash ^ (uint)currentTickIndex) * 16777619u;
                hash = (hash ^ (uint)sourceEntityId) * 16777619u;
                hash = (hash ^ (uint)targetEntityId) * 16777619u;
                hash = (hash ^ (uint)TickEntityExitCause.DestroyedByImpact) * 16777619u;
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private readonly struct EnemyActionStateSeed
        {
            public EnemyActionStateSeed(int entityId, EnemyActionRuntimeState state)
            {
                EntityId = entityId;
                State = state;
            }

            public int EntityId { get; }

            public EnemyActionRuntimeState State { get; }
        }

        private readonly struct EnemyJumpStateSeed
        {
            public EnemyJumpStateSeed(int entityId, EnemyJumpRuntimeState state)
            {
                EntityId = entityId;
                State = state;
            }

            public int EntityId { get; }

            public EnemyJumpRuntimeState State { get; }
        }

        private readonly struct EnemyChargeStateSeed
        {
            public EnemyChargeStateSeed(int entityId, EnemyChargeRuntimeState state)
            {
                EntityId = entityId;
                State = state;
            }

            public int EntityId { get; }

            public EnemyChargeRuntimeState State { get; }
        }

        private readonly struct EnemyGlideStateSeed
        {
            public EnemyGlideStateSeed(int entityId, EnemyGlideRuntimeState state)
            {
                EntityId = entityId;
                State = state;
            }

            public int EntityId { get; }

            public EnemyGlideRuntimeState State { get; }
        }

        private sealed class FixedEnemyGlidePresentationSettingsResolver : IEnemyGlidePresentationSettingsResolver
        {
            private readonly EnemyGlidePresentationSettings _settings;

            public FixedEnemyGlidePresentationSettingsResolver(EnemyGlidePresentationSettings settings)
            {
                _settings = settings;
            }

            public bool TryResolveEnemyGlidePresentationSettings(
                WorldSnapshot snapshot,
                in EntityState entity,
                out EnemyGlidePresentationSettings settings)
            {
                settings = _settings;
                return true;
            }
        }

        private sealed class StubEntityLogic : IMovementEntityLogic, IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawAttackIntent? _attackIntent;
            private readonly RawMovementIntent? _movementIntent;

            public StubEntityLogic(RawMovementIntent? movementIntent, RawAttackIntent? attackIntent)
            {
                _movementIntent = movementIntent;
                _attackIntent = attackIntent;
            }

            public int ControlledEntityId => _movementIntent?.SourceId ?? _attackIntent?.SourceId ?? 0;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntent.HasValue)
                {
                    buffer.Add(_movementIntent.Value);
                }
            }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (_attackIntent.HasValue)
                {
                    buffer.Add(_attackIntent.Value);
                }
            }
        }

        private sealed class StubEntityLogicProvider : ISnapshotEntityLogicProvider
        {
            private readonly IReadOnlyList<IEntityLogic> _dynamicEntityLogics;

            public StubEntityLogicProvider(params IEntityLogic[] dynamicEntityLogics)
            {
                _dynamicEntityLogics = dynamicEntityLogics;
            }

            public EntityLogicSet Build(
                WorldSnapshot snapshot,
                IReadOnlyList<IEntityLogic> staticEntityLogics)
            {
                var preMovementStateLogics = new List<IPreMovementStateLogic>(staticEntityLogics.Count + _dynamicEntityLogics.Count);
                var aiStateLogics = new List<IEnemyAiStateLogic>(staticEntityLogics.Count + _dynamicEntityLogics.Count);
                var movementLogics = new List<IMovementEntityLogic>(staticEntityLogics.Count + _dynamicEntityLogics.Count);
                var attackLogics = new List<IAttackEntityLogic>(staticEntityLogics.Count + _dynamicEntityLogics.Count);

                for (var i = 0; i < staticEntityLogics.Count; i++)
                {
                    AddEntityLogic(staticEntityLogics[i], preMovementStateLogics, aiStateLogics, movementLogics, attackLogics);
                }

                for (var i = 0; i < _dynamicEntityLogics.Count; i++)
                {
                    AddEntityLogic(_dynamicEntityLogics[i], preMovementStateLogics, aiStateLogics, movementLogics, attackLogics);
                }

                return new EntityLogicSet(preMovementStateLogics, aiStateLogics, movementLogics, attackLogics);
            }

            private static void AddEntityLogic(
                IEntityLogic entityLogic,
                List<IPreMovementStateLogic> preMovementStateLogics,
                List<IEnemyAiStateLogic> aiStateLogics,
                List<IMovementEntityLogic> movementLogics,
                List<IAttackEntityLogic> attackLogics)
            {
                if (entityLogic is IPreMovementStateLogic preMovementStateLogic)
                {
                    preMovementStateLogics.Add(preMovementStateLogic);
                }

                if (entityLogic is IEnemyAiStateLogic aiStateLogic)
                {
                    aiStateLogics.Add(aiStateLogic);
                }

                if (entityLogic is IMovementEntityLogic movementLogic)
                {
                    movementLogics.Add(movementLogic);
                }

                if (entityLogic is IAttackEntityLogic attackLogic)
                {
                    attackLogics.Add(attackLogic);
                }
            }
        }

        private sealed class SyntheticMovementIntent : Intent
        {
            private readonly int _tieBreak;

            public SyntheticMovementIntent(int sourceId, int priority, int tieBreak)
                : base(sourceId, priority, TickPhase.Plan)
            {
                _tieBreak = tieBreak;
            }

            protected internal override int GetTypeSortKey()
            {
                return 1;
            }

            protected internal override int CompareSameType(Intent other)
            {
                return _tieBreak.CompareTo(((SyntheticMovementIntent)other)._tieBreak);
            }
        }
    }
}
