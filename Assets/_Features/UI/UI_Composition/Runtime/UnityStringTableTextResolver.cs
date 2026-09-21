using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Composition
{
    public sealed class UnityStringTableTextResolver : ILocalizedTextResolver, IUiLocaleSelectionPort, IDisposable
    {
        public const string DefaultLocaleCode = "en-US";
        public const string KoreanLocaleCode = "ko-KR";
        public const string MissingTranslationSentinel = "□";

        private readonly IUiLocalePreferenceStore _localePreferenceStore;
        private readonly IUiLocalePersistenceReporter _persistenceReporter;
        private readonly UiLocaleCatalog _localeCatalog;
        private readonly TryResolveExactLocale _tryResolveExactLocale;
        private readonly TryValidateStartupHealth _validateStartupHealth;
        private IReadOnlyList<LocaleOptionModel> _availableLocaleOptions = Array.Empty<LocaleOptionModel>();
        private IReadOnlyDictionary<string, Locale> _registeredLocaleByCode =
            new Dictionary<string, Locale>(StringComparer.Ordinal);
        private LocaleSelectionPolicy _selectionPolicy;
        private bool _isDisposed;
        private bool _suppressSelectedLocaleEvent;
        private string _currentLocaleCode = DefaultLocaleCode;

        private UnityStringTableTextResolver(
            IUiLocalePreferenceStore localePreferenceStore,
            IUiLocalePersistenceReporter persistenceReporter,
            UiLocaleCatalog localeCatalog,
            TryResolveExactLocale tryResolveExactLocale,
            TryValidateStartupHealth validateStartupHealth)
        {
            _localePreferenceStore = localePreferenceStore ??
                throw new ArgumentNullException(nameof(localePreferenceStore));
            _persistenceReporter = persistenceReporter ??
                throw new ArgumentNullException(nameof(persistenceReporter));
            _localeCatalog = localeCatalog ?? throw new ArgumentNullException(nameof(localeCatalog));
            _tryResolveExactLocale = tryResolveExactLocale ?? TryResolveFromUnity;
            _validateStartupHealth = validateStartupHealth ?? ValidateStartupHealth;
        }

        internal delegate bool TryResolveExactLocale(
            string canonicalLocaleCode,
            LocalizedTextDescriptor descriptor,
            out string value);

        internal delegate bool TryValidateStartupHealth(
            string canonicalLocaleCode,
            out string failureReason);

        public string CurrentLocaleCode => _currentLocaleCode;

        public IReadOnlyList<LocaleOptionModel> AvailableLocaleOptions => _availableLocaleOptions;

        public event Action LocaleChanged;

        public static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
            out UnityStringTableTextResolver resolver,
            out string failureReason)
        {
            return TryCreateSettingsDefault(
                localePreferenceStore,
                new NoOpUiLocalePersistenceReporter(),
                UiLocaleCatalog.CreateProduction(),
                null,
                null,
                out resolver,
                out failureReason);
        }

        public static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
            IUiLocalePersistenceReporter persistenceReporter,
            out UnityStringTableTextResolver resolver,
            out string failureReason)
        {
            return TryCreateSettingsDefault(
                localePreferenceStore,
                persistenceReporter,
                UiLocaleCatalog.CreateProduction(),
                null,
                null,
                out resolver,
                out failureReason);
        }

        internal static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
            UiLocaleCatalog localeCatalog,
            out UnityStringTableTextResolver resolver,
            out string failureReason)
        {
            return TryCreateSettingsDefault(
                localePreferenceStore,
                new NoOpUiLocalePersistenceReporter(),
                localeCatalog,
                null,
                null,
                out resolver,
                out failureReason);
        }

        internal static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
            IUiLocalePersistenceReporter persistenceReporter,
            UiLocaleCatalog localeCatalog,
            TryValidateStartupHealth validateStartupHealth,
            out UnityStringTableTextResolver resolver,
            out string failureReason)
        {
            return TryCreateSettingsDefault(
                localePreferenceStore,
                persistenceReporter,
                localeCatalog,
                null,
                validateStartupHealth,
                out resolver,
                out failureReason);
        }

        internal static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
            IUiLocalePersistenceReporter persistenceReporter,
            UiLocaleCatalog localeCatalog,
            TryResolveExactLocale tryResolveExactLocale,
            TryValidateStartupHealth validateStartupHealth,
            out UnityStringTableTextResolver resolver,
            out string failureReason)
        {
            resolver = null;
            failureReason = string.Empty;
            if (localePreferenceStore == null)
            {
                throw new ArgumentNullException(nameof(localePreferenceStore));
            }

            if (persistenceReporter == null)
            {
                throw new ArgumentNullException(nameof(persistenceReporter));
            }

            if (!LocalizationSettings.HasSettings)
            {
                failureReason = "Unity Localization settings are not configured.";
                return false;
            }

            if (localeCatalog == null)
            {
                failureReason = "A UI locale catalog is required.";
                return false;
            }

            var candidate = new UnityStringTableTextResolver(
                localePreferenceStore,
                persistenceReporter,
                localeCatalog,
                tryResolveExactLocale,
                validateStartupHealth);
            if (!candidate.TryInitialize(out failureReason))
            {
                candidate.Dispose();
                return false;
            }

            resolver = candidate;
            return true;
        }

        public string Resolve(LocalizedTextDescriptor descriptor)
        {
            if (_tryResolveExactLocale(CurrentLocaleCode, descriptor, out var value))
            {
                return value;
            }

            return MissingTranslationSentinel;
        }

        public bool TrySetLocale(string localeCode)
        {
            var result = _selectionPolicy.EvaluateRequest(localeCode, _currentLocaleCode);
            if (result.Status == LocaleSelectionStatus.Rejected)
            {
                return false;
            }

            if (result.Status == LocaleSelectionStatus.NoOp)
            {
                return true;
            }

            ApplyApprovedChange(result.CanonicalCode, LocalePersistenceOperation.SettingsSelection);
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            }

            _isDisposed = true;
        }

        private bool TryInitialize(out string failureReason)
        {
            failureReason = string.Empty;
            try
            {
                var initialization = LocalizationSettings.InitializationOperation;
                if (!initialization.IsDone)
                {
                    initialization.WaitForCompletion();
                }

                if (initialization.Result == null)
                {
                    failureReason = "Unity Localization initialization did not complete successfully.";
                    return false;
                }

                _registeredLocaleByCode = ResolveRegisteredLocales();
                _selectionPolicy = new LocaleSelectionPolicy(
                    _localeCatalog,
                    _registeredLocaleByCode.Keys);
                if (!_registeredLocaleByCode.ContainsKey(_localeCatalog.DefaultLocaleCode))
                {
                    failureReason =
                        $"The default ShipReady locale '{_localeCatalog.DefaultLocaleCode}' is not registered " +
                        "in Unity Localization AvailableLocales.";
                    return false;
                }

                _availableLocaleOptions = LocaleOptionSnapshot.FromCatalogEntries(
                    _selectionPolicy.SelectableLocales);

                var readResult = _localePreferenceStore.Load() ??
                    throw new InvalidOperationException("Locale preference store returned a null read result.");
                var persistedLocaleCode = readResult.Status == LocalePreferenceReadStatus.Loaded
                    ? readResult.RawLocaleCode
                    : null;

                var selectedLocaleCode = GetCanonicalUnitySelectionCandidate(
                    LocalizationSettings.SelectedLocale?.Identifier.Code);
                var initialLocaleCode = _selectionPolicy.ResolveInitialLocale(
                    persistedLocaleCode,
                    selectedLocaleCode);
                var initialLocale = GetRegisteredLocaleOrThrow(initialLocaleCode);

                if (_availableLocaleOptions.Count > 0 &&
                    !_availableLocaleOptions.Any(option =>
                        string.Equals(option.CanonicalCode, initialLocaleCode, StringComparison.Ordinal)))
                {
                    failureReason =
                        $"Resolved current locale '{initialLocaleCode}' is absent from available locale options " +
                        $"[{string.Join(", ", _availableLocaleOptions.Select(option => option.CanonicalCode))}].";
                    return false;
                }

                SetSelectedLocale(initialLocale, initialLocaleCode, preload: true);
                if (!_validateStartupHealth(initialLocaleCode, out failureReason))
                {
                    return false;
                }

                if (readResult.Status == LocalePreferenceReadStatus.Failed)
                {
                    ReportFailure(
                        LocalePersistenceOperation.StartupRead,
                        initialLocaleCode,
                        readResult.FailureReason);
                }
                else if (readResult.Status == LocalePreferenceReadStatus.Loaded &&
                         !string.Equals(readResult.RawLocaleCode, initialLocaleCode, StringComparison.Ordinal))
                {
                    SaveAndReport(initialLocaleCode, LocalePersistenceOperation.StartupRewrite);
                }

                LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;

                return true;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                return false;
            }
        }

        private bool ValidateStartupHealth(string canonicalLocaleCode, out string failureReason)
        {
            failureReason = string.Empty;
            if (!_tryResolveExactLocale(
                    canonicalLocaleCode,
                    SettingsStaticTextDescriptors.Title,
                    out _))
            {
                failureReason = CreateStartupHealthFailureReason(
                    canonicalLocaleCode,
                    SettingsStaticTextDescriptors.Title);
                return false;
            }

            var stageDisplayName = new LocalizedTextDescriptor(
                "Stage",
                "stage.stage-0-1.display_name");
            if (!_tryResolveExactLocale(
                    canonicalLocaleCode,
                    stageDisplayName,
                    out _))
            {
                failureReason = CreateStartupHealthFailureReason(
                    canonicalLocaleCode,
                    stageDisplayName);
                return false;
            }

            return true;
        }

        private static string CreateStartupHealthFailureReason(
            string canonicalLocaleCode,
            LocalizedTextDescriptor descriptor)
        {
            return
                $"Selected locale '{canonicalLocaleCode}' String Table health failed for " +
                $"table '{descriptor.Table}', key '{descriptor.Key}': the exact entry is missing, null, or empty.";
        }

        private void ApplyApprovedChange(
            string canonicalLocaleCode,
            LocalePersistenceOperation operation)
        {
            var locale = GetRegisteredLocaleOrThrow(canonicalLocaleCode);
            SetSelectedLocale(locale, canonicalLocaleCode, preload: true);
            LocaleChanged?.Invoke();
            SaveAndReport(canonicalLocaleCode, operation);
        }

        private void SaveAndReport(
            string canonicalLocaleCode,
            LocalePersistenceOperation operation)
        {
            var writeResult = _localePreferenceStore.Save(canonicalLocaleCode) ??
                throw new InvalidOperationException("Locale preference store returned a null write result.");
            if (writeResult.Status == LocalePreferenceWriteStatus.Failed)
            {
                ReportFailure(operation, canonicalLocaleCode, writeResult.FailureReason);
            }
        }

        private void ReportFailure(
            LocalePersistenceOperation operation,
            string canonicalLocaleCode,
            string failureReason)
        {
            LocalePersistenceReportGuard.SafeReport(
                _persistenceReporter,
                new LocalePersistenceDiagnostic(operation, canonicalLocaleCode, failureReason));
        }

        private void SetSelectedLocale(Locale locale, string localeCode, bool preload)
        {
            _currentLocaleCode = localeCode;
            var selectedLocale = LocalizationSettings.SelectedLocale;
            if (!ReferenceEquals(selectedLocale, locale))
            {
                _suppressSelectedLocaleEvent = true;
                try
                {
                    LocalizationSettings.SelectedLocale = locale;
                }
                finally
                {
                    _suppressSelectedLocaleEvent = false;
                }
            }

            if (preload)
            {
                PreloadTable(locale);
            }
        }

        private bool TryResolveFromUnity(
            string localeCode,
            LocalizedTextDescriptor descriptor,
            out string value)
        {
            value = null;
            if (!TryGetLocale(localeCode, out var locale))
            {
                return false;
            }

            var table = PreloadTable(locale, descriptor.Table);
            var entry = table != null ? table.GetEntry(descriptor.Key) : null;
            if (entry == null)
            {
                return false;
            }

            value = ResolveEntry(entry, descriptor);
            return !string.IsNullOrEmpty(value);
        }

        private string ResolveEntry(StringTableEntry entry, LocalizedTextDescriptor descriptor)
        {
            if (descriptor.Arguments.Count == 0)
            {
                return entry.GetLocalizedString();
            }

            var arguments = new object[descriptor.Arguments.Count];
            for (var i = 0; i < descriptor.Arguments.Count; i++)
            {
                arguments[i] = descriptor.Arguments[i] is LocalizedTextDescriptor nestedDescriptor
                    ? Resolve(nestedDescriptor)
                    : descriptor.Arguments[i];
            }

            return entry.GetLocalizedString(arguments);
        }

        private static StringTable PreloadTable(Locale locale, string tableName = "UI")
        {
            if (locale == null || string.IsNullOrWhiteSpace(tableName))
            {
                return null;
            }

            return LocalizationSettings.StringDatabase.GetTable(tableName, locale);
        }

        private static bool TryGetLocale(string localeCode, out Locale locale)
        {
            locale = LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
            return locale != null;
        }

        private void HandleSelectedLocaleChanged(Locale locale)
        {
            if (_suppressSelectedLocaleEvent)
            {
                return;
            }

            var localeCode = GetCanonicalUnitySelectionCandidate(locale?.Identifier.Code);
            var result = _selectionPolicy.EvaluateRequest(localeCode, _currentLocaleCode);
            if (result.Status == LocaleSelectionStatus.Rejected)
            {
                RestoreLastApprovedSelectedLocale();
                return;
            }

            if (result.Status == LocaleSelectionStatus.NoOp)
            {
                return;
            }

            ApplyApprovedChange(result.CanonicalCode, LocalePersistenceOperation.ExternalSelection);
        }

        private void RestoreLastApprovedSelectedLocale()
        {
            var approvedLocale = GetRegisteredLocaleOrThrow(_currentLocaleCode);
            if (ReferenceEquals(LocalizationSettings.SelectedLocale, approvedLocale))
            {
                return;
            }

            _suppressSelectedLocaleEvent = true;
            try
            {
                LocalizationSettings.SelectedLocale = approvedLocale;
            }
            finally
            {
                _suppressSelectedLocaleEvent = false;
            }
        }

        private Locale GetRegisteredLocaleOrThrow(string canonicalCode)
        {
            if (_registeredLocaleByCode.TryGetValue(canonicalCode, out var locale))
            {
                return locale;
            }

            throw new InvalidOperationException(
                $"Approved locale '{canonicalCode}' does not resolve to a registered Unity Locale.");
        }

        private string GetCanonicalUnitySelectionCandidate(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode) ||
                !_registeredLocaleByCode.ContainsKey(localeCode) ||
                !_localeCatalog.TryGetEntry(localeCode, out var entry) ||
                !string.Equals(entry.CanonicalCode, localeCode, StringComparison.Ordinal))
            {
                return null;
            }

            return localeCode;
        }

        private static IReadOnlyDictionary<string, Locale> ResolveRegisteredLocales()
        {
            var localesByCode = new Dictionary<string, Locale>(StringComparer.Ordinal);
            var locales = LocalizationSettings.AvailableLocales?.Locales;
            if (locales == null)
            {
                return localesByCode;
            }

            for (var i = 0; i < locales.Count; i++)
            {
                var locale = locales[i];
                var localeCode = locale?.Identifier.Code;
                if (!string.IsNullOrWhiteSpace(localeCode) &&
                    !localesByCode.ContainsKey(localeCode))
                {
                    localesByCode.Add(localeCode, locale);
                }
            }

            return localesByCode;
        }
    }
}
