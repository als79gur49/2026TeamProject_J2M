using System;
using System.IO;
using System.Linq;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class LocaleCatalogSelectionPolicyTests
    {
        [Test]
        public void Catalog_OrdersAuthoringKnownAndShipReadyRowsByStableOrder()
        {
            var catalog = CreateSyntheticCatalog();

            Assert.That(
                catalog.AuthoringKnownLocales.Select(entry => entry.CanonicalCode),
                Is.EqualTo(new[] { "loc-A", "loc-B", "loc-C" }));
            Assert.That(
                catalog.ShipReadyLocales.Select(entry => entry.CanonicalCode),
                Is.EqualTo(new[] { "loc-A", "loc-C" }));
        }

        [Test]
        public void SelectionPolicy_DistinguishesRegisteredAndSelectableSets()
        {
            var policy = new LocaleSelectionPolicy(
                CreateSyntheticCatalog(),
                new[] { "loc-C", "loc-B", "external-locale", "loc-A", "loc-C" });

            Assert.That(
                policy.RegisteredLocaleCodes,
                Is.EqualTo(new[] { "external-locale", "loc-A", "loc-B", "loc-C" }));
            Assert.That(
                policy.RegisteredLocaleCodes.Except(policy.Catalog.AuthoringKnownLocales.Select(entry => entry.CanonicalCode)),
                Is.EqualTo(new[] { "external-locale" }));
            Assert.That(policy.SelectableLocales.Select(entry => entry.CanonicalCode), Is.EqualTo(new[] { "loc-A", "loc-C" }));
        }

        [TestCase("loc-A", 1, "loc-C")]
        [TestCase("loc-C", 1, "loc-A")]
        [TestCase("loc-C", -1, "loc-A")]
        [TestCase("loc-A", -1, "loc-C")]
        public void SelectionPolicy_CyclesBothDirectionsAndWraps(string current, int direction, string expected)
        {
            var policy = CreateSyntheticPolicy();

            Assert.That(policy.GetAdjacentSelectableCode(current, direction), Is.EqualTo(expected));
        }

        [Test]
        public void SelectionPolicy_RejectsDraftAndUnregisteredRequests()
        {
            var policy = CreateSyntheticPolicy();

            Assert.That(policy.EvaluateRequest("loc-B", "loc-A").Status, Is.EqualTo(LocaleSelectionStatus.Rejected));
            Assert.That(policy.EvaluateRequest("missing", "loc-A").Status, Is.EqualTo(LocaleSelectionStatus.Rejected));
            Assert.That(policy.EvaluateRequest("LOC-A", "loc-A").Status, Is.EqualTo(LocaleSelectionStatus.Rejected));

            var aliasOnlyRegistration = new LocaleSelectionPolicy(
                CreateSyntheticCatalog(),
                new[] { "loc-A", "legacy-c" });
            Assert.That(
                aliasOnlyRegistration.EvaluateRequest("legacy-c", "loc-A").Status,
                Is.EqualTo(LocaleSelectionStatus.Rejected));
        }

        [Test]
        public void SelectionPolicy_UsesPersistedThenSelectedThenDefaultPrecedence()
        {
            var policy = CreateSyntheticPolicy();

            Assert.That(policy.ResolveInitialLocale("legacy-c", "loc-A"), Is.EqualTo("loc-C"));
            Assert.That(policy.ResolveInitialLocale("loc-B", "loc-C"), Is.EqualTo("loc-C"));
            Assert.That(policy.ResolveInitialLocale("missing", "loc-B"), Is.EqualTo("loc-A"));
            Assert.That(policy.ResolveInitialLocale(null, null), Is.EqualTo("loc-A"));
        }

        [Test]
        public void SelectionPolicy_DraftPersistedAndSelectedCandidatesFallThroughToRegisteredDefault()
        {
            var policy = CreateSyntheticPolicy();

            Assert.That(policy.ResolveInitialLocale("loc-B", "loc-B"), Is.EqualTo("loc-A"));
            Assert.That(policy.ResolveInitialLocale("loc-B", "loc-C"), Is.EqualTo("loc-C"));
        }

        [Test]
        public void SelectionPolicy_MissingRegisteredDefaultFailsDeterministically()
        {
            var policy = new LocaleSelectionPolicy(
                CreateSyntheticCatalog(),
                new[] { "loc-B", "loc-C" });

            var exception = Assert.Throws<InvalidOperationException>(() =>
                policy.ResolveInitialLocale("loc-B", "missing"));
            Assert.That(exception.Message, Does.Contain("default ShipReady locale"));
            Assert.That(exception.Message, Does.Contain("not registered"));
        }

        [Test]
        public void SelectionPolicy_CanonicalizesExplicitAliasAndReportsSameLocaleNoOp()
        {
            var policy = CreateSyntheticPolicy();

            var changed = policy.EvaluateRequest("legacy-c", "loc-A");
            var noOp = policy.EvaluateRequest("legacy-c", "loc-C");

            Assert.That(changed.Status, Is.EqualTo(LocaleSelectionStatus.Change));
            Assert.That(changed.CanonicalCode, Is.EqualTo("loc-C"));
            Assert.That(noOp.Status, Is.EqualTo(LocaleSelectionStatus.NoOp));
            Assert.That(noOp.CanonicalCode, Is.EqualTo("loc-C"));
        }

        [Test]
        public void ProductionCatalog_PreservesCurrentShipReadyRows()
        {
            var catalog = UiLocaleCatalog.CreateProduction();

            Assert.That(catalog.DefaultLocaleCode, Is.EqualTo("en-US"));
            Assert.That(catalog.EmergencyFallbackLocaleCode, Is.EqualTo("en-US"));
            Assert.That(catalog.AuthoringKnownLocales.Select(entry => entry.CanonicalCode), Is.EqualTo(new[] { "en-US", "ko-KR" }));
            Assert.That(catalog.AuthoringKnownLocales.Select(entry => entry.DisplayName), Is.EqualTo(new[] { "English", "한국어" }));
            Assert.That(catalog.ShipReadyLocales.All(entry => entry.Lifecycle == LocaleLifecycle.ShipReady), Is.True);
            Assert.That(PackageFreeLocalizedTextResolver.CreateSettingsDefault().AvailableLocaleCodes, Is.EqualTo(new[] { "en-US", "ko-KR" }));
        }

        [Test]
        public void AddingThirdLocale_RequiresOnlyCatalogData()
        {
            var rows = new[]
            {
                new LocaleCatalogEntry("loc-A", "Locale A", 10, LocaleLifecycle.ShipReady),
                new LocaleCatalogEntry("loc-B", "Locale B", 20, LocaleLifecycle.ShipReady),
                new LocaleCatalogEntry("loc-C", "Locale C", 30, LocaleLifecycle.ShipReady),
            };
            var policy = new LocaleSelectionPolicy(
                new UiLocaleCatalog(rows, "loc-A", "loc-A"),
                rows.Select(row => row.CanonicalCode));

            Assert.That(policy.SelectableLocales.Select(entry => entry.CanonicalCode), Is.EqualTo(new[] { "loc-A", "loc-B", "loc-C" }));
            Assert.That(policy.GetAdjacentSelectableCode("loc-B", 1), Is.EqualTo("loc-C"));
        }

        [Test]
        public void Catalog_RejectsDuplicateIdentityAliasCollisionAndInvalidFallback()
        {
            Assert.Throws<ArgumentException>(() => new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("loc-A", "A", 10, LocaleLifecycle.ShipReady, new[] { "old-a" }),
                    new LocaleCatalogEntry("loc-B", "B", 20, LocaleLifecycle.ShipReady, new[] { "old-a" }),
                },
                "loc-A",
                "loc-A"));

            Assert.Throws<ArgumentException>(() => new UiLocaleCatalog(
                new[] { new LocaleCatalogEntry("loc-A", "A", 10, LocaleLifecycle.ShipReady) },
                "missing",
                "loc-A"));

            Assert.Throws<ArgumentException>(() => new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("loc-A", "A", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("loc-B", "B", 10, LocaleLifecycle.ShipReady),
                },
                "loc-A",
                "loc-A"));

            Assert.Throws<ArgumentException>(() => new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("loc-A", "A", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("loc-B", "B", 20, LocaleLifecycle.Draft),
                },
                "loc-B",
                "loc-A"));

            Assert.Throws<ArgumentException>(() => new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("loc-A", "A", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("loc-B", "B", 20, LocaleLifecycle.Draft),
                },
                "loc-A",
                "loc-B"));
        }

        [Test]
        public void SelectionPolicy_RejectsEmptyRegisteredIdentities()
        {
            Assert.Throws<ArgumentException>(() => new LocaleSelectionPolicy(
                CreateSyntheticCatalog(),
                new[] { "loc-A", null }));
            Assert.Throws<ArgumentException>(() => new LocaleSelectionPolicy(
                CreateSyntheticCatalog(),
                new[] { "loc-A", " " }));
        }

        [Test]
        public void SelectionResult_CannotBeConstructedOutsideItsPolicyOwner()
        {
            Assert.That(typeof(LocaleSelectionResult).GetConstructors(), Is.Empty);
        }

        [Test]
        public void LocaleCatalog_RemainsFreeOfUnityLocalizationAndAddressablesDependencies()
        {
            var source = File.ReadAllText(
                "Assets/_Features/UI/UI_ViewShared/Runtime/UiLocaleCatalog.cs");
            var assemblyDefinition = File.ReadAllText(
                "Assets/_Features/UI/UI_ViewShared/UI.ViewShared.asmdef");

            Assert.That(source, Does.Not.Contain("using Unity"));
            Assert.That(source, Does.Not.Contain("LocalizationSettings"));
            Assert.That(source, Does.Not.Contain("Addressables"));
            Assert.That(assemblyDefinition, Does.Not.Contain("Unity.Localization"));
            Assert.That(assemblyDefinition, Does.Not.Contain("Unity.Addressables"));
        }

        private static LocaleSelectionPolicy CreateSyntheticPolicy()
        {
            return new LocaleSelectionPolicy(
                CreateSyntheticCatalog(),
                new[] { "loc-A", "loc-B", "loc-C" });
        }

        private static UiLocaleCatalog CreateSyntheticCatalog()
        {
            return new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("loc-C", "Locale C", 30, LocaleLifecycle.ShipReady, new[] { "legacy-c" }),
                    new LocaleCatalogEntry("loc-A", "Locale A", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("loc-B", "Locale B", 20, LocaleLifecycle.Draft),
                },
                "loc-A",
                "loc-A");
        }
    }
}
