using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
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
        [Category("Extended")]
        public void AttackExpander_SyntheticReservations_ExpandToDamageCandidates()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 1, position: new Vector2Int(2, 0), hp: 1),
            });
            var snapshot = SnapshotBuilder.Create(worldState);
            var normalizedInputs = new List<AttackIntent>();
            var allocator = new IdAllocator();

            new AttackInputNormalizer().Normalize(
                new[] { new RawAttackIntent(10, 5, 20) },
                new[]
                {
                    new ImpactReservation(30, 20, new SurfaceCell(FaceId.Floor, 1, 0), 1, 5, 2, 1),
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
                    (IntentId: 2, SourceId: 30, FirstDamageTargetId: 20, DamageCount: 1),
                },
                expandedCandidates
                    .Select(group => (group.IntentId, group.SourceId, FirstDamageTargetId: group.Damages[0].TargetId, DamageCount: group.Damages.Count))
                    .ToArray());
            Assert.That(rejectedReasons, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (IntentId: 2, TargetId: 20, Amount: 1),
                },
                expandedCandidates
                    .Single(group => group.IntentId == 2)
                    .Damages
                    .Select(damage => (IntentId: 2, damage.TargetId, damage.Amount))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 20 },
                expandedCandidates
                    .Single(group => group.IntentId == 2)
                    .Destroys
                    .Select(destroy => destroy.TargetId)
                    .ToArray());
        }

        [Test]
        [Category("Extended")]
        public void TickTraceBuilder_EmitsDrainedReservations_WithoutNormalizedInputIr()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 30, teamId: 1, position: new Vector2Int(2, 0), hp: 1),
            });
            var snapshot = SnapshotBuilder.Create(worldState);
            var reservation = new ImpactReservation(30, 20, new SurfaceCell(FaceId.Floor, 1, 0), 1, 7, 2, 1);
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);

            var attackPhaseResult = new AttackPhaseResult(
                Array.Empty<RawAttackIntent>(),
                new[] { reservation },
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<DamageResolutionRecord>(),
                Array.Empty<ResolutionRecord>(),
                Array.Empty<FinalizationOperation>(),
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());

            var trace = new TickTraceBuilder().Build(
                7,
                snapshot,
                new EnemyAiPhaseResult(new List<string>(), new List<string>(), new List<string>()),
                new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()),
                new PreMovementStatePhaseResult(new List<string>()),
                MovementPhaseResult.Empty,
                snapshot,
                attackPhaseResult,
                CleanupFixtureFactory.None(),
                snapshot,
                new TickResultData(finalEntities, Array.Empty<DelayedAttackEffectRecord>(), Array.Empty<string>()),
                "0123456789ABCDEF");

            Assert.That(trace.Text, Does.Contain("Attack.DrainedImpacts"));
            Assert.That(trace.Text, Does.Contain("Reservation|Source=30|Target=20|Position=(1,0)|Damage=1|Tick=7"));
        }

        [Test]
        [Category("Extended")]
        public void TickTraceBuilder_FormatsNonFloorReservationsWithFace()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 20, teamId: 2, position: new Vector2Int(1, 0), hp: 3),
            });
            var snapshot = SnapshotBuilder.Create(worldState);
            var reservation = new ImpactReservation(30, 20, new SurfaceCell(FaceId.Front, 1, 0), 1, 7, 2, 1);
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);

            var attackPhaseResult = new AttackPhaseResult(
                Array.Empty<RawAttackIntent>(),
                new[] { reservation },
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<DamageResolutionRecord>(),
                Array.Empty<ResolutionRecord>(),
                Array.Empty<FinalizationOperation>(),
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());

            var trace = new TickTraceBuilder().Build(
                7,
                snapshot,
                new EnemyAiPhaseResult(new List<string>(), new List<string>(), new List<string>()),
                new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()),
                new PreMovementStatePhaseResult(new List<string>()),
                MovementPhaseResult.Empty,
                snapshot,
                attackPhaseResult,
                CleanupFixtureFactory.None(),
                snapshot,
                new TickResultData(finalEntities, Array.Empty<DelayedAttackEffectRecord>(), Array.Empty<string>()),
                "0123456789ABCDEF");

            Assert.That(trace.Text, Does.Contain("Reservation|Source=30|Target=20|Position=Front(1,0)|Damage=1|Tick=7"));
        }

        [Test]
        [Category("Extended")]
        public void ImpactGeometryResolver_SameFaceFrontImpact_ComputesFacingWithoutReject()
        {
            var resolved = ImpactGeometryResolver.TryResolve(
                new SurfaceCell(FaceId.Front, 0, 0),
                new SurfaceCell(FaceId.Front, 0, 1),
                out var geometry,
                out var rejectReason);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(ImpactGeometryRejectReason.None));
            Assert.That(geometry.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(geometry.ImpactCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
            Assert.That(geometry.FollowThroughCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
            Assert.That(geometry.TravelDirection, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void ImpactGeometryResolver_TrueCrossFaceImpact_Rejects()
        {
            var resolved = ImpactGeometryResolver.TryResolve(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Front, 1, 0),
                out _,
                out var rejectReason);

            Assert.That(resolved, Is.False);
            Assert.That(rejectReason, Is.EqualTo(ImpactGeometryRejectReason.CrossFaceUnsupported));
        }

        [Test]
        [Category("Extended")]
        public void ImpactGeometryResolver_BottomFrontSeamImpact_ComputesUpFacing()
        {
            var resolved = ImpactGeometryResolver.TryResolve(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                new SurfaceCell(FaceId.Floor, 0, 1),
                new SurfaceCell(FaceId.Front, 0, 0),
                out var geometry,
                out var rejectReason);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(ImpactGeometryRejectReason.None));
            Assert.That(geometry.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(geometry.ImpactCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(geometry.FollowThroughCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(geometry.TravelDirection, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void ImpactGeometryResolver_FrontBottomSeamImpact_ComputesDownFacing()
        {
            var resolved = ImpactGeometryResolver.TryResolve(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                new SurfaceCell(FaceId.Front, 0, 0),
                new SurfaceCell(FaceId.Floor, 0, 1),
                out var geometry,
                out var rejectReason);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(ImpactGeometryRejectReason.None));
            Assert.That(geometry.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(geometry.ImpactCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(geometry.FollowThroughCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(geometry.TravelDirection, Is.EqualTo(Direction.Down));
        }

        [Test]
        [Category("Extended")]
        public void ImpactReservationComparer_PreservesFaceBeforePlanarOrder()
        {
            var reservations = new List<ImpactReservation>
            {
                new ImpactReservation(10, 20, new SurfaceCell(FaceId.Front, 0, 0), 1, 1, 1, 1),
                new ImpactReservation(10, 20, new SurfaceCell(FaceId.Floor, 1, 0), 1, 1, 2, 1),
                new ImpactReservation(10, 20, new SurfaceCell(FaceId.Floor, 0, 1), 1, 1, 3, 1),
            };

            reservations.Sort(ImpactReservationComparer.Instance);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    new SurfaceCell(FaceId.Front, 0, 0),
                },
                reservations.Select(reservation => reservation.ImpactCell).ToArray());
            Assert.That(
                reservations.Select(reservation => reservation.ImpactCell.face).Distinct().Count(),
                Is.EqualTo(2));
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

    }
}
