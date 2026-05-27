using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class AstretonJumpRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string JumpChaserProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset";

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_WindupDoesNotMoveDamageOrMutateOccupancy()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, targetCell, hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    windupEndTick = 5,
                    landingTick = 10,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(sourceCell));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Windup));
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(tick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_AirborneSuppressesGroundMovement()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, targetCell, hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    windupEndTick = 0,
                    landingTick = 5,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(sourceCell));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_AirborneOffBottomDefersLandingUntilFaceReturns()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, targetCell, hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                },
                topology: new CubeTopologyState(FaceId.Floor));
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    landingTick = 2,
                });
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var stillOffBottom = pipeline.RunTick(new TickInput(2));
            var suspendedState = GetJumpState(worldState);

            Assert.That(suspendedState.phase, Is.EqualTo(EnemyJumpPhase.Airborne), stillOffBottom.Trace.Text);
            Assert.That(suspendedState.landingTick, Is.GreaterThan(2));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(stillOffBottom.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.False);

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            var returnedTick = pipeline.RunTick(new TickInput(3));

            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne), returnedTick.Trace.Text);
            Assert.That(returnedTick.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_LandingRevalidatesSurfaceCellAfterTopologyReturn()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var staleTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 4, 0), hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                    CreateWall(90, staleTargetCell),
                },
                topology: new CubeTopologyState(FaceId.Front));
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = staleTargetCell,
                    landingTick = 2,
                });
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            RunUntilNotAirborne(pipeline, worldState, startTick: 2, maxTick: 12);

            var enemy = GetEntity(worldState, EnemyId);
            Assert.That(enemy.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(enemy.position, Is.Not.EqualTo(staleTargetCell), "Landing must revalidate the locked SurfaceCell and avoid a stale blocked target.");
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(staleTargetCell, out _), Is.True);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), enemy.position, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_PushImpactDuringWindupClearsOrPreservesDocumentedState()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateBox(50, new SurfaceCell(FaceId.Floor, -1, 0), BoxCapabilities.Push, EntityPhaseState.Sliding, Direction.Right, PlayerId, 1),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 3, 0),
                    windupEndTick = 5,
                    landingTick = 10,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).hp, Is.EqualTo(2), "CurrentContract: push-box impact damages windup Astreton.");
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Windup), "CurrentContract: windup state is preserved after non-lethal push impact.");
            Assert.That(tick.AttackPhaseResult.DrainedImpactReservations.Any(reservation => reservation.TargetId == EnemyId), Is.True);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_FlipImpactDuringAirborneClearsOrPreservesDocumentedState()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                CreateBox(50, new SurfaceCell(FaceId.Floor, -1, 0), BoxCapabilities.Flip, EntityPhaseState.Sliding, Direction.Right, PlayerId, 1),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 3, 0),
                    landingTick = 5,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).hp, Is.EqualTo(3), "CurrentContract: detached airborne Astreton is ignored by the sliding flip-capable impact carrier.");
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(tick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.Zero);
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(JumpChaserProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing Astreton JumpChaser profile at '{JumpChaserProfilePath}'.");

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerKinematicLocomotionTiming: CreateOneTickKinematicTiming());
        }

        private static void RunUntilNotAirborne(TickPipeline pipeline, WorldState worldState, int startTick, int maxTick)
        {
            for (var tick = startTick; tick <= maxTick; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
                if (GetJumpState(worldState).phase != EnemyJumpPhase.Airborne)
                {
                    return;
                }
            }

            Assert.Fail($"Astreton stayed airborne through tick {maxTick}.");
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            CubeTopologyState? topology = null,
            GameplayTerrainData terrainData = null)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(6, 6)),
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EnemyJumpRuntimeState GetJumpState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(EnemyId, out var jumpState), Is.True);
            return jumpState;
        }

        private static int CountUnitsAt(WorldSnapshot snapshot, SurfaceCell cell, int entityId)
        {
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            return units.Count(unit => unit.entityId == entityId);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = teamId == 1 ? UnitRole.Player : UnitRole.Enemy,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = boardPresence,
                spawnTick = 0,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities,
            EntityPhaseState state,
            Direction facing,
            int kineticInstigatorEntityId,
            int kineticInstigatorTeamId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                state = state,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
                kineticInstigatorEntityId = kineticInstigatorEntityId,
                kineticInstigatorTeamId = kineticInstigatorTeamId,
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
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
