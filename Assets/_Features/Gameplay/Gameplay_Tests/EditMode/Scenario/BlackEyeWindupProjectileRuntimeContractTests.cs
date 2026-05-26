using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class BlackEyeWindupProjectileRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string WindupProjectileProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset";

        [Test]
        [Category("Extended")]
        public void BlackEye_WindupForwardCellProjectile_DoesNotCreatePendingImpact_DuringWindup()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            var startTick = pipeline.RunTick(new TickInput(1));
            var action = GetEnemyActionState(worldState);

            Assert.That(action.IsActive, Is.True);
            Assert.That(action.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(startTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(startTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);

            if (action.executeTick > 2)
            {
                var windupTick = pipeline.RunTick(new TickInput(2));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
                Assert.That(windupTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(windupTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);
            }
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_WindupForwardCellProjectile_CreatesPendingImpact_OnExecuteTickOnly()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var action = GetEnemyActionState(worldState);
            var occupancyBeforeExecute = DumpOccupancy(worldState.CreateSnapshot());

            for (var tickIndex = 2; tickIndex < action.executeTick; tickIndex++)
            {
                pipeline.RunTick(new TickInput(tickIndex));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero, $"Pending impact created before execute tick {action.executeTick}.");
            }

            var executeTick = pipeline.RunTick(new TickInput(action.executeTick));
            var pendingImpact = GetSinglePendingImpact(worldState);

            Assert.That(pendingImpact.ReleaseTick, Is.EqualTo(action.executeTick));
            Assert.That(pendingImpact.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty, "WindupForwardCellProjectile release schedules PendingCellImpact; direct damage is not same-tick.");
            Assert.That(executeTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);
            Assert.That(DumpOccupancy(worldState.CreateSnapshot()), Is.EqualTo(occupancyBeforeExecute));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_EntersRecover_AfterForwardCellProjectileExecute()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            pipeline.RunTick(new TickInput(startedAction.executeTick));

            var action = GetEnemyActionState(worldState);
            var enemy = GetEntity(worldState, EnemyId);
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.EqualTo(1));
            Assert.That(action.executionAttempted, Is.True);
            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemy.aiStateTimer, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_DoesNotMoveDuringWindupExecuteRecover()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            var startTick = pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            AssertNoEnemyMovement(startTick);

            if (startedAction.executeTick > 2)
            {
                AssertNoEnemyMovement(pipeline.RunTick(new TickInput(2)));
            }

            AssertNoEnemyMovement(pipeline.RunTick(new TickInput(startedAction.executeTick)));
            AssertNoEnemyMovement(pipeline.RunTick(new TickInput(startedAction.executeTick + 1)));
            Assert.That(GetEntity(worldState, EnemyId).aiMode, Is.EqualTo(EnemyAiMode.Recover));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ResumesGroundMovement_AfterRecoverEnds()
        {
            var profile = LoadProfile();
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            pipeline.RunTick(new TickInput(startedAction.executeTick));

            worldState.CreateWriteContext().MoveEntity(PlayerId, new SurfaceCell(FaceId.Floor, 0, 4));
            var recoverTicks = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond)
                .Core
                .CommonSettings
                .RecoverTicks;
            for (var offset = 1; offset < recoverTicks; offset++)
            {
                AssertNoEnemyMovement(pipeline.RunTick(new TickInput(startedAction.executeTick + offset)));
            }

            var afterRecoverEnd = pipeline.RunTick(new TickInput(startedAction.executeTick + recoverTicks));
            if (GetEntity(worldState, EnemyId).aiMode == EnemyAiMode.Recover)
            {
                afterRecoverEnd = pipeline.RunTick(new TickInput(startedAction.executeTick + recoverTicks + 1));
            }

            Assert.That(GetEntity(worldState, EnemyId).aiMode, Is.Not.EqualTo(EnemyAiMode.Recover));
            Assert.That(
                afterRecoverEnd.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == EnemyId) ||
                GetEntity(worldState, EnemyId).position != new SurfaceCell(FaceId.Floor, 0, 0),
                Is.True,
                "Current contract should re-enable ground movement participation when recover expires.");
        }

        [Test]
        [Category("Extended")]
        [TestCase("HpZero")]
        [TestCase("MarkedForDeath")]
        [TestCase("Detached")]
        public void BlackEye_TargetInvalidatedDuringWindup_DoesNotCreateStalePendingImpact(string invalidationKind)
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            InvalidatePlayerDuringWindup(worldState, invalidationKind);

            var executeTick = pipeline.RunTick(new TickInput(startedAction.executeTick));

            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(executeTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);
            Assert.That(executeTick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
            AssertActionInactiveOrMissing(worldState);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ClearsActionState_WhenTopologyMovesOwnerOffBottomFace()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            var suspendedTick = pipeline.RunTick(new TickInput(2));

            AssertActionInactiveOrMissing(worldState);
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(suspendedTick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_DoesNotCreateStalePendingImpact_AfterTopologySuspension()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var originalExecuteTick = GetEnemyActionState(worldState).executeTick;
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));

            for (var tickIndex = 2; tickIndex <= originalExecuteTick; tickIndex++)
            {
                var tick = pipeline.RunTick(new TickInput(tickIndex));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
                Assert.That(tick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
            }
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ReacquiresOrRestartsAction_WhenReturnedToBottomFace()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var originalAction = GetEnemyActionState(worldState);
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            pipeline.RunTick(new TickInput(2));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));

            var restartTick = originalAction.executeTick + 1;
            pipeline.RunTick(new TickInput(restartTick));
            var restartedAction = GetEnemyActionState(worldState);

            Assert.That(restartedAction.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(restartedAction.sequence, Is.GreaterThan(originalAction.sequence));
            Assert.That(restartedAction.startTick, Is.EqualTo(restartTick));
            Assert.That(restartedAction.executeTick, Is.GreaterThan(restartTick));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectileUsesSurfaceCell_NotPlanarCellOnly()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Front, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));

            if (worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action))
            {
                Assert.That(action.kind, Is.Not.EqualTo(EnemyActionKind.ForwardCellProjectile));
            }

            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_DoesNotMutateOccupancy_WhenCreatingPendingImpact()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var action = GetEnemyActionState(worldState);
            var before = DumpOccupancy(worldState.CreateSnapshot());
            pipeline.RunTick(new TickInput(action.executeTick));
            var pendingImpact = GetSinglePendingImpact(worldState);

            Assert.That(pendingImpact.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
            Assert.That(DumpOccupancy(worldState.CreateSnapshot()), Is.EqualTo(before));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_TerrainBlocked_CurrentContract()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var terrainData = new GameplayTerrainData(
                new[]
                {
                    new TerrainCellState(targetCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                });
            var worldState = CreateCombatWorld(targetCell, terrainData: terrainData);

            Assert.That(worldState.CreateSnapshot().IsTerrainBlockedForUnit(targetCell), Is.True);

            var observation = ReleaseForwardCellProjectile(worldState);

            Assert.That(observation.Impact.TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single().TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.AfterOccupancy, Is.EqualTo(observation.BeforeOccupancy));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_SolidBlocked_CurrentContract()
        {
            var solidCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var worldState = CreateCombatWorld(
                targetCell,
                extraEntities: new[] { CreateBox(60, solidCell) });

            Assert.That(
                worldState.CreateSnapshot().TryGetPlacementBlocker(EntityType.Unit, solidCell, EnemyId, out _),
                Is.True);

            var before = DumpOccupancy(worldState.CreateSnapshot());
            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            AssertNoForwardCellProjectileStarted(worldState);
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(tick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
            Assert.That(DumpOccupancy(worldState.CreateSnapshot()), Is.EqualTo(before));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_BoardEdge_CurrentContract()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var snapshot = worldState.CreateSnapshot();

            var resolved = WindupMeleeCombatPoseQueries.TryResolveForwardTargetCell(
                snapshot,
                new SurfaceCell(FaceId.Floor, 4, 0),
                Direction.Right,
                out _);

            Assert.That(resolved, Is.False, "Current forward-cell resolver does not produce an off-board SurfaceCell.");
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_ProjectileOccupied_CurrentContract()
        {
            var projectileCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var worldState = CreateCombatWorld(
                targetCell,
                extraEntities: new[] { CreateProjectile(70, projectileCell) });

            Assert.That(DumpOccupancy(worldState.CreateSnapshot()), Does.Contain($"Projectile:70@{projectileCell};"));

            var observation = ReleaseForwardCellProjectile(worldState);

            Assert.That(observation.Action.lockedTargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.Impact.TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.AfterOccupancy, Is.EqualTo(observation.BeforeOccupancy));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_UnitOccupied_CurrentContract()
        {
            var occupiedCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var worldState = CreateCombatWorld(
                targetCell,
                extraEntities: new[] { CreateUnit(50, 2, occupiedCell, EnemyAiMode.None, Direction.Left, UnitRole.Enemy) });

            Assert.That(DumpOccupancy(worldState.CreateSnapshot()), Does.Contain($"Unit:40@{new SurfaceCell(FaceId.Floor, 0, 0)};50@{occupiedCell};"));

            var observation = ReleaseForwardCellProjectile(worldState);

            Assert.That(observation.Action.lockedTargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.Impact.TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.AfterOccupancy, Is.EqualTo(observation.BeforeOccupancy));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_InactiveFace_CurrentContract()
        {
            var inactiveTargetCell = new SurfaceCell(FaceId.Ceiling, 4, 0);
            var worldState = CreateCombatWorld(inactiveTargetCell);
            var pipeline = CreatePipeline(worldState);

            var tick = pipeline.RunTick(new TickInput(1));

            if (worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action))
            {
                Assert.That(action.kind, Is.Not.EqualTo(EnemyActionKind.ForwardCellProjectile));
            }

            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(tick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_DoesNotFlattenAcrossFaces_CurrentContract()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var frontTerrainCell = new SurfaceCell(FaceId.Front, 4, 0);
            var frontSolidCell = new SurfaceCell(FaceId.Front, 2, 0);
            var frontProjectileCell = new SurfaceCell(FaceId.Front, 3, 0);
            var frontUnitCell = new SurfaceCell(FaceId.Front, 1, 0);
            var terrainData = new GameplayTerrainData(
                new[]
                {
                    new TerrainCellState(frontTerrainCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                });
            var worldState = CreateCombatWorld(
                targetCell,
                extraEntities: new[]
                {
                    CreateUnit(50, 2, frontUnitCell, EnemyAiMode.None, Direction.Left, UnitRole.Enemy),
                    CreateBox(60, frontSolidCell),
                    CreateProjectile(70, frontProjectileCell),
                },
                terrainData: terrainData);

            Assert.That(worldState.CreateSnapshot().IsTerrainBlockedForUnit(frontTerrainCell), Is.True);
            Assert.That(worldState.CreateSnapshot().IsTerrainBlockedForUnit(targetCell), Is.False);

            var observation = ReleaseForwardCellProjectile(worldState);

            Assert.That(observation.Action.lockedTargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.Impact.TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.AfterOccupancy, Is.EqualTo(observation.BeforeOccupancy));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_BlockerPolicy_DoesNotMutateOccupancy_CurrentContract()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var terrainData = new GameplayTerrainData(
                new[]
                {
                    new TerrainCellState(targetCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                });
            var worldState = CreateCombatWorld(
                targetCell,
                extraEntities: new EntityState[]
                {
                    CreateUnit(50, 2, new SurfaceCell(FaceId.Floor, 1, 0), EnemyAiMode.None, Direction.Left, UnitRole.Enemy),
                    CreateProjectile(70, new SurfaceCell(FaceId.Floor, 3, 0)),
                },
                terrainData: terrainData);

            var observation = ReleaseForwardCellProjectile(worldState);

            Assert.That(observation.Impact.TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.AfterOccupancy, Is.EqualTo(observation.BeforeOccupancy));
            Assert.That(observation.ReleaseTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_TargetMovesDuringWindup_CurrentContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Floor, 3, 0);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(lockedCell, movedCell);

            Assert.That(observation.StartedAction.lockedTargetEntityId, Is.EqualTo(PlayerId));
            Assert.That(observation.StartedAction.lockedTargetCell, Is.EqualTo(lockedCell));
            Assert.That(observation.TargetAfterMove.position, Is.EqualTo(movedCell));
            AssertReleasedAtLockedCell(observation, lockedCell, movedCell);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_TargetMovesOutOfTelegraphedCell_CurrentContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Floor, 4, 1);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(lockedCell, movedCell);

            AssertReleasedAtLockedCell(observation, lockedCell, movedCell);
        }

        [Test]
        [Category("Extended")]
        [TestCase((int)FaceId.Front, true)]
        [TestCase((int)FaceId.Ceiling, false)]
        public void BlackEye_TargetMovesAcrossFacesDuringWindup_CurrentContract(int movedFaceValue, bool expectFaceActive)
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell((FaceId)movedFaceValue, 4, 0);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(
                lockedCell,
                movedCell,
                expectMovedFaceActive: expectFaceActive);

            Assert.That(observation.StartedAction.lockedTargetCell, Is.EqualTo(lockedCell));
            Assert.That(observation.TargetAfterMove.position, Is.EqualTo(movedCell));
            Assert.That(observation.TargetAfterMove.position.PlanarPosition, Is.EqualTo(lockedCell.PlanarPosition));
            Assert.That(observation.TargetAfterMove.position.face, Is.Not.EqualTo(lockedCell.face));

            if (expectFaceActive)
            {
                AssertReleasedAtLockedCell(observation, lockedCell, movedCell);
                return;
            }

            Assert.That(observation.HasImpact, Is.False);
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileClearSignals, Is.Empty);
            Assert.That(observation.AfterReleaseOccupancy, Is.EqualTo(observation.AfterMoveOccupancy));
            AssertActionInactiveOrMissing(observation);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_TargetMovesButRemainsValid_DoesNotMutateOccupancy_CurrentContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Floor, 4, 1);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(lockedCell, movedCell);

            Assert.That(observation.BeforeMoveOccupancy, Is.Not.EqualTo(observation.AfterMoveOccupancy));
            Assert.That(observation.AfterReleaseOccupancy, Is.EqualTo(observation.AfterMoveOccupancy));
            Assert.That(observation.ProjectileCountAfterRelease, Is.EqualTo(observation.ProjectileCountBeforeMove));
            AssertReleasedAtLockedCell(observation, lockedCell, movedCell);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_TargetMovesDuringWindup_ReplayDeterminism_CurrentContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Floor, 4, 1);

            var firstReplay = RunTargetMoveDuringWindupReplay(lockedCell, movedCell);
            var secondReplay = RunTargetMoveDuringWindupReplay(lockedCell, movedCell);

            Assert.That(secondReplay.Count, Is.EqualTo(firstReplay.Count));
            for (var i = 0; i < firstReplay.Count; i++)
            {
                Assert.That(secondReplay[i].TickIndex, Is.EqualTo(firstReplay[i].TickIndex), $"Tick index mismatch at frame {i}.");
                Assert.That(secondReplay[i].DeterminismHash, Is.EqualTo(firstReplay[i].DeterminismHash), $"Hash mismatch at frame {i}.");
                Assert.That(secondReplay[i].FinalEntitiesDump, Is.EqualTo(firstReplay[i].FinalEntitiesDump), $"Final entity mismatch at frame {i}.");
                Assert.That(secondReplay[i].ActionDump, Is.EqualTo(firstReplay[i].ActionDump), $"Action state mismatch at frame {i}.");
                Assert.That(secondReplay[i].PendingImpactDump, Is.EqualTo(firstReplay[i].PendingImpactDump), $"Pending impact mismatch at frame {i}.");
                Assert.That(secondReplay[i].ReleaseSignalDump, Is.EqualTo(firstReplay[i].ReleaseSignalDump), $"Release signal mismatch at frame {i}.");
                Assert.That(secondReplay[i].OccupancyDump, Is.EqualTo(firstReplay[i].OccupancyDump), $"Occupancy mismatch at frame {i}.");
            }

            Assert.That(firstReplay.Any(frame => frame.PendingImpactDump.Contains("Target=Floor:4,0")), Is.True);
            Assert.That(firstReplay.Any(frame => frame.ReleaseSignalDump.Contains("Target=Floor:4,0")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_LockedTelegraphCell_StrongContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var observation = ReleaseForwardCellProjectile(CreateCombatWorld(lockedCell));

            Assert.That(observation.Action.lockedTargetEntityId, Is.EqualTo(PlayerId));
            Assert.That(observation.Action.lockedTargetCell, Is.EqualTo(lockedCell));
            Assert.That(observation.Impact.TargetCell, Is.EqualTo(lockedCell));
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals, Has.Count.EqualTo(1));
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single().TargetCell, Is.EqualTo(lockedCell));
            Assert.That(observation.ReleaseTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_TargetMovesSameFace_KeepsLockedCell_StrongContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Floor, 4, 1);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(lockedCell, movedCell);

            Assert.That(observation.TargetAfterMove.position.face, Is.EqualTo(lockedCell.face));
            AssertReleasedAtLockedCell(observation, lockedCell, movedCell);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_TargetMovesActiveOtherFace_KeepsLockedCell_StrongContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Front, 4, 0);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(
                lockedCell,
                movedCell,
                expectMovedFaceActive: true);

            Assert.That(observation.TargetAfterMove.position.face, Is.Not.EqualTo(lockedCell.face));
            Assert.That(observation.TargetAfterMove.position.PlanarPosition, Is.EqualTo(lockedCell.PlanarPosition));
            AssertReleasedAtLockedCell(observation, lockedCell, movedCell);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_TargetMovesInactiveFace_CancelsWithoutPendingImpact_StrongContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Ceiling, 4, 0);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(
                lockedCell,
                movedCell,
                expectMovedFaceActive: false);

            Assert.That(observation.TargetAfterMove.position.face, Is.EqualTo(FaceId.Ceiling));
            Assert.That(observation.HasImpact, Is.False);
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileClearSignals, Is.Empty);
            Assert.That(observation.AfterReleaseOccupancy, Is.EqualTo(observation.AfterMoveOccupancy));
            AssertActionInactiveOrMissing(observation);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_TerrainBlockedTarget_ReleasesCellImpact_StrongContract()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var terrainData = new GameplayTerrainData(
                new[]
                {
                    new TerrainCellState(targetCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                });
            var worldState = CreateCombatWorld(targetCell, terrainData: terrainData);

            var observation = ReleaseForwardCellProjectile(worldState);

            Assert.That(worldState.CreateSnapshot().IsTerrainBlockedForUnit(targetCell), Is.True);
            Assert.That(observation.Impact.TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single().TargetCell, Is.EqualTo(targetCell));
            Assert.That(observation.AfterOccupancy, Is.EqualTo(observation.BeforeOccupancy));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_ReleaseDoesNotCreateProjectileEntityOrMutateOccupancy_StrongContract()
        {
            var lockedCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var movedCell = new SurfaceCell(FaceId.Floor, 4, 1);

            var observation = ReleaseForwardCellProjectileAfterTargetMove(lockedCell, movedCell);

            Assert.That(observation.ProjectileCountBeforeMove, Is.Zero);
            Assert.That(observation.ProjectileCountAfterRelease, Is.Zero);
            Assert.That(observation.BeforeMoveOccupancy, Is.Not.EqualTo(observation.AfterMoveOccupancy));
            Assert.That(observation.AfterReleaseOccupancy, Is.EqualTo(observation.AfterMoveOccupancy));
            AssertReleasedAtLockedCell(observation, lockedCell, movedCell);
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(LoadProfile()).CreateTickPipeline(worldState);
        }

        private static void InvalidatePlayerDuringWindup(WorldState worldState, string invalidationKind)
        {
            var writeContext = worldState.CreateWriteContext();
            switch (invalidationKind)
            {
                case "HpZero":
                    writeContext.ApplyDamage(PlayerId, amount: 99);
                    break;

                case "MarkedForDeath":
                    ((IAttackCommitContext)writeContext).MarkDestroy(PlayerId);
                    break;

                case "Detached":
                    writeContext.SetBoardPresence(PlayerId, EntityBoardPresence.Detached);
                    break;

                default:
                    Assert.Fail($"Unsupported invalidation kind '{invalidationKind}'.");
                    break;
            }
        }

        private static EnemyAiProfile LoadProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(WindupProjectileProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing EnemyAiProfile asset at '{WindupProjectileProfilePath}'.");
            return profile;
        }

        private static WorldState CreateCombatWorld(
            SurfaceCell playerCell,
            IEnumerable<EntityState> extraEntities = null,
            GameplayTerrainData terrainData = null,
            BoardBounds? boardBounds = null,
            CubeTopologyState? topology = null)
        {
            var entities = new List<EntityState>
            {
                CreateUnit(PlayerId, 1, playerCell, EnemyAiMode.None, Direction.Left, UnitRole.Player),
                CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Attack, Direction.Right, UnitRole.Enemy),
            };
            if (extraEntities != null)
            {
                entities.AddRange(extraEntities);
            }

            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                boardBounds ?? new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(4, 4)),
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell cell,
            EnemyAiMode aiMode,
            Direction facing,
            UnitRole unitRole)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }

        private static EntityState CreateProjectile(int entityId, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Projectile,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
            };
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EnemyActionRuntimeState GetEnemyActionState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action), Is.True);
            return action;
        }

        private static PendingCellImpact GetSinglePendingImpact(WorldState worldState)
        {
            var impacts = new List<PendingCellImpactSnapshotEntry>();
            worldState.CreateSnapshot().EnumeratePendingCellImpactsOrdered(impacts);
            return impacts.Single().Impact;
        }

        private static ForwardCellProjectileReleaseObservation ReleaseForwardCellProjectile(WorldState worldState)
        {
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var action = GetEnemyActionState(worldState);
            Assert.That(action.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            var beforeOccupancy = DumpOccupancy(worldState.CreateSnapshot());

            var releaseTick = pipeline.RunTick(new TickInput(action.executeTick));
            var impact = GetSinglePendingImpact(worldState);
            var afterOccupancy = DumpOccupancy(worldState.CreateSnapshot());

            Assert.That(releaseTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(releaseTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.EqualTo(CountProjectiles(worldState.CreateSnapshot())));

            return new ForwardCellProjectileReleaseObservation(
                action,
                releaseTick,
                impact,
                beforeOccupancy,
                afterOccupancy);
        }

        private static TargetMoveDuringWindupObservation ReleaseForwardCellProjectileAfterTargetMove(
            SurfaceCell initialTargetCell,
            SurfaceCell movedTargetCell,
            bool expectMovedFaceActive = true)
        {
            var worldState = CreateCombatWorld(initialTargetCell);
            var pipeline = CreatePipeline(worldState);
            var startTick = pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            var beforeMoveOccupancy = DumpOccupancy(worldState.CreateSnapshot());
            var projectileCountBeforeMove = CountProjectiles(worldState.CreateSnapshot());

            Assert.That(startedAction.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(startedAction.lockedTargetEntityId, Is.EqualTo(PlayerId));
            Assert.That(startedAction.lockedTargetCell, Is.EqualTo(initialTargetCell));
            Assert.That(startedAction.executeTick, Is.GreaterThan(startedAction.startTick));

            worldState.CreateWriteContext().MoveEntity(PlayerId, movedTargetCell);
            var targetAfterMove = GetEntity(worldState, PlayerId);
            AssertTargetMovedButAlive(worldState, targetAfterMove, movedTargetCell, expectMovedFaceActive);
            var afterMoveOccupancy = DumpOccupancy(worldState.CreateSnapshot());

            if (startedAction.executeTick > 2)
            {
                var windupTickAfterMove = pipeline.RunTick(new TickInput(2));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
                Assert.That(windupTickAfterMove.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
                afterMoveOccupancy = DumpOccupancy(worldState.CreateSnapshot());
            }

            var releaseTick = pipeline.RunTick(new TickInput(startedAction.executeTick));
            var afterReleaseOccupancy = DumpOccupancy(worldState.CreateSnapshot());
            var projectileCountAfterRelease = CountProjectiles(worldState.CreateSnapshot());
            var impacts = new List<PendingCellImpactSnapshotEntry>();
            worldState.CreateSnapshot().EnumeratePendingCellImpactsOrdered(impacts);
            var hasActionAfterRelease = worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var actionAfterRelease);

            Assert.That(releaseTick.AttackPhaseResult.DamageResolutions, Is.Empty);

            return new TargetMoveDuringWindupObservation(
                startTick,
                startedAction,
                targetAfterMove,
                releaseTick,
                impacts.Count == 1,
                impacts.Count == 1 ? impacts[0].Impact : default,
                hasActionAfterRelease,
                actionAfterRelease,
                beforeMoveOccupancy,
                afterMoveOccupancy,
                afterReleaseOccupancy,
                projectileCountBeforeMove,
                projectileCountAfterRelease);
        }

        private static IReadOnlyList<TargetMoveReplayFrame> RunTargetMoveDuringWindupReplay(
            SurfaceCell initialTargetCell,
            SurfaceCell movedTargetCell)
        {
            var worldState = CreateCombatWorld(initialTargetCell);
            var pipeline = CreatePipeline(worldState);
            var frames = new List<TargetMoveReplayFrame>();

            frames.Add(CaptureTargetMoveReplayFrame(pipeline.RunTick(new TickInput(1)), worldState));
            var startedAction = GetEnemyActionState(worldState);
            worldState.CreateWriteContext().MoveEntity(PlayerId, movedTargetCell);
            AssertTargetMovedButAlive(worldState, GetEntity(worldState, PlayerId), movedTargetCell, expectMovedFaceActive: true);

            if (startedAction.executeTick > 2)
            {
                frames.Add(CaptureTargetMoveReplayFrame(pipeline.RunTick(new TickInput(2)), worldState));
            }

            frames.Add(CaptureTargetMoveReplayFrame(pipeline.RunTick(new TickInput(startedAction.executeTick)), worldState));
            return frames;
        }

        private static TargetMoveReplayFrame CaptureTargetMoveReplayFrame(TickResult result, WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            return new TargetMoveReplayFrame(
                result.TickIndex,
                result.DeterminismHash,
                DumpFinalEntities(result.FinalEntities),
                DumpActionState(snapshot),
                DumpPendingImpacts(snapshot),
                DumpReleaseSignals(result),
                DumpOccupancy(snapshot));
        }

        private static int CountProjectiles(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            return entities.Count(entity => entity.type == EntityType.Projectile);
        }

        private static void AssertReleasedAtLockedCell(
            in TargetMoveDuringWindupObservation observation,
            SurfaceCell lockedCell,
            SurfaceCell movedCell)
        {
            Assert.That(observation.HasImpact, Is.True);
            Assert.That(observation.Impact.TargetCell, Is.EqualTo(lockedCell));
            Assert.That(observation.Impact.TargetCell, Is.Not.EqualTo(movedCell));
            Assert.That(observation.Impact.ReleaseTick, Is.EqualTo(observation.StartedAction.executeTick));
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals, Has.Count.EqualTo(1));
            Assert.That(
                observation.ReleaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single().TargetCell,
                Is.EqualTo(lockedCell));
            Assert.That(observation.ReleaseTick.PresentationData.ForwardCellProjectileClearSignals, Is.Empty);
            Assert.That(observation.AfterReleaseOccupancy, Is.EqualTo(observation.AfterMoveOccupancy));
            Assert.That(observation.ProjectileCountAfterRelease, Is.EqualTo(observation.ProjectileCountBeforeMove));
            Assert.That(observation.HasActionAfterRelease, Is.True);
            Assert.That(observation.ActionAfterRelease.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(observation.ActionAfterRelease.executionAttempted, Is.True);
            Assert.That(GetEntityAfterTick(observation.ReleaseTick, EnemyId).aiMode, Is.EqualTo(EnemyAiMode.Recover));
        }

        private static void AssertActionInactiveOrMissing(in TargetMoveDuringWindupObservation observation)
        {
            if (!observation.HasActionAfterRelease)
            {
                return;
            }

            Assert.That(observation.ActionAfterRelease.IsActive, Is.False);
        }

        private static void AssertTargetMovedButAlive(
            WorldState worldState,
            in EntityState target,
            SurfaceCell expectedCell,
            bool expectMovedFaceActive)
        {
            Assert.That(target.position, Is.EqualTo(expectedCell));
            Assert.That(target.hp, Is.GreaterThan(0));
            Assert.That(target.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(target.markedForDeath, Is.False);
            Assert.That(worldState.CreateSnapshot().Topology.IsFaceActive(expectedCell.face), Is.EqualTo(expectMovedFaceActive));
        }

        private static EntityState GetEntityAfterTick(TickResult tick, int entityId)
        {
            return tick.FinalEntities.Single(entity => entity.entityId == entityId);
        }

        private static void AssertNoEnemyMovement(TickResult tick)
        {
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(tick.EventLog.Any(line => line.Contains($"MoveCommitted") && line.Contains($"E={EnemyId}")), Is.False);
        }

        private static void AssertActionInactiveOrMissing(WorldState worldState)
        {
            if (!worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action))
            {
                return;
            }

            Assert.That(action.IsActive, Is.False);
        }

        private static void AssertNoForwardCellProjectileStarted(WorldState worldState)
        {
            if (!worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action))
            {
                return;
            }

            Assert.That(action.kind, Is.Not.EqualTo(EnemyActionKind.ForwardCellProjectile));
        }

        private static string DumpOccupancy(WorldSnapshot snapshot)
        {
            var builder = new StringBuilder();
            AppendOccupancy(builder, "Unit", snapshot, (source, buffer) => source.EnumerateUnitOccupancyOrdered(buffer));
            AppendOccupancy(builder, "Solid", snapshot, (source, buffer) => source.EnumerateSolidOccupancyOrdered(buffer));
            AppendOccupancy(builder, "Projectile", snapshot, (source, buffer) => source.EnumerateProjectileOccupancyOrdered(buffer));
            return builder.ToString();
        }

        private static void AppendOccupancy(
            StringBuilder builder,
            string lane,
            WorldSnapshot snapshot,
            System.Action<WorldSnapshot, List<SnapshotOccupancyEntry>> enumerate)
        {
            var entries = new List<SnapshotOccupancyEntry>();
            enumerate(snapshot, entries);
            builder.Append(lane).Append(':');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].EntityId)
                    .Append('@')
                    .Append(entries[i].Cell)
                    .Append(';');
            }

            builder.Append('\n');
        }

        private static string DumpFinalEntities(IReadOnlyList<EntityState> entities)
        {
            var builder = new StringBuilder();
            foreach (var entity in entities.OrderBy(entity => entity.entityId))
            {
                builder
                    .Append("E=").Append(entity.entityId)
                    .Append("|Type=").Append(entity.type)
                    .Append("|Cell=").Append(FormatCell(entity.position))
                    .Append("|Hp=").Append(entity.hp)
                    .Append("|Presence=").Append(entity.boardPresence)
                    .Append("|Marked=").Append(entity.markedForDeath ? 1 : 0)
                    .Append("|Ai=").Append(entity.aiMode)
                    .Append('\n');
            }

            return builder.Length == 0 ? "<empty>" : builder.ToString();
        }

        private static string DumpActionState(WorldSnapshot snapshot)
        {
            if (!snapshot.TryGetEnemyActionState(EnemyId, out var action))
            {
                return "<empty>";
            }

            return new StringBuilder()
                .Append("Kind=").Append(action.kind)
                .Append("|Seq=").Append(action.sequence)
                .Append("|TargetId=").Append(action.lockedTargetEntityId)
                .Append("|Start=").Append(action.startTick)
                .Append("|Execute=").Append(action.executeTick)
                .Append("|Attempted=").Append(action.executionAttempted ? 1 : 0)
                .Append("|HasForward=").Append(action.hasLockedForwardCellImpact ? 1 : 0)
                .Append("|Locked=").Append(FormatCell(action.lockedTargetCell))
                .ToString();
        }

        private static string DumpPendingImpacts(WorldSnapshot snapshot)
        {
            var impacts = new List<PendingCellImpactSnapshotEntry>();
            snapshot.EnumeratePendingCellImpactsOrdered(impacts);
            if (impacts.Count == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < impacts.Count; i++)
            {
                var impact = impacts[i].Impact;
                builder
                    .Append("Id=").Append(impact.ImpactId)
                    .Append("|Owner=").Append(impact.OwnerId)
                    .Append("|Target=").Append(FormatCell(impact.TargetCell))
                    .Append("|Release=").Append(impact.ReleaseTick)
                    .Append("|Impact=").Append(impact.ImpactTick)
                    .Append('\n');
            }

            return builder.ToString();
        }

        private static string DumpReleaseSignals(TickResult tick)
        {
            var signals = tick.PresentationData.ForwardCellProjectileReleaseSignals;
            if (signals.Count == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                builder
                    .Append("Impact=").Append(signal.ImpactId)
                    .Append("|Owner=").Append(signal.OwnerId)
                    .Append("|Source=").Append(FormatCell(signal.SourceCell))
                    .Append("|Target=").Append(FormatCell(signal.TargetCell))
                    .Append("|Release=").Append(signal.ReleaseTick)
                    .Append("|Impact=").Append(signal.ImpactTick)
                    .Append('\n');
            }

            return builder.ToString();
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return $"{cell.face}:{cell.x},{cell.y}";
        }

        private readonly struct ForwardCellProjectileReleaseObservation
        {
            public ForwardCellProjectileReleaseObservation(
                in EnemyActionRuntimeState action,
                TickResult releaseTick,
                in PendingCellImpact impact,
                string beforeOccupancy,
                string afterOccupancy)
            {
                Action = action;
                ReleaseTick = releaseTick;
                Impact = impact;
                BeforeOccupancy = beforeOccupancy;
                AfterOccupancy = afterOccupancy;
            }

            public EnemyActionRuntimeState Action { get; }

            public TickResult ReleaseTick { get; }

            public PendingCellImpact Impact { get; }

            public string BeforeOccupancy { get; }

            public string AfterOccupancy { get; }
        }

        private readonly struct TargetMoveDuringWindupObservation
        {
            public TargetMoveDuringWindupObservation(
                TickResult startTick,
                in EnemyActionRuntimeState startedAction,
                in EntityState targetAfterMove,
                TickResult releaseTick,
                bool hasImpact,
                in PendingCellImpact impact,
                bool hasActionAfterRelease,
                in EnemyActionRuntimeState actionAfterRelease,
                string beforeMoveOccupancy,
                string afterMoveOccupancy,
                string afterReleaseOccupancy,
                int projectileCountBeforeMove,
                int projectileCountAfterRelease)
            {
                StartTick = startTick;
                StartedAction = startedAction;
                TargetAfterMove = targetAfterMove;
                ReleaseTick = releaseTick;
                HasImpact = hasImpact;
                Impact = impact;
                HasActionAfterRelease = hasActionAfterRelease;
                ActionAfterRelease = actionAfterRelease;
                BeforeMoveOccupancy = beforeMoveOccupancy;
                AfterMoveOccupancy = afterMoveOccupancy;
                AfterReleaseOccupancy = afterReleaseOccupancy;
                ProjectileCountBeforeMove = projectileCountBeforeMove;
                ProjectileCountAfterRelease = projectileCountAfterRelease;
            }

            public TickResult StartTick { get; }

            public EnemyActionRuntimeState StartedAction { get; }

            public EntityState TargetAfterMove { get; }

            public TickResult ReleaseTick { get; }

            public bool HasImpact { get; }

            public PendingCellImpact Impact { get; }

            public bool HasActionAfterRelease { get; }

            public EnemyActionRuntimeState ActionAfterRelease { get; }

            public string BeforeMoveOccupancy { get; }

            public string AfterMoveOccupancy { get; }

            public string AfterReleaseOccupancy { get; }

            public int ProjectileCountBeforeMove { get; }

            public int ProjectileCountAfterRelease { get; }
        }

        private readonly struct TargetMoveReplayFrame
        {
            public TargetMoveReplayFrame(
                int tickIndex,
                string determinismHash,
                string finalEntitiesDump,
                string actionDump,
                string pendingImpactDump,
                string releaseSignalDump,
                string occupancyDump)
            {
                TickIndex = tickIndex;
                DeterminismHash = determinismHash;
                FinalEntitiesDump = finalEntitiesDump;
                ActionDump = actionDump;
                PendingImpactDump = pendingImpactDump;
                ReleaseSignalDump = releaseSignalDump;
                OccupancyDump = occupancyDump;
            }

            public int TickIndex { get; }

            public string DeterminismHash { get; }

            public string FinalEntitiesDump { get; }

            public string ActionDump { get; }

            public string PendingImpactDump { get; }

            public string ReleaseSignalDump { get; }

            public string OccupancyDump { get; }
        }
    }
}
