using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
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
        [Category("Extended")]
        public void EnemyPhaseThroughLockedTargetQueries_TryResolveCurrentTerminalCell_RequiresSameFace()
        {
            var source = CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right);
            var lockedTarget = CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 1, 0), hp: 3);

            Assert.That(
                EnemyPhaseThroughLockedTargetQueries.TryResolveCurrentTerminalCell(
                    source,
                    lockedTarget,
                    Direction.Right,
                    out _),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPhaseThroughLockedTargetQueries_TryResolveCurrentTerminalCell_UsesCommittedStraightLineAndSingleFixedOffset()
        {
            var source = CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right);
            var adjacentLockedTarget = CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3);
            var diagonalLockedTarget = CreateUnit(entityId: 11, teamId: 1, position: new SurfaceCell(FaceId.Floor, 1, 1), hp: 3);

            Assert.That(
                EnemyPhaseThroughLockedTargetQueries.TryResolveCurrentTerminalCell(
                    source,
                    adjacentLockedTarget,
                    Direction.Right,
                    out var terminalCell),
                Is.True);
            Assert.That(terminalCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(
                EnemyPhaseThroughLockedTargetQueries.TryResolveCurrentTerminalCell(
                    source,
                    diagonalLockedTarget,
                    Direction.Right,
                    out _),
                Is.False);
            Assert.That(
                EnemyPhaseThroughLockedTargetQueries.TryResolveCurrentTerminalCell(
                    source,
                    adjacentLockedTarget,
                    Direction.Up,
                    out _),
                Is.False);
        }

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
        public void EnemyUtilitySummon_InitializesCooldown_TriggersAndResetsThroughCanonicalState()
        {
            var profile = CreateUtilitySummonProfile(initialDelayTicks: 2, cooldownTicks: 3, windupTicks: 1);
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
                var firstUtilityState = GetEnemyUtilityState(worldState, 40);

                Assert.That(firstUtilityState.EffectStates[0].cooldownTicksRemaining, Is.EqualTo(1));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);
                Assert.That(firstTick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));

                var secondTick = pipeline.RunTick(new TickInput(2));
                var secondUtilityState = GetEnemyUtilityState(worldState, 40);

                Assert.That(secondUtilityState.EffectStates[0].cooldownTicksRemaining, Is.Zero);
                Assert.That(secondUtilityState.EffectStates[0].phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
                Assert.That(secondTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);
                Assert.That(secondTick.EventLog, Has.None.Contains("SummonCommitted|Source=40|Effect=0"));

                var thirdTick = pipeline.RunTick(new TickInput(3));
                var thirdUtilityState = GetEnemyUtilityState(worldState, 40);

                Assert.That(thirdUtilityState.EffectStates[0].cooldownTicksRemaining, Is.EqualTo(3));
                Assert.That(thirdUtilityState.EffectStates[0].phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
                Assert.That(thirdTick.PresentationData.SummonWindupWarnings, Is.Empty);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out var child), Is.True);
                Assert.That(child.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(thirdTick.EventLog, Has.Some.Contains("SummonCommitted|Source=40|Effect=0"));
                Assert.That(worldState.CreateSnapshot().TryGetSummonedEntityState(41, out var summonedState), Is.True);
                Assert.That(summonedState.SourceEntityId, Is.EqualTo(40));
                Assert.That(summonedState.SourceEffectIndex, Is.EqualTo(0));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyDefinitionBindingState(41, out var bindingState), Is.True);
                Assert.That(bindingState.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));
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
        public void EnemyUtilitySummon_OffBottomExistingUtilityState_DoesNotAdvance()
        {
            var profile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 3);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new CubeTopologyState(FaceId.Floor));
            worldState.SetEnemyUtilityState(
                40,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState { cooldownTicksRemaining = 2 },
                    }));

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(GetEnemyUtilityState(worldState, 40).EffectStates[0].cooldownTicksRemaining, Is.EqualTo(2));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);
                Assert.That(tick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
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
        public void EnemyUtilitySummon_DeterministicCandidateOrder_SkipsBlockedForwardAndRight()
        {
            var profile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
            EnemyAiProfile defaultProfile = null;
            EnemyUnitArchetypeCatalog archetypeCatalog = null;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateWall(entityId: 60, position: new Vector2Int(0, -1)),
            });

            try
            {
                var pipeline = CreateSharedSummonTickPipeline(profile, worldState, out defaultProfile, out archetypeCatalog);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));

                Assert.That(worldState.CreateSnapshot().TryGetEntity(61, out var child), Is.True);
                Assert.That(child.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
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
        public void EnemyUtilitySummon_SourceKilledAfterTrigger_DoesNotSpawn()
        {
            var profile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 5, windupTicks: 1);
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
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);

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
        public void EnemyUtilitySummon_IneligibleDuringWindup_CancelsAndRestartsFullWindup()
        {
            var profile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 2, windupTicks: 1);
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

                var firstWarning = pipeline.RunTick(new TickInput(1));
                Assert.That(firstWarning.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Front, 0, 0));
                var canceledTick = pipeline.RunTick(new TickInput(2));
                var canceledState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(canceledTick.PresentationData.SummonWindupWarnings, Is.Empty);
                Assert.That(canceledState.phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
                Assert.That(canceledState.cooldownTicksRemaining, Is.EqualTo(2));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Floor, 0, 0));
                var cooldownTick = pipeline.RunTick(new TickInput(3));
                var cooldownState = GetEnemyUtilityState(worldState, 40).EffectStates[0];

                Assert.That(cooldownTick.PresentationData.SummonWindupWarnings, Is.Empty);
                Assert.That(cooldownState.phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
                Assert.That(cooldownState.cooldownTicksRemaining, Is.EqualTo(1));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);

                var restartedWarning = pipeline.RunTick(new TickInput(4));

                Assert.That(restartedWarning.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(restartedWarning.PresentationData.SummonWindupWarnings[0].ActivationSequence, Is.EqualTo(2));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);

                var committedTick = pipeline.RunTick(new TickInput(5));

                Assert.That(committedTick.PresentationData.SummonWindupWarnings, Is.Empty);
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
        public void EnemyUtilitySummon_MaxAliveChildren_BlocksAliveChild_AndIgnoresDeadChild()
        {
            var defaultProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
            });
            var profile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 5, maxAliveChildren: 1, windupTicks: 1);
            var archetypeCatalog = CreateEnemyUnitArchetypeCatalog(GetSharedSummonedArchetype());
            var blockedWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(1, 0), hp: 1, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            blockedWorld.SetSummonedEntityState(50, new SummonedEntityState(40, 0));

            var deadChildWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(1, 0), hp: 0, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            deadChildWorld.SetSummonedEntityState(50, new SummonedEntityState(40, 0));

            try
            {
                var blockedPipeline = CreateTickPipeline(defaultProfile, profile, archetypeCatalog, blockedWorld);
                blockedPipeline.RunTick(new TickInput(1));
                var blockedTick = blockedPipeline.RunTick(new TickInput(2));
                Assert.That(blockedWorld.CreateSnapshot().TryGetEntity(51, out _), Is.False);
                Assert.That(blockedTick.EventLog, Has.Some.Contains("SummonSkipped|Source=40|Effect=0|SpawnIndex=0|Reason=MaxAliveReached"));

                var deadChildPipeline = CreateTickPipeline(defaultProfile, profile, archetypeCatalog, deadChildWorld);
                deadChildPipeline.RunTick(new TickInput(1));
                deadChildPipeline.RunTick(new TickInput(2));
                Assert.That(deadChildWorld.CreateSnapshot().TryGetEntity(51, out var spawnedChild), Is.True);
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
        public void EnemyUtilitySummon_SpawnedChild_DoesNotParticipateSameTick_AndUsesArchetypeDefinitionNextTick()
        {
            var defaultProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
            });
            var archetypeProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
            });
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 1, initialAiMode: EnemyAiMode.Patrol);
            var archetypeCatalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var utilityProfile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 10, summonedArchetype: archetype, windupTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateTickPipeline(defaultProfile, utilityProfile, archetypeCatalog, worldState);

                var warningTick = pipeline.RunTick(new TickInput(1));
                Assert.That(warningTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);

                pipeline.RunTick(new TickInput(2));
                Assert.That(GetEntity(worldState, 41).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyDefinitionBindingState(41, out var bindingState), Is.True);
                Assert.That(bindingState.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));

                pipeline.RunTick(new TickInput(3));
                Assert.That(GetEntity(worldState, 41).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
                DestroyProfile(utilityProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilitySummon_ArchetypeMode_SpawnedChildGetsBinding_UsesArchetypeDefaults_AndMovesNextTick()
        {
            var defaultProfile = CreateUtilityProfile();
            var archetypeProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
            });
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var catalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var summonerProfile = CreateUtilitySummonProfile(
                initialDelayTicks: 0,
                cooldownTicks: 10,
                summonedArchetype: archetype,
                windupTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateTickPipeline(defaultProfile, summonerProfile, catalog, worldState);
                var warningTick = pipeline.RunTick(new TickInput(1));

                Assert.That(warningTick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);

                var firstTick = pipeline.RunTick(new TickInput(2));
                var snapshotAfterSpawn = worldState.CreateSnapshot();

                Assert.That(snapshotAfterSpawn.TryGetEntity(41, out var child), Is.True);
                Assert.That(child.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(child.hp, Is.EqualTo(4));
                Assert.That(child.maxHp, Is.EqualTo(4));
                Assert.That(child.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
                Assert.That(child.teamId, Is.EqualTo(2));
                Assert.That(child.facing, Is.EqualTo(Direction.Right));
                Assert.That(snapshotAfterSpawn.TryGetEnemyDefinitionBindingState(41, out var bindingState), Is.True);
                Assert.That(bindingState.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));
                Assert.That(firstTick.EventLog, Has.Some.Contains("SummonCommitted|Source=40|Effect=0|SpawnIndex=0|Spawned=41|Pos=(1,0)|Archetype=BasicMinion|Tick=2"));
                Assert.That(firstTick.EventLog, Has.None.Contains("DefinitionMode="));

                pipeline.RunTick(new TickInput(3));

                Assert.That(GetEntity(worldState, 41).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(summonerProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilitySummon_ArchetypeMode_OverrideHp_AppliesOverride_AndChildRemainsIndependentOfSource()
        {
            var defaultProfile = CreateUtilityProfile();
            var archetypeProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
            });
            var archetype = CreateEnemyUnitArchetypeAsset("HeavyMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var catalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var summonerProfile = CreateUtilitySummonProfile(
                initialDelayTicks: 0,
                cooldownTicks: 10,
                summonedArchetype: archetype,
                overrideHp: true,
                hpOverride: 6,
                windupTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var pipeline = CreateTickPipeline(defaultProfile, summonerProfile, catalog, worldState);
                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));

                var snapshotAfterSpawn = worldState.CreateSnapshot();
                Assert.That(snapshotAfterSpawn.TryGetEntity(41, out var child), Is.True);
                Assert.That(child.hp, Is.EqualTo(6));
                Assert.That(snapshotAfterSpawn.TryGetEnemyDefinitionBindingState(41, out var bindingState), Is.True);
                Assert.That(bindingState.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("HeavyMinion")));

                worldState.CreateWriteContext().ApplyDamage(40, 3);
                pipeline.RunTick(new TickInput(3));

                var snapshotAfterFollowup = worldState.CreateSnapshot();
                Assert.That(snapshotAfterFollowup.TryGetEntity(40, out _), Is.False);
                Assert.That(snapshotAfterFollowup.TryGetEntity(41, out var survivingChild), Is.True);
                Assert.That(survivingChild.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
                Assert.That(snapshotAfterFollowup.TryGetEnemyDefinitionBindingState(41, out bindingState), Is.True);
                Assert.That(bindingState.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("HeavyMinion")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(summonerProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityLockNearbyBoxes_OffBottomExistingUtilityState_DoesNotAdvance()
        {
            var profile = CreateUtilityLockNearbyBoxesProfile(
                initialDelayTicks: 0,
                cooldownTicks: 3,
                radius: 1,
                durationTicks: 2);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 1, 0), capabilities: BoxCapabilities.Push),
                },
                new CubeTopologyState(FaceId.Floor));
            worldState.SetEnemyUtilityState(
                40,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState { cooldownTicksRemaining = 2 },
                    }));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(GetEnemyUtilityState(worldState, 40).EffectStates[0].cooldownTicksRemaining, Is.EqualTo(2));
                Assert.That(worldState.CreateSnapshot().TryGetBoxInteractionLockState(20, out _), Is.False);
                Assert.That(tick.EventLog, Has.None.Contains("BoxInteractionLockApplied|Source=40"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityLockNearbyBoxes_BlocksPushSameTick_AndExpiresAfterDuration()
        {
            var profile = CreateUtilityLockNearbyBoxesProfile(
                initialDelayTicks: 0,
                cooldownTicks: 3,
                radius: 1,
                durationTicks: 2,
                blocksPush: true,
                blocksFlip: false);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3, facing: Direction.Right),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            });

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        CreateImmediatePushPlayerLogic(10, recoveryTicks: 1),
                    });

                var blockedTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
                var snapshotAfterBlockedTick = worldState.CreateSnapshot();

                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        blockedTick.MovementPhaseResult.RejectedReasons,
                        "MovementRejected",
                        "Stage=Expand",
                        "Source=10",
                        "Reason=PushTargetLocked",
                        "Cell=(1,0)",
                        "Target=20"),
                    Is.True);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        blockedTick.EventLog,
                        "PlayerActionBlockedByBoxInteractionLock",
                        "Action=Push",
                        "Actor=10",
                        "Box=20",
                        "Cell=(1,0)",
                        "Tick=1"),
                    Is.True);
                Assert.That(snapshotAfterBlockedTick.TryGetBoxInteractionLockState(20, out var lockState), Is.True);
                Assert.That(snapshotAfterBlockedTick.TryGetActiveBoxInteractionLockState(20, 1, out _), Is.True);
                Assert.That(lockState.ExpiresTickExclusive, Is.EqualTo(3));
                Assert.That(GetEnemyUtilityState(worldState, 40).EffectStates[0].cooldownTicksRemaining, Is.EqualTo(3));
                Assert.That(GetEntity(worldState, 10).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(GetEntity(worldState, 20).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        blockedTick.MovementPhaseResult.CommitEvents,
                        "PlayerActionBlockedByBoxInteractionLock",
                        "Action=Push",
                        "Actor=10",
                        "Box=20",
                        "Cell=(1,0)",
                        "Tick=1"),
                    Is.True);
                Assert.That(blockedTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
                Assert.That(snapshotAfterBlockedTick.TryGetPlayerControlState(10, out var controlStateAfterBlockedTick), Is.True);
                Assert.That(controlStateAfterBlockedTick.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
                Assert.That(controlStateAfterBlockedTick.activeAction.executionAttempted, Is.True);

                pipeline.RunTick(new TickInput(2));
                var expiryTick = pipeline.RunTick(new TickInput(3));
                var snapshotAfterExpiryTick = worldState.CreateSnapshot();

                Assert.That(snapshotAfterExpiryTick.TryGetActiveBoxInteractionLockState(20, 3, out _), Is.False);
                Assert.That(snapshotAfterExpiryTick.TryGetBoxInteractionLockState(20, out _), Is.False);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        expiryTick.EventLog,
                        "BoxInteractionLockExpired",
                        "Box=20",
                        "Expires=3",
                        "Tick=3"),
                    Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityLockNearbyBoxes_BlocksFlipSameTick_AndStartsRecovery()
        {
            var profile = CreateUtilityLockNearbyBoxesProfile(
                initialDelayTicks: 0,
                cooldownTicks: 3,
                radius: 1,
                durationTicks: 2,
                blocksPush: false,
                blocksFlip: true);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3, facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(-1, 1), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Down),
            });

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        CreateImmediateFlipPlayerLogic(10, recoveryTicks: 1),
                    });

                var blockedTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
                var snapshotAfterBlockedTick = worldState.CreateSnapshot();

                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        blockedTick.MovementPhaseResult.RejectedReasons,
                        "MovementRejected",
                        "Stage=Expand",
                        "Source=10",
                        "Reason=FlipTargetLocked",
                        "Cell=(-1,0)",
                        "Target=20"),
                    Is.True);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        blockedTick.EventLog,
                        "PlayerActionBlockedByBoxInteractionLock",
                        "Action=Flip",
                        "Actor=10",
                        "Box=20",
                        "Cell=(-1,0)",
                        "Tick=1"),
                    Is.True);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        blockedTick.MovementPhaseResult.CommitEvents,
                        "PlayerActionBlockedByBoxInteractionLock",
                        "Action=Flip",
                        "Actor=10",
                        "Box=20",
                        "Cell=(-1,0)",
                        "Tick=1"),
                    Is.True);
                Assert.That(blockedTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
                Assert.That(GetEntity(worldState, 20).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
                Assert.That(snapshotAfterBlockedTick.TryGetPlayerControlState(10, out var controlStateAfterBlockedTick), Is.True);
                Assert.That(controlStateAfterBlockedTick.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
                Assert.That(controlStateAfterBlockedTick.activeAction.executionAttempted, Is.True);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityLockNearbyBoxes_LocksOnlyNearbyBoxesOnSameFace()
        {
            var profile = CreateUtilityLockNearbyBoxesProfile(
                initialDelayTicks: 0,
                cooldownTicks: 5,
                radius: 1,
                durationTicks: 2,
                targetPattern: BoxLockTargetPattern.OrthogonalAdjacent4);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateBox(entityId: 21, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push),
                    CreateBox(entityId: 22, position: new SurfaceCell(FaceId.Floor, 0, 1), capabilities: BoxCapabilities.Push),
                    CreateBox(entityId: 23, position: new SurfaceCell(FaceId.Floor, 0, -1), capabilities: BoxCapabilities.Push),
                    CreateBox(entityId: 24, position: new SurfaceCell(FaceId.Front, 2, 1), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 60, position: new SurfaceCell(FaceId.Floor, 2, 0)),
                    CreateUnit(entityId: 61, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 2), hp: 3),
                },
                new CubeTopologyState(FaceId.Floor));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var tick = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();
                var appliedEvents = SemanticEventAssertions.FilterEvents(tick.EventLog, "BoxInteractionLockApplied");
                var diagnostics = string.Join("\n", tick.EventLog) + "\nTRACE\n" + tick.Trace.Text;

                Assert.That(appliedEvents, Has.Length.EqualTo(4), diagnostics);
                Assert.That(appliedEvents[0], Does.Contain("Box=20"));
                Assert.That(appliedEvents[1], Does.Contain("Box=21"));
                Assert.That(appliedEvents[2], Does.Contain("Box=22"));
                Assert.That(appliedEvents[3], Does.Contain("Box=23"));
                Assert.That(snapshot.TryGetBoxInteractionLockState(20, out _), Is.True);
                Assert.That(snapshot.TryGetBoxInteractionLockState(21, out _), Is.True);
                Assert.That(snapshot.TryGetBoxInteractionLockState(22, out _), Is.True);
                Assert.That(snapshot.TryGetBoxInteractionLockState(23, out _), Is.True);
                Assert.That(snapshot.TryGetBoxInteractionLockState(24, out _), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityLockNearbyBoxes_MergesLongestExpiryAndInteractionFlagsAcrossSources()
        {
            var defaultProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
            });
            var shortPushProfile = CreateUtilityLockNearbyBoxesProfile(
                initialDelayTicks: 0,
                cooldownTicks: 5,
                radius: 1,
                durationTicks: 1,
                blocksPush: true,
                blocksFlip: false,
                targetPattern: BoxLockTargetPattern.OrthogonalAdjacent4);
            var longFlipProfile = CreateUtilityLockNearbyBoxesProfile(
                initialDelayTicks: 0,
                cooldownTicks: 5,
                radius: 1,
                durationTicks: 3,
                blocksPush: false,
                blocksFlip: true,
                targetPattern: BoxLockTargetPattern.OrthogonalAdjacent4);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                CreateUnit(entityId: 50, teamId: 2, position: new Vector2Int(2, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            });

            try
            {
                var defaultDefinition = defaultProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var shortPushDefinition = shortPushProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var longFlipDefinition = longFlipProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var bootstrapper = new GameplayBootstrapper(
                    GameplayEntityLogicProviderFactory.CreateDefault(
                        defaultDefinition,
                        new Dictionary<int, EnemyAiRuntimeDefinition>
                        {
                            { 40, shortPushDefinition },
                            { 50, longFlipDefinition },
                        }));
                var pipeline = bootstrapper.CreateTickPipeline(worldState);

                var tick = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();
                var appliedEvents = SemanticEventAssertions.FilterEvents(tick.EventLog, "BoxInteractionLockApplied");
                var diagnostics = string.Join("\n", tick.EventLog) + "\nTRACE\n" + tick.Trace.Text;

                Assert.That(appliedEvents, Has.Length.EqualTo(1), diagnostics);
                Assert.That(snapshot.TryGetBoxInteractionLockState(20, out var lockState), Is.True);
                Assert.That(lockState.ExpiresTickExclusive, Is.EqualTo(4));
                Assert.That(lockState.BlocksPush, Is.True);
                Assert.That(lockState.BlocksFlip, Is.True);
                Assert.That(lockState.SourceEntityId, Is.EqualTo(50));
                Assert.That(lockState.SourceEffectIndex, Is.EqualTo(0));
            }
            finally
            {
                DestroyProfile(defaultProfile);
                DestroyProfile(shortPushProfile);
                DestroyProfile(longFlipProfile);
            }
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
            Assert.That(executeTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(executeTick.Trace.Text, Does.Contain("PhaseEnter|Entity=40|Tick=2|Owner=EnemyPreMovement|Rule=LockedTargetCrossThrough"));
            Assert.That(executeTick.Trace.Text, Does.Contain("EnemyPhaseRelocation|E=40|Label=Committed|Target=10"));
            Assert.That(executeTick.Trace.Text, Does.Contain("Kind=SetPhasedState"));
            Assert.That(executeTick.Trace.Text, Does.Contain("Timing=EnemyLockedTargetCrossThroughValidator"));
            Assert.That(executeTick.Trace.Text, Does.Contain("ReservationRead=CellOnlyPreSettle"));
            Assert.That(executeTick.Trace.Text, Does.Contain("Settle=AnchoredLikeDefault(StageDefault)"));
            Assert.That(executeTick.Trace.Text, Does.Contain("OccupancyClaim=True(StageDefault)"));
            Assert.That(executeTick.Trace.Text, Does.Contain("ExistingEnemyLock=Retained(StageScopedContract)"));

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
        public void EnemyAi_PhaseThroughLockedTargetValidator_TerminalBlocked_DoesNotChooseAlternateOpenCellOrRetargetAlternateHostile()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 11, teamId: 1, position: new Vector2Int(0, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateWall(entityId: 70, position: new Vector2Int(2, 0)),
            });
            var pipeline = CreateEnemyPipeline(worldState, CreatePhaseThroughLockedTargetDefinition(windupTicks: 1));

            pipeline.RunTick(new TickInput(1));
            var executeTick = pipeline.RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
            Assert.That(GetEntity(worldState, 11).hp, Is.EqualTo(3));
            Assert.That(executeTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(executeTick.Trace.Text, Does.Contain("EnemyPhaseRelocation|E=40|Label=Rejected|Target=10"));
            Assert.That(executeTick.Trace.Text, Does.Contain("Result=SettleBlocked"));
            Assert.That(executeTick.Trace.Text, Does.Not.Contain("EnemyPhaseRelocation|E=40|Label=Committed|Target=11"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PhaseThroughLockedTargetValidator_LosingLockedTarget_DoesNotReacquireAlternateHostileOrOpenPhase()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 11, teamId: 1, position: new Vector2Int(0, 2), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = CreateEnemyPipeline(worldState, CreatePhaseThroughLockedTargetDefinition(windupTicks: 1));

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().ApplyDamage(10, 3);

            var executeTick = pipeline.RunTick(new TickInput(2));
            var actionState = GetEnemyActionState(worldState, 40);

            Assert.That(GetEntity(worldState, 11).hp, Is.EqualTo(3));
            Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
            Assert.That(actionState.IsActive, Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(40, out _), Is.False);
            Assert.That(executeTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(executeTick.Trace.Text, Does.Not.Contain("PhaseEnter|Entity=40|Tick=2|Owner=EnemyPreMovement"));
            Assert.That(executeTick.Trace.Text, Does.Not.Contain("EnemyPhaseRelocation|E=40"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_PhaseThroughLockedTargetValidator_GeometryChange_RejectsWithoutFallbackAttackOrMovement()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = CreateEnemyPipeline(worldState, CreatePhaseThroughLockedTargetDefinition(windupTicks: 1));

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 0, 1));

            var executeTick = pipeline.RunTick(new TickInput(2));
            var actionState = GetEnemyActionState(worldState, 40);

            Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
            Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(actionState.IsActive, Is.True);
            Assert.That(actionState.executionAttempted, Is.True);
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(40, out _), Is.False);
            Assert.That(executeTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(executeTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(executeTick.Trace.Text, Does.Not.Contain("PhaseEnter|Entity=40|Tick=2|Owner=EnemyPreMovement"));
            Assert.That(executeTick.Trace.Text, Does.Not.Contain("EnemyPhaseRelocation|E=40"));
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
        public void EnemyAi_WindupForwardBaseline_First3Ticks_MatchPinnedPatrolChaseWindupSequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, CreateEnemyProfile(windupTicks: 1));
            var summaries = new List<string>();

            for (var tickIndex = 1; tickIndex <= 3; tickIndex++)
            {
                summaries.Add(SummarizeEnemyTick(pipeline.RunTick(new TickInput(tickIndex)), 40));
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "Pos=Floor(1,0)|Facing=Right|Mode=Chase|Timer=0|Move=True|Attack=False|Reasons=",
                    "Pos=Floor(2,0)|Facing=Right|Mode=Attack|Timer=0|Move=True|Attack=False|Reasons=",
                    "Pos=Floor(2,0)|Facing=Right|Mode=Attack|Timer=0|Move=False|Attack=False|Reasons=",
                },
                summaries);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupRandomWalkPilot_DirectLane_MatchesExactTransitionTicks()
        {
            var controlProfile = CreateEnemyProfile(windupTicks: 1);
            var baselineProfile = CreateEnemyProfile(windupTicks: 1);
            var pilotProfile = CreateWindupRandomWalkPilotProfile(windupTicks: 1);
            var controlWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var baselineWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pilotWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            try
            {
                var expectedRecoverTicks = ResolveRecoverTicks(controlProfile);
                AssertWindupAttackControlGreen(
                    RunWindupContractMetrics(controlWorld, controlProfile, ticks: 4),
                    "forward baseline self-check",
                    expectedRecoverTicks);
                var comparison = RunWindupParityComparison(baselineWorld, baselineProfile, pilotWorld, pilotProfile, ticks: 6);
                var baselineMetrics = comparison.Baseline;
                var pilotMetrics = comparison.Pilot;
                TestContext.Progress.WriteLine($"WindupGateSummary|Label=baseline direct-lane|{BuildWindupMetricsSummary(baselineMetrics)}");
                TestContext.Progress.WriteLine($"WindupGateSummary|Label=pilot direct-lane|{BuildWindupMetricsSummary(pilotMetrics)}");
                AssertWindupProbeComplete(baselineMetrics, "baseline direct-lane", ResolveRecoverTicks(baselineProfile));
                AssertWindupProbeComplete(pilotMetrics, "pilot direct-lane", ResolveRecoverTicks(pilotProfile));

                Assert.That(pilotMetrics.TargetSensedTick, Is.EqualTo(baselineMetrics.TargetSensedTick));
                Assert.That(pilotMetrics.TargetInRangeTick, Is.EqualTo(baselineMetrics.TargetInRangeTick));
                Assert.That(pilotMetrics.AttackEntryTick, Is.EqualTo(baselineMetrics.AttackEntryTick));
                Assert.That(pilotMetrics.ActionStartTick, Is.EqualTo(baselineMetrics.ActionStartTick));
                Assert.That(pilotMetrics.AttackExecuteTick, Is.EqualTo(baselineMetrics.AttackExecuteTick));
                Assert.That(pilotMetrics.AttackExecuteActionStateActiveTick, Is.EqualTo(baselineMetrics.AttackExecuteActionStateActiveTick));
                Assert.That(pilotMetrics.AttackExecuteExecutionAttemptedTick, Is.EqualTo(baselineMetrics.AttackExecuteExecutionAttemptedTick));
                Assert.That(pilotMetrics.AttackCommittedTraceTick, Is.EqualTo(baselineMetrics.AttackCommittedTraceTick));
                Assert.That(pilotMetrics.AttackCommittedRecoverTick, Is.EqualTo(baselineMetrics.AttackCommittedRecoverTick));
                Assert.That(pilotMetrics.RecoverEntryTick, Is.EqualTo(baselineMetrics.RecoverEntryTick));
                Assert.That(pilotMetrics.RecoverCompleteTick, Is.EqualTo(baselineMetrics.RecoverCompleteTick));
                Assert.That(pilotMetrics.RecoverTickCount, Is.EqualTo(baselineMetrics.RecoverTickCount));
                Assert.That(pilotMetrics.RecoverPatrolWriteCount, Is.EqualTo(baselineMetrics.RecoverPatrolWriteCount));
                Assert.That(pilotMetrics.FirstCombatDamageTick, Is.EqualTo(baselineMetrics.FirstCombatDamageTick));
                Assert.That(
                    pilotMetrics.AttackCommittedTick - pilotMetrics.AttackEntryTick,
                    Is.EqualTo(baselineMetrics.AttackCommittedTick - baselineMetrics.AttackEntryTick));
                Assert.That(
                    pilotMetrics.RecoverEntryTick - pilotMetrics.AttackCommittedTick,
                    Is.EqualTo(baselineMetrics.RecoverEntryTick - baselineMetrics.AttackCommittedTick));
                Assert.That(
                    pilotMetrics.RecoverCompleteTick - pilotMetrics.RecoverEntryTick,
                    Is.EqualTo(baselineMetrics.RecoverCompleteTick - baselineMetrics.RecoverEntryTick));
                Assert.That(comparison.FirstDivergentPositionOrFacingTick, Is.Zero);
            }
            finally
            {
                DestroyProfile(controlProfile);
                DestroyProfile(baselineProfile);
                DestroyProfile(pilotProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupRandomWalkPilot_OpenRoomOffset_DoesNotAdvanceAggressionEarlierThanBaseline()
        {
            var controlProfile = CreateEnemyProfile(windupTicks: 1);
            var baselineProfile = CreateEnemyProfile(windupTicks: 1);
            var pilotProfile = CreateWindupRandomWalkPilotProfile(windupTicks: 1);
            var bounds = new BoardBounds(Vector2Int.zero, new Vector2Int(5, 5));
            var controlWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var baselineWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 3), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 2), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            }, bounds);
            var pilotWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 3), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 2), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            }, bounds);

            try
            {
                var expectedRecoverTicks = ResolveRecoverTicks(controlProfile);
                AssertWindupAttackControlGreen(
                    RunWindupContractMetrics(controlWorld, controlProfile, ticks: 4),
                    "forward baseline self-check",
                    expectedRecoverTicks);
                var comparison = RunWindupParityComparison(baselineWorld, baselineProfile, pilotWorld, pilotProfile, ticks: 10);
                var baselineMetrics = comparison.Baseline;
                var pilotMetrics = comparison.Pilot;
                TestContext.Progress.WriteLine($"WindupGateSummary|Label=baseline open-room|{BuildWindupMetricsSummary(baselineMetrics)}");
                TestContext.Progress.WriteLine($"WindupGateSummary|Label=pilot open-room|{BuildWindupMetricsSummary(pilotMetrics)}");
                AssertWindupProbeComplete(baselineMetrics, "baseline open-room", ResolveRecoverTicks(baselineProfile));
                AssertWindupProbeComplete(pilotMetrics, "pilot open-room", ResolveRecoverTicks(pilotProfile));

                AssertLaterOnlyBoundedDrift(baselineMetrics.TargetSensedTick, pilotMetrics.TargetSensedTick, "TargetSensed");
                AssertLaterOnlyBoundedDrift(baselineMetrics.TargetInRangeTick, pilotMetrics.TargetInRangeTick, "TargetInRange");
                AssertLaterOnlyBoundedDrift(baselineMetrics.AttackEntryTick, pilotMetrics.AttackEntryTick, "AttackEntry");
                AssertLaterOnlyBoundedDrift(baselineMetrics.ActionStartTick, pilotMetrics.ActionStartTick, "ActionStart");
                AssertLaterOnlyBoundedDrift(baselineMetrics.AttackExecuteTick, pilotMetrics.AttackExecuteTick, "AttackExecute");
                AssertLaterOnlyBoundedDrift(baselineMetrics.AttackExecuteActionStateActiveTick, pilotMetrics.AttackExecuteActionStateActiveTick, "AttackExecuteActionStateActive");
                AssertLaterOnlyBoundedDrift(baselineMetrics.AttackExecuteExecutionAttemptedTick, pilotMetrics.AttackExecuteExecutionAttemptedTick, "AttackExecuteExecutionAttempted");
                AssertLaterOnlyBoundedDrift(baselineMetrics.AttackCommittedTraceTick, pilotMetrics.AttackCommittedTraceTick, "AttackCommittedTrace");
                AssertLaterOnlyBoundedDrift(baselineMetrics.AttackCommittedRecoverTick, pilotMetrics.AttackCommittedRecoverTick, "AttackCommittedRecover");
                Assert.That(pilotMetrics.RecoverTickCount, Is.EqualTo(baselineMetrics.RecoverTickCount));
                Assert.That(pilotMetrics.RecoverPatrolWriteCount, Is.EqualTo(baselineMetrics.RecoverPatrolWriteCount));
                AssertLaterOnlyBoundedDrift(baselineMetrics.FirstCombatDamageTick, pilotMetrics.FirstCombatDamageTick, "FirstCombatDamage");
                Assert.That(
                    pilotMetrics.AttackCommittedTick - pilotMetrics.AttackEntryTick,
                    Is.EqualTo(baselineMetrics.AttackCommittedTick - baselineMetrics.AttackEntryTick));
                Assert.That(
                    pilotMetrics.RecoverEntryTick - pilotMetrics.AttackCommittedTick,
                    Is.EqualTo(baselineMetrics.RecoverEntryTick - baselineMetrics.AttackCommittedTick));
                Assert.That(
                    pilotMetrics.RecoverCompleteTick - pilotMetrics.RecoverEntryTick,
                    Is.EqualTo(baselineMetrics.RecoverCompleteTick - baselineMetrics.RecoverEntryTick));
                if (comparison.FirstDivergentPositionOrFacingTick != 0)
                {
                    Assert.That(
                        comparison.FirstDivergentPositionOrFacingTick,
                        Is.GreaterThanOrEqualTo(baselineMetrics.TargetSensedTick),
                        "Position/facing divergence must not predate the first baseline sense tick.");
                }
            }
            finally
            {
                DestroyProfile(controlProfile);
                DestroyProfile(baselineProfile);
                DestroyProfile(pilotProfile);
            }
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
        public void EnemyAi_NonAttackingRandomWalkPilot_OpenRoom_VisitsMultipleCellsAndKeepsMoving()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(2, 2), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));
            var profile = CreateNonAttackingEnemyProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var visited = new HashSet<Vector2Int>();
                var moveCommittedCount = 0;

                for (var tickIndex = 1; tickIndex <= 40; tickIndex++)
                {
                    var tick = pipeline.RunTick(new TickInput(tickIndex));
                    visited.Add(GetEntity(worldState, 40).position.PlanarPosition);

                    if (SemanticEventAssertions.ContainsEvent(tick.EventLog, "MoveCommitted", "E=40"))
                    {
                        moveCommittedCount++;
                    }
                }

                Assert.That(visited.Count, Is.GreaterThanOrEqualTo(5));
                Assert.That(moveCommittedCount, Is.EqualTo(40));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_NonAttackingRandomWalkPilot_First10Ticks_MatchPinnedSequence()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(2, 2), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));
            var profile = CreateNonAttackingEnemyProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var summaries = new List<string>();

                for (var tickIndex = 1; tickIndex <= 10; tickIndex++)
                {
                    summaries.Add(SummarizeEnemyTick(pipeline.RunTick(new TickInput(tickIndex)), 40));
                }

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Pos=Floor(2,3)|Facing=Up|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(2,4)|Facing=Up|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(2,3)|Facing=Down|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(2,2)|Facing=Down|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(2,1)|Facing=Down|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(3,1)|Facing=Right|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(3,2)|Facing=Up|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(3,3)|Facing=Up|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(2,3)|Facing=Left|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                        "Pos=Floor(1,3)|Facing=Left|Mode=Patrol|Timer=0|Move=True|Attack=False|Reasons=",
                    },
                    summaries);
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
                    CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), hp: 1),
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
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
        public void EnemyAi_NonAttackingRandomWalkPilot_AfterLosingTarget_ReturnsTowardHomeThenResumesPatrol()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 8, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: homeCell, hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(8, 0)));
            var profile = CreateNonAttackingEnemyProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                pipeline.RunTick(new TickInput(2));
                pipeline.RunTick(new TickInput(3));

                Assert.That(GetEntity(worldState, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 5, 0)));
                Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));

                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Front, 0, 0));

                var returnTick1 = pipeline.RunTick(new TickInput(4));
                var returnTick2 = pipeline.RunTick(new TickInput(5));

                Assert.That(returnTick1.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Chase|FromTimer=0|To=Patrol|ToTimer=0|Reason=NoTarget"));
                Assert.That(GetEntityAfterTick(returnTick1, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
                Assert.That(GetEntityAfterTick(returnTick2, 40).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 0)));
                Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Patrol));
                Assert.That(GetPlanarDistance(homeCell, GetEntityAfterTick(returnTick1, 40).position), Is.EqualTo(2));
                Assert.That(GetPlanarDistance(homeCell, GetEntityAfterTick(returnTick2, 40).position), Is.EqualTo(1));

                var resumedPatrolCells = new HashSet<Vector2Int>();
                for (var tickIndex = 6; tickIndex <= 15; tickIndex++)
                {
                    var tick = pipeline.RunTick(new TickInput(tickIndex));
                    Assert.That(SemanticEventAssertions.ContainsEvent(tick.EventLog, "MoveCommitted", "E=40"), Is.True);
                    resumedPatrolCells.Add(GetEntity(worldState, 40).position.PlanarPosition);
                }

                Assert.That(resumedPatrolCells.Count, Is.GreaterThanOrEqualTo(3));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupRandomWalkPilot_LoseTargetDuringWindup_ReturnsHomeThenResumesPatrol()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var controlProfile = CreateEnemyProfile(windupTicks: 2);
            var profile = CreateWindupRandomWalkPilotProfile(windupTicks: 2, includePassiveContact: false);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 3, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: homeCell, hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(5, 0)));

            try
            {
                AssertLockedTargetLostControlGreen(
                    RunLockedTargetLostControlProbe(controlProfile),
                    "forward locked-target-lost self-check",
                    EnemyAiMode.Patrol);
                // Keep the bounded lose-target window free of player respawn reacquisition so the
                // scenario measures cancel -> home-return -> resumed patrol, not post-respawn chase.
                var pipeline = CreateEnemyPipelineWithRespawnDelay(worldState, profile, playerRespawnDelayTicks: 32);

                var tick1 = pipeline.RunTick(new TickInput(1));
                var activeWindupTick = default(TickResult);
                var activeWindupTickIndex = 0;
                for (var tickIndex = 2; tickIndex <= 6; tickIndex++)
                {
                    var tick = pipeline.RunTick(new TickInput(tickIndex));
                    var actionSignals = tick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
                    var hasActiveActionState = worldState.CreateSnapshot().TryGetEnemyActionState(40, out var actionState) && actionState.IsActive;
                    if (actionSignals.Any(signal => signal.StartedThisTick) || hasActiveActionState)
                    {
                        activeWindupTick = tick;
                        activeWindupTickIndex = tickIndex;
                        break;
                    }
                }

                Assert.That(activeWindupTickIndex, Is.GreaterThan(0), "pilot lose-target fixture never entered active windup within the bounded probe window");
                worldState.CreateWriteContext().ApplyDamage(10, 3);

                var cancelTickIndex = activeWindupTickIndex + 1;
                var cancelTick = pipeline.RunTick(new TickInput(cancelTickIndex));
                var cancelSignals = cancelTick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
                var cancelActionState = GetEnemyActionState(worldState, 40);
                var cancelOwnerTick = cancelSignals.Any(signal => signal.CanceledThisTick) &&
                                      !cancelActionState.IsActive &&
                                      GetEntityAfterTick(cancelTick, 40).aiMode == EnemyAiMode.Patrol
                    ? cancelTickIndex
                    : 0;
                var cancelTraceTick = cancelTick.Trace.Text.Contains("LockedTargetLost", StringComparison.Ordinal)
                    ? cancelTickIndex
                    : 0;
                var returnTick1 = pipeline.RunTick(new TickInput(cancelTickIndex + 1));
                var returnTick2 = pipeline.RunTick(new TickInput(cancelTickIndex + 2));

                Assert.That(tick1.Trace.Text, Does.Contain("EnemyPatrolStateUpdated|E=40|Label=Initialized"));
                var cancelDistance = GetPlanarDistance(homeCell, GetEntityAfterTick(cancelTick, 40).position);
                var returnDistance1 = GetPlanarDistance(homeCell, GetEntityAfterTick(returnTick1, 40).position);
                var returnDistance2 = GetPlanarDistance(homeCell, GetEntityAfterTick(returnTick2, 40).position);
                var metrics = new LockedTargetLostMetrics(
                    activeWindupTickIndex,
                    cancelOwnerTick,
                    cancelTraceTick,
                    GetEntityAfterTick(cancelTick, 40).aiMode,
                    new[] { cancelDistance, returnDistance1, returnDistance2 });
                TestContext.Progress.WriteLine($"LockedTargetLostGateSummary|Label=pilot lose-target active-windup|{BuildLockedTargetLostMetricsSummary(metrics)}");

                AssertLockedTargetLostEntryGreen(metrics, "pilot lose-target active-windup");
                AssertLockedTargetLostCancelOwnerGreen(metrics, "pilot lose-target active-windup", EnemyAiMode.Patrol);
                AssertLockedTargetLostWordingGreen(metrics, "pilot lose-target active-windup");
                AssertLockedTargetLostHomeReturnGreen(metrics, "pilot lose-target active-windup", leashRadius: 1);

                var resumedPatrolCells = new HashSet<Vector2Int>();
                for (var tickIndex = cancelTickIndex + 3; tickIndex <= cancelTickIndex + 9; tickIndex++)
                {
                    var tick = pipeline.RunTick(new TickInput(tickIndex));
                    var enemy = GetEntityAfterTick(tick, 40);
                    resumedPatrolCells.Add(enemy.position.PlanarPosition);
                    Assert.That(GetPlanarDistance(homeCell, enemy.position), Is.LessThanOrEqualTo(1));
                }

                Assert.That(resumedPatrolCells.Count, Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                DestroyProfile(controlProfile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
        [Category("Core")]
        public void EnemyMovesIntoPlayer_FlagOff_LegacyImmediateContact()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var result = pipeline.RunTick(new TickInput(1));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "MoveCommitted",
                        "E=40",
                        "To=(0,0)",
                        "Facing=Left"),
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
        public void EnemyMovesIntoPlayer_FlagOff_ViewStillMoving_LogicAlreadyContact()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
                LegacyMovementBoundaryAssert.HasLegacyFallbackMove(result, 40);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyMovesIntoPlayer_Kinematic_NoContactBeforeCommit()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

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
        public void EnemyMovesIntoPlayer_Kinematic_ContactAtCommit()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateContactDamageProfile();

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
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_NoContactBeforeCommit()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateGlideContactDamageProfile(durationTicks: 20);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 20, durationTicks: 20, recoveryTicks: 1, cooldownTicks: 0));

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);

                for (var tick = 1; tick <= 9; tick++)
                {
                    var result = pipeline.RunTick(new TickInput(tick));
                    var snapshot = worldState.CreateSnapshot();

                    Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                    Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                    Assert.That(enemy.position, Is.EqualTo(enemySourceCell), BuildContactTimingDebug(tick, "EnemyGlide", 40, 10, snapshot, result));
                    Assert.That(player.position, Is.EqualTo(playerCell));
                    Assert.That(snapshot.TryGetUnitKinematicState(40, out var enemyKinematic), Is.True);
                    Assert.That(enemyKinematic.mode, Is.EqualTo(MotionMode.Voluntary));
                    Assert.That(
                        HasAcceptedPassiveContact(result, 40, 10),
                        Is.False,
                        BuildContactTimingDebug(tick, "EnemyGlide", 40, 10, snapshot, result));
                    Assert.That(player.hp, Is.EqualTo(3));
                    LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(result, 40);
                }
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_ContactAtCommit()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var enemySourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: playerCell, hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: enemySourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
            });
            var profile = CreateGlideContactDamageProfile(durationTicks: 20);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 20, durationTicks: 20, recoveryTicks: 1, cooldownTicks: 0));

            try
            {
                var pipeline = CreateEnemyPipeline(
                    worldState,
                    profile,
                    GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);

                TickResult result = null;
                for (var tick = 1; tick <= 10; tick++)
                {
                    result = pipeline.RunTick(new TickInput(tick));
                }

                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(playerCell), BuildContactTimingDebug(10, "EnemyGlide", 40, 10, snapshot, result));
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
                    "GlideActiveKinematicAnchorCommit");
                Assert.That(
                    HasAcceptedPassiveContact(result, 40, 10),
                    Is.True,
                    BuildContactTimingDebug(10, "EnemyGlide", 40, 10, snapshot, result));
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
        public void GlideActive_Kinematic_HitNonlethal_CleansUpWithoutStaleHover()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateGlideContactDamageProfile(durationTicks: 20);
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
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Floor, 0, 1), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 1, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = CreateGlideContactDamageProfile(durationTicks: 20);
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
            var profile = CreateGlideContactDamageProfile(durationTicks: 4);

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
            var profile = CreateContactDamageProfile();

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
            var profile = CreateContactDamageProfile();

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
        public void EnemyCharge_NotMigrated()
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
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
                Assert.That(result.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.False);
                Assert.That(
                    result.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40 &&
                        motion.MotionKind == TickEntityMotionKind.ChargeMove),
                    Is.True);
                LegacyMovementBoundaryAssert.HasLegacyChargeMove(result, 40);
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
                        motion.EntityId == 40 &&
                        motion.MotionKind == TickEntityMotionKind.ChargeMove),
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
        public void EnemyCharge_ChargeKinematicFlagOff_PatrolToChargeWaitsForOrdinarySettleThenUsesLegacyChargeMove()
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
                        motion.EntityId == 40 &&
                        motion.MotionKind == TickEntityMotionKind.ChargeMove),
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
                    Is.EqualTo(new Vector2Int(2, 0)),
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    thirdTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40),
                    Is.False,
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
                Assert.That(
                    thirdTick.PresentationData.EntityMotions.Any(motion =>
                        motion.EntityId == 40 &&
                        motion.MotionKind == TickEntityMotionKind.ChargeMove),
                    Is.True,
                    BuildChargeKinematicDebug(3, worldState, thirdTick));
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
                        motion.EntityId == 40 &&
                        motion.MotionKind == TickEntityMotionKind.ChargeMove),
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
                        motion.EntityId == 40 &&
                        motion.MotionKind == TickEntityMotionKind.ChargeMove),
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
        public void EnemyPhase_NotMigrated()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = CreateEnemyPipeline(
                worldState,
                CreatePhaseThroughLockedTargetDefinition(windupTicks: 1),
                GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);

            pipeline.RunTick(new TickInput(1));
            var result = pipeline.RunTick(new TickInput(2));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(snapshot.TryGetUnitKinematicState(40, out _), Is.False);
            Assert.That(result.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("EnemyPhaseRelocation|E=40|Label=Committed"));
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
            var profile = CreateContactDamageProfile();

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
            var profile = CreateContactDamageProfile();

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
        public void EnemyAi_WindupRandomWalkPilot_PrimedSameCell_PreservesCombatThenPassiveOrdering()
        {
            var baselineProfile = CreateDefaultMeleeProfile(includePassiveContact: true);
            var pilotProfile = CreateWindupRandomWalkPilotProfile(includePassiveContact: true);

            try
            {
                var baselineResult = RunPrimedSameCellCombatPassiveTick(baselineProfile);
                var pilotResult = RunPrimedSameCellCombatPassiveTick(pilotProfile);

                CollectionAssert.AreEqual(
                    baselineResult.AttackPhaseResult.RawIntents.Select(intent => (intent.SourceKind, intent.LocalSequence)).ToArray(),
                    pilotResult.AttackPhaseResult.RawIntents.Select(intent => (intent.SourceKind, intent.LocalSequence)).ToArray());
                CollectionAssert.AreEqual(
                    new[]
                    {
                        (AttackSourceKind.Combat, 0),
                        (AttackSourceKind.PassiveContact, 1),
                    },
                    pilotResult.AttackPhaseResult.RawIntents.Select(intent => (intent.SourceKind, intent.LocalSequence)).ToArray());
                CollectionAssert.AreEqual(
                    new[]
                    {
                        AttackSourceKind.Combat,
                        AttackSourceKind.PassiveContact,
                    },
                    pilotResult.AttackPhaseResult.DamageResolutions.Select(record => record.SourceKind).ToArray());
                Assert.That(pilotResult.AttackPhaseResult.DamageResolutions[0].Accepted, Is.True);
                Assert.That(pilotResult.AttackPhaseResult.DamageResolutions[1].Accepted, Is.False);
                Assert.That(pilotResult.AttackPhaseResult.DamageResolutions[1].RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
                Assert.That(GetEntityAfterTick(pilotResult, 10).hp, Is.EqualTo(4));

                var passiveOnlyWorld = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                });
                var passiveOnlyPipeline = CreateEnemyPipeline(passiveOnlyWorld, pilotProfile, playerDamageCooldownTicks: 1);
                var passiveOnlyTick = passiveOnlyPipeline.RunTick(new TickInput(1));

                CollectionAssert.AreEqual(
                    new[]
                    {
                        AttackSourceKind.PassiveContact,
                    },
                    passiveOnlyTick.AttackPhaseResult.RawIntents.Select(intent => intent.SourceKind).ToArray());
                Assert.That(
                    passiveOnlyTick.AttackPhaseResult.DamageResolutions.Count(record => record.Accepted),
                    Is.EqualTo(1));
                Assert.That(GetEntityAfterTick(passiveOnlyTick, 10).hp, Is.EqualTo(4));
            }
            finally
            {
                DestroyProfile(baselineProfile);
                DestroyProfile(pilotProfile);
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
        public void EnemyAi_ChargingProfile_AllUnitsTransparentUntilSolid_AndOnlyPlayerTakesDamage()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateUnit(entityId: 60, teamId: 2, position: new Vector2Int(2, 0), hp: 3),
                    CreateUnit(entityId: 70, teamId: 3, position: new Vector2Int(3, 0), hp: 3),
                    CreateUnit(entityId: 80, teamId: 3, position: new Vector2Int(6, 0), hp: 3),
                    CreateBox(entityId: 90, position: new Vector2Int(7, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(7, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var ticks = Enumerable.Range(1, 7)
                    .Select(tick => pipeline.RunTick(new TickInput(tick)))
                    .ToArray();
                var snapshotAfter = worldState.CreateSnapshot();
                var stackedUnits = new List<EntityState>();
                var allDamageResolutions = ticks
                    .SelectMany(tick => tick.AttackPhaseResult.DamageResolutions)
                    .ToArray();
                var enemy = GetEntity(worldState, 40);
                var player = GetEntity(worldState, 10);
                var alliedUnit = GetEntity(worldState, 60);
                var nonPlayerLaneUnit = GetEntity(worldState, 70);
                var nonPlayerTerminalUnit = GetEntity(worldState, 80);

                Assert.That(GetEntityAfterTick(ticks[0], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(ticks[1], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[1], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[2], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(GetEntityAfterTick(ticks[3], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(4, 0)));
                Assert.That(GetEntityAfterTick(ticks[4], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(5, 0)));
                Assert.That(GetEntityAfterTick(ticks[5], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(6, 0)));
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(6, 0)));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(player.hp, Is.EqualTo(4));
                Assert.That(alliedUnit.hp, Is.EqualTo(3));
                Assert.That(nonPlayerLaneUnit.hp, Is.EqualTo(3));
                Assert.That(nonPlayerTerminalUnit.hp, Is.EqualTo(3));
                Assert.That(allDamageResolutions.Count(record => record.Accepted), Is.EqualTo(1));
                Assert.That(allDamageResolutions.Select(record => record.TargetId).Distinct().ToArray(), Is.EqualTo(new[] { 10 }));
                Assert.That(allDamageResolutions.Single(record => record.Accepted).SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(ticks[1].Trace.Text, Does.Contain("Reason=ChargeStart"));

                snapshotAfter.EnumerateUnitsAt(new Vector2Int(6, 0), stackedUnits);
                CollectionAssert.AreEquivalent(new[] { 40, 80 }, stackedUnits.Select(entity => entity.entityId).ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_AdjacentPlayerAndSolidDownLane_DoesNotFreezeAndContinuesPastPlayer()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(4, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var secondTick = pipeline.RunTick(new TickInput(2));
                var thirdTick = pipeline.RunTick(new TickInput(3));
                var fourthTick = pipeline.RunTick(new TickInput(4));

                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(secondTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(secondTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(thirdTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(thirdTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(GetEntityAfterTick(fourthTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(secondTick.Trace.Text, Does.Contain("Reason=ChargeStart"));
                Assert.That(secondTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
            }
            finally
            {
                DestroyProfile(profile);
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
        public void EnemyAi_ChargingProfile_WithoutPassiveContact_PassesThroughPlayerWithoutDamage()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, includePassiveContact: false);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var ticks = Enumerable.Range(1, 6)
                    .Select(tick => pipeline.RunTick(new TickInput(tick)))
                    .ToArray();

                Assert.That(GetEntityAfterTick(ticks[1], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[3], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(4, 0)));
                Assert.That(GetEntityAfterTick(ticks[4], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(5, 0)));
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(5));
                Assert.That(ticks.SelectMany(tick => tick.AttackPhaseResult.DamageResolutions), Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_WithWindup_StartsChargeWithoutImmediateMoveOrPassiveContact()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(4, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, windupTicks: 1);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var windupTick = pipeline.RunTick(new TickInput(2));
                var windupState = GetEnemyChargeState(worldState, 40);
                var activeTick = pipeline.RunTick(new TickInput(3));
                var activeState = GetEnemyChargeState(worldState, 40);

                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(windupTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(windupTick, 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(windupTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(windupState.phase, Is.EqualTo(EnemyChargePhase.Windup));
                Assert.That(windupState.lockedDirection, Is.EqualTo(Direction.Right));
                Assert.That(windupState.remainingActiveSteps, Is.EqualTo(2));
                Assert.That(windupState.recoverRemainingTicks, Is.Zero);
                Assert.That(windupTick.Trace.Text, Does.Contain("Reason=ChargeStart"));
                Assert.That(windupTick.AttackPhaseResult.DamageResolutions, Is.Empty);

                Assert.That(GetEntityAfterTick(activeTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(activeTick, 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(activeTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(activeState.phase, Is.EqualTo(EnemyChargePhase.Active));
                Assert.That(activeState.lockedDirection, Is.EqualTo(Direction.Right));
                Assert.That(activeState.remainingActiveSteps, Is.EqualTo(1));
                Assert.That(activeState.recoverRemainingTicks, Is.Zero);
                Assert.That(activeTick.AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(GetEntityHp(worldState, 10), Is.EqualTo(4));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_WithRecover_SuppressesPassiveContactWhileStacked()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(4, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, windupTicks: 0, recoverTicks: 2);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var ticks = Enumerable.Range(1, 5)
                    .Select(tick => pipeline.RunTick(new TickInput(tick)))
                    .ToArray();
                var recoverState = GetEnemyChargeState(worldState, 40);

                Assert.That(GetEntityAfterTick(ticks[1], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[2], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(GetEntityAfterTick(ticks[2], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[2], 40).aiStateTimer, Is.Zero);
                Assert.That(ticks[2].AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);

                Assert.That(GetEntityAfterTick(ticks[3], 40).aiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(GetEntityAfterTick(ticks[3], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[3], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(GetEntityAfterTick(ticks[4], 40).aiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(GetEntityAfterTick(ticks[4], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[4], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(recoverState.phase, Is.EqualTo(EnemyChargePhase.Recover));
                Assert.That(recoverState.remainingActiveSteps, Is.Zero);
                Assert.That(recoverState.recoverRemainingTicks, Is.EqualTo(1));
                Assert.That(ticks[3].Trace.Text, Does.Contain("Reason=ChargeComplete"));
                Assert.That(ticks[3].AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(ticks[4].AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(GetEntityHp(worldState, 10), Is.EqualTo(4));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_WithWindup_LocksDirectionUntilActivation()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(5, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(5, 1)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 0, windupTicks: 1);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var firstTick = pipeline.RunTick(new TickInput(1));
                var windupTick = pipeline.RunTick(new TickInput(2));
                var windupState = GetEnemyChargeState(worldState, 40);

                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 1, 1));

                var activeTick = pipeline.RunTick(new TickInput(3));
                var activeState = GetEnemyChargeState(worldState, 40);

                Assert.That(GetEntityAfterTick(firstTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(windupTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(windupState.phase, Is.EqualTo(EnemyChargePhase.Windup));
                Assert.That(windupState.lockedDirection, Is.EqualTo(Direction.Right));
                Assert.That(windupState.remainingActiveSteps, Is.EqualTo(3));
                Assert.That(GetEntityAfterTick(windupTick, 40).aiStateTimer, Is.Zero);

                Assert.That(GetEntityAfterTick(activeTick, 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(activeTick, 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(activeTick, 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(activeTick, 40).facing, Is.EqualTo(Direction.Right));
                Assert.That(activeState.phase, Is.EqualTo(EnemyChargePhase.Active));
                Assert.That(activeState.lockedDirection, Is.EqualTo(Direction.Right));
                Assert.That(activeState.remainingActiveSteps, Is.EqualTo(2));
                Assert.That(activeTick.Trace.Text, Does.Contain("Reason=ChargeContinue"));
                Assert.That(activeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_PassiveContactFollowsPlayerCooldownWhileChargeWaitsOnPlayerCell()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 3, chargeStepCooldownTicks: 3);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var ticks = Enumerable.Range(1, 7)
                    .Select(tick => pipeline.RunTick(new TickInput(tick)))
                    .ToArray();

                Assert.That(GetEntityAfterTick(ticks[3], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[4], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[5], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[6], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(GetEntityAfterTick(ticks[1], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[2], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[3], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[4], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[5], 40).aiStateTimer, Is.Zero);
                Assert.That(ticks[3].AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(ticks[4].AttackPhaseResult.DamageResolutions.Single().Accepted, Is.False);
                Assert.That(ticks[4].AttackPhaseResult.DamageResolutions.Single().RejectReason, Is.EqualTo(DamageRejectReason.ReceiverCooldown));
                Assert.That(ticks[5].AttackPhaseResult.DamageResolutions.Single().Accepted, Is.True);
                Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(3));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_WithChargeStepCooldown_ContinuesSameChargeSessionUntilSolid()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), hp: 5),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                    CreateBox(entityId: 50, position: new Vector2Int(6, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var profile = CreateChargingEnemyProfile(moveCooldownTicks: 2, chargeStepCooldownTicks: 2);

            try
            {
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var ticks = Enumerable.Range(1, 12)
                    .Select(tick => pipeline.RunTick(new TickInput(tick)))
                    .ToArray();

                Assert.That(GetEntityAfterTick(ticks[0], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(ticks[1], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(ticks[1], 40).enemyLocomotionCooldownTicks, Is.EqualTo(1));
                Assert.That(GetEntityAfterTick(ticks[2], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[2], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[2], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[3], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[3], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[3], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[4], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(GetEntityAfterTick(ticks[6], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(4, 0)));
                Assert.That(GetEntityAfterTick(ticks[8], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(5, 0)));
                Assert.That(GetEntityAfterTick(ticks[9], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[9], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[9], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(5, 0)));
                Assert.That(GetEntityAfterTick(ticks[10], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[10], 40).aiStateTimer, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[10], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(5, 0)));
                Assert.That(GetEntityAfterTick(ticks[11], 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(GetEntityAfterTick(ticks[11], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(4, 0)));
                Assert.That(ticks[1].Trace.Text, Does.Contain("Reason=ChargeStart"));
                Assert.That(ticks[0].PresentationData.EntityMotions.Single().MotionKind, Is.EqualTo(TickEntityMotionKind.Move));
                Assert.That(ticks[2].PresentationData.EntityMotions.Single().MotionKind, Is.EqualTo(TickEntityMotionKind.ChargeMove));
                Assert.That(ticks[3].PresentationData.EntityMotions.Any(motion => motion.MotionKind == TickEntityMotionKind.ChargeMove), Is.False);
                Assert.That(ticks[4].PresentationData.EntityMotions.Single().MotionKind, Is.EqualTo(TickEntityMotionKind.ChargeMove));

                var chargeState = GetEnemyChargeState(worldState, 40);
                Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.None));
                Assert.That(chargeState.sequence, Is.GreaterThan(0));
                Assert.That(chargeState.remainingActiveSteps, Is.Zero);
                Assert.That(chargeState.recoverRemainingTicks, Is.Zero);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAi_ChargingProfile_SeparatesChaseAndChargeCooldownCadence()
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
                var pipeline = CreateEnemyPipeline(worldState, profile, playerDamageCooldownTicks: 1);
                var ticks = Enumerable.Range(1, 6)
                    .Select(tick => pipeline.RunTick(new TickInput(tick)))
                    .ToArray();

                Assert.That(GetEntityAfterTick(ticks[0], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(ticks[0], 40).enemyLocomotionCooldownTicks, Is.EqualTo(2));
                Assert.That(GetEntityAfterTick(ticks[1], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(GetEntityAfterTick(ticks[2], 40).aiMode, Is.EqualTo(EnemyAiMode.Charge));
                Assert.That(GetEntityAfterTick(ticks[2], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(GetEntityAfterTick(ticks[2], 40).enemyLocomotionCooldownTicks, Is.Zero);
                Assert.That(GetEntityAfterTick(ticks[3], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(3, 0)));
                Assert.That(GetEntityAfterTick(ticks[4], 40).position.PlanarPosition, Is.EqualTo(new Vector2Int(4, 0)));
                Assert.That(ticks[2].Trace.Text, Does.Contain("Reason=ChargeContinue"));
                Assert.That(ticks[3].Trace.Text, Does.Contain("Reason=ChargeContinue"));
                Assert.That(ticks[0].PresentationData.EntityMotions.Single().MotionKind, Is.EqualTo(TickEntityMotionKind.Move));
                Assert.That(ticks[2].PresentationData.EntityMotions.Single().MotionKind, Is.EqualTo(TickEntityMotionKind.ChargeMove));
                Assert.That(ticks[3].PresentationData.EntityMotions.Single().MotionKind, Is.EqualTo(TickEntityMotionKind.ChargeMove));
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

            Assert.That(factory.CanCreate(new EntityLogicCreationContext(worldState.CreateSnapshot(), passiveTutorialEnemy)), Is.False);
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

        private static EnemyAiProfile CreateEnemyProfile(
            int windupTicks,
            int moveCooldownTicks = 0,
            int recoverTicks = 1)
        {
            return EnemyAiProfileTestFactory.CreateDefaultMelee(windupTicks, moveCooldownTicks, recoverTicks);
        }

        private static EnemyAiProfile CreateUtilitySummonProfile(
            int initialDelayTicks,
            int cooldownTicks,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            EnemyUnitArchetypeAsset summonedArchetype = null,
            bool overrideHp = false,
            int hpOverride = 1,
            int windupTicks = 1)
        {
            return CreateUtilityProfile(
                CreateSummonUtilityEffect(
                    initialDelayTicks,
                    cooldownTicks,
                    spawnCountPerTrigger,
                    maxAliveChildren,
                    summonedArchetype != null ? summonedArchetype : GetSharedSummonedArchetype(),
                    overrideHp,
                    hpOverride,
                    windupTicks));
        }

        private static EnemyAiProfile CreateUtilityLockNearbyBoxesProfile(
            int initialDelayTicks,
            int cooldownTicks,
            int radius,
            int durationTicks,
            bool blocksPush = true,
            bool blocksFlip = true,
            bool includeSourceCell = false,
            BoxLockTargetPattern targetPattern = BoxLockTargetPattern.ManhattanRadius)
        {
            return CreateUtilityProfile(
                CreateLockNearbyBoxesUtilityEffect(
                    initialDelayTicks,
                    cooldownTicks,
                    radius,
                    durationTicks,
                    blocksPush,
                    blocksFlip,
                    includeSourceCell,
                    targetPattern));
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

        private static EnemyUtilityEffectAuthoring CreateSummonUtilityEffect(
            int initialDelayTicks,
            int cooldownTicks,
            int spawnCountPerTrigger,
            int maxAliveChildren,
            EnemyUnitArchetypeAsset summonedArchetype,
            bool overrideHp = false,
            int hpOverride = 1,
            int windupTicks = 1)
        {
            var summon = new SummonMinionAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(summon, "spawnCountPerTrigger", spawnCountPerTrigger);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "maxAliveChildren", maxAliveChildren);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "candidatePattern", SummonCandidatePattern.OrthogonalAdjacent4);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoUnitAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoSolidAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "summonedArchetype", summonedArchetype);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "overrideHp", overrideHp);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "hpOverride", hpOverride);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "windupSeconds", TicksToSeconds(windupTicks));

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.SummonMinion);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", TicksToSeconds(initialDelayTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", TicksToSeconds(cooldownTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "summon", summon);
            return effect;
        }

        private static EnemyUtilityEffectAuthoring CreateLockNearbyBoxesUtilityEffect(
            int initialDelayTicks,
            int cooldownTicks,
            int radius,
            int durationTicks,
            bool blocksPush,
            bool blocksFlip,
            bool includeSourceCell,
            BoxLockTargetPattern targetPattern)
        {
            var lockNearbyBoxes = new LockNearbyBoxesAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "radius", radius);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "durationSeconds", TicksToSeconds(durationTicks));
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "blocksPush", blocksPush);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "blocksFlip", blocksFlip);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "includeSourceCell", includeSourceCell);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "targetPattern", targetPattern);

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.LockNearbyBoxes);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", TicksToSeconds(initialDelayTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", TicksToSeconds(cooldownTicks));
            EnemyAiProfileTestFactory.SetSerializedField(effect, "lockNearbyBoxes", lockNearbyBoxes);
            return effect;
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

        private static EnemyAiProfile CreateWindupRandomWalkPilotProfile(
            int windupTicks = 1,
            int moveCooldownTicks = 0,
            int recoverTicks = 1,
            bool includePassiveContact = true)
        {
            return EnemyAiProfileTestFactory.CreateWindupRandomWalkPilot(
                windupTicks,
                moveCooldownTicks,
                recoverTicks,
                includePassiveContact);
        }

        private static EnemyAiProfile CreateContactDamageProfile(int moveCooldownTicks = 0, int recoverTicks = 1)
        {
            return EnemyAiProfileTestFactory.CreateContactDamage(moveCooldownTicks, recoverTicks);
        }

        private static EnemyAiProfile CreateGlideContactDamageProfile(int durationTicks)
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
                landingPendingCell: default);
        }

        private static bool HasAcceptedPassiveContact(TickResult result, int sourceId, int targetId)
        {
            return result.AttackPhaseResult.DamageResolutions.Any(
                record => record.Accepted &&
                          record.SourceId == sourceId &&
                          record.TargetId == targetId &&
                          record.SourceKind == AttackSourceKind.PassiveContact);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
            var baselinePipeline = GameplayCompositionRoot.CreateTickPipeline(baselineWorldState, baselineProfile);
            var pilotPipeline = GameplayCompositionRoot.CreateTickPipeline(pilotWorldState, pilotProfile);
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
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
                tick.AttackPhaseResult.DamageResolutions.Any(record =>
                    record.Accepted &&
                    record.SourceId == 40 &&
                    record.TargetId == 10 &&
                    record.SourceKind == AttackSourceKind.Combat))
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
                entityLogics ?? Array.Empty<IEntityLogic>());
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
                playerRespawnDelayTicks);
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
                    runtimeSnapshot.DefinitionsByArchetypeId),
                runtimeSnapshot.SpawnDefaultsByArchetypeId)
                .CreateTickPipeline(worldState);
        }

        private static GameplayBootstrapper CreateSharedSummonBootstrapper(
            EnemyAiProfile summonerProfile,
            out EnemyAiProfile defaultProfile,
            out EnemyUnitArchetypeCatalog archetypeCatalog)
        {
            defaultProfile = CreateUtilityProfile();
            archetypeCatalog = CreateEnemyUnitArchetypeCatalog(GetSharedSummonedArchetype());

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
                    runtimeSnapshot.DefinitionsByArchetypeId),
                runtimeSnapshot.SpawnDefaultsByArchetypeId);
        }

        private static TickPipeline CreateSharedSummonTickPipeline(
            EnemyAiProfile summonerProfile,
            WorldState worldState,
            out EnemyAiProfile defaultProfile,
            out EnemyUnitArchetypeCatalog archetypeCatalog)
        {
            return CreateSharedSummonBootstrapper(summonerProfile, out defaultProfile, out archetypeCatalog)
                .CreateTickPipeline(worldState);
        }

        private static EnemyUnitArchetypeAsset CreateEnemyUnitArchetypeAsset(
            string archetypeId,
            EnemyAiProfile profile,
            int hp,
            EnemyAiMode initialAiMode)
        {
            var asset = ScriptableObject.CreateInstance<EnemyUnitArchetypeAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(asset, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            EnemyAiProfileTestFactory.SetSerializedField(asset, "aiProfile", profile);
            EnemyAiProfileTestFactory.SetSerializedField(asset, "spawnDefaults", CreateEnemyUnitSpawnDefaults(hp, initialAiMode));
            return asset;
        }

        private static EnemyUnitArchetypeCatalog CreateEnemyUnitArchetypeCatalog(params EnemyUnitArchetypeAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyUnitArchetypeCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(catalog, "entries", entries ?? Array.Empty<EnemyUnitArchetypeAsset>());
            return catalog;
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

        private static EnemyUnitSpawnDefaults CreateEnemyUnitSpawnDefaults(int hp, EnemyAiMode initialAiMode)
        {
            object boxed = EnemyUnitSpawnDefaults.CreateDefault();
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "hp", hp);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "initialAiMode", initialAiMode);
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
