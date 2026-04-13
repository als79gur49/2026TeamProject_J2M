using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class AttackInputNormalizationCoreTests
    {
        [Test]
        [Category("Core")]
        public void AttackInputNormalizer_NormalizesEntityIntentsAndReservations_ByDocumentOrder()
        {
            var rawAttackIntents = new[]
            {
                new RawAttackIntent(20, 99, 200),
                new RawAttackIntent(10, 1, 100),
            };
            var impactReservations = new[]
            {
                new ImpactReservation(20, 210, new Vector2Int(2, 0), 1, 5, 8, 3),
                new ImpactReservation(10, 111, new Vector2Int(1, 0), 1, 5, 7, 2),
                new ImpactReservation(10, 110, new Vector2Int(1, 0), 1, 5, 6, 1),
            };
            var normalizedInputs = new List<AttackIntent>();

            new AttackInputNormalizer().Normalize(rawAttackIntents, impactReservations, normalizedInputs);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, Kind: AttackInputKind.EntityIntent, LocalSequence: 0, TargetId: 200),
                    (SourceId: 10, Kind: AttackInputKind.EntityIntent, LocalSequence: 0, TargetId: 100),
                    (SourceId: 10, Kind: AttackInputKind.ImpactReservation, LocalSequence: 1, TargetId: 110),
                    (SourceId: 10, Kind: AttackInputKind.ImpactReservation, LocalSequence: 2, TargetId: 111),
                    (SourceId: 20, Kind: AttackInputKind.ImpactReservation, LocalSequence: 3, TargetId: 210),
                },
                normalizedInputs
                    .Select(intent => (intent.SourceId, intent.InputKind, intent.LocalSequence, intent.TargetId))
                    .ToArray());
        }

        [Test]
        [Category("Core")]
        public void AttackInputNormalizer_DuplicateReservationSequenceForSameSource_Throws()
        {
            var impactReservations = new[]
            {
                new ImpactReservation(10, 20, new Vector2Int(1, 0), 1, 5, 1, 1),
                new ImpactReservation(10, 30, new Vector2Int(1, 1), 1, 5, 2, 1),
            };

            var exception = Assert.Throws<InvalidOperationException>(
                () => new AttackInputNormalizer().Normalize(
                    Array.Empty<RawAttackIntent>(),
                    impactReservations,
                    new List<AttackIntent>()));

            StringAssert.Contains("Duplicate attack input normalization key detected", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void AttackInputNormalizer_AssignsSyntheticIntentIds_AfterPostSortNormalization()
        {
            var rawAttackIntents = new[]
            {
                new RawAttackIntent(20, 99, 200),
                new RawAttackIntent(10, 1, 100),
            };
            var impactReservations = new[]
            {
                new ImpactReservation(10, 110, new Vector2Int(1, 0), 1, 5, 6, 2),
                new ImpactReservation(10, 120, new Vector2Int(1, 1), 1, 5, 6, 1),
            };
            var normalizedInputs = new List<AttackIntent>();
            var allocator = new IdAllocator();

            new AttackInputNormalizer().Normalize(rawAttackIntents, impactReservations, normalizedInputs);

            allocator.ResetForTick(12);
            for (var i = 0; i < normalizedInputs.Count; i++)
            {
                normalizedInputs[i].AssignIntentId(allocator.AllocateIntentId());
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    (IntentId: 1, SourceId: 20, Kind: AttackInputKind.EntityIntent, LocalSequence: 0, IsSynthetic: false),
                    (IntentId: 2, SourceId: 10, Kind: AttackInputKind.EntityIntent, LocalSequence: 0, IsSynthetic: false),
                    (IntentId: 3, SourceId: 10, Kind: AttackInputKind.ImpactReservation, LocalSequence: 1, IsSynthetic: true),
                    (IntentId: 4, SourceId: 10, Kind: AttackInputKind.ImpactReservation, LocalSequence: 2, IsSynthetic: true),
                },
                normalizedInputs
                    .Select(intent => (intent.IntentId, intent.SourceId, intent.InputKind, intent.LocalSequence, intent.IsSynthetic))
                    .ToArray());
        }

        [Test]
        [Category("Core")]
        public void RawAttackIntent_AttackRejectsNonPositiveTargetIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RawAttackIntent(10, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RawAttackIntent(10, 5, -1));
        }

        [Test]
        [Category("Core")]
        public void AttackInputNormalizer_PreservesExplicitEntityCommandKinds()
        {
            var normalizedInputs = new List<AttackIntent>();

            new AttackInputNormalizer().Normalize(
                new[]
                {
                    RawAttackIntent.CreateFireProjectile(10, 5),
                    new RawAttackIntent(20, 1, 30),
                },
                Array.Empty<ImpactReservation>(),
                normalizedInputs);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Command: AttackCommandKind.FireProjectile, TargetId: 0),
                    (SourceId: 20, Command: AttackCommandKind.Attack, TargetId: 30),
                },
                normalizedInputs
                    .Select(intent => (intent.SourceId, intent.CommandKind, intent.TargetId))
                    .ToArray());
        }
    }
}
