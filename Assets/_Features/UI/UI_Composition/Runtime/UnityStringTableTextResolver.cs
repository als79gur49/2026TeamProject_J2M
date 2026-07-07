using System;
using System.Collections.Generic;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Composition
{
    internal sealed class UnityStringTableTextResolver : ILocalizedTextResolver, IUiLocaleSelectionPort, IDisposable
    {
        internal const string DefaultLocaleCode = "en-US";
        internal const string KoreanLocaleCode = "ko-KR";

        private static readonly IReadOnlyList<string> SupportedLocaleCodes =
            Array.AsReadOnly(new[]
            {
                DefaultLocaleCode,
                KoreanLocaleCode,
            });

        private readonly IUiLocalePreferenceStore _localePreferenceStore;
        private bool _isDisposed;
        private bool _suppressSelectedLocaleEvent;
        private string _currentLocaleCode = DefaultLocaleCode;

        private UnityStringTableTextResolver(IUiLocalePreferenceStore localePreferenceStore)
        {
            _localePreferenceStore = localePreferenceStore;
        }

        public string CurrentLocaleCode => _currentLocaleCode;

        public IReadOnlyList<string> AvailableLocaleCodes => SupportedLocaleCodes;

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
            if (!IsSupportedLocaleCode(normalizedLocaleCode) ||
                !TryGetLocale(normalizedLocaleCode, out var locale))
            {
                return false;
            }

            if (string.Equals(_currentLocaleCode, normalizedLocaleCode, StringComparison.Ordinal))
            {
                return true;
            }

            SetSelectedLocale(locale, normalizedLocaleCode);
            _localePreferenceStore?.Save(normalizedLocaleCode);
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

                var initialLocaleCode = ResolveInitialLocaleCode(_localePreferenceStore);
                if (!TryGetLocale(initialLocaleCode, out var initialLocale))
                {
                    initialLocaleCode = DefaultLocaleCode;
                    initialLocale = defaultLocale;
                }

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

            value = entry.GetLocalizedString(descriptor.Arguments);
            return !string.IsNullOrEmpty(value);
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
            if (!IsSupportedLocaleCode(localeCode) ||
                string.Equals(_currentLocaleCode, localeCode, StringComparison.Ordinal))
            {
                return;
            }

            _currentLocaleCode = localeCode;
            PreloadTable(locale);
            LocaleChanged?.Invoke();
        }

        private static string ResolveInitialLocaleCode(IUiLocalePreferenceStore localePreferenceStore)
        {
            if (localePreferenceStore != null &&
                localePreferenceStore.TryLoad(out var persistedLocaleCode) &&
                IsSupportedLocaleCode(NormalizeLocaleCode(persistedLocaleCode)))
            {
                return NormalizeLocaleCode(persistedLocaleCode);
            }

            return DefaultLocaleCode;
        }

        private static string NormalizeLocaleCode(string localeCode)
        {
            return string.IsNullOrWhiteSpace(localeCode)
                ? DefaultLocaleCode
                : localeCode;
        }

        private static bool IsSupportedLocaleCode(string localeCode)
        {
            for (var i = 0; i < SupportedLocaleCodes.Count; i++)
            {
                if (string.Equals(SupportedLocaleCodes[i], localeCode, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
