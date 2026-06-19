using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Gameplay.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class EnemyAiScenarioTests
    {
        private static EnemyUnitArchetypeAsset SharedSummonedArchetype;
        private static EnemyAiProfile SharedSummonedProfile;

        private readonly struct WindupContractMetrics
        {
            public WindupContractMetrics(
                int targetSensedTick,
                int targetInRangeTick,
                int attackEntryTick,
                int actionStartTargetResolvedTick,
                int actionStartBlockedByMoveLockTick,
                int actionStartActiveWithoutSignalTick,
                int actionStartTick,
                int attackExecuteRawIntentTick,
                int attackExecuteRejectedByExecutionLockTick,
                int attackExecuteTick,
                int attackExecuteActionStateActiveTick,
                int attackExecuteExecutionAttemptedTick,
                int attackCommittedTraceTick,
                int attackCommittedRecoverTick,
                int recoverEntryTick,
                int recoverCompleteTick,
                int recoverTickCount,
                int recoverPatrolWriteCount,
                int firstCombatDamageTick)
            {
                TargetSensedTick = targetSensedTick;
                TargetInRangeTick = targetInRangeTick;
                AttackEntryTick = attackEntryTick;
                ActionStartTargetResolvedTick = actionStartTargetResolvedTick;
                ActionStartBlockedByMoveLockTick = actionStartBlockedByMoveLockTick;
                ActionStartActiveWithoutSignalTick = actionStartActiveWithoutSignalTick;
                ActionStartTick = actionStartTick;
                AttackExecuteRawIntentTick = attackExecuteRawIntentTick;
                AttackExecuteRejectedByExecutionLockTick = attackExecuteRejectedByExecutionLockTick;
                AttackExecuteTick = attackExecuteTick;
                AttackExecuteActionStateActiveTick = attackExecuteActionStateActiveTick;
                AttackExecuteExecutionAttemptedTick = attackExecuteExecutionAttemptedTick;
                AttackCommittedTraceTick = attackCommittedTraceTick;
                AttackCommittedRecoverTick = attackCommittedRecoverTick;
                RecoverEntryTick = recoverEntryTick;
                RecoverCompleteTick = recoverCompleteTick;
                RecoverTickCount = recoverTickCount;
                RecoverPatrolWriteCount = recoverPatrolWriteCount;
                FirstCombatDamageTick = firstCombatDamageTick;
            }

            public int TargetSensedTick { get; }

            public int TargetInRangeTick { get; }

            public int AttackEntryTick { get; }

            public int ActionStartTargetResolvedTick { get; }

            public int ActionStartBlockedByMoveLockTick { get; }

            public int ActionStartActiveWithoutSignalTick { get; }

            public int ActionStartTick { get; }

            public int AttackExecuteRawIntentTick { get; }

            public int AttackExecuteRejectedByExecutionLockTick { get; }

            public int AttackExecuteTick { get; }

            public int AttackExecuteActionStateActiveTick { get; }

            public int AttackExecuteExecutionAttemptedTick { get; }

            public int AttackCommittedTraceTick { get; }

            public int AttackCommittedRecoverTick { get; }

            public int AttackCommittedTick => AttackCommittedTraceTick != 0
                ? AttackCommittedTraceTick
                : AttackCommittedRecoverTick;

            public int RecoverEntryTick { get; }

            public int RecoverCompleteTick { get; }

            public int RecoverTickCount { get; }

            public int RecoverPatrolWriteCount { get; }

            public int FirstCombatDamageTick { get; }
        }

        private readonly struct LockedTargetLostMetrics
        {
            public LockedTargetLostMetrics(
                int activeWindupEntryTick,
                int cancelOwnerTick,
                int cancelTraceTick,
                EnemyAiMode fallbackMode,
                IReadOnlyList<int> homeDistanceSeries)
            {
                ActiveWindupEntryTick = activeWindupEntryTick;
                CancelOwnerTick = cancelOwnerTick;
                CancelTraceTick = cancelTraceTick;
                FallbackMode = fallbackMode;
                HomeDistanceSeries = homeDistanceSeries ?? Array.Empty<int>();
            }

            public int ActiveWindupEntryTick { get; }

            public int CancelOwnerTick { get; }

            public int CancelTraceTick { get; }

            public EnemyAiMode FallbackMode { get; }

            public IReadOnlyList<int> HomeDistanceSeries { get; }
        }

        private readonly struct WindupParityComparisonMetrics
        {
            public WindupParityComparisonMetrics(
                in WindupContractMetrics baseline,
                in WindupContractMetrics pilot,
                int firstDivergentPositionOrFacingTick)
            {
                Baseline = baseline;
                Pilot = pilot;
                FirstDivergentPositionOrFacingTick = firstDivergentPositionOrFacingTick;
            }

            public WindupContractMetrics Baseline { get; }

            public WindupContractMetrics Pilot { get; }

            public int FirstDivergentPositionOrFacingTick { get; }
        }

        [Test]
        [Category("Core")]
        public void EnemyAi_KinematicPatrolChaseAttackRecover_CurrentContract()
        {
            EnemyPatrol_KinematicMovement();
            EnemyMovesIntoPlayer_Kinematic_NoPassiveContactWithoutFinalizedSameCellMove();
            EnemyMovesIntoPlayer_Kinematic_PassiveContactFiresOnFinalizedSameCellMove();
            EnemyAi_WindupForwardCellProjectile_TelegraphsBeforeReleaseAndThenEntersRecover();
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_SummonedChild_UsesArchetypeAndMovesOnNextEligibleKinematicCommit()
        {
            BehaviorSummon_InitialDelayCooldownWindupRecoveryParity();
            BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder();
            BehaviorSummon_MaxAliveParity();
            BehaviorSummon_SourceLeavesTopologyCancelsOrSuspendsAsUtility();
        }

        [Test]
        [Category("Core")]
        public void EnemyAi_MoveOccupancy_BlocksAttackUntilKinematicCommitUnlocksRange()
        {
            EnemyMovesIntoPlayer_Kinematic_NoPassiveContactWithoutFinalizedSameCellMove();
            EnemyMovesIntoPlayer_Kinematic_PassiveContactFiresOnFinalizedSameCellMove();
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WallFollower_ChoosesLegalKinematicStep_AroundSolidOrEdge()
        {
            EnemyAi_WallFollowerProfile_PlayerInSenseRange_RemainsInPatrolPermanently();
            EnemyAi_WallFollowerProfile_WithLocomotionCooldown_PreservesWallFollowRule();
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpCooldown_DoesNotBlockCurrentPatrolLocomotion()
        {
            EnemyAi_JumpCooldown_SameFacePlayer_DoesNotRestartDuringCooldown();
            EnemyAi_JumpCooldown_Complete_WithSameFacePlayer_AllowsNewJump();
            EnemyAi_JumpCooldown_OpenGround_ChasesBeforeCooldownCompletes();
        }

        [Test]
        [Category("Core")]
        public void EnemyAi_Charger_CurrentPresentationAndContactContract()
        {
            EnemyCharge_KinematicFlag_ActiveStepUsesChargeKinematicMove();
            EnemyCharge_KinematicFlag_ContactStartsAtCommitAndConsumesStepAtSettle();
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_Charger_WindupRecoverLocksDirectionWithoutFallbackMove()
        {
            EnemyCharge_KinematicFlag_PatrolToChargeWaitsForOrdinarySettleThenUsesChargeKinematicMove();
            EnemyCharge_SettleWait_DuringOrdinaryKinematic_DoesNotSnap();
            EnemyCharge_AlreadyActiveChargeKinematic_Continues();
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_WindupStartTickPresentationSignal_MarksWindupStarted()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(
                moveCooldownTicks: 0,
                includePassiveContact: false,
                windupTicks: 1,
                recoverTicks: 1,
                chargeStepCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);

                var windupTick = pipeline.RunTick(new TickInput(1));
                var chargeSignal = windupTick.PresentationData.EnemyChargeSignals.Single(signal => signal.EntityId == 40);

                Assert.That(chargeSignal.Phase, Is.EqualTo(EnemyChargePhase.Windup), BuildChargeKinematicDebug(1, worldState, windupTick));
                Assert.That(chargeSignal.StartedWindupThisTick, Is.True, BuildChargeKinematicDebug(1, worldState, windupTick));
                Assert.That(chargeSignal.StartedActiveThisTick, Is.False, BuildChargeKinematicDebug(1, worldState, windupTick));
                Assert.That(chargeSignal.StartedRecoverThisTick, Is.False, BuildChargeKinematicDebug(1, worldState, windupTick));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }


        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardCellProjectile_TelegraphsBeforeReleaseAndThenEntersRecover()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = CreateEnemyPipeline(worldState, CreateEnemyProfile(windupTicks: 1));

            var windupTick = pipeline.RunTick(new TickInput(1));
            var enemyAfterWindupTick = GetEntity(worldState, 40);
            var actionStateAfterWindupTick = GetEnemyActionState(worldState, 40);
            var windupSignal = windupTick.PresentationData.EnemyActionSignals.Single();

            Assert.That(windupTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(windupTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(enemyAfterWindupTick.aiMode, Is.EqualTo(EnemyAiMode.Attack));
            Assert.That(actionStateAfterWindupTick.IsActive, Is.True);
            Assert.That(actionStateAfterWindupTick.executeTick, Is.EqualTo(2));
            Assert.That(actionStateAfterWindupTick.executionAttempted, Is.False);
            Assert.That(windupSignal.EntityId, Is.EqualTo(40));
            Assert.That(windupSignal.ActiveActionKind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(windupSignal.StartedThisTick, Is.True);
            Assert.That(windupSignal.CanceledThisTick, Is.False);
            Assert.That(windupSignal.ExecutedThisTick, Is.False);
            Assert.That(windupSignal.StartedRecoveryThisTick, Is.False);

            var executeTick = pipeline.RunTick(new TickInput(2));
            var enemyAfterExecuteTick = GetEntity(worldState, 40);
            var playerAfterExecuteTick = GetEntity(worldState, 10);
            var actionStateAfterExecuteTick = GetEnemyActionState(worldState, 40);
            var executeSignal = executeTick.PresentationData.EnemyActionSignals.Single();

            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(40), Is.EqualTo(1));
            Assert.That(playerAfterExecuteTick.hp, Is.EqualTo(3));
            Assert.That(enemyAfterExecuteTick.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemyAfterExecuteTick.aiStateTimer, Is.EqualTo(1));
            Assert.That(actionStateAfterExecuteTick.IsActive, Is.True);
            Assert.That(actionStateAfterExecuteTick.executionAttempted, Is.True);
            Assert.That(executeSignal.EntityId, Is.EqualTo(40));
            Assert.That(executeSignal.ActiveActionKind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(executeSignal.StartedThisTick, Is.False);
            Assert.That(executeSignal.CanceledThisTick, Is.False);
            Assert.That(executeSignal.ExecutedThisTick, Is.True);
            Assert.That(executeSignal.StartedRecoveryThisTick, Is.True);

            var recoverTick = pipeline.RunTick(new TickInput(3));
            var enemyAfterRecoverTick = GetEntity(worldState, 40);

            Assert.That(recoverTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(recoverTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(enemyAfterRecoverTick.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemyAfterRecoverTick.aiStateTimer, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardCellProjectile_SimulationDistanceOutsideSlack_BlocksWindup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            SetUnitContinuousLocomotionState(worldState, 40, localX: -KinematicFixed.HalfCellUnits, localY: 0);
            var profile = CreateEnemyProfile(windupTicks: 1);

            try
            {
                var result = CreateEnemyPipeline(worldState, profile, GameplayRuntimeFeatureFlags.None).RunTick(new TickInput(1));

                var snapshot = worldState.CreateSnapshot();
                if (snapshot.TryGetEnemyActionState(40, out var actionState))
                {
                    Assert.That(actionState.IsActive, Is.False);
                }

                Assert.That(result.PresentationData.EnemyActionSignals, Is.Empty);
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardCellProjectile_SevereTransition_BlocksWindup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            worldState.CreateWriteContext().SetUnitKinematicState(
                40,
                new UnitKinematicRuntimeState
                {
                    localOffset = new KinematicOffset2(KinematicFixed.FromRaw(256), KinematicFixed.Zero),
                    velocity = new KinematicVelocity2(KinematicFixed.FromRaw(128), KinematicFixed.Zero),
                    mode = MotionMode.Forced,
                    forcedOp = ForcedMotionOp.Knockback,
                    sequenceId = 1,
                });
            var profile = CreateEnemyProfile(windupTicks: 1);

            try
            {
                CreateEnemyPipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(40, out var actionState), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardCellProjectile_PlayerAtDeadZoneRange_ApproachesInsteadOfIdling()
        {
            var profile = CreateEnemyProfile(windupTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)));
            SetUnitContinuousLocomotionState(worldState, 40, localX: -KinematicFixed.HalfCellUnits, localY: 0);

            try
            {
                var beforeSnapshot = worldState.CreateSnapshot();
                Assert.That(beforeSnapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(beforeSnapshot.TryGetEntity(10, out var player), Is.True);
                var logicRange = EnemyAttackRangeQueries.IsTargetInRange(
                    enemy,
                    player,
                    AttackDecisionSettings.CreateAdjacentRange());
                var query = CombatWindupPoseQueries.QueryStartWindupForwardCellProjectile(
                    beforeSnapshot,
                    enemy,
                    player,
                    WindupForwardCellProjectileAttackDecisionStrategy.Instance,
                    AttackDecisionSettings.CreateAdjacentRange(),
                    WindupForwardCellProjectileSettings.CreateDefault(),
                    out _);

                var result = CreateEnemyPipeline(worldState, profile).RunTick(new TickInput(1));
                var enemyAfterTick = GetEntity(worldState, 40);

                Assert.That(logicRange, Is.True, BuildWindupStartGateDebug(logicRange, query, result));
                Assert.That(query.CanStart, Is.False, BuildWindupStartGateDebug(logicRange, query, result));
                Assert.That(query.ShouldApproach, Is.True, BuildWindupStartGateDebug(logicRange, query, result));
                Assert.That(query.BlockReason, Is.EqualTo(CombatWindupStartBlockReason.OutsideSimulationStartRange));
                Assert.That(query.DistanceFixedUnits, Is.GreaterThan(query.ThresholdFixedUnits));
                Assert.That(
                    result.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40),
                    Is.Empty,
                    BuildWindupStartGateDebug(logicRange, query, result));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(40, out var actionState) && actionState.IsActive, Is.False);
                Assert.That(enemyAfterTick.aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(
                    result.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == 40),
                    Is.True,
                    BuildWindupStartGateDebug(logicRange, query, result));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardCellProjectile_OutsideSimulationRange_DoesNotConsumeCombatActionAsHandled()
        {
            var profile = CreateEnemyProfile(windupTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)));
            SetUnitContinuousLocomotionState(worldState, 40, localX: -KinematicFixed.HalfCellUnits, localY: 0);

            try
            {
                var result = CreateEnemyPipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(result.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40), Is.Empty);
                Assert.That(result.AttackPhaseResult.RawIntents.Where(intent => intent.SourceId == 40), Is.Empty);
                Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(result.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(40, out var actionState) && actionState.IsActive, Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardCellProjectile_SevereTransition_DoesNotApproachAsDistanceFallback()
        {
            var profile = CreateEnemyProfile(windupTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            worldState.CreateWriteContext().SetUnitKinematicState(
                40,
                new UnitKinematicRuntimeState
                {
                    localOffset = new KinematicOffset2(KinematicFixed.FromRaw(256), KinematicFixed.Zero),
                    velocity = new KinematicVelocity2(KinematicFixed.FromRaw(128), KinematicFixed.Zero),
                    mode = MotionMode.Forced,
                    forcedOp = ForcedMotionOp.Knockback,
                    sequenceId = 1,
                });

            try
            {
                var beforeSnapshot = worldState.CreateSnapshot();
                Assert.That(beforeSnapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(beforeSnapshot.TryGetEntity(10, out var player), Is.True);
                var query = CombatWindupPoseQueries.QueryStartWindupForwardCellProjectile(
                    beforeSnapshot,
                    enemy,
                    player,
                    WindupForwardCellProjectileAttackDecisionStrategy.Instance,
                    AttackDecisionSettings.CreateAdjacentRange(),
                    WindupForwardCellProjectileSettings.CreateDefault(),
                    out _);

                var result = CreateEnemyPipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(query.CanStart, Is.False, BuildWindupStartGateDebug(true, query, result));
                Assert.That(query.ShouldApproach, Is.False);
                Assert.That(query.BlockReason, Is.EqualTo(CombatWindupStartBlockReason.SevereTransition));
                Assert.That(result.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == 40), Is.Empty);
                Assert.That(result.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40), Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EntitySpawnMaterializer_RequestPayloadSnapshot_DoesNotDriftWhenSourceStateChangesBeforeMaterialization()
        {
            var request = new EntitySpawnRequest(
                EntitySpawnRequestKind.Summon,
                new EntitySpawnRequestSource(
                    sourceEntityId: 40,
                    sourceEffectIndex: 0,
                    triggerTick: 2,
                    originCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    sourceFacing: Direction.Right,
                    sourceTeamId: 2),
                spawnIndex: 0,
                tickIndex: 2,
                summon: new EnemySummonCompiledConfig(
                    spawnCountPerTrigger: 1,
                    candidatePattern: SummonCandidatePattern.OrthogonalAdjacent4,
                    requireNoUnitAtSpawnCell: true,
                    requireNoSolidAtSpawnCell: true,
                    maxAliveChildren: 3,
                    summonedArchetypeId: new EnemyUnitArchetypeId("BasicMinion"),
                    windupTicks: 1),
                spawnDefaults: new EnemyUnitSpawnDefaultsRuntime(
                    hp: 1,
                    initialAiMode: EnemyAiMode.Patrol,
                    unitMobilityKind: UnitMobilityKind.Ground));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(
                    entityId: 40,
                    teamId: 7,
                    position: new SurfaceCell(FaceId.Floor, 5, 5),
                    hp: 3,
                    aiMode: EnemyAiMode.Patrol,
                    facing: Direction.Left),
            });
            var snapshot = worldState.CreateSnapshot();
            var batch = new FinalizationBatch();
            var eventLogEntries = new List<string>();

            var result = EntitySpawnMaterializer.Materialize(
                snapshot,
                request,
                EntityIdAllocator.Create(snapshot),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                new HashSet<SurfaceCell>(),
                batch,
                eventLogEntries);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SpawnCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(result.SpawnedEntity.entityId, Is.EqualTo(41));
            Assert.That(result.SpawnedEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(result.SpawnedEntity.teamId, Is.EqualTo(2));
            Assert.That(result.SpawnedEntity.facing, Is.EqualTo(Direction.Right));

            Assert.That(batch.Operations, Has.Count.EqualTo(1));
            var spawnOperation = batch.Operations[0];
            Assert.That(spawnOperation.Kind, Is.EqualTo(FinalizationOperationKind.SpawnEntity));
            Assert.That(spawnOperation.SpawnedEntity.entityId, Is.EqualTo(41));
            Assert.That(spawnOperation.SpawnedEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(spawnOperation.Metadata.SourceActorEntityId, Is.EqualTo(40));
            Assert.That(spawnOperation.HasSpawnedEntitySummonedState, Is.True);
            Assert.That(spawnOperation.SpawnedEntitySummonedState.SourceEntityId, Is.EqualTo(40));
            Assert.That(spawnOperation.SpawnedEntitySummonedState.SourceEffectIndex, Is.EqualTo(0));
            Assert.That(spawnOperation.HasSpawnedEntityEnemyDefinitionBindingState, Is.True);
            Assert.That(
                spawnOperation.SpawnedEntityEnemyDefinitionBindingState.ArchetypeId,
                Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));
            CollectionAssert.AreEqual(
                new[]
                {
                    "SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=41|Pos=(1,0)|Archetype=BasicMinion|Tick=2",
                },
                eventLogEntries);
        }

        private static void AssertSummonedChildMetadata(WorldSnapshot snapshot, int entityId, int sourceEntityId = 40)
        {
            Assert.That(snapshot.TryGetSummonedEntityState(entityId, out var summonedState), Is.True);
            Assert.That(summonedState.SourceEntityId, Is.EqualTo(sourceEntityId));
            Assert.That(summonedState.SourceEffectIndex, Is.EqualTo(0));
            Assert.That(snapshot.TryGetEnemyDefinitionBindingState(entityId, out var bindingState), Is.True);
            Assert.That(bindingState.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));
        }

        private static TickResult RunSingleBehaviorSummonCommit(
            out EnemyAiProfile profile,
            out EnemyAiProfile defaultProfile,
            out EnemyUnitArchetypeCatalog archetypeCatalog)
        {
            profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
            pipeline.RunTick(new TickInput(1));
            return pipeline.RunTick(new TickInput(2));
        }

        private static (int OwnerEntityId, EnemyAudioCue Cue)[] GetEnemyAudioRequests(TickResult result)
        {
            return new EnemyAudioRequestPlanner()
                .BuildRequests(result)
                .Where(request => request.Cue == EnemyAudioCue.Windup || request.Cue == EnemyAudioCue.Active)
                .Select(request => (request.OwnerEntityId, request.Cue))
                .ToArray();
        }

        private static IReadOnlyList<GameplayVfxRequest> PlanEnemyVfxRequests(TickResult result)
        {
            var builder = new GameplayVfxRequestPlanBuilder();
            new EnemyVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(
                    result.TickIndex,
                    result.PresentationData,
                    result.FinalTopology),
                builder);
            return builder.Build().Requests;
        }

        private static bool IsUtilityWindupRequest(GameplayVfxRequest request)
        {
            return request.CueId == GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup);
        }

        private static bool IsUtilitySummonSpawnRequest(GameplayVfxRequest request)
        {
            return request.CueId == GameplayVfxCueId.From(EnemyVfxCue.UtilitySummonSpawn);
        }

        private static void AssertSummonedSpawnPresentation(
            TickResult result,
            int spawnedEntityId,
            int sourceEntityId)
        {
            Assert.That(
                result.PresentationData.SummonedEnemyPresentationBindings.Any(binding =>
                    binding.EntityId == spawnedEntityId &&
                    binding.SourceEntityId == sourceEntityId &&
                    binding.HasEnemyDefinitionBinding &&
                    binding.ArchetypeId.Equals(new EnemyUnitArchetypeId("BasicMinion"))),
                Is.True);
            Assert.That(
                result.PresentationData.VisibilityChanges.Any(change =>
                    change.EntityId == spawnedEntityId &&
                    change.ChangeKind == TickVisibilityChangeKind.Spawn),
                Is.True);
            Assert.That(
                GetEnemyAudioRequests(result),
                Does.Contain((sourceEntityId, EnemyAudioCue.Active)));
            Assert.That(
                PlanEnemyVfxRequests(result).Any(request =>
                    IsUtilitySummonSpawnRequest(request) &&
                    request.SourceEntityId == spawnedEntityId),
                Is.True);
        }

        private static void AssertNoSpawnPresentationAudioOrVfx(TickResult result)
        {
            var spawnedEntityIds = result.PresentationData.VisibilityChanges
                .Where(change => change.ChangeKind == TickVisibilityChangeKind.Spawn)
                .Select(change => change.EntityId)
                .ToArray();
            Assert.That(spawnedEntityIds, Is.Empty);
            Assert.That(
                result.PresentationData.SummonedEnemyPresentationBindings.Any(binding =>
                    spawnedEntityIds.Contains(binding.EntityId)),
                Is.False);
            Assert.That(
                GetEnemyAudioRequests(result).Any(request => request.Cue == EnemyAudioCue.Active),
                Is.False);
            Assert.That(
                PlanEnemyVfxRequests(result).Any(IsUtilitySummonSpawnRequest),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_InitialDelayCooldownWindupRecoveryParity()
        {
            var profile = CreateBehaviorSummonProfile(
                initialDelayTicks: 2,
                cooldownTicks: 3,
                windupTicks: 1,
                recoveryTicks: 2,
                suppressMovementDuringRecover: true);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);

                var firstTick = pipeline.RunTick(new TickInput(1));
                var firstState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(firstState.cooldownTicksRemaining, Is.EqualTo(1));
                Assert.That(firstState.phase, Is.EqualTo(EnemySummonBehaviorPhase.None));
                Assert.That(firstTick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));

                var secondTick = pipeline.RunTick(new TickInput(2));
                var secondState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(secondState.cooldownTicksRemaining, Is.Zero);
                Assert.That(secondState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Windup));
                Assert.That(secondState.windupStartTick, Is.EqualTo(2));
                Assert.That(secondState.windupEndTick, Is.EqualTo(3));
                Assert.That(secondTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));

                var committedTick = pipeline.RunTick(new TickInput(3));
                var committedState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(committedState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Recover));
                Assert.That(committedState.cooldownTicksRemaining, Is.EqualTo(3));
                Assert.That(committedState.recoverStartTick, Is.EqualTo(3));
                Assert.That(committedState.recoverEndTickExclusive, Is.EqualTo(5));
                Assert.That(committedTick.EventLog, Has.Some.Contains("SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=41"));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out var child), Is.True);
                Assert.That(child.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                AssertSummonedChildMetadata(worldState.CreateSnapshot(), 41);

                pipeline.RunTick(new TickInput(4));
                var recoverHoldState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(recoverHoldState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Recover));
                Assert.That(recoverHoldState.cooldownTicksRemaining, Is.EqualTo(2));

                pipeline.RunTick(new TickInput(5));
                var recoveredState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(recoveredState.phase, Is.EqualTo(EnemySummonBehaviorPhase.None));
                Assert.That(recoveredState.cooldownTicksRemaining, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_MovementSuppressionParity()
        {
            var profile = CreateBehaviorSummonProfile(
                initialDelayTicks: 0,
                cooldownTicks: 3,
                windupTicks: 2,
                suppressMovementDuringWindup: true,
                recoveryTicks: 2,
                suppressMovementDuringRecover: true,
                patrolStrategyKind: PatrolStrategyKind.RandomWalk,
                patrolSettings: PatrolSettings.CreateDefaultRandomWalk());
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateSuppressedRandomWalkFacingWorld(includeBox: false);

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);

                var windupStartTick = pipeline.RunTick(new TickInput(1));
                AssertSuppressedFacingTick(worldState, windupStartTick, Direction.Right);

                var windupHoldTick = pipeline.RunTick(new TickInput(2));
                AssertSuppressedFacingTick(worldState, windupHoldTick, Direction.Right);

                var recoverStartTick = pipeline.RunTick(new TickInput(3));
                AssertSuppressedFacingTick(worldState, recoverStartTick, Direction.Right);

                var recoverHoldTick = pipeline.RunTick(new TickInput(4));
                AssertSuppressedFacingTick(worldState, recoverHoldTick, Direction.Right);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder()
        {
            var profile = CreateBehaviorSummonProfile(
                initialDelayTicks: 0,
                cooldownTicks: 5,
                spawnCountPerTrigger: 2,
                windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(3, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(
                    profile,
                    worldState,
                    out defaultProfile,
                    out archetypeCatalog,
                    summonerEntityIds: new[] { 40, 41 });
                pipeline.RunTick(new TickInput(1));
                var committedTick = pipeline.RunTick(new TickInput(2));
                var eventLogDump = string.Join("\n", committedTick.EventLog);

                var source40Index = eventLogDump.IndexOf(
                    "SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=42|Pos=(1,0)|Archetype=BasicMinion|Tick=2",
                    StringComparison.Ordinal);
                var source41Index = eventLogDump.IndexOf(
                    "SummonCommitted|Source=41|Effect=0|SpawnIndex=0|Spawned=44|Pos=(4,0)|Archetype=BasicMinion|Tick=2",
                    StringComparison.Ordinal);
                Assert.That(source40Index, Is.GreaterThanOrEqualTo(0), eventLogDump);
                Assert.That(source41Index, Is.GreaterThanOrEqualTo(0), eventLogDump);
                Assert.That(source40Index, Is.LessThan(source41Index), eventLogDump);

                AssertSummonedChildMetadata(worldState.CreateSnapshot(), 42, sourceEntityId: 40);
                AssertSummonedChildMetadata(worldState.CreateSnapshot(), 43, sourceEntityId: 40);
                AssertSummonedChildMetadata(worldState.CreateSnapshot(), 44, sourceEntityId: 41);
                AssertSummonedChildMetadata(worldState.CreateSnapshot(), 45, sourceEntityId: 41);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SourceDeathCancelsOrSkipsAsUtility()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 2, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonBootstrapper(profile, out defaultProfile, out archetypeCatalog)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { new ScriptedAttackLogic(10, 40) });
                var warningTick = pipeline.RunTick(new TickInput(1));
                Assert.That(warningTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));

                var tick = pipeline.RunTick(new TickInput(2));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out _), Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);
                Assert.That(tick.EventLog, Has.Some.Contains("SummonSkipped|Source=40|Effect=0|SpawnIndex=0|Reason=SourceInvalid"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SourceLeavesTopologyCancelsOrSuspendsAsUtility()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 2, windupTicks: 2);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);

                pipeline.RunTick(new TickInput(1));
                var firstState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(firstState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Windup));
                Assert.That(firstState.windupEndTick, Is.EqualTo(3));

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Front, 0, 0));
                pipeline.RunTick(new TickInput(2));
                var suspendedState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(suspendedState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Windup));
                Assert.That(suspendedState.windupEndTick, Is.EqualTo(4));

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Floor, 0, 0));
                pipeline.RunTick(new TickInput(3));
                var resumedState = GetEnemySummonBehaviorState(worldState, 40);
                Assert.That(resumedState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Windup));
                Assert.That(resumedState.windupEndTick, Is.EqualTo(4));

                var committedTick = pipeline.RunTick(new TickInput(4));
                Assert.That(committedTick.EventLog, Has.Some.Contains("SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=41"));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_MaxAliveParity()
        {
            var defaultProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
            });
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, maxAliveChildren: 1, windupTicks: 1);
            var archetypeCatalog = CreateEnemyUnitArchetypeCatalog(GetSharedSummonedArchetype());
            var blockedWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(1, 0), hp: 1, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            blockedWorld.SetSummonedEntityState(50, new SummonedEntityState(40, 0));

            var detachedChildWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(1, 0), hp: 1, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            detachedChildWorld.SetSummonedEntityState(50, new SummonedEntityState(40, 0));
            detachedChildWorld.CreateWriteContext().SetBoardPresence(50, EntityBoardPresence.Detached);

            try
            {
                var blockedPipeline = CreateTickPipeline(defaultProfile, profile, archetypeCatalog, blockedWorld);
                var blockedTick = blockedPipeline.RunTick(new TickInput(1));
                var blockedState = GetEnemySummonBehaviorState(blockedWorld, 40);
                Assert.That(blockedTick.PresentationData.SummonWindupWarnings, Is.Empty);
                Assert.That(blockedState.phase, Is.EqualTo(EnemySummonBehaviorPhase.None));
                Assert.That(blockedState.cooldownTicksRemaining, Is.EqualTo(0));

                var detachedChildPipeline = CreateTickPipeline(defaultProfile, profile, archetypeCatalog, detachedChildWorld);
                var windupTick = detachedChildPipeline.RunTick(new TickInput(1));
                detachedChildPipeline.RunTick(new TickInput(2));
                Assert.That(windupTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(detachedChildWorld.CreateSnapshot().TryGetEntity(51, out var spawnedChild), Is.True);
                Assert.That(spawnedChild.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_ReplayNames_PreserveSummonCommittedAndSkipped()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 2, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonBootstrapper(profile, out defaultProfile, out archetypeCatalog)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { new ScriptedAttackLogic(10, 40) });
                pipeline.RunTick(new TickInput(1));
                var skippedTick = pipeline.RunTick(new TickInput(2));
                var skippedDump = string.Join("\n", skippedTick.EventLog);

                Assert.That(skippedDump, Does.Contain("SummonSkipped|Source=40|Effect=0|SpawnIndex=0|Reason=SourceInvalid|Tick=2"));
                Assert.That(skippedDump, Does.Not.Contain("BehaviorSummonSkipped"));
                Assert.That(skippedDump, Does.Not.Contain("SourceEffectIndex="));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SourceMetadataExportParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                pipeline.RunTick(new TickInput(1));
                var committedTick = pipeline.RunTick(new TickInput(2));
                var eventLogDump = string.Join("\n", committedTick.EventLog);

                Assert.That(eventLogDump, Does.Contain("SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=41|Pos=(1,0)|Archetype=BasicMinion|Tick=2"));
                AssertSummonedChildMetadata(worldState.CreateSnapshot(), 41);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SummonedEntitiesHashParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                var windupTick = pipeline.RunTick(new TickInput(1));
                var committedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(committedTick.DeterminismHash, Is.Not.EqualTo(windupTick.DeterminismHash));
                Assert.That(committedTick.Trace.Text, Does.Contain("Final.SummonedEntities"));
                Assert.That(committedTick.Trace.Text, Does.Contain("E=41|Source=40|Effect=0"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_EnemyDefinitionBindingsHashParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                var windupTick = pipeline.RunTick(new TickInput(1));
                var committedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(committedTick.DeterminismHash, Is.Not.EqualTo(windupTick.DeterminismHash));
                Assert.That(committedTick.Trace.Text, Does.Contain("Final.EnemyDefinitionBindings"));
                Assert.That(committedTick.Trace.Text, Does.Contain("E=41|Archetype=BasicMinion"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_StateHashIncluded()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 2);
            var passiveProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
            });
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var behaviorWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var passiveWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var behaviorPipeline = CreateSharedSummonTickPipeline(profile, behaviorWorld, out defaultProfile, out archetypeCatalog);
                var behaviorTick = behaviorPipeline.RunTick(new TickInput(1));
                var passiveTick = CreateTickPipeline(passiveProfile, passiveProfile, archetypeCatalog, passiveWorld)
                    .RunTick(new TickInput(1));

                Assert.That(behaviorTick.DeterminismHash, Is.Not.EqualTo(passiveTick.DeterminismHash));
                Assert.That(behaviorTick.Trace.Text, Does.Contain("Final.EnemySummonBehaviors"));
                Assert.That(behaviorTick.Trace.Text, Does.Contain("E=40|Cooldown=0|Phase=Windup|WindupStart=1|WindupEnd=3"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(passiveProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_WindupWarningSignalParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 2);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                var windupTick = pipeline.RunTick(new TickInput(1));

                Assert.That(windupTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                var warning = windupTick.PresentationData.SummonWindupWarnings[0];
                Assert.That(warning.SourceEntityId, Is.EqualTo(40));
                Assert.That(warning.EffectIndex, Is.EqualTo(0));
                Assert.That(warning.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(warning.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
                Assert.That(warning.Facing, Is.EqualTo(Direction.Right));
                Assert.That(warning.WindupStartTick, Is.EqualTo(1));
                Assert.That(warning.WindupEndTick, Is.EqualTo(3));
                Assert.That(warning.ActivationSequence, Is.EqualTo(1));
                Assert.That(warning.TickIndex, Is.EqualTo(1));
                Assert.That(warning.PresentationSeed, Is.Not.Zero);
                Assert.That(
                    windupTick.PresentationData.EnemyUtilitySignals.Select(signal =>
                        (signal.EntityId, signal.Kind, signal.Phase, signal.EffectIndex, signal.ActivationSequence)).ToArray(),
                    Is.EqualTo(new[]
                    {
                        (40, EnemyUtilityPresentationKind.SummonMinion, EnemyUtilityPresentationPhase.WindupStarted, 0, 1),
                    }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SourceDeathCancelsWindupPresentation()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 2, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonBootstrapper(profile, out defaultProfile, out archetypeCatalog)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { new ScriptedAttackLogic(10, 40) });
                var warningTick = pipeline.RunTick(new TickInput(1));
                Assert.That(warningTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(PlanEnemyVfxRequests(warningTick).Any(IsUtilityWindupRequest), Is.True);

                var canceledTick = pipeline.RunTick(new TickInput(2));

                Assert.That(canceledTick.PresentationData.SummonWindupWarnings, Is.Empty);
                AssertNoSpawnPresentationAudioOrVfx(canceledTick);
                Assert.That(
                    canceledTick.EventLog,
                    Has.Some.Contains("SummonSkipped|Source=40|Effect=0|SpawnIndex=0|Reason=SourceInvalid"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_TopologySuspendPresentationParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 2, windupTicks: 2);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);

                var firstWarningTick = pipeline.RunTick(new TickInput(1));
                Assert.That(firstWarningTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Front, 0, 0));
                var suspendedTick = pipeline.RunTick(new TickInput(2));
                var suspendedState = GetEnemySummonBehaviorState(worldState, 40);

                Assert.That(suspendedState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Windup));
                Assert.That(suspendedState.windupEndTick, Is.EqualTo(4));
                Assert.That(suspendedTick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
                Assert.That(suspendedTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(
                    suspendedTick.PresentationData.SummonWindupWarnings[0].SourceCell,
                    Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Floor, 0, 0));
                pipeline.RunTick(new TickInput(3));
                var committedTick = pipeline.RunTick(new TickInput(4));

                Assert.That(committedTick.PresentationData.SummonWindupWarnings, Is.Empty);
                AssertSummonedSpawnPresentation(committedTick, spawnedEntityId: 41, sourceEntityId: 40);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SummonedEnemyPresentationBindingParity()
        {
            var committedTick = RunSingleBehaviorSummonCommit(out var profile, out var defaultProfile, out var archetypeCatalog);
            try
            {
                Assert.That(committedTick.PresentationData.SummonedEnemyPresentationBindings, Has.Count.EqualTo(1));
                var binding = committedTick.PresentationData.SummonedEnemyPresentationBindings[0];
                Assert.That(binding.EntityId, Is.EqualTo(41));
                Assert.That(binding.HasEnemyDefinitionBinding, Is.True);
                Assert.That(binding.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));
                Assert.That(binding.SourceEntityId, Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SpawnVisibilityChangeParity()
        {
            var committedTick = RunSingleBehaviorSummonCommit(out var profile, out var defaultProfile, out var archetypeCatalog);
            try
            {
                var spawnChanges = committedTick.PresentationData.VisibilityChanges
                    .Where(change => change.ChangeKind == TickVisibilityChangeKind.Spawn)
                    .ToArray();

                Assert.That(spawnChanges, Has.Length.EqualTo(1));
                Assert.That(spawnChanges[0].EntityId, Is.EqualTo(41));
                Assert.That(spawnChanges[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(spawnChanges[0].Facing, Is.EqualTo(Direction.Right));
                Assert.That(spawnChanges[0].Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
                AssertSummonedSpawnPresentation(committedTick, spawnedEntityId: 41, sourceEntityId: 40);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SameTickMultiSummonerPresentationOrderParity()
        {
            var profile = CreateBehaviorSummonProfile(
                initialDelayTicks: 0,
                cooldownTicks: 5,
                spawnCountPerTrigger: 1,
                windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new SurfaceCell(FaceId.Floor, 3, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(
                    profile,
                    worldState,
                    out defaultProfile,
                    out archetypeCatalog,
                    summonerEntityIds: new[] { 40, 41 });
                pipeline.RunTick(new TickInput(1));
                var committedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(
                    committedTick.PresentationData.SummonedEnemyPresentationBindings
                        .Select(binding => (binding.EntityId, binding.SourceEntityId)).ToArray(),
                    Is.EqualTo(new[] { (42, 40), (43, 41) }));
                Assert.That(
                    committedTick.PresentationData.VisibilityChanges
                        .Where(change => change.ChangeKind == TickVisibilityChangeKind.Spawn)
                        .Select(change => change.EntityId).ToArray(),
                    Is.EqualTo(new[] { 42, 43 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_AudioWindupCueParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 2);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                var windupStartTick = pipeline.RunTick(new TickInput(1));
                var windupHoldTick = pipeline.RunTick(new TickInput(2));

                Assert.That(GetEnemyAudioRequests(windupStartTick), Is.EqualTo(new[] { (40, EnemyAudioCue.Windup) }));
                Assert.That(GetEnemyAudioRequests(windupHoldTick), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_AudioActiveSummonCueParity()
        {
            var committedTick = RunSingleBehaviorSummonCommit(out var profile, out var defaultProfile, out var archetypeCatalog);
            try
            {
                Assert.That(GetEnemyAudioRequests(committedTick), Is.EqualTo(new[] { (40, EnemyAudioCue.Active) }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_OwnerViewMissing_AudioFallbackParity()
        {
            var committedTick = RunSingleBehaviorSummonCommit(out var profile, out var defaultProfile, out var archetypeCatalog);
            try
            {
                var audioRequests = GetEnemyAudioRequests(committedTick);

                Assert.That(audioRequests, Has.Length.EqualTo(1));
                Assert.That(audioRequests[0], Is.EqualTo((40, EnemyAudioCue.Active)));
                Assert.That(
                    committedTick.PresentationData.SummonedEnemyPresentationBindings.Single().EntityId,
                    Is.EqualTo(41),
                    "Active summon audio remains source-owned; missing source views are handled by the existing host no-op policy.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SameTickMultiSummonerAudioOrderParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new SurfaceCell(FaceId.Floor, 3, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(
                    profile,
                    worldState,
                    out defaultProfile,
                    out archetypeCatalog,
                    summonerEntityIds: new[] { 40, 41 });
                pipeline.RunTick(new TickInput(1));
                var committedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(
                    GetEnemyAudioRequests(committedTick),
                    Is.EqualTo(new[] { (40, EnemyAudioCue.Active), (41, EnemyAudioCue.Active) }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_UtilityWindupVfxParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 2);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                var windupTick = pipeline.RunTick(new TickInput(1));

                var request = PlanEnemyVfxRequests(windupTick).Single(IsUtilityWindupRequest);
                var cueId = GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup);
                Assert.That(request.SourceEntityId, Is.EqualTo(40));
                Assert.That(request.CueId, Is.EqualTo(cueId));
                Assert.That(request.IsPersistent, Is.True);
                Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Entity));
                Assert.That(request.Anchor.EntityId, Is.EqualTo(40));
                Assert.That(request.Anchor.HasFallbackCell, Is.True);
                Assert.That(request.Anchor.FallbackCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(request.PersistentKey, Is.EqualTo(new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Entity,
                    entityId: 40,
                    effectIndex: 0,
                    activationSequence: 1)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_UtilitySummonSpawnVfxParity()
        {
            var committedTick = RunSingleBehaviorSummonCommit(out var profile, out var defaultProfile, out var archetypeCatalog);
            try
            {
                var request = PlanEnemyVfxRequests(committedTick).Single(IsUtilitySummonSpawnRequest);

                Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.UtilitySummonSpawn)));
                Assert.That(request.SourceEntityId, Is.EqualTo(41));
                Assert.That(request.IsPersistent, Is.False);
                Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
                Assert.That(request.Anchor.Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(request.Anchor.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
                Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_SameTickMultiSummonerVfxOrderParity()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new SurfaceCell(FaceId.Floor, 3, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(
                    profile,
                    worldState,
                    out defaultProfile,
                    out archetypeCatalog,
                    summonerEntityIds: new[] { 40, 41 });
                pipeline.RunTick(new TickInput(1));
                var committedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(
                    PlanEnemyVfxRequests(committedTick)
                        .Where(IsUtilitySummonSpawnRequest)
                        .Select(request => request.SourceEntityId).ToArray(),
                    Is.EqualTo(new[] { 42, 43 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_FailedPlacement_NoSpawnVfxOrActiveCue()
        {
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateWall(entityId: 60, position: new Vector2Int(1, 0)),
                CreateWall(entityId: 61, position: new Vector2Int(0, -1)),
                CreateWall(entityId: 62, position: new Vector2Int(-1, 0)),
                CreateWall(entityId: 63, position: new Vector2Int(0, 1)),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                pipeline.RunTick(new TickInput(1));
                var failedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(
                    failedTick.EventLog,
                    Has.Some.Contains("SummonSkipped|Source=40|Effect=0|SpawnIndex=0|Reason=NoCandidateCell"));
                AssertNoSpawnPresentationAudioOrVfx(failedTick);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_MaxAliveBlocked_NoSpawnVfxOrActiveCue()
        {
            var defaultProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
            });
            var profile = CreateBehaviorSummonProfile(initialDelayTicks: 0, cooldownTicks: 5, maxAliveChildren: 1, windupTicks: 1);
            var archetypeCatalog = CreateEnemyUnitArchetypeCatalog(GetSharedSummonedArchetype());
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(1, 0), hp: 1, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            worldState.SetSummonedEntityState(50, new SummonedEntityState(40, 0));

            try
            {
                var pipeline = CreateTickPipeline(defaultProfile, profile, archetypeCatalog, worldState);
                var blockedTick = pipeline.RunTick(new TickInput(1));

                Assert.That(blockedTick.PresentationData.SummonWindupWarnings, Is.Empty);
                AssertNoSpawnPresentationAudioOrVfx(blockedTick);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_PresentationDoesNotMutateAuthoritativeState()
        {
            var committedTick = RunSingleBehaviorSummonCommit(out var profile, out var defaultProfile, out var archetypeCatalog);
            try
            {
                var hashBefore = committedTick.DeterminismHash;
                var eventLogBefore = committedTick.EventLog.ToArray();
                var finalEntitiesBefore = committedTick.FinalEntities.ToArray();
                var bindingCountBefore = committedTick.PresentationData.SummonedEnemyPresentationBindings.Count;
                var visibilityCountBefore = committedTick.PresentationData.VisibilityChanges.Count;

                _ = new EnemyAudioRequestPlanner().BuildRequests(committedTick);
                _ = PlanEnemyVfxRequests(committedTick);

                Assert.That(committedTick.DeterminismHash, Is.EqualTo(hashBefore));
                CollectionAssert.AreEqual(eventLogBefore, committedTick.EventLog);
                CollectionAssert.AreEqual(finalEntitiesBefore, committedTick.FinalEntities);
                Assert.That(committedTick.PresentationData.SummonedEnemyPresentationBindings, Has.Count.EqualTo(bindingCountBefore));
                Assert.That(committedTick.PresentationData.VisibilityChanges, Has.Count.EqualTo(visibilityCountBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                DestroyProfile(defaultProfile);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityWindup_TopologyParticipationRestored_ResumesFromSuspendedPhase()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 2,
                radius: 1,
                windupTicks: 2,
                durationTicks: 2,
                recoverTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);

                var firstWarning = pipeline.RunTick(new TickInput(1));
                var firstState = GetEnemyUtilityState(worldState, 40).EffectStates[0];
                Assert.That(firstWarning.PresentationData.EnemyUtilitySignals.Single().Kind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
                Assert.That(firstWarning.PresentationData.EnemyUtilitySignals.Single().Phase, Is.EqualTo(EnemyUtilityPresentationPhase.WindupStarted));
                Assert.That(firstState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
                Assert.That(firstState.windupStartTick, Is.EqualTo(1));
                Assert.That(firstState.windupEndTick, Is.EqualTo(3));

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Front, 0, 0));
                var firstSuspendedTick = pipeline.RunTick(new TickInput(2));
                var firstSuspendedState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(firstSuspendedState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
                Assert.That(firstSuspendedState.windupStartTick, Is.EqualTo(1));
                Assert.That(firstSuspendedState.windupEndTick, Is.EqualTo(4));
                Assert.That(firstSuspendedState.cooldownTicksRemaining, Is.Zero);
                Assert.That(firstSuspendedTick.Trace.Text, Does.Not.Contain("Kind=GravityFieldAura|Tick=2"));

                var secondSuspendedTick = pipeline.RunTick(new TickInput(3));
                var secondSuspendedState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(secondSuspendedState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
                Assert.That(secondSuspendedState.windupStartTick, Is.EqualTo(1));
                Assert.That(secondSuspendedState.windupEndTick, Is.EqualTo(5));
                Assert.That(secondSuspendedState.cooldownTicksRemaining, Is.Zero);
                Assert.That(secondSuspendedTick.Trace.Text, Does.Not.Contain("Kind=GravityFieldAura|Tick=3"));

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Floor, 0, 0));
                var resumedTick = pipeline.RunTick(new TickInput(4));
                var resumedState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(resumedState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
                Assert.That(resumedState.windupStartTick, Is.EqualTo(1));
                Assert.That(resumedState.windupEndTick, Is.EqualTo(5));
                Assert.That(resumedState.activationSequence, Is.EqualTo(1));
                Assert.That(resumedTick.Trace.Text, Does.Not.Contain("Kind=GravityFieldAura|Tick=4"));

                var committedTick = pipeline.RunTick(new TickInput(5));
                var committedState = GetEnemyUtilityState(worldState, 40).EffectStates[0];
                var fieldId = EnemyGravityFieldAuraFieldIds.Compute(40, sourceEffectIndex: 0, activationSequence: 1);

                Assert.That(committedTick.PresentationData.EnemyUtilitySignals.Any(signal => signal.Phase == EnemyUtilityPresentationPhase.AttackStarted), Is.True);
                Assert.That(committedState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
                Assert.That(committedTick.Trace.Text, Does.Contain("Source=40|Effect=0|Kind=GravityFieldAura|Tick=5"));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGravityFieldAuraFieldState(fieldId, out _), Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityWindup_HardInvalidState_CancelsAndAppliesCooldown()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 2,
                radius: 1,
                windupTicks: 2,
                durationTicks: 2,
                recoverTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                Assert.That(GetEnemyUtilityState(worldState, 40).EffectStates[0].phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));

                worldState.CreateWriteContext().SetBoardPresence(40, EntityBoardPresence.Detached);
                var canceledTick = pipeline.RunTick(new TickInput(2));
                var canceledState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(canceledState.phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
                Assert.That(canceledState.cooldownTicksRemaining, Is.EqualTo(2));
                Assert.That(canceledState.windupStartTick, Is.Zero);
                Assert.That(canceledState.windupEndTick, Is.Zero);
                Assert.That(canceledTick.PresentationData.EnemyUtilitySignals, Has.Count.EqualTo(1));
                Assert.That(canceledTick.PresentationData.EnemyUtilitySignals[0].Phase, Is.EqualTo(EnemyUtilityPresentationPhase.Canceled));
                Assert.That(canceledTick.Trace.Text, Does.Not.Contain("Kind=GravityFieldAura|Tick=2"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityGravityFieldAura_WindupThenAttackCreatesField_AndCooldownStartsAfterRecover()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var sliding = CreateBox(entityId: 24, position: new Vector2Int(0, -1), capabilities: BoxCapabilities.Push);
            sliding.state = EntityPhaseState.Sliding;
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 21, position: new Vector2Int(1, 1), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 22, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 23, position: new Vector2Int(-1, -1), capabilities: BoxCapabilities.Push),
                sliding,
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);

                var windupTick = pipeline.RunTick(new TickInput(1));
                var windupState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(windupState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
                Assert.That(worldState.CreateSnapshot().TryGetBoxInteractionLockState(20, out _), Is.False);
                Assert.That(windupTick.PresentationData.EnemyUtilitySignals.Single().Kind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
                Assert.That(windupTick.PresentationData.EnemyUtilitySignals.Single().Phase, Is.EqualTo(EnemyUtilityPresentationPhase.WindupStarted));
                Assert.That(windupTick.PresentationData.EnemyGravityFieldAuraVisualStates.Single().Phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));

                pipeline.RunTick(new TickInput(2));
                var attackTick = pipeline.RunTick(new TickInput(3));
                var recoverState = GetEnemyUtilityState(worldState, 40).EffectStates[0];
                var attackSnapshot = worldState.CreateSnapshot();
                var fieldId = EnemyGravityFieldAuraFieldIds.Compute(40, sourceEffectIndex: 0, activationSequence: 1);

                Assert.That(attackTick.Trace.Text, Does.Contain("Source=40|Effect=0|Kind=GravityFieldAura|Tick=3"));
                Assert.That(recoverState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
                Assert.That(recoverState.recoverStartTick, Is.EqualTo(3));
                Assert.That(recoverState.recoverEndTickExclusive, Is.EqualTo(4));
                Assert.That(attackSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out var fieldState), Is.True);
                Assert.That(fieldState.OriginCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(fieldState.ExpiresTickExclusive, Is.EqualTo(6));
                Assert.That(attackSnapshot.TryGetBoxInteractionLockState(20, out var cardinalLock), Is.True);
                Assert.That(attackSnapshot.TryGetBoxInteractionLockState(21, out var diagonalLock), Is.True);
                Assert.That(attackSnapshot.TryGetBoxInteractionLockState(23, out _), Is.True);
                Assert.That(attackSnapshot.TryGetBoxInteractionLockState(22, out _), Is.False);
                Assert.That(attackSnapshot.TryGetBoxInteractionLockState(24, out _), Is.False);
                Assert.That(cardinalLock.ExpiresTickExclusive, Is.EqualTo(4));
                Assert.That(cardinalLock.BlocksPush, Is.True);
                Assert.That(cardinalLock.BlocksFlip, Is.True);
                Assert.That(cardinalLock.BlocksDestroy, Is.True);
                Assert.That(cardinalLock.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.EnemyGravityFieldAura));
                Assert.That(diagonalLock.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.EnemyGravityFieldAura));
                Assert.That(
                    attackTick.PresentationData.EnemyUtilitySignals.Select(signal => signal.Phase).ToArray(),
                    Is.EqualTo(new[] { EnemyUtilityPresentationPhase.AttackStarted, EnemyUtilityPresentationPhase.RecoverStarted }));
                Assert.That(attackTick.PresentationData.EnemyGravityFieldAuraVisualStates.Single().Phase, Is.EqualTo(EnemyUtilityEffectPhase.Active));
                Assert.That(attackTick.PresentationData.EnemyGravityFieldAuraVisualStates.Single().Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(attackTick.PresentationData.EnemyGravityFieldAuraVisualStates.Single().AreaCells, Has.Count.EqualTo(9));

                var cooldownStartTick = pipeline.RunTick(new TickInput(4));
                Assert.That(GetEnemyUtilityState(worldState, 40).EffectStates[0].cooldownTicksRemaining, Is.EqualTo(4));
                Assert.That(worldState.CreateSnapshot().TryGetBoxInteractionLockState(20, out var refreshedLock), Is.True);
                Assert.That(refreshedLock.ExpiresTickExclusive, Is.EqualTo(5));

                pipeline.RunTick(new TickInput(5));
                var cooldownTick = pipeline.RunTick(new TickInput(6));
                var cooldownState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(cooldownState.phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
                Assert.That(cooldownState.cooldownTicksRemaining, Is.EqualTo(2));
                Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 6, out _), Is.False);
                Assert.That(cooldownStartTick.PresentationData.EnemyUtilityCooldownSignals.Single().Kind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
                Assert.That(cooldownStartTick.PresentationData.EnemyUtilityCooldownSignals.Single().CooldownTicksRemaining, Is.EqualTo(4));
                Assert.That(cooldownTick.PresentationData.EnemyUtilityCooldownSignals.Single().CooldownTicksRemaining, Is.EqualTo(2));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        public enum EnemyGravityFieldAuraSourceInvalidation
        {
            RemoveSource,
            MarkSourceForDeath,
            KillSource,
            DetachSource,
            MoveSourceOffBottom,
        }

        [TestCase(EnemyGravityFieldAuraSourceInvalidation.RemoveSource)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.MarkSourceForDeath)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.KillSource)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.DetachSource)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.MoveSourceOffBottom)]
        [Category("Extended")]
        public void EnemyGravityFieldAura_SourceInvalidAfterEmit_ContinuesLockingFromStoredOrigin(
            EnemyGravityFieldAuraSourceInvalidation invalidation)
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));
                var fieldId = EnemyGravityFieldAuraFieldIds.Compute(40, sourceEffectIndex: 0, activationSequence: 1);
                var emittedSnapshot = worldState.CreateSnapshot();

                Assert.That(emittedSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out _), Is.True);
                Assert.That(emittedSnapshot.TryGetActiveBoxInteractionLockState(20, 3, out var emittedLock), Is.True);
                Assert.That(emittedLock.ExpiresTickExclusive, Is.EqualTo(4));

                InvalidateEnemyGravityFieldAuraSource(worldState, invalidation);
                var invalidTick = pipeline.RunTick(new TickInput(4));
                var invalidSnapshot = worldState.CreateSnapshot();

                Assert.That(
                    invalidSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out var sustainedField),
                    Is.True,
                    invalidation.ToString());
                Assert.That(sustainedField.OriginCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)), invalidation.ToString());
                Assert.That(
                    invalidSnapshot.TryGetActiveBoxInteractionLockState(20, 4, out var sustainedLock),
                    Is.True,
                    invalidation.ToString());
                Assert.That(sustainedLock.ExpiresTickExclusive, Is.EqualTo(5), invalidation.ToString());
                Assert.That(invalidTick.Trace.Text, Does.Not.Contain("EnemyGravityFieldAuraFieldSourceInvalid"), invalidation.ToString());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [TestCase]
        [Category("Extended")]
        public void EnemyGravityFieldAura_SourceRemovedAfterEmit_ContinuesLockingFromOriginCell()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));
                var fieldId = EnemyGravityFieldAuraFieldIds.Compute(40, sourceEffectIndex: 0, activationSequence: 1);

                worldState.CreateWriteContext().RemoveEntity(40);
                pipeline.RunTick(new TickInput(4));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out _), Is.False);
                Assert.That(snapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out var fieldState), Is.True);
                Assert.That(fieldState.OriginCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(snapshot.TryGetActiveBoxInteractionLockState(20, 4, out var lockState), Is.True);
                Assert.That(lockState.SourceEntityId, Is.EqualTo(40));
                Assert.That(lockState.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.EnemyGravityFieldAura));
                Assert.That(lockState.ExpiresTickExclusive, Is.EqualTo(5));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [TestCase]
        [Category("Extended")]
        public void EnemyGravityFieldAura_SourceKilledAfterEmit_ContinuesUntilFieldDurationExpires()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));
                var fieldId = EnemyGravityFieldAuraFieldIds.Compute(40, sourceEffectIndex: 0, activationSequence: 1);

                worldState.CreateWriteContext().ApplyDamage(40, 99);
                pipeline.RunTick(new TickInput(4));
                var activeSnapshot = worldState.CreateSnapshot();

                Assert.That(activeSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out var activeField), Is.True);
                Assert.That(activeField.ExpiresTickExclusive, Is.EqualTo(6));
                Assert.That(activeSnapshot.TryGetActiveBoxInteractionLockState(20, 4, out var renewedLock), Is.True);
                Assert.That(renewedLock.ExpiresTickExclusive, Is.EqualTo(5));

                pipeline.RunTick(new TickInput(5));
                var lastActiveSnapshot = worldState.CreateSnapshot();

                Assert.That(lastActiveSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out _), Is.True);
                Assert.That(lastActiveSnapshot.TryGetActiveBoxInteractionLockState(20, 5, out var lastRenewedLock), Is.True);
                Assert.That(lastRenewedLock.ExpiresTickExclusive, Is.EqualTo(6));

                var expiryTick = pipeline.RunTick(new TickInput(6));
                var expiredSnapshot = worldState.CreateSnapshot();

                Assert.That(expiredSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out _), Is.False);
                Assert.That(expiredSnapshot.TryGetActiveBoxInteractionLockState(20, 6, out _), Is.False);
                Assert.That(expiryTick.Trace.Text, Does.Contain("EnemyGravityFieldAuraFieldExpired"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [TestCase(EnemyGravityFieldAuraSourceInvalidation.RemoveSource)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.MarkSourceForDeath)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.KillSource)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.DetachSource)]
        [TestCase(EnemyGravityFieldAuraSourceInvalidation.MoveSourceOffBottom)]
        [Category("Extended")]
        public void EnemyGravityFieldAura_SourceInvalidBeforeEmit_DoesNotCreateField(
            EnemyGravityFieldAuraSourceInvalidation invalidation)
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));

                InvalidateEnemyGravityFieldAuraSource(worldState, invalidation);
                var emitTick = pipeline.RunTick(new TickInput(3));
                var snapshot = worldState.CreateSnapshot();
                var fieldId = EnemyGravityFieldAuraFieldIds.Compute(40, sourceEffectIndex: 0, activationSequence: 1);

                Assert.That(snapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out _), Is.False, invalidation.ToString());
                Assert.That(snapshot.TryGetActiveBoxInteractionLockState(20, 3, out _), Is.False, invalidation.ToString());
                Assert.That(emitTick.Trace.Text, Does.Not.Contain("Kind=GravityFieldAura|Tick=3"), invalidation.ToString());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [TestCase]
        [Category("Extended")]
        public void EnemyGravityFieldAura_EmittedFieldUsesStoredOriginCellAfterSourceMovesOrDies()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 21, position: new Vector2Int(5, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));
                var fieldId = EnemyGravityFieldAuraFieldIds.Compute(40, sourceEffectIndex: 0, activationSequence: 1);

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Floor, 4, 0));
                pipeline.RunTick(new TickInput(4));
                var movedSnapshot = worldState.CreateSnapshot();

                Assert.That(movedSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out var fieldState), Is.True);
                Assert.That(fieldState.OriginCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(movedSnapshot.TryGetActiveBoxInteractionLockState(20, 4, out _), Is.True);
                Assert.That(movedSnapshot.TryGetActiveBoxInteractionLockState(21, 4, out _), Is.False);

                worldState.CreateWriteContext().RemoveEntity(40);
                pipeline.RunTick(new TickInput(5));
                var removedSnapshot = worldState.CreateSnapshot();

                Assert.That(removedSnapshot.TryGetEnemyGravityFieldAuraFieldState(fieldId, out var removedSourceField), Is.True);
                Assert.That(removedSourceField.OriginCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(removedSnapshot.TryGetActiveBoxInteractionLockState(20, 5, out _), Is.True);
                Assert.That(removedSnapshot.TryGetActiveBoxInteractionLockState(21, 5, out _), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyGravityFieldAura_StaticGravityField_TargetEligibilityParity()
        {
            // Parity here means target eligibility and lock semantics, not source lifetime semantics.
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var auraTargets = CreateGravityFieldParityTargets();
            var auraWorldState = CreateWorldState(auraTargets.Concat(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            }));
            var staticWorldState = CreateWorldState(CreateGravityFieldParityTargets().Concat(new[]
            {
                CreateStaticGravityFieldEmitter(90, new SurfaceCell(FaceId.Floor, 0, 0)),
            }));
            staticWorldState.CreateWriteContext().SetGravityFieldState(90, GravityFieldPhase.Active, timerTicks: 2);

            try
            {
                var auraPipeline = CreateEnemyPipeline(auraWorldState, profile);
                auraPipeline.RunTick(new TickInput(1));
                auraPipeline.RunTick(new TickInput(2));
                auraPipeline.RunTick(new TickInput(3));

                GameplayCompositionRoot.CreateTickPipeline(staticWorldState).RunTick(new TickInput(3));

                var auraSnapshot = auraWorldState.CreateSnapshot();
                var staticSnapshot = staticWorldState.CreateSnapshot();
                Assert.That(ActiveLockTargetIds(auraSnapshot, tickIndex: 3, 20, 21, 22, 23, 24, 25).ToArray(), Is.EqualTo(new[] { 20 }));
                Assert.That(ActiveLockTargetIds(staticSnapshot, tickIndex: 3, 20, 21, 22, 23, 24, 25).ToArray(), Is.EqualTo(new[] { 20 }));
                Assert.That(auraSnapshot.TryGetActiveBoxInteractionLockState(20, 3, out var auraLock), Is.True);
                Assert.That(staticSnapshot.TryGetActiveBoxInteractionLockState(20, 3, out var staticLock), Is.True);
                Assert.That(auraLock.ExpiresTickExclusive, Is.EqualTo(staticLock.ExpiresTickExclusive));
                Assert.That(auraLock.BlocksPush, Is.EqualTo(staticLock.BlocksPush));
                Assert.That(auraLock.BlocksFlip, Is.EqualTo(staticLock.BlocksFlip));
                Assert.That(auraLock.BlocksDestroy, Is.EqualTo(staticLock.BlocksDestroy));
                Assert.That(auraLock.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.EnemyGravityFieldAura));
                Assert.That(staticLock.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.GravityField));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyGravityFieldAuraLockedTargetsAreExposedForPresentation()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 3,
                recoverTicks: 1);
            var sliding = CreateBox(entityId: 24, position: new Vector2Int(0, -1), capabilities: BoxCapabilities.Push);
            sliding.state = EntityPhaseState.Sliding;
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 21, position: new Vector2Int(1, 1), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 22, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 23, position: new Vector2Int(-1, -1), capabilities: BoxCapabilities.Push),
                sliding,
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var attackTick = pipeline.RunTick(new TickInput(3));
                var visualState = attackTick.PresentationData.EnemyGravityFieldAuraVisualStates
                    .Single(state => state.Phase == EnemyUtilityEffectPhase.Active);

                Assert.That(visualState.LockedTargetEntityIds.ToArray(), Is.EqualTo(new[] { 20, 21, 23 }));
                Assert.That(visualState.LockedTargetEntityIds, Is.Not.Contains(22));
                Assert.That(visualState.LockedTargetEntityIds, Is.Not.Contains(24));
                Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 3, out var lockState), Is.True);
                Assert.That(lockState.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.EnemyGravityFieldAura));
                Assert.That(lockState.BlocksPush, Is.True);
                Assert.That(lockState.BlocksFlip, Is.True);
                Assert.That(lockState.BlocksDestroy, Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyGravityFieldAuraVisualStateClearsTargetsWhenAuraExpires()
        {
            var profile = CreateUtilityGravityFieldAuraProfile(
                initialDelayTicks: 0,
                cooldownTicks: 4,
                radius: 1,
                windupTicks: 2,
                durationTicks: 2,
                recoverTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, teamId: 2, position: SurfaceCell.FromPlanar(new Vector2Int(0, 0)), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var activeTick = pipeline.RunTick(new TickInput(3));
                pipeline.RunTick(new TickInput(4));
                var expiredTick = pipeline.RunTick(new TickInput(5));

                Assert.That(
                    activeTick.PresentationData.EnemyGravityFieldAuraVisualStates
                        .Single(state => state.Phase == EnemyUtilityEffectPhase.Active)
                        .LockedTargetEntityIds
                        .ToArray(),
                    Is.EqualTo(new[] { 20 }));
                Assert.That(
                    expiredTick.PresentationData.EnemyGravityFieldAuraVisualStates
                        .SelectMany(state => state.LockedTargetEntityIds)
                        .ToArray(),
                    Is.Empty);
                Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 5, out _), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityGravityFieldAura_SuppressesWindupAndRecover_ButFieldPersistsOnCell()
        {
            var profile = CreateMovingUtilityProfile(
                CreateGravityFieldAuraUtilityEffect(
                    initialDelayTicks: 0,
                    cooldownTicks: 4,
                    radius: 1,
                    windupTicks: 2,
                    durationTicks: 3,
                    recoverTicks: 1,
                    blocksPush: true,
                    blocksFlip: true,
                    blocksDestroy: true,
                    suppressMovementDuringWindup: true,
                    suppressMovementDuringRecover: true));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateBox(entityId: 20, position: new Vector2Int(0, 1), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 21, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Push),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);

                var windupTick = pipeline.RunTick(new TickInput(1));
                var windupState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(windupState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
                Assert.That(windupState.movementSuppressionUntilTickInclusive, Is.EqualTo(3));
                Assert.That(windupTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == 40), Is.Empty);

                pipeline.RunTick(new TickInput(2));
                var attackTick = pipeline.RunTick(new TickInput(3));
                var recoverState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(recoverState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
                Assert.That(attackTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == 40), Is.Empty);
                Assert.That(attackTick.Trace.Text, Does.Contain("Source=40|Effect=0|Kind=GravityFieldAura|Tick=3"));
                Assert.That(attackTick.PresentationData.EnemyGravityFieldAuraVisualStates.Single().Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));

                var sustainedActiveTick = pipeline.RunTick(new TickInput(4));
                var sustainedSnapshot = worldState.CreateSnapshot();

                Assert.That(sustainedActiveTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == 40), Is.Not.Empty);
                Assert.That(sustainedSnapshot.TryGetBoxInteractionLockState(20, out var originLock), Is.True);
                Assert.That(originLock.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.EnemyGravityFieldAura));
                Assert.That(sustainedSnapshot.TryGetBoxInteractionLockState(21, out _), Is.False);
                Assert.That(sustainedActiveTick.PresentationData.EnemyGravityFieldAuraVisualStates.Single().Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BehaviorSummon_WindupDefaultPolicy_DoesNotImplicitlySuppressMovement()
        {
            var profile = CreateBehaviorSummonProfile(
                initialDelayTicks: 0,
                cooldownTicks: 3,
                windupTicks: 2,
                patrolStrategyKind: PatrolStrategyKind.Forward);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                var windupTick = pipeline.RunTick(new TickInput(1));
                var windupState = GetEnemySummonBehaviorState(worldState, 40);

                Assert.That(windupState.phase, Is.EqualTo(EnemySummonBehaviorPhase.Windup));
                Assert.That(windupState.movementSuppressionUntilTickInclusive, Is.Zero);
                Assert.That(windupTick.MovementPhaseResult.RawIntents, Is.Not.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_FatalDamage_IsRemovedByCleanupAtTickEnd()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 1, aiMode: EnemyAiMode.None, facing: Direction.Left),
                });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedAttackLogic(10, 40),
                    new ScriptedAttackLogic(40, 10),
                });

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEquivalent(
                new[]
                {
                    (SourceId: 10, TargetId: 40),
                    (SourceId: 40, TargetId: 10),
                },
                result.AttackPhaseResult
                    .DamageResolutions
                    .Where(record => record.Accepted)
                    .Select(record => (record.SourceId, record.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(new[] { 40 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out _), Is.False);
            Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(2));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=40"));
        }

        [TestCase(FaceId.Front)]
        [TestCase(FaceId.Ceiling)]
        [TestCase(FaceId.Back)]
        public void EnemyAi_MultiTick_OffBottomEnemy_DoesNotMoveOrDamagePlayer(FaceId enemyFace)
        {
            var enemyCell = new SurfaceCell(enemyFace, 0, 0);
            var playerCell = new SurfaceCell(enemyFace, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: enemyCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));
            var profile = CreateNonAttackingEnemyProfile();
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(enemyCell));
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
                Assert.That(firstTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(secondTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(thirdTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(firstTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(thirdTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_TopologyChange_MakesBottomEnemySuspendImmediately()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateStationaryPassiveContactProfile();
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var activeTick = pipeline.RunTick(new TickInput(1));
                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
                var suspendedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(
                    activeTick.AttackPhaseResult.DamageResolutions.Any(
                        record => record.Accepted && record.SourceId == 40),
                    Is.True);
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(2));
                Assert.That(suspendedTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(suspendedTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(suspendedTick.Trace.Text, Does.Not.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40"));
                Assert.That(suspendedTick.Trace.Text, Does.Not.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_TopologyChange_RestoresParticipationWhenEnemyReturnsToBottom()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));
            var profile = CreateStationaryPassiveContactProfile();
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var suspendedTick = pipeline.RunTick(new TickInput(1));
                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
                var resumedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(suspendedTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(suspendedTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(
                    resumedTick.AttackPhaseResult.DamageResolutions.Any(
                        record => record.Accepted &&
                                  record.SourceId == 40 &&
                                  record.TargetId == 10),
                    Is.True);
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(2));
                Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpProfile_OffBottom_DoesNotStartOrProgressJump()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 3, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));

                Assert.That(firstTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(firstTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(40, out _), Is.False);
                Assert.That(firstTick.PresentationData.EnemyJumpSignals, Is.Empty);

                worldState.CreateWriteContext().SetEnemyJumpState(
                    40,
                    new EnemyJumpRuntimeState
                    {
                        phase = EnemyJumpPhase.Windup,
                        sequence = 1,
                        sourceCell = new SurfaceCell(FaceId.Front, 0, 0),
                        lockedTargetCell = new SurfaceCell(FaceId.Front, 2, 0),
                        windupEndTick = 2,
                        landingTick = 3,
                        cooldownRemainingTicks = 0,
                        retryCount = 0,
                    });

                var secondTick = pipeline.RunTick(new TickInput(2));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(secondTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(jumpState.windupEndTick, Is.EqualTo(3));
                Assert.That(jumpState.landingTick, Is.EqualTo(4));
                Assert.That(jumpState.topologySuspendLastTick, Is.EqualTo(2));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_MultiTick_BoundaryPatrol_NeverCommitsTopologyChange()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(
                        entityId: 40,
                        teamId: 2,
                        position: new SurfaceCell(FaceId.Floor, 0, 0),
                        hp: 3,
                        aiMode: EnemyAiMode.Patrol,
                        facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)));
            var profile = CreateForwardNonAttackingProfile();
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var topologyAfterFirstTick = worldState.CreateSnapshot().Topology;
                var secondTick = pipeline.RunTick(new TickInput(2));
                var topologyAfterSecondTick = worldState.CreateSnapshot().Topology;
                var thirdTick = pipeline.RunTick(new TickInput(3));
                var topologyAfterThirdTick = worldState.CreateSnapshot().Topology;

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
                Assert.That(topologyAfterFirstTick, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
                Assert.That(topologyAfterSecondTick, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
                Assert.That(topologyAfterThirdTick, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
                Assert.That(firstTick.EventLog, Has.None.Contains("TopologyCommitted"));
                Assert.That(secondTick.EventLog, Has.None.Contains("TopologyCommitted"));
                Assert.That(thirdTick.EventLog, Has.None.Contains("TopologyCommitted"));
                Assert.That(firstTick.Trace.Text, Does.Not.Contain("TopologyCommitted"));
                Assert.That(secondTick.Trace.Text, Does.Not.Contain("TopologyCommitted"));
                Assert.That(thirdTick.Trace.Text, Does.Not.Contain("TopologyCommitted"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }







        [Test]
        [Category("Extended")]
        public void EnemyAi_ForwardBlockedStop_RemainsInPlace_WithoutUnexpectedFacingWrite()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0)));
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.Forward,
                PatrolSettings = new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                var tick = pipeline.RunTick(new TickInput(1));
                var enemy = GetEntity(worldState, 40);

                Assert.That(tick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(tick.EventLog, Has.None.Contains("MoveCommitted|"));
                Assert.That(tick.Trace.Text, Does.Not.Contain("EnemyPatrolStateUpdated|E=40"));
                Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Right));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }



        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpPatrol_SameFacePlayer_StartsWindupEvenWhenGroundOpen()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateJumpPatrolProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 2);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(GetEntity(worldState, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 1)));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(jumpState.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(firstTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(firstTick.PresentationData.EnemyJumpSignals.Single().StartedWindupThisTick, Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpStart_DifferentFacePlayer_DoesNotStartJump()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateJumpPatrolProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 2);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));

                Assert.That(GetEntity(worldState, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(40, out _), Is.False);
                Assert.That(firstTick.PresentationData.EnemyJumpSignals, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpChase_OpenGround_StartsWindup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(GetEntity(worldState, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 1)));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(jumpState.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(firstTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(firstTick.PresentationData.EnemyJumpSignals.Single().StartedWindupThisTick, Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpStart_FacesLockedTargetAndSignalUsesFacing()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(3, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var enemy = GetEntity(worldState, 40);
                var jumpSignal = firstTick.PresentationData.EnemyJumpSignals.Single();

                Assert.That(enemy.facing, Is.EqualTo(Direction.Left));
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(jumpSignal.StartedWindupThisTick, Is.True);
                Assert.That(jumpSignal.Facing, Is.EqualTo(Direction.Left));
                Assert.That(jumpSignal.WindupTicks, Is.EqualTo(1));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpStart_DiagonalTarget_DefaultTieBreakFacesHorizontal()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 2), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Up),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));

                Assert.That(GetEntity(worldState, 40).facing, Is.EqualTo(Direction.Right));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpStart_DiagonalTarget_VerticalFirstFacesVertical()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 2), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateJumpChaserProfile(
                windupTicks: 1,
                airborneTicks: 1,
                cooldownTicks: 1,
                chaseSettings: new ChaseSettings(
                    ChaseAxisPriorityMode.VerticalFirst,
                    trySecondaryAxisWhenBlocked: true));
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));

                Assert.That(GetEntity(worldState, 40).facing, Is.EqualTo(Direction.Up));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpStart_ZeroWindup_FacesTargetBeforeAirborne()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(3, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 0, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var jumpSignal = firstTick.PresentationData.EnemyJumpSignals.Single();

                Assert.That(GetEntity(worldState, 40).facing, Is.EqualTo(Direction.Left));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpSignal.Facing, Is.EqualTo(Direction.Left));
                Assert.That(jumpSignal.WindupTicks, Is.Zero);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpStart_SameFaceFarPlayer_StartsWindup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(20, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateJumpPatrolProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 2);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(GetEntity(worldState, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 1)));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(jumpState.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 20, 1)));
                Assert.That(firstTick.PresentationData.EnemyJumpSignals.Single().StartedWindupThisTick, Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpStart_LocksPlayerSurfaceCellAtStartTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 2, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(jumpState.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(jumpState.sourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpWindup_KeepsSourceCellOccupied()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 2, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();
                var stackedUnits = new List<EntityState>();

                snapshot.EnumerateUnitsAt(sourceCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                CollectionAssert.AreEqual(new[] { 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Windup));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_Jump_PresentationSignals_EmitWindupAndAirborneFromRuntimePipeline()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var windupTick = pipeline.RunTick(new TickInput(1));
                var windupSignal = windupTick.PresentationData.EnemyJumpSignals.Single();

                Assert.That(windupSignal.EntityId, Is.EqualTo(40));
                Assert.That(windupSignal.Phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(windupSignal.StartedWindupThisTick, Is.True);
                Assert.That(windupSignal.StartedAirborneThisTick, Is.False);
                Assert.That(windupSignal.LandedThisTick, Is.False);
                Assert.That(windupSignal.RetryThisTick, Is.False);
                Assert.That(windupSignal.SourceCell, Is.EqualTo(sourceCell));
                Assert.That(windupSignal.LockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(windupSignal.Facing, Is.EqualTo(Direction.Right));
                Assert.That(windupSignal.LandingTick, Is.EqualTo(3));
                Assert.That(windupSignal.RemainingAirborneTicks, Is.Zero);
                Assert.That(windupSignal.RetryCount, Is.Zero);

                var airborneTick = pipeline.RunTick(new TickInput(2));
                var airborneSignal = airborneTick.PresentationData.EnemyJumpSignals.Single();

                Assert.That(airborneSignal.EntityId, Is.EqualTo(40));
                Assert.That(airborneSignal.Phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(airborneSignal.StartedWindupThisTick, Is.False);
                Assert.That(airborneSignal.StartedAirborneThisTick, Is.True);
                Assert.That(airborneSignal.LandedThisTick, Is.False);
                Assert.That(airborneSignal.RetryThisTick, Is.False);
                Assert.That(airborneSignal.SourceCell, Is.EqualTo(sourceCell));
                Assert.That(airborneSignal.LockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(airborneSignal.Facing, Is.EqualTo(Direction.Right));
                Assert.That(airborneSignal.LandingTick, Is.EqualTo(3));
                Assert.That(airborneSignal.RemainingAirborneTicks, Is.EqualTo(1));
                Assert.That(airborneSignal.RetryCount, Is.Zero);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpAirborne_SetsDetached_AndBecomesUntargetable()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var snapshot = worldState.CreateSnapshot();
                var stackedUnits = new List<EntityState>();

                snapshot.EnumerateUnitsAt(sourceCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(snapshot.CanBeTargetedForNewSelection(40), Is.False);
                Assert.That(stackedUnits, Is.Empty);
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void JumpWindupEnemy_CanBeHitByBoxImpact()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateSlidingPushBox(entityId: 50, position: new Vector2Int(-1, 1), facing: Direction.Right, kineticInstigatorEntityId: 10, kineticInstigatorTeamId: 1),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var impactTick = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.hp, Is.EqualTo(2));
                Assert.That(enemy.position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 1)));
                Assert.That(GetEntity(worldState, 50).state, Is.EqualTo(EntityPhaseState.Idle));
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Windup));
                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 50, TargetId: 40, Position: new SurfaceCell(FaceId.Floor, 0, 1), Damage: 1),
                    },
                    impactTick.AttackPhaseResult
                        .DrainedImpactReservations
                        .Select(reservation => (reservation.SourceId, reservation.TargetId, reservation.ImpactCell, reservation.Damage))
                        .ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void JumpAirborneEnemy_IsIgnoredByBoxImpact()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateBox(entityId: 50, position: new Vector2Int(-1, 1), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var writeContext = (IMovementCommitContext)worldState.CreateWriteContext();
                writeContext.ApplyStateChange(50, EntityPhaseState.Sliding, stateTimer: 0);
                writeContext.SetFacing(50, Direction.Right);
                writeContext.SetBoxKineticOwner(50, 10, 1);

                var airborneTick = pipeline.RunTick(new TickInput(2));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.hp, Is.EqualTo(3));
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(sourceCell));
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(airborneTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAi_JumpLanding_OnLockedPlayerCell_AllowsUnitStacking()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var snapshot = worldState.CreateSnapshot();
                var stackedUnits = new List<EntityState>();

                snapshot.EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                CollectionAssert.AreEqual(new[] { 10, 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetExact"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAi_JumpLanding_OnLockedPlayerCell_WithHostileExtraOccupant_Lands()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 60, teamId: 2, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                CollectionAssert.AreEqual(new[] { 10, 40, 60 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.retryCount, Is.EqualTo(0));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetExact"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Target=0"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_OnLockedPlayerCell_WithFriendlyExtraOccupant_Lands()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 20, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                CollectionAssert.AreEqual(new[] { 10, 20, 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.retryCount, Is.EqualTo(0));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetExact"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Target=0"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_SameTickUnitReservedLockedTarget_AllowsStacking()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 60, teamId: 2, position: new Vector2Int(2, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(
                worldState,
                profile,
                new TickScriptedMovementLogic(
                    60,
                    new Dictionary<int, RawMovementIntent>
                    {
                        { 3, new RawMovementIntent(60, 5, targetCell.PlanarPosition) },
                    }));
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 60).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                CollectionAssert.AreEqual(new[] { 10, 40, 60 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.retryCount, Is.EqualTo(0));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetExact"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Target=0"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_ResolveUpgrade_WhenExtraOccupantLeavesBeforeResolve()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var retreatCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 60, teamId: 2, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(
                worldState,
                profile,
                new TickScriptedMovementLogic(
                    60,
                    new Dictionary<int, RawMovementIntent>
                    {
                        { 3, new RawMovementIntent(60, 5, retreatCell.PlanarPosition) },
                    }));
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 60).position, Is.EqualTo(retreatCell));
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                CollectionAssert.AreEqual(new[] { 10, 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetExact"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_ExtraOccupantDoesNotRequireRetry()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 60, teamId: 2, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                CollectionAssert.AreEqual(new[] { 10, 40, 60 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.retryCount, Is.EqualTo(0));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetExact"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Target=0"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_PersistentExtraOccupant_LandsImmediately()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 60, teamId: 2, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                CollectionAssert.AreEqual(new[] { 10, 40, 60 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.retryCount, Is.EqualTo(0));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetExact"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Target=0"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void JumpLandingThenBoxImpact_SameTick_UsesLandedOccupancy()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateBox(entityId: 50, position: new Vector2Int(2, 1), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));

                var writeContext = (IMovementCommitContext)worldState.CreateWriteContext();
                writeContext.ApplyStateChange(50, EntityPhaseState.Sliding, stateTimer: 0);
                writeContext.SetFacing(50, Direction.Right);
                writeContext.SetBoxKineticOwner(50, 10, 1);

                var landingImpactTick = pipeline.RunTick(new TickInput(3));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(player.hp, Is.EqualTo(2));
                Assert.That(enemy.position, Is.EqualTo(targetCell));
                Assert.That(enemy.hp, Is.EqualTo(2));
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 1)));
                Assert.That(GetEntity(worldState, 50).state, Is.EqualTo(EntityPhaseState.Idle));
                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 50, TargetId: 10, Position: new SurfaceCell(FaceId.Floor, 3, 1), Damage: 1),
                        (SourceId: 50, TargetId: 40, Position: new SurfaceCell(FaceId.Floor, 3, 1), Damage: 1),
                    },
                    landingImpactTick.AttackPhaseResult
                        .DrainedImpactReservations
                        .Select(reservation => (reservation.SourceId, reservation.TargetId, reservation.ImpactCell, reservation.Damage))
                        .ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxImpactFollowThrough_UpdatesPostMovementBeforeJumpLandingResolve()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateSlidingPushBox(entityId: 50, position: new Vector2Int(2, 1), facing: Direction.Right, kineticInstigatorEntityId: 10, kineticInstigatorTeamId: 1),
                CreateUnit(entityId: 60, teamId: 2, position: targetCell, hp: 1, facing: Direction.Left),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var writeContext = worldState.CreateWriteContext();
                writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
                writeContext.SetEnemyJumpState(
                    40,
                    new EnemyJumpRuntimeState
                    {
                        phase = EnemyJumpPhase.Airborne,
                        sequence = 1,
                        sourceCell = sourceCell,
                        lockedTargetCell = targetCell,
                        windupEndTick = 0,
                        landingTick = 1,
                        cooldownRemainingTicks = 0,
                        retryCount = 0,
                    });

                var tick = pipeline.RunTick(new TickInput(1));
                var jumpState = GetEnemyJumpState(worldState, 40);

                CollectionAssert.AreEqual(new[] { 60 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(tick.EventLog));
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        tick.MovementPhaseResult.CommitEvents,
                        "MoveCommitted",
                        "E=50",
                        "To=(3,1)",
                        "Facing=Right"),
                    Is.True);
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(targetCell));
                Assert.That(GetEntity(worldState, 50).state, Is.EqualTo(EntityPhaseState.Sliding));
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.lockedTargetCell, Is.EqualTo(targetCell));
                Assert.That(jumpState.retryCount, Is.EqualTo(1));
                Assert.That(jumpState.landingTick, Is.EqualTo(2));
                Assert.That(tick.Trace.Text, Does.Contain("Label=Retry"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAi_JumpLanding_BoxOnLockedCell_UsesTwoRingFallback()
        {
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: lockedTargetCell, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(1, 0)),
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var writeContext = worldState.CreateWriteContext();
                writeContext.MoveEntity(10, new SurfaceCell(FaceId.Floor, 4, 1));
                writeContext.MoveEntity(50, lockedTargetCell);

                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(lockedTargetCell));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetRing1Forward"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_SameTickUnitReservedFallbackCell_AllowsStacking()
        {
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: lockedTargetCell, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    CreateUnit(entityId: 60, teamId: 2, position: new Vector2Int(3, 0), hp: 3),
                    CreateBox(entityId: 50, position: new Vector2Int(1, 0)),
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(
                worldState,
                profile,
                new TickScriptedMovementLogic(
                    60,
                    new Dictionary<int, RawMovementIntent>
                    {
                        { 3, new RawMovementIntent(60, 5, fallbackCell.PlanarPosition) },
                    }));
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var writeContext = worldState.CreateWriteContext();
                writeContext.MoveEntity(10, new SurfaceCell(FaceId.Floor, 4, 1));
                writeContext.MoveEntity(50, lockedTargetCell);

                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(fallbackCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(fallbackCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                Assert.That(GetEntity(worldState, 60).position, Is.EqualTo(fallbackCell));
                CollectionAssert.AreEqual(new[] { 40, 60 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.retryCount, Is.EqualTo(0));
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetRing1Forward"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAi_JumpLanding_JumpCrushableBoxOnLockedCell_CrushesAndLands()
        {
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var playerRetreatCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: lockedTargetCell, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.JumpCrushable),
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var writeContext = worldState.CreateWriteContext();
                writeContext.MoveEntity(10, playerRetreatCell);
                writeContext.MoveEntity(50, lockedTargetCell);

                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var snapshot = worldState.CreateSnapshot();
                var stackedUnits = new List<EntityState>();

                snapshot.EnumerateUnitsAt(lockedTargetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(lockedTargetCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                Assert.That(snapshot.TryGetEntity(50, out _), Is.False);
                Assert.That(snapshot.TryGetBoxAt(lockedTargetCell, out _), Is.False);
                CollectionAssert.AreEqual(new[] { 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                CollectionAssert.AreEqual(new[] { 50 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(landingTick.EventLog));
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetCrushBox"));

                var jumpSignal = landingTick.PresentationData.EnemyJumpSignals.Single(signal => signal.EntityId == 40);
                Assert.That(jumpSignal.Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded));
                Assert.That(jumpSignal.LandedThisTick, Is.True);
                Assert.That(jumpSignal.RetryThisTick, Is.False);

                var exitSignal = landingTick.PresentationData.EntityExitSignals.Single(signal => signal.ExitedEntityId == 50);
                Assert.That(exitSignal.ExitCause, Is.EqualTo(TickEntityExitCause.BoxDestroy));
                Assert.That(exitSignal.SourceActorEntityId, Is.EqualTo(40));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [TestCase((int)BoxCapabilities.None)]
        [TestCase((int)BoxCapabilities.Destroy)]
        [TestCase((int)BoxCapabilities.Item)]
        public void EnemyAi_JumpLanding_NonJumpCrushableBoxOnLockedCell_FallsBackWithoutDestroy(int boxCapabilityValue)
        {
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: lockedTargetCell, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(1, 0), capabilities: (BoxCapabilities)boxCapabilityValue),
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var writeContext = worldState.CreateWriteContext();
                writeContext.MoveEntity(10, new SurfaceCell(FaceId.Floor, 4, 1));
                writeContext.MoveEntity(50, lockedTargetCell);

                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(lockedTargetCell));
                Assert.That(GetEntity(worldState, 50).markedForDeath, Is.False);
                Assert.That(SemanticEventAssertions.GetCleanupRemovedEntityIds(landingTick.EventLog), Is.Empty);
                Assert.That(landingTick.PresentationData.EntityExitSignals.Any(signal => signal.ExitedEntityId == 50), Is.False);
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=TargetRing1Forward"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_TargetTwoRingBlocked_FallsBackToSourceTwoRing()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: lockedTargetCell, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(1, 0)),
                    CreateBox(entityId: 51, position: new Vector2Int(2, 0)),
                    CreateWall(entityId: 60, position: new Vector2Int(4, 2)),
                    CreateWall(entityId: 61, position: new Vector2Int(4, 0)),
                    CreateWall(entityId: 62, position: new Vector2Int(3, 1)),
                    CreateWall(entityId: 63, position: new Vector2Int(3, 2)),
                    CreateWall(entityId: 64, position: new Vector2Int(3, 0)),
                    CreateWall(entityId: 65, position: new Vector2Int(2, 1)),
                    CreateWall(entityId: 66, position: new Vector2Int(1, 1)),
                    CreateWall(entityId: 67, position: new Vector2Int(0, 2)),
                    CreateWall(entityId: 68, position: new Vector2Int(0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var writeContext = worldState.CreateWriteContext();
                writeContext.MoveEntity(10, new SurfaceCell(FaceId.Front, 2, 2));
                writeContext.MoveEntity(50, lockedTargetCell);

                pipeline.RunTick(new TickInput(2));
                worldState.CreateWriteContext().MoveEntity(51, sourceCell);
                var landingTick = pipeline.RunTick(new TickInput(3));

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 2)));
                Assert.That(landingTick.Trace.Text, Does.Contain("Rule=SourceRing2ForwardLeft"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_NoLegalCellWithinAllowedSpace_StaysAirborne_AndRetriesSameLockedTarget()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: lockedTargetCell, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(2, 2)),
                    CreateBox(entityId: 51, position: new Vector2Int(2, 0)),
                    CreateWall(entityId: 60, position: new Vector2Int(4, 2)),
                    CreateWall(entityId: 61, position: new Vector2Int(4, 0)),
                    CreateWall(entityId: 62, position: new Vector2Int(3, 1)),
                    CreateWall(entityId: 63, position: new Vector2Int(3, 2)),
                    CreateWall(entityId: 64, position: new Vector2Int(3, 0)),
                    CreateWall(entityId: 65, position: new Vector2Int(2, 1)),
                    CreateWall(entityId: 66, position: new Vector2Int(1, 1)),
                    CreateWall(entityId: 67, position: new Vector2Int(0, 2)),
                    CreateWall(entityId: 68, position: new Vector2Int(1, 2)),
                    CreateWall(entityId: 69, position: new Vector2Int(0, 0)),
                    CreateWall(entityId: 70, position: new Vector2Int(1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                var writeContext = worldState.CreateWriteContext();
                writeContext.MoveEntity(10, new SurfaceCell(FaceId.Front, 2, 2));
                writeContext.MoveEntity(50, lockedTargetCell);

                pipeline.RunTick(new TickInput(2));
                worldState.CreateWriteContext().MoveEntity(51, sourceCell);
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.lockedTargetCell, Is.EqualTo(lockedTargetCell));
                Assert.That(jumpState.retryCount, Is.EqualTo(1));
                Assert.That(jumpState.landingTick, Is.EqualTo(4));
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Retry"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_Jump_DoesNotUseAttackPhase()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));

                Assert.That(firstTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(thirdTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(40, out _), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpCooldown_SameFacePlayer_DoesNotRestartDuringCooldown()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateWall(entityId: 90, position: new Vector2Int(4, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 2)));
            var profile = CreateJumpPatrolProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 2);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));
                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 5, 1));

                var cooldownTick = pipeline.RunTick(new TickInput(4));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var cooldownSignal = cooldownTick.PresentationData.EnemyJumpSignals.Single();

                Assert.That(GetEntityAfterTick(cooldownTick, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.cooldownRemainingTicks, Is.EqualTo(1));
                Assert.That(cooldownSignal.StartedWindupThisTick, Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpCooldown_OpenGround_ChasesBeforeCooldownCompletes()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(6, 2)));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 3);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));
                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 5, 1));

                var chaseIntentTick = pipeline.RunTick(new TickInput(4));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var chaseIntent = chaseIntentTick.MovementPhaseResult.RawIntents.Single(intent => intent.SourceId == 40);

                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.cooldownRemainingTicks, Is.GreaterThan(0));
                Assert.That(chaseIntent.Destination, Is.EqualTo(new Vector2Int(4, 1)));
                Assert.That(
                    chaseIntentTick.MovementPhaseResult.RejectedReasons,
                    Has.None.Contains("Source=40"),
                    chaseIntentTick.Trace.Text);
                Assert.That(chaseIntentTick.PresentationData.EnemyJumpSignals.Single().StartedWindupThisTick, Is.False);

                var chaseCommitTick = pipeline.RunTick(new TickInput(5));
                jumpState = GetEnemyJumpState(worldState, 40);
                var enemyAfterChase = GetEntityAfterTick(chaseCommitTick, 40);

                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.cooldownRemainingTicks, Is.GreaterThan(0));
                Assert.That(enemyAfterChase.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 1)));
                Assert.That(chaseCommitTick.PresentationData.EnemyJumpSignals.Single().StartedWindupThisTick, Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpCooldown_Complete_WithSameFacePlayer_AllowsNewJump()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateWall(entityId: 90, position: new Vector2Int(4, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 2)));
            var profile = CreateJumpPatrolProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 2);
            var pipeline = CreateEnemyPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));
                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 5, 1));

                pipeline.RunTick(new TickInput(4));
                var cooldownCompleteTick = pipeline.RunTick(new TickInput(5));
                var restartTick = pipeline.RunTick(new TickInput(6));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(GetEntityAfterTick(cooldownCompleteTick, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 1)));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(jumpState.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 5, 1)));
                Assert.That(restartTick.PresentationData.EnemyJumpSignals.Single().StartedWindupThisTick, Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }


        [Test]
        [Category("Full")]
        public void EnemyAi_WallFollowerProfile_PlayerInSenseRange_RemainsInPatrolPermanently()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 2)));
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Right);
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));
                var enemy = GetEntity(worldState, 40);
                var player = GetEntity(worldState, 10);

                Assert.That(firstTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(thirdTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
                Assert.That(player.hp, Is.EqualTo(3));
                Assert.That(firstTick.Trace.Text, Does.Not.Contain("To=Chase"));
                Assert.That(secondTick.Trace.Text, Does.Not.Contain("To=Chase"));
                Assert.That(thirdTick.Trace.Text, Does.Not.Contain("To=Chase"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PassiveContactProfile_MovesIntoPlayerCell_AndDealsSameTickDamage()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshotAfter = worldState.CreateSnapshot();
                var stackedUnits = new List<EntityState>();
                var enemy = GetEntity(worldState, 40);
                var player = GetEntity(worldState, 10);

                Assert.That(
                    result.AttackPhaseResult.DamageResolutions.Any(
                        record => record.Accepted &&
                                  record.SourceId == 40 &&
                                  record.TargetId == 10),
                    Is.True);
                Assert.That(result.AttackPhaseResult.DamageResolutions.Count(record => record.Accepted), Is.EqualTo(1));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(player.hp, Is.EqualTo(2));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(enemy.aiStateTimer, Is.EqualTo(0));
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));

                snapshotAfter.EnumerateUnitsAt(new Vector2Int(0, 0), stackedUnits);
                CollectionAssert.AreEqual(new[] { 10, 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(result.Trace.Text, Does.Contain("Attack.DamageResolutions"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyMovesIntoPlayer_LegacyFallbackBaseline_CommitsMoveAndPassiveContact()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "MoveCommitted",
                        "E=40"),
                    Is.True,
                    BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                Assert.That(enemy.position, Is.EqualTo(playerCell), BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                Assert.That(player.position, Is.EqualTo(playerCell));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(
                    HasAcceptedPassiveContact(result, 40, 10),
                    Is.True,
                    BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                Assert.That(player.hp, Is.EqualTo(2));
                Assert.That(
                    result.EventLog.Any(entry =>
                        entry.Contains("DamageCommitted", StringComparison.Ordinal) &&
                        entry.Contains("SourceKind=PassiveContact", StringComparison.Ordinal) &&
                        entry.Contains("Target=10", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovesIntoPlayer_LegacyFallbackBaseline_PublishesLegacyMotionAndContact()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(playerCell), BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                Assert.That(player.position, Is.EqualTo(playerCell));
                Assert.That(
                    HasAcceptedPassiveContact(result, 40, 10),
                    Is.True,
                    BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                Assert.That(
                    result.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40 &&
                        motion.MotionKind == TickEntityMotionKind.Move &&
                        motion.SourceCell == enemySourceCell &&
                        motion.DestinationCell == playerCell),
                    Is.True,
                    BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                Assert.That(result.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyMovesIntoPlayer_Kinematic_NoPassiveContactWithoutFinalizedSameCellMove()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);

                for (var tick = 1; tick <= 9; tick++)
                {
                    var result = pipeline.RunTick(new TickInput(tick));
                    var snapshot = worldState.CreateSnapshot();

                    Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                    Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                    Assert.That(enemy.position, Is.EqualTo(enemySourceCell), BuildContactTimingDebug(tick, "Enemy", 40, 10, snapshot, result));
                    Assert.That(player.position, Is.EqualTo(playerCell));
                    Assert.That(snapshot.TryGetUnitKinematicState(40, out var enemyKinematic), Is.True);
                    Assert.That(enemyKinematic.mode, Is.EqualTo(MotionMode.Voluntary));
                    Assert.That(
                        HasAcceptedPassiveContact(result, 40, 10),
                        Is.False,
                        BuildContactTimingDebug(tick, "Enemy", 40, 10, snapshot, result));
                    Assert.That(player.hp, Is.EqualTo(3));
                }
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyMovesIntoPlayer_Kinematic_PassiveContactFiresOnFinalizedSameCellMove()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);

                TickResult result = null;
                for (var tick = 1; tick <= 10; tick++)
                {
                    result = pipeline.RunTick(new TickInput(tick));
                }

                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(playerCell), BuildContactTimingDebug(10, "Enemy", 40, 10, snapshot, result));
                Assert.That(player.position, Is.EqualTo(playerCell));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary));
                Assert.That(state.elapsedTicks, Is.EqualTo(10));
                Assert.That(state.commitTick, Is.EqualTo(10));
                Assert.That(
                    result.MovementPhaseResult.CommitEvents.Any(entry =>
                        entry.Contains("KinematicAnchorCommitted", StringComparison.Ordinal) &&
                        entry.Contains("E=40", StringComparison.Ordinal) &&
                        entry.Contains("To=(0,0)", StringComparison.Ordinal)),
                    Is.True);
                LegacyMovementBoundaryAssert.HasMoveEntityBoundaryReason(
                    result,
                    40,
                    MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                    "OrdinaryKinematicAnchorCommit");
                LegacyMovementBoundaryAssert.NoEnemyLegacyOrdinaryFallback(result, 40);
                Assert.That(
                    HasAcceptedPassiveContact(result, 40, 10),
                    Is.True,
                    BuildContactTimingDebug(10, "Enemy", 40, 10, snapshot, result));
                Assert.That(player.hp, Is.EqualTo(2));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void Phase5_EnemyKinematicAnchorCommit_UsesMoveEntityButNoLegacyMove()
        {
            EnemyMovesIntoPlayer_Kinematic_PassiveContactFiresOnFinalizedSameCellMove();
        }

        [Test]
        [Category("Core")]
        [Category("GlideKinematicV11")]
        public void GlideActive_NoPassiveContactWithoutFinalizedSameCellMove()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(
                    entityId: 40,
                    teamId: 2,
                    position: enemySourceCell,
                    hp: 3,
                    aiMode: EnemyAiMode.Chase,
                    facing: Direction.Left,
                    enemyLocomotionCooldownTicks: 3),
            });
            var profile = CreateGlidePassiveContactProfile(durationTicks: 20);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 20, durationTicks: 20, recoveryTicks: 1, cooldownTicks: 0));

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);

                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(enemySourceCell), BuildContactTimingDebug(1, "EnemyGlide", 40, 10, snapshot, result));
                Assert.That(player.position, Is.EqualTo(playerCell));
                Assert.That(HasMoveEntityTo(result, 40, playerCell), Is.False);
                Assert.That(
                    HasAcceptedPassiveContact(result, 40, 10),
                    Is.False,
                    BuildContactTimingDebug(1, "EnemyGlide", 40, 10, snapshot, result));
                Assert.That(player.hp, Is.EqualTo(3));
                LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(result, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        [Category("GlideKinematicV11")]
        public void GlideActive_PassiveContactFiresOnFinalizedSameCellMove()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateGlidePassiveContactProfile(durationTicks: 20);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 20, durationTicks: 20, recoveryTicks: 1, cooldownTicks: 0));

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);

                var result = pipeline.RunTick(new TickInput(1));

                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(playerCell), BuildContactTimingDebug(1, "EnemyGlide", 40, 10, snapshot, result));
                Assert.That(player.position, Is.EqualTo(playerCell));
                Assert.That(HasMoveEntityTo(result, 40, playerCell), Is.True);
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary));
                Assert.That(
                    result.MovementPhaseResult.CommitEvents.Any(entry =>
                        entry.Contains("KinematicAnchorCommitted", StringComparison.Ordinal) &&
                        entry.Contains("E=40", StringComparison.Ordinal) &&
                        entry.Contains("To=(0,0)", StringComparison.Ordinal)),
                    Is.True);
                LegacyMovementBoundaryAssert.HasMoveEntityBoundaryReason(
                    result,
                    40,
                    MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                    "GlideActiveKinematicAnchorCommit");
                Assert.That(
                    HasAcceptedPassiveContact(result, 40, 10),
                    Is.True,
                    BuildContactTimingDebug(1, "EnemyGlide", 40, 10, snapshot, result));
                Assert.That(player.hp, Is.EqualTo(2));
                LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(result, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideCooldown_Kinematic_AllowsOrdinaryChaseMovement()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateGlidePassiveContactProfile(durationTicks: 20);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateCooldownGlide(cooldownUntilTickExclusive: 100, durationTicks: 20, recoveryTicks: 2, cooldownTicks: 100));

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEnemyGlideState(40, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary));
                Assert.That(state.stepDirectionX, Is.EqualTo(-1));
                Assert.That(state.stepDirectionY, Is.Zero);
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Voluntary),
                    Is.True,
                    BuildContactTimingDebug(1, "EnemyGlideCooldown", 40, 10, snapshot, result));
                Assert.That(result.Trace.Text, Does.Not.Contain("EnemyGlideActiveKinematicStart"));
                LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(result, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_HitNonlethal_CleansUpWithoutStaleHover()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateGlidePassiveContactProfile(durationTicks: 20);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 20, durationTicks: 20, recoveryTicks: 2, cooldownTicks: 0));

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                    new ScriptedAttackLogic(10, 40));
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.hp, Is.EqualTo(2));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var interrupted), Is.True);
                Assert.That(interrupted.mode, Is.EqualTo(MotionMode.Interrupted));
                Assert.That(snapshot.TryGetEnemyGlideState(40, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                Assert.That(result.AttackPhaseResult.MotionInterruptRecords.Any(record => record.EntityId == 40), Is.True);
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.TerminalKind == TickKinematicMotionTerminalKind.Interrupted),
                    Is.True);
                Assert.That(
                    result.PresentationData.EnemyGlideSignals.Any(signal =>
                        signal.EntityId == 40 &&
                        signal.Phase == EnemyGlidePhase.Active &&
                        !signal.IsTerminalZero),
                    Is.False);

                var closureTick = pipeline.RunTick(new TickInput(2));
                Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(
                    closureTick.EventLog.Any(entry =>
                        entry.Contains("KinematicInterruptClosed|E=40", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_HitLethal_RemovalClearsKinematicAndGlideSignal()
        {
            AssertGlideActiveKinematicHitDeathCleanupStable(
                GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_DefaultGameplay_HitDeathCleanupStable()
        {
            AssertGlideActiveKinematicHitDeathCleanupStable(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
        }

        private static void AssertGlideActiveKinematicHitDeathCleanupStable(
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 1, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateGlidePassiveContactProfile(durationTicks: 20);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 20, durationTicks: 20, recoveryTicks: 2, cooldownTicks: 0));

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    runtimeFeatureFlags,
                    new ScriptedAttackLogic(10, 40));
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out _), Is.False);
                Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(snapshot.TryGetEnemyGlideState(40, out _), Is.False);
                Assert.That(result.EventLog.Any(entry => entry.Contains("CleanupRemoved|E=40", StringComparison.Ordinal)), Is.True);
                Assert.That(result.EventLog.Any(entry => entry.Contains("KinematicPoseRemoved|E=40", StringComparison.Ordinal)), Is.True);
                Assert.That(result.EventLog.Any(entry => entry.Contains("PlayerRespawnDelayStarted|E=40", StringComparison.Ordinal)), Is.False);
                Assert.That(result.PresentationData.EnemyGlideSignals.Any(signal => signal.EntityId == 40), Is.False);
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.TerminalKind == TickKinematicMotionTerminalKind.Removed),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_ContinuationDoesNotTreatArbitraryVoluntaryStateAsGlide()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            worldState.CreateWriteContext().SetUnitKinematicState(
                40,
                new UnitKinematicRuntimeState
                {
                    localOffset = new KinematicOffset2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                    velocity = new KinematicVelocity2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                    mode = MotionMode.Voluntary,
                    remainingDistanceUnits = 3072,
                    remainingTicks = 3,
                    speedScalePermille = 1000,
                    sequenceId = 1,
                    elapsedTicks = 1,
                    totalTicks = 4,
                    commitTick = 2,
                    startedTick = 1,
                    stepDirectionX = 1,
                });
            var profile = CreateGlidePassiveContactProfile(durationTicks: 4);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(2));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.elapsedTicks, Is.EqualTo(1));
                Assert.That(result.Trace.Text, Does.Not.Contain("EnemyGlideActiveKinematicContinuation"));
                Assert.That(result.Trace.Text, Does.Not.Contain("GlideActiveKinematicAnchorCommit"));
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40),
                    Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovesIntoPlayer_Kinematic_ViewUsesKinematicTrack()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary));
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.SourceAnchorCell == enemySourceCell &&
                        track.DestinationAnchorCell == enemySourceCell &&
                        track.MotionMode == MotionMode.Voluntary),
                    Is.True,
                    BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                Assert.That(
                    result.PresentationData.EntityMotions.Any(motion => motion.EntityId == 40),
                    Is.False,
                    BuildContactTimingDebug(1, "Enemy", 40, 10, snapshot, result));
                LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(result, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyChase_KinematicContinuation_DoesNotReplanEachTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(firstTick.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == 40), Is.True);
                Assert.That(secondTick.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == 40), Is.False);
                Assert.That(secondTick.MovementPhaseResult.SortedIntents.Any(intent => intent.SourceId == 40), Is.False);
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.elapsedTicks, Is.EqualTo(2));
                Assert.That(
                    secondTick.MovementPhaseResult.CommitEvents.Any(entry =>
                        entry.Contains("KinematicPoseCommitted", StringComparison.Ordinal) &&
                        entry.Contains("E=40", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrol_KinematicMovement()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var profile = CreateNonAttackingEnemyProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(result.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == 40), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(sourceCell));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary));
                Assert.That(state.stepDirectionX, Is.EqualTo(1));
                Assert.That(state.stepDirectionY, Is.EqualTo(0));
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Voluntary),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrol_KinematicDuration_FallsBackToMoveCooldownTiming()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var profile = CreateNonAttackingEnemyProfile(
                moveCooldownSeconds: 8f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                ordinaryKinematicMoveDurationSeconds: 0f);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(8));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.totalTicks, Is.EqualTo(8));
                Assert.That(state.commitTick, Is.EqualTo(4));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrol_KinematicDuration_ExplicitDurationDoesNotChangeMoveCooldown()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var profile = CreateNonAttackingEnemyProfile(
                moveCooldownSeconds: 12f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                ordinaryKinematicMoveDurationSeconds: 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(12));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.totalTicks, Is.EqualTo(4));
                Assert.That(state.commitTick, Is.EqualTo(2));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrol_KinematicDuration_UnsetTimingUsesConfiguredPlayerFallback()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var profile = CreateNonAttackingEnemyProfile(
                moveCooldownSeconds: 0f,
                ordinaryKinematicMoveDurationSeconds: 0f);

            try
            {
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var fallbackTiming = new PlayerKinematicLocomotionTimingSettings
                {
                    KinematicMoveDurationSeconds = 6f / timingProfile.SimulationTicksPerSecond,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled,
                    fallbackTiming);
                pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.totalTicks, Is.EqualTo(6));
                Assert.That(state.commitTick, Is.EqualTo(3));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_ChargeKinematicFlagOff_NoLegacyChargeMoveProducer()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0);
            worldState.CreateWriteContext().SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 2,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(result.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.False);
                LegacyMovementBoundaryAssert.RequiresExplicitLegacyFallbackBaseline(result, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_KinematicFlag_ActiveStepUsesChargeKinematicMove()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, chargeStepCooldownTicks: 3);
            worldState.CreateWriteContext().SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 2,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True);
                Assert.That(state.mode, Is.EqualTo(MotionMode.Charge));
                Assert.That(state.elapsedTicks, Is.EqualTo(1));
                Assert.That(state.totalTicks, Is.EqualTo(4));
                Assert.That(state.commitTick, Is.EqualTo(2));
                Assert.That(result.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 40 &&
                    track.MotionMode == MotionMode.Charge), Is.True);
                Assert.That(
                    result.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40),
                    Is.False);
                LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(result, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_KinematicFlag_ContactStartsAtCommitAndConsumesStepAtSettle()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, chargeStepCooldownTicks: 4);
            worldState.CreateWriteContext().SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);

                var beforeCommit = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(HasAcceptedPassiveContact(beforeCommit, 40, 10), Is.False);
                Assert.That(snapshot.TryGetEnemyChargeState(40, out var chargeState), Is.True);
                Assert.That(chargeState.remainingActiveSteps, Is.EqualTo(1));

                var commit = pipeline.RunTick(new TickInput(2));
                snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetEntity(40, out enemy), Is.True);
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(HasAcceptedPassiveContact(commit, 40, 10), Is.True);
                Assert.That(snapshot.TryGetEnemyChargeState(40, out chargeState), Is.True);
                Assert.That(chargeState.remainingActiveSteps, Is.EqualTo(1));

                pipeline.RunTick(new TickInput(3));
                var settled = pipeline.RunTick(new TickInput(4));
                snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(snapshot.TryGetEnemyChargeState(40, out chargeState), Is.True);
                Assert.That(chargeState.remainingActiveSteps, Is.Zero);
                Assert.That(
                    settled.MovementPhaseResult.CommitEvents.Any(entry =>
                        entry.Contains("EnemyChargeStateUpdated", StringComparison.Ordinal) &&
                        entry.Contains("ConsumeChargeKinematicSettledStep", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_TopologySuspend_DoesNotClearChargeIntoStuckPendingState()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Front));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, chargeStepCooldownTicks: 0);
            SeedActiveCharge(worldState, 40, Direction.Right, remainingActiveSteps: 2);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);
                var suspendedTick = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEnemyChargeState(40, out var chargeState), Is.True);
                Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.Active));
                Assert.That(suspendedTick.Trace.Text, Does.Not.Contain("ClearNonParticipant"));
                Assert.That(
                    suspendedTick.PresentationData.EnemyChargeSignals.Any(signal =>
                        signal.EntityId == 40 &&
                        signal.Phase == EnemyChargePhase.Active),
                    Is.False,
                    BuildChargeKinematicDebug(1, worldState, suspendedTick));

                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
                var resumedTick = pipeline.RunTick(new TickInput(2));
                snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEnemyChargeState(40, out chargeState), Is.True);
                Assert.That(chargeState.phase, Is.Not.EqualTo(EnemyChargePhase.None));
                Assert.That(GetEntityAfterTick(resumedTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(
                    resumedTick.PresentationData.EnemyChargeSignals.Any(signal =>
                        signal.EntityId == 40 &&
                        signal.Phase == EnemyChargePhase.Active),
                    Is.True,
                    BuildChargeKinematicDebug(2, worldState, resumedTick));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_ChargeKinematicFlagOff_PatrolToChargeWaitsForOrdinarySettleThenRejectsLegacyChargeMove()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 2, chargeStepCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled,
                    new PlayerKinematicLocomotionTimingSettings
                    {
                        KinematicMoveDurationSeconds = 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

                var firstTick = pipeline.RunTick(new TickInput(1));

                Assert.That(
                    firstTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40),
                    Is.True,
                    BuildChargeKinematicDebug(1, worldState, firstTick));

                var secondTick = pipeline.RunTick(new TickInput(2));
                Assert.That(
                    secondTick.Trace.Text,
                    Does.Contain("EnemyChargeStartDeferred"),
                    BuildChargeKinematicDebug(2, worldState, secondTick));
                Assert.That(
                    GetEntityAfterTick(secondTick, 40).aiMode,
                    Is.Not.EqualTo(EnemyAiMode.Charge),
                    BuildChargeKinematicDebug(2, worldState, secondTick));
                Assert.That(
                    secondTick.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40),
                    Is.False,
                    BuildChargeKinematicDebug(2, worldState, secondTick));

                var thirdTick = pipeline.RunTick(new TickInput(3));
                Assert.That(
                    thirdTick.Trace.Text,
                    Does.Contain("Reason=ChargeStart"),
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    GetEntityAfterTick(thirdTick, 40).aiMode,
                    Is.EqualTo(EnemyAiMode.Charge),
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    GetEntityAfterTick(thirdTick, 40).position.PlanarPosition,
                    Is.EqualTo(new Vector2Int(1, 0)),
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    thirdTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40),
                    Is.False,
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    thirdTick.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40),
                    Is.False,
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                LegacyMovementBoundaryAssert.RequiresExplicitLegacyFallbackBaseline(thirdTick, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_KinematicFlag_PatrolToChargeWaitsForOrdinarySettleThenUsesChargeKinematicMove()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 2, chargeStepCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled,
                    new PlayerKinematicLocomotionTimingSettings
                    {
                        KinematicMoveDurationSeconds = 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

                var firstTick = pipeline.RunTick(new TickInput(1));

                Assert.That(
                    firstTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40),
                    Is.True,
                    BuildChargeKinematicDebug(1, worldState, firstTick));

                var secondTick = pipeline.RunTick(new TickInput(2));
                Assert.That(
                    secondTick.Trace.Text,
                    Does.Contain("EnemyChargeStartDeferred"),
                    BuildChargeKinematicDebug(2, worldState, secondTick));
                Assert.That(
                    GetEntityAfterTick(secondTick, 40).aiMode,
                    Is.Not.EqualTo(EnemyAiMode.Charge),
                    BuildChargeKinematicDebug(2, worldState, secondTick));
                Assert.That(
                    secondTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Charge),
                    Is.False,
                    BuildChargeKinematicDebug(2, worldState, secondTick));
                Assert.That(
                    secondTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Voluntary),
                    Is.False,
                    BuildChargeKinematicDebug(2, worldState, secondTick));
                Assert.That(
                    secondTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Settled),
                    Is.True,
                    BuildChargeKinematicDebug(2, worldState, secondTick));

                var thirdTick = pipeline.RunTick(new TickInput(3));
                Assert.That(
                    thirdTick.Trace.Text,
                    Does.Contain("Reason=ChargeStart"),
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    GetEntityAfterTick(thirdTick, 40).aiMode,
                    Is.EqualTo(EnemyAiMode.Charge),
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    GetEntityAfterTick(thirdTick, 40).position.PlanarPosition,
                    Is.EqualTo(new Vector2Int(2, 0)),
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    thirdTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Charge),
                    Is.True,
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    thirdTick.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40),
                    Is.False,
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_SettleWait_DuringOrdinaryKinematic_DoesNotSnap()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 4, chargeStepCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled,
                    new PlayerKinematicLocomotionTimingSettings
                    {
                        KinematicMoveDurationSeconds = 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

                pipeline.RunTick(new TickInput(1));
                var deferredTick = pipeline.RunTick(new TickInput(2));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(GetEntityAfterTick(deferredTick, 40).aiMode, Is.Not.EqualTo(EnemyAiMode.Charge), BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(deferredTick.Trace.Text, Does.Contain("EnemyChargeStartDeferred"), BuildChargeKinematicDebug(2, worldState, deferredTick));
                if (snapshot.TryGetEnemyChargeState(40, out var chargeState))
                {
                    Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.None), BuildChargeKinematicDebug(2, worldState, deferredTick));
                }

                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True, BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary), BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(state.localOffset.IsZero, Is.False, BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(
                    deferredTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Voluntary &&
                        !track.DestinationLocalOffset.IsZero),
                    Is.True,
                    BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(deferredTick.PresentationData.EnemyChargeSignals.Any(signal => signal.EntityId == 40), Is.False);
                Assert.That(deferredTick.Trace.Text, Does.Not.Contain("EnemyChargeOrdinaryKinematicResidueCleared"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_SettleWait_StartsAfterOrdinarySettled_WhenStillValid()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 2, chargeStepCooldownTicks: 3);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled,
                    new PlayerKinematicLocomotionTimingSettings
                    {
                        KinematicMoveDurationSeconds = 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

                pipeline.RunTick(new TickInput(1));
                var deferredTick = pipeline.RunTick(new TickInput(2));
                var activeTick = pipeline.RunTick(new TickInput(3));

                Assert.That(deferredTick.Trace.Text, Does.Contain("EnemyChargeStartDeferred"), BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(deferredTick.PresentationData.EnemyChargeSignals.Any(signal => signal.EntityId == 40), Is.False);
                Assert.That(activeTick.Trace.Text, Does.Contain("Reason=ChargeStart"), BuildChargeKinematicDebug(3, worldState, activeTick));
                Assert.That(GetEntityAfterTick(activeTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge), BuildChargeKinematicDebug(3, worldState, activeTick));
                Assert.That(
                    activeTick.PresentationData.EnemyChargeSignals.Any(signal =>
                        signal.EntityId == 40 &&
                        signal.Phase == EnemyChargePhase.Active),
                    Is.True,
                    BuildChargeKinematicDebug(3, worldState, activeTick));
                Assert.That(
                    activeTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Charge),
                    Is.True,
                    BuildChargeKinematicDebug(3, worldState, activeTick));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_SettleWait_DoesNotStart_WhenTargetInvalidAfterSettled()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 1)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 2, chargeStepCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled,
                    new PlayerKinematicLocomotionTimingSettings
                    {
                        KinematicMoveDurationSeconds = 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

                pipeline.RunTick(new TickInput(1));
                var deferredTick = pipeline.RunTick(new TickInput(2));
                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 5, 1));
                var revalidatedTick = pipeline.RunTick(new TickInput(3));

                Assert.That(deferredTick.Trace.Text, Does.Contain("EnemyChargeStartDeferred"), BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(GetEntityAfterTick(revalidatedTick, 40).aiMode, Is.Not.EqualTo(EnemyAiMode.Charge), BuildChargeKinematicDebug(3, worldState, revalidatedTick));
                Assert.That(revalidatedTick.Trace.Text, Does.Not.Contain("Reason=ChargeStart"), BuildChargeKinematicDebug(3, worldState, revalidatedTick));
                Assert.That(revalidatedTick.PresentationData.EnemyChargeSignals.Any(signal => signal.EntityId == 40), Is.False);
                if (worldState.CreateSnapshot().TryGetEnemyChargeState(40, out var chargeState))
                {
                    Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.None), BuildChargeKinematicDebug(3, worldState, revalidatedTick));
                }
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_SettleWait_DoesNotClearVoluntaryKinematicResidue()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 4, chargeStepCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled,
                    new PlayerKinematicLocomotionTimingSettings
                    {
                        KinematicMoveDurationSeconds = 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

                pipeline.RunTick(new TickInput(1));
                var deferredTick = pipeline.RunTick(new TickInput(2));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(deferredTick.Trace.Text, Does.Contain("EnemyChargeStartDeferred"), BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(deferredTick.Trace.Text, Does.Not.Contain("EnemyChargeOrdinaryKinematicResidueCleared"), BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(deferredTick.EventLog.Any(entry => entry.Contains("EnemyChargeOrdinaryKinematicResidueCleared", StringComparison.Ordinal)), Is.False);
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True, BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary), BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(state.IsSettledZero, Is.False, BuildChargeKinematicDebug(2, worldState, deferredTick));
                Assert.That(state.localOffset.IsZero, Is.False, BuildChargeKinematicDebug(2, worldState, deferredTick));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_AlreadyActiveChargeKinematic_Continues()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, chargeStepCooldownTicks: 4);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 2,
                });
            writeContext.SetUnitKinematicState(
                40,
                new UnitKinematicRuntimeState
                {
                    localOffset = new KinematicOffset2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                    velocity = new KinematicVelocity2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                    mode = MotionMode.Charge,
                    remainingDistanceUnits = 3072,
                    remainingTicks = 3,
                    speedScalePermille = 1000,
                    sequenceId = 1,
                    elapsedTicks = 1,
                    totalTicks = 4,
                    commitTick = 2,
                    startedTick = 1,
                    stepDirectionX = 1,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(2));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(result.Trace.Text, Does.Not.Contain("EnemyChargeStartDeferred"), BuildChargeKinematicDebug(2, worldState, result));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out var state), Is.True, BuildChargeKinematicDebug(2, worldState, result));
                Assert.That(state.mode, Is.EqualTo(MotionMode.Charge), BuildChargeKinematicDebug(2, worldState, result));
                Assert.That(state.elapsedTicks, Is.EqualTo(2), BuildChargeKinematicDebug(2, worldState, result));
                Assert.That(GetEntityAfterTick(result, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)), BuildChargeKinematicDebug(2, worldState, result));
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Charge),
                    Is.True,
                    BuildChargeKinematicDebug(2, worldState, result));
                Assert.That(
                    result.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40),
                    Is.False,
                    BuildChargeKinematicDebug(2, worldState, result));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void ChargeActive_UnitOccupiedCell_DoesNotStopAsTraversalBlocker()
        {
            AssertActiveChargeAdvancesThroughUnitOccupiedCell(occupantEntityId: 30, occupantTeamId: 3);
        }

        [Test]
        [Category("Extended")]
        public void ChargeActive_SameTeamUnitOccupiedCell_DoesNotStopAsTraversalBlocker()
        {
            AssertActiveChargeAdvancesThroughUnitOccupiedCell(occupantEntityId: 41, occupantTeamId: 2);
        }

        [Test]
        [Category("Extended")]
        public void ChargeActive_PlayerUnitOccupiedCell_DoesNotStopAsTraversalBlocker()
        {
            AssertActiveChargeAdvancesThroughUnitOccupiedCell(occupantEntityId: 10, occupantTeamId: 1);
        }

        [Test]
        [Category("Extended")]
        public void ChargeActive_SolidOrTerrainStillStops()
        {
            AssertActiveChargeStopsBeforeHardBlocker(
                CreateWorldState(
                    new[]
                    {
                        CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                        CreateBox(entityId: 50, position: new Vector2Int(1, 0)),
                    },
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0))));
            AssertActiveChargeStopsBeforeHardBlocker(
                CreateWorldState(
                    new[]
                    {
                        CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                        CreateWall(entityId: 51, position: new Vector2Int(1, 0)),
                    },
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0))));
        }

        [Test]
        [Category("Extended")]
        public void ChargePassiveContact_StillUsesTargetSelection()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, includePassiveContact: true, chargeStepCooldownTicks: 0);
            SeedActiveCharge(worldState);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));

                Assert.That(GetEntityAfterTick(result, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.True, BuildChargeKinematicDebug(1, worldState, result));
                Assert.That(
                    result.MovementPhaseResult.RejectedReasons.Any(reason =>
                        reason.Contains("EnemyChargeKinematicTraversalBlocked", StringComparison.Ordinal)),
                    Is.False,
                    BuildChargeKinematicDebug(1, worldState, result));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_EnemyOrdinaryKinematicOff_NoDeferralNeeded()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(5, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 2, chargeStepCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);

                pipeline.RunTick(new TickInput(1));
                var chargeStartTick = pipeline.RunTick(new TickInput(2));

                Assert.That(chargeStartTick.Trace.Text, Does.Contain("Reason=ChargeStart"), BuildChargeKinematicDebug(2, worldState, chargeStartTick));
                Assert.That(chargeStartTick.Trace.Text, Does.Not.Contain("EnemyChargeStartDeferred"), BuildChargeKinematicDebug(2, worldState, chargeStartTick));
                Assert.That(GetEntityAfterTick(chargeStartTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge), BuildChargeKinematicDebug(2, worldState, chargeStartTick));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_NotMigrated()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 1,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0),
                    windupEndTick = 2,
                    landingTick = 3,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(result.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.False);
                Assert.That(snapshot.TryGetEnemyJumpState(40, out var jumpState), Is.True);
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupTopologySuspendDoesNotConsumeRemainingTicks()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 3, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Front));
            var profile = CreateJumpChaserProfile(windupTicks: 2, airborneTicks: 2, cooldownTicks: 1);
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = lockedTargetCell,
                    windupEndTick = 2,
                    landingTick = 4,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));

                var suspendedState = GetEnemyJumpState(worldState, 40);
                Assert.That(suspendedState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(suspendedState.windupEndTick, Is.EqualTo(5));
                Assert.That(suspendedState.landingTick, Is.EqualTo(7));

                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
                var resumedTick = pipeline.RunTick(new TickInput(4));
                var resumedState = GetEnemyJumpState(worldState, 40);

                Assert.That(resumedState.phase, Is.EqualTo(EnemyJumpPhase.Windup), resumedTick.Trace.Text);
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
                Assert.That(resumedTick.PresentationData.EnemyJumpSignals.Single().StartedAirborneThisTick, Is.False);

                var takeoffTick = pipeline.RunTick(new TickInput(5));
                var takeoffState = GetEnemyJumpState(worldState, 40);

                Assert.That(takeoffState.phase, Is.EqualTo(EnemyJumpPhase.Airborne), takeoffTick.Trace.Text);
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneTopologySuspendDoesNotReachLandingTick()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 3, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Front));
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
            writeContext.SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = lockedTargetCell,
                    windupEndTick = 1,
                    landingTick = 2,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var stillSuspendedTick = pipeline.RunTick(new TickInput(3));
                var suspendedState = GetEnemyJumpState(worldState, 40);

                Assert.That(suspendedState.phase, Is.EqualTo(EnemyJumpPhase.Airborne), stillSuspendedTick.Trace.Text);
                Assert.That(suspendedState.landingTick, Is.EqualTo(5));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(stillSuspendedTick.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.False);

                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
                var resumedTick = pipeline.RunTick(new TickInput(4));
                var resumedState = GetEnemyJumpState(worldState, 40);

                Assert.That(resumedState.phase, Is.EqualTo(EnemyJumpPhase.Airborne), resumedTick.Trace.Text);
                Assert.That(resumedTick.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpOffBottomHiddenStillDoesNotConsumePhaseTime()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var lockedTargetCell = new SurfaceCell(FaceId.Front, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 3, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));
            var profile = CreateJumpChaserProfile(windupTicks: 2, airborneTicks: 2, cooldownTicks: 1);
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = lockedTargetCell,
                    windupEndTick = 2,
                    landingTick = 4,
                });

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var suspendedState = GetEnemyJumpState(worldState, 40);

                Assert.That(suspendedState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(suspendedState.windupEndTick, Is.EqualTo(4));
                Assert.That(suspendedState.landingTick, Is.EqualTo(6));

                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
                var resumedTick = pipeline.RunTick(new TickInput(3));
                var resumedState = GetEnemyJumpState(worldState, 40);

                Assert.That(resumedState.phase, Is.EqualTo(EnemyJumpPhase.Windup), resumedTick.Trace.Text);
                Assert.That(resumedTick.PresentationData.EnemyJumpSignals.Single().StartedAirborneThisTick, Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void PassiveContact_CommitTickOnly()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                TickResult result = null;

                for (var tick = 1; tick <= 9; tick++)
                {
                    result = pipeline.RunTick(new TickInput(tick));
                    Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.False);
                }

                result = pipeline.RunTick(new TickInput(10));
                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(playerCell));
                Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.True);
                Assert.That(
                    result.MovementPhaseResult.CommitEvents.Any(entry =>
                        entry.Contains("KinematicAnchorCommitted", StringComparison.Ordinal) &&
                        entry.Contains("E=40", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    result.EventLog.Any(entry =>
                        entry.Contains("DamageCommitted", StringComparison.Ordinal) &&
                        entry.Contains("SourceKind=PassiveContact", StringComparison.Ordinal) &&
                        entry.Contains("Target=10", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathDuringKinematicMove()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 1, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled,
                    new ScriptedAttackLogic(10, 40));
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(40, out _), Is.False);
                Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(result.EventLog.Any(entry => entry.Contains("CleanupRemoved|E=40", StringComparison.Ordinal)), Is.True);
                Assert.That(result.EventLog.Any(entry => entry.Contains("KinematicPoseRemoved|E=40", StringComparison.Ordinal)), Is.True);
                Assert.That(result.EventLog.Any(entry => entry.Contains("PlayerRespawnDelayStarted|E=40", StringComparison.Ordinal)), Is.False);
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.EntityType == EntityType.Unit &&
                        track.TerminalKind == TickKinematicMotionTerminalKind.Removed),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PassiveContactProfile_AlreadySharingPlayerCell_DealsDamageWithoutMoving()
        {
            var stackedCell = new Vector2Int(0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: stackedCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: stackedCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshotAfter = worldState.CreateSnapshot();
                var stackedUnits = new List<EntityState>();
                var enemy = GetEntity(worldState, 40);
                var player = GetEntity(worldState, 10);

                Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(
                    result.AttackPhaseResult.DamageResolutions.Any(
                        record => record.Accepted &&
                                  record.SourceId == 40 &&
                                  record.TargetId == 10),
                    Is.True);
                Assert.That(result.AttackPhaseResult.DamageResolutions.Count(record => record.Accepted), Is.EqualTo(1));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(stackedCell));
                Assert.That(player.hp, Is.EqualTo(2));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(enemy.aiStateTimer, Is.EqualTo(0));
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));

                snapshotAfter.EnumerateUnitsAt(stackedCell, stackedUnits);
                CollectionAssert.AreEqual(new[] { 10, 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpChaserProfile_WithPassiveContact_SameCellDealsDamage()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1, includePassiveContact: true);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var result = pipeline.RunTick(new TickInput(1));
                var enemy = GetEntity(worldState, 40);

                Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(result.EventLog, Has.None.Contains("MoveCommitted|"));
                Assert.That(result.AttackPhaseResult.DamageResolutions.Select(record => record.SourceKind).ToArray(), Is.EqualTo(new[] { AttackSourceKind.PassiveContact }));
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(4));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Left));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_NonAttackingProfile_WithPassiveContact_SameCellDealsDamage()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateNonAttackingEnemyProfile(includePassiveContact: true);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var result = pipeline.RunTick(new TickInput(1));

                Assert.That(result.AttackPhaseResult.DamageResolutions.Select(record => record.SourceKind).ToArray(), Is.EqualTo(new[] { AttackSourceKind.PassiveContact }));
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(4));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_WallFollowerProfile_WithPassiveContact_SameCellDealsDamage()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            });
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Left, includePassiveContact: true);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var result = pipeline.RunTick(new TickInput(1));
                var enemy = GetEntity(worldState, 40);

                Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(result.EventLog, Has.None.Contains("MoveCommitted|"));
                Assert.That(result.AttackPhaseResult.DamageResolutions.Select(record => record.SourceKind).ToArray(), Is.EqualTo(new[] { AttackSourceKind.PassiveContact }));
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(result.AttackPhaseResult.DamageResolutions.Single().SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(4));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Left));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PassiveContactProfile_PlayerOwnedCooldownWhileStacked_OnlyAcceptsAtReceiverCadence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);

                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));
                var fourthTick = pipeline.RunTick(new TickInput(4));

                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
                Assert.That(firstTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.False);
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions.Single().RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
                Assert.That(thirdTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(fourthTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.False);
                Assert.That(firstTick.PresentationData.PlayerDamageSignals.Count, Is.EqualTo(1));
                Assert.That(secondTick.PresentationData.PlayerDamageSignals, Is.Empty);
                Assert.That(thirdTick.PresentationData.PlayerDamageSignals.Count, Is.EqualTo(1));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PassiveContactProfile_TwoEnemiesSameCellSameTick_OnlyFirstDeterministicHitIsAccepted()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var result = pipeline.RunTick(new TickInput(1));
                var accepted = result.AttackPhaseResult.DamageResolutions.Where(record => record.Accepted).ToArray();
                var rejected = result.AttackPhaseResult.DamageResolutions.Where(record => !record.Accepted).ToArray();

                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(4));
                Assert.That(accepted.Length, Is.EqualTo(1));
                Assert.That(rejected.Length, Is.EqualTo(1));
                Assert.That(accepted[0].SourceId, Is.EqualTo(40));
                Assert.That(accepted[0].SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(rejected[0].SourceId, Is.EqualTo(50));
                Assert.That(rejected[0].SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(rejected[0].RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
                Assert.That(result.PresentationData.PlayerDamageSignals.Count, Is.EqualTo(1));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PassiveContactProfile_CooldownExpiryWhileStillStacked_ReacceptsExactlyOneHit()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreatePassiveContactProfile();

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);

                pipeline.RunTick(new TickInput(1));
                var cooldownTick = pipeline.RunTick(new TickInput(2));
                var expiryTick = pipeline.RunTick(new TickInput(3));

                Assert.That(cooldownTick.AttackPhaseResult.DamageResolutions.Count(record => record.Accepted), Is.EqualTo(0));
                Assert.That(expiryTick.AttackPhaseResult.DamageResolutions.Count(record => record.Accepted), Is.EqualTo(1));
                Assert.That(expiryTick.AttackPhaseResult.DamageResolutions.Count(record => !record.Accepted), Is.EqualTo(1));
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PassiveContactProfile_RecoverTicks_DoNotControlContactCadence()
        {
            var fastContactProfile = CreatePassiveContactProfile(recoverTicks: 0);
            var slowContactProfile = CreatePassiveContactProfile(recoverTicks: 5);

            try
            {
                var contactFastWorld = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                });
                var contactSlowWorld = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                });
                var fastContactPipeline = CreateEnemyPipeline(contactFastWorld, fastContactProfile, playerDamageCooldownTicks: 1);
                var slowContactPipeline = CreateEnemyPipeline(contactSlowWorld, slowContactProfile, playerDamageCooldownTicks: 1);

                for (var tick = 1; tick <= 3; tick++)
                {
                    fastContactPipeline.RunTick(new TickInput(tick));
                    slowContactPipeline.RunTick(new TickInput(tick));
                }

                Assert.That(GetEntity(contactFastWorld, 10).hp, Is.EqualTo(GetEntity(contactSlowWorld, 10).hp));
            }
            finally
            {
                DestroyProfile(fastContactProfile);
                DestroyProfile(slowContactProfile);
            }
        }



        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_DifferentFaceTarget_DoesNotStartCharge()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 2, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));

                Assert.That(GetEntityAfterTick(firstTick, 40).aiMode, Is.Not.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(secondTick, 40).aiMode, Is.Not.EqualTo(EnemyAiMode.Charge));
                Assert.That(firstTick.Trace.Text, Does.Not.Contain("Reason=ChargeStart"));
                Assert.That(secondTick.Trace.Text, Does.Not.Contain("Reason=ChargeStart"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_DiagonalTarget_DoesNotStartCharge()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 1), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 2)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));

                Assert.That(GetEntityAfterTick(firstTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(GetEntityAfterTick(secondTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(firstTick.Trace.Text, Does.Not.Contain("Reason=ChargeStart"));
                Assert.That(secondTick.Trace.Text, Does.Not.Contain("Reason=ChargeStart"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_BlockedFirstStep_DoesNotStartCharge()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(2, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));

                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(secondTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(secondTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(secondTick.Trace.Text, Does.Not.Contain("Reason=ChargeStart"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }








        [Test]
        [Category("Full")]
        public void EnemyAi_WallFollowerProfile_WithLocomotionCooldown_PreservesWallFollowRule()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Left, moveCooldownTicks: 2);
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));
                var fourthTick = pipeline.RunTick(new TickInput(4));

                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(firstTick, 40).enemyLocomotionCooldownTicks, Is.EqualTo(2));
                Assert.That(GetEntityAfterTick(firstTick, 40).facing, Is.EqualTo(Direction.Right));
                Assert.That(secondTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(GetEntityAfterTick(secondTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(secondTick, 40).facing, Is.EqualTo(Direction.Right));
                Assert.That(GetEntityAfterTick(thirdTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 1)));
                Assert.That(GetEntityAfterTick(thirdTick, 40).facing, Is.EqualTo(Direction.Up));
                Assert.That(fourthTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(GetEntityAfterTick(fourthTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 1)));
                Assert.That(GetEntityAfterTick(fourthTick, 40).facing, Is.EqualTo(Direction.Up));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_AllDirectionsBlocked_RotatesInPlaceWithoutMovementIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 2)),
                CreateWall(entityId: 91, position: new Vector2Int(2, 1)),
                CreateWall(entityId: 92, position: new Vector2Int(0, 1)),
                CreateWall(entityId: 93, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Right);
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));

                Assert.That(firstTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(GetEntityAfterTick(firstTick, 40).facing, Is.EqualTo(Direction.Right));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultEntityLogicProvider_WallFollowerProfile_ForwardBlocked_TurnsAndMovesInSameTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Left);
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var result = pipeline.RunTick(new TickInput(1));
                var enemy = GetEntity(worldState, 40);

                Assert.That(result.MovementPhaseResult.SortedIntents.Select(intent => intent.SourceId).ToArray(), Is.EqualTo(new[] { 40 }));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, -1)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Down));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyEntityLogicFactory_RoleEnemyWithNoneAiMode_DoesNotCreateLogicOrAdvanceState()
        {
            var passiveTutorialEnemy = CreateUnit(
                entityId: 40,
                teamId: 2,
                position: new Vector2Int(0, 0),
                aiMode: EnemyAiMode.None,
                facing: Direction.Right,
                aiStateTimer: 2,
                enemyLocomotionCooldownTicks: 3);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                passiveTutorialEnemy,
            });
            var factory = new EnemyEntityLogicFactory();
            var provider = GameplayEntityLogicProviderFactory.CreateDefault();
            var logicSet = provider.Build(worldState.CreateSnapshot(), Array.Empty<IEntityLogic>());
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 7,
                    lockedTargetEntityId = 10,
                    direction = Direction.Right,
                    startTick = 1,
                    executeTick = 2,
                });

            Assert.That(factory.CanCreate(new EntityLogicCreationContext(worldState.CreateSnapshot(), passiveTutorialEnemy)), Is.False);
            Assert.That(EnemyParticipationPolicy.IsEnemyLogicEntity(passiveTutorialEnemy), Is.False);
            Assert.That(logicSet.AiStateLogics, Is.Empty);
            Assert.That(logicSet.PreMovementStateLogics, Is.Empty);
            Assert.That(logicSet.MovementLogics, Is.Empty);
            Assert.That(logicSet.EnemyActionStateLogics, Is.Empty);
            Assert.That(logicSet.AttackLogics, Is.Empty);

            var pipeline = CreateEnemyPipeline(worldState);
            var result = pipeline.RunTick(new TickInput(1));
            var enemy = GetEntity(worldState, 40);

            Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.None));
            Assert.That(enemy.aiStateTimer, Is.EqualTo(2));
            Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(3));
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(40, out var actionState), Is.True);
            Assert.That(actionState.sequence, Is.EqualTo(7));
            Assert.That(actionState.lockedTargetEntityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLocomotionCooldown_Authority_ComesFromEnemyAiProfileRuntimeDefinitionAndEntityState()
        {
            var profile = CreateNonAttackingEnemyProfile(moveCooldownTicks: 2);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = CreateEnemyPipeline(worldState, profile);

            try
            {
                var runtimeDefinition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var enemyAfterSecondTick = GetEntity(worldState, 40);

                Assert.That(runtimeDefinition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(2));
                Assert.That(firstTick.MovementPhaseResult.SortedIntents.Select(intent => intent.SourceId).ToArray(), Is.EqualTo(new[] { 40 }));
                Assert.That(GetEntityAfterTick(firstTick, 40).enemyLocomotionCooldownTicks, Is.EqualTo(2));
                Assert.That(secondTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(enemyAfterSecondTick.enemyLocomotionCooldownTicks, Is.EqualTo(1));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32)),
                topology);
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static Vector2Int GetEntityPosition(WorldState worldState, int entityId)
        {
            return GetEntity(worldState, entityId).position.PlanarPosition;
        }

        private static int GetEntityHp(WorldState worldState, int entityId)
        {
            return GetEntity(worldState, entityId).hp;
        }

        private static EntityState GetEntityAfterTick(TickResult tickResult, int entityId)
        {
            return tickResult.FinalEntities.Single(entity => entity.entityId == entityId);
        }

        private static WorldState CreateSuppressedRandomWalkFacingWorld(bool includeBox = true)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var initialEntities = new List<EntityState>
            {
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            };
            if (includeBox)
            {
                initialEntities.Add(CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 4, 1), capabilities: BoxCapabilities.Push));
            }

            var worldState = CreateWorldState(
                initialEntities,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(5, 1)));
            worldState.CreateWriteContext().SetEnemyPatrolState(
                40,
                new EnemyPatrolRuntimeState
                {
                    sequence = 1,
                    homeCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lastCommittedDirection = Direction.Right,
                });
            return worldState;
        }

        private static void AssertSuppressedFacingTick(
            WorldState worldState,
            TickResult tick,
            Direction expectedFacing)
        {
            var enemy = GetEntityAfterTick(tick, 40);
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == 40), Is.Empty);
            Assert.That(tick.EventLog, Has.None.Contains("MoveCommitted|"));
            Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
            Assert.That(enemy.facing, Is.EqualTo(expectedFacing));
            Assert.That(GetEntity(worldState, 40).facing, Is.EqualTo(expectedFacing));
        }

        private static string SummarizeEnemyTick(TickResult tickResult, int entityId)
        {
            var entity = GetEntityAfterTick(tickResult, entityId);
            var hasMoveIntent = tickResult.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == entityId);
            var hasAttackIntent = tickResult.AttackPhaseResult.RawIntents.Any(intent => intent.SourceId == entityId);
            var reasons = ExtractEnemyTransitionReasons(tickResult.Trace.Text, entityId);

            return $"Pos={entity.position}|Facing={entity.facing}|Mode={entity.aiMode}|Timer={entity.aiStateTimer}|Move={hasMoveIntent}|Attack={hasAttackIntent}|Reasons={reasons}";
        }

        private static string ExtractEnemyTransitionReasons(string traceText, int entityId)
        {
            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var reasons = new List<string>();
            var prefix = $"EnemyAiTransition|Stage=";
            var entityToken = $"|E={entityId}|";

            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(prefix, StringComparison.Ordinal) ||
                    !lines[i].Contains(entityToken, StringComparison.Ordinal))
                {
                    continue;
                }

                var reasonIndex = lines[i].IndexOf("|Reason=", StringComparison.Ordinal);
                if (reasonIndex < 0)
                {
                    continue;
                }

                var reasonStart = reasonIndex + "|Reason=".Length;
                var reasonEnd = lines[i].IndexOf("|Facing=", reasonStart, StringComparison.Ordinal);
                reasons.Add(reasonEnd >= 0
                    ? lines[i].Substring(reasonStart, reasonEnd - reasonStart)
                    : lines[i].Substring(reasonStart));
            }

            return string.Join(",", reasons);
        }

        private static int GetPlanarDistance(SurfaceCell source, SurfaceCell target)
        {
            var sourcePlanar = source.PlanarPosition;
            var targetPlanar = target.PlanarPosition;
            return Math.Abs(targetPlanar.x - sourcePlanar.x) + Math.Abs(targetPlanar.y - sourcePlanar.y);
        }

        private static EnemyActionRuntimeState GetEnemyActionState(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(entityId, out var actionState), Is.True);
            return actionState;
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

        private static void SetUnitContinuousLocomotionState(
            WorldState worldState,
            int entityId,
            int localX,
            int localY,
            int velocityX = 0,
            int velocityY = 0,
            ContinuousLocomotionMode mode = ContinuousLocomotionMode.Idle)
        {
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                entityId,
                new UnitContinuousLocomotionState
                {
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(localX),
                        KinematicFixed.FromRaw(localY)),
                    velocity = new KinematicVelocity2(
                        KinematicFixed.FromRaw(velocityX),
                        KinematicFixed.FromRaw(velocityY)),
                    mode = mode,
                    facing = Direction.Right,
                    speedUnitsPerTick = Math.Max(Math.Abs(velocityX), Math.Abs(velocityY)),
                    sequenceId = 1,
                });
        }

        private static string BuildWindupStartGateDebug(
            bool logicRange,
            in CombatWindupStartQueryResult query,
            TickResult result)
        {
            var movementSources = string.Join(
                ",",
                result.MovementPhaseResult.RawIntents.Select(intent => $"{intent.SourceId}:{intent.Destination}"));
            var actionSignals = string.Join(
                ",",
                result.PresentationData.EnemyActionSignals.Select(signal => $"{signal.EntityId}:Start={signal.StartedThisTick}:Execute={signal.ExecutedThisTick}"));

            return $"LogicRange={logicRange}|CanStart={query.CanStart}|ShouldApproach={query.ShouldApproach}|Reason={query.BlockReason}|Distance={query.DistanceFixedUnits}|Threshold={query.ThresholdFixedUnits}|Slack={ProjectileWindupSettings.CreateDefault().VisualRangeSlackUnits}|Movement=[{movementSources}]|Signals=[{actionSignals}]|Trace={result.Trace.Text}";
        }

        private static void SetUnitKinematicLocomotionState(
            WorldState worldState,
            int entityId,
            int localX,
            int localY,
            int stepDirectionX,
            int stepDirectionY)
        {
            worldState.CreateWriteContext().SetUnitKinematicState(
                entityId,
                new UnitKinematicRuntimeState
                {
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(localX),
                        KinematicFixed.FromRaw(localY)),
                    velocity = new KinematicVelocity2(
                        KinematicFixed.FromRaw(stepDirectionX * KinematicFixed.UnitsPerCell / 4),
                        KinematicFixed.FromRaw(stepDirectionY * KinematicFixed.UnitsPerCell / 4)),
                    mode = MotionMode.Voluntary,
                    remainingDistanceUnits = KinematicFixed.UnitsPerCell - Math.Abs(localX) - Math.Abs(localY),
                    remainingTicks = 3,
                    speedScalePermille = 1000,
                    sequenceId = 1,
                    elapsedTicks = 1,
                    totalTicks = 4,
                    commitTick = 4,
                    startedTick = 1,
                    stepDirectionX = stepDirectionX,
                    stepDirectionY = stepDirectionY,
                });
        }

        private static EnemyAiProfile CreateEnemyProfile(
            int windupTicks,
            int moveCooldownTicks = 0,
            int recoverTicks = 1)
        {
            return EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(
                windupTicks: windupTicks,
                recoverTicks: recoverTicks);
        }

        private static EnemyAiProfile CreateBehaviorSummonProfile(
            int initialDelayTicks,
            int cooldownTicks,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            EnemyUnitArchetypeAsset summonedArchetype = null,
            bool overrideHp = false,
            int hpOverride = 1,
            int windupTicks = 1,
            bool suppressMovementDuringWindup = false,
            int recoveryTicks = 0,
            bool suppressMovementDuringRecover = false,
            PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Stationary,
            PatrolSettings patrolSettings = default)
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = patrolStrategyKind,
                PatrolSettings = patrolSettings.Equals(default(PatrolSettings))
                    ? PatrolSettings.CreateDefault()
                    : patrolSettings,
            });
            var module = CreateSummonBehaviorModule(
                initialDelayTicks,
                cooldownTicks,
                spawnCountPerTrigger,
                maxAliveChildren,
                summonedArchetype ?? GetSharedSummonedArchetype(),
                overrideHp,
                hpOverride,
                windupTicks,
                suppressMovementDuringWindup,
                recoveryTicks,
                suppressMovementDuringRecover);
            EnemyAiProfileTestFactory.SetSerializedField(
                profile,
                "behaviorModuleAssets",
                new List<EnemyBehaviorModuleAsset> { module });
            return profile;
        }

        private static EnemySummonBehaviorModuleAsset CreateSummonBehaviorModule(
            int initialDelayTicks,
            int cooldownTicks,
            int spawnCountPerTrigger,
            int maxAliveChildren,
            EnemyUnitArchetypeAsset summonedArchetype,
            bool overrideHp,
            int hpOverride,
            int windupTicks,
            bool suppressMovementDuringWindup,
            int recoveryTicks,
            bool suppressMovementDuringRecover)
        {
            var summon = new EnemySummonAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(summon, "spawnCountPerTrigger", spawnCountPerTrigger);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "maxAliveChildren", maxAliveChildren);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "candidatePattern", SummonCandidatePattern.OrthogonalAdjacent4);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoUnitAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoSolidAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "summonedArchetype", summonedArchetype);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "overrideHp", overrideHp);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "hpOverride", hpOverride);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "windupSeconds", TicksToSeconds(windupTicks));
            EnemyAiProfileTestFactory.SetSerializedField(summon, "suppressMovementDuringWindup", suppressMovementDuringWindup);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "recoverySeconds", TicksToSeconds(recoveryTicks));
            EnemyAiProfileTestFactory.SetSerializedField(summon, "suppressMovementDuringRecover", suppressMovementDuringRecover);

            var module = ScriptableObject.CreateInstance<EnemySummonBehaviorModuleAsset>();
            module.name = "Test_EnemySummonBehaviorModule";
            module.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(module, "initialDelaySeconds", TicksToSeconds(initialDelayTicks));
            EnemyAiProfileTestFactory.SetSerializedField(module, "cooldownSeconds", TicksToSeconds(cooldownTicks));
            EnemyAiProfileTestFactory.SetSerializedField(module, "summon", summon);
            return module;
        }

        private static EnemyAiProfile CreateUtilityGravityFieldAuraProfile(
            int initialDelayTicks,
            int cooldownTicks,
            int radius,
            int windupTicks,
            int durationTicks,
            int recoverTicks = 1,
            bool blocksPush = true,
            bool blocksFlip = true,
            bool blocksDestroy = true,
            bool suppressMovementDuringWindup = true,
            bool suppressMovementDuringRecover = true)
        {
            return CreateUtilityProfile(
                CreateGravityFieldAuraUtilityEffect(
                    initialDelayTicks,
                    cooldownTicks,
                    radius,
                    windupTicks,
                    durationTicks,
                    recoverTicks,
                    blocksPush,
                    blocksFlip,
                    blocksDestroy,
                    suppressMovementDuringWindup,
                    suppressMovementDuringRecover));
        }

        private static EnemyAiProfile CreateUtilityProfile(params EnemyUtilityEffectAuthoring[] effects)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                UtilityEffects = effects,
            });
        }

        private static EnemyAiProfile CreateMovingUtilityProfile(params EnemyUtilityEffectAuthoring[] effects)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
                UtilityEffects = effects,
            });
        }

        private static EnemyAiProfile CreateRandomWalkUtilityProfile(params EnemyUtilityEffectAuthoring[] effects)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.RandomWalk,
                PatrolSettings = PatrolSettings.CreateDefaultRandomWalk(),
                UtilityEffects = effects,
            });
        }

        private static EnemyUtilityEffectAuthoring CreateGravityFieldAuraUtilityEffect(
            int initialDelayTicks,
            int cooldownTicks,
            int radius,
            int windupTicks,
            int durationTicks,
            int recoverTicks,
            bool blocksPush,
            bool blocksFlip,
            bool blocksDestroy,
            bool suppressMovementDuringWindup,
            bool suppressMovementDuringRecover)
        {
            var gravityFieldAura = new EnemyGravityFieldAuraAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "radius", radius);
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "windupSeconds", TicksToSeconds(windupTicks));
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "fieldDurationSeconds", TicksToSeconds(durationTicks));
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "recoverSeconds", TicksToSeconds(recoverTicks));
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "blocksPush", blocksPush);
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "blocksFlip", blocksFlip);
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "blocksDestroy", blocksDestroy);
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "suppressMovementDuringWindup", suppressMovementDuringWindup);
            EnemyAiProfileTestFactory.SetSerializedField(gravityFieldAura, "suppressMovementDuringRecover", suppressMovementDuringRecover);

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.GravityFieldAura);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", TicksToSeconds(initialDelayTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", TicksToSeconds(cooldownTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "gravityFieldAura", gravityFieldAura);
            return effect;
        }

        private static void InvalidateEnemyGravityFieldAuraSource(
            WorldState worldState,
            EnemyGravityFieldAuraSourceInvalidation invalidation)
        {
            var writeContext = worldState.CreateWriteContext();
            switch (invalidation)
            {
                case EnemyGravityFieldAuraSourceInvalidation.RemoveSource:
                    writeContext.RemoveEntity(40);
                    break;
                case EnemyGravityFieldAuraSourceInvalidation.MarkSourceForDeath:
                    ((IAttackCommitContext)writeContext).MarkDestroy(40);
                    break;
                case EnemyGravityFieldAuraSourceInvalidation.KillSource:
                    writeContext.ApplyDamage(40, 99);
                    break;
                case EnemyGravityFieldAuraSourceInvalidation.DetachSource:
                    writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
                    break;
                case EnemyGravityFieldAuraSourceInvalidation.MoveSourceOffBottom:
                    writeContext.SetTopology(new CubeTopologyState(FaceId.Back));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidation), invalidation, null);
            }
        }

        private static IReadOnlyList<EntityState> CreateGravityFieldParityTargets()
        {
            var dead = CreateBox(entityId: 21, position: new Vector2Int(0, 1), capabilities: BoxCapabilities.Push);
            dead.hp = 0;
            var marked = CreateBox(entityId: 22, position: new Vector2Int(0, -1), capabilities: BoxCapabilities.Push);
            marked.markedForDeath = true;
            var detached = CreateBox(entityId: 23, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Push);
            detached.boardPresence = EntityBoardPresence.Detached;
            var sliding = CreateBox(
                entityId: 24,
                position: new Vector2Int(1, 1),
                capabilities: BoxCapabilities.Push,
                state: EntityPhaseState.Sliding);
            var otherFace = CreateBox(entityId: 25, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push);
            otherFace.position = new SurfaceCell(FaceId.Back, 1, 0);

            return new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                dead,
                marked,
                detached,
                sliding,
                otherFace,
            };
        }

        private static EntityState CreateStaticGravityFieldEmitter(int entityId, SurfaceCell position)
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
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push,
                boxArchetype = BoxArchetype.GravityField,
                gravityFieldPhase = GravityFieldPhase.Active,
                gravityFieldTimerTicks = 2,
            };
        }

        private static List<int> ActiveLockTargetIds(WorldSnapshot snapshot, int tickIndex, params int[] entityIds)
        {
            var result = new List<int>();
            for (var i = 0; i < entityIds.Length; i++)
            {
                if (snapshot.TryGetActiveBoxInteractionLockState(entityIds[i], tickIndex, out _))
                {
                    result.Add(entityIds[i]);
                }
            }

            return result;
        }

        private static float TicksToSeconds(int ticks)
        {
            return ticks / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        }

        private static PlayerLogic CreateImmediatePushPlayerLogic(int entityId, int recoveryTicks = 0)
        {
            return new PlayerLogic(
                entityId,
                pushWindupTicks: 0,
                pushRecoveryTicks: recoveryTicks,
                flipWindupTicks: 1,
                flipRecoveryTicks: 0);
        }

        private static PlayerLogic CreateImmediateFlipPlayerLogic(int entityId, int recoveryTicks = 0)
        {
            return new PlayerLogic(
                entityId,
                pushWindupTicks: 1,
                pushRecoveryTicks: 0,
                flipWindupTicks: 0,
                flipRecoveryTicks: recoveryTicks);
        }

        private static EnemyChargeRuntimeState GetEnemyChargeState(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyChargeState(entityId, out var chargeState), Is.True);
            return chargeState;
        }

        private static EnemyUtilityRuntimeState GetEnemyUtilityState(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyUtilityState(entityId, out var utilityState), Is.True);
            return utilityState;
        }

        private static EnemySummonBehaviorRuntimeState GetEnemySummonBehaviorState(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemySummonBehaviorState(entityId, out var summonState), Is.True);
            return summonState;
        }

        private static EnemyAiProfile CreateChargingEnemyProfile(
            int moveCooldownTicks,
            bool includePassiveContact = true,
            int windupTicks = 0,
            int recoverTicks = 0,
            int chargeStepCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateCharging(
                moveCooldownTicks,
                includePassiveContact,
                windupTicks,
                recoverTicks,
                chargeStepCooldownTicks);
        }

        private static EnemyAiProfile CreateNonAttackingEnemyProfile(int moveCooldownTicks = 0, bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.CreateNonAttacking(moveCooldownTicks, includePassiveContact);
        }

        private static EnemyAiProfile CreateNonAttackingEnemyProfile(
            float moveCooldownSeconds,
            float ordinaryKinematicMoveDurationSeconds,
            bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds,
                    ordinaryKinematicMoveDurationSeconds),
                PatrolStrategyKind = PatrolStrategyKind.RandomWalk,
                PatrolSettings = PatrolSettings.CreateDefaultRandomWalk(),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = includePassiveContact,
            });
        }

        private static EnemyAiProfile CreatePassiveContactProfile(int moveCooldownTicks = 0, int recoverTicks = 1)
        {
            return EnemyAiProfileTestFactory.CreatePassiveContact(moveCooldownTicks, recoverTicks);
        }

        private static EnemyAiProfile CreateStationaryPassiveContactProfile()
        {
            return EnemyAiProfileTestFactory.CreateStationaryPassiveContact();
        }

        private static EnemyAiProfile CreateForwardNonAttackingProfile()
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
            });
        }

        private static EnemyAiProfile CreateGlidePassiveContactProfile(int durationTicks)
        {
            return EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: durationTicks, recoveryTicks: 2, cooldownTicks: 0),
                includePassiveContact: true);
        }

        private static EnemyGlideRuntimeState CreateActiveGlide(
            int activeUntilTickExclusive,
            int durationTicks,
            int recoveryTicks,
            int cooldownTicks)
        {
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Active,
                sequence: 1,
                windupUntilTickExclusive: 0,
                activeUntilTickExclusive,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: 0,
                durationTicks,
                recoveryTicks,
                cooldownTicks,
                lastExitedTick: 0,
                hasLockedStep: true,
                lockedStepX: -1,
                lockedStepY: 0);
        }

        private static EnemyGlideRuntimeState CreateCooldownGlide(
            int cooldownUntilTickExclusive,
            int durationTicks,
            int recoveryTicks,
            int cooldownTicks)
        {
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Cooldown,
                sequence: 1,
                windupUntilTickExclusive: 0,
                activeUntilTickExclusive: 0,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive,
                windupTicks: 0,
                durationTicks,
                recoveryTicks,
                cooldownTicks,
                lastExitedTick: 0);
        }

        private static bool HasAcceptedPassiveContact(TickResult result, int sourceId, int targetId)
        {
            return result.AttackPhaseResult.DamageResolutions.Any(
                record => record.Accepted &&
                          record.SourceId == sourceId &&
                          record.TargetId == targetId &&
                          record.SourceKind == AttackSourceKind.PassiveContact);
        }

        private static bool HasMoveEntityTo(TickResult result, int entityId, SurfaceCell destination)
        {
            return result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MoveEntity &&
                operation.EntityId == entityId &&
                operation.Destination == destination);
        }

        private static string BuildContactTimingDebug(
            int tick,
            string mover,
            int moverId,
            int targetId,
            WorldSnapshot snapshot,
            TickResult result)
        {
            snapshot.TryGetEntity(10, out var player);
            snapshot.TryGetEntity(40, out var enemy);
            var hasPlayerKinematic = snapshot.TryGetUnitKinematicState(10, out var playerKinematic);
            var hasEnemyKinematic = snapshot.TryGetUnitKinematicState(40, out var enemyKinematic);
            var hasSameCellContact = player.entityId != 0 &&
                                     enemy.entityId != 0 &&
                                     player.position == enemy.position;
            var damageApplied = result.AttackPhaseResult.DamageResolutions.Any(record => record.Accepted);
            var presentationSource = ResolvePresentationSource(result, moverId, out var viewPose);

            return $"ContactTimingDebug|Tick={tick}|Mover={mover}|MoverId={moverId}|TargetId={targetId}" +
                   $"|MoverAnchor={(moverId == 10 ? player.position.ToString() : enemy.position.ToString())}" +
                   $"|MoverKinematicMode={(moverId == 10 && hasPlayerKinematic ? playerKinematic.mode.ToString() : moverId == 40 && hasEnemyKinematic ? enemyKinematic.mode.ToString() : "None")}" +
                   $"|MoverLocal={(moverId == 10 && hasPlayerKinematic ? $"{playerKinematic.localOffset.X.RawValue},{playerKinematic.localOffset.Y.RawValue}" : moverId == 40 && hasEnemyKinematic ? $"{enemyKinematic.localOffset.X.RawValue},{enemyKinematic.localOffset.Y.RawValue}" : "None")}" +
                   $"|EnemyAnchor={enemy.position}|PlayerAnchor={player.position}|HasSameCellContact={(hasSameCellContact ? 1 : 0)}" +
                   $"|DamageApplied={(damageApplied ? 1 : 0)}|ViewPose={viewPose}|PresentationSource={presentationSource}";
        }

        private static string BuildChargeKinematicDebug(int tick, WorldState worldState, TickResult result)
        {
            var snapshot = worldState.CreateSnapshot();
            snapshot.TryGetEntity(40, out var enemy);
            snapshot.TryGetUnitKinematicState(40, out var kinematicState);
            snapshot.TryGetEnemyChargeState(40, out var chargeState);
            var rawIntent = result.MovementPhaseResult.RawIntents.FirstOrDefault(intent => intent.SourceId == 40);
            var hasRawIntent = rawIntent.SourceId == 40;
            var sortedIntent = result.MovementPhaseResult.SortedIntents.FirstOrDefault(intent => intent.SourceId == 40);
            var hasSortedIntent = sortedIntent != null;
            var kinematicTrack = result.PresentationData.KinematicMotionTracks.FirstOrDefault(track => track.EntityId == 40);
            var legacyMotion = result.PresentationData.EntityMotions.FirstOrDefault(motion => motion.EntityId == 40);
            var damageApplied = result.AttackPhaseResult.DamageResolutions.Any(record => record.Accepted);
            var rejectedReasons = string.Join(";", result.MovementPhaseResult.RejectedReasons);
            var commitEvents = string.Join(";", result.MovementPhaseResult.CommitEvents);

            return $"ChargeDebug|Tick={tick}|Enemy=40|Pos={enemy.position}|Mode={enemy.aiMode}|AiTimer={enemy.aiStateTimer}" +
                   $"|LocomotionCooldown={enemy.enemyLocomotionCooldownTicks}" +
                   $"|HasKinematic={snapshot.TryGetUnitKinematicState(40, out _)}" +
                   $"|KinematicMode={kinematicState.mode}|KinematicSettled={kinematicState.IsSettledAtAnchor}" +
                   $"|KinematicElapsed={kinematicState.elapsedTicks}|KinematicTotal={kinematicState.totalTicks}|KinematicCommit={kinematicState.commitTick}" +
                   $"|KinematicOffset={kinematicState.localOffset.X.RawValue},{kinematicState.localOffset.Y.RawValue}" +
                   $"|ChargePhase={chargeState.phase}|ChargeSeq={chargeState.sequence}|ChargeDirection={chargeState.lockedDirection}" +
                   $"|WindupEnd={chargeState.windupEndTick}|ActiveSteps={chargeState.remainingActiveSteps}|RecoverTicks={chargeState.recoverRemainingTicks}" +
                   $"|RawIntent={(hasRawIntent ? 1 : 0)}|RawKind={(hasRawIntent ? rawIntent.CommandKind.ToString() : "None")}|RawDest={(hasRawIntent ? rawIntent.Destination.ToString() : "None")}|RawOrdinaryKinematicTicks={(hasRawIntent ? rawIntent.OrdinaryKinematicMoveTicks : 0)}" +
                   $"|SortedIntent={(hasSortedIntent ? 1 : 0)}|SortedKind={(hasSortedIntent ? sortedIntent.CommandKind.ToString() : "None")}|SortedDest={(hasSortedIntent ? sortedIntent.Destination.ToString() : "None")}|SortedOrdinaryKinematicTicks={(hasSortedIntent ? sortedIntent.OrdinaryKinematicMoveTicks : 0)}" +
                   $"|KinematicTrack={(kinematicTrack.EntityId == 40 ? 1 : 0)}|KinematicTrackMode={kinematicTrack.MotionMode}" +
                   $"|LegacyMotion={(legacyMotion.EntityId == 40 ? 1 : 0)}|LegacyMotionKind={legacyMotion.MotionKind}" +
                   $"|DamageApplied={(damageApplied ? 1 : 0)}|Rejected={rejectedReasons}|Commits={commitEvents}|Trace={result.Trace.Text}";
        }

        private static string ResolvePresentationSource(TickResult result, int entityId, out string viewPose)
        {
            var kinematicTrack = result.PresentationData.KinematicMotionTracks.FirstOrDefault(track => track.EntityId == entityId);
            if (kinematicTrack.EntityId == entityId)
            {
                viewPose = $"{kinematicTrack.SourceAnchorCell}->{kinematicTrack.DestinationAnchorCell}|Local={kinematicTrack.SourceLocalOffset}->{kinematicTrack.DestinationLocalOffset}";
                return "kinematic";
            }

            var legacyMotion = result.PresentationData.EntityMotions.FirstOrDefault(motion => motion.EntityId == entityId);
            if (legacyMotion.EntityId == entityId)
            {
                viewPose = $"{legacyMotion.SourceCell}->{legacyMotion.DestinationCell}";
                return "legacy TickEntityMotion";
            }

            viewPose = "committed";
            return "committed fallback";
        }

        private static int CountPatrolStateUpdates(string traceText, int entityId)
        {
            if (string.IsNullOrEmpty(traceText))
            {
                return 0;
            }

            return traceText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.Contains("EnemyPatrolStateUpdated|", StringComparison.Ordinal) &&
                               line.Contains($"|E={entityId}|", StringComparison.Ordinal));
        }

        private static void AssertActiveChargeAdvancesThroughUnitOccupiedCell(int occupantEntityId, int occupantTeamId)
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: occupantEntityId, teamId: occupantTeamId, position: new Vector2Int(1, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, includePassiveContact: false, chargeStepCooldownTicks: 0);
            SeedActiveCharge(worldState);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));

                Assert.That(GetEntityAfterTick(result, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)), BuildChargeKinematicDebug(1, worldState, result));
                Assert.That(
                    result.Trace.Text,
                    Does.Not.Contain("ChargeBlocked"),
                    BuildChargeKinematicDebug(1, worldState, result));
                Assert.That(
                    result.MovementPhaseResult.RejectedReasons.Any(reason =>
                        reason.Contains("EnemyChargeKinematicTraversalBlocked", StringComparison.Ordinal)),
                    Is.False,
                    BuildChargeKinematicDebug(1, worldState, result));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        private static void AssertActiveChargeStopsBeforeHardBlocker(WorldState worldState)
        {
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, includePassiveContact: false, chargeStepCooldownTicks: 0);
            SeedActiveCharge(worldState);

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled);
                var result = pipeline.RunTick(new TickInput(1));

                Assert.That(GetEntityAfterTick(result, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 0)), BuildChargeKinematicDebug(1, worldState, result));
                Assert.That(result.Trace.Text, Does.Contain("ChargeBlocked"), BuildChargeKinematicDebug(1, worldState, result));
                Assert.That(
                    result.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40),
                    Is.False,
                    BuildChargeKinematicDebug(1, worldState, result));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        private static void SeedActiveCharge(
            WorldState worldState,
            int entityId = 40,
            Direction direction = Direction.Right,
            int remainingActiveSteps = 2)
        {
            worldState.CreateWriteContext().SetEnemyChargeState(
                entityId,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = direction,
                    remainingActiveSteps = remainingActiveSteps,
                });
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            EnemyAiProfile profile,
            int playerDamageCooldownTicks)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = new PlayerControlTimingSettings
            {
                DamageCooldownSeconds = playerDamageCooldownTicks / (float)timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                playerTiming,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerKinematicLocomotionTiming: CreateOneTickKinematicTiming(timingProfile));
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            EnemyAiProfile profile,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            PlayerKinematicLocomotionTimingSnapshot playerKinematicLocomotionTiming,
            params IEntityLogic[] entityLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                entityLogics ?? Array.Empty<IEntityLogic>(),
                timingProfile,
                playerTiming,
                runtimeFeatureFlags: runtimeFeatureFlags,
                playerKinematicLocomotionTiming: playerKinematicLocomotionTiming);
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            EnemyAiProfile profile,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            params IEntityLogic[] entityLogics)
        {
            return CreateEnemyPipeline(
                worldState,
                profile,
                runtimeFeatureFlags,
                default,
                entityLogics);
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            EnemyAiRuntimeDefinition runtimeDefinition,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            params IEntityLogic[] entityLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);

            return new GameplayBootstrapper(
                    GameplayEntityLogicProviderFactory.CreateDefault(runtimeDefinition))
                .CreateTickPipeline(
                    worldState,
                    entityLogics ?? Array.Empty<IEntityLogic>(),
                    timingProfile,
                    playerTiming,
                    runtimeFeatureFlags: runtimeFeatureFlags);
        }

        private static TickResult RunPrimedSameCellCombatPassiveTick(EnemyAiProfile profile)
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Attack, facing: Direction.Left),
            });
            PrimeEnemyActionState(worldState, 40, targetId: 10, executeTick: 1);
            var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);

            return pipeline.RunTick(new TickInput(1));
        }

        private static LockedTargetLostMetrics RunLockedTargetLostControlProbe(EnemyAiProfile profile)
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = CreateEnemyPipeline(worldState, profile);
            var activeWindupEntryTick = 0;
            var cancelOwnerTick = 0;
            var cancelTraceTick = 0;

            var windupTick = pipeline.RunTick(new TickInput(1));
            var windupSignals = windupTick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
            if (windupSignals.Any(signal => signal.StartedThisTick) || GetEnemyActionState(worldState, 40).IsActive)
            {
                activeWindupEntryTick = 1;
            }

            worldState.CreateWriteContext().ApplyDamage(10, 3);
            var cancelTick = pipeline.RunTick(new TickInput(2));
            var enemyAfterCancelTick = GetEntity(worldState, 40);
            var actionStateAfterCancelTick = GetEnemyActionState(worldState, 40);
            var cancelSignals = cancelTick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
            if (cancelSignals.Any(signal => signal.CanceledThisTick) &&
                !actionStateAfterCancelTick.IsActive &&
                enemyAfterCancelTick.aiMode == EnemyAiMode.Patrol)
            {
                cancelOwnerTick = 2;
            }

            if (cancelTick.Trace.Text.Contains("LockedTargetLost", StringComparison.Ordinal))
            {
                cancelTraceTick = 2;
            }

            return new LockedTargetLostMetrics(
                activeWindupEntryTick,
                cancelOwnerTick,
                cancelTraceTick,
                enemyAfterCancelTick.aiMode,
                Array.Empty<int>());
        }

        private static WindupParityComparisonMetrics RunWindupParityComparison(
            WorldState baselineWorldState,
            EnemyAiProfile baselineProfile,
            WorldState pilotWorldState,
            EnemyAiProfile pilotProfile,
            int ticks)
        {
            var baselinePipeline = CreateEnemyPipeline(baselineWorldState, baselineProfile);
            var pilotPipeline = CreateEnemyPipeline(pilotWorldState, pilotProfile);
            var baselineDefinition = baselineProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var pilotDefinition = pilotProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var baselineMetrics = default(WindupContractMetrics);
            var pilotMetrics = default(WindupContractMetrics);
            var firstDivergentPositionOrFacingTick = 0;

            for (var tickIndex = 1; tickIndex <= ticks; tickIndex++)
            {
                var baselineTick = baselinePipeline.RunTick(new TickInput(tickIndex));
                var pilotTick = pilotPipeline.RunTick(new TickInput(tickIndex));

                baselineMetrics = UpdateWindupContractMetrics(
                    baselineMetrics,
                    baselineWorldState,
                    baselineDefinition,
                    baselineTick,
                    tickIndex);
                pilotMetrics = UpdateWindupContractMetrics(
                    pilotMetrics,
                    pilotWorldState,
                    pilotDefinition,
                    pilotTick,
                    tickIndex);

                if (firstDivergentPositionOrFacingTick == 0)
                {
                    var baselineEnemy = GetEntityAfterTick(baselineTick, 40);
                    var pilotEnemy = GetEntityAfterTick(pilotTick, 40);
                    if (baselineEnemy.position != pilotEnemy.position ||
                        baselineEnemy.facing != pilotEnemy.facing)
                    {
                        firstDivergentPositionOrFacingTick = tickIndex;
                    }
                }
            }

            return new WindupParityComparisonMetrics(baselineMetrics, pilotMetrics, firstDivergentPositionOrFacingTick);
        }

        private static WindupContractMetrics RunWindupContractMetrics(
            WorldState worldState,
            EnemyAiProfile profile,
            int ticks)
        {
            var pipeline = CreateEnemyPipeline(worldState, profile);
            var runtimeDefinition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var metrics = default(WindupContractMetrics);

            for (var tickIndex = 1; tickIndex <= ticks; tickIndex++)
            {
                var tick = pipeline.RunTick(new TickInput(tickIndex));
                metrics = UpdateWindupContractMetrics(metrics, worldState, runtimeDefinition, tick, tickIndex);
            }

            return metrics;
        }

        private static WindupContractMetrics UpdateWindupContractMetrics(
            WindupContractMetrics metrics,
            WorldState worldState,
            EnemyAiRuntimeDefinition runtimeDefinition,
            TickResult tick,
            int tickIndex)
        {
            var trace = tick.Trace.Text;
            var actionSignals = tick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
            var snapshot = worldState.CreateSnapshot();
            snapshot.TryGetEntity(40, out var source);
            snapshot.TryGetEnemyActionState(40, out var currentActionState);
            var targetSensedTick = CaptureFirstTransitionTick(trace, 40, "TargetSensed", tickIndex, metrics.TargetSensedTick);
            var targetInRangeTick = CaptureFirstTransitionTick(trace, 40, "TargetInRange", tickIndex, metrics.TargetInRangeTick);
            var attackEntryTick = CaptureFirstTransitionIntoModeTick(trace, 40, EnemyAiMode.Attack, tickIndex, metrics.AttackEntryTick);
            var actionStartTargetResolvedTick = metrics.ActionStartTargetResolvedTick;
            var actionStartBlockedByMoveLockTick = metrics.ActionStartBlockedByMoveLockTick;
            var actionStartActiveWithoutSignalTick = metrics.ActionStartActiveWithoutSignalTick;
            var actionStartTick = metrics.ActionStartTick;

            if (source.entityId == 40 &&
                source.aiMode == EnemyAiMode.Attack &&
                !currentActionState.IsActive)
            {
                var canResolveStart = EnemyActionStateTargeting.TryResolveStartAction(
                    snapshot,
                    source,
                    runtimeDefinition.DetectionStrategy,
                    runtimeDefinition.AttackDecisionStrategy,
                    runtimeDefinition.DetectionSettings,
                    runtimeDefinition.AttackDecisionSettings,
                    out _,
                    out _);

                if (actionStartTargetResolvedTick == 0 && canResolveStart)
                {
                    actionStartTargetResolvedTick = tickIndex;
                }

                if (actionStartBlockedByMoveLockTick == 0 &&
                    canResolveStart &&
                    !snapshot.CanStartAction(40, tickIndex) &&
                    snapshot.TryGetEntityExecutionLockState(40, out var executionLockState) &&
                    executionLockState.phase == EntityExecutionPhase.Move &&
                    EntityExecutionLockQueries.IsLocked(executionLockState, tickIndex))
                {
                    actionStartBlockedByMoveLockTick = tickIndex;
                }
            }

            if (actionStartTick == 0 && actionSignals.Any(signal => signal.StartedThisTick))
            {
                actionStartTick = tickIndex;
            }

            if (actionStartActiveWithoutSignalTick == 0 &&
                actionStartTick == 0 &&
                currentActionState.IsActive &&
                !actionSignals.Any(signal => signal.StartedThisTick))
            {
                actionStartActiveWithoutSignalTick = tickIndex;
            }

            var attackExecuteRawIntentTick = metrics.AttackExecuteRawIntentTick;
            var attackExecuteRejectedByExecutionLockTick = metrics.AttackExecuteRejectedByExecutionLockTick;
            var hasCombatRawIntent = tick.AttackPhaseResult.RawIntents.Any(intent =>
                intent.SourceId == 40 &&
                intent.TargetId == 10 &&
                intent.SourceKind == AttackSourceKind.Combat);
            if (attackExecuteRawIntentTick == 0 && hasCombatRawIntent)
            {
                attackExecuteRawIntentTick = tickIndex;
            }

            if (attackExecuteRejectedByExecutionLockTick == 0 &&
                hasCombatRawIntent &&
                tick.AttackPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("AttackRejected|Stage=ExecutionLock|", StringComparison.Ordinal) &&
                    reason.Contains("Source=40|", StringComparison.Ordinal)))
            {
                attackExecuteRejectedByExecutionLockTick = tickIndex;
            }

            var attackExecuteTick = metrics.AttackExecuteTick;
            var attackExecuteActionStateActiveTick = metrics.AttackExecuteActionStateActiveTick;
            var attackExecuteExecutionAttemptedTick = metrics.AttackExecuteExecutionAttemptedTick;
            if (attackExecuteTick == 0 &&
                (actionSignals.Any(signal => signal.ExecutedThisTick) ||
                 tick.AttackPhaseResult.DamageResolutions.Any(record =>
                     record.Accepted &&
                     record.SourceId == 40 &&
                     record.TargetId == 10 &&
                     record.SourceKind == AttackSourceKind.Combat)))
            {
                attackExecuteTick = tickIndex;
                var actionState = GetEnemyActionState(worldState, 40);
                if (actionState.IsActive)
                {
                    attackExecuteActionStateActiveTick = tickIndex;
                }

                if (actionState.executionAttempted)
                {
                    attackExecuteExecutionAttemptedTick = tickIndex;
                }
            }

            var attackCommittedTraceTick = CaptureFirstTransitionTick(trace, 40, "AttackCommitted", tickIndex, metrics.AttackCommittedTraceTick);
            var attackCommittedRecoverTick = CaptureFirstTransitionExactTick(
                trace,
                40,
                EnemyAiMode.Attack,
                EnemyAiMode.Recover,
                tickIndex,
                metrics.AttackCommittedRecoverTick);
            var recoverEntryTick = CaptureFirstTransitionIntoModeTick(trace, 40, EnemyAiMode.Recover, tickIndex, metrics.RecoverEntryTick);
            var recoverCompleteTick = CaptureFirstTransitionReasonPrefixTick(
                trace,
                40,
                "RecoverComplete",
                tickIndex,
                metrics.RecoverCompleteTick);
            var recoverTickCount = metrics.RecoverCompleteTick == 0
                ? metrics.RecoverTickCount + CountTransitionReasonsWithPrefix(trace, 40, "RecoverTick")
                : metrics.RecoverTickCount;
            var recoverPatrolWriteCount = metrics.RecoverPatrolWriteCount;
            if (metrics.RecoverCompleteTick == 0 && IsRecoverLaneTick(trace, 40))
            {
                recoverPatrolWriteCount += CountPatrolStateUpdates(trace, 40);
            }
            var firstCombatDamageTick = metrics.FirstCombatDamageTick;

            if (firstCombatDamageTick == 0 &&
                (tick.AttackPhaseResult.DamageResolutions.Any(record =>
                    record.Accepted &&
                    record.SourceId == 40 &&
                    record.TargetId == 10 &&
                    record.SourceKind == AttackSourceKind.Combat) ||
                 worldState.CreateSnapshot().CountPendingCellImpactsForOwner(40) > 0))
            {
                firstCombatDamageTick = tickIndex;
            }

            return new WindupContractMetrics(
                targetSensedTick,
                targetInRangeTick,
                attackEntryTick,
                actionStartTargetResolvedTick,
                actionStartBlockedByMoveLockTick,
                actionStartActiveWithoutSignalTick,
                actionStartTick,
                attackExecuteRawIntentTick,
                attackExecuteRejectedByExecutionLockTick,
                attackExecuteTick,
                attackExecuteActionStateActiveTick,
                attackExecuteExecutionAttemptedTick,
                attackCommittedTraceTick,
                attackCommittedRecoverTick,
                recoverEntryTick,
                recoverCompleteTick,
                recoverTickCount,
                recoverPatrolWriteCount,
                firstCombatDamageTick);
        }

        private static int CaptureFirstTransitionTick(string traceText, int entityId, string reason, int tickIndex, int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|Reason={reason}", StringComparison.Ordinal))
                {
                    return tickIndex;
                }
            }

            return currentTick;
        }

        private static int CaptureFirstTransitionReasonPrefixTick(
            string traceText,
            int entityId,
            string reasonPrefix,
            int tickIndex,
            int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) ||
                    !lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal))
                {
                    continue;
                }

                var reason = ReadDelimitedField(lines[i], "Reason");
                if (reason.StartsWith(reasonPrefix, StringComparison.Ordinal))
                {
                    return tickIndex;
                }
            }

            return currentTick;
        }

        private static int CaptureFirstTransitionIntoModeTick(
            string traceText,
            int entityId,
            EnemyAiMode targetMode,
            int tickIndex,
            int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) ||
                    !lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal) ||
                    !lines[i].Contains($"|To={targetMode}", StringComparison.Ordinal))
                {
                    continue;
                }

                if (lines[i].Contains($"|From={targetMode}|", StringComparison.Ordinal))
                {
                    continue;
                }

                return tickIndex;
            }

            return currentTick;
        }

        private static int CaptureFirstTransitionExactTick(
            string traceText,
            int entityId,
            EnemyAiMode fromMode,
            EnemyAiMode toMode,
            int tickIndex,
            int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|From={fromMode}|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|To={toMode}", StringComparison.Ordinal))
                {
                    return tickIndex;
                }
            }

            return currentTick;
        }

        private static int CountTransitionReasonsWithPrefix(string traceText, int entityId, string reasonPrefix)
        {
            if (string.IsNullOrEmpty(traceText))
            {
                return 0;
            }

            return traceText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line =>
                    line.Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    line.Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    ReadDelimitedField(line, "Reason").StartsWith(reasonPrefix, StringComparison.Ordinal));
        }

        private static bool IsRecoverLaneTick(string traceText, int entityId)
        {
            if (string.IsNullOrEmpty(traceText))
            {
                return false;
            }

            return traceText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line =>
                    line.Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    line.Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    (line.Contains("|From=Recover|", StringComparison.Ordinal) ||
                     line.Contains("|To=Recover|", StringComparison.Ordinal)));
        }

        private static void AssertWindupAttackControlGreen(in WindupContractMetrics metrics, string label, int expectedRecoverTicks)
        {
            AssertCombatStartGateComplete(metrics, label, requireTargetSensed: false);
            AssertExecuteGateComplete(metrics, label);
            AssertCommitTraceGateComplete(metrics, label);
            AssertRecoverGateComplete(metrics, label, expectedRecoverTicks);
            AssertWindupDiagnosticsClean(metrics, label);
        }

        private static void AssertWindupProbeComplete(in WindupContractMetrics metrics, string label, int expectedRecoverTicks)
        {
            AssertCombatStartGateComplete(metrics, label, requireTargetSensed: true);
            AssertExecuteGateComplete(metrics, label);
            AssertCommitTraceGateComplete(metrics, label);
            AssertRecoverGateComplete(metrics, label, expectedRecoverTicks);
            AssertWindupDiagnosticsClean(metrics, label);
        }

        private static void AssertCombatStartGateComplete(in WindupContractMetrics metrics, string label, bool requireTargetSensed)
        {
            if (requireTargetSensed)
            {
                Assert.That(metrics.TargetSensedTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            }

            Assert.That(metrics.TargetInRangeTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackEntryTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.ActionStartTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertExecuteGateComplete(in WindupContractMetrics metrics, string label)
        {
            Assert.That(metrics.AttackExecuteTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackExecuteActionStateActiveTick, Is.EqualTo(metrics.AttackExecuteTick), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackExecuteExecutionAttemptedTick, Is.EqualTo(metrics.AttackExecuteTick), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.FirstCombatDamageTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertCommitTraceGateComplete(in WindupContractMetrics metrics, string label)
        {
            Assert.That(metrics.AttackCommittedTraceTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackCommittedRecoverTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackCommittedTraceTick, Is.EqualTo(metrics.AttackCommittedRecoverTick), $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertRecoverGateComplete(in WindupContractMetrics metrics, string label, int expectedRecoverTicks)
        {
            Assert.That(metrics.RecoverEntryTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.RecoverCompleteTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.RecoverTickCount, Is.EqualTo(expectedRecoverTicks), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.RecoverPatrolWriteCount, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertWindupDiagnosticsClean(in WindupContractMetrics metrics, string label)
        {
            Assert.That(metrics.ActionStartActiveWithoutSignalTick, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.ActionStartBlockedByMoveLockTick, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackExecuteRejectedByExecutionLockTick, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostControlGreen(
            in LockedTargetLostMetrics metrics,
            string label,
            EnemyAiMode expectedFallbackMode)
        {
            AssertLockedTargetLostEntryGreen(metrics, label);
            AssertLockedTargetLostCancelOwnerGreen(metrics, label, expectedFallbackMode);
            AssertLockedTargetLostWordingGreen(metrics, label);
        }

        private static void AssertLockedTargetLostEntryGreen(in LockedTargetLostMetrics metrics, string label)
        {
            Assert.That(metrics.ActiveWindupEntryTick, Is.GreaterThan(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostCancelOwnerGreen(
            in LockedTargetLostMetrics metrics,
            string label,
            EnemyAiMode expectedFallbackMode)
        {
            Assert.That(metrics.CancelOwnerTick, Is.GreaterThan(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            Assert.That(metrics.FallbackMode, Is.EqualTo(expectedFallbackMode), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostWordingGreen(in LockedTargetLostMetrics metrics, string label)
        {
            Assert.That(metrics.CancelTraceTick, Is.GreaterThan(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            Assert.That(metrics.CancelTraceTick, Is.EqualTo(metrics.CancelOwnerTick), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostHomeReturnGreen(in LockedTargetLostMetrics metrics, string label, int leashRadius)
        {
            Assert.That(metrics.HomeDistanceSeries, Is.Not.Empty, $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            var firstHomeIndex = -1;
            for (var i = 0; i < metrics.HomeDistanceSeries.Count; i++)
            {
                if (metrics.HomeDistanceSeries[i] == 0)
                {
                    firstHomeIndex = i;
                    break;
                }
            }

            Assert.That(firstHomeIndex, Is.GreaterThanOrEqualTo(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            for (var i = 1; i <= firstHomeIndex; i++)
            {
                Assert.That(
                    metrics.HomeDistanceSeries[i],
                    Is.LessThanOrEqualTo(metrics.HomeDistanceSeries[i - 1]),
                    $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            }

            Assert.That(metrics.HomeDistanceSeries[firstHomeIndex], Is.LessThanOrEqualTo(leashRadius), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            Assert.That(metrics.HomeDistanceSeries[^1], Is.LessThanOrEqualTo(leashRadius), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static string BuildWindupMetricsSummary(in WindupContractMetrics metrics)
        {
            return $"TargetSensed={metrics.TargetSensedTick}, TargetInRange={metrics.TargetInRangeTick}, AttackEntry={metrics.AttackEntryTick}, ActionStartResolved={metrics.ActionStartTargetResolvedTick}, ActionStartBlockedByMoveLock={metrics.ActionStartBlockedByMoveLockTick}, ActionStartActiveWithoutSignal={metrics.ActionStartActiveWithoutSignalTick}, ActionStart={metrics.ActionStartTick}, AttackRawIntent={metrics.AttackExecuteRawIntentTick}, AttackRejectedByExecutionLock={metrics.AttackExecuteRejectedByExecutionLockTick}, AttackExecute={metrics.AttackExecuteTick}, ExecuteActionStateActive={metrics.AttackExecuteActionStateActiveTick}, ExecuteAttempted={metrics.AttackExecuteExecutionAttemptedTick}, AttackCommittedTrace={metrics.AttackCommittedTraceTick}, AttackCommittedRecover={metrics.AttackCommittedRecoverTick}, RecoverEntry={metrics.RecoverEntryTick}, RecoverComplete={metrics.RecoverCompleteTick}, RecoverTickCount={metrics.RecoverTickCount}, RecoverPatrolWrites={metrics.RecoverPatrolWriteCount}, FirstCombatDamage={metrics.FirstCombatDamageTick}";
        }

        private static string BuildLockedTargetLostMetricsSummary(in LockedTargetLostMetrics metrics)
        {
            var homeDistanceSeries = metrics.HomeDistanceSeries.Count == 0
                ? "<none>"
                : string.Join(">", metrics.HomeDistanceSeries);
            return $"ActiveWindupEntry={metrics.ActiveWindupEntryTick}, CancelOwner={metrics.CancelOwnerTick}, CancelTrace={metrics.CancelTraceTick}, FallbackMode={metrics.FallbackMode}, HomeDistanceSeries={homeDistanceSeries}";
        }

        private static string ReadDelimitedField(string line, string fieldName)
        {
            var fieldPrefix = $"{fieldName}=";
            var startIndex = line.IndexOf(fieldPrefix, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return string.Empty;
            }

            startIndex += fieldPrefix.Length;
            var endIndex = line.IndexOf('|', startIndex);
            return endIndex >= 0
                ? line.Substring(startIndex, endIndex - startIndex)
                : line.Substring(startIndex);
        }

        private static int ResolveRecoverTicks(EnemyAiProfile profile)
        {
            return profile
                .CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .CommonSettings
                .RecoverTicks;
        }

        private static void AssertLaterOnlyBoundedDrift(int baselineTick, int pilotTick, string label)
        {
            Assert.That(pilotTick, Is.GreaterThanOrEqualTo(baselineTick), $"{label} advanced earlier than the Forward baseline.");
            Assert.That(pilotTick, Is.LessThanOrEqualTo(baselineTick + 1), $"{label} drift exceeded the bounded +1 tick exposure window.");
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            EnemyAiProfile profile,
            params IEntityLogic[] entityLogics)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                entityLogics ?? Array.Empty<IEntityLogic>(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerKinematicLocomotionTiming: CreateOneTickKinematicTiming());
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            params IEntityLogic[] entityLogics)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                entityLogics ?? Array.Empty<IEntityLogic>(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerKinematicLocomotionTiming: CreateOneTickKinematicTiming());
        }

        private static TickPipeline CreateEnemyPipelineWithRespawnDelay(
            WorldState worldState,
            EnemyAiProfile profile,
            int playerRespawnDelayTicks,
            params IEntityLogic[] entityLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                entityLogics ?? Array.Empty<IEntityLogic>(),
                timingProfile,
                playerTiming,
                playerRespawnDelayTicks,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerKinematicLocomotionTiming: CreateOneTickKinematicTiming(timingProfile));
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            EnemyAiRuntimeDefinition runtimeDefinition,
            params IEntityLogic[] entityLogics)
        {
            return new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(runtimeDefinition))
                .CreateTickPipeline(
                    worldState,
                    entityLogics ?? Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                    playerKinematicLocomotionTiming: CreateOneTickKinematicTiming());
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming(
            GameplayTimingProfile timingProfile = null)
        {
            if (timingProfile == null || timingProfile.SimulationTicksPerSecond <= 0)
            {
                timingProfile = GameplayTimingProfile.CreateDefault();
            }

            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static EnemyAiProfile CreateJumpChaserProfile(
            int windupTicks,
            int airborneTicks,
            int cooldownTicks,
            bool includePassiveContact = false,
            ChaseSettings? chaseSettings = null)
        {
            return EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(windupTicks, airborneTicks, cooldownTicks),
                includePassiveContact: includePassiveContact,
                chaseSettings: chaseSettings);
        }

        private static EnemyAiProfile CreateJumpPatrolProfile(
            int windupTicks,
            int airborneTicks,
            int cooldownTicks)
        {
            return EnemyAiProfileTestFactory.CreateJumpPatrol(
                new EnemyJumpTimingSettings(windupTicks, airborneTicks, cooldownTicks));
        }

        private static EnemyAiProfile CreateWallFollowerProfile(
            WallFollowTurnPreference turnPreference,
            int moveCooldownTicks = 0,
            bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.CreateWallFollower(turnPreference, moveCooldownTicks, includePassiveContact);
        }

        private static void PrimeEnemyActionState(
            WorldState worldState,
            int entityId,
            int targetId,
            int executeTick)
        {
            var writeContext = worldState.CreateWriteContext();
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            var anchor = new CombatOriginAnchor(
                entity.position,
                KinematicOffset2.Zero,
                entity.position.x * KinematicFixed.UnitsPerCell,
                entity.position.y * KinematicFixed.UnitsPerCell,
                Direction.Left);
            writeContext.SetEnemyActionState(
                entityId,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 1,
                    lockedTargetEntityId = targetId,
                    direction = Direction.Left,
                    startTick = Math.Max(0, executeTick - 1),
                    executeTick = executeTick,
                    executionAttempted = false,
                    hasLockedCombatAnchor = true,
                    lockedCombatAnchor = anchor,
                });
        }

        private static void DestroyProfile(EnemyAiProfile profile)
        {
            EnemyAiProfileTestFactory.Destroy(profile);
        }

        private static TickPipeline CreateTickPipeline(
            EnemyAiProfile defaultProfile,
            EnemyAiProfile summonerProfile,
            EnemyUnitArchetypeCatalog archetypeCatalog,
            WorldState worldState)
        {
            var runtimeSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = 40,
                        Profile = summonerProfile,
                    },
                },
                EnemyUnitArchetypeCatalog = archetypeCatalog,
            }.CreateEnemyAiRuntimeSnapshot();

            return new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    runtimeSnapshot.DefaultDefinition,
                    runtimeSnapshot.DefinitionsByEntityId,
                    runtimeSnapshot.DefinitionsByArchetypeId,
                    runtimeSnapshot.HasDefaultDefinition),
                runtimeSnapshot.SpawnDefaultsByArchetypeId)
                .CreateTickPipeline(worldState);
        }

        private static GameplayBootstrapper CreateSharedSummonBootstrapper(
            EnemyAiProfile summonerProfile,
            out EnemyAiProfile defaultProfile,
            out EnemyUnitArchetypeCatalog archetypeCatalog,
            EnemyUnitArchetypeAsset summonedArchetype = null,
            IReadOnlyList<int> summonerEntityIds = null)
        {
            defaultProfile = CreateUtilityProfile();
            archetypeCatalog = CreateEnemyUnitArchetypeCatalog(summonedArchetype ?? GetSharedSummonedArchetype());
            var overrideEntityIds = summonerEntityIds ?? new[] { 40 };

            var runtimeSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = overrideEntityIds
                    .Select(entityId => new EnemyAiProfileOverride
                    {
                        EntityId = entityId,
                        Profile = summonerProfile,
                    })
                    .ToArray(),
                EnemyUnitArchetypeCatalog = archetypeCatalog,
            }.CreateEnemyAiRuntimeSnapshot();

            return new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    runtimeSnapshot.DefaultDefinition,
                    runtimeSnapshot.DefinitionsByEntityId,
                    runtimeSnapshot.DefinitionsByArchetypeId,
                    runtimeSnapshot.HasDefaultDefinition),
                runtimeSnapshot.SpawnDefaultsByArchetypeId);
        }

        private static TickPipeline CreateSharedSummonTickPipeline(
            EnemyAiProfile summonerProfile,
            WorldState worldState,
            out EnemyAiProfile defaultProfile,
            out EnemyUnitArchetypeCatalog archetypeCatalog,
            EnemyUnitArchetypeAsset summonedArchetype = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<int> summonerEntityIds = null)
        {
            var bootstrapper = CreateSharedSummonBootstrapper(
                summonerProfile,
                out defaultProfile,
                out archetypeCatalog,
                summonedArchetype,
                summonerEntityIds);
            if (tileFeatureDefinitions == null)
            {
                return bootstrapper.CreateTickPipeline(worldState);
            }

            var timingProfile = GameplayTimingProfile.CreateDefault();
            return bootstrapper.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static EnemyUnitArchetypeAsset CreateEnemyUnitArchetypeAsset(
            string archetypeId,
            EnemyAiProfile profile,
            int hp,
            EnemyAiMode initialAiMode,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            var asset = ScriptableObject.CreateInstance<EnemyUnitArchetypeAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(asset, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            EnemyAiProfileTestFactory.SetSerializedField(asset, "aiProfile", profile);
            EnemyAiProfileTestFactory.SetSerializedField(asset, "spawnDefaults", CreateEnemyUnitSpawnDefaults(hp, initialAiMode, unitMobilityKind));
            return asset;
        }

        private static EnemyUnitArchetypeCatalog CreateEnemyUnitArchetypeCatalog(params EnemyUnitArchetypeAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyUnitArchetypeCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(catalog, "entries", entries ?? Array.Empty<EnemyUnitArchetypeAsset>());
            return catalog;
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

        private static TileFeatureRuntimeDefinition CreateTileFeatureDefinition(
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

        private static EnemyUnitArchetypeAsset GetSharedSummonedArchetype()
        {
            if (SharedSummonedArchetype == null)
            {
                SharedSummonedProfile = CreateUtilityProfile();
                SharedSummonedArchetype = CreateEnemyUnitArchetypeAsset(
                    "BasicMinion",
                    SharedSummonedProfile,
                    hp: 1,
                    initialAiMode: EnemyAiMode.Patrol);
            }

            return SharedSummonedArchetype;
        }

        private static EnemyUnitSpawnDefaults CreateEnemyUnitSpawnDefaults(
            int hp,
            EnemyAiMode initialAiMode,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            object boxed = EnemyUnitSpawnDefaults.CreateDefault();
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "hp", hp);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "initialAiMode", initialAiMode);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "unitMobilityKind", unitMobilityKind);
            return (EnemyUnitSpawnDefaults)boxed;
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp = 1,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right,
            int aiStateTimer = 0,
            int enemyLocomotionCooldownTicks = 0)
        {
            var unitRole = teamId switch
            {
                1 => UnitRole.Player,
                2 => UnitRole.Enemy,
                _ => UnitRole.None,
            };

            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = aiStateTimer,
                enemyLocomotionCooldownTicks = enemyLocomotionCooldownTicks,
            };
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp = 1,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right,
            int aiStateTimer = 0,
            int enemyLocomotionCooldownTicks = 0)
        {
            var unitRole = teamId switch
            {
                1 => UnitRole.Player,
                2 => UnitRole.Enemy,
                _ => UnitRole.None,
            };

            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = aiStateTimer,
                enemyLocomotionCooldownTicks = enemyLocomotionCooldownTicks,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            Vector2Int position,
            BoxCapabilities capabilities = BoxCapabilities.None,
            EntityPhaseState state = EntityPhaseState.Idle,
            int stateTimer = 0,
            Direction facing = Direction.None,
            int kineticInstigatorEntityId = 0,
            int kineticInstigatorTeamId = 0)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = state,
                stateTimer = stateTimer,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = capabilities,
                kineticInstigatorEntityId = kineticInstigatorEntityId,
                kineticInstigatorTeamId = kineticInstigatorTeamId,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }

        private static EntityState CreateSlidingPushBox(
            int entityId,
            Vector2Int position,
            Direction facing,
            int kineticInstigatorEntityId,
            int kineticInstigatorTeamId)
        {
            return CreateBox(
                entityId,
                position,
                capabilities: BoxCapabilities.Push,
                state: EntityPhaseState.Sliding,
                stateTimer: 0,
                facing: facing,
                kineticInstigatorEntityId: kineticInstigatorEntityId,
                kineticInstigatorTeamId: kineticInstigatorTeamId);
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }

        private sealed class TickScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _controlledEntityId;
            private readonly IReadOnlyDictionary<int, RawMovementIntent> _movementIntentsByTick;

            public TickScriptedMovementLogic(
                int controlledEntityId,
                IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                _controlledEntityId = controlledEntityId;
                _movementIntentsByTick = movementIntentsByTick ?? throw new ArgumentNullException(nameof(movementIntentsByTick));
            }

            public int ControlledEntityId => _controlledEntityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntentsByTick.TryGetValue(input.TickIndex, out var movementIntent))
                {
                    buffer.Add(movementIntent);
                }
            }
        }

        private sealed class ScriptedAttackLogic : IAttackEntityLogic
        {
            private readonly int _sourceId;
            private readonly int _targetId;

            public ScriptedAttackLogic(int sourceId, int targetId)
            {
                _sourceId = sourceId;
                _targetId = targetId;
            }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (!snapshot.TryGetEntity(_sourceId, out var source) ||
                    !snapshot.TryGetEntity(_targetId, out var target) ||
                    source.hp <= 0 ||
                    target.hp <= 0 ||
                    source.markedForDeath ||
                    target.markedForDeath)
                {
                    return;
                }

                buffer.Add(new RawAttackIntent(_sourceId, 100, _targetId));
            }
        }
    }
}
