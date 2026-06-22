using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class UnitContinuousLocomotionStateTests
    {
        [Test]
        [Category("Core")]
        public void UnitContinuousLocomotionState_IdleZero_OmissionPolicy()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var baselineHash = BuildHash(worldState.CreateSnapshot());
            var idleZeroWithProgressMetadata = new UnitContinuousLocomotionState
            {
                localOffset = SimulationOffset2.Zero,
                velocity = SimulationVelocity2.Zero,
                facing = Direction.Right,
                lastMoveDirection = Direction.Right,
                speedUnitsPerTick = 410,
                mode = ContinuousLocomotionMode.Idle,
                sequenceId = 7,
                subUnitRemainderX = 3,
                subUnitRemainderY = 2,
            }.NormalizedForStorage();

            Assert.That(idleZeroWithProgressMetadata.IsOmittableIdleZero, Is.True);

            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(10, idleZeroWithProgressMetadata);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionPose(10, out var pose), Is.True);
            Assert.That(pose.HasAuthoritativeState, Is.False);
            Assert.That(pose.IsSettledAtAnchor, Is.True);
            Assert.That(BuildHash(snapshot), Is.EqualTo(baselineHash));
        }

        [Test]
        [Category("Extended")]
        public void UnitContinuousLocomotionState_AlignToAnchor_RemainsAuthoritativeUntilLocalZero()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var alignState = new UnitContinuousLocomotionState
            {
                localOffset = new SimulationOffset2(KinematicFixed.FromRaw(1280), KinematicFixed.Zero),
                velocity = new SimulationVelocity2(KinematicFixed.FromRaw(-205), KinematicFixed.Zero),
                facing = Direction.Right,
                lastMoveDirection = Direction.Right,
                speedUnitsPerTick = 205,
                mode = ContinuousLocomotionMode.AlignToAnchor,
                sequenceId = 3,
            }.NormalizedForStorage();

            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(10, alignState);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var stored), Is.True);
            Assert.That(stored.mode, Is.EqualTo(ContinuousLocomotionMode.AlignToAnchor));
            Assert.That(stored.IsSettledAtAnchor, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void UnitContinuousLocomotionState_AlignToAnchor_LocalZeroCanonicalizesToIdleAbsent()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var alignZero = new UnitContinuousLocomotionState
            {
                localOffset = SimulationOffset2.Zero,
                velocity = SimulationVelocity2.Zero,
                facing = Direction.Right,
                lastMoveDirection = Direction.Right,
                speedUnitsPerTick = 205,
                mode = ContinuousLocomotionMode.AlignToAnchor,
                sequenceId = 3,
            }.NormalizedForStorage();

            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(10, alignZero);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(alignZero.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.True);
        }

        [Test]
        [Category("Core")]
        public void WorldState_RemoveEntity_PurgesContinuousLocomotionState()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitContinuousLocomotionState(10, CreateContinuousState(localX: 1024, localY: 0));

            writeContext.RemoveEntity(10);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionPose(10, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void WorldState_MutualExclusion_KinematicAndContinuous()
        {
            var continuousFirst = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            continuousFirst.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                CreateContinuousState(localX: 1024, localY: 0));

            Assert.Throws<InvalidOperationException>(
                () => continuousFirst.CreateWriteContext().SetUnitKinematicState(
                    10,
                    CreateKinematicState(localX: 1024, localY: 0)));

            var kinematicFirst = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            kinematicFirst.CreateWriteContext().SetUnitKinematicState(
                10,
                CreateKinematicState(localX: 1024, localY: 0));

            Assert.Throws<InvalidOperationException>(
                () => kinematicFirst.CreateWriteContext().SetUnitContinuousLocomotionState(
                    10,
                    CreateContinuousState(localX: 1024, localY: 0)));
        }

        [Test]
        [Category("Core")]
        public void FinalizationBatch_MoveEntityThenSetContinuousState_PreservesNormalizedPose()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { CreateUnit(10) });
            var batch = new FinalizationBatch();
            batch.MoveEntity(10, new SurfaceCell(FaceId.Floor, 1, 0));
            batch.SetUnitContinuousLocomotionState(
                10,
                CreateContinuousState(localX: KinematicFixed.MinLocalOffset, localY: 0));

            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var unit), Is.True);
            Assert.That(unit.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var continuousState), Is.True);
            Assert.That(continuousState.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset));
            Assert.That(continuousState.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
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

        private static UnitContinuousLocomotionState CreateContinuousState(int localX, int localY)
        {
            return new UnitContinuousLocomotionState
            {
                localOffset = new SimulationOffset2(KinematicFixed.FromRaw(localX), KinematicFixed.FromRaw(localY)),
                velocity = new SimulationVelocity2(
                    KinematicFixed.FromRaw(localX == 0 ? 0 : 410),
                    KinematicFixed.FromRaw(localY == 0 ? 0 : 410)),
                facing = localX < 0 ? Direction.Left : Direction.Right,
                lastMoveDirection = localX < 0 ? Direction.Left : Direction.Right,
                speedUnitsPerTick = 410,
                mode = ContinuousLocomotionMode.Moving,
                sequenceId = 1,
            }.NormalizedForStorage();
        }

        private static UnitKinematicRuntimeState CreateKinematicState(int localX, int localY)
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = new SimulationOffset2(KinematicFixed.FromRaw(localX), KinematicFixed.FromRaw(localY)),
                velocity = new SimulationVelocity2(KinematicFixed.FromRaw(410), KinematicFixed.Zero),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = KinematicFixed.UnitsPerCell,
                remainingTicks = 10,
                speedScalePermille = 1000,
                sequenceId = 1,
                elapsedTicks = 1,
                totalTicks = 20,
                commitTick = 10,
                startedTick = 1,
                stepDirectionX = localX == 0 ? 0 : 1,
                stepDirectionY = localY == 0 ? 0 : 1,
            }.NormalizedForStorage();
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
