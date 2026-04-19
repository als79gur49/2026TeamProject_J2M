using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class EnemyAiScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyAi_MultiTick_FollowsPatrolChaseAttackRecoverSequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstTick = pipeline.RunTick(new TickInput(1));
            var enemyAfterFirstTick = GetEntity(worldState, 40);

            Assert.That(enemyAfterFirstTick.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(enemyAfterFirstTick.aiMode, Is.EqualTo(EnemyAiMode.Chase));
            Assert.That(firstTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(firstTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Patrol|FromTimer=0|To=Chase|ToTimer=0|Reason=TargetSensed"));

            var secondTick = pipeline.RunTick(new TickInput(2));
            var enemyAfterSecondTick = GetEntity(worldState, 40);
            var playerAfterSecondTick = GetEntity(worldState, 10);

            Assert.That(enemyAfterSecondTick.position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(enemyAfterSecondTick.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemyAfterSecondTick.aiStateTimer, Is.EqualTo(1));
            Assert.That(playerAfterSecondTick.hp, Is.EqualTo(2));
            Assert.That(
                secondTick.AttackPhaseResult.DamageResolutions.Any(
                    record => record.Accepted &&
                              record.SourceId == 40 &&
                              record.TargetId == 10 &&
                              record.SourceKind == AttackSourceKind.Combat),
                Is.True);
            Assert.That(secondTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40|From=Chase|FromTimer=0|To=Attack|ToTimer=0|Reason=TargetInRange"));
            Assert.That(secondTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=AfterAttack|E=40|From=Attack|FromTimer=0|To=Recover|ToTimer=1|Reason=AttackCommitted"));

            var thirdTick = pipeline.RunTick(new TickInput(3));
            var enemyAfterThirdTick = GetEntity(worldState, 40);

            Assert.That(thirdTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(thirdTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(enemyAfterThirdTick.position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(enemyAfterThirdTick.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemyAfterThirdTick.aiStateTimer, Is.Zero);
            Assert.That(thirdTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Recover|FromTimer=1|To=Recover|ToTimer=0|Reason=RecoverTick"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupProfile_TelegraphsBeforeExecuteAndThenEntersRecover()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, CreateEnemyProfile(windupTicks: 1));

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
            Assert.That(windupSignal.ActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
            Assert.That(windupSignal.StartedThisTick, Is.True);
            Assert.That(windupSignal.CanceledThisTick, Is.False);
            Assert.That(windupSignal.ExecutedThisTick, Is.False);
            Assert.That(windupSignal.StartedRecoveryThisTick, Is.False);

            var executeTick = pipeline.RunTick(new TickInput(2));
            var enemyAfterExecuteTick = GetEntity(worldState, 40);
            var playerAfterExecuteTick = GetEntity(worldState, 10);
            var actionStateAfterExecuteTick = GetEnemyActionState(worldState, 40);
            var executeSignal = executeTick.PresentationData.EnemyActionSignals.Single();

            Assert.That(
                executeTick.AttackPhaseResult.DamageResolutions.Any(
                    record => record.Accepted &&
                              record.SourceId == 40 &&
                              record.TargetId == 10 &&
                              record.SourceKind == AttackSourceKind.Combat),
                Is.True);
            Assert.That(playerAfterExecuteTick.hp, Is.EqualTo(2));
            Assert.That(enemyAfterExecuteTick.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemyAfterExecuteTick.aiStateTimer, Is.EqualTo(1));
            Assert.That(actionStateAfterExecuteTick.IsActive, Is.True);
            Assert.That(actionStateAfterExecuteTick.executionAttempted, Is.True);
            Assert.That(executeSignal.EntityId, Is.EqualTo(40));
            Assert.That(executeSignal.ActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
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
        public void EnemyAi_PhaseThroughLockedTargetValidator_RelocatesAcrossLockedTarget_WithoutSameTickAttack()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = CreateEnemyPipeline(worldState, CreatePhaseThroughLockedTargetDefinition(windupTicks: 1));

            var windupTick = pipeline.RunTick(new TickInput(1));
            Assert.That(windupTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(GetEnemyActionState(worldState, 40).executeTick, Is.EqualTo(2));

            var executeTick = pipeline.RunTick(new TickInput(2));
            var executeSnapshot = worldState.CreateSnapshot();

            Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
            Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(executeSnapshot.TryGetPhasedState(40, out var phasedState), Is.True);
            Assert.That(phasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.EnemyPreMovement));
            Assert.That(GetEnemyActionState(worldState, 40).executionAttempted, Is.True);
            Assert.That(executeTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.Trace.Text, Does.Contain("PhaseEnter|Entity=40|Tick=2|Owner=EnemyPreMovement|Rule=LockedTargetCrossThrough"));
            Assert.That(executeTick.Trace.Text, Does.Contain("EnemyPhaseRelocation|E=40|Label=Committed|Target=10"));
            Assert.That(executeTick.Trace.Text, Does.Contain("Kind=SetPhasedState"));
            Assert.That(executeTick.Trace.Text, Does.Contain("Timing=EnemyLockedTargetCrossThroughValidator"));
            Assert.That(executeTick.Trace.Text, Does.Contain("ReservationRead=CellOnlyPreSettle"));

            var clearTick = pipeline.RunTick(new TickInput(3));
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(40, out _), Is.False);
            Assert.That(clearTick.Trace.Text, Does.Contain("PhaseExit|Entity=40|Tick=3|Owner=EnemyPreMovement|Reason=LockedTargetCrossThroughWindowClosed"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PhaseThroughLockedTargetValidator_FailClosesWhenAnchoredLikeSettlementBlocksTerminalCell()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 70, position: new Vector2Int(2, 0)),
            });
            var pipeline = CreateEnemyPipeline(worldState, CreatePhaseThroughLockedTargetDefinition(windupTicks: 1));

            pipeline.RunTick(new TickInput(1));
            var executeTick = pipeline.RunTick(new TickInput(2));
            var executeSnapshot = worldState.CreateSnapshot();

            Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
            Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(executeSnapshot.TryGetPhasedState(40, out var phasedState), Is.True);
            Assert.That(phasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.EnemyPreMovement));
            Assert.That(executeTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.Trace.Text, Does.Contain("EnemyPhaseRelocation|E=40|Label=Rejected|Target=10"));
            Assert.That(executeTick.Trace.Text, Does.Contain("Result=SettleBlocked"));

            pipeline.RunTick(new TickInput(3));
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(40, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupProfile_LosingLockedTarget_CancelsActionAndFallsBackToPatrol()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, CreateEnemyProfile(windupTicks: 2));

            var windupTick = pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().ApplyDamage(10, 3);

            var cancelTick = pipeline.RunTick(new TickInput(2));
            var enemyAfterCancelTick = GetEntity(worldState, 40);
            var actionStateAfterCancelTick = GetEnemyActionState(worldState, 40);
            var cancelSignal = cancelTick.PresentationData.EnemyActionSignals.Single();

            Assert.That(windupTick.PresentationData.EnemyActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(cancelTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(cancelTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(enemyAfterCancelTick.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(enemyAfterCancelTick.aiStateTimer, Is.Zero);
            Assert.That(actionStateAfterCancelTick.IsActive, Is.False);
            Assert.That(SemanticEventAssertions.GetCleanupRemovedEntityIds(cancelTick.EventLog), Does.Contain(10));
            Assert.That(cancelSignal.EntityId, Is.EqualTo(40));
            Assert.That(cancelSignal.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
            Assert.That(cancelSignal.StartedThisTick, Is.False);
            Assert.That(cancelSignal.CanceledThisTick, Is.True);
            Assert.That(cancelSignal.ExecutedThisTick, Is.False);
            Assert.That(cancelSignal.StartedRecoveryThisTick, Is.False);
            Assert.That(cancelTick.Trace.Text, Does.Contain("LockedTargetLost"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_MoveOccupancy_BlocksAttackStartUntilFirstUnlockedTick()
        {
            var timingProfile = new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 1f / 60f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 1f / 60f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8,
                moveOccupancyDurationSeconds: 1f / 60f);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot
                .CreateDefaultBootstrapper(CreateEnemyProfile(windupTicks: 1))
                .CreateTickPipeline(
                    worldState,
                    new IEntityLogic[0],
                    timingProfile,
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        timingProfile.SimulationTicksPerSecond,
                        timingProfile.RepeatedMoveIntervalSeconds));

            var moveTick = pipeline.RunTick(new TickInput(1));
            var lockedTick = pipeline.RunTick(new TickInput(2));
            var unlockTick = pipeline.RunTick(new TickInput(3));
            var executeTick = pipeline.RunTick(new TickInput(4));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    moveTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=40",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(worldState.CreateSnapshot().TryGetEntityExecutionLockState(40, out var executionLockState), Is.True);
            Assert.That(executionLockState.phase, Is.EqualTo(EntityExecutionPhase.Move));
            Assert.That(executionLockState.unlockTickExclusive, Is.EqualTo(3));
            Assert.That(moveTick.PresentationData.EnemyActionSignals, Is.Empty);
            Assert.That(lockedTick.PresentationData.EnemyActionSignals, Is.Empty);
            Assert.That(lockedTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(unlockTick.PresentationData.EnemyActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(unlockTick.PresentationData.EnemyActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(
                executeTick.AttackPhaseResult.DamageResolutions.Any(
                    record => record.Accepted &&
                              record.SourceId == 40 &&
                              record.TargetId == 10 &&
                              record.SourceKind == AttackSourceKind.Combat),
                Is.True);
            Assert.That(executeTick.PresentationData.EnemyActionSignals.Single().ExecutedThisTick, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_FatalDamage_IsRemovedByCleanupAtTickEnd()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 1, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedAttackLogic(10, 40),
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
            Assert.That(result.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Patrol|FromTimer=0|To=Attack|ToTimer=0|Reason=TargetInRange"));
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

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

        [Test]
        [Category("Full")]
        public void EnemyAi_TopologyChange_MakesBottomEnemySuspendImmediately()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

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

        [Test]
        [Category("Full")]
        public void EnemyAi_TopologyChange_RestoresParticipationWhenEnemyReturnsToBottom()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 1, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

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
            Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Recover));
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
                Assert.That(jumpState.windupEndTick, Is.EqualTo(2));
                Assert.That(jumpState.landingTick, Is.EqualTo(3));
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

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

        [Test]
        [Category("Full")]
        public void EnemyAi_WallFollowerProfile_CirculatesAroundWallAcrossMultipleTicks()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Right);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

            try
            {
                var ticks = new[]
                {
                    pipeline.RunTick(new TickInput(1)),
                    pipeline.RunTick(new TickInput(2)),
                    pipeline.RunTick(new TickInput(3)),
                    pipeline.RunTick(new TickInput(4)),
                    pipeline.RunTick(new TickInput(5)),
                    pipeline.RunTick(new TickInput(6)),
                    pipeline.RunTick(new TickInput(7)),
                    pipeline.RunTick(new TickInput(8)),
                };
                var enemy = GetEntity(worldState, 40);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, 2),
                        new Vector2Int(1, 2),
                        new Vector2Int(2, 2),
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 0),
                        new Vector2Int(1, 0),
                    },
                    ticks.Select(tick => GetEntityAfterTick(tick, 40).position.PlanarPosition).ToArray());
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Left));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_WallFollowerProfile_CirculatesAroundBoxAcrossMultipleTicks()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateBox(entityId: 50, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Left);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

            try
            {
                var ticks = new[]
                {
                    pipeline.RunTick(new TickInput(1)),
                    pipeline.RunTick(new TickInput(2)),
                    pipeline.RunTick(new TickInput(3)),
                    pipeline.RunTick(new TickInput(4)),
                    pipeline.RunTick(new TickInput(5)),
                    pipeline.RunTick(new TickInput(6)),
                    pipeline.RunTick(new TickInput(7)),
                    pipeline.RunTick(new TickInput(8)),
                };
                var enemy = GetEntity(worldState, 40);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        new Vector2Int(2, 0),
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 2),
                        new Vector2Int(1, 2),
                        new Vector2Int(0, 2),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                    },
                    ticks.Select(tick => GetEntityAfterTick(tick, 40).position.PlanarPosition).ToArray());
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Right));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_WallFollowerProfile_CirculatesAlongBoardEdgeAcrossMultipleTicks()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Right);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

            try
            {
                var ticks = new[]
                {
                    pipeline.RunTick(new TickInput(1)),
                    pipeline.RunTick(new TickInput(2)),
                    pipeline.RunTick(new TickInput(3)),
                    pipeline.RunTick(new TickInput(4)),
                    pipeline.RunTick(new TickInput(5)),
                    pipeline.RunTick(new TickInput(6)),
                    pipeline.RunTick(new TickInput(7)),
                    pipeline.RunTick(new TickInput(8)),
                };
                var enemy = GetEntity(worldState, 40);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        new Vector2Int(2, 0),
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 2),
                        new Vector2Int(1, 2),
                        new Vector2Int(0, 2),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                    },
                    ticks.Select(tick => GetEntityAfterTick(tick, 40).position.PlanarPosition).ToArray());
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Right));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_NonAttackingProfile_OnlyPatrolsAndChases()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateNonAttackingEnemyProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    });

                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));
                var enemy = GetEntity(worldState, 40);
                var player = GetEntity(worldState, 10);

                Assert.That(firstTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(thirdTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(player.hp, Is.EqualTo(3));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
        public void EnemyAi_JumpStart_SameFaceFarPlayer_StartsWindup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(20, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateJumpPatrolProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 2);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
        public void EnemyAi_JumpLanding_OnLockedPlayerCell_WithHostileExtraOccupant_Retries()
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                CollectionAssert.AreEqual(new[] { 10, 60 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.retryCount, Is.EqualTo(1));
                Assert.That(jumpState.landingTick, Is.EqualTo(4));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Retry"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_OnLockedPlayerCell_WithFriendlyExtraOccupant_Retries()
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                CollectionAssert.AreEqual(new[] { 10, 20 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.retryCount, Is.EqualTo(1));
                Assert.That(jumpState.landingTick, Is.EqualTo(4));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Retry"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_RechecksResolveSnapshot_WhenExtraOccupantArrivesAfterPlan()
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
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                CollectionAssert.AreEqual(new[] { 10, 60 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.retryCount, Is.EqualTo(1));
                Assert.That(landingTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Retry"));
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
        public void EnemyAi_JumpLanding_ExactReject_LandsOnRetryAfterExtraOccupantLeaves()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var retreatCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, hp: 3),
                CreateUnit(entityId: 60, teamId: 2, position: targetCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
            });
            var profile = CreateJumpChaserProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var retryTick = pipeline.RunTick(new TickInput(3));
                worldState.CreateWriteContext().MoveEntity(60, retreatCell);

                var landingTick = pipeline.RunTick(new TickInput(4));
                var jumpState = GetEnemyJumpState(worldState, 40);
                var stackedUnits = new List<EntityState>();

                worldState.CreateSnapshot().EnumerateUnitsAt(targetCell, stackedUnits);

                Assert.That(retryTick.AttackPhaseResult.RawIntents, Is.Empty);
                Assert.That(GetEntity(worldState, 60).position, Is.EqualTo(retreatCell));
                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(targetCell));
                CollectionAssert.AreEqual(new[] { 10, 40 }, stackedUnits.Select(entity => entity.entityId).ToArray());
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(jumpState.retryCount, Is.EqualTo(0));
                Assert.That(landingTick.Trace.Text, Does.Contain("Label=Landing"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpLanding_ExactReject_WithPersistentExtraOccupant_RetriesMonotonically()
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                var firstRetryTick = pipeline.RunTick(new TickInput(3));
                var secondRetryTick = pipeline.RunTick(new TickInput(4));
                var jumpState = GetEnemyJumpState(worldState, 40);

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(sourceCell));
                Assert.That(GetEntity(worldState, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                Assert.That(jumpState.retryCount, Is.EqualTo(2));
                Assert.That(jumpState.landingTick, Is.EqualTo(5));
                Assert.That(firstRetryTick.Trace.Text, Does.Contain("Label=Retry"));
                Assert.That(secondRetryTick.Trace.Text, Does.Contain("Label=Retry"));
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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

                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(targetCell));
                Assert.That(enemy.hp, Is.EqualTo(2));
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 1)));
                Assert.That(GetEntity(worldState, 50).state, Is.EqualTo(EntityPhaseState.Idle));
                CollectionAssert.AreEqual(
                    new[]
                    {
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
        [Category("Extended")]
        public void EnemyAi_JumpCooldown_DoesNotBlockPatrolLocomotion_AfterPlayerLeavesFace()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 1), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)));
            var profile = CreateJumpPatrolProfile(windupTicks: 1, airborneTicks: 1, cooldownTicks: 2);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            PrimePlayerControlState(worldState, 10);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var landingTick = pipeline.RunTick(new TickInput(3));
                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Front, 3, 1));
                var cooldownTick = pipeline.RunTick(new TickInput(4));

                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 1)));
                Assert.That(GetEntityAfterTick(secondTick, 40).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
                Assert.That(GetEntityAfterTick(landingTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 1)));
                Assert.That(GetEntityAfterTick(cooldownTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(4, 1)));
                Assert.That(GetEnemyJumpState(worldState, 40).phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
                Assert.That(landingTick.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.True);
                Assert.That(cooldownTick.PresentationData.EnemyJumpSignals.Single().StartedWindupThisTick, Is.False);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

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
        public void EnemyAi_ContactDamageProfile_MovesIntoPlayerCell_AndDealsSameTickDamage()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
        [Category("Extended")]
        public void EnemyAi_ContactDamageProfile_AlreadySharingPlayerCell_DealsDamageWithoutMoving()
        {
            var stackedCell = new Vector2Int(0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: stackedCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: stackedCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
        public void EnemyAi_MeleeProfile_WithPassiveContact_SameCellProducesCombatAndPassiveCandidates()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Attack, facing: Direction.Left),
            });
            PrimeEnemyActionState(worldState, 40, targetId: 10, executeTick: 1);
            var profile = CreateDefaultMeleeProfile(includePassiveContact: true);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var result = pipeline.RunTick(new TickInput(1));
                var resolutions = result.AttackPhaseResult.DamageResolutions;

                CollectionAssert.AreEqual(
                    new[]
                    {
                        AttackSourceKind.Combat,
                        AttackSourceKind.PassiveContact,
                    },
                    result.AttackPhaseResult.DamageResolutions.Select(record => record.SourceKind).ToArray());
                Assert.That(resolutions.Count, Is.EqualTo(2));
                Assert.That(resolutions[0].Accepted, Is.True);
                Assert.That(resolutions[0].SourceKind, Is.EqualTo(AttackSourceKind.Combat));
                Assert.That(resolutions[1].Accepted, Is.False);
                Assert.That(resolutions[1].SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(resolutions[1].RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(4));
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
        [Category("Extended")]
        public void EnemyAi_ContactDamageProfile_PlayerOwnedCooldownWhileStacked_OnlyAcceptsAtReceiverCadence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

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
        public void EnemyAi_ContactDamageProfile_TwoEnemiesSameCellSameTick_OnlyFirstDeterministicHitIsAccepted()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

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
        public void EnemyAi_ContactDamageProfile_CooldownExpiryWhileStillStacked_ReacceptsExactlyOneHit()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

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
        public void EnemyAi_ContactDamageProfile_RecoverTicks_NoLongerControlContactCadence_ButRegularMeleeStillUsesRecover()
        {
            var fastContactProfile = CreateContactDamageProfile(recoverTicks: 0);
            var slowContactProfile = CreateContactDamageProfile(recoverTicks: 5);
            var fastMeleeProfile = CreateDefaultMeleeProfile(recoverTicks: 0);
            var slowMeleeProfile = CreateDefaultMeleeProfile(recoverTicks: 5);

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

                var fastMeleeWorld = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                });
                var slowMeleeWorld = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                });
                var fastMeleePipeline = GameplayCompositionRoot.CreateTickPipeline(fastMeleeWorld, fastMeleeProfile);
                var slowMeleePipeline = GameplayCompositionRoot.CreateTickPipeline(slowMeleeWorld, slowMeleeProfile);

                for (var tick = 1; tick <= 3; tick++)
                {
                    fastMeleePipeline.RunTick(new TickInput(tick));
                    slowMeleePipeline.RunTick(new TickInput(tick));
                }

                Assert.That(GetEntity(fastMeleeWorld, 10).hp, Is.LessThan(GetEntity(slowMeleeWorld, 10).hp));
            }
            finally
            {
                DestroyProfile(fastContactProfile);
                DestroyProfile(slowContactProfile);
                DestroyProfile(fastMeleeProfile);
                DestroyProfile(slowMeleeProfile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_StartsChargeUsingObstacleLane_AndStopsAtAdjacentUnit()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0);

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        new EnemyLogic(40, profile),
                    });

                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));
                var fourthTick = pipeline.RunTick(new TickInput(4));
                var enemy = GetEntity(worldState, 40);
                var player = GetEntity(worldState, 10);

                Assert.That(GetEntityAfterTick(firstTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(GetEntityAfterTick(secondTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(secondTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(thirdTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(thirdTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(player.hp, Is.EqualTo(3));
                Assert.That(secondTick.Trace.Text, Does.Contain("Reason=ChargeStart"));
                Assert.That(fourthTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_WithLocomotionCooldown_WaitsForCommittedMoves()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new EnemyLogic(40, CreateChargingEnemyProfile(moveCooldownTicks: 2)),
                });

            var firstTick = pipeline.RunTick(new TickInput(1));
            var secondTick = pipeline.RunTick(new TickInput(2));
            var thirdTick = pipeline.RunTick(new TickInput(3));
            var fourthTick = pipeline.RunTick(new TickInput(4));

            Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityAfterTick(secondTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityAfterTick(secondTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
            Assert.That(GetEntityAfterTick(secondTick, 40).enemyLocomotionCooldownTicks, Is.EqualTo(1));
            Assert.That(secondTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(GetEntityAfterTick(thirdTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(GetEntityAfterTick(thirdTick, 40).enemyLocomotionCooldownTicks, Is.EqualTo(2));
            Assert.That(GetEntityAfterTick(fourthTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(GetEntityAfterTick(fourthTick, 40).enemyLocomotionCooldownTicks, Is.EqualTo(1));
            Assert.That(fourthTick.MovementPhaseResult.RawIntents, Is.Empty);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

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
        public void WallFollowPatrolStrategy_DeadEnd_RotatesInPlaceBeforeResumingPatrol()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 2)),
                CreateWall(entityId: 91, position: new Vector2Int(2, 1)),
                CreateWall(entityId: 92, position: new Vector2Int(0, 1)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Right);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

            try
            {
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));

                Assert.That(firstTick.MovementPhaseResult.RawIntents, Is.Empty);
                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(GetEntityAfterTick(firstTick, 40).facing, Is.EqualTo(Direction.Right));
                Assert.That(GetEntityAfterTick(secondTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

            try
            {
                var result = pipeline.RunTick(new TickInput(1));
                var enemy = GetEntity(worldState, 40);

                Assert.That(result.MovementPhaseResult.SortedIntents.Select(intent => intent.SourceId).ToArray(), Is.EqualTo(new[] { 40 }));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(0, 1)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Up));
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
            var provider = GameplayEntityLogicProviderFactory.CreateDefault((EnemyAiProfile)null);
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

            Assert.That(factory.CanCreate(passiveTutorialEnemy), Is.False);
            Assert.That(EnemyParticipationPolicy.IsEnemyLogicEntity(passiveTutorialEnemy), Is.False);
            Assert.That(logicSet.AiStateLogics, Is.Empty);
            Assert.That(logicSet.PreMovementStateLogics, Is.Empty);
            Assert.That(logicSet.MovementLogics, Is.Empty);
            Assert.That(logicSet.EnemyActionStateLogics, Is.Empty);
            Assert.That(logicSet.AttackLogics, Is.Empty);

            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
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
            var profile = CreateEnemyProfile(windupTicks: 0, moveCooldownTicks: 2);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

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
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, Game.Feature.Gameplay.BoardState.TerrainData.Empty);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
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

        private static EnemyAiProfile CreateEnemyProfile(
            int windupTicks,
            int moveCooldownTicks = 0,
            int recoverTicks = 1)
        {
            return EnemyAiProfileTestFactory.CreateDefaultMelee(windupTicks, moveCooldownTicks, recoverTicks);
        }

        private static EnemyAiRuntimeDefinition CreatePhaseThroughLockedTargetDefinition(
            int windupTicks = 1,
            int recoverTicks = 1)
        {
            return new EnemyAiRuntimeDefinition(
                new EnemyAiCommonSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverTicks: recoverTicks),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                new EnemyAttackTimingSettings(windupTicks),
                EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                MovementSkillStrategyKind.PhaseThroughLockedTarget,
                EnemyJumpTimingSettings.CreateDefault(),
                ForwardPatrolStrategy.Instance,
                NearestOpponentDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                MeleeAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        private static EnemyAiProfile CreateChargingEnemyProfile(int moveCooldownTicks)
        {
            return EnemyAiProfileTestFactory.CreateCharging(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateNonAttackingEnemyProfile(int moveCooldownTicks = 0, bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.CreateNonAttacking(moveCooldownTicks, includePassiveContact);
        }

        private static EnemyAiProfile CreateDefaultMeleeProfile(
            int moveCooldownTicks = 0,
            int recoverTicks = 1,
            bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.CreateDefaultMelee(
                windupTicks: 0,
                moveCooldownTicks: moveCooldownTicks,
                recoverTicks: recoverTicks,
                includePassiveContact: includePassiveContact);
        }

        private static EnemyAiProfile CreateContactDamageProfile(int moveCooldownTicks = 0, int recoverTicks = 1)
        {
            return EnemyAiProfileTestFactory.CreateContactDamage(moveCooldownTicks, recoverTicks);
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
                playerTiming);
        }

        private static TickPipeline CreateEnemyPipeline(
            WorldState worldState,
            EnemyAiProfile profile,
            params IEntityLogic[] entityLogics)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                entityLogics ?? Array.Empty<IEntityLogic>());
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
                    entityLogics ?? Array.Empty<IEntityLogic>());
        }

        private static EnemyAiProfile CreateJumpChaserProfile(
            int windupTicks,
            int airborneTicks,
            int cooldownTicks,
            bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(windupTicks, airborneTicks, cooldownTicks),
                includePassiveContact: includePassiveContact);
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
                });
        }

        private static void DestroyProfile(EnemyAiProfile profile)
        {
            EnemyAiProfileTestFactory.Destroy(profile);
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
