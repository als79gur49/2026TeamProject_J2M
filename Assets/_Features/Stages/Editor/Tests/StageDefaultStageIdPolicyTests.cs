using System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageDefaultStageIdPolicyTests
    {
        private StageCatalog catalog;
        private ScriptableObjectStageCatalogProvider provider;
        private StageContentEntry entry;
        private StageRuntimeContentResolver resolver;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<StageCatalog>();
            provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            entry = ScriptableObject.CreateInstance<StageContentEntry>();
            resolver = new StageRuntimeContentResolver();

            Assert.That(StageId.TryCreate("combined-gameplay-showcase", out var stageId), Is.True);
            entry.AssignStageId(stageId);
            catalog.SetEntries(new[] { entry });
            provider.AssignCatalog(catalog);
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            UnityEngine.Object.DestroyImmediate(entry);
            UnityEngine.Object.DestroyImmediate(provider);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Resolve_UsesLaunchContextOnly_WhenCurrentStageIsPresent()
        {
            StageLaunchContextStore.SetCurrent(entry.StageId);

            var resolved = resolver.Resolve(StageLoadRequest.CreateLaunchContextOnly(
                provider,
                "PolicyTestScene"));

            Assert.That(resolved.RequestedStageId, Is.EqualTo(entry.StageId));
            Assert.That(resolved.UsedLaunchContext, Is.True);
        }

        [Test]
        public void Resolve_ConsumesPendingEditorDirectPlayStageId()
        {
            StageLaunchContextStore.PrimePendingEditorDirectPlay(entry.StageId);

            var resolved = resolver.Resolve(StageLoadRequest.CreateLaunchContextOnly(
                provider,
                "PolicyTestScene"));

            Assert.That(resolved.RequestedStageId, Is.EqualTo(entry.StageId));
            Assert.That(resolved.UsedLaunchContext, Is.True);
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
        }

        [Test]
        public void Resolve_ThrowsWhenLaunchContextIsMissing()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(StageLoadRequest.CreateLaunchContextOnly(
                provider,
                "PolicyTestScene")));

            StringAssert.Contains("Tools/Stages/Direct Play/Launch Current Scene", exception?.Message);
        }

        [Test]
        public void DirectPlayCatalog_ResolvesGameplayShellScenePathOnly()
        {
            var catalogAsset = StageEditorDirectPlayCatalog.LoadDefault();

            Assert.That(catalogAsset, Is.Not.Null);
            Assert.That(catalogAsset.TryResolveScenePath("Assets/Scenes/UIAudioScene.unity", out var uiAudioStageId), Is.True);
            Assert.That(uiAudioStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(catalogAsset.HasScenePath(BuildScenePath("CombinedGameplayShowcase")), Is.False);
            Assert.That(catalogAsset.HasScenePath(BuildScenePath("TutorialScene")), Is.False);
        }

        [Test]
        public void Launcher_PrimesPendingStageIdForSelectedStageThroughGameplayShell()
        {
            var stageId = StageEditorDirectPlayLauncher.PrimePendingLaunchForStage(
                StageId.CreateOrThrow("tutorial-scene"));

            Assert.That(stageId.Value, Is.EqualTo("tutorial-scene"));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var pendingStageId), Is.True);
            Assert.That(pendingStageId, Is.EqualTo(stageId));
        }

        [Test]
        public void PlayerCaptureLaunchOptions_NormalizesTutorialStageArgument()
        {
            var parsed = PlayerCaptureLaunchOptions.TryParse(
                new[] { "Game.exe", "--capture-stage", "Tutorial Scene" },
                out var options,
                out var error);

            Assert.That(parsed, Is.True, error);
            Assert.That(options.HasCaptureStage, Is.True);
            Assert.That(options.StageId.Value, Is.EqualTo("tutorial-scene"));
        }

        [Test]
        public void PlayerCaptureBootstrap_PrimesTutorialStageIdBeforeSceneLoad()
        {
            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[] { "Game.exe", "--capture-stage=tutorial-scene" },
                logErrors: false,
                out var error);

            Assert.That(primed, Is.True, error);
            Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
            Assert.That(current.Value, Is.EqualTo("tutorial-scene"));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().SuppressCampaignFlow, Is.True);
        }

        [Test]
        public void PlayerCaptureBuildScenes_CaptureStageUsesGameplayShellScene()
        {
            var scenes = PlayerProfilerCaptureCli.ResolveBuildScenesForTests(new[]
            {
                "Unity.exe",
                "-captureStage",
                "tutorial-scene",
                "-captureScenes",
                "Assets/Scenes/UIAudioScene.unity",
            });

            Assert.That(scenes, Is.EqualTo(new[] { "Assets/Scenes/UIAudioScene.unity" }));
        }

        [Test]
        public void PlayerCaptureBuildScenes_RejectsGameplayShellWithoutCaptureStage()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => PlayerProfilerCaptureCli.ResolveBuildScenesForTests(new[]
                {
                    "Unity.exe",
                    "-captureScenes",
                    "Assets/Scenes/UIAudioScene.unity",
                }));

            Assert.That(exception?.Message, Does.Contain("--capture-stage stage-1-1"));
        }

        private static string BuildScenePath(string sceneName)
        {
            return $"Assets/Scenes/{sceneName}.unity";
        }
    }
}
