using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TickPipelineStageOneCoreTests
    {
        [Test]
        [Category("Core")]
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
        [Category("Core")]
        public void TickInputBuffer_RecordRejectsDuplicateTick()
        {
            var inputBuffer = new TickInputBuffer();

            inputBuffer.Record(new TickInput(3));

            Assert.That(
                () => inputBuffer.Record(new TickInput(3)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        [Category("Core")]
        public void TickInputBuffer_ConsumeOrDefault_ReturnsRecordedInputOrDefaultTick()
        {
            var inputBuffer = new TickInputBuffer();

            inputBuffer.Record(new TickInput(5));

            Assert.That(inputBuffer.HasBufferedInput(5), Is.True);
            Assert.That(inputBuffer.ConsumeOrDefault(5).TickIndex, Is.EqualTo(5));
            Assert.That(inputBuffer.HasBufferedInput(5), Is.False);
            Assert.That(inputBuffer.ConsumeOrDefault(6).TickIndex, Is.EqualTo(6));
        }

        [Test]
        [Category("Core")]
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
        [Category("Core")]
        public void DelayedEventQueue_OrderIsDeterministic()
        {
            var queue = new DelayedAttackEffectQueue();
            queue.Enqueue(new DelayedAttackEffectRecord(2, 40, 1, 5, 3, 4, 2, 3));
            queue.Enqueue(new DelayedAttackEffectRecord(1, 30, 1, 5, 3, 5, 3, 1));
            queue.Enqueue(new DelayedAttackEffectRecord(1, 20, 1, 5, 3, 4, 2, 2));
            queue.Enqueue(new DelayedAttackEffectRecord(1, 10, 1, 5, 3, 4, 1, 1));

            var drainedEffects = queue.Drain(4);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, GroupId: 1, Sequence: 1, TargetId: 10),
                    (SourceId: 1, GroupId: 2, Sequence: 2, TargetId: 20),
                    (SourceId: 2, GroupId: 2, Sequence: 3, TargetId: 40),
                },
                drainedEffects
                    .Select(effect => (effect.SourceId, GroupId: effect.SourceActionGroupId, Sequence: effect.EffectSequence, effect.TargetId))
                    .ToArray());
            Assert.That(queue.Drain(4), Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 1, GroupId: 3, Sequence: 1, TargetId: 30),
                },
                queue.Drain(5)
                    .Select(effect => (effect.SourceId, GroupId: effect.SourceActionGroupId, Sequence: effect.EffectSequence, effect.TargetId))
                    .ToArray());
        }

        [Test]
        [Category("Core")]
        public void AttackInputComparer_SortsBySourceKindAndLocalSequence()
        {
            var impactIntent = AttackIntent.FromImpactReservation(
                new ImpactReservation(1, 10, new Vector2Int(0, 0), 1, 5, 1, 2));
            var entityIntentSameSource = new AttackIntent(1, 99, 11);
            var laterEntityIntent = new AttackIntent(2, 1, 22);

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
                    (SourceId: 2, Kind: AttackInputKind.EntityIntent, Sequence: 0),
                    (SourceId: 1, Kind: AttackInputKind.ImpactReservation, Sequence: 2),
                },
                sortedInputs.Select(intent => (intent.SourceId, intent.InputKind, intent.LocalSequence)).ToArray());
        }

        [Test]
        [Category("Core")]
        public void IntentComparer_ProvidesTotalOrder()
        {
            var first = new MoveIntent(1, 10);
            var second = new MoveIntent(1, 10);
            var third = new AttackIntent(1, 10, 20);
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
        [Category("Core")]
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

        private static ActionGroup CreateActionGroup(int intentId, int sourceId, int priority, int groupId)
        {
            var actionGroup = new ActionGroup(intentId, sourceId, priority, ActionGroupKind.Move);
            actionGroup.AssignGroupId(groupId);
            return actionGroup;
        }
    }
}
