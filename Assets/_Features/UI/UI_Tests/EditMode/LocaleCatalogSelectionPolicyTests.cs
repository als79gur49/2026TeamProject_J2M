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
        public void LocaleOptionModel_RejectsBlankIdentityAndAutonym()
        {
            Assert.Throws<ArgumentException>(() => new LocaleOptionModel(" ", "English"));
            Assert.Throws<ArgumentException>(() => new LocaleOptionModel("en-US", " "));
        }

        [Test]
        public void LocaleOptionSnapshot_IsOwnedReadOnlyAndRejectsNullOrDuplicateRows()
        {
            var source = new[]
            {
                new LocaleCatalogEntry("en-US", "English", 10, LocaleLifecycle.ShipReady),
            };
            var snapshot = LocaleOptionSnapshot.FromCatalogEntries(source);

            source[0] = new LocaleCatalogEntry("ko-KR", "한국어", 20, LocaleLifecycle.ShipReady);

            Assert.That(snapshot[0].CanonicalCode, Is.EqualTo("en-US"));
            Assert.That(snapshot, Is.InstanceOf<System.Collections.IList>());
            Assert.That(((System.Collections.IList)snapshot).IsReadOnly, Is.True);
            Assert.Throws<ArgumentException>(() =>
                LocaleOptionSnapshot.FromCatalogEntries(new LocaleCatalogEntry[] { null }));
            Assert.Throws<ArgumentException>(() =>
                LocaleOptionSnapshot.FromCatalogEntries(new[]
                {
                    new LocaleCatalogEntry("en-US", "English", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("en-US", "English duplicate", 20, LocaleLifecycle.ShipReady),
                }));
        }

        [Test]
        public void PackageFreeOptionProjection_UsesCatalogOrderNotMembershipOrder()
        {
            var catalogOrder = new[]
            {
                new LocaleCatalogEntry("b-BB", "B", 10, LocaleLifecycle.ShipReady),
                new LocaleCatalogEntry("a-AA", "A", 20, LocaleLifecycle.ShipReady),
                new LocaleCatalogEntry("c-CC", "C", 30, LocaleLifecycle.ShipReady),
            };

            var options = LocaleOptionSnapshot.FromCatalogMembership(
                catalogOrder,
                new[] { "a-AA", "b-BB" });

            Assert.That(
                options.Select(option => option.CanonicalCode).ToArray(),
                Is.EqualTo(new[] { "b-BB", "a-AA" }));
            Assert.That(
                options.Select(option => option.DisplayNameAutonym).ToArray(),
                Is.EqualTo(new[] { "B", "A" }));
        }

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

        [Test]
        public void FinalOptionProjection_DeduplicatesRegistrationAndExcludesDraftUnknownAndAliasRows()
        {
            var policy = new LocaleSelectionPolicy(
                CreateSyntheticCatalog(),
                new[] { "loc-C", "loc-B", "missing", "legacy-c", "loc-A", "loc-C" });

            var options = LocaleOptionSnapshot.FromCatalogEntries(policy.SelectableLocales);

            Assert.That(
                options.Select(option => option.CanonicalCode).ToArray(),
                Is.EqualTo(new[] { "loc-A", "loc-C" }));
            Assert.That(
                options.Select(option => option.DisplayNameAutonym).ToArray(),
                Is.EqualTo(new[] { "Locale A", "Locale C" }));
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

        [TestCase("loc-A", "loc-C", "loc-A", TestName = "PersistedCanonical")]
        [TestCase("legacy-c", "loc-A", "loc-C", TestName = "PersistedAlias")]
        [TestCase("", "loc-C", "loc-C", TestName = "PersistedBlankFallsThrough")]
        [TestCase("loc-B", "loc-C", "loc-C", TestName = "PersistedDraftFallsThrough")]
        [TestCase("unknown", "loc-C", "loc-C", TestName = "PersistedUnknownFallsThrough")]
        [TestCase("loc-D", "loc-C", "loc-C", TestName = "PersistedUnregisteredShipReadyFallsThrough")]
        [TestCase(null, "loc-C", "loc-C", TestName = "PersistedMissingFallsThrough")]
        [TestCase("unknown", "loc-B", "loc-A", TestName = "InvalidSelectedFallsBackToDefault")]
        public void SelectionPolicy_StartupClassificationTable(
            string persistedLocaleCode,
            string selectedLocaleCode,
            string expectedLocaleCode)
        {
            var catalog = new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("loc-A", "Locale A", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("loc-B", "Locale B", 20, LocaleLifecycle.Draft),
                    new LocaleCatalogEntry("loc-C", "Locale C", 30, LocaleLifecycle.ShipReady, new[] { "legacy-c" }),
                    new LocaleCatalogEntry("loc-D", "Locale D", 40, LocaleLifecycle.ShipReady),
                },
                "loc-A",
                "loc-A");
            var policy = new LocaleSelectionPolicy(catalog, new[] { "loc-A", "loc-B", "loc-C" });

            Assert.That(
                policy.ResolveInitialLocale(persistedLocaleCode, selectedLocaleCode),
                Is.EqualTo(expectedLocaleCode));
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
        public void ProductionCatalog_TracksFourLocales_AndExposesOnlyApprovedRows()
        {
            var catalog = UiLocaleCatalog.CreateProduction();

            Assert.That(catalog.DefaultLocaleCode, Is.EqualTo("en-US"));
            Assert.That(catalog.EmergencyFallbackLocaleCode, Is.EqualTo("en-US"));
            Assert.That(
                catalog.AuthoringKnownLocales.Select(entry => entry.CanonicalCode),
                Is.EqualTo(new[] { "en-US", "ko-KR", "ja-JP", "zh-CN" }));
            Assert.That(
                catalog.AuthoringKnownLocales.Select(entry => entry.DisplayName),
                Is.EqualTo(new[] { "English", "한국어", "日本語", "简体中文" }));
            Assert.That(
                catalog.AuthoringKnownLocales.All(entry => entry.Lifecycle == LocaleLifecycle.ShipReady),
                Is.True);
            Assert.That(catalog.ShipReadyLocales.All(entry => entry.Lifecycle == LocaleLifecycle.ShipReady), Is.True);
            Assert.That(
                catalog.ShipReadyLocales.Select(entry => entry.CanonicalCode),
                Is.EqualTo(new[] { "en-US", "ko-KR", "ja-JP", "zh-CN" }));
            Assert.That(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault()
                    .AvailableLocaleOptions
                    .Select(option => option.CanonicalCode)
                    .ToArray(),
                Is.EqualTo(new[] { "en-US", "ko-KR" }));
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
