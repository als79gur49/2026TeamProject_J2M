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
        private VfxLifetimeState state;
        private VfxPresentationSuspendReason suspendReasons;
        private float gameplayPauseSuspendedAtSeconds = -1f;

        public int HandleId { get; }

        public GameplayVfxCueId CueId { get; }

        public VfxPersistentKey PersistentKey { get; }

        public bool IsPersistent { get; }

        public VfxLifetimeState State =>
            suspendReasons != VfxPresentationSuspendReason.None && CanShowAsPresentationSuspended(state)
                ? VfxLifetimeState.PresentationSuspended
                : state;

        public GameplayVfxTopologyStopMode TopologyStopMode { get; }

        public GameplayVfxTopologySpawnMode TopologySpawnMode { get; }

        internal VfxBindingRuntimePolicy Policy { get; }

        internal GameplayVfxPooledInstance Instance { get; private set; }

        public Transform InstanceTransform => Instance?.Transform;

        internal float StartedAtSeconds { get; private set; }

        internal float TailStartedAtSeconds { get; private set; }

        internal bool HasTailStarted { get; private set; }

        internal bool IsLifetimeControllerManaged { get; }

        internal bool IsTerminal =>
            state == VfxLifetimeState.ReleasedToPool ||
            state == VfxLifetimeState.HardCleanup;

        public bool IsPresentationSuspended => State == VfxLifetimeState.PresentationSuspended;

        internal VfxPresentationSuspendReason SuspendReasons => suspendReasons;

        public void MarkSpawned()
        {
            if (!IsTerminal)
            {
                state = VfxLifetimeState.Spawned;
            }
        }

        public void MarkActive()
        {
            if (!IsTerminal)
            {
                state = VfxLifetimeState.Active;
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
            suspendReasons = VfxPresentationSuspendReason.None;
            gameplayPauseSuspendedAtSeconds = -1f;
            state = VfxLifetimeState.StopEmitting;
        }

        private void StopEmittingAndClear()
        {
            if (IsTerminal)
            {
                return;
            }

            Instance?.StopEmittingAndClear();
            suspendReasons = VfxPresentationSuspendReason.None;
            gameplayPauseSuspendedAtSeconds = -1f;
            state = VfxLifetimeState.ReleasedToPool;
        }

        public void Detach()
        {
            if (IsTerminal)
            {
                return;
            }

            Instance?.DetachToTailRoot();
            suspendReasons = VfxPresentationSuspendReason.None;
            gameplayPauseSuspendedAtSeconds = -1f;
            state = VfxLifetimeState.Detached;
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
            SuspendPresentation(VfxPresentationSuspendReason.Visibility);
        }

        public void SuspendPresentation(VfxPresentationSuspendReason reason)
        {
            if (IsTerminal ||
                state == VfxLifetimeState.StopEmitting ||
                state == VfxLifetimeState.Detached ||
                state == VfxLifetimeState.TailPlaying ||
                reason == VfxPresentationSuspendReason.None)
            {
                return;
            }

            if (reason == VfxPresentationSuspendReason.GameplayPause &&
                gameplayPauseSuspendedAtSeconds < 0f)
            {
                gameplayPauseSuspendedAtSeconds = timeProvider?.TimeSeconds ?? 0f;
            }

            suspendReasons |= reason;
            Instance?.SuspendPresentation(reason);
        }

        public void ResumePresentation()
        {
            ResumePresentation(VfxPresentationSuspendReason.Visibility);
        }

        public void ResumePresentation(VfxPresentationSuspendReason reason)
        {
            if (IsTerminal ||
                suspendReasons == VfxPresentationSuspendReason.None ||
                reason == VfxPresentationSuspendReason.None)
            {
                return;
            }

            suspendReasons &= ~reason;
            if (reason == VfxPresentationSuspendReason.GameplayPause)
            {
                gameplayPauseSuspendedAtSeconds = -1f;
            }

            Instance?.ResumePresentation(reason);
            if (suspendReasons == VfxPresentationSuspendReason.None)
            {
                state = VfxLifetimeState.Active;
            }
        }

        internal void MarkTailPlaying(float nowSeconds)
        {
            if (IsTerminal)
            {
                return;
            }

            suspendReasons = VfxPresentationSuspendReason.None;
            gameplayPauseSuspendedAtSeconds = -1f;
            state = VfxLifetimeState.TailPlaying;
            TailStartedAtSeconds = nowSeconds;
            HasTailStarted = true;
        }

        public void ReleaseToPool()
        {
            suspendReasons = VfxPresentationSuspendReason.None;
            gameplayPauseSuspendedAtSeconds = -1f;
            state = VfxLifetimeState.ReleasedToPool;
        }

        public void HardCleanup()
        {
            suspendReasons = VfxPresentationSuspendReason.None;
            gameplayPauseSuspendedAtSeconds = -1f;
            state = VfxLifetimeState.HardCleanup;
            Instance?.HardCleanup();
            DetachInstance();
        }

        internal void DetachInstance()
        {
            Instance = null;
        }

        internal void ShiftPresentationClock(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            StartedAtSeconds += deltaSeconds;
            if (HasTailStarted)
            {
                TailStartedAtSeconds += deltaSeconds;
            }
        }

        internal float ConsumeGameplayPauseSuspendDurationSeconds()
        {
            if (gameplayPauseSuspendedAtSeconds < 0f)
            {
                return 0f;
            }

            var durationSeconds = Mathf.Max(0f, (timeProvider?.TimeSeconds ?? 0f) - gameplayPauseSuspendedAtSeconds);
            gameplayPauseSuspendedAtSeconds = -1f;
            return durationSeconds;
        }

        private static bool CanShowAsPresentationSuspended(VfxLifetimeState value)
        {
            return value == VfxLifetimeState.Spawned ||
                   value == VfxLifetimeState.Active ||
                   value == VfxLifetimeState.PresentationSuspended;
        }
    }
}
