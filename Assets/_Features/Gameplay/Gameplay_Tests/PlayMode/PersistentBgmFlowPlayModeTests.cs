using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Flow.Audio;
using Game.Feature.Stages;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class PersistentBgmFlowPlayModeTests
    {
        private const string TestScenePrefix = "PersistentBgmFlowPlayModeTests_";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        private readonly List<UnityEngine.Object> ownedObjects = new();

        [SetUp]
        public void SetUp()
        {
            Assert.That(AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot().HasRegisteredRuntime, Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();

            for (var i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();

            foreach (var bootstrap in Resources.FindObjectsOfTypeAll<GlobalAudioFlowBootstrap>())
            {
                if (bootstrap != null)
                {
                    UnityEngine.Object.DestroyImmediate(bootstrap.gameObject);
                }
            }

            foreach (var requestSource in Resources.FindObjectsOfTypeAll<SceneBgmRequestSource>())
            {
                if (requestSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(requestSource.gameObject);
                }
            }

            foreach (var persistentRoot in Resources.FindObjectsOfTypeAll<GlobalAudioFlowRoot>())
            {
                if (persistentRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(persistentRoot.gameObject);
                }
            }

            for (var sceneIndex = SceneManager.sceneCount - 1; sceneIndex >= 0; sceneIndex--)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.IsValid() && scene.isLoaded &&
                    (scene.name.StartsWith(TestScenePrefix, StringComparison.Ordinal) ||
                     scene.path == MainMenuScenePath ||
                     scene.path == UIAudioScenePath))
                {
                    var unloadOperation = SceneManager.UnloadSceneAsync(scene);
                    if (unloadOperation != null)
                    {
                        while (!unloadOperation.isDone)
                        {
                            yield return null;
                        }
                    }
                }
            }

            Assert.That(AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot().HasRegisteredRuntime, Is.False);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualUIAudioProfileToMainMenu_ReleasesStageClaimAndSelectsMenuBgm()
        {
            PrepareNonCampaignStage("stage-0-1");
            yield return LoadProductionScene(UIAudioScenePath);

            var persistentRoot = GlobalAudioFlowRoot.Current;
            Assert.That(persistentRoot.RequestRouter.ActiveRequest.Value.SourceKind,
                Is.EqualTo(BgmRequestSourceKind.StageGameplay));
            var stageProfile = persistentRoot.RequestRouter.ActiveRequest.Value.Profile;
            yield return WaitForLiveProfile(persistentRoot, stageProfile);

            yield return LoadProductionScene(MainMenuScenePath);

            Assert.That(persistentRoot.RequestRouter.ActiveRequest.Value.SourceKind,
                Is.EqualTo(BgmRequestSourceKind.SceneDefault));
            var menuProfile = persistentRoot.RequestRouter.ActiveRequest.Value.Profile;
            Assert.That(menuProfile, Is.Not.SameAs(stageProfile));
            yield return WaitForLiveProfile(persistentRoot, menuProfile);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualUIAudioNoneToMainMenu_ReleasesStopClaimAndDoesNotRemainSilent()
        {
            PrepareNonCampaignStage("legacy-stage-5-1");
            yield return LoadProductionScene(UIAudioScenePath);

            var persistentRoot = GlobalAudioFlowRoot.Current;
            Assert.That(persistentRoot.RequestRouter.ActiveRequest.Value.SourceKind,
                Is.EqualTo(BgmRequestSourceKind.StageGameplay));
            Assert.That(persistentRoot.RequestRouter.ActiveRequest.Value.StopBgm, Is.True);
            Assert.That(persistentRoot.Coordinator.GetCurrentProfile(), Is.Null);

            yield return LoadProductionScene(MainMenuScenePath);

            Assert.That(persistentRoot.RequestRouter.ActiveRequest.Value.SourceKind,
                Is.EqualTo(BgmRequestSourceKind.SceneDefault));
            var menuProfile = persistentRoot.RequestRouter.ActiveRequest.Value.Profile;
            yield return WaitForLiveProfile(persistentRoot, menuProfile);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualMainMenuToUIAudio_SelectsStageGameplayPriority()
        {
            yield return LoadProductionScene(MainMenuScenePath);
            var persistentRoot = GlobalAudioFlowRoot.Current;
            var menuProfile = persistentRoot.RequestRouter.ActiveRequest.Value.Profile;
            yield return WaitForLiveProfile(persistentRoot, menuProfile);

            PrepareNonCampaignStage("stage-0-1");
            yield return LoadProductionScene(UIAudioScenePath);

            var activeRequest = persistentRoot.RequestRouter.ActiveRequest;
            Assert.That(activeRequest.HasValue, Is.True);
            Assert.That(activeRequest.Value.SourceKind, Is.EqualTo(BgmRequestSourceKind.StageGameplay));
            Assert.That(activeRequest.Value.Priority, Is.EqualTo(BgmRequestPriority.StageGameplay));
            Assert.That(activeRequest.Value.Profile, Is.Not.SameAs(menuProfile));
            yield return WaitForLiveProfile(persistentRoot, activeRequest.Value.Profile);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualUIAudioSameProfileTransition_DoesNotRestartPersistentSource()
        {
            PrepareNonCampaignStage("stage-0-1");
            yield return LoadProductionScene(UIAudioScenePath);
            var persistentRoot = GlobalAudioFlowRoot.Current;
            var sharedProfile = persistentRoot.RequestRouter.ActiveRequest.Value.Profile;
            yield return WaitForLiveProfile(persistentRoot, sharedProfile);
            yield return new WaitForSecondsRealtime(0.1f);
            var firstSource = CaptureLiveBgmSnapshots(persistentRoot)[0].Source;
            var firstTimeSamples = firstSource.timeSamples;

            PrepareNonCampaignStage("stage-0-2");
            yield return LoadProductionScene(UIAudioScenePath);

            Assert.That(persistentRoot.RequestRouter.ActiveRequest.Value.Profile, Is.SameAs(sharedProfile));
            var snapshots = CaptureLiveBgmSnapshots(persistentRoot);
            Assert.That(snapshots, Has.Length.EqualTo(1));
            Assert.That(snapshots[0].Source, Is.SameAs(firstSource));
            Assert.That(snapshots[0].Source.clip, Is.SameAs(sharedProfile.LoopDefinition.Resolve(default).Clip));
            Assert.That(
                snapshots[0].Source.timeSamples,
                Is.GreaterThan(firstTimeSamples),
                "Same-profile scene replacement must continue the existing playback position instead of replaying the clip.");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator ActualUIAudioDifferentProfileTransition_PreservesAuthoredFadeOutIn()
        {
            PrepareNonCampaignStage("stage-0-1");
            yield return LoadProductionScene(UIAudioScenePath);
            var persistentRoot = GlobalAudioFlowRoot.Current;
            var firstProfile = persistentRoot.RequestRouter.ActiveRequest.Value.Profile;
            yield return WaitForLiveProfile(persistentRoot, firstProfile);
            var firstSnapshot = CaptureLiveBgmSnapshots(persistentRoot)[0];
            var firstClip = firstSnapshot.Source.clip;

            PrepareNonCampaignStage("stage-0-3");
            yield return LoadProductionScene(UIAudioScenePath);

            var secondProfile = persistentRoot.RequestRouter.ActiveRequest.Value.Profile;
            Assert.That(secondProfile, Is.Not.SameAs(firstProfile));
            Assert.That(secondProfile.TransitionMode, Is.EqualTo(BgmTransitionMode.FadeOutIn));
            var transitionSnapshot = CaptureLiveBgmSnapshots(persistentRoot);
            Assert.That(transitionSnapshot, Has.Length.EqualTo(1));
            Assert.That(transitionSnapshot[0].Source, Is.SameAs(firstSnapshot.Source));
            Assert.That(transitionSnapshot[0].Source.clip, Is.SameAs(firstClip));

            yield return WaitForLiveProfile(persistentRoot, secondProfile);

            var completedSnapshot = CaptureLiveBgmSnapshots(persistentRoot);
            Assert.That(completedSnapshot, Has.Length.EqualTo(1));
            Assert.That(completedSnapshot[0].Source, Is.SameAs(firstSnapshot.Source));
            Assert.That(completedSnapshot[0].Source.clip,
                Is.SameAs(secondProfile.LoopDefinition.Resolve(default).Clip));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GlobalAudioFlowBootstrap_CreatesSinglePersistentRoot_AndDuplicateBootstrapsReuseIt()
        {
            var sceneA = SceneManager.CreateScene($"{TestScenePrefix}BootstrapA");
            var sceneB = SceneManager.CreateScene($"{TestScenePrefix}BootstrapB");
            var bootstrapContextA = CreateBootstrapContext("BootstrapA");
            var bootstrapContextB = CreateBootstrapContext("BootstrapB");

            SceneManager.MoveGameObjectToScene(bootstrapContextA.RootObject, sceneA);
            SceneManager.MoveGameObjectToScene(bootstrapContextB.RootObject, sceneB);

            InvokePrivateMethod(bootstrapContextA.Bootstrap, "Awake");
            yield return null;

            var persistentRoot = GlobalAudioFlowRoot.Current;
            Assert.That(persistentRoot, Is.Not.Null);
            Assert.That(bootstrapContextA.AudioInstaller.RuntimeRoot, Is.SameAs(persistentRoot.RuntimeRoot));
            Assert.That(AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot().RuntimeRoot, Is.SameAs(persistentRoot.RuntimeRoot));

            InvokePrivateMethod(bootstrapContextB.Bootstrap, "Awake");
            yield return null;

            Assert.That(GlobalAudioFlowRoot.Current, Is.SameAs(persistentRoot));
            Assert.That(Resources.FindObjectsOfTypeAll<GlobalAudioFlowRoot>(), Has.Length.EqualTo(1));
            Assert.That(bootstrapContextB.AudioInstaller.RuntimeRoot, Is.SameAs(persistentRoot.RuntimeRoot));
            Assert.That(bootstrapContextB.Bootstrap.PersistentRoot, Is.SameAs(persistentRoot));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PersistentBgmFlow_SurvivesSceneChange_WithoutRestartingSameProfile_AndReplacesDifferentProfile()
        {
            var sceneA = SceneManager.CreateScene($"{TestScenePrefix}SceneA");
            var sceneB = SceneManager.CreateScene($"{TestScenePrefix}SceneB");
            var bootstrapContextA = CreateBootstrapContext("SceneA");
            var bootstrapContextB = CreateBootstrapContext("SceneB");
            var profileA = CreateProfile("SceneProfileA", CreateDefinition("SceneDefinitionA"));
            var profileB = CreateProfile("SceneProfileB", CreateDefinition("SceneDefinitionB"));
            var requestSourceA = CreateRequestSource("RequestSourceA", bootstrapContextA.Bootstrap, profileA);
            var requestSourceB = CreateRequestSource("RequestSourceB", bootstrapContextB.Bootstrap, profileA);

            SceneManager.MoveGameObjectToScene(bootstrapContextA.RootObject, sceneA);
            SceneManager.MoveGameObjectToScene(bootstrapContextB.RootObject, sceneB);
            SceneManager.MoveGameObjectToScene(requestSourceA.gameObject, sceneA);
            SceneManager.MoveGameObjectToScene(requestSourceB.gameObject, sceneB);

            InvokePrivateMethod(bootstrapContextA.Bootstrap, "Awake");
            InvokePrivateMethod(requestSourceA, "Start");
            yield return null;

            var persistentRoot = GlobalAudioFlowRoot.Current;
            var manager = persistentRoot.RuntimeRoot.AudioManager;
            var firstSnapshots = manager.CaptureLivePlaybackSnapshots();
            Assert.That(firstSnapshots, Has.Length.EqualTo(1));
            var firstSource = firstSnapshots[0].Source;
            Assert.That(firstSnapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Bgm));
            Assert.That(persistentRoot.Coordinator.GetCurrentProfile(), Is.SameAs(profileA));

            InvokePrivateMethod(bootstrapContextB.Bootstrap, "Awake");
            var unloadOperation = SceneManager.UnloadSceneAsync(sceneA);
            while (unloadOperation != null && !unloadOperation.isDone)
            {
                yield return null;
            }

            InvokePrivateMethod(requestSourceB, "Start");
            yield return null;

            var secondSnapshots = manager.CaptureLivePlaybackSnapshots();
            Assert.That(secondSnapshots, Has.Length.EqualTo(1));
            Assert.That(secondSnapshots[0].Source, Is.SameAs(firstSource));
            Assert.That(persistentRoot.Coordinator.GetCurrentProfile(), Is.SameAs(profileA));

            persistentRoot.Coordinator.RequestSceneDefault(profileB);
            yield return null;

            var replacedSnapshots = manager.CaptureLivePlaybackSnapshots();
            Assert.That(replacedSnapshots, Has.Length.EqualTo(1));
            Assert.That(replacedSnapshots[0].Source, Is.SameAs(firstSource));
            Assert.That(replacedSnapshots[0].Source.clip, Is.SameAs(profileB.LoopDefinition.Resolve(default).Clip));
            Assert.That(persistentRoot.Coordinator.GetCurrentProfile(), Is.SameAs(profileB));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator SceneLocalAudioInstaller_SettingsStillAffectPersistentBgm_AndStopClearsCurrentState()
        {
            var scene = SceneManager.CreateScene($"{TestScenePrefix}Settings");
            var bootstrapContext = CreateBootstrapContext("Settings");
            var profile = CreateProfile("SettingsProfile", CreateDefinition("SettingsDefinition"));
            var requestSource = CreateRequestSource("SettingsRequestSource", bootstrapContext.Bootstrap, profile);

            SceneManager.MoveGameObjectToScene(bootstrapContext.RootObject, scene);
            SceneManager.MoveGameObjectToScene(requestSource.gameObject, scene);

            InvokePrivateMethod(bootstrapContext.Bootstrap, "Awake");
            InvokePrivateMethod(requestSource, "Start");
            yield return null;

            var persistentRoot = GlobalAudioFlowRoot.Current;
            var manager = persistentRoot.RuntimeRoot.AudioManager;
            bootstrapContext.AudioInstaller.AudioSettingsService.SetChannelMuted(AudioChannel.Master, false);
            bootstrapContext.AudioInstaller.AudioSettingsService.SetChannelVolume(AudioChannel.Master, 1f);
            bootstrapContext.AudioInstaller.AudioSettingsService.SetChannelMuted(AudioChannel.Bgm, false);
            bootstrapContext.AudioInstaller.AudioSettingsService.SetChannelVolume(AudioChannel.Bgm, 1f);
            var snapshots = manager.CaptureLivePlaybackSnapshots();
            Assert.That(snapshots, Has.Length.EqualTo(1));
            Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Bgm));
            Assert.That(snapshots[0].Source.volume, Is.EqualTo(1f).Within(0.0001f));

            bootstrapContext.AudioInstaller.AudioSettingsService.SetChannelVolume(AudioChannel.Bgm, 0.25f);
            Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.25f).Within(0.0001f));

            bootstrapContext.AudioInstaller.AudioSettingsService.SetChannelMuted(AudioChannel.Bgm, true);
            Assert.That(snapshots[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));

            bootstrapContext.AudioInstaller.AudioSettingsService.SetChannelMuted(AudioChannel.Bgm, false);
            Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.25f).Within(0.0001f));

            persistentRoot.Coordinator.StopCurrent();
            yield return null;

            Assert.That(manager.CaptureLivePlaybackSnapshots(), Is.Empty);
            Assert.That(persistentRoot.Coordinator.GetCurrentProfile(), Is.Null);
        }

        [Test]
        [Category("Full")]
        public void AudioRuntimeInstaller_LocalOnly_CreatesLocalRuntime_WhenPersistentBootstrapIsAbsent()
        {
            var rootObject = Track(new GameObject("AudioRuntimeInstaller_LocalOnly_CreatesLocalRuntime_WhenPersistentBootstrapIsAbsent"));
            rootObject.SetActive(false);
            var installer = rootObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "bindingMode", AudioRuntimeInstallerBindingMode.LocalOnly);

            installer.Install();

            Assert.That(installer.RuntimeRoot, Is.Not.Null);
            Assert.That(installer.RuntimeRoot.transform.parent, Is.EqualTo(rootObject.transform));
            Assert.That(installer.AudioService, Is.Not.Null);
            Assert.That(AudioRuntimeExternalRootRegistry.CaptureDebugSnapshot().HasRegisteredRuntime, Is.False);
        }

        private BootstrapContext CreateBootstrapContext(string suffix)
        {
            var rootObject = Track(new GameObject($"{TestScenePrefix}{suffix}_BootstrapRoot"));
            rootObject.SetActive(false);
            var audioInstaller = rootObject.AddComponent<AudioRuntimeInstaller>();
            var bootstrap = rootObject.AddComponent<GlobalAudioFlowBootstrap>();
            SetSerializedField(typeof(AudioRuntimeInstaller), audioInstaller, "installOnAwake", false);
            SetSerializedField(
                typeof(AudioRuntimeInstaller),
                audioInstaller,
                "bindingMode",
                AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime);
            SetSerializedField(typeof(GlobalAudioFlowBootstrap), bootstrap, "audioRuntimeInstaller", audioInstaller);
            return new BootstrapContext(rootObject, audioInstaller, bootstrap);
        }

        private SceneBgmRequestSource CreateRequestSource(string objectName, GlobalAudioFlowBootstrap bootstrap, BgmProfile profile)
        {
            var requestObject = Track(new GameObject(objectName));
            requestObject.SetActive(false);
            var requestSource = requestObject.AddComponent<SceneBgmRequestSource>();
            SetSerializedField(typeof(SceneBgmRequestSource), requestSource, "bootstrap", bootstrap);
            SetSerializedField(typeof(SceneBgmRequestSource), requestSource, "profile", profile);
            return requestSource;
        }

        private BgmProfile CreateProfile(string profileName, AudioDefinition definition)
        {
            var profile = Track(ScriptableObject.CreateInstance<BgmProfile>());
            profile.name = profileName;
            SetSerializedField(typeof(BgmProfile), profile, "loopDefinition", definition);
            SetSerializedField(typeof(BgmProfile), profile, "transitionMode", BgmTransitionMode.Immediate);
            SetSerializedField(typeof(BgmProfile), profile, "restartIfAlreadyPlaying", false);
            return profile;
        }

        private SingleAudioDefinition CreateDefinition(string definitionName)
        {
            var clip = Track(AudioClip.Create($"{definitionName}_Clip", 4410, 1, 44100, false));
            var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>());
            definition.name = definitionName;
            SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
            SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Bgm);
            SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
            SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
            SetSerializedField(typeof(AudioDefinition), definition, "loop", true);
            return definition;
        }

        private static void PrepareNonCampaignStage(string stageId)
        {
            var id = StageId.CreateOrThrow(stageId);
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            StageLaunchContextStore.SetCurrent(id);
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(id));
        }

        private static IEnumerator LoadProductionScene(string scenePath)
        {
#if UNITY_EDITOR
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
#endif
            Assert.That(operation, Is.Not.Null, scenePath);
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator WaitForLiveProfile(
            GlobalAudioFlowRoot persistentRoot,
            BgmProfile profile)
        {
            Assert.That(profile, Is.Not.Null);
            var expectedClip = profile.LoopDefinition.Resolve(default).Clip;
            var deadline = Time.realtimeSinceStartup + profile.FadeOutSeconds + profile.FadeInSeconds + 2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var snapshots = CaptureLiveBgmSnapshots(persistentRoot);
                if (persistentRoot.Coordinator.GetCurrentProfile() == profile &&
                    snapshots.Length == 1 &&
                    snapshots[0].Source.clip == expectedClip)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Timed out waiting for BGM profile '{profile.name}' to become live.");
        }

        private static AudioLivePlaybackDebugSnapshot[] CaptureLiveBgmSnapshots(
            GlobalAudioFlowRoot persistentRoot)
        {
            return Array.FindAll(
                persistentRoot.RuntimeRoot.AudioManager.CaptureLivePlaybackSnapshots(),
                snapshot => snapshot.LeafChannel == AudioChannel.Bgm);
        }

        private T Track<T>(T unityObject) where T : UnityEngine.Object
        {
            if (unityObject != null)
            {
                ownedObjects.Add(unityObject);
            }

            return unityObject;
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{declaringType.Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private readonly struct BootstrapContext
        {
            public BootstrapContext(
                GameObject rootObject,
                AudioRuntimeInstaller audioInstaller,
                GlobalAudioFlowBootstrap bootstrap)
            {
                RootObject = rootObject;
                AudioInstaller = audioInstaller;
                Bootstrap = bootstrap;
            }

            public GameObject RootObject { get; }

            public AudioRuntimeInstaller AudioInstaller { get; }

            public GlobalAudioFlowBootstrap Bootstrap { get; }
        }
    }
}
