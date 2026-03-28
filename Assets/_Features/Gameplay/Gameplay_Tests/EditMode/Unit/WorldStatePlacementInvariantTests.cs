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

        [Test]
        public void SpawnEntity_InactiveFaceOccupiedDestination_StillThrowsForAuthoritativeStateValidation()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Ceiling, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Ceiling, 1, 0))));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var existingEntity), Is.True);
            Assert.That(existingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Ceiling, 1, 0)));
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        public void MoveEntity_MarkedForDeathOccupiedDestination_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                    CreateUnit(entityId: 20, position: new Vector2Int(1, 0), markedForDeath: true),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().MoveEntity(10, new Vector2Int(1, 0)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var movingEntity), Is.True);
            Assert.That(movingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetEntity(20, out var blockingEntity), Is.True);
            Assert.That(blockingEntity.markedForDeath, Is.True);
            Assert.That(blockingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.zero, out var originalOccupant), Is.True);
            Assert.That(originalOccupant.entityId, Is.EqualTo(10));
            Assert.That(snapshot.TryGetUnitAt(new Vector2Int(1, 0), out var blockerOccupant), Is.True);
            Assert.That(blockerOccupant.entityId, Is.EqualTo(20));
        }

        [Test]
        public void SetBoardPresence_ReoccupyingIntoOccupiedCell_ThrowsAndLeavesStateConsistent()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                    CreateUnit(entityId: 20, position: Vector2Int.right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var writeContext = worldState.CreateWriteContext();

            writeContext.SetBoardPresence(10, EntityBoardPresence.Detached);
            writeContext.MoveEntity(20, Vector2Int.zero);

            Assert.Throws<InvalidOperationException>(
                () => writeContext.SetBoardPresence(10, EntityBoardPresence.Occupying));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var reoccupyingEntity), Is.True);
            Assert.That(reoccupyingEntity.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(reoccupyingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetEntity(20, out var currentOccupant), Is.True);
            Assert.That(currentOccupant.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.zero, out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(20));
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.right, out _), Is.False);
        }

        [Test]
        public void SpawnEntity_ProjectileDestinationOccupiedByUnit_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                });

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateProjectile(entityId: 20, position: Vector2Int.zero)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetUnitAt(Vector2Int.zero, out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(10));
            Assert.That(snapshot.TryGetProjectileAt(Vector2Int.zero, out _), Is.False);
        }

        [Test]
        public void SpawnEntity_ProjectileDestinationOccupiedByProjectile_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateProjectile(entityId: 10, position: Vector2Int.zero),
                });

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateProjectile(entityId: 20, position: Vector2Int.zero)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetProjectileAt(Vector2Int.zero, out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(10));
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position, bool markedForDeath = false)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position), markedForDeath);
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, bool markedForDeath = false)
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
                markedForDeath = markedForDeath,
                spawnTick = 0,
            };
        }

        private static EntityState CreateProjectile(int entityId, Vector2Int position)
        {
            return CreateProjectile(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateProjectile(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
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
