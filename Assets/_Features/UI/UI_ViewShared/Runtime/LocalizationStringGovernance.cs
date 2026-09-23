using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Game.Feature.UI.ViewShared
{
    public sealed class PlaceholderSignature : IEquatable<PlaceholderSignature>
    {
        private readonly IReadOnlyDictionary<string, int> _counts;

        private PlaceholderSignature(IDictionary<string, int> counts)
        {
            _counts = new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(counts, StringComparer.Ordinal));
        }

        public static PlaceholderSignature Empty { get; } =
            new PlaceholderSignature(new Dictionary<string, int>(StringComparer.Ordinal));

        public IReadOnlyDictionary<string, int> Counts => _counts;

        public int TotalCount => _counts.Values.Sum();

        public static PlaceholderSignature FromSelectors(IEnumerable<string> selectors)
        {
            if (selectors == null)
            {
                throw new ArgumentNullException(nameof(selectors));
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var selector in selectors)
            {
                if (string.IsNullOrWhiteSpace(selector))
                {
                    throw new ArgumentException("Placeholder selector identities cannot be empty.", nameof(selectors));
                }

                counts.TryGetValue(selector, out var count);
                counts[selector] = count + 1;
            }

            return counts.Count == 0
                ? Empty
                : new PlaceholderSignature(counts);
        }

        public bool Equals(PlaceholderSignature other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || _counts.Count != other._counts.Count)
            {
                return false;
            }

            return _counts.All(pair =>
                other._counts.TryGetValue(pair.Key, out var count) && count == pair.Value);
        }

        public override bool Equals(object obj) => Equals(obj as PlaceholderSignature);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var pair in _counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(pair.Key);
                    hash = (hash * 31) + pair.Value;
                }

                return hash;
            }
        }

        public override string ToString()
        {
            return string.Join(", ", _counts
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}x{pair.Value}"));
        }
    }

    public sealed class LocalizationStringRequirement
    {
        public LocalizationStringRequirement(
            string table,
            string key,
            bool isSmart,
            PlaceholderSignature placeholderSignature,
            string owner)
        {
            if (string.IsNullOrWhiteSpace(table))
            {
                throw new ArgumentException("A governed table is required.", nameof(table));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A governed key is required.", nameof(key));
            }

            Table = table;
            Key = key;
            IsSmart = isSmart;
            PlaceholderSignature = placeholderSignature ?? throw new ArgumentNullException(nameof(placeholderSignature));
            Owner = owner ?? string.Empty;
        }

        public string Table { get; }
        public string Key { get; }
        public bool IsSmart { get; }
        public PlaceholderSignature PlaceholderSignature { get; }
        public string Owner { get; }
    }

    public sealed class LocalizationStringEntrySnapshot
    {
        public LocalizationStringEntrySnapshot(
            string key,
            string value,
            bool isSmart,
            bool smartAnalysisSucceeded,
            PlaceholderSignature placeholderSignature,
            string smartAnalysisFailureReason)
        {
            Key = key ?? string.Empty;
            Value = value;
            IsSmart = isSmart;
            SmartAnalysisSucceeded = smartAnalysisSucceeded;
            PlaceholderSignature = placeholderSignature ?? PlaceholderSignature.Empty;
            SmartAnalysisFailureReason = smartAnalysisFailureReason ?? string.Empty;
        }

        public string Key { get; }
        public string Value { get; }
        public bool IsSmart { get; }
        public bool SmartAnalysisSucceeded { get; }
        public PlaceholderSignature PlaceholderSignature { get; }
        public string SmartAnalysisFailureReason { get; }
    }

    public sealed class LocalizationLocaleTableSnapshot
    {
        private readonly IReadOnlyDictionary<string, LocalizationStringEntrySnapshot> _entries;

        public LocalizationLocaleTableSnapshot(
            string localeCode,
            IEnumerable<LocalizationStringEntrySnapshot> entries)
        {
            LocaleCode = localeCode ?? string.Empty;
            _entries = new ReadOnlyDictionary<string, LocalizationStringEntrySnapshot>(
                (entries ?? throw new ArgumentNullException(nameof(entries)))
                .ToDictionary(entry => entry.Key, StringComparer.Ordinal));
        }

        public string LocaleCode { get; }
        public IReadOnlyDictionary<string, LocalizationStringEntrySnapshot> Entries => _entries;
    }

    public sealed class LocalizationStringTableSnapshot
    {
        private readonly IReadOnlyCollection<string> _sharedKeys;
        private readonly IReadOnlyDictionary<string, LocalizationLocaleTableSnapshot> _localeTables;

        public LocalizationStringTableSnapshot(
            string table,
            IEnumerable<string> sharedKeys,
            IEnumerable<LocalizationLocaleTableSnapshot> localeTables)
        {
            Table = table ?? string.Empty;
            _sharedKeys = Array.AsReadOnly((sharedKeys ?? throw new ArgumentNullException(nameof(sharedKeys)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray());
            _localeTables = new ReadOnlyDictionary<string, LocalizationLocaleTableSnapshot>(
                (localeTables ?? throw new ArgumentNullException(nameof(localeTables)))
                .ToDictionary(localeTable => localeTable.LocaleCode, StringComparer.Ordinal));
        }

        public string Table { get; }
        public IReadOnlyCollection<string> SharedKeys => _sharedKeys;
        public IReadOnlyDictionary<string, LocalizationLocaleTableSnapshot> LocaleTables => _localeTables;
    }

    public sealed class LocalizationStringGovernanceSnapshot
    {
        public LocalizationStringGovernanceSnapshot(
            UiLocaleCatalog catalog,
            IEnumerable<string> registeredLocaleCodes,
            IEnumerable<LocalizationStringRequirement> requirements,
            IEnumerable<LocalizationStringTableSnapshot> tables)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            RegisteredLocaleCodes = Array.AsReadOnly(
                (registeredLocaleCodes ?? throw new ArgumentNullException(nameof(registeredLocaleCodes)))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
            Requirements = Array.AsReadOnly(
                (requirements ?? throw new ArgumentNullException(nameof(requirements))).ToArray());
            Tables = Array.AsReadOnly(
                (tables ?? throw new ArgumentNullException(nameof(tables))).ToArray());
        }

        public UiLocaleCatalog Catalog { get; }
        public IReadOnlyList<string> RegisteredLocaleCodes { get; }
        public IReadOnlyList<LocalizationStringRequirement> Requirements { get; }
        public IReadOnlyList<LocalizationStringTableSnapshot> Tables { get; }
    }

    public enum LocalizationDiagnosticSeverity
    {
        NonBlocking = 0,
        Blocking = 1,
    }

    public enum LocalizationDiagnosticCode
    {
        ShipReadyLocaleNotRegistered,
        DraftLocaleRegistered,
        RegisteredLocaleNotInCatalog,
        RequiredTableMissing,
        RequiredKeyMissing,
        ValueNull,
        ValueEmpty,
        ValueWhitespace,
        SmartFlagMismatch,
        SmartFormatMalformed,
        PlaceholderMissing,
        PlaceholderAdded,
        PlaceholderSelectorMismatch,
        PlaceholderMultiplicityMismatch,
        GovernedKeyMissingFromSharedData,
        SharedDataOrphanKey,
        RequirementMetadataConflict,
    }

    public sealed class LocalizationGovernanceDiagnostic
    {
        public LocalizationGovernanceDiagnostic(
            LocalizationDiagnosticCode code,
            LocalizationDiagnosticSeverity severity,
            LocaleLifecycle? lifecycle,
            string localeCode,
            string table,
            string key,
            string message)
        {
            Code = code;
            Severity = severity;
            Lifecycle = lifecycle;
            LocaleCode = localeCode ?? string.Empty;
            Table = table ?? string.Empty;
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public LocalizationDiagnosticCode Code { get; }
        public LocalizationDiagnosticSeverity Severity { get; }
        public LocaleLifecycle? Lifecycle { get; }
        public string LocaleCode { get; }
        public string Table { get; }
        public string Key { get; }
        public string Message { get; }
    }

    public sealed class LocalizationGovernanceReport
    {
        public LocalizationGovernanceReport(IEnumerable<LocalizationGovernanceDiagnostic> diagnostics)
        {
            Diagnostics = Array.AsReadOnly((diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).ToArray());
        }

        public IReadOnlyList<LocalizationGovernanceDiagnostic> Diagnostics { get; }
        public bool HasBlockingFailures => Diagnostics.Any(diagnostic =>
            diagnostic.Severity == LocalizationDiagnosticSeverity.Blocking);
    }

    public sealed class LocalizationStringGovernanceValidator
    {
        public LocalizationGovernanceReport Validate(LocalizationStringGovernanceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var diagnostics = new List<LocalizationGovernanceDiagnostic>();
            var registered = new HashSet<string>(snapshot.RegisteredLocaleCodes, StringComparer.Ordinal);
            ValidateRegistration(snapshot.Catalog, registered, diagnostics);
            var normalized = NormalizeRequirements(snapshot.Requirements, diagnostics);
            var tables = snapshot.Tables.ToDictionary(table => table.Table, StringComparer.Ordinal);
            ValidateSharedData(normalized.Requirements, tables, diagnostics);

            foreach (var locale in snapshot.Catalog.AuthoringKnownLocales)
            {
                ValidateLocale(locale, normalized.Requirements, normalized.ConflictedKeys, tables, diagnostics);
            }

            var stableOrder = snapshot.Catalog.AuthoringKnownLocales
                .Select((entry, index) => new { entry.CanonicalCode, Index = index })
                .ToDictionary(pair => pair.CanonicalCode, pair => pair.Index, StringComparer.Ordinal);
            return new LocalizationGovernanceReport(diagnostics
                .OrderBy(diagnostic => stableOrder.TryGetValue(diagnostic.LocaleCode, out var index) ? index : int.MaxValue)
                .ThenBy(diagnostic => stableOrder.ContainsKey(diagnostic.LocaleCode) ? "" : diagnostic.LocaleCode,
                    StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Table, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Key, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code)
                .ToArray());
        }

        private static void ValidateRegistration(
            UiLocaleCatalog catalog,
            ISet<string> registered,
            ICollection<LocalizationGovernanceDiagnostic> diagnostics)
        {
            var canonicalCodes = new HashSet<string>(
                catalog.AuthoringKnownLocales.Select(locale => locale.CanonicalCode),
                StringComparer.Ordinal);
            foreach (var localeCode in registered
                         .Where(localeCode => !canonicalCodes.Contains(localeCode))
                         .OrderBy(localeCode => localeCode, StringComparer.Ordinal))
            {
                diagnostics.Add(new LocalizationGovernanceDiagnostic(
                    LocalizationDiagnosticCode.RegisteredLocaleNotInCatalog,
                    LocalizationDiagnosticSeverity.Blocking,
                    null,
                    localeCode,
                    "",
                    "",
                    "Registered production locale has no canonical catalog row."));
            }

            foreach (var locale in catalog.AuthoringKnownLocales)
            {
                if (locale.Lifecycle == LocaleLifecycle.ShipReady && !registered.Contains(locale.CanonicalCode))
                {
                    Add(diagnostics, LocalizationDiagnosticCode.ShipReadyLocaleNotRegistered,
                        LocalizationDiagnosticSeverity.Blocking, locale, "", "",
                        "ShipReady locale is absent from the production registration snapshot.");
                }
                else if (locale.Lifecycle == LocaleLifecycle.Draft && registered.Contains(locale.CanonicalCode))
                {
                    Add(diagnostics, LocalizationDiagnosticCode.DraftLocaleRegistered,
                        LocalizationDiagnosticSeverity.Blocking, locale, "", "",
                        "Draft locale is present in the production registration snapshot.");
                }
            }
        }

        private static NormalizedRequirements NormalizeRequirements(
            IEnumerable<LocalizationStringRequirement> requirements,
            ICollection<LocalizationGovernanceDiagnostic> diagnostics)
        {
            var normalized = new List<LocalizationStringRequirement>();
            var conflictedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in requirements.GroupBy(
                         requirement => requirement.Table + "\u001f" + requirement.Key,
                         StringComparer.Ordinal))
            {
                var ordered = group
                    .OrderBy(candidate => candidate.IsSmart)
                    .ThenBy(candidate => candidate.PlaceholderSignature.ToString(), StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Owner, StringComparer.Ordinal)
                    .ToArray();
                var first = ordered[0];
                if (ordered.Any(candidate =>
                        candidate.IsSmart != first.IsSmart ||
                        !candidate.PlaceholderSignature.Equals(first.PlaceholderSignature)))
                {
                    conflictedKeys.Add(group.Key);
                    diagnostics.Add(new LocalizationGovernanceDiagnostic(
                        LocalizationDiagnosticCode.RequirementMetadataConflict,
                        LocalizationDiagnosticSeverity.Blocking,
                        null,
                        "",
                        first.Table,
                        first.Key,
                        $"Governed requirement metadata conflicts across owners: {string.Join(", ", ordered.Select(item => item.Owner))}."));
                }

                normalized.Add(first);
            }

            return new NormalizedRequirements(normalized, conflictedKeys);
        }

        private static void ValidateSharedData(
            IEnumerable<LocalizationStringRequirement> requirements,
            IReadOnlyDictionary<string, LocalizationStringTableSnapshot> tables,
            ICollection<LocalizationGovernanceDiagnostic> diagnostics)
        {
            foreach (var requirementGroup in requirements.GroupBy(requirement => requirement.Table, StringComparer.Ordinal))
            {
                var governedKeys = new HashSet<string>(requirementGroup.Select(item => item.Key), StringComparer.Ordinal);
                var sharedKeys = tables.TryGetValue(requirementGroup.Key, out var table)
                    ? new HashSet<string>(table.SharedKeys, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                foreach (var key in governedKeys.Except(sharedKeys).OrderBy(key => key, StringComparer.Ordinal))
                {
                    diagnostics.Add(Global(LocalizationDiagnosticCode.GovernedKeyMissingFromSharedData,
                        requirementGroup.Key, key, "Governed key is absent from Shared Data."));
                }

                foreach (var key in sharedKeys.Except(governedKeys).OrderBy(key => key, StringComparer.Ordinal))
                {
                    diagnostics.Add(Global(LocalizationDiagnosticCode.SharedDataOrphanKey,
                        requirementGroup.Key, key, "Shared Data key has no governed descriptor requirement."));
                }
            }
        }

        private static void ValidateLocale(
            LocaleCatalogEntry locale,
            IEnumerable<LocalizationStringRequirement> requirements,
            ISet<string> conflictedKeys,
            IReadOnlyDictionary<string, LocalizationStringTableSnapshot> tables,
            ICollection<LocalizationGovernanceDiagnostic> diagnostics)
        {
            var severity = locale.Lifecycle == LocaleLifecycle.ShipReady
                ? LocalizationDiagnosticSeverity.Blocking
                : LocalizationDiagnosticSeverity.NonBlocking;
            foreach (var tableGroup in requirements.GroupBy(requirement => requirement.Table, StringComparer.Ordinal))
            {
                if (!tables.TryGetValue(tableGroup.Key, out var table) ||
                    !table.LocaleTables.TryGetValue(locale.CanonicalCode, out var localeTable))
                {
                    Add(diagnostics, LocalizationDiagnosticCode.RequiredTableMissing, severity,
                        locale, tableGroup.Key, "", "Required locale table is missing.");
                    continue;
                }

                foreach (var requirement in tableGroup)
                {
                    if (conflictedKeys.Contains(RequirementIdentity(requirement)))
                    {
                        continue;
                    }

                    if (!localeTable.Entries.TryGetValue(requirement.Key, out var entry))
                    {
                        Add(diagnostics, LocalizationDiagnosticCode.RequiredKeyMissing, severity,
                            locale, requirement.Table, requirement.Key, "Required localized key is missing.");
                        continue;
                    }

                    ValidateEntry(locale, requirement, entry, severity, diagnostics);
                }
            }
        }

        private static void ValidateEntry(
            LocaleCatalogEntry locale,
            LocalizationStringRequirement requirement,
            LocalizationStringEntrySnapshot entry,
            LocalizationDiagnosticSeverity severity,
            ICollection<LocalizationGovernanceDiagnostic> diagnostics)
        {
            if (entry.Value == null)
            {
                Add(diagnostics, LocalizationDiagnosticCode.ValueNull, severity, locale,
                    requirement.Table, requirement.Key, "Localized value is null.");
                return;
            }

            if (entry.Value.Length == 0)
            {
                Add(diagnostics, LocalizationDiagnosticCode.ValueEmpty, severity, locale,
                    requirement.Table, requirement.Key, "Localized value is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                Add(diagnostics, LocalizationDiagnosticCode.ValueWhitespace, severity, locale,
                    requirement.Table, requirement.Key, "Localized value contains only whitespace.");
                return;
            }

            if (entry.IsSmart != requirement.IsSmart)
            {
                Add(diagnostics, LocalizationDiagnosticCode.SmartFlagMismatch, severity, locale,
                    requirement.Table, requirement.Key,
                    $"Smart flag is {entry.IsSmart} but governed metadata requires {requirement.IsSmart}.");
                return;
            }

            if (!requirement.IsSmart)
            {
                return;
            }

            if (!entry.SmartAnalysisSucceeded)
            {
                Add(diagnostics, LocalizationDiagnosticCode.SmartFormatMalformed, severity, locale,
                    requirement.Table, requirement.Key,
                    $"SmartFormat analysis failed: {entry.SmartAnalysisFailureReason}");
                return;
            }

            var mismatch = ClassifyPlaceholderMismatch(
                requirement.PlaceholderSignature,
                entry.PlaceholderSignature);
            if (mismatch.HasValue)
            {
                Add(diagnostics, mismatch.Value, severity, locale, requirement.Table, requirement.Key,
                    $"Placeholder signature expected [{requirement.PlaceholderSignature}] but was [{entry.PlaceholderSignature}].");
            }
        }

        private static LocalizationDiagnosticCode? ClassifyPlaceholderMismatch(
            PlaceholderSignature expected,
            PlaceholderSignature actual)
        {
            if (expected.Equals(actual))
            {
                return null;
            }

            var expectedKeys = new HashSet<string>(expected.Counts.Keys, StringComparer.Ordinal);
            var actualKeys = new HashSet<string>(actual.Counts.Keys, StringComparer.Ordinal);
            if (expectedKeys.SetEquals(actualKeys))
            {
                return LocalizationDiagnosticCode.PlaceholderMultiplicityMismatch;
            }

            if (actualKeys.IsSubsetOf(expectedKeys) && actual.TotalCount < expected.TotalCount)
            {
                return LocalizationDiagnosticCode.PlaceholderMissing;
            }

            if (expectedKeys.IsSubsetOf(actualKeys) && actual.TotalCount > expected.TotalCount)
            {
                return LocalizationDiagnosticCode.PlaceholderAdded;
            }

            return LocalizationDiagnosticCode.PlaceholderSelectorMismatch;
        }

        private static LocalizationGovernanceDiagnostic Global(
            LocalizationDiagnosticCode code,
            string table,
            string key,
            string message)
        {
            return new LocalizationGovernanceDiagnostic(
                code, LocalizationDiagnosticSeverity.Blocking, null, "", table, key, message);
        }

        private static string RequirementIdentity(LocalizationStringRequirement requirement)
        {
            return requirement.Table + "\u001f" + requirement.Key;
        }

        private sealed class NormalizedRequirements
        {
            public NormalizedRequirements(
                IReadOnlyList<LocalizationStringRequirement> requirements,
                ISet<string> conflictedKeys)
            {
                Requirements = requirements;
                ConflictedKeys = conflictedKeys;
            }

            public IReadOnlyList<LocalizationStringRequirement> Requirements { get; }
            public ISet<string> ConflictedKeys { get; }
        }

        private static void Add(
            ICollection<LocalizationGovernanceDiagnostic> diagnostics,
            LocalizationDiagnosticCode code,
            LocalizationDiagnosticSeverity severity,
            LocaleCatalogEntry locale,
            string table,
            string key,
            string message)
        {
            diagnostics.Add(new LocalizationGovernanceDiagnostic(
                code, severity, locale.Lifecycle, locale.CanonicalCode, table, key, message));
        }
    }
}
