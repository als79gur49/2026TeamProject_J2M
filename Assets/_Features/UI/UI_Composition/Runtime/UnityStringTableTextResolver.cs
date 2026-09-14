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

        private readonly IUiLocalePreferenceStore _localePreferenceStore;
        private readonly UiLocaleCatalog _localeCatalog;
        private IReadOnlyList<string> _availableLocaleCodes = Array.Empty<string>();
        private IReadOnlyDictionary<string, Locale> _registeredLocaleByCode =
            new Dictionary<string, Locale>(StringComparer.Ordinal);
        private LocaleSelectionPolicy _selectionPolicy;
        private bool _isDisposed;
        private bool _suppressSelectedLocaleEvent;
        private string _currentLocaleCode = DefaultLocaleCode;

        private UnityStringTableTextResolver(
            IUiLocalePreferenceStore localePreferenceStore,
            UiLocaleCatalog localeCatalog)
        {
            _localePreferenceStore = localePreferenceStore;
            _localeCatalog = localeCatalog ?? throw new ArgumentNullException(nameof(localeCatalog));
        }

        public string CurrentLocaleCode => _currentLocaleCode;

        public IReadOnlyList<string> AvailableLocaleCodes => _availableLocaleCodes;

        public event Action LocaleChanged;

        public static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
            out UnityStringTableTextResolver resolver,
            out string failureReason)
        {
            return TryCreateSettingsDefault(
                localePreferenceStore,
                UiLocaleCatalog.CreateProduction(),
                out resolver,
                out failureReason);
        }

        internal static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
            UiLocaleCatalog localeCatalog,
            out UnityStringTableTextResolver resolver,
            out string failureReason)
        {
            resolver = null;
            failureReason = string.Empty;
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

            var candidate = new UnityStringTableTextResolver(localePreferenceStore, localeCatalog);
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
            if (TryResolveFromUnity(CurrentLocaleCode, descriptor, out var value) ||
                TryResolveFromUnity(DefaultLocaleCode, descriptor, out value))
            {
                return value;
            }

            return $"[{descriptor.Table}:{descriptor.Key}]";
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

            var locale = GetRegisteredLocaleOrThrow(result.CanonicalCode);
            SetSelectedLocale(locale, result.CanonicalCode, preload: true);
            _localePreferenceStore?.Save(result.CanonicalCode);
            LocaleChanged?.Invoke();
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

                if (!TryGetLocale(DefaultLocaleCode, out _) ||
                    !TryGetLocale(KoreanLocaleCode, out _))
                {
                    failureReason = "Required en-US and ko-KR Locale assets are not available.";
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

                _availableLocaleCodes = Array.AsReadOnly(
                    _selectionPolicy.SelectableLocales
                        .Select(entry => entry.CanonicalCode)
                        .ToArray());

                string persistedLocaleCode = null;
                if (_localePreferenceStore != null &&
                    _localePreferenceStore.TryLoad(out var loadedLocaleCode))
                {
                    persistedLocaleCode = loadedLocaleCode;
                }

                var selectedLocaleCode = GetCanonicalUnitySelectionCandidate(
                    LocalizationSettings.SelectedLocale?.Identifier.Code);
                var initialLocaleCode = _selectionPolicy.ResolveInitialLocale(
                    persistedLocaleCode,
                    selectedLocaleCode);
                var initialLocale = GetRegisteredLocaleOrThrow(initialLocaleCode);

                SetSelectedLocale(initialLocale, initialLocaleCode, preload: true);
                LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;

                if (!TryResolveFromUnity(
                        DefaultLocaleCode,
                        SettingsStaticTextDescriptors.Title,
                        out _))
                {
                    failureReason = "UI String Table is not available or does not contain Settings title.";
                    return false;
                }

                if (!TryResolveFromUnity(
                        KoreanLocaleCode,
                        SettingsStaticTextDescriptors.Title,
                        out _))
                {
                    failureReason = "ko-KR UI String Table is not available or does not contain Settings title.";
                    return false;
                }

                if (!TryResolveFromUnity(
                        DefaultLocaleCode,
                        new LocalizedTextDescriptor("Stage", "stage.stage-0-1.display_name"),
                        out _))
                {
                    failureReason = "Stage String Table is not available or does not contain the canonical stage display name sample.";
                    return false;
                }

                if (!TryResolveFromUnity(
                        KoreanLocaleCode,
                        new LocalizedTextDescriptor("Stage", "stage.stage-0-1.display_name"),
                        out _))
                {
                    failureReason = "ko-KR Stage String Table is not available or does not contain the canonical stage display name sample.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                return false;
            }
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

            var approvedLocale = GetRegisteredLocaleOrThrow(result.CanonicalCode);
            SetSelectedLocale(approvedLocale, result.CanonicalCode, preload: true);
            LocaleChanged?.Invoke();
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
