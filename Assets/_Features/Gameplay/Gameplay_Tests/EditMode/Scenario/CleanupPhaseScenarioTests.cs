using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class CleanupPhaseScenarioTests
    {
        [Test]
        public void Cleanup_MarkedForDeathOccupyingEntity_RemainsPresentUntilCleanupThenIsRemoved()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 2, markedForDeath: true),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            var beforeSnapshot = CreateSnapshot(worldState);

            Assert.That(beforeSnapshot.IsBlockedForUnit(new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);
            Assert.That(beforeSnapshot.TryGetEntity(10, out var entityBefore), Is.True);
            Assert.That(entityBefore.markedForDeath, Is.True);

            var result = pipeline.RunTick(new TickInput(5));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(afterSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(afterSnapshot.IsBlockedForUnit(new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);
        }

        [Test]
        public void Cleanup_DetachedEntityWithoutDestroyMark_RemainsDetachedAndSurvivesCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    hp: 2,
                    boardPresence: EntityBoardPresence.Detached),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            var beforeSnapshot = CreateSnapshot(worldState);

            Assert.That(beforeSnapshot.TryGetEntity(10, out var entityBefore), Is.True);
            Assert.That(entityBefore.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(beforeSnapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
            Assert.That(beforeSnapshot.IsBlockedForUnit(new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);

            var result = pipeline.RunTick(new TickInput(6));
            var afterSnapshot = CreateSnapshot(worldState);

            Assert.That(result.CleanupPhaseResult.RemovedEntityIds, Is.Empty);
            Assert.That(afterSnapshot.TryGetEntity(10, out var entityAfter), Is.True);
            Assert.That(entityAfter.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(afterSnapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
            Assert.That(afterSnapshot.IsBlockedForUnit(new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);
        }

        [Test]
        public void Cleanup_HpZeroEntity_IsRemovedInCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 2, 0), hp: 0),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(7));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(afterSnapshot.TryGetEntity(10, out _), Is.False);
        }

        [Test]
        public void Respawn_PlayerRemovedInCleanup_RespawnsNextTickAtInitialSpawnWithFullHp()
        {
            var spawnCell = new SurfaceCell(FaceId.Front, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayerUnit(entityId: 10, position: spawnCell, hp: 4, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            worldState.CreateWriteContext().ApplyDamage(10, amount: 4);

            var deathTick = pipeline.RunTick(new TickInput(20));
            var afterDeathSnapshot = CreateSnapshot(worldState);
            var respawnTick = pipeline.RunTick(new TickInput(21));
            var afterRespawnSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, deathTick.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(deathTick.EventLog, Has.None.EqualTo("RespawnCommitted|E=10|Pos=(2,1)|Face=Front|Facing=Left|Tick=20"));
            Assert.That(afterDeathSnapshot.TryGetEntity(10, out _), Is.False);

            Assert.That(respawnTick.EventLog, Does.Contain("RespawnCommitted|E=10|Pos=(2,1)|Face=Front|Facing=Left|Tick=21"));
            Assert.That(respawnTick.PresentationData.VisibilityChanges.Any(change =>
                change.EntityId == 10 &&
                change.ChangeKind == TickVisibilityChangeKind.Spawn), Is.True);
            Assert.That(afterRespawnSnapshot.TryGetEntity(10, out var respawnedPlayer), Is.True);
            Assert.That(respawnedPlayer.entityId, Is.EqualTo(10));
            Assert.That(respawnedPlayer.position, Is.EqualTo(spawnCell));
            Assert.That(respawnedPlayer.facing, Is.EqualTo(Direction.Left));
            Assert.That(respawnedPlayer.hp, Is.EqualTo(4));
            Assert.That(respawnedPlayer.maxHp, Is.EqualTo(4));
            Assert.That(respawnedPlayer.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(respawnedPlayer.stateTimer, Is.Zero);
            Assert.That(respawnedPlayer.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(respawnedPlayer.markedForDeath, Is.False);
            Assert.That(respawnedPlayer.spawnTick, Is.EqualTo(21));
            Assert.That(respawnedPlayer.kineticInstigatorEntityId, Is.Zero);
            Assert.That(respawnedPlayer.kineticInstigatorTeamId, Is.Zero);
        }

        [Test]
        public void Respawn_ConfiguredDelay_WaitsEligibleTickBeforeRespawning()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayerUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 1), hp: 5),
            });
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                playerControlTiming,
                playerRespawnDelayTicks: 3);

            worldState.CreateWriteContext().ApplyDamage(10, amount: 5);

            var deathTick = pipeline.RunTick(new TickInput(50));
            var waitingTickOne = pipeline.RunTick(new TickInput(51));
            var waitingTickTwo = pipeline.RunTick(new TickInput(52));
            var respawnTick = pipeline.RunTick(new TickInput(53));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(deathTick.CleanupPhaseResult.RemovedEntityIds, Has.Member(10));
            Assert.That(waitingTickOne.EventLog, Has.None.StartWith("RespawnCommitted|E=10|"));
            Assert.That(waitingTickTwo.EventLog, Has.None.StartWith("RespawnCommitted|E=10|"));
            Assert.That(respawnTick.EventLog, Does.Contain("RespawnCommitted|E=10|Pos=(1,1)|Face=Floor|Facing=Right|Tick=53"));
            Assert.That(finalSnapshot.TryGetEntity(10, out var respawnedPlayer), Is.True);
            Assert.That(respawnedPlayer.spawnTick, Is.EqualTo(53));
        }

        [Test]
        public void Respawn_SolidBlockerAtInitialSpawn_SkipsUntilBlockerIsRemoved()
        {
            var spawnCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayerUnit(entityId: 10, position: spawnCell, hp: 3),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            worldState.CreateWriteContext().ApplyDamage(10, amount: 3);
            var deathTick = pipeline.RunTick(new TickInput(30));
            worldState.CreateWriteContext().SpawnEntity(CreateWall(entityId: 90, position: spawnCell));

            var blockedRespawnTick = pipeline.RunTick(new TickInput(31));
            var blockedSnapshot = CreateSnapshot(worldState);

            Assert.That(deathTick.CleanupPhaseResult.RemovedEntityIds, Has.Member(10));
            Assert.That(blockedRespawnTick.EventLog, Does.Contain("RespawnSkipped|E=10|Pos=(0,0)|Face=Floor|Tick=31|Reason=Entity|BlockerEntity=90|BlockerType=None"));
            Assert.That(blockedSnapshot.TryGetEntity(10, out _), Is.False);

            worldState.CreateWriteContext().RemoveEntity(90);

            var successfulRespawnTick = pipeline.RunTick(new TickInput(32));
            var successfulSnapshot = CreateSnapshot(worldState);

            Assert.That(successfulRespawnTick.EventLog, Does.Contain("RespawnCommitted|E=10|Pos=(0,0)|Face=Floor|Facing=Right|Tick=32"));
            Assert.That(successfulSnapshot.TryGetEntity(10, out var respawnedPlayer), Is.True);
            Assert.That(respawnedPlayer.spawnTick, Is.EqualTo(32));
            Assert.That(respawnedPlayer.hp, Is.EqualTo(3));
        }

        [Test]
        public void Respawn_PlayerControlState_IsResetWhenPlayerReturns()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayerUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    moveCooldownTicks = 9,
                    nextMoveAllowedTick = 42,
                    pushContactTicks = 3,
                    pushTargetEntityId = 99,
                    pushDirection = Direction.Left,
                    actionSequenceCounter = 7,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Flip,
                        sequence = 7,
                        direction = Direction.Left,
                        targetEntityId = 99,
                        startTick = 10,
                        executeTick = 12,
                        recoveryEndTick = 20,
                        executionAttempted = true,
                    },
                });

            worldState.CreateWriteContext().ApplyDamage(10, amount: 3);
            pipeline.RunTick(new TickInput(40));
            pipeline.RunTick(new TickInput(41));

            var snapshotAfterRespawn = CreateSnapshot(worldState);

            Assert.That(snapshotAfterRespawn.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.moveCooldownTicks, Is.Zero);
            Assert.That(controlState.nextMoveAllowedTick, Is.Zero);
            Assert.That(controlState.pushContactTicks, Is.Zero);
            Assert.That(controlState.pushTargetEntityId, Is.Zero);
            Assert.That(controlState.pushDirection, Is.EqualTo(Direction.None));
            Assert.That(controlState.actionSequenceCounter, Is.Zero);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.activeAction.sequence, Is.Zero);
            Assert.That(controlState.activeAction.direction, Is.EqualTo(Direction.None));
            Assert.That(controlState.activeAction.targetEntityId, Is.Zero);
            Assert.That(controlState.activeAction.startTick, Is.Zero);
            Assert.That(controlState.activeAction.executeTick, Is.Zero);
            Assert.That(controlState.activeAction.recoveryEndTick, Is.Zero);
            Assert.That(controlState.activeAction.executionAttempted, Is.False);
        }

        [Test]
        public void Cleanup_RemovalClearsOccupancy()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 3, 1), hp: 1, markedForDeath: true),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            pipeline.RunTick(new TickInput(8));

            var afterSnapshot = CreateSnapshot(worldState);
            Assert.That(afterSnapshot.IsBlockedForUnit(new SurfaceCell(FaceId.Floor, 3, 1)), Is.False);
            Assert.That(afterSnapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 3, 1), out _), Is.False);
        }

        [Test]
        public void Cleanup_SpawnedThisTick_DoesNotTickTimer()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 2,
                    spawnTick: 11),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(11));
            var afterSnapshot = CreateSnapshot(worldState);

            Assert.That(result.CleanupPhaseResult.TimerChanges, Is.Empty);
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(2));
            Assert.That(GetEntityState(afterSnapshot, 10).state, Is.EqualTo(EntityPhaseState.Cooldown));
        }

        [Test]
        public void Cleanup_StateTimer_DecrementsOnlyForSurvivors()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 2, 0),
                    hp: 0,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 3,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 3,
                    spawnTick: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(12));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 20 }, result.CleanupPhaseResult.RemovedEntityIds);
            CollectionAssert.AreEqual(
                new[]
                {
                    "TimerTicked|E=10|State=Cooldown|From=3|To=2",
                },
                result.CleanupPhaseResult.TimerChanges);
            Assert.That(afterSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(2));
        }

        [Test]
        public void Cleanup_StateTransition_AppliesAfterTimerTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 1,
                    spawnTick: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(13));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TimerTicked|E=10|State=Cooldown|From=1|To=0",
                },
                result.CleanupPhaseResult.TimerChanges);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateTransitioned|E=10|From=Cooldown|To=Idle|Timer=0",
                },
                result.CleanupPhaseResult.StateTransitions);
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(0));
            Assert.That(GetEntityState(afterSnapshot, 10).state, Is.EqualTo(EntityPhaseState.Idle));
        }

        [Test]
        public void Cleanup_SlidingState_DoesNotAutoTransitionWhenTimerReachesZero()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 3,
                    state: EntityPhaseState.Sliding,
                    stateTimer: 1,
                    spawnTick: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(14));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TimerTicked|E=10|State=Sliding|From=1|To=0",
                },
                result.CleanupPhaseResult.TimerChanges);
            Assert.That(result.CleanupPhaseResult.StateTransitions, Is.Empty);
            Assert.That(GetEntityState(afterSnapshot, 10).stateTimer, Is.EqualTo(0));
            Assert.That(GetEntityState(afterSnapshot, 10).state, Is.EqualTo(EntityPhaseState.Sliding));
        }

        [Test]
        public void Cleanup_SameInput_ProducesDeterministicResult()
        {
            var firstRun = RunDeterministicCleanupTick();
            var secondRun = RunDeterministicCleanupTick();

            CollectionAssert.AreEqual(firstRun.Result.CleanupPhaseResult.RemovedEntityIds, secondRun.Result.CleanupPhaseResult.RemovedEntityIds);
            CollectionAssert.AreEqual(firstRun.Result.CleanupPhaseResult.TimerChanges, secondRun.Result.CleanupPhaseResult.TimerChanges);
            CollectionAssert.AreEqual(firstRun.Result.CleanupPhaseResult.StateTransitions, secondRun.Result.CleanupPhaseResult.StateTransitions);
            Assert.That(firstRun.StateDumpAfter, Is.EqualTo(secondRun.StateDumpAfter));
        }

        private static (TickResult Result, string StateDumpAfter) RunDeterministicCleanupTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 30,
                    position: new SurfaceCell(FaceId.Floor, 2, 0),
                    hp: 4,
                    state: EntityPhaseState.Acting,
                    stateTimer: 1,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 10,
                    position: new SurfaceCell(FaceId.Floor, 0, 0),
                    hp: 4,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 2,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    hp: 0,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 5,
                    spawnTick: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(14));
            return (result, DumpEntityStates(CreateSnapshot(worldState)));
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
            int hp,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            EntityPhaseState state = EntityPhaseState.Idle,
            int stateTimer = 0,
            int spawnTick = 0)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position), hp, markedForDeath, boardPresence, state, stateTimer, spawnTick);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int hp,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            EntityPhaseState state = EntityPhaseState.Idle,
            int stateTimer = 0,
            int spawnTick = 0)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp > 0 ? hp : 3,
                teamId = 1,
                type = EntityType.Unit,
                state = state,
                stateTimer = stateTimer,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
                spawnTick = spawnTick,
            };
        }

        private static EntityState CreatePlayerUnit(
            int entityId,
            SurfaceCell position,
            int hp,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
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
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static string DumpEntityStates(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            return string.Join(
                ",",
                entities.Select(entity =>
                    $"{entity.entityId}:{entity.state}:{entity.stateTimer}:{entity.position.x}:{entity.position.y}:{entity.hp}:{entity.markedForDeath}"));
        }

        private static EntityState GetEntityState(WorldSnapshot snapshot, int entityId)
        {
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            var createSnapshotMethod = typeof(WorldState).GetMethod(
                "CreateSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createSnapshotMethod, Is.Not.Null);

            return (WorldSnapshot)createSnapshotMethod.Invoke(worldState, null);
        }
    }
}
