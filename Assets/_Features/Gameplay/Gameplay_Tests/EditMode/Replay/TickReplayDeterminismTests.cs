using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class TickReplayDeterminismTests
    {
        private static EnemyUnitArchetypeAsset SharedSummonedArchetype;
        private static EnemyAiProfile SharedSummonedProfile;
        private static EnemyAiProfile ReplayDefaultProfile;

        [Test]
        [Category("Core")]
        public void TickResult_ExtendsFinalStateAndEventLog_WithoutCrossPhaseBackflow()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(2, 0), hp: 1),
            });
            var pipeline = CreateReplayTickPipeline(
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
                firstReplay.Select(frame => frame.EnemyPatrolDump).ToArray(),
                secondReplay.Select(frame => frame.EnemyPatrolDump).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_UnitMobilityDifference_ChangesHash()
        {
            var groundWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
            });
            var airWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3, unitMobilityKind: UnitMobilityKind.Air),
            });

            var groundHash = CreateReplayTickPipeline(groundWorld, Array.Empty<IEntityLogic>())
                .RunTick(new TickInput(1))
                .DeterminismHash;
            var airHash = CreateReplayTickPipeline(airWorld, Array.Empty<IEntityLogic>())
                .RunTick(new TickInput(1))
                .DeterminismHash;

            Assert.That(groundHash, Is.Not.Empty);
            Assert.That(airHash, Is.Not.Empty);
            Assert.That(groundHash, Is.Not.EqualTo(airHash));
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
        public void DeterminismHash_EntitySpawnPresentationSignals_DoNotAffectCanonicalStateOrHash()
        {
            var player = CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3);
            player.unitRole = UnitRole.Player;
            var finalEntities = new[] { player };
            var finalSnapshot = SnapshotBuilder.Create(CreateWorldState(finalEntities));
            var eventLog = new[]
            {
                "RespawnCommitted|E=10|Pos=(1,0)|Face=Floor|Facing=Left|Tick=12",
            };
            var spawnPresentation = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                entitySpawnSignals: new[]
                {
                    new EntitySpawnPresentationSignal(
                        10,
                        EntityPresentationKind.Player,
                        EntitySpawnPresentationReason.PlayerRespawn,
                        SurfaceCell.FromPlanar(new Vector2Int(1, 0)),
                        finalSnapshot.Topology,
                        Direction.Left,
                        new TileFeaturePresentationSource(
                            100,
                            TileFeatureKind.Entrance,
                            SurfaceCell.FromPlanar(new Vector2Int(1, 0)))),
                });
            var baselineData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                TickPresentationData.Empty);
            var spawnPresentationData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                spawnPresentation);
            var hashBuilder = new DeterminismHashBuilder();

            CollectionAssert.AreEqual(baselineData.FinalEntities, spawnPresentationData.FinalEntities);
            CollectionAssert.AreEqual(baselineData.EventLog, spawnPresentationData.EventLog);
            Assert.That(
                hashBuilder.Build(12, finalSnapshot, baselineData),
                Is.EqualTo(hashBuilder.Build(12, finalSnapshot, spawnPresentationData)));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_BoxSlideStopPresentationData_DoesNotAffectCanonicalStateOrHash()
        {
            var finalEntities = new[]
            {
                CreateBox(entityId: 30, position: new Vector2Int(3, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 90, position: new Vector2Int(4, 0), capabilities: BoxCapabilities.Push),
            };
            finalEntities[0].state = EntityPhaseState.Idle;
            finalEntities[1].state = EntityPhaseState.Idle;
            var finalSnapshot = SnapshotBuilder.Create(CreateWorldState(finalEntities));
            var eventLog = new[]
            {
                "StateChanged|E=30|State=Idle|Timer=0",
            };
            var topology = finalSnapshot.Topology;
            var boxSlideStopPresentation = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                boxSlideStopSignals: new[]
                {
                    new BoxSlideStopPresentationSignal(
                        boxEntityId: 30,
                        sourceCell: new SurfaceCell(FaceId.Floor, 3, 0),
                        stopperCell: new SurfaceCell(FaceId.Floor, 4, 0),
                        slideDirection: Direction.Right,
                        stopperKind: BoxSlideStopperKind.SolidEntity,
                        stopperEntityId: 90,
                        solidKind: SolidKind.Box,
                        topology: topology,
                        cause: BoxSlideStopCause.SlidingContinuationBlocked),
                });
            var baselineData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                TickPresentationData.Empty);
            var boxSlideStopData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                boxSlideStopPresentation);
            var hashBuilder = new DeterminismHashBuilder();

            CollectionAssert.AreEqual(baselineData.FinalEntities, boxSlideStopData.FinalEntities);
            CollectionAssert.AreEqual(baselineData.EventLog, boxSlideStopData.EventLog);
            Assert.That(
                hashBuilder.Build(25, finalSnapshot, baselineData),
                Is.EqualTo(hashBuilder.Build(25, finalSnapshot, boxSlideStopData)));
        }

        [Test]
        [Category("Core")]
        public void Replay_BoundaryMetadata_DoesNotAffectCanonicalHash()
        {
            var finalEntities = new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
            };
            var finalSnapshot = SnapshotBuilder.Create(CreateWorldState(finalEntities));
            var eventLog = new[]
            {
                "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right",
            };
            var boundaryMetadata = new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.Move,
                sourceActorEntityId: 10,
                actionPlanId: 1,
                movementSemanticKind: MovementSemanticKind.Move,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                boundaryReason: "HashExcludedBoundaryMetadata");
            var baselineData = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                eventLog,
                TickPresentationData.Empty);
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                boundaryMetadata.MovementExecutionBoundaryKind,
                Is.EqualTo(MovementExecutionBoundaryKind.LocomotionAnchorCommit));
            Assert.That(eventLog, Has.None.Contains(nameof(MovementExecutionBoundaryKind)));
            Assert.That(
                hashBuilder.Build(13, finalSnapshot, baselineData),
                Is.EqualTo(hashBuilder.Build(13, finalSnapshot, baselineData)));
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
        public void Replay_JumpLandingExactShrinkScenario_ProducesDeterministicLandingWithStackedUnits()
        {
            var firstReplay = RunJumpLandingExactShrinkReplaySequence();
            var secondReplay = RunJumpLandingExactShrinkReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay[0].Trace, Does.Contain("Label=Landing"));
            Assert.That(firstReplay[0].Trace, Does.Contain("Rule=TargetExact"));
            Assert.That(firstReplay[0].Trace, Does.Contain("Target=0"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Contain("Layer=Unit|Cell=(3,1)|E=10|Face=Floor"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Contain("Layer=Unit|Cell=(3,1)|E=60|Face=Floor"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Contain("Layer=Unit|Cell=(3,1)|E=40|Face=Floor"));
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
            var stackedResult = CreateReplayTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(0, 0), hp: 2),
                }))
                .RunTick(new TickInput(1));
            var separatedResult = CreateReplayTickPipeline(
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

            var unlockedResult = CreateReplayTickPipeline(unlockedWorldState).RunTick(new TickInput(1));
            var lockedResult = CreateReplayTickPipeline(lockedWorldState).RunTick(new TickInput(1));

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
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("MoveCommitted|E=30"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[1].EventLogDump,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=7"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[1].EventLogDump,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=30|Pos=(2,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Sliding|Timer=6|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Push"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PushBoxTerrainStopperScenario_ProducesSameHashTraceAndEventLog()
        {
            var firstReplay = RunPushBoxSolidReplaySequence();
            var secondReplay = RunPushBoxSolidReplaySequence();

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
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("MoveCommitted|E=30"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=30|Pos=(2,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Sliding|Timer=6|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Push"));
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
            Assert.That(firstReplay[0].PlayerControlDump, Does.Contain("Action=Push|ActionSeq=1|ActionDirection=Right|ActionTarget=30"));
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("MoveCommitted|E=30"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=30|Pos=(2,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Sliding|Timer=6|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=Push"));
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
            Assert.That(firstReplay[0].Trace, Does.Contain("Boundary=BoxActionMovement"));
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
            Assert.That(firstReplay[0].Trace, Does.Contain("Boundary=BoxActionMovement"));
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
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("MoveCommitted|E=30"));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstReplay[1].EventLogDump,
                    "MoveCommitted",
                    "E=30",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3|MaxHp=3|Team=1|Type=Unit|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities=None"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain($"E=30|Pos=(1,0)|Hp=1|MaxHp=1|Team=0|Type=Box|State=Idle|Timer=0|Facing=Right|Marked=0|SpawnTick=0|BoxCapabilities={BoxCapabilities.Flip}"));
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
            var pipelineWithoutDelayedEvent = CreateReplayTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
                }));
            var pipelineWithDelayedEvent = CreateReplayTickPipeline(
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
                    sourceActionPlanId: 99,
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
        public void Snapshot_EnemyPatrolState_PreservesStoredRuntimeState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
            });
            worldState.CreateWriteContext().SetEnemyPatrolState(
                40,
                new EnemyPatrolRuntimeState
                {
                    sequence = 4,
                    homeCell = new SurfaceCell(FaceId.Floor, 1, 1),
                    lastCommittedDirection = Direction.Left,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEnemyPatrolState(40, out var patrolState), Is.True);
            Assert.That(patrolState.sequence, Is.EqualTo(4));
            Assert.That(patrolState.homeCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(patrolState.lastCommittedDirection, Is.EqualTo(Direction.Left));
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
                    remainingActiveSteps = 2,
                    recoverRemainingTicks = 0,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEnemyChargeState(40, out var chargeState), Is.True);
            Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(chargeState.sequence, Is.EqualTo(3));
            Assert.That(chargeState.lockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(chargeState.windupEndTick, Is.EqualTo(7));
            Assert.That(chargeState.remainingActiveSteps, Is.EqualTo(2));
            Assert.That(chargeState.recoverRemainingTicks, Is.Zero);
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
            var idlePipeline = CreateReplayTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.None),
                }));
            var chasePipeline = CreateReplayTickPipeline(
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

            var idleResult = CreateReplayTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var kineticResult = CreateReplayTickPipeline(kineticWorldState).RunTick(new TickInput(1));
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
            var zeroTimerPipeline = CreateReplayTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Recover, aiStateTimer: 0),
                }));
            var timedPipeline = CreateReplayTickPipeline(
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
            var zeroCooldownPipeline = CreateReplayTickPipeline(
                CreateWorldState(new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase, enemyLocomotionCooldownTicks: 0),
                }));
            var cooledDownPipeline = CreateReplayTickPipeline(
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
        public void Replay_EnemyPatrolAndActionState_AffectDeterminismHash()
        {
            var idleActionWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Attack),
            });
            var actionWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Attack),
            });
            actionWorldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 2,
                    lockedTargetEntityId = 10,
                    direction = Direction.Left,
                    startTick = 4,
                    executeTick = 5,
                });

            var idlePatrolWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(0, 2), hp: 2, aiMode: EnemyAiMode.Patrol),
            });
            var patrolWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(0, 2), hp: 2, aiMode: EnemyAiMode.Patrol),
            });
            patrolWorldState.CreateWriteContext().SetEnemyPatrolState(
                41,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = new SurfaceCell(FaceId.Floor, 1, 1),
                    lastCommittedDirection = Direction.Right,
                });

            var idleActionResult = CreateReplayTickPipeline(idleActionWorldState).RunTick(new TickInput(1));
            var actionResult = CreateReplayTickPipeline(actionWorldState).RunTick(new TickInput(1));
            var idlePatrolResult = CreateReplayTickPipeline(idlePatrolWorldState).RunTick(new TickInput(1));
            var patrolResult = CreateReplayTickPipeline(patrolWorldState).RunTick(new TickInput(1));

            Assert.That(idleActionResult.DeterminismHash, Is.Not.EqualTo(actionResult.DeterminismHash));
            Assert.That(actionResult.Trace.Text, Does.Contain("Final.EnemyActions"));
            Assert.That(actionResult.Trace.Text, Does.Contain("E=40|Kind=Melee|Seq=2|Target=10|Direction=Left|Start=4|Execute=5|Attempted=0"));
            Assert.That(idlePatrolResult.DeterminismHash, Is.Not.EqualTo(patrolResult.DeterminismHash));
            Assert.That(patrolResult.Trace.Text, Does.Contain("Final.EnemyPatrols"));
            Assert.That(patrolResult.Trace.Text, Does.Contain("E=41|Seq="));
            Assert.That(patrolResult.Trace.Text, Does.Contain("Home=Floor(1,1)"));
            Assert.That(patrolResult.Trace.Text, Does.Contain("LastDirection=Right"));
        }

        [Test]
        [Category("Core")]
        public void Replay_EnemyAiKinematicScenario_DeterministicCanonicalState()
        {
            var firstReplay = RunWindupForwardCellProjectileRandomWalkReplaySequence();
            var secondReplay = RunWindupForwardCellProjectileRandomWalkReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EnemyPatrolDump).ToArray(),
                secondReplay.Select(frame => frame.EnemyPatrolDump).ToArray());
            Assert.That(firstReplay.Select(frame => frame.EnemyPatrolDump), Has.All.Not.EqualTo("<empty>"));
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

            var jumpWriteContext = jumpWorldState.CreateWriteContext();
            jumpWriteContext.SetBoardPresence(40, EntityBoardPresence.Detached);
            jumpWriteContext.SetEnemyJumpState(
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

            var idleResult = CreateReplayTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var jumpResult = CreateReplayTickPipeline(jumpWorldState).RunTick(new TickInput(1));

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(jumpResult.DeterminismHash));
            Assert.That(jumpResult.Trace.Text, Does.Contain("Final.EnemyJumps"));
            Assert.That(jumpResult.Trace.Text, Does.Contain("E=40|Phase=Airborne|Seq=2|Source=Floor(0,1)|Locked=Floor(4,1)|WindupEnd=4|Landing=6|Cooldown=0|Retry=3"));
        }

        [Test]
        [Category("Extended")]
        public void DeterminismHash_EnemyGlideState_IsIncludedInCanonicalState()
        {
            var idleWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
            });
            var glideWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
            });

            glideWorldState.CreateWriteContext().SetEnemyGlideState(
                40,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 2,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 8,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 1,
                    durationTicks: 3,
                    recoveryTicks: 1,
                    cooldownTicks: 2,
                    lastExitedTick: 0));

            var idleResult = CreateReplayTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var glideResult = CreateReplayTickPipeline(glideWorldState).RunTick(new TickInput(1));

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(glideResult.DeterminismHash));
            Assert.That(glideResult.Trace.Text, Does.Contain("Final.EnemyGlides"));
            Assert.That(glideResult.Trace.Text, Does.Contain("E=40|Phase=Active|Active=1|WantsRecover=0|Seq=2|WindupUntil=0|ActiveUntil=8|RecoveryUntil=0|CooldownUntil=0|Windup=1|Duration=3|Recovery=1|Cooldown=2|GlideMoveTicks=2|LastExited=0"));
            Assert.That(glideResult.Trace.Text, Does.Contain("LockedTarget=0"));
        }

        [Test]
        [Category("Extended")]
        public void DeterminismHash_EnemyGlideLockedTarget_IsIncludedInCanonicalState()
        {
            var firstWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
            });
            var secondWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Chase),
            });

            firstWorldState.CreateWriteContext().SetEnemyGlideState(
                40,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 2,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 8,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 1,
                    durationTicks: 3,
                    recoveryTicks: 1,
                    cooldownTicks: 2,
                    lastExitedTick: 0,
                    lockedTargetEntityId: 10));
            secondWorldState.CreateWriteContext().SetEnemyGlideState(
                40,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 2,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 8,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 1,
                    durationTicks: 3,
                    recoveryTicks: 1,
                    cooldownTicks: 2,
                    lastExitedTick: 0,
                    lockedTargetEntityId: 20));

            var firstResult = CreateReplayTickPipeline(firstWorldState).RunTick(new TickInput(1));
            var secondResult = CreateReplayTickPipeline(secondWorldState).RunTick(new TickInput(1));

            Assert.That(firstResult.DeterminismHash, Is.Not.EqualTo(secondResult.DeterminismHash));
            Assert.That(firstResult.Trace.Text, Does.Contain("LockedTarget=10"));
            Assert.That(secondResult.Trace.Text, Does.Contain("LockedTarget=20"));
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
                remainingActiveSteps = 0,
                recoverRemainingTicks = 2,
            };

            chargeWorldState.CreateWriteContext().SetEnemyChargeState(40, chargeState);
            replayWorldState.CreateWriteContext().SetEnemyChargeState(40, chargeState);

            var idleResult = CreateReplayTickPipeline(idleWorldState).RunTick(new TickInput(1));
            var chargeResult = CreateReplayTickPipeline(chargeWorldState).RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                replayWorldState,
                new IEntityLogic[0],
                new[] { new TickInput(1) });

            Assert.That(idleResult.DeterminismHash, Is.Not.EqualTo(chargeResult.DeterminismHash));
            Assert.That(chargeResult.Trace.Text, Does.Contain("Final.EnemyCharges"));
            Assert.That(chargeResult.Trace.Text, Does.Contain("E=40|Phase=Recover|Seq=5|Direction=Right|WindupEnd=4|ActiveSteps=0|RecoverTicks=2"));
            Assert.That(replay[0].EnemyChargeDump, Does.Contain("E=40|Phase=Recover|Seq=5|Direction=Right|WindupEnd=4|ActiveSteps=0|RecoverTicks=2"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyChargePresentationGuard_DoesNotMutateCanonicalStateOrHash()
        {
            var enemy = CreateUnit(
                entityId: 40,
                teamId: 2,
                position: new Vector2Int(0, 1),
                hp: 2,
                aiMode: EnemyAiMode.Charge);
            var worldState = CreateWorldState(
                new[] { enemy },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                new CubeTopologyState(FaceId.Front));
            var chargeState = new EnemyChargeRuntimeState
            {
                phase = EnemyChargePhase.Active,
                sequence = 7,
                lockedDirection = Direction.Right,
                remainingActiveSteps = 2,
            };
            worldState.CreateWriteContext().SetEnemyChargeState(40, chargeState);
            var finalSnapshot = worldState.CreateSnapshot();

            var presentationData = new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    finalSnapshot,
                    CanonicalPhaseResultFactory.CreateMovementPhaseResult(),
                    AttackPhaseResult.Empty,
                    CleanupFixtureFactory.None(),
                    currentTickIndex: 4));
            var baselineData = new TickResultData(
                new[] { enemy },
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                TickPresentationData.Empty);
            var guardedPresentationData = new TickResultData(
                new[] { enemy },
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                presentationData);
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(presentationData.EnemyChargeSignals, Is.Empty);
            Assert.That(finalSnapshot.TryGetEnemyChargeState(40, out var finalChargeState), Is.True);
            Assert.That(finalChargeState.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(
                hashBuilder.Build(4, finalSnapshot, baselineData),
                Is.EqualTo(hashBuilder.Build(4, finalSnapshot, guardedPresentationData)));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyUtilityState_IsIncludedInCanonicalState()
        {
            var baselineWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
            });
            var utilityWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
            });
            var utilitySuppressionWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
            });
            var utilitySuspendedWindowWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
            });
            var replayWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
            });

            utilityWorldState.SetEnemyUtilityState(
                40,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState { cooldownTicksRemaining = 2 },
                    }));
            utilitySuppressionWorldState.SetEnemyUtilityState(
                40,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            cooldownTicksRemaining = 2,
                            phase = EnemyUtilityEffectPhase.Recover,
                            recoverStartTick = 3,
                            recoverEndTickExclusive = 5,
                            movementSuppressionUntilTickInclusive = 4,
                        },
                    }));
            utilitySuspendedWindowWorldState.SetEnemyUtilityState(
                40,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            phase = EnemyUtilityEffectPhase.Windup,
                            windupStartTick = 1,
                            windupEndTick = 4,
                            activationSequence = 1,
                        },
                    }));
            replayWorldState.SetEnemyUtilityState(
                40,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState { cooldownTicksRemaining = 2 },
                    }));

            var baselineResult = CreateReplayTickPipeline(baselineWorldState).RunTick(new TickInput(1));
            var utilityResult = CreateReplayTickPipeline(utilityWorldState).RunTick(new TickInput(1));
            var utilitySuppressionResult = CreateReplayTickPipeline(utilitySuppressionWorldState).RunTick(new TickInput(1));
            var utilitySuspendedWindowResult = CreateReplayTickPipeline(utilitySuspendedWindowWorldState).RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                replayWorldState,
                Array.Empty<IEntityLogic>(),
                new[] { new TickInput(1) });

            Assert.That(baselineResult.DeterminismHash, Is.Not.EqualTo(utilityResult.DeterminismHash));
            Assert.That(utilitySuppressionResult.DeterminismHash, Is.Not.EqualTo(utilityResult.DeterminismHash));
            Assert.That(utilitySuspendedWindowResult.DeterminismHash, Is.Not.EqualTo(utilityResult.DeterminismHash));
            Assert.That(utilityResult.Trace.Text, Does.Contain("Final.EnemyUtilities"));
            Assert.That(utilityResult.Trace.Text, Does.Contain("E=40|Effect=0|Cooldown=2"));
            Assert.That(utilitySuppressionResult.Trace.Text, Does.Contain("Phase=Recover"));
            Assert.That(utilitySuppressionResult.Trace.Text, Does.Contain("RecoverStart=3|RecoverEnd=5"));
            Assert.That(utilitySuppressionResult.Trace.Text, Does.Contain("MoveSuppressUntil=4"));
            Assert.That(replay[0].Trace, Does.Contain("Final.EnemyUtilities"));
            Assert.That(replay[0].Trace, Does.Contain("E=40|Effect=0|Cooldown=2"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_BoxInteractionLockState_IsIncludedInCanonicalState()
        {
            var baselineWorldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
            });
            var lockedWorldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
            });
            var replayWorldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
            });

            lockedWorldState.SetBoxInteractionLockState(20, new BoxInteractionLockState(40, 0, 3, blocksPush: true, blocksFlip: false));
            replayWorldState.SetBoxInteractionLockState(20, new BoxInteractionLockState(40, 0, 3, blocksPush: true, blocksFlip: false));

            var baselineResult = CreateReplayTickPipeline(baselineWorldState).RunTick(new TickInput(1));
            var lockedResult = CreateReplayTickPipeline(lockedWorldState).RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                replayWorldState,
                Array.Empty<IEntityLogic>(),
                new[] { new TickInput(1) });

            Assert.That(baselineResult.DeterminismHash, Is.Not.EqualTo(lockedResult.DeterminismHash));
            Assert.That(lockedResult.Trace.Text, Does.Contain("Final.BoxInteractionLocks"));
            Assert.That(lockedResult.Trace.Text, Does.Contain("Box=20"));
            Assert.That(lockedResult.Trace.Text, Does.Contain("Source=40"));
            Assert.That(lockedResult.Trace.Text, Does.Contain("Effect=0"));
            Assert.That(lockedResult.Trace.Text, Does.Contain("Expires=3"));
            Assert.That(lockedResult.Trace.Text, Does.Contain("BlocksPush=1"));
            Assert.That(lockedResult.Trace.Text, Does.Contain("BlocksFlip=0"));
            Assert.That(replay[0].Trace, Does.Contain("Final.BoxInteractionLocks"));
            Assert.That(replay[0].Trace, Does.Contain("Box=20"));
            Assert.That(replay[0].Trace, Does.Contain("Source=40"));
            Assert.That(replay[0].Trace, Does.Contain("Effect=0"));
            Assert.That(replay[0].Trace, Does.Contain("Expires=3"));
            Assert.That(replay[0].Trace, Does.Contain("BlocksPush=1"));
            Assert.That(replay[0].Trace, Does.Contain("BlocksFlip=0"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_SummonedEntityMetadata_IsIncludedInCanonicalState()
        {
            var baselineWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 1), hp: 1, aiMode: EnemyAiMode.None),
            });
            var summonedWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 1), hp: 1, aiMode: EnemyAiMode.None),
            });
            var replayWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 1), hp: 1, aiMode: EnemyAiMode.None),
            });

            summonedWorldState.SetSummonedEntityState(41, new SummonedEntityState(40, 0));
            replayWorldState.SetSummonedEntityState(41, new SummonedEntityState(40, 0));

            var baselineResult = CreateReplayTickPipeline(baselineWorldState).RunTick(new TickInput(1));
            var summonedResult = CreateReplayTickPipeline(summonedWorldState).RunTick(new TickInput(1));
            var replay = new TickReplayHarness().Run(
                replayWorldState,
                Array.Empty<IEntityLogic>(),
                new[] { new TickInput(1) });

            Assert.That(baselineResult.DeterminismHash, Is.Not.EqualTo(summonedResult.DeterminismHash));
            Assert.That(summonedResult.Trace.Text, Does.Contain("Final.SummonedEntities"));
            Assert.That(summonedResult.Trace.Text, Does.Contain("E=41|Source=40|Effect=0"));
            Assert.That(replay[0].Trace, Does.Contain("Final.SummonedEntities"));
            Assert.That(replay[0].Trace, Does.Contain("E=41|Source=40|Effect=0"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_EnemyDefinitionBindingState_IsIncludedInCanonicalState()
        {
            var profile = CreateUtilityProfile();
            var archetypeId = new EnemyUnitArchetypeId("BoundMinion");

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var bootstrapper = new GameplayBootstrapper(
                    GameplayEntityLogicProviderFactory.CreateDefault(
                        definition,
                        definitionsByArchetypeId: new Dictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition>(EnemyUnitArchetypeId.EqualityComparer)
                        {
                            { archetypeId, definition },
                        }));
                var baselineWorldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
                });
                var boundWorldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
                });
                var replayWorldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(0, 1), hp: 2, aiMode: EnemyAiMode.Patrol),
                });

                boundWorldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(archetypeId));
                replayWorldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(archetypeId));

                var baselineResult = bootstrapper.CreateTickPipeline(baselineWorldState).RunTick(new TickInput(1));
                var boundResult = bootstrapper.CreateTickPipeline(boundWorldState).RunTick(new TickInput(1));
                var replay = new TickReplayHarness().Run(
                    bootstrapper,
                    replayWorldState,
                    Array.Empty<IEntityLogic>(),
                    new[] { new TickInput(1) });

                Assert.That(baselineResult.DeterminismHash, Is.Not.EqualTo(boundResult.DeterminismHash));
                Assert.That(boundResult.Trace.Text, Does.Contain("Final.EnemyDefinitionBindings"));
                Assert.That(boundResult.Trace.Text, Does.Contain("E=41|Archetype=BoundMinion"));
                Assert.That(replay[0].Trace, Does.Contain("Final.EnemyDefinitionBindings"));
                Assert.That(replay[0].Trace, Does.Contain("E=41|Archetype=BoundMinion"));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void Replay_EnemyUtilitySummonScenario_ProducesStablePerTickHashTraceAndSummonMetadata()
        {
            var firstReplay = RunUtilitySummonReplaySequence();
            var secondReplay = RunUtilitySummonReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay[0].Trace, Does.Contain("Final.EnemyUtilities"));
            Assert.That(firstReplay[0].Trace, Does.Contain("E=40|Effect=0|Cooldown=0|Phase=Windup"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("E=41|"));
            Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("SummonCommitted|Source=40"));
            Assert.That(firstReplay[1].Trace, Does.Contain("PreMovement.UtilityTriggers"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Source=40|Effect=0|Kind=SummonMinion|Tick=2"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Final.EnemyUtilities"));
            Assert.That(firstReplay[1].Trace, Does.Contain("E=40|Effect=0|Cooldown=2|Phase=None"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Final.SummonedEntities"));
            Assert.That(firstReplay[1].Trace, Does.Contain("E=41|Source=40|Effect=0"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Final.EnemyDefinitionBindings"));
            Assert.That(firstReplay[1].Trace, Does.Contain("E=41|Archetype=BasicMinion"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=41|Pos=(1,0)|Hp=1"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=41|Pos=(1,0)|Archetype=BasicMinion|Tick=2"));
            Assert.That(firstReplay[1].EventLogDump, Does.Not.Contain("DefinitionMode="));
            Assert.That(firstReplay[1].EventLogDump, Does.Not.Contain("SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=42"));
        }

        [Test]
        [Category("Core")]
        public void Replay_EnemyUtilityArchetypeSummonScenario_ProducesStableBindingHashTraceAndMetadata()
        {
            var firstReplay = RunUtilityArchetypeSummonReplaySequence();
            var secondReplay = RunUtilityArchetypeSummonReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay[0].Trace, Does.Contain("E=40|Effect=0|Cooldown=0|Phase=Windup"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("E=41|"));
            Assert.That(firstReplay[1].Trace, Does.Contain("PreMovement.UtilityTriggers"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Source=40|Effect=0|Kind=SummonMinion|Tick=2"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Final.SummonedEntities"));
            Assert.That(firstReplay[1].Trace, Does.Contain("E=41|Source=40|Effect=0"));
            Assert.That(firstReplay[1].Trace, Does.Contain("Final.EnemyDefinitionBindings"));
            Assert.That(firstReplay[1].Trace, Does.Contain("E=41|Archetype=BasicMinion"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=41|Pos=(1,0)|Hp=4|MaxHp=4|Team=2|Type=Unit"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=41|Pos=(1,0)|Archetype=BasicMinion|Tick=2"));
            Assert.That(firstReplay[1].EventLogDump, Does.Not.Contain("DefinitionMode="));
        }



        [Test]
        [Category("Core")]
        public void Replay_WindupForwardCellProjectileRandomWalk_ProducesStableHashTrace_AndBoundedPatrolDump()
        {
            var firstReplay = RunWindupForwardCellProjectileRandomWalkReplaySequence();
            var secondReplay = RunWindupForwardCellProjectileRandomWalkReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EnemyPatrolDump).ToArray(),
                secondReplay.Select(frame => frame.EnemyPatrolDump).ToArray());
            Assert.That(firstReplay[0].Trace, Does.Contain("Final.EnemyPatrols"));
            Assert.That(firstReplay[0].EnemyPatrolDump, Does.Contain("E=40|Seq=1|Home=Floor(0,0)|LastDirection=None"));
            Assert.That(firstReplay[1].Trace, Does.Not.Contain("EnemyPatrolStateUpdated|E=40|Label=Initialized"));
            Assert.That(firstReplay[1].EnemyPatrolDump, Does.Contain("E=40|Seq=1|Home=Floor(0,0)|LastDirection=None"));
        }

        [Test]
        [Category("Core")]
        public void Replay_WindupForwardCellProjectileRandomWalk_PatrolDump_MatchesFinalSnapshotState()
        {
            var replay = RunWindupForwardCellProjectileRandomWalkReplaySequence();
            var snapshotDumps = RunWindupForwardCellProjectileRandomWalkSnapshotDumpSequence();

            CollectionAssert.AreEqual(
                snapshotDumps,
                replay.Select(frame => frame.EnemyPatrolDump).ToArray());
        }


        [Test]
        [Category("Core")]
        public void DeterminismHash_WindupForwardCellProjectileRandomWalk_PatrolFootprint_IsLimitedToEnemyPatrolRuntimeState()
        {
            var replay = RunWindupForwardCellProjectileRandomWalkReplaySequence();

            Assert.That(replay.Select(frame => frame.EnemyPatrolDump), Has.All.Not.EqualTo("<empty>"));
            foreach (var frame in replay)
            {
                Assert.That(frame.EnemyPatrolDump.StartsWith("E=40|Seq=", StringComparison.Ordinal), Is.True);
                Assert.That(frame.EnemyPatrolDump.Contains("|Home=Floor(", StringComparison.Ordinal), Is.True);
                Assert.That(frame.EnemyPatrolDump.Contains("|LastDirection=", StringComparison.Ordinal), Is.True);
            }

            Assert.That(replay.Select(frame => frame.Trace), Has.All.Not.Contains("EnemyPatrolStateUpdated|E=40|Label=Initialized|Seq=3"));
        }



        [Test]
        [Category("Core")]
        public void Replay_ForwardProfile_BlockedStop_ProducesStableNoMove_NoPatrolTrace()
        {
            var firstReplay = RunForwardBlockedStopReplaySequence();
            var secondReplay = RunForwardBlockedStopReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                secondReplay.Select(frame => frame.EventLogDump).ToArray());
            Assert.That(firstReplay[0].EventLogDump, Is.EqualTo("<empty>"));
            Assert.That(firstReplay[0].Trace, Does.Not.Contain("EnemyPatrolStateUpdated|E=40"));
            Assert.That(firstReplay[0].EnemyPatrolDump, Is.EqualTo("<empty>"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=40|Pos=(0,0)|Hp=3"));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("Facing=Right"));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_ForwardProposalPath_DoesNotCreateEnemyPatrolStateFootprint()
        {
            var replay = RunForwardPatrolReplaySequence();

            Assert.That(replay.Select(frame => frame.EnemyPatrolDump), Has.All.EqualTo("<empty>"));
            Assert.That(replay.Select(frame => frame.Trace), Has.All.Not.Contains("EnemyPatrolStateUpdated|E=40"));
        }

        [Test]
        [Category("Core")]
        public void Replay_WallFollowerProfile_ProducesStableHashTrace_AndNoPatrolStateWrites()
        {
            var firstReplay = RunWallFollowPatrolReplaySequence();
            var secondReplay = RunWallFollowPatrolReplaySequence();

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
            Assert.That(firstReplay.Select(frame => frame.EnemyPatrolDump), Has.All.EqualTo("<empty>"));
            Assert.That(firstReplay.Select(frame => frame.Trace), Has.All.Not.Contains("EnemyPatrolStateUpdated|E=40"));
        }

        [Test]
        [Category("Core")]
        public void Replay_WallFollower_HandRuleSeek_StableHash()
        {
            var firstReplay = RunWallFollowSeekReplaySequence();
            var secondReplay = RunWallFollowSeekReplaySequence();

            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                secondReplay.Select(frame => frame.DeterminismHash).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                secondReplay.Select(frame => frame.Trace).ToArray());
            CollectionAssert.AreEqual(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray());
            Assert.That(firstReplay.Select(frame => frame.EnemyPatrolDump), Has.All.EqualTo("<empty>"));
            Assert.That(firstReplay.Select(frame => frame.Trace), Has.All.Not.Contains("EnemyPatrolStateUpdated|E=40"));
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
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=2"));
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
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 0)));

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
                new CubeTopologyState(FaceId.Floor));
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

        private static IReadOnlyList<TickReplayFrame> RunUtilitySummonReplaySequence()
        {
            var utilityProfile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 2);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1)));

            try
            {
                var bootstrapper = CreateSharedSummonBootstrapper(utilityProfile, out defaultProfile, out archetypeCatalog);

                return new TickReplayHarness().Run(
                    bootstrapper,
                    worldState,
                    Array.Empty<IEntityLogic>(),
                    new[]
                    {
                        new TickInput(1),
                        new TickInput(2),
                    });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                EnemyAiProfileTestFactory.Destroy(defaultProfile);
                EnemyAiProfileTestFactory.Destroy(utilityProfile);
            }
        }

        private static IReadOnlyList<TickReplayFrame> RunUtilityArchetypeSummonReplaySequence()
        {
            var defaultProfile = CreateUtilityProfile();
            var archetypeProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
            });
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var catalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var utilityProfile = CreateUtilitySummonProfile(
                initialDelayTicks: 0,
                cooldownTicks: 2,
                summonedArchetype: archetype);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1)));

            try
            {
                return new TickReplayHarness().Run(
                    CreateArchetypeBootstrapper(defaultProfile, utilityProfile, catalog),
                    worldState,
                    Array.Empty<IEntityLogic>(),
                    new[]
                    {
                        new TickInput(1),
                        new TickInput(2),
                    });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                EnemyAiProfileTestFactory.Destroy(archetypeProfile);
                EnemyAiProfileTestFactory.Destroy(defaultProfile);
                EnemyAiProfileTestFactory.Destroy(utilityProfile);
            }
        }

        private static IReadOnlyList<TickReplayFrame> RunRandomWalkPatrolReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(2, 2), hp: 3, aiMode: EnemyAiMode.Patrol),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking();

            try
            {
                return new TickReplayHarness().Run(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    },
                    new[]
                    {
                        new TickInput(1),
                        new TickInput(2),
                        new TickInput(3),
                        new TickInput(4),
                        new TickInput(5),
                    });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static IReadOnlyList<TickReplayFrame> RunWindupForwardCellProjectileRandomWalkReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectileRandomWalk(windupTicks: 1);

            try
            {
                return new TickReplayHarness().Run(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    },
                    new[]
                    {
                        new TickInput(1),
                        new TickInput(2),
                        new TickInput(3),
                        new TickInput(4),
                        new TickInput(5),
                        new TickInput(6),
                    });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static IReadOnlyList<string> RunWindupForwardCellProjectileRandomWalkSnapshotDumpSequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectileRandomWalk(windupTicks: 1);
            var dumps = new List<string>();

            try
            {
                var pipeline = CreateReplayTickPipeline(worldState, profile);
                for (var tickIndex = 1; tickIndex <= 6; tickIndex++)
                {
                    pipeline.RunTick(new TickInput(tickIndex));
                    dumps.Add(BuildEnemyPatrolDump(worldState.CreateSnapshot()));
                }

                return dumps;
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static string BuildEnemyPatrolDump(WorldSnapshot snapshot)
        {
            var entries = new List<EnemyPatrolSnapshotEntry>();
            snapshot.EnumerateEnemyPatrolStatesOrdered(entries);
            if (entries.Count == 0)
            {
                return "<empty>";
            }

            return string.Join(
                "\n",
                entries.Select(entry =>
                    $"E={entry.EntityId}|Seq={entry.State.sequence}|Home={entry.State.homeCell}|LastDirection={entry.State.lastCommittedDirection}"));
        }

        private static IReadOnlyList<TickReplayFrame> RunForwardPatrolReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.Forward,
                PatrolSettings = new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });

            try
            {
                return new TickReplayHarness().Run(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    },
                    new[]
                    {
                        new TickInput(1),
                        new TickInput(2),
                        new TickInput(3),
                    });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static IReadOnlyList<TickReplayFrame> RunWallFollowPatrolReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var profile = EnemyAiProfileTestFactory.CreateWallFollower(WallFollowTurnPreference.Right);

            try
            {
                return new TickReplayHarness().Run(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    },
                    new[]
                    {
                        new TickInput(1),
                        new TickInput(2),
                        new TickInput(3),
                        new TickInput(4),
                        new TickInput(5),
                        new TickInput(6),
                        new TickInput(7),
                        new TickInput(8),
                    });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static IReadOnlyList<TickReplayFrame> RunWallFollowSeekReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));
            var profile = EnemyAiProfileTestFactory.CreateWallFollower(WallFollowTurnPreference.Left);

            try
            {
                return new TickReplayHarness().Run(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    },
                    new[]
                    {
                        new TickInput(1),
                        new TickInput(2),
                        new TickInput(3),
                    });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static IReadOnlyList<TickReplayFrame> RunForwardBlockedStopReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0)));
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.Forward,
                PatrolSettings = new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });

            try
            {
                return new TickReplayHarness().Run(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    },
                    new[]
                    {
                        new TickInput(1),
                    });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
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
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
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
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                    new TickInput(2, PlayerTickCommand.Move(Direction.Up)),
                });
        }

        private static IReadOnlyList<TickReplayFrame> RunPushBoxSolidReplaySequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push, facing: Direction.Left),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                    new TickInput(2, PlayerTickCommand.Move(Direction.Up)),
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
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));

            return new TickReplayHarness().Run(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                    new TickInput(2, PlayerTickCommand.Move(Direction.Up)),
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
                    new PlayerLogic(10),
                },
                new[]
                {
                    new TickInput(1, PlayerTickCommand.Flip(Direction.Left)),
                    new TickInput(2, PlayerTickCommand.Move(Direction.Up)),
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
                        sourceActionPlanId: 99,
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
            int enemyLocomotionCooldownTicks = 0,
            Direction facing = Direction.Right,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return CreateUnit(entityId, teamId, SurfaceCell.FromPlanar(position), hp, aiMode, aiStateTimer, enemyLocomotionCooldownTicks, facing, unitMobilityKind);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            int aiStateTimer = 0,
            int enemyLocomotionCooldownTicks = 0,
            Direction facing = Direction.Right,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitMobilityKind = unitMobilityKind,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
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
            BoardBounds boardBounds)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, topology);
        }

        private static EnemyAiProfile CreateUtilitySummonProfile(
            int initialDelayTicks,
            int cooldownTicks,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            EnemyUnitArchetypeAsset summonedArchetype = null,
            bool overrideHp = false,
            int hpOverride = 1)
        {
            return CreateUtilityProfile(
                CreateSummonUtilityEffect(
                    initialDelayTicks,
                    cooldownTicks,
                    spawnCountPerTrigger,
                    maxAliveChildren,
                    summonedArchetype != null ? summonedArchetype : GetSharedSummonedArchetype(),
                    overrideHp,
                    hpOverride));
        }

        private static EnemyAiProfile CreateUtilityProfile(params EnemyUtilityEffectAuthoring[] effects)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                UtilityEffects = effects,
            });
        }

        private static EnemyUtilityEffectAuthoring CreateSummonUtilityEffect(
            int initialDelayTicks,
            int cooldownTicks,
            int spawnCountPerTrigger,
            int maxAliveChildren,
            EnemyUnitArchetypeAsset summonedArchetype,
            bool overrideHp = false,
            int hpOverride = 1)
        {
            var summon = new SummonMinionAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(summon, "spawnCountPerTrigger", spawnCountPerTrigger);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "maxAliveChildren", maxAliveChildren);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "candidatePattern", SummonCandidatePattern.OrthogonalAdjacent4);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoUnitAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoSolidAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "summonedArchetype", summonedArchetype);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "overrideHp", overrideHp);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "hpOverride", hpOverride);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "windupSeconds", TicksToSeconds(1));

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.SummonMinion);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", TicksToSeconds(initialDelayTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", TicksToSeconds(cooldownTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "summon", summon);
            return effect;
        }

        private static GameplayBootstrapper CreateSharedSummonBootstrapper(
            EnemyAiProfile summonerProfile,
            out EnemyAiProfile defaultProfile,
            out EnemyUnitArchetypeCatalog archetypeCatalog)
        {
            defaultProfile = CreateSharedSummonedProfile();
            archetypeCatalog = CreateEnemyUnitArchetypeCatalog(GetSharedSummonedArchetype());
            return CreateArchetypeBootstrapper(defaultProfile, summonerProfile, archetypeCatalog);
        }

        private static GameplayBootstrapper CreateArchetypeBootstrapper(
            EnemyAiProfile defaultProfile,
            EnemyAiProfile summonerProfile,
            EnemyUnitArchetypeCatalog archetypeCatalog)
        {
            var runtimeSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = 40,
                        Profile = summonerProfile,
                    },
                },
                EnemyUnitArchetypeCatalog = archetypeCatalog,
            }.CreateEnemyAiRuntimeSnapshot();

            return new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    runtimeSnapshot.DefaultDefinition,
                    runtimeSnapshot.DefinitionsByEntityId,
                    runtimeSnapshot.DefinitionsByArchetypeId),
                runtimeSnapshot.SpawnDefaultsByArchetypeId);
        }

        private static EnemyUnitArchetypeAsset CreateEnemyUnitArchetypeAsset(
            string archetypeId,
            EnemyAiProfile profile,
            int hp,
            EnemyAiMode initialAiMode)
        {
            var asset = ScriptableObject.CreateInstance<EnemyUnitArchetypeAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(asset, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            EnemyAiProfileTestFactory.SetSerializedField(asset, "aiProfile", profile);
            EnemyAiProfileTestFactory.SetSerializedField(asset, "spawnDefaults", CreateEnemyUnitSpawnDefaults(hp, initialAiMode));
            return asset;
        }

        private static EnemyUnitArchetypeCatalog CreateEnemyUnitArchetypeCatalog(params EnemyUnitArchetypeAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyUnitArchetypeCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(catalog, "entries", entries ?? Array.Empty<EnemyUnitArchetypeAsset>());
            return catalog;
        }

        private static TickPipeline CreateReplayTickPipeline(WorldState worldState, params IEntityLogic[] entityLogics)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(GetReplayDefaultProfile())
                .CreateTickPipeline(worldState, entityLogics ?? Array.Empty<IEntityLogic>());
        }

        private static TickPipeline CreateReplayTickPipeline(WorldState worldState, IReadOnlyList<IEntityLogic> entityLogics)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(GetReplayDefaultProfile())
                .CreateTickPipeline(worldState, entityLogics ?? Array.Empty<IEntityLogic>());
        }

        private static TickPipeline CreateReplayTickPipeline(WorldState worldState, EnemyAiProfile profile)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);
        }

        private static EnemyAiProfile GetReplayDefaultProfile()
        {
            if (ReplayDefaultProfile == null)
            {
                ReplayDefaultProfile = EnemyAiProfileTestFactory.CreateNonAttacking();
            }

            return ReplayDefaultProfile;
        }

        private static EnemyAiProfile CreateSharedSummonedProfile()
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
            });
        }

        private static EnemyUnitArchetypeAsset GetSharedSummonedArchetype()
        {
            if (SharedSummonedArchetype == null)
            {
                SharedSummonedProfile = CreateSharedSummonedProfile();
                SharedSummonedArchetype = CreateEnemyUnitArchetypeAsset(
                    "BasicMinion",
                    SharedSummonedProfile,
                    hp: 1,
                    initialAiMode: EnemyAiMode.Patrol);
            }

            return SharedSummonedArchetype;
        }

        private static EnemyUnitSpawnDefaults CreateEnemyUnitSpawnDefaults(int hp, EnemyAiMode initialAiMode)
        {
            object boxed = EnemyUnitSpawnDefaults.CreateDefault();
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "hp", hp);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "initialAiMode", initialAiMode);
            return (EnemyUnitSpawnDefaults)boxed;
        }

        private static float TicksToSeconds(int ticks)
        {
            return ticks / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond;
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
                firstReplay.Select(frame => frame.EnemyPatrolDump).ToArray(),
                secondReplay.Select(frame => frame.EnemyPatrolDump).ToArray());
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
