using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class WorldStatePlacementInvariantTests
    {
        [Test]
        [Category("Extended")]
        public void MoveEntity_TerrainBlockedDestination_AllowsRepresentableAuthoritativeMove()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                },
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(2, 2)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            Assert.DoesNotThrow(
                () => worldState.CreateWriteContext().MoveEntity(10, new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void MoveEntity_TerrainBlockedDestination_UpdatesEntityStateAndOccupancy()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                },
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(2, 2)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            worldState.CreateWriteContext().MoveEntity(10, new Vector2Int(1, 0));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetUnitIdsAt(snapshot, Vector2Int.zero), Is.Empty);
            CollectionAssert.AreEqual(new[] { 10 }, GetUnitIdsAt(snapshot, new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_TerrainBlockedDestination_AllowsRepresentableAuthoritativeSpawn()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new EntityState[0],
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(2, 2)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            Assert.DoesNotThrow(
                () => worldState.CreateWriteContext().SpawnEntity(CreateUnit(entityId: 20, position: new Vector2Int(1, 0))));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out var spawnedEntity), Is.True);
            Assert.That(spawnedEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            CollectionAssert.AreEqual(new[] { 20 }, GetUnitIdsAt(snapshot, new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
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
            CollectionAssert.AreEqual(new[] { 10 }, GetUnitIdsAt(snapshot, Vector2Int.zero));
            Assert.That(GetUnitIdsAt(snapshot, Vector2Int.right), Is.Empty);
        }

        [Test]
        [Category("Extended")]
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
            Assert.That(GetUnitIdsAt(snapshot, Vector2Int.right), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_UnitOccupiedDestination_AllowsStackingAndKeepsPrimaryOccupantDeterministic()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                });

            worldState.CreateWriteContext().SpawnEntity(CreateUnit(entityId: 20, position: Vector2Int.zero));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out var stackedEntity), Is.True);
            Assert.That(stackedEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetPrimaryUnitAt(Vector2Int.zero, out var primaryOccupant), Is.True);
            Assert.That(primaryOccupant.entityId, Is.EqualTo(10));

            var occupancy = new List<SnapshotOccupancyEntry>();
            snapshot.EnumerateUnitOccupancyOrdered(occupancy);

            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                occupancy
                    .Where(entry => entry.Cell == SurfaceCell.FromPlanar(Vector2Int.zero))
                    .Select(entry => entry.EntityId)
                    .ToArray());
        }

        [Test]
        [Category("Extended")]
        public void MoveEntity_UnitOccupiedDestination_AllowsAuthoritativeUnitStacking()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                    CreateUnit(entityId: 20, position: Vector2Int.right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);

            worldState.CreateWriteContext().MoveEntity(10, Vector2Int.right);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var movingEntity), Is.True);
            Assert.That(movingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetEntity(20, out var existingEntity), Is.True);
            Assert.That(existingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetUnitIdsAt(snapshot, Vector2Int.zero), Is.Empty);
            CollectionAssert.AreEqual(new[] { 10, 20 }, GetUnitIdsAt(snapshot, Vector2Int.right));
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_InactiveFaceTerrainBlockedDestination_AllowsRepresentableAuthoritativeState()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }));

            Assert.DoesNotThrow(
                () => worldState.CreateWriteContext().SpawnEntity(
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Ceiling, 1, 0))));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out var spawnedEntity), Is.True);
            Assert.That(spawnedEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Ceiling, 1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_FaceAwareTerrain_AllowsSamePlanarCellOnDifferentFace()
        {
            var floorBlockedCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var frontOpenCell = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new GameplayTerrainData(new[]
                {
                    new TerrainCellState(
                        floorBlockedCell,
                        TerrainKind.Generic,
                        TerrainFlags.BlocksGroundTraversal),
                }));

            worldState.CreateWriteContext().SpawnEntity(CreateUnit(entityId: 20, position: frontOpenCell));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(frontOpenCell));
            Assert.That(snapshot.IsTerrainBlockedForUnit(floorBlockedCell), Is.True);
            Assert.That(snapshot.IsTerrainBlockedForUnit(frontOpenCell), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_InactiveFaceSolidDestination_StillThrowsForAuthoritativeStateValidation()
        {
            var blockedCell = new SurfaceCell(FaceId.Ceiling, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 10, position: blockedCell),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(
                    CreateUnit(entityId: 20, position: blockedCell)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(10, out var solid), Is.True);
            Assert.That(solid.position, Is.EqualTo(blockedCell));
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_InactiveFaceOccupiedDestination_AllowsAuthoritativeUnitStacking()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Ceiling, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);

            worldState.CreateWriteContext().SpawnEntity(
                CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Ceiling, 1, 0)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var existingEntity), Is.True);
            Assert.That(existingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Ceiling, 1, 0)));
            Assert.That(snapshot.TryGetEntity(20, out var stackedEntity), Is.True);
            Assert.That(stackedEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Ceiling, 1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void MoveEntity_MarkedForDeathOccupiedDestination_AllowsAuthoritativeUnitStacking()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                    CreateUnit(entityId: 20, position: new Vector2Int(1, 0), markedForDeath: true),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);

            worldState.CreateWriteContext().MoveEntity(10, new Vector2Int(1, 0));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var movingEntity), Is.True);
            Assert.That(movingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetEntity(20, out var blockingEntity), Is.True);
            Assert.That(blockingEntity.markedForDeath, Is.True);
            Assert.That(blockingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetUnitIdsAt(snapshot, Vector2Int.zero), Is.Empty);
            Assert.That(snapshot.TryGetPrimaryUnitAt(new Vector2Int(1, 0), out var primaryOccupant), Is.True);
            Assert.That(primaryOccupant.entityId, Is.EqualTo(10));
            CollectionAssert.AreEqual(new[] { 10, 20 }, GetUnitIdsAt(snapshot, new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void SetBoardPresence_ReoccupyingIntoOccupiedCell_AllowsUnitStacking()
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
            writeContext.SetBoardPresence(10, EntityBoardPresence.Occupying);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var reoccupyingEntity), Is.True);
            Assert.That(reoccupyingEntity.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(reoccupyingEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetEntity(20, out var currentOccupant), Is.True);
            Assert.That(currentOccupant.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetPrimaryUnitAt(Vector2Int.zero, out var primaryOccupant), Is.True);
            Assert.That(primaryOccupant.entityId, Is.EqualTo(10));
            CollectionAssert.AreEqual(new[] { 10, 20 }, GetUnitIdsAt(snapshot, Vector2Int.zero));
            Assert.That(GetUnitIdsAt(snapshot, Vector2Int.right), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy()
        {
            var detachedBox = CreateBox(entityId: 20, position: Vector2Int.zero);
            detachedBox.boardPresence = EntityBoardPresence.Detached;
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                    detachedBox,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var unit), Is.True);
            Assert.That(unit.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(snapshot.TryGetEntity(20, out var detachedEntity), Is.True);
            Assert.That(detachedEntity.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            CollectionAssert.AreEqual(new[] { 10 }, GetUnitIdsAt(snapshot, Vector2Int.zero));
            Assert.That(snapshot.TryGetSolidOccupantAt(Vector2Int.zero, out _), Is.False);
            Assert.That(snapshot.TryPickImpactTargetAt(Vector2Int.zero, sourceTeamId: 2, out var impactTarget), Is.True);
            Assert.That(impactTarget.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_BoxOccupiedDestination_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 10, position: Vector2Int.zero),
                });

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateBox(entityId: 20, position: Vector2Int.zero)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var existingBox), Is.True);
            Assert.That(existingBox.type, Is.EqualTo(EntityType.Box));
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetSolidOccupantAt(Vector2Int.zero, out var solidOccupant), Is.True);
            Assert.That(solidOccupant.entityId, Is.EqualTo(10));
            Assert.That(solidOccupant.type, Is.EqualTo(EntityType.Box));
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_BoxDestinationOccupiedByUnit_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                });

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateBox(entityId: 20, position: Vector2Int.zero)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            CollectionAssert.AreEqual(new[] { 10 }, GetUnitIdsAt(snapshot, Vector2Int.zero));
            Assert.That(snapshot.TryGetSolidOccupantAt(Vector2Int.zero, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_ProjectileDestinationOccupiedByUnit_AllowsProjectileAndPreservesUnitOccupancy()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                });

            worldState.CreateWriteContext().SpawnEntity(CreateProjectile(entityId: 20, position: Vector2Int.zero));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out var projectile), Is.True);
            Assert.That(projectile.type, Is.EqualTo(EntityType.Projectile));
            Assert.That(projectile.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            CollectionAssert.AreEqual(new[] { 10 }, GetUnitIdsAt(snapshot, Vector2Int.zero));
            Assert.That(snapshot.TryGetProjectileAt(Vector2Int.zero, out var projectileOccupant), Is.True);
            Assert.That(projectileOccupant.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void SpawnEntity_ProjectileDestinationOccupiedByBox_ThrowsAndLeavesWorldUnchanged()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 10, position: Vector2Int.zero),
                });

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SpawnEntity(CreateProjectile(entityId: 20, position: Vector2Int.zero)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var box), Is.True);
            Assert.That(box.type, Is.EqualTo(EntityType.Box));
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetProjectileAt(Vector2Int.zero, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ProjectedWorld_BoxMovedOntoProjectileCell_RemainsMaterializable()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 10, position: new Vector2Int(1, 0)),
                    CreateProjectile(entityId: 20, position: new Vector2Int(2, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                GameplayTerrainData.Empty);
            worldState.CreateWriteContext().MoveEntity(10, new Vector2Int(2, 0));

            var projectedSnapshot = new ProjectedWorld(worldState.CreateSnapshot()).CreateSnapshot();

            Assert.That(projectedSnapshot.TryGetSolidOccupantAt(new Vector2Int(2, 0), out var boxOccupant), Is.True);
            Assert.That(boxOccupant.entityId, Is.EqualTo(10));
            Assert.That(projectedSnapshot.TryGetProjectileAt(new Vector2Int(2, 0), out var projectileOccupant), Is.True);
            Assert.That(projectileOccupant.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
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

        [Test]
        [Category("Extended")]
        public void WorldState_MoveEntityTo_GliderActive_CanRepresentAirborneOverSolid()
        {
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateWall(entityId: 238, position: wallCell),
                    CreateUnit(entityId: 241, position: Vector2Int.zero),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                241,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 1,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 5,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 0,
                    durationTicks: 5,
                    recoveryTicks: 1,
                    cooldownTicks: 0,
                    lastExitedTick: 0,
                    hasLockedStep: true,
                    lockedStepX: 1,
                    lockedStepY: 0));

            Assert.DoesNotThrow(() => writeContext.MoveEntity(241, wallCell));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(241, out var glider), Is.True);
            Assert.That(glider.position, Is.EqualTo(wallCell));
            Assert.That(snapshot.TryGetSolidOccupantAt(wallCell, out var solid), Is.True);
            Assert.That(solid.entityId, Is.EqualTo(238));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_MoveEntityTo_GliderNormal_CannotRepresentGroundedOnSolid()
        {
            AssertGliderGroundedPhaseCannotMoveOntoSolid(null);
        }

        [Test]
        [Category("Extended")]
        public void WorldState_MoveEntityTo_GliderWindup_CannotRepresentGroundedOnSolid()
        {
            AssertGliderGroundedPhaseCannotMoveOntoSolid(
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Windup,
                    sequence: 1,
                    windupUntilTickExclusive: 5,
                    activeUntilTickExclusive: 0,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 2,
                    durationTicks: 5,
                    recoveryTicks: 1,
                    cooldownTicks: 0,
                    lastExitedTick: 0));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_MoveEntityTo_GliderRecover_CannotRepresentGroundedOnSolid()
        {
            AssertGliderGroundedPhaseCannotMoveOntoSolid(
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Recovery,
                    sequence: 1,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 3,
                    recoveryUntilTickExclusive: 5,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 0,
                    durationTicks: 3,
                    recoveryTicks: 2,
                    cooldownTicks: 0,
                    lastExitedTick: 0));
        }

        private static void AssertGliderGroundedPhaseCannotMoveOntoSolid(EnemyGlideRuntimeState? glideState)
        {
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateWall(entityId: 238, position: wallCell),
                    CreateUnit(entityId: 241, position: Vector2Int.zero),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var writeContext = worldState.CreateWriteContext();
            if (glideState.HasValue)
            {
                writeContext.SetEnemyGlideState(241, glideState.Value);
            }

            Assert.Throws<InvalidOperationException>(
                () => writeContext.MoveEntity(241, wallCell));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(241, out var glider), Is.True);
            Assert.That(glider.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetSolidOccupantAt(wallCell, out var solid), Is.True);
            Assert.That(solid.entityId, Is.EqualTo(238));
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

        private static EntityState CreateBox(int entityId, Vector2Int position)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
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
                stateTimer = 0,
                facing = Direction.Right,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
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
                stateTimer = 0,
                facing = Direction.None,
                markedForDeath = false,
                spawnTick = 0,
            };
        }

        private static int[] GetUnitIdsAt(WorldSnapshot snapshot, Vector2Int cell)
        {
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            return units.Select(entity => entity.entityId).ToArray();
        }
    }
}
