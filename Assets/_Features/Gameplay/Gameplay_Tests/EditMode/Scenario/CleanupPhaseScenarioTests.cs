using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class CleanupPhaseScenarioTests
    {
        [Test]
        public void Cleanup_DestroyMarkedEntity_IsRemovedOnlyInCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(1, 0), hp: 2, markedForDeath: true),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            var beforeSnapshot = CreateSnapshot(worldState);

            Assert.That(beforeSnapshot.IsBlockedForUnit(new Vector2Int(1, 0)), Is.True);
            Assert.That(beforeSnapshot.TryGetEntity(10, out var entityBefore), Is.True);
            Assert.That(entityBefore.markedForDeath, Is.True);

            var result = pipeline.RunTick(new TickInput(5));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(afterSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(afterSnapshot.IsBlockedForUnit(new Vector2Int(1, 0)), Is.False);
        }

        [Test]
        public void Cleanup_HpZeroEntity_IsRemovedInCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(2, 0), hp: 0),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(7));
            var afterSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 10 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(afterSnapshot.TryGetEntity(10, out _), Is.False);
        }

        [Test]
        public void Cleanup_RemovalClearsOccupancy()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(3, 1), hp: 1, markedForDeath: true),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            pipeline.RunTick(new TickInput(8));

            var afterSnapshot = CreateSnapshot(worldState);
            Assert.That(afterSnapshot.IsBlockedForUnit(new Vector2Int(3, 1)), Is.False);
            Assert.That(afterSnapshot.TryGetUnitAt(new Vector2Int(3, 1), out _), Is.False);
        }

        [Test]
        public void Cleanup_SpawnedThisTick_DoesNotTickTimer()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 10,
                    position: new Vector2Int(0, 0),
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
                    position: new Vector2Int(2, 0),
                    hp: 0,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 3,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 10,
                    position: new Vector2Int(0, 0),
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
                    position: new Vector2Int(0, 0),
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
                    position: new Vector2Int(2, 0),
                    hp: 4,
                    state: EntityPhaseState.Acting,
                    stateTimer: 1,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 10,
                    position: new Vector2Int(0, 0),
                    hp: 4,
                    state: EntityPhaseState.Cooldown,
                    stateTimer: 2,
                    spawnTick: 1),
                CreateUnit(
                    entityId: 20,
                    position: new Vector2Int(1, 0),
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
                markedForDeath = markedForDeath,
                spawnTick = spawnTick,
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
            var constructor = typeof(WorldState).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(IEnumerable<EntityState>) },
                modifiers: null);

            Assert.That(constructor, Is.Not.Null);

            return (WorldState)constructor.Invoke(new object[] { initialEntities });
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
