using System;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EntityEffectPresentationAuthoringTests
    {
        private const string PlayerPrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";
        private const string StartisPrefabPath = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Startis.prefab";
        private const string BlackEyePrefabPath = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_BlackEye.prefab";

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

        [Test]
        [Category("Full")]
        public void BlackEyePrefab_BindsFloatingPresentationDriverToModelRoot()
        {
            var view = LoadGameplayPrefab(BlackEyePrefabPath);
            var driver = view.GetComponent<EnemyFloatingPresentationDriver>();

            Assert.That(driver, Is.Not.Null, $"Missing {nameof(EnemyFloatingPresentationDriver)} on '{BlackEyePrefabPath}'.");
            Assert.That(driver.gameObject, Is.SameAs(view.gameObject));
            Assert.That(driver.Target, Is.SameAs(view.ModelRoot));
            AssertVector(driver.LocalAxis, Vector3.forward);
            Assert.That(driver.Amplitude, Is.GreaterThan(0f));
            Assert.That(driver.FrequencyHz, Is.GreaterThan(0f));
            Assert.That(driver.PhaseOffsetSeconds, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void EnemyFloatingPresentationDriver_AdvancesTargetOnlyAndRestoresBasePosition()
        {
            var rootObject = new GameObject("EnemyFloatingPresentationDriver_Root");
            var targetObject = new GameObject("ModelRoot");

            try
            {
                targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                rootObject.transform.localPosition = new Vector3(4f, 5f, 6f);
                targetObject.transform.localPosition = new Vector3(1f, 2f, 3f);

                var driver = rootObject.AddComponent<EnemyFloatingPresentationDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "target", targetObject.transform);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "localAxis", Vector3.forward);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "amplitude", 0.5f);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "frequencyHz", 1f);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "phaseOffsetSeconds", 0f);

                driver.CaptureBaseLocalPosition();
                driver.Advance(0.25f);

                AssertVector(rootObject.transform.localPosition, new Vector3(4f, 5f, 6f));
                AssertVector(targetObject.transform.localPosition, new Vector3(1f, 2f, 3.5f));
                AssertVector(driver.CurrentOffset, new Vector3(0f, 0f, 0.5f));

                driver.RestoreBaseLocalPosition();

                AssertVector(rootObject.transform.localPosition, new Vector3(4f, 5f, 6f));
                AssertVector(targetObject.transform.localPosition, new Vector3(1f, 2f, 3f));
                AssertVector(driver.CurrentOffset, Vector3.zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyFloatingPresentationDriver_AutonomousPresentationPaused_RestoresBaseAndDoesNotAdvance()
        {
            var rootObject = new GameObject("EnemyFloatingPresentationDriver_AutonomousPresentationPaused");
            var targetObject = new GameObject("ModelRoot");

            try
            {
                targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                targetObject.transform.localPosition = new Vector3(1f, 2f, 3f);

                var driver = rootObject.AddComponent<EnemyFloatingPresentationDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "target", targetObject.transform);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "localAxis", Vector3.forward);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "amplitude", 0.5f);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "frequencyHz", 1f);

                Assert.That(driver, Is.InstanceOf<IEnemyVisualSemanticPresentationDriver>());

                driver.CaptureBaseLocalPosition();
                driver.Advance(0.25f);
                AssertVector(targetObject.transform.localPosition, new Vector3(1f, 2f, 3.5f));

                driver.Apply(new EnemyVisualSemanticState(
                    EnemyVisualActivityState.Normal,
                    shouldPauseAnimatorPlayback: true,
                    shouldPauseAutonomousPresentation: true));
                Assert.That(driver.IsSuspended, Is.True);
                AssertVector(targetObject.transform.localPosition, new Vector3(1f, 2f, 3f));
                AssertVector(driver.CurrentOffset, Vector3.zero);

                driver.Advance(0.25f);
                AssertVector(targetObject.transform.localPosition, new Vector3(1f, 2f, 3f));
                AssertVector(driver.CurrentOffset, Vector3.zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyFloatingPresentationDriver_NormalAfterSuspended_ResumesFromBasePosition()
        {
            var rootObject = new GameObject("EnemyFloatingPresentationDriver_NormalAfterSuspended");
            var targetObject = new GameObject("ModelRoot");

            try
            {
                targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                targetObject.transform.localPosition = new Vector3(1f, 2f, 3f);

                var driver = rootObject.AddComponent<EnemyFloatingPresentationDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "target", targetObject.transform);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "localAxis", Vector3.forward);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "amplitude", 0.5f);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "frequencyHz", 1f);

                driver.CaptureBaseLocalPosition();
                driver.Apply(new EnemyVisualSemanticState(
                    EnemyVisualActivityState.Normal,
                    shouldPauseAnimatorPlayback: true,
                    shouldPauseAutonomousPresentation: true));
                driver.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));
                driver.Advance(0.25f);

                Assert.That(driver.IsSuspended, Is.False);
                AssertVector(targetObject.transform.localPosition, new Vector3(1f, 2f, 3.5f));
                AssertVector(driver.CurrentOffset, new Vector3(0f, 0f, 0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
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

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
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
