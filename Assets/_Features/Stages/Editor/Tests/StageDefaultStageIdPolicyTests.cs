using System;
using System.IO;
using System.Linq;
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

            Assert.That(StageId.TryCreate("mechanics-showcase", out var stageId), Is.True);
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

            StringAssert.Contains("Tools/Stages/Direct Play/Launch Stage", exception?.Message);
        }

        [Test]
        public void DirectPlayCatalog_DefinesCanonicalShellAndSupportedStageIds()
        {
            var catalogAsset = StageEditorDirectPlayCatalog.LoadDefault();

            Assert.That(catalogAsset, Is.Not.Null);
            Assert.That(catalogAsset.CanonicalShellScenePath, Is.EqualTo("Assets/Scenes/UIAudioScene.unity"));
            Assert.That(catalogAsset.IsCanonicalShellScenePath("Assets/Scenes/UIAudioScene.unity"), Is.True);
            Assert.That(
                catalogAsset.SupportedStages.Select(entry => entry.StageId.Value).ToArray(),
                Is.EqualTo(new[]
                {
                    "mechanics-showcase",
                    "onboarding",
                    "stage-0-1",
                    "stage-1-1",
                }));
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("mechanics-showcase")), Is.True);
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("onboarding")), Is.True);
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("stage-0-1")), Is.True);
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("stage-1-1")), Is.True);
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("legacy-stage-5-1")), Is.False);
        }

        [Test]
        public void DirectPlayWindow_UsesEditorLaunchContextVocabulary()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayWindow.cs");

            Assert.That(source, Does.Contain("Editor Direct-Play Context"));
            Assert.That(source, Does.Contain("Editor-only direct-play mapping support"));
            Assert.That(source, Does.Contain("Injects launch context before editor play"));
            Assert.That(source, Does.Not.Contain("EnumPopup(\"Mode\""));
            Assert.That(source, Does.Not.Contain("production " + "fallback"));
        }

        [Test]
        public void Launcher_PrimesPendingStageIdForSupportedStage()
        {
            var stageId = StageId.CreateOrThrow("onboarding");

            StageEditorDirectPlayLauncher.PrimeNonCampaignForTests(stageId);

            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().StageId, Is.EqualTo(stageId));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var pendingStageId), Is.True);
            Assert.That(pendingStageId, Is.EqualTo(stageId));
        }

        [Test]
        public void PlayerCaptureLaunchOptions_NormalizesOnboardingStageArgument()
        {
            var parsed = PlayerCaptureLaunchOptions.TryParse(
                new[] { "Game.exe", "--capture-stage", "Onboarding" },
                out var options,
                out var error);

            Assert.That(parsed, Is.True, error);
            Assert.That(options.HasCaptureStage, Is.True);
            Assert.That(options.StageId.Value, Is.EqualTo("onboarding"));
        }

        [Test]
        public void PlayerCaptureBootstrap_PrimesOnboardingStageIdBeforeSceneLoad()
        {
            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[] { "Game.exe", "--capture-stage=onboarding" },
                logErrors: false,
                out var error);

            Assert.That(primed, Is.True, error);
            Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
            Assert.That(current.Value, Is.EqualTo("onboarding"));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().SuppressCampaignFlow, Is.True);
        }

        [Test]
        public void PlayerCaptureBuildScenes_CaptureStageUsesGameplayShellScene()
        {
            var scenes = PlayerProfilerCaptureCli.ResolveBuildScenesForTests(new[]
            {
                "Unity.exe",
                "-captureStage",
                "onboarding",
            });

            Assert.That(scenes, Is.EqualTo(new[] { "Assets/Scenes/UIAudioScene.unity" }));
        }

        [Test]
        public void PlayerCaptureBuildScenes_RejectsGameplayShellSceneWithoutCaptureStage()
        {
            var exception = Assert.Throws<System.InvalidOperationException>(() =>
                PlayerProfilerCaptureCli.ResolveBuildScenesForTests(new[]
                {
                    "Unity.exe",
                    "-captureScenes",
                    "Assets/Scenes/UIAudioScene.unity",
                }));

            StringAssert.Contains("--capture-stage", exception?.Message);
        }
    }
}
