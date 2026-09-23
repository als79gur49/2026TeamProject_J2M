using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class LocalizationStringGovernanceTests
    {
        [Test]
        public void CompleteFixture_AddsThirdShipReadyLocaleAsDataAndPasses()
        {
            var report = Validate(CreateFixture());

            Assert.That(report.HasBlockingFailures, Is.False);
            Assert.That(report.Diagnostics, Is.Empty);
        }

        [Test]
        public void PublicDictionaryViews_RejectMutationAndKeepSharedEmptySignatureStable()
        {
            var signature = Signature("0");
            var localeTable = new LocalizationLocaleTableSnapshot(
                "loc-A",
                new[] { Entry("ui.title", "Title", false) });
            var table = new LocalizationStringTableSnapshot(
                "UI",
                new[] { "ui.title" },
                new[] { localeTable });

            Assert.That(((IDictionary<string, int>)signature.Counts).IsReadOnly, Is.True);
            Assert.That(
                () => ((IDictionary<string, int>)signature.Counts).Add("1", 1),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(((IDictionary<string, LocalizationStringEntrySnapshot>)localeTable.Entries).IsReadOnly,
                Is.True);
            Assert.That(
                () => ((IDictionary<string, LocalizationStringEntrySnapshot>)localeTable.Entries)
                    .Add("ui.other", Entry("ui.other", "Other", false)),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(((IDictionary<string, LocalizationLocaleTableSnapshot>)table.LocaleTables).IsReadOnly,
                Is.True);
            Assert.That(
                () => ((IDictionary<string, LocalizationLocaleTableSnapshot>)table.LocaleTables)
                    .Add("loc-B", localeTable),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(PlaceholderSignature.Empty.Counts, Is.Empty);
        }

        [Test]
        public void ShipReadyLocaleMissingRegistration_IsBlocking()
        {
            var fixture = CreateFixture(registered: new[] { "loc-A" });

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.ShipReadyLocaleNotRegistered,
                LocalizationDiagnosticSeverity.Blocking, "loc-C", "", "");
        }

        [Test]
        public void DraftLocaleRegisteredForProduction_IsBlocking()
        {
            var fixture = CreateFixture(registered: new[] { "loc-A", "loc-B", "loc-C" });

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.DraftLocaleRegistered,
                LocalizationDiagnosticSeverity.Blocking, "loc-B", "", "");
        }

        [Test]
        public void RegisteredLocaleOutsideCatalog_IsBlocking()
        {
            var fixture = CreateFixture(registered: new[] { "loc-A", "loc-C", "loc-X" });

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.RegisteredLocaleNotInCatalog,
                LocalizationDiagnosticSeverity.Blocking, "loc-X", "", "");
        }

        [TestCase("UI")]
        [TestCase("Stage")]
        public void ShipReadyTableMissing_IsBlocking(string table)
        {
            var fixture = CreateFixture();
            fixture.Tables.Single(candidate => candidate.Table == table).LocaleTables.Remove("loc-C");

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.RequiredTableMissing,
                LocalizationDiagnosticSeverity.Blocking, "loc-C", table, "");
        }

        [Test]
        public void RequiredKeyMissing_IsBlocking()
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-C").Remove("ui.smart");

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.RequiredKeyMissing,
                LocalizationDiagnosticSeverity.Blocking, "loc-C", "UI", "ui.smart");
        }

        [TestCase(null, LocalizationDiagnosticCode.ValueNull)]
        [TestCase("", LocalizationDiagnosticCode.ValueEmpty)]
        [TestCase("   ", LocalizationDiagnosticCode.ValueWhitespace)]
        public void InvalidValue_IsStructuredBlocking(string value, LocalizationDiagnosticCode code)
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-C")["ui.title"] = Entry("ui.title", value, false);

            AssertDiagnostic(Validate(fixture), code, LocalizationDiagnosticSeverity.Blocking,
                "loc-C", "UI", "ui.title");
        }

        [Test]
        public void SmartFlagMismatch_IsBlocking()
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-C")["ui.smart"] =
                Entry("ui.smart", "{1} / {0}", false, true, "1", "0");

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.SmartFlagMismatch,
                LocalizationDiagnosticSeverity.Blocking, "loc-C", "UI", "ui.smart");
        }

        [Test]
        public void MalformedSmartFormat_IsConvertedToDiagnostic()
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-C")["ui.smart"] =
                Entry("ui.smart", "{0", true, false);

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.SmartFormatMalformed,
                LocalizationDiagnosticSeverity.Blocking, "loc-C", "UI", "ui.smart");
        }

        [TestCase(new[] { "0" }, LocalizationDiagnosticCode.PlaceholderMissing)]
        [TestCase(new[] { "0", "1", "2" }, LocalizationDiagnosticCode.PlaceholderAdded)]
        [TestCase(new[] { "0", "2" }, LocalizationDiagnosticCode.PlaceholderSelectorMismatch)]
        [TestCase(new[] { "0", "1", "1" }, LocalizationDiagnosticCode.PlaceholderMultiplicityMismatch)]
        public void PlaceholderDamage_IsClassified(string[] selectors, LocalizationDiagnosticCode code)
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-C")["ui.smart"] =
                Entry("ui.smart", "translated", true, true, selectors);

            AssertDiagnostic(Validate(fixture), code, LocalizationDiagnosticSeverity.Blocking,
                "loc-C", "UI", "ui.smart");
        }

        [Test]
        public void PlaceholderOrderChange_IsAllowed()
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-C")["ui.smart"] =
                Entry("ui.smart", "{1} / {0}", true, true, "1", "0");

            Assert.That(Validate(fixture).HasBlockingFailures, Is.False);
        }

        [Test]
        public void EscapedLiteralBrace_IsNotAPlaceholder()
        {
            var fixture = CreateFixture();
            fixture.Requirements.Add(new LocalizationStringRequirement(
                "UI", "ui.literal", false, PlaceholderSignature.Empty, "literal descriptor"));
            fixture.Tables.Single(table => table.Table == "UI").SharedKeys.Add("ui.literal");
            foreach (var locale in new[] { "loc-A", "loc-B", "loc-C" })
            {
                fixture.Table("UI", locale)["ui.literal"] = Entry("ui.literal", "{{literal}}", false);
            }

            Assert.That(Validate(fixture).Diagnostics, Is.Empty);
        }

        [Test]
        public void GovernedKeyMissingFromSharedData_IsBlocking()
        {
            var fixture = CreateFixture();
            fixture.Tables.Single(table => table.Table == "UI").SharedKeys.Remove("ui.title");

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.GovernedKeyMissingFromSharedData,
                LocalizationDiagnosticSeverity.Blocking, "", "UI", "ui.title");
        }

        [Test]
        public void SharedDataOrphanKey_IsBlocking()
        {
            var fixture = CreateFixture();
            fixture.Tables.Single(table => table.Table == "UI").SharedKeys.Add("ui.orphan");

            AssertDiagnostic(Validate(fixture), LocalizationDiagnosticCode.SharedDataOrphanKey,
                LocalizationDiagnosticSeverity.Blocking, "", "UI", "ui.orphan");
        }

        [Test]
        public void DuplicateRequirementWithMatchingMetadata_IsDeduplicated()
        {
            var fixture = CreateFixture();
            fixture.Requirements.Add(new LocalizationStringRequirement(
                "UI", "ui.smart", true, Signature("1", "0"), "second descriptor"));

            Assert.That(Validate(fixture).Diagnostics, Is.Empty);
        }

        [Test]
        public void DuplicateRequirementSmartMetadataConflict_IsBlockingWithoutCascadingDiagnostics()
        {
            var fixture = CreateFixture();
            fixture.Requirements.Add(new LocalizationStringRequirement(
                "UI", "ui.title", true, PlaceholderSignature.Empty, "conflicting descriptor"));

            var report = Validate(fixture);
            AssertDiagnostic(report, LocalizationDiagnosticCode.RequirementMetadataConflict,
                LocalizationDiagnosticSeverity.Blocking, "", "UI", "ui.title");
            Assert.That(report.Diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void DuplicateRequirementPlaceholderMetadataConflict_IsInputOrderIndependent()
        {
            var first = CreateFixture();
            first.Requirements.Add(new LocalizationStringRequirement(
                "UI", "ui.smart", true, Signature("0"), "conflicting descriptor"));
            var reversed = CreateFixture();
            reversed.Requirements.Insert(0, new LocalizationStringRequirement(
                "UI", "ui.smart", true, Signature("0"), "conflicting descriptor"));

            var firstReport = Validate(first);
            var reversedReport = Validate(reversed);

            AssertDiagnostic(firstReport, LocalizationDiagnosticCode.RequirementMetadataConflict,
                LocalizationDiagnosticSeverity.Blocking, "", "UI", "ui.smart");
            Assert.That(firstReport.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(reversedReport.Diagnostics.Select(ToDiagnosticIdentity),
                Is.EqualTo(firstReport.Diagnostics.Select(ToDiagnosticIdentity)));
        }

        [Test]
        public void DraftIncompleteCopy_IsNonBlocking()
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-B").Remove("ui.title");

            var report = Validate(fixture);
            Assert.That(report.HasBlockingFailures, Is.False);
            AssertDiagnostic(report, LocalizationDiagnosticCode.RequiredKeyMissing,
                LocalizationDiagnosticSeverity.NonBlocking, "loc-B", "UI", "ui.title");
        }

        [Test]
        public void DraftMissingTable_IsNonBlocking()
        {
            var fixture = CreateFixture();
            fixture.Tables.Single(table => table.Table == "Stage").LocaleTables.Remove("loc-B");

            var report = Validate(fixture);

            Assert.That(report.HasBlockingFailures, Is.False);
            AssertDiagnostic(report, LocalizationDiagnosticCode.RequiredTableMissing,
                LocalizationDiagnosticSeverity.NonBlocking, "loc-B", "Stage", "");
            Assert.That(report.Diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void DraftInvalidValue_IsNonBlocking()
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-B")["ui.title"] = Entry("ui.title", " ", false);

            var report = Validate(fixture);

            Assert.That(report.HasBlockingFailures, Is.False);
            AssertDiagnostic(report, LocalizationDiagnosticCode.ValueWhitespace,
                LocalizationDiagnosticSeverity.NonBlocking, "loc-B", "UI", "ui.title");
            Assert.That(report.Diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void NonSmartEntry_DoesNotRequireSmartFormatAnalysis()
        {
            var fixture = CreateFixture();
            fixture.Table("UI", "loc-C")["ui.title"] = new LocalizationStringEntrySnapshot(
                "ui.title", "literal { brace", false, false, Signature("unexpected"),
                "parser was intentionally not used");

            Assert.That(Validate(fixture).Diagnostics, Is.Empty);
        }

        [Test]
        public void MultipleDiagnostics_AreSortedByLocaleStableOrderThenTableKeyAndCode()
        {
            var fixture = CreateFixture(registered: new[] { "loc-A" });
            fixture.Table("Stage", "loc-A").Remove("stage.one");
            fixture.Table("UI", "loc-A")["ui.title"] = Entry("ui.title", "", false);
            fixture.Table("UI", "loc-B").Remove("ui.title");

            var actual = Validate(fixture).Diagnostics
                .Select(diagnostic => $"{diagnostic.LocaleCode}|{diagnostic.Table}|{diagnostic.Key}|{diagnostic.Code}")
                .ToArray();

            Assert.That(actual, Is.EqualTo(new[]
            {
                "loc-A|Stage|stage.one|RequiredKeyMissing",
                "loc-A|UI|ui.title|ValueEmpty",
                "loc-B|UI|ui.title|RequiredKeyMissing",
                "loc-C|||ShipReadyLocaleNotRegistered",
            }));
        }

        private static LocalizationGovernanceReport Validate(Fixture fixture)
        {
            return new LocalizationStringGovernanceValidator().Validate(fixture.Build());
        }

        private static Fixture CreateFixture(IEnumerable<string> registered = null)
        {
            var catalog = new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("loc-A", "A", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("loc-B", "B", 20, LocaleLifecycle.Draft),
                    new LocaleCatalogEntry("loc-C", "C", 30, LocaleLifecycle.ShipReady),
                },
                "loc-A",
                "loc-A");
            var requirements = new List<LocalizationStringRequirement>
            {
                new("UI", "ui.title", false, PlaceholderSignature.Empty, "UI title"),
                new("UI", "ui.smart", true, Signature("0", "1"), "UI smart"),
                new("Stage", "stage.one", false, PlaceholderSignature.Empty, "stage presentation"),
            };
            var tables = new List<MutableTable>
            {
                new("UI", new[] { "ui.title", "ui.smart" }),
                new("Stage", new[] { "stage.one" }),
            };
            foreach (var locale in new[] { "loc-A", "loc-B", "loc-C" })
            {
                tables[0].LocaleTables.Add(locale, new Dictionary<string, LocalizationStringEntrySnapshot>
                {
                    ["ui.title"] = Entry("ui.title", $"Title {locale}", false),
                    ["ui.smart"] = Entry("ui.smart", "{0} / {1}", true, true, "0", "1"),
                });
                tables[1].LocaleTables.Add(locale, new Dictionary<string, LocalizationStringEntrySnapshot>
                {
                    ["stage.one"] = Entry("stage.one", $"Stage {locale}", false),
                });
            }

            return new Fixture(catalog, registered ?? new[] { "loc-A", "loc-C" }, requirements, tables);
        }

        private static LocalizationStringEntrySnapshot Entry(
            string key,
            string value,
            bool isSmart,
            bool analysisSucceeded = true,
            params string[] selectors)
        {
            return new LocalizationStringEntrySnapshot(
                key, value, isSmart, analysisSucceeded, Signature(selectors),
                analysisSucceeded ? "" : "synthetic parse failure");
        }

        private static PlaceholderSignature Signature(params string[] selectors)
        {
            return PlaceholderSignature.FromSelectors(selectors ?? Array.Empty<string>());
        }

        private static void AssertDiagnostic(
            LocalizationGovernanceReport report,
            LocalizationDiagnosticCode code,
            LocalizationDiagnosticSeverity severity,
            string locale,
            string table,
            string key)
        {
            var diagnostic = report.Diagnostics.Single(candidate =>
                candidate.Code == code && candidate.LocaleCode == locale &&
                candidate.Table == table && candidate.Key == key);
            Assert.That(diagnostic.Severity, Is.EqualTo(severity));
            Assert.That(diagnostic.LocaleCode, Is.EqualTo(locale));
            Assert.That(diagnostic.Table, Is.EqualTo(table));
            Assert.That(diagnostic.Key, Is.EqualTo(key));
        }

        private static string ToDiagnosticIdentity(LocalizationGovernanceDiagnostic diagnostic)
        {
            return $"{diagnostic.Code}|{diagnostic.Severity}|{diagnostic.LocaleCode}|" +
                   $"{diagnostic.Table}|{diagnostic.Key}|{diagnostic.Message}";
        }

        private sealed class Fixture
        {
            public Fixture(
                UiLocaleCatalog catalog,
                IEnumerable<string> registered,
                List<LocalizationStringRequirement> requirements,
                List<MutableTable> tables)
            {
                Catalog = catalog;
                Registered = registered.ToArray();
                Requirements = requirements;
                Tables = tables;
            }

            public UiLocaleCatalog Catalog { get; }
            public string[] Registered { get; }
            public List<LocalizationStringRequirement> Requirements { get; }
            public List<MutableTable> Tables { get; }

            public Dictionary<string, LocalizationStringEntrySnapshot> Table(string table, string locale)
            {
                return Tables.Single(candidate => candidate.Table == table).LocaleTables[locale];
            }

            public LocalizationStringGovernanceSnapshot Build()
            {
                return new LocalizationStringGovernanceSnapshot(
                    Catalog,
                    Registered,
                    Requirements,
                    Tables.Select(table => table.Build()).ToArray());
            }
        }

        private sealed class MutableTable
        {
            public MutableTable(string table, IEnumerable<string> sharedKeys)
            {
                Table = table;
                SharedKeys = new HashSet<string>(sharedKeys, StringComparer.Ordinal);
            }

            public string Table { get; }
            public HashSet<string> SharedKeys { get; }
            public Dictionary<string, Dictionary<string, LocalizationStringEntrySnapshot>> LocaleTables { get; } =
                new(StringComparer.Ordinal);

            public LocalizationStringTableSnapshot Build()
            {
                return new LocalizationStringTableSnapshot(
                    Table,
                    SharedKeys,
                    LocaleTables.Select(pair => new LocalizationLocaleTableSnapshot(pair.Key, pair.Value.Values)));
            }
        }
    }
}
