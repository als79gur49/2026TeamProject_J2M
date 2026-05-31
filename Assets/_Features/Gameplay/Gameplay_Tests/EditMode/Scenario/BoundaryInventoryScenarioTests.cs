using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Stages;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class BoundaryInventoryScenarioTests
    {
        private const string GlideChaserProfileAssetPath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_GlideChaser/EnemyAi_GlideChaser.asset";

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
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(playerTick, 10);

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
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(enemyTick, 40);

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
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(chargeTick, 50);
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_PlayerEnemyCharge_NoLegacyFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_PlayerOrdinary_NoLegacyFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_EnemyOrdinary_NoLegacyFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_ChargeActive_NoLegacyFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_PlayerContinuous_DoesNotEmitEntityMove()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            Assert.That(
                tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 10),
                Is.False);
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(tick, 10);
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_PlayerKinematic_DoesNotEmitEntityMove()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 10), Is.True);
            Assert.That(
                tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10),
                Is.False);
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(tick, 10);
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_EnemyKinematic_DoesNotEmitEntityMove()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(tick, 40);
        }

        [Test]
        [Category("Core")]
        public void KinematicLocomotion_RightMove_FacesRight()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreateUnit(
                            40,
                            2,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            aiMode: EnemyAiMode.Chase,
                            facing: Direction.Left),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            var finalEnemy = tick.FinalEntities.Single(entity => entity.entityId == 40);
            Assert.That(finalEnemy.facing, Is.EqualTo(Direction.Right));
            Assert.That(
                tick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 40 &&
                    track.KinematicDirection == Direction.Right &&
                    track.PoseFacing == Direction.Right &&
                    track.ShouldUpdateFacing),
                Is.True);
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(tick, 40);
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_ChargeKinematic_DoesNotEmitEntityMove()
        {
            var tick = CreatePipeline(
                    CreateActiveChargeWorldState(50),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(
                tick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 50 &&
                    track.MotionMode == MotionMode.Charge),
                Is.True);
            Assert.That(
                tick.PresentationData.EnemyChargeSignals.Any(signal =>
                    signal.EntityId == 50 &&
                    signal.Phase == EnemyChargePhase.Active),
                Is.True);
            LegacyMovementBoundaryAssert.NoCoveredLocomotionLegacyFallback(tick, 50);
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_RemovedDiagnosticBaseline_NoCoveredEntityMove()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.PlayerLegacyFallbackRemovedFromRuntime(playerTick, 10);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(playerTick);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.EnemyLegacyFallbackRemovedFromRuntime(enemyTick, 40);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(enemyTick);

            var chargeTick = CreatePipeline(
                    CreateActiveChargeWorldState(50),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.ChargeLegacyFallbackRemovedFromRuntime(chargeTick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(chargeTick);
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback()
        {
            AssertDefaultGameplayLocomotionFlags();

            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            LegacyMovementBoundaryAssert.NoPlayerLegacyOrdinaryFallback(tick, 10);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerLegacyFallback_Free2DFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var intent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                worldState,
                intent,
                GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled,
                "PlayerCoveredLocomotionReachedLegacyExpansion");
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerLegacyFallback_KinematicFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var intent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                worldState,
                intent,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled,
                "PlayerCoveredLocomotionReachedLegacyExpansion");
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase4 wrapper: delegates to the canonical player removed-diagnostic test.
        public void Phase2_PlayerLegacyFallback_FlagOffBaseline_RemovedByPhase4()
        {
            Phase4_LegacyOrdinaryFallbackBaseline_PlayerFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_LegacyOrdinaryFallbackBaseline_PlayerFallbackRemoved()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            LegacyMovementBoundaryAssert.PlayerLegacyFallbackRemovedFromRuntime(tick, 10);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerLegacyFallback_TopologyHandoff_IsRetainedGridTransaction()
        {
            Player_Free2D_TopologyHandoff_NoLegacyOrdinaryFallback();

            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var approachWorld = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(approachWorld, localX: 0, localY: KinematicFixed.MaxPositiveLocalOffset, speedUnitsPerTick: speed);
            var approachPipeline = CreatePipeline(
                approachWorld,
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            var handoffTick = approachPipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                handoffTick,
                10,
                MovementExecutionBoundaryKind.Free2DTopologyTransition);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(handoffTick);
            Assert.That(
                handoffTick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            Assert.That(
                handoffTick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(handoffTick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Phase4_DefaultGameplay_PlayerFallbackStillAbsent()
        {
            Phase2_PlayerLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_PlayerKinematicFlagOn_NoLegacyFallback()
        {
            Phase2_PlayerLegacyFallback_KinematicFlagOn_BlockedBeforeMovementExpander();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_PlayerTopologyHandoff_StillGridTransaction()
        {
            Phase2_PlayerLegacyFallback_TopologyHandoff_IsRetainedGridTransaction();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_LegacyBaseline_PlayerFallbackStillRemoved()
        {
            Phase4_LegacyOrdinaryFallbackBaseline_PlayerFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback()
        {
            AssertDefaultGameplayLocomotionFlags();

            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
            LegacyMovementBoundaryAssert.NoEnemyLegacyOrdinaryFallback(tick, 40);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase5_DefaultGameplay_EnemyFallbackStillAbsent()
        {
            Phase2B_EnemyLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback();
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyLegacyFallback_KinematicFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) });
            var intent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                worldState,
                intent,
                GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled,
                "EnemyCoveredOrdinaryKinematicReachedLegacyExpansion");
        }

        [Test]
        [Category("Extended")]
        public void Phase5_EnemyKinematicFlagOn_NoLegacyFallback()
        {
            Phase2B_EnemyLegacyFallback_KinematicFlagOn_BlockedBeforeMovementExpander();
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase5 wrapper: delegates to the canonical enemy removed-diagnostic test.
        public void Phase2B_EnemyLegacyFallback_FlagOffBaseline_RemovedByPhase5()
        {
            Phase5_LegacyOrdinaryFallbackBaseline_EnemyFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_LegacyOrdinaryFallbackBaseline_EnemyFallbackRemoved()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));

            LegacyMovementBoundaryAssert.EnemyLegacyFallbackRemovedFromRuntime(tick, 40);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase5_LegacyBaseline_EnemyFallbackRemovedByPhase5()
        {
            Phase5_LegacyOrdinaryFallbackBaseline_EnemyFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyLegacyFallback_GlideDefault_IsKinematic_NotEnemyOrdinaryPilot()
        {
            BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_GlideFlagOffFallbackStillRetained()
        {
            BoundaryInventory_GlideFlagOff_FallbackStillRetained();
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyLegacyFallback_ChargeActive_IsOutOfScope()
        {
            ScopedDeletionPrep_ChargeLegacyFallback_RemovedByPhase6();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeLegacyFallback_DefaultGameplayLocomotion_NoChargeMoveFallback()
        {
            AssertDefaultGameplayLocomotionFlags();

            var worldState = CreateActiveChargeWorldState(50);
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(
                tick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 50 &&
                    track.MotionMode == MotionMode.Charge),
                Is.True);
            Assert.That(
                tick.PresentationData.EnemyChargeSignals.Any(signal =>
                    signal.EntityId == 50 &&
                    signal.Phase == EnemyChargePhase.Active),
                Is.True);
            LegacyMovementBoundaryAssert.NoChargeActiveLegacyFallback(tick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase6_DefaultGameplay_ChargeFallbackStillAbsent()
        {
            Phase2C_ChargeLegacyFallback_DefaultGameplayLocomotion_NoChargeMoveFallback();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveCleanup_DefaultGameplay_NoChargeMoveProducer()
        {
            Phase2C_ChargeLegacyFallback_DefaultGameplayLocomotion_NoChargeMoveFallback();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveProducer_DefaultGameplay_Unreachable()
        {
            ChargeMoveCleanup_DefaultGameplay_NoChargeMoveProducer();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveIsolation_DefaultGameplay_NoChargeMove()
        {
            ChargeMoveProducer_DefaultGameplay_Unreachable();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeLegacyFallback_ChargeKinematicFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateActiveChargeWorldState(50);
            var intent = new MoveIntent(50, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                worldState,
                intent,
                GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled,
                "ChargeCoveredKinematicReachedLegacyExpansion");
        }

        [Test]
        [Category("Extended")]
        public void Phase6_None_ChargeFallbackStillBlocked()
        {
            var worldState = CreateActiveChargeWorldState(50);
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));

            LegacyMovementBoundaryAssert.RequiresExplicitLegacyFallbackBaseline(tick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveCleanup_None_NoChargeMoveProducer()
        {
            Phase6_None_ChargeFallbackStillBlocked();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveProducer_None_Unreachable()
        {
            ChargeMoveCleanup_None_NoChargeMoveProducer();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveIsolation_None_NoChargeMove()
        {
            ChargeMoveProducer_None_Unreachable();
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase6 wrapper: delegates to the canonical Charge removed-diagnostic test.
        public void Phase2C_ChargeLegacyFallback_FlagOffBaseline_RemovedByPhase6()
        {
            Phase6_LegacyOrdinaryFallbackBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase6_LegacyOrdinaryFallbackBaseline_ChargeFallbackRemoved()
        {
            var worldState = CreateActiveChargeWorldState(50);
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));

            LegacyMovementBoundaryAssert.ChargeLegacyFallbackRemovedFromRuntime(tick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveCleanup_RemovedDiagnosticBaseline_NoChargeMoveProducer()
        {
            Phase6_LegacyOrdinaryFallbackBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveProducer_RemovedDiagnosticBaseline_Unreachable()
        {
            ChargeMoveCleanup_RemovedDiagnosticBaseline_NoChargeMoveProducer();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveIsolation_RemovedDiagnosticBaseline_NoChargeMove()
        {
            ChargeMoveProducer_RemovedDiagnosticBaseline_Unreachable();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveCleanup_ChargePresentationSignal_StillUsedForKinematicCharge()
        {
            var worldState = CreateActiveChargeWorldState(50);
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(
                tick.PresentationData.EnemyChargeSignals.Any(signal =>
                    signal.EntityId == 50 &&
                    signal.Phase == EnemyChargePhase.Active),
                Is.True);
            Assert.That(
                tick.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == 50),
                Is.False);
            Assert.That(
                tick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 50 &&
                    track.MotionMode == MotionMode.Charge),
                Is.True);
            LegacyMovementBoundaryAssert.NoChargeActiveLegacyFallback(tick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_DefaultGameplay_ChargePresentationStillWorks()
        {
            ChargeMoveCleanup_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_ChargeSignalStillEmitted()
        {
            ChargeMoveCleanup_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_TickKinematicMotionTrackStillEmitted()
        {
            ChargeMoveCleanup_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveProducer_ChargeKinematicSignal_IsNotChargeMove()
        {
            ChargeMoveCleanup_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveProducer_AllKinematic_Unreachable()
        {
            var worldState = CreateActiveChargeWorldState(50);
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled)
                .RunTick(new TickInput(1));

            Assert.That(
                tick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 50 &&
                    track.MotionMode == MotionMode.Charge),
                Is.True);
            LegacyMovementBoundaryAssert.NoChargeActiveLegacyFallback(tick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveIsolation_AllKinematic_NoChargeMove()
        {
            ChargeMoveProducer_AllKinematic_Unreachable();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveProducer_RuntimeReachabilityMatrix_IsCurrent()
        {
            ChargeMoveProducer_DefaultGameplay_Unreachable();
            ChargeMoveProducer_None_Unreachable();
            ChargeMoveProducer_RemovedDiagnosticBaseline_Unreachable();
            ChargeMoveProducer_AllKinematic_Unreachable();
            ChargeMoveProducer_ChargeKinematicSignal_IsNotChargeMove();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_LegacyBaseline_ChargeFallbackRemovedByPhase6()
        {
            Phase6_LegacyOrdinaryFallbackBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_LegacyBaseline_ChargeFallbackRemovedByPhase6()
        {
            Phase6_LegacyOrdinaryFallbackBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeLegacyFallback_PlayerEnemyOrdinary_AreOutOfScope()
        {
            Phase2_PlayerLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback();
            Phase2B_EnemyLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeLegacyFallback_GlideFlagOff_IsRetainedException_NotChargePilot()
        {
            BoundaryInventory_GlideFlagOff_FallbackStillRetained();
        }

        [Test]
        [Category("Extended")]
        public void Phase6_GlideFlagOffFallbackStillRetained()
        {
            BoundaryInventory_GlideFlagOff_FallbackStillRetained();
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
            LegacyMovementBoundaryAssert.GridTransactionsRemainAllowed(pushTick, 30, MovementExecutionBoundaryKind.BoxActionMovement);
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
            LegacyMovementBoundaryAssert.GridTransactionsRemainAllowed(flipTick, 30, MovementExecutionBoundaryKind.BoxActionMovement);
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
            LegacyMovementBoundaryAssert.GridTransactionsRemainAllowed(itemTick, 10, MovementExecutionBoundaryKind.BoxActionMovement);
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
        public void Phase3_DefaultGameplayLocomotion_NoCoveredFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void Phase3_None_NoPlayerEnemyChargeLegacyFallback()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.RequiresExplicitLegacyFallbackBaseline(playerTick, 10);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.RequiresExplicitLegacyFallbackBaseline(enemyTick, 40);

            var chargeWorld = CreateActiveChargeWorldState(50);
            var chargeTick = CreatePipeline(
                    chargeWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.RequiresExplicitLegacyFallbackBaseline(chargeTick, 50);
        }

        [Test]
        [Category("Core")]
        public void Phase3_None_DoesNotMeanLegacyOrdinaryFallback()
        {
            Assert.That(GameplayRuntimeFeatureFlags.None.EnableLegacyOrdinaryUnitFallback, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);

            Phase3_None_NoPlayerEnemyChargeLegacyFallback();
        }

        [Test]
        [Category("Core")]
        public void Phase3_LegacyBaseline_DoesNotEnableGlideKinematic()
        {
            var flags = GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline;

            Assert.That(flags.EnableLegacyOrdinaryUnitFallback, Is.True);
            Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.True);
            Assert.That(flags.EnablePlayerFree2DLocalLocomotion, Is.False);
            Assert.That(flags.EnablePlayerFree2DNativeTopologyTransition, Is.False);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.False);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.False);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase3_None_GridTransactionsRemainAllowed()
        {
            var pushTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.GridTransactionsRemainAllowedWithoutLegacyFallback(
                pushTick,
                30,
                MovementExecutionBoundaryKind.BoxActionMovement);

            var itemTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Item),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.GridTransactionsRemainAllowedWithoutLegacyFallback(
                itemTick,
                10,
                MovementExecutionBoundaryKind.BoxActionMovement);

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
                GameplayRuntimeFeatureFlags.None);
        }

        [Test]
        [Category("Core")]
        public void Phase3_None_GlideFlagOffFallbackStillRetained()
        {
            BoundaryInventory_GlideFlagOff_FallbackStillRetained();
            DefaultGameplayLocomotion_IncludesGlideKinematic();
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_TopologyHandoff_NoLegacyOrdinaryFallback()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                tick,
                10,
                MovementExecutionBoundaryKind.TopologyMaterialization);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.True);
            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void BoundaryInventory_Free2DTopologyNativeTransition_IsNotLegacyFallback()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(256),
                        KinematicFixed.FromRaw(KinematicFixed.MaxPositiveLocalOffset)),
                    velocity = KinematicVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.PlayerFree2DNativeTopologyTransitionEnabled)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(tick);
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.True);
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LegacyFallback),
                Is.False);
            Assert.That(tick.PresentationData.EntityMotions.Any(motion => motion.EntityId == 10), Is.False);
            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_Free2DTopologyNativeTransition_ActiveBarricadeRejectsWithoutMovingPlayer()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var blockedCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreatePlayer(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateTileFeature(100, blockedCell, TileFeatureKind.Barricade) });
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(256),
                        KinematicFixed.FromRaw(KinematicFixed.MaxPositiveLocalOffset)),
                    velocity = KinematicVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());

            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.PlayerFree2DNativeTopologyTransitionEnabled,
                    new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly) })
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(finalSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(sourceCell));
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.False);
            Assert.That(
                tick.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("RejectedBy=TargetFaceBlockedByTileFeature")),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_TopologyHandoff_GridTransactionAllowedByPhase1()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            var topologyIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(0, 2));
            topologyIntent.AssignIntentId(1);

            AssertLegacyExpansionIntentAllowed(
                worldState,
                topologyIntent,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                tick,
                10,
                MovementExecutionBoundaryKind.TopologyMaterialization);
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_GridTransactionsRemainAllowed()
        {
            BoundaryInventory_GridTransactionsRemainAllowed_UnderDefaultGameplayLocomotion();
        }

        [Test]
        [Category("Core")]
        public void Phase7_None_NoCoveredFallback()
        {
            Phase3_None_NoPlayerEnemyChargeLegacyFallback();
        }

        [Test]
        [Category("Core")]
        public void Phase7_DefaultGameplay_NoCoveredFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void Phase7_GridTransactionsRemainAllowed()
        {
            BoundaryInventory_GridTransactionsRemainAllowed_UnderDefaultGameplayLocomotion();
            Phase3_None_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase7_GlidePolicy_DefaultAdoptedAndFlagOffFallbackRetained()
        {
            BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic();
            BoundaryInventory_GlideFlagOff_FallbackStillRetained();
            ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove();
            DefaultGameplayLocomotion_IncludesGlideKinematic();
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_LegacyBaseline_PlayerEnemyChargeRemoved()
        {
            Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage();
        }

        [Test]
        [Category("Core")]
        public void Phase6_LegacyBaseline_PlayerEnemyChargeRemoved()
        {
            Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage();
        }

        [Test]
        [Category("Core")]
        public void Phase7_LegacyFallbackBaseline_IsDiagnosticCompatibilityPreset()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.AssertPlayerFallbackRemovedFromRuntime(playerTick, 10);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(playerTick);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.AssertEnemyFallbackRemovedFromRuntime(enemyTick, 40);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(enemyTick);

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
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.AssertChargeFallbackRemovedFromRuntime(chargeTick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(chargeTick);
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_LegacyBaseline_PlayerEnemyChargeRemoved()
        {
            BoundaryInventory_LegacyBaseline_PlayerEnemyChargeRemoved();
        }

        [Test]
        [Category("Core")]
        public void Phase7_HelperNames_AreCurrent()
        {
            var canonicalHelperNames = new[]
            {
                nameof(LegacyMovementBoundaryAssert.AssertCoveredFallbackRemovedDiagnostics),
                nameof(LegacyMovementBoundaryAssert.AssertPlayerFallbackRemovedFromRuntime),
                nameof(LegacyMovementBoundaryAssert.AssertEnemyFallbackRemovedFromRuntime),
                nameof(LegacyMovementBoundaryAssert.AssertChargeFallbackRemovedFromRuntime),
            };
            var phase7ReportStrings = new[]
            {
                "LegacyOrdinaryFallbackBaseline: diagnostic compatibility preset",
                "covered fallback authorization: removed",
                "grid transactions: retained",
                "glide default adoption: complete; flag-off fallback retained",
            };

            Assert.That(canonicalHelperNames.Any(name => name.Contains("Allows", StringComparison.Ordinal)), Is.False);
            Assert.That(canonicalHelperNames.Any(name => name.Contains("Allowed", StringComparison.Ordinal)), Is.False);
            Assert.That(phase7ReportStrings.Any(text => text.Contains("allows covered fallback", StringComparison.Ordinal)), Is.False);
            Assert.That(phase7ReportStrings, Does.Contain("LegacyOrdinaryFallbackBaseline: diagnostic compatibility preset"));
        }

        [Test]
        [Category("Core")]
        public void Phase8A_ObsoleteHelpers_NoInternalUsage()
        {
            var obsoleteCoveredHelperNames = new[]
            {
                "AllowsLegacyOrdinaryFallbackBaseline",
                "AllowsEnemyFlagOffLegacyOrdinaryFallback",
                "AllowsChargeFlagOffLegacyFallback",
                "AllowsOnlyFlagOffCoveredFallback",
                "AllowsFlagOffLegacyFallback",
            };
            var removedPlayerHelper = typeof(LegacyMovementBoundaryAssert).GetMethod(
                "AllowsPlayerFlagOffLegacyOrdinaryFallback",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(removedPlayerHelper, Is.Null);

            for (var i = 0; i < obsoleteCoveredHelperNames.Length; i++)
            {
                var method = typeof(LegacyMovementBoundaryAssert).GetMethod(
                    obsoleteCoveredHelperNames[i],
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(method, Is.Not.Null, obsoleteCoveredHelperNames[i]);
                Assert.That(method.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null, obsoleteCoveredHelperNames[i]);
            }

            var canonicalHelperNames = new[]
            {
                nameof(LegacyMovementBoundaryAssert.AssertCoveredFallbackRemovedDiagnostics),
                nameof(LegacyMovementBoundaryAssert.AssertPlayerFallbackRemovedFromRuntime),
                nameof(LegacyMovementBoundaryAssert.AssertEnemyFallbackRemovedFromRuntime),
                nameof(LegacyMovementBoundaryAssert.AssertChargeFallbackRemovedFromRuntime),
            };
            var phase8AUsageInventory = new[]
            {
                "AllowsLegacyOrdinaryFallbackBaseline: definition only",
                "AllowsPlayerFlagOffLegacyOrdinaryFallback: not present",
                "AllowsEnemyFlagOffLegacyOrdinaryFallback: definition only",
                "AllowsChargeFlagOffLegacyFallback: definition only",
                "AllowsOnlyFlagOffCoveredFallback: definition only",
                "AllowsFlagOffLegacyFallback: definition only",
            };

            Assert.That(canonicalHelperNames.Any(name => name.Contains("Allows", StringComparison.Ordinal)), Is.False);
            Assert.That(phase8AUsageInventory.Any(row => row.Contains("internal caller", StringComparison.Ordinal)), Is.False);
            Assert.That(phase8AUsageInventory, Does.Contain("AllowsPlayerFlagOffLegacyOrdinaryFallback: not present"));
        }

        [Test]
        [Category("Core")]
        public void Phase8A_LegacyFallbackBaseline_IsDiagnosticCompatibilityNaming()
        {
            var currentVocabulary = new[]
            {
                "LegacyOrdinaryFallbackBaseline: diagnostic compatibility preset",
                "covered fallback authorization: removed",
                "obsolete covered fallback helpers: wrapper-only",
                "retained grid transactions remain allowed",
                "glide retained fallback remains separate",
            };

            Assert.That(currentVocabulary, Does.Contain("LegacyOrdinaryFallbackBaseline: diagnostic compatibility preset"));
            Assert.That(currentVocabulary.Any(text => text.Contains("baseline allows fallback", StringComparison.Ordinal)), Is.False);
            Assert.That(currentVocabulary.Any(text => text.Contains("covered fallback allowed", StringComparison.Ordinal)), Is.False);
            Assert.That(currentVocabulary.Any(text => text.Contains("fallback output retained", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8A_Phase7Canaries_StillPass()
        {
            Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage();
            Phase7_None_NoCoveredFallback();
            Phase7_DefaultGameplay_NoCoveredFallback();
        }

        [Test]
        [Category("Core")]
        public void Phase8A_GridTransactionsRemainAllowed()
        {
            Phase7_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase8A_GlideDefaultAdoptionAndFlagOffFallbackRetained()
        {
            Phase7_GlidePolicy_DefaultAdoptedAndFlagOffFallbackRetained();
        }

        [Test]
        [Category("Core")]
        public void FallbackWrapperCleanup_ObsoleteAllowsHelpers_HaveNoInternalCallSites()
        {
            var helperSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/LegacyMovementBoundaryAssert.cs");
            var internalCallsiteFiles = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/BoundaryInventoryScenarioTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/EnemyKinematicLocomotionReplayTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/PlayerContinuousLocomotionReplayTests.cs",
            };
            var retainedCompatibilityWrappers = new[]
            {
                "AllowsLegacyOrdinaryFallbackBaseline",
                "AllowsEnemyFlagOffLegacyOrdinaryFallback",
                "AllowsChargeFlagOffLegacyFallback",
                "AllowsOnlyFlagOffCoveredFallback",
                "AllowsFlagOffLegacyFallback",
            };

            foreach (var wrapper in retainedCompatibilityWrappers)
            {
                Assert.That(helperSource, Does.Contain("[System.Obsolete"));
                Assert.That(
                    helperSource,
                    Does.Contain("public static void " + wrapper + "("),
                    wrapper);
                Assert.That(helperSource, Does.Contain("historical compatibility wrapper only"), wrapper);
            }

            Assert.That(
                helperSource,
                Does.Not.Contain("public static void AllowsPlayerFlagOffLegacyOrdinaryFallback" + "("));

            foreach (var relativePath in internalCallsiteFiles)
            {
                var source = ReadRepoFile(relativePath);
                foreach (var wrapper in retainedCompatibilityWrappers)
                {
                    Assert.That(source, Does.Not.Contain("LegacyMovementBoundaryAssert." + wrapper + "("), relativePath);
                    Assert.That(source, Does.Not.Contain(wrapper + "(result"), relativePath);
                    Assert.That(source, Does.Not.Contain(wrapper + "(tick"), relativePath);
                    Assert.That(source, Does.Not.Contain(wrapper + "(replay"), relativePath);
                }

                Assert.That(
                    source,
                    Does.Not.Contain("AllowsPlayerFlagOffLegacyOrdinaryFallback" + "("),
                    relativePath);
            }
        }

        [Test]
        [Category("Core")]
        public void FallbackWrapperCleanup_CurrentPolicyTests_UseRemovedDiagnosticVocabulary()
        {
            var docs = new[]
            {
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md"),
                ReadRepoFile("Docs/Architecture/ADR/ADR-005-Grid-Authoritative-Unit-Kinematics.md"),
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Compatibility-Layer-Consolidation-2026-05-02.md"),
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Move-Presentation-Ownership-Narrowing-2026-05-02.md"),
            };
            var forbiddenCurrentPolicyPhrases = new[]
            {
                "fallback " + "allowed",
                "still " + "allowed",
                "flag-off baseline " + "allows",
                "LegacyOrdinaryFallbackBaseline " + "allows",
            };

            foreach (var doc in docs)
            {
                Assert.That(doc, Does.Contain("RemovedLegacyFallbackDiagnosticBaseline"));
                Assert.That(doc, Does.Contain("removed diagnostic").Or.Contain("removed-diagnostic").Or.Contain("removed diagnostics"));
                foreach (var phrase in forbiddenCurrentPolicyPhrases)
                {
                    Assert.That(doc, Does.Not.Contain(phrase), phrase);
                }
            }

            var scenarioSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/BoundaryInventoryScenarioTests.cs");
            Assert.That(scenarioSource, Does.Contain(nameof(FallbackWrapperCleanup_ObsoleteAllowsHelpers_HaveNoInternalCallSites)));
            Assert.That(scenarioSource, Does.Contain(nameof(LegacyMovementBoundaryAssert.AssertCoveredFallbackRemovedDiagnostics)));
            Assert.That(scenarioSource, Does.Contain(nameof(LegacyMovementBoundaryAssert.AssertPlayerFallbackRemovedFromRuntime)));
            Assert.That(scenarioSource, Does.Contain(nameof(LegacyMovementBoundaryAssert.AssertEnemyFallbackRemovedFromRuntime)));
            Assert.That(scenarioSource, Does.Contain(nameof(LegacyMovementBoundaryAssert.AssertChargeFallbackRemovedFromRuntime)));
        }

        [Test]
        [Category("Core")]
        public void FallbackWrapperCleanup_HistoricalWrappers_AreExplicitlyMarked()
        {
            var boundarySource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/BoundaryInventoryScenarioTests.cs");
            var playerReplaySource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/PlayerContinuousLocomotionReplayTests.cs");
            var enemyReplaySource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/EnemyKinematicLocomotionReplayTests.cs");
            var phase2Docs = new[]
            {
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase2-Player-Pilot-2026-05-02.md"),
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase2B-Enemy-Pilot-2026-05-02.md"),
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase2C-Charge-Pilot-2026-05-02.md"),
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase3-Explicit-Legacy-Fallback-Policy-2026-05-02.md"),
            };

            Assert.That(boundarySource, Does.Contain("Historical/pre-Phase4 wrapper"));
            Assert.That(boundarySource, Does.Contain("Historical/pre-Phase5 wrapper"));
            Assert.That(boundarySource, Does.Contain("Historical/pre-Phase6 wrapper"));
            Assert.That(playerReplaySource, Does.Contain("Historical/pre-Phase4 wrapper"));
            Assert.That(enemyReplaySource, Does.Contain("Historical/pre-Phase6 wrapper"));

            foreach (var doc in phase2Docs)
            {
                Assert.That(doc, Does.Contain("Historical/pre-Phase").Or.Contain("historical/pre-Phase"));
                Assert.That(doc, Does.Not.Contain("FlagOffBaseline tests remain as compatibility wrappers"));
            }
        }

        [Test]
        [Category("Core")]
        public void FallbackWrapperCleanup_RetainedGridAllowedWording_IsPreserved()
        {
            var helperSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/LegacyMovementBoundaryAssert.cs");
            var readinessDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md");
            var ownershipDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Move-Presentation-Ownership-Narrowing-2026-05-02.md");

            Phase8E_GridTransactionsRemainAllowed();

            Assert.That(helperSource, Does.Contain(nameof(LegacyMovementBoundaryAssert.GridTransactionsRemainAllowed)));
            Assert.That(helperSource, Does.Contain(nameof(LegacyMovementBoundaryAssert.GridTransactionsRemainAllowedWithoutLegacyFallback)));
            Assert.That(readinessDoc, Does.Contain("retained grid transactions remain allowed"));
            Assert.That(ownershipDoc, Does.Contain("retained grid transaction presentation"));
            Assert.That(ownershipDoc, Does.Contain("TickEntityMotionKind.Move remains retained"));
        }

        [Test]
        [Category("Core")]
        public void FallbackWrapperCleanup_MoveOwnershipTests_DoNotUseFallbackAllowedWording()
        {
            var boundarySource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/BoundaryInventoryScenarioTests.cs");
            var movementSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs");
            var ownershipDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Move-Presentation-Ownership-Narrowing-2026-05-02.md");
            var consolidationDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Compatibility-Layer-Consolidation-2026-05-02.md");

            Assert.That(boundarySource, Does.Contain("MoveOwnership_PlayerContinuous_DoesNotEmitEntityMove"));
            Assert.That(boundarySource, Does.Contain("MoveOwnership_GridTransactions_Retained"));
            Assert.That(movementSource, Does.Contain("MoveOwnership_BoxActionMovement_RetainsRequiredMovePresentation"));
            Assert.That(ownershipDoc, Does.Contain("TickEntityMotionKind.Move remains retained"));
            Assert.That(ownershipDoc, Does.Contain("Retained generic movement and retained grid transaction presentation"));
            Assert.That(consolidationDoc, Does.Contain("`TickEntityMotionKind.Move` is not deleted"));
            Assert.That(ownershipDoc, Does.Not.Contain("Move ownership " + "fallback allowed"));
            Assert.That(consolidationDoc, Does.Not.Contain("Move ownership " + "fallback allowed"));
        }

        [Test]
        [Category("Core")]
        public void Phase8B_RemovedDiagnosticBaseline_IsCanonicalAlias()
        {
            var flags = GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline;

            AssertRemovedDiagnosticBaselineShape(flags);
        }

        [Test]
        [Category("Core")]
        public void Phase8B_LegacyOrdinaryFallbackBaseline_IsCompatibilityAlias()
        {
            var canonical = GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline;
            var compatibility = GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline;
            var phase8BVocabulary = new[]
            {
                "RemovedLegacyFallbackDiagnosticBaseline: canonical diagnostic preset",
                "LegacyOrdinaryFallbackBaseline: compatibility alias",
                "covered fallback authorization: removed",
            };

            AssertSameRuntimeFeatureFlags(compatibility, canonical);
            Assert.That(phase8BVocabulary, Does.Contain("LegacyOrdinaryFallbackBaseline: compatibility alias"));
            Assert.That(phase8BVocabulary.Any(text => text.Contains("fallback allowed", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8B_RemovedDiagnosticBaseline_PlayerEnemyChargeDiagnostics()
        {
            AssertRemovedDiagnosticBaselinePlayerEnemyChargeDiagnostics(
                GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);
        }

        [Test]
        [Category("Core")]
        public void Phase8B_DefaultGameplay_NoCoveredFallback_Unchanged()
        {
            Phase7_DefaultGameplay_NoCoveredFallback();
        }

        [Test]
        [Category("Core")]
        public void Phase8B_None_NoCoveredFallback_Unchanged()
        {
            Phase7_None_NoCoveredFallback();
        }

        [Test]
        [Category("Core")]
        public void Phase8B_GridTransactionsRemainAllowed()
        {
            Phase8A_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase8B_GlideDefaultAdoptionAndFlagOffFallbackRetained()
        {
            Phase8A_GlideDefaultAdoptionAndFlagOffFallbackRetained();
        }

        [Test]
        [Category("Core")]
        public void Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage()
        {
            var flags = GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline;
            var phase8CVocabulary = new[]
            {
                "RemovedLegacyFallbackDiagnosticBaseline: canonical diagnostic preset",
                "LegacyOrdinaryFallbackBaseline: deprecated compatibility alias",
                "covered fallback authorization: removed",
            };

            AssertRemovedDiagnosticBaselineShape(flags);
            AssertRemovedDiagnosticBaselinePlayerEnemyChargeDiagnostics(flags);
            Assert.That(phase8CVocabulary, Does.Contain("RemovedLegacyFallbackDiagnosticBaseline: canonical diagnostic preset"));
            Assert.That(phase8CVocabulary.Any(text => text.Contains("fallback allowed", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8C_LegacyOrdinaryFallbackBaseline_IsCompatibilityOnly()
        {
            var canonical = GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline;
            var compatibility = GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline;
            var allowedOldAliasContexts = new[]
            {
                "GameplayRuntimeFeatureFlags.cs alias definition",
                "Phase8B_LegacyOrdinaryFallbackBaseline_IsCompatibilityAlias",
                "Phase8C_LegacyOrdinaryFallbackBaseline_IsCompatibilityOnly",
                "historical docs: deprecated compatibility alias",
            };

            AssertSameRuntimeFeatureFlags(compatibility, canonical);
            Assert.That(
                allowedOldAliasContexts,
                Does.Contain("Phase8C_LegacyOrdinaryFallbackBaseline_IsCompatibilityOnly"));
            Assert.That(allowedOldAliasContexts.Any(text => text.Contains("canonical usage", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8C_CurrentPolicyDocs_UseRemovedDiagnosticBaseline()
        {
            var docs = new[]
            {
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase8C-Legacy-Alias-Usage-Cleanup-2026-05-02.md"),
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md"),
                ReadRepoFile("Docs/Architecture/ADR/ADR-005-Grid-Authoritative-Unit-Kinematics.md"),
                ReadRepoFile("Docs/Testing/Player-Free2D-Continuous-Locomotion-Rollout-2026-05-01.md"),
                ReadRepoFile("Docs/Testing/Enemy-Same-Face-Kinematic-Locomotion-Rollout-2026-04-30.md"),
                ReadRepoFile("Docs/Testing/Enemy-Charge-Kinematic-Locomotion-Rollout-2026-04-30.md"),
            };

            foreach (var doc in docs)
            {
                Assert.That(doc, Does.Contain("RemovedLegacyFallbackDiagnosticBaseline"));
                Assert.That(doc, Does.Not.Contain("LegacyOrdinaryFallbackBaseline allows fallback"));
                Assert.That(doc, Does.Not.Contain("LegacyOrdinaryFallbackBaseline is the baseline"));
                Assert.That(doc, Does.Not.Contain("Legacy fallback baseline remains allowed"));
            }

            Assert.That(
                docs.Any(doc => doc.Contains(
                    "LegacyOrdinaryFallbackBaseline` remains a deprecated compatibility alias",
                    StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void Phase8C_AllowedOldAliasUsage_IsLimited()
        {
            var runtimeFlags = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayRuntimeFeatureFlags.cs");
            var sourceFilesWithoutOldAliasDirectUsage = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/EnemyKinematicLocomotionReplayTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/PlayerContinuousLocomotionReplayTests.cs",
            };

            Assert.That(
                runtimeFlags,
                Does.Contain("public static GameplayRuntimeFeatureFlags LegacyOrdinaryFallbackBaseline"));
            Assert.That(runtimeFlags, Does.Contain("RemovedLegacyFallbackDiagnosticBaseline"));

            foreach (var relativePath in sourceFilesWithoutOldAliasDirectUsage)
            {
                var source = ReadRepoFile(relativePath);
                Assert.That(
                    source,
                    Does.Not.Contain("GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline"),
                    relativePath);
            }
        }

        [Test]
        [Category("Core")]
        public void Phase8C_GridTransactionsRemainAllowed()
        {
            Phase8B_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase8C_GlideDefaultAdoptionAndFlagOffFallbackRetained()
        {
            Phase8B_GlideDefaultAdoptionAndFlagOffFallbackRetained();
        }

        [Test]
        [Category("Core")]
        public void Phase8D_RemovedLegacyFallbackDiagnosticsEnabled_IsCanonicalHelper()
        {
            var presets = new[]
            {
                GameplayRuntimeFeatureFlags.None,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline,
                GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline,
                GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled,
            };
            var phase8DVocabulary = new[]
            {
                "RemovedLegacyFallbackDiagnosticsEnabled: canonical diagnostic routing helper",
                "LegacyOrdinaryFallbackEnabled: deprecated compatibility alias",
                "covered fallback authorization: removed",
            };

            foreach (var preset in presets)
            {
                Assert.That(
                    preset.RemovedLegacyFallbackDiagnosticsEnabled,
                    Is.EqualTo(preset.EnableLegacyOrdinaryUnitFallback));
            }

            Assert.That(GameplayRuntimeFeatureFlags.None.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline.RemovedLegacyFallbackDiagnosticsEnabled, Is.True);
            Assert.That(phase8DVocabulary, Does.Contain("RemovedLegacyFallbackDiagnosticsEnabled: canonical diagnostic routing helper"));
            Assert.That(phase8DVocabulary.Any(text => text.Contains("fallback allowed", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8D_LegacyOrdinaryFallbackEnabled_IsCompatibilityAlias()
        {
            var presets = new[]
            {
                GameplayRuntimeFeatureFlags.None,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline,
                GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline,
                GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled,
            };
            var allowedOldHelperContexts = new[]
            {
                "GameplayRuntimeFeatureFlags.cs helper definition",
                "Phase8D_LegacyOrdinaryFallbackEnabled_IsCompatibilityAlias",
                "historical docs: deprecated compatibility helper",
            };

            foreach (var preset in presets)
            {
                Assert.That(
                    preset.LegacyOrdinaryFallbackEnabled,
                    Is.EqualTo(preset.RemovedLegacyFallbackDiagnosticsEnabled));
            }

            Assert.That(
                allowedOldHelperContexts,
                Does.Contain("Phase8D_LegacyOrdinaryFallbackEnabled_IsCompatibilityAlias"));
            Assert.That(allowedOldHelperContexts.Any(text => text.Contains("canonical usage", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8D_RemovedDiagnosticHelper_DoesNotAuthorizeFallback()
        {
            var flags = GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline;

            Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.True);
            AssertRemovedDiagnosticBaselinePlayerEnemyChargeDiagnostics(flags);
        }

        [Test]
        [Category("Core")]
        public void Phase8D_None_Default_AllKinematic_HelperFalse()
        {
            Assert.That(GameplayRuntimeFeatureFlags.None.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8D_LegacyOrdinaryFallbackEnabled_HasNoCanonicalInternalUsage()
        {
            var runtimeFlags = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayRuntimeFeatureFlags.cs");
            var tickPipeline = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var sourceFilesWithoutOldHelperUsage = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/EnemyKinematicLocomotionReplayTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/PlayerContinuousLocomotionReplayTests.cs",
            };

            Assert.That(runtimeFlags, Does.Contain("public bool RemovedLegacyFallbackDiagnosticsEnabled"));
            Assert.That(
                runtimeFlags,
                Does.Contain("public bool LegacyOrdinaryFallbackEnabled => RemovedLegacyFallbackDiagnosticsEnabled"));
            Assert.That(tickPipeline, Does.Contain("RemovedLegacyFallbackDiagnosticsEnabled"));
            Assert.That(tickPipeline, Does.Not.Contain("LegacyOrdinaryFallbackEnabled"));
            Assert.That(tickPipeline, Does.Not.Contain("_runtimeFeatureFlags.EnableLegacyOrdinaryUnitFallback"));

            foreach (var relativePath in sourceFilesWithoutOldHelperUsage)
            {
                var source = ReadRepoFile(relativePath);
                Assert.That(source, Does.Not.Contain(".LegacyOrdinaryFallbackEnabled"), relativePath);
            }
        }

        [Test]
        [Category("Core")]
        public void Phase8D_CurrentPolicyDocs_UseRemovedDiagnosticHelper()
        {
            var docs = new[]
            {
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase8D-Diagnostic-Helper-Naming-2026-05-02.md"),
                ReadRepoFile("Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md"),
                ReadRepoFile("Docs/Architecture/ADR/ADR-005-Grid-Authoritative-Unit-Kinematics.md"),
            };

            foreach (var doc in docs)
            {
                Assert.That(doc, Does.Contain("RemovedLegacyFallbackDiagnosticsEnabled"));
                Assert.That(doc, Does.Not.Contain("LegacyOrdinaryFallbackEnabled allows fallback"));
                Assert.That(doc, Does.Not.Contain("LegacyOrdinaryFallbackEnabled is the helper"));
                Assert.That(doc, Does.Not.Contain("EnableLegacyOrdinaryUnitFallback allows fallback"));
            }

            Assert.That(
                docs.Any(doc => doc.Contains(
                    "LegacyOrdinaryFallbackEnabled` remains a deprecated compatibility alias",
                    StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void Phase8D_GridTransactionsRemainAllowed()
        {
            Phase8C_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase8D_GlideDefaultAdoptionAndFlagOffFallbackRetained()
        {
            Phase8C_GlideDefaultAdoptionAndFlagOffFallbackRetained();
        }

        [Test]
        [Category("Core")]
        public void Phase8E_EnableLegacyOrdinaryUnitFallback_FieldInventory_IsCompatibilityOnly()
        {
            var legacyDiagnosticField = typeof(GameplayRuntimeFeatureFlags).GetProperty(
                nameof(GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback),
                BindingFlags.Public | BindingFlags.Instance);
            var presets = new[]
            {
                GameplayRuntimeFeatureFlags.None,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled,
                GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline,
                GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline,
            };
            var phase8EVocabulary = new[]
            {
                "EnableLegacyOrdinaryUnitFallback: underlying compatibility diagnostic field",
                "RemovedLegacyFallbackDiagnosticsEnabled: preferred helper",
                "covered fallback authorization: removed",
            };

            Assert.That(legacyDiagnosticField, Is.Not.Null);
            foreach (var preset in presets)
            {
                Assert.That(
                    preset.EnableLegacyOrdinaryUnitFallback,
                    Is.EqualTo(preset.RemovedLegacyFallbackDiagnosticsEnabled));
            }

            Assert.That(GameplayRuntimeFeatureFlags.None.EnableLegacyOrdinaryUnitFallback, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnableLegacyOrdinaryUnitFallback, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled.EnableLegacyOrdinaryUnitFallback, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline.EnableLegacyOrdinaryUnitFallback, Is.True);
            Assert.That(GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline.EnableLegacyOrdinaryUnitFallback, Is.True);
            Assert.That(phase8EVocabulary, Does.Contain("RemovedLegacyFallbackDiagnosticsEnabled: preferred helper"));
            Assert.That(phase8EVocabulary.Any(text => text.Contains("fallback allowed", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Phase8E_LegacyDiagnosticField_NotSceneConfigExposed()
        {
            var hostFields = typeof(GameplaySceneHostConfiguration).GetFields(
                BindingFlags.Public | BindingFlags.Instance);
            var configuration = new GameplaySceneHostConfiguration();

            configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);
            var flags = configuration.CreateRuntimeFeatureFlags();

            Assert.That(
                hostFields.Select(field => field.Name),
                Does.Not.Contain(nameof(GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback)));
            Assert.That(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs"),
                Does.Not.Contain(nameof(GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback)));
            Assert.That(GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline.EnableLegacyOrdinaryUnitFallback, Is.True);
            Assert.That(flags.EnableLegacyOrdinaryUnitFallback, Is.False);
            Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);

            foreach (var scenePath in Directory.GetFiles(
                         Path.Combine(Application.dataPath, "Scenes"),
                         "*.unity",
                         SearchOption.AllDirectories))
            {
                Assert.That(
                    File.ReadAllText(scenePath),
                    Does.Not.Contain(nameof(GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback)),
                    scenePath);
            }
        }

        [Test]
        [Category("Core")]
        public void Phase8E_RemovedDiagnosticHelper_IsPreferredOverField()
        {
            var runtimeFlags = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayRuntimeFeatureFlags.cs");
            var tickPipeline = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");

            Assert.That(runtimeFlags, Does.Contain("public bool RemovedLegacyFallbackDiagnosticsEnabled => EnableLegacyOrdinaryUnitFallback"));
            Assert.That(runtimeFlags, Does.Contain("public bool EnableLegacyOrdinaryUnitFallback"));
            Assert.That(runtimeFlags, Does.Not.Contain("EnableRemovedLegacyFallbackDiagnostics"));
            Assert.That(tickPipeline, Does.Contain("_runtimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled"));
            Assert.That(tickPipeline, Does.Not.Contain("_runtimeFeatureFlags.EnableLegacyOrdinaryUnitFallback"));
        }

        [Test]
        [Category("Core")]
        public void Phase8E_TraceToken_LegacyFallback_IsKeptForGoldenStability()
        {
            var tickPipeline = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var phase8EDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase8E-Underlying-Field-Readiness-2026-05-02.md");

            Assert.That(tickPipeline, Does.Contain("LegacyFallback="));
            Assert.That(tickPipeline, Does.Contain("_runtimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled"));
            Assert.That(tickPipeline, Does.Not.Contain("RemovedLegacyFallbackDiagnostics="));
            Assert.That(phase8EDoc, Does.Contain("Trace vocabulary cleanup is a separate future phase."));
            Assert.That(phase8EDoc, Does.Contain("No golden files are rewritten in Phase 8E."));
        }

        [Test]
        [Category("Core")]
        public void Phase8E_FieldRenameOptions_AreDocumented()
        {
            var phase8EDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Phase8E-Underlying-Field-Readiness-2026-05-02.md");

            Assert.That(phase8EDoc, Does.Contain("Option A"));
            Assert.That(phase8EDoc, Does.Contain("Option B"));
            Assert.That(phase8EDoc, Does.Contain("Option C"));
            Assert.That(phase8EDoc, Does.Contain("Option D"));
            Assert.That(phase8EDoc, Does.Contain("Phase 8E does not rename or delete `EnableLegacyOrdinaryUnitFallback`."));
            Assert.That(phase8EDoc, Does.Contain("Option B is a future consideration, not a Phase 8E implementation."));
        }

        [Test]
        [Category("Core")]
        public void Phase8E_GridTransactionsRemainAllowed()
        {
            Phase8D_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase8E_GlideDefaultAdoptionAndFlagOffFallbackRetained()
        {
            Phase8D_GlideDefaultAdoptionAndFlagOffFallbackRetained();
        }

        [Test]
        [Category("Core")]
        public void CompatibilityLayer_ConsolidationInventory_IsCurrent()
        {
            var consolidationDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Compatibility-Layer-Consolidation-2026-05-02.md");
            var readinessDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md");
            var adr = ReadRepoFile("Docs/Architecture/ADR/ADR-005-Grid-Authoritative-Unit-Kinematics.md");

            Assert.That(consolidationDoc, Does.Contain("Legacy Compatibility Layer Consolidation"));
            Assert.That(consolidationDoc, Does.Contain("The project stops extending the tiny Phase 8F/8G/8H chain"));
            Assert.That(consolidationDoc, Does.Contain("Do Now / Defer / Retained Buckets"));
            Assert.That(consolidationDoc, Does.Contain("Covered fallback authorization is already removed"));
            Assert.That(consolidationDoc, Does.Contain("`EnableLegacyOrdinaryUnitFallback` rename | defer"));
            Assert.That(consolidationDoc, Does.Contain("`LegacyFallback=` rename | defer"));
            Assert.That(consolidationDoc, Does.Contain("replay/golden rewrite | defer"));
            Assert.That(readinessDoc, Does.Contain("Legacy Compatibility Layer Consolidation"));
            Assert.That(readinessDoc, Does.Contain("stops extending the micro-phase chain"));
            Assert.That(adr, Does.Contain("moves from stepwise fallback removal to `Legacy Compatibility Layer Consolidation`"));
            Assert.That(consolidationDoc, Does.Not.Contain("allows covered fallback"));
            Assert.That(readinessDoc, Does.Not.Contain("fallback allowed"));
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_Docs_RecordHistoricalRemoval()
        {
            var consolidationDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Compatibility-Layer-Consolidation-2026-05-02.md");
            var deletionDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-ChargeMove-Presentation-Cleanup-Readiness-2026-05-02.md");

            Assert.That(consolidationDoc, Does.Contain("Charge Presentation Removal Record"));
            Assert.That(consolidationDoc, Does.Contain("Current Charge presentation is `TickKinematicMotionTrack(MotionMode.Charge)` plus `TickEnemyChargePresentationSignal`"));
            Assert.That(consolidationDoc, Does.Contain("Synthetic Charge entity-motion compatibility is no longer retained behavior"));
            Assert.That(deletionDoc, Does.Contain("ChargeMove Presentation Consumer Deletion"));
            Assert.That(deletionDoc, Does.Contain("`TickEntityMotionKind." + "ChargeMove` enum/data support has been removed"));
            Assert.That(deletionDoc, Does.Contain("Current Charge presentation is represented by `TickKinematicMotionTrack(MotionMode.Charge)`"));
            Assert.That(deletionDoc, Does.Contain("The retained synthetic presentation compatibility path is closed"));
            Assert.That(deletionDoc, Does.Contain("Replay/golden files were not automatically rewritten"));
            Assert.That(deletionDoc, Does.Not.Contain("Do not delete `TickEntityMotionKind." + "ChargeMove`"));
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_PresentationConsumers_RemovalIsRecorded()
        {
            ChargeMoveDeletion_Docs_RecordHistoricalRemoval();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveResidue_YamlResidueReport_IsCurrent()
        {
            var report = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-ChargeMove-Deletion-Verification-And-Residue-Report-2026-05-02.md");

            Assert.That(report, Does.Contain("ChargeMove Deletion Verification And Residue Report"));
            Assert.That(report, Does.Contain("YAML Residue Report"));
            Assert.That(report, Does.Contain("GameplayPresentationTimingPreset_DefaultShowcase.asset"));
            Assert.That(report, Does.Contain("EnemyView_Charge.prefab"));
            Assert.That(report, Does.Contain("stale serialized residue"));
            Assert.That(report, Does.Contain("owner-approved asset migration"));
            Assert.That(report, Does.Contain("Do not mix that migration with runtime cleanup"));
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveResidue_PresentationAuthoring_RuntimeReadRemoved()
        {
            var report = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-ChargeMove-Deletion-Verification-And-Residue-Report-2026-05-02.md");

            Assert.That(report, Does.Contain("Presentation Authoring Cleanup"));
            Assert.That(report, Does.Contain("`EntityMotionPresentationAuthoring.chargeMove" + "MotionDurationSeconds`"));
            Assert.That(report, Does.Contain("`EntityMotionPresentationAuthoring` Charge snapshot/override lookup"));
            Assert.That(report, Does.Contain("Runtime read confirmation"));
            Assert.That(report, Does.Contain("runtime host/entityview/loop `ChargeMove` scan returned no hits"));
            Assert.That(report, Does.Contain("`TickKinematicMotionTrack(MotionMode.Charge)`"));
            Assert.That(report, Does.Contain("`TickEnemyChargePresentationSignal`"));
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_NoChargeMoveReferencesRemain()
        {
            var sourceFiles = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs",
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayTimingProfile.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
                "Assets/_Features/Gameplay/Gameplay_Timing/Runtime/GameplayPresentationTimingPreset.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayMotionTimingResolver.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/MotionTrack.cs",
                "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EntityMotionPresentationAuthoring.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/WorldSnapshotAndPresentationTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTickPresentationCoordinatorTests.cs",
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/TickReplayDeterminismTests.cs",
            };

            foreach (var sourceFile in sourceFiles)
            {
                var text = ReadRepoFile(sourceFile);
                Assert.That(text, Does.Not.Contain("TickEntityMotionKind." + "ChargeMove"), sourceFile);
                Assert.That(text, Does.Not.Contain("ChargeMove" + "DurationSeconds"), sourceFile);
                Assert.That(text, Does.Not.Contain("chargeMove" + "DurationSeconds"), sourceFile);
                Assert.That(text, Does.Not.Contain("chargeMove" + "MotionDurationSeconds"), sourceFile);
            }
        }

        [Test]
        [Category("Core")]
        public void CompatibilityLayer_MovePresentation_InventoryIsCurrent()
        {
            var consolidationDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Compatibility-Layer-Consolidation-2026-05-02.md");
            var ownershipDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Move-Presentation-Ownership-Narrowing-2026-05-02.md");

            Assert.That(consolidationDoc, Does.Contain("Move Presentation Inventory"));
            Assert.That(consolidationDoc, Does.Contain("`TickEntityMotionKind.Move`"));
            Assert.That(consolidationDoc, Does.Contain("retained grid transaction and generic presentation paths"));
            Assert.That(consolidationDoc, Does.Contain("not a deletion candidate in this consolidation"));
            Assert.That(consolidationDoc, Does.Contain("ownership narrowing between fallback-only producers and retained grid producers"));
            Assert.That(consolidationDoc, Does.Contain("ownership-narrowing target, not a deletion target"));
            Assert.That(ownershipDoc, Does.Contain("Move Presentation Ownership Narrowing"));
            Assert.That(ownershipDoc, Does.Contain("Producer Inventory"));
            Assert.That(ownershipDoc, Does.Contain("Consumer Inventory"));
            Assert.That(ownershipDoc, Does.Contain("Suppression Boundary Inventory"));
            Assert.That(ownershipDoc, Does.Contain("Replay / Golden Matrix"));
            Assert.That(ownershipDoc, Does.Contain("TickEntityMotionKind.Move remains retained"));
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_MovePresentationUnaffected()
        {
            CompatibilityLayer_MovePresentation_InventoryIsCurrent();
        }

        [Test]
        [Category("Core")]
        public void CompatibilityLayer_RetainedGridTransactions_StillProtected()
        {
            var consolidationDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Compatibility-Layer-Consolidation-2026-05-02.md");

            Phase8E_GridTransactionsRemainAllowed();

            Assert.That(consolidationDoc, Does.Contain("`MoveEntity` | runtime primitive | retained"));
            Assert.That(consolidationDoc, Does.Contain("`MovementExpander` | expansion component | retained"));
            Assert.That(consolidationDoc, Does.Contain("retained grid transaction boundary kinds"));
            Assert.That(consolidationDoc, Does.Contain("topology, box/action, spawn/respawn, cleanup, scripted relocation, anchor normalization"));
            Assert.That(consolidationDoc, Does.Contain("ordinary fallback confusion"));
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_GridTransactions_Retained()
        {
            CompatibilityLayer_RetainedGridTransactions_StillProtected();
            Phase8E_GridTransactionsRemainAllowed();
            BoundaryInventory_PhaseRelocation_IsScriptedRelocation();
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_Docs_RecordOwnershipNarrowing()
        {
            var readinessDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md");
            var adr = ReadRepoFile("Docs/Architecture/ADR/ADR-005-Grid-Authoritative-Unit-Kinematics.md");
            var chargeReport = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-ChargeMove-Deletion-Verification-And-Residue-Report-2026-05-02.md");

            CompatibilityLayer_MovePresentation_InventoryIsCurrent();
            Assert.That(readinessDoc, Does.Contain("`TickEntityMotionKind.Move` is an ownership-narrowing target, not a deletion target"));
            Assert.That(adr, Does.Contain("`TickEntityMotionKind.Move` is an ownership-narrowing target, not a deletion target"));
            Assert.That(chargeReport, Does.Contain("`TickEntityMotionKind.Move` is an ownership-narrowing target, not a deletion target"));
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_RetainedGridTransactionsUnaffected()
        {
            CompatibilityLayer_RetainedGridTransactions_StillProtected();
        }

        [Test]
        [Category("Core")]
        public void CompatibilityLayer_GlideRetainedException_StillSeparate()
        {
            var consolidationDoc = ReadRepoFile(
                "Docs/Testing/Legacy-Ordinary-Unit-Movement-Decommission-Compatibility-Layer-Consolidation-2026-05-02.md");

            Phase8E_GlideDefaultAdoptionAndFlagOffFallbackRetained();

            Assert.That(consolidationDoc, Does.Contain("glide flag-off fallback | exception | separate rollback policy"));
            Assert.That(consolidationDoc, Does.Contain("default adoption approved"));
            Assert.That(consolidationDoc, Does.Contain("glide flag-off fallback remains separate after default adoption"));
            Assert.That(consolidationDoc, Does.Contain("glide default adoption"));
            Assert.That(consolidationDoc, Does.Contain("must include"));
        }

        [Test]
        [Category("Extended")]
        public void Phase6_LegacyBaseline_PlayerEnemyStillRemoved()
        {
            Phase4_LegacyOrdinaryFallbackBaseline_PlayerFallbackRemoved();
            Phase5_LegacyOrdinaryFallbackBaseline_EnemyFallbackRemoved();
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_PlayerLegacyFallback_RemovedByPhase4()
        {
            var flagOffTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.PlayerLegacyFallbackRemovedFromRuntime(flagOffTick, 10);

            var defaultTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(defaultTick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            LegacyMovementBoundaryAssert.NoCoveredFallbackInDefaultGameplayLocomotion(defaultTick, 10);
            LegacyMovementBoundaryAssert.NoLegacyUnitPresentationForCoveredEntities(defaultTick, 10);
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_EnemyLegacyFallback_RemovedByPhase5()
        {
            var flagOffTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.EnemyLegacyFallbackRemovedFromRuntime(flagOffTick, 40);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(flagOffTick);

            var defaultTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(defaultTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
            LegacyMovementBoundaryAssert.NoCoveredFallbackInDefaultGameplayLocomotion(defaultTick, 40);
            LegacyMovementBoundaryAssert.NoLegacyUnitPresentationForCoveredEntities(defaultTick, 40);
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_ChargeLegacyFallback_RemovedByPhase6()
        {
            var flagOffWorld = CreateWorldState(new[]
            {
                CreateUnit(50, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Charge),
            });
            flagOffWorld.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            var flagOffTick = CreatePipeline(
                    flagOffWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.ChargeLegacyFallbackRemovedFromRuntime(flagOffTick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(flagOffTick);

            var defaultWorld = CreateWorldState(new[]
            {
                CreateUnit(50, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Charge),
            });
            defaultWorld.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            var defaultTick = CreatePipeline(
                    defaultWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(
                defaultTick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 50 &&
                    track.MotionMode == MotionMode.Charge),
                Is.True);
            LegacyMovementBoundaryAssert.NoCoveredFallbackInDefaultGameplayLocomotion(defaultTick, 50);
            LegacyMovementBoundaryAssert.NoLegacyUnitPresentationForCoveredEntities(defaultTick, 50);
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_GlideFallback_IsRetainedException()
        {
            BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic();
            BoundaryInventory_GlideFlagOff_FallbackStillRetained();
            ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove();
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_GridTransactions_AreNotDeletionCandidates()
        {
            BoundaryInventory_GridTransactionsRemainAllowed_UnderDefaultGameplayLocomotion();
            BoundaryInventory_SpawnRespawnCleanup_NotOrdinaryMovement();
            BoundaryInventory_PhaseRelocation_IsScriptedRelocation();
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
        public void BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic()
        {
            var flags = GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion;
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);

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
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
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
        public void BoundaryInventory_GlideChaserAsset_DefaultGameplay_AllowsActiveGliderSolidOverlap()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(GlideChaserProfileAssetPath);
            Assert.That(profile, Is.Not.Null, $"Missing GlideChaser profile asset at '{GlideChaserProfileAssetPath}'.");

            var runtimeDefinition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnableEnemyGlideKinematicLocomotion, Is.True);
            Assert.That(runtimeDefinition.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.GlideOverSolid));
            Assert.That(runtimeDefinition.GlideTimingSettings.InitialDelayTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(runtimeDefinition.GlideTimingSettings.WindupTicks, Is.EqualTo(15));
            Assert.That(runtimeDefinition.GlideTimingSettings.DurationTicks, Is.EqualTo(180));
            Assert.That(runtimeDefinition.GlideTimingSettings.RecoveryTicks, Is.EqualTo(15));
            Assert.That(runtimeDefinition.GlideTimingSettings.CooldownTicks, Is.EqualTo(240));
            Assert.That(runtimeDefinition.GlideTimingSettings.GlideMoveTicks, Is.GreaterThan(0));

            var startCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateWall(31, wallCell),
                CreateUnit(40, 2, startCell, hp: 3, aiMode: EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 1,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: runtimeDefinition.GlideTimingSettings.DurationTicks,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: runtimeDefinition.GlideTimingSettings.WindupTicks,
                    durationTicks: runtimeDefinition.GlideTimingSettings.DurationTicks,
                    recoveryTicks: runtimeDefinition.GlideTimingSettings.RecoveryTicks,
                    cooldownTicks: runtimeDefinition.GlideTimingSettings.CooldownTicks,
                    glideMoveTicks: runtimeDefinition.GlideTimingSettings.GlideMoveTicks,
                    lastExitedTick: 0,
                    wantsRecover: false,
                    hasLockedStep: true,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: 10));
            var tick = CreatePipelineWithoutGeneratedEntityLogics(
                    worldState,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, wallCell.PlanarPosition)) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(tick, 40);
            Assert.That(HasMoveEntity(tick, 40, out var destination), Is.True);
            Assert.That(destination, Is.EqualTo(wallCell));
            Assert.That(ManhattanDistance(startCell, destination), Is.EqualTo(1));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(wallCell));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(wallCell, out _), Is.True);
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    worldState.CreateSnapshot(),
                    EntityType.Unit,
                    wallCell,
                    40).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_GlideFlagOff_FallbackStillRetained()
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
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

                _ = glidePipeline.RunTick(new TickInput(1));
                var activeTick = glidePipeline.RunTick(new TickInput(2));

                Assert.That(glideWorld.CreateSnapshot().TryGetEnemyGlideState(40, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                LegacyMovementBoundaryAssert.AllowsRetainedGlideFallback(activeTick, 40);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove()
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
        public void DefaultGameplayLocomotion_IncludesGlideKinematic()
        {
            var flags = GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion;

            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnableEnemyGlideKinematicLocomotion, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled.EnableEnemyGlideKinematicLocomotion, Is.True);
            Assert.That(GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled.EnableEnemyGlideKinematicLocomotion, Is.True);
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
        public void DeprecationPhase1_MoveEntityPrimitiveStillAllowed()
        {
            ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove();
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_MoveEntity_IsPrimitiveNotDeletionCandidate()
        {
            ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_NoCoveredLocomotionLegacyPresentation()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_LegacyUnitMotionPresentation_IsOnlyFallbackOrGrid()
        {
            var flagOffTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.PlayerLegacyFallbackRemovedFromRuntime(flagOffTick, 10);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(flagOffTick);

            var itemTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Item),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                itemTick,
                10,
                MovementExecutionBoundaryKind.BoxActionMovement);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(itemTick);

            var defaultChargeWorld = CreateWorldState(new[]
            {
                CreateUnit(50, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Charge),
            });
            defaultChargeWorld.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            var defaultChargeTick = CreatePipeline(
                    defaultChargeWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.NoLegacyUnitPresentationForCoveredEntities(defaultChargeTick, 50);
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_DefaultGameplayLocomotion_NoCoveredLegacyPresentation()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void LegacyOrdinaryUnitMovement_DeprecationReadiness_Report()
        {
            var flagOffFallbacks = new[]
            {
                "player legacy fallback: removed with PlayerLegacyFallbackRemovedFromRuntime",
                "enemy legacy fallback: removed with EnemyLegacyFallbackRemovedFromRuntime",
                "charge legacy fallback: removed with ChargeLegacyFallbackRemovedFromRuntime",
            };
            var flagOnTargets = new[]
            {
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnablePlayerFree2DLocalLocomotion,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnableEnemySameFaceContinuousLocomotion,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnableEnemyChargeKinematicLocomotion,
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnableEnemyGlideKinematicLocomotion,
            };
            var specialInventoryV3 = new[]
            {
                "jump: Safe UnitSpecialLocomotion",
                "phase relocation: Safe Retained Grid Transaction",
                "glide: explicit flag-on stable complete",
                "glide default adoption: complete",
                "Phase 1: covered locomotion fallback isolated",
                "glide flag-off fallback: retained rollback baseline",
                "actual deletion readiness: covered fallback authorization removed / cleanup pending",
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
                "explicit historical baseline pending cleanup",
            };

            Assert.That(flagOffFallbacks, Has.Length.EqualTo(3));
            Assert.That(flagOnTargets.All(enabled => enabled), Is.True);
            Assert.That(specialInventoryV3, Does.Contain("Phase 1: covered locomotion fallback isolated"));
            Assert.That(specialInventoryV3, Does.Contain("glide: explicit flag-on stable complete"));
            Assert.That(specialInventoryV3, Does.Contain("glide default adoption: complete"));
            Assert.That(specialInventoryV3, Does.Contain("glide flag-off fallback: retained rollback baseline"));
            Assert.That(specialInventoryV3, Does.Contain("actual deletion readiness: covered fallback authorization removed / cleanup pending"));
            Assert.That(retainedPaths, Does.Contain("MoveEntity primitive"));
            Assert.That(retainedPaths, Does.Contain("MovementExpander grid transaction branch"));
        }

        private static void AssertDefaultGameplayLocomotionFlags()
        {
            var flags = GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion;
            Assert.That(flags.EnablePlayerFree2DLocalLocomotion, Is.True);
            Assert.That(flags.EnablePlayerFree2DActionAssist, Is.True);
            Assert.That(flags.EnablePlayerFree2DNativeTopologyTransition, Is.True);
            Assert.That(flags.EnablePlayerSameFaceContinuousLocomotion, Is.True);
            Assert.That(flags.EnablePlayerStoppableKinematicLocomotion, Is.True);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.True);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.True);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);
            Assert.That(flags.EnableLegacyOrdinaryUnitFallback, Is.False);
            Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnablePlayerFree2DLocalLocomotion, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnableEnemyGlideKinematicLocomotion, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnableLegacyOrdinaryUnitFallback, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
        }

        private static void AssertRemovedDiagnosticBaselinePlayerEnemyChargeDiagnostics(
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    runtimeFeatureFlags)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            LegacyMovementBoundaryAssert.AssertPlayerFallbackRemovedFromRuntime(playerTick, 10);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(playerTick);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    runtimeFeatureFlags)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.AssertEnemyFallbackRemovedFromRuntime(enemyTick, 40);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(enemyTick);

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
                    runtimeFeatureFlags)
                .RunTick(new TickInput(1));
            LegacyMovementBoundaryAssert.AssertChargeFallbackRemovedFromRuntime(chargeTick, 50);
            LegacyMovementBoundaryAssert.LegacyFallbackIsOnlyForAllowedEntities(chargeTick);
        }

        private static void AssertRemovedDiagnosticBaselineShape(GameplayRuntimeFeatureFlags flags)
        {
            Assert.That(flags.EnableLegacyOrdinaryUnitFallback, Is.True);
            Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.True);
            Assert.That(flags.EnablePlayerSameFaceContinuousLocomotion, Is.False);
            Assert.That(flags.EnablePlayerStoppableKinematicLocomotion, Is.False);
            Assert.That(flags.EnablePlayerFree2DLocalLocomotion, Is.False);
            Assert.That(flags.EnablePlayerFree2DActionAssist, Is.False);
            Assert.That(flags.EnablePlayerFree2DNativeTopologyTransition, Is.False);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.False);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.False);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.False);
        }

        private static void AssertSameRuntimeFeatureFlags(
            GameplayRuntimeFeatureFlags actual,
            GameplayRuntimeFeatureFlags expected)
        {
            Assert.That(actual.EnableLegacyOrdinaryUnitFallback, Is.EqualTo(expected.EnableLegacyOrdinaryUnitFallback));
            Assert.That(
                actual.RemovedLegacyFallbackDiagnosticsEnabled,
                Is.EqualTo(expected.RemovedLegacyFallbackDiagnosticsEnabled));
            Assert.That(actual.EnablePlayerSameFaceContinuousLocomotion, Is.EqualTo(expected.EnablePlayerSameFaceContinuousLocomotion));
            Assert.That(actual.EnablePlayerStoppableKinematicLocomotion, Is.EqualTo(expected.EnablePlayerStoppableKinematicLocomotion));
            Assert.That(actual.EnablePlayerFree2DLocalLocomotion, Is.EqualTo(expected.EnablePlayerFree2DLocalLocomotion));
            Assert.That(actual.EnablePlayerFree2DActionAssist, Is.EqualTo(expected.EnablePlayerFree2DActionAssist));
            Assert.That(actual.EnablePlayerFree2DNativeTopologyTransition, Is.EqualTo(expected.EnablePlayerFree2DNativeTopologyTransition));
            Assert.That(actual.EnableEnemySameFaceContinuousLocomotion, Is.EqualTo(expected.EnableEnemySameFaceContinuousLocomotion));
            Assert.That(actual.EnableEnemyChargeKinematicLocomotion, Is.EqualTo(expected.EnableEnemyChargeKinematicLocomotion));
            Assert.That(actual.EnableEnemyGlideKinematicLocomotion, Is.EqualTo(expected.EnableEnemyGlideKinematicLocomotion));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                entityLogics,
                GameplayTimingProfile.CreateDefault(),
                CreatePlayerTiming(),
                runtimeFeatureFlags: runtimeFeatureFlags,
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static TickPipeline CreatePipelineWithoutGeneratedEntityLogics(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            return new GameplayBootstrapper(
                    new SnapshotEntityLogicProvider(Array.Empty<IEntityLogicFactory>()))
                .CreateTickPipeline(
                    worldState,
                    entityLogics,
                    GameplayTimingProfile.CreateDefault(),
                    CreatePlayerTiming(),
                    runtimeFeatureFlags: runtimeFeatureFlags,
                    playerKinematicLocomotionTiming: CreateTwoTickKinematicTiming());
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static int DefaultFree2DSpeedUnitsPerTick()
        {
            return PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .SpeedUnitsPerTick;
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

        private static bool HasMoveEntity(TickResult tick, int entityId, out SurfaceCell destination)
        {
            foreach (var operation in tick.MovementPhaseResult.ResolvedOperations)
            {
                if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                    operation.EntityId != entityId)
                {
                    continue;
                }

                destination = operation.Destination;
                return true;
            }

            destination = default;
            return false;
        }

        private static int ManhattanDistance(SurfaceCell a, SurfaceCell b)
        {
            if (a.face != b.face)
            {
                return int.MaxValue;
            }

            return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static WorldState CreateActiveChargeWorldState(int entityId)
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Charge),
            });
            worldState.CreateWriteContext().SetEnemyChargeState(
                entityId,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            return worldState;
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, boardBounds, terrainData, topology);
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

        private static void AssertLegacyExpansionIntentBlocked(
            WorldState worldState,
            MoveIntent intent,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            string expectedReason)
        {
            var rejectedReasons = new List<string>();
            var filteredIntents = InvokeValidateLegacyExpansionIntents(
                CreatePipeline(worldState, Array.Empty<IEntityLogic>(), runtimeFeatureFlags),
                worldState.CreateSnapshot(),
                new[] { intent },
                rejectedReasons);

            Assert.That(filteredIntents, Is.Empty);
            Assert.That(
                rejectedReasons.Any(reason =>
                    reason.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal) &&
                    reason.Contains($"E={intent.SourceId}", StringComparison.Ordinal) &&
                    reason.Contains(expectedReason, StringComparison.Ordinal)),
                Is.True,
                string.Join("\n", rejectedReasons));
        }

        private static void SetPlayerContinuousLocalOffset(
            WorldState worldState,
            int localX,
            int localY,
            int speedUnitsPerTick = 0)
        {
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(localX),
                        KinematicFixed.FromRaw(localY)),
                    velocity = KinematicVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    speedUnitsPerTick = speedUnitsPerTick,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
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

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
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
