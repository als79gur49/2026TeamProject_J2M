using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EntityEffectPresentationRuntimePolicyInternalTests
    {
        [Test]
        [Category("Extended")]
        public void PlayerDeathVisibilityTail_UsesMaxOfAnimatorDurationAndTailOverride()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerDeathVisibilityTail_UsesMaxOfAnimatorDurationAndTailOverride");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var timingAuthoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var effectAuthoring = rootObject.AddComponent<EntityEffectPresentationAuthoring>();

                Assert.That(view, Is.Not.Null);
                Assert.That(timingAuthoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(timingAuthoring, "deathAnimatorDurationSeconds", 2f);
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "deathViewTailSeconds", 3f);

                var stateStore = new GameplayPresentationStateStore();
                stateStore.ViewsByEntityId[10] = view;
                stateStore.EntityTypesByEntityId[10] = EntityType.Unit;
                var resolver = new GameplayMotionTimingResolver(stateStore, new GameplayPresentationTrackState());

                var durationSeconds = resolver.ResolveVisibilityDurationSeconds(
                    10,
                    TickVisibilityChangeKind.Remove,
                    CreateTimingProfile(pushMotionDurationSeconds: 0.25f));

                Assert.That(driver.DeathPresentationDurationSeconds, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(durationSeconds, Is.EqualTo(3f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DeathViewTail_DoesNotChangeAnimatorSpeed()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("DeathViewTail_DoesNotChangeAnimatorSpeed");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var timingAuthoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var effectAuthoring = rootObject.AddComponent<EntityEffectPresentationAuthoring>();
                var animator = rootObject.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3DM/1Player/Player_S1.controller");

                Assert.That(view, Is.Not.Null);
                Assert.That(timingAuthoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);

                animator.runtimeAnimatorController = controller;
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
                PlayerViewPrefabTestUtility.SetSerializedField(timingAuthoring, "deathAnimatorDurationSeconds", 2f);
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "deathViewTailSeconds", 5f);

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Death);

                var expectedReferenceLengthSeconds = controller.animationClips
                    .Single(clip => clip != null && string.Equals(clip.name, "Death", StringComparison.Ordinal))
                    .length;
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(expectedReferenceLengthSeconds / 2f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(2f).Within(0.0001f));

                var stateStore = new GameplayPresentationStateStore();
                stateStore.ViewsByEntityId[10] = view;
                stateStore.EntityTypesByEntityId[10] = EntityType.Unit;
                var resolver = new GameplayMotionTimingResolver(stateStore, new GameplayPresentationTrackState());
                var durationSeconds = resolver.ResolveVisibilityDurationSeconds(
                    10,
                    TickVisibilityChangeKind.Remove,
                    CreateTimingProfile(pushMotionDurationSeconds: 0.25f));

                Assert.That(durationSeconds, Is.EqualTo(5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(expectedReferenceLengthSeconds / 2f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static GameplayTimingProfile CreateTimingProfile(float pushMotionDurationSeconds)
        {
            return new GameplayTimingProfile(
                GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                GameplayTimingProfile.DefaultInitialMoveDelaySeconds,
                GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds,
                GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds,
                GameplayTimingProfile.DefaultForwardCellTravelStepIntervalSeconds,
                GameplayTimingProfile.DefaultMoveMotionDurationSeconds,
                pushMotionDurationSeconds,
                GameplayTimingProfile.DefaultTopologyMotionDurationSeconds,
                GameplayTimingProfile.DefaultFlipMotionDurationSeconds,
                GameplayTimingProfile.DefaultFlipArcHeightInCells,
                GameplayTimingProfile.DefaultMaxTicksPerFrame);
        }
    }
}
