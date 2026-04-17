using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class EnemyAiInactiveFaceResidualScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_InactiveFaceSolidBlocker_ResolveRejectsBeforeMaterialization()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 4, 1)),
                    CreateEnemyUnit(entityId: 40, position: sourceCell),
                    CreateWall(entityId: 90, position: targetCell),
                },
                GameplayTerrainData.Empty);
            var profile = CreateJumpChaserProfile();
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var writeContext = worldState.CreateWriteContext();
                writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
                writeContext.SetEnemyJumpState(40, CreateEnemyJumpState(sourceCell, targetCell, landingTick: 1));

                var airborneSnapshot = worldState.CreateSnapshot();
                Assert.That(airborneSnapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.True);

                writeContext.SetTopology(new CubeTopologyState(FaceId.Back));
                var driftedSnapshot = worldState.CreateSnapshot();
                Assert.That(driftedSnapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);
                Assert.That(driftedSnapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.True);

                TickResult landingTick = default;
                Assert.DoesNotThrow(() => landingTick = pipeline.RunTick(new TickInput(1)));

                var jumpState = GetEnemyJumpState(worldState, 40);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        landingTick.MovementPhaseResult.CommitEvents,
                        "EnemyJumpStateUpdated",
                        "E=40",
                        "Label=Retry",
                        "Reason=ResolveRejected"),
                    Is.True);
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.retryCount, Is.EqualTo(1));
                Assert.That(jumpState.landingTick, Is.EqualTo(2));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_InactiveFaceTerrainBlocker_ResolveRejectsBeforeMaterialization()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 4, 1)),
                    CreateEnemyUnit(entityId: 40, position: sourceCell),
                },
                new GameplayTerrainData(new[] { targetCell.PlanarPosition }));
            var profile = CreateJumpChaserProfile();
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var writeContext = worldState.CreateWriteContext();
                writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
                writeContext.SetEnemyJumpState(40, CreateEnemyJumpState(sourceCell, targetCell, landingTick: 1));

                var airborneSnapshot = worldState.CreateSnapshot();
                Assert.That(airborneSnapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.True);

                writeContext.SetTopology(new CubeTopologyState(FaceId.Back));
                var driftedSnapshot = worldState.CreateSnapshot();
                Assert.That(driftedSnapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);
                Assert.That(driftedSnapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out var blocker), Is.True);
                Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Terrain));

                TickResult landingTick = default;
                Assert.DoesNotThrow(() => landingTick = pipeline.RunTick(new TickInput(1)));

                var jumpState = GetEnemyJumpState(worldState, 40);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        landingTick.MovementPhaseResult.CommitEvents,
                        "EnemyJumpStateUpdated",
                        "E=40",
                        "Label=Retry",
                        "Reason=ResolveRejected"),
                    Is.True);
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.retryCount, Is.EqualTo(1));
                Assert.That(jumpState.landingTick, Is.EqualTo(2));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_InactiveFaceDetachedHiddenOccupant_LandsSuccessfully()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 4, 1)),
                    CreateEnemyUnit(entityId: 40, position: sourceCell),
                    CreateUnit(entityId: 60, teamId: 1, position: targetCell, boardPresence: EntityBoardPresence.Detached),
                },
                GameplayTerrainData.Empty);
            var profile = CreateJumpChaserProfile();
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var writeContext = worldState.CreateWriteContext();
                writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
                writeContext.SetEnemyJumpState(40, CreateEnemyJumpState(sourceCell, targetCell, landingTick: 1));
                writeContext.SetTopology(new CubeTopologyState(FaceId.Back));

                var driftedSnapshot = worldState.CreateSnapshot();
                Assert.That(driftedSnapshot.TryGetPlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);
                Assert.That(driftedSnapshot.TryGetAuthoritativePlacementBlocker(EntityType.Unit, targetCell, ignoredEntityId: 40, out _), Is.False);

                TickResult landingTick = default;
                Assert.DoesNotThrow(() => landingTick = pipeline.RunTick(new TickInput(1)));

                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        landingTick.MovementPhaseResult.CommitEvents,
                        "EnemyJumpStateUpdated",
                        "E=40",
                        "Label=Landing"),
                    Is.True);
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                Assert.That(GetEntity(worldState, 60).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        private static WorldState CreateWorldState(
            EntityState[] initialEntities,
            GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)),
                terrainData,
                new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EnemyJumpRuntimeState GetEnemyJumpState(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(entityId, out var jumpState), Is.True);
            return jumpState;
        }

        private static void PrimePlayerControlState(WorldState worldState, params int[] entityIds)
        {
            var writeContext = worldState.CreateWriteContext();
            for (var i = 0; i < entityIds.Length; i++)
            {
                writeContext.SetPlayerControlState(entityIds[i], default);
            }
        }

        private static EnemyAiProfile CreateJumpChaserProfile()
        {
            return EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1));
        }

        private static void DestroyProfile(EnemyAiProfile profile)
        {
            EnemyAiProfileTestFactory.Destroy(profile);
        }

        private static EnemyJumpRuntimeState CreateEnemyJumpState(
            SurfaceCell sourceCell,
            SurfaceCell lockedTargetCell,
            int landingTick)
        {
            return new EnemyJumpRuntimeState
            {
                phase = EnemyJumpPhase.Airborne,
                sequence = 1,
                sourceCell = sourceCell,
                lockedTargetCell = lockedTargetCell,
                windupEndTick = landingTick - 1,
                landingTick = landingTick,
                cooldownRemainingTicks = 0,
                retryCount = 0,
            };
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            var entity = CreateUnit(entityId, teamId: 2, position: position);
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
