using System;

namespace Game.Shared.Audio
{
    [Flags]
    public enum AudioPauseReason
    {
        None = 0,
        GameplayPause = 1 << 0,
    }
}
