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
        public void Attack_MoveThenAttack_UsesPostMoveSnapshot()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(
                        movementIntent: new RawMovementIntent(10, 5, new Vector2Int(1, 0)),
                        attackIntentFactory: snapshot => TryCreateAdjacentAttack(snapshot, 10, 20, 5)),
                });

            var result = pipeline.RunTick(new TickInput(1));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 2, TargetId: 20),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 2, IntentId: 2, SourceId: 10, Kind: ActionGroupKind.Attack, DamageTargetId: 20, DestroyTargetId: 20),
                },
                result.AttackPhaseResult
                    .ExpandedCandidates
                    .Select(group => (
                        group.GroupId,
                        group.IntentId,
                        group.SourceId,
                        group.GroupKind,
                        DamageTargetId: group.Damages.Single().TargetId,
                        DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { (GroupId: 2, SourceId: 10, DestroyTargetId: 20) },
                result.AttackPhaseResult
                    .SelectedGroups
                    .Select(group => (group.GroupId, group.SourceId, DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=2|I=2|E=10|State=Acting|Timer=0",
                    "DamageCommitted|G=2|I=2|Target=20|Amount=1",
                },
                result.AttackPhaseResult.CommitEvents);

            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetEntityHp(snapshotAfter, 20), Is.EqualTo(1));
            Assert.That(IsMarkedForDeath(snapshotAfter, 20), Is.False);
        }

        [Test]
        public void Attack_DeadAfterDamage_StillOccupiesUntilCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
            });
            var entityLogics = new IEntityLogic[]
            {
                new StubCombatLogic(attackIntentFactory: snapshot => TryCreateAdjacentAttack(snapshot, 10, 30, 5)),
                new StubCombatLogic(attackIntentFactory: snapshot => TryCreateAdjacentAttack(snapshot, 20, 30, 5)),
            };

            var attackPhaseResult = RunAttackPhaseOnly(worldState, entityLogics, tickIndex: 5);
            var snapshotAfterAttack = SnapshotBuilder.Create(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=10|State=Acting|Timer=0",
                    "StateChanged|G=2|I=2|E=20|State=Acting|Timer=0",
                    "DamageCommitted|G=1|I=1|Target=30|Amount=1",
                    "DamageCommitted|G=2|I=2|Target=30|Amount=1",
                    "DestroyMarked|G=1|I=1|Target=30|FinalHp=-1|Condition=WhenHpDepleted",
                },
                attackPhaseResult.CommitEvents);
            Assert.That(snapshotAfterAttack.IsBlockedForUnit(new Vector2Int(1, 0)), Is.True);
            Assert.That(snapshotAfterAttack.TryGetUnitAt(new Vector2Int(1, 0), out var targetAfterAttack), Is.True);
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
            Assert.That(snapshotAfterCleanup.IsBlockedForUnit(new Vector2Int(1, 0)), Is.False);
            Assert.That(snapshotAfterCleanup.TryGetUnitAt(new Vector2Int(1, 0), out _), Is.False);
            Assert.That(snapshotAfterCleanup.TryGetEntity(30, out _), Is.False);
        }

        [Test]
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(
                new TickInput(
                    1,
                    PlayerTickCommand.Push(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { ActionGroupKind.Item },
                result.MovementPhaseResult.SelectedGroups.Select(group => group.GroupKind).ToArray());
            CollectionAssert.AreEqual(
                new[] { MovementCommandKind.Push },
                result.MovementPhaseResult.SortedIntents.Select(intent => intent.CommandKind).ToArray());
            Assert.That(result.AttackPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
            Assert.That(result.EventLog, Does.Not.Contain("LootGranted"));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
        }

        [Test]
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    new StubCombatLogic(attackIntentFactory: snapshot => TryCreateAdjacentAttack(snapshot, 40, 30, 10)),
                    new StubCombatLogic(attackIntentFactory: snapshot => TryCreateAdjacentAttack(snapshot, 50, 10, 5)),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, IntentId: 2, TargetId: 30),
                    (SourceId: 50, IntentId: 3, TargetId: 10),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "AttackRejected|Stage=Expand|I=2|Source=40|Target=30|Reason=TargetNotSelectable",
                },
                result.AttackPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 2, SourceId: 50, DamageTargetId: 10),
                },
                result.AttackPhaseResult
                    .SelectedGroups
                    .Select(group => (group.GroupId, group.SourceId, DamageTargetId: group.Damages.Single().TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=2|I=3|E=50|State=Acting|Timer=0",
                    "DamageCommitted|G=2|I=3|Target=10|Amount=1",
                },
                result.AttackPhaseResult.CommitEvents);
            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetEntityHp(snapshotAfter, 10), Is.EqualTo(2));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=30"));
        }

        [Test]
        public void Attack_FatalDamage_IsRemovedByCleanupAtTickEnd()
        {
            var firstRun = RunFatalAttackTick();
            var secondRun = RunFatalAttackTick();

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, TargetId: 30),
                    (SourceId: 20, IntentId: 2, TargetId: 30),
                },
                firstRun.Result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 1, IntentId: 1, SourceId: 10, Kind: ActionGroupKind.Attack, DamageTargetId: 30, DestroyTargetId: 30),
                    (GroupId: 2, IntentId: 2, SourceId: 20, Kind: ActionGroupKind.Attack, DamageTargetId: 30, DestroyTargetId: 30),
                },
                firstRun.Result.AttackPhaseResult
                    .ExpandedCandidates
                    .Select(group => (
                        group.GroupId,
                        group.IntentId,
                        group.SourceId,
                        group.GroupKind,
                        DamageTargetId: group.Damages.Single().TargetId,
                        DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 1, SourceId: 10, DamageTargetId: 30, DestroyTargetId: 30),
                    (GroupId: 2, SourceId: 20, DamageTargetId: 30, DestroyTargetId: 30),
                },
                firstRun.Result.AttackPhaseResult
                    .SelectedGroups
                    .Select(group => (
                        group.GroupId,
                        group.SourceId,
                        DamageTargetId: group.Damages.Single().TargetId,
                        DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=10|State=Acting|Timer=0",
                    "StateChanged|G=2|I=2|E=20|State=Acting|Timer=0",
                    "DamageCommitted|G=1|I=1|Target=30|Amount=1",
                    "DamageCommitted|G=2|I=2|Target=30|Amount=1",
                    "DestroyMarked|G=1|I=1|Target=30|FinalHp=-1|Condition=WhenHpDepleted",
                },
                firstRun.Result.AttackPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(new[] { 30 }, firstRun.Result.CleanupPhaseResult.RemovedEntityIds);

            Assert.That(firstRun.OccupancyAfter, Is.EqualTo("10@(0,0),20@(2,0)"));
            Assert.That(firstRun.SnapshotAfter.IsBlockedForUnit(new Vector2Int(1, 0)), Is.False);
            Assert.That(firstRun.SnapshotAfter.TryGetUnitAt(new Vector2Int(1, 0), out _), Is.False);
            Assert.That(firstRun.SnapshotAfter.TryGetEntity(30, out _), Is.False);

            CollectionAssert.AreEqual(
                firstRun.Result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.TargetId))
                    .ToArray(),
                secondRun.Result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                firstRun.Result.AttackPhaseResult
                    .ExpandedCandidates
                    .Select(group => (
                        group.GroupId,
                        group.IntentId,
                        group.SourceId,
                        DamageTargetId: group.Damages.Single().TargetId,
                        DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray(),
                secondRun.Result.AttackPhaseResult
                    .ExpandedCandidates
                    .Select(group => (
                        group.GroupId,
                        group.IntentId,
                        group.SourceId,
                        DamageTargetId: group.Damages.Single().TargetId,
                        DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                firstRun.Result.AttackPhaseResult
                    .SelectedGroups
                    .Select(group => (
                        group.GroupId,
                        group.SourceId,
                        DamageTargetId: group.Damages.Single().TargetId,
                        DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray(),
                secondRun.Result.AttackPhaseResult
                    .SelectedGroups
                    .Select(group => (
                        group.GroupId,
                        group.SourceId,
                        DamageTargetId: group.Damages.Single().TargetId,
                        DestroyTargetId: group.Destroys.Single().TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(firstRun.Result.AttackPhaseResult.CommitEvents, secondRun.Result.AttackPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(firstRun.Result.CleanupPhaseResult.RemovedEntityIds, secondRun.Result.CleanupPhaseResult.RemovedEntityIds);
            CollectionAssert.AreEqual(firstRun.Result.CleanupPhaseResult.TimerChanges, secondRun.Result.CleanupPhaseResult.TimerChanges);
            CollectionAssert.AreEqual(firstRun.Result.CleanupPhaseResult.StateTransitions, secondRun.Result.CleanupPhaseResult.StateTransitions);
            Assert.That(firstRun.OccupancyAfter, Is.EqualTo(secondRun.OccupancyAfter));
        }

        [Test]
        public void Attack_NonAdjacentTarget_DoesNotProduceCandidate()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 3),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(attackIntentFactory: _ => new RawAttackIntent(10, 5, 20)),
                });

            var result = pipeline.RunTick(new TickInput(9));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, TargetId: 20) },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.TargetId))
                    .ToArray());
            Assert.That(result.AttackPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.AttackPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.AttackPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "AttackRejected|Stage=Expand|I=1|Source=10|Target=20|Reason=NotAdjacent|SourceCell=(0,0)|TargetCell=(2,0)",
                },
                result.AttackPhaseResult.RejectedReasons);
            Assert.That(GetEntityHp(snapshotAfter, 20), Is.EqualTo(3));
            Assert.That(IsMarkedForDeath(snapshotAfter, 20), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Attack.RejectedReasons"));
            Assert.That(result.Trace.Text, Does.Contain("Reason=NotAdjacent"));
        }

        [Test]
        public void Attack_FireProjectileIntent_SpawnsProjectileDuringCommit()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(attackIntentFactory: _ => RawAttackIntent.CreateFireProjectile(10, 5)),
                });
            var defaultTimingProfile = GameplayTimingProfile.CreateDefault();

            var result = pipeline.RunTick(new TickInput(4));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Command: AttackCommandKind.FireProjectile, TargetId: 0) },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.CommandKind, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 1, IntentId: 1, SourceId: 10, SpawnId: 1, EntityId: 11, Cell: new SurfaceCell(FaceId.Floor, 1, 0), Type: EntityType.Projectile, SpawnTick: 4),
                },
                result.AttackPhaseResult
                    .ExpandedCandidates
                    .Select(group =>
                    {
                        var spawn = group.Spawns.Single();
                        return (group.GroupId, group.IntentId, group.SourceId, spawn.SpawnId, EntityId: spawn.Entity.entityId, Cell: spawn.Entity.position, Type: spawn.Entity.type, SpawnTick: spawn.Entity.spawnTick);
                    })
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=10|State=Acting|Timer=0",
                    "SpawnCommitted|G=1|I=1|SpawnId=1|E=11|Pos=(1,0)|Type=Projectile|SpawnTick=4",
                },
                result.AttackPhaseResult.CommitEvents);
            Assert.That(result.AttackPhaseResult.SelectedGroups.Single().Spawns.Single().SpawnId, Is.EqualTo(1));
            Assert.That(result.AttackPhaseResult.SelectedGroups.Single().Spawns.Single().Entity.entityId, Is.EqualTo(11));
            Assert.That(snapshotAfter.TryGetProjectileAt(new Vector2Int(1, 0), out var projectileAfterTick), Is.True);
            Assert.That(projectileAfterTick.entityId, Is.EqualTo(11));
            Assert.That(projectileAfterTick.spawnTick, Is.EqualTo(4));
            Assert.That(projectileAfterTick.stateTimer, Is.EqualTo(defaultTimingProfile.ProjectileStepIntervalTicks));
            Assert.That(result.Trace.Text, Does.Contain("SpawnCommitted|G=1|I=1|SpawnId=1|E=11|Pos=(1,0)|Type=Projectile|SpawnTick=4"));
            Assert.That(result.Trace.Text, Does.Contain($"Spawns=[SpawnId=1:Entity=E=11|Pos=(1,0)|Hp=1/1|Team=1|Type=Projectile|State=Idle|Timer={defaultTimingProfile.ProjectileStepIntervalTicks}|Facing=Right|Marked=False|SpawnTick=4|BoxCapabilities=None|Face=Floor|Presence=Occupying]"));
        }

        [Test]
        public void Attack_SpawnIds_AreAssignedByCommitOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3, facing: Direction.Right),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(3, 0), hp: 3, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(attackIntentFactory: _ => RawAttackIntent.CreateFireProjectile(10, 5)),
                    new StubCombatLogic(attackIntentFactory: _ => RawAttackIntent.CreateFireProjectile(20, 10)),
                });

            var result = pipeline.RunTick(new TickInput(6));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Acting|Timer=0",
                    "StateChanged|G=2|I=2|E=10|State=Acting|Timer=0",
                    "SpawnCommitted|G=1|I=1|SpawnId=1|E=21|Pos=(2,0)|Type=Projectile|SpawnTick=6",
                    "SpawnCommitted|G=2|I=2|SpawnId=2|E=22|Pos=(1,0)|Type=Projectile|SpawnTick=6",
                },
                result.AttackPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 1, SourceId: 20, SpawnId: 1, EntityId: 21),
                    (GroupId: 2, SourceId: 10, SpawnId: 2, EntityId: 22),
                },
                result.AttackPhaseResult
                    .SelectedGroups
                    .Select(group =>
                    {
                        var spawn = group.Spawns.Single();
                        return (group.GroupId, group.SourceId, spawn.SpawnId, EntityId: spawn.Entity.entityId);
                    })
                    .ToArray());
            Assert.That(snapshotAfter.TryGetProjectileAt(new Vector2Int(2, 0), out var highPriorityProjectile), Is.True);
            Assert.That(snapshotAfter.TryGetProjectileAt(new Vector2Int(1, 0), out var lowPriorityProjectile), Is.True);
            Assert.That(highPriorityProjectile.entityId, Is.EqualTo(21));
            Assert.That(lowPriorityProjectile.entityId, Is.EqualTo(22));
        }

        [Test]
        public void Attack_SpawnedProjectile_BeginsMovingOnlyAfterConfiguredCadence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3, facing: Direction.Right),
            });
            var logic = new TickScriptedCombatLogic(
                10,
                new Dictionary<int, RawAttackIntent>
                {
                    { 1, RawAttackIntent.CreateFireProjectile(10, 5) },
                });
            var timingProfile = CreateTimingProfile(
                simulationTicksPerSecond: 60,
                projectileStepIntervalSeconds: 0.2f);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[] { logic },
                timingProfile);

            logic.SetTickIndex(1);
            var firstResult = pipeline.RunTick(new TickInput(1));
            var snapshotAfterFirstTick = CreateSnapshot(worldState);

            Assert.That(firstResult.MovementPhaseResult.SortedIntents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=10|State=Acting|Timer=0",
                    "SpawnCommitted|G=1|I=1|SpawnId=1|E=11|Pos=(1,0)|Type=Projectile|SpawnTick=1",
                },
                firstResult.AttackPhaseResult.CommitEvents);
            Assert.That(firstResult.EventLog.Any(evt => evt.Contains("ImpactReservationCreated")), Is.False);
            Assert.That(snapshotAfterFirstTick.TryGetProjectileAt(new Vector2Int(1, 0), out var projectileAfterFirstTick), Is.True);
            Assert.That(projectileAfterFirstTick.entityId, Is.EqualTo(11));
            Assert.That(projectileAfterFirstTick.stateTimer, Is.EqualTo(timingProfile.ProjectileStepIntervalTicks));

            var idleResults = RunTicks(
                pipeline,
                startTickIndex: 2,
                endTickIndex: timingProfile.ProjectileStepIntervalTicks + 1,
                beforeTick: logic.SetTickIndex);
            var snapshotBeforeFirstMove = CreateSnapshot(worldState);

            Assert.That(idleResults.All(result => result.MovementPhaseResult.SortedIntents.Count == 0), Is.True);
            Assert.That(snapshotBeforeFirstMove.TryGetProjectileAt(new Vector2Int(1, 0), out var projectileBeforeFirstMove), Is.True);
            Assert.That(projectileBeforeFirstMove.entityId, Is.EqualTo(11));
            Assert.That(projectileBeforeFirstMove.stateTimer, Is.EqualTo(0));

            logic.SetTickIndex(timingProfile.ProjectileStepIntervalTicks + 2);
            var firstMoveResult = pipeline.RunTick(new TickInput(timingProfile.ProjectileStepIntervalTicks + 2));
            var snapshotAfterFirstMove = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (SourceId: 11, IntentId: 1, Destination: new Vector2Int(2, 0)) },
                firstMoveResult.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=11|State=Idle|Timer=12",
                    "MoveCommitted|G=1|I=1|E=11|To=(2,0)|Facing=Right",
                },
                firstMoveResult.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfterFirstMove.TryGetProjectileAt(new Vector2Int(2, 0), out var projectileAfterFirstMove), Is.True);
            Assert.That(projectileAfterFirstMove.entityId, Is.EqualTo(11));
            Assert.That(projectileAfterFirstMove.stateTimer, Is.EqualTo(timingProfile.ProjectileStepIntervalTicks - 1));
        }

        [Test]
        public void Movement_PreExistingProjectileAt60Tps_PreservesRealTimeCadence()
        {
            var timingProfile = CreateTimingProfile(
                simulationTicksPerSecond: 60,
                projectileStepIntervalSeconds: 0.2f);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateProjectile(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 1, facing: Direction.Right),
                },
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile);

            var firstResult = pipeline.RunTick(new TickInput(1));
            var snapshotAfterFirstTick = CreateSnapshot(worldState);

            Assert.That(firstResult.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(snapshotAfterFirstTick.TryGetProjectileAt(new Vector2Int(0, 0), out var projectileAfterFirstTick), Is.True);
            Assert.That(projectileAfterFirstTick.stateTimer, Is.EqualTo(timingProfile.ProjectileStepIntervalTicks - 1));

            RunTicks(
                pipeline,
                startTickIndex: 2,
                endTickIndex: timingProfile.ProjectileStepIntervalTicks);

            var firstMoveResult = pipeline.RunTick(new TickInput(timingProfile.ProjectileStepIntervalTicks + 1));
            var snapshotAfterFirstMove = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0)) },
                firstMoveResult.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            Assert.That(snapshotAfterFirstMove.TryGetProjectileAt(new Vector2Int(1, 0), out var projectileAfterFirstMove), Is.True);
            Assert.That(projectileAfterFirstMove.entityId, Is.EqualTo(10));
            Assert.That(projectileAfterFirstMove.stateTimer, Is.EqualTo(timingProfile.ProjectileStepIntervalTicks - 1));

            RunTicks(
                pipeline,
                startTickIndex: timingProfile.ProjectileStepIntervalTicks + 2,
                endTickIndex: (timingProfile.ProjectileStepIntervalTicks * 2));

            var secondMoveResult = pipeline.RunTick(new TickInput((timingProfile.ProjectileStepIntervalTicks * 2) + 1));
            var snapshotAfterSecondMove = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(2, 0)) },
                secondMoveResult.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            Assert.That(snapshotAfterSecondMove.TryGetProjectileAt(new Vector2Int(2, 0), out var projectileAfterSecondMove), Is.True);
            Assert.That(projectileAfterSecondMove.entityId, Is.EqualTo(10));
        }

        [Test]
        public void Movement_PreExistingProjectileAt120Tps_PreservesSameRealTimeCadence()
        {
            var timingProfile = CreateTimingProfile(
                simulationTicksPerSecond: 120,
                projectileStepIntervalSeconds: 0.2f);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateProjectile(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 1, facing: Direction.Right),
                },
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile);

            var firstResult = pipeline.RunTick(new TickInput(1));
            Assert.That(firstResult.MovementPhaseResult.SortedIntents, Is.Empty);

            RunTicks(
                pipeline,
                startTickIndex: 2,
                endTickIndex: timingProfile.ProjectileStepIntervalTicks);

            var firstMoveResult = pipeline.RunTick(new TickInput(timingProfile.ProjectileStepIntervalTicks + 1));
            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0)) },
                firstMoveResult.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());

            RunTicks(
                pipeline,
                startTickIndex: timingProfile.ProjectileStepIntervalTicks + 2,
                endTickIndex: (timingProfile.ProjectileStepIntervalTicks * 2));

            var secondMoveResult = pipeline.RunTick(new TickInput((timingProfile.ProjectileStepIntervalTicks * 2) + 1));
            var snapshotAfterSecondMove = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(2, 0)) },
                secondMoveResult.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            Assert.That(snapshotAfterSecondMove.TryGetProjectileAt(new Vector2Int(2, 0), out var projectileAfterSecondMove), Is.True);
            Assert.That(projectileAfterSecondMove.entityId, Is.EqualTo(10));
        }

        [Test]
        public void Attack_SpawnedEntityIds_AreNotReusedAfterCleanupAcrossTicks()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3, facing: Direction.Right),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(2, 0), hp: 1),
            });
            var logic = new TickScriptedCombatLogic(
                10,
                new Dictionary<int, RawAttackIntent>
                {
                    { 1, RawAttackIntent.CreateFireProjectile(10, 5) },
                    { 15, RawAttackIntent.CreateFireProjectile(10, 5) },
                });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, new IEntityLogic[] { logic });

            logic.SetTickIndex(1);
            var firstResult = pipeline.RunTick(new TickInput(1));
            var firstProjectileId = firstResult.AttackPhaseResult.SelectedGroups.Single().Spawns.Single().Entity.entityId;

            var intermediateResults = RunTicks(
                pipeline,
                startTickIndex: 2,
                endTickIndex: 14,
                beforeTick: logic.SetTickIndex);
            var cleanupResult = intermediateResults.Last();

            logic.SetTickIndex(15);
            var secondSpawnResult = pipeline.RunTick(new TickInput(15));
            var secondProjectileId = secondSpawnResult.AttackPhaseResult.SelectedGroups.Single().Spawns.Single().Entity.entityId;

            CollectionAssert.AreEqual(new[] { 20, firstProjectileId }, cleanupResult.CleanupPhaseResult.RemovedEntityIds.OrderBy(id => id).ToArray());
            Assert.That(secondProjectileId, Is.EqualTo(firstProjectileId + 1));
            Assert.That(secondProjectileId, Is.Not.EqualTo(firstProjectileId));
        }

        [Test]
        public void Attack_OnHit_DoesNotCreateSameTickNewIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 5, teamId: 1, position: new Vector2Int(2, 0), hp: 1, facing: Direction.Left),
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 2),
            });
            var retargetingLogic = new PriorityTargetSelectionLogic(10, 5, 20, 40);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(movementIntent: new RawMovementIntent(5, 0, new Vector2Int(1, 0))),
                    retargetingLogic,
                });

            var result = pipeline.RunTick(new TickInput(1));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(retargetingLogic.AttackCollectCallCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Command: AttackCommandKind.Attack, TargetId: 20, IsSynthetic: false),
                    (SourceId: 5, Command: AttackCommandKind.ImpactReservation, TargetId: 20, IsSynthetic: true),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.CommandKind, intent.TargetId, intent.IsSynthetic))
                    .ToArray());
            Assert.That(result.AttackPhaseResult.SortedInputs.Any(intent => intent.TargetId == 40), Is.False);
            Assert.That(
                result.AttackPhaseResult.ExpandedCandidates.Any(group => group.Damages.Any(damage => damage.TargetId == 40)),
                Is.False);
            Assert.That(result.EventLog.Any(evt => evt.Contains("Target=40")), Is.False);
            CollectionAssert.AreEqual(new[] { 5, 20 }, result.CleanupPhaseResult.RemovedEntityIds.OrderBy(id => id).ToArray());
            Assert.That(snapshotAfter.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshotAfter.TryGetEntity(40, out var fallbackAfter), Is.True);
            Assert.That(fallbackAfter.hp, Is.EqualTo(2));
            Assert.That(fallbackAfter.markedForDeath, Is.False);
        }

        [Test]
        public void Attack_OnHit_DoesNotReenterMovementPhase()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 5, teamId: 1, position: new Vector2Int(2, 0), hp: 1, facing: Direction.Left),
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
            });
            var attackLogic = new PriorityTargetSelectionLogic(10, 5, 20);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(movementIntent: new RawMovementIntent(5, 0, new Vector2Int(1, 0))),
                    attackLogic,
                });

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(attackLogic.MovementCollectCallCount, Is.EqualTo(1));
            Assert.That(attackLogic.AttackCollectCallCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { TickPhase.Movement, TickPhase.Attack, TickPhase.Cleanup },
                result.CompletedPhases);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Movement:Enter",
                    "Movement:Exit",
                    "Attack:Enter",
                    "Attack:Exit",
                    "Cleanup:Enter",
                    "Cleanup:Exit",
                },
                result.PhaseTrace);
            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationCreated|G=1|I=1|Source=5|Target=20|At=(1,0)|Damage=1|Sequence=1",
                },
                result.MovementPhaseResult.CommitEvents);
        }

        [Test]
        public void Attack_ImpactReservation_CanStillExpandToFixedSameTickActions()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 1, facing: Direction.Right),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(movementIntent: new RawMovementIntent(10, 0, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.AttackPhaseResult.RawIntents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20, Sequence: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (reservation.SourceId, reservation.TargetId, reservation.ReservationSequence))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Command: AttackCommandKind.ImpactReservation, TargetId: 20, IsSynthetic: true),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.CommandKind, intent.TargetId, intent.IsSynthetic))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "DamageCommitted|G=2|I=2|Target=20|Amount=1",
                    "DamageCommitted|G=2|I=2|Target=10|Amount=1",
                    "DestroyMarked|G=2|I=2|Target=10|FinalHp=0|Condition=WhenHpDepleted",
                },
                result.AttackPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(new[] { 10 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(snapshotAfter.TryGetEntity(10, out _), Is.False);
            Assert.That(snapshotAfter.TryGetEntity(20, out var targetAfter), Is.True);
            Assert.That(targetAfter.hp, Is.EqualTo(1));
            Assert.That(targetAfter.markedForDeath, Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Attack.DrainedImpacts"));
            Assert.That(result.Trace.Text, Does.Contain("Command=ImpactReservation"));
        }

        [Test]
        public void DelayedEventQueue_DrainsOnNextTickOnly()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            pipeline.EnqueueDelayedAttackEffect(
                new DelayedAttackEffectRecord(
                    sourceId: 10,
                    targetId: 20,
                    damage: 1,
                    priority: 5,
                    tickGenerated: 1,
                    executeAtTick: 2,
                    sourceActionGroupId: 99,
                    effectSequence: 1));

            var firstResult = pipeline.RunTick(new TickInput(1));
            var snapshotAfterFirstTick = CreateSnapshot(worldState);

            Assert.That(firstResult.AttackPhaseResult.DrainedDelayedAttackEffects, Is.Empty);
            Assert.That(firstResult.AttackPhaseResult.SortedInputs, Is.Empty);
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
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Command: AttackCommandKind.DelayedEffect, TargetId: 20, IsSynthetic: true),
                },
                secondResult.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.CommandKind, intent.TargetId, intent.IsSynthetic))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "DamageCommitted|G=1|I=1|Target=20|Amount=1",
                },
                secondResult.AttackPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    "DelayedAttackDrained|Tick=2|Source=10|Target=20|Damage=1|GeneratedTick=1|ExecuteTick=2|Group=99|Sequence=1",
                    "DamageCommitted|G=1|I=1|Target=20|Amount=1",
                },
                secondResult.EventLog);
            Assert.That(GetEntityHp(snapshotAfterSecondTick, 20), Is.EqualTo(1));
            Assert.That(IsMarkedForDeath(snapshotAfterSecondTick, 20), Is.False);
        }

        [Test]
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
            var commitEvents = new List<string>();
            var delayedAttackEnqueueEvents = new List<string>();

            new AttackCommitter().Commit(
                snapshot,
                worldState.CreateWriteContext(),
                tickIndex: 7,
                queue,
                new[] { group },
                commitEvents,
                delayedAttackEnqueueEvents);

            var snapshotAfterCommit = CreateSnapshot(worldState);
            var sameTickDrain = queue.Drain(7);
            var nextTickDrain = queue.Drain(8);

            CollectionAssert.AreEqual(
                new[]
                {
                    "DamageCommitted|G=1|I=1|Target=20|Amount=1",
                },
                commitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    "DelayedAttackEnqueued|G=1|I=1|Source=10|Target=30|Damage=1|ExecuteTick=8|Sequence=1",
                },
                delayedAttackEnqueueEvents);
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
            var commitEvents = new List<string> { "StaleCommitEvent" };
            var delayedAttackEnqueueEvents = new List<string> { "StaleDelayedEvent" };

            new AttackCommitter().Commit(
                snapshot,
                worldState.CreateWriteContext(),
                tickIndex: 7,
                queue,
                new[] { group },
                commitEvents,
                delayedAttackEnqueueEvents);

            CollectionAssert.AreEqual(
                new[]
                {
                    "DamageCommitted|G=1|I=1|Target=20|Amount=1",
                },
                commitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    "DelayedAttackEnqueued|G=1|I=1|Source=10|Target=30|Damage=1|ExecuteTick=8|Sequence=1",
                },
                delayedAttackEnqueueEvents);
        }

        private static (TickResult Result, WorldSnapshot SnapshotAfter, string OccupancyAfter) RunFatalAttackTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubCombatLogic(attackIntentFactory: snapshot => TryCreateAdjacentAttack(snapshot, 10, 30, 5)),
                    new StubCombatLogic(attackIntentFactory: snapshot => TryCreateAdjacentAttack(snapshot, 20, 30, 5)),
                });

            var result = pipeline.RunTick(new TickInput(5));
            var snapshotAfter = CreateSnapshot(worldState);
            return (result, snapshotAfter, DumpUnitOccupancy(snapshotAfter));
        }

        private static AttackPhaseResult RunAttackPhaseOnly(
            WorldState worldState,
            IReadOnlyList<IEntityLogic> entityLogics,
            int tickIndex,
            TickInput input = default)
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
            FinalizeAttackSpawns(selectedGroups, idAllocator, entityIdAllocator);

            var commitEvents = new List<string>();
            var delayedAttackEnqueueEvents = new List<string>();
            new AttackCommitter().Commit(
                snapshot,
                worldState.CreateWriteContext(),
                tickIndex,
                new DelayedAttackEffectQueue(),
                selectedGroups,
                commitEvents,
                delayedAttackEnqueueEvents);

            return new AttackPhaseResult(
                rawAttackIntents,
                drainedImpactReservations,
                sortedInputs,
                expandedCandidates,
                selectedGroups,
                commitEvents,
                rejectedReasons);
        }

        private static void FinalizeAttackSpawns(
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

        private static RawAttackIntent? TryCreateAdjacentAttack(
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

            if (Math.Abs(source.position.x - target.position.x) + Math.Abs(source.position.y - target.position.y) != 1)
            {
                return null;
            }

            return new RawAttackIntent(sourceId, priority, targetId);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp,
            Direction facing = Direction.None)
        {
            return CreateUnit(entityId, teamId, SurfaceCell.FromPlanar(position), hp, facing);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp,
            Direction facing = Direction.None)
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
                facing = facing,
            };
        }

        private static EntityState CreateProjectile(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp,
            Direction facing = Direction.None)
        {
            return CreateProjectile(entityId, teamId, SurfaceCell.FromPlanar(position), hp, facing);
        }

        private static EntityState CreateProjectile(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp,
            Direction facing = Direction.None)
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

        private static GameplayTimingProfile CreateTimingProfile(
            int simulationTicksPerSecond = 60,
            float initialMoveDelaySeconds = 0f,
            float repeatedMoveIntervalSeconds = 0.4f,
            float boxSlideStepIntervalSeconds = 0.2f,
            float projectileStepIntervalSeconds = 0.2f,
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
                projectileStepIntervalSeconds,
                pushMotionDurationSeconds,
                flipMotionDurationSeconds,
                flipArcHeightInCells,
                maxTicksPerFrame);
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

        private sealed class StubCombatLogic : IEntityLogic, IEntityLogicSourceBinding
        {
            private readonly Func<WorldSnapshot, RawAttackIntent?> _attackIntentFactory;
            private readonly RawMovementIntent? _movementIntent;

            public StubCombatLogic(
                RawMovementIntent? movementIntent = null,
                Func<WorldSnapshot, RawAttackIntent?> attackIntentFactory = null)
            {
                _movementIntent = movementIntent;
                _attackIntentFactory = attackIntentFactory;
            }

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

            public bool ControlsEntity(int entityId, TickPhase phase)
            {
                return phase == TickPhase.Movement &&
                    _movementIntent.HasValue &&
                    _movementIntent.Value.SourceId == entityId;
            }
        }

        private sealed class TickScriptedCombatLogic : IEntityLogic
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

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
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

        private sealed class PriorityTargetSelectionLogic : IEntityLogic
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

            public int MovementCollectCallCount { get; private set; }

            public int AttackCollectCallCount { get; private set; }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                MovementCollectCallCount++;
            }

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
