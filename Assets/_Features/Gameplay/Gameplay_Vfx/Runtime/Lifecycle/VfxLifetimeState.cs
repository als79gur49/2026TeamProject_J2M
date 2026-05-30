using System;

namespace Game.Feature.Gameplay.Vfx
{
    public enum VfxLifetimeState
    {
        None = 0,
        Spawned = 1,
        Active = 2,
        StopEmitting = 3,
        Detached = 4,
        TailPlaying = 5,
        ReleasedToPool = 6,
        HardCleanup = 7,
        PresentationSuspended = 8,
    }

    [Flags]
    public enum VfxPresentationSuspendReason
    {
        None = 0,
        Visibility = 1 << 0,
        TopologyTransition = 1 << 1,
        GameplayPause = 1 << 2,
    }
}
