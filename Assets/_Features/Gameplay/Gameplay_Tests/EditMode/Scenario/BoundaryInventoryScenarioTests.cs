using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class BoundaryInventoryScenarioTests
    {
        [Test]
        [Category("Core")]
        public void BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement()
        {
            AssertDefaultGameplayLocomotionFlags();

            var playerWorld = CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) });
            var playerTick = CreatePipeline(
                    playerWorld,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(playerTick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(playerTick, 10);

            var enemyWorld = CreateWorldState(new[]
            {
                CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase),
            });
            var enemyTick = CreatePipeline(
                    enemyWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(enemyTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
            LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(enemyTick, 40);

            var chargeWorld = CreateWorldState(new[]
            {
                CreateUnit(50, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Charge),
            });
            chargeWorld.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            var chargeTick = CreatePipeline(
                    chargeWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(
                chargeTick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 50 &&
                    track.MotionMode == MotionMode.Charge),
                Is.True);
            LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(chargeTick, 50);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_GridTransactionsRemainAllowed_UnderDefaultGameplayLocomotion()
        {
            var pushTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(pushTick, 30, MovementExecutionBoundaryKind.BoxActionMovement);
            Assert.That(pushTick.PresentationData.EntityMotions.Any(motion => motion.EntityId == 30), Is.True);

            var flipTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Left),
                        CreateBox(30, new SurfaceCell(FaceId.Floor, -1, 0), BoxCapabilities.Flip),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(-1, 0), MovementCommandKind.Flip)) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(flipTick, 30, MovementExecutionBoundaryKind.BoxActionMovement);
            Assert.That(flipTick.PresentationData.EntityMotions.Any(motion => motion.EntityId == 30), Is.True);

            var itemTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Item),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(itemTick, 10, MovementExecutionBoundaryKind.BoxActionMovement);
            Assert.That(itemTick.PresentationData.EntityMotions.Any(motion => motion.EntityId == 10), Is.True);

            var topologyWorld = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            topologyWorld.CreateWriteContext().SetPlayerControlState(10, default);
            var topologyIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(0, 2));
            topologyIntent.AssignIntentId(1);
            AssertLegacyExpansionIntentAllowed(
                topologyWorld,
                topologyIntent,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(pushTick);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(flipTick);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(itemTick);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_FlagOff_LegacyFallbackStillAllowed()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.HasLegacyFallbackMoveEntity(playerTick, 10);
            LegacyMovementBoundaryAssert.HasLegacyFallbackMove(playerTick, 10);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.HasLegacyFallbackMoveEntity(enemyTick, 40);
            LegacyMovementBoundaryAssert.HasLegacyFallbackMove(enemyTick, 40);

            var chargeWorld = CreateWorldState(new[]
            {
                CreateUnit(50, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Charge),
            });
            chargeWorld.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            var chargeTick = CreatePipeline(
                    chargeWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.HasLegacyFallbackMoveEntity(chargeTick, 50);
            LegacyMovementBoundaryAssert.HasLegacyChargeMove(chargeTick, 50);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_SpawnRespawnCleanup_NotOrdinaryMovement()
        {
            var worldState = CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0), hp: 1) });
            var pipeline = CreatePipeline(
                worldState,
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            ((IAttackCommitContext)worldState.CreateWriteContext()).MarkDestroy(10);
            var cleanupTick = pipeline.RunTick(new TickInput(1));
            Assert.That(SemanticEventAssertions.GetCleanupRemovedEntityIds(cleanupTick.EventLog), Does.Contain(10));
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(cleanupTick);

            var respawnTick = pipeline.RunTick(new TickInput(2));
            Assert.That(
                respawnTick.PresentationData.TransitionVisibilityChanges.Any(change => change.EntityId == 10) ||
                respawnTick.EventLog.Any(entry => entry.Contains("RespawnCommitted|E=10", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                respawnTick.Trace.Text.Contains("Boundary=SpawnRespawnPlacement", StringComparison.Ordinal) ||
                respawnTick.Trace.Text.Contains("BoundaryReason=PlayerRespawnPlacement", StringComparison.Ordinal),
                Is.True);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(respawnTick);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_SpecialMovement_JumpPhaseGlide_ClassifiedOrReported()
        {
            var jumpProfile = EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1));
            try
            {
                var jumpWorld = CreateWorldState(new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 3, 0)),
                    CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                });
                var jumpWriteContext = jumpWorld.CreateWriteContext();
                jumpWriteContext.SetEnemyJumpState(
                    40,
                    new EnemyJumpRuntimeState
                    {
                        phase = EnemyJumpPhase.Airborne,
                        sequence = 1,
                        sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                        lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0),
                        windupEndTick = 0,
                        landingTick = 1,
                    });
                jumpWriteContext.SetBoardPresence(40, EntityBoardPresence.Detached);
                var jumpTick = GameplayCompositionRoot.CreateDefaultBootstrapper(jumpProfile)
                    .CreateTickPipeline(jumpWorld, Array.Empty<IEntityLogic>())
                    .RunTick(new TickInput(1));
                LegacyMovementBoundaryAssert.HasOperationBoundary(
                    jumpTick,
                    40,
                    FinalizationOperationKind.MoveEntity,
                    MovementExecutionBoundaryKind.UnitSpecialLocomotion);
                LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(jumpTick);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(jumpProfile);
            }

            var phaseWorld = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
            });
            var phasePipeline = new GameplayBootstrapper(
                    GameplayEntityLogicProviderFactory.CreateDefault(CreatePhaseThroughLockedTargetDefinition()))
                .CreateTickPipeline(
                    phaseWorld,
                    Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    CreatePlayerTiming(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            phasePipeline.RunTick(new TickInput(1));
            var phaseTick = phasePipeline.RunTick(new TickInput(2));
            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(phaseTick, 40, MovementExecutionBoundaryKind.ScriptedRelocation);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(phaseTick);

            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 1, recoveryTicks: 1, cooldownTicks: 1));
            try
            {
                var glideWorld = CreateWorldState(new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 2, 0)),
                    CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                });
                var glideTick = GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile)
                    .CreateTickPipeline(
                        glideWorld,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                    .RunTick(new TickInput(1));
                Assert.That(glideWorld.CreateSnapshot().TryGetEnemyGlideState(40, out _), Is.True);
                LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(glideTick);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_SpecialMovement_Jump_IsUnitSpecialLocomotion()
        {
            var jumpProfile = EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1));
            try
            {
                var jumpWorld = CreateWorldState(new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 3, 0)),
                    CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                });
                var jumpWriteContext = jumpWorld.CreateWriteContext();
                jumpWriteContext.SetEnemyJumpState(
                    40,
                    new EnemyJumpRuntimeState
                    {
                        phase = EnemyJumpPhase.Airborne,
                        sequence = 1,
                        sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                        lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0),
                        windupEndTick = 0,
                        landingTick = 1,
                    });
                jumpWriteContext.SetBoardPresence(40, EntityBoardPresence.Detached);

                var jumpTick = GameplayCompositionRoot.CreateDefaultBootstrapper(jumpProfile)
                    .CreateTickPipeline(jumpWorld, Array.Empty<IEntityLogic>())
                    .RunTick(new TickInput(1));

                LegacyMovementBoundaryAssert.HasOperationBoundary(
                    jumpTick,
                    40,
                    FinalizationOperationKind.MoveEntity,
                    MovementExecutionBoundaryKind.UnitSpecialLocomotion);
                LegacyMovementBoundaryAssert.HasOperationBoundary(
                    jumpTick,
                    40,
                    FinalizationOperationKind.SetEnemyJumpState,
                    MovementExecutionBoundaryKind.UnitSpecialLocomotion);
                LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(jumpTick);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(jumpProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_PhaseRelocation_IsScriptedRelocation()
        {
            var phaseWorld = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
            });
            var phasePipeline = new GameplayBootstrapper(
                    GameplayEntityLogicProviderFactory.CreateDefault(CreatePhaseThroughLockedTargetDefinition()))
                .CreateTickPipeline(
                    phaseWorld,
                    Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    CreatePlayerTiming(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            phasePipeline.RunTick(new TickInput(1));
            var phaseTick = phasePipeline.RunTick(new TickInput(2));

            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(phaseTick, 40, MovementExecutionBoundaryKind.ScriptedRelocation);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(phaseTick);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_Glide_IsReportedSpecialCandidate()
        {
            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 1, recoveryTicks: 1, cooldownTicks: 1));
            try
            {
                var glideWorld = CreateWorldState(new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 2, 0)),
                    CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                });
                var glideTick = GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile)
                    .CreateTickPipeline(
                        glideWorld,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                    .RunTick(new TickInput(1));

                Assert.That(glideWorld.CreateSnapshot().TryGetEnemyGlideState(40, out _), Is.True);
                LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMoveOperationOrDiagnostic(glideTick, 40);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_Glide_NoLegacyOrdinaryMoveLeak()
        {
            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 1));
            try
            {
                var glideWorld = CreateWorldState(new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 3, 0)),
                    CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                });
                var glidePipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile)
                    .CreateTickPipeline(
                        glideWorld,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

                var windupTick = glidePipeline.RunTick(new TickInput(1));

                Assert.That(glideWorld.CreateSnapshot().TryGetEnemyGlideState(40, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(windupTick, 40);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_Glide_ActiveLegacyFallback_FlagOff_IsDocumented()
        {
            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 1));
            try
            {
                var glideWorld = CreateWorldState(new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 3, 0)),
                    CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                });
                var glidePipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile)
                    .CreateTickPipeline(
                        glideWorld,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

                _ = glidePipeline.RunTick(new TickInput(1));
                var activeTick = glidePipeline.RunTick(new TickInput(2));

                Assert.That(glideWorld.CreateSnapshot().TryGetEnemyGlideState(40, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                LegacyMovementBoundaryAssert.HasLegacyFallbackMove(activeTick, 40);
                LegacyMovementBoundaryAssert.HasLegacyFallbackMoveEntity(activeTick, 40);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_Glide_ActiveKinematic_NoLegacyOrdinaryMove()
        {
            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 1));
            try
            {
                var glideWorld = CreateWorldState(new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 3, 0)),
                    CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
                });
                var glidePipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile)
                    .CreateTickPipeline(
                        glideWorld,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                        playerKinematicLocomotionTiming: CreateTwoTickKinematicTiming());

                _ = glidePipeline.RunTick(new TickInput(1));
                var activeTick = glidePipeline.RunTick(new TickInput(2));

                Assert.That(glideWorld.CreateSnapshot().TryGetEnemyGlideState(40, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(
                    activeTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Voluntary),
                    Is.True);
                Assert.That(
                    activeTick.PresentationData.EnemyGlideSignals.Any(signal =>
                        signal.EntityId == 40 &&
                        signal.Phase == EnemyGlidePhase.Active &&
                        signal.CurrentHeightUnits > 0),
                    Is.True);
                LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(activeTick, 40);
                var commitTick = HasGlideActiveKinematicAnchorCommit(activeTick, 40)
                    ? activeTick
                    : null;
                for (var tickIndex = 3; tickIndex <= 8; tickIndex++)
                {
                    if (commitTick != null)
                    {
                        break;
                    }

                    var tick = glidePipeline.RunTick(new TickInput(tickIndex));
                    LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(tick, 40);
                    if (HasGlideActiveKinematicAnchorCommit(tick, 40))
                    {
                        commitTick = tick;
                    }
                }

                Assert.That(commitTick, Is.Not.Null);
                LegacyMovementBoundaryAssert.HasMoveEntityBoundaryReason(
                    commitTick,
                    40,
                    MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                    "GlideActiveKinematicAnchorCommit");
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_DefaultBundleStillExcludesFlag()
        {
            var flags = GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion;

            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled.EnableEnemyGlideKinematicLocomotion, Is.True);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_ForcedMotion_IsReportedOrAbsent()
        {
            var inventory = new[]
            {
                "forced motion vocabulary: MotionMode.Forced",
                "knockback vocabulary: ForcedMotionOp.Knockback",
                "forced motion gameplay producer: absent",
                "knockback gameplay producer: absent",
                "future classification: UnitSpecialLocomotion or UnitKinematicRuntimeState with explicit boundary",
            };

            Assert.That(inventory, Has.Length.EqualTo(5));
            Assert.That(MotionMode.Forced, Is.EqualTo(MotionMode.Forced));
            Assert.That(ForcedMotionOp.Knockback, Is.EqualTo(ForcedMotionOp.Knockback));
            Assert.That(inventory.Any(entry => entry.Contains("gameplay producer: absent", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_ForcedMotion_NoRuntimeProducerYet()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.NoForcedKinematicProducer(playerTick, 10);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.NoForcedKinematicProducer(enemyTick, 40);

            var chargeWorld = CreateWorldState(new[]
            {
                CreateUnit(50, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Charge),
            });
            chargeWorld.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            var chargeTick = CreatePipeline(
                    chargeWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.NoForcedKinematicProducer(chargeTick, 50);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_NoUnexpectedUnknownMovement_Representative()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.NoUnexpectedUnknownMovementBoundaryAllowingStateOnly(playerTick);

            var pushTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.NoUnexpectedUnknownMovementBoundaryAllowingStateOnly(pushTick);

            var phaseWorld = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase),
            });
            var phasePipeline = new GameplayBootstrapper(
                    GameplayEntityLogicProviderFactory.CreateDefault(CreatePhaseThroughLockedTargetDefinition()))
                .CreateTickPipeline(
                    phaseWorld,
                    Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    CreatePlayerTiming(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            phasePipeline.RunTick(new TickInput(1));
            var phaseTick = phasePipeline.RunTick(new TickInput(2));
            LegacyMovementBoundaryAssert.NoUnexpectedUnknownMovementBoundaryAllowingStateOnly(phaseTick);
        }

        [Test]
        [Category("Core")]
        public void LegacyOrdinaryUnitMovement_DeprecationReadiness_Report()
        {
            var flagOffFallbacks = new[]
            {
                "player legacy fallback: LegacyFallback + TickEntityMotionKind.Move",
                "enemy legacy fallback: LegacyFallback + TickEntityMotionKind.Move",
                "charge legacy fallback: LegacyFallback + TickEntityMotionKind.ChargeMove",
            };
            var flagOnTargets = new[]
            {
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnablePlayerFree2DLocalLocomotion,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnableEnemySameFaceContinuousLocomotion,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnableEnemyChargeKinematicLocomotion,
            };
            var specialInventoryV3 = new[]
            {
                "jump: Safe UnitSpecialLocomotion",
                "phase relocation: Safe Retained Grid Transaction",
                "glide: Flag-Gated Kinematic Candidate, default adoption pending",
                "forced motion: Future Runtime State Needed",
                "knockback: Future Runtime State Needed",
            };
            var retainedPaths = new[]
            {
                "MoveEntity primitive",
                "MovementExpander grid transaction branch",
                "box push / flip / item",
                "topology materialization",
                "spawn / respawn placement",
                "cleanup removal",
                "scripted relocation / phase relocation",
                "flag-off historical baseline",
            };

            Assert.That(flagOffFallbacks, Has.Length.EqualTo(3));
            Assert.That(flagOnTargets.All(enabled => enabled), Is.True);
            Assert.That(specialInventoryV3, Does.Contain("glide: Flag-Gated Kinematic Candidate, default adoption pending"));
            Assert.That(retainedPaths, Does.Contain("MoveEntity primitive"));
            Assert.That(retainedPaths, Does.Contain("MovementExpander grid transaction branch"));
        }

        private static void AssertDefaultGameplayLocomotionFlags()
        {
            var flags = GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion;
            Assert.That(flags.EnablePlayerFree2DLocalLocomotion, Is.True);
            Assert.That(flags.EnablePlayerFree2DActionAssist, Is.True);
            Assert.That(flags.EnablePlayerSameFaceContinuousLocomotion, Is.True);
            Assert.That(flags.EnablePlayerStoppableKinematicLocomotion, Is.True);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.True);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.True);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnablePlayerFree2DLocalLocomotion, Is.False);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                entityLogics,
                GameplayTimingProfile.CreateDefault(),
                CreatePlayerTiming(),
                runtimeFeatureFlags: runtimeFeatureFlags);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateTwoTickKinematicTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 2f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static bool HasGlideActiveKinematicAnchorCommit(TickResult tick, int entityId)
        {
            return tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MoveEntity &&
                operation.EntityId == entityId &&
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LocomotionAnchorCommit &&
                operation.Metadata.BoundaryReason == "GlideActiveKinematicAnchorCommit");
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static List<MoveIntent> InvokeValidateLegacyExpansionIntents(
            TickPipeline pipeline,
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> expansionIntents,
            List<string> rejectedReasons)
        {
            var method = typeof(TickPipeline).GetMethod(
                "ValidateLegacyExpansionIntents",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            return (List<MoveIntent>)method.Invoke(
                pipeline,
                new object[]
                {
                    snapshot,
                    expansionIntents,
                    rejectedReasons,
                });
        }

        private static void AssertLegacyExpansionIntentAllowed(
            WorldState worldState,
            MoveIntent intent,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var rejectedReasons = new List<string>();
            var filteredIntents = InvokeValidateLegacyExpansionIntents(
                CreatePipeline(worldState, Array.Empty<IEntityLogic>(), runtimeFeatureFlags),
                worldState.CreateSnapshot(),
                new[] { intent },
                rejectedReasons);

            CollectionAssert.AreEqual(
                new[] { intent.IntentId },
                filteredIntents.Select(filtered => filtered.IntentId).ToArray());
            Assert.That(
                rejectedReasons.Any(reason => reason.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False);
        }

        private static EntityState CreatePlayer(
            int entityId,
            SurfaceCell position,
            int hp = 3,
            Direction facing = Direction.Right)
        {
            return CreateUnit(entityId, teamId: 1, position, hp, EnemyAiMode.None, facing);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp = 3,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = teamId == 1 ? UnitRole.Player : teamId == 2 ? UnitRole.Enemy : UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
            };
        }

        private static EnemyAiRuntimeDefinition CreatePhaseThroughLockedTargetDefinition()
        {
            return new EnemyAiRuntimeDefinition(
                new EnemyAiCommonSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverTicks: 1),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                new EnemyAttackTimingSettings(windupTicks: 1),
                EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                MovementSkillStrategyKind.PhaseThroughLockedTarget,
                EnemyJumpTimingSettings.CreateDefault(),
                ForwardPatrolStrategy.Instance,
                NearestOpponentDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                MeleeAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        private sealed class ScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _tickIndex;
            private readonly RawMovementIntent _intent;

            public ScriptedMovementLogic(int tickIndex, RawMovementIntent intent)
            {
                _tickIndex = tickIndex;
                _intent = intent;
            }

            public int ControlledEntityId => _intent.SourceId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (input.TickIndex == _tickIndex)
                {
                    buffer.Add(_intent);
                }
            }
        }
    }
}
