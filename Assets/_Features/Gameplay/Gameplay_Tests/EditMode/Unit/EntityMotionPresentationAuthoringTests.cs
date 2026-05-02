using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EntityMotionPresentationAuthoringTests
    {
        [Test]
        [Category("Extended")]
        public void EntityMotionPresentationAuthoring_CreateSnapshot_PreservesOverridesAndFallbackSentinel()
        {
            var rootObject = new GameObject("EntityMotionPresentationAuthoring_CreateSnapshot");

            try
            {
                var authoring = rootObject.AddComponent<EntityMotionPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", 0.15f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushMotionDurationSeconds", EntityMotionPresentationAuthoring.UseGlobalTimingSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipMotionDurationSeconds", 0.45f);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.MoveMotionDurationSeconds, Is.EqualTo(0.15f));
                Assert.That(snapshot.PushMotionDurationSeconds, Is.EqualTo(EntityMotionPresentationAuthoring.UseGlobalTimingSentinel));
                Assert.That(snapshot.FlipMotionDurationSeconds, Is.EqualTo(0.45f));

                Assert.That(snapshot.TryGetOverrideDurationSeconds(TickEntityMotionKind.Move, out var moveDurationSeconds), Is.True);
                Assert.That(moveDurationSeconds, Is.EqualTo(0.15f));
                Assert.That(snapshot.TryGetOverrideDurationSeconds(TickEntityMotionKind.Push, out _), Is.False);
                Assert.That(snapshot.TryGetOverrideDurationSeconds(TickEntityMotionKind.Flip, out var flipDurationSeconds), Is.True);
                Assert.That(flipDurationSeconds, Is.EqualTo(0.45f));
                Assert.That(snapshot.TryGetOverrideDurationSeconds(TickEntityMotionKind.BoxSlide, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EntityMotionPresentationAuthoring_InvalidOverrideDuration_Throws()
        {
            var rootObject = new GameObject("EntityMotionPresentationAuthoring_InvalidOverrideDuration");

            try
            {
                var authoring = rootObject.AddComponent<EntityMotionPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushMotionDurationSeconds", 0f);

                Assert.Throws<ArgumentOutOfRangeException>(() => authoring.CreateSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_PlayerPrefabValidation_AllowsMissingEntityMotionPresentationAuthoring()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_AllowsMissingEntityMotionPresentationAuthoring");
            var prefabObject = new GameObject("PlayerViewPrefab_WithoutEntityMotionPresentationAuthoring");

            try
            {
                var view = prefabObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);
                prefabObject.AddComponent<PlayerAnimationTimingAuthoring>();
                prefabObject.AddComponent<PlayerAnimatorDriver>();

                var factory = new DefaultGameplayEntityViewFactory(rootObject.transform, 1f, playerEntityId: 10, view);

                Assert.DoesNotThrow(() => factory.CreateView(CreatePlayerEntityState()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_PlayerPrefabValidation_ValidatesOptionalEntityMotionPresentationAuthoring()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_ValidatesOptionalEntityMotionPresentationAuthoring");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("PlayerViewPrefabRequirements_ValidatesOptionalEntityMotionPresentationAuthoring");

            try
            {
                var motionAuthoring = playerViewPrefab.GetComponent<EntityMotionPresentationAuthoring>();

                Assert.That(motionAuthoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(motionAuthoring, "pushMotionDurationSeconds", -2f);

                var factory = new DefaultGameplayEntityViewFactory(rootObject.transform, 1f, playerEntityId: 10, playerViewPrefab);

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => factory.CreateView(CreatePlayerEntityState()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static EntityState CreatePlayerEntityState()
        {
            return new EntityState
            {
                entityId = 10,
                type = EntityType.Unit,
                aiMode = EnemyAiMode.None,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                boardPresence = EntityBoardPresence.Occupying,
                facing = Direction.Up,
            };
        }
    }
}
