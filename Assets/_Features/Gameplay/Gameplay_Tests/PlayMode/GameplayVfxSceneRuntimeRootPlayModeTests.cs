using System.Collections;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class GameplayVfxSceneRuntimeRootPlayModeTests
    {
        private const string CombinedGameplayShowcaseScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string RuntimeRootPath = "GameplayVfxRuntimeRoot";

        [UnityTest]
        [Category("Full")]
        public IEnumerator CombinedGameplayShowcase_DirectPlayTick_CreatesGameplayVfxRuntimeRoot()
        {
            yield return AssertSceneTickCreatesRuntimeRoot(
                CombinedGameplayShowcaseScenePath,
                StageId.CreateOrThrow("stage-1-1"));
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator UIAudioScene_DirectPlayTick_CreatesGameplayVfxRuntimeRoot()
        {
            yield return AssertSceneTickCreatesRuntimeRoot(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-0-1"));
        }

        private static IEnumerator AssertSceneTickCreatesRuntimeRoot(string scenePath, StageId stageId)
        {
            try
            {
                StageLaunchContextStore.SetCurrent(stageId);
                EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
                yield return LoadScene(scenePath);

                var host = Object.FindFirstObjectByType<GameplaySceneHost>();
                Assert.That(host, Is.Not.Null, $"{scenePath} must create a GameplaySceneHost.");
                Assert.That(host.InputHost, Is.Not.Null, $"{scenePath} must create a GameplayInputHost.");
                var runtime = host.GetComponent<GameplayVfxProductionRuntime>();
                Assert.That(runtime, Is.Not.Null, $"{scenePath} must include a GameplayVfxProductionRuntime.");
                Assert.That(runtime.EnableEnemyJumpTargetVfx, Is.True);

                var tickResult = host.InputHost.RunSingleTick();
                yield return null;

                Assert.That(tickResult, Is.Not.Null, $"{scenePath} must allow a runtime tick after bootstrap.");
                Assert.That(runtime.IsRuntimeInitialized, Is.True, $"{scenePath} must attach the VFX runtime as a tick presentation extension.");
                var runtimeRoot = FindRuntimeRoot(host.transform);
                Assert.That(runtimeRoot, Is.Not.Null, $"{scenePath} did not create {RuntimeRootPath} after a tick.");
                Assert.That(runtimeRoot.Find("OneShot"), Is.Not.Null);
                Assert.That(runtimeRoot.Find("Persistent"), Is.Not.Null);
                Assert.That(runtimeRoot.Find("Tail"), Is.Not.Null);
                Assert.That(runtimeRoot.Find("Pool"), Is.Not.Null);
            }
            finally
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
            }
        }

        private static Transform FindRuntimeRoot(Transform hostRoot)
        {
            return hostRoot != null ? hostRoot.Find(RuntimeRootPath) : null;
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
    }
}
