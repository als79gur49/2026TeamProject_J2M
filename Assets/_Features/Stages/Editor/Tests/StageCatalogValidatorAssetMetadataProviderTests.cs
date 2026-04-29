using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCatalogValidatorAssetMetadataProviderTests
    {
        [Test]
        public void AssetMetadataProvider_ExplicitProviderOverridesDefault()
        {
            var previous = StageCatalogValidator.GetDefaultAssetMetadataProviderForTests();
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();

            try
            {
                StageCatalogValidator.ConfigureDefaultAssetMetadataProvider(new FixedMetadataProvider("default-path", "default-guid"));

                var report = new StageCatalogValidator().ValidateEntries(
                    new[] { entry },
                    aliasTable: null,
                    new StageCatalogValidationOptions
                    {
                        AssetMetadataProvider = new FixedMetadataProvider("explicit-path", "explicit-guid"),
                    });

                Assert.That(report.Issues.Any(issue => issue.AssetPath == "explicit-path"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.AssetPath == "default-path"), Is.False);
            }
            finally
            {
                RestoreDefaultProvider(previous);
                Object.DestroyImmediate(entry);
            }
        }

        [Test]
        public void AssetMetadataProvider_DefaultProviderCanBeCleared()
        {
            var previous = StageCatalogValidator.GetDefaultAssetMetadataProviderForTests();
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();

            try
            {
                StageCatalogValidator.ConfigureDefaultAssetMetadataProvider(new FixedMetadataProvider("default-path", "default-guid"));
                var withDefault = new StageCatalogValidator().ValidateEntries(new[] { entry }, aliasTable: null);
                Assert.That(withDefault.Issues.Any(issue => issue.AssetPath == "default-path"), Is.True);

                StageCatalogValidator.ClearDefaultAssetMetadataProvider();
                var withoutDefault = new StageCatalogValidator().ValidateEntries(new[] { entry }, aliasTable: null);
                Assert.That(withoutDefault.Issues.All(issue => string.IsNullOrEmpty(issue.AssetPath)), Is.True);
            }
            finally
            {
                RestoreDefaultProvider(previous);
                Object.DestroyImmediate(entry);
            }
        }

        [Test]
        public void AssetMetadataProvider_ScopeRestoresPreviousProvider()
        {
            var previous = StageCatalogValidator.GetDefaultAssetMetadataProviderForTests();

            try
            {
                var outer = new FixedMetadataProvider("outer-path", "outer-guid");
                var inner = new FixedMetadataProvider("inner-path", "inner-guid");
                StageCatalogValidator.ConfigureDefaultAssetMetadataProvider(outer);

                using (StageCatalogValidator.UseDefaultAssetMetadataProvider(inner))
                {
                    Assert.That(StageCatalogValidator.GetDefaultAssetMetadataProviderForTests(), Is.SameAs(inner));
                }

                Assert.That(StageCatalogValidator.GetDefaultAssetMetadataProviderForTests(), Is.SameAs(outer));
            }
            finally
            {
                RestoreDefaultProvider(previous);
            }
        }

        [Test]
        public void AssetMetadataProvider_NoProviderUsesNoOpMetadata()
        {
            var previous = StageCatalogValidator.GetDefaultAssetMetadataProviderForTests();
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();

            try
            {
                StageCatalogValidator.ClearDefaultAssetMetadataProvider();

                var report = new StageCatalogValidator().ValidateEntries(new[] { entry }, aliasTable: null);

                Assert.That(report.Issues.Count, Is.GreaterThan(0));
                Assert.That(report.Issues.All(issue => string.IsNullOrEmpty(issue.AssetPath)), Is.True);
            }
            finally
            {
                RestoreDefaultProvider(previous);
                Object.DestroyImmediate(entry);
            }
        }

        private static void RestoreDefaultProvider(IStageValidationAssetMetadataProvider provider)
        {
            if (provider == null)
            {
                StageCatalogValidator.ClearDefaultAssetMetadataProvider();
                return;
            }

            StageCatalogValidator.ConfigureDefaultAssetMetadataProvider(provider);
        }

        private sealed class FixedMetadataProvider : IStageValidationAssetMetadataProvider
        {
            private readonly string assetPath;
            private readonly string assetGuid;

            public FixedMetadataProvider(string assetPath, string assetGuid)
            {
                this.assetPath = assetPath;
                this.assetGuid = assetGuid;
            }

            public string GetAssetPath(UnityEngine.Object asset)
            {
                return asset == null ? string.Empty : assetPath;
            }

            public string GetAssetGuid(UnityEngine.Object asset)
            {
                return asset == null ? string.Empty : assetGuid;
            }
        }
    }
}
