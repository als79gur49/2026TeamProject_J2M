using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public sealed class GameplayVfxPlaybackHandle : IVfxPlaybackHandle
    {
        internal GameplayVfxPlaybackHandle(
            int handleId,
            in ResolvedVfxPlaybackCommand command,
            GameplayVfxPooledInstance instance,
            float startedAtSeconds,
            IGameplayVfxTimeProvider timeProvider,
            bool isLifetimeControllerManaged = false)
        {
            HandleId = handleId;
            CueId = command.CueId;
            PersistentKey = command.PersistentKey;
            IsPersistent = command.IsPersistent;
            Policy = command.Policy;
            Instance = instance;
            StartedAtSeconds = startedAtSeconds;
            this.timeProvider = timeProvider;
            IsLifetimeControllerManaged = isLifetimeControllerManaged;
        }

        private readonly IGameplayVfxTimeProvider timeProvider;

        public int HandleId { get; }

        public GameplayVfxCueId CueId { get; }

        public VfxPersistentKey PersistentKey { get; }

        public bool IsPersistent { get; }

        public VfxLifetimeState State { get; private set; }

        internal VfxBindingRuntimePolicy Policy { get; }

        internal GameplayVfxPooledInstance Instance { get; private set; }

        public Transform InstanceTransform => Instance?.Transform;

        internal float StartedAtSeconds { get; }

        internal float TailStartedAtSeconds { get; private set; }

        internal bool HasTailStarted { get; private set; }

        internal bool IsLifetimeControllerManaged { get; }

        internal bool IsTerminal =>
            State == VfxLifetimeState.ReleasedToPool ||
            State == VfxLifetimeState.HardCleanup;

        public void MarkSpawned()
        {
            if (!IsTerminal)
            {
                State = VfxLifetimeState.Spawned;
            }
        }

        public void MarkActive()
        {
            if (!IsTerminal)
            {
                State = VfxLifetimeState.Active;
            }
        }

        public void StopEmitting()
        {
            if (IsTerminal)
            {
                return;
            }

            Instance?.StopEmitting();
            State = VfxLifetimeState.StopEmitting;
        }

        public void Detach()
        {
            if (IsTerminal)
            {
                return;
            }

            Instance?.DetachToTailRoot();
            State = VfxLifetimeState.Detached;
        }

        public void MarkTailPlaying()
        {
            MarkTailPlaying(timeProvider?.TimeSeconds ?? 0f);
        }

        internal void MarkTailPlaying(float nowSeconds)
        {
            if (IsTerminal)
            {
                return;
            }

            State = VfxLifetimeState.TailPlaying;
            TailStartedAtSeconds = nowSeconds;
            HasTailStarted = true;
        }

        public void ReleaseToPool()
        {
            State = VfxLifetimeState.ReleasedToPool;
        }

        public void HardCleanup()
        {
            State = VfxLifetimeState.HardCleanup;
            Instance?.HardCleanup();
            DetachInstance();
        }

        internal void DetachInstance()
        {
            Instance = null;
        }
    }
}
