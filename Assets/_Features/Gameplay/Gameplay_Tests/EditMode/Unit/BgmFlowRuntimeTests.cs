using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Flow.Audio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class BgmFlowRuntimeTests
    {
        private readonly List<UnityEngine.Object> ownedObjects = new();

        [TearDown]
        public void TearDown()
        {
            DestroyAllTrackedObjects();

            foreach (var persistentRoot in Resources.FindObjectsOfTypeAll<GlobalAudioFlowRoot>())
            {
                if (persistentRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(persistentRoot.gameObject);
                }
            }

            Assert.That(AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot().HasRegisteredRuntime, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Category("Extended")]
        public void BgmProfile_ValidateOrThrow_RejectsNonBgmDefinition()
        {
            var profile = CreateProfile("NonBgmProfile", CreateDefinition("NonBgmDefinition", AudioCategory.Sfx));

            var exception = Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
            Assert.That(exception.Message, Is.EqualTo("BgmProfile 'NonBgmProfile' requires loopDefinition to use AudioCategory.Bgm."));
        }

        [Test]
        [Category("Extended")]
        public void BgmProfile_OnValidate_LogsStableAuthoringErrorForNonBgmDefinition()
        {
            var profile = CreateProfile("AuthoringGuardProfile", CreateDefinition("NonBgmDefinition", AudioCategory.Sfx));

            LogAssert.Expect(
                LogType.Error,
                "BgmProfile 'AuthoringGuardProfile' requires loopDefinition to use AudioCategory.Bgm.");
            InvokePrivateMethod(profile, "OnValidate");
        }

        [Test]
        [Category("Extended")]
        public void BgmProfile_ValidateOrThrow_RejectsInvalidFadeDurations()
        {
            var profile = CreateProfile(
                "InvalidFadeProfile",
                CreateDefinition("InvalidFadeDefinition", AudioCategory.Bgm),
                transitionMode: BgmTransitionMode.FadeOutIn,
                fadeOutSeconds: float.NaN);

            var exception = Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
            Assert.That(
                exception.Message,
                Is.EqualTo("BgmProfile 'InvalidFadeProfile' requires fadeOutSeconds to be finite and greater than or equal to zero."));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_RequestSceneDefault_NullProfile_IsExplicitNoOp()
        {
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            coordinator.RequestSceneDefault(null);

            Assert.That(playbackPort.PlayRequests, Is.Empty);
            Assert.That(coordinator.GetCurrentProfile(), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_RequestSceneDefault_SameProfileWithoutRestart_IsNoOp()
        {
            var profile = CreateProfile("PersistentBgm", CreateDefinition("PersistentDefinition", AudioCategory.Bgm));
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            coordinator.RequestSceneDefault(profile);
            coordinator.RequestSceneDefault(profile);

            Assert.That(playbackPort.PlayRequests, Has.Count.EqualTo(1));
            Assert.That(playbackPort.PlayRequests[0].Definition, Is.SameAs(profile.LoopDefinition));
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(profile));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_RequestSceneDefault_SameProfileWithRestart_ReplaysCurrentProfile()
        {
            var profile = CreateProfile(
                "RestartableBgm",
                CreateDefinition("RestartableDefinition", AudioCategory.Bgm),
                restartIfAlreadyPlaying: true);
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            coordinator.RequestSceneDefault(profile);
            coordinator.RequestSceneDefault(profile);

            Assert.That(playbackPort.PlayRequests, Has.Count.EqualTo(2));
            Assert.That(playbackPort.PlayRequests[0].Definition, Is.SameAs(profile.LoopDefinition));
            Assert.That(playbackPort.PlayRequests[1].Definition, Is.SameAs(profile.LoopDefinition));
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(profile));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_RequestSceneDefault_DifferentProfile_ReplacesImmediately()
        {
            var profileA = CreateProfile("SceneA", CreateDefinition("SceneADefinition", AudioCategory.Bgm));
            var profileB = CreateProfile("SceneB", CreateDefinition("SceneBDefinition", AudioCategory.Bgm));
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            coordinator.RequestSceneDefault(profileA);
            coordinator.RequestSceneDefault(profileB);

            Assert.That(playbackPort.PlayRequests, Has.Count.EqualTo(2));
            Assert.That(playbackPort.PlayRequests[0].Definition, Is.SameAs(profileA.LoopDefinition));
            Assert.That(playbackPort.PlayRequests[1].Definition, Is.SameAs(profileB.LoopDefinition));
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(profileB));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_StopCurrent_StopsPlaybackAndClearsCurrentProfile()
        {
            var profile = CreateProfile("StopProfile", CreateDefinition("StopDefinition", AudioCategory.Bgm));
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            coordinator.RequestSceneDefault(profile);
            coordinator.StopCurrent();

            Assert.That(playbackPort.StopRequests, Has.Count.EqualTo(1));
            Assert.That(playbackPort.StopRequests[0].Transition.Mode, Is.EqualTo(BgmExecutedTransitionMode.Immediate));
            Assert.That(coordinator.GetCurrentProfile(), Is.Null);
        }

        [Test]
        [Category("Core")]
        public void BgmFlowCoordinator_RequestSceneDefault_ImmediateProfile_SendsImmediatePlaybackRequest()
        {
            var profile = CreateProfile("ImmediateProfile", CreateDefinition("ImmediateDefinition", AudioCategory.Bgm));
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            coordinator.RequestSceneDefault(profile);

            Assert.That(playbackPort.PlayRequests, Has.Count.EqualTo(1));
            Assert.That(playbackPort.PlayRequests[0].Transition.Mode, Is.EqualTo(BgmExecutedTransitionMode.Immediate));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_RequestSceneDefault_FadeOutInProfile_SendsFadeOutInPlaybackRequest()
        {
            var profile = CreateProfile(
                "FadeOutInProfile",
                CreateDefinition("FadeOutInDefinition", AudioCategory.Bgm),
                transitionMode: BgmTransitionMode.FadeOutIn,
                fadeOutSeconds: 0.25f,
                fadeInSeconds: 0.75f);
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            coordinator.RequestSceneDefault(profile);

            Assert.That(playbackPort.PlayRequests, Has.Count.EqualTo(1));
            Assert.That(playbackPort.PlayRequests[0].Transition.Mode, Is.EqualTo(BgmExecutedTransitionMode.FadeOutIn));
            Assert.That(playbackPort.PlayRequests[0].Transition.FadeOutSeconds, Is.EqualTo(0.25f));
            Assert.That(playbackPort.PlayRequests[0].Transition.FadeInSeconds, Is.EqualTo(0.75f));
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(profile));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_RequestSceneDefault_Crossfade_WarnsAndFallsBack()
        {
            var profile = CreateProfile(
                "CrossfadeProfile",
                CreateDefinition("CrossfadeDefinition", AudioCategory.Bgm),
                transitionMode: BgmTransitionMode.Crossfade,
                fadeOutSeconds: 0.25f,
                fadeInSeconds: 0.5f);
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            LogAssert.Expect(
                LogType.Warning,
                "BgmProfile 'CrossfadeProfile' requests Crossfade, but single-source BGM runtime does not support Crossfade. Falling back to FadeOutIn.");
            coordinator.RequestSceneDefault(profile);

            Assert.That(playbackPort.PlayRequests, Has.Count.EqualTo(1));
            Assert.That(playbackPort.PlayRequests[0].Transition.Mode, Is.EqualTo(BgmExecutedTransitionMode.FadeOutIn));
            Assert.That(playbackPort.PlayRequests[0].Transition.FadeOutSeconds, Is.EqualTo(0.25f));
            Assert.That(playbackPort.PlayRequests[0].Transition.FadeInSeconds, Is.EqualTo(0.5f));
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(profile));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowCoordinator_RequestSceneDefault_CrossfadeWithZeroDurations_FallsBackImmediate()
        {
            var profile = CreateProfile(
                "CrossfadeZeroProfile",
                CreateDefinition("CrossfadeZeroDefinition", AudioCategory.Bgm),
                transitionMode: BgmTransitionMode.Crossfade,
                fadeOutSeconds: 0f,
                fadeInSeconds: 0f);
            var playbackPort = new RecordingBgmPlaybackPort();
            var coordinator = new BgmFlowCoordinator(playbackPort);

            LogAssert.Expect(
                LogType.Warning,
                "BgmProfile 'CrossfadeZeroProfile' requests Crossfade, but single-source BGM runtime does not support Crossfade. Falling back to Immediate.");
            coordinator.RequestSceneDefault(profile);

            Assert.That(playbackPort.PlayRequests, Has.Count.EqualTo(1));
            Assert.That(playbackPort.PlayRequests[0].Transition.Mode, Is.EqualTo(BgmExecutedTransitionMode.Immediate));
            Assert.That(coordinator.GetCurrentProfile(), Is.SameAs(profile));
        }

        [Test]
        [Category("Extended")]
        public void BgmPlaybackPortAdapter_MapsFlowRequestToSharedAudioRequest()
        {
            var audioService = new RecordingAudioService();
            var adapterType = typeof(IBgmPlaybackPort).Assembly.GetType(
                "Game.Feature.Flow.Audio.BgmPlaybackPortAdapter",
                throwOnError: true);
            var adapter = (IBgmPlaybackPort)Activator.CreateInstance(adapterType, audioService);
            var definition = CreateDefinition("AdapterBgmDefinition", AudioCategory.Bgm);

            adapter.Play(new BgmPlaybackRequest(
                definition,
                BgmPlaybackTransition.FadeOutIn(0.25f, 0.75f)));
            adapter.Stop(new BgmStopRequest(BgmPlaybackTransition.FadeOutIn(0.5f, 0f)));

            Assert.That(audioService.PlayBgmRequests, Has.Count.EqualTo(1));
            Assert.That(audioService.PlayBgmRequests[0].Definition, Is.SameAs(definition));
            Assert.That(audioService.PlayBgmRequests[0].Transition.Mode, Is.EqualTo(AudioBgmTransitionMode.FadeOutIn));
            Assert.That(audioService.PlayBgmRequests[0].Transition.FadeOutSeconds, Is.EqualTo(0.25f));
            Assert.That(audioService.PlayBgmRequests[0].Transition.FadeInSeconds, Is.EqualTo(0.75f));
            Assert.That(audioService.StopBgmRequests, Has.Count.EqualTo(1));
            Assert.That(audioService.StopBgmRequests[0].Transition.Mode, Is.EqualTo(AudioBgmTransitionMode.FadeOutIn));
            Assert.That(audioService.StopBgmRequests[0].Transition.FadeOutSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        [Category("Extended")]
        public void SceneBgmRequestSource_Start_WithNullProfile_IsExplicitNoOp()
        {
            var requestSourceObject = Track(new GameObject("SceneBgmRequestSource_Start_WithNullProfile_IsExplicitNoOp"));
            requestSourceObject.SetActive(false);
            var requestSource = requestSourceObject.AddComponent<SceneBgmRequestSource>();

            Assert.DoesNotThrow(() => InvokePrivateMethod(requestSource, "Start"));
        }

        [Test]
        [Category("Extended")]
        public void SceneBgmRequestSource_Start_RequiresSerializedBootstrapReference_WhenProfileIsAssigned()
        {
            var requestSourceObject =
                Track(new GameObject("SceneBgmRequestSource_Start_RequiresSerializedBootstrapReference_WhenProfileIsAssigned"));
            requestSourceObject.SetActive(false);
            var requestSource = requestSourceObject.AddComponent<SceneBgmRequestSource>();
            SetSerializedField(
                typeof(SceneBgmRequestSource),
                requestSource,
                "profile",
                CreateProfile("SceneProfile", CreateDefinition("SceneDefinition", AudioCategory.Bgm)));

            var exception = Assert.Throws<InvalidOperationException>(() => InvokePrivateMethod(requestSource, "Start"));
            Assert.That(
                exception.Message,
                Is.EqualTo("SceneBgmRequestSource requires a serialized GlobalAudioFlowBootstrap reference when a BgmProfile is assigned."));
        }

        [Test]
        [Category("Extended")]
        public void GlobalAudioFlowBootstrap_Awake_RequiresCoLocatedInstallerConfiguredForPersistentBinding()
        {
            var bootstrapRoot = Track(new GameObject("GlobalAudioFlowBootstrap_Awake_RequiresCoLocatedInstallerConfiguredForPersistentBinding"));
            bootstrapRoot.SetActive(false);
            var installer = bootstrapRoot.AddComponent<AudioRuntimeInstaller>();
            var bootstrap = bootstrapRoot.AddComponent<GlobalAudioFlowBootstrap>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "bindingMode", AudioRuntimeInstallerBindingMode.LocalOnly);
            SetSerializedField(typeof(GlobalAudioFlowBootstrap), bootstrap, "audioRuntimeInstaller", installer);

            var exception = Assert.Throws<InvalidOperationException>(() => InvokePrivateMethod(bootstrap, "Awake"));
            Assert.That(
                exception.Message,
                Is.EqualTo("GlobalAudioFlowBootstrap requires a co-located AudioRuntimeInstaller configured for PreferRegisteredPersistentRuntime."));
        }

        [Test]
        [Category("Extended")]
        public void GlobalAudioFlowBootstrap_GetCoordinatorOrThrow_UsesStableMissingCoordinatorMessage()
        {
            var bootstrapRoot = Track(new GameObject("GlobalAudioFlowBootstrap_GetCoordinatorOrThrow_UsesStableMissingCoordinatorMessage"));
            bootstrapRoot.SetActive(false);
            var bootstrap = bootstrapRoot.AddComponent<GlobalAudioFlowBootstrap>();

            var exception = Assert.Throws<InvalidOperationException>(() => bootstrap.GetCoordinatorOrThrow());
            Assert.That(
                exception.Message,
                Is.EqualTo("GlobalAudioFlowBootstrap could not provide a persistent BGM flow coordinator. Verify the persistent audio-flow bootstrap path; scene-global lookup is not supported."));
        }

        [Test]
        [Category("Extended")]
        public void GlobalAudioFlowRoot_Awake_PreventsDuplicatePersistentRoots()
        {
            Track(GlobalAudioFlowRoot.GetOrCreate().gameObject);
            var duplicateRootObject = Track(new GameObject("DuplicateGlobalAudioFlowRoot"));
            duplicateRootObject.SetActive(false);
            var duplicateRoot = duplicateRootObject.AddComponent<GlobalAudioFlowRoot>();

            var exception = Assert.Throws<InvalidOperationException>(() => InvokePrivateMethod(duplicateRoot, "Awake"));
            Assert.That(
                exception.Message,
                Is.EqualTo("GlobalAudioFlowRoot cannot exist more than once. Reuse the existing persistent audio-flow root instead of creating another."));
        }

        [Test]
        [Category("Extended")]
        public void AudioRuntimeExternalRootRegistry_FirstRegistrationSucceeds()
        {
            var runtimeRoot = CreateRuntimeRoot("RegisteredPersistentRuntime");
            var ownerToken = new object();

            try
            {
                AudioRuntimeExternalRootRegistry.RegisterPersistentRuntime(runtimeRoot, ownerToken);

                var snapshot = AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot();
                Assert.That(snapshot.HasRegisteredRuntime, Is.True);
                Assert.That(snapshot.RuntimeRoot, Is.SameAs(runtimeRoot));
                Assert.That(snapshot.OwnerTypeName, Is.EqualTo(nameof(System.Object)));
            }
            finally
            {
                AudioRuntimeExternalRootRegistry.UnregisterPersistentRuntime(runtimeRoot, ownerToken);
            }
        }

        [Test]
        [Category("Extended")]
        public void AudioRuntimeExternalRootRegistry_DuplicateRegistration_FailsFast()
        {
            var firstRuntimeRoot = CreateRuntimeRoot("FirstPersistentRuntime");
            var secondRuntimeRoot = CreateRuntimeRoot("SecondPersistentRuntime");
            var firstOwnerToken = new object();
            var secondOwnerToken = new object();

            try
            {
                AudioRuntimeExternalRootRegistry.RegisterPersistentRuntime(firstRuntimeRoot, firstOwnerToken);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => AudioRuntimeExternalRootRegistry.RegisterPersistentRuntime(secondRuntimeRoot, secondOwnerToken));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("AudioRuntimeExternalRootRegistry cannot register multiple persistent AudioRuntimeRoot instances."));
            }
            finally
            {
                AudioRuntimeExternalRootRegistry.UnregisterPersistentRuntime(firstRuntimeRoot, firstOwnerToken);
            }
        }

        [Test]
        [Category("Extended")]
        public void AudioRuntimeExternalRootRegistry_Unregister_ClearsRegistryDeterministically()
        {
            var runtimeRoot = CreateRuntimeRoot("PersistentRuntime");
            var ownerToken = new object();

            AudioRuntimeExternalRootRegistry.RegisterPersistentRuntime(runtimeRoot, ownerToken);
            AudioRuntimeExternalRootRegistry.UnregisterPersistentRuntime(runtimeRoot, ownerToken);

            var snapshot = AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot();
            Assert.That(snapshot.HasRegisteredRuntime, Is.False);
            Assert.That(snapshot.RuntimeRoot, Is.Null);
            Assert.That(snapshot.OwnerTypeName, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void AudioRuntimeInstaller_PreferRegisteredPersistentRuntime_BindsToRegisteredRuntimeWhenPresent()
        {
            var registeredRuntimeRoot = CreateRuntimeRoot("SharedPersistentRuntime");
            var ownerToken = new object();
            var installerObject = Track(new GameObject("AudioRuntimeInstaller_PreferRegisteredPersistentRuntime_BindsToRegisteredRuntimeWhenPresent"));
            installerObject.SetActive(false);
            var installer = installerObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);
            SetSerializedField(
                typeof(AudioRuntimeInstaller),
                installer,
                "bindingMode",
                AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime);

            try
            {
                AudioRuntimeExternalRootRegistry.RegisterPersistentRuntime(registeredRuntimeRoot, ownerToken);

                installer.Install();

                Assert.That(installer.RuntimeRoot, Is.SameAs(registeredRuntimeRoot));
                Assert.That(installer.AudioService, Is.SameAs(registeredRuntimeRoot.AudioService));
                Assert.That(installer.AudioSettingsService, Is.SameAs(registeredRuntimeRoot.AudioSettingsService));
                Assert.That(installerObject.GetComponentsInChildren<AudioRuntimeRoot>(true), Is.Empty);
            }
            finally
            {
                AudioRuntimeExternalRootRegistry.UnregisterPersistentRuntime(registeredRuntimeRoot, ownerToken);
            }
        }

        [Test]
        [Category("Extended")]
        public void AudioRuntimeInstaller_PreferRegisteredPersistentRuntime_FallsBackToLocalChildRootWhenRegistryIsEmpty()
        {
            var installerObject = Track(new GameObject("AudioRuntimeInstaller_PreferRegisteredPersistentRuntime_FallsBackToLocalChildRootWhenRegistryIsEmpty"));
            installerObject.SetActive(false);
            var installer = installerObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);
            SetSerializedField(
                typeof(AudioRuntimeInstaller),
                installer,
                "bindingMode",
                AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime);

            installer.Install();

            Assert.That(installer.RuntimeRoot, Is.Not.Null);
            Assert.That(installer.RuntimeRoot.transform.parent, Is.EqualTo(installerObject.transform));
            Assert.That(installer.AudioService, Is.Not.Null);
            Assert.That(installer.AudioSettingsService, Is.Not.Null);
            Assert.That(AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot().HasRegisteredRuntime, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void AudioRuntimeInstaller_LocalOnly_KeepsHistoricalLocalRuntimeCreationEvenWhenPersistentRuntimeExists()
        {
            var registeredRuntimeRoot = CreateRuntimeRoot("RegisteredButIgnoredPersistentRuntime");
            var ownerToken = new object();
            var installerObject = Track(new GameObject("AudioRuntimeInstaller_LocalOnly_KeepsHistoricalLocalRuntimeCreationEvenWhenPersistentRuntimeExists"));
            installerObject.SetActive(false);
            var installer = installerObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "bindingMode", AudioRuntimeInstallerBindingMode.LocalOnly);

            try
            {
                AudioRuntimeExternalRootRegistry.RegisterPersistentRuntime(registeredRuntimeRoot, ownerToken);

                installer.Install();

                Assert.That(installer.RuntimeRoot, Is.Not.Null);
                Assert.That(installer.RuntimeRoot, Is.Not.SameAs(registeredRuntimeRoot));
                Assert.That(installer.RuntimeRoot.transform.parent, Is.EqualTo(installerObject.transform));
            }
            finally
            {
                AudioRuntimeExternalRootRegistry.UnregisterPersistentRuntime(registeredRuntimeRoot, ownerToken);
            }
        }

        private T Track<T>(T unityObject) where T : UnityEngine.Object
        {
            if (unityObject != null)
            {
                ownedObjects.Add(unityObject);
            }

            return unityObject;
        }

        private AudioRuntimeRoot CreateRuntimeRoot(string rootName)
        {
            var runtimeRootObject = Track(new GameObject(rootName));
            runtimeRootObject.SetActive(false);
            var runtimeRoot = runtimeRootObject.AddComponent<AudioRuntimeRoot>();
            runtimeRoot.InitializeRuntime();
            return runtimeRoot;
        }

        private BgmProfile CreateProfile(
            string profileName,
            AudioDefinition definition,
            BgmTransitionMode transitionMode = BgmTransitionMode.Immediate,
            bool restartIfAlreadyPlaying = false,
            float fadeOutSeconds = 0.35f,
            float fadeInSeconds = 0.35f)
        {
            var profile = Track(ScriptableObject.CreateInstance<BgmProfile>());
            profile.name = profileName;
            SetSerializedField(typeof(BgmProfile), profile, "loopDefinition", definition);
            SetSerializedField(typeof(BgmProfile), profile, "transitionMode", transitionMode);
            SetSerializedField(typeof(BgmProfile), profile, "fadeOutSeconds", fadeOutSeconds);
            SetSerializedField(typeof(BgmProfile), profile, "fadeInSeconds", fadeInSeconds);
            SetSerializedField(typeof(BgmProfile), profile, "restartIfAlreadyPlaying", restartIfAlreadyPlaying);
            return profile;
        }

        private SingleAudioDefinition CreateDefinition(string definitionName, AudioCategory category)
        {
            var clip = Track(AudioClip.Create($"{definitionName}_Clip", 4410, 1, 44100, false));
            var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>());
            definition.name = definitionName;
            SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
            SetSerializedField(typeof(AudioDefinition), definition, "category", category);
            SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
            SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
            SetSerializedField(typeof(AudioDefinition), definition, "loop", true);
            return definition;
        }

        private void DestroyAllTrackedObjects()
        {
            for (var i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);

            try
            {
                method.Invoke(target, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{declaringType.Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private sealed class RecordingBgmPlaybackPort : IBgmPlaybackPort
        {
            public readonly List<BgmPlaybackRequest> PlayRequests = new();

            public readonly List<BgmStopRequest> StopRequests = new();

            public void Play(BgmPlaybackRequest request)
            {
                PlayRequests.Add(request);
            }

            public void Stop(BgmStopRequest request)
            {
                StopRequests.Add(request);
            }
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public readonly List<AudioBgmPlaybackRequest> PlayBgmRequests = new();

            public readonly List<AudioBgmStopRequest> StopBgmRequests = new();

            public AudioPlaybackHandle Play2D(AudioDefinition definition, in AudioPlaybackContext context = default)
            {
                return default;
            }

            public AudioPlaybackHandle PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context = default)
            {
                return default;
            }

            public AudioPlaybackHandle PlayBgm(AudioBgmPlaybackRequest request)
            {
                PlayBgmRequests.Add(request);
                return default;
            }

            public void Stop(AudioPlaybackHandle handle)
            {
            }

            public void StopBgm(AudioBgmStopRequest request)
            {
                StopBgmRequests.Add(request);
            }
        }
    }
}
