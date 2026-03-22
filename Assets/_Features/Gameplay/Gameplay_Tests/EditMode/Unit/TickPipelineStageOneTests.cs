using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Groups;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineStageOneTests
    {
        [Test]
        public void IdAllocator_ResetForTick_RestartsCategorySequences()
        {
            var allocator = new IdAllocator();

            allocator.ResetForTick(3);

            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(2));
            Assert.That(allocator.AllocateGroupId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateSpawnId(), Is.EqualTo(1));

            allocator.ResetForTick(4);

            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateGroupId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateSpawnId(), Is.EqualTo(1));
        }

        [Test]
        public void RunTick_SortsRawIntents_AndAssignsCentralIntentIds()
        {
            var entityLogics = new IEntityLogic[]
            {
                new StubEntityLogic(new RawMovementIntent(20, 10), new RawAttackIntent(20, 10)),
                new StubEntityLogic(new RawMovementIntent(10, 5), new RawAttackIntent(10, 5)),
            };
            var pipeline = new TickPipeline(new WorldState(), entityLogics);

            var result = pipeline.RunTick(new TickInput(12));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1),
                    (SourceId: 20, IntentId: 2),
                },
                result.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 3),
                    (SourceId: 20, IntentId: 4),
                },
                result.AttackPhaseResult.SortedInputs.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
        }

        [Test]
        public void WorldSnapshot_EnumeratesEntitiesInEntityIdOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 30,
                    position = new Vector2Int(3, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
                new EntityState
                {
                    entityId = 20,
                    position = new Vector2Int(2, 0),
                    hp = 1,
                    maxHp = 1,
                    teamId = 2,
                    type = EntityType.Projectile,
                },
            });
            var snapshot = CreateSnapshot(worldState);
            var orderedEntities = new List<EntityState>();

            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, orderedEntities.Select(entity => entity.entityId).ToArray());
        }

        [Test]
        public void WorldSnapshot_BlocksMovementUntilCleanupEvenWhenEntityIsMarkedForDeath()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
            });
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.IsBlockedForUnit(new Vector2Int(1, 0)), Is.True);
            Assert.That(snapshot.BlocksMovement(10), Is.True);
        }

        [Test]
        public void WorldSnapshot_CanBeTargetedForNewSelection_FollowsMarkedForDeathPolicy()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
            });
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.CanBeTargetedForNewSelection(10), Is.False);
        }

        [Test]
        public void WorldSnapshot_IsImmutable_AfterWorldMutation()
        {
            var worldState = CreateWorldState(new[]
            {
                new EntityState
                {
                    entityId = 30,
                    position = new Vector2Int(3, 0),
                    hp = 3,
                    maxHp = 3,
                    teamId = 1,
                    type = EntityType.Unit,
                },
                new EntityState
                {
                    entityId = 10,
                    position = new Vector2Int(1, 0),
                    hp = 0,
                    maxHp = 3,
                    teamId = 2,
                    type = EntityType.Unit,
                    markedForDeath = true,
                },
                new EntityState
                {
                    entityId = 20,
                    position = new Vector2Int(2, 0),
                    hp = 1,
                    maxHp = 1,
                    teamId = 2,
                    type = EntityType.Projectile,
                },
            });
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(1, 0), out var deathMarkedUnit), Is.True);
            Assert.That(deathMarkedUnit.entityId, Is.EqualTo(10));
            Assert.That(snapshot.TryGetProjectileAt(new Vector2Int(2, 0), out var projectile), Is.True);
            Assert.That(projectile.entityId, Is.EqualTo(20));

            CreateWriteContext(worldState).MoveEntity(30, new Vector2Int(5, 0));

            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(3, 0), out var originalUnit), Is.True);
            Assert.That(originalUnit.entityId, Is.EqualTo(30));
            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(5, 0), out _), Is.False);
        }

        [Test]
        public void PhaseTransientBuffer_DrainImpacts_ReturnsDeterministicOrder_AndClearsBuffer()
        {
            var transientBuffer = new PhaseTransientBuffer();
            transientBuffer.AddImpact(new ImpactReservation(2, 20, new Vector2Int(1, 0), 1, 5, 2, 3));
            transientBuffer.AddImpact(new ImpactReservation(1, 30, new Vector2Int(0, 0), 1, 5, 2, 2));
            transientBuffer.AddImpact(new ImpactReservation(1, 10, new Vector2Int(0, 0), 1, 5, 1, 1));

            var drainedImpacts = transientBuffer.DrainImpacts();

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, GroupId: 1, Sequence: 1),
                    (SourceId: 1, GroupId: 2, Sequence: 2),
                    (SourceId: 2, GroupId: 2, Sequence: 3),
                },
                drainedImpacts
                    .Select(impact => (impact.SourceId, GroupId: impact.SourceActionGroupId, Sequence: impact.ReservationSequence))
                    .ToArray());
            Assert.That(transientBuffer.DrainImpacts(), Is.Empty);
        }

        [Test]
        public void AttackInputComparer_SortsBySourceKindAndLocalSequence()
        {
            var impactIntent = AttackIntent.FromImpactReservation(
                new ImpactReservation(1, 10, new Vector2Int(0, 0), 1, 5, 1, 2));
            var entityIntentSameSource = new AttackIntent(1, 99, AttackInputKind.EntityIntent, 0);
            var laterEntityIntent = new AttackIntent(2, 1, AttackInputKind.EntityIntent, 0);

            var sortedInputs = new List<AttackIntent>
            {
                impactIntent,
                laterEntityIntent,
                entityIntentSameSource,
            };

            sortedInputs.Sort(AttackInputComparer.Instance);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, Kind: AttackInputKind.EntityIntent, Sequence: 0),
                    (SourceId: 1, Kind: AttackInputKind.ImpactReservation, Sequence: 2),
                    (SourceId: 2, Kind: AttackInputKind.EntityIntent, Sequence: 0),
                },
                sortedInputs.Select(intent => (intent.SourceId, intent.InputKind, intent.LocalSequence)).ToArray());
        }

        [Test]
        public void IntentComparer_ProvidesTotalOrder()
        {
            var first = new MoveIntent(1, 10);
            var second = new MoveIntent(1, 10);
            var third = new AttackIntent(1, 10, AttackInputKind.EntityIntent, 0);
            first.AssignIntentId(1);
            second.AssignIntentId(2);
            third.AssignIntentId(3);

            var sortedIntents = new List<Intent>
            {
                third,
                second,
                first,
            };

            sortedIntents.Sort(IntentComparer.Instance);

            CollectionAssert.AreEqual(
                new Intent[] { first, second, third },
                sortedIntents);
        }

        [Test]
        public void ActionGroupComparer_ProvidesTotalOrder()
        {
            var second = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 2);
            var first = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 1);
            var third = CreateActionGroup(intentId: 1, sourceId: 2, priority: 10, groupId: 1);

            var sortedGroups = new List<ActionGroup>
            {
                third,
                second,
                first,
            };

            sortedGroups.Sort(ActionGroupComparer.Instance);

            CollectionAssert.AreEqual(
                new[] { first, second, third },
                sortedGroups);
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

        private static IWorldWriteContext CreateWriteContext(WorldState worldState)
        {
            var createWriteContextMethod = typeof(WorldState).GetMethod(
                "CreateWriteContext",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createWriteContextMethod, Is.Not.Null);

            return (IWorldWriteContext)createWriteContextMethod.Invoke(worldState, null);
        }

        private static ActionGroup CreateActionGroup(int intentId, int sourceId, int priority, int groupId)
        {
            var actionGroup = new ActionGroup(intentId, sourceId, priority, ActionGroupKind.Move);
            actionGroup.AssignGroupId(groupId);
            return actionGroup;
        }

        private sealed class StubEntityLogic : IEntityLogic
        {
            private readonly RawAttackIntent? _attackIntent;
            private readonly RawMovementIntent? _movementIntent;

            public StubEntityLogic(RawMovementIntent? movementIntent, RawAttackIntent? attackIntent)
            {
                _movementIntent = movementIntent;
                _attackIntent = attackIntent;
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
                if (_attackIntent.HasValue)
                {
                    buffer.Add(_attackIntent.Value);
                }
            }
        }
    }
}
