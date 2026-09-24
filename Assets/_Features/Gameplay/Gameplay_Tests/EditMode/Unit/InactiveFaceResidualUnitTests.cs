using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class InactiveFaceResidualUnitTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyJumpQueries_TryResolveLandingCell_InactiveFaceSolidLockedTarget_StillReturnsExactTarget()
        {
            var snapshot = CreateInactiveFaceJumpSnapshot("solid");
            var source = GetEntity(snapshot, 40);
            var jumpState = CreateEnemyJumpState(
                EnemyJumpPhase.Airborne,
                source.position,
                new SurfaceCell(FaceId.Front, 2, 1),
                landingTick: 1);

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, jumpState.lockedTargetCell, source.entityId, out _), Is.False);
            Assert.That(
                EnemyJumpQueries.TryResolveLandingCell(
                    snapshot,
                    source,
                    jumpState,
                    out var landingCell,
                    out var landingRule,
                    Array.Empty<TileFeatureRuntimeDefinition>()),
                Is.True);
            Assert.That(landingCell, Is.EqualTo(jumpState.lockedTargetCell));
            Assert.That(landingRule, Is.EqualTo("TargetExact"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateJumpLandingCell_InactiveFaceSolidBlocker_ReturnsBlocked()
        {
            var snapshot = CreateInactiveFaceJumpSnapshot("solid");
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);
            Assert.That(snapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.True);
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                    snapshot,
                    snapshot,
                    targetCell,
                    sourceId: 40,
                    ignoredDeadTargetId: 0,
                    tileFeatureEvidence: TileFeatureSettlementEvidence.Empty).Verdict,
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
                    ignoredDeadTargetId: 0,
                    tileFeatureEvidence: TileFeatureSettlementEvidence.Empty).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        [Test]
        [Category("Extended")]
        public void ProjectedWorld_JumpLandingBatch_MoveIntoInactiveFaceSolid_StillThrows()
        {
            var baseSnapshot = CreateInactiveFaceJumpSnapshot("solid");
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

        [Test]
        [Category("Extended")]
        public void ProjectedWorld_JumpLandingBatch_MoveIntoInactiveFaceTerrainFreeCell_RemainsRepresentableAndAuthoritativePlacementAllowed()
        {
            var baseSnapshot = CreateInactiveFaceJumpSnapshot("none");
            var batch = new FinalizationBatch();
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            batch.MoveEntity(40, targetCell);
            batch.SetBoardPresence(40, EntityBoardPresence.Occupying);

            var projectedWorld = new ProjectedWorld(baseSnapshot);
            projectedWorld.ApplyBatch(batch);

            WorldSnapshot projectedSnapshot = null;
            Assert.DoesNotThrow(() => projectedSnapshot = projectedWorld.CreateSnapshot());
            Assert.That(projectedSnapshot, Is.Not.Null);
            Assert.That(projectedSnapshot.TryGetEntity(40, out var actor), Is.True);
            Assert.That(actor.position, Is.EqualTo(targetCell));
            Assert.That(
                projectedSnapshot.TryGetAuthoritativePlacementBlocker(
                    EntityType.Unit,
                    targetCell,
                    ignoredEntityId: 40,
                    out _),
                Is.False);
        }

        private static WorldSnapshot CreateInactiveFaceJumpSnapshot(string blockerKind)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            var initialEntities = new List<EntityState>
            {
                CreateEnemyUnit(entityId: 40, position: sourceCell, boardPresence: EntityBoardPresence.Detached),
            };

            switch (blockerKind)
            {
                case "solid":
                    initialEntities.Add(CreateWall(entityId: 90, position: targetCell));
                    break;

                case "none":
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(blockerKind), blockerKind, "Unsupported inactive-face jump snapshot blocker kind.");
            }

            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)),
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
                type = EntityType.Wall,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
