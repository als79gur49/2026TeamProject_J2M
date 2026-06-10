using Game.Shared.Audio;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class UiAudioUserSettingsPolicyTests
    {
        [Test]
        [Category("Extended")]
        public void SfxMute_MutesUiAudio()
        {
            var mixingService = CreateMixingService(
                master: new AudioChannelState(1f, false),
                sfx: new AudioChannelState(1f, true),
                ui: new AudioChannelState(1f, false));

            Assert.That(
                mixingService.ResolvePlaybackVolume(AudioChannel.Ui, 1f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void SfxVolume_ScalesUiAudio()
        {
            var mixingService = CreateMixingService(
                master: new AudioChannelState(1f, false),
                sfx: new AudioChannelState(0.25f, false),
                ui: new AudioChannelState(1f, false));

            Assert.That(
                mixingService.ResolvePlaybackVolume(AudioChannel.Ui, 1f),
                Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void MasterStillAffectsUiAudio()
        {
            var mixingService = CreateMixingService(
                master: new AudioChannelState(0.5f, false),
                sfx: new AudioChannelState(0.5f, false),
                ui: new AudioChannelState(1f, false));

            Assert.That(
                mixingService.ResolvePlaybackVolume(AudioChannel.Ui, 1f),
                Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void BgmDoesNotAffectUiAudio()
        {
            var mixingService = CreateMixingService(
                master: new AudioChannelState(1f, false),
                bgm: new AudioChannelState(0f, true),
                sfx: new AudioChannelState(1f, false),
                ui: new AudioChannelState(1f, false));

            Assert.That(
                mixingService.ResolvePlaybackVolume(AudioChannel.Ui, 1f),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void VoiceAndAmbience_DoNotFollowSfx()
        {
            var mixingService = CreateMixingService(
                master: new AudioChannelState(1f, false),
                sfx: new AudioChannelState(0f, true),
                voice: new AudioChannelState(0.5f, false),
                ambience: new AudioChannelState(0.75f, false));

            Assert.That(
                mixingService.ResolvePlaybackVolume(AudioChannel.Voice, 1f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                mixingService.ResolvePlaybackVolume(AudioChannel.Ambience, 1f),
                Is.EqualTo(0.75f).Within(0.0001f));
        }

        private static AudioMixingService CreateMixingService(
            AudioChannelState? master = null,
            AudioChannelState? bgm = null,
            AudioChannelState? sfx = null,
            AudioChannelState? ui = null,
            AudioChannelState? voice = null,
            AudioChannelState? ambience = null)
        {
            return new AudioMixingService(
                new SnapshotAudioSettingsPersistenceStore(
                    new AudioSettingsSnapshot(
                        master ?? new AudioChannelState(1f, false),
                        bgm ?? new AudioChannelState(1f, false),
                        sfx ?? new AudioChannelState(1f, false),
                        ui ?? new AudioChannelState(1f, false),
                        voice ?? new AudioChannelState(1f, false),
                        ambience ?? new AudioChannelState(1f, false))));
        }

        private sealed class SnapshotAudioSettingsPersistenceStore : IAudioSettingsPersistenceStore
        {
            private readonly AudioSettingsSnapshot snapshot;

            public SnapshotAudioSettingsPersistenceStore(AudioSettingsSnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public AudioSettingsSnapshot Load()
            {
                return snapshot;
            }

            public void Save(AudioSettingsSnapshot snapshot)
            {
            }
        }
    }
}
