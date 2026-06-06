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
    [Category("Core")]
    [Category("Phase3BGate")]
    public sealed class GameplayVfxSceneRuntimeRootPlayModeTests
    {
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string RuntimeRootPath = "GameplayVfxRuntimeRoot";

        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplayShellCombinedGameplayShowcaseStage_DirectPlayTick_CreatesGameplayVfxRuntimeRoot()
        {
            yield return AssertSceneTickCreatesRuntimeRoot(
                UIAudioScenePath,
                StageId.CreateOrThrow("combined-gameplay-showcase"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplayShellStage0_1_DirectPlayTick_CreatesGameplayVfxRuntimeRoot()
        {
            yield return AssertSceneTickCreatesRuntimeRoot(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-0-1"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator UIAudioScene_Stage0_1_InitialEntranceSpawn_ResolvesBeforeFirstTick()
        {
            try
            {
                StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("stage-0-1"));
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateNonCampaign(StageId.CreateOrThrow("stage-0-1")));
                yield return LoadScene(UIAudioScenePath);

                var host = Object.FindFirstObjectByType<GameplaySceneHost>();
                Assert.That(host, Is.Not.Null, $"{UIAudioScenePath} must create a GameplaySceneHost.");
                var runtime = host.GetComponent<GameplayVfxProductionRuntime>();
                Assert.That(runtime, Is.Not.Null, $"{UIAudioScenePath} must include a GameplayVfxProductionRuntime.");

                Assert.That(runtime.IsHostDefaultMapConfigured, Is.True);
                Assert.That(runtime.LastInitialPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.LastInitialEntranceSpawnRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingPrefabCount, Is.Zero);
                Assert.That(runtime.MapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.InitialRequestSkippedBecauseMapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastInitialActiveEntranceSpawnInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayShellCombinedGameplayShowcaseStage_InitialBootstrap_ConfiguresVfxMapBeforeFirstTick()
        {
            try
            {
                StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("combined-gameplay-showcase"));
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateNonCampaign(StageId.CreateOrThrow("combined-gameplay-showcase")));
                yield return LoadScene(UIAudioScenePath);

                var host = Object.FindFirstObjectByType<GameplaySceneHost>();
                Assert.That(host, Is.Not.Null, $"{UIAudioScenePath} must create a GameplaySceneHost.");
                var runtime = host.GetComponent<GameplayVfxProductionRuntime>();
                Assert.That(runtime, Is.Not.Null, $"{UIAudioScenePath} must include a GameplayVfxProductionRuntime.");

                Assert.That(runtime.IsHostDefaultMapConfigured, Is.True);
                Assert.That(runtime.MapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.InitialRequestSkippedBecauseMapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
            }
            finally
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplayShellTutorialStage_InitialBootstrap_KeepsCanonicalMigratedVfxEnabled()
        {
            try
            {
                StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("tutorial-scene"));
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateNonCampaign(StageId.CreateOrThrow("tutorial-scene")));
                yield return LoadScene(UIAudioScenePath);

                var host = Object.FindFirstObjectByType<GameplaySceneHost>();
                Assert.That(host, Is.Not.Null, $"{UIAudioScenePath} must create a GameplaySceneHost.");
                var runtime = host.GetComponent<GameplayVfxProductionRuntime>();
                Assert.That(runtime, Is.Not.Null, $"{UIAudioScenePath} must include a GameplayVfxProductionRuntime.");

                Assert.That(runtime.EnableGameplayVfxTileFeatureLane, Is.True);
                Assert.That(runtime.MapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.InitialRequestSkippedBecauseMapNotConfiguredCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
            }
            finally
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
            }
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

        private static int CountOneShotInstances(Transform hostRoot)
        {
            var runtimeRoot = FindRuntimeRoot(hostRoot);
            var oneShotRoot = runtimeRoot != null ? runtimeRoot.Find("OneShot") : null;
            return oneShotRoot != null ? oneShotRoot.childCount : 0;
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
