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

        [UnityTest]
        [Category("Full")]
        public IEnumerator AudioManager_Play2D_OneShotCompletion_UnregistersLivePlayback()
        {
            var rootObject = new GameObject("AudioOneShotCompletionRoot");
            var clip = AudioClip.Create("OneShotCompletion", 64, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: false);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.Play2D(definition);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                yield return null;

                Assert.That(handle.IsValid, Is.False);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioManager_PooledSourceReuse_DoesNotLeaveStaleRegistryEntries()
        {
            var rootObject = new GameObject("AudioPooledSourceReuseRoot");
            var clip = AudioClip.Create("Reuse", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var firstHandle = manager.Play2D(definition);
                var firstSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(firstSnapshot, Has.Length.EqualTo(1));

                firstHandle.Stop();
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));

                manager.Play2D(definition);
                var secondSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(secondSnapshot, Has.Length.EqualTo(1));
                Assert.That(secondSnapshot[0].Source, Is.SameAs(firstSnapshot[0].Source));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator AudioManager_DestroyedSource_IsPrunedFromLiveRegistry()
        {
            var rootObject = new GameObject("AudioDestroyedSourceCleanupRoot");
            var clip = AudioClip.Create("DestroyedSource", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.Play2D(definition);
                var snapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].IsSourceReferenceValid, Is.True);

                UnityEngine.Object.DestroyImmediate(snapshot[0].Source.gameObject);
                yield return null;

                Assert.That(handle.IsValid, Is.False);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioManager_Destroy_CleansUpLiveHandleState()
        {
            var rootObject = new GameObject("AudioManagerDestroyCleanupRoot");
            var clip = AudioClip.Create("DestroyCleanup", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.Play2D(definition);
                Assert.That(handle.IsValid, Is.True);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                UnityEngine.Object.DestroyImmediate(manager);

                Assert.That(handle.IsValid, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioManager_RuntimeMixUpdatesImmediately_WhilePersistenceFlushRemainsBounded()
        {
            var rootObject = new GameObject("AudioImmediateApplyAndFlushRoot");
            var clip = AudioClip.Create("BoundedFlush", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var persistenceStore = new RecordingAudioSettingsPersistenceStore();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Bgm);
                var manager = CreateInitializedManager(rootObject, persistenceStore);

                manager.Play2D(definition);
                var snapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshot, Has.Length.EqualTo(1));

                manager.SetChannelVolume(AudioChannel.Bgm, 0.5f);

                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.EqualTo(0));

                manager.FlushSettings();
                Assert.That(persistenceStore.SaveCallCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioManager_HiddenInternalChannels_FollowMasterOnly()
        {
            var rootObject = new GameObject("AudioHiddenChannelsRoot");
            var clip = AudioClip.Create("UiLeaf", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Ui);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.Play2D(definition);
                var snapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].LeafChannel, Is.EqualTo(AudioChannel.Ui));

                manager.SetChannelVolume(AudioChannel.Master, 0.5f);
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));

                manager.SetChannelVolume(AudioChannel.Bgm, 0.1f);
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));

                manager.SetChannelVolume(AudioChannel.Sfx, 0.2f);
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioManager_PlayBgm_Replacement_UnregistersPreviousLiveRecordBeforeRegisteringNewOne()
        {
            var rootObject = new GameObject("AudioBgmReplacementRoot");
            var clipA = AudioClip.Create("BgmA", 4410, 1, 44100, false);
            var clipB = AudioClip.Create("BgmB", 4410, 1, 44100, false);
            var definitionA = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var definitionB = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definitionA, clipA, loop: true);
                ConfigureDefinition(definitionB, clipB, loop: true);
                SetSerializedField(typeof(AudioDefinition), definitionA, "category", AudioCategory.Bgm);
                SetSerializedField(typeof(AudioDefinition), definitionB, "category", AudioCategory.Bgm);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var firstHandle = manager.PlayBgm(definitionA);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                var secondHandle = manager.PlayBgm(definitionB);
                var snapshots = manager.CaptureLivePlaybackSnapshots();

                Assert.That(firstHandle.IsValid, Is.False);
                Assert.That(secondHandle.IsValid, Is.True);
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Bgm));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definitionA);
                UnityEngine.Object.DestroyImmediate(definitionB);
                UnityEngine.Object.DestroyImmediate(clipA);
                UnityEngine.Object.DestroyImmediate(clipB);
            }
        }

        private static AudioManager CreateInitializedManager(
            GameObject rootObject,
            RecordingAudioSettingsPersistenceStore persistenceStore)
        {
            var runtimeRoot = rootObject.AddComponent<AudioRuntimeRoot>();
            var manager = rootObject.AddComponent<AudioManager>();
            manager.SetPersistenceStoreOverrideForTesting(persistenceStore);
            runtimeRoot.InitializeRuntime();
            return manager;
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

        private sealed class RecordingAudioSettingsPersistenceStore : IAudioSettingsPersistenceStore
        {
            public int SaveCallCount { get; private set; }

            public AudioSettingsSnapshot Snapshot { get; private set; } = AudioSettingsSnapshot.Default;

            public AudioSettingsSnapshot Load()
            {
                return Snapshot;
            }

            public void Save(AudioSettingsSnapshot snapshot)
            {
                SaveCallCount++;
                Snapshot = snapshot;
            }
        }

        private sealed class TestOwner : MonoBehaviour
        {
        }
    }
}
