using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    [Category("Full")]
    public sealed class EnemyAnimationSparseBindingRuntimeScenarioTests
    {
        private const int EnemyEntityId = 40;

        [Test]
        public void JumpingSyntheticValid_FirstAirborneSignalUsesTrigger_ResyncUsesStateWithoutNewSignal()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                JumpingSyntheticValid_FirstAirborneSignalUsesTrigger_ResyncUsesStateWithoutNewSignal), -1f);

            fixture.Driver.Apply(JumpState(1, EnemyJumpPhase.Airborne, startedAirborne: true));
            fixture.Animator.Update(0.02f);

            AssertState(fixture.Animator, "JumpAirborneTriggerTrap");
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));

            fixture.Animator.Play("JumpAirborne", 0, 0.25f);
            fixture.Animator.Update(0f);
            Assert.That(fixture.Driver.ResyncAnimatorStateFromLastPresentation(), Is.True);
            fixture.Animator.Update(0.02f);

            AssertState(fixture.Animator, "JumpAirborne");
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));
        }

        [Test]
        public void JumpingSyntheticValid_TriggerIsNotPendingWhileAnimatorDisabled_StateEnsureRestoresAirborne()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                JumpingSyntheticValid_TriggerIsNotPendingWhileAnimatorDisabled_StateEnsureRestoresAirborne), -1f);
            fixture.Animator.enabled = false;

            fixture.Driver.Apply(JumpState(1, EnemyJumpPhase.Airborne, startedAirborne: true));
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));

            fixture.Animator.enabled = true;
            fixture.Animator.Rebind();
            fixture.Animator.Update(0f);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            fixture.Animator.Update(0.02f);

            AssertState(fixture.Animator, "JumpAirborne");
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));
        }

        [Test]
        public void CrossFadeOverride_SelectsStateBranchAndExcludesTriggerTrap()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                CrossFadeOverride_SelectsStateBranchAndExcludesTriggerTrap), 0.001f);

            fixture.Driver.Apply(JumpState(1, EnemyJumpPhase.Airborne, startedAirborne: true));
            fixture.Animator.Update(0.02f);

            AssertState(fixture.Animator, "JumpAirborne");
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
            Assert.That(fixture.Driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.001f).Within(0.0001f));
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));
        }

        [Test]
        public void PendingStateSlot_IsLastWriteWins_AndConsumeDoesNotRepublishSignals()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                PendingStateSlot_IsLastWriteWins_AndConsumeDoesNotRepublishSignals), 0.001f);
            fixture.Animator.enabled = false;

            fixture.Driver.Apply(JumpState(1, EnemyJumpPhase.Windup, startedWindup: true));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("JumpWindup"));
            fixture.Driver.Apply(JumpState(2, EnemyJumpPhase.Airborne, startedAirborne: true));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
            Assert.That(fixture.Driver.JumpWindupSignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));

            fixture.Animator.enabled = true;
            fixture.Animator.Rebind();
            fixture.Animator.Update(0f);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            fixture.Animator.Update(0.02f);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);

            AssertState(fixture.Animator, "JumpAirborne");
            Assert.That(fixture.Driver.JumpWindupSignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));
        }

        [Test]
        public void RecoveryAndHitSameTick_RecoveryOwnsTiming_WhileHitTriggerDispatchesOnce()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                RecoveryAndHitSameTick_RecoveryOwnsTiming_WhileHitTriggerDispatchesOnce), 0.001f);

            fixture.Driver.Apply(ActionState(
                tickIndex: 1,
                aiMode: EnemyAiMode.Recover,
                startedRecovery: true,
                tookDamage: true));
            fixture.Animator.Update(0.02f);
            fixture.Animator.Update(0.02f);

            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Animator.speed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
            Assert.That(fixture.Driver.RecoverySignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.HitSignalCount, Is.EqualTo(1));
            AssertState(fixture.Animator, "HitTriggerTrap");
        }

        [Test]
        public void UtilityWindupAndRecovery_UseTriggerBranches_ButDirectApplyDoesNotRetainUtilityTiming()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                UtilityWindupAndRecovery_UseTriggerBranches_ButDirectApplyDoesNotRetainUtilityTiming), -1f);

            fixture.Driver.Apply(UtilityState(1, windup: true));
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "WindupTriggerTrap");
            Assert.That(fixture.Driver.UtilityWindupSignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero,
                "Utility phase timing is not selected by the general state resolver.");

            fixture.Animator.Rebind();
            fixture.Animator.Update(0f);
            fixture.Driver.Apply(UtilityState(2, windup: false));
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "RecoveryTriggerTrap");
            Assert.That(fixture.Driver.RecoverySignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero,
                "Utility recovery timing also requires the coordinator-owned playback track.");
        }

        [Test]
        public void LegacyGlideWithoutCrossFade_KeepsGenericWindupAndRecoveryTriggerFallbacks()
        {
            using var windupFixture = RuntimeFixture.Create(nameof(
                LegacyGlideWithoutCrossFade_KeepsGenericWindupAndRecoveryTriggerFallbacks) + "_Windup", -1f);
            windupFixture.Driver.Apply(GlideState(EnemyGlidePhase.Windup));
            windupFixture.Animator.Update(0.02f);
            AssertState(windupFixture.Animator, "WindupTriggerTrap");

            using var recoveryFixture = RuntimeFixture.Create(nameof(
                LegacyGlideWithoutCrossFade_KeepsGenericWindupAndRecoveryTriggerFallbacks) + "_Recovery", -1f);
            recoveryFixture.Driver.Apply(GlideState(EnemyGlidePhase.Recovery));
            recoveryFixture.Animator.Update(0.02f);
            AssertState(recoveryFixture.Animator, "RecoveryTriggerTrap");
        }

        [Test]
        public void UtilityTrackSuppression_KeepsComputedTiming_SuppressesOnlyPlayback_AndRestoresAfterExpiry()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                UtilityTrackSuppression_KeepsComputedTiming_SuppressesOnlyPlayback_AndRestoresAfterExpiry), -1f);
            var coordinator = new GameplayAnimationSyncCoordinator();
            var views = new Dictionary<int, GameplayEntityView> { [EnemyEntityId] = fixture.View };
            coordinator.CacheDrivers(EnemyEntityId, fixture.View);
            coordinator.ApplyTickPresentation(CreateUtilityTick(1), views, (_, _) => 0f);
            var signalCount = fixture.Driver.UtilityWindupSignalCount;

            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));

            coordinator.SyncEnemyRuntimeState(
                EnemyEntityId,
                isVisible: true,
                isMoving: false,
                playbackSuppressed: true,
                viewsByEntityId: views);
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Animator.speed, Is.Zero);

            coordinator.SyncEnemyRuntimeState(
                EnemyEntityId,
                isVisible: true,
                isMoving: false,
                playbackSuppressed: false,
                viewsByEntityId: views);
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Animator.speed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Driver.UtilityWindupSignalCount, Is.EqualTo(signalCount));

            coordinator.AdvancePresentation(0.51f);
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            Assert.That(fixture.Animator.speed, Is.EqualTo(1f));
            Assert.That(fixture.Driver.UtilityWindupSignalCount, Is.EqualTo(signalCount));
        }

        [Test]
        public void TriggerOnlyCues_AreNotGeneralResyncTargets_AndAreNotRepublished()
        {
            var cases = new[]
            {
                new ResyncCase("TriggerOnlyWindup", ActionState(1, EnemyAiMode.Attack, startedWindup: true),
                    "WindupTriggerTrap"),
                new ResyncCase("TriggerOnlyRecovery", ActionState(1, EnemyAiMode.Recover, startedRecovery: true),
                    "RecoveryTriggerTrap"),
                new ResyncCase("TriggerOnlyHit", ActionState(1, EnemyAiMode.None, tookDamage: true),
                    "HitTriggerTrap"),
                new ResyncCase("TriggerOnlyDeath", DeathState(tookDamage: false), "DeathTriggerTrap"),
            };

            foreach (var testCase in cases)
            {
                using var fixture = RuntimeFixture.Create(testCase.Name, -1f);
                fixture.Driver.Apply(testCase.State);
                fixture.Animator.Update(0.02f);
                AssertState(fixture.Animator, testCase.ExpectedState, testCase.Name);
                var signalCount = TotalSignalCount(fixture.Driver);

                fixture.Animator.Rebind();
                fixture.Animator.Update(0f);
                Assert.That(fixture.Driver.ResyncAnimatorStateFromLastPresentation(), Is.False, testCase.Name);
                fixture.Animator.Update(0.02f);

                AssertState(fixture.Animator, "Move", testCase.Name);
                Assert.That(TotalSignalCount(fixture.Driver), Is.EqualTo(signalCount), testCase.Name);
            }
        }

        [Test]
        public void SustainedStateResyncMatrix_RestoresStateWithoutIncreasingSemanticCounters()
        {
            var cases = new[]
            {
                new ResyncCase("ActionWindup", ActionState(1, EnemyAiMode.Attack, startedWindup: true), "Windup"),
                new ResyncCase("ActionRecovery", ActionState(1, EnemyAiMode.Recover, startedRecovery: true), "Recover"),
                new ResyncCase("JumpWindup", JumpState(1, EnemyJumpPhase.Windup, startedWindup: true), "JumpWindup"),
                new ResyncCase("JumpAirborne", JumpState(1, EnemyJumpPhase.Airborne, startedAirborne: true), "JumpAirborne"),
                new ResyncCase("ChargeWindup", ChargeState(EnemyChargePhase.Windup), "Windup"),
                new ResyncCase("ChargeActive", ChargeState(EnemyChargePhase.Active), "Charge"),
                new ResyncCase("ChargeRecovery", ChargeState(EnemyChargePhase.Recover), "Recover"),
                new ResyncCase("GlideWindup", GlideState(EnemyGlidePhase.Windup), "Fly_Start"),
                new ResyncCase("GlideActive", GlideState(EnemyGlidePhase.Active), "Fly_Loop"),
                new ResyncCase("GlideRecovery", GlideState(EnemyGlidePhase.Recovery), "Fly_Done"),
            };

            foreach (var testCase in cases)
            {
                using var fixture = RuntimeFixture.Create(testCase.Name, 0.001f);
                fixture.Driver.Apply(testCase.State);
                var signalCount = TotalSignalCount(fixture.Driver);
                fixture.Animator.Rebind();
                fixture.Animator.Update(0f);

                Assert.That(fixture.Driver.ResyncAnimatorStateFromLastPresentation(), Is.True, testCase.Name);
                fixture.Animator.Update(0.02f);

                AssertState(fixture.Animator, testCase.ExpectedState, testCase.Name);
                Assert.That(TotalSignalCount(fixture.Driver), Is.EqualTo(signalCount), testCase.Name);
            }
        }

        [Test]
        public void HiddenAndActivePlaybackSuppression_PreserveCalculatedTimingAndDoNotRepublishSignal()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                HiddenAndActivePlaybackSuppression_PreserveCalculatedTimingAndDoNotRepublishSignal), 0.001f);
            fixture.Driver.Apply(ActionState(1, EnemyAiMode.Attack, startedWindup: true));
            var signalCount = fixture.Driver.WindupSignalCount;

            fixture.Driver.SyncHiddenRuntimeState(isMoving: false, playbackSuppressed: true);
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Animator.speed, Is.EqualTo(2f).Within(0.0001f),
                "Hidden sync updates calculated properties without driving Animator playback.");

            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);
            Assert.That(fixture.Animator.speed, Is.Zero);
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));

            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);
            Assert.That(fixture.Animator.speed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Driver.WindupSignalCount, Is.EqualTo(signalCount));
        }

        [Test]
        public void SharedDeathApply_WithSuppressionMask_SuppressesCueButKeepsDeathTiming()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                SharedDeathApply_WithSuppressionMask_SuppressesCueButKeepsDeathTiming), -1f, false);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);

            fixture.Driver.Apply(
                DeathState(tookDamage: false),
                EnemyPresentationOneShotBlockMask.DeathTrigger);

            Assert.That(fixture.Driver.DeathSignalCount, Is.Zero);
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);
            Assert.That(fixture.Animator.speed, Is.Zero);
        }

        [Test]
        public void SharedDeathApply_Unsuppressed_PreservesCurrentSignalOrderAndDeathTimingPriority()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                SharedDeathApply_Unsuppressed_PreservesCurrentSignalOrderAndDeathTimingPriority), 0.001f);

            fixture.Driver.Apply(DeathState(tookDamage: true, startedRecovery: true));

            Assert.That(fixture.Driver.RecoverySignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.HitSignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.DeathSignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);
            Assert.That(fixture.Animator.speed, Is.EqualTo(1f));
        }

        [Test]
        public void TypedDeathPlayback_RemovesUtilityTrackBeforePlayingDeathCue()
        {
            using var fixture = RuntimeFixture.Create(nameof(
                TypedDeathPlayback_RemovesUtilityTrackBeforePlayingDeathCue), -1f);
            var coordinator = new GameplayAnimationSyncCoordinator();
            var views = new Dictionary<int, GameplayEntityView> { [EnemyEntityId] = fixture.View };
            coordinator.CacheDrivers(EnemyEntityId, fixture.View);
            coordinator.ApplyTickPresentation(CreateUtilityTick(1), views, (_, _) => 0f);
            Assert.That(GetUtilityTrackCount(coordinator), Is.EqualTo(1));

            var request = new GameplayEnemyPresentationPlaybackRequest(
                new EnemyPresentationPlaybackKey(
                    2,
                    PresentationSemanticSource.EntityExit,
                    EnemyEntityId,
                    PresentationAnimationCueKey.EnemyDeath,
                    PresentationEnemyPresentationKind.Death,
                    PresentationEnemyPresentationPhase.Death,
                    sourceSequenceId: 1),
                PresentationAnimationCueKey.EnemyDeath,
                new PresentationEnemyPayload(
                    PresentationEnemyPresentationKind.Death,
                    PresentationEnemyPresentationPhase.Death,
                    EnemyEntityId,
                    sourceTickIndex: 2,
                    sourceSequenceId: 1,
                    outcome: PresentationEnemyPresentationOutcome.Death),
                new PresentationAnimationPayload(
                    PresentationAnimationFactKind.EnemyPresentation,
                    EnemyEntityId,
                    PresentationAnimationActionKind.EnemyDeath,
                    PresentationAnimationPhaseKind.Death,
                    PresentationAnimationOutcomeKind.Death,
                    sourceTickIndex: 2,
                    sourceSequenceId: 1),
                PresentationTarget.Entity(EnemyEntityId),
                PresentationAnchor.ForEntityVisualRoot(EnemyEntityId));

            Assert.That(coordinator.TryApplyEnemyPresentationPlayback(request, views, out _), Is.True);
            Assert.That(GetUtilityTrackCount(coordinator), Is.Zero);
            Assert.That(fixture.Driver.DeathSignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);
        }

        [Test]
        public void SunwheelLikeUnusedCrossFade_DoesNotCreateStateCommandForHitOrDeathTriggers()
        {
            using var hitFixture = RuntimeFixture.Create("SunwheelHit", 0.001f, false);
            hitFixture.Driver.Apply(ActionState(1, EnemyAiMode.None, tookDamage: true));
            hitFixture.Animator.Update(0.02f);
            Assert.That(hitFixture.Driver.LastCrossFadedStateName, Is.Empty);
            Assert.That(hitFixture.Driver.HitSignalCount, Is.EqualTo(1));
            AssertState(hitFixture.Animator, "HitTriggerTrap");

            using var deathFixture = RuntimeFixture.Create("SunwheelDeath", 0.001f, false);
            deathFixture.Driver.Apply(DeathState(tookDamage: false));
            deathFixture.Animator.Update(0.02f);
            Assert.That(deathFixture.Driver.LastCrossFadedStateName, Is.Empty);
            Assert.That(deathFixture.Driver.DeathSignalCount, Is.EqualTo(1));
            AssertState(deathFixture.Animator, "DeathTriggerTrap");
        }

        [Test]
        public void NewBinding_RecoveryAndHit_PreservesTimingCountersAndDispatchOrder()
        {
            using var fixture = RuntimeFixture.CreateWithNewBinding(nameof(
                NewBinding_RecoveryAndHit_PreservesTimingCountersAndDispatchOrder));

            fixture.Driver.Apply(ActionState(
                tickIndex: 1,
                aiMode: EnemyAiMode.Recover,
                startedRecovery: true,
                tookDamage: true));
            fixture.Animator.Update(0.02f);
            fixture.Animator.Update(0.02f);

            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
            Assert.That(fixture.Driver.RecoverySignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.HitSignalCount, Is.EqualTo(1));
            AssertState(fixture.Animator, "HitTriggerTrap");
        }

        [Test]
        public void NewBinding_UtilityTrack_UsesExactWindupAndRecoveryCuesAndRestoresBaseline()
        {
            using var fixture = RuntimeFixture.CreateWithNewBinding(nameof(
                NewBinding_UtilityTrack_UsesExactWindupAndRecoveryCuesAndRestoresBaseline));
            var coordinator = new GameplayAnimationSyncCoordinator();
            var views = new Dictionary<int, GameplayEntityView> { [EnemyEntityId] = fixture.View };
            coordinator.CacheDrivers(EnemyEntityId, fixture.View);

            coordinator.ApplyTickPresentation(CreateUtilityTick(1), views, (_, _) => 0f);
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "WindupTriggerTrap");
            Assert.That(fixture.Driver.UtilityWindupSignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            coordinator.SyncEnemyRuntimeState(
                EnemyEntityId,
                isVisible: true,
                isMoving: false,
                playbackSuppressed: true,
                viewsByEntityId: views);
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fixture.Animator.speed, Is.Zero);
            coordinator.AdvancePresentation(0.51f);
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));

            coordinator.ApplyTickPresentation(
                CreateUtilityTick(2, EnemyUtilityPresentationPhase.RecoverStarted),
                views,
                (_, _) => 0f);
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "RecoveryTriggerTrap");
            Assert.That(fixture.Driver.RecoverySignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(4f).Within(0.0001f));
            coordinator.AdvancePresentation(0.26f);
            Assert.That(fixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);
            Assert.That(fixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
        }

        [Test]
        public void NewBinding_DeathEntryPaths_KeepDeathTimingAndRemoveUtilityTrack()
        {
            using var sharedFixture = RuntimeFixture.CreateWithNewBinding(nameof(
                NewBinding_DeathEntryPaths_KeepDeathTimingAndRemoveUtilityTrack) + "_Shared");
            sharedFixture.Driver.Apply(DeathState(tookDamage: false));
            sharedFixture.Animator.Update(0.02f);
            AssertState(sharedFixture.Animator, "DeathTriggerTrap");
            Assert.That(sharedFixture.Driver.DeathSignalCount, Is.EqualTo(1));
            Assert.That(sharedFixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            Assert.That(sharedFixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);

            using var suppressedFixture = RuntimeFixture.CreateWithNewBinding(nameof(
                NewBinding_DeathEntryPaths_KeepDeathTimingAndRemoveUtilityTrack) + "_Suppressed");
            suppressedFixture.Driver.Apply(
                DeathState(tookDamage: false),
                EnemyPresentationOneShotBlockMask.DeathTrigger);
            suppressedFixture.Animator.Update(0.02f);
            AssertState(suppressedFixture.Animator, "Move");
            Assert.That(suppressedFixture.Driver.DeathSignalCount, Is.Zero);
            Assert.That(suppressedFixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            Assert.That(suppressedFixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);

            using var typedFixture = RuntimeFixture.CreateWithNewBinding(nameof(
                NewBinding_DeathEntryPaths_KeepDeathTimingAndRemoveUtilityTrack) + "_Typed");
            var coordinator = new GameplayAnimationSyncCoordinator();
            var views = new Dictionary<int, GameplayEntityView> { [EnemyEntityId] = typedFixture.View };
            coordinator.CacheDrivers(EnemyEntityId, typedFixture.View);
            coordinator.ApplyTickPresentation(CreateUtilityTick(1), views, (_, _) => 0f);
            typedFixture.Animator.Update(0.02f);
            Assert.That(GetUtilityTrackCount(coordinator), Is.EqualTo(1));
            Assert.That(
                coordinator.TryApplyEnemyPresentationPlayback(CreateDeathPlaybackRequest(), views, out _),
                Is.True);
            typedFixture.Animator.Update(0.02f);
            AssertState(typedFixture.Animator, "DeathTriggerTrap");
            Assert.That(GetUtilityTrackCount(coordinator), Is.Zero);
            Assert.That(typedFixture.Driver.DeathSignalCount, Is.EqualTo(1));
            Assert.That(typedFixture.Driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            Assert.That(typedFixture.Driver.CurrentPresentationDurationSeconds, Is.Zero);

            typedFixture.Animator.Rebind();
            typedFixture.Animator.Update(0f);
#pragma warning disable CS0618
            Assert.That(coordinator.BeginEnemyDeathPresentation(EnemyEntityId, views), Is.Zero);
#pragma warning restore CS0618
            typedFixture.Animator.Update(0.02f);
            AssertState(typedFixture.Animator, "DeathTriggerTrap");
            Assert.That(typedFixture.Driver.DeathSignalCount, Is.EqualTo(2));
        }

        [Test]
        public void NewBinding_JumpTopologyPreservesNormalizedTime_WhileGeneralResyncReenters()
        {
            using var fixture = RuntimeFixture.CreateWithNewBinding(nameof(
                NewBinding_JumpTopologyPreservesNormalizedTime_WhileGeneralResyncReenters));
            fixture.Driver.Apply(JumpState(1, EnemyJumpPhase.Airborne, startedAirborne: true));
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "JumpAirborneTriggerTrap");
            var signalCount = fixture.Driver.JumpAirborneSignalCount;

            fixture.Animator.Play("JumpAirborne", 0, 0.42f);
            fixture.Animator.Update(0f);
            fixture.Driver.SyncHiddenRuntimeState(isMoving: false, playbackSuppressed: true);
            Assert.That(fixture.Driver.HasJumpAirborneTopologySuspendSnapshot, Is.True);
            fixture.Animator.Rebind();
            fixture.Animator.Update(0f);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);
            var restored = fixture.Animator.GetCurrentAnimatorStateInfo(0);
            AssertState(fixture.Animator, "JumpAirborne");
            Assert.That(restored.normalizedTime, Is.EqualTo(0.42f).Within(0.03f));
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(signalCount));

            fixture.Animator.Play("JumpAirborne", 0, 0.73f);
            fixture.Animator.Update(0f);
            fixture.Animator.Play("Move", 0, 0f);
            fixture.Animator.Update(0f);
            Assert.That(fixture.Driver.ResyncAnimatorStateFromLastPresentation(), Is.True);
            fixture.Animator.Update(0.02f);
            var resynced = fixture.Animator.GetCurrentAnimatorStateInfo(0);
            AssertState(fixture.Animator, "JumpAirborne");
            Assert.That(resynced.normalizedTime, Is.LessThan(0.2f));
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(signalCount));
        }

        private static EnemyViewPresentationState ActionState(
            int tickIndex,
            EnemyAiMode aiMode,
            bool startedWindup = false,
            bool startedRecovery = false,
            bool tookDamage = false)
        {
            return new EnemyViewPresentationState(
                EnemyEntityId,
                tickIndex,
                aiMode,
                EnemyActionKind.Melee,
                isMoving: false,
                startedWindup,
                executedThisTick: false,
                startedRecovery,
                tookDamage,
                didDie: false);
        }

        private static EnemyViewPresentationState JumpState(
            int tickIndex,
            EnemyJumpPhase phase,
            bool startedWindup = false,
            bool startedAirborne = false)
        {
            return new EnemyViewPresentationState(
                EnemyEntityId,
                tickIndex,
                EnemyAiMode.Patrol,
                EnemyActionKind.None,
                phase,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: startedWindup,
                startedJumpAirborneThisTick: startedAirborne,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                tookDamage: false,
                didDie: false);
        }

        private static EnemyViewPresentationState ChargeState(EnemyChargePhase phase)
        {
            return new EnemyViewPresentationState(
                EnemyEntityId,
                tickIndex: 1,
                EnemyAiMode.Charge,
                EnemyActionKind.None,
                EnemyJumpPhase.None,
                phase,
                isMoving: false,
                startedWindupThisTick: phase == EnemyChargePhase.Windup,
                executedThisTick: false,
                startedRecoveryThisTick: phase == EnemyChargePhase.Recover,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: phase == EnemyChargePhase.Windup,
                startedChargeActiveThisTick: phase == EnemyChargePhase.Active,
                startedChargeRecoverThisTick: phase == EnemyChargePhase.Recover,
                tookDamage: false,
                didDie: false);
        }

        private static EnemyViewPresentationState GlideState(EnemyGlidePhase phase)
        {
            return new EnemyViewPresentationState(
                EnemyEntityId,
                tickIndex: 1,
                EnemyAiMode.Chase,
                EnemyActionKind.None,
                EnemyJumpPhase.None,
                EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: phase == EnemyGlidePhase.Windup,
                executedThisTick: false,
                startedRecoveryThisTick: phase == EnemyGlidePhase.Recovery,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: false,
                glidePhase: phase,
                startedGlideWindupThisTick: phase == EnemyGlidePhase.Windup,
                startedGlideActiveThisTick: phase == EnemyGlidePhase.Active,
                startedGlideRecoverThisTick: phase == EnemyGlidePhase.Recovery);
        }

        private static EnemyViewPresentationState UtilityState(int tickIndex, bool windup)
        {
            return new EnemyViewPresentationState(
                EnemyEntityId,
                tickIndex,
                EnemyAiMode.None,
                EnemyActionKind.None,
                EnemyJumpPhase.None,
                EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: !windup,
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
                startedUtilityWindupThisTick: windup,
                utilityPhase: windup ? EnemyUtilityEffectPhase.Windup : EnemyUtilityEffectPhase.Recover,
                startedUtilityRecoverThisTick: !windup,
                utilityEffectIndex: 0,
                utilityActivationSequence: 1);
        }

        private static EnemyViewPresentationState DeathState(bool tookDamage, bool startedRecovery = false)
        {
            return new EnemyViewPresentationState(
                EnemyEntityId,
                tickIndex: 1,
                startedRecovery ? EnemyAiMode.Recover : EnemyAiMode.Dead,
                EnemyActionKind.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: startedRecovery,
                tookDamage,
                didDie: true);
        }

        private static TickResult CreateUtilityTick(
            int tickIndex,
            EnemyUtilityPresentationPhase phase = EnemyUtilityPresentationPhase.WindupStarted)
        {
            var presentationData = new TickPresentationData(
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                enemyUtilitySignals: new[]
                {
                    new TickEnemyUtilityPresentationSignal(
                        EnemyEntityId,
                        EnemyUtilityPresentationKind.GravityFieldAura,
                        phase,
                        startTick: tickIndex,
                        executeTick: tickIndex + 1,
                        durationTicks: 1,
                        effectIndex: 0,
                        activationSequence: 1),
                });
            var result = new TickResult(tickIndex, Array.Empty<TickPhase>(), Array.Empty<string>());
            SetField(result, "<PresentationData>k__BackingField", presentationData);
            SetField(result, "<FinalTopology>k__BackingField", new CubeTopologyState(FaceId.Floor));
            SetField(result, "_finalEntities", new ReadOnlyCollection<EntityState>(new List<EntityState>
            {
                new()
                {
                    entityId = EnemyEntityId,
                    hp = 1,
                    maxHp = 1,
                    type = EntityType.Unit,
                    unitRole = UnitRole.Enemy,
                    boardPresence = EntityBoardPresence.Occupying,
                },
            }));
            return result;
        }

        private static GameplayEnemyPresentationPlaybackRequest CreateDeathPlaybackRequest()
        {
            return new GameplayEnemyPresentationPlaybackRequest(
                new EnemyPresentationPlaybackKey(
                    2,
                    PresentationSemanticSource.EntityExit,
                    EnemyEntityId,
                    PresentationAnimationCueKey.EnemyDeath,
                    PresentationEnemyPresentationKind.Death,
                    PresentationEnemyPresentationPhase.Death,
                    sourceSequenceId: 1),
                PresentationAnimationCueKey.EnemyDeath,
                new PresentationEnemyPayload(
                    PresentationEnemyPresentationKind.Death,
                    PresentationEnemyPresentationPhase.Death,
                    EnemyEntityId,
                    sourceTickIndex: 2,
                    sourceSequenceId: 1,
                    outcome: PresentationEnemyPresentationOutcome.Death),
                new PresentationAnimationPayload(
                    PresentationAnimationFactKind.EnemyPresentation,
                    EnemyEntityId,
                    PresentationAnimationActionKind.EnemyDeath,
                    PresentationAnimationPhaseKind.Death,
                    PresentationAnimationOutcomeKind.Death,
                    sourceTickIndex: 2,
                    sourceSequenceId: 1),
                PresentationTarget.Entity(EnemyEntityId),
                PresentationAnchor.ForEntityVisualRoot(EnemyEntityId));
        }

        private static int GetUtilityTrackCount(GameplayAnimationSyncCoordinator coordinator)
        {
            var field = typeof(GameplayAnimationSyncCoordinator).GetField(
                "_enemyUtilityAnimationTracks",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return ((IDictionary)field.GetValue(coordinator)).Count;
        }

        private static int TotalSignalCount(EnemyAnimatorDriver driver)
        {
            return driver.WindupSignalCount +
                   driver.AttackSignalCount +
                   driver.RecoverySignalCount +
                   driver.JumpWindupSignalCount +
                   driver.JumpAirborneSignalCount +
                   driver.ChargeActiveSignalCount +
                   driver.GlideWindupSignalCount +
                   driver.GlideActiveSignalCount +
                   driver.GlideRecoverySignalCount +
                   driver.UtilityWindupSignalCount +
                   driver.HitSignalCount +
                   driver.DeathSignalCount;
        }

        private static void AssertState(Animator animator, string expected, string message = null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.IsName(expected) || state.IsName("Base Layer." + expected), Is.True,
                message ?? $"Expected Animator state '{expected}', actual short hash={state.shortNameHash}.");
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private readonly struct ResyncCase
        {
            public ResyncCase(string name, EnemyViewPresentationState state, string expectedState)
            {
                Name = name;
                State = state;
                ExpectedState = expectedState;
            }

            public string Name { get; }
            public EnemyViewPresentationState State { get; }
            public string ExpectedState { get; }
        }

        private sealed class RuntimeFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> _ownedObjects;

            private RuntimeFixture(
                GameObject root,
                GameplayEntityView view,
                Animator animator,
                EnemyAnimatorDriver driver,
                List<UnityEngine.Object> ownedObjects)
            {
                Root = root;
                View = view;
                Animator = animator;
                Driver = driver;
                _ownedObjects = ownedObjects;
            }

            public GameObject Root { get; }
            public GameplayEntityView View { get; }
            public Animator Animator { get; }
            public EnemyAnimatorDriver Driver { get; }

            public static RuntimeFixture Create(string name, float crossFade, bool authoredDurations = true)
            {
                var ownedObjects = new List<UnityEngine.Object>();
                var root = new GameObject(name);
                var view = root.AddComponent<GameplayEntityView>();
                var animatorObject = new GameObject(name + "_Animator");
                animatorObject.transform.SetParent(root.transform, worldPositionStays: false);
                var animator = animatorObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = CreateController(name, ownedObjects);
                var timing = root.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = root.AddComponent<EnemyAnimatorDriver>();
                SetField(driver, "animator", animator);
                SetField(driver, "animationTimingAuthoring", timing);

                SetField(timing, "stateTransitionCrossFadeDurationSeconds", crossFade);
                if (authoredDurations)
                {
                    SetField(timing, "attackWindupAnimatorDurationSeconds", 0.5f);
                    SetField(timing, "jumpWindupAnimatorDurationSeconds", 0.5f);
                    SetField(timing, "jumpAirborneAnimatorDurationSeconds", 0.5f);
                    SetField(timing, "recoverAnimatorDurationSeconds", 0.5f);
                    SetField(timing, "attackWindupReferenceClip", FindClip(ownedObjects, "Windup"));
                    SetField(timing, "jumpWindupReferenceClip", FindClip(ownedObjects, "JumpWindup"));
                    SetField(timing, "jumpAirborneReferenceClip", FindClip(ownedObjects, "JumpAirborne"));
                    SetField(timing, "recoverReferenceClip", FindClip(ownedObjects, "Recover"));
                }

                timing.Validate();
                animator.Rebind();
                animator.Update(0f);
                return new RuntimeFixture(root, view, animator, driver, ownedObjects);
            }

            public static RuntimeFixture CreateWithNewBinding(string name)
            {
                var fixture = Create(name, crossFade: -1f);
                var bindings = new[]
                {
                    TimedState(EnemyAnimationCue.ActionWindup, "Windup", fixture, 0.5f),
                    Trigger(EnemyAnimationCue.ActionExecute, "Attack"),
                    TimedState(EnemyAnimationCue.ActionRecovery, "Recover", fixture, 0.5f),
                    TimedState(EnemyAnimationCue.JumpWindup, "JumpWindup", fixture, 0.5f),
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.JumpAirborne,
                        EnemyAnimationDispatchMode.Trigger,
                        "JumpAirborne",
                        sustainedStateName: "JumpAirborne",
                        animatorDurationSeconds: 0.5f,
                        referenceClip: FindClip(fixture._ownedObjects, "JumpAirborne")),
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.JumpLanding,
                        EnemyAnimationDispatchMode.State,
                        "Move"),
                    TimedState(EnemyAnimationCue.ChargeWindup, "Windup", fixture, 0.5f),
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.ChargeActive,
                        EnemyAnimationDispatchMode.State,
                        "Charge"),
                    TimedState(EnemyAnimationCue.ChargeRecovery, "Recover", fixture, 0.5f),
                    TimedState(EnemyAnimationCue.GlideWindup, "Fly_Start", fixture, 0.5f),
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.GlideActive,
                        EnemyAnimationDispatchMode.State,
                        "Fly_Loop"),
                    TimedState(EnemyAnimationCue.GlideRecovery, "Fly_Done", fixture, 0.5f),
                    TimedTrigger(EnemyAnimationCue.UtilityWindup, "Windup", "Windup", fixture, 0.5f),
                    TimedTrigger(EnemyAnimationCue.UtilityRecovery, "Recover", "Recover", fixture, 0.25f),
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death"),
                };
                var authoring = fixture.Root.AddComponent<EnemyAnimationBindingAuthoring>();
                authoring.ConfigureForTests(bindings, stateCrossFadeDurationSeconds: 0f);
                authoring.Validate();
                return fixture;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                for (var index = _ownedObjects.Count - 1; index >= 0; index--)
                {
                    if (_ownedObjects[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_ownedObjects[index]);
                    }
                }
            }

            private static AnimationClip FindClip(IEnumerable<UnityEngine.Object> objects, string stateName)
            {
                return objects.OfType<AnimationClip>().Single(clip => clip.name.EndsWith("_" + stateName));
            }

            private static EnemyAnimationCueBinding Trigger(EnemyAnimationCue cue, string target)
            {
                return EnemyAnimationCueBinding.CreateForTests(
                    cue,
                    EnemyAnimationDispatchMode.Trigger,
                    target);
            }

            private static EnemyAnimationCueBinding TimedState(
                EnemyAnimationCue cue,
                string target,
                RuntimeFixture fixture,
                float durationSeconds)
            {
                return EnemyAnimationCueBinding.CreateForTests(
                    cue,
                    EnemyAnimationDispatchMode.State,
                    target,
                    animatorDurationSeconds: durationSeconds,
                    referenceClip: FindClip(fixture._ownedObjects, target));
            }

            private static EnemyAnimationCueBinding TimedTrigger(
                EnemyAnimationCue cue,
                string target,
                string clipStateName,
                RuntimeFixture fixture,
                float durationSeconds)
            {
                return EnemyAnimationCueBinding.CreateForTests(
                    cue,
                    EnemyAnimationDispatchMode.Trigger,
                    target,
                    animatorDurationSeconds: durationSeconds,
                    referenceClip: FindClip(fixture._ownedObjects, clipStateName));
            }

            private static AnimatorController CreateController(string name, ICollection<UnityEngine.Object> ownedObjects)
            {
                var stateMachine = new AnimatorStateMachine
                {
                    name = name + "_StateMachine",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                ownedObjects.Add(stateMachine);
                var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
                foreach (var stateName in new[]
                         {
                             "Move", "Windup", "Recover", "JumpWindup", "JumpAirborne", "Charge",
                             "Fly_Start", "Fly_Loop", "Fly_Done", "WindupTriggerTrap", "RecoveryTriggerTrap",
                             "JumpAirborneTriggerTrap", "HitTriggerTrap", "DeathTriggerTrap",
                         })
                {
                    var state = stateMachine.AddState(stateName);
                    var clip = CreateClip(name + "_" + stateName);
                    state.motion = clip;
                    states.Add(stateName, state);
                    ownedObjects.Add(clip);
                }

                stateMachine.defaultState = states["Move"];
                var controller = new AnimatorController
                {
                    name = name + "_Controller",
                    hideFlags = HideFlags.HideAndDontSave,
                    layers = new[]
                    {
                        new AnimatorControllerLayer
                        {
                            name = "Base Layer",
                            defaultWeight = 1f,
                            stateMachine = stateMachine,
                        },
                    },
                };
                ownedObjects.Add(controller);
                foreach (var trigger in new[] { "Windup", "Recover", "JumpWindup", "JumpAirborne", "Attack", "Hit", "Death" })
                {
                    controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                }

                controller.AddParameter("EnemyAiMode", AnimatorControllerParameterType.Int);
                controller.AddParameter("EnemyActionKind", AnimatorControllerParameterType.Int);
                controller.AddParameter("EnemyJumpPhase", AnimatorControllerParameterType.Int);
                controller.AddParameter("EnemyChargePhase", AnimatorControllerParameterType.Int);
                controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
                AddTriggerTransition(stateMachine, states["WindupTriggerTrap"], "Windup");
                AddTriggerTransition(stateMachine, states["RecoveryTriggerTrap"], "Recover");
                AddTriggerTransition(stateMachine, states["JumpAirborneTriggerTrap"], "JumpAirborne");
                AddTriggerTransition(stateMachine, states["HitTriggerTrap"], "Hit");
                AddTriggerTransition(stateMachine, states["DeathTriggerTrap"], "Death");
                return controller;
            }

            private static void AddTriggerTransition(
                AnimatorStateMachine stateMachine,
                AnimatorState destination,
                string parameter)
            {
                var transition = stateMachine.AddAnyStateTransition(destination);
                transition.hasExitTime = false;
                transition.duration = 0f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            }

            private static AnimationClip CreateClip(string name)
            {
                var clip = new AnimationClip
                {
                    name = name,
                    frameRate = 60f,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                clip.SetCurve(
                    string.Empty,
                    typeof(Transform),
                    "m_LocalPosition.x",
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
                return clip;
            }
        }
    }
}
