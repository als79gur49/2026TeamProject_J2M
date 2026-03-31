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
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineStageOneTests
    {
        [Test]
        public void IdAllocator_ResetForTick_RestartsCategorySequences()
        {
            var allocator = new IdAllocator();

            allocator.ResetForTick(3);

            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(2));
            Assert.That(allocator.AllocateGroupId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateSpawnId(), Is.EqualTo(1));

            allocator.ResetForTick(4);

            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateGroupId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateSpawnId(), Is.EqualTo(1));
        }

        [Test]
        public void RunTick_SortsRawIntents_AndAssignsCentralIntentIds()
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
                    entityId = 10,
                    position = new Vector2Int(0, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
            });
            var entityLogics = new IEntityLogic[]
            {
                new StubEntityLogic(new RawMovementIntent(20, 10, new Vector2Int(3, 0)), RawAttackIntent.CreateFireProjectile(20, 10)),
                new StubEntityLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0)), RawAttackIntent.CreateFireProjectile(10, 5)),
            };
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, entityLogics);

            var result = pipeline.RunTick(new TickInput(12));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, IntentId: 1),
                    (SourceId: 10, IntentId: 2),
                },
                result.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, IntentId: 3),
                    (SourceId: 10, IntentId: 4),
                },
                result.AttackPhaseResult.SortedInputs.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
        }

        [Test]
        public void RunTick_AttackPhase_CollectsOnlyAliveEntityIntents()
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
                    entityId = 20,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
            });
            var entityLogics = new IEntityLogic[]
            {
                new StubEntityLogic(null, RawAttackIntent.CreateFireProjectile(20, 10)),
                new StubEntityLogic(null, RawAttackIntent.CreateFireProjectile(10, 5)),
                new StubEntityLogic(null, RawAttackIntent.CreateFireProjectile(30, 1)),
            };
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, entityLogics);

            var result = pipeline.RunTick(new TickInput(13));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1),
                },
                result.AttackPhaseResult.SortedInputs.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
        }

        [Test]
        public void RunTick_UsesInjectedEntityLogicProvider()
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
            });
            var pipeline = new TickPipeline(
                worldState,
                new IEntityLogic[] { },
                new StubEntityLogicProvider(
                    new StubEntityLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0)), null)));

            var result = pipeline.RunTick(new TickInput(3));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0)),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
        }

        [Test]
        public void TickInputBuffer_RecordRejectsDuplicateTick()
        {
            var inputBuffer = new TickInputBuffer();

            inputBuffer.Record(new TickInput(3));

            Assert.That(
                () => inputBuffer.Record(new TickInput(3)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TickInputBuffer_ConsumeOrDefault_ReturnsRecordedInputOrDefaultTick()
        {
            var inputBuffer = new TickInputBuffer();

            inputBuffer.Record(new TickInput(5));

            Assert.That(inputBuffer.HasBufferedInput(5), Is.True);
            Assert.That(inputBuffer.ConsumeOrDefault(5).TickIndex, Is.EqualTo(5));
            Assert.That(inputBuffer.HasBufferedInput(5), Is.False);
            Assert.That(inputBuffer.ConsumeOrDefault(6).TickIndex, Is.EqualTo(6));
        }

        [Test]
        public void TickRunner_RunNextTick_ConsumesBufferedInputAndAdvancesIndex()
        {
            var inputBuffer = new TickInputBuffer();
            var runner = new TickRunner(
                GameplayCompositionRoot.CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>())),
                inputBuffer,
                startTickIndex: 4);

            inputBuffer.Record(new TickInput(4));

            var result = runner.RunNextTick();

            Assert.That(result.TickIndex, Is.EqualTo(4));
            Assert.That(runner.NextTickIndex, Is.EqualTo(5));
            Assert.That(inputBuffer.HasBufferedInput(4), Is.False);
        }

        [Test]
        public void TickRunner_RunTick_RejectsOutOfOrderTickIndex()
        {
            var runner = new TickRunner(
                GameplayCompositionRoot.CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>())),
                new TickInputBuffer(),
                startTickIndex: 3);

            Assert.That(
                () => runner.RunTick(new TickInput(4)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(runner.NextTickIndex, Is.EqualTo(3));
        }

        [Test]
        public void GameplayCompositionRoot_CreateTickRunner_UsesDefaultProviderWithProjectileCadence()
        {
            var worldState = CreateWorldState(new[]
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
            });
            var runner = GameplayCompositionRoot.CreateTickRunner(worldState, new TickInputBuffer());
            var timingProfile = GameplayTimingProfile.CreateDefault();

            var firstResult = runner.RunNextTick();

            Assert.That(firstResult.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(firstResult.FinalEntities.Single().position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));

            for (var tick = 2; tick <= timingProfile.ProjectileStepIntervalTicks; tick++)
            {
                runner.RunNextTick();
            }

            var moveResult = runner.RunNextTick();

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0)),
                },
                moveResult.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.Destination))
                    .ToArray());
            Assert.That(moveResult.FinalEntities.Single().position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(runner.NextTickIndex, Is.EqualTo(timingProfile.ProjectileStepIntervalTicks + 2));
        }

        [Test]
        public void GameplayBootstrapper_CreateTickRunner_PreservesPreExistingProjectileCadence()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 20,
                    position = new Vector2Int(2, 1),
                    hp = 1,
                    maxHp = 1,
                    teamId = 2,
                    type = EntityType.Projectile,
                    facing = Direction.Left,
                },
            });
            var bootstrapper = GameplayCompositionRoot.CreateDefaultBootstrapper();
            var inputBuffer = new TickInputBuffer();
            var timingProfile = GameplayTimingProfile.CreateDefault();

            inputBuffer.Record(new TickInput(7));

            var runner = bootstrapper.CreateTickRunner(
                worldState,
                Array.Empty<IEntityLogic>(),
                inputBuffer,
                startTickIndex: 7);
            var firstResult = runner.RunNextTick();

            Assert.That(firstResult.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(firstResult.FinalEntities.Single().position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 1)));

            for (var tick = 8; tick <= (7 + timingProfile.ProjectileStepIntervalTicks - 1); tick++)
            {
                runner.RunNextTick();
            }

            var moveResult = runner.RunNextTick();

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, Destination: new Vector2Int(1, 1)),
                },
                moveResult.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.Destination))
                    .ToArray());
            Assert.That(moveResult.FinalEntities.Single().position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(runner.NextTickIndex, Is.EqualTo(7 + timingProfile.ProjectileStepIntervalTicks + 1));
            Assert.That(inputBuffer.HasBufferedInput(7), Is.False);
        }

        [Test]
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
        public void RunTick_UsesAuthoritativeProjectileStateTimerWithoutSessionStartMutation()
        {
            var worldState = GameplayCompositionRoot.CreateWorldState(
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
                new BoardBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                GameplayTimingProfile.CreateDefault());

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0)) },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            Assert.That(result.FinalEntities.Single().position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
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
            var orderedProjectiles = new List<SnapshotOccupancyEntry>();

            snapshot.EnumerateUnitOccupancyOrdered(orderedUnits);
            snapshot.EnumerateProjectileOccupancyOrdered(orderedProjectiles);

            CollectionAssert.AreEqual(
                new[]
                {
                    (X: 0, Y: 2, EntityId: 10),
                    (X: 3, Y: 1, EntityId: 30),
                },
                orderedUnits.Select(entry => (entry.Cell.x, entry.Cell.y, entry.EntityId)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (X: 1, Y: 1, EntityId: 20),
                    (X: 2, Y: 1, EntityId: 40),
                },
                orderedProjectiles.Select(entry => (entry.Cell.x, entry.Cell.y, entry.EntityId)).ToArray());
        }

        [Test]
        public void WorldSnapshot_BlocksMovementUntilCleanupEvenWhenEntityIsMarkedForDeath()
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

            Assert.That(snapshot.IsBlockedForUnit(new Vector2Int(1, 0)), Is.True);
            Assert.That(snapshot.BlocksMovement(10), Is.True);
        }

        [Test]
        public void WorldSnapshot_IsBlockedForUnit_ConsidersBoardBoundsTerrainAndIgnoresProjectileLayer()
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
            Assert.That(snapshot.IsBlockedForUnit(new Vector2Int(-1, 0)), Is.True);
            Assert.That(snapshot.IsBlockedForUnit(new Vector2Int(0, 1)), Is.True);
            Assert.That(snapshot.IsBlockedForUnit(new Vector2Int(1, 0)), Is.True);
            Assert.That(snapshot.IsBlockedForUnit(new Vector2Int(2, 0)), Is.False);
            Assert.That(snapshot.IsBlockedForUnit(new Vector2Int(3, 0)), Is.True);
        }

        [Test]
        public void WorldSnapshot_TryGetPlacementBlocker_AppliesGameplayFaceAndBoardPresencePolicy()
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
                snapshot.TryGetPlacementBlocker(EntityType.Unit, new SurfaceCell(FaceId.Floor, 3, 0), ignoredEntityId: 0, out var blocker),
                Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(blocker.EntityId, Is.EqualTo(30));
        }

        [Test]
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

            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(1, 0), out var deathMarkedUnit), Is.True);
            Assert.That(deathMarkedUnit.entityId, Is.EqualTo(10));
            Assert.That(snapshot.TryGetProjectileAt(new Vector2Int(2, 0), out var projectile), Is.True);
            Assert.That(projectile.entityId, Is.EqualTo(20));

            CreateWriteContext(worldState).MoveEntity(30, new Vector2Int(5, 0));

            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(3, 0), out var originalUnit), Is.True);
            Assert.That(originalUnit.entityId, Is.EqualTo(30));
            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(5, 0), out _), Is.False);
        }

        [Test]
        public void PhaseTransientBuffer_DrainImpacts_ReturnsDeterministicOrder_AndClearsBuffer()
        {
            var transientBuffer = new PhaseTransientBuffer();
            transientBuffer.AddImpact(new ImpactReservation(2, 20, new Vector2Int(1, 0), 1, 5, 2, 3));
            transientBuffer.AddImpact(new ImpactReservation(1, 30, new Vector2Int(0, 0), 1, 5, 2, 2));
            transientBuffer.AddImpact(new ImpactReservation(1, 10, new Vector2Int(0, 0), 1, 5, 1, 1));

            var drainedImpacts = transientBuffer.DrainImpacts();

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, GroupId: 1, Sequence: 1),
                    (SourceId: 1, GroupId: 2, Sequence: 2),
                    (SourceId: 2, GroupId: 2, Sequence: 3),
                },
                drainedImpacts
                    .Select(impact => (impact.SourceId, GroupId: impact.SourceActionGroupId, Sequence: impact.ReservationSequence))
                    .ToArray());
            Assert.That(transientBuffer.DrainImpacts(), Is.Empty);
        }

        [Test]
        public void DelayedEventQueue_OrderIsDeterministic()
        {
            var queue = new DelayedAttackEffectQueue();
            queue.Enqueue(new DelayedAttackEffectRecord(2, 40, 1, 5, 3, 4, 2, 3));
            queue.Enqueue(new DelayedAttackEffectRecord(1, 30, 1, 5, 3, 5, 3, 1));
            queue.Enqueue(new DelayedAttackEffectRecord(1, 20, 1, 5, 3, 4, 2, 2));
            queue.Enqueue(new DelayedAttackEffectRecord(1, 10, 1, 5, 3, 4, 1, 1));

            var drainedEffects = queue.Drain(4);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, GroupId: 1, Sequence: 1, TargetId: 10),
                    (SourceId: 1, GroupId: 2, Sequence: 2, TargetId: 20),
                    (SourceId: 2, GroupId: 2, Sequence: 3, TargetId: 40),
                },
                drainedEffects
                    .Select(effect => (effect.SourceId, GroupId: effect.SourceActionGroupId, Sequence: effect.EffectSequence, effect.TargetId))
                    .ToArray());
            Assert.That(queue.Drain(4), Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, GroupId: 3, Sequence: 1, TargetId: 30),
                },
                queue.Drain(5)
                    .Select(effect => (effect.SourceId, GroupId: effect.SourceActionGroupId, Sequence: effect.EffectSequence, effect.TargetId))
                    .ToArray());
        }

        [Test]
        public void AttackInputComparer_SortsBySourceKindAndLocalSequence()
        {
            var impactIntent = AttackIntent.FromImpactReservation(
                new ImpactReservation(1, 10, new Vector2Int(0, 0), 1, 5, 1, 2));
            var entityIntentSameSource = new AttackIntent(1, 99, 11);
            var laterEntityIntent = new AttackIntent(2, 1, 22);

            var sortedInputs = new List<AttackIntent>
            {
                impactIntent,
                laterEntityIntent,
                entityIntentSameSource,
            };

            sortedInputs.Sort(AttackInputComparer.Instance);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, Kind: AttackInputKind.EntityIntent, Sequence: 0),
                    (SourceId: 2, Kind: AttackInputKind.EntityIntent, Sequence: 0),
                    (SourceId: 1, Kind: AttackInputKind.ImpactReservation, Sequence: 2),
                },
                sortedInputs.Select(intent => (intent.SourceId, intent.InputKind, intent.LocalSequence)).ToArray());
        }

        [Test]
        public void IntentComparer_ProvidesTotalOrder()
        {
            var first = new MoveIntent(1, 10);
            var second = new MoveIntent(1, 10);
            var third = new AttackIntent(1, 10, 20);
            first.AssignIntentId(1);
            second.AssignIntentId(2);
            third.AssignIntentId(3);

            var sortedIntents = new List<Intent>
            {
                third,
                second,
                first,
            };

            sortedIntents.Sort(IntentComparer.Instance);

            CollectionAssert.AreEqual(
                new Intent[] { first, second, third },
                sortedIntents);
        }

        [Test]
        public void IntentComparer_SortsNewIntentType_WithoutComparerChanges()
        {
            var first = new MoveIntent(1, 10, new Vector2Int(0, 1));
            var second = new SyntheticMovementIntent(1, 10, tieBreak: 0);
            var third = new SyntheticMovementIntent(1, 10, tieBreak: 1);
            first.AssignIntentId(10);
            second.AssignIntentId(30);
            third.AssignIntentId(20);

            var sortedIntents = new List<Intent>
            {
                third,
                first,
                second,
            };

            sortedIntents.Sort(IntentComparer.Instance);

            CollectionAssert.AreEqual(
                new Intent[] { second, third, first },
                sortedIntents);
        }

        [Test]
        public void ActionGroupComparer_ProvidesTotalOrder()
        {
            var second = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 2);
            var first = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 1);
            var third = CreateActionGroup(intentId: 1, sourceId: 2, priority: 10, groupId: 1);

            var sortedGroups = new List<ActionGroup>
            {
                third,
                second,
                first,
            };

            sortedGroups.Sort(ActionGroupComparer.Instance);

            CollectionAssert.AreEqual(
                new[] { first, second, third },
                sortedGroups);
        }

        [Test]
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
                    CreateMovementPhaseResult(actionGroup),
                    AttackPhaseResult.Empty,
                    CleanupPhaseResult.Empty));

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
                    CreateMovementPhaseResult(actionGroup),
                    AttackPhaseResult.Empty,
                    CleanupPhaseResult.Empty));

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
                    new CleanupPhaseResult(new[] { 20 }, Array.Empty<string>(), Array.Empty<string>())));

            Assert.That(presentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(presentationData.TopologyMotion.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(presentationData.TopologyMotion.Value.SourceTopology, Is.EqualTo(initialTopology));
            Assert.That(presentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(rotatedTopology));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 20, Kind: TickVisibilityChangeKind.Detach),
                    (EntityId: 30, Kind: TickVisibilityChangeKind.Spawn),
                    (EntityId: 20, Kind: TickVisibilityChangeKind.Remove),
                },
                presentationData.VisibilityChanges
                    .Select(change => (change.EntityId, change.ChangeKind))
                    .ToArray());
            Assert.That(presentationData.TransitionVisibilityChanges, Is.Empty);
        }

        [Test]
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
                    CleanupPhaseResult.Empty));

            CollectionAssert.AreEqual(
                new[]
                {
                    (
                        EntityId: 20,
                        Mode: TickTransitionVisibilityMode.RetainUntilTransitionComplete,
                        Cell: retainedCell,
                        Topology: initialTopology,
                        Facing: Direction.Left),
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

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
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

        private static IWorldWriteContext CreateWriteContext(WorldState worldState)
        {
            var createWriteContextMethod = typeof(WorldState).GetMethod(
                "CreateWriteContext",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createWriteContextMethod, Is.Not.Null);

            return (IWorldWriteContext)createWriteContextMethod.Invoke(worldState, null);
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
            var actionGroup = new ActionGroup(intentId, sourceId, priority, ActionGroupKind.Move);
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
            return new MovementPhaseResult(
                Array.Empty<RawMovementIntent>(),
                Array.Empty<MoveIntent>(),
                selectedGroups,
                selectedGroups,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static AttackPhaseResult CreateAttackPhaseResult(params ActionGroup[] selectedGroups)
        {
            return new AttackPhaseResult(
                Array.Empty<RawAttackIntent>(),
                Array.Empty<ImpactReservation>(),
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<AttackIntent>(),
                selectedGroups,
                selectedGroups,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
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
                var movementLogics = new List<IMovementEntityLogic>(staticEntityLogics.Count + _dynamicEntityLogics.Count);
                var attackLogics = new List<IAttackEntityLogic>(staticEntityLogics.Count + _dynamicEntityLogics.Count);

                for (var i = 0; i < staticEntityLogics.Count; i++)
                {
                    AddEntityLogic(staticEntityLogics[i], movementLogics, attackLogics);
                }

                for (var i = 0; i < _dynamicEntityLogics.Count; i++)
                {
                    AddEntityLogic(_dynamicEntityLogics[i], movementLogics, attackLogics);
                }

                return new EntityLogicSet(movementLogics, attackLogics);
            }

            private static void AddEntityLogic(
                IEntityLogic entityLogic,
                List<IMovementEntityLogic> movementLogics,
                List<IAttackEntityLogic> attackLogics)
            {
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
                : base(sourceId, priority, TickPhase.Movement)
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
