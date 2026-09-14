using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Feature.UI.ViewShared
{
    public enum LocaleLifecycle
    {
        Draft = 0,
        ShipReady = 1,
    }

    public sealed class LocaleCatalogEntry
    {
        private readonly IReadOnlyList<string> _legacyAliases;

        public LocaleCatalogEntry(
            string canonicalCode,
            string displayName,
            int stableOrder,
            LocaleLifecycle lifecycle,
            IEnumerable<string> legacyAliases = null)
        {
            if (string.IsNullOrWhiteSpace(canonicalCode))
            {
                throw new ArgumentException("A canonical locale code is required.", nameof(canonicalCode));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A locale display name is required.", nameof(displayName));
            }

            if (!Enum.IsDefined(typeof(LocaleLifecycle), lifecycle))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycle));
            }

            CanonicalCode = canonicalCode;
            DisplayName = displayName;
            StableOrder = stableOrder;
            Lifecycle = lifecycle;
            _legacyAliases = CopyAliases(legacyAliases);
        }

        public string CanonicalCode { get; }

        public string DisplayName { get; }

        public int StableOrder { get; }

        public LocaleLifecycle Lifecycle { get; }

        public IReadOnlyList<string> LegacyAliases => _legacyAliases;

        private static IReadOnlyList<string> CopyAliases(IEnumerable<string> aliases)
        {
            if (aliases == null)
            {
                return Array.Empty<string>();
            }

            var result = aliases.ToArray();
            for (var i = 0; i < result.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(result[i]))
                {
                    throw new ArgumentException("Locale aliases cannot be empty.", nameof(aliases));
                }
            }

            return Array.AsReadOnly(result);
        }
    }

    public sealed class UiLocaleCatalog
    {
        private readonly IReadOnlyList<LocaleCatalogEntry> _authoringKnownLocales;
        private readonly IReadOnlyList<LocaleCatalogEntry> _shipReadyLocales;
        private readonly IReadOnlyDictionary<string, LocaleCatalogEntry> _canonicalEntries;
        private readonly IReadOnlyDictionary<string, string> _canonicalCodeByIdentity;

        public UiLocaleCatalog(
            IEnumerable<LocaleCatalogEntry> entries,
            string defaultLocaleCode,
            string emergencyFallbackLocaleCode)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var orderedEntries = entries
                .OrderBy(entry => entry?.StableOrder ?? int.MaxValue)
                .ToArray();
            if (orderedEntries.Length == 0)
            {
                throw new ArgumentException("At least one locale catalog entry is required.", nameof(entries));
            }

            var canonicalEntries = new Dictionary<string, LocaleCatalogEntry>(StringComparer.Ordinal);
            var canonicalCodeByIdentity = new Dictionary<string, string>(StringComparer.Ordinal);
            var stableOrders = new HashSet<int>();
            foreach (var entry in orderedEntries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("Locale catalog entries cannot be null.", nameof(entries));
                }

                if (!stableOrders.Add(entry.StableOrder))
                {
                    throw new ArgumentException($"Duplicate locale stable order '{entry.StableOrder}'.", nameof(entries));
                }

                AddIdentity(canonicalCodeByIdentity, entry.CanonicalCode, entry.CanonicalCode, entries);
                canonicalEntries.Add(entry.CanonicalCode, entry);
                foreach (var alias in entry.LegacyAliases)
                {
                    AddIdentity(canonicalCodeByIdentity, alias, entry.CanonicalCode, entries);
                }
            }

            if (!TryGetShipReadyEntry(defaultLocaleCode, canonicalEntries, canonicalCodeByIdentity, out var defaultEntry))
            {
                throw new ArgumentException("The default locale must resolve to a ShipReady catalog entry.", nameof(defaultLocaleCode));
            }

            if (!TryGetShipReadyEntry(emergencyFallbackLocaleCode, canonicalEntries, canonicalCodeByIdentity, out var fallbackEntry))
            {
                throw new ArgumentException("The emergency fallback locale must resolve to a ShipReady catalog entry.", nameof(emergencyFallbackLocaleCode));
            }

            _authoringKnownLocales = Array.AsReadOnly(orderedEntries);
            _shipReadyLocales = Array.AsReadOnly(
                orderedEntries.Where(entry => entry.Lifecycle == LocaleLifecycle.ShipReady).ToArray());
            _canonicalEntries = canonicalEntries;
            _canonicalCodeByIdentity = canonicalCodeByIdentity;
            DefaultLocaleCode = defaultEntry.CanonicalCode;
            EmergencyFallbackLocaleCode = fallbackEntry.CanonicalCode;
        }

        public string DefaultLocaleCode { get; }

        public string EmergencyFallbackLocaleCode { get; }

        public IReadOnlyList<LocaleCatalogEntry> AuthoringKnownLocales => _authoringKnownLocales;

        public IReadOnlyList<LocaleCatalogEntry> ShipReadyLocales => _shipReadyLocales;

        public static UiLocaleCatalog CreateProduction()
        {
            return new UiLocaleCatalog(
                new[]
                {
                    new LocaleCatalogEntry("en-US", "English", 10, LocaleLifecycle.ShipReady),
                    new LocaleCatalogEntry("ko-KR", "한국어", 20, LocaleLifecycle.ShipReady),
                },
                "en-US",
                "en-US");
        }

        public bool TryCanonicalize(string localeIdentity, out string canonicalCode)
        {
            canonicalCode = null;
            if (string.IsNullOrWhiteSpace(localeIdentity) ||
                !_canonicalCodeByIdentity.TryGetValue(localeIdentity, out var resolvedCode))
            {
                return false;
            }

            canonicalCode = resolvedCode;
            return true;
        }

        public bool TryGetEntry(string localeIdentity, out LocaleCatalogEntry entry)
        {
            entry = null;
            return TryCanonicalize(localeIdentity, out var canonicalCode) &&
                   _canonicalEntries.TryGetValue(canonicalCode, out entry);
        }

        private static void AddIdentity(
            IDictionary<string, string> identities,
            string identity,
            string canonicalCode,
            IEnumerable<LocaleCatalogEntry> entries)
        {
            if (identities.ContainsKey(identity))
            {
                throw new ArgumentException($"Duplicate locale identity or alias '{identity}'.", nameof(entries));
            }

            identities.Add(identity, canonicalCode);
        }

        private static bool TryGetShipReadyEntry(
            string identity,
            IReadOnlyDictionary<string, LocaleCatalogEntry> canonicalEntries,
            IReadOnlyDictionary<string, string> canonicalCodeByIdentity,
            out LocaleCatalogEntry entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(identity) &&
                   canonicalCodeByIdentity.TryGetValue(identity, out var canonicalCode) &&
                   canonicalEntries.TryGetValue(canonicalCode, out entry) &&
                   entry.Lifecycle == LocaleLifecycle.ShipReady;
        }
    }

    public enum LocaleSelectionStatus
    {
        Rejected = 0,
        NoOp = 1,
        Change = 2,
    }

    public readonly struct LocaleSelectionResult
    {
        internal LocaleSelectionResult(LocaleSelectionStatus status, string canonicalCode)
        {
            Status = status;
            CanonicalCode = canonicalCode;
        }

        public LocaleSelectionStatus Status { get; }

        public string CanonicalCode { get; }
    }

    public sealed class LocaleSelectionPolicy
    {
        private readonly UiLocaleCatalog _catalog;
        private readonly HashSet<string> _registeredCodeSet;
        private readonly IReadOnlyList<string> _registeredLocaleCodes;
        private readonly IReadOnlyList<LocaleCatalogEntry> _selectableLocales;

        public LocaleSelectionPolicy(UiLocaleCatalog catalog, IEnumerable<string> registeredLocaleCodes)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (registeredLocaleCodes == null)
            {
                throw new ArgumentNullException(nameof(registeredLocaleCodes));
            }

            _registeredCodeSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var registeredLocaleCode in registeredLocaleCodes)
            {
                if (string.IsNullOrWhiteSpace(registeredLocaleCode))
                {
                    throw new ArgumentException(
                        "Registered locale identities cannot be empty.",
                        nameof(registeredLocaleCodes));
                }

                _registeredCodeSet.Add(registeredLocaleCode);
            }

            _registeredLocaleCodes = Array.AsReadOnly(
                _registeredCodeSet
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray());
            _selectableLocales = Array.AsReadOnly(
                catalog.ShipReadyLocales
                    .Where(entry => _registeredCodeSet.Contains(entry.CanonicalCode))
                    .ToArray());
        }

        public UiLocaleCatalog Catalog => _catalog;

        public IReadOnlyList<string> RegisteredLocaleCodes => _registeredLocaleCodes;

        public IReadOnlyList<LocaleCatalogEntry> SelectableLocales => _selectableLocales;

        public LocaleSelectionResult EvaluateRequest(string requestedLocaleIdentity, string currentLocaleIdentity)
        {
            if (!TryResolveSelectable(requestedLocaleIdentity, out var requestedCode))
            {
                return new LocaleSelectionResult(LocaleSelectionStatus.Rejected, null);
            }

            if (_catalog.TryCanonicalize(currentLocaleIdentity, out var currentCode) &&
                string.Equals(requestedCode, currentCode, StringComparison.Ordinal))
            {
                return new LocaleSelectionResult(LocaleSelectionStatus.NoOp, requestedCode);
            }

            return new LocaleSelectionResult(LocaleSelectionStatus.Change, requestedCode);
        }

        public string ResolveInitialLocale(string persistedLocaleIdentity, string selectedLocaleIdentity)
        {
            if (TryResolveSelectable(persistedLocaleIdentity, out var persistedCode))
            {
                return persistedCode;
            }

            if (TryResolveSelectable(selectedLocaleIdentity, out var selectedCode))
            {
                return selectedCode;
            }

            if (TryResolveSelectable(_catalog.DefaultLocaleCode, out var defaultCode))
            {
                return defaultCode;
            }

            throw new InvalidOperationException("The default ShipReady locale is not registered and selectable.");
        }

        public string GetAdjacentSelectableCode(string currentLocaleIdentity, int direction)
        {
            if (direction == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be positive or negative.");
            }

            if (_selectableLocales.Count == 0)
            {
                throw new InvalidOperationException("No selectable locale is registered.");
            }

            var currentCode = ResolveInitialLocale(currentLocaleIdentity, null);
            var currentIndex = -1;
            for (var i = 0; i < _selectableLocales.Count; i++)
            {
                if (string.Equals(_selectableLocales[i].CanonicalCode, currentCode, StringComparison.Ordinal))
                {
                    currentIndex = i;
                    break;
                }
            }

            var step = direction > 0 ? 1 : -1;
            var adjacentIndex = (currentIndex + step + _selectableLocales.Count) % _selectableLocales.Count;
            return _selectableLocales[adjacentIndex].CanonicalCode;
        }

        private bool TryResolveSelectable(string localeIdentity, out string canonicalCode)
        {
            canonicalCode = null;
            if (!_catalog.TryGetEntry(localeIdentity, out var entry) ||
                entry.Lifecycle != LocaleLifecycle.ShipReady ||
                !_registeredCodeSet.Contains(entry.CanonicalCode))
            {
                return false;
            }

            canonicalCode = entry.CanonicalCode;
            return true;
        }
    }
}
