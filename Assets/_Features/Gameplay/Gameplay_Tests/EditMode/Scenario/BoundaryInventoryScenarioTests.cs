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

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class BoundaryInventoryScenarioTests
    {
        private const string GlideChaserProfileAssetPath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_GlideChaser/EnemyAi_GlideChaser.asset";

        private static EnemyAiProfile defaultEnemyProfile;
        private static EnemyAiProfile chargeEnemyProfile;

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            defaultEnemyProfile = EnemyAiProfileTestFactory.CreateNonAttacking();
            chargeEnemyProfile = EnemyAiProfileTestFactory.CreateCharging();
        }

        [OneTimeTearDown]
        public static void OneTimeTearDown()
        {
            EnemyAiProfileTestFactory.Destroy(defaultEnemyProfile);
            EnemyAiProfileTestFactory.Destroy(chargeEnemyProfile);
            defaultEnemyProfile = null;
            chargeEnemyProfile = null;
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement()
        {
            AssertDefaultGameplayLocomotionFlags();

            var playerWorld = CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) });
            var playerTick = CreatePipeline(
                    playerWorld,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(playerTick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            MovementExecutionOwnershipAssert.NoCoveredLocomotionGenericExpansionOwned(playerTick, 10);
            EnemyOrdinary_DefaultGameplay_UsesKinematicAndNoGenericExpansionOwned();
            EnemyCharge_DefaultGameplay_KinematicNoGenericExpansionOwned();
        }

        [Test]
        [Category("Core")]
        public void EnemyOrdinary_DefaultGameplay_UsesKinematicAndNoGenericExpansionOwned()
        {
            AssertDefaultGameplayLocomotionFlags();
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
            MovementExecutionOwnershipAssert.NoCoveredLocomotionGenericExpansionOwned(enemyTick, 40);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(enemyTick);
        }

        [Test]
        [Category("Core")]
        public void EnemyCharge_DefaultGameplay_KinematicNoGenericExpansionOwned()
        {
            AssertDefaultGameplayLocomotionFlags();
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
            MovementExecutionOwnershipAssert.NoCoveredLocomotionGenericExpansionOwned(chargeTick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(chargeTick);
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_PlayerEnemyCharge_NoGenericExpansionOwned()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_PlayerOrdinary_NoGenericExpansionOwned()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_EnemyOrdinary_NoGenericExpansionOwned()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_DefaultGameplayLocomotion_ChargeActive_NoGenericExpansionOwned()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
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
            MovementExecutionOwnershipAssert.NoCoveredLocomotionGenericExpansionOwned(tick, 10);
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_PlayerFree2DCompatibilityPreset_DoesNotEmitPlayerKinematic()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(
                tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10),
                Is.True);
            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 10), Is.False);
            MovementExecutionOwnershipAssert.NoCoveredLocomotionGenericExpansionOwned(tick, 10);
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
            MovementExecutionOwnershipAssert.NoCoveredLocomotionGenericExpansionOwned(tick, 40);
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
            MovementExecutionOwnershipAssert.NoCoveredLocomotionGenericExpansionOwned(tick, 50);
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_CurrentOwnershipBaseline_NoCoveredEntityMove()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            MovementExecutionOwnershipAssert.PlayerGenericExpansionRemovedFromRuntime(playerTick, 10);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(playerTick);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.EnemyGenericExpansionCurrentOwnershipBaseline(enemyTick, 40);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(enemyTick, 40);

            var chargeTick = CreatePipeline(
                    CreateActiveChargeWorldState(50),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.ChargeActiveRejectedBeforeGenericExpansion(chargeTick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(chargeTick);
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerGenericExpansionOwned_DefaultGameplayLocomotion_NoGenericExpansionOwned()
        {
            AssertDefaultGameplayLocomotionFlags();

            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            MovementExecutionOwnershipAssert.NoPlayerLegacyOrdinaryFallback(tick, 10);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerGenericExpansionOwned_Free2DFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var intent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertPartitionConsumesPlayerFree2DOrdinaryIntent(worldState, intent);
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerGenericExpansionOwned_KinematicFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var intent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertPartitionConsumesPlayerFree2DOrdinaryIntent(worldState, intent);
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase4 canary: delegates to the canonical player removed-diagnostic test.
        public void Phase2_PlayerGenericExpansionOwned_FlagOffBaseline_RemovedByPhase4()
        {
            Phase4_CurrentOwnershipBaseline_PlayerFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_CurrentOwnershipBaseline_PlayerFallbackRemoved()
        {
            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            MovementExecutionOwnershipAssert.PlayerGenericExpansionRemovedFromRuntime(tick, 10);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerGenericExpansionOwned_TopologySeam_UsesNativeFree2DOnly()
        {
            Player_Free2D_TopologySeam_NoLegacyOrdinaryFallback();

            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var approachWorld = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(approachWorld, localX: 0, localY: SimulationFixed.MaxPositiveLocalOffset, speedUnitsPerTick: speed);
            var approachPipeline = CreatePipeline(
                approachWorld,
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            var handoffTick = approachPipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            MovementExecutionOwnershipAssert.GridTransactionBranchesRemainAllowed(
                handoffTick,
                10,
                MovementExecutionBoundaryKind.Free2DTopologyTransition);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(handoffTick);
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
            Phase2_PlayerGenericExpansionOwned_DefaultGameplayLocomotion_NoGenericExpansionOwned();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_PlayerCompatibilityPreset_NoGenericExpansionOwned()
        {
            Phase2_PlayerGenericExpansionOwned_KinematicFlagOn_BlockedBeforeMovementExpander();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_PlayerTopologySeam_StillNativeFree2DOnly()
        {
            Phase2_PlayerGenericExpansionOwned_TopologySeam_UsesNativeFree2DOnly();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_CurrentOwnershipBaseline_PlayerFallbackStillRemoved()
        {
            Phase4_CurrentOwnershipBaseline_PlayerFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyGenericExpansionOwned_DefaultGameplayLocomotion_NoGenericExpansionOwned()
        {
            AssertDefaultGameplayLocomotionFlags();

            var tick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
            MovementExecutionOwnershipAssert.NoEnemyGenericExpansionOrdinaryFallback(tick, 40);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase5_DefaultGameplay_EnemyFallbackStillAbsent()
        {
            Phase2B_EnemyGenericExpansionOwned_DefaultGameplayLocomotion_NoGenericExpansionOwned();
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyGenericExpansionOwned_KinematicFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) });
            var intent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertPartitionKeepsGenericExpansionIntent(worldState, intent);
        }

        [Test]
        [Category("Extended")]
        public void Phase5_EnemyKinematicFlagOn_NoGenericExpansionOwned()
        {
            Phase2B_EnemyGenericExpansionOwned_KinematicFlagOn_BlockedBeforeMovementExpander();
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase5 canary: delegates to the canonical enemy removed-diagnostic test.
        public void Phase2B_EnemyGenericExpansionOwned_FlagOffBaseline_RemovedByPhase5()
        {
            Phase5_CurrentOwnershipBaseline_EnemyFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_CurrentOwnershipBaseline_EnemyFallbackRemoved()
        {
            EnemyOrdinary_NonePolicy_GenericExpansionCurrentOwnershipBaseline();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_CurrentOwnershipBaseline_EnemyFallbackRemovedByPhase5()
        {
            Phase5_CurrentOwnershipBaseline_EnemyFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyGenericExpansionOwned_GlideDefault_IsKinematic_NotEnemyOrdinaryPilot()
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
        public void Phase2B_EnemyGenericExpansionOwned_ChargeActive_IsOutOfScope()
        {
            ScopedDeletionPrep_ChargeGenericExpansionOwned_RemovedByPhase6();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeGenericExpansionOwned_DefaultGameplayLocomotion_NoChargeGenericExpansionFallback()
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
            MovementExecutionOwnershipAssert.NoChargeActiveGenericExpansionOwned(tick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Extended")]
        public void Phase6_DefaultGameplay_ChargeFallbackStillAbsent()
        {
            Phase2C_ChargeGenericExpansionOwned_DefaultGameplayLocomotion_NoChargeGenericExpansionFallback();
        }

        [Test]
        [Category("Core")]
        public void NoLegacyChargeEntityMotionOutput_DefaultGameplay()
        {
            Phase2C_ChargeGenericExpansionOwned_DefaultGameplayLocomotion_NoChargeGenericExpansionFallback();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputProducer_DefaultGameplay_Unreachable()
        {
            NoLegacyChargeEntityMotionOutput_DefaultGameplay();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputIsolation_DefaultGameplay_NoOutput()
        {
            LegacyChargeEntityMotionOutputProducer_DefaultGameplay_Unreachable();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeGenericExpansionOwned_ChargeKinematicFlagOn_BlockedBeforeMovementExpander()
        {
            var worldState = CreateActiveChargeWorldState(50);
            var intent = new MoveIntent(50, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertPartitionKeepsGenericExpansionIntent(worldState, intent);
        }

        [Test]
        [Category("Extended")]
        public void Phase6_None_ChargeFallbackStillBlocked()
        {
            EnemyCharge_NonePolicy_RejectedBeforeGenericExpansion();
        }

        [Test]
        [Category("Core")]
        public void NoLegacyChargeEntityMotionOutput_None()
        {
            Phase6_None_ChargeFallbackStillBlocked();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputProducer_None_Unreachable()
        {
            NoLegacyChargeEntityMotionOutput_None();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputIsolation_None_NoOutput()
        {
            LegacyChargeEntityMotionOutputProducer_None_Unreachable();
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase6 canary: delegates to the canonical Charge removed-diagnostic test.
        public void Phase2C_ChargeGenericExpansionOwned_FlagOffBaseline_RemovedByPhase6()
        {
            Phase6_CurrentOwnershipBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase6_CurrentOwnershipBaseline_ChargeFallbackRemoved()
        {
            EnemyCharge_NonePolicy_RejectedBeforeGenericExpansion();
        }

        [Test]
        [Category("Core")]
        public void NoLegacyChargeEntityMotionOutput_CurrentOwnershipBaseline()
        {
            Phase6_CurrentOwnershipBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputProducer_CurrentOwnershipBaseline_Unreachable()
        {
            NoLegacyChargeEntityMotionOutput_CurrentOwnershipBaseline();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputIsolation_CurrentOwnershipBaseline_NoOutput()
        {
            LegacyChargeEntityMotionOutputProducer_CurrentOwnershipBaseline_Unreachable();
        }

        [Test]
        [Category("Core")]
        public void RemovedLegacyChargeEntityMotionOutput_ChargePresentationSignal_StillUsedForKinematicCharge()
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
            MovementExecutionOwnershipAssert.NoChargeActiveGenericExpansionOwned(tick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputDeletion_DefaultGameplay_ChargePresentationStillWorks()
        {
            RemovedLegacyChargeEntityMotionOutput_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputDeletion_ChargeSignalStillEmitted()
        {
            RemovedLegacyChargeEntityMotionOutput_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputDeletion_TickKinematicMotionTrackStillEmitted()
        {
            RemovedLegacyChargeEntityMotionOutput_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputProducer_ChargeKinematicSignal_IsNotLegacyOutput()
        {
            RemovedLegacyChargeEntityMotionOutput_ChargePresentationSignal_StillUsedForKinematicCharge();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputProducer_AllKinematic_Unreachable()
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
            MovementExecutionOwnershipAssert.NoChargeActiveGenericExpansionOwned(tick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputIsolation_AllKinematic_NoOutput()
        {
            LegacyChargeEntityMotionOutputProducer_AllKinematic_Unreachable();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputProducer_RuntimeReachabilityMatrix_IsCurrent()
        {
            LegacyChargeEntityMotionOutputProducer_DefaultGameplay_Unreachable();
            LegacyChargeEntityMotionOutputProducer_None_Unreachable();
            LegacyChargeEntityMotionOutputProducer_CurrentOwnershipBaseline_Unreachable();
            LegacyChargeEntityMotionOutputProducer_AllKinematic_Unreachable();
            LegacyChargeEntityMotionOutputProducer_ChargeKinematicSignal_IsNotLegacyOutput();
        }

        [Test]
        [Category("Extended")]
        public void Phase4_CurrentOwnershipBaseline_ChargeFallbackRemovedByPhase6()
        {
            Phase6_CurrentOwnershipBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase5_CurrentOwnershipBaseline_ChargeFallbackRemovedByPhase6()
        {
            Phase6_CurrentOwnershipBaseline_ChargeFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeGenericExpansionOwned_PlayerEnemyOrdinary_AreOutOfScope()
        {
            Phase2_PlayerGenericExpansionOwned_DefaultGameplayLocomotion_NoGenericExpansionOwned();
            Phase2B_EnemyGenericExpansionOwned_DefaultGameplayLocomotion_NoGenericExpansionOwned();
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeGenericExpansionOwned_GlideFlagOff_IsRetainedException_NotChargePilot()
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
            MovementExecutionOwnershipAssert.GridTransactionsRemainAllowed(pushTick, 30, MovementExecutionBoundaryKind.BoxActionMovement);
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
            MovementExecutionOwnershipAssert.GridTransactionsRemainAllowed(flipTick, 30, MovementExecutionBoundaryKind.BoxActionMovement);
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
            MovementExecutionOwnershipAssert.GridTransactionsRemainAllowed(itemTick, 10, MovementExecutionBoundaryKind.BoxActionMovement);
            Assert.That(itemTick.PresentationData.EntityMotions.Any(motion => motion.EntityId == 10), Is.True);

            var topologyWorld = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)));
            topologyWorld.CreateWriteContext().SetPlayerControlState(10, default);
            var topologyIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(0, 2));
            topologyIntent.AssignIntentId(1);
            AssertPartitionConsumesPlayerFree2DOrdinaryIntent(topologyWorld, topologyIntent);

            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(pushTick);
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(flipTick);
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(itemTick);
        }

        [Test]
        [Category("Core")]
        public void Phase3_DefaultGameplayLocomotion_NoCoveredFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void Phase3_None_NoPlayerEnemyChargeGenericExpansionOwned()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            MovementExecutionOwnershipAssert.NoCoveredFallbackUnderNone(playerTick, 10);

            EnemyOrdinary_NonePolicy_GenericExpansionCurrentOwnershipBaseline();
            EnemyCharge_NonePolicy_RejectedBeforeGenericExpansion();
        }

        [Test]
        [Category("Core")]
        public void EnemyOrdinary_NonePolicy_GenericExpansionCurrentOwnershipBaseline()
        {
            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.EnemyGenericExpansionCurrentOwnershipBaseline(enemyTick, 40);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(enemyTick, 40);
        }

        [Test]
        [Category("Core")]
        public void EnemyCharge_NonePolicy_RejectedBeforeGenericExpansion()
        {
            var chargeWorld = CreateActiveChargeWorldState(50);
            var chargeTick = CreatePipeline(
                    chargeWorld,
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(50, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.ChargeActiveRejectedBeforeGenericExpansion(chargeTick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(chargeTick);
        }

        [Test]
        [Category("Core")]
        public void Phase3_None_DoesNotMeanLegacyOrdinaryFallback()
        {
            Phase3_None_NoPlayerEnemyChargeGenericExpansionOwned();
        }

        [Test]
        [Category("Core")]
        public void Phase3_CurrentOwnershipBaseline_DoesNotEnableGlideKinematic()
        {
            var flags = GameplayRuntimeFeatureFlags.None;            Assert.That(flags.EnablePlayerFree2DActionAssist, Is.False);
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
            MovementExecutionOwnershipAssert.GridTransactionsRemainAllowedWithoutGenericExpansionOwned(
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
            MovementExecutionOwnershipAssert.GridTransactionsRemainAllowedWithoutGenericExpansionOwned(
                itemTick,
                10,
                MovementExecutionBoundaryKind.BoxActionMovement);

            var topologyWorld = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)));
            topologyWorld.CreateWriteContext().SetPlayerControlState(10, default);
            var topologyIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(0, 2));
            topologyIntent.AssignIntentId(1);
            AssertPartitionConsumesPlayerFree2DOrdinaryIntent(topologyWorld, topologyIntent);
        }

        [Test]
        [Category("Core")]
        public void UnhandledExpansionPartition_DoesNotMeanLegacyMovement()
        {
            var enemyWorld = CreateWorldState(new[]
            {
                CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase),
            });
            var enemyIntent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            enemyIntent.AssignIntentId(1);
            AssertPartitionKeepsGenericExpansionIntent(enemyWorld, enemyIntent);
            EnemyOrdinary_DefaultGameplay_UsesKinematicAndNoGenericExpansionOwned();

            var chargeWorld = CreateActiveChargeWorldState(50);
            var chargeIntent = new MoveIntent(50, priority: 100, destination: new Vector2Int(1, 0));
            chargeIntent.AssignIntentId(2);
            AssertPartitionKeepsGenericExpansionIntent(chargeWorld, chargeIntent);
            EnemyCharge_NonePolicy_RejectedBeforeGenericExpansion();
        }

        [Test]
        [Category("Core")]
        public void SharedExpansionPartition_RemainsAvailableForNonEnemyPaths()
        {
            BoundaryInventory_GridTransactionsRemainAllowed_UnderDefaultGameplayLocomotion();
            Phase3_None_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void ForwardCellMove_SharedPresentationVocabulary_NotEnemyFallback()
        {
            Assert.That((int)ResolvedActionSemanticKind.ForwardCellMove, Is.EqualTo(7));
            Assert.That((int)MovementSemanticKind.ForwardCellMove, Is.EqualTo(7));
            Assert.That(TickEntityMotionKind.ForwardCellMove, Is.Not.EqualTo(TickEntityMotionKind.Move));
            EnemyOrdinary_DefaultGameplay_UsesKinematicAndNoGenericExpansionOwned();
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
        public void Player_Free2D_TopologySeam_NoLegacyOrdinaryFallback()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void BoundaryInventory_Free2DTopologyNativeTransition_IsNotGenericExpansionOwned()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(
                        SimulationFixed.FromRaw(256),
                        SimulationFixed.FromRaw(SimulationFixed.MaxPositiveLocalOffset)),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(tick);
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.True);
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned),
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
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateTileFeature(100, blockedCell, TileFeatureKind.Barricade) });
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(
                        SimulationFixed.FromRaw(256),
                        SimulationFixed.FromRaw(SimulationFixed.MaxPositiveLocalOffset)),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());

            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None,
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
        public void Player_Free2D_TopologyHandoff_BlockedFromLegacyExpansion()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var topologyIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(0, 2));
            topologyIntent.AssignIntentId(1);

            AssertPartitionConsumesPlayerFree2DOrdinaryIntent(worldState, topologyIntent);

            var tick = CreatePipeline(
                    worldState,
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            Assert.That(
                tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
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
            Phase3_None_NoPlayerEnemyChargeGenericExpansionOwned();
        }

        [Test]
        [Category("Core")]
        public void Phase7_DefaultGameplay_NoCoveredFallback()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
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
        public void BoundaryInventory_CurrentOwnershipBaseline_PlayerEnemyChargeRemoved()
        {
            Phase7_GenericExpansionOwnedBaseline_IsDiagnosticCompatibilityPreset();
        }

        [Test]
        [Category("Core")]
        public void Phase6_CurrentOwnershipBaseline_PlayerEnemyChargeRemoved()
        {
            Phase7_GenericExpansionOwnedBaseline_IsDiagnosticCompatibilityPreset();
        }

        [Test]
        [Category("Core")]
        public void Phase8C_CurrentOwnershipBaseline_IsCanonicalUsage()
        {
            Phase7_GenericExpansionOwnedBaseline_IsDiagnosticCompatibilityPreset();
        }

        [Test]
        [Category("Core")]
        public void Phase8C_CurrentPolicyDocs_UseCurrentOwnershipBaseline()
        {
            var free2D = ReadRepoFile("Docs/Testing/Player-Free2D-Continuous-Locomotion-Rollout-2026-05-01.md");
            var adr = ReadRepoFile("Docs/Architecture/ADR/ADR-005-Grid-Authoritative-Unit-Kinematics.md");

            Assert.That(free2D, Does.Contain("Player ordinary movement is now unconditionally Free2D-owned in production."));
            Assert.That(free2D, Does.Not.Contain("EnablePlayerFree2DLocalLocomotion"));
            Assert.That(adr, Does.Contain("the removed Player legacy fallback diagnostic flag, constructor parameter, trace token, and helper vocabulary are no longer current API"));
        }

        [Test]
        [Category("Core")]
        public void Phase7_GenericExpansionOwnedBaseline_IsDiagnosticCompatibilityPreset()
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            MovementExecutionOwnershipAssert.AssertPlayerFallbackRemovedFromRuntime(playerTick, 10);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(playerTick);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.AssertEnemyFallbackRemovedFromRuntime(enemyTick, 40);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(enemyTick, 40);

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
            MovementExecutionOwnershipAssert.AssertChargeFallbackRemovedFromRuntime(chargeTick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(chargeTick);
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_CurrentOwnershipBaseline_PlayerEnemyChargeRemoved()
        {
            BoundaryInventory_CurrentOwnershipBaseline_PlayerEnemyChargeRemoved();
        }

        [Test]
        [Category("Core")]
        public void DiagnosticRoutingFlag_IsDeleted()
        {
            var runtimeFlags = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayRuntimeFeatureFlags.cs");

            Assert.That(runtimeFlags, Does.Not.Contain("RemovedLegacy" + "FallbackDiagnosticsEnabled"));
            Assert.That(runtimeFlags, Does.Not.Contain("removedLegacy" + "FallbackDiagnosticsEnabled"));
        }

        [Test]
        [Category("Core")]
        public void CurrentOwnershipCanaries_StillPass()
        {
            Phase7_None_NoCoveredFallback();
            Phase7_DefaultGameplay_NoCoveredFallback();
            Phase7_GridTransactionsRemainAllowed();
            Phase7_GlidePolicy_DefaultAdoptedAndFlagOffFallbackRetained();
        }

        [Test]
        [Category("Core")]
        public void Phase8E_GridTransactionsRemainAllowed()
        {
            Phase7_GridTransactionsRemainAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase8E_GlideDefaultAdoptionAndFlagOffFallbackRetained()
        {
            Phase7_GlidePolicy_DefaultAdoptedAndFlagOffFallbackRetained();
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
            Assert.That(consolidationDoc, Does.Contain("Removed fallback diagnostics canonical migration"));
            Assert.That(consolidationDoc, Does.Contain("No old-name compatibility projection remains"));
            Assert.That(consolidationDoc, Does.Not.Contain("replay/golden rewrite | defer"));
            Assert.That(readinessDoc, Does.Contain("Legacy Compatibility Layer Consolidation"));
            Assert.That(readinessDoc, Does.Contain("stops extending the micro-phase chain"));
            Assert.That(adr, Does.Contain("moves from stepwise fallback removal to `Legacy Compatibility Layer Consolidation`"));
            Assert.That(consolidationDoc, Does.Not.Contain("allows covered fallback"));
            Assert.That(readinessDoc, Does.Not.Contain("fallback allowed"));
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputDeletion_Docs_RecordHistoricalRemoval()
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
        public void LegacyChargeEntityMotionOutputDeletion_PresentationConsumers_RemovalIsRecorded()
        {
            LegacyChargeEntityMotionOutputDeletion_Docs_RecordHistoricalRemoval();
        }

        [Test]
        [Category("Core")]
        public void LegacyChargeEntityMotionOutputResidue_YamlResidueReport_IsCurrent()
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
        public void LegacyChargeEntityMotionOutputResidue_PresentationAuthoring_RuntimeReadRemoved()
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
        public void LegacyChargeEntityMotionOutputDeletion_NoReferencesRemain()
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
        public void LegacyChargeEntityMotionOutputDeletion_MovePresentationUnaffected()
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
        public void LegacyChargeEntityMotionOutputDeletion_RetainedGridTransactionsUnaffected()
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
        public void Phase6_CurrentOwnershipBaseline_PlayerEnemyStillRemoved()
        {
            Phase4_CurrentOwnershipBaseline_PlayerFallbackRemoved();
            Phase5_CurrentOwnershipBaseline_EnemyFallbackRemoved();
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_PlayerGenericExpansionOwned_RemovedByPhase4()
        {
            var flagOffTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            MovementExecutionOwnershipAssert.PlayerGenericExpansionRemovedFromRuntime(flagOffTick, 10);

            var defaultTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(defaultTick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            MovementExecutionOwnershipAssert.NoCoveredFallbackInDefaultGameplayLocomotion(defaultTick, 10);
            MovementExecutionOwnershipAssert.NoLegacyUnitPresentationForCoveredEntities(defaultTick, 10);
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_EnemyGenericExpansionOwned_RemovedByPhase5()
        {
            var flagOffTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.EnemyGenericExpansionRemovedFromRuntime(flagOffTick, 40);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(flagOffTick, 40);

            var defaultTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));

            Assert.That(defaultTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
            MovementExecutionOwnershipAssert.NoCoveredFallbackInDefaultGameplayLocomotion(defaultTick, 40);
            MovementExecutionOwnershipAssert.NoLegacyUnitPresentationForCoveredEntities(defaultTick, 40);
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_ChargeGenericExpansionOwned_RemovedByPhase6()
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
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.ChargeGenericExpansionRemovedFromRuntime(flagOffTick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(flagOffTick);

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
            MovementExecutionOwnershipAssert.NoCoveredFallbackInDefaultGameplayLocomotion(defaultTick, 50);
            MovementExecutionOwnershipAssert.NoLegacyUnitPresentationForCoveredEntities(defaultTick, 50);
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
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(cleanupTick);

            var respawnTick = pipeline.RunTick(new TickInput(2));
            Assert.That(
                respawnTick.PresentationData.TransitionVisibilityChanges.Any(change => change.EntityId == 10) ||
                respawnTick.EventLog.Any(entry => entry.Contains("RespawnCommitted|E=10", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                respawnTick.Trace.Text.Contains("Boundary=SpawnRespawnPlacement", StringComparison.Ordinal) ||
                respawnTick.Trace.Text.Contains("BoundaryReason=PlayerRespawnPlacement", StringComparison.Ordinal),
                Is.True);
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(respawnTick);
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
                MovementExecutionOwnershipAssert.HasOperationBoundary(
                    jumpTick,
                    40,
                    FinalizationOperationKind.MoveEntity,
                    MovementExecutionBoundaryKind.UnitSpecialLocomotion);
                MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(jumpTick);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(jumpProfile);
            }

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
                MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(glideTick);
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

                MovementExecutionOwnershipAssert.HasOperationBoundary(
                    jumpTick,
                    40,
                    FinalizationOperationKind.MoveEntity,
                    MovementExecutionBoundaryKind.UnitSpecialLocomotion);
                MovementExecutionOwnershipAssert.HasOperationBoundary(
                    jumpTick,
                    40,
                    FinalizationOperationKind.SetEnemyJumpState,
                    MovementExecutionBoundaryKind.UnitSpecialLocomotion);
                MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(jumpTick);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(jumpProfile);
            }
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
                MovementExecutionOwnershipAssert.NoGenericExpansionOrdinaryUnitMoveOperationOrDiagnostic(glideTick, 40);
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
                MovementExecutionOwnershipAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(windupTick, 40);
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
                        unitKinematicLocomotionTiming: CreateTwoTickKinematicTiming());

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
                MovementExecutionOwnershipAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(activeTick, 40);
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
                    MovementExecutionOwnershipAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(tick, 40);
                    if (HasGlideActiveKinematicAnchorCommit(tick, 40))
                    {
                        commitTick = tick;
                    }
                }

                Assert.That(commitTick, Is.Not.Null);
                MovementExecutionOwnershipAssert.HasMoveEntityBoundaryReason(
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
            Assert.That(runtimeDefinition.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None));
            Assert.That(runtimeDefinition.TryGetGlideBehavior(out var glide), Is.True);
            Assert.That(glide.Key, Is.EqualTo(EnemyBehaviorModuleKey.Glide));
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

            MovementExecutionOwnershipAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(tick, 40);
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
                    40,
                    TileFeatureSettlementEvidence.Empty).Verdict,
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
                MovementExecutionOwnershipAssert.AllowsRetainedGlideFallback(activeTick, 40);
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
                        unitKinematicLocomotionTiming: CreateTwoTickKinematicTiming());

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
                MovementExecutionOwnershipAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(activeTick, 40);
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
                    MovementExecutionOwnershipAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(tick, 40);
                    if (HasGlideActiveKinematicAnchorCommit(tick, 40))
                    {
                        commitTick = tick;
                    }
                }

                Assert.That(commitTick, Is.Not.Null);
                MovementExecutionOwnershipAssert.HasMoveEntityBoundaryReason(
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
            MovementExecutionOwnershipAssert.NoForcedKinematicProducer(playerTick, 10);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.NoForcedKinematicProducer(enemyTick, 40);

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
            MovementExecutionOwnershipAssert.NoForcedKinematicProducer(chargeTick, 50);
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
            MovementExecutionOwnershipAssert.NoUnexpectedUnknownMovementBoundaryAllowingStateOnly(playerTick);

            var pushTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0), MovementCommandKind.Push)) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.NoUnexpectedUnknownMovementBoundaryAllowingStateOnly(pushTick);

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
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_LegacyUnitMotionPresentation_IsOnlyFallbackOrGrid()
        {
            var flagOffTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    GameplayRuntimeFeatureFlags.None)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            MovementExecutionOwnershipAssert.PlayerGenericExpansionRemovedFromRuntime(flagOffTick, 10);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(flagOffTick);

            var itemTick = CreatePipeline(
                    CreateWorldState(new[]
                    {
                        CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Item),
                    }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(10, 100, new Vector2Int(1, 0))) },
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.GridTransactionBranchesRemainAllowed(
                itemTick,
                10,
                MovementExecutionBoundaryKind.BoxActionMovement);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(itemTick);

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
            MovementExecutionOwnershipAssert.NoLegacyUnitPresentationForCoveredEntities(defaultChargeTick, 50);
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_DefaultGameplayLocomotion_NoCoveredLegacyPresentation()
        {
            BoundaryInventory_DefaultGameplayLocomotion_NoGenericExpansionOrdinaryUnitMovement();
        }

        [Test]
        [Category("Core")]
        public void LegacyOrdinaryUnitMovement_DeprecationReadiness_Report()
        {
            var flagOffFallbacks = new[]
            {
                "player generic expansion: removed with PlayerGenericExpansionRemovedFromRuntime",
                "enemy generic expansion: current baseline with EnemyGenericExpansionCurrentOwnershipBaseline",
                "charge generic expansion: rejected before expansion with ChargeActiveRejectedBeforeGenericExpansion",
            };
            var flagOnTargets = new[]
            {
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion.EnablePlayerFree2DActionAssist,
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
            Assert.That(flags.EnablePlayerFree2DActionAssist, Is.True);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.True);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.True);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);            Assert.That(GameplayRuntimeFeatureFlags.None.EnablePlayerFree2DActionAssist, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnableEnemyGlideKinematicLocomotion, Is.False);        }

        private static void AssertCurrentOwnershipBaselinePlayerEnemyChargeDiagnostics(
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var playerTick = CreatePipeline(
                    CreateWorldState(new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) }),
                    new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                    runtimeFeatureFlags)
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            MovementExecutionOwnershipAssert.AssertPlayerFallbackRemovedFromRuntime(playerTick, 10);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(playerTick);

            var enemyTick = CreatePipeline(
                    CreateWorldState(new[] { CreateUnit(40, 2, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase) }),
                    new IEntityLogic[] { new ScriptedMovementLogic(1, new RawMovementIntent(40, 50, new Vector2Int(1, 0))) },
                    runtimeFeatureFlags)
                .RunTick(new TickInput(1));
            MovementExecutionOwnershipAssert.AssertEnemyFallbackRemovedFromRuntime(enemyTick, 40);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(enemyTick, 40);

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
            MovementExecutionOwnershipAssert.AssertChargeFallbackRemovedFromRuntime(chargeTick, 50);
            MovementExecutionOwnershipAssert.GenericExpansionOwnedIsOnlyForAllowedEntities(chargeTick);
        }

        private static void AssertCurrentOwnershipBaselineShape(GameplayRuntimeFeatureFlags flags)
        {            Assert.That(flags.EnablePlayerFree2DActionAssist, Is.False);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.False);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.False);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.False);
        }

        private static void AssertSameRuntimeFeatureFlags(
            GameplayRuntimeFeatureFlags actual,
            GameplayRuntimeFeatureFlags expected)
        {            Assert.That(actual.EnablePlayerFree2DActionAssist, Is.EqualTo(expected.EnablePlayerFree2DActionAssist));
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
            return GameplayCompositionRoot.CreateDefaultBootstrapper(ResolveDefaultEnemyProfile(worldState)).CreateTickPipeline(
                worldState,
                entityLogics,
                GameplayTimingProfile.CreateDefault(),
                CreatePlayerTiming(),
                runtimeFeatureFlags: runtimeFeatureFlags,
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static EnemyAiProfile ResolveDefaultEnemyProfile(WorldState worldState)
        {
            if (worldState == null)
            {
                return RequireDefaultEnemyProfile();
            }

            var snapshot = worldState.CreateSnapshot();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            for (var i = 0; i < entities.Count; i++)
            {
                if (entities[i].type == EntityType.Unit &&
                    entities[i].aiMode == EnemyAiMode.Charge)
                {
                    return RequireChargeEnemyProfile();
                }
            }

            return RequireDefaultEnemyProfile();
        }

        private static EnemyAiProfile RequireDefaultEnemyProfile()
        {
            Assert.That(defaultEnemyProfile, Is.Not.Null, "BoundaryInventory test default enemy profile was not initialized.");
            return defaultEnemyProfile;
        }

        private static EnemyAiProfile RequireChargeEnemyProfile()
        {
            Assert.That(chargeEnemyProfile, Is.Not.Null, "BoundaryInventory test charge enemy profile was not initialized.");
            return chargeEnemyProfile;
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
                    unitKinematicLocomotionTiming: CreateTwoTickKinematicTiming());
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

        private static UnitKinematicLocomotionTimingSnapshot CreateTwoTickKinematicTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new UnitKinematicLocomotionTimingSettings
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
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, boardBounds, topology);
        }

        private static void AssertPartitionKeepsGenericExpansionIntent(WorldState worldState, MoveIntent intent)
        {
            var partitions = MovementIntentPartitioner.Partition(
                worldState.CreateSnapshot(),
                new[] { intent });

            Assert.That(partitions.PlayerFree2DOrdinaryIntents.Select(candidate => candidate.IntentId), Is.Empty);
            Assert.That(partitions.GenericExpansionIntents.Select(candidate => candidate.IntentId), Is.EquivalentTo(new[] { intent.IntentId }));
        }

        private static void AssertPartitionConsumesPlayerFree2DOrdinaryIntent(WorldState worldState, MoveIntent intent)
        {
            var partitions = MovementIntentPartitioner.Partition(
                worldState.CreateSnapshot(),
                new[] { intent });

            Assert.That(partitions.PlayerFree2DOrdinaryIntents.Select(candidate => candidate.IntentId), Is.EquivalentTo(new[] { intent.IntentId }));
            Assert.That(partitions.GenericExpansionIntents.Select(candidate => candidate.IntentId), Is.Empty);
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
                    localOffset = new SimulationOffset2(
                        SimulationFixed.FromRaw(localX),
                        SimulationFixed.FromRaw(localY)),
                    velocity = SimulationVelocity2.Zero,
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
                boundEntityId: 0);
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
