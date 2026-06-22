using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class GliderAirborneP0CoreTests
    {
        private const int PlayerId = 10;
        private const int GliderId = 40;
        private const int WallId = 30;
        private const int Stage31GliderId = 241;
        private const int Stage31WallId = 238;
        private const string GlideActiveKinematicAnchorCommitReason = "GlideActiveKinematicAnchorCommit";

        [Test]
        [Category("Core")]
        public void Glider_ExpiredActive_OnSolid_ForMultipleTicks_DoesNotEnterRecoverOrSeekLanding()
        {
            var first = RunExpiredActiveOnSolidLongRun();
            var second = RunExpiredActiveOnSolidLongRun();

            CollectionAssert.AreEqual(first.TickHashes, second.TickHashes);
            CollectionAssert.AreEqual(first.TickTraces, second.TickTraces);
            Assert.That(first.FinalState, Is.EqualTo(second.FinalState));
        }

        [Test]
        [Category("Core")]
        public void Glider_WantsRecover_MovesFromSolidToNonSolid_DoesNotRecoverInSameMovementTick()
        {
            var solidCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var nonSolidCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(WallId, solidCell),
                CreateUnit(PlayerId, teamId: 1, new SurfaceCell(FaceId.Floor, 4, 1), EnemyAiMode.None),
                CreateUnit(GliderId, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 1), EnemyAiMode.Chase),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                GliderId,
                CreateActiveGlide(
                    activeUntilTickExclusive: 2,
                    durationTicks: 1,
                    cooldownTicks: 0,
                    recoveryTicks: 2,
                    glideMoveTicks: 6,
                    wantsRecover: true,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: PlayerId));
            writeContext.MoveEntity(GliderId, solidCell);
            writeContext.SetUnitKinematicState(
                GliderId,
                CreateVoluntaryStepState(
                    solidCell,
                    elapsedTicks: 2,
                    totalTicks: 6,
                    startedTick: 1,
                    stepDirectionX: 1,
                    stepDirectionY: 0));

            var pipeline = CreateGlidePipeline(worldState, glideMoveTicks: 6);
            var movementTick = pipeline.RunTick(new TickInput(3));

            Assert.That(HasMoveEntity(movementTick, GliderId, nonSolidCell), Is.True);
            Assert.That(HasGlideRecoveryTransition(movementTick, GliderId), Is.False);
            AssertNoSameTickMoveAndRecovery(movementTick, GliderId);
            AssertActiveWantsRecover(worldState, GliderId);
            AssertEntityAt(worldState, GliderId, nonSolidCell);

            var nextBoundary = pipeline.RunTick(new TickInput(4));

            _ = nextBoundary;
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var recovered), Is.True);
            Assert.That(recovered.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
        }

        [Test]
        [Category("Core")]
        public void Glider_WantsRecover_UsesSurfaceCellNotPlanarCellForSolidGateAfterTopologyChange()
        {
            var faceASolidOutcome = RunFaceAwareRecoverGateCase(
                gliderCell: new SurfaceCell(FaceId.Floor, 1, 1),
                solidCell: new SurfaceCell(FaceId.Floor, 1, 1),
                topologyFace: FaceId.Floor);
            var faceBNonSolidOutcome = RunFaceAwareRecoverGateCase(
                gliderCell: new SurfaceCell(FaceId.Back, 1, 1),
                solidCell: new SurfaceCell(FaceId.Floor, 1, 1),
                topologyFace: FaceId.Back);
            var repeatedFaceBNonSolidOutcome = RunFaceAwareRecoverGateCase(
                gliderCell: new SurfaceCell(FaceId.Back, 1, 1),
                solidCell: new SurfaceCell(FaceId.Floor, 1, 1),
                topologyFace: FaceId.Back);

            Assert.That(faceASolidOutcome.Phase, Is.EqualTo(EnemyGlidePhase.Active));
            Assert.That(faceASolidOutcome.WantsRecover, Is.True);
            Assert.That(faceBNonSolidOutcome.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
            Assert.That(faceBNonSolidOutcome, Is.EqualTo(repeatedFaceBNonSolidOutcome));
        }

        [Test]
        [Category("Core")]
        public void Glider_ExpiredActiveOnSolid_TopologySuspend_DoesNotEnterRecoverOrMove()
        {
            var solidCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateExpiredActiveOnSolidWorld(solidCell);
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Back));

            var pipeline = CreateGlidePipeline(worldState);
            var tick = pipeline.RunTick(new TickInput(3));

            Assert.That(HasMoveEntity(tick, GliderId), Is.False);
            Assert.That(HasGlideRecoveryTransition(tick, GliderId), Is.False);
            Assert.That(HasGlideActiveKinematicAnchorCommit(tick, GliderId), Is.False);
            AssertActiveWantsRecover(worldState, GliderId);
            AssertEntityAt(worldState, GliderId, solidCell);
        }

        [Test]
        [Category("Core")]
        public void Glider_ExpiredActiveOnSolid_TopologyResumeOnSolid_RemainsActiveWithWantsRecover()
        {
            var solidCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateExpiredActiveOnSolidWorld(solidCell);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetTopology(new CubeTopologyState(FaceId.Back));
            CommitPreMovement(CreateGlideLogic(), worldState, tickIndex: 3);
            writeContext.SetTopology(new CubeTopologyState(FaceId.Floor));

            var pipeline = CreateGlidePipeline(worldState);
            var tick = pipeline.RunTick(new TickInput(4));

            Assert.That(HasGlideRecoveryTransition(tick, GliderId), Is.False);
            Assert.That(tick.EventLog, Has.None.Contains("|Label=Start|"));
            Assert.That(tick.EventLog, Has.None.Contains("|Label=EnterActive|"));
            AssertActiveWantsRecover(worldState, GliderId);
            AssertActiveSolidPlacementAllowed(worldState, solidCell, GliderId);
        }

        [Test]
        [Category("Core")]
        public void Glider_TopologyChurnAcrossWindupActiveExpiredRecover_PreservesPhaseAndDeterminism()
        {
            var first = RunTopologyChurnScenario();
            var second = RunTopologyChurnScenario();

            CollectionAssert.AreEqual(first.TickHashes, second.TickHashes);
            CollectionAssert.AreEqual(first.TickTraces, second.TickTraces);
            Assert.That(first.FinalState, Is.EqualTo(second.FinalState));
        }

        [Test]
        [Category("Core")]
        public void Stage31_Glider241_StageShapedFallback_ActivePassesWall238StepByStep()
        {
            var first = RunStage31ShapedFallbackPass();
            var second = RunStage31ShapedFallbackPass();

            CollectionAssert.AreEqual(first.TickHashes, second.TickHashes);
            CollectionAssert.AreEqual(first.TickTraces, second.TickTraces);
            Assert.That(first.FinalState, Is.EqualTo(second.FinalState));
        }

        [Test]
        [Category("Core")]
        public void Glider_WindupAndRecover_LockMovementAndKeepFacing()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(GliderId, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase, Direction.Left),
            });
            var logic = CreateGlideLogic(new EnemyGlideTimingSettings(
                initialDelayTicks: 0,
                windupTicks: 3,
                durationTicks: 3,
                recoveryTicks: 3,
                cooldownTicks: 0,
                glideMoveTicks: 2));

            foreach (var phase in new[] { EnemyGlidePhase.Windup, EnemyGlidePhase.Recovery })
            {
                var movementIntents = new List<RawMovementIntent>();
                worldState.CreateWriteContext().SetEnemyGlideState(
                    GliderId,
                    CreateGlideState(
                        phase,
                        windupUntilTickExclusive: phase == EnemyGlidePhase.Windup ? 5 : 0,
                        recoveryUntilTickExclusive: phase == EnemyGlidePhase.Recovery ? 5 : 0,
                        windupTicks: 3,
                        durationTicks: 3,
                        recoveryTicks: 3));

                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(2), movementIntents);

                Assert.That(movementIntents, Is.Empty);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(GliderId, out var glider), Is.True);
                Assert.That(glider.facing, Is.EqualTo(Direction.Left));
            }
        }

        [Test]
        [Category("Core")]
        public void Glider_Active_BoxOverlapSmoke_DoesNotMoveBoxOrTriggerRecovery()
        {
            var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(20, boxCell),
                CreateUnit(PlayerId, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(GliderId, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                GliderId,
                CreateActiveGlide(
                    activeUntilTickExclusive: 6,
                    durationTicks: 5,
                    cooldownTicks: 0,
                    recoveryTicks: 2,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: PlayerId));

            Assert.DoesNotThrow(() => writeContext.MoveEntity(GliderId, boxCell));
            AssertEntityAt(worldState, GliderId, boxCell);
            AssertEntityAt(worldState, 20, boxCell);
            AssertActiveSolidPlacementAllowed(worldState, boxCell, GliderId);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var glide), Is.True);
            Assert.That(glide.Phase, Is.EqualTo(EnemyGlidePhase.Active));
            Assert.That(glide.WantsRecover, Is.False);
        }

        private static LongRunOutcome RunExpiredActiveOnSolidLongRun()
        {
            var solidCell = new SurfaceCell(FaceId.Floor, -4, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(WallId, solidCell),
                CreateUnit(50, teamId: 1, new SurfaceCell(FaceId.Floor, 0, 1), EnemyAiMode.None),
                CreateUnit(PlayerId, teamId: 1, new SurfaceCell(FaceId.Floor, 4, 1), EnemyAiMode.None),
                CreateUnit(GliderId, teamId: 2, new SurfaceCell(FaceId.Floor, -3, 1), EnemyAiMode.Patrol, Direction.Left),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                GliderId,
                CreateActiveGlide(
                    activeUntilTickExclusive: 2,
                    durationTicks: 1,
                    cooldownTicks: 0,
                    recoveryTicks: 2,
                    wantsRecover: true,
                    lockedStepX: -1,
                    lockedStepY: 0,
                    lockedTargetEntityId: PlayerId));
            writeContext.MoveEntity(GliderId, solidCell);
            writeContext.SetEntityExecutionLockState(
                GliderId,
                new EntityExecutionLockState
                {
                    phase = EntityExecutionPhase.Attack,
                    sequence = 1,
                    unlockTickExclusive = 20,
                });

            var pipeline = CreateGlidePipeline(worldState);
            var hashes = new List<string>();
            var traces = new List<string>();

            for (var tickIndex = 3; tickIndex <= 8; tickIndex++)
            {
                var tick = pipeline.RunTick(new TickInput(tickIndex));
                hashes.Add(tick.DeterminismHash);
                traces.Add(tick.Trace.Text);

                Assert.That(HasGlideRecoveryTransition(tick, GliderId), Is.False);
                Assert.That(HasMoveEntity(tick, GliderId), Is.False);
                AssertNoLandingSeekTrace(tick);
                AssertActiveWantsRecover(worldState, GliderId);
                AssertEntityAt(worldState, GliderId, solidCell);
                AssertActiveSolidPlacementAllowed(worldState, solidCell, GliderId);
            }

            return new LongRunOutcome(hashes, traces, DumpGliderState(worldState));
        }

        private static FaceGateOutcome RunFaceAwareRecoverGateCase(
            SurfaceCell gliderCell,
            SurfaceCell solidCell,
            FaceId topologyFace)
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(WallId, solidCell),
                CreateUnit(PlayerId, teamId: 1, new SurfaceCell(topologyFace, 4, 1), EnemyAiMode.None),
                CreateUnit(GliderId, teamId: 2, CreateNonOverlappingSeedCell(gliderCell, solidCell), EnemyAiMode.Chase),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetTopology(new CubeTopologyState(topologyFace));
            writeContext.SetEnemyGlideState(
                GliderId,
                CreateActiveGlide(
                    activeUntilTickExclusive: 2,
                    durationTicks: 1,
                    cooldownTicks: 0,
                    recoveryTicks: 2,
                    wantsRecover: true,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: PlayerId));
            writeContext.MoveEntity(GliderId, gliderCell);

            CommitPreMovement(CreateGlideLogic(), worldState, tickIndex: 3);

            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var state), Is.True);
            return new FaceGateOutcome(state.Phase, state.WantsRecover, state.RecoveryUntilTickExclusive);
        }

        private static LongRunOutcome RunTopologyChurnScenario()
        {
            var solidCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var nonSolidCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(WallId, solidCell),
                CreateUnit(PlayerId, teamId: 1, new SurfaceCell(FaceId.Floor, 4, 1), EnemyAiMode.None),
                CreateUnit(GliderId, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 1), EnemyAiMode.Chase),
            });
            var logic = CreateGlideLogic(new EnemyGlideTimingSettings(
                initialDelayTicks: 0,
                windupTicks: 1,
                durationTicks: 1,
                recoveryTicks: 2,
                cooldownTicks: 1,
                glideMoveTicks: 6));
            var hashes = new List<string>();
            var traces = new List<string>();

            var windupUpdates = CommitPreMovementAndGetUpdates(logic, worldState, tickIndex: 1);
            Assert.That(windupUpdates, Has.Some.Contains("|Label=Start|"));
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var windup), Is.True);
            Assert.That(windup.Phase, Is.EqualTo(EnemyGlidePhase.Windup));

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Back));
            CommitPreMovement(logic, worldState, tickIndex: 2);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var suspendedWindup), Is.True);
            Assert.That(suspendedWindup.Phase, Is.EqualTo(EnemyGlidePhase.Windup));

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            CommitPreMovement(logic, worldState, tickIndex: 3);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var active), Is.True);
            Assert.That(active.Phase, Is.EqualTo(EnemyGlidePhase.Active));
            worldState.CreateWriteContext().MoveEntity(GliderId, solidCell);
            CommitPreMovement(logic, worldState, tickIndex: 4);
            AssertActiveWantsRecover(worldState, GliderId);

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Back));
            CommitPreMovement(logic, worldState, tickIndex: 5);
            AssertActiveWantsRecover(worldState, GliderId);
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));

            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(
                GliderId,
                CreateVoluntaryStepState(
                    solidCell,
                    elapsedTicks: 2,
                    totalTicks: 6,
                    startedTick: 5,
                    stepDirectionX: 1,
                    stepDirectionY: 0));
            var pipeline = CreateGlidePipeline(worldState, glideMoveTicks: 6);

            var moveTick = pipeline.RunTick(new TickInput(6));
            hashes.Add(moveTick.DeterminismHash);
            traces.Add(moveTick.Trace.Text);
            Assert.That(HasMoveEntity(moveTick, GliderId, nonSolidCell), Is.True);
            Assert.That(HasGlideRecoveryTransition(moveTick, GliderId), Is.False);
            AssertNoSameTickMoveAndRecovery(moveTick, GliderId);
            AssertActiveWantsRecover(worldState, GliderId);

            var recoverTick = pipeline.RunTick(new TickInput(7));
            hashes.Add(recoverTick.DeterminismHash);
            traces.Add(recoverTick.Trace.Text);
            _ = recoverTick;
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var recovery), Is.True);
            Assert.That(recovery.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Back));
            var suspendedRecoveryTick = pipeline.RunTick(new TickInput(8));
            hashes.Add(suspendedRecoveryTick.DeterminismHash);
            traces.Add(suspendedRecoveryTick.Trace.Text);
            Assert.That(HasMoveEntity(suspendedRecoveryTick, GliderId), Is.False);

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            var cooldownTick = pipeline.RunTick(new TickInput(9));
            hashes.Add(cooldownTick.DeterminismHash);
            traces.Add(cooldownTick.Trace.Text);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(GliderId, out var cooldown), Is.True);
            Assert.That(cooldown.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
            AssertNoNonActiveUnitSolidOverlap(worldState);

            return new LongRunOutcome(hashes, traces, DumpGliderState(worldState));
        }

        private static LongRunOutcome RunStage31ShapedFallbackPass()
        {
            var previousCell = new SurfaceCell(FaceId.Floor, 2, 7);
            var wall238Cell = new SurfaceCell(FaceId.Floor, 3, 7);
            var nextNonSolidCell = new SurfaceCell(FaceId.Floor, 4, 7);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(Stage31WallId, wall238Cell),
                    CreateUnit(PlayerId, teamId: 1, new SurfaceCell(FaceId.Floor, 5, 7), EnemyAiMode.None),
                    CreateUnit(Stage31GliderId, teamId: 2, previousCell, EnemyAiMode.Chase),
                },
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(8, 8)));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                Stage31GliderId,
                CreateActiveGlide(
                    activeUntilTickExclusive: 20,
                    durationTicks: 18,
                    cooldownTicks: 0,
                    recoveryTicks: 2,
                    glideMoveTicks: 6,
                    wantsRecover: false,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: PlayerId));
            var hashes = new List<string>();
            var traces = new List<string>();

            hashes.Add(DumpGliderState(worldState, Stage31GliderId));
            traces.Add(DumpOccupancy(worldState, previousCell, wall238Cell, nextNonSolidCell));

            AssertEntityAt(worldState, Stage31GliderId, previousCell);
            Assert.DoesNotThrow(() => writeContext.MoveEntity(Stage31GliderId, wall238Cell));
            AssertEntityAt(worldState, Stage31GliderId, wall238Cell);
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(wall238Cell, out _), Is.True);
            AssertActiveSolidPlacementAllowed(worldState, wall238Cell, Stage31GliderId);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(Stage31GliderId, out var onWallGlide), Is.True);
            Assert.That(onWallGlide.Phase, Is.EqualTo(EnemyGlidePhase.Active));

            hashes.Add(DumpGliderState(worldState, Stage31GliderId));
            traces.Add(DumpOccupancy(worldState, previousCell, wall238Cell, nextNonSolidCell));

            Assert.DoesNotThrow(() => writeContext.MoveEntity(Stage31GliderId, nextNonSolidCell));
            AssertEntityAt(worldState, Stage31GliderId, nextNonSolidCell);
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(wall238Cell, out _), Is.True);

            hashes.Add(DumpGliderState(worldState, Stage31GliderId));
            traces.Add(DumpOccupancy(worldState, previousCell, wall238Cell, nextNonSolidCell));

            return new LongRunOutcome(hashes, traces, DumpGliderState(worldState, Stage31GliderId));
        }

        private static WorldState CreateExpiredActiveOnSolidWorld(SurfaceCell solidCell)
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(WallId, solidCell),
                CreateUnit(PlayerId, teamId: 1, new SurfaceCell(FaceId.Floor, 4, 1), EnemyAiMode.None),
                CreateUnit(GliderId, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 1), EnemyAiMode.Chase),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                GliderId,
                CreateActiveGlide(
                    activeUntilTickExclusive: 2,
                    durationTicks: 1,
                    cooldownTicks: 0,
                    recoveryTicks: 2,
                    wantsRecover: true,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: PlayerId));
            writeContext.MoveEntity(GliderId, solidCell);
            return worldState;
        }

        private static SurfaceCell CreateNonOverlappingSeedCell(SurfaceCell targetCell, SurfaceCell solidCell)
        {
            if (targetCell != solidCell)
            {
                return targetCell;
            }

            return new SurfaceCell(targetCell.face, targetCell.x + 1, targetCell.y);
        }

        private static TickPipeline CreateGlidePipeline(WorldState worldState, int glideMoveTicks = 2)
        {
            var timing = new EnemyGlideTimingSettings(
                initialDelayTicks: 0,
                windupTicks: 0,
                durationTicks: 1,
                recoveryTicks: 2,
                cooldownTicks: 0,
                glideMoveTicks: glideMoveTicks);
            var definition = CreateGlideDefinition(timing);
            return new GameplayBootstrapper(GameplayEntityLogicProviderFactory.CreateDefault(definition))
                .CreateTickPipeline(
                    worldState,
                    Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    CreatePlayerTiming(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                    unitKinematicLocomotionTiming: CreateKinematicTiming(glideMoveTicks));
        }

        private static EnemyLogic CreateGlideLogic()
        {
            return CreateGlideLogic(new EnemyGlideTimingSettings(
                initialDelayTicks: 0,
                windupTicks: 0,
                durationTicks: 1,
                recoveryTicks: 2,
                cooldownTicks: 0,
                glideMoveTicks: 2));
        }

        private static EnemyLogic CreateGlideLogic(EnemyGlideTimingSettings timing)
        {
            return new EnemyLogic(GliderId, CreateGlideDefinition(timing));
        }

        private static EnemyAiRuntimeDefinition CreateGlideDefinition(EnemyGlideTimingSettings timing)
        {
            return new EnemyAiRuntimeDefinition(
                new EnemyCoreRuntime(
                    new EnemyAiCommonSettings(movementPriority: 50, attackPriority: 50, recoverTicks: 1),
                    new EnemyLocomotionTimingSettings(moveCooldownTicks: 0)),
                new EnemyBrainRuntime(
                    new EnemyStateResolverRuntime(EnemyAiStateResolverKind.Default, DefaultEnemyAiStateResolver.Instance),
                    new EnemyPatrolRuntime(PatrolStrategyKind.Forward, PatrolSettings.CreateDefault(), ForwardPatrolStrategy.Instance),
                    new EnemyDetectionRuntime(DetectionStrategyKind.NearestOpponent, DetectionSettings.CreateStandardEnemyDetection(), NearestOpponentDetectionStrategy.Instance),
                    new EnemyChaseRuntime(ChaseStrategyKind.AxisPriority, ChaseSettings.CreateDefault(), AxisPriorityChaseStrategy.Instance)),
                new EnemyCapabilityRuntimeSet(
                    null,
                    new EnemyMovementSkillCapabilityRuntime(
                        MovementSkillStrategyKind.GlideOverSolid,
                        EnemyJumpTimingSettings.CreateDefault(),
                        timing),
                    null,
                    null));
        }

        private static void CommitPreMovement(EnemyLogic logic, WorldState worldState, int tickIndex)
        {
            _ = CommitPreMovementAndGetUpdates(logic, worldState, tickIndex);
        }

        private static List<string> CommitPreMovementAndGetUpdates(EnemyLogic logic, WorldState worldState, int tickIndex)
        {
            var updates = new List<string>();
            ((IPreMovementStateLogic)logic).CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(tickIndex),
                worldState.CreateWriteContext(),
                updates,
                new List<PlayerActionTransition>());
            return updates;
        }

        private static EnemyGlideRuntimeState CreateActiveGlide(
            int activeUntilTickExclusive,
            int durationTicks,
            int cooldownTicks,
            int recoveryTicks,
            int glideMoveTicks = 2,
            bool wantsRecover = false,
            int lockedStepX = 1,
            int lockedStepY = 0,
            int lockedTargetEntityId = 0)
        {
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Active,
                sequence: 1,
                windupUntilTickExclusive: 0,
                activeUntilTickExclusive: activeUntilTickExclusive,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: 0,
                durationTicks: durationTicks,
                recoveryTicks: recoveryTicks,
                cooldownTicks: cooldownTicks,
                glideMoveTicks: glideMoveTicks,
                lastExitedTick: 0,
                wantsRecover: wantsRecover,
                hasLockedStep: true,
                lockedStepX: lockedStepX,
                lockedStepY: lockedStepY,
                lockedTargetEntityId: lockedTargetEntityId);
        }

        private static EnemyGlideRuntimeState CreateGlideState(
            EnemyGlidePhase phase,
            int windupUntilTickExclusive = 0,
            int activeUntilTickExclusive = 0,
            int recoveryUntilTickExclusive = 0,
            int windupTicks = 0,
            int durationTicks = 0,
            int recoveryTicks = 0)
        {
            return EnemyGlideRuntimeState.Create(
                phase,
                sequence: 1,
                windupUntilTickExclusive: windupUntilTickExclusive,
                activeUntilTickExclusive: activeUntilTickExclusive,
                recoveryUntilTickExclusive: recoveryUntilTickExclusive,
                cooldownUntilTickExclusive: 0,
                windupTicks: windupTicks,
                durationTicks: durationTicks,
                recoveryTicks: recoveryTicks,
                cooldownTicks: 0,
                glideMoveTicks: 2,
                lastExitedTick: 0,
                wantsRecover: false,
                hasLockedStep: true,
                lockedStepX: 1,
                lockedStepY: 0,
                lockedTargetEntityId: PlayerId);
        }

        private static UnitKinematicRuntimeState CreateVoluntaryStepState(
            SurfaceCell anchor,
            int elapsedTicks,
            int totalTicks,
            int startedTick,
            int stepDirectionX,
            int stepDirectionY)
        {
            var resolution = KinematicProgressResolver.ResolvePose(
                anchor,
                stepDirectionX,
                stepDirectionY,
                elapsedTicks,
                totalTicks);
            return new UnitKinematicRuntimeState
            {
                localOffset = resolution.LocalOffset,
                velocity = new SimulationVelocity2(
                    KinematicFixed.FromRaw(stepDirectionX * KinematicFixed.UnitsPerCell / totalTicks),
                    KinematicFixed.FromRaw(stepDirectionY * KinematicFixed.UnitsPerCell / totalTicks)),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = resolution.RemainingDistanceUnits,
                remainingTicks = resolution.RemainingTicks,
                speedScalePermille = 1000,
                sequenceId = 1,
                elapsedTicks = elapsedTicks,
                totalTicks = totalTicks,
                commitTick = totalTicks / 2,
                startedTick = startedTick,
                stepDirectionX = stepDirectionX,
                stepDirectionY = stepDirectionY,
            }.NormalizedForStorage();
        }

        private static bool HasMoveEntity(TickResult result, int entityId)
        {
            return result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MoveEntity &&
                operation.EntityId == entityId);
        }

        private static bool HasMoveEntity(TickResult result, int entityId, SurfaceCell destination)
        {
            return result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MoveEntity &&
                operation.EntityId == entityId &&
                operation.Destination == destination);
        }

        private static bool HasGlideRecoveryTransition(TickResult result, int entityId)
        {
            return result.EventLog.Any(entry =>
                       entry.Contains($"EnemyGlideStateUpdated|E={entityId}|Label=EnterRecovery|", StringComparison.Ordinal)) ||
                   result.Trace.Text.Contains($"SetEnemyGlideState|E={entityId}", StringComparison.Ordinal) &&
                   result.Trace.Text.Contains("Phase=Recovery", StringComparison.Ordinal);
        }

        private static bool HasGlideActiveKinematicAnchorCommit(TickResult result, int entityId)
        {
            return result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MoveEntity &&
                operation.EntityId == entityId &&
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LocomotionAnchorCommit &&
                operation.Metadata.BoundaryReason == GlideActiveKinematicAnchorCommitReason);
        }

        private static void AssertNoSameTickMoveAndRecovery(TickResult result, int entityId)
        {
            Assert.That(
                HasMoveEntity(result, entityId) && HasGlideRecoveryTransition(result, entityId),
                Is.False,
                "Same tick mixed MoveEntity with SetGlideState(Recovery).");
        }

        private static void AssertActiveWantsRecover(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(entityId, out var state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
            Assert.That(state.WantsRecover, Is.True);
            Assert.That(state.IsActive, Is.True);
        }

        private static void AssertEntityAt(WorldState worldState, int entityId, SurfaceCell expectedCell)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(expectedCell));
        }

        private static void AssertActiveSolidPlacementAllowed(WorldState worldState, SurfaceCell solidCell, int entityId)
        {
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    worldState.CreateSnapshot(),
                    EntityType.Unit,
                    solidCell,
                    entityId).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        private static void AssertNoLandingSeekTrace(TickResult result)
        {
            Assert.That(result.Trace.Text, Does.Not.Contain("LandingSeek"));
            Assert.That(result.Trace.Text, Does.Not.Contain("Egress"));
            Assert.That(result.Trace.Text, Does.Not.Contain("Snap"));
        }

        private static void AssertNoNonActiveUnitSolidOverlap(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            foreach (var entity in entities)
            {
                if (entity.type != EntityType.Unit ||
                    entity.boardPresence != EntityBoardPresence.Occupying ||
                    !snapshot.TryGetSolidSemanticAt(entity.position, out _))
                {
                    continue;
                }

                if (snapshot.TryGetEnemyGlideState(entity.entityId, out var glideState) &&
                    glideState.Phase == EnemyGlidePhase.Active)
                {
                    continue;
                }

                Assert.Fail($"Non-active unit {entity.entityId} overlaps solid at {entity.position}.");
            }
        }

        private static string DumpGliderState(WorldState worldState)
        {
            return DumpGliderState(worldState, GliderId);
        }

        private static string DumpGliderState(WorldState worldState, int entityId)
        {
            var snapshot = worldState.CreateSnapshot();
            var builder = new StringBuilder();
            if (snapshot.TryGetEntity(entityId, out var entity))
            {
                builder.Append("Entity=");
                builder.Append(entity.position);
                builder.Append("|Presence=");
                builder.Append(entity.boardPresence);
                builder.Append("|Ai=");
                builder.Append(entity.aiMode);
            }

            if (snapshot.TryGetEnemyGlideState(entityId, out var glide))
            {
                builder.Append("|Glide=");
                builder.Append(glide.Phase);
                builder.Append("|Wants=");
                builder.Append(glide.WantsRecover ? 1 : 0);
                builder.Append("|Seq=");
                builder.Append(glide.Sequence);
                builder.Append("|ActiveUntil=");
                builder.Append(glide.ActiveUntilTickExclusive);
                builder.Append("|RecoveryUntil=");
                builder.Append(glide.RecoveryUntilTickExclusive);
                builder.Append("|CooldownUntil=");
                builder.Append(glide.CooldownUntilTickExclusive);
            }

            return builder.ToString();
        }

        private static string DumpOccupancy(WorldState worldState, params SurfaceCell[] cells)
        {
            var snapshot = worldState.CreateSnapshot();
            var builder = new StringBuilder();
            foreach (var cell in cells)
            {
                builder.Append(cell);
                builder.Append(":Unit=");
                builder.Append(snapshot.TryGetPrimaryUnitAt(cell, out var unit) ? unit.entityId : 0);
                builder.Append(",Solid=");
                builder.Append(snapshot.TryGetSolidOccupantAt(cell, out var solid) ? solid.entityId : 0);
                builder.Append("|");
            }

            return builder.ToString();
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities)
        {
            return CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(6, 6)));
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities, BoardBounds bounds)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                bounds);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            EnemyAiMode aiMode,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = teamId == 1 ? UnitRole.Player : UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
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
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static UnitKinematicLocomotionTimingSnapshot CreateKinematicTiming(int ticksPerCell)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new UnitKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = ticksPerCell / (float)timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private readonly struct FaceGateOutcome
        {
            public FaceGateOutcome(EnemyGlidePhase phase, bool wantsRecover, int recoveryUntilTickExclusive)
            {
                Phase = phase;
                WantsRecover = wantsRecover;
                RecoveryUntilTickExclusive = recoveryUntilTickExclusive;
            }

            public EnemyGlidePhase Phase { get; }

            public bool WantsRecover { get; }

            public int RecoveryUntilTickExclusive { get; }
        }

        private sealed class LongRunOutcome
        {
            public LongRunOutcome(
                IReadOnlyList<string> tickHashes,
                IReadOnlyList<string> tickTraces,
                string finalState)
            {
                TickHashes = tickHashes;
                TickTraces = tickTraces;
                FinalState = finalState;
            }

            public IReadOnlyList<string> TickHashes { get; }

            public IReadOnlyList<string> TickTraces { get; }

            public string FinalState { get; }
        }
    }
}
