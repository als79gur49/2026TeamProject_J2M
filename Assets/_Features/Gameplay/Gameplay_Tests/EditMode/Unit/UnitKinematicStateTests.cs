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
