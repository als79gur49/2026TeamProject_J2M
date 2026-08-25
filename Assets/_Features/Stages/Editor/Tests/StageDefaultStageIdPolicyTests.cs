using System;
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

            Assert.That(StageId.TryCreate("stage-0-1", out var stageId), Is.True);
            entry.AssignStageId(stageId);
            catalog.SetEntries(new[] { entry });
            provider.AssignCatalog(catalog);
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
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
                    "stage-0-1",
                    "stage-1-1",
                }));
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("stage-0-1")), Is.True);
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("stage-1-1")), Is.True);
            Assert.That(catalogAsset.HasSupportedStageId(StageId.CreateOrThrow("legacy-stage-5-1")), Is.False);
        }

        [Test]
        public void Launcher_PrimesPendingStageIdForSupportedStage()
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");

            StageEditorDirectPlayLauncher.PrimeNonCampaignForTests(stageId);

            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().StageId, Is.EqualTo(stageId));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var pendingStageId), Is.True);
            Assert.That(pendingStageId, Is.EqualTo(stageId));
        }

        [Test]
        public void PlayerCaptureLaunchOptions_NormalizesStageArgument()
        {
            var parsed = PlayerCaptureLaunchOptions.TryParse(
                new[] { "Game.exe", "--capture-stage", "Stage_0/1" },
                out var options,
                out var error);

            Assert.That(parsed, Is.True, error);
            Assert.That(options.HasCaptureStage, Is.True);
            Assert.That(options.StageId.Value, Is.EqualTo("stage-0-1"));
        }

        [Test]
        public void PlayerCaptureBootstrap_PrimesStageIdBeforeSceneLoad()
        {
            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[] { "Game.exe", "--capture-stage=stage-0-1" },
                logErrors: false,
                out var error);

            Assert.That(primed, Is.True, error);
            Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
            Assert.That(current.Value, Is.EqualTo("stage-0-1"));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().SuppressCampaignFlow, Is.True);
        }

        [TestCase(3)]
        [TestCase(2)]
        [TestCase(1)]
        public void PlayerCaptureBootstrap_CampaignTempSlotHonorsExplicitRemainingChances(
            int remainingChances)
        {
            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[]
                {
                    "Game.exe",
                    "--capture-stage=stage-0-1",
                    PlayerCaptureLaunchBootstrap.CampaignTempSlotArgument,
                    PlayerCaptureLaunchBootstrap.CampaignTempSlotChancesArgument,
                    remainingChances.ToString(),
                },
                logErrors: false,
                out var error);

            var store = CampaignSaveCompositionProvider.CreateTemporaryProfileBacked();
            Assert.That(primed, Is.True, error);
            Assert.That(store.LoadSlot(1).State.RemainingChances, Is.EqualTo(remainingChances));
            Assert.That(
                EditorDirectPlayContextStore.GetCurrentOrNone().RemainingChances,
                Is.EqualTo(remainingChances));
        }

        [Test]
        public void PlayerCaptureBuildScenes_CaptureStageUsesGameplayShellScene()
        {
            var scenes = PlayerProfilerCaptureCli.ResolveBuildScenesForTests(new[]
            {
                "Unity.exe",
                "-captureStage",
                "stage-0-1",
            });

            Assert.That(scenes, Is.EqualTo(new[] { "Assets/Scenes/UIAudioScene.unity" }));
        }

        [Test]
        public void PlayerCaptureBuildScenes_CaptureStageCanIncludeMainMenuSupplement()
        {
            var scenes = PlayerProfilerCaptureCli.ResolveBuildScenesForTests(new[]
            {
                "Unity.exe",
                "--capture-stage",
                "stage-0-1",
                "-captureSupplementalScenes",
                "Assets/Scenes/MainMenuScene.unity",
            });

            Assert.That(
                scenes,
                Is.EqualTo(new[]
                {
                    "Assets/Scenes/UIAudioScene.unity",
                    "Assets/Scenes/MainMenuScene.unity",
                }));
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
