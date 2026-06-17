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
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class ActualSceneBootstrapSmokePlayModeTests
    {
        private const int FirstTickSmokeCount = 5;
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";
#if UNITY_EDITOR
        private const string StageBackedGameplaySceneInstallerGuid = "41909c3f1846cad878e314473f74442c";
        private const string StageBackedGameplaySceneInstallerBasePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs";
        private const string StageBackedGameplaySceneInstallerBaseGuid = "c22c31beb01e4098b026132a77fcc93d";
#endif

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            CampaignChanceHudDiagnostics.Clear();
            CampaignChanceHudDiagnostics.IsEnabled = false;
            CampaignChanceHudDiagnostics.LogToUnityConsole = false;
            yield return CleanupSceneRuntime();
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioSceneStage0_1_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-0-1"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioSceneStage1_1_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-1-1"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioScene_ResolvesFirstStageAndInstallsUiAudio()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-0-1"));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ActualSceneBootstrap_UIAudioSceneStage1_1_DirectPlayEvidence_FirstFiveTicks_NoException()
        {
            yield return AssertSceneBootstrapFirstFiveTicks(
                UIAudioScenePath,
                StageId.CreateOrThrow("stage-1-1"),
                assertDirectPlayEvidence: true);
        }

        private static IEnumerator AssertSceneBootstrapFirstFiveTicks(
            string scenePath,
            StageId stageId,
            bool assertDirectPlayEvidence = false)
        {
            var bootstrapRuntimeErrorCount = 0;

            void CountBootstrapRuntimeErrors(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception)
                {
                    bootstrapRuntimeErrorCount++;
                }
            }

            CampaignChanceHudDiagnostics.Clear();
            CampaignChanceHudDiagnostics.IsEnabled = assertDirectPlayEvidence;
            Application.logMessageReceived += CountBootstrapRuntimeErrors;
            try
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
                Assert.That(host.TickRunner, Is.Not.Null, $"{scenePath} must initialize TickRunner.");
                Assert.That(host.WorldState, Is.Not.Null, $"{scenePath} must reach the initial gameplay state.");
                Assert.That(host.BoardRoot, Is.Not.Null, $"{scenePath} must create the runtime board root.");
                Assert.That(host.UiAccess, Is.Not.Null, $"{scenePath} must expose UIAccess as the read/intent seam.");

                AssertAudioBootstrap(scenePath, host);
                AssertUiBootstrap(scenePath);
                AssertTopologyBootstrap(scenePath, host);
                if (assertDirectPlayEvidence)
                {
                    AssertStage1_1DirectPlayEvidence(scenePath, stageId, host);
                }

                var hashes = new string[FirstTickSmokeCount];
                for (var i = 0; i < FirstTickSmokeCount; i++)
                {
                    var result = host.InputHost.RunSingleTick();
                    yield return null;
                    Assert.That(result, Is.Not.Null, $"{scenePath} tick {i + 1} returned null.");
                    hashes[i] = string.IsNullOrEmpty(result.DeterminismHash) ? "<empty>" : result.DeterminismHash;
                }

                Assert.That(
                    bootstrapRuntimeErrorCount,
                    Is.Zero,
                    $"{scenePath} bootstrap/runtime console error count must be zero.");
                TestContext.WriteLine($"{scenePath} first-five determinism hashes: {string.Join(", ", hashes)}");
            }
            finally
            {
                Application.logMessageReceived -= CountBootstrapRuntimeErrors;
                CampaignChanceHudDiagnostics.IsEnabled = false;
            }
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
            Assert.That(
                host.Presenter.CoreGameplaySfxExecutionMode,
                Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor),
                $"{scenePath} must boot Core SFX with the production orchestration owner.");
            Assert.That(
                host.Presenter.CoreGameplaySfxExecutorDiagnostics.IsProductionDefaultOwner,
                Is.True,
                $"{scenePath} must report Core SFX production default owner telemetry at bootstrap.");
            Assert.That(
                host.Presenter.ActionAudioExecutionMode,
                Is.EqualTo(ActionAudioExecutionMode.LegacyActionAudioController),
                $"{scenePath} must not switch action audio production ownership.");
            Assert.That(
                host.Presenter.EnemyAudioExecutionMode,
                Is.EqualTo(EnemyAudioExecutionMode.LegacyEnemyAudioController),
                $"{scenePath} must not switch enemy audio production ownership.");
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

        private static void AssertStage1_1DirectPlayEvidence(
            string scenePath,
            StageId stageId,
            GameplaySceneHost host)
        {
            Assert.That(stageId.Value, Is.EqualTo("stage-1-1"), "This evidence smoke is scoped to stage-1-1.");
            Assert.That(scenePath, Is.EqualTo(UIAudioScenePath));
            Assert.That(stageId.Value, Is.Not.EqualTo("legacy-stage-5-1"));
            Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(stageId), "requested id must be stage-1-1.");

            var resolveRecord = CampaignChanceHudDiagnostics.Snapshot()
                .SingleOrDefault(record => record.Kind == CampaignChanceHudDiagnosticKind.StageResolve);
            Assert.That(resolveRecord, Is.Not.Null, "Runtime bootstrap must record a StageResolve diagnostic.");
            Assert.That(resolveRecord.LaunchStageId, Is.EqualTo("stage-1-1"), "resolved launch id must match the requested id.");
            Assert.That(resolveRecord.ResolvedStageId, Is.EqualTo("stage-1-1"), "alias use must be false for canonical stage-1-1.");

            var installerRecord = CampaignChanceHudDiagnostics.Snapshot()
                .SingleOrDefault(record => record.Kind == CampaignChanceHudDiagnosticKind.Installer);
            Assert.That(installerRecord, Is.Not.Null, "Runtime bootstrap must record installer diagnostics.");
            Assert.That(installerRecord.LaunchStageId, Is.EqualTo("stage-1-1"));
            Assert.That(installerRecord.ResolvedStageId, Is.EqualTo("stage-1-1"));
            Assert.That(installerRecord.SuppressCampaignFlow, Is.True);
            Assert.That(installerRecord.CampaignRuntimeActive, Is.False);

            Assert.That(host.WorldState, Is.Not.Null, "first gameplay state reached.");
            Assert.That(host.TickRunner.NextTickIndex, Is.GreaterThanOrEqualTo(1), "initial presentation refresh reached before manual tick smoke.");
            AssertSceneInstallerIntegrity(scenePath);
        }

        private static void AssertSceneInstallerIntegrity(string scenePath)
        {
            var installers = Object.FindObjectsByType<StageBackedGameplaySceneInstaller>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(installers, Has.Length.EqualTo(1), $"{scenePath} must have exactly one StageBackedGameplaySceneInstaller.");

#if UNITY_EDITOR
            Assert.That(CountMissingScripts(), Is.Zero, $"{scenePath} must not contain missing MonoBehaviour scripts.");
            var script = MonoScript.FromMonoBehaviour(installers[0]);
            Assert.That(script, Is.Not.Null, $"{scenePath} concrete installer script must resolve.");
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)),
                Is.EqualTo(StageBackedGameplaySceneInstallerGuid),
                "concrete StageBackedGameplaySceneInstaller script GUID must stay stable.");
            Assert.That(
                AssetDatabase.AssetPathToGUID(StageBackedGameplaySceneInstallerBasePath),
                Is.EqualTo(StageBackedGameplaySceneInstallerBaseGuid),
                "base StageBackedGameplaySceneInstallerBase script GUID must stay stable.");
#endif
        }

#if UNITY_EDITOR
        private static int CountMissingScripts()
        {
            var total = 0;
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                total += CountMissingScripts(root);
            }

            return total;
        }

        private static int CountMissingScripts(GameObject root)
        {
            var total = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            foreach (Transform child in root.transform)
            {
                total += CountMissingScripts(child.gameObject);
            }

            return total;
        }
#endif

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
