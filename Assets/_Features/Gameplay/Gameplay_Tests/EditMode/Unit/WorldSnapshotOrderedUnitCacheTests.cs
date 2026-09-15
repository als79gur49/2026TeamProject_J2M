using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class WorldSnapshotOrderedUnitCacheTests
    {
        [Test]
        [Category("Extended")]
        public void GetOrderedUnitsForRead_MixedEntities_ReturnsEveryUnitByEntityIdOnly()
        {
            var dead = CreateUnit(15, new SurfaceCell(FaceId.Floor, 2, 0));
            dead.hp = 0;
            var marked = CreateUnit(20, new SurfaceCell(FaceId.Floor, 3, 0));
            marked.markedForDeath = true;
            var detached = CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0));
            detached.boardPresence = EntityBoardPresence.Detached;
            var entities = new[]
            {
                CreateNonUnit(40, EntityType.Wall, new SurfaceCell(FaceId.Floor, 7, 0)),
                CreateUnit(35, new SurfaceCell(FaceId.Ceiling, 6, 0)),
                marked,
                CreateUnit(5, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateNonUnit(1, EntityType.Box, new SurfaceCell(FaceId.Floor, 8, 0)),
                CreateUnit(30, new SurfaceCell(FaceId.Floor, 5, 0)),
                detached,
                CreateUnit(25, new SurfaceCell(FaceId.Floor, 4, 0)),
                dead,
            };
            var worldState = GameplayWorldStateTestFactory.CreateBounded(entities);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(25, new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Airborne });
            writeContext.SetPhasedState(
                30,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));

            var orderedUnits = worldState.CreateSnapshot().GetOrderedUnitsForRead().ToArray();

            CollectionAssert.AreEqual(new[] { 5, 10, 15, 20, 25, 30, 35 }, orderedUnits.Select(unit => unit.entityId));
            Assert.That(orderedUnits, Has.All.Matches<EntityState>(unit => unit.type == EntityType.Unit));
        }

        [Test]
        [Category("Extended")]
        public void GetOrderedUnitsForRead_InsertionOrderDoesNotAffectResult()
        {
            var first = new[]
            {
                CreateUnit(30, new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateNonUnit(1, EntityType.Box, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 2, 0)),
            };
            var second = first.Reverse().ToArray();

            var firstIds = GameplayWorldStateTestFactory.CreateBounded(first)
                .CreateSnapshot()
                .GetOrderedUnitsForRead()
                .ToArray()
                .Select(unit => unit.entityId);
            var secondIds = GameplayWorldStateTestFactory.CreateBounded(second)
                .CreateSnapshot()
                .GetOrderedUnitsForRead()
                .ToArray()
                .Select(unit => unit.entityId);

            CollectionAssert.AreEqual(firstIds, secondIds);
            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, firstIds);
        }

        [Test]
        [Category("Extended")]
        public void GetOrderedUnitsForRead_EmptyAndRepeatedRead_BuildsOncePerSnapshot()
        {
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateNonUnit(1, EntityType.Box, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateNonUnit(2, EntityType.Wall, new SurfaceCell(FaceId.Floor, 1, 0)),
                    })
                .CreateSnapshot();

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                Assert.That(snapshot.GetOrderedUnitsForRead().Length, Is.Zero);
                Assert.That(snapshot.GetOrderedUnitsForRead().Length, Is.Zero);
                counts = capture.Counts;
            }

            Assert.That(counts.OrderedUnitsCacheMissCount, Is.EqualTo(1));
            Assert.That(counts.OrderedUnitsCacheHitCount, Is.EqualTo(1));
            Assert.That(counts.OrderedUnitsSortCount, Is.EqualTo(1));
            Assert.That(counts.OrderedUnitsEnumeratedCount, Is.Zero);
            Assert.That(counts.OrderedEntitiesCacheMissCount, Is.Zero);
            Assert.That(counts.OrderedEntitiesSortCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void GetOrderedUnitsForRead_RepeatedRead_ReportsUnitCacheOnly()
        {
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(30, new SurfaceCell(FaceId.Floor, 3, 0)),
                        CreateNonUnit(1, EntityType.Box, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                        CreateUnit(20, new SurfaceCell(FaceId.Floor, 2, 0)),
                    })
                .CreateSnapshot();

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                Assert.That(snapshot.GetOrderedUnitsForRead().Length, Is.EqualTo(3));
                Assert.That(snapshot.GetOrderedUnitsForRead().Length, Is.EqualTo(3));
                counts = capture.Counts;
            }

            Assert.That(counts.OrderedUnitsCacheMissCount, Is.EqualTo(1));
            Assert.That(counts.OrderedUnitsCacheHitCount, Is.EqualTo(1));
            Assert.That(counts.OrderedUnitsSortCount, Is.EqualTo(1));
            Assert.That(counts.OrderedUnitsEnumeratedCount, Is.EqualTo(6));
            Assert.That(counts.OrderedEntitiesCacheMissCount, Is.Zero);
            Assert.That(counts.OrderedEntitiesCacheHitCount, Is.Zero);
            Assert.That(counts.OrderedEntitiesSortCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void GetOrderedUnitsForRead_WorldMutation_DoesNotChangeExistingSnapshotCache()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)) });
            var beforeSnapshot = worldState.CreateSnapshot();
            var beforeIds = beforeSnapshot.GetOrderedUnitsForRead().ToArray().Select(unit => unit.entityId).ToArray();

            var writeContext = worldState.CreateWriteContext();
            writeContext.RemoveEntity(10);
            writeContext.SpawnEntity(CreateUnit(20, new SurfaceCell(FaceId.Floor, 2, 0)));
            var afterIds = worldState.CreateSnapshot().GetOrderedUnitsForRead().ToArray().Select(unit => unit.entityId);

            CollectionAssert.AreEqual(new[] { 10 }, beforeIds);
            CollectionAssert.AreEqual(new[] { 10 }, beforeSnapshot.GetOrderedUnitsForRead().ToArray().Select(unit => unit.entityId));
            CollectionAssert.AreEqual(new[] { 20 }, afterIds);
        }

        [Test]
        [Category("Extended")]
        public void GetOrderedUnitsForRead_FirstReadAfterWorldMutation_UsesSnapshotOwnedEntities()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)) });
            var beforeSnapshot = worldState.CreateSnapshot();

            var writeContext = worldState.CreateWriteContext();
            writeContext.RemoveEntity(10);
            writeContext.SpawnEntity(CreateUnit(20, new SurfaceCell(FaceId.Floor, 2, 0)));

            var beforeIds = beforeSnapshot.GetOrderedUnitsForRead().ToArray().Select(unit => unit.entityId);
            var afterIds = worldState.CreateSnapshot().GetOrderedUnitsForRead().ToArray().Select(unit => unit.entityId);

            CollectionAssert.AreEqual(new[] { 10 }, beforeIds);
            CollectionAssert.AreEqual(new[] { 20 }, afterIds);
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
                unitRole = UnitRole.Enemy,
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
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = type == EntityType.Box ? BoxCapabilities.Push : BoxCapabilities.None,
            };
        }
    }
}
