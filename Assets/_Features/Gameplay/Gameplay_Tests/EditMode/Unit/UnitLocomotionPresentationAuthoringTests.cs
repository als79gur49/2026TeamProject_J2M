using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class UnitLocomotionPresentationAuthoringTests
    {
        [Test]
        public void UnitLocomotionPresentationAuthoring_CreateSnapshot_PreservesOverrideAndFallbackSentinel()
        {
            var rootObject = new GameObject("UnitLocomotionPresentationAuthoring_CreateSnapshot");

            try
            {
                var authoring = rootObject.AddComponent<UnitLocomotionPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", 0.35f);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.MoveMotionDurationSeconds, Is.EqualTo(0.35f));
                Assert.That(snapshot.TryGetOverrideDurationSeconds(TickEntityMotionKind.Move, out var moveDurationSeconds), Is.True);
                Assert.That(moveDurationSeconds, Is.EqualTo(0.35f));
                Assert.That(snapshot.TryGetOverrideDurationSeconds(TickEntityMotionKind.Push, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void UnitLocomotionPresentationAuthoring_InvalidOverrideDuration_Throws()
        {
            var rootObject = new GameObject("UnitLocomotionPresentationAuthoring_InvalidOverrideDuration");

            try
            {
                var authoring = rootObject.AddComponent<UnitLocomotionPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", 0f);

                Assert.Throws<ArgumentOutOfRangeException>(() => authoring.CreateSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DefaultGameplayEntityViewFactory_PlayerPrefabValidation_ValidatesOptionalUnitLocomotionPresentationAuthoring()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_ValidatesOptionalUnitLocomotionPresentationAuthoring");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("PlayerViewPrefabRequirements_ValidatesOptionalUnitLocomotionPresentationAuthoring");

            try
            {
                var authoring = playerViewPrefab.GetComponent<UnitLocomotionPresentationAuthoring>();

                Assert.That(authoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", -2f);

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

        [Test]
        public void EnemyViewPrefabRequirements_ValidatesOptionalUnitLocomotionPresentationAuthoring()
        {
            var prefabObject = new GameObject("EnemyViewPrefabRequirements_ValidatesOptionalUnitLocomotionPresentationAuthoring");

            try
            {
                var view = prefabObject.AddComponent<GameplayEntityView>();
                view.Initialize(20);
                prefabObject.AddComponent<EnemyAnimatorDriver>();
                var authoring = prefabObject.AddComponent<UnitLocomotionPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", -2f);

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(view, nameof(UnitLocomotionPresentationAuthoringTests)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void EnemyViewPrefabRequirements_AllowsUnitLocomotionWithoutLegacyEntityMotionAuthoring()
        {
            var prefabObject = new GameObject("EnemyViewPrefabRequirements_AllowsUnitLocomotionWithoutLegacyEntityMotionAuthoring");

            try
            {
                var view = prefabObject.AddComponent<GameplayEntityView>();
                view.Initialize(20);
                prefabObject.AddComponent<Animator>();
                prefabObject.AddComponent<UnitLocomotionPresentationAuthoring>();
                prefabObject.AddComponent<EnemyAnimationTimingAuthoring>();
                prefabObject.AddComponent<EnemyAnimatorDriver>();

                Assert.DoesNotThrow(
                    () => EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(view, nameof(UnitLocomotionPresentationAuthoringTests)));
                Assert.That(prefabObject.GetComponent<EntityMotionPresentationAuthoring>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
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
