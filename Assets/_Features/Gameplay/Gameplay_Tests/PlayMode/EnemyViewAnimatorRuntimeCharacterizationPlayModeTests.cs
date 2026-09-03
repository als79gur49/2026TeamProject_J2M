using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
#if UNITY_EDITOR
    public sealed class EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
    {
        private const int EnemyEntityId = 40;
        private const string EnemyPrefabRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs";
        private const string BlackEyePrefabPath = EnemyPrefabRoot + "/EnemyView_BlackEye.prefab";
        private const string AstretonPrefabPath = EnemyPrefabRoot + "/EnemyView_Astreton.prefab";
        private const string DrSaturnPrefabPath = EnemyPrefabRoot + "/EnemyView_DrSaturn.prefab";
        private const string RocketFacePrefabPath = EnemyPrefabRoot + "/EnemyView_RocketFace.prefab";
        private const string JPeterPrefabPath = EnemyPrefabRoot + "/EnemyView_JPeter.prefab";
        private const string SunwheelPrefabPath = EnemyPrefabRoot + "/EnemyView_Sunwheel.prefab";
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";

        private static readonly CubeTopologyState Topology = new(FaceId.Floor);
        private static readonly SurfaceCell EnemyCell = new(FaceId.Floor, 1, 1);
        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static readonly int HitStateHash = Animator.StringToHash("Hit");
        private static readonly int DeathStateHash = Animator.StringToHash("Death");
        private static readonly int JPeterMoveStateHash = Animator.StringToHash("Base Layer.Locomotion.Move");

        [UnityTest]
        [Category("Full")]
        public IEnumerator Astreton_JumpSignalsUseAuthoredStates_LandingAndCompletionReturnToMove()
        {
            var instance = InstantiateProductionPrefab(AstretonPrefabPath);
            try
            {
                var driver = RequireDriver(instance.GetComponent<GameplayEntityView>());
                var animator = RequireAnimator(instance);
                RebindDeterministically(animator);

                driver.Apply(CreateJumpState(1, EnemyJumpPhase.Windup, startedWindup: true));
                animator.Update(0.02f);
                AssertAnimatorState(animator, "JumpWindup");
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpWindup"));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.001f).Within(0.0001f));
                Assert.That(driver.JumpWindupSignalCount, Is.EqualTo(1));

                driver.Apply(CreateJumpState(2, EnemyJumpPhase.Airborne, startedAirborne: true));
                animator.Update(0.02f);
                AssertAnimatorState(animator, "JumpAirborne");
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(1));

                Assert.That(driver.ResyncAnimatorStateFromLastPresentation(), Is.True);
                animator.Update(0.02f);
                AssertAnimatorState(animator, "JumpAirborne");
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(1));

                driver.Apply(CreateJumpState(3, EnemyJumpPhase.None, landed: true));
                animator.Update(0.02f);
                AssertAnimatorState(animator, "Move");
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Move"));

                driver.Apply(CreateJumpState(4, EnemyJumpPhase.Airborne, startedAirborne: true));
                animator.Update(0.02f);
                driver.CompleteJumpLandingPresentation();
                animator.Update(0.02f);
                AssertAnimatorState(animator, "Move");
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Move"));
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            yield break;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator Astreton_DisabledAnimatorQueuesOnlyLatestState_AndConsumeDoesNotRepublishSignals()
        {
            var instance = InstantiateProductionPrefab(AstretonPrefabPath);
            try
            {
                var driver = RequireDriver(instance.GetComponent<GameplayEntityView>());
                var animator = RequireAnimator(instance);
                RebindDeterministically(animator);
                animator.enabled = false;

                driver.Apply(CreateJumpState(1, EnemyJumpPhase.Windup, startedWindup: true));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpWindup"));
                driver.Apply(CreateJumpState(2, EnemyJumpPhase.Airborne, startedAirborne: true));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
                Assert.That(driver.JumpWindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(1));

                animator.enabled = true;
                RebindDeterministically(animator);
                driver.SyncRuntimeState(isVisible: true, isMoving: false);
                animator.Update(0.02f);
                AssertAnimatorState(animator, "JumpAirborne");

                driver.SyncRuntimeState(isVisible: true, isMoving: false);
                Assert.That(driver.JumpWindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            yield break;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator DrSaturn_UtilityWindupTrackAppliesHalfSecondTiming_ThenRestoresBaseline()
        {
            var instance = InstantiateProductionPrefab(DrSaturnPrefabPath);
            try
            {
                var view = instance.GetComponent<GameplayEntityView>();
                var driver = RequireDriver(view);
                var animator = RequireAnimator(instance);
                RebindDeterministically(animator);

                driver.Apply(CreateUtilityState(1, windup: true));
                animator.Update(0.02f);
                Assert.That(driver.UtilityWindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero,
                    "Direct Driver Apply does not own Utility phase timing.");

                var coordinator = new GameplayAnimationSyncCoordinator();
                var views = new Dictionary<int, GameplayEntityView> { [EnemyEntityId] = view };
                coordinator.CacheDrivers(EnemyEntityId, view);
                coordinator.ApplyTickPresentation(
                    CreateUtilityTickResult(2, EnemyUtilityPresentationPhase.WindupStarted),
                    views,
                    (_, _) => 0f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));

                coordinator.ApplyTickPresentation(CreateUtilityTickResult(3), views, (_, _) => 0f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                coordinator.AdvancePresentation(0.51f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            yield break;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator DrSaturn_UtilityRecoveryTrackAppliesHalfSecondTiming_ThenRestoresBaseline()
        {
            var instance = InstantiateProductionPrefab(DrSaturnPrefabPath);
            try
            {
                var view = instance.GetComponent<GameplayEntityView>();
                var driver = RequireDriver(view);
                var animator = RequireAnimator(instance);
                RebindDeterministically(animator);

                driver.Apply(CreateUtilityState(1, windup: false));
                animator.Update(0.02f);
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);

                var coordinator = new GameplayAnimationSyncCoordinator();
                var views = new Dictionary<int, GameplayEntityView> { [EnemyEntityId] = view };
                coordinator.CacheDrivers(EnemyEntityId, view);
                coordinator.ApplyTickPresentation(
                    CreateUtilityTickResult(2, EnemyUtilityPresentationPhase.RecoverStarted),
                    views,
                    (_, _) => 0f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));

                coordinator.ApplyTickPresentation(CreateUtilityTickResult(3), views, (_, _) => 0f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                coordinator.AdvancePresentation(0.51f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            yield break;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator Sunwheel_UnusedCrossFadeDoesNotCreateHitOrDeathStateCommand()
        {
            var instance = InstantiateProductionPrefab(SunwheelPrefabPath);
            try
            {
                var driver = RequireDriver(instance.GetComponent<GameplayEntityView>());
                var animator = RequireAnimator(instance);
                RebindDeterministically(animator);

                driver.Apply(new EnemyViewPresentationState(
                    EnemyEntityId,
                    tickIndex: 1,
                    EnemyAiMode.None,
                    EnemyActionKind.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: true,
                    didDie: false));
                animator.Update(0.02f);
                Assert.That(driver.HitSignalCount, Is.EqualTo(1));
                Assert.That(driver.LastCrossFadedStateName, Is.Empty);

                driver.Apply(new EnemyViewPresentationState(
                    EnemyEntityId,
                    tickIndex: 2,
                    EnemyAiMode.Dead,
                    EnemyActionKind.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: true));
                animator.Update(0.02f);
                Assert.That(driver.DeathSignalCount, Is.EqualTo(1));
                Assert.That(driver.LastCrossFadedStateName, Is.Empty);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            yield break;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator BlackEye_ContactDelayedFatalHit_EntersEmptyHitStateAndRetainsOriginalView()
        {
            return AssertContactDelayedFatalHitCurrentPolicy(
                BlackEyePrefabPath,
                "EnemyAnimator_Attacking");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator RocketFace_ContactDelayedFatalHit_EntersEmptyHitStateAndRetainsOriginalView()
        {
            return AssertContactDelayedFatalHitCurrentPolicy(
                RocketFacePrefabPath,
                "EnemyAnimator_Charge");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator JPeter_ContactDelayedFatalHit_EntersEmptyHitStateAndRetainsOriginalView()
        {
            return AssertContactDelayedFatalHitCurrentPolicy(
                JPeterPrefabPath,
                "EnemyAnimator_JPeter_Fix");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator JPeter_SummonRecovery_IncrementsRecoveryCounterWithoutAnimatorDispatch()
        {
            var instance = InstantiateProductionPrefab(JPeterPrefabPath);
            try
            {
                var driver = RequireDriver(instance.GetComponent<GameplayEntityView>());
                var animator = RequireAnimator(instance);
                RebindDeterministically(animator);
                animator.Play(JPeterMoveStateHash, layer: 0, normalizedTime: 0.2f);
                animator.Update(0f);
                var stateBeforeRecovery = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;

                var serializedDriver = new SerializedObject(driver);
                Assert.That(
                    serializedDriver.FindProperty("recoveryTriggerName").stringValue,
                    Is.Empty,
                    "The current JPeter no-visual Summon recovery policy depends on its blank legacy recovery binding.");

                driver.Apply(CreateSummonRecoveryState(tickIndex: 1));
                animator.Update(0f);

                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
                Assert.That(driver.LastCrossFadedStateName, Is.Empty);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(stateBeforeRecovery),
                    "Summon recovery currently publishes the generic recovery counter but sends no Animator command.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            yield break;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator BlackEye_ImmediateFatalDeath_PassiveAttachPointsDoNotForceFallback()
        {
            return AssertPassiveAttachPointsPreserveDeathMotionSourceClone(
                BlackEyePrefabPath,
                expectedAttachPointCount: 2);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator DrSaturn_ImmediateFatalDeath_PassiveAttachPointDoesNotForceFallback()
        {
            return AssertPassiveAttachPointsPreserveDeathMotionSourceClone(
                DrSaturnPrefabPath,
                expectedAttachPointCount: 1);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator JPeter_ImmediateFatalDeath_SourceClonePreservesSampledVisiblePoseStrongContract()
        {
            var context = CreateContext(
                nameof(JPeter_ImmediateFatalDeath_SourceClonePreservesSampledVisiblePoseStrongContract),
                JPeterPrefabPath,
                enemyDeathEffectDurationSeconds: 1f);
            try
            {
                yield return null;

                var originalView = context.View;
                var originalAnimator = RequireAnimator(originalView.gameObject);
                Assert.That(originalAnimator.runtimeAnimatorController.name, Is.EqualTo("EnemyAnimator_JPeter_Fix"));
                var expectedPose = SampleJPeterNonDefaultVisiblePose(originalView, originalAnimator);

                context.Host.Presenter.Present(CreateFatalResult(
                    tickIndex: 2,
                    EntityExitPresentationTiming.Immediate));

                Assert.That(originalView.gameObject.activeSelf, Is.False,
                    "Immediate enemy death must clean up the original registered View in the presentation pass.");

                var cloneRoot = RequireActiveDeathMotionClone(context.RootObject, context.VfxRuntime);
                var cloneAnimator = RequireAnimator(cloneRoot.gameObject);
                var cloneVisibleRoot = cloneRoot.Find("J");
                Assert.That(cloneVisibleRoot, Is.Not.Null);
                Assert.That(cloneAnimator.enabled, Is.False,
                    "DeathMotion must disable the cloned Animator before the clone becomes active.");
                AssertLocalPose(cloneVisibleRoot, expectedPose, "immediately after Present");
                var motionRoot = cloneRoot.parent;
                var motionStartPosition = motionRoot.localPosition;

                yield return null;
                Assert.That(cloneRoot != null && cloneRoot.gameObject.activeInHierarchy, Is.True);
                AssertLocalPose(cloneVisibleRoot, expectedPose, "after the first Animator/LateUpdate frame");

                for (var frame = 1; frame < 12; frame++)
                {
                    yield return null;
                    Assert.That(cloneRoot != null && cloneRoot.gameObject.activeInHierarchy, Is.True,
                        $"The 1-second endurance fixture released the clone before frame {frame}.");
                    AssertLocalPose(cloneVisibleRoot, expectedPose, $"at endurance frame {frame}");
                }

                Assert.That(Vector3.Distance(motionRoot.localPosition, motionStartPosition), Is.GreaterThan(0.001f),
                    "Freezing the cloned model pose must not freeze the parent DeathMotion fly-away.");
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator JPeter_ImmediateFatalDeath_DefaultDurationPreservesPoseThroughFirstPostFrame()
        {
            var context = CreateContext(
                nameof(JPeter_ImmediateFatalDeath_DefaultDurationPreservesPoseThroughFirstPostFrame),
                JPeterPrefabPath);
            try
            {
                yield return null;

                var originalAnimator = RequireAnimator(context.View.gameObject);
                var expectedPose = SampleJPeterNonDefaultVisiblePose(context.View, originalAnimator);
                context.Host.Presenter.Present(CreateFatalResult(
                    tickIndex: 3,
                    EntityExitPresentationTiming.Immediate));

                var cloneRoot = RequireActiveDeathMotionClone(context.RootObject, context.VfxRuntime);
                var cloneVisibleRoot = cloneRoot.Find("J");
                Assert.That(cloneVisibleRoot, Is.Not.Null);
                AssertLocalPose(cloneVisibleRoot, expectedPose, "immediately after Present at production duration");

                yield return null;

                Assert.That(cloneRoot != null && cloneRoot.gameObject.activeInHierarchy, Is.True,
                    "The production-duration clone must survive through its first post-Present frame.");
                AssertLocalPose(cloneVisibleRoot, expectedPose, "after the first production-duration frame");
            }
            finally
            {
                context.Dispose();
            }
        }

        private static IEnumerator AssertContactDelayedFatalHitCurrentPolicy(
            string prefabPath,
            string expectedControllerName)
        {
            var context = CreateContext(
                nameof(AssertContactDelayedFatalHitCurrentPolicy) + "_" + expectedControllerName,
                prefabPath);
            try
            {
                yield return null;

                var view = context.View;
                var driver = RequireDriver(view);
                var animator = RequireAnimator(view.gameObject);
                Assert.That(animator.runtimeAnimatorController.name, Is.EqualTo(expectedControllerName));

                context.Host.Presenter.Present(CreateFatalResult(
                    tickIndex: 1,
                    EntityExitPresentationTiming.AtContactTime));

                var snapshot = context.Host.Presenter.DebugCaptureEntityPresentationLifecycle(EnemyEntityId);
                Assert.That(driver.HitSignalCount, Is.EqualTo(1));
                Assert.That(driver.DeathSignalCount, Is.EqualTo(1),
                    "CurrentPolicy: production typed animation dispatch emits Death in the tick pass, " +
                    "while AtContactTime separately retains the original View until visual contact.");
                Assert.That(snapshot.ViewsByEntityIdContainsEntityId, Is.True);
                Assert.That(snapshot.ContactDelayedRetainedEntityIdsContainsEntityId, Is.True);
                Assert.That(snapshot.PendingContactExitContainsEntityId, Is.True);
                Assert.That(snapshot.GameObjectActiveSelf, Is.True);
                Assert.That(snapshot.RendererActiveInHierarchy, Is.True);

                animator.Update(0f);
                var observation = CaptureAnimatorObservation(animator, frame: 1);
                Assert.That(
                    observation.CurrentStateHash == HitStateHash || observation.NextStateHash == HitStateHash,
                    Is.True,
                    $"CurrentPolicy: production trigger dispatch must enter or transition toward Hit. {observation.Description}");
                Assert.That(
                    ResolveClipCountForState(animator, HitStateHash),
                    Is.Zero,
                    $"CurrentPolicy: '{expectedControllerName}' currently uses Hit as an empty-Motion pose hold. " +
                    observation.Description);
                TestContext.WriteLine($"{expectedControllerName} contact-delayed Hit Animator: {observation.Description}");
            }
            finally
            {
                context.Dispose();
            }
        }

        private static IEnumerator AssertPassiveAttachPointsPreserveDeathMotionSourceClone(
            string prefabPath,
            int expectedAttachPointCount)
        {
            var context = CreateContext(
                nameof(AssertPassiveAttachPointsPreserveDeathMotionSourceClone) + "_" + expectedAttachPointCount,
                prefabPath);
            try
            {
                yield return null;

                var sourceAttachPoints = context.View.ModelRoot
                    .GetComponentsInChildren<GameplayVfxAttachPoint>(includeInactive: true);
                Assert.That(sourceAttachPoints, Has.Length.EqualTo(expectedAttachPointCount),
                    "The production fixture must retain the passive marker configuration that caused the regression.");
                Assert.That(sourceAttachPoints.All(attachPoint => attachPoint.enabled), Is.True);

                context.Host.Presenter.Present(CreateFatalResult(
                    tickIndex: 4,
                    EntityExitPresentationTiming.Immediate));

                var cloneRoot = RequireActiveDeathMotionClone(context.RootObject, context.VfxRuntime);
                var cloneRenderers = cloneRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
                Assert.That(cloneRenderers.Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy), Is.True,
                    "The DeathMotion source clone must retain a visible production silhouette.");
                var fallbackRenderers = cloneRoot.parent
                    .GetComponentsInChildren<Renderer>(includeInactive: true)
                    .Where(renderer => !renderer.transform.IsChildOf(cloneRoot))
                    .ToArray();
                Assert.That(fallbackRenderers, Is.Not.Empty,
                    "The production DeathMotion fixture must contain authored fallback visuals.");
                Assert.That(fallbackRenderers.All(renderer => !renderer.enabled), Is.True,
                    "A successful source clone must hide the generic authored fallback silhouette.");
                Assert.That(RequireAnimator(cloneRoot.gameObject).enabled, Is.False);
                Assert.That(
                    cloneRoot.GetComponentsInChildren<GameplayVfxAttachPoint>(includeInactive: true)
                        .All(attachPoint => !attachPoint.enabled),
                    Is.True,
                    "Source-only attach metadata must be disabled before the transient clone is activated.");

                yield return null;

                Assert.That(cloneRoot != null && cloneRoot.gameObject.activeInHierarchy, Is.True);
                Assert.That(
                    cloneRoot.GetComponentsInChildren<GameplayVfxAttachPoint>(includeInactive: true),
                    Is.Empty,
                    "Deferred clone sanitation must remove source-only attach metadata by the end of the frame.");
            }
            finally
            {
                context.Dispose();
            }
        }

        private static GameObject InstantiateProductionPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing production prefab at '{prefabPath}'.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = prefab.name + "_CharacterizationClone";
            instance.SetActive(true);
            return instance;
        }

        private static void RebindDeterministically(Animator animator)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        private static void AssertAnimatorState(Animator animator, string expectedState)
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(
                current.IsName(expectedState) ||
                current.IsName("Base Layer." + expectedState) ||
                current.IsName("Base Layer.Locomotion." + expectedState),
                Is.True,
                $"Expected production Animator state '{expectedState}', actual short hash={current.shortNameHash}.");
        }

        private static EnemyViewPresentationState CreateJumpState(
            int tickIndex,
            EnemyJumpPhase phase,
            bool startedWindup = false,
            bool startedAirborne = false,
            bool landed = false)
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
                landedFromJumpThisTick: landed,
                retryingJumpAirborneThisTick: false,
                tookDamage: false,
                didDie: false);
        }

        private static EnemyViewPresentationState CreateUtilityState(int tickIndex, bool windup)
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

        private static EnemyViewPresentationState CreateSummonRecoveryState(int tickIndex)
        {
            return new EnemyViewPresentationState(
                EnemyEntityId,
                tickIndex,
                EnemyAiMode.Recover,
                EnemyActionKind.None,
                EnemyJumpPhase.None,
                EnemyChargePhase.None,
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
                startedSummonRecoverThisTick: true,
                summonEffectIndex: 0,
                summonActivationSequence: 1);
        }

        private static TickResult CreateUtilityTickResult(
            int tickIndex,
            EnemyUtilityPresentationPhase phase = EnemyUtilityPresentationPhase.None)
        {
            var utilitySignals = phase == EnemyUtilityPresentationPhase.None
                ? Array.Empty<TickEnemyUtilityPresentationSignal>()
                : new[]
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
                };
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
                enemyUtilitySignals: utilitySignals);
            var result = new TickResult(tickIndex, Array.Empty<TickPhase>(), Array.Empty<string>());
            SetPrivateField(result, "<PresentationData>k__BackingField", presentationData);
            SetPrivateField(result, "<FinalTopology>k__BackingField", Topology);
            SetPrivateField(result, "<Trace>k__BackingField", TickTrace.Empty);
            SetPrivateField(result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetPrivateField(
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState> { CreateEnemy() }));
            SetPrivateField(
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string>()));
            return result;
        }

        private static RuntimeContext CreateContext(
            string rootName,
            string prefabPath,
            float enemyDeathEffectDurationSeconds = -1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing production enemy prefab at '{prefabPath}'.");
            var cueMap = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(HostDefaultCueMapPath);
            Assert.That(cueMap, Is.Not.Null, $"Missing production VFX cue map at '{HostDefaultCueMapPath}'.");

            var rootObject = new GameObject(rootName);
            rootObject.SetActive(false);
            var boardRootObject = new GameObject("GameplayBoardRoot");
            boardRootObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
            boardRoot.EnsureHierarchy();
            var cameraObject = new GameObject("OutputCamera");
            cameraObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            cameraObject.transform.localPosition = new Vector3(0f, 2f, -10f);
            cameraObject.transform.localRotation = Quaternion.identity;
            var outputCamera = cameraObject.AddComponent<Camera>();
            var vfxRuntime = rootObject.AddComponent<GameplayVfxProductionRuntime>();
            SetPrivateField(vfxRuntime, "hostDefaultCueMap", cueMap);
            var host = rootObject.AddComponent<GameplaySceneHost>();
            var viewFactory = new DefaultGameplayEntityViewFactory(
                boardRoot.EntityRoot,
                cellSize: 1f,
                playerEntityId: 10,
                enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                {
                    { EnemyEntityId, prefab },
                });

            rootObject.SetActive(true);
            host.Initialize(new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                CellSize = 1f,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                InitialEntities = new[] { CreateEnemy() },
                InitialTopology = Topology,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
                ViewFactory = viewFactory,
                ViewCamera = outputCamera,
                SnapViewCameraToTarget = false,
                EnemyDeathEffectDurationSeconds = enemyDeathEffectDurationSeconds,
            });

            Assert.That(host.ViewRegistry.TryGetView(EnemyEntityId, out var view), Is.True);
            Assert.That(view, Is.Not.Null);
            return new RuntimeContext(rootObject, host, view, vfxRuntime);
        }

        private static TickResult CreateFatalResult(
            int tickIndex,
            EntityExitPresentationTiming timing)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyDamagePresentationSignal(
                        EnemyEntityId,
                        tookDamageThisTick: true,
                        damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        EnemyEntityId,
                        TickEntityExitCause.Killed,
                        EnemyCell,
                        Topology,
                        Direction.Right,
                        EntityType.Unit,
                        sourceActorEntityId: 10,
                        presentationSeed: 9040,
                        timing: timing,
                        visualContactNormalizedTime: timing == EntityExitPresentationTiming.AtContactTime ? 0.9f : 0f),
                });

            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetPrivateField(result, "<PresentationData>k__BackingField", presentationData);
            SetPrivateField(result, "<FinalTopology>k__BackingField", Topology);
            SetPrivateField(result, "<DeterminismHash>k__BackingField", $"ENEMY-VIEW-ANIMATOR-{tickIndex}");
            SetPrivateField(result, "<Trace>k__BackingField", TickTrace.Empty);
            SetPrivateField(result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetPrivateField(
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>()));
            SetPrivateField(
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string>()));
            return result;
        }

        private static EntityState CreateEnemy()
        {
            return new EntityState
            {
                entityId = EnemyEntityId,
                position = EnemyCell,
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EnemyAnimatorDriver RequireDriver(GameplayEntityView view)
        {
            var driver = view.GetComponent<EnemyAnimatorDriver>();
            Assert.That(driver, Is.Not.Null, $"'{view.name}' is missing {nameof(EnemyAnimatorDriver)}.");
            return driver;
        }

        private static LocalPose SampleJPeterNonDefaultVisiblePose(
            GameplayEntityView view,
            Animator animator)
        {
            var visibleRoot = view.ModelRoot.Find("J");
            Assert.That(visibleRoot, Is.Not.Null, "JPeter production ModelRoot must contain the animated 'J' visual root.");
            Assert.That(visibleRoot.GetComponentsInChildren<Renderer>(includeInactive: true).Length, Is.GreaterThan(0),
                "The JP Peter pose probe must address a visible renderer hierarchy, not an inert transform.");
            var defaultPosition = visibleRoot.localPosition;

            animator.speed = 0f;
            animator.Play(JPeterMoveStateHash, layer: 0, normalizedTime: 0.24f);
            animator.Update(0f);

            var state = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.fullPathHash, Is.EqualTo(JPeterMoveStateHash),
                "The pose probe must sample the exact full-path Move state; short Idle hashes are ambiguous in this controller.");
            Assert.That(Mathf.Abs(visibleRoot.localPosition.z - defaultPosition.z), Is.GreaterThan(1f),
                "The sampled JP Peter pose must differ materially from the controller default or the regression could pass vacuously.");
            return new LocalPose(visibleRoot.localPosition, visibleRoot.localRotation, visibleRoot.localScale);
        }

        private static void AssertLocalPose(Transform actual, in LocalPose expected, string observation)
        {
            Assert.That(Vector3.Distance(actual.localPosition, expected.Position), Is.LessThanOrEqualTo(0.0001f),
                $"JPeter DeathMotion clone position drifted {observation}.");
            Assert.That(Quaternion.Angle(actual.localRotation, expected.Rotation), Is.LessThanOrEqualTo(0.1f),
                $"JPeter DeathMotion clone rotation drifted {observation}.");
            Assert.That(Vector3.Distance(actual.localScale, expected.Scale), Is.LessThanOrEqualTo(0.0001f),
                $"JPeter DeathMotion clone scale drifted {observation}.");
        }

        private static Animator RequireAnimator(GameObject owner)
        {
            var animator = owner.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.That(animator, Is.Not.Null, $"'{owner.name}' is missing an Animator.");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null, $"'{owner.name}' Animator is missing a controller.");
            return animator;
        }

        private static Transform RequireActiveDeathMotionClone(
            GameObject rootObject,
            GameplayVfxProductionRuntime vfxRuntime)
        {
            var transforms = rootObject.GetComponentsInChildren<Transform>(includeInactive: true);
            var clone = transforms
                .FirstOrDefault(candidate =>
                    candidate.name == "ParameterizedMotionCloneRoot" &&
                    candidate.gameObject.activeInHierarchy);
            Assert.That(clone, Is.Not.Null,
                "Enemy DeathMotion VFX must capture an active source clone before immediate original-View cleanup. " +
                $"VFX initialized={vfxRuntime.IsRuntimeInitialized}, planned={vfxRuntime.LastPlannedRequestCount}, " +
                $"active={vfxRuntime.ActiveVfxInstanceCount}, missingBinding={vfxRuntime.MissingBindingCount}, " +
                $"missingAnchor={vfxRuntime.MissingAnchorCount}, missingPrefab={vfxRuntime.MissingPrefabCount}, " +
                $"missingSource={vfxRuntime.MissingSourceViewCount}. " +
                "Hierarchy=" + string.Join(", ", transforms.Select(candidate =>
                    $"{candidate.name}[self={candidate.gameObject.activeSelf},hier={candidate.gameObject.activeInHierarchy}]")));
            return clone;
        }

        private static int ResolveClipCountForState(Animator animator, int stateHash)
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == stateHash)
            {
                return animator.GetCurrentAnimatorClipInfo(0).Length;
            }

            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == stateHash)
                {
                    return animator.GetNextAnimatorClipInfo(0).Length;
                }
            }

            return -1;
        }

        private static AnimatorObservation CaptureAnimatorObservation(Animator animator, int frame)
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var inTransition = animator.IsInTransition(0);
            var next = inTransition ? animator.GetNextAnimatorStateInfo(0) : default;
            return new AnimatorObservation(
                current.shortNameHash,
                inTransition ? next.shortNameHash : 0,
                $"frame={frame}, current={DescribeState(current.shortNameHash)}, " +
                $"next={DescribeState(inTransition ? next.shortNameHash : 0)}, " +
                $"transition={inTransition}, currentClips={animator.GetCurrentAnimatorClipInfo(0).Length}, " +
                $"nextClips={(inTransition ? animator.GetNextAnimatorClipInfo(0).Length : 0)}");
        }

        private static string DescribeState(int stateHash)
        {
            if (stateHash == 0)
            {
                return "None(0)";
            }

            if (stateHash == HitStateHash)
            {
                return $"Hit({stateHash})";
            }

            if (stateHash == DeathStateHash)
            {
                return $"Death({stateHash})";
            }

            if (stateHash == IdleStateHash)
            {
                return $"Idle({stateHash})";
            }

            return $"Other({stateHash})";
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private readonly struct AnimatorObservation
        {
            public AnimatorObservation(int currentStateHash, int nextStateHash, string description)
            {
                CurrentStateHash = currentStateHash;
                NextStateHash = nextStateHash;
                Description = description;
            }

            public int CurrentStateHash { get; }

            public int NextStateHash { get; }

            public string Description { get; }
        }

        private readonly struct LocalPose
        {
            public LocalPose(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public Vector3 Position { get; }

            public Quaternion Rotation { get; }

            public Vector3 Scale { get; }
        }

        private sealed class RuntimeContext : IDisposable
        {
            public RuntimeContext(
                GameObject rootObject,
                GameplaySceneHost host,
                GameplayEntityView view,
                GameplayVfxProductionRuntime vfxRuntime)
            {
                RootObject = rootObject;
                Host = host;
                View = view;
                VfxRuntime = vfxRuntime;
            }

            public GameObject RootObject { get; }

            public GameplaySceneHost Host { get; }

            public GameplayEntityView View { get; }

            public GameplayVfxProductionRuntime VfxRuntime { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(RootObject);
            }
        }
    }
#endif
}
