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
            var clip = AudioClip.Create("OneShotCompletion", 11025, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var previousTimeScale = Time.timeScale;

            try
            {
                ConfigureDefinition(definition, clip, loop: false);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.Play2D(definition);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                Time.timeScale = 0f;
                var deadline = Time.realtimeSinceStartupAsDouble + 1d;
                while (handle.IsValid &&
                       manager.CaptureLivePlaybackCount() > 0 &&
                       Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                }

                Assert.That(handle.IsValid, Is.False);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
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

        [UnityTest]
        [Category("Full")]
        public IEnumerator AudioManager_DestroyedLeasedSource_DoesNotShrinkPoolCapacity()
        {
            var rootObject = new GameObject("AudioDestroyedLeasedSourceCapacityRoot");
            var clip = AudioClip.Create("DestroyedLeasedSourceCapacity", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(
                    rootObject,
                    new RecordingAudioSettingsPersistenceStore(),
                    initialPoolSize: 1,
                    maxPoolSize: 1);

                var firstHandle = manager.Play2D(definition);
                var firstSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(firstSnapshot, Has.Length.EqualTo(1));

                UnityEngine.Object.DestroyImmediate(firstSnapshot[0].Source.gameObject);
                yield return null;

                Assert.That(firstHandle.IsValid, Is.False);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));

                var secondHandle = manager.Play2D(definition);
                var secondSnapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(secondHandle.IsValid, Is.True);
                Assert.That(secondSnapshot, Has.Length.EqualTo(1));
                Assert.That(secondSnapshot[0].Source, Is.Not.Null);
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
        public void AudioManager_DestroyedAvailableSource_IsReplacedOnNextAcquire()
        {
            var rootObject = new GameObject("AudioDestroyedAvailableSourceCapacityRoot");
            var clip = AudioClip.Create("DestroyedAvailableSourceCapacity", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(
                    rootObject,
                    new RecordingAudioSettingsPersistenceStore(),
                    initialPoolSize: 1,
                    maxPoolSize: 1);

                var firstHandle = manager.Play2D(definition);
                var firstSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(firstSnapshot, Has.Length.EqualTo(1));

                firstHandle.Stop();
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));

                UnityEngine.Object.DestroyImmediate(firstSnapshot[0].Source.gameObject);

                var secondHandle = manager.Play2D(definition);
                var secondSnapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(secondHandle.IsValid, Is.True);
                Assert.That(secondSnapshot, Has.Length.EqualTo(1));
                Assert.That(secondSnapshot[0].Source, Is.Not.SameAs(firstSnapshot[0].Source));
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
        public void AudioManager_UiChannel_FollowsMasterAndSfxWhileIgnoringBgm()
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
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void SfxSettingChange_UpdatesLiveUiSources()
        {
            var rootObject = new GameObject("AudioLiveUiSfxMixRoot");
            var clip = AudioClip.Create("LiveUiSfxMix", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Ui);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.Play2D(definition);
                var snapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(handle.IsValid, Is.True);
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].LeafChannel, Is.EqualTo(AudioChannel.Ui));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(1f).Within(0.0001f));

                manager.SetChannelVolume(AudioChannel.Sfx, 0.25f);
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.25f).Within(0.0001f));

                manager.SetChannelMuted(AudioChannel.Sfx, true);
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_Play2D_SilentSfxOneShot_ReturnsInvalidWithoutPoolAcquire()
        {
            var rootObject = new GameObject("AudioSilentSfxSkipRoot");
            var blockerClip = AudioClip.Create("SilentSfxPoolBlocker", 44100, 1, 44100, false);
            var oneShotClip = AudioClip.Create("SilentSfxOneShot", 44100, 1, 44100, false);
            var blockerDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var oneShotDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(blockerDefinition, blockerClip, loop: true);
                ConfigureDefinition(oneShotDefinition, oneShotClip, loop: false);
                var manager = CreateInitializedManager(
                    rootObject,
                    new RecordingAudioSettingsPersistenceStore(),
                    initialPoolSize: 1,
                    maxPoolSize: 1);

                var blockerHandle = manager.Play2D(blockerDefinition);
                Assert.That(blockerHandle.IsValid, Is.True);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                manager.SetChannelMuted(AudioChannel.Sfx, true);
                var failureCountBefore = manager.CaptureSourcePoolAcquireFailureCount();
                var silentHandle = manager.Play2D(oneShotDefinition);

                Assert.That(silentHandle.IsValid, Is.False);
                Assert.DoesNotThrow(() => manager.Stop(silentHandle));
                Assert.That(manager.CaptureSourcePoolAcquireFailureCount(), Is.EqualTo(failureCountBefore));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(blockerDefinition);
                UnityEngine.Object.DestroyImmediate(oneShotDefinition);
                UnityEngine.Object.DestroyImmediate(blockerClip);
                UnityEngine.Object.DestroyImmediate(oneShotClip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_Play2D_MasterMutedOneShot_ReturnsInvalidWithoutPoolAcquire()
        {
            var rootObject = new GameObject("AudioMasterMutedSkipRoot");
            var blockerClip = AudioClip.Create("MasterMutedPoolBlocker", 44100, 1, 44100, false);
            var oneShotClip = AudioClip.Create("MasterMutedOneShot", 44100, 1, 44100, false);
            var blockerDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var oneShotDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(blockerDefinition, blockerClip, loop: true);
                ConfigureDefinition(oneShotDefinition, oneShotClip, loop: false);
                var manager = CreateInitializedManager(
                    rootObject,
                    new RecordingAudioSettingsPersistenceStore(),
                    initialPoolSize: 1,
                    maxPoolSize: 1);

                var blockerHandle = manager.Play2D(blockerDefinition);
                Assert.That(blockerHandle.IsValid, Is.True);

                manager.SetChannelMuted(AudioChannel.Master, true);
                var failureCountBefore = manager.CaptureSourcePoolAcquireFailureCount();
                var silentHandle = manager.Play2D(oneShotDefinition);

                Assert.That(silentHandle.IsValid, Is.False);
                Assert.That(manager.CaptureSourcePoolAcquireFailureCount(), Is.EqualTo(failureCountBefore));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(blockerDefinition);
                UnityEngine.Object.DestroyImmediate(oneShotDefinition);
                UnityEngine.Object.DestroyImmediate(blockerClip);
                UnityEngine.Object.DestroyImmediate(oneShotClip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_Play2D_AudibleOneShot_StillUsesPoolAndRegistersPlayback()
        {
            var rootObject = new GameObject("AudioAudibleOneShotRoot");
            var clip = AudioClip.Create("AudibleOneShot", 44100, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: false);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.Play2D(definition);
                var snapshots = manager.CaptureLivePlaybackSnapshots();

                Assert.That(handle.IsValid, Is.True);
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Sfx));
                Assert.That(snapshots[0].Source.volume, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_Play2D_SfxMuteSkipsHiddenUiOneShotWithoutPoolAcquire()
        {
            var rootObject = new GameObject("AudioUiSfxMuteSkipRoot");
            var blockerClip = AudioClip.Create("UiSfxMutePoolBlocker", 44100, 1, 44100, false);
            var uiClip = AudioClip.Create("UiSfxMutedOneShot", 44100, 1, 44100, false);
            var blockerDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var uiDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(blockerDefinition, blockerClip, loop: true);
                ConfigureDefinition(uiDefinition, uiClip, loop: false);
                SetSerializedField(typeof(AudioDefinition), uiDefinition, "category", AudioCategory.Ui);
                var manager = CreateInitializedManager(
                    rootObject,
                    new RecordingAudioSettingsPersistenceStore(),
                    initialPoolSize: 1,
                    maxPoolSize: 1);

                var blockerHandle = manager.Play2D(blockerDefinition);
                Assert.That(blockerHandle.IsValid, Is.True);

                manager.SetChannelMuted(AudioChannel.Sfx, true);
                var failureCountBefore = manager.CaptureSourcePoolAcquireFailureCount();
                var silentHandle = manager.Play2D(uiDefinition);

                Assert.That(silentHandle.IsValid, Is.False);
                Assert.DoesNotThrow(() => manager.Stop(silentHandle));
                Assert.That(manager.CaptureSourcePoolAcquireFailureCount(), Is.EqualTo(failureCountBefore));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(blockerDefinition);
                UnityEngine.Object.DestroyImmediate(uiDefinition);
                UnityEngine.Object.DestroyImmediate(blockerClip);
                UnityEngine.Object.DestroyImmediate(uiClip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_Play2D_MasterMuteSkipsHiddenUiOneShotWithoutPoolAcquire()
        {
            var rootObject = new GameObject("AudioUiMasterMuteSkipRoot");
            var blockerClip = AudioClip.Create("UiMasterMutePoolBlocker", 44100, 1, 44100, false);
            var uiClip = AudioClip.Create("UiMasterMutedOneShot", 44100, 1, 44100, false);
            var blockerDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var uiDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(blockerDefinition, blockerClip, loop: true);
                ConfigureDefinition(uiDefinition, uiClip, loop: false);
                SetSerializedField(typeof(AudioDefinition), uiDefinition, "category", AudioCategory.Ui);
                var manager = CreateInitializedManager(
                    rootObject,
                    new RecordingAudioSettingsPersistenceStore(),
                    initialPoolSize: 1,
                    maxPoolSize: 1);

                var blockerHandle = manager.Play2D(blockerDefinition);
                Assert.That(blockerHandle.IsValid, Is.True);

                manager.SetChannelMuted(AudioChannel.Master, true);
                var failureCountBefore = manager.CaptureSourcePoolAcquireFailureCount();
                var uiHandle = manager.Play2D(uiDefinition);

                Assert.That(uiHandle.IsValid, Is.False);
                Assert.That(manager.CaptureSourcePoolAcquireFailureCount(), Is.EqualTo(failureCountBefore));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(blockerDefinition);
                UnityEngine.Object.DestroyImmediate(uiDefinition);
                UnityEngine.Object.DestroyImmediate(blockerClip);
                UnityEngine.Object.DestroyImmediate(uiClip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_PlayBgm_MasterMutedStillCreatesBgmPlayback()
        {
            var rootObject = new GameObject("AudioBgmMutedStillCreatesRoot");
            var clip = AudioClip.Create("BgmMutedStillCreates", 44100, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definition, clip);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.SetChannelMuted(AudioChannel.Master, true);
                var handle = manager.PlayBgm(CreateBgmPlayRequest(definition, AudioBgmTransition.Immediate));
                var snapshots = manager.CaptureLivePlaybackSnapshots();

                Assert.That(handle.IsValid, Is.True);
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Bgm));
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_Play2D_MasterMutedLoopStillCreatesPlayback()
        {
            var rootObject = new GameObject("AudioMutedLoopStillCreatesRoot");
            var clip = AudioClip.Create("MutedLoopStillCreates", 44100, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.SetChannelMuted(AudioChannel.Master, true);
                var handle = manager.Play2D(definition);
                var snapshots = manager.CaptureLivePlaybackSnapshots();

                Assert.That(handle.IsValid, Is.True);
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Sfx));
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_PlayAttached_SilentOneShotPreservesReuseAndReplaceSemantics()
        {
            var rootObject = new GameObject("AudioAttachedSilentSemanticsRoot");
            var ownerObject = new GameObject("AudioAttachedSilentOwner");
            var clipA = AudioClip.Create("AttachedSilentA", 44100, 1, 44100, false);
            var clipB = AudioClip.Create("AttachedSilentB", 44100, 1, 44100, false);
            var definitionA = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var definitionB = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definitionA, clipA, loop: false);
                ConfigureDefinition(definitionB, clipB, loop: false);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());
                var owner = ownerObject.AddComponent<TestOwner>();
                var slot = AudioAttachmentSlot.FromId("silent-slot");

                manager.SetChannelMuted(AudioChannel.Sfx, true);
                var firstHandle = manager.PlayAttached(definitionA, owner, slot);
                Assert.That(firstHandle.IsValid, Is.True);

                var reusedHandle = manager.PlayAttached(definitionA, owner, slot);
                var replacementHandle = manager.PlayAttached(definitionB, owner, slot);

                Assert.That(firstHandle.IsValid, Is.False);
                Assert.That(reusedHandle, Is.SameAs(firstHandle));
                Assert.That(replacementHandle.IsValid, Is.True);
                Assert.That(replacementHandle, Is.Not.SameAs(firstHandle));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(definitionA);
                UnityEngine.Object.DestroyImmediate(definitionB);
                UnityEngine.Object.DestroyImmediate(clipA);
                UnityEngine.Object.DestroyImmediate(clipB);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioManager_DefaultPoolSizes_KeepInitialPrewarmAndRaiseMaxCapTo48()
        {
            var rootObject = new GameObject("AudioDefaultPoolSizeRoot");
            var clip = AudioClip.Create("DefaultPoolSizeLoop", 44100, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                definition.name = "DefaultPoolSizeLoopDefinition";
                ConfigureDefinition(definition, clip, loop: true);
                var runtimeRoot = rootObject.AddComponent<AudioRuntimeRoot>();
                var manager = rootObject.AddComponent<AudioManager>();
                manager.SetPersistenceStoreOverrideForTesting(new RecordingAudioSettingsPersistenceStore());

                Assert.That(GetSerializedField<int>(typeof(AudioManager), manager, "initialPoolSize"), Is.EqualTo(8));
                Assert.That(GetSerializedField<int>(typeof(AudioManager), manager, "maxPoolSize"), Is.EqualTo(48));

                runtimeRoot.InitializeRuntime();
                for (var i = 0; i < 48; i++)
                {
                    var handle = manager.Play2D(definition);
                    Assert.That(handle.IsValid, Is.True, $"Expected pooled playback {i + 1} to fit the default cap.");
                }

                var failureCountBeforeOverflow = manager.CaptureSourcePoolAcquireFailureCount();
                LogAssert.Expect(
                    LogType.Warning,
                    "AudioPlaybackService could not acquire an AudioSource for 'DefaultPoolSizeLoopDefinition' on category 'Sfx'. " +
                    "Playback was skipped by the shared runtime fallback budget.");
                var overflowHandle = manager.Play2D(definition);

                Assert.That(overflowHandle.IsValid, Is.False);
                Assert.That(
                    manager.CaptureSourcePoolAcquireFailureCount(),
                    Is.EqualTo(failureCountBeforeOverflow + 1));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(48));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioPlaybackPauseService_GameplayPresentation_PausesAndResumesSfxWithoutDuplicatingPlayback()
        {
            var rootObject = new GameObject("AudioGameplayPauseSfxRoot");
            var clip = AudioClip.Create("GameplayPauseSfxLoop", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.Play2D(definition);
                var initialSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(handle.IsValid, Is.True);
                Assert.That(initialSnapshot, Has.Length.EqualTo(1));
                Assert.That(initialSnapshot[0].PauseGroup, Is.EqualTo(AudioPlaybackPauseGroup.GameplayPresentation));

                manager.PauseGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                var pausedSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(manager.IsGroupPaused(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause), Is.True);
                Assert.That(pausedSnapshot, Has.Length.EqualTo(1));
                Assert.That(pausedSnapshot[0].Source, Is.SameAs(initialSnapshot[0].Source));
                Assert.That(pausedSnapshot[0].ActivePauseReasons, Is.EqualTo(AudioPauseReason.GameplayPause));

                manager.ResumeGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                var resumedSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(manager.IsGroupPaused(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause), Is.False);
                Assert.That(resumedSnapshot, Has.Length.EqualTo(1));
                Assert.That(resumedSnapshot[0].Source, Is.SameAs(initialSnapshot[0].Source));
                Assert.That(resumedSnapshot[0].ActivePauseReasons, Is.EqualTo(AudioPauseReason.None));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioPlaybackPauseService_GameplayPresentation_ExcludesUiAndBgm()
        {
            var rootObject = new GameObject("AudioGameplayPauseExclusionRoot");
            var sfxClip = AudioClip.Create("GameplayPauseSfx", 4410, 1, 44100, false);
            var uiClip = AudioClip.Create("GameplayPauseUi", 4410, 1, 44100, false);
            var bgmClip = AudioClip.Create("GameplayPauseBgm", 4410, 1, 44100, false);
            var sfxDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var uiDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var bgmDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(sfxDefinition, sfxClip, loop: true);
                ConfigureDefinition(uiDefinition, uiClip, loop: true);
                SetSerializedField(typeof(AudioDefinition), uiDefinition, "category", AudioCategory.Ui);
                ConfigureBgmDefinition(bgmDefinition, bgmClip);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.Play2D(sfxDefinition);
                manager.Play2D(uiDefinition);
                manager.PlayBgm(CreateBgmPlayRequest(bgmDefinition, AudioBgmTransition.Immediate));
                manager.PauseGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);

                var snapshots = manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshots, Has.Length.EqualTo(3));
                Assert.That(
                    Array.Find(snapshots, snapshot => snapshot.LeafChannel == AudioChannel.Sfx).ActivePauseReasons,
                    Is.EqualTo(AudioPauseReason.GameplayPause));
                Assert.That(
                    Array.Find(snapshots, snapshot => snapshot.LeafChannel == AudioChannel.Ui).ActivePauseReasons,
                    Is.EqualTo(AudioPauseReason.None));
                Assert.That(
                    Array.Find(snapshots, snapshot => snapshot.LeafChannel == AudioChannel.Bgm).ActivePauseReasons,
                    Is.EqualTo(AudioPauseReason.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(sfxDefinition);
                UnityEngine.Object.DestroyImmediate(uiDefinition);
                UnityEngine.Object.DestroyImmediate(bgmDefinition);
                UnityEngine.Object.DestroyImmediate(sfxClip);
                UnityEngine.Object.DestroyImmediate(uiClip);
                UnityEngine.Object.DestroyImmediate(bgmClip);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioPlaybackPauseService_NewSfxCreatedDuringGameplayPause_IsRegisteredPaused()
        {
            var rootObject = new GameObject("AudioGameplayPauseNewSfxRoot");
            var clip = AudioClip.Create("GameplayPauseNewSfx", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(definition, clip, loop: true);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.PauseGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                var handle = manager.Play2D(definition);
                var pausedSnapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(handle.IsValid, Is.True);
                Assert.That(pausedSnapshot, Has.Length.EqualTo(1));
                Assert.That(pausedSnapshot[0].ActivePauseReasons, Is.EqualTo(AudioPauseReason.GameplayPause));

                manager.ResumeGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                var resumedSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(resumedSnapshot, Has.Length.EqualTo(1));
                Assert.That(resumedSnapshot[0].Source, Is.SameAs(pausedSnapshot[0].Source));
                Assert.That(resumedSnapshot[0].ActivePauseReasons, Is.EqualTo(AudioPauseReason.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator AudioPlaybackPauseService_StoppedOrNaturallyCompletedPlayback_DoesNotReviveOnResume()
        {
            var rootObject = new GameObject("AudioGameplayPauseCleanupRoot");
            var ownerObject = new GameObject("AudioGameplayPauseOwner");
            var loopClip = AudioClip.Create("GameplayPauseStoppedLoop", 4410, 1, 44100, false);
            var shortClip = AudioClip.Create("GameplayPauseShortOneShot", 64, 1, 44100, false);
            var loopDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var shortDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureDefinition(loopDefinition, loopClip, loop: true);
                ConfigureDefinition(shortDefinition, shortClip, loop: false);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var stoppedHandle = manager.Play2D(loopDefinition);
                manager.PauseGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                stoppedHandle.Stop();
                manager.ResumeGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                Assert.That(stoppedHandle.IsValid, Is.False);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));

                var owner = ownerObject.AddComponent<TestOwner>();
                var attachedHandle = manager.PlayAttached(
                    loopDefinition,
                    owner,
                    AudioAttachmentSlot.FromId("pause-cleanup"));
                manager.PauseGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                ownerObject.SetActive(false);
                yield return null;
                manager.ResumeGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                Assert.That(attachedHandle.IsValid, Is.False);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));

                manager.PauseGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                var oneShotHandle = manager.Play2D(shortDefinition);
                yield return null;
                Assert.That(oneShotHandle.IsValid, Is.True);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                manager.ResumeGroup(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause);
                yield return new WaitForSecondsRealtime(0.03f);

                Assert.That(oneShotHandle.IsValid, Is.False);
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(loopDefinition);
                UnityEngine.Object.DestroyImmediate(shortDefinition);
                UnityEngine.Object.DestroyImmediate(loopClip);
                UnityEngine.Object.DestroyImmediate(shortClip);
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

                var firstHandle = manager.PlayBgm(CreateBgmPlayRequest(definitionA, AudioBgmTransition.Immediate));
                Assert.That(manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                var secondHandle = manager.PlayBgm(CreateBgmPlayRequest(definitionB, AudioBgmTransition.Immediate));
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

        [Test]
        [Category("Full")]
        public void AudioPlaybackService_PlayBgmFadeOutIn_FadesOutThenFadesIn()
        {
            var rootObject = new GameObject("AudioBgmFadeOutInRoot");
            var clipA = AudioClip.Create("BgmFadeA", 4410, 1, 44100, false);
            var clipB = AudioClip.Create("BgmFadeB", 4410, 1, 44100, false);
            var definitionA = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var definitionB = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definitionA, clipA);
                ConfigureBgmDefinition(definitionB, clipB);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.PlayBgm(CreateBgmPlayRequest(definitionA, AudioBgmTransition.Immediate));
                manager.PlayBgm(CreateBgmPlayRequest(definitionB, AudioBgmTransition.FadeOutIn(1f, 1f)));

                manager.TickForTesting(0.5f);
                var fadeOutSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(fadeOutSnapshot, Has.Length.EqualTo(1));
                Assert.That(fadeOutSnapshot[0].Source.clip, Is.SameAs(clipA));
                Assert.That(fadeOutSnapshot[0].FadeMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(fadeOutSnapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));

                manager.TickForTesting(0.5f);
                var switchedSnapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(switchedSnapshot, Has.Length.EqualTo(1));
                Assert.That(switchedSnapshot[0].Source.clip, Is.SameAs(clipB));
                Assert.That(switchedSnapshot[0].FadeMultiplier, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(switchedSnapshot[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));

                manager.TickForTesting(0.5f);
                Assert.That(switchedSnapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));

                manager.TickForTesting(0.5f);
                Assert.That(switchedSnapshot[0].Source.volume, Is.EqualTo(1f).Within(0.0001f));
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

        [Test]
        [Category("Full")]
        public void AudioPlaybackService_FadeInWhenNoCurrentBgm_StartsAtZeroAndRampsUp()
        {
            var rootObject = new GameObject("AudioBgmFadeInOnlyRoot");
            var clip = AudioClip.Create("BgmFadeInOnly", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definition, clip);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var handle = manager.PlayBgm(CreateBgmPlayRequest(definition, AudioBgmTransition.FadeOutIn(1f, 1f)));
                var snapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(handle.IsValid, Is.True);
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));

                manager.TickForTesting(0.5f);
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));

                manager.TickForTesting(0.5f);
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(1f).Within(0.0001f));
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
        public void AudioPlaybackService_StopFadeOut_FadesToZeroThenStops()
        {
            var rootObject = new GameObject("AudioBgmStopFadeRoot");
            var clip = AudioClip.Create("BgmStopFade", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definition, clip);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.PlayBgm(CreateBgmPlayRequest(definition, AudioBgmTransition.Immediate));
                manager.StopBgm(new AudioBgmStopRequest(AudioBgmTransition.FadeOutIn(1f, 1f)));

                manager.TickForTesting(0.5f);
                var snapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));

                manager.TickForTesting(0.5f);
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
        public void AudioPlaybackService_ZeroFadeDuration_BehavesImmediate()
        {
            var rootObject = new GameObject("AudioBgmZeroFadeRoot");
            var clipA = AudioClip.Create("BgmZeroA", 4410, 1, 44100, false);
            var clipB = AudioClip.Create("BgmZeroB", 4410, 1, 44100, false);
            var definitionA = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var definitionB = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definitionA, clipA);
                ConfigureBgmDefinition(definitionB, clipB);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                var firstHandle = manager.PlayBgm(CreateBgmPlayRequest(definitionA, AudioBgmTransition.Immediate));
                var secondHandle = manager.PlayBgm(CreateBgmPlayRequest(definitionB, AudioBgmTransition.FadeOutIn(0f, 0f)));
                var snapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(firstHandle.IsValid, Is.False);
                Assert.That(secondHandle.IsValid, Is.True);
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].Source.clip, Is.SameAs(clipB));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(1f).Within(0.0001f));
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

        [Test]
        [Category("Full")]
        public void AudioPlaybackService_NewRequestDuringFade_CancelsPreviousAndUsesLatest()
        {
            var rootObject = new GameObject("AudioBgmFadeCancelRoot");
            var clipA = AudioClip.Create("BgmCancelA", 4410, 1, 44100, false);
            var clipB = AudioClip.Create("BgmCancelB", 4410, 1, 44100, false);
            var clipC = AudioClip.Create("BgmCancelC", 4410, 1, 44100, false);
            var definitionA = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var definitionB = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var definitionC = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definitionA, clipA);
                ConfigureBgmDefinition(definitionB, clipB);
                ConfigureBgmDefinition(definitionC, clipC);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.PlayBgm(CreateBgmPlayRequest(definitionA, AudioBgmTransition.Immediate));
                manager.PlayBgm(CreateBgmPlayRequest(definitionB, AudioBgmTransition.FadeOutIn(1f, 1f)));
                manager.TickForTesting(0.5f);
                manager.PlayBgm(CreateBgmPlayRequest(definitionC, AudioBgmTransition.Immediate));

                var snapshot = manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].Source.clip, Is.SameAs(clipC));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definitionA);
                UnityEngine.Object.DestroyImmediate(definitionB);
                UnityEngine.Object.DestroyImmediate(definitionC);
                UnityEngine.Object.DestroyImmediate(clipA);
                UnityEngine.Object.DestroyImmediate(clipB);
                UnityEngine.Object.DestroyImmediate(clipC);
            }
        }

        [Test]
        [Category("Full")]
        public void AudioPlaybackService_ApplyLiveMixDuringFade_PreservesFadeMultiplier()
        {
            var rootObject = new GameObject("AudioBgmFadeMixRoot");
            var clip = AudioClip.Create("BgmFadeMix", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definition, clip);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.PlayBgm(CreateBgmPlayRequest(definition, AudioBgmTransition.FadeOutIn(0f, 1f)));
                manager.TickForTesting(0.5f);
                manager.SetChannelVolume(AudioChannel.Bgm, 0.5f);
                var snapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].FadeMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.25f).Within(0.0001f));
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
        public void AudioPlaybackService_MuteDuringFade_ProducesZeroFinalVolume()
        {
            var rootObject = new GameObject("AudioBgmFadeMuteRoot");
            var clip = AudioClip.Create("BgmFadeMute", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definition, clip);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.PlayBgm(CreateBgmPlayRequest(definition, AudioBgmTransition.FadeOutIn(0f, 1f)));
                manager.TickForTesting(0.5f);
                manager.SetChannelMuted(AudioChannel.Bgm, true);
                var snapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));
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
        public void AudioPlaybackService_UnmuteDuringFade_RespectsCurrentFadeMultiplier()
        {
            var rootObject = new GameObject("AudioBgmFadeUnmuteRoot");
            var clip = AudioClip.Create("BgmFadeUnmute", 4410, 1, 44100, false);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();

            try
            {
                ConfigureBgmDefinition(definition, clip);
                var manager = CreateInitializedManager(rootObject, new RecordingAudioSettingsPersistenceStore());

                manager.PlayBgm(CreateBgmPlayRequest(definition, AudioBgmTransition.FadeOutIn(0f, 1f)));
                manager.TickForTesting(0.5f);
                manager.SetChannelMuted(AudioChannel.Bgm, true);
                manager.SetChannelMuted(AudioChannel.Bgm, false);
                var snapshot = manager.CaptureLivePlaybackSnapshots();

                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].FadeMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(snapshot[0].Source.volume, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static AudioManager CreateInitializedManager(
            GameObject rootObject,
            RecordingAudioSettingsPersistenceStore persistenceStore,
            int initialPoolSize = 8,
            int maxPoolSize = 48)
        {
            var runtimeRoot = rootObject.AddComponent<AudioRuntimeRoot>();
            var manager = rootObject.AddComponent<AudioManager>();
            manager.SetPersistenceStoreOverrideForTesting(persistenceStore);
            SetSerializedField(typeof(AudioManager), manager, "initialPoolSize", initialPoolSize);
            SetSerializedField(typeof(AudioManager), manager, "maxPoolSize", maxPoolSize);
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

        private static void ConfigureBgmDefinition(SingleAudioDefinition definition, AudioClip clip)
        {
            ConfigureDefinition(definition, clip, loop: true);
            SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Bgm);
        }

        private static AudioBgmPlaybackRequest CreateBgmPlayRequest(
            AudioDefinition definition,
            AudioBgmTransition transition)
        {
            return new AudioBgmPlaybackRequest(definition, transition);
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

        private static T GetSerializedField<T>(Type declaringType, object target, string fieldName)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            return (T)field.GetValue(target);
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
