using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class InactiveFaceResidualUnitTests
    {
        [Test]
        [Category("Extended")]
        public void RespawnProcessor_InactiveFaceTerrainAtSpawn_DoesNotThrow_AndSkipsDeterministically()
        {
            var spawnCell = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                new GameplayTerrainData(new[] { new Vector2Int(1, 0) }),
                new CubeTopologyState(FaceId.Back));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, spawnCell, ignoredEntityId: 0, out _), Is.False);
            Assert.That(snapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, spawnCell, ignoredEntityId: 0, out var blocker), Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Terrain));

            var processor = new RespawnProcessor();
            RespawnPhaseResult result = null;

            Assert.DoesNotThrow(
                () => result = processor.Process(
                    snapshot,
                    snapshot,
                    CleanupFixtureFactory.None(),
                    new[] { CreatePlayerUnit(entityId: 10, position: spawnCell, hp: 3) },
                    tickIndex: 1,
                    respawnDelayTicks: 1,
                    (IRespawnCommitContext)worldState.CreateWriteContext()));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.RespawnedEntities, Is.Empty);
            Assert.That(result.EventLogEntries, Does.Contain("RespawnSkipped|E=10|Pos=(1,0)|Face=Front|Tick=1|Reason=Terrain"));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out _), Is.False);
        }

        [TestCase("solid")]
        [TestCase("terrain")]
        [Category("Extended")]
        public void EnemyJumpQueries_TryResolveLandingCell_InactiveFaceSolidOrTerrainLockedTarget_StillReturnsExactTarget(string blockerKind)
        {
            var snapshot = CreateInactiveFaceJumpSnapshot(blockerKind);
            var source = GetEntity(snapshot, 40);
            var jumpState = CreateEnemyJumpState(
                EnemyJumpPhase.Airborne,
                source.position,
                new SurfaceCell(FaceId.Front, 2, 1),
                landingTick: 1);

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, jumpState.lockedTargetCell, source.entityId, out _), Is.False);
            Assert.That(
                EnemyJumpQueries.TryResolveLandingCell(snapshot, source, jumpState, out var landingCell, out var landingRule),
                Is.True);
            Assert.That(landingCell, Is.EqualTo(jumpState.lockedTargetCell));
            Assert.That(landingRule, Is.EqualTo("TargetExact"));
        }

        [TestCase("solid")]
        [TestCase("terrain")]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateJumpLandingCell_InactiveFaceSolidOrTerrainBlocker_ReturnsBlocked(string blockerKind)
        {
            var snapshot = CreateInactiveFaceJumpSnapshot(blockerKind);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);
            Assert.That(snapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.True);
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                    snapshot,
                    snapshot,
                    targetCell,
                    sourceId: 40,
                    ignoredDeadTargetId: 0).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateJumpLandingCell_InactiveFaceDetachedHiddenOccupant_ReturnsAllowed()
        {
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateEnemyUnit(
                            entityId: 40,
                            position: new SurfaceCell(FaceId.Floor, 0, 1),
                            boardPresence: EntityBoardPresence.Detached),
                        CreateUnit(
                            entityId: 60,
                            teamId: 1,
                            position: targetCell,
                            boardPresence: EntityBoardPresence.Detached),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)),
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Back))
                .CreateSnapshot();

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);
            Assert.That(snapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                    snapshot,
                    snapshot,
                    targetCell,
                    sourceId: 40,
                    ignoredDeadTargetId: 0).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        [TestCase("solid")]
        [TestCase("terrain")]
        [Category("Extended")]
        public void ProjectedWorld_JumpLandingBatch_MoveIntoInactiveFaceSolidOrTerrain_StillThrows(string blockerKind)
        {
            var baseSnapshot = CreateInactiveFaceJumpSnapshot(blockerKind);
            var batch = new FinalizationBatch();
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            batch.MoveEntity(40, targetCell);
            batch.SetBoardPresence(40, EntityBoardPresence.Occupying);

            var projectedWorld = new ProjectedWorld(baseSnapshot);
            projectedWorld.ApplyBatch(batch);

            var exception = Assert.Throws<InvalidOperationException>(() => projectedWorld.CreateSnapshot());
            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("cannot occupy", exception.Message);
        }

        private static WorldSnapshot CreateInactiveFaceJumpSnapshot(string blockerKind)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            var initialEntities = blockerKind == "solid"
                ? new[]
                {
                    CreateEnemyUnit(entityId: 40, position: sourceCell, boardPresence: EntityBoardPresence.Detached),
                    CreateWall(entityId: 90, position: targetCell),
                }
                : new[]
                {
                    CreateEnemyUnit(entityId: 40, position: sourceCell, boardPresence: EntityBoardPresence.Detached),
                };
            var terrainData = blockerKind == "terrain"
                ? new GameplayTerrainData(new[] { targetCell.PlanarPosition })
                : GameplayTerrainData.Empty;
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)),
                terrainData,
                new CubeTopologyState(FaceId.Back));

            return worldState.CreateSnapshot();
        }

        private static EntityState GetEntity(WorldSnapshot snapshot, int entityId)
        {
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EnemyJumpRuntimeState CreateEnemyJumpState(
            EnemyJumpPhase phase,
            SurfaceCell sourceCell,
            SurfaceCell lockedTargetCell,
            int landingTick)
        {
            return new EnemyJumpRuntimeState
            {
                phase = phase,
                sequence = 1,
                sourceCell = sourceCell,
                lockedTargetCell = lockedTargetCell,
                windupEndTick = landingTick - 1,
                landingTick = landingTick,
                cooldownRemainingTicks = 0,
                retryCount = 0,
            };
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position, int hp)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemyUnit(
            int entityId,
            SurfaceCell position,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            var entity = CreateUnit(entityId, teamId: 2, position: position, boardPresence: boardPresence);
            entity.aiMode = EnemyAiMode.Chase;
            entity.facing = Direction.Right;
            return entity;
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = boardPresence,
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
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
