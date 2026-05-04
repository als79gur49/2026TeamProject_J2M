namespace Game.Feature.Gameplay.Vfx
{
    public interface IVfxPlaybackHandle
    {
        int HandleId { get; }

        GameplayVfxCueId CueId { get; }

        VfxPersistentKey PersistentKey { get; }

        bool IsPersistent { get; }

        VfxLifetimeState State { get; }

        void MarkSpawned();

        void MarkActive();

        void StopEmitting();

        void Detach();

        void MarkTailPlaying();

        void ReleaseToPool();

        void HardCleanup();
    }
}
