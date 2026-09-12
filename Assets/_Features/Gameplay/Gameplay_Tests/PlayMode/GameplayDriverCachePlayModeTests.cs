using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using Game.Feature.Gameplay.Model.Phases;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.BoardState;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class GameplayDriverCachePlayModeTests
    {
        private const string JPeterPath = "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_JPeter.prefab";

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualJPeter_PauseDisableReappearAndReplacement_PreserveLifecycle()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(JPeterPath);
            Assert.That(prefab, Is.Not.Null);
            var root = new GameObject(nameof(GameplayDriverCachePlayModeTests));
            var sync = new GameplayAnimationSyncCoordinator { DriverCacheDiagnosticsEnabled = true };
            try
            {
                var view = Object.Instantiate(prefab, root.transform);
                view.Initialize(40);
                var views = new Dictionary<int, GameplayEntityView> { [40] = view };
                sync.CacheDrivers(40, view);
                var pulse = view.GetComponent<EnemySummonScalePulsePresentationDriver>();
                var original = view.ModelRoot.localScale;
                pulse.Apply(State(windup: true));
                // Semantic must be consumed before autonomous Advance on the first paused frame.
                pulse.ApplyEnemyVisualSemanticState(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive,
                    shouldPauseAnimatorPlayback: true, shouldPauseAutonomousPresentation: true));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.5f);
                Assert.That(pulse.CurrentScaleMultiplier, Is.EqualTo(1f));
                pulse.ApplyEnemyVisualSemanticState(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
                Assert.That(pulse.CurrentScaleMultiplier, Is.GreaterThan(1f));
                var progressed = pulse.CurrentScaleMultiplier;
                sync.CacheDrivers(40, view);
                Assert.That(pulse.CurrentScaleMultiplier, Is.EqualTo(progressed));
                view.gameObject.SetActive(false);
                yield return null;
                Assert.That(pulse.IsPlaying, Is.False);
                Assert.That(view.ModelRoot.localScale, Is.EqualTo(original));
                view.gameObject.SetActive(true);
                yield return null;
                sync.CacheDrivers(40, view);
                Assert.That(sync.DriverComponentLookupCount, Is.EqualTo(3));
                pulse.Apply(State(windup: true));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
                Assert.That(pulse.CurrentScaleMultiplier, Is.GreaterThan(1f));
                var replacement = Object.Instantiate(prefab, root.transform);
                replacement.Initialize(40);
                views[40] = replacement;
                Assert.That(sync.TryGetEnemyScalePulseDriver(40, views, out var next), Is.True);
                Assert.That(next, Is.SameAs(replacement.GetComponent<EnemySummonScalePulsePresentationDriver>()));
                Assert.That(view.ModelRoot.localScale, Is.EqualTo(original));
                Assert.That(pulse.IsPlaying, Is.False);
                Assert.That(next.IsPlaying, Is.False);
                Assert.That(sync.DriverComponentLookupCount, Is.EqualTo(6));
                next.Apply(State(windup: true));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
                sync.ReleaseEntity(40);
                Assert.That(next.IsPlaying, Is.False);
                Assert.That(next.CurrentScaleMultiplier, Is.EqualTo(1f));
                sync.CacheDrivers(40, replacement);
                Assert.That(sync.DriverComponentLookupCount, Is.EqualTo(9));
                next.Apply(State(windup: true));
                sync.Reset();
                Assert.That(next.IsPlaying, Is.False);
                Assert.That(next.CurrentScaleMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                sync.Reset();
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualJPeter_RecoverCancelDeathAndDestroyedComponent_AreSafe()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(JPeterPath);
            var view = Object.Instantiate(prefab);
            view.Initialize(40);
            var sync = new GameplayAnimationSyncCoordinator { DriverCacheDiagnosticsEnabled = true };
            try
            {
                sync.CacheDrivers(40, view);
                var pulse = view.GetComponent<EnemySummonScalePulsePresentationDriver>();
                var original = view.ModelRoot.localScale;
                pulse.Apply(State(windup: true));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(pulse.WindupDurationSeconds);
                Assert.That(pulse.CurrentScaleMultiplier, Is.EqualTo(pulse.WindupEndScaleMultiplier).Within(0.0001f));
                pulse.Apply(State(recover: true));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(pulse.RecoverDurationSeconds);
                Assert.That(view.ModelRoot.localScale, Is.EqualTo(original));
                pulse.Apply(State(windup: true));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
                pulse.Apply(State(canceled: true));
                Assert.That(view.ModelRoot.localScale, Is.EqualTo(original));
                pulse.Apply(State(windup: true));
                sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
                pulse.Apply(State(dead: true));
                Assert.That(view.ModelRoot.localScale, Is.EqualTo(original));
                Object.Destroy(pulse);
                yield return null;
                Assert.DoesNotThrow(() => sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f));
                sync.CacheDrivers(40, view);
                Assert.That(sync.DestroyedCachedDriverCount, Is.EqualTo(1));
                Assert.That(sync.DriverComponentLookupCount, Is.EqualTo(3));
                sync.ReleaseEntity(40);
            }
            finally
            {
                sync.Reset();
                Object.DestroyImmediate(view.gameObject);
            }
        }

        private const string DrSaturnPath = "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab";

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_DeathReplacement_RestoresAnimatorWithoutSignals() => CheckDeathReplacement(false);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_InactiveDeathReplacement_RestoresOnResume() => CheckDeathReplacement(true);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_UtilityReplacement_RestoresAnimatorAndRemainingTime() => CheckUtilityReplacement(false, false);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_InactiveUtilityReplacement_RestoresOnResume() => CheckUtilityReplacement(true, false);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_ExpiredInactiveUtilityReplacement_DoesNotReplay() => CheckUtilityReplacement(true, true);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_DeathReplacementWithAirborneState_KeepsDeath() => CheckDeathReplacement(false, true);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_PendingUtilityReplacementDies_ResumesDeath() => CheckUtilityReplacement(true, true, true);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_UtilityRecoveryReplacement_RestoresRecoverAndRemainingTime() =>
            CheckUtilityReplacement(false, false, recovery: true);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_InactiveUtilityRecoveryReplacement_RestoresRecoverOnResume() =>
            CheckUtilityReplacement(true, false, recovery: true);

        [UnityTest, Category("Full")]
        public IEnumerator ActualDrSaturn_InactiveUnpausedFatalHit_PreservesDeathAfterUtilityExpiry()
        {
            var root = new GameObject("FatalHitReplacement");
            var sync = new GameplayAnimationSyncCoordinator { DriverCacheDiagnosticsEnabled = true };
            try
            {
                var oldView = CreateDrSaturn(root.transform);
                var views = new Dictionary<int, GameplayEntityView> { [40] = oldView };
                sync.CacheDrivers(40, oldView);
                sync.ApplyTickPresentation(UtilityResult(true), views, (_, _) => 0f);
                var duration = oldView.GetComponent<EnemyAnimatorDriver>().CurrentPresentationDurationSeconds;
                Assert.That(duration, Is.GreaterThan(0f));
                sync.AdvancePresentationBeforeEnemySemantic(duration * 0.4f);

                var replacement = CreateDrSaturn(root.transform);
                replacement.gameObject.SetActive(false);
                var driver = replacement.GetComponent<EnemyAnimatorDriver>();
                views[40] = replacement;
                sync.CacheDrivers(40, replacement);

                driver.Apply(State(dead: true, damaged: true));
                Assert.That(driver.HitSignalCount, Is.EqualTo(1));
                Assert.That(driver.DeathSignalCount, Is.EqualTo(1));
                sync.AdvancePresentationBeforeEnemySemantic(duration);
                yield return null;

                replacement.gameObject.SetActive(true);
                sync.SyncEnemyRuntimeState(40, true, false, false, views);
                sync.ResyncEnemyAnimatorState(40, views);
                var animator = replacement.GetComponentInChildren<Animator>();
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True);

                sync.SyncEnemyRuntimeState(40, true, false, false, views);
                sync.ResyncEnemyAnimatorState(40, views);
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True);
                Assert.That(driver.HitSignalCount, Is.EqualTo(1));
                Assert.That(driver.DeathSignalCount, Is.EqualTo(1));
            }
            finally { sync.Reset(); Object.DestroyImmediate(root); }
        }

        private static IEnumerator CheckDeathReplacement(bool inactive, bool airborne = false)
        {
            var root = new GameObject("DeathReplacement");
            var sync = new GameplayAnimationSyncCoordinator { DriverCacheDiagnosticsEnabled = true };
            try
            {
                var oldView = CreateDrSaturn(root.transform);
                sync.CacheDrivers(40, oldView);
                var oldDriver = oldView.GetComponent<EnemyAnimatorDriver>();
                if (airborne)
                {
                    oldDriver.SetPresentationPaused(true);
                    oldDriver.Apply(State(airborne: true));
                }
                oldDriver.PlayDeathPresentation(40);
                var replacement = CreateDrSaturn(root.transform);
                replacement.gameObject.SetActive(!inactive);
                var driver = replacement.GetComponent<EnemyAnimatorDriver>();
                driver.SetPresentationPaused(inactive);
                var views = new Dictionary<int, GameplayEntityView> { [40] = replacement };
                sync.CacheDrivers(40, replacement);
                if (inactive)
                {
                    yield return null;
                    replacement.gameObject.SetActive(true);
                    driver.SetPresentationPaused(false);
                }
                sync.SyncEnemyRuntimeState(40, true, false, false, views);
                sync.ResyncEnemyAnimatorState(40, views);
                var animator = replacement.GetComponentInChildren<Animator>();
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True);
                Assert.That(driver.DeathSignalCount, Is.Zero);
                animator.Update(0.05f);
                var progress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                for (var i = 0; i < 10; i++)
                {
                    sync.CacheDrivers(40, replacement);
                    sync.SyncEnemyRuntimeState(40, true, false, false, views);
                }
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(progress).Within(0.0001f));
                Assert.That(sync.DriverComponentLookupCount, Is.EqualTo(6));
            }
            finally { sync.Reset(); Object.DestroyImmediate(root); }
        }

        private static IEnumerator CheckUtilityReplacement(bool inactive, bool expire, bool dieBeforeResume = false, bool recovery = false)
        {
            var root = new GameObject("UtilityReplacement");
            var sync = new GameplayAnimationSyncCoordinator { DriverCacheDiagnosticsEnabled = true };
            try
            {
                var oldView = CreateDrSaturn(root.transform);
                var views = new Dictionary<int, GameplayEntityView> { [40] = oldView };
                sync.CacheDrivers(40, oldView);
                sync.ApplyTickPresentation(UtilityResult(true, recovery), views, (_, _) => 0f);
                var duration = oldView.GetComponent<EnemyAnimatorDriver>().CurrentPresentationDurationSeconds;
                Assert.That(duration, Is.GreaterThan(0f));
                sync.AdvancePresentationBeforeEnemySemantic(duration * 0.4f);
                sync.ApplyTickPresentation(UtilityResult(false), views, (_, _) => 0f);
                var replacement = CreateDrSaturn(root.transform);
                replacement.gameObject.SetActive(!inactive);
                var driver = replacement.GetComponent<EnemyAnimatorDriver>();
                driver.SetPresentationPaused(inactive);
                views[40] = replacement;
                sync.CacheDrivers(40, replacement);
                if (inactive)
                {
                    if (dieBeforeResume)
                    {
                        driver.PlayDeathPresentation(40);
                    }
                    sync.AdvancePresentationBeforeEnemySemantic(duration * (expire ? 0.7f : 0.1f));
                    yield return null;
                    replacement.gameObject.SetActive(true);
                    driver.SetPresentationPaused(false);
                }
                sync.SyncEnemyRuntimeState(40, true, false, false, views);
                var animator = replacement.GetComponentInChildren<Animator>();
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(dieBeforeResume ? "Death" : expire ? "Move" : recovery ? "Recover" : "Windup"), Is.True);
                Assert.That(driver.UtilityWindupSignalCount, Is.Zero);
                Assert.That(driver.WindupSignalCount, Is.Zero);
                Assert.That(driver.DeathSignalCount, Is.Zero);
                if (!expire)
                {
                    var progress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    Assert.That(progress, Is.EqualTo(inactive ? 0.5f : 0.4f).Within(0.001f));
                    for (var i = 0; i < 10; i++)
                    {
                        sync.CacheDrivers(40, replacement);
                        sync.SyncEnemyRuntimeState(40, true, false, false, views);
                    }
                    animator.Update(0f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(progress).Within(0.001f));
                    sync.AdvancePresentationBeforeEnemySemantic(duration * (inactive ? 0.49f : 0.59f));
                    Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(duration).Within(0.001f));
                    sync.AdvancePresentationBeforeEnemySemantic(duration * 0.02f);
                }
                if (!dieBeforeResume)
                {
                    Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                }
                Assert.That(sync.DriverComponentLookupCount, Is.EqualTo(6));
            }
            finally { sync.Reset(); Object.DestroyImmediate(root); }
        }

        private static GameplayEntityView CreateDrSaturn(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(DrSaturnPath);
            Assert.That(prefab, Is.Not.Null);
            var view = Object.Instantiate(prefab, parent);
            view.Initialize(40);
            view.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return view;
        }

        private static TickResult UtilityResult(bool start, bool recovery = false)
        {
            var entity = new EntityState
            {
                entityId = 40, type = EntityType.Unit, unitRole = UnitRole.Enemy,
                position = new SurfaceCell(FaceId.Floor, 0, 0), hp = 1, maxHp = 1,
                boardPresence = EntityBoardPresence.Occupying, facing = Direction.Right, aiMode = EnemyAiMode.Patrol,
            };
            var data = new TickPresentationData(
                Array.Empty<TickEntityMotion>(), null, Array.Empty<TickVisibilityChange>(), Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(), Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(), Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(), Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(), Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                enemyUtilitySignals: start ? new[] { new TickEnemyUtilityPresentationSignal(40,
                    EnemyUtilityPresentationKind.GravityFieldAura, recovery ? EnemyUtilityPresentationPhase.RecoverStarted : EnemyUtilityPresentationPhase.WindupStarted,
                    startTick: 1, executeTick: 3, durationTicks: 2, effectIndex: 0, activationSequence: 5) } : null);
            // PlayMode has no access to simulation's internal constructor; supply only
            // the read-only presentation inputs, as other presentation PlayMode fixtures do.
            var result = new TickResult(1, Array.Empty<TickPhase>(), Array.Empty<string>());
            typeof(TickResult).GetField("<PresentationData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(result, data);
            typeof(TickResult).GetField("_finalEntities", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(result, new ReadOnlyCollection<EntityState>(new[] { entity }));
            return result;
        }

        private static EnemyViewPresentationState State(bool windup = false, bool recover = false,
            bool canceled = false, bool dead = false, bool airborne = false, bool damaged = false) => new EnemyViewPresentationState(
                40, 1, EnemyAiMode.Patrol, EnemyActionKind.None,
                airborne ? EnemyJumpPhase.Airborne : EnemyJumpPhase.None, EnemyChargePhase.None,
                false, false, false, false, false, false, false, false, false, false, false, damaged, dead,
                startedSummonWindupThisTick: windup, startedSummonRecoverThisTick: recover, summonCanceledThisTick: canceled);
    }
}
