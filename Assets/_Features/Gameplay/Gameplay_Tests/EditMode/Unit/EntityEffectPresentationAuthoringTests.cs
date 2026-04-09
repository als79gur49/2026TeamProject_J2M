using System;
using System.Linq;
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
        public void EntityEffectPresentationAuthoring_CreateSnapshot_PreservesOverridesPrefabsAndOwnership()
        {
            var rootObject = new GameObject("EntityEffectPresentationAuthoring_CreateSnapshot");
            var hitPrefab = new GameObject("HitVfxPrefab");
            var deathPrefab = new GameObject("DeathVfxPrefab");

            try
            {
                var authoring = rootObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "hitEffectDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathEffectDurationSeconds", EntityEffectPresentationAuthoring.UseGlobalTimingSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathViewTailSeconds", 0.45f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "hitVfxPrefab", hitPrefab);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathVfxPrefab", deathPrefab);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathOwnershipMode", DeathPresentationOwnershipMode.AnchorToNamedTransform);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathAnchorName", "Head");

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.HitEffectDurationSeconds, Is.EqualTo(0.2f));
                Assert.That(snapshot.DeathEffectDurationSeconds, Is.EqualTo(EntityEffectPresentationAuthoring.UseGlobalTimingSentinel));
                Assert.That(snapshot.DeathViewTailSeconds, Is.EqualTo(0.45f));
                Assert.That(snapshot.HitVfxPrefab, Is.SameAs(hitPrefab));
                Assert.That(snapshot.DeathVfxPrefab, Is.SameAs(deathPrefab));
                Assert.That(snapshot.DeathOwnershipMode, Is.EqualTo(DeathPresentationOwnershipMode.AnchorToNamedTransform));
                Assert.That(snapshot.DeathAnchorName, Is.EqualTo("Head"));
                Assert.That(snapshot.HasHitEffectDurationOverride, Is.True);
                Assert.That(snapshot.HasDeathEffectDurationOverride, Is.False);
                Assert.That(snapshot.HasDeathViewTailOverride, Is.True);
                Assert.That(snapshot.HasHitVfxPrefab, Is.True);
                Assert.That(snapshot.HasDeathVfxPrefab, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(deathPrefab);
                UnityEngine.Object.DestroyImmediate(hitPrefab);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
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
        public void DefaultGameplayEntityViewFactory_PlayerPrefabValidation_ValidatesOptionalEntityEffectPresentationAuthoring()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_ValidatesOptionalEntityEffectPresentationAuthoring");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("PlayerViewPrefabRequirements_ValidatesOptionalEntityEffectPresentationAuthoring");

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

        [Test]
        public void PlayerPrefab_ExposesDeathAnimatorDurationAuthoring()
        {
            var view = LoadGameplayPrefab(PlayerPrefabPath);
            var authoring = view.GetComponent<PlayerAnimationTimingAuthoring>();

            Assert.That(authoring, Is.Not.Null);
            Assert.That(new SerializedObject(authoring).FindProperty("deathAnimatorDurationSeconds"), Is.Not.Null);
        }

        [Test]
        public void StartisPrefab_ExposesDeathAnimatorDurationAndReferenceClipAuthoring()
        {
            AssertEnemyDeathAuthoringSurface(StartisPrefabPath, expectedReferenceClipName: "Die");
        }

        [Test]
        public void BlackEyePrefab_ExposesDeathAnimatorDurationAndReferenceClipAuthoring()
        {
            AssertEnemyDeathAuthoringSurface(BlackEyePrefabPath, expectedReferenceClipName: "Die");
        }

        [TestCase(PlayerPrefabPath)]
        [TestCase(StartisPrefabPath)]
        [TestCase(BlackEyePrefabPath)]
        public void GameplayPrefabs_HaveEntityEffectPresentationAuthoringOnRoot(string prefabPath)
        {
            var view = LoadGameplayPrefab(prefabPath);
            var authoring = view.GetComponent<EntityEffectPresentationAuthoring>();

            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EntityEffectPresentationAuthoring)} on '{prefabPath}'.");
            Assert.That(authoring.gameObject, Is.SameAs(view.gameObject));
        }

        [TestCase(PlayerPrefabPath)]
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
            Assert.That(snapshot.HitVfxPrefab, Is.Null);
            Assert.That(snapshot.DeathVfxPrefab, Is.Null);
            Assert.That(snapshot.DeathOwnershipMode, Is.EqualTo(DeathPresentationOwnershipMode.CloneSourceView));
            Assert.That(snapshot.DeathAnchorName, Is.EqualTo("Chest"));
            Assert.That(snapshot.HasHitEffectDurationOverride, Is.False);
            Assert.That(snapshot.HasDeathEffectDurationOverride, Is.False);
            Assert.That(snapshot.HasDeathViewTailOverride, Is.False);
            Assert.That(snapshot.HasHitVfxPrefab, Is.False);
            Assert.That(snapshot.HasDeathVfxPrefab, Is.False);
        }

        [TestCase(PlayerPrefabPath)]
        [TestCase(StartisPrefabPath)]
        [TestCase(BlackEyePrefabPath)]
        public void GameplayPrefabs_EntityEffectPresentationAuthoring_PassesPrefabValidationWithCloneSourceViewDefaults(string prefabPath)
        {
            var view = LoadGameplayPrefab(prefabPath);
            var authoring = view.GetComponent<EntityEffectPresentationAuthoring>();

            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EntityEffectPresentationAuthoring)} on '{prefabPath}'.");
            Assert.That(authoring.DeathOwnershipMode, Is.EqualTo(DeathPresentationOwnershipMode.CloneSourceView));
            Assert.That(authoring.DeathOwnershipMode, Is.Not.EqualTo(DeathPresentationOwnershipMode.AnchorToNamedTransform));

            Assert.DoesNotThrow(() => ValidatePrefab(view, prefabPath));
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

        private static void ValidatePrefab(GameplayEntityView view, string prefabPath)
        {
            if (view.TryGetComponent<PlayerAnimatorDriver>(out _))
            {
                InvokePlayerPrefabValidation(view, prefabPath);
                return;
            }

            EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(view, prefabPath);
        }

        private static void InvokePlayerPrefabValidation(GameplayEntityView view, string prefabPath)
        {
            var requirementsType = typeof(PlayerAnimationTimingAuthoring).Assembly.GetType(
                "Game.Feature.Gameplay.Host.PlayerViewPrefabRequirements",
                throwOnError: true);
            var validateMethod = requirementsType.GetMethod(
                "ValidatePlayerViewPrefab",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(validateMethod, Is.Not.Null, "Could not locate PlayerViewPrefabRequirements.ValidatePlayerViewPrefab.");

            try
            {
                validateMethod.Invoke(null, new object[] { view, prefabPath });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
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

        private static GameplayTimingProfile CreateTimingProfile(float pushMotionDurationSeconds)
        {
            return new GameplayTimingProfile(
                GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                GameplayTimingProfile.DefaultInitialMoveDelaySeconds,
                GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds,
                GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds,
                GameplayTimingProfile.DefaultProjectileStepIntervalSeconds,
                GameplayTimingProfile.DefaultMoveMotionDurationSeconds,
                pushMotionDurationSeconds,
                GameplayTimingProfile.DefaultTopologyMotionDurationSeconds,
                GameplayTimingProfile.DefaultFlipMotionDurationSeconds,
                GameplayTimingProfile.DefaultFlipArcHeightInCells,
                GameplayTimingProfile.DefaultMaxTicksPerFrame);
        }
    }
}
