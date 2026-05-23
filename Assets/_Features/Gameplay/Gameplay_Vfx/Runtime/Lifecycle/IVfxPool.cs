namespace Game.Feature.Gameplay.Vfx
{
    public interface IVfxPool
    {
        IVfxPlaybackHandle PlayTransient(in ResolvedVfxPlaybackCommand command);

        IVfxPlaybackHandle StartPersistent(in ResolvedVfxPlaybackCommand command);

        void Release(IVfxPlaybackHandle handle);

        void HardClearActiveForTopologyTransition();

        void HardCleanupAll();

        void HardCleanupFamily(GameplayVfxFamily family);
    }
}
