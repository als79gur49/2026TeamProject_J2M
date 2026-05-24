using System;
using Game.Feature.Flow.Audio;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class CinematicAudioFocusController : MonoBehaviour
    {
        [SerializeField] private AudioRuntimeInstaller _audioRuntimeInstaller;
        [SerializeField] private GlobalAudioFlowBootstrap _audioFlowBootstrap;

        private AudioSource _activeAudioSource;
        private IAudioSettingsService _audioSettingsService;
        private bool _isFocused;

        public bool IsFocused => _isFocused;

        public void BeginFocus(AudioSource cinematicAudioSource)
        {
            _activeAudioSource = cinematicAudioSource ?? throw new ArgumentNullException(nameof(cinematicAudioSource));
            ResolveAudioSettingsService();
            StopCurrentBgmIfAvailable();
            ApplyCurrentSettings();
            _isFocused = true;
        }

        public void EndFocus()
        {
            if (_activeAudioSource != null)
            {
                _activeAudioSource.Stop();
            }

            _activeAudioSource = null;
            _isFocused = false;
        }

        private void Update()
        {
            if (_isFocused)
            {
                ApplyCurrentSettings();
            }
        }

        private void ResolveAudioSettingsService()
        {
            if (_audioRuntimeInstaller == null)
            {
                _audioRuntimeInstaller = GetComponent<AudioRuntimeInstaller>();
            }

            if (_audioRuntimeInstaller == null)
            {
                _audioSettingsService = null;
                return;
            }

            _audioRuntimeInstaller.Install();
            _audioSettingsService = _audioRuntimeInstaller.AudioSettingsService;
        }

        private void StopCurrentBgmIfAvailable()
        {
            if (_audioFlowBootstrap == null)
            {
                _audioFlowBootstrap = GetComponent<GlobalAudioFlowBootstrap>();
            }

            if (_audioFlowBootstrap == null)
            {
                return;
            }

            _audioFlowBootstrap.GetCoordinatorOrThrow().StopCurrent();
        }

        private void ApplyCurrentSettings()
        {
            if (_activeAudioSource == null)
            {
                return;
            }

            var snapshot = _audioSettingsService != null
                ? _audioSettingsService.ReadSettings()
                : AudioSettingsSnapshot.Default;
            var master = snapshot.Master;
            _activeAudioSource.mute = master.IsMuted;
            _activeAudioSource.volume = master.EffectiveFactor;
        }
    }
}
