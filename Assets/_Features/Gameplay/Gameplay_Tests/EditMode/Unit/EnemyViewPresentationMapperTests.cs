using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyViewPresentationMapperTests
    {
        private const string JPeterPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_JPeter.prefab";
        private const string DrSaturnPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab";
        private const string SummonScalePulseScriptGuid = "d5bc7f8cc6194d2882c9cdb56e96d289";
        private const string GravityAuraVfxScriptGuid = "f18cc0d83f3741f087d10f75a8d2d59c";

        [Test]
        [Category("Extended")]
        public void EnemyViewPresentationMapper_MapsJumpWindupAndAirborneWithoutUsingAttackPhase()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 1,
                    enemy,
                    new TickEnemyJumpPresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyJumpPhase.Windup,
                        startedWindupThisTick: true,
                        startedAirborneThisTick: false,
                        landedThisTick: false,
                        retryThisTick: false)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var windupState), Is.True);
            Assert.That(windupState.JumpPhase, Is.EqualTo(EnemyJumpPhase.Windup));
            Assert.That(windupState.StartedJumpWindupThisTick, Is.True);
            Assert.That(windupState.StartedJumpAirborneThisTick, Is.False);
            Assert.That(windupState.StartedWindupThisTick, Is.False);
            Assert.That(windupState.ExecutedThisTick, Is.False);
            Assert.That(windupState.StartedRecoveryThisTick, Is.False);
            Assert.That(windupState.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));

            mapper.Build(
                CreateResult(
                    tickIndex: 2,
                    enemy,
                    new TickEnemyJumpPresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyJumpPhase.Airborne,
                        startedWindupThisTick: false,
                        startedAirborneThisTick: true,
                        landedThisTick: false,
                        retryThisTick: false)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var airborneState), Is.True);
            Assert.That(airborneState.JumpPhase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(airborneState.StartedJumpWindupThisTick, Is.False);
            Assert.That(airborneState.StartedJumpAirborneThisTick, Is.True);
            Assert.That(airborneState.StartedWindupThisTick, Is.False);
            Assert.That(airborneState.ExecutedThisTick, Is.False);
            Assert.That(airborneState.StartedRecoveryThisTick, Is.False);
            Assert.That(airborneState.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_MapsCrushedBoxJumpOutcome()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 3,
                    enemy,
                    new TickEnemyJumpPresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyJumpPhase.Cooldown,
                        startedWindupThisTick: false,
                        startedAirborneThisTick: false,
                        landedThisTick: true,
                        retryThisTick: false,
                        outcome: TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var state), Is.True);
            Assert.That(state.JumpOutcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded));
            Assert.That(state.LandedFromJumpThisTick, Is.True);
            Assert.That(state.RetryingJumpAirborneThisTick, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyViewPresentationState_JumpLandingCompletionHold_SuppressesLandedSignal()
        {
            var state = new EnemyViewPresentationState(
                entityId: 40,
                tickIndex: 3,
                EnemyAiMode.Patrol,
                EnemyActionKind.None,
                EnemyJumpPhase.Cooldown,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: true,
                retryingJumpAirborneThisTick: false,
                tookDamage: false,
                didDie: false);

            var held = state.WithJumpLandingCompletionHold();

            Assert.That(held.JumpPhase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(held.LandedFromJumpThisTick, Is.False);
            Assert.That(held.StartedJumpAirborneThisTick, Is.False);
            Assert.That(held.RetryingJumpAirborneThisTick, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyViewPresentationMapper_TryMapInitial_RoleEnemyWithNoneAiMode_StillMapsEnemyViewState()
        {
            var mapper = new EnemyViewPresentationMapper();
            var enemy = CreateEnemy(entityId: 40, EnemyAiMode.None);

            var mapped = mapper.TryMapInitial(enemy, out var state);

            Assert.That(mapped, Is.True);
            Assert.That(state.EntityId, Is.EqualTo(40));
            Assert.That(state.AiMode, Is.EqualTo(EnemyAiMode.None));
            Assert.That(state.DidDie, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyViewPresentationMapper_MapsChargeWindupActiveAndRecoverStates()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();

            mapper.Build(
                CreateResult(
                    tickIndex: 1,
                    CreateEnemy(enemyId, EnemyAiMode.Charge),
                    new TickEnemyChargePresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyChargePhase.Windup,
                        startedWindupThisTick: true,
                        startedActiveThisTick: false,
                        startedRecoverThisTick: false,
                        lockedDirection: Direction.Right)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var windupState), Is.True);
            Assert.That(windupState.ChargePhase, Is.EqualTo(EnemyChargePhase.Windup));
            Assert.That(windupState.StartedChargeWindupThisTick, Is.True);
            Assert.That(windupState.StartedChargeActiveThisTick, Is.False);
            Assert.That(windupState.StartedChargeRecoverThisTick, Is.False);
            Assert.That(windupState.StartedWindupThisTick, Is.True);
            Assert.That(windupState.StartedRecoveryThisTick, Is.False);

            mapper.Build(
                CreateResult(
                    tickIndex: 2,
                    CreateEnemy(enemyId, EnemyAiMode.Charge),
                    new TickEnemyChargePresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyChargePhase.Active,
                        startedWindupThisTick: false,
                        startedActiveThisTick: true,
                        startedRecoverThisTick: false,
                        lockedDirection: Direction.Right)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var activeState), Is.True);
            Assert.That(activeState.ChargePhase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(activeState.StartedChargeWindupThisTick, Is.False);
            Assert.That(activeState.StartedChargeActiveThisTick, Is.True);
            Assert.That(activeState.StartedChargeRecoverThisTick, Is.False);
            Assert.That(activeState.StartedWindupThisTick, Is.False);
            Assert.That(activeState.StartedRecoveryThisTick, Is.False);

            mapper.Build(
                CreateResult(
                    tickIndex: 3,
                    CreateEnemy(enemyId, EnemyAiMode.Recover),
                    new TickEnemyChargePresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyChargePhase.Recover,
                        startedWindupThisTick: false,
                        startedActiveThisTick: false,
                        startedRecoverThisTick: true,
                        lockedDirection: Direction.Right)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var recoverState), Is.True);
            Assert.That(recoverState.ChargePhase, Is.EqualTo(EnemyChargePhase.Recover));
            Assert.That(recoverState.StartedChargeWindupThisTick, Is.False);
            Assert.That(recoverState.StartedChargeActiveThisTick, Is.False);
            Assert.That(recoverState.StartedChargeRecoverThisTick, Is.True);
            Assert.That(recoverState.StartedWindupThisTick, Is.False);
            Assert.That(recoverState.StartedRecoveryThisTick, Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_MapsGravityFieldAuraUtilityWindupWithoutEnemyActionSignal()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 1,
                    enemy,
                    new TickEnemyUtilityPresentationSignal(
                        enemyId,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityPresentationPhase.WindupStarted,
                        startTick: 1,
                        executeTick: 3,
                        durationTicks: 2)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var state), Is.True);
            Assert.That(state.UtilityPresentationKind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
            Assert.That(state.StartedUtilityWindupThisTick, Is.True);
            Assert.That(state.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
            Assert.That(state.StartedWindupThisTick, Is.False);
            Assert.That(state.ExecutedThisTick, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_MapsSummonWindupAndRecoverWithoutAttackSemantic()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 1,
                    enemy,
                    new TickEnemySummonPresentationSignal(
                        enemyId,
                        EnemySummonPresentationPhase.WindupStarted,
                        startTick: 1,
                        executeTick: 103,
                        durationTicks: 102)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var windupState), Is.True);
            Assert.That(windupState.UtilityPresentationKind, Is.EqualTo(EnemyUtilityPresentationKind.None));
            Assert.That(windupState.StartedSummonWindupThisTick, Is.True);
            Assert.That(windupState.StartedRecoveryThisTick, Is.False);
            Assert.That(windupState.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
            Assert.That(windupState.StartedWindupThisTick, Is.False);

            mapper.Build(
                CreateResult(
                    tickIndex: 103,
                    enemy,
                    new TickEnemySummonPresentationSignal(
                        enemyId,
                        EnemySummonPresentationPhase.RecoverStarted,
                        startTick: 103,
                        executeTick: 145,
                        durationTicks: 42)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var recoverState), Is.True);
            Assert.That(recoverState.UtilityPresentationKind, Is.EqualTo(EnemyUtilityPresentationKind.None));
            Assert.That(recoverState.StartedSummonWindupThisTick, Is.False);
            Assert.That(recoverState.StartedSummonRecoverThisTick, Is.True);
            Assert.That(recoverState.StartedRecoveryThisTick, Is.True);
            Assert.That(recoverState.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
            Assert.That(recoverState.ExecutedThisTick, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_SummonCanceled_ClearsSummonState()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 2,
                    enemy,
                    new TickEnemySummonPresentationSignal(
                        enemyId,
                        EnemySummonPresentationPhase.Canceled,
                        startTick: 1,
                        executeTick: 3,
                        durationTicks: 2)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var state), Is.True);
            Assert.That(state.UtilityPresentationKind, Is.EqualTo(EnemyUtilityPresentationKind.None));
            Assert.That(state.StartedSummonWindupThisTick, Is.False);
            Assert.That(state.StartedSummonRecoverThisTick, Is.False);
            Assert.That(state.StartedRecoveryThisTick, Is.False);
            Assert.That(state.SummonCanceledThisTick, Is.True);
            Assert.That(state.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_SummonSignalFlags_ResetOnNextTickWithoutSignal()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 1,
                    enemy,
                    new TickEnemySummonPresentationSignal(
                        enemyId,
                        EnemySummonPresentationPhase.WindupStarted,
                        startTick: 1,
                        executeTick: 103,
                        durationTicks: 102)),
                viewsByEntityId,
                states);
            Assert.That(states[enemyId].StartedSummonWindupThisTick, Is.True);

            mapper.Build(
                CreateResult(tickIndex: 2, enemy),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var state), Is.True);
            Assert.That(state.StartedSummonWindupThisTick, Is.False);
            Assert.That(state.StartedSummonRecoverThisTick, Is.False);
            Assert.That(state.SummonCanceledThisTick, Is.False);
            Assert.That(state.StartedRecoveryThisTick, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_MapsOngoingUtilityRecoverStateWithoutAttackSemantic()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 104,
                    enemy,
                    new TickEnemyUtilityPhasePresentationState(
                        enemyId,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityEffectPhase.Recover,
                        phaseElapsedTicks: 1,
                        phaseDurationTicks: 42)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var state), Is.True);
            Assert.That(state.UtilityPresentationKind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
            Assert.That(state.UtilityPhase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
            Assert.That(state.StartedUtilityWindupThisTick, Is.False);
            Assert.That(state.StartedUtilityRecoverThisTick, Is.False);
            Assert.That(state.StartedRecoveryThisTick, Is.False);
            Assert.That(state.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
            Assert.That(state.ExecutedThisTick, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_MapsOngoingUtilityWindupStateWithoutAttackSemantic()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 102,
                    enemy,
                    new TickEnemyUtilityPhasePresentationState(
                        enemyId,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        EnemyUtilityEffectPhase.Windup,
                        phaseElapsedTicks: 1,
                        phaseDurationTicks: 3)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var state), Is.True);
            Assert.That(state.UtilityPresentationKind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
            Assert.That(state.UtilityPhase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
            Assert.That(state.StartedUtilityWindupThisTick, Is.False);
            Assert.That(state.StartedUtilityRecoverThisTick, Is.False);
            Assert.That(state.StartedRecoveryThisTick, Is.False);
            Assert.That(state.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
            Assert.That(state.ExecutedThisTick, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyViewPresentationMapper_UtilityRecoverPhaseTakesPriorityForAnimatorTiming()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 103,
                    enemy,
                    new[]
                    {
                        new TickEnemyUtilityPhasePresentationState(
                            enemyId,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityEffectPhase.Windup,
                            phaseElapsedTicks: 2,
                            phaseDurationTicks: 5,
                            effectIndex: 0),
                        new TickEnemyUtilityPhasePresentationState(
                            enemyId,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityEffectPhase.Recover,
                            phaseElapsedTicks: 0,
                            phaseDurationTicks: 42,
                            effectIndex: 1),
                    }),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var state), Is.True);
            Assert.That(state.UtilityPresentationKind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
            Assert.That(state.UtilityPhase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
            Assert.That(state.StartedUtilityRecoverThisTick, Is.False);
            Assert.That(state.StartedRecoveryThisTick, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemySummonScalePulsePresentationDriver_SummonWindupAndRecover_ScalesModelRoot()
        {
            var rootObject = new UnityEngine.GameObject(nameof(EnemySummonScalePulsePresentationDriver_SummonWindupAndRecover_ScalesModelRoot));
            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var modelRoot = view.ModelRoot;
                modelRoot.localScale = new UnityEngine.Vector3(0.4f, 0.4f, 0.4f);
                var driver = rootObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Patrol,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false,
                    startedSummonWindupThisTick: true));

                driver.Advance(1.05f);
                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(1.1f).Within(0.0001f));
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.44f).Within(0.0001f));

                driver.Advance(0.65f);
                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.3f).Within(0.0001f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 103,
                    aiMode: EnemyAiMode.Patrol,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: true,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false,
                    startedSummonRecoverThisTick: true));

                driver.Advance(0.7f);
                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(driver.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemySummonScalePulsePresentationDriver_SummonCanceled_NormalizesScalePulse()
        {
            var rootObject = new UnityEngine.GameObject(nameof(EnemySummonScalePulsePresentationDriver_SummonCanceled_NormalizesScalePulse));
            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var modelRoot = view.ModelRoot;
                modelRoot.localScale = new UnityEngine.Vector3(0.4f, 0.4f, 0.4f);
                var driver = rootObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                driver.Apply(CreateSummonState(startedSummonWindupThisTick: true));
                driver.Advance(0.5f);
                Assert.That(modelRoot.localScale.x, Is.Not.EqualTo(0.4f).Within(0.0001f));

                driver.Apply(CreateSummonState(summonCanceledThisTick: true, tickIndex: 2));

                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(driver.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemySummonScalePulsePresentationDriver_IgnoresGravityUtilitySignals()
        {
            var rootObject = new UnityEngine.GameObject(nameof(EnemySummonScalePulsePresentationDriver_IgnoresGravityUtilitySignals));
            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var modelRoot = view.ModelRoot;
                modelRoot.localScale = new UnityEngine.Vector3(0.4f, 0.4f, 0.4f);
                var driver = rootObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                driver.Apply(CreateGravityUtilityState(startedUtilityWindupThisTick: true));
                driver.Advance(0.2f);

                Assert.That(driver.IsPlaying, Is.False);
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemySummonScalePulsePresentationDriver_IgnoresUnknownUtilitySignals()
        {
            var rootObject = new UnityEngine.GameObject(nameof(EnemySummonScalePulsePresentationDriver_IgnoresUnknownUtilitySignals));
            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var modelRoot = view.ModelRoot;
                modelRoot.localScale = new UnityEngine.Vector3(0.4f, 0.4f, 0.4f);
                var driver = rootObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                driver.Apply(CreateUtilityState(
                    (EnemyUtilityPresentationKind)99,
                    startedUtilityWindupThisTick: true));
                driver.Advance(0.2f);

                Assert.That(driver.IsPlaying, Is.False);
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemySummonScalePulsePresentationDriver_SemanticSuppression_FreezesCurrentScale()
        {
            var rootObject = new UnityEngine.GameObject(nameof(EnemySummonScalePulsePresentationDriver_SemanticSuppression_FreezesCurrentScale));
            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var modelRoot = view.ModelRoot;
                modelRoot.localScale = new UnityEngine.Vector3(0.4f, 0.4f, 0.4f);
                var driver = rootObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                driver.Apply(CreateSummonState(startedSummonWindupThisTick: true));
                driver.Advance(0.5f);

                var frozenMultiplier = driver.CurrentScaleMultiplier;
                var frozenScale = modelRoot.localScale;
                Assert.That(frozenMultiplier, Is.GreaterThan(1f));

                driver.ApplyEnemyVisualSemanticState(new EnemyVisualSemanticState(
                    EnemyVisualActivityState.FrontFaceInactive,
                    shouldPauseAnimatorPlayback: true,
                    shouldPauseAutonomousPresentation: true));
                driver.Advance(10f);

                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(frozenMultiplier).Within(0.0001f));
                Assert.That(modelRoot.localScale.x, Is.EqualTo(frozenScale.x).Within(0.0001f));

                driver.ApplyEnemyVisualSemanticState(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));
                driver.Advance(0.1f);

                Assert.That(driver.CurrentScaleMultiplier, Is.GreaterThan(frozenMultiplier));
                Assert.That(modelRoot.localScale.x, Is.GreaterThan(frozenScale.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemySummonScalePulsePresentationDriver_DisableStillNormalizesToBaseScale()
        {
            var rootObject = new UnityEngine.GameObject(nameof(EnemySummonScalePulsePresentationDriver_DisableStillNormalizesToBaseScale));
            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var modelRoot = view.ModelRoot;
                modelRoot.localScale = new UnityEngine.Vector3(0.4f, 0.4f, 0.4f);
                var driver = rootObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                driver.Apply(CreateSummonState(startedSummonWindupThisTick: true));
                driver.Advance(0.5f);
                Assert.That(modelRoot.localScale.x, Is.Not.EqualTo(0.4f).Within(0.0001f));

                driver.ApplyEnemyVisualSemanticState(new EnemyVisualSemanticState(
                    EnemyVisualActivityState.FrontFaceInactive,
                    shouldPauseAnimatorPlayback: true,
                    shouldPauseAutonomousPresentation: true));
                driver.Advance(10f);
                Assert.That(modelRoot.localScale.x, Is.Not.EqualTo(0.4f).Within(0.0001f));

                typeof(EnemySummonScalePulsePresentationDriver)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(driver, Array.Empty<object>());

                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(driver.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemySummonScalePulse_SummonCanceled_DoesNotRemainInWindupHold()
        {
            var rootObject = new UnityEngine.GameObject(nameof(EnemySummonScalePulse_SummonCanceled_DoesNotRemainInWindupHold));
            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var modelRoot = view.ModelRoot;
                modelRoot.localScale = new UnityEngine.Vector3(0.4f, 0.4f, 0.4f);
                var driver = rootObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                driver.Apply(CreateSummonState(startedSummonWindupThisTick: true));
                driver.Advance(1.7f);
                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(driver.IsPlaying, Is.False);

                driver.Apply(CreateSummonState(summonCanceledThisTick: true, tickIndex: 2));

                Assert.That(driver.CurrentScaleMultiplier, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(driver.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyView_JPeter_UsesTypedSummonScalePulseBinding()
        {
            var yaml = ReadProjectText(JPeterPrefabPath);

            Assert.That(yaml, Does.Contain($"guid: {SummonScalePulseScriptGuid}"));
            Assert.That(
                yaml,
                Does.Contain("Game.Feature.Gameplay.Host.EnemySummonScalePulsePresentationDriver"));
            Assert.That(
                yaml,
                Does.Not.Contain("Game.Feature.Gameplay.Host.EnemyUtilityScalePulsePresentationDriver"));
        }

        [Test]
        [Category("Core")]
        public void EnemyView_JPeter_HasNoLegacyUtilityKind3()
        {
            var yaml = ReadProjectText(JPeterPrefabPath);

            Assert.That(yaml, Does.Not.Contain("utilityKind: 3"));
        }

        [Test]
        [Category("Core")]
        public void EnemyView_JPeter_PreservesSummonScalePulseTuning()
        {
            var yaml = ReadProjectText(JPeterPrefabPath);

            Assert.That(yaml, Does.Contain("windupDurationSeconds: 1.7"));
            Assert.That(yaml, Does.Contain("windupPeakTimeSeconds: 1.05"));
            Assert.That(yaml, Does.Contain("recoverDurationSeconds: 0.7"));
            Assert.That(yaml, Does.Contain("peakScaleMultiplier: 1.1"));
            Assert.That(yaml, Does.Contain("windupEndScaleMultiplier: 0.75"));
            Assert.That(yaml, Does.Contain("recoverEndScaleMultiplier: 1"));
        }

        [Test]
        [Category("Core")]
        public void EnemyView_DrSaturn_PreservesGravityUtilityKind2()
        {
            var yaml = ReadProjectText(DrSaturnPrefabPath);

            Assert.That(yaml, Does.Contain($"guid: {GravityAuraVfxScriptGuid}"));
            Assert.That(yaml, Does.Contain("utilityKind: 2"));
            Assert.That(yaml, Does.Not.Contain("utilityKind: 3"));
        }

        [Test]
        [Category("Core")]
        public void EnemyAnimatorDriver_GravityFieldAuraUtilityWindupUsesUtilityPathWithoutAttackSemantic()
        {
            var gameObject = new UnityEngine.GameObject("EnemyAnimatorDriver_GravityFieldAuraUtilityWindupUsesUtilityPathWithoutAttackSemantic");
            try
            {
                var driver = gameObject.AddComponent<EnemyAnimatorDriver>();

                driver.Apply(
                    new EnemyViewPresentationState(
                        entityId: 40,
                        tickIndex: 1,
                        aiMode: EnemyAiMode.Patrol,
                        activeActionKind: EnemyActionKind.None,
                        jumpPhase: EnemyJumpPhase.None,
                        chargePhase: EnemyChargePhase.None,
                        isMoving: false,
                        startedWindupThisTick: false,
                        executedThisTick: false,
                        startedRecoveryThisTick: false,
                        startedJumpWindupThisTick: false,
                        startedJumpAirborneThisTick: false,
                        landedFromJumpThisTick: false,
                        retryingJumpAirborneThisTick: false,
                        startedChargeWindupThisTick: false,
                        startedChargeActiveThisTick: false,
                        startedChargeRecoverThisTick: false,
                        tookDamage: false,
                        didDie: false,
                        utilityPresentationKind: EnemyUtilityPresentationKind.GravityFieldAura,
                        startedUtilityWindupThisTick: true));

                Assert.That(driver.UtilityWindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.AttackSignalCount, Is.Zero);
                Assert.That(driver.WindupSignalCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAnimatorDriver_TypedLegacyOneShotSuppression_PreservesSustainedState()
        {
            var gameObject = new UnityEngine.GameObject("EnemyAnimatorDriver_TypedLegacyOneShotSuppression_PreservesSustainedState");
            try
            {
                var driver = gameObject.AddComponent<EnemyAnimatorDriver>();

                driver.Apply(
                    CreateEnemyPresentationState(
                        tickIndex: 1,
                        jumpPhase: EnemyJumpPhase.Airborne,
                        startedJumpAirborneThisTick: true),
                    EnemyPresentationLegacyOneShotSuppression.JumpAirborneStartOrRetry);

                Assert.That(driver.JumpAirborneSignalCount, Is.Zero);
                Assert.That(driver.LastPresentationState.JumpPhase, Is.EqualTo(EnemyJumpPhase.Airborne));

                driver.Apply(
                    CreateEnemyPresentationState(
                        tickIndex: 2,
                        chargePhase: EnemyChargePhase.Active,
                        startedChargeActiveThisTick: true),
                    EnemyPresentationLegacyOneShotSuppression.ChargeActiveStart);

                Assert.That(driver.ChargeActiveSignalCount, Is.Zero);
                Assert.That(driver.LastPresentationState.ChargePhase, Is.EqualTo(EnemyChargePhase.Active));

                driver.Apply(
                    CreateEnemyPresentationState(
                        tickIndex: 3,
                        didDie: true),
                    EnemyPresentationLegacyOneShotSuppression.DeathTrigger);

                Assert.That(driver.DeathSignalCount, Is.Zero);
                Assert.That(driver.LastPresentationState.DidDie, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState enemy,
            TickEnemyJumpPresentationSignal jumpSignal)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { enemy },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    new[] { jumpSignal },
                    Array.Empty<TickEntityExitPresentationSignal>()),
                string.Empty,
                TickTrace.Empty);
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState enemy,
            TickEnemyChargePresentationSignal chargeSignal)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { enemy },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    new[] { chargeSignal },
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<TickImpactTransientPresentationSignal>()),
                string.Empty,
                TickTrace.Empty);
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState enemy,
            TickEnemyUtilityPresentationSignal utilitySignal)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { enemy },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEnemyChargePresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<TickImpactTransientPresentationSignal>(),
                    Array.Empty<FlipImpactPresentationSignal>(),
                    enemyUtilitySignals: new[] { utilitySignal }),
                string.Empty,
                TickTrace.Empty);
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState enemy,
            TickEnemySummonPresentationSignal summonSignal)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { enemy },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEnemyChargePresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<TickImpactTransientPresentationSignal>(),
                    Array.Empty<FlipImpactPresentationSignal>(),
                    enemySummonSignals: new[] { summonSignal }),
                string.Empty,
                TickTrace.Empty);
        }

        private static TickResult CreateResult(int tickIndex, EntityState enemy)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { enemy },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEnemyChargePresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<TickImpactTransientPresentationSignal>()),
                string.Empty,
                TickTrace.Empty);
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState enemy,
            TickEnemyUtilityPhasePresentationState utilityPhaseState)
        {
            return CreateResult(tickIndex, enemy, new[] { utilityPhaseState });
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState enemy,
            IReadOnlyList<TickEnemyUtilityPhasePresentationState> utilityPhaseStates)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { enemy },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEnemyChargePresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<TickImpactTransientPresentationSignal>(),
                    Array.Empty<FlipImpactPresentationSignal>(),
                    enemyUtilityPhaseStates: utilityPhaseStates),
                string.Empty,
                TickTrace.Empty);
        }

        private static EntityState CreateEnemy(int entityId, EnemyAiMode aiMode)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 1, 1),
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EnemyViewPresentationState CreateSummonState(
            bool startedSummonWindupThisTick = false,
            bool startedRecoveryThisTick = false,
            bool startedSummonRecoverThisTick = false,
            bool didDie = false,
            bool summonCanceledThisTick = false,
            int tickIndex = 1)
        {
            return new EnemyViewPresentationState(
                entityId: 40,
                tickIndex: tickIndex,
                aiMode: EnemyAiMode.Patrol,
                activeActionKind: EnemyActionKind.None,
                jumpPhase: EnemyJumpPhase.None,
                chargePhase: EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: startedRecoveryThisTick,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: didDie,
                startedSummonWindupThisTick: startedSummonWindupThisTick,
                startedSummonRecoverThisTick: startedSummonRecoverThisTick,
                summonCanceledThisTick: summonCanceledThisTick);
        }

        private static EnemyViewPresentationState CreateGravityUtilityState(
            bool startedUtilityWindupThisTick = false,
            bool startedUtilityRecoverThisTick = false,
            bool utilityCanceledThisTick = false,
            int tickIndex = 1)
        {
            return CreateUtilityState(
                EnemyUtilityPresentationKind.GravityFieldAura,
                startedUtilityWindupThisTick,
                startedUtilityRecoverThisTick,
                utilityCanceledThisTick,
                tickIndex);
        }

        private static EnemyViewPresentationState CreateUtilityState(
            EnemyUtilityPresentationKind utilityPresentationKind,
            bool startedUtilityWindupThisTick = false,
            bool startedUtilityRecoverThisTick = false,
            bool utilityCanceledThisTick = false,
            int tickIndex = 1)
        {
            return new EnemyViewPresentationState(
                entityId: 40,
                tickIndex: tickIndex,
                aiMode: EnemyAiMode.Patrol,
                activeActionKind: EnemyActionKind.None,
                jumpPhase: EnemyJumpPhase.None,
                chargePhase: EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: false,
                utilityPresentationKind: utilityPresentationKind,
                startedUtilityWindupThisTick: startedUtilityWindupThisTick,
                utilityPhase: startedUtilityRecoverThisTick
                    ? EnemyUtilityEffectPhase.Recover
                    : EnemyUtilityEffectPhase.Windup,
                startedUtilityRecoverThisTick: startedUtilityRecoverThisTick,
                utilityCanceledThisTick: utilityCanceledThisTick);
        }

        private static string ReadProjectText(string path)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static EnemyViewPresentationState CreateEnemyPresentationState(
            int tickIndex,
            EnemyJumpPhase jumpPhase = EnemyJumpPhase.None,
            EnemyChargePhase chargePhase = EnemyChargePhase.None,
            bool startedJumpAirborneThisTick = false,
            bool startedChargeActiveThisTick = false,
            bool didDie = false)
        {
            return new EnemyViewPresentationState(
                entityId: 40,
                tickIndex: tickIndex,
                aiMode: EnemyAiMode.Patrol,
                activeActionKind: EnemyActionKind.None,
                jumpPhase: jumpPhase,
                chargePhase: chargePhase,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: startedJumpAirborneThisTick,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: startedChargeActiveThisTick,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: didDie);
        }
    }
}
