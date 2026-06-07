using System.Linq;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxPrefabValidationTests
    {
        private const string JumperLandingTargetPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/JumperLandingTargetVfx.prefab";
        private const string UtilityWindupPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyUtilityWindupTelegraphVfx.prefab";
        private const string FrontFaceShieldWindupPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldWindupVfx.prefab";
        private const string CommonEmptyHostPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Common/GameplayVfxCommonEmptyHost.prefab";
        private const string EnemyDeathMotionPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDeathMotionVfx.prefab";
        private const string BoxDestroySmokePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxDestroySmokeVfx.prefab";
        private const string FlipImpactBurstPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FlipImpactBurstVfx.prefab";
        private const string BoxSlideSolidStopPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideSolidStopVfx.prefab";
        private static readonly string[] ReservedHookPrefabPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/ImpactTransientBreakVfx.prefab",
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxOutOfBoundsExitVfx.prefab",
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyOutOfBoundsExitVfx.prefab",
        };
        private const string GameplayVfxPrefabRoot = "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs";

        [Test]
        [Category("Extended")]
        public void JumperLandingTargetPrefab_PassesVfxPrefabValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JumperLandingTargetPrefabPath);

            Assert.That(prefab, Is.Not.Null, JumperLandingTargetPrefabPath);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void UtilityWindupPrefab_RemovedFromDefaultAuthoring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UtilityWindupPrefabPath);

            Assert.That(prefab, Is.Null, UtilityWindupPrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void FrontFaceShieldWindupPrefab_RemovedFromDefaultAuthoring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FrontFaceShieldWindupPrefabPath);

            Assert.That(prefab, Is.Null, FrontFaceShieldWindupPrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void ReservedHookPrefabs_RemovedFromDefaultAuthoring()
        {
            foreach (var path in ReservedHookPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(prefab, Is.Null, path);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxPrefabs_UseRuntimeRootAndModelRootContract()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { GameplayVfxPrefabRoot });

            Assert.That(guids, Is.Not.Empty);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(prefab, Is.Not.Null, path);
                var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);
                var modelRootValidation = VfxPrefabValidationDiagnostics.ValidateModelRootContract(prefab);

                Assert.That(validation.HasErrors, Is.False, $"{path}\n{string.Join("\n", validation.Messages)}");
                Assert.That(modelRootValidation.HasErrors, Is.False, $"{path}\n{string.Join("\n", modelRootValidation.Messages)}");
                Assert.That(prefab.transform.Find(VfxPrefabValidationDiagnostics.ModelRootName), Is.Not.Null, path);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxPrefabInventory_ClassifiesCommonHostActualVisualAndPlaceholder()
        {
            var commonHost = AssetDatabase.LoadAssetAtPath<GameObject>(CommonEmptyHostPrefabPath);

            Assert.That(commonHost, Is.Not.Null, CommonEmptyHostPrefabPath);
            Assert.That(VfxPrefabValidationDiagnostics.HasPresentationVisualContent(commonHost), Is.False);

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { GameplayVfxPrefabRoot });
            Assert.That(guids, Is.Not.Empty);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(
                    VfxPrefabValidationDiagnostics.HasPresentationVisualContent(prefab),
                    Is.True,
                    $"{path} should classify as ActualVisualPrefab, not a host-only placeholder.");
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxPrefabInventory_DoesNotClassifyParticlePrefabAsPlaceholder()
        {
            var particlePrefabPaths = new[]
            {
                BoxDestroySmokePrefabPath,
                FlipImpactBurstPrefabPath,
                BoxSlideSolidStopPrefabPath,
            };

            foreach (var path in particlePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty, path);
                Assert.That(VfxPrefabValidationDiagnostics.HasPresentationVisualContent(prefab), Is.True, path);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxPrefabInventory_DoesNotClassifyEnemyDeathMotionAsPlaceholder()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyDeathMotionPrefabPath);

            Assert.That(prefab, Is.Not.Null, EnemyDeathMotionPrefabPath);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            Assert.That(VfxPrefabValidationDiagnostics.HasPresentationVisualContent(prefab), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PrefabModelRootContract_RequiresDirectModelRoot()
        {
            var prefab = new GameObject("MissingModelRootVfxPrefab");

            try
            {
                var validation = VfxPrefabValidationDiagnostics.ValidateModelRootContract(prefab);

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_MODEL_ROOT_MISSING"));
            }
            finally
            {
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void PrefabModelRootContract_RejectsVisualComponentsOnRuntimeRoot()
        {
            var prefab = new GameObject("RootParticleVfxPrefab");
            prefab.AddComponent<ParticleSystem>();
            var modelRoot = new GameObject(VfxPrefabValidationDiagnostics.ModelRootName);
            modelRoot.transform.SetParent(prefab.transform, worldPositionStays: false);

            try
            {
                var validation = VfxPrefabValidationDiagnostics.ValidateModelRootContract(prefab);

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_ROOT_COMPONENT"));
            }
            finally
            {
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void PrefabValidation_RejectsCollider()
        {
            var prefab = new GameObject("ColliderVfxPrefab");
            prefab.AddComponent<BoxCollider>();

            try
            {
                var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_COLLIDER"));
            }
            finally
            {
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void PrefabValidation_RejectsAudioSource()
        {
            var prefab = new GameObject("AudioSourceVfxPrefab");
            prefab.AddComponent<AudioSource>();

            try
            {
                var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_AUDIO_SOURCE"));
            }
            finally
            {
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void PrefabValidation_AllowsPresentationComponents()
        {
            var prefab = new GameObject("PresentationVfxPrefab");
            prefab.AddComponent<ParticleSystem>();
            prefab.AddComponent<MeshFilter>();
            prefab.AddComponent<MeshRenderer>();
            prefab.AddComponent<Animator>();

            try
            {
                var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

                Assert.That(validation.HasErrors, Is.False);
                Assert.That(validation.HasWarnings, Is.False);
            }
            finally
            {
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void PrefabValidation_RejectsNonKinematicRigidbody()
        {
            var prefab = new GameObject("RigidbodyVfxPrefab");
            var rigidbody = prefab.AddComponent<Rigidbody>();
            rigidbody.isKinematic = false;

            try
            {
                var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_RIGIDBODY_DYNAMIC"));
            }
            finally
            {
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void PrefabValidation_WarnsForCustomMonoBehaviour()
        {
            var prefab = new GameObject("CustomBehaviourVfxPrefab");
            prefab.AddComponent<CustomVfxBehaviour>();

            try
            {
                var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

                Assert.That(validation.HasErrors, Is.False);
                Assert.That(validation.HasWarnings, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_MONO_BEHAVIOUR"));
            }
            finally
            {
                Destroy(prefab);
            }
        }

        private static void Destroy(UnityEngine.Object unityObject)
        {
            if (unityObject != null)
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }

        private sealed class CustomVfxBehaviour : MonoBehaviour
        {
        }
    }
}
