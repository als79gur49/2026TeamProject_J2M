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
        public void RespawnProcessor_InactiveFaceSolidAtSpawn_DefersUntilTopologyReset()
        {
            var spawnCell = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateWall(entityId: 90, position: spawnCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                new CubeTopologyState(FaceId.Back));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, spawnCell, ignoredEntityId: 0, out _), Is.False);
            Assert.That(snapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, spawnCell, ignoredEntityId: 0, out var blocker), Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Entity));

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
                    allowRespawn: true,
                    worldState.CreateWriteContext()));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.RespawnedEntities, Is.Empty);
            Assert.That(result.TopologyResetRequest.HasValue, Is.True);
            Assert.That(result.TopologyResetRequest.Value.SourceTopology, Is.EqualTo(new CubeTopologyState(FaceId.Back)));
            Assert.That(result.TopologyResetRequest.Value.DestinationTopology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(result.TopologyResetRequest.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(result.EventLogEntries, Does.Contain("RespawnDeferred|E=10|Reason=TopologyResetRequired|TargetFace=Front|Tick=1"));
            Assert.That(result.EventLogEntries, Does.Contain("RespawnTopologyResetRequested|E=10|From=Back|To=Floor|Rotation=Forward|TargetFace=Front|Tick=1"));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
        }

        [Test]
        [Category("Extended")]
        public void RespawnProcessor_ActiveFrontFaceSolidAtSpawn_DefersUntilBottomFace()
        {
            var spawnCell = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateWall(entityId: 90, position: spawnCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                new CubeTopologyState(FaceId.Floor));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.Topology.IsFaceActive(spawnCell.face), Is.True);
            Assert.That(snapshot.Topology.BottomFace, Is.Not.EqualTo(spawnCell.face));
            Assert.That(snapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, spawnCell, ignoredEntityId: 0, out var blocker), Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Entity));

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
                    allowRespawn: true,
                    worldState.CreateWriteContext()));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.RespawnedEntities, Is.Empty);
            Assert.That(result.EventLogEntries, Does.Contain("RespawnDeferred|E=10|Reason=TopologyResetRequired|TargetFace=Front|Tick=1"));
            Assert.That(result.EventLogEntries, Does.Contain("RespawnTopologyResetRequested|E=10|From=Floor|To=Front|Rotation=Forward|TargetFace=Front|Tick=1"));
            Assert.That(result.EventLogEntries, Has.None.StartWith("RespawnSkipped|E=10|"));
            Assert.That(result.TopologyResetRequest.HasValue, Is.True);
            Assert.That(result.TopologyResetRequest.Value.SourceTopology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(result.TopologyResetRequest.Value.DestinationTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(result.TopologyResetRequest.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
        }

        [Test]
        [Category("Core")]
        public void RespawnPlacement_BoundaryMetadata_IsSpawnRespawnPlacement()
        {
            var spawnCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                new CubeTopologyState(FaceId.Floor));
            var snapshot = worldState.CreateSnapshot();
            var processor = new RespawnProcessor();

            var result = processor.Process(
                snapshot,
                snapshot,
                CleanupFixtureFactory.None(),
                new[] { CreatePlayerUnit(entityId: 10, position: spawnCell, hp: 3) },
                tickIndex: 1,
                respawnDelayTicks: 1,
                allowRespawn: true,
                worldState.CreateWriteContext());

            Assert.That(result.RespawnedEntities.Count, Is.EqualTo(1));
            Assert.That(result.RespawnPlacementRecords.Count, Is.EqualTo(1));
            Assert.That(result.RespawnPlacementRecords[0].EntityId, Is.EqualTo(10));
            Assert.That(result.RespawnPlacementRecords[0].PlacementCell, Is.EqualTo(spawnCell));
            Assert.That(
                result.RespawnPlacementRecords[0].BoundaryKind,
                Is.EqualTo(MovementExecutionBoundaryKind.SpawnRespawnPlacement));
            Assert.That(result.RespawnPlacementRecords[0].BoundaryReason, Is.EqualTo("PlayerRespawnPlacement"));
            Assert.That(result.EventLogEntries, Has.None.Contains("LegacyUnitOrdinaryMovementDetected"));
        }

        [Test]
        [Category("Core")]
        public void RespawnPlacement_BoundaryTrace_IsSpawnRespawnPlacement()
        {
            var spawnCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                new CubeTopologyState(FaceId.Floor));
            var snapshot = worldState.CreateSnapshot();
            var processor = new RespawnProcessor();

            var result = processor.Process(
                snapshot,
                snapshot,
                CleanupFixtureFactory.None(),
                new[] { CreatePlayerUnit(entityId: 10, position: spawnCell, hp: 3) },
                tickIndex: 1,
                respawnDelayTicks: 1,
                allowRespawn: true,
                worldState.CreateWriteContext());
            var finalSnapshot = worldState.CreateSnapshot();
            var tickResultData = new TickResultData(
                result.RespawnedEntities,
                Array.Empty<Game.Feature.Gameplay.Attack.DelayedAttackEffectRecord>(),
                result.EventLogEntries);

            var trace = new TickTraceFormatter().Format(
                1,
                snapshot,
                new EnemyAiPhaseResult(new List<string>(), new List<string>(), new List<string>()),
                new EnemyActionPhaseResult(new List<EnemyActionTransition>(), new List<EnemyActionTransition>()),
                new PreMovementStatePhaseResult(new List<string>()),
                MovementPhaseResult.Empty,
                snapshot,
                AttackPhaseResult.Empty,
                CleanupPhaseResult.Empty,
                result,
                finalSnapshot,
                tickResultData,
                "hash");

            Assert.That(trace, Does.Contain("Respawn.Placements"));
            Assert.That(trace, Does.Contain("Boundary=SpawnRespawnPlacement"));
            Assert.That(trace, Does.Contain("BoundaryReason=PlayerRespawnPlacement"));
            Assert.That(trace, Does.Not.Contain("LegacyUnitOrdinaryMovementDetected"));
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
        public void ProjectedWorld_JumpLandingBatch_MoveIntoInactiveFaceTerrain_RemainsRepresentable_ButAuthoritativePlacementStaysBlocked()
        {
            var baseSnapshot = CreateInactiveFaceJumpSnapshot("solid");
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
                    out var blocker),
                Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
        }

        private static WorldSnapshot CreateInactiveFaceJumpSnapshot(string blockerKind)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            var initialEntities = new[]
            {
                CreateEnemyUnit(entityId: 40, position: sourceCell, boardPresence: EntityBoardPresence.Detached),
                CreateWall(entityId: 90, position: targetCell),
            };
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
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
