namespace Game.Feature.Gameplay.Vfx
{
    public interface IVfxPool
    {
        IVfxPlaybackHandle PlayTransient(in GameplayVfxRequest request, in VfxResolvedAnchor anchor);

        IVfxPlaybackHandle StartPersistent(in GameplayVfxRequest request, in VfxResolvedAnchor anchor);

        void Release(IVfxPlaybackHandle handle);

        void HardCleanupAll();
    }
}
