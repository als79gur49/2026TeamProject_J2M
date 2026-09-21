using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.ViewShared;
using Game.Shared.Audio;
using Game.Shared.Display;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal static class UiSettingsBridgeAssembly
    {
        internal delegate bool TryCreateLocalizedTextResolver(
            IUiLocalePreferenceStore localePreferenceStore,
            out ILocalizedTextResolver resolver,
            out string failureReason);

        internal static IAudioSettingsPort CreateAudioSettingsPort(
            GameObject owner,
            string missingAudioInstallerMessage)
        {
            var audioRuntimeInstaller = GetRequiredAudioRuntimeInstaller(owner, missingAudioInstallerMessage);
            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioSettingsService == null)
            {
                throw new InvalidOperationException(missingAudioInstallerMessage);
            }

            return new AudioSettingsPortAdapter(audioRuntimeInstaller.AudioSettingsService);
        }

        internal static IUiAudioPort CreateUiAudioPort(
            GameObject owner,
            UiAudioCueMap cueMap,
            string missingAudioInstallerMessage,
            string missingUiAudioCueMapMessage)
        {
            if (cueMap == null)
            {
                throw new InvalidOperationException(missingUiAudioCueMapMessage);
            }

            var audioRuntimeInstaller = GetRequiredAudioRuntimeInstaller(owner, missingAudioInstallerMessage);
            audioRuntimeInstaller.Install();
            if (audioRuntimeInstaller.AudioService == null)
            {
                throw new InvalidOperationException(missingAudioInstallerMessage);
            }

            return new UiAudioPortAdapter(audioRuntimeInstaller.AudioService, cueMap);
        }

        internal static IDisplaySettingsPort CreateDisplaySettingsPort(
            GameObject owner,
            string missingDisplayInstallerMessage)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var displayRuntimeInstaller = owner.GetComponent<DisplayRuntimeInstaller>();
            if (displayRuntimeInstaller == null)
            {
                throw new InvalidOperationException(missingDisplayInstallerMessage);
            }

            displayRuntimeInstaller.Install();
            if (displayRuntimeInstaller.DisplaySettingsService == null)
            {
                throw new InvalidOperationException(missingDisplayInstallerMessage);
            }

            return new DisplaySettingsPortAdapter(displayRuntimeInstaller.DisplaySettingsService);
        }

        internal static ILocalizedTextResolver CreatePersistentSettingsLocalizedTextResolver()
        {
            return CreatePersistentSettingsLocalizedTextResolver(
                new PlayerPrefsUiLocalePreferenceStore(),
                new UnityLogUiLocalePersistenceReporter());
        }

        internal static ILocalizedTextResolver CreatePersistentSettingsLocalizedTextResolver(
            IUiLocalePreferenceStore localePreferenceStore)
        {
            return CreatePersistentSettingsLocalizedTextResolver(
                localePreferenceStore,
                new NoOpUiLocalePersistenceReporter());
        }

        internal static ILocalizedTextResolver CreatePersistentSettingsLocalizedTextResolver(
            IUiLocalePreferenceStore localePreferenceStore,
            IUiLocalePersistenceReporter persistenceReporter)
        {
            if (localePreferenceStore == null)
            {
                throw new ArgumentNullException(nameof(localePreferenceStore));
            }

            if (persistenceReporter == null)
            {
                throw new ArgumentNullException(nameof(persistenceReporter));
            }

            return UnityStringTableTextResolver.TryCreateSettingsDefault(
                    localePreferenceStore,
                    persistenceReporter,
                    out var resolver,
                    out var failureReason)
                ? resolver ?? throw new InvalidOperationException(
                    "Unity Localization production setup returned a null UI text resolver.")
                : throw new InvalidOperationException(
                    "Unity Localization production setup is required for UI text resolution. " +
                    $"Fix Localization Settings, required Locales, and UI/Stage String Tables. Detail: {failureReason}");
        }

        internal static ILocalizedTextResolver CreatePersistentSettingsLocalizedTextResolver(
            IUiLocalePreferenceStore localePreferenceStore,
            TryCreateLocalizedTextResolver tryCreateResolver)
        {
            if (localePreferenceStore == null)
            {
                throw new ArgumentNullException(nameof(localePreferenceStore));
            }
            if (tryCreateResolver == null)
            {
                throw new ArgumentNullException(nameof(tryCreateResolver));
            }

            return tryCreateResolver(
                    localePreferenceStore,
                    out var resolver,
                    out var failureReason)
                ? resolver ?? throw new InvalidOperationException(
                    "Unity Localization production setup returned a null UI text resolver.")
                : throw new InvalidOperationException(
                    "Unity Localization production setup is required for UI text resolution. " +
                    $"Fix Localization Settings, required Locales, and UI/Stage String Tables. Detail: {failureReason}");
        }

        private static bool TryCreateUnityStringTableTextResolver(
            IUiLocalePreferenceStore localePreferenceStore,
            out ILocalizedTextResolver resolver,
            out string failureReason)
        {
            var created = UnityStringTableTextResolver.TryCreateSettingsDefault(
                localePreferenceStore,
                out var unityResolver,
                out failureReason);
            resolver = unityResolver;
            return created;
        }

        internal static AudioRuntimeInstaller GetRequiredAudioRuntimeInstaller(
            GameObject owner,
            string missingAudioInstallerMessage)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var audioRuntimeInstaller = owner.GetComponent<AudioRuntimeInstaller>();
            if (audioRuntimeInstaller == null)
            {
                throw new InvalidOperationException(missingAudioInstallerMessage);
            }

            return audioRuntimeInstaller;
        }

        internal static AudioSettingsLifecycleRelay EnsureAudioSettingsLifecycleRelay(
            GameObject owner,
            IAudioSettingsPort audioSettingsPort)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var relay = owner.GetComponent<AudioSettingsLifecycleRelay>();
            if (relay == null)
            {
                relay = owner.AddComponent<AudioSettingsLifecycleRelay>();
            }

            relay.Initialize(audioSettingsPort);
            return relay;
        }
    }

    internal sealed class PlayerPrefsUiLocalePreferenceStore : IUiLocalePreferenceStore
    {
        internal const string DefaultKey = "ui.selected_locale";

        private readonly string _key;

        public PlayerPrefsUiLocalePreferenceStore(string key = DefaultKey)
        {
            _key = string.IsNullOrWhiteSpace(key)
                ? throw new ArgumentException("Preference key must be non-empty.", nameof(key))
                : key;
        }

        public LocalePreferenceReadResult Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(_key))
                {
                    return LocalePreferenceReadResult.Missing();
                }

                return LocalePreferenceReadResult.Loaded(PlayerPrefs.GetString(_key, string.Empty));
            }
            catch (Exception ex)
            {
                return LocalePreferenceReadResult.Failed(GetFailureReason(ex));
            }
        }

        public LocalePreferenceWriteResult Save(string canonicalLocaleCode)
        {
            if (string.IsNullOrWhiteSpace(canonicalLocaleCode))
            {
                throw new ArgumentException(
                    "Canonical locale code must be non-empty.",
                    nameof(canonicalLocaleCode));
            }

            try
            {
                PlayerPrefs.SetString(_key, canonicalLocaleCode);
                PlayerPrefs.Save();
                return LocalePreferenceWriteResult.Completed();
            }
            catch (Exception ex)
            {
                return LocalePreferenceWriteResult.Failed(GetFailureReason(ex));
            }
        }

        private static string GetFailureReason(Exception exception)
        {
            return string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().FullName
                : exception.Message;
        }
    }

    internal sealed class UnityLogUiLocalePersistenceReporter : IUiLocalePersistenceReporter
    {
        public void Report(LocalePersistenceDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            Debug.LogWarning(
                $"UI locale persistence {diagnostic.Operation} failed for " +
                $"'{diagnostic.CanonicalLocaleCode}': {diagnostic.FailureReason}");
        }
    }
}
