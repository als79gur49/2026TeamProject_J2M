using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyTargetSelectorContractTests
    {
        private static readonly DetectionSettings DetectionSettings =
            new DetectionSettings(senseRange: 8, requireSameFace: true, canTargetMarkedForDeath: false);

        [TestCase(EntityType.Box)]
        [TestCase(EntityType.Wall)]
        [Category("Extended")]
        public void CrossLineOfSight_LowerIdNonUnit_DoesNotOwnBlockedUnitRejection(EntityType blockerType)
        {
            var worldState = CreateWorld(
                CreateNonUnit(1, blockerType, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 2, 0)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                CrossLineOfSightOpponentDetectionStrategy.Instance,
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 10, EnemyTargetEligibilityRejectReason.BlockedByProfileRule);
        }

        [TestCase(EntityType.Box)]
        [TestCase(EntityType.Wall)]
        [Category("Extended")]
        public void Nearest_LowerIdNonUnit_DoesNotOwnDetachedUnitRejection(EntityType nonUnitType)
        {
            var detached = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 2, 0));
            detached.boardPresence = EntityBoardPresence.Detached;
            var worldState = CreateWorld(
                CreateNonUnit(1, nonUnitType, new SurfaceCell(FaceId.Floor, 4, 4)),
                detached,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                NearestOpponentDetectionStrategy.Instance,
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 10, EnemyTargetEligibilityRejectReason.TargetNotOccupying);
        }

        [TestCase(false, EntityType.Box)]
        [TestCase(false, EntityType.Wall)]
        [TestCase(true, EntityType.Box)]
        [TestCase(true, EntityType.Wall)]
        [Category("Extended")]
        public void BuiltInStrategy_NonUnitAfterSelectedUnit_DoesNotOverwriteAcceptedResult(
            bool crossLineOfSight,
            EntityType nonUnitType)
        {
            var worldState = CreateWorld(
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateNonUnit(50, nonUnitType, new SurfaceCell(FaceId.Floor, 4, 4)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                Strategy(crossLineOfSight),
                out var target,
                out var result);

            TestContext.WriteLine(
                $"FreshAcquireOracle|Strategy={(crossLineOfSight ? "CrossLos" : "Nearest")}" +
                $"|Case=SuccessWithTrailing{nonUnitType}|Found={(found ? 1 : 0)}" +
                $"|Target={target.entityId}|Eligible={(result.Eligible ? 1 : 0)}" +
                $"|Purpose={result.Purpose}|Accept={result.AcceptReason}|Reject={result.RejectReason}" +
                $"|ResultTarget={result.TargetEntityId}");
            AssertAccepted(found, target, result, expectedTargetEntityId: 10);
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Extended")]
        public void BuiltInStrategy_RejectedUnitAfterSelectedUnit_DoesNotOverwriteAcceptedResult(bool crossLineOfSight)
        {
            var worldState = CreateWorld(
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(20, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 2)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                Strategy(crossLineOfSight),
                out var target,
                out var result);

            AssertAccepted(found, target, result, expectedTargetEntityId: 10);
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Extended")]
        public void BuiltInStrategy_EqualDistanceUnits_SelectsLowerEntityId(bool crossLineOfSight)
        {
            var worldState = CreateWorld(
                CreateUnit(20, teamId: 1, new SurfaceCell(FaceId.Floor, 0, 2)),
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 2, 0)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                Strategy(crossLineOfSight),
                out var target,
                out var result);

            AssertAccepted(found, target, result, expectedTargetEntityId: 10);
        }

        [TestCase(false, "Detached", "TargetNotOccupying")]
        [TestCase(false, "Dead", "TargetDead")]
        [TestCase(false, "Marked", "TargetMarkedForDeath")]
        [TestCase(false, "Phased", "FreshSelectionSuppressedBySpatialState")]
        [TestCase(true, "Detached", "TargetNotOccupying")]
        [TestCase(true, "Dead", "TargetDead")]
        [TestCase(true, "Marked", "TargetMarkedForDeath")]
        [TestCase(true, "Phased", "FreshSelectionSuppressedBySpatialState")]
        [Category("Extended")]
        public void BuiltInStrategy_IneligibleUnit_PreservesCandidateSpecificMeaning(
            bool crossLineOfSight,
            string state,
            string expectedReason)
        {
            var candidate = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0));
            if (state == "Detached")
            {
                candidate.boardPresence = EntityBoardPresence.Detached;
            }
            else if (state == "Dead")
            {
                candidate.hp = 0;
            }
            else if (state == "Marked")
            {
                candidate.markedForDeath = true;
            }

            var worldState = CreateWorld(
                candidate,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));
            if (state == "Phased")
            {
                worldState.CreateWriteContext().SetPhasedState(
                    10,
                    PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            }

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                Strategy(crossLineOfSight),
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(
                result,
                targetEntityId: 10,
                (EnemyTargetEligibilityRejectReason)System.Enum.Parse(
                    typeof(EnemyTargetEligibilityRejectReason),
                    expectedReason));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Extended")]
        public void BuiltInStrategy_PhasedRejection_PreservesPriorityOverEarlierUnitRejection(bool crossLineOfSight)
        {
            var detached = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0));
            detached.boardPresence = EntityBoardPresence.Detached;
            var worldState = CreateWorld(
                detached,
                CreateUnit(20, teamId: 1, new SurfaceCell(FaceId.Floor, 0, 2)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));
            worldState.CreateWriteContext().SetPhasedState(
                20,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                Strategy(crossLineOfSight),
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(
                result,
                targetEntityId: 20,
                EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState);
        }

        [Test]
        [Category("Extended")]
        public void CentralSelector_CustomStrategyReturningNonUnit_RejectsSuccessAsTargetMissing()
        {
            var nonUnit = CreateNonUnit(1, EntityType.Box, new SurfaceCell(FaceId.Floor, 1, 0));
            var worldState = CreateWorld(
                nonUnit,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                new FixedSuccessDetectionStrategy(nonUnit),
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 0, EnemyTargetEligibilityRejectReason.TargetMissing);
        }

        [Test]
        [Category("Extended")]
        public void CentralSelector_CustomStrategyReturningIneligibleUnit_RejectsSuccessWithUnitReason()
        {
            var detached = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0));
            detached.boardPresence = EntityBoardPresence.Detached;
            var worldState = CreateWorld(
                detached,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                new FixedSuccessDetectionStrategy(detached),
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 10, EnemyTargetEligibilityRejectReason.TargetNotOccupying);
        }

        [Test]
        [Category("Extended")]
        public void CentralSelector_CustomStrategyForgingUnitForSnapshotBox_RejectsAsTargetMissing()
        {
            var forgedUnit = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0));
            var worldState = CreateWorld(
                CreateNonUnit(10, EntityType.Box, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                new FixedSuccessDetectionStrategy(forgedUnit),
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 0, EnemyTargetEligibilityRejectReason.TargetMissing);
        }

        [Test]
        [Category("Extended")]
        public void CentralSelector_CustomStrategyForgingAliveState_UsesCanonicalDetachedUnitRejection()
        {
            var forgedAlive = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0));
            var canonicalDetached = forgedAlive;
            canonicalDetached.boardPresence = EntityBoardPresence.Detached;
            var worldState = CreateWorld(
                canonicalDetached,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                new FixedSuccessDetectionStrategy(forgedAlive),
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 10, EnemyTargetEligibilityRejectReason.TargetNotOccupying);
        }

        [Test]
        [Category("Extended")]
        public void CentralSelector_CustomStrategyReturningEligibleUnit_NormalizesAcceptedResult()
        {
            var candidate = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0));
            var worldState = CreateWorld(
                candidate,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                new FixedSuccessDetectionStrategy(candidate),
                out var target,
                out var result);

            AssertAccepted(found, target, result, expectedTargetEntityId: 10);
        }

        [Test]
        [Category("Extended")]
        public void CentralSelector_CustomStrategyReturningFalse_NormalizesTargetMissingAndClearsTarget()
        {
            var returnedTarget = CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0));
            var worldState = CreateWorld(
                returnedTarget,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                new FixedFailureDetectionStrategy(returnedTarget),
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 0, EnemyTargetEligibilityRejectReason.TargetMissing);
        }

        [Test]
        [Category("Extended")]
        public void CentralSelector_NoDetectionStrategy_RemainsTargetMissing()
        {
            var worldState = CreateWorld(
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var found = Acquire(
                worldState,
                sourceEntityId: 40,
                NoDetectionStrategy.Instance,
                out var target,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
            AssertRejected(result, targetEntityId: 0, EnemyTargetEligibilityRejectReason.TargetMissing);
        }

        [Test]
        [Category("Extended")]
        public void BuiltInSelector_NonUnitAddRemoveRoundTrip_PreservesSelectedTargetAndDetailedResult()
        {
            var worldState = CreateWorld(
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));

            var beforeFound = Acquire(
                worldState,
                sourceEntityId: 40,
                NearestOpponentDetectionStrategy.Instance,
                out var beforeTarget,
                out var beforeResult);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SpawnEntity(CreateNonUnit(1, EntityType.Box, new SurfaceCell(FaceId.Floor, 4, 4)));
            writeContext.RemoveEntity(1);
            var afterFound = Acquire(
                worldState,
                sourceEntityId: 40,
                NearestOpponentDetectionStrategy.Instance,
                out var afterTarget,
                out var afterResult);

            AssertAccepted(beforeFound, beforeTarget, beforeResult, expectedTargetEntityId: 10);
            AssertAccepted(afterFound, afterTarget, afterResult, expectedTargetEntityId: 10);
            Assert.That(afterResult.Eligible, Is.EqualTo(beforeResult.Eligible));
            Assert.That(afterResult.TargetEntityId, Is.EqualTo(beforeResult.TargetEntityId));
            Assert.That(afterResult.AcceptReason, Is.EqualTo(beforeResult.AcceptReason));
            Assert.That(afterResult.RejectReason, Is.EqualTo(beforeResult.RejectReason));
        }

        [Test]
        [Category("Extended")]
        public void EligibilityPolicy_DirectNonUnitEvaluation_RemainsTargetMissing()
        {
            var nonUnit = CreateNonUnit(1, EntityType.Wall, new SurfaceCell(FaceId.Floor, 1, 0));
            var worldState = CreateWorld(
                nonUnit,
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0)));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(40, out var source), Is.True);

            var result = EnemyTargetEligibilityPolicy.EvaluateFreshAcquire(
                snapshot,
                source,
                nonUnit,
                DetectionSettings);

            AssertRejected(result, targetEntityId: 1, EnemyTargetEligibilityRejectReason.TargetMissing);
        }

        private static IDetectionStrategy Strategy(bool crossLineOfSight)
        {
            return crossLineOfSight
                ? CrossLineOfSightOpponentDetectionStrategy.Instance
                : NearestOpponentDetectionStrategy.Instance;
        }

        private static bool Acquire(
            WorldState worldState,
            int sourceEntityId,
            IDetectionStrategy strategy,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(sourceEntityId, out var source), Is.True);
            return EnemyTargetSelector.TryAcquireFreshTarget(
                snapshot,
                source,
                strategy,
                DetectionSettings,
                out target,
                out result);
        }

        private static void AssertAccepted(
            bool found,
            in EntityState target,
            in EnemyTargetEligibilityResult result,
            int expectedTargetEntityId)
        {
            Assert.That(found, Is.True);
            Assert.That(target.entityId, Is.EqualTo(expectedTargetEntityId));
            Assert.That(result.Eligible, Is.True);
            Assert.That(result.SourceEntityId, Is.EqualTo(40));
            Assert.That(result.TargetEntityId, Is.EqualTo(expectedTargetEntityId));
            Assert.That(result.Purpose, Is.EqualTo(EnemyTargetEligibilityPurpose.FreshAcquire));
            Assert.That(result.AcceptReason, Is.EqualTo(EnemyTargetEligibilityAcceptReason.FreshAcquired));
            Assert.That(result.RejectReason, Is.EqualTo(EnemyTargetEligibilityRejectReason.None));
        }

        private static void AssertRejected(
            in EnemyTargetEligibilityResult result,
            int targetEntityId,
            EnemyTargetEligibilityRejectReason reason)
        {
            Assert.That(result.Eligible, Is.False);
            Assert.That(result.SourceEntityId, Is.EqualTo(40));
            Assert.That(result.TargetEntityId, Is.EqualTo(targetEntityId));
            Assert.That(result.Purpose, Is.EqualTo(EnemyTargetEligibilityPurpose.FreshAcquire));
            Assert.That(result.AcceptReason, Is.EqualTo(EnemyTargetEligibilityAcceptReason.None));
            Assert.That(result.RejectReason, Is.EqualTo(reason));
        }

        private static WorldState CreateWorld(params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(4, 4)));
        }

        private static EntityState CreateUnit(int entityId, int teamId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = teamId == 1 ? UnitRole.Player : UnitRole.Enemy,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateNonUnit(int entityId, EntityType type, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = type,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = type == EntityType.Box ? BoxCapabilities.Push : BoxCapabilities.None,
            };
        }

        private sealed class FixedSuccessDetectionStrategy : IDetectionStrategy
        {
            private readonly EntityState _target;

            public FixedSuccessDetectionStrategy(EntityState target)
            {
                _target = target;
            }

            public bool TryFindTarget(
                WorldSnapshot snapshot,
                in EntityState source,
                in DetectionSettings settings,
                out EntityState target,
                EnemyDetectionQueryOptions options = default)
            {
                target = _target;
                return true;
            }
        }

        private sealed class FixedFailureDetectionStrategy : IDetectionStrategy
        {
            private readonly EntityState _target;

            public FixedFailureDetectionStrategy(EntityState target)
            {
                _target = target;
            }

            public bool TryFindTarget(
                WorldSnapshot snapshot,
                in EntityState source,
                in DetectionSettings settings,
                out EntityState target,
                EnemyDetectionQueryOptions options = default)
            {
                target = _target;
                return false;
            }
        }
    }
}
