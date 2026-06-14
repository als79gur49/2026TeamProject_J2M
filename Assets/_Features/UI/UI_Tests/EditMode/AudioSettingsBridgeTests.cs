using System;
using System.IO;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class AudioSettingsBridgeTests
    {
        [Test]
        public void AudioSettingsChannel_PublicSurface_RemainsVisibleScopeOnly()
        {
            Assert.That(Enum.GetNames(typeof(AudioSettingsChannel)), Is.EqualTo(new[] { "Main", "Bgm", "Sfx" }));
        }

        [Test]
        public void VisibleChannels_RemainMainBgmSfx()
        {
            Assert.That(Enum.GetNames(typeof(AudioSettingsChannel)), Is.EqualTo(new[] { "Main", "Bgm", "Sfx" }));
            Assert.That(UIAudioChannelMapper.Map(AudioSettingsChannel.Main), Is.EqualTo(AudioChannel.Master));
            Assert.That(UIAudioChannelMapper.Map(AudioSettingsChannel.Bgm), Is.EqualTo(AudioChannel.Bgm));
            Assert.That(UIAudioChannelMapper.Map(AudioSettingsChannel.Sfx), Is.EqualTo(AudioChannel.Sfx));
        }

        [Test]
        public void UiSettingsBridgeAssembly_RemainsInternalCompositionHelper()
        {
            var helperType = typeof(UiSettingsBridgeAssembly);

            Assert.That(helperType.Namespace, Is.EqualTo("Game.Feature.UI.Composition"));
            Assert.That(helperType.IsNotPublic, Is.True);
        }

        [TestCase(AudioSettingsChannel.Main, AudioChannel.Master)]
        [TestCase(AudioSettingsChannel.Bgm, AudioChannel.Bgm)]
        [TestCase(AudioSettingsChannel.Sfx, AudioChannel.Sfx)]
        public void UIAudioChannelMapper_MapsVisibleChannels_ToSingleSharedTruth(AudioSettingsChannel source, AudioChannel expected)
        {
            Assert.That(UIAudioChannelMapper.Map(source), Is.EqualTo(expected));
        }

        [Test]
        public void AudioSettingsPortAdapter_ReadAndWrite_UseBridgeOwnedMapping()
        {
            var service = new RecordingAudioSettingsService(
                new AudioSettingsSnapshot(
                    new AudioChannelState(0.11f, false),
                    new AudioChannelState(0.22f, true),
                    new AudioChannelState(0.33f, false),
                    new AudioChannelState(0.44f, false),
                    new AudioChannelState(0.55f, false),
                    new AudioChannelState(0.66f, false)));
            var adapter = new AudioSettingsPortAdapter(service);

            var snapshot = adapter.Read();
            adapter.SetVolume(AudioSettingsChannel.Main, 0.4f);
            adapter.SetMuted(AudioSettingsChannel.Sfx, true);

            Assert.That(snapshot.Main.Volume, Is.EqualTo(0.11f).Within(0.0001f));
            Assert.That(snapshot.Bgm.IsMuted, Is.True);
            Assert.That(snapshot.Sfx.Volume, Is.EqualTo(0.33f).Within(0.0001f));
            Assert.That(service.LastVolumeChannel, Is.EqualTo(AudioChannel.Master));
            Assert.That(service.LastMutedChannel, Is.EqualTo(AudioChannel.Sfx));
        }

        [Test]
        public void AudioSettingsPortAdapter_Source_UsesCentralMapper_InsteadOfInlineSharedChannelLiterals()
        {
            var sourcePath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Features/UI/UI_Composition/Runtime/AudioSettingsPortAdapter.cs");
            var source = File.ReadAllText(sourcePath);

            StringAssert.Contains("UIAudioChannelMapper.Map(channel)", source);
            Assert.That(source, Does.Not.Contain("AudioChannel.Master"));
            Assert.That(source, Does.Not.Contain("AudioChannel.Bgm"));
            Assert.That(source, Does.Not.Contain("AudioChannel.Sfx"));
        }

        private sealed class RecordingAudioSettingsService : IAudioSettingsService
        {
            private readonly AudioSettingsSnapshot snapshot;

            public RecordingAudioSettingsService(AudioSettingsSnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public AudioChannel? LastVolumeChannel { get; private set; }

            public AudioChannel? LastMutedChannel { get; private set; }

            public AudioSettingsSnapshot ReadSettings()
            {
                return snapshot;
            }

            public void SetChannelVolume(AudioChannel channel, float volume)
            {
                LastVolumeChannel = channel;
            }

            public void SetChannelMuted(AudioChannel channel, bool isMuted)
            {
                LastMutedChannel = channel;
            }

            public void FlushSettings()
            {
            }
        }
    }
}
