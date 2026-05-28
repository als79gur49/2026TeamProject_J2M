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
        public void DirectPlayCatalog_ResolvesSupportedScenePaths()
        {
            var catalogAsset = StageEditorDirectPlayCatalog.LoadDefault();

            Assert.That(catalogAsset, Is.Not.Null);
            Assert.That(catalogAsset.TryResolveScenePath("Assets/Scenes/CombinedGameplayShowcase.unity", out var combinedStageId), Is.True);
            Assert.That(combinedStageId.Value, Is.EqualTo("combined-gameplay-showcase"));
            Assert.That(catalogAsset.TryResolveScenePath("Assets/Scenes/UIAudioScene.unity", out var uiAudioStageId), Is.True);
            Assert.That(uiAudioStageId.Value, Is.EqualTo("stage-1-1"));
        }

        [Test]
        public void Launcher_PrimesPendingStageIdForRegisteredScene()
        {
            var stageId = StageEditorDirectPlayLauncher.PrimePendingLaunchForScene("Assets/Scenes/TutorialScene.unity");

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
                "Assets/Scenes/TutorialScene.unity",
            });

            Assert.That(scenes, Is.EqualTo(new[] { "Assets/Scenes/UIAudioScene.unity" }));
        }

        [Test]
        public void PlayerCaptureBuildScenes_RejectsDirectTutorialSceneWithoutCaptureStage()
        {
            var exception = Assert.Throws<System.InvalidOperationException>(() =>
                PlayerProfilerCaptureCli.ResolveBuildScenesForTests(new[]
                {
                    "Unity.exe",
                    "-captureScenes",
                    "Assets/Scenes/TutorialScene.unity",
                }));

            StringAssert.Contains("--capture-stage tutorial-scene", exception?.Message);
        }
    }
}
