using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class AttackInputNormalizationTests
    {
        [Test]
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
        public void RawAttackIntent_AttackRejectsNonPositiveTargetIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RawAttackIntent(10, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RawAttackIntent(10, 5, -1));
        }

        [Test]
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

        [Test]
        public void AttackExpander_SyntheticReservations_ExpandToDamageAndProjectileDestroyCandidates()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
                CreateProjectile(entityId: 30, teamId: 1, position: new Vector2Int(2, 0), hp: 1),
            });
            var snapshot = SnapshotBuilder.Create(worldState);
            var normalizedInputs = new List<AttackIntent>();
            var allocator = new IdAllocator();

            new AttackInputNormalizer().Normalize(
                new[] { new RawAttackIntent(10, 5, 20) },
                new[]
                {
                    new ImpactReservation(30, 20, new Vector2Int(1, 0), 1, 5, 2, 1),
                },
                normalizedInputs);

            allocator.ResetForTick(5);
            for (var i = 0; i < normalizedInputs.Count; i++)
            {
                normalizedInputs[i].AssignIntentId(allocator.AllocateIntentId());
            }

            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            new AttackExpander().Expand(snapshot, normalizedInputs, expandedCandidates, rejectedReasons);

            CollectionAssert.AreEqual(
                new[]
                {
                    (IntentId: 1, SourceId: 10, FirstDamageTargetId: 20, DamageCount: 1),
                    (IntentId: 2, SourceId: 30, FirstDamageTargetId: 20, DamageCount: 2),
                },
                expandedCandidates
                    .Select(group => (group.IntentId, group.SourceId, FirstDamageTargetId: group.Damages[0].TargetId, DamageCount: group.Damages.Count))
                    .ToArray());
            Assert.That(rejectedReasons, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (IntentId: 2, TargetId: 20, Amount: 1),
                    (IntentId: 2, TargetId: 30, Amount: 1),
                },
                expandedCandidates
                    .Single(group => group.IntentId == 2)
                    .Damages
                    .Select(damage => (IntentId: 2, damage.TargetId, damage.Amount))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 20, 30 },
                expandedCandidates
                    .Single(group => group.IntentId == 2)
                    .Destroys
                    .Select(destroy => destroy.TargetId)
                    .ToArray());
        }

        [Test]
        public void TickTraceBuilder_EmitsDrainedReservations_AndNormalizedSyntheticInputs()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
                CreateProjectile(entityId: 30, teamId: 1, position: new Vector2Int(2, 0), hp: 1),
            });
            var snapshot = SnapshotBuilder.Create(worldState);
            var reservation = new ImpactReservation(30, 20, new Vector2Int(1, 0), 1, 7, 2, 1);
            var normalizedInputs = new List<AttackIntent>();
            var finalEntities = new List<EntityState>();

            new AttackInputNormalizer().Normalize(
                Array.Empty<RawAttackIntent>(),
                new[] { reservation },
                normalizedInputs);
            normalizedInputs[0].AssignIntentId(1);
            snapshot.EnumerateEntitiesOrdered(finalEntities);

            var attackPhaseResult = new AttackPhaseResult(
                Array.Empty<RawAttackIntent>(),
                new[] { reservation },
                normalizedInputs,
                Array.Empty<ActionGroup>(),
                Array.Empty<ActionGroup>(),
                Array.Empty<string>(),
                Array.Empty<string>());

            var trace = new TickTraceBuilder().Build(
                7,
                snapshot,
                MovementPhaseResult.Empty,
                snapshot,
                attackPhaseResult,
                CleanupPhaseResult.Empty,
                snapshot,
                new TickResultData(finalEntities, Array.Empty<DelayedAttackEffectRecord>(), Array.Empty<string>()),
                "0123456789ABCDEF");

            Assert.That(trace.Text, Does.Contain("Attack.DrainedImpacts"));
            Assert.That(trace.Text, Does.Contain("Attack.NormalizedInputs"));
            Assert.That(trace.Text, Does.Contain("Reservation|Source=30|Target=20|Position=(1,0)|Damage=1|Tick=7|Group=2|Sequence=1"));
            Assert.That(trace.Text, Does.Contain("I=1|Source=30|Priority=0|Target=20|Command=ImpactReservation|Kind=ImpactReservation|LocalSequence=1"));
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
    }
}
