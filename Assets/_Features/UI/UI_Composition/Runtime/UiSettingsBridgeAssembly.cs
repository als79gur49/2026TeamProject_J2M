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

        internal static PackageFreeLocalizedTextResolver CreatePersistentSettingsLocalizedTextResolver()
        {
            return PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                new PlayerPrefsUiLocalePreferenceStore());
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

        public bool TryLoad(out string localeCode)
        {
            localeCode = string.Empty;
            if (!PlayerPrefs.HasKey(_key))
            {
                return false;
            }

            localeCode = PlayerPrefs.GetString(_key, string.Empty);
            return !string.IsNullOrWhiteSpace(localeCode);
        }

        public void Save(string localeCode)
        {
            PlayerPrefs.SetString(_key, localeCode ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
