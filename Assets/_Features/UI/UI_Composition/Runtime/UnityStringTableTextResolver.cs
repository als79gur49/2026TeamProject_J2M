using System;
using System.Collections.Generic;
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
        private IReadOnlyList<string> _availableLocaleCodes = Array.Empty<string>();
        private bool _isDisposed;
        private bool _suppressSelectedLocaleEvent;
        private string _currentLocaleCode = DefaultLocaleCode;

        private UnityStringTableTextResolver(IUiLocalePreferenceStore localePreferenceStore)
        {
            _localePreferenceStore = localePreferenceStore;
        }

        public string CurrentLocaleCode => _currentLocaleCode;

        public IReadOnlyList<string> AvailableLocaleCodes => _availableLocaleCodes;

        public event Action LocaleChanged;

        public static bool TryCreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore,
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

            var candidate = new UnityStringTableTextResolver(localePreferenceStore);
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
            var normalizedLocaleCode = NormalizeLocaleCode(localeCode);
            if (!TryResolveAvailableLocale(
                    normalizedLocaleCode,
                    out var locale,
                    out var resolvedLocaleCode))
            {
                return false;
            }

            if (string.Equals(_currentLocaleCode, resolvedLocaleCode, StringComparison.Ordinal))
            {
                return true;
            }

            SetSelectedLocale(locale, resolvedLocaleCode);
            _localePreferenceStore?.Save(resolvedLocaleCode);
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

                if (!TryGetLocale(DefaultLocaleCode, out var defaultLocale) ||
                    !TryGetLocale(KoreanLocaleCode, out _))
                {
                    failureReason = "Required en-US and ko-KR Locale assets are not available.";
                    return false;
                }

                _availableLocaleCodes = ResolveAvailableLocaleCodes();
                var initialLocale = ResolveInitialLocale(
                    _localePreferenceStore,
                    defaultLocale,
                    out var initialLocaleCode);

                SetSelectedLocale(initialLocale, initialLocaleCode);
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

        private void SetSelectedLocale(Locale locale, string localeCode)
        {
            _currentLocaleCode = localeCode;
            var selectedLocale = LocalizationSettings.SelectedLocale;
            if (selectedLocale != null &&
                (ReferenceEquals(selectedLocale, locale) ||
                 string.Equals(
                     selectedLocale.Identifier.Code,
                     localeCode,
                     StringComparison.Ordinal)))
            {
                PreloadTable(selectedLocale);
                return;
            }

            _suppressSelectedLocaleEvent = true;
            try
            {
                LocalizationSettings.SelectedLocale = locale;
                PreloadTable(locale);
            }
            finally
            {
                _suppressSelectedLocaleEvent = false;
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

        private static string ResolveEntry(StringTableEntry entry, LocalizedTextDescriptor descriptor)
        {
            if (descriptor.Arguments.Count == 0)
            {
                return entry.GetLocalizedString();
            }

            var arguments = new object[descriptor.Arguments.Count];
            for (var i = 0; i < descriptor.Arguments.Count; i++)
            {
                arguments[i] = descriptor.Arguments[i];
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
            if (_suppressSelectedLocaleEvent || locale == null)
            {
                return;
            }

            var localeCode = locale.Identifier.Code;
            if (!TryResolveAvailableLocale(
                    localeCode,
                    out _,
                    out var resolvedLocaleCode) ||
                string.Equals(
                    _currentLocaleCode,
                    resolvedLocaleCode,
                    StringComparison.Ordinal))
            {
                return;
            }

            _currentLocaleCode = resolvedLocaleCode;
            PreloadTable(locale);
            LocaleChanged?.Invoke();
        }

        private static Locale ResolveInitialLocale(
            IUiLocalePreferenceStore localePreferenceStore,
            Locale defaultLocale,
            out string localeCode)
        {
            if (localePreferenceStore != null &&
                localePreferenceStore.TryLoad(out var persistedLocaleCode) &&
                !string.IsNullOrWhiteSpace(persistedLocaleCode) &&
                TryResolveAvailableLocale(
                    NormalizeLocaleCode(persistedLocaleCode),
                    out var persistedLocale,
                    out localeCode))
            {
                return persistedLocale;
            }

            var selectedLocale = LocalizationSettings.SelectedLocale;
            if (selectedLocale != null &&
                TryResolveAvailableLocale(
                    selectedLocale.Identifier.Code,
                    out _,
                    out localeCode))
            {
                return selectedLocale;
            }

            localeCode = DefaultLocaleCode;
            return defaultLocale;
        }

        private static string NormalizeLocaleCode(string localeCode)
        {
            return string.IsNullOrWhiteSpace(localeCode)
                ? DefaultLocaleCode
                : localeCode;
        }

        private static bool TryResolveAvailableLocale(
            string localeCode,
            out Locale locale,
            out string resolvedLocaleCode)
        {
            locale = null;
            resolvedLocaleCode = string.Empty;
            if (string.IsNullOrWhiteSpace(localeCode) ||
                !TryGetLocale(localeCode, out locale))
            {
                return false;
            }

            resolvedLocaleCode = locale.Identifier.Code;
            return string.Equals(
                resolvedLocaleCode,
                localeCode,
                StringComparison.Ordinal);
        }

        private static IReadOnlyList<string> ResolveAvailableLocaleCodes()
        {
            var codes = new List<string>();
            var locales = LocalizationSettings.AvailableLocales?.Locales;
            if (locales == null)
            {
                return codes.AsReadOnly();
            }

            for (var i = 0; i < locales.Count; i++)
            {
                var locale = locales[i];
                if (locale != null &&
                    TryResolveAvailableLocale(
                        locale.Identifier.Code,
                        out _,
                        out var resolvedLocaleCode))
                {
                    codes.Add(resolvedLocaleCode);
                }
            }

            return codes.AsReadOnly();
        }
    }
}
