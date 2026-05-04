namespace Game.Feature.Gameplay.Vfx
{
    public enum VfxPlaybackCommand
    {
        None = 0,
        PlayTransient = 1,
        StartPersistent = 2,
        Stop = 3,
        Release = 4,
        HardCleanup = 5,
    }
}
