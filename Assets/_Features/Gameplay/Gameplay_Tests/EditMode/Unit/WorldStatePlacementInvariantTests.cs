using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class WorldStatePlacementInvariantTests
    {
        [Test]
        public void MoveEntity_TerrainBlockedDestination_Throws()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                },
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(2, 2)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            var exception = Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().MoveEntity(10, new Vector2Int(1, 0)));

            StringAssert.Contains("Terrain blocks", exception.Message);
        }

        [Test]
        public void MoveEntity_TerrainBlockedDestination_LeavesEntityStateAndOccupancyUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                },
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(2, 2)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().MoveEntity(10, new Vector2Int(1, 0)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.zero, out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(10));
            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(1, 0), out _), Is.False);
        }

        [Test]
        public void SpawnEntity_TerrainBlockedDestination_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new EntityState[0],
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(2, 2)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateUnit(entityId: 20, position: new Vector2Int(1, 0))));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(1, 0), out _), Is.False);
        }

        [Test]
        public void MoveEntity_BoardOutsideDestination_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                },
                new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                GameplayTerrainData.Empty);

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().MoveEntity(10, Vector2Int.right));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.zero, out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(10));
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.right, out _), Is.False);
        }

        [Test]
        public void SpawnEntity_BoardOutsideDestination_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new EntityState[0],
                new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                GameplayTerrainData.Empty);

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateUnit(entityId: 20, position: Vector2Int.right)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.right, out _), Is.False);
        }

        [Test]
        public void SpawnEntity_UnitOccupiedDestination_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                });

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateUnit(entityId: 20, position: Vector2Int.zero)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.zero, out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(10));
        }

        [Test]
        public void SpawnEntity_InactiveFaceTerrainBlockedDestination_StillThrowsForAuthoritativeStateValidation()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Ceiling, 1, 0))));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position));
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
                stateTimer = 0,
                facing = Direction.Right,
                markedForDeath = false,
                spawnTick = 0,
            };
        }
    }
}
