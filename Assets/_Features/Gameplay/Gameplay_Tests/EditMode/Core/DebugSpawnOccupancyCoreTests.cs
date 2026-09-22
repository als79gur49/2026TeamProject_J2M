using System;
using Game.Feature.Gameplay.BoardState;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class DebugSpawnOccupancyCoreTests
    {
        private static readonly CubeTopologyState TestTopology = new(FaceId.Floor);

        [TestCase(false)]
        [TestCase(true)]
        [Category("Core")]
        public void DebugSpawnPolicy_DetachedBoxSharingUnitCell_AllowsBothOrders(bool detachedFirst)
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var unit = CreateUnit(entityId: 10, position: sharedCell);
            var detachedBox = CreateBox(
                entityId: 20,
                position: sharedCell,
                boardPresence: EntityBoardPresence.Detached);
            var entities = detachedFirst
                ? new[] { detachedBox, unit }
                : new[] { unit, detachedBox };

            Assert.DoesNotThrow(
                () => DebugSpawnValidityPolicy.EnsureRepresentable(
                    BoardBounds.Unbounded,
                    TestTopology,
                    entities));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Core")]
        public void DebugSpawnPolicy_OccupyingBoxSharingUnitCell_RejectsBothOrders(bool boxFirst)
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var unit = CreateUnit(entityId: 10, position: sharedCell);
            var box = CreateBox(
                entityId: 20,
                position: sharedCell,
                boardPresence: EntityBoardPresence.Occupying);
            var entities = boxFirst
                ? new[] { box, unit }
                : new[] { unit, box };

            Assert.Throws<InvalidOperationException>(
                () => DebugSpawnValidityPolicy.EnsureRepresentable(
                    BoardBounds.Unbounded,
                    TestTopology,
                    entities));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Core")]
        public void DebugSpawnPolicy_InactiveFaceOccupyingWallSharingUnitCell_RejectsBothOrders(bool wallFirst)
        {
            var sharedCell = new SurfaceCell(FaceId.Ceiling, 0, 0);
            var unit = CreateUnit(entityId: 10, position: sharedCell);
            var wall = CreateLegacyNoneSolid(entityId: 20, position: sharedCell);
            var entities = wallFirst
                ? new[] { wall, unit }
                : new[] { unit, wall };

            Assert.Throws<InvalidOperationException>(
                () => DebugSpawnValidityPolicy.EnsureRepresentable(
                    BoardBounds.Unbounded,
                    TestTopology,
                    entities));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Core")]
        public void DebugSpawnPolicy_ExplicitWallSharingUnitCell_RejectsBothOrders(bool wallFirst)
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var unit = CreateUnit(entityId: 10, position: sharedCell);
            var wall = CreateExplicitWall(entityId: 20, position: sharedCell);
            var entities = wallFirst
                ? new[] { wall, unit }
                : new[] { unit, wall };

            var exception = Assert.Throws<InvalidOperationException>(
                () => DebugSpawnValidityPolicy.EnsureRepresentable(
                    BoardBounds.Unbounded,
                    TestTopology,
                    entities));

            var expectedBlockerId = wallFirst ? wall.entityId : unit.entityId;
            var expectedBlockerType = wallFirst ? EntityType.Wall : EntityType.Unit;
            Assert.That(
                exception.Message,
                Does.Contain($"BlockerEntity={expectedBlockerId}"));
            Assert.That(
                exception.Message,
                Does.Contain($"BlockerType={expectedBlockerType}"));
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            EntityBoardPresence boardPresence)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push,
                boardPresence = boardPresence,
            };
        }

        private static EntityState CreateLegacyNoneSolid(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateExplicitWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Wall,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
