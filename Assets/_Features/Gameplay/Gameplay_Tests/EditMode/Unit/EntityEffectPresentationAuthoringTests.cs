using System;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EntityEffectPresentationAuthoringTests
    {
        private const string PlayerPrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";
        private const string StartisPrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Startis.prefab";
        private const string BlackEyePrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_BlackEye.prefab";

        [Test]
        [Category("Full")]
        public void EntityEffectPresentationAuthoring_CreateSnapshot_PreservesOverridesAndOwnership()
        {
            var rootObject = new GameObject("EntityEffectPresentationAuthoring_CreateSnapshot");

            try
            {
                var authoring = rootObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "hitEffectDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathEffectDurationSeconds", EntityEffectPresentationAuthoring.UseGlobalTimingSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathViewTailSeconds", 0.45f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathOwnershipMode", DeathPresentationOwnershipMode.AnchorToNamedTransform);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathAnchorName", "Head");

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.HitEffectDurationSeconds, Is.EqualTo(0.2f));
                Assert.That(snapshot.DeathEffectDurationSeconds, Is.EqualTo(EntityEffectPresentationAuthoring.UseGlobalTimingSentinel));
                Assert.That(snapshot.DeathViewTailSeconds, Is.EqualTo(0.45f));
                Assert.That(snapshot.DeathOwnershipMode, Is.EqualTo(DeathPresentationOwnershipMode.AnchorToNamedTransform));
                Assert.That(snapshot.DeathAnchorName, Is.EqualTo("Head"));
                Assert.That(snapshot.HasHitEffectDurationOverride, Is.True);
                Assert.That(snapshot.HasDeathEffectDurationOverride, Is.False);
                Assert.That(snapshot.HasDeathViewTailOverride, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EntityEffectPresentationAuthoring_NoDeadHitDeathPrefabFields()
        {
            Assert.That(
                typeof(EntityEffectPresentationAuthoring).GetField("hitVfxPrefab", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(EntityEffectPresentationAuthoring).GetField("deathVfxPrefab", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(typeof(EntityEffectPresentationAuthoring).GetProperty("HitVfxPrefab"), Is.Null);
            Assert.That(typeof(EntityEffectPresentationAuthoring).GetProperty("DeathVfxPrefab"), Is.Null);
            Assert.That(typeof(EntityEffectPresentationSnapshot).GetProperty("HitVfxPrefab"), Is.Null);
            Assert.That(typeof(EntityEffectPresentationSnapshot).GetProperty("DeathVfxPrefab"), Is.Null);
            Assert.That(typeof(EntityEffectPresentationSnapshot).GetProperty("HasHitVfxPrefab"), Is.Null);
            Assert.That(typeof(EntityEffectPresentationSnapshot).GetProperty("HasDeathVfxPrefab"), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void EntityEffectPresentationAuthoring_InvalidOverrideDuration_Throws()
        {
            var rootObject = new GameObject("EntityEffectPresentationAuthoring_InvalidOverrideDuration");

            try
            {
                var authoring = rootObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathEffectDurationSeconds", 0f);

                Assert.Throws<ArgumentOutOfRangeException>(() => authoring.CreateSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EntityEffectPresentationAuthoring_AnchorOwnershipWithoutAnchorName_Throws()
        {
            var rootObject = new GameObject("EntityEffectPresentationAuthoring_AnchorOwnershipWithoutAnchorName");

            try
            {
                var authoring = rootObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathOwnershipMode", DeathPresentationOwnershipMode.AnchorToNamedTransform);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathAnchorName", " ");

                Assert.Throws<ArgumentException>(() => authoring.CreateSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_PlayerPrefabValidation_AllowsMissingEntityEffectPresentationAuthoring()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_AllowsMissingEntityEffectPresentationAuthoring");
            var prefabObject = new GameObject("PlayerViewPrefab_WithoutEntityEffectPresentationAuthoring");

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
        public void DefaultGameplayEntityViewFactory_PlayerPrefabValidation_ValidatesOptionalEntityEffectPresentationAuthoring()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_ValidatesOptionalEntityEffectPresentationAuthoring");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("PlayerViewFactory_ValidatesOptionalEntityEffectPresentationAuthoring");

            try
            {
                var authoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "hitEffectDurationSeconds", -2f);

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
        [Category("Full")]
        public void EnemyViewPrefabRequirements_ValidatesOptionalEntityEffectPresentationAuthoring()
        {
            var prefabObject = new GameObject("EnemyViewPrefabRequirements_ValidatesOptionalEntityEffectPresentationAuthoring");

            try
            {
                var view = prefabObject.AddComponent<GameplayEntityView>();
                view.Initialize(20);
                prefabObject.AddComponent<EnemyAnimatorDriver>();
                var authoring = prefabObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathViewTailSeconds", -2f);

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(view, nameof(EntityEffectPresentationAuthoringTests)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PlayerPrefab_ExposesDeathAnimatorDurationAuthoring()
        {
            var view = LoadGameplayPrefab(PlayerPrefabPath);
            var authoring = view.GetComponent<PlayerAnimationTimingAuthoring>();

            Assert.That(authoring, Is.Not.Null);
            Assert.That(new SerializedObject(authoring).FindProperty("deathAnimatorDurationSeconds"), Is.Not.Null);
        }

        [Test]
        [Category("Full")]
        public void StartisPrefab_ExposesDeathAnimatorDurationAndReferenceClipAuthoring()
        {
            AssertEnemyDeathAuthoringSurface(StartisPrefabPath, expectedReferenceClipName: "Die");
        }

        [Test]
        [Category("Full")]
        public void BlackEyePrefab_ExposesDeathAnimatorDurationAndReferenceClipAuthoring()
        {
            AssertEnemyDeathAuthoringSurface(BlackEyePrefabPath, expectedReferenceClipName: "Die");
        }

        [TestCase(StartisPrefabPath)]
        [TestCase(BlackEyePrefabPath)]
        public void GameplayPrefabs_HaveEntityEffectPresentationAuthoringOnRoot(string prefabPath)
        {
            var view = LoadGameplayPrefab(prefabPath);
            var authoring = view.GetComponent<EntityEffectPresentationAuthoring>();

            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EntityEffectPresentationAuthoring)} on '{prefabPath}'.");
            Assert.That(authoring.gameObject, Is.SameAs(view.gameObject));
        }

        [TestCase(StartisPrefabPath)]
        [TestCase(BlackEyePrefabPath)]
        public void GameplayPrefabs_EntityEffectPresentationAuthoring_CreateSnapshot_UsesRolloutDefaults(string prefabPath)
        {
            var view = LoadGameplayPrefab(prefabPath);
            var authoring = view.GetComponent<EntityEffectPresentationAuthoring>();

            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EntityEffectPresentationAuthoring)} on '{prefabPath}'.");

            var snapshot = authoring.CreateSnapshot();

            Assert.That(snapshot.HitEffectDurationSeconds, Is.EqualTo(EntityEffectPresentationAuthoring.UseGlobalTimingSentinel));
            Assert.That(snapshot.DeathEffectDurationSeconds, Is.EqualTo(EntityEffectPresentationAuthoring.UseGlobalTimingSentinel));
            Assert.That(snapshot.DeathViewTailSeconds, Is.EqualTo(EntityEffectPresentationAuthoring.UseGlobalTimingSentinel));
            Assert.That(snapshot.DeathOwnershipMode, Is.EqualTo(DeathPresentationOwnershipMode.CloneSourceView));
            Assert.That(snapshot.DeathAnchorName, Is.EqualTo("Chest"));
            Assert.That(snapshot.HasHitEffectDurationOverride, Is.False);
            Assert.That(snapshot.HasDeathEffectDurationOverride, Is.False);
            Assert.That(snapshot.HasDeathViewTailOverride, Is.False);
        }

        [TestCase(StartisPrefabPath)]
        [TestCase(BlackEyePrefabPath)]
        public void GameplayPrefabs_EntityEffectPresentationAuthoring_PassesPrefabValidationWithCloneSourceViewDefaults(string prefabPath)
        {
            var view = LoadGameplayPrefab(prefabPath);
            var authoring = view.GetComponent<EntityEffectPresentationAuthoring>();

            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EntityEffectPresentationAuthoring)} on '{prefabPath}'.");
            Assert.That(authoring.DeathOwnershipMode, Is.EqualTo(DeathPresentationOwnershipMode.CloneSourceView));
            Assert.That(authoring.DeathOwnershipMode, Is.Not.EqualTo(DeathPresentationOwnershipMode.AnchorToNamedTransform));

            Assert.DoesNotThrow(() => ValidatePrefabThroughPublicSeams(view, prefabPath));
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

        private static GameplayEntityView LoadGameplayPrefab(string prefabPath)
        {
            var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(view, Is.Not.Null, $"Missing gameplay prefab at '{prefabPath}'.");
            return view;
        }

        private static void ValidatePrefabThroughPublicSeams(GameplayEntityView view, string prefabPath)
        {
            if (view.TryGetComponent<PlayerAnimatorDriver>(out _))
            {
                ValidatePlayerPrefabThroughFactory(view);
                return;
            }

            EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(view, prefabPath);
        }

        private static void ValidatePlayerPrefabThroughFactory(GameplayEntityView playerViewPrefab)
        {
            var rootObject = new GameObject("ValidatePlayerPrefabThroughFactory");
            try
            {
                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    1f,
                    playerEntityId: 10,
                    playerViewPrefab);

                var runtimeView = factory.CreateView(CreatePlayerEntityState());
                Assert.That(runtimeView, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static void AssertEnemyDeathAuthoringSurface(
            string prefabPath,
            string expectedReferenceClipName)
        {
            var view = LoadGameplayPrefab(prefabPath);
            var authoring = view.GetComponent<EnemyAnimationTimingAuthoring>();

            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EnemyAnimationTimingAuthoring)} on '{prefabPath}'.");

            var serializedObject = new SerializedObject(authoring);
            var durationProperty = serializedObject.FindProperty("deathAnimatorDurationSeconds");
            var referenceClipProperty = serializedObject.FindProperty("deathReferenceClip");

            Assert.That(durationProperty, Is.Not.Null);
            Assert.That(referenceClipProperty, Is.Not.Null);
            Assert.That(referenceClipProperty.objectReferenceValue, Is.TypeOf<AnimationClip>());
            Assert.That(referenceClipProperty.objectReferenceValue.name, Is.EqualTo(expectedReferenceClipName));
        }

    }
}
