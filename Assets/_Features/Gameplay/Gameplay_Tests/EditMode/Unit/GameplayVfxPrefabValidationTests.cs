using System.Linq;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxPrefabValidationTests
    {
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
