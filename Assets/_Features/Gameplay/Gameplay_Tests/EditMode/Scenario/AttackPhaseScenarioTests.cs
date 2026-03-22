using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Resolution;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Sorting;
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
            var pipeline = new TickPipeline(
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

            Assert.That(GetEntityPosition(snapshotAfter, 10), Is.EqualTo(new Vector2Int(1, 0)));
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
                    "DestroyMarked|G=1|I=1|Target=30|FinalHp=-1",
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
                    "DestroyMarked|G=1|I=1|Target=30|FinalHp=-1",
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
            var pipeline = new TickPipeline(
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
            Assert.That(GetEntityHp(snapshotAfter, 20), Is.EqualTo(3));
            Assert.That(IsMarkedForDeath(snapshotAfter, 20), Is.False);
        }

        private static (TickResult Result, WorldSnapshot SnapshotAfter, string OccupancyAfter) RunFatalAttackTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
            });
            var pipeline = new TickPipeline(
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
            int tickIndex)
        {
            var snapshot = SnapshotBuilder.Create(worldState);
            var rawAttackIntents = new List<RawAttackIntent>();
            new AttackIntentCollector().Collect(snapshot, entityLogics, rawAttackIntents);

            var idAllocator = new IdAllocator();
            idAllocator.ResetForTick(tickIndex);

            var sortedInputs = new List<AttackIntent>(rawAttackIntents.Count);
            for (var i = 0; i < rawAttackIntents.Count; i++)
            {
                var rawIntent = rawAttackIntents[i];
                sortedInputs.Add(new AttackIntent(rawIntent.SourceId, rawIntent.Priority, rawIntent.TargetId));
            }

            sortedInputs.Sort(AttackInputComparer.Instance);
            for (var i = 0; i < sortedInputs.Count; i++)
            {
                sortedInputs[i].AssignIntentId(idAllocator.AllocateIntentId());
            }

            var expandedCandidates = new List<ActionGroup>();
            new AttackExpander().Expand(snapshot, sortedInputs, expandedCandidates);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(idAllocator.AllocateGroupId());
            }

            var selectedGroups = new List<ActionGroup>();
            new AttackResolver().Resolve(expandedCandidates, selectedGroups);

            var commitEvents = new List<string>();
            new AttackCommitter().Commit(
                snapshot,
                worldState.CreateWriteContext(),
                selectedGroups,
                commitEvents);

            return new AttackPhaseResult(
                sortedInputs,
                expandedCandidates,
                selectedGroups,
                commitEvents);
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

        private static Vector2Int GetEntityPosition(WorldSnapshot snapshot, int entityId)
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

        private sealed class StubCombatLogic : IEntityLogic
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
    }
}
