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
        public void Resolve_PrefersLaunchContextOverDefaultStageId()
        {
            Assert.That(StageId.TryCreate("combined-gameplay-showcase", out var catalogStageId), Is.True);
            Assert.That(StageId.TryCreate("tutorial-scene", out var defaultStageId), Is.True);
            StageLaunchContextStore.SetCurrent(catalogStageId);

            var resolved = resolver.Resolve(StageLoadRequest.CreateEditorDirectPlayFallback(
                provider,
                defaultStageId,
                "PolicyTestScene"));

            Assert.That(resolved.RequestedStageId, Is.EqualTo(catalogStageId));
            Assert.That(resolved.UsedLaunchContext, Is.True);
            Assert.That(resolved.UsedDefaultStageIdFallback, Is.False);
        }

        [Test]
        public void Resolve_UsesDefaultStageIdOnlyWhenFallbackIsExplicitlyAllowed()
        {
            Assert.That(StageId.TryCreate("combined-gameplay-showcase", out var defaultStageId), Is.True);

            var resolved = resolver.Resolve(StageLoadRequest.CreateEditorDirectPlayFallback(
                provider,
                defaultStageId,
                "PolicyTestScene"));

            Assert.That(resolved.RequestedStageId, Is.EqualTo(defaultStageId));
            Assert.That(resolved.UsedLaunchContext, Is.False);
            Assert.That(resolved.UsedDefaultStageIdFallback, Is.True);
        }

        [Test]
        public void Resolve_ThrowsWhenLaunchContextIsMissingAndFallbackIsDisabled()
        {
            Assert.That(StageId.TryCreate("combined-gameplay-showcase", out var defaultStageId), Is.True);

            var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(StageLoadRequest.CreateLaunchContextOnly(
                provider,
                "PolicyTestScene")));

            StringAssert.Contains("defaultStageId fallback is not permitted", exception?.Message);
        }

        [Test]
        public void CreateEditorDirectPlayFallback_RequiresValidDefaultStageId()
        {
            var exception = Assert.Throws<ArgumentException>(() => StageLoadRequest.CreateEditorDirectPlayFallback(
                provider,
                StageId.None,
                "PolicyTestScene"));

            StringAssert.Contains("valid defaultStageId", exception?.Message);
        }
    }
}
