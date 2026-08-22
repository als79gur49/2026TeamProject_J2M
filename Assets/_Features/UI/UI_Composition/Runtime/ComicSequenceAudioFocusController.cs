using System;
using Game.Feature.Flow.Audio;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class ComicSequenceAudioFocusController : MonoBehaviour, IComicSequenceAudioFocusOwner
    {
        [SerializeField] private AudioRuntimeInstaller _audioRuntimeInstaller;
        [SerializeField] private GlobalAudioFlowBootstrap _audioFlowBootstrap;

        private AudioSource _activeAudioSource;
        private IAudioSettingsService _audioSettingsService;
        private BgmPlaybackSuppressionLease _bgmPlaybackSuppression;
        private bool _isFocused;
        private float _comicSequenceFadeGain = 1f;

        public bool IsFocused => _isFocused;

        internal float ComicSequenceFadeGain => _comicSequenceFadeGain;

        public void BeginFocus(AudioSource comicSequenceAudioSource)
        {
            _activeAudioSource = comicSequenceAudioSource ?? throw new ArgumentNullException(nameof(comicSequenceAudioSource));
            _comicSequenceFadeGain = 1f;
            ResolveAudioSettingsService();
            BeginBgmPlaybackSuppressionIfAvailable();
            ApplyCurrentSettings();
            _isFocused = true;
        }

        public void EndFocus()
        {
            EndFocus(ComicSequenceAudioFocusEndMode.RestoreCurrentBgm);
        }

        void IComicSequenceAudioFocusOwner.EndFocus(
            ComicSequenceAudioFocusEndMode endMode)
        {
            EndFocus(endMode);
        }

        private void EndFocus(ComicSequenceAudioFocusEndMode endMode)
        {
            var activeAudioSource = _activeAudioSource;
            var playbackSuppression = _bgmPlaybackSuppression;
            _activeAudioSource = null;
            _bgmPlaybackSuppression = null;
            _comicSequenceFadeGain = 1f;
            _isFocused = false;
            try
            {
                activeAudioSource?.Stop();
            }
            finally
            {
                if (endMode ==
                    ComicSequenceAudioFocusEndMode.KeepBgmStoppedForTransition)
                {
                    playbackSuppression?.ReleaseWithoutRestore();
                }
                else
                {
                    playbackSuppression?.Dispose();
                }
            }
        }

        internal void SetComicSequenceFadeGain(float gain)
        {
            _comicSequenceFadeGain = Mathf.Clamp01(gain);
            ApplyCurrentSettings();
        }

        internal static bool ResolvePlaybackMuted(AudioSettingsSnapshot snapshot)
        {
            return snapshot.Master.IsMuted || snapshot.Bgm.IsMuted;
        }

        internal static float ResolvePlaybackVolume(
            AudioSettingsSnapshot snapshot,
            float comicSequenceFadeGain)
        {
            return snapshot.Master.EffectiveFactor *
                   snapshot.Bgm.EffectiveFactor *
                   Mathf.Clamp01(comicSequenceFadeGain);
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

        private void BeginBgmPlaybackSuppressionIfAvailable()
        {
            if (_audioFlowBootstrap == null)
            {
                _audioFlowBootstrap = GetComponent<GlobalAudioFlowBootstrap>();
            }

            if (_audioFlowBootstrap == null)
            {
                return;
            }

            _bgmPlaybackSuppression = _audioFlowBootstrap
                .GetRequestRouterOrThrow()
                .BeginPlaybackSuppression();
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
            _activeAudioSource.mute = ResolvePlaybackMuted(snapshot);
            _activeAudioSource.volume = ResolvePlaybackVolume(
                snapshot,
                _comicSequenceFadeGain);
        }
    }
}
