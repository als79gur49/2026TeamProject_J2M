using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
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
            var pipeline = new TickPipeline(
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
                    "DestroyMarked|G=2|I=2|Target=30|FinalHp=0",
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
        public void Replay_PushChainScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunPushChainReplaySequence();
            var secondReplay = RunPushChainReplaySequence();

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
            Assert.That(firstReplay[0].Trace, Does.Contain("Kind=PushChain"));
            Assert.That(firstReplay[0].Trace, Does.Contain("Moves=[E=30:(2,0)->(3,0):Right,E=20:(1,0)->(2,0):Right,E=10:(0,0)->(1,0):Right]"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=30|To=(3,0)|Facing=Right"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right"));
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

        private static IReadOnlyList<TickReplayFrame> RunPushChainReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(2, 0), hp: 3),
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

        private sealed class ScriptedCombatLogic : IEntityLogic, IReplayTickAwareEntityLogic
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

            public void CollectAttackIntents(WorldSnapshot snapshot, List<RawAttackIntent> buffer)
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
        }
    }
}
