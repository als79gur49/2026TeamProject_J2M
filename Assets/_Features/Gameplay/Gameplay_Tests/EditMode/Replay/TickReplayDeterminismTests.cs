using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class TickReplayDeterminismTests
    {
        [Test]
        [Category("Core")]
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

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.EventLog,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 10, Position: new SurfaceCell(FaceId.Floor, 1, 0), Hp: 3, State: EntityPhaseState.Idle),
                    (EntityId: 30, Position: new SurfaceCell(FaceId.Floor, 2, 0), Hp: 1, State: EntityPhaseState.Idle),
                },
                result.FinalEntities
                    .Select(entity => (entity.entityId, entity.position, entity.hp, entity.state))
                    .ToArray());
            Assert.That(result.DeterminismHash, Is.Not.Empty);
            Assert.That(result.Trace.Text, Does.Contain("S0.Entities"));
            Assert.That(result.Trace.Text, Does.Contain("TickResult.EventLog"));
            Assert.That(result.Trace.Text, Does.Contain("Final.ExecutionLocks"));
            Assert.That(result.Trace.Text, Does.Contain("E=10|Phase=Move|Sequence=1|UnlockTickExclusive=14"));
        }

        [Test]
        [Category("Core")]
        public void Replay_SameInitialWorldAndInputSequence_ProducesSamePerTickHashAndTrace()
        {
            var firstReplay = RunReplaySequence();
            var secondReplay = RunReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.PlayerControlDump).ToArray(),
                secondReplay.Select(frame => frame.PlayerControlDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.PlayerDamageDump).ToArray(),
                secondReplay.Select(frame => frame.PlayerDamageDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EnemyActionDump).ToArray(),
                secondReplay.Select(frame => frame.EnemyActionDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
        }

        [Test]
        [Category("Core")]
        public void Replay_AttackLogicRegistrationPermutation_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunAttackLogicPermutationReplaySequence(reverseLogicOrder: false);
            var secondReplay = RunAttackLogicPermutationReplaySequence(reverseLogicOrder: true);

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(
                firstReplay[0].EventLogDump
                    .Split('\n')
                    .Count(line => line.StartsWith("SpawnCommitted|", StringComparison.Ordinal)),
                Is.EqualTo(2));
            Assert.That(
                firstReplay[0].FinalEntitiesDump
                    .Split('\n')
                    .Count(line => line.Contains("|Type=Projectile|", StringComparison.Ordinal)),
                Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void Replay_OffBottomEnemySuppression_ProducesStableEmptyAttackSurface()
        {
            var firstReplay = RunOffBottomEnemyReplaySequence();
            var secondReplay = RunOffBottomEnemyReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay[0].EventLogDump, Is.EqualTo("<empty>"));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40"));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40"));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("EnemyAiTransition|Stage=AfterAttack|E=40"));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("Source=40|Priority="));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("Source=40|Target=10"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=40|"));
        }

        [Test]
        [Category("Core")]
        public void Replay_TopologyRotation_CancelsEnemyActionBeforeAttackCollection_WithoutAttackArtifacts()
        {
            var firstReplay = RunTopologyRotationAttackSuppressionReplaySequence();
            var secondReplay = RunTopologyRotationAttackSuppressionReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "TopologyCommitted",
                    "Rotation=Forward"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "MoveCommitted",
                    "E=10"),
                Is.True);
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("DamageCommitted"));
            Assert.That(firstReplay[0].EnemyActionDump, Does.Contain("E=40|Kind=None|Seq=1|Target=0|Direction=None"));
            Assert.That(firstReplay[0].EnemyActionDump, Does.Not.Contain("Kind=Melee"));
            Assert.That(firstReplay[0].Trace, Does.Contain("EnemyAction.BeforeAttackCollectionTransitions"));
            Assert.That(firstReplay[0].Trace, Does.Contain("E=40|Prev=Melee|Curr=None|PrevSeq=1|CurrSeq=1|Started=False|Canceled=True"));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40"));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("EnemyAiTransition|Stage=AfterAttack|E=40"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyDeathPresentationData_DoesNotAffectCanonicalStateOrHash()
        {
            var finalEntities = new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
            };
            var finalSnapshot = SnapshotBuilder.Create(CreateWorldState(finalEntities));
            var eventLog = new[]
            {
                "CleanupRemoved|E=40",
            };
            var enemyDeathPresentation = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: new[]
                {
                    new TickEntityExitPresentationSignal(
                        40,
                        TickEntityExitCause.EnemyDeath,
                        SurfaceCell.FromPlanar(new Vector2Int(2, 0)),
                        finalSnapshot.Topology,
                        Direction.Left,
                        EntityType.Unit,
                        sourceActorEntityId: 10,
                        presentationSeed: 987654321),
                });
            var baselineData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                TickPresentationData.Empty);
            var enemyDeathData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                enemyDeathPresentation);
            var hashBuilder = new DeterminismHashBuilder();

            CollectionAssert.AreEqual(baselineData.FinalEntities, enemyDeathData.FinalEntities);
            CollectionAssert.AreEqual(baselineData.EventLog, enemyDeathData.EventLog);
            Assert.That(
                hashBuilder.Build(11, finalSnapshot, baselineData),
                Is.EqualTo(hashBuilder.Build(11, finalSnapshot, enemyDeathData)));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_PlayerDeathPresentationData_DoesNotAffectCanonicalStateOrHash()
        {
            var finalEntities = new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 0),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 3),
            };
            finalEntities[0].unitRole = UnitRole.Player;
            finalEntities[0].markedForDeath = true;
            finalEntities[1].unitRole = UnitRole.Enemy;
            var finalSnapshot = SnapshotBuilder.Create(CreateWorldState(finalEntities));
            var eventLog = new[]
            {
                "DamageCommitted|Source=20|Target=10|Amount=3",
            };
            var playerDeathPresentation = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: new[]
                {
                    new TickPlayerDeathPresentationSignal(
                        10,
                        didDieThisTick: true,
                        sourceEntityId: 20,
                        fallbackFacing: Direction.Right,
                        resolvedDamageSourceAvailable: true,
                        damageAmountAtFatalHit: 3,
                        deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                },
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>());
            var baselineData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                TickPresentationData.Empty);
            var playerDeathData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                playerDeathPresentation);
            var hashBuilder = new DeterminismHashBuilder();

            CollectionAssert.AreEqual(baselineData.FinalEntities, playerDeathData.FinalEntities);
            CollectionAssert.AreEqual(baselineData.EventLog, playerDeathData.EventLog);
            Assert.That(
                hashBuilder.Build(12, finalSnapshot, baselineData),
                Is.EqualTo(hashBuilder.Build(12, finalSnapshot, playerDeathData)));
        }

        [Test]
        [Category("Core")]
        public void Replay_PlayerControlState_IsIncludedInHashTraceAndReplayDump()
        {
            var playerLogic = CreatePlayerLogicWithActionTiming(entityId: 10);
            var harness = new TickReplayHarness();
            var firstRun = harness.Run(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                }),
                new IEntityLogic[]
                {
                    playerLogic,
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                    new TickInput(2),
                    new TickInput(3),
                });
            var secondRun = harness.Run(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                }),
                new IEntityLogic[]
                {
                    CreatePlayerLogicWithActionTiming(entityId: 10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                    new TickInput(2),
                    new TickInput(3),
                });

            CollectionAssert.AreEqual(
                firstRun.Select(frame => frame.DeterminismHash).ToArray(),
                secondRun.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstRun.Select(frame => frame.PlayerControlDump).ToArray(),
                secondRun.Select(frame => frame.PlayerControlDump).ToArray());
            CollectionAssert.AreEqual(
                firstRun.Select(frame => frame.PlayerDamageDump).ToArray(),
                secondRun.Select(frame => frame.PlayerDamageDump).ToArray());
            Assert.That(firstRun[0].Trace, Does.Contain("Final.PlayerControl"));
            Assert.That(firstRun[0].Trace, Does.Contain("Final.PlayerDamage"));
            Assert.That(firstRun[0].Trace, Does.Not.Contain("PushTicks="));
            Assert.That(firstRun[0].PlayerControlDump, Does.Not.Contain("PushTicks="));
            Assert.That(firstRun[0].PlayerControlDump, Does.Contain("Action=Push|ActionSeq=1|ActionDirection=Right|ActionTarget=30|Start=1|Execute=2|Recovery=2|Attempted=0"));
            Assert.That(firstRun[1].PlayerControlDump, Does.Not.Contain("PushTicks="));
            Assert.That(firstRun[1].PlayerControlDump, Does.Contain("Action=Push|ActionSeq=1|ActionDirection=Right|ActionTarget=30|Start=1|Execute=2|Recovery=2|Attempted=1"));
            Assert.That(firstRun[2].PlayerControlDump, Does.Not.Contain("PushTicks="));
            Assert.That(firstRun[2].PlayerControlDump, Does.Contain("Action=None|ActionSeq=0|ActionDirection=None|ActionTarget=0|Start=0|Execute=0|Recovery=0|Attempted=0"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PlayerControlState_DeterministicallyReflectsCustomAuthoritativeActionTiming()
        {
            var snapshot = new PlayerControlTimingSettings
            {
                PushExecuteDelaySeconds = 2f / 60f,
                PushInputLockDurationSeconds = 4f / 60f,
            }.CreateAuthoritativeSnapshot(60, GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds);
            var harness = new TickReplayHarness();
            var frames = harness.Run(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                }),
                new IEntityLogic[]
                {
                    new PlayerLogic(
                        10,
                        pushWindupTicks: snapshot.PushWindupTicks,
                        pushRecoveryTicks: snapshot.PushRecoveryTicks,
                        flipWindupTicks: snapshot.FlipWindupTicks,
                        flipRecoveryTicks: snapshot.FlipRecoveryTicks),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                    new TickInput(2, PlayerTickCommand.Move(Direction.Right)),
                });

            Assert.That(frames[0].Trace, Does.Contain("Final.PlayerControl"));
            Assert.That(frames[0].Trace, Does.Contain("Final.PlayerDamage"));
            Assert.That(frames[0].PlayerControlDump, Does.Contain("Action=Push|ActionSeq=1|ActionDirection=Right|ActionTarget=30|Start=1|Execute=3|Recovery=5|Attempted=0"));
            Assert.That(frames[1].PlayerControlDump, Does.Contain("Action=Push|ActionSeq=1|ActionDirection=Right|ActionTarget=30|Start=1|Execute=3|Recovery=5|Attempted=0"));
        }

        [Test]
        [Category("Core")]
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
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "ImpactReservationCreated",
                    "Source=10",
                    "Target=20",
                    "At=(1,0)",
                    "Damage=1",
                    "Sequence=1"),
                Is.True);
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("CleanupRemoved|E=10"));
        }

        [Test]
        [Category("Core")]
        public void Replay_ScriptedMoveIntoUnitStackedScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunScriptedMoveIntoUnitStackedReplaySequence();
            var secondReplay = RunScriptedMoveIntoUnitStackedReplaySequence();

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
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(1,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=20|Pos=(1,0)|Hp=2|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
        }

        [Test]
        [Category("Core")]
        public void Replay_JumpLandingExactShrinkScenario_ProducesDeterministicRetryThenLanding()
        {
            var firstReplay = RunJumpLandingExactShrinkReplaySequence();
            var secondReplay = RunJumpLandingExactShrinkReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay[0].Trace, Does.Contain("Label=Retry"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Contain("Layer=Unit|Cell=(3,1)|E=10|Face=Floor"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Contain("Layer=Unit|Cell=(3,1)|E=60|Face=Floor"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Not.Contain("Layer=Unit|Cell=(3,1)|E=40|Face=Floor"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Label=Landing"));
            Assert.That(firstReplay[1].OccupancyDump, Does.Contain("Layer=Unit|Cell=(3,1)|E=10|Face=Floor"));
            Assert.That(firstReplay[1].OccupancyDump, Does.Contain("Layer=Unit|Cell=(3,1)|E=40|Face=Floor"));
        }

        [Test]
        [Category("Core")]
        public void Replay_OccupancyDump_ListsLayeredEntriesForStackedUnitsInCellOrder()
        {
            var frames = new TickReplayHarness().Run(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(0, 0), hp: 2),
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateProjectile(entityId: 50, teamId: 1, position: new Vector2Int(0, 0), hp: 1),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0)),
                }),
                new IEntityLogic[0],
                new[] { new TickInput(1) });

            Assert.That(
                frames[0].OccupancyDump,
                Is.EqualTo(
                    "Layer=Unit|Cell=(0,0)|E=10|Face=Floor\n" +
                    "Layer=Unit|Cell=(0,0)|E=20|Face=Floor\n" +
                    "Layer=Projectile|Cell=(0,0)|E=50|Face=Floor\n" +
                    "Layer=Solid|Cell=(1,0)|E=30|Face=Floor"));
            Assert.That(frames[0].Trace, Does.Contain("Final.Occupancy"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_StackedUnitOccupancy_IsIncludedInCanonicalState()
        {
            var stackedResult = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(0, 0), hp: 2),
                }))
                .RunTick(new TickInput(1));
            var separatedResult = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
                }))
                .RunTick(new TickInput(1));

            Assert.That(stackedResult.DeterminismHash, Is.Not.EqualTo(separatedResult.DeterminismHash));
            Assert.That(stackedResult.Trace.Text, Does.Contain("Layer=Unit|Cell=(0,0)|E=10|Face=Floor"));
            Assert.That(stackedResult.Trace.Text, Does.Contain("Layer=Unit|Cell=(0,0)|E=20|Face=Floor"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EntityExecutionLockState_IsIncludedInCanonicalState()
        {
            var unlockedWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
            });
            var lockedWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
            });
            lockedWorldState.CreateWriteContext().SetEntityExecutionLockState(
                10,
                new EntityExecutionLockState
                {
                    phase = EntityExecutionPhase.Move,
                    sequence = 7,
                    unlockTickExclusive = 11,
                });

            var unlockedResult = GameplayCompositionRoot.CreateTickPipeline(unlockedWorldState).RunTick(new TickInput(1));
            var lockedResult = GameplayCompositionRoot.CreateTickPipeline(lockedWorldState).RunTick(new TickInput(1));

            Assert.That(unlockedResult.DeterminismHash, Is.Not.EqualTo(lockedResult.DeterminismHash));
            Assert.That(lockedResult.Trace.Text, Does.Contain("Final.ExecutionLocks"));
            Assert.That(lockedResult.Trace.Text, Does.Contain("E=10|Phase=Move|Sequence=7|UnlockTickExclusive=11"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PushBoxEntityStopperScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunPushBoxReplaySequence();
            var secondReplay = RunPushBoxReplaySequence();

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
            Assert.That(firstReplay[0].PlayerControlDump, Does.Contain("Action=Push|ActionSeq=1|ActionDirection=Right|ActionTarget=30"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=30|Pos=(2,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Sliding|Timer=11|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Push"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PushBoxTerrainStopperScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunPushBoxTerrainReplaySequence();
            var secondReplay = RunPushBoxTerrainReplaySequence();

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
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=30|Pos=(2,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Sliding|Timer=11|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Push"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PushBoxBoardEdgeScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunPushBoxBoardEdgeReplaySequence();
            var secondReplay = RunPushBoxBoardEdgeReplaySequence();

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
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=30|Pos=(2,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Sliding|Timer=11|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Push"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PlayerMoveIntoUnitStackedScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunPlayerMoveIntoUnitStackedReplaySequence();
            var secondReplay = RunPlayerMoveIntoUnitStackedReplaySequence();

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
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(1,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=20|Pos=(1,0)|Hp=2|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
        }

        [Test]
        [Category("Core")]
        public void Replay_ItemScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunItemReplaySequence();
            var secondReplay = RunItemReplaySequence();

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
            Assert.That(firstReplay[0].Trace, Does.Contain("Command=Move"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "BoardPresenceCommitted",
                    "E=30",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "DestroyMarked",
                    "Target=30",
                    "Condition=AlwaysMark"),
                Is.True);
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("CleanupRemoved|E=30"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(1,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("E=30|"));
        }

        [Test]
        [Category("Core")]
        public void Replay_CompositeItemAttackScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunCompositeItemAttackReplaySequence();
            var secondReplay = RunCompositeItemAttackReplaySequence();

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
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "BoardPresenceCommitted",
                    "E=30",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "DamageCommitted",
                    "Target=10",
                    "Amount=1"),
                Is.True);
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("CleanupRemoved|E=30"));
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("Target=30|Amount=1"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(1,0)|Hp=2|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("E=30|"));
        }

        [Test]
        [Category("Core")]
        public void Replay_FlipBoxScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunFlipBoxReplaySequence();
            var secondReplay = RunFlipBoxReplaySequence();

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
            Assert.That(firstReplay[0].PlayerControlDump, Does.Contain("Action=Flip|ActionSeq=1|ActionDirection=Left|ActionTarget=30"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "FacingCommitted",
                    "E=10",
                    "Facing=Left"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "MoveCommitted",
                    "E=30",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Left|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain($"E=30|Pos=(1,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities={BoxCapabilities.Flip}"));
        }

        [Test]
        [Category("Core")]
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
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(1,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Left"));
            Assert.That(firstReplay[1].Trace, Does.Not.Contain("Reason=EdgeReserved"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[1].EventLogDump,
                    "MoveCommitted",
                    "E=10",
                    "To=(0,0)",
                    "Facing=Left"),
                Is.True);
        }

        [Test]
        [Category("Core")]
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
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=21|Pos=(1,0)|Hp=1|MaxHp=1|Team=1|Type=Projectile|State=Idle|Timer=12|Facing=Right|Marked=0|SpawnTick=1"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "SpawnCommitted",
                    "SpawnId=1",
                    "E=21",
                    "Pos=(1,0)",
                    "Type=Projectile",
                    "SpawnTick=1"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[13].EventLogDump,
                    "ImpactReservationCreated",
                    "Source=21",
                    "Target=20",
                    "At=(2,0)",
                    "Damage=1",
                    "Sequence=1"),
                Is.True);
            Assert.That(firstReplay[13].EventLogDump, Does.Contain("CleanupRemoved|E=21"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[14].EventLogDump,
                    "SpawnCommitted",
                    "SpawnId=1",
                    "E=22",
                    "Pos=(1,0)",
                    "Type=Projectile",
                    "SpawnTick=15"),
                Is.True);
            Assert.That(firstReplay[14].FinalEntitiesDump, Does.Contain("E=22|Pos=(1,0)|Hp=1|MaxHp=1|Team=1|Type=Projectile|State=Idle|Timer=12|Facing=Right|Marked=0|SpawnTick=15"));
        }

        [Test]
        [Category("Core")]
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
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "ImpactReservationCreated",
                    "Source=5",
                    "Target=20",
                    "At=(1,0)",
                    "Damage=1",
                    "Sequence=1"),
                Is.True);
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("Target=40"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=40|Pos=(0,1)|Hp=2|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0"));
        }

        [Test]
        [Category("Core")]
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
            Assert.That(firstReplay[0].Trace, Does.Contain("SourcePlan=99"));
            Assert.That(firstReplay[0].EventLogDump, Is.EqualTo("<empty>"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Attack.DrainedDelayedEffects"));
            Assert.That(firstReplay[1].Trace, Does.Contain("GeneratedTick=1|ExecuteTick=2"));
            Assert.That(firstReplay[1].Trace, Does.Contain("SourcePlan=99"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("DelayedAttackDrained|Tick=2|Source=10|Target=20|Damage=1|GeneratedTick=1|ExecuteTick=2|Group=99|Sequence=1"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[1].EventLogDump,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=20|Pos=(1,0)|Hp=1|MaxHp=2|Team=2|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0"));
        }

        [Test]
        [Category("Core")]
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
            Assert.That(resultWithDelayedEvent.Trace.Text, Does.Contain("SourcePlan=99"));
        }

        [Test]
        [Category("Core")]
        public void Snapshot_EntityState_PreservesEnemyAiMode()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase, aiStateTimer: 3),
            });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(40, out var entity), Is.True);
            Assert.That(entity.aiMode, Is.EqualTo(EnemyAiMode.Chase));
            Assert.That(entity.aiStateTimer, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void Snapshot_EnemyActionState_PreservesStoredRuntimeState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Attack),
            });
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 3,
                    lockedTargetEntityId = 10,
                    direction = Direction.Left,
                    startTick = 7,
                    executeTick = 9,
                    executionAttempted = false,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEnemyActionState(40, out var actionState), Is.True);
            Assert.That(actionState.kind, Is.EqualTo(EnemyActionKind.Melee));
            Assert.That(actionState.sequence, Is.EqualTo(3));
            Assert.That(actionState.lockedTargetEntityId, Is.EqualTo(10));
            Assert.That(actionState.direction, Is.EqualTo(Direction.Left));
            Assert.That(actionState.startTick, Is.EqualTo(7));
            Assert.That(actionState.executeTick, Is.EqualTo(9));
            Assert.That(actionState.executionAttempted, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Snapshot_EnemyJumpState_PreservesStoredRuntimeState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 4,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 1),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 4, 1),
                    windupEndTick = 7,
                    landingTick = 9,
                    cooldownRemainingTicks = 2,
                    retryCount = 1,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEnemyJumpState(40, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(jumpState.sequence, Is.EqualTo(4));
            Assert.That(jumpState.sourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(jumpState.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 1)));
            Assert.That(jumpState.windupEndTick, Is.EqualTo(7));
            Assert.That(jumpState.landingTick, Is.EqualTo(9));
            Assert.That(jumpState.cooldownRemainingTicks, Is.EqualTo(2));
            Assert.That(jumpState.retryCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void Snapshot_EnemyChargeState_PreservesStoredRuntimeState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Charge),
            });
            worldState.CreateWriteContext().SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 3,
                    lockedDirection = Direction.Right,
                    windupEndTick = 7,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEnemyChargeState(40, out var chargeState), Is.True);
            Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(chargeState.sequence, Is.EqualTo(3));
            Assert.That(chargeState.lockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(chargeState.windupEndTick, Is.EqualTo(7));
        }

        [Test]
        [Category("Core")]
        public void Snapshot_BoxKineticOwner_PreservesStoredRuntimeState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 40, position: new Vector2Int(0, 0)),
            });
            worldState.CreateWriteContext().SetBoxKineticOwner(40, instigatorEntityId: 10, instigatorTeamId: 1);

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(40, out var box), Is.True);
            Assert.That(box.kineticInstigatorEntityId, Is.EqualTo(10));
            Assert.That(box.kineticInstigatorTeamId, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyAiMode_IsIncludedInCanonicalState()
        {
            var idlePipeline = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.None),
                }));
            var chasePipeline = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
                }));

            var idleResult = idlePipeline.RunTick(new TickInput(1));
            var chaseResult = chasePipeline.RunTick(new TickInput(1));
            var chaseReplay = new TickReplayHarness().Run(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
                }),
                new IEntityLogic[0],
                new[] { new TickInput(1) });

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(chaseResult.DeterminismHash));
            Assert.That(chaseResult.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Chase|FromTimer=0|To=Patrol|ToTimer=0|Reason=NoTarget"));
            Assert.That(chaseReplay[0].FinalEntitiesDump, Does.Contain("AiMode=Patrol"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_BoxKineticOwner_IsIncludedInCanonicalState()
        {
            var idleWorldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 40, position: new Vector2Int(0, 0)),
            });
            var kineticWorldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 40, position: new Vector2Int(0, 0)),
            });

            kineticWorldState.CreateWriteContext().SetBoxKineticOwner(40, instigatorEntityId: 10, instigatorTeamId: 1);

            var idleResult = GameplayCompositionRoot.CreateTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var kineticResult = GameplayCompositionRoot.CreateTickPipeline(kineticWorldState).RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                kineticWorldState,
                new IEntityLogic[0],
                new[] { new TickInput(1) });

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(kineticResult.DeterminismHash));
            Assert.That(kineticResult.Trace.Text, Does.Contain("KineticInstigator=10|KineticTeam=1"));
            Assert.That(replay[0].FinalEntitiesDump, Does.Contain("KineticInstigator=10|KineticTeam=1"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyAiStateTimer_IsIncludedInCanonicalState()
        {
            var zeroTimerPipeline = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Recover, aiStateTimer: 0),
                }));
            var timedPipeline = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Recover, aiStateTimer: 2),
                }));

            var zeroTimerResult = zeroTimerPipeline.RunTick(new TickInput(1));
            var timedResult = timedPipeline.RunTick(new TickInput(1));

            Assert.That(zeroTimerResult.DeterminismHash, Is.Not.EqualTo(timedResult.DeterminismHash));
            Assert.That(timedResult.Trace.Text, Does.Contain("AiTimer=1"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyLocomotionCooldown_IsIncludedInCanonicalState()
        {
            var zeroCooldownPipeline = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase, enemyLocomotionCooldownTicks: 0),
                }));
            var cooledDownPipeline = GameplayCompositionRoot.CreateTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase, enemyLocomotionCooldownTicks: 2),
                }));

            var zeroCooldownResult = zeroCooldownPipeline.RunTick(new TickInput(1));
            var cooledDownResult = cooledDownPipeline.RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase, enemyLocomotionCooldownTicks: 2),
                }),
                new IEntityLogic[0],
                new[] { new TickInput(1) });

            Assert.That(zeroCooldownResult.DeterminismHash, Is.Not.EqualTo(cooledDownResult.DeterminismHash));
            Assert.That(cooledDownResult.Trace.Text, Does.Contain("LocomotionCooldown=1"));
            Assert.That(replay[0].FinalEntitiesDump, Does.Contain("LocomotionCooldown=1"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyActionState_IsIncludedInCanonicalState()
        {
            var idleWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Attack),
            });
            var actionWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Attack),
            });
            var replayWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Attack),
            });
            var enemyActionState = new EnemyActionRuntimeState
            {
                kind = EnemyActionKind.Melee,
                sequence = 2,
                lockedTargetEntityId = 10,
                direction = Direction.Left,
                startTick = 4,
                executeTick = 5,
            };
            actionWorldState.CreateWriteContext().SetEnemyActionState(40, enemyActionState);
            replayWorldState.CreateWriteContext().SetEnemyActionState(40, enemyActionState);

            var idleResult = GameplayCompositionRoot.CreateTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var actionResult = GameplayCompositionRoot.CreateTickPipeline(actionWorldState).RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                replayWorldState,
                new IEntityLogic[0],
                new[] { new TickInput(1) });

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(actionResult.DeterminismHash));
            Assert.That(actionResult.Trace.Text, Does.Contain("Final.EnemyActions"));
            Assert.That(actionResult.Trace.Text, Does.Contain("E=40|Kind=Melee|Seq=2|Target=10|Direction=Left|Start=4|Execute=5|Attempted=0"));
            Assert.That(replay[0].EnemyActionDump, Does.Contain("E=40|Kind=Melee|Seq=2|Target=10|Direction=Left|Start=4|Execute=5|Attempted=0"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyJumpState_IsIncludedInCanonicalState()
        {
            var idleWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
            });
            var jumpWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
            });

            jumpWorldState.CreateWriteContext().SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 2,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 1),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 4, 1),
                    windupEndTick = 4,
                    landingTick = 6,
                    cooldownRemainingTicks = 0,
                    retryCount = 3,
                });

            var idleResult = GameplayCompositionRoot.CreateTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var jumpResult = GameplayCompositionRoot.CreateTickPipeline(jumpWorldState).RunTick(new TickInput(1));

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(jumpResult.DeterminismHash));
            Assert.That(jumpResult.Trace.Text, Does.Contain("Final.EnemyJumps"));
            Assert.That(jumpResult.Trace.Text, Does.Contain("E=40|Phase=Airborne|Seq=2|Source=Floor(0,1)|Locked=Floor(4,1)|WindupEnd=4|Landing=6|Cooldown=0|Retry=3"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyChargeState_IsIncludedInCanonicalState()
        {
            var idleWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Charge),
            });
            var chargeWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Charge),
            });
            var replayWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Charge),
            });
            var chargeState = new EnemyChargeRuntimeState
            {
                phase = EnemyChargePhase.Recover,
                sequence = 5,
                lockedDirection = Direction.Right,
                windupEndTick = 4,
            };

            chargeWorldState.CreateWriteContext().SetEnemyChargeState(40, chargeState);
            replayWorldState.CreateWriteContext().SetEnemyChargeState(40, chargeState);

            var idleResult = GameplayCompositionRoot.CreateTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var chargeResult = GameplayCompositionRoot.CreateTickPipeline(chargeWorldState).RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                replayWorldState,
                new IEntityLogic[0],
                new[] { new TickInput(1) });

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(chargeResult.DeterminismHash));
            Assert.That(chargeResult.Trace.Text, Does.Contain("Final.EnemyCharges"));
            Assert.That(chargeResult.Trace.Text, Does.Contain("E=40|Phase=Recover|Seq=5|Direction=Right|WindupEnd=4"));
            Assert.That(replay[0].EnemyChargeDump, Does.Contain("E=40|Phase=Recover|Seq=5|Direction=Right|WindupEnd=4"));
        }

        [Test]
        [Category("Core")]
        public void Replay_EnemyAiScenario_ProducesStablePerTickHashTraceAndFinalState()
        {
            var firstReplay = RunEnemyAiReplaySequence();
            var secondReplay = RunEnemyAiReplaySequence();

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
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=40|Pos=(1,0)|Hp=3"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("AiMode=Chase"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("AiMode=Recover"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("AiTimer=1"));
            Assert.That(firstReplay[1].Trace, Does.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40|From=Chase|FromTimer=0|To=Attack|ToTimer=0|Reason=TargetInRange"));
            Assert.That(firstReplay[1].Trace, Does.Contain("EnemyAiTransition|Stage=AfterAttack|E=40|From=Attack|FromTimer=0|To=Recover|ToTimer=1|Reason=AttackCommitted"));
            Assert.That(firstReplay[2].FinalEntitiesDump, Does.Contain("AiTimer=0"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PassiveContactScenario_ProducesStableHashTraceAndPlayerDamage()
        {
            var firstReplay = RunPassiveContactReplaySequence();
            var secondReplay = RunPassiveContactReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.PlayerDamageDump).ToArray(),
                secondReplay.Select(frame => frame.PlayerDamageDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("SourceKind=PassiveContact"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[0].EventLogDump,
                    "DamageCommitted",
                    "SourceKind=PassiveContact",
                    "Target=10",
                    "Amount=1"),
                Is.True);
            Assert.That(firstReplay[0].PlayerDamageDump, Does.Contain("E=10|NextDamageAllowed="));
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

        private static IReadOnlyList<TickReplayFrame> RunAttackLogicPermutationReplaySequence(bool reverseLogicOrder)
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 0)),
                GameplayTerrainData.Empty);

            var firstLogic = new ScriptedCombatLogic(
                sourceId: 10,
                attackIntent: RawAttackIntent.CreateFireProjectile(10, 5));
            var secondLogic = new ScriptedCombatLogic(
                sourceId: 20,
                attackIntent: RawAttackIntent.CreateFireProjectile(20, 5));
            var entityLogics = reverseLogicOrder
                ? new IEntityLogic[] { secondLogic, firstLogic }
                : new IEntityLogic[] { firstLogic, secondLogic };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                new[]
                {
                    new TickInput(1),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunOffBottomEnemyReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 1, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));

            return new TickReplayHarness().Run(
                worldState,
                Array.Empty<IEntityLogic>(),
                new[]
                {
                    new TickInput(1),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunTopologyRotationAttackSuppressionReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 1), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Attack),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 1,
                    lockedTargetEntityId = 10,
                    direction = Direction.Up,
                    startTick = 0,
                    executeTick = 1,
                });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Move(Direction.Up)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunEnemyAiReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[0],
                new[]
                {
                    new TickInput(1),
                    new TickInput(2),
                    new TickInput(3),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunPassiveContactReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedCombatLogic(
                        sourceId: 40,
                        attackIntent: new RawAttackIntent(40, 5, 10, AttackSourceKind.PassiveContact, localSequence: 1)),
                },
                new[]
                {
                    new TickInput(1),
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

        private static IReadOnlyList<TickReplayFrame> RunScriptedMoveIntoUnitStackedReplaySequence()
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

        private static IReadOnlyList<TickReplayFrame> RunJumpLandingExactShrinkReplaySequence()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var retreatCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 60, teamId: 2, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(10, default);
            writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
            writeContext.SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    windupEndTick = 0,
                    landingTick = 1,
                    cooldownRemainingTicks = 0,
                    retryCount = 0,
                });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedJumpTimingLogic(40, cooldownTicks: 1),
                    new ScriptedCombatLogic(
                        sourceId: 60,
                        movementIntentsByTick: new Dictionary<int, RawMovementIntent>
                        {
                            { 2, new RawMovementIntent(60, 5, retreatCell.PlanarPosition) },
                        }),
                },
                new[]
                {
                    new TickInput(1),
                    new TickInput(2),
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

        private static IReadOnlyList<TickReplayFrame> RunItemReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
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

        private static IReadOnlyList<TickReplayFrame> RunCompositeItemAttackReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item | BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 2, 0), hp: 3),
                CreateUnit(entityId: 50, teamId: 2, position: new SurfaceCell(FaceId.Floor, 1, 1), hp: 3),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                    new ScriptedCombatLogic(sourceId: 40, attackIntent: new RawAttackIntent(40, 10, 30)),
                    new ScriptedCombatLogic(sourceId: 50, attackIntent: new RawAttackIntent(50, 5, 10)),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunPushBoxReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push, facing: Direction.Left),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunPushBoxTerrainReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push, facing: Direction.Left),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunPushBoxBoardEdgeReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push, facing: Direction.Left),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)),
                GameplayTerrainData.Empty);

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunPlayerMoveIntoUnitStackedReplaySequence()
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

        private static IReadOnlyList<TickReplayFrame> RunFlipBoxReplaySequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Flip(Direction.Left)),
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
                        { 15, RawAttackIntent.CreateFireProjectile(10, 5) },
                    }),
            };

            return new TickReplayHarness().Run(
                worldState,
                entityLogics,
                Enumerable.Range(1, 15)
                    .Select(tickIndex => new TickInput(tickIndex))
                    .ToArray());
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

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            int aiStateTimer = 0,
            int enemyLocomotionCooldownTicks = 0)
        {
            return CreateUnit(entityId, teamId, SurfaceCell.FromPlanar(position), hp, aiMode, aiStateTimer, enemyLocomotionCooldownTicks);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            int aiStateTimer = 0,
            int enemyLocomotionCooldownTicks = 0)
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
                aiMode = aiMode,
                aiStateTimer = aiStateTimer,
                enemyLocomotionCooldownTicks = enemyLocomotionCooldownTicks,
            };
        }

        private static EntityState CreateProjectile(int entityId, int teamId, Vector2Int position, int hp)
        {
            return CreateProjectile(entityId, teamId, SurfaceCell.FromPlanar(position), hp);
        }

        private static EntityState CreateProjectile(int entityId, int teamId, SurfaceCell position, int hp)
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
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            Direction facing = Direction.Left)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position), capabilities, facing);
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
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
            return CreateWall(entityId, SurfaceCell.FromPlanar(position));
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

        private static void AssertEquivalentReplayOutputs(
            IReadOnlyList<TickReplayFrame> firstReplay,
            IReadOnlyList<TickReplayFrame> secondReplay)
        {
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
                firstReplay.Select(frame => frame.PlayerControlDump).ToArray(),
                secondReplay.Select(frame => frame.PlayerControlDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.PlayerDamageDump).ToArray(),
                secondReplay.Select(frame => frame.PlayerDamageDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EnemyActionDump).ToArray(),
                secondReplay.Select(frame => frame.EnemyActionDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
        }

        private static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static PlayerLogic CreateImmediatePushPlayerLogic(int entityId)
        {
            return CreatePlayerLogicWithActionTiming(
                entityId,
                pushWindupTicks: 0,
                pushRecoveryTicks: 0);
        }

        private static PlayerLogic CreateImmediateFlipPlayerLogic(int entityId)
        {
            return CreatePlayerLogicWithActionTiming(
                entityId,
                flipWindupTicks: 0,
                flipRecoveryTicks: 0);
        }

        private static PlayerLogic CreatePlayerLogicWithActionTiming(
            int entityId,
            int pushWindupTicks = 1,
            int pushRecoveryTicks = 0,
            int flipWindupTicks = 1,
            int flipRecoveryTicks = 0)
        {
            return new PlayerLogic(
                entityId,
                pushWindupTicks,
                pushRecoveryTicks,
                flipWindupTicks,
                flipRecoveryTicks);
        }

        private sealed class ScriptedJumpTimingLogic : IMovementEntityLogic, IEnemyJumpTimingBinding
        {
            private readonly int _controlledEntityId;
            private readonly int _cooldownTicks;

            public ScriptedJumpTimingLogic(int controlledEntityId, int cooldownTicks)
            {
                _controlledEntityId = controlledEntityId;
                _cooldownTicks = cooldownTicks;
            }

            public int ControlledEntityId => _controlledEntityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
            }

            public bool TryGetJumpCooldownTicks(out int cooldownTicks)
            {
                cooldownTicks = _cooldownTicks;
                return true;
            }
        }

        private sealed class ScriptedCombatLogic : IMovementEntityLogic, IAttackEntityLogic, IReplayTickAwareEntityLogic, IEntityLogicSourceBinding
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

            public int ControlledEntityId => _sourceId;

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
        }
    }
}
