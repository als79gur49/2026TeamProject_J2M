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
            TopologyStopMode = command.Request.TopologyStopMode;
            TopologySpawnMode = command.Request.TopologySpawnMode;
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

        public GameplayVfxTopologyStopMode TopologyStopMode { get; }

        public GameplayVfxTopologySpawnMode TopologySpawnMode { get; }

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

        public bool IsPresentationSuspended => State == VfxLifetimeState.PresentationSuspended;

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

        public void Stop(GameplayVfxStopMode mode)
        {
            switch (mode)
            {
                case GameplayVfxStopMode.Default:
                case GameplayVfxStopMode.StopWithTail:
                    StopEmitting();
                    MarkTailPlaying();
                    break;
                case GameplayVfxStopMode.StopEmittingAndClear:
                case GameplayVfxStopMode.ReleaseImmediately:
                case GameplayVfxStopMode.TopologyTransitionHardClear:
                    StopEmittingAndClear();
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(mode), mode, null);
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

        private void StopEmittingAndClear()
        {
            if (IsTerminal)
            {
                return;
            }

            Instance?.StopEmittingAndClear();
            State = VfxLifetimeState.ReleasedToPool;
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

        public void Reanchor(in VfxResolvedAnchor anchor)
        {
            if (IsTerminal ||
                State == VfxLifetimeState.StopEmitting ||
                State == VfxLifetimeState.Detached ||
                State == VfxLifetimeState.TailPlaying)
            {
                return;
            }

            Instance?.Reanchor(anchor);
        }

        public void SuspendPresentation()
        {
            if (IsTerminal ||
                State == VfxLifetimeState.StopEmitting ||
                State == VfxLifetimeState.Detached ||
                State == VfxLifetimeState.TailPlaying)
            {
                return;
            }

            Instance?.SuspendPresentation();
            State = VfxLifetimeState.PresentationSuspended;
        }

        public void ResumePresentation()
        {
            if (IsTerminal ||
                State != VfxLifetimeState.PresentationSuspended)
            {
                return;
            }

            Instance?.ResumePresentation();
            State = VfxLifetimeState.Active;
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
