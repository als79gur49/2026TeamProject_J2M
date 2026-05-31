using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class UnitKinematicStateTests
    {
        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryGetUnitKinematicPose_AbsentStateSynthesizesSettledZero()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitKinematicPose(10, out var pose), Is.True);
            Assert.That(pose.AnchorCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(pose.HasAuthoritativeState, Is.False);
            Assert.That(pose.LocalOffset.IsZero, Is.True);
            Assert.That(pose.Mode, Is.EqualTo(MotionMode.Settled));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_SetUnitKinematicState_PersistsNonDefaultAndLegacyMoveClearsIt()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var writeContext = worldState.CreateWriteContext();

            writeContext.SetUnitKinematicState(10, CreateOffsetState(localX: 1024, localY: 0));

            var movingSnapshot = worldState.CreateSnapshot();
            Assert.That(movingSnapshot.TryGetUnitKinematicState(10, out var storedState), Is.True);
            Assert.That(storedState.localOffset.X.RawValue, Is.EqualTo(1024));

            writeContext.MoveEntity(10, new SurfaceCell(FaceId.Floor, 1, 0));

            var movedSnapshot = worldState.CreateSnapshot();
            Assert.That(movedSnapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(movedSnapshot.TryGetUnitKinematicPose(10, out var pose), Is.True);
            Assert.That(pose.HasAuthoritativeState, Is.False);
            Assert.That(pose.AnchorCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void FinalizationBatch_SetUnitKinematicState_AppliesOnlyThroughWorldWriteContext()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var batch = new FinalizationBatch();
            batch.SetUnitKinematicState(10, CreateOffsetState(localX: 0, localY: -512));

            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetUnitKinematicPose(10, out var pose), Is.True);
            Assert.That(pose.HasAuthoritativeState, Is.True);
            Assert.That(pose.LocalOffset.Y.RawValue, Is.EqualTo(-512));
        }

        [Test]
        [Category("Extended")]
        public void DeterminismHashBuilder_UnitKinematicsAffectHashAndSettledZeroIsOmitted()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var baselineSnapshot = worldState.CreateSnapshot();
            var baselineHash = BuildHash(baselineSnapshot);

            worldState.CreateWriteContext().SetUnitKinematicState(10, CreateOffsetState(localX: 1024, localY: 0));
            var movingSnapshot = worldState.CreateSnapshot();
            var movingHash = BuildHash(movingSnapshot);

            worldState.CreateWriteContext().SetUnitKinematicState(10, UnitKinematicRuntimeState.SettledZero);
            var settledSnapshot = worldState.CreateSnapshot();
            var settledHash = BuildHash(settledSnapshot);

            Assert.That(movingHash, Is.Not.EqualTo(baselineHash));
            Assert.That(settledSnapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(settledHash, Is.EqualTo(baselineHash));
        }

        [Test]
        [Category("Extended")]
        public void CombatAction_LocomotionLease_NormalComplete_Releases()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var lease = CreateCombatLease(10, sequence: 7, EntityLocomotionLeaseStateKind.HeldByOwner);
            worldState.CreateWriteContext().SetEntityLocomotionLeaseState(10, lease);

            lease.stateKind = EntityLocomotionLeaseStateKind.Completed;
            lease.lastReleaseTick = 11;
            lease.lastReleaseReason = EntityLocomotionLeaseReleaseReason.NormalComplete;
            worldState.CreateWriteContext().SetEntityLocomotionLeaseState(10, lease);

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out var released), Is.True);
            Assert.That(released.stateKind, Is.EqualTo(EntityLocomotionLeaseStateKind.Completed));
            Assert.That(released.lastReleaseReason, Is.EqualTo(EntityLocomotionLeaseReleaseReason.NormalComplete));
            Assert.That(released.lastReleaseTick, Is.EqualTo(11));
        }

        [Test]
        [Category("Extended")]
        public void CombatAction_LocomotionLease_DoubleRelease_IsIdempotent()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var released = CreateCombatLease(10, sequence: 7, EntityLocomotionLeaseStateKind.Completed);
            released.lastReleaseTick = 11;
            released.lastReleaseReason = EntityLocomotionLeaseReleaseReason.NormalComplete;
            var batch = new FinalizationBatch();

            batch.SetEntityLocomotionLeaseState(10, released);
            batch.SetEntityLocomotionLeaseState(10, released);
            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(released.NormalizedForStorage()));
        }

        [Test]
        [Category("Extended")]
        public void KinematicNotSettled_WithoutLeaseOrSettle_IsDefect()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            worldState.CreateWriteContext().SetUnitKinematicState(
                10,
                UnitKinematicRuntimeState.CreateHeldFreeze(CreateOffsetState(localX: 1024, localY: 0)));

            var diagnostic = EntityLocomotionLeaseDiagnostics.BuildKinematicNotSettledDiagnostic(
                worldState.CreateSnapshot(),
                tickIndex: 3,
                entityId: 10,
                stage: "UnitTest",
                intentId: 99);

            Assert.That(diagnostic, Does.Contain("Operation=OrphanedKinematicNotSettled"));
            Assert.That(diagnostic, Does.Contain("OwnerKind=None"));
            Assert.That(diagnostic, Does.Contain("Policy=UnitTest"));
        }

        [Test]
        [Category("Extended")]
        public void Jump_UsesExistingSeparateContract_NotCombatLease()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });

            worldState.CreateWriteContext().SetEnemyJumpState(
                10,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 2,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 1, 0),
                    windupEndTick = 4,
                    landingTick = 7,
                });

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Charge_UsesExistingSeparateContract_NotCombatLease()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });

            worldState.CreateWriteContext().SetEnemyChargeState(
                10,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 3,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void DrSaturn_Utility_DoesNotAcquireCombatLease()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });

            worldState.CreateWriteContext().SetEnemyUtilityState(
                10,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            phase = EnemyUtilityEffectPhase.Windup,
                            windupStartTick = 1,
                            windupEndTick = 4,
                            activationSequence = 8,
                        },
                    }));

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void UnitSpatialQuery_TryResolveSettledProbeCell_RejectsNonSettledPose()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            worldState.CreateWriteContext().SetUnitKinematicState(10, CreateOffsetState(localX: 1024, localY: 0));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(
                UnitSpatialQuery.TryResolveSettledProbeCell(snapshot, 10, Direction.Right, out var result),
                Is.False);
            Assert.That(result.RejectedBy, Is.EqualTo(UnitProbeRejectionReason.NotSettledAtAnchor));
        }

        [Test]
        [Category("Extended")]
        public void CreateInterruptedFreeze_PreservesOffsetAndZerosMotion()
        {
            var sourceState = CreateOffsetState(localX: 1024, localY: 0);

            var interrupted = UnitKinematicRuntimeState.CreateInterruptedFreeze(sourceState);

            Assert.That(interrupted.localOffset.X.RawValue, Is.EqualTo(1024));
            Assert.That(interrupted.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(interrupted.velocity.IsZero, Is.True);
            Assert.That(interrupted.mode, Is.EqualTo(MotionMode.Interrupted));
            Assert.That(interrupted.forcedOp, Is.EqualTo(ForcedMotionOp.None));
            Assert.That(interrupted.remainingDistanceUnits, Is.EqualTo(0));
            Assert.That(interrupted.remainingTicks, Is.EqualTo(0));
            Assert.That(interrupted.speedScalePermille, Is.EqualTo(0));
            Assert.That(interrupted.sequenceId, Is.EqualTo(sourceState.sequenceId + 1));
            Assert.That(interrupted.elapsedTicks, Is.EqualTo(0));
            Assert.That(interrupted.totalTicks, Is.EqualTo(0));
            Assert.That(interrupted.commitTick, Is.EqualTo(0));
            Assert.That(interrupted.startedTick, Is.EqualTo(0));
            Assert.That(interrupted.stepDirectionX, Is.EqualTo(0));
            Assert.That(interrupted.stepDirectionY, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void MotionModeHeld_NormalizedForStorage_PreservesProgressAndZerosVelocity()
        {
            var sourceState = CreateOffsetState(localX: 1024, localY: 0);

            var held = UnitKinematicRuntimeState.CreateHeldFreeze(sourceState);

            Assert.That(held.mode, Is.EqualTo(MotionMode.Held));
            Assert.That(held.localOffset.X.RawValue, Is.EqualTo(1024));
            Assert.That(held.velocity.IsZero, Is.True);
            Assert.That(held.remainingDistanceUnits, Is.EqualTo(sourceState.remainingDistanceUnits));
            Assert.That(held.remainingTicks, Is.EqualTo(sourceState.remainingTicks));
            Assert.That(held.speedScalePermille, Is.EqualTo(0));
            Assert.That(held.elapsedTicks, Is.EqualTo(sourceState.elapsedTicks));
            Assert.That(held.totalTicks, Is.EqualTo(sourceState.totalTicks));
            Assert.That(held.commitTick, Is.EqualTo(sourceState.commitTick));
            Assert.That(held.startedTick, Is.EqualTo(sourceState.startedTick));
            Assert.That(held.stepDirectionX, Is.EqualTo(sourceState.stepDirectionX));
            Assert.That(held.stepDirectionY, Is.EqualTo(sourceState.stepDirectionY));
            Assert.That(held.sequenceId, Is.EqualTo(sourceState.sequenceId + 1));
        }

        [Test]
        [Category("Extended")]
        public void NormalizedForStorage_ChargeMode_PreservesStepProgress()
        {
            var state = new UnitKinematicRuntimeState
            {
                localOffset = new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.Zero),
                velocity = new KinematicVelocity2(KinematicFixed.FromRaw(128), KinematicFixed.Zero),
                mode = MotionMode.Charge,
                remainingDistanceUnits = 3584,
                remainingTicks = 3,
                speedScalePermille = 1000,
                sequenceId = 7,
                elapsedTicks = 1,
                totalTicks = 4,
                commitTick = 2,
                startedTick = 11,
                stepDirectionX = 1,
            }.NormalizedForStorage();

            Assert.That(state.mode, Is.EqualTo(MotionMode.Charge));
            Assert.That(state.elapsedTicks, Is.EqualTo(1));
            Assert.That(state.totalTicks, Is.EqualTo(4));
            Assert.That(state.commitTick, Is.EqualTo(2));
            Assert.That(state.startedTick, Is.EqualTo(11));
            Assert.That(state.stepDirectionX, Is.EqualTo(1));
            Assert.That(state.remainingDistanceUnits, Is.EqualTo(3584));
        }

        [TestCase(30, 10)]
        [TestCase(60, 20)]
        [TestCase(120, 40)]
        [Category("Extended")]
        public void PlayerKinematicLocomotionTimingSettings_DefaultDuration_QuantizesToEvenTicks(
            int simulationTicksPerSecond,
            int expectedTicksPerCell)
        {
            var snapshot = PlayerKinematicLocomotionTimingSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(simulationTicksPerSecond);

            Assert.That(snapshot.TicksPerCell, Is.EqualTo(expectedTicksPerCell));
            Assert.That(snapshot.CommitTick, Is.EqualTo(expectedTicksPerCell / 2));
        }

        [TestCase(30, 12)]
        [TestCase(60, 22)]
        [TestCase(120, 42)]
        [Category("Extended")]
        public void PlayerKinematicLocomotionTimingSettings_ExplicitPointThirtyFive_UsesEvenCeil(
            int simulationTicksPerSecond,
            int expectedTicksPerCell)
        {
            var snapshot = new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 0.35f,
            }.CreateAuthoritativeSnapshot(simulationTicksPerSecond);

            Assert.That(snapshot.TicksPerCell, Is.EqualTo(expectedTicksPerCell));
            Assert.That(snapshot.CommitTick, Is.EqualTo(expectedTicksPerCell / 2));
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(2.01f)]
        [Category("Extended")]
        public void PlayerKinematicLocomotionTimingSettings_InvalidDuration_Throws(float durationSeconds)
        {
            var settings = new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = durationSeconds,
            };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [Test]
        [Category("Extended")]
        public void KinematicProgressResolver_TwentyTickRightMove_UsesProgressTableAndSettlesZero()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);

            var tickOne = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 1, totalTicks: 20);
            var tickNine = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 9, totalTicks: 20);
            var tickTen = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 10, totalTicks: 20);
            var tickNineteen = KinematicProgressResolver.ResolvePose(tickTen.AnchorCell, 1, 0, elapsedTicks: 19, totalTicks: 20);
            var tickTwenty = KinematicProgressResolver.ResolvePose(tickTen.AnchorCell, 1, 0, elapsedTicks: 20, totalTicks: 20);

            Assert.That(tickOne.AnchorCell, Is.EqualTo(source));
            Assert.That(tickOne.LocalOffset.X.RawValue, Is.EqualTo(205));
            Assert.That(tickNine.LocalOffset.X.RawValue, Is.EqualTo(1843));
            Assert.That(tickTen.AnchorCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(tickTen.LocalOffset.X.RawValue, Is.EqualTo(-2048));
            Assert.That(tickTen.IsAnchorCommitTick, Is.True);
            Assert.That(tickNineteen.LocalOffset.X.RawValue, Is.EqualTo(-205));
            Assert.That(tickTwenty.IsSettled, Is.True);
            Assert.That(tickTwenty.LocalOffset.IsZero, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void KinematicProgressResolver_TwentyTwoTickRightMove_UsesProgressTable()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);

            var tickOne = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 1, totalTicks: 22);
            var tickTen = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 10, totalTicks: 22);
            var tickEleven = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 11, totalTicks: 22);
            var tickTwentyOne = KinematicProgressResolver.ResolvePose(tickEleven.AnchorCell, 1, 0, elapsedTicks: 21, totalTicks: 22);

            Assert.That(tickOne.LocalOffset.X.RawValue, Is.EqualTo(186));
            Assert.That(tickTen.LocalOffset.X.RawValue, Is.EqualTo(1862));
            Assert.That(tickEleven.AnchorCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(tickEleven.LocalOffset.X.RawValue, Is.EqualTo(-2048));
            Assert.That(tickTwentyOne.LocalOffset.X.RawValue, Is.EqualTo(-186));
        }

        [Test]
        [Category("Extended")]
        public void KinematicProgressResolver_NegativeDirectionCommitOffset_IsRepresentable()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);

            var commit = KinematicProgressResolver.ResolvePose(source, -1, 0, elapsedTicks: 10, totalTicks: 20);

            Assert.That(commit.AnchorCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(commit.LocalOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(commit.LocalOffset.IsRepresentableLocalOffset, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void KinematicProgressResolver_ReverseFromHeld_BeforeCommit_MirrorsProgressAndPreservesPose()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var oldPose = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 1, totalTicks: 20);
            var held = CreateHeldStepState(oldPose.LocalOffset, elapsedTicks: 1, stepX: 1, stepY: 0);

            var resolved = KinematicProgressResolver.TryResolveReverseFromHeld(
                source,
                held,
                reverseStepDirectionX: -1,
                reverseStepDirectionY: 0,
                out var mirroredAnchor,
                out var mirroredState,
                out var poseDelta);

            Assert.That(resolved, Is.True);
            Assert.That(poseDelta, Is.EqualTo(0));
            Assert.That(mirroredAnchor, Is.EqualTo(source));
            Assert.That(mirroredState.mode, Is.EqualTo(MotionMode.Voluntary));
            Assert.That(mirroredState.elapsedTicks, Is.EqualTo(19));
            Assert.That(mirroredState.stepDirectionX, Is.EqualTo(-1));
            Assert.That(mirroredState.localOffset, Is.EqualTo(held.localOffset));
        }

        [Test]
        [Category("Extended")]
        public void KinematicProgressResolver_ReverseFromHeld_AfterCommit_MirrorsProgressAndPreservesPose()
        {
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var oldPose = KinematicProgressResolver.ResolvePose(destination, 1, 0, elapsedTicks: 11, totalTicks: 20);
            var held = CreateHeldStepState(oldPose.LocalOffset, elapsedTicks: 11, stepX: 1, stepY: 0);

            var resolved = KinematicProgressResolver.TryResolveReverseFromHeld(
                destination,
                held,
                reverseStepDirectionX: -1,
                reverseStepDirectionY: 0,
                out var mirroredAnchor,
                out var mirroredState,
                out var poseDelta);

            Assert.That(resolved, Is.True);
            Assert.That(poseDelta, Is.EqualTo(0));
            Assert.That(mirroredAnchor, Is.EqualTo(destination));
            Assert.That(mirroredState.elapsedTicks, Is.EqualTo(9));
            Assert.That(mirroredState.stepDirectionX, Is.EqualTo(-1));
            Assert.That(mirroredState.localOffset, Is.EqualTo(held.localOffset));
        }

        [Test]
        [Category("Extended")]
        public void KinematicProgressResolver_ReverseFromHeld_AtCommit_UsesRepresentableMirrorTolerance()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var oldPose = KinematicProgressResolver.ResolvePose(source, 1, 0, elapsedTicks: 10, totalTicks: 20);
            var held = CreateHeldStepState(oldPose.LocalOffset, elapsedTicks: 10, stepX: 1, stepY: 0);

            var resolved = KinematicProgressResolver.TryResolveReverseFromHeld(
                destination,
                held,
                reverseStepDirectionX: -1,
                reverseStepDirectionY: 0,
                out var mirroredAnchor,
                out var mirroredState,
                out var poseDelta);

            Assert.That(resolved, Is.True);
            Assert.That(poseDelta, Is.LessThanOrEqualTo(1));
            Assert.That(mirroredAnchor, Is.EqualTo(source));
            Assert.That(mirroredState.elapsedTicks, Is.EqualTo(10));
            Assert.That(mirroredState.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(mirroredState.stepDirectionX, Is.EqualTo(-1));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_RemoveEntity_PurgesUnitKinematicState()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(10, CreateOffsetState(localX: 1024, localY: 0));
            writeContext.SetEntityLocomotionLeaseState(10, CreateCombatLease(10, sequence: 7, EntityLocomotionLeaseStateKind.HeldByOwner));

            writeContext.RemoveEntity(10);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(snapshot.TryGetUnitKinematicPose(10, out _), Is.False);
            Assert.That(snapshot.TryGetEntityLocomotionLeaseState(10, out _), Is.False);
        }

        private static string BuildHash(WorldSnapshot snapshot)
        {
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);
            return new DeterminismHashBuilder().Build(
                tickIndex: 7,
                snapshot,
                new TickResultData(
                    finalEntities,
                    Array.Empty<DelayedAttackEffectRecord>(),
                    Array.Empty<string>()));
        }

        private static UnitKinematicRuntimeState CreateOffsetState(int localX, int localY)
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = new KinematicOffset2(KinematicFixed.FromRaw(localX), KinematicFixed.FromRaw(localY)),
                velocity = new KinematicVelocity2(KinematicFixed.FromRaw(localX == 0 ? 0 : 1024), KinematicFixed.Zero),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = 4096,
                remainingTicks = 3,
                speedScalePermille = 1000,
                sequenceId = 1,
                elapsedTicks = 1,
                totalTicks = 20,
                commitTick = 10,
                startedTick = 7,
                stepDirectionX = localX == 0 ? 0 : 1,
                stepDirectionY = localY == 0 ? 0 : 1,
            };
        }

        private static UnitKinematicRuntimeState CreateHeldStepState(
            KinematicOffset2 localOffset,
            int elapsedTicks,
            int stepX,
            int stepY)
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = localOffset,
                velocity = KinematicVelocity2.Zero,
                mode = MotionMode.Held,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = KinematicFixed.UnitsPerCell,
                remainingTicks = 20 - elapsedTicks,
                speedScalePermille = 0,
                sequenceId = 3,
                elapsedTicks = elapsedTicks,
                totalTicks = 20,
                commitTick = 10,
                startedTick = 1,
                stepDirectionX = stepX,
                stepDirectionY = stepY,
            }.NormalizedForStorage();
        }

        private static EntityLocomotionLeaseState CreateCombatLease(
            int entityId,
            int sequence,
            EntityLocomotionLeaseStateKind stateKind)
        {
            return new EntityLocomotionLeaseState
            {
                leaseId = entityId * 100000 + sequence,
                entityId = entityId,
                ownerKind = EntityLocomotionLeaseOwnerKind.CombatAction,
                stateKind = stateKind,
                ownerActionSequenceId = sequence,
                capturedKinematic = CreateOffsetState(localX: 1024, localY: 0),
                anchorAtAcquire = new SurfaceCell(FaceId.Floor, 0, 0),
                acquiredTick = 2,
                lastReleaseTick = stateKind == EntityLocomotionLeaseStateKind.Completed ? 3 : 0,
                lastReleaseReason = EntityLocomotionLeaseReleaseReason.NormalComplete,
            };
        }

        private static EntityState CreateUnit(int entityId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
