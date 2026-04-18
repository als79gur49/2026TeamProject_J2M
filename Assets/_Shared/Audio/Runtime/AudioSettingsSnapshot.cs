using System;

namespace Game.Shared.Audio
{
    public readonly struct AudioSettingsSnapshot : IEquatable<AudioSettingsSnapshot>
    {
        public static readonly AudioSettingsSnapshot Default = new(
            new AudioChannelState(1f, false),
            new AudioChannelState(1f, false),
            new AudioChannelState(1f, false),
            new AudioChannelState(1f, false),
            new AudioChannelState(1f, false),
            new AudioChannelState(1f, false));

        public AudioSettingsSnapshot(
            AudioChannelState master,
            AudioChannelState bgm,
            AudioChannelState sfx,
            AudioChannelState ui,
            AudioChannelState voice,
            AudioChannelState ambience)
        {
            Master = master;
            Bgm = bgm;
            Sfx = sfx;
            Ui = ui;
            Voice = voice;
            Ambience = ambience;
        }

        public AudioChannelState Master { get; }

        public AudioChannelState Bgm { get; }

        public AudioChannelState Sfx { get; }

        public AudioChannelState Ui { get; }

        public AudioChannelState Voice { get; }

        public AudioChannelState Ambience { get; }

        public AudioChannelState GetChannelState(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Master:
                    return Master;
                case AudioChannel.Bgm:
                    return Bgm;
                case AudioChannel.Sfx:
                    return Sfx;
                case AudioChannel.Ui:
                    return Ui;
                case AudioChannel.Voice:
                    return Voice;
                case AudioChannel.Ambience:
                    return Ambience;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        public AudioSettingsSnapshot WithChannelState(AudioChannel channel, AudioChannelState state)
        {
            switch (channel)
            {
                case AudioChannel.Master:
                    return new AudioSettingsSnapshot(state, Bgm, Sfx, Ui, Voice, Ambience);
                case AudioChannel.Bgm:
                    return new AudioSettingsSnapshot(Master, state, Sfx, Ui, Voice, Ambience);
                case AudioChannel.Sfx:
                    return new AudioSettingsSnapshot(Master, Bgm, state, Ui, Voice, Ambience);
                case AudioChannel.Ui:
                    return new AudioSettingsSnapshot(Master, Bgm, Sfx, state, Voice, Ambience);
                case AudioChannel.Voice:
                    return new AudioSettingsSnapshot(Master, Bgm, Sfx, Ui, state, Ambience);
                case AudioChannel.Ambience:
                    return new AudioSettingsSnapshot(Master, Bgm, Sfx, Ui, Voice, state);
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        public bool Equals(AudioSettingsSnapshot other)
        {
            return Master.Equals(other.Master) &&
                   Bgm.Equals(other.Bgm) &&
                   Sfx.Equals(other.Sfx) &&
                   Ui.Equals(other.Ui) &&
                   Voice.Equals(other.Voice) &&
                   Ambience.Equals(other.Ambience);
        }

        public override bool Equals(object obj)
        {
            return obj is AudioSettingsSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Master, Bgm, Sfx, Ui, Voice, Ambience);
        }
    }
}
