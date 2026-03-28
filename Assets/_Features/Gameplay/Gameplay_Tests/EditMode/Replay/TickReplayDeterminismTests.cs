using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class TickReplayDeterminismTests
    {
        [Test]
        public void TickResult_ExtendsFinalStateAndEventLog_WithoutCrossPhaseBackflow()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(2, 0), hp: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedCombatLogic(
                        sourceId: 10,
                        movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                        {
                            { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                        },
                        attackIntent: new RawAttackIntent(10, 5, 30)),
                });

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEqual(
                new[]
                {
                    "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right",
                    "StateChanged|G=2|I=2|E=10|State=Acting|Timer=0",
                    "DamageCommitted|G=2|I=2|Target=30|Amount=1",
                    "DestroyMarked|G=2|I=2|Target=30|FinalHp=0|Condition=WhenHpDepleted",
                    "CleanupRemoved|E=30",
                    "StateTransitioned|E=10|From=Acting|To=Idle|Timer=0",
                },
                result.EventLog);
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 10, Position: new Vector2Int(1, 0), Hp: 3, State: EntityPhaseState.Idle),
                },
                result.FinalEntities
                    .Select(entity => (entity.entityId, entity.position, entity.hp, entity.state))
                    .ToArray());
            Assert.That(result.DeterminismHash, Is.Not.Empty);
            Assert.That(result.Trace.Text, Does.Contain("S0.Entities"));
            Assert.That(result.Trace.Text, Does.Contain("TickResult.EventLog"));
            Assert.That(result.Trace.Text, Does.Contain("CleanupRemoved|E=30"));
        }

        [Test]
        public void Replay_SameInitialWorldAndInputSequence_ProducesSamePerTickHashAndTrace()
        {
            var firstReplay = RunReplaySequence();
            var secondReplay = RunReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
        }

        [Test]
        public void Replay_ProjectileImpactScenario_ProducesSamePerTickHashTraceAndEventLog()
        {
            var firstReplay = RunProjectileImpactReplaySequence();
            var secondReplay = RunProjectileImpactReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Attack.DrainedImpacts"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("ImpactReservationCreated|G=1|I=1|Source=10|Target=20|At=(1,0)|Damage=1|Sequence=1"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("CleanupRemoved|E=10"));
        }

        [Test]
        public void Replay_ScriptedMoveIntoUnitBlockedScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunScriptedMoveIntoUnitBlockedReplaySequence();
            var secondReplay = RunScriptedMoveIntoUnitBlockedReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Reason=BlockedDestination|Cell=(1,0)"));
            Assert.That(firstReplay[0].EventLogDump, Is.EqualTo("<empty>"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=20|Pos=(1,0)|Hp=2|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
        }

        [Test]
        public void Replay_BoxSlideEntityStopperScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunBoxSlideReplaySequence();
            var secondReplay = RunBoxSlideReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Kind=BoxSlide"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=30|To=(3,0)|Facing=Right"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=30|Pos=(3,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Pushable"));
        }

        [Test]
        public void Replay_BoxSlideTerrainStopperScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunBoxSlideTerrainReplaySequence();
            var secondReplay = RunBoxSlideTerrainReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("S0.Terrain"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=30|Pos=(3,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Pushable"));
        }

        [Test]
        public void Replay_BoxSlideBoardEdgeScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunBoxSlideBoardEdgeReplaySequence();
            var secondReplay = RunBoxSlideBoardEdgeReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("S0.BoardBounds"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=30|Pos=(3,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Pushable"));
        }

        [Test]
        public void Replay_PlayerMoveIntoUnitBlockedScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunPlayerMoveIntoUnitBlockedReplaySequence();
            var secondReplay = RunPlayerMoveIntoUnitBlockedReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].EventLogDump, Is.EqualTo("<empty>"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=20|Pos=(1,0)|Hp=2|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
        }

        [Test]
        public void Replay_ItemInteractScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunItemInteractReplaySequence();
            var secondReplay = RunItemInteractReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Kind=Item"));
            Assert.That(firstReplay[0].Trace, Does.Contain("Command=Interact"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("BoardPresenceCommitted|G=1|I=1|E=30|Presence=Detached"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("DestroyMarked|G=1|I=1|Target=30|Condition=AlwaysMark"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("CleanupRemoved|E=30"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(1,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("E=30|"));
        }

        [Test]
        public void Replay_BoxThrowScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunBoxThrowReplaySequence();
            var secondReplay = RunBoxThrowReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Kind=Throw"));
            Assert.That(firstReplay[0].Trace, Does.Contain("Moves=[E=30:(-1,0)->(1,0):Right]"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("FacingCommitted|G=1|I=1|E=10|Facing=Left"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=30|To=(1,0)|Facing=Right"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Left|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=30|Pos=(1,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Throwable"));
        }

        [Test]
        public void Replay_EdgeReservationScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunEdgeReservationReplaySequence();
            var secondReplay = RunEdgeReservationReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Moves=[E=10:(0,0)->(1,0):Right]"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Moves=[E=10:(1,0)->(0,0):Left]"));
            Assert.That(firstReplay[1].Trace, Does.Not.Contain("Reason=EdgeReserved"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=10|To=(0,0)|Facing=Left"));
        }

        [Test]
        public void Replay_SpawnScenario_ProducesSamePerTickHashTraceAndEventLog()
        {
            var firstReplay = RunSpawnReplaySequence();
            var secondReplay = RunSpawnReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Source=10|Priority=5|Target=0|Command=FireProjectile"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("SpawnCommitted|G=1|I=1|SpawnId=1|E=21|Pos=(1,0)|Type=Projectile|SpawnTick=1"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("ImpactReservationCreated|G=1|I=1|Source=21|Target=20|At=(2,0)|Damage=1|Sequence=1"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("CleanupRemoved|E=21"));
            Assert.That(firstReplay[2].EventLogDump, Does.Contain("SpawnCommitted|G=1|I=1|SpawnId=1|E=22|Pos=(1,0)|Type=Projectile|SpawnTick=3"));
            Assert.That(firstReplay[2].FinalEntitiesDump, Does.Contain("E=22|Pos=(1,0)|Hp=1|MaxHp=1|Team=1|Type=Projectile|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=3"));
        }

        [Test]
        public void Replay_OnHitBoundaryScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunOnHitBoundaryReplaySequence();
            var secondReplay = RunOnHitBoundaryReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Attack.DrainedImpacts"));
            Assert.That(firstReplay[0].Trace, Does.Contain("Command=ImpactReservation"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("ImpactReservationCreated|G=1|I=1|Source=5|Target=20|At=(1,0)|Damage=1|Sequence=1"));
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("Target=40"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=40|Pos=(0,1)|Hp=2|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0"));
        }

        [Test]
        public void Replay_DelayedEventScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunDelayedEventReplaySequence();
            var secondReplay = RunDelayedEventReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Final.PendingDelayedEffects"));
            Assert.That(firstReplay[0].Trace, Does.Contain("ExecuteTick=2"));
            Assert.That(firstReplay[0].EventLogDump, Is.EqualTo("<empty>"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Attack.DrainedDelayedEffects"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Command=DelayedEffect"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("DelayedAttackDrained|Tick=2|Source=10|Target=20|Damage=1|GeneratedTick=1|ExecuteTick=2|Group=99|Sequence=1"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("DamageCommitted|G=1|I=1|Target=20|Amount=1"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=20|Pos=(1,0)|Hp=1|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0"));
        }

        [Test]
        public void DeterminismHash_PendingDelayedEvent_IsIncludedInCanonicalState()
        {
            var pipelineWithoutDelayedEvent = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
                }));
            var pipelineWithDelayedEvent = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
                }));
            pipelineWithDelayedEvent.EnqueueDelayedAttackEffect(
                new DelayedAttackEffectRecord(
                    sourceId: 10,
                    targetId: 20,
                    damage: 1,
                    priority: 5,
                    tickGenerated: 1,
                    executeAtTick: 2,
                    sourceActionGroupId: 99,
                    effectSequence: 1));

            var resultWithoutDelayedEvent = pipelineWithoutDelayedEvent.RunTick(new TickInput(1));
            var resultWithDelayedEvent = pipelineWithDelayedEvent.RunTick(new TickInput(1));

            Assert.That(resultWithoutDelayedEvent.EventLog, Is.Empty);
            Assert.That(resultWithDelayedEvent.EventLog, Is.Empty);
            Assert.That(resultWithoutDelayedEvent.DeterminismHash, Is.Not.EqualTo(resultWithDelayedEvent.DeterminismHash));
            Assert.That(resultWithDelayedEvent.Trace.Text, Does.Contain("Final.PendingDelayedEffects"));
            Assert.That(resultWithDelayedEvent.Trace.Text, Does.Contain("ExecuteTick=2"));
        }

        private static IReadOnlyList<TickReplayFrame> RunReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(2, 0), hp: 1),
            });
            var entityLogics = new IEntityLogic[]
            {
                new ScriptedCombatLogic(
                    sourceId: 20,
                    movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                    {
                        { 2, new RawMovementIntent(20, 3, new Vector2Int(2, 0)) },
                    }),
                new ScriptedCombatLogic(
                    sourceId: 10,
                    movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                    {
                        { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                    },
                    attackIntent: new RawAttackIntent(10, 5, 30)),
            };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                new[]
                {
                    new TickInput(1),
                    new TickInput(2),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunProjectileImpactReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 1),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
            });
            var entityLogics = new IEntityLogic[]
            {
                new ScriptedCombatLogic(
                    sourceId: 10,
                    movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                    {
                        { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                    }),
            };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                new[]
                {
                    new TickInput(1),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunScriptedMoveIntoUnitBlockedReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
            });
            var entityLogics = new IEntityLogic[]
            {
                new ScriptedCombatLogic(
                    sourceId: 10,
                    movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                    {
                        { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                    }),
            };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                new[]
                {
                    new TickInput(1),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunEdgeReservationReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
            });
            var entityLogics = new IEntityLogic[]
            {
                new ScriptedCombatLogic(
                    sourceId: 10,
                    movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                    {
                        { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                        { 2, new RawMovementIntent(10, 5, new Vector2Int(0, 0)) },
                    }),
            };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                new[]
                {
                    new TickInput(1),
                    new TickInput(2),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunItemInteractReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.LootOnInteractDestroy),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Interact(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunBoxSlideReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable, facing: Direction.Left),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Interact(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunBoxSlideTerrainReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable, facing: Direction.Left),
                },
                BoardBounds.Unbounded,
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Interact(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunBoxSlideBoardEdgeReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable, facing: Direction.Left),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)),
                GameplayTerrainData.Empty);

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Interact(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunPlayerMoveIntoUnitBlockedReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunBoxThrowReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Throwable),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Throw(Direction.Left)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunSpawnReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 2),
            });
            var entityLogics = new IEntityLogic[]
            {
                new ScriptedCombatLogic(
                    sourceId: 10,
                    attackIntentsByTick: new Dictionary<int, RawAttackIntent>
                    {
                        { 1, RawAttackIntent.CreateFireProjectile(10, 5) },
                        { 3, RawAttackIntent.CreateFireProjectile(10, 5) },
                    }),
            };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                new[]
                {
                    new TickInput(1),
                    new TickInput(2),
                    new TickInput(3),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunOnHitBoundaryReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 5, teamId: 1, position: new Vector2Int(2, 0), hp: 1),
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2),
            });
            var entityLogics = new IEntityLogic[]
            {
                new ScriptedCombatLogic(
                    sourceId: 5,
                    movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                    {
                        { 1, new RawMovementIntent(5, 0, new Vector2Int(1, 0)) },
                    }),
                new ScriptedCombatLogic(
                    sourceId: 10,
                    attackIntent: new RawAttackIntent(10, 5, 20)),
            };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                new[]
                {
                    new TickInput(1),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunDelayedEventReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[0],
                new[]
                {
                    new TickInput(1),
                    new TickInput(2),
                },
                new[]
                {
                    new DelayedAttackEffectRecord(
                        sourceId: 10,
                        targetId: 20,
                        damage: 1,
                        priority: 5,
                        tickGenerated: 1,
                        executeAtTick: 2,
                        sourceActionGroupId: 99,
                        effectSequence: 1),
                });
        }

        private static EntityState CreateUnit(int entityId, int teamId, Vector2Int position, int hp)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.Right,
                markedForDeath = false,
                spawnTick = 0,
            };
        }

        private static EntityState CreateProjectile(int entityId, int teamId, Vector2Int position, int hp)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Projectile,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.Right,
                markedForDeath = false,
                spawnTick = 0,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            Vector2Int position,
            BoxCapabilities capabilities = BoxCapabilities.Pushable | BoxCapabilities.Throwable,
            Direction facing = Direction.Left)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = capabilities,
            };
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
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
                stateTimer = 0,
                facing = Direction.None,
                markedForDeath = false,
                spawnTick = 0,
            };
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return boardBounds.IsBounded
                ? GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, terrainData)
                : GameplayWorldStateTestFactory.CreateLegacyUnbounded(initialEntities, terrainData);
        }

        private sealed class ScriptedCombatLogic : IEntityLogic, IReplayTickAwareEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawAttackIntent? _attackIntent;
            private readonly Dictionary<int, RawAttackIntent> _attackIntentsByTick;
            private readonly Dictionary<int, RawMovementIntent> _movementIntentsByTick;
            private readonly int _sourceId;
            private int _currentTickIndex;

            public ScriptedCombatLogic(
                int sourceId,
                Dictionary<int, RawMovementIntent> movementIntentsByTick = null,
                RawAttackIntent? attackIntent = null,
                Dictionary<int, RawAttackIntent> attackIntentsByTick = null)
            {
                _sourceId = sourceId;
                _movementIntentsByTick = movementIntentsByTick ?? new Dictionary<int, RawMovementIntent>();
                _attackIntent = attackIntent;
                _attackIntentsByTick = attackIntentsByTick ?? new Dictionary<int, RawAttackIntent>();
            }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (!snapshot.TryGetEntity(_sourceId, out var source) || source.hp <= 0 || source.markedForDeath)
                {
                    return;
                }

                if (_movementIntentsByTick.TryGetValue(input.TickIndex, out var movementIntent))
                {
                    buffer.Add(movementIntent);
                }
            }

            public void CollectAttackIntents(WorldSnapshot snapshot, in TickInput input, List<RawAttackIntent> buffer)
            {
                if (!snapshot.TryGetEntity(_sourceId, out var source) || source.hp <= 0 || source.markedForDeath)
                {
                    return;
                }

                if (_currentTickIndex > 0 && _attackIntentsByTick.TryGetValue(_currentTickIndex, out var tickAttackIntent))
                {
                    buffer.Add(tickAttackIntent);
                    return;
                }

                if (_attackIntent.HasValue)
                {
                    buffer.Add(_attackIntent.Value);
                }
            }

            public void SetReplayTickIndex(int tickIndex)
            {
                _currentTickIndex = tickIndex;
            }

            public bool ControlsEntity(int entityId, TickPhase phase)
            {
                return phase == TickPhase.Movement &&
                    _movementIntentsByTick.Count > 0 &&
                    _sourceId == entityId;
            }
        }
    }
}
