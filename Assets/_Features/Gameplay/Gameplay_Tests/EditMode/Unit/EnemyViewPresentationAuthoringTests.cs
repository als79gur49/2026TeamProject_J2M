using System;
using System.IO;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyViewPresentationAuthoringTests
    {
        private const string PlayerPrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";
        private const string StartisPrefabPath = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Startis.prefab";
        private const string BlackEyePrefabPath = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_BlackEye.prefab";
        private const string SecBotPrefabPath = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_SecBot.prefab";
        private const string FormerEntityEffectPresentationAuthoringPath =
            "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EntityEffectPresentationAuthoring.cs";
        private const string FormerEntityEffectPresentationAuthoringGuid =
            "6c751b2feb5f4cb9b5bf1a1b799f0684";

        [Test]
        [Category("Full")]
        public void PlayerPrefab_ExposesDeathAnimatorDurationAuthoring()
        {
            var view = LoadGameplayPrefab(PlayerPrefabPath);
            var authoring = view.GetComponent<PlayerAnimationTimingAuthoring>();

            Assert.That(authoring, Is.Not.Null);
            Assert.That(new SerializedObject(authoring).FindProperty("deathAnimatorDurationSeconds"), Is.Not.Null);
        }

        [TestCase(StartisPrefabPath)]
        [TestCase(BlackEyePrefabPath)]
        [Category("Full")]
        public void EnemyPrefab_SparseBindingAuthorsDeathWithoutTiming(string prefabPath)
        {
            var view = LoadGameplayPrefab(prefabPath);
            var authoring = view.GetComponent<EnemyAnimationBindingAuthoring>();

            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EnemyAnimationBindingAuthoring)} on '{prefabPath}'.");
            Assert.That(view.GetComponent<EnemyAnimationTimingAuthoring>(), Is.Null, prefabPath);
            var snapshot = authoring.CreateSnapshot();
            Assert.That(snapshot.TryGetBinding(EnemyAnimationCue.Death, out var death), Is.True, prefabPath);
            Assert.That(death.PrimaryDispatchMode, Is.EqualTo(EnemyAnimationDispatchMode.Trigger), prefabPath);
            Assert.That(death.TargetName, Is.EqualTo("Death"), prefabPath);
            Assert.That(death.AnimatorDurationSeconds,
                Is.EqualTo(EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel), prefabPath);
            Assert.That(death.ReferenceClip, Is.Null, prefabPath);
        }

        [Test]
        [Category("Full")]
        public void MigratedEnemyPrefabYaml_DoesNotRetainRemovedDeathTimingFields()
        {
            const string prefabRoot = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/";
            var prefabPaths = new[]
            {
                prefabRoot + "EnemyView_Astreton.prefab",
                prefabRoot + "EnemyView_BlackEye.prefab",
                prefabRoot + "EnemyView_DrSaturn.prefab",
                prefabRoot + "EnemyView_JPeter.prefab",
                prefabRoot + "EnemyView_Nebulous.prefab",
                prefabRoot + "EnemyView_RocketFace.prefab",
                prefabRoot + "EnemyView_Startis.prefab",
                prefabRoot + "EnemyView_Sunwheel.prefab",
            };

            foreach (var prefabPath in prefabPaths)
            {
                var prefabText = File.ReadAllText(prefabPath);
                StringAssert.DoesNotContain("deathAnimatorDurationSeconds:", prefabText, prefabPath);
                StringAssert.DoesNotContain("deathReferenceClip:", prefabText, prefabPath);
            }
        }

        [Test]
        [Category("Full")]
        public void Assets_DoNotRetainRemovedEntityEffectPresentationAuthoringScriptOrGuid()
        {
            Assert.That(File.Exists(FormerEntityEffectPresentationAuthoringPath), Is.False);
            Assert.That(File.Exists(FormerEntityEffectPresentationAuthoringPath + ".meta"), Is.False);

            var assetPaths = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories);
            foreach (var assetPath in assetPaths)
            {
                var extension = Path.GetExtension(assetPath);
                if (!string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetText;
                try
                {
                    assetText = File.ReadAllText(assetPath);
                }
                catch (FileNotFoundException)
                {
                    // Unity may reimport and replace generated asset variants while this inventory is running.
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                StringAssert.DoesNotContain(
                    FormerEntityEffectPresentationAuthoringGuid,
                    assetText,
                    $"Former EntityEffectPresentationAuthoring GUID remains in '{assetPath}'.");
            }
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
        [Category("Full")]
        public void SecBotPrefab_AuthorsSemanticParticleEffectResumePolicy()
        {
            var view = LoadGameplayPrefab(SecBotPrefabPath);
            var controller = view.GetComponent<EnemySemanticParticleEffectController>();

            Assert.That(controller, Is.Not.Null, $"Missing {nameof(EnemySemanticParticleEffectController)} on '{SecBotPrefabPath}'.");
            Assert.That(controller.gameObject, Is.SameAs(view.gameObject));

            var serializedObject = new SerializedObject(controller);
            var bindings = serializedObject.FindProperty("bindings");
            Assert.That(bindings, Is.Not.Null);
            Assert.That(bindings.arraySize, Is.GreaterThan(0));

            var hasResumePolicyBinding = false;
            for (var i = 0; i < bindings.arraySize; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                var particles = binding.FindPropertyRelative("particleSystem").objectReferenceValue as ParticleSystem;
                var policy = binding.FindPropertyRelative("policy").enumValueIndex;
                var includeChildren = binding.FindPropertyRelative("includeChildren").boolValue;
                var clearOnInactive = binding.FindPropertyRelative("clearOnInactive").boolValue;
                var restoreRendererOnNormal = binding.FindPropertyRelative("restoreRendererOnNormal").boolValue;

                if (particles == null ||
                    policy != (int)EnemyPresentationEffectInactivePolicy.StopOnInactiveResumeOnNormal)
                {
                    continue;
                }

                hasResumePolicyBinding = true;
                Assert.That(includeChildren, Is.True);
                Assert.That(clearOnInactive, Is.True);
                Assert.That(restoreRendererOnNormal, Is.True);
            }

            Assert.That(hasResumePolicyBinding, Is.True);
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

        private static GameplayEntityView LoadGameplayPrefab(string prefabPath)
        {
            var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(view, Is.Not.Null, $"Missing gameplay prefab at '{prefabPath}'.");
            return view;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }

    }
}
