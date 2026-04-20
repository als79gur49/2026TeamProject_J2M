using System;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;

namespace Game.Feature.UI.Composition
{
    internal static class UIAudioChannelMapper
    {
        public static AudioChannel Map(AudioSettingsChannel channel)
        {
            switch (channel)
            {
                case AudioSettingsChannel.Main:
                    return AudioChannel.Master;
                case AudioSettingsChannel.Bgm:
                    return AudioChannel.Bgm;
                case AudioSettingsChannel.Sfx:
                    return AudioChannel.Sfx;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }
    }
}
