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
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
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
            Assert.That(combinedStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(catalogAsset.TryResolveScenePath("Assets/Scenes/UIAudioScene.unity", out var uiAudioStageId), Is.True);
            Assert.That(uiAudioStageId.Value, Is.EqualTo("stage-0-1"));
        }

        [Test]
        public void Launcher_PrimesPendingStageIdForRegisteredScene()
        {
            var stageId = StageEditorDirectPlayLauncher.PrimePendingLaunchForScene("Assets/Scenes/TutorialScene.unity");

            Assert.That(stageId.Value, Is.EqualTo("stage-0-1"));
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var pendingStageId), Is.True);
            Assert.That(pendingStageId, Is.EqualTo(stageId));
        }
    }
}
