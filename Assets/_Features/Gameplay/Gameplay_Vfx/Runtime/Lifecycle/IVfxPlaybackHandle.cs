namespace Game.Feature.Gameplay.Vfx
{
    public enum GameplayVfxStopMode
    {
        Default = 0,
        StopWithTail = 1,
        StopEmittingAndClear = 2,
        ReleaseImmediately = 3,
        TopologyTransitionHardClear = 4,
    }

    public interface IVfxPlaybackHandle
    {
        int HandleId { get; }

        GameplayVfxCueId CueId { get; }

        VfxPersistentKey PersistentKey { get; }

        bool IsPersistent { get; }

        VfxLifetimeState State { get; }

        GameplayVfxTopologyStopMode TopologyStopMode { get; }

        GameplayVfxTopologySpawnMode TopologySpawnMode { get; }

        void MarkSpawned();

        void MarkActive();

        void Stop(GameplayVfxStopMode mode);

        void StopEmitting();

        void Detach();

        void MarkTailPlaying();

        void Reanchor(in VfxResolvedAnchor anchor);

        void ReleaseToPool();

        void HardCleanup();
    }
}
