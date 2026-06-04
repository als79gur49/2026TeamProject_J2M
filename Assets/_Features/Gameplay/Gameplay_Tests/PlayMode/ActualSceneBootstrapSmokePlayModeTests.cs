using System.Collections;
using System.Linq;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
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
    public sealed class ActualSceneBootstrapSmokePlayModeTests
    {
        private const int FirstTickSmokeCount = 5;
        private const string CombinedGameplayShowcaseScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            yield return CleanupSceneRuntime();
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_CombinedGameplayShowcase_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                CombinedGameplayShowcaseScenePath,
                StageId.CreateOrThrow("stage-1-1"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_TutorialScene_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                TutorialScenePath,
                StageId.CreateOrThrow("tutorial-scene"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioScene_ResolvesTutorialStageAndInstallsUiAudio()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-0-1"));
        }

        private static IEnumerator AssertSceneBootstrapFirstFiveTicks(string scenePath, StageId stageId)
        {
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
            yield return LoadScene(scenePath);

            Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(stageId), scenePath);

            var hosts = Object.FindObjectsByType<GameplaySceneHost>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.That(hosts, Has.Length.EqualTo(1), $"{scenePath} must have exactly one active GameplaySceneHost.");
            var host = hosts[0];
            Assert.That(host.InputHost, Is.Not.Null, $"{scenePath} must install GameplayInputHost.");
            Assert.That(host.Presenter, Is.Not.Null, $"{scenePath} must install GameplayTickViewPresenter.");
            Assert.That(host.UiAccess, Is.Not.Null, $"{scenePath} must expose UIAccess as the read/intent seam.");

            AssertAudioBootstrap(scenePath, host);
            AssertUiBootstrap(scenePath);
            AssertTopologyBootstrap(scenePath, host);

            var hashes = new string[FirstTickSmokeCount];
            for (var i = 0; i < FirstTickSmokeCount; i++)
            {
                var result = host.InputHost.RunSingleTick();
                yield return null;
                Assert.That(result, Is.Not.Null, $"{scenePath} tick {i + 1} returned null.");
                hashes[i] = string.IsNullOrEmpty(result.DeterminismHash) ? "<empty>" : result.DeterminismHash;
            }

            TestContext.WriteLine($"{scenePath} first-five determinism hashes: {string.Join(", ", hashes)}");
        }

        private static void AssertAudioBootstrap(string scenePath, GameplaySceneHost host)
        {
            var audioInstaller = host.GetComponent<AudioRuntimeInstaller>();
            Assert.That(audioInstaller, Is.Not.Null, $"{scenePath} must keep AudioRuntimeInstaller co-located on the gameplay root.");
            Assert.That(
                audioInstaller.BindingMode,
                Is.EqualTo(AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime),
                scenePath);
            Assert.That(audioInstaller.AudioService, Is.Not.Null, $"{scenePath} must install an audio service before first gameplay playback.");
            Assert.That(host.GetComponent<GlobalAudioFlowBootstrap>(), Is.Not.Null, $"{scenePath} must keep persistent BGM bootstrap co-located.");
            Assert.That(
                Object.FindObjectsByType<GlobalAudioFlowRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(1),
                $"{scenePath} must not create duplicate persistent BGM roots.");
        }

        private static void AssertUiBootstrap(string scenePath)
        {
            var installers = Object.FindObjectsByType<GameplayUiFlowInstaller>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.That(installers, Has.Length.EqualTo(1), $"{scenePath} must have exactly one GameplayUiFlowInstaller.");
            var installer = installers[0];
            Assert.That(installer.RootView, Is.Not.Null, $"{scenePath} must create the runtime Gameplay UI canvas root.");
            Assert.That(installer.RootView.HudView, Is.Not.Null, $"{scenePath} must mount the HUD prefab under the runtime UI root.");
            Assert.That(installer.RootView.ScreenLayerView, Is.Not.Null, $"{scenePath} must create the screen layer runtime.");
            Assert.That(installer.RootView.PopupLayerView, Is.Not.Null, $"{scenePath} must create the popup layer runtime.");
            Assert.That(installer.PresentationSource, Is.Not.Null, $"{scenePath} must install the mapped UI presentation source.");
        }

        private static void AssertTopologyBootstrap(string scenePath, GameplaySceneHost host)
        {
            Assert.That(host.GetComponent<GameplayCameraTopologyAuthoring>(), Is.Not.Null, $"{scenePath} must keep scene camera topology authoring on the gameplay root.");
            Assert.That(host.GetComponent<GameplayCameraRig>(), Is.Not.Null, $"{scenePath} must install GameplayCameraRig.");
            Assert.That(host.GetComponent<TopologyTransitionPostFxController>(), Is.Not.Null, $"{scenePath} must install topology post-fx controller.");
            Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False, $"{scenePath} should not start stuck in a topology presentation lock.");
        }

        private static IEnumerator LoadScene(string scenePath)
        {
#if UNITY_EDITOR
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
#endif
            Assert.That(operation, Is.Not.Null, $"Failed to start loading scene '{scenePath}'.");
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator CleanupSceneRuntime()
        {
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    while (!unload.isDone)
                    {
                        yield return null;
                    }
                }
            }

            foreach (var root in Object.FindObjectsByType<GlobalAudioFlowRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            foreach (var root in Object.FindObjectsByType<AudioRuntimeRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            yield return null;
        }
    }
}
