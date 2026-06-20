using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Resolution;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class AttackPhaseScenarioTests
    {
        [Test]
        [Category("Core")]
        public void Attack_MoveCommit_BlocksSameTickAttackUntilExecutionUnlock()
        {
            var timingProfile = new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 1f / 60f,
                boxSlideStepIntervalSeconds: 0.2f,
                forwardCellTravelStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 1f / 60f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8,
                moveOccupancyDurationSeconds: 1f / 60f);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 2),
            }, timingProfile);
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new TickScriptedMovementLogic(
                        10,
                        new Dictionary<int, RawMovementIntent>
                        {
                            { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                        }),
                    new StubAttackLogic(
                        controlledEntityId: 10,
                        attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 10, 20, 5)),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile));

            var result = pipeline.RunTick(new TickInput(1));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20),
                },
                result.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(result.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(result.AttackPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.AttackPhaseResult.RejectedReasons, Has.Some.Contains("Stage=ExecutionLock"));
            Assert.That(result.PresentationData.EnemyActionSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);

            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetEntityHp(snapshotAfter, 20), Is.EqualTo(2));
            Assert.That(IsMarkedForDeath(snapshotAfter, 20), Is.False);
            Assert.That(snapshotAfter.TryGetEntityExecutionLockState(10, out var executionLockState), Is.True);
            Assert.That(executionLockState.phase, Is.EqualTo(EntityExecutionPhase.Move));
            Assert.That(executionLockState.unlockTickExclusive, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void Attack_MoveCommit_AllowsAttackOnFirstTickAfterExecutionUnlock()
        {
            var timingProfile = new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 1f / 60f,
                boxSlideStepIntervalSeconds: 0.2f,
                forwardCellTravelStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 1f / 60f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8,
                moveOccupancyDurationSeconds: 1f / 60f);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 2),
            }, timingProfile);
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new TickScriptedMovementLogic(
                        10,
                        new Dictionary<int, RawMovementIntent>
                        {
                            { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                        }),
                    new StubAttackLogic(
                        controlledEntityId: 10,
                        attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 10, 20, 5)),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile));

            var moveTick = pipeline.RunTick(new TickInput(1));
            var lockedTick = pipeline.RunTick(new TickInput(2));
            var unlockTick = pipeline.RunTick(new TickInput(3));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(moveTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(lockedTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20),
                },
                unlockTick.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    unlockTick.AttackPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=10",
                    "State=Acting",
                    "Timer=0"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    unlockTick.AttackPhaseResult.CommitEvents,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);

            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetEntityHp(snapshotAfter, 20), Is.EqualTo(1));
            Assert.That(unlockTick.AttackPhaseResult.RejectedReasons, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void Attack_AlreadySameCellContactAttack_SucceedsWithoutMovement()
        {
            var stackedCell = new Vector2Int(1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: stackedCell, hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: stackedCell, hp: 2),
            });
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubAttackLogic(
                        controlledEntityId: 10,
                        attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 10, 20, 5)),
                });

            var result = pipeline.RunTick(new TickInput(1));
            var snapshotAfter = CreateSnapshot(worldState);
            var stackedUnits = new List<EntityState>();

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20),
                },
                result.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=10",
                    "State=Acting",
                    "Timer=0"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.CommitEvents,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);

            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetEntityPosition(snapshotAfter, 20), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetEntityHp(snapshotAfter, 20), Is.EqualTo(1));

            snapshotAfter.EnumerateUnitsAt(stackedCell, stackedUnits);
            CollectionAssert.AreEqual(new[] { 10, 20 }, stackedUnits.Select(entity => entity.entityId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void Attack_DeadAfterDamage_StillOccupiesUntilCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
            });
            var entityLogics = new IAttackEntityLogic[]
            {
                new StubAttackLogic(attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 10, 30, 5)),
                new StubAttackLogic(attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 20, 30, 5)),
            };

            var attackPhaseResult = RunAttackPhaseOnly(worldState, entityLogics, tickIndex: 5);
            var snapshotAfterAttack = SnapshotBuilder.Create(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    attackPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=10",
                    "State=Acting",
                    "Timer=0"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    attackPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=20",
                    "State=Acting",
                    "Timer=0"),
                Is.True);
            Assert.That(
                attackPhaseResult.CommitEvents.Count(evt =>
                    SemanticEventAssertions.ContainsEvent(new[] { evt }, "DamageCommitted", "Target=30", "Amount=1")),
                Is.EqualTo(2));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    attackPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=30",
                    "Condition=WhenHpDepleted"),
                Is.True);
            var unitsAfterAttack = new List<EntityState>();
            Assert.That(snapshotAfterAttack.TryPickImpactTargetAt(new Vector2Int(1, 0), sourceTeamId: 1, out var targetAfterAttack), Is.True);
            snapshotAfterAttack.EnumerateUnitsAt(new Vector2Int(1, 0), unitsAfterAttack);
            Assert.That(unitsAfterAttack.Select(entity => entity.entityId).ToArray(), Is.EqualTo(new[] { 30 }));
            Assert.That(targetAfterAttack.entityId, Is.EqualTo(30));
            Assert.That(targetAfterAttack.hp, Is.EqualTo(-1));
            Assert.That(targetAfterAttack.markedForDeath, Is.True);

            var cleanupProcessor = new CleanupProcessor();
            var cleanupResult = cleanupProcessor.Process(
                snapshotAfterAttack,
                worldState.CreateWriteContext(),
                tickIndex: 5);
            var snapshotAfterCleanup = SnapshotBuilder.Create(worldState);

            CollectionAssert.AreEqual(new[] { 30 }, cleanupResult.RemovedEntityIds);
            var unitsAfterCleanup = new List<EntityState>();
            Assert.That(snapshotAfterCleanup.TryGetUnitTraversalBlocker(new Vector2Int(1, 0), out _), Is.False);
            snapshotAfterCleanup.EnumerateUnitsAt(new Vector2Int(1, 0), unitsAfterCleanup);
            Assert.That(unitsAfterCleanup, Is.Empty);
            Assert.That(snapshotAfterCleanup.TryGetEntity(30, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Attack_PlayerPushInput_ResolvesItemInMovement_AndLeavesAttackPhaseEmpty()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateBox(
                    entityId: 30,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    capabilities: BoxCapabilities.Push | BoxCapabilities.Item),
            });
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(
                new TickInput(
                    1,
                    PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { MovementCommandKind.Push },
                result.MovementPhaseResult.SortedIntents.Select(intent => intent.CommandKind).ToArray());
            Assert.That(result.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
            Assert.That(result.EventLog, Does.Not.Contain("LootGranted"));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
        }

        [Test]
        [Category("Core")]
        public void Attack_CompositeItemConsumption_RejectsConsumedBoxTarget_AndUsesPostMovePlayerPosition()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateBox(
                    entityId: 30,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    capabilities: BoxCapabilities.Item | BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 2, 0), hp: 3),
                CreateUnit(entityId: 50, teamId: 2, position: new SurfaceCell(FaceId.Floor, 1, 1), hp: 3),
            });
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                    new StubAttackLogic(controlledEntityId: 40, attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 40, 30, 10)),
                    new StubAttackLogic(controlledEntityId: 50, attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 50, 10, 5)),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 30),
                    (SourceId: 50, TargetId: 10),
                },
                result.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.RejectedReasons,
                    "AttackRejected",
                    "Stage=Expand",
                    "Source=40",
                    "Target=30",
                    "Reason=TargetNotSelectable"),
                Is.True);
            Assert.That(
                result.AttackPhaseResult.DamageResolutions.Any(
                    record => record.Accepted &&
                              record.SourceId == 50 &&
                              record.TargetId == 10 &&
                              record.Amount == 1),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=50",
                    "State=Acting",
                    "Timer=0"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.CommitEvents,
                    "DamageCommitted",
                    "Target=10",
                    "Amount=1"),
                Is.True);
            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetEntityHp(snapshotAfter, 10), Is.EqualTo(2));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
        }

        [Test]
        [Category("Core")]
        public void Attack_FatalDamage_IsRemovedByCleanupAtTickEnd()
        {
            var firstRun = RunFatalAttackTick();
            var secondRun = RunFatalAttackTick();

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 30),
                    (SourceId: 20, TargetId: 30),
                },
                firstRun.Result.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(
                firstRun.Result.AttackPhaseResult.DamageResolutions.Count(
                    record => record.Accepted && record.TargetId == 30 && record.Amount == 1),
                Is.EqualTo(2));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstRun.Result.AttackPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=30",
                    "Condition=WhenHpDepleted"),
                Is.True);
            CollectionAssert.AreEqual(new[] { 30 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(firstRun.Result.EventLog));

            Assert.That(firstRun.OccupancyAfter, Is.EqualTo("10@(0,0),20@(2,0)"));
            var unitsAfterFirstRun = new List<EntityState>();
            Assert.That(firstRun.SnapshotAfter.TryGetUnitTraversalBlocker(new Vector2Int(1, 0), out _), Is.False);
            firstRun.SnapshotAfter.EnumerateUnitsAt(new Vector2Int(1, 0), unitsAfterFirstRun);
            Assert.That(unitsAfterFirstRun, Is.Empty);
            Assert.That(firstRun.SnapshotAfter.TryGetEntity(30, out _), Is.False);

            CollectionAssert.AreEqual(
                firstRun.Result.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray(),
                secondRun.Result.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                firstRun.Result.AttackPhaseResult
                    .DamageResolutions
                    .Select(record => (record.SourceId, record.TargetId, record.Accepted, record.Amount, record.RejectReason))
                    .ToArray(),
                secondRun.Result.AttackPhaseResult
                    .DamageResolutions
                    .Select(record => (record.SourceId, record.TargetId, record.Accepted, record.Amount, record.RejectReason))
                    .ToArray());
            CollectionAssert.AreEqual(firstRun.Result.AttackPhaseResult.CommitEvents, secondRun.Result.AttackPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                SemanticEventAssertions.GetCleanupRemovedEntityIds(firstRun.Result.EventLog),
                SemanticEventAssertions.GetCleanupRemovedEntityIds(secondRun.Result.EventLog));
            CollectionAssert.AreEqual(
                SemanticEventAssertions.FilterEvents(firstRun.Result.EventLog, "TimerTicked"),
                SemanticEventAssertions.FilterEvents(secondRun.Result.EventLog, "TimerTicked"));
            CollectionAssert.AreEqual(
                SemanticEventAssertions.FilterEvents(firstRun.Result.EventLog, "StateTransitioned"),
                SemanticEventAssertions.FilterEvents(secondRun.Result.EventLog, "StateTransitioned"));
            Assert.That(firstRun.OccupancyAfter, Is.EqualTo(secondRun.OccupancyAfter));
        }

        [Test]
        [Category("Core")]
        public void Attack_NonAdjacentTarget_DoesNotProduceCandidate()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 3),
            });
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubAttackLogic(controlledEntityId: 10, attackIntentFactory: _ => new RawAttackIntent(10, 5, 20)),
                });

            var result = pipeline.RunTick(new TickInput(9));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, TargetId: 20) },
                result.AttackPhaseResult
                    .RawIntents
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(result.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(result.AttackPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.RejectedReasons,
                    "AttackRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Target=20",
                    "Reason=NotSameCellOrAdjacent"),
                Is.True);
            Assert.That(GetEntityHp(snapshotAfter, 20), Is.EqualTo(3));
            Assert.That(IsMarkedForDeath(snapshotAfter, 20), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Attack.RejectedReasons"));
            Assert.That(result.Trace.Text, Does.Contain("Reason=NotSameCellOrAdjacent"));
        }

        [Test]
        [Category("Core")]
        public void DelayedEventQueue_DrainsOnNextTickOnly()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
            });
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(worldState);
            pipeline.EnqueueDelayedAttackEffect(
                new DelayedAttackEffectRecord(
                    sourceId: 10,
                    targetId: 20,
                    damage: 1,
                    priority: 5,
                    tickGenerated: 1,
                    executeAtTick: 2,
                    sourceActionPlanId: 99,
                    effectSequence: 1));

            var firstResult = pipeline.RunTick(new TickInput(1));
            var snapshotAfterFirstTick = CreateSnapshot(worldState);

            Assert.That(firstResult.AttackPhaseResult.DrainedDelayedAttackEffects, Is.Empty);
            Assert.That(firstResult.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(firstResult.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(firstResult.EventLog.Any(evt => evt.Contains("DelayedAttackDrained")), Is.False);
            Assert.That(GetEntityHp(snapshotAfterFirstTick, 20), Is.EqualTo(2));

            var secondResult = pipeline.RunTick(new TickInput(2));
            var snapshotAfterSecondTick = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20, ExecuteTick: 2, Sequence: 1),
                },
                secondResult.AttackPhaseResult
                    .DrainedDelayedAttackEffects
                    .Select(effect => (effect.SourceId, effect.TargetId, effect.ExecuteAtTick, effect.EffectSequence))
                    .ToArray());
            Assert.That(
                secondResult.AttackPhaseResult.DamageResolutions.Any(
                    record => record.Accepted &&
                              record.SourceId == 10 &&
                              record.TargetId == 20 &&
                              record.Amount == 1),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    secondResult.AttackPhaseResult.CommitEvents,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "DelayedAttackDrained|Tick=2|Source=10|Target=20|Damage=1|GeneratedTick=1|ExecuteTick=2|Group=99|Sequence=1",
                },
                secondResult.EventLog.Where(evt => evt.StartsWith("DelayedAttackDrained|", StringComparison.Ordinal)).ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    secondResult.EventLog,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);
            Assert.That(GetEntityHp(snapshotAfterSecondTick, 20), Is.EqualTo(1));
            Assert.That(IsMarkedForDeath(snapshotAfterSecondTick, 20), Is.False);
        }

        [Test]
        [Category("Core")]
        public void DelayedEventQueue_DoesNotCreateSameTickBackflow()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(0, 1), hp: 3),
            });
            var snapshot = SnapshotBuilder.Create(worldState);
            var group = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.Attack);
            group.AssignGroupId(1);
            group.Damages.Add(new DamageAction(20, 1));
            group.DelayedAttacks.Add(new DelayedAttackAction(30, 1));

            var queue = new DelayedAttackEffectQueue();
            var damageResolutions = new List<DamageResolutionRecord>();
            var commitEvents = new List<string>();
            var delayedAttackEnqueueEvents = new List<string>();

            new AttackCommitter().Commit(
                snapshot,
                worldState.CreateWriteContext(),
                tickIndex: 7,
                queue,
                new[] { group },
                damageResolutions,
                commitEvents,
                delayedAttackEnqueueEvents);

            var snapshotAfterCommit = CreateSnapshot(worldState);
            var sameTickDrain = queue.Drain(7);
            var nextTickDrain = queue.Drain(8);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    commitEvents,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    delayedAttackEnqueueEvents,
                    "DelayedAttackEnqueued",
                    "Source=10",
                    "Target=30",
                    "Damage=1",
                    "ExecuteTick=8",
                    "Sequence=1"),
                Is.True);
            Assert.That(GetEntityHp(snapshotAfterCommit, 20), Is.EqualTo(2));
            Assert.That(GetEntityHp(snapshotAfterCommit, 30), Is.EqualTo(3));
            Assert.That(sameTickDrain, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 30, ExecuteTick: 8, Sequence: 1),
                },
                nextTickDrain
                    .Select(effect => (effect.SourceId, effect.TargetId, effect.ExecuteAtTick, effect.EffectSequence))
                    .ToArray());
        }

        [Test]
        [Category("Core")]
        public void AttackCommitter_ClearsReusedOutputBuffersBeforeAppendingEvents()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(0, 1), hp: 3),
            });
            var snapshot = SnapshotBuilder.Create(worldState);
            var group = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.Attack);
            group.AssignGroupId(1);
            group.Damages.Add(new DamageAction(20, 1));
            group.DelayedAttacks.Add(new DelayedAttackAction(30, 1));

            var queue = new DelayedAttackEffectQueue();
            var damageResolutions = new List<DamageResolutionRecord> { default };
            var commitEvents = new List<string> { "StaleCommitEvent" };
            var delayedAttackEnqueueEvents = new List<string> { "StaleDelayedEvent" };

            new AttackCommitter().Commit(
                snapshot,
                worldState.CreateWriteContext(),
                tickIndex: 7,
                queue,
                new[] { group },
                damageResolutions,
                commitEvents,
                delayedAttackEnqueueEvents);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    commitEvents,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    delayedAttackEnqueueEvents,
                    "DelayedAttackEnqueued",
                    "Source=10",
                    "Target=30",
                    "Damage=1",
                    "ExecuteTick=8",
                    "Sequence=1"),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void PassiveContact_PlayerOwnedCooldown_RejectsStackedHitUntilReceiverGateExpires()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5, unitRole: UnitRole.Player),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3),
            });
            var playerTiming = CreatePlayerControlTimingSnapshot(damageCooldownTicks: 1);
            var attackLogic = new StubAttackLogic(
                controlledEntityId: 40,
                attackIntentFactory: snapshot => TryCreatePassiveContactAttack(snapshot, 40, 10, 5));

            var firstTick = RunAttackPhaseOnly(worldState, new[] { attackLogic }, tickIndex: 1, playerControlTiming: playerTiming);
            var secondTick = RunAttackPhaseOnly(worldState, new[] { attackLogic }, tickIndex: 2, playerControlTiming: playerTiming);
            var thirdTick = RunAttackPhaseOnly(worldState, new[] { attackLogic }, tickIndex: 3, playerControlTiming: playerTiming);

            Assert.That(firstTick.DamageResolutions.Single().Accepted, Is.True);
            Assert.That(firstTick.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
            Assert.That(secondTick.DamageResolutions.Single().Accepted, Is.False);
            Assert.That(secondTick.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
            Assert.That(secondTick.DamageResolutions.Single().RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
            Assert.That(thirdTick.DamageResolutions.Single().Accepted, Is.True);
            Assert.That(thirdTick.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
            Assert.That(GetEntityHp(CreateSnapshot(worldState), 10), Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_WhenOff_PlayerDamageAppliesNormally()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            var result = pipeline.RunTick(new TickInput(1), DemoGameplayOverrideSnapshot.None);
            var snapshot = CreateSnapshot(worldState);

            Assert.That(result.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
            Assert.That(GetEntityHp(snapshot, 10), Is.EqualTo(4));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_WhenOn_PlayerDamageIsIgnored()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));
            var snapshot = CreateSnapshot(worldState);

            var resolution = result.AttackPhaseResult.DamageResolutions.Single();
            Assert.That(resolution.Accepted, Is.False);
            Assert.That(resolution.RejectReason, Is.EqualTo(DamageRejectReason.PlayerInvincible));
            Assert.That(resolution.ConsumesReceiverCooldown, Is.True);
            Assert.That(resolution.HasPlayerDamageState, Is.True);
            Assert.That(GetEntityHp(snapshot, 10), Is.EqualTo(5));
            Assert.That(snapshot.TryGetPlayerDamageState(10, out var playerDamageState), Is.True);
            Assert.That(playerDamageState.nextDamageAllowedTick, Is.GreaterThan(1));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_WhenOn_LethalDamageDoesNotMarkPlayerForDeath()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 1, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 5);

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));
            var snapshot = CreateSnapshot(worldState);

            Assert.That(result.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.False);
            Assert.That(IsMarkedForDeath(snapshot, 10), Is.False);
            Assert.That(snapshot.TryGetEntity(10, out _), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_WhenOn_PlayerHpDoesNotChange()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 3);

            pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));

            Assert.That(GetEntityHp(CreateSnapshot(worldState), 10), Is.EqualTo(5));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_WhenOn_EnemyDamageStillApplies()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubAttackLogic(
                        controlledEntityId: 10,
                        attackIntentFactory: snapshot => TryCreatePassiveContactAttack(snapshot, 10, 40, 2)),
                });

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));
            var snapshot = CreateSnapshot(worldState);

            Assert.That(result.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
            Assert.That(GetEntityHp(snapshot, 40), Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_WhenOffAgain_DamageWaitsForReceiverCooldownThenAppliesAgain()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));
            var cooldownTick = pipeline.RunTick(new TickInput(2), new DemoGameplayOverrideSnapshot(playerInvincible: false));
            var resumedTick = pipeline.RunTick(new TickInput(3), new DemoGameplayOverrideSnapshot(playerInvincible: false));

            Assert.That(GetEntityHp(CreateSnapshot(worldState), 10), Is.EqualTo(4));
            Assert.That(cooldownTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.False);
            Assert.That(cooldownTick.AttackPhaseResult.DamageResolutions.Single().RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
            Assert.That(resumedTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_DoesNotCreateCleanupRemovalForPlayer()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 1, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 5);

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));

            Assert.That(result.PresentationData.EntityExitSignals.Select(signal => signal.ExitedEntityId), Has.No.EqualTo(10));
            Assert.That(result.FinalEntities.Select(entity => entity.entityId), Has.Some.EqualTo(10));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_DoesNotTriggerPlayerDeathSignal()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 1, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 5);

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));

            Assert.That(result.PresentationData.PlayerDamageSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerDeathSignals, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_PassiveContactOverlap_ConsumesReceiverCooldown()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));
            var snapshot = CreateSnapshot(worldState);
            var resolution = result.AttackPhaseResult.DamageResolutions.Single();

            Assert.That(resolution.Accepted, Is.False);
            Assert.That(resolution.RejectReason, Is.EqualTo(DamageRejectReason.PlayerInvincible));
            Assert.That(resolution.ConsumesReceiverCooldown, Is.True);
            Assert.That(snapshot.TryGetPlayerDamageState(10, out var playerDamageState), Is.True);
            Assert.That(playerDamageState.nextDamageAllowedTick, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_PassiveContactOverlap_DoesNotRetryInvincibleEveryTick()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            var first = CaptureInvincibleContactDiagnostics(
                pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true)));
            var second = CaptureInvincibleContactDiagnostics(
                pipeline.RunTick(new TickInput(2), new DemoGameplayOverrideSnapshot(playerInvincible: true)));
            var third = CaptureInvincibleContactDiagnostics(
                pipeline.RunTick(new TickInput(3), new DemoGameplayOverrideSnapshot(playerInvincible: true)));

            Assert.That(first.PlayerInvincibleRejectCount, Is.EqualTo(1));
            Assert.That(first.EnemyActionExecutedSignalCount, Is.EqualTo(1));
            Assert.That(second.PlayerInvincibleRejectCount, Is.EqualTo(0));
            Assert.That(second.ReceiverCooldownRejectCount, Is.EqualTo(1));
            Assert.That(second.EnemyActionExecutedSignalCount, Is.EqualTo(0));
            Assert.That(third.PlayerInvincibleRejectCount, Is.EqualTo(1));
            Assert.That(third.EnemyActionExecutedSignalCount, Is.EqualTo(1));
            Assert.That(GetEntityHp(CreateSnapshot(worldState), 10), Is.EqualTo(5));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_RejectedDamage_DoesNotEmitCorePlayerDamageAudio()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            var diagnostics = CaptureInvincibleContactDiagnostics(
                pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true)));

            Assert.That(diagnostics.PlayerDamageSignalCount, Is.EqualTo(0));
            Assert.That(diagnostics.PlayerDeathSignalCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_PassiveContactReject_DoesNotProduceCombatPresentationSource()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));
            var executedSignals = result.PresentationData.EnemyActionSignals
                .Where(signal => signal.ExecutedThisTick)
                .ToArray();

            Assert.That(
                executedSignals.Any(signal => signal.PresentationSource == EnemyActionPresentationSource.Combat),
                Is.False);
            Assert.That(
                executedSignals.Any(signal => signal.PresentationOutcome == EnemyActionPresentationOutcome.Executed),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void ReceiverCooldownReject_DoesNotProduceEnemyActionExecutionSignal()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));
            var result = pipeline.RunTick(new TickInput(2), new DemoGameplayOverrideSnapshot(playerInvincible: true));

            Assert.That(result.AttackPhaseResult.DamageResolutions.Single().RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
            Assert.That(result.PresentationData.EnemyActionSignals.Count(signal => signal.ExecutedThisTick), Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_FlagIncludedInTraceOrDebugSnapshot()
        {
            var worldState = CreatePlayerPassiveContactWorld(playerHp: 5, enemyHp: 3);
            var pipeline = CreatePassiveContactPipeline(worldState, damage: 1);

            var result = pipeline.RunTick(new TickInput(1), new DemoGameplayOverrideSnapshot(playerInvincible: true));

            Assert.That(result.Trace.Text, Does.Contain("RejectReason=PlayerInvincible"));
        }

        [Test]
        [Category("Core")]
        public void PassiveContact_SameTickMultipleSources_OnlyFirstDeterministicResolutionIsAccepted()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5, unitRole: UnitRole.Player),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(0, 0), hp: 3),
            });
            var playerTiming = CreatePlayerControlTimingSnapshot(damageCooldownTicks: 1);

            var result = RunAttackPhaseOnly(
                worldState,
                new IAttackEntityLogic[]
                {
                    new StubAttackLogic(controlledEntityId: 40, attackIntentFactory: snapshot => TryCreatePassiveContactAttack(snapshot, 40, 10, 5)),
                    new StubAttackLogic(controlledEntityId: 50, attackIntentFactory: snapshot => TryCreatePassiveContactAttack(snapshot, 50, 10, 5)),
                },
                tickIndex: 1,
                playerControlTiming: playerTiming);

            var accepted = result.DamageResolutions.Where(record => record.Accepted).ToArray();
            var rejected = result.DamageResolutions.Where(record => !record.Accepted).ToArray();

            Assert.That(accepted.Length, Is.EqualTo(1));
            Assert.That(rejected.Length, Is.EqualTo(1));
            Assert.That(accepted[0].SourceId, Is.EqualTo(40));
            Assert.That(accepted[0].SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
            Assert.That(rejected[0].SourceId, Is.EqualTo(50));
            Assert.That(rejected[0].SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
            Assert.That(rejected[0].RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
        }

        [Test]
        [Category("Core")]
        public void PassiveContact_AndCombat_FromSameSource_ShareOrderingButPassiveSkipsActingStateChange()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5, unitRole: UnitRole.Player),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3),
            });
            var playerTiming = CreatePlayerControlTimingSnapshot(damageCooldownTicks: 1);
            var result = RunAttackPhaseOnly(
                worldState,
                new IAttackEntityLogic[]
                {
                    new MultiAttackIntentLogic(
                        controlledEntityId: 40,
                        attackIntentFactories: new Func<WorldSnapshot, RawAttackIntent?>[]
                        {
                            snapshot => TryCreateContactRangeAttack(snapshot, 40, 10, 5),
                            snapshot => TryCreatePassiveContactAttack(snapshot, 40, 10, 5),
                        }),
                },
                tickIndex: 1,
                playerControlTiming: playerTiming);

            CollectionAssert.AreEqual(
                new[]
                {
                    AttackSourceKind.Combat,
                    AttackSourceKind.PassiveContact,
                },
                result.DamageResolutions.Select(record => record.SourceKind).ToArray());
            Assert.That(
                result.CommitEvents.Count(evt =>
                    SemanticEventAssertions.ContainsEvent(new[] { evt }, "StateChanged", "E=40", "State=Acting")),
                Is.EqualTo(1));
            Assert.That(result.DamageResolutions[0].Accepted, Is.True);
            Assert.That(result.DamageResolutions[0].SourceKind, Is.EqualTo(AttackSourceKind.Combat));
            Assert.That(result.DamageResolutions[1].Accepted, Is.False);
            Assert.That(result.DamageResolutions[1].SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
        }

        private static (TickResult Result, WorldSnapshot SnapshotAfter, string OccupancyAfter) RunFatalAttackTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
            });
            var pipeline = GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubAttackLogic(controlledEntityId: 10, attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 10, 30, 5)),
                    new StubAttackLogic(controlledEntityId: 20, attackIntentFactory: snapshot => TryCreateContactRangeAttack(snapshot, 20, 30, 5)),
                });

            var result = pipeline.RunTick(new TickInput(5));
            var snapshotAfter = CreateSnapshot(worldState);
            return (result, snapshotAfter, DumpUnitOccupancy(snapshotAfter));
        }

        private static AttackPhaseResult RunAttackPhaseOnly(
            WorldState worldState,
            IReadOnlyList<IAttackEntityLogic> entityLogics,
            int tickIndex,
            TickInput input = default,
            PlayerControlTimingAuthoritativeSnapshot? playerControlTiming = null)
        {
            var snapshot = SnapshotBuilder.Create(worldState);
            var rawAttackIntents = new List<RawAttackIntent>();
            var effectiveInput = input.TickIndex == 0 ? new TickInput(tickIndex) : input;
            new AttackIntentCollector().Collect(snapshot, in effectiveInput, entityLogics, rawAttackIntents);
            var drainedImpactReservations = new List<ImpactReservation>();

            var idAllocator = new IdAllocator();
            idAllocator.ResetForTick(tickIndex);
            var entityIdAllocator = EntityIdAllocator.Create(snapshot);

            var sortedInputs = new List<AttackIntent>(rawAttackIntents.Count);
            new AttackInputNormalizer().Normalize(rawAttackIntents, drainedImpactReservations, sortedInputs);
            for (var i = 0; i < sortedInputs.Count; i++)
            {
                sortedInputs[i].AssignIntentId(idAllocator.AllocateIntentId());
            }

            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            new AttackExpander().Expand(snapshot, sortedInputs, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(idAllocator.AllocateGroupId());
            }

            var selectedGroups = new List<ActionGroup>();
            new AttackResolver().Resolve(expandedCandidates, selectedGroups, rejectedReasons);
            AssignSpawnIdsForSelectedGroups(selectedGroups, idAllocator, entityIdAllocator);

            var damageResolutions = new List<DamageResolutionRecord>();
            var commitEvents = new List<string>();
            var delayedAttackEnqueueEvents = new List<string>();
            new AttackCommitter(playerControlTiming ?? CreateDefaultPlayerControlTimingSnapshot()).Commit(
                snapshot,
                worldState.CreateWriteContext(),
                tickIndex,
                new DelayedAttackEffectQueue(),
                selectedGroups,
                damageResolutions,
                commitEvents,
                delayedAttackEnqueueEvents);

            return CanonicalPhaseResultFactory.CreateAttackPhaseResult(
                rawAttackIntents,
                drainedImpactReservations,
                Array.Empty<DelayedAttackEffectRecord>(),
                damageResolutions,
                selectedGroups,
                commitEvents,
                commitEvents,
                rejectedReasons);
        }

        private static WorldState CreatePlayerPassiveContactWorld(int playerHp, int enemyHp)
        {
            return CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: playerHp, unitRole: UnitRole.Player),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: enemyHp),
            });
        }

        private static TickPipeline CreatePassiveContactPipeline(WorldState worldState, int damage)
        {
            return GameplayTestRuntimeFactory.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubAttackLogic(
                        controlledEntityId: 40,
                        attackIntentFactory: snapshot => TryCreatePassiveContactAttack(snapshot, 40, 10, damage)),
                });
        }

        private static PlayerInvincibleContactDiagnostics CaptureInvincibleContactDiagnostics(TickResult result)
        {
            var playerTargetDamageActionCount = 0;
            var acceptedDamageCount = 0;
            var playerInvincibleRejectCount = 0;
            var receiverCooldownRejectCount = 0;
            var enemyActionExecutedSignalCount = 0;
            for (var i = 0; i < result.AttackPhaseResult.DamageResolutions.Count; i++)
            {
                var resolution = result.AttackPhaseResult.DamageResolutions[i];
                if (resolution.TargetId != 10)
                {
                    continue;
                }

                playerTargetDamageActionCount++;
                if (resolution.Accepted)
                {
                    acceptedDamageCount++;
                }

                if (resolution.RejectReason == DamageRejectReason.PlayerInvincible)
                {
                    playerInvincibleRejectCount++;
                }

                if (resolution.RejectReason == DamageRejectReason.ReceiverCooldown)
                {
                    receiverCooldownRejectCount++;
                }
            }

            for (var i = 0; i < result.PresentationData.EnemyActionSignals.Count; i++)
            {
                if (result.PresentationData.EnemyActionSignals[i].ExecutedThisTick)
                {
                    enemyActionExecutedSignalCount++;
                }
            }

            return new PlayerInvincibleContactDiagnostics(
                result.TickIndex,
                playerTargetDamageActionCount,
                acceptedDamageCount,
                playerInvincibleRejectCount,
                receiverCooldownRejectCount,
                enemyActionExecutedSignalCount,
                result.PresentationData.PlayerDamageSignals.Count,
                result.PresentationData.PlayerDeathSignals.Count);
        }

        private static void AssignSpawnIdsForSelectedGroups(
            IReadOnlyList<ActionGroup> selectedGroups,
            IdAllocator idAllocator,
            EntityIdAllocator entityIdAllocator)
        {
            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var spawnIndex = 0; spawnIndex < group.Spawns.Count; spawnIndex++)
                {
                    var entity = group.Spawns[spawnIndex].Entity;
                    entity.entityId = entityIdAllocator.AllocateEntityId();
                    entity.spawnTick = idAllocator.CurrentTickIndex;
                    group.Spawns[spawnIndex] = new SpawnAction(idAllocator.AllocateSpawnId(), entity);
                }
            }
        }

        private static RawAttackIntent? TryCreateContactRangeAttack(
            WorldSnapshot snapshot,
            int sourceId,
            int targetId,
            int priority)
        {
            if (!snapshot.TryGetEntity(sourceId, out var source))
            {
                return null;
            }

            if (!snapshot.TryGetEntity(targetId, out var target))
            {
                return null;
            }

            if (source.position.face != target.position.face)
            {
                return null;
            }

            if (Math.Abs(source.position.x - target.position.x) + Math.Abs(source.position.y - target.position.y) > 1)
            {
                return null;
            }

            return new RawAttackIntent(sourceId, priority, targetId);
        }

        private static RawAttackIntent? TryCreatePassiveContactAttack(
            WorldSnapshot snapshot,
            int sourceId,
            int targetId,
            int priority)
        {
            if (!snapshot.TryGetEntity(sourceId, out var source))
            {
                return null;
            }

            if (!snapshot.TryGetEntity(targetId, out var target))
            {
                return null;
            }

            if (source.position.face != target.position.face ||
                source.position.PlanarPosition != target.position.PlanarPosition)
            {
                return null;
            }

            return new RawAttackIntent(
                sourceId,
                priority,
                targetId,
                AttackSourceKind.PassiveContact,
                localSequence: 1);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp,
            Direction facing = Direction.None,
            UnitRole unitRole = UnitRole.None)
        {
            return CreateUnit(entityId, teamId, SurfaceCell.FromPlanar(position), hp, facing, unitRole);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp,
            Direction facing = Direction.None,
            UnitRole unitRole = UnitRole.None)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = facing,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            Vector2Int position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            Direction facing = Direction.Right)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position), capabilities, facing);
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            Direction facing = Direction.Right)
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
                facing = facing,
                boxCapabilities = capabilities,
            };
        }

        private static string DumpUnitOccupancy(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            return string.Join(
                ",",
                entities
                    .Where(entity => entity.type == EntityType.Unit)
                    .Select(entity => $"{entity.entityId}@({entity.position.x},{entity.position.y})"));
        }

        private static int GetEntityHp(WorldSnapshot snapshot, int entityId)
        {
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.hp;
        }

        private static SurfaceCell GetEntityPosition(WorldSnapshot snapshot, int entityId)
        {
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.position;
        }

        private static bool IsMarkedForDeath(WorldSnapshot snapshot, int entityId)
        {
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.markedForDeath;
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

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            var createSnapshotMethod = typeof(WorldState).GetMethod(
                "CreateSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createSnapshotMethod, Is.Not.Null);

            return (WorldSnapshot)createSnapshotMethod.Invoke(worldState, null);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile = null)
        {
            var generalTimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond,
                generalTimingProfile.RepeatedMoveIntervalSeconds);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerControlTimingSnapshot(
            int damageCooldownTicks,
            GameplayTimingProfile timingProfile = null)
        {
            var generalTimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            return new PlayerControlTimingSettings
            {
                DamageCooldownSeconds = damageCooldownTicks / (float)generalTimingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond,
                generalTimingProfile.RepeatedMoveIntervalSeconds);
        }

        private static GameplayTimingProfile CreateTimingProfile(
            int simulationTicksPerSecond = 60,
            float initialMoveDelaySeconds = 0f,
            float repeatedMoveIntervalSeconds = 0.4f,
            float boxSlideStepIntervalSeconds = 0.2f,
            float forwardCellTravelStepIntervalSeconds = 0.2f,
            float pushMotionDurationSeconds = 0.2f,
            float flipMotionDurationSeconds = 0.2f,
            float flipArcHeightInCells = 0.65f,
            int maxTicksPerFrame = 8)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond,
                initialMoveDelaySeconds,
                repeatedMoveIntervalSeconds,
                boxSlideStepIntervalSeconds,
                forwardCellTravelStepIntervalSeconds,
                pushMotionDurationSeconds,
                flipMotionDurationSeconds,
                flipArcHeightInCells,
                maxTicksPerFrame);
        }

        private static IMovementEntityLogic CreateImmediatePushPlayerLogic(int entityId)
        {
            return new ImmediatePlayerInteractionLogic(entityId, MovementCommandKind.Push);
        }

        private static List<TickResult> RunTicks(
            TickPipeline pipeline,
            int startTickIndex,
            int endTickIndex,
            Action<int> beforeTick = null)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (startTickIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startTickIndex));
            }

            if (endTickIndex < startTickIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endTickIndex));
            }

            var results = new List<TickResult>(endTickIndex - startTickIndex + 1);
            for (var tickIndex = startTickIndex; tickIndex <= endTickIndex; tickIndex++)
            {
                beforeTick?.Invoke(tickIndex);
                results.Add(pipeline.RunTick(new TickInput(tickIndex)));
            }

            return results;
        }

        private readonly struct PlayerInvincibleContactDiagnostics
        {
            public PlayerInvincibleContactDiagnostics(
                int tickIndex,
                int playerTargetDamageActionCount,
                int acceptedDamageCount,
                int playerInvincibleRejectCount,
                int receiverCooldownRejectCount,
                int enemyActionExecutedSignalCount,
                int playerDamageSignalCount,
                int playerDeathSignalCount)
            {
                TickIndex = tickIndex;
                PlayerTargetDamageActionCount = playerTargetDamageActionCount;
                AcceptedDamageCount = acceptedDamageCount;
                PlayerInvincibleRejectCount = playerInvincibleRejectCount;
                ReceiverCooldownRejectCount = receiverCooldownRejectCount;
                EnemyActionExecutedSignalCount = enemyActionExecutedSignalCount;
                PlayerDamageSignalCount = playerDamageSignalCount;
                PlayerDeathSignalCount = playerDeathSignalCount;
            }

            public int TickIndex { get; }

            public int PlayerTargetDamageActionCount { get; }

            public int AcceptedDamageCount { get; }

            public int PlayerInvincibleRejectCount { get; }

            public int ReceiverCooldownRejectCount { get; }

            public int EnemyActionExecutedSignalCount { get; }

            public int PlayerDamageSignalCount { get; }

            public int PlayerDeathSignalCount { get; }
        }

        private sealed class StubAttackLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly Func<WorldSnapshot, RawAttackIntent?> _attackIntentFactory;
            private readonly int _controlledEntityId;

            public StubAttackLogic(
                int controlledEntityId = 0,
                Func<WorldSnapshot, RawAttackIntent?> attackIntentFactory = null)
            {
                _controlledEntityId = controlledEntityId;
                _attackIntentFactory = attackIntentFactory;
            }

            public int ControlledEntityId => _controlledEntityId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (_attackIntentFactory == null)
                {
                    return;
                }

                var attackIntent = _attackIntentFactory(snapshot);
                if (attackIntent.HasValue)
                {
                    buffer.Add(attackIntent.Value);
                }
            }
        }

        private sealed class StubCombatLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _controlledEntityId;
            private readonly RawMovementIntent? _movementIntent;

            public StubCombatLogic(
                int controlledEntityId = 0,
                RawMovementIntent? movementIntent = null)
            {
                _controlledEntityId = controlledEntityId;
                _movementIntent = movementIntent;
            }

            public int ControlledEntityId => _movementIntent?.SourceId ?? _controlledEntityId;

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
        }

        private sealed class MultiAttackIntentLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly Func<WorldSnapshot, RawAttackIntent?>[] _attackIntentFactories;
            private readonly int _controlledEntityId;

            public MultiAttackIntentLogic(
                int controlledEntityId,
                IReadOnlyList<Func<WorldSnapshot, RawAttackIntent?>> attackIntentFactories)
            {
                _controlledEntityId = controlledEntityId;
                _attackIntentFactories = attackIntentFactories?.ToArray()
                    ?? throw new ArgumentNullException(nameof(attackIntentFactories));
            }

            public int ControlledEntityId => _controlledEntityId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                for (var i = 0; i < _attackIntentFactories.Length; i++)
                {
                    var attackIntent = _attackIntentFactories[i](snapshot);
                    if (attackIntent.HasValue)
                    {
                        buffer.Add(attackIntent.Value);
                    }
                }
            }
        }

        private sealed class TickScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _entityId;
            private readonly IReadOnlyDictionary<int, RawMovementIntent> _movementIntentsByTick;

            public TickScriptedMovementLogic(int entityId, IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                _entityId = entityId;
                _movementIntentsByTick = movementIntentsByTick ?? throw new ArgumentNullException(nameof(movementIntentsByTick));
            }

            public int ControlledEntityId => _entityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntentsByTick.TryGetValue(input.TickIndex, out var movementIntent))
                {
                    buffer.Add(movementIntent);
                }
            }
        }

        private sealed class ImmediatePlayerInteractionLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly MovementCommandKind _commandKind;
            private readonly int _entityId;

            public ImmediatePlayerInteractionLogic(int entityId, MovementCommandKind commandKind)
            {
                _entityId = entityId;
                _commandKind = commandKind;
            }

            public int ControlledEntityId => _entityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (snapshot == null)
                {
                    throw new ArgumentNullException(nameof(snapshot));
                }

                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (!snapshot.TryGetEntity(_entityId, out var entity) ||
                    entity.hp <= 0 ||
                    entity.markedForDeath ||
                    input.PlayerCommand.MoveDirection == Direction.None ||
                    !TryResolveDelta(input.PlayerCommand.MoveDirection, out var delta))
                {
                    return;
                }

                buffer.Add(
                    new RawMovementIntent(
                        _entityId,
                        priority: 100,
                        entity.position.PlanarPosition + delta,
                        _commandKind,
                        localSequence: 0));
            }

            private static bool TryResolveDelta(Direction direction, out Vector2Int delta)
            {
                switch (direction)
                {
                    case Direction.Up:
                        delta = Vector2Int.up;
                        return true;

                    case Direction.Right:
                        delta = Vector2Int.right;
                        return true;

                    case Direction.Down:
                        delta = Vector2Int.down;
                        return true;

                    case Direction.Left:
                        delta = Vector2Int.left;
                        return true;

                    default:
                        delta = Vector2Int.zero;
                        return false;
                }
            }
        }

        private sealed class TickScriptedCombatLogic : IAttackEntityLogic
        {
            private readonly IReadOnlyDictionary<int, RawAttackIntent> _attackIntentsByTick;
            private readonly int _sourceId;
            private int _currentTickIndex;

            public TickScriptedCombatLogic(int sourceId, IReadOnlyDictionary<int, RawAttackIntent> attackIntentsByTick)
            {
                _sourceId = sourceId;
                _attackIntentsByTick = attackIntentsByTick ?? throw new ArgumentNullException(nameof(attackIntentsByTick));
            }

            public void SetTickIndex(int tickIndex)
            {
                _currentTickIndex = tickIndex;
            }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (_currentTickIndex <= 0)
                {
                    return;
                }

                if (!_attackIntentsByTick.TryGetValue(_currentTickIndex, out var attackIntent))
                {
                    return;
                }

                if (!snapshot.TryGetEntity(_sourceId, out var source) || source.hp <= 0 || source.markedForDeath)
                {
                    return;
                }

                buffer.Add(attackIntent);
            }
        }

        private sealed class PriorityTargetSelectionLogic : IAttackEntityLogic
        {
            private readonly int[] _candidateTargetIds;
            private readonly int _priority;
            private readonly int _sourceId;

            public PriorityTargetSelectionLogic(int sourceId, int priority, params int[] candidateTargetIds)
            {
                _sourceId = sourceId;
                _priority = priority;
                _candidateTargetIds = candidateTargetIds ?? throw new ArgumentNullException(nameof(candidateTargetIds));
            }

            public int AttackCollectCallCount { get; private set; }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                AttackCollectCallCount++;

                if (!snapshot.TryGetEntity(_sourceId, out var source) || source.hp <= 0 || source.markedForDeath)
                {
                    return;
                }

                for (var i = 0; i < _candidateTargetIds.Length; i++)
                {
                    var targetId = _candidateTargetIds[i];
                    if (!snapshot.TryGetEntity(targetId, out var target))
                    {
                        continue;
                    }

                    if (!snapshot.CanBeTargetedForNewSelection(targetId))
                    {
                        continue;
                    }

                    if (Math.Abs(source.position.x - target.position.x) + Math.Abs(source.position.y - target.position.y) != 1)
                    {
                        continue;
                    }

                    buffer.Add(new RawAttackIntent(_sourceId, _priority, targetId));
                    return;
                }
            }
        }

    }
}
