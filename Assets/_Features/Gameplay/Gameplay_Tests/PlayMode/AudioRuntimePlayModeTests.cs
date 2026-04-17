using System;
using System.Collections;
using System.Reflection;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class AudioRuntimePlayModeTests
    {
        [UnityTest]
        [Category("Full")]
        public IEnumerator AudioRuntimeInstaller_Install_IsIdempotent_AndCreatesSingleRuntimeRoot()
        {
            var installerObject = new GameObject("AudioRuntimeInstallerRoot");
            try
            {
                var installer = installerObject.AddComponent<AudioRuntimeInstaller>();
                yield return null;

                installer.Install();
                installer.Install();

                Assert.That(installer.RuntimeRoot, Is.Not.Null);
                Assert.That(installer.GetComponentsInChildren<AudioRuntimeRoot>(true), Has.Length.EqualTo(1));
                Assert.That(installer.GetComponentsInChildren<AudioManager>(true), Has.Length.EqualTo(1));
                Assert.That(installer.AudioService, Is.SameAs(installer.RuntimeRoot.AudioManager));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioManager_Play2D_WithoutRuntimeInitialization_ThrowsSetupDefect()
        {
            var managerObject = new GameObject("AudioManagerOnly");
            var clip = AudioClip.Create("OneShot", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: false);
                var manager = managerObject.AddComponent<AudioManager>();

                var exception = Assert.Throws<InvalidOperationException>(() => manager.Play2D(definition));
                StringAssert.Contains("AudioManager must be initialized by AudioRuntimeRoot", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioRuntimeInstaller_Install_FailsWhenMultipleRuntimeRootsExist()
        {
            var installerObject = new GameObject("AudioInstallerDuplicateRoot");
            installerObject.SetActive(false);

            try
            {
                var installer = installerObject.AddComponent<AudioRuntimeInstaller>();
                var rootA = new GameObject("AudioRuntimeRootA");
                var rootB = new GameObject("AudioRuntimeRootB");
                rootA.transform.SetParent(installerObject.transform, worldPositionStays: false);
                rootB.transform.SetParent(installerObject.transform, worldPositionStays: false);
                rootA.AddComponent<AudioRuntimeRoot>();
                rootB.AddComponent<AudioRuntimeRoot>();

                var exception = Assert.Throws<InvalidOperationException>(() => installer.Install());
                StringAssert.Contains("multiple AudioRuntimeRoot instances", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioRuntimeRoot_InitializeRuntime_FailsWhenDuplicateManagersExist()
        {
            var rootObject = new GameObject("AudioRootDuplicateManager");
            try
            {
                var runtimeRoot = rootObject.AddComponent<AudioRuntimeRoot>();
                var managerA = new GameObject("AudioManagerA");
                var managerB = new GameObject("AudioManagerB");
                managerA.transform.SetParent(rootObject.transform, worldPositionStays: false);
                managerB.transform.SetParent(rootObject.transform, worldPositionStays: false);
                managerA.AddComponent<AudioManager>();
                managerB.AddComponent<AudioManager>();

                var exception = Assert.Throws<InvalidOperationException>(() => runtimeRoot.InitializeRuntime());
                StringAssert.Contains("duplicate AudioManager instances", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator AudioManager_PlayAttached_DedupesByOwnerAndSlot_AndStopsOnOwnerDisable()
        {
            var installerObject = new GameObject("AudioAttachedPlaybackRoot");
            var ownerObject = new GameObject("AudioAttachedOwner");
            var clip = AudioClip.Create("AttachedLoop", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var installer = installerObject.AddComponent<AudioRuntimeInstaller>();
                var owner = ownerObject.AddComponent<TestOwner>();
                yield return null;

                var service = installer.AudioService;
                var slot = AudioAttachmentSlot.FromId("charge");

                var firstHandle = service.PlayAttached(definition, owner, slot);
                var reusedHandle = service.PlayAttached(definition, owner, slot);
                var siblingHandle = service.PlayAttached(definition, owner, AudioAttachmentSlot.FromId("hum"));

                Assert.That(firstHandle.IsValid, Is.True);
                Assert.That(reusedHandle, Is.SameAs(firstHandle));
                Assert.That(siblingHandle.IsValid, Is.True);
                Assert.That(siblingHandle, Is.Not.SameAs(firstHandle));

                ownerObject.SetActive(false);
                yield return null;

                Assert.That(firstHandle.IsValid, Is.False);
                Assert.That(siblingHandle.IsValid, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator AudioManager_PlayAttached_ReplacesDifferentDefinition_ForSameOwnerAndSlot()
        {
            var installerObject = new GameObject("AudioReplaceRoot");
            var ownerObject = new GameObject("AudioReplaceOwner");
            var clipA = AudioClip.Create("LoopA", 4410, 1, 44100, false);
            var clipB = AudioClip.Create("LoopB", 4410, 1, 44100, false);
            var definitionA = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var definitionB = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definitionA, clipA, loop: true);
                ConfigureDefinition(definitionB, clipB, loop: true);
                var installer = installerObject.AddComponent<AudioRuntimeInstaller>();
                var owner = ownerObject.AddComponent<TestOwner>();
                yield return null;

                var service = installer.AudioService;
                var slot = AudioAttachmentSlot.FromId("loop");

                var oldHandle = service.PlayAttached(definitionA, owner, slot);
                var replacementHandle = service.PlayAttached(definitionB, owner, slot);

                Assert.That(oldHandle.IsValid, Is.False);
                Assert.That(replacementHandle.IsValid, Is.True);
                Assert.That(replacementHandle, Is.Not.SameAs(oldHandle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(definitionA);
                UnityEngine.Object.DestroyImmediate(definitionB);
                UnityEngine.Object.DestroyImmediate(clipA);
                UnityEngine.Object.DestroyImmediate(clipB);
            }
        }

        private static void ConfigureDefinition(
            SingleAudioDefinition definition,
            AudioClip clip,
            bool loop)
        {
            SetSerializedField(definition, "clip", clip);
            SetSerializedField(typeof(AudioDefinition), definition, "loop", loop);
        }

        private static void SetSerializedField(object target, string fieldName, object value)
        {
            SetSerializedField(target.GetType(), target, fieldName, value);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private sealed class TestOwner : MonoBehaviour
        {
        }
    }
}
