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

            var resolved = resolver.Resolve(new StageLoadRequest(
                StageLoadSourceMode.CatalogResolvedStageId,
                provider,
                defaultStageId,
                null,
                null,
                null,
                null,
                "PolicyTestScene",
                allowDefaultStageIdFallback: true));

            Assert.That(resolved.RequestedStageId, Is.EqualTo(catalogStageId));
            Assert.That(resolved.UsedLaunchContext, Is.True);
            Assert.That(resolved.UsedDefaultStageIdFallback, Is.False);
        }

        [Test]
        public void Resolve_UsesDefaultStageIdOnlyWhenFallbackIsExplicitlyAllowed()
        {
            Assert.That(StageId.TryCreate("combined-gameplay-showcase", out var defaultStageId), Is.True);

            var resolved = resolver.Resolve(new StageLoadRequest(
                StageLoadSourceMode.CatalogResolvedStageId,
                provider,
                defaultStageId,
                null,
                null,
                null,
                null,
                "PolicyTestScene",
                allowDefaultStageIdFallback: true));

            Assert.That(resolved.RequestedStageId, Is.EqualTo(defaultStageId));
            Assert.That(resolved.UsedLaunchContext, Is.False);
            Assert.That(resolved.UsedDefaultStageIdFallback, Is.True);
        }

        [Test]
        public void Resolve_ThrowsWhenLaunchContextIsMissingAndFallbackIsDisabled()
        {
            Assert.That(StageId.TryCreate("combined-gameplay-showcase", out var defaultStageId), Is.True);

            var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new StageLoadRequest(
                StageLoadSourceMode.CatalogResolvedStageId,
                provider,
                defaultStageId,
                null,
                null,
                null,
                null,
                "PolicyTestScene",
                allowDefaultStageIdFallback: false)));

            StringAssert.Contains("defaultStageId fallback is not permitted", exception?.Message);
        }
    }
}
