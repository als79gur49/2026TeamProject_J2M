using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class TickPipelineStructureSimulationTests
    {
        [Test]
        [Category("Extended")]
        public void RunTick_CompletesPlanResolveFinalizeCleanupRespawn()
        {
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()));

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(result.TickIndex, Is.EqualTo(7));
            Assert.That(result.CompletedAllPhases, Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    TickPhase.Plan,
                    TickPhase.Resolve,
                    TickPhase.Finalize,
                    TickPhase.Cleanup,
                    TickPhase.Respawn,
                },
                result.CompletedPhases);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Plan:Enter",
                    "Plan:Exit",
                    "Resolve:Enter",
                    "Resolve:Exit",
                    "Finalize:Enter",
                    "Finalize:Exit",
                    "Cleanup:Enter",
                    "Cleanup:Exit",
                    "Respawn:Enter",
                    "Respawn:Exit",
                },
                result.PhaseTrace);
        }
    }

    public sealed class TickPipelineExecutionScenarioTests
    {
        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
                Array.Empty<IEntityLogic>(),
                new StubEntityLogicProvider(
                    new StubEntityLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0)), null)),
                GameplayTimingProfile.CreateDefault(),
                CreateDefaultPlayerControlTimingSnapshot());

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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
        public void RunTick_OffBottomEnemy_DoesNotEmitMovementOrAttackTrace()
        {
            var player = CreateEntity(10, EntityType.Unit, new SurfaceCell(FaceId.Front, 1, 0), Direction.Left);
            var enemy = CreateEnemyEntity(40, new SurfaceCell(FaceId.Front, 0, 0), EnemyAiMode.Chase, Direction.Right);
            var worldState = CreateWorldState(
                new[]
                {
                    player,
                    enemy,
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.AttackPhaseResult.SortedInputs, Is.Empty);
            Assert.That(result.Trace.Text, Does.Not.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40"));
            Assert.That(result.Trace.Text, Does.Not.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40"));
            Assert.That(result.Trace.Text, Does.Not.Contain("EnemyAiTransition|Stage=AfterAttack|E=40"));
            Assert.That(result.Trace.Text, Does.Not.Contain("Source=40|Priority="));
            Assert.That(result.Trace.Text, Does.Not.Contain("Source=40|Target=10"));
        }

        [Test]
        [Category("Extended")]
        public void RunTick_TopologyRotation_ReevaluatesEnemyParticipationBeforeAttackCollection()
        {
            var player = CreateEntity(10, EntityType.Unit, new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up);
            var enemy = CreateEnemyEntity(40, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Attack, Direction.Up);
            var worldState = CreateWorldState(
                new[]
                {
                    player,
                    enemy,
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                CreateEnemyActionState(
                    EnemyActionKind.Melee,
                    sequence: 1,
                    lockedTargetEntityId: 10,
                    direction: Direction.Up,
                    startTick: 0,
                    executeTick: 1));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            Assert.That(worldState.CreateSnapshot().Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(result.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.AttackPhaseResult.SortedInputs, Is.Empty);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(40, out var actionState), Is.True);
            Assert.That(actionState.IsActive, Is.False);
            Assert.That(actionState.sequence, Is.EqualTo(1));
            Assert.That(result.Trace.Text, Does.Contain("EnemyAction.BeforeAttackCollectionTransitions"));
            Assert.That(result.Trace.Text, Does.Contain("E=40|Prev=Melee|Curr=None|PrevSeq=1|CurrSeq=1|Started=False|Canceled=True"));
            Assert.That(result.Trace.Text, Does.Not.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40"));
            Assert.That(result.Trace.Text, Does.Not.Contain("EnemyAiTransition|Stage=AfterAttack|E=40"));
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
                GameplayTimingProfile.CreateDefault(),
                CreateDefaultPlayerControlTimingSnapshot());

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0)) },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            Assert.That(result.FinalEntities.Single().position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
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

            public void CollectMovementIntents(WorldSnapshot snapshot, in TickInput input, List<RawMovementIntent> buffer)
            {
                if (_movementIntent.HasValue)
                {
                    buffer.Add(_movementIntent.Value);
                }
            }

            public void CollectAttackIntents(WorldSnapshot snapshot, in TickInput input, List<RawAttackIntent> buffer)
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

            public EntityLogicSet Build(WorldSnapshot snapshot, IReadOnlyList<IEntityLogic> staticEntityLogics)
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
    }

    public sealed class StageObjectiveSimulationTests
    {
        private static readonly BoardBounds DefaultBoardBounds = new(new Vector2Int(0, 0), new Vector2Int(4, 4));

        [Test]
        [Category("Extended")]
        public void TickPipeline_ExposesObjectiveResultInTickResult()
        {
            var objective = CreateSimpleObjectiveDefinition(new SurfaceCell(FaceId.Floor, 1, 1));
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                },
                DefaultBoardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                objectiveDefinition: objective);

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.ObjectiveResult.HasObjective, Is.True);
            Assert.That(result.ObjectiveResult.IsCleared, Is.True);
            Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_DeterminismHash_ChangesWhenObjectiveStateChanges()
        {
            var clearObjective = CreateSimpleObjectiveDefinition(new SurfaceCell(FaceId.Floor, 1, 1));
            var unclearedObjective = CreateSimpleObjectiveDefinition(new SurfaceCell(FaceId.Floor, 2, 2));
            var timingProfile = GameplayTimingProfile.CreateDefault();

            var clearedResult = RunSingleTickWithObjective(
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                clearObjective,
                timingProfile);
            var unclearedResult = RunSingleTickWithObjective(
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                unclearedObjective,
                timingProfile);

            Assert.That(clearedResult.DeterminismHash, Is.Not.EqualTo(unclearedResult.DeterminismHash));
        }

        private static TickResult RunSingleTickWithObjective(
            EntityState player,
            StageObjectiveRuntimeDefinition objectiveDefinition,
            GameplayTimingProfile timingProfile)
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { player },
                DefaultBoardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                objectiveDefinition: objectiveDefinition);
            return pipeline.RunTick(new TickInput(1));
        }

        private static StageObjectiveRuntimeDefinition CreateSimpleObjectiveDefinition(SurfaceCell goalCell)
        {
            var goalZone = new StageZoneRuntimeDefinition(
                "goal",
                goalCell.face,
                new[]
                {
                    new StageZoneRuntimeRegion(goalCell.PlanarPosition, goalCell.PlanarPosition),
                });

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                10,
                new[] { goalZone },
                new[] { goalZone },
                Array.Empty<StageConditionRuntimeDefinition>());
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(GameplayTimingProfile timingProfile)
        {
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static EntityState CreatePlayerEntity(int entityId, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                unitRole = UnitRole.Player,
            };
        }
    }

    public sealed class RuntimeBoardBoundsGuardScenarioTests
    {
        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_WithoutPlayerPrefabAuthoritativeSource_UsesDefaultPlayerControlTiming()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_WithoutPlayerPrefabAuthoritativeSource_UsesDefaultPlayerControlTiming");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                Assert.DoesNotThrow(
                    () => host.Initialize(
                        new GameplaySceneHostConfiguration
                        {
                            AutoAdvanceTicks = false,
                            AutoCreateViews = false,
                            InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                            RepeatedMoveIntervalSeconds = 1f / 60f,
                            InitialEntities = new[]
                            {
                                new EntityState
                                {
                                    entityId = 10,
                                    position = new SurfaceCell(FaceId.Floor, 0, 0),
                                    hp = 3,
                                    maxHp = 3,
                                    teamId = 1,
                                    type = EntityType.Unit,
                                    state = EntityPhaseState.Idle,
                                    facing = Direction.Right,
                                },
                            },
                            InitialTopology = new CubeTopologyState(FaceId.Floor),
                            PlayerEntityId = 10,
                            StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        }));

                host.InputHost.SetRawMoveInput(Vector2.right);
                var firstTick = host.InputHost.RunSingleTick();
                var secondTick = host.InputHost.RunSingleTick();

                CollectionAssert.AreEqual(
                    new[] { "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right" },
                    firstTick.MovementPhaseResult.CommitEvents);
                Assert.That(secondTick.MovementPhaseResult.SortedIntents, Is.Empty);
                Assert.That(host.ViewRegistry.TryGetView(10, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }
    }
}
