using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public sealed class GameplayVfxGameObjectPool : IVfxPool
    {
        private readonly Dictionary<int, Stack<GameplayVfxPooledInstance>> availableByPrefabId = new();
        private readonly Dictionary<int, List<GameplayVfxPooledInstance>> allByPrefabId = new();
        private readonly Dictionary<GameplayVfxPlaybackHandle, ParameterizedMotionVfxCommand> activeParameterizedMotions = new();
        private readonly HashSet<GameplayVfxPlaybackHandle> activeHandles = new();
        private readonly IGameplayVfxCloneSourceProvider cloneSourceProvider;
        private readonly GameplayVfxRuntimeRoot root;
        private readonly IVfxPrefabProvider prefabProvider;
        private readonly IGameplayVfxTimeProvider timeProvider;
        private readonly Dictionary<GameplayVfxCueId, int> releaseToPoolCountByCue = new();
        private int nextHandleId;

        public GameplayVfxGameObjectPool(
            GameplayVfxRuntimeRoot root,
            IVfxPrefabProvider prefabProvider,
            IGameplayVfxTimeProvider timeProvider = null,
            IGameplayVfxCloneSourceProvider cloneSourceProvider = null)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            root.InitializeRuntime();
            this.prefabProvider = prefabProvider ?? throw new ArgumentNullException(nameof(prefabProvider));
            this.timeProvider = timeProvider ?? new UnityGameplayVfxTimeProvider();
            this.cloneSourceProvider = cloneSourceProvider;
        }

        public int DroppedByLimitCount { get; private set; }

        public int MissingPrefabCount { get; private set; }

        public int ActiveCount => activeHandles.Count(handle => handle != null && !handle.IsTerminal);

        public int PooledCount => availableByPrefabId.Values.Sum(stack => stack.Count);

        internal int GetActiveCount(GameplayVfxCueId cueId)
        {
            return activeHandles.Count(handle =>
                handle != null &&
                !handle.IsTerminal &&
                handle.CueId == cueId);
        }

        internal int GetReleaseToPoolCount(GameplayVfxCueId cueId)
        {
            return releaseToPoolCountByCue.TryGetValue(cueId, out var count)
                ? count
                : 0;
        }

        public IVfxPlaybackHandle PlayTransient(in ResolvedVfxPlaybackCommand command)
        {
            return Play(command, root.OneShotRoot);
        }

        public IVfxPlaybackHandle PlayAttachedTransient(
            in ResolvedVfxPlaybackCommand command,
            Transform parent,
            bool controllerManagedLifetime = false)
        {
            return Play(
                command,
                parent != null ? parent : root.OneShotRoot,
                controllerManagedLifetime);
        }

        public IVfxPlaybackHandle PlayFlipDestroySelfMotion(
            in ResolvedVfxPlaybackCommand command,
            in FlipDestroySelfMotionVfxCommand motionCommand)
        {
            return PlayParameterizedMotion(command, motionCommand.ToParameterizedMotionVfxCommand());
        }

        public IVfxPlaybackHandle PlayParameterizedMotion(
            in ResolvedVfxPlaybackCommand command,
            in ParameterizedMotionVfxCommand motionCommand)
        {
            command.Policy.ValidateOrThrow();
            if (command.CueId != motionCommand.CueId)
            {
                throw new InvalidOperationException("Parameterized VFX command cue does not match resolved playback command cue.");
            }

            if (!prefabProvider.TryResolvePrefab(command, out var prefab) || prefab == null)
            {
                MissingPrefabCount++;
                return null;
            }

            if (IsOverConcurrentLimit(command.Policy))
            {
                DroppedByLimitCount++;
                return null;
            }

            var prefabInstanceId = prefab.GetInstanceID();
            var instance = Lease(prefab, prefabInstanceId);
            var now = timeProvider.TimeSeconds;
            var handle = new GameplayVfxPlaybackHandle(++nextHandleId, command, instance, now, timeProvider);
            instance.ActivateParameterizedMotion(prefabInstanceId, handle, root.OneShotRoot, motionCommand, cloneSourceProvider);
            handle.MarkSpawned();
            handle.MarkActive();
            activeHandles.Add(handle);
            activeParameterizedMotions[handle] = motionCommand;
            return handle;
        }

        public IVfxPlaybackHandle StartPersistent(in ResolvedVfxPlaybackCommand command)
        {
            return Play(command, root.PersistentRoot);
        }

        public void Release(IVfxPlaybackHandle handle)
        {
            if (handle is not GameplayVfxPlaybackHandle playbackHandle)
            {
                return;
            }

            ReleaseInternal(playbackHandle, forceHardCleanup: false);
        }

        public void HardCleanupAll()
        {
            foreach (var handle in activeHandles.ToArray())
            {
                handle?.HardCleanup();
            }

            activeHandles.Clear();
            activeParameterizedMotions.Clear();
            releaseToPoolCountByCue.Clear();

            foreach (var instance in allByPrefabId.Values.SelectMany(list => list).ToArray())
            {
                if (instance?.GameObject != null)
                {
                    instance.HardCleanup();
                }
            }

            availableByPrefabId.Clear();
            allByPrefabId.Clear();
        }

        public void Advance(float deltaSeconds)
        {
            var now = timeProvider.TimeSeconds;
            foreach (var handle in activeHandles.ToArray())
            {
                if (handle == null || handle.IsTerminal)
                {
                    activeHandles.Remove(handle);
                    continue;
                }

                AdvanceHandle(handle, now);
            }
        }

        private GameplayVfxPlaybackHandle Play(
            in ResolvedVfxPlaybackCommand command,
            Transform parent,
            bool controllerManagedLifetime = false)
        {
            command.Policy.ValidateOrThrow();
            if (!prefabProvider.TryResolvePrefab(command, out var prefab) || prefab == null)
            {
                MissingPrefabCount++;
                return null;
            }

            if (IsOverConcurrentLimit(command.Policy))
            {
                DroppedByLimitCount++;
                return null;
            }

            var prefabInstanceId = prefab.GetInstanceID();
            var instance = Lease(prefab, prefabInstanceId);
            var now = timeProvider.TimeSeconds;
            var handle = new GameplayVfxPlaybackHandle(
                ++nextHandleId,
                command,
                instance,
                now,
                timeProvider,
                controllerManagedLifetime);
            instance.Activate(prefabInstanceId, handle, parent, command.Anchor);
            handle.MarkSpawned();
            handle.MarkActive();
            activeHandles.Add(handle);
            return handle;
        }

        private bool IsOverConcurrentLimit(VfxBindingRuntimePolicy policy)
        {
            if (policy.MaxConcurrentInstances <= 0)
            {
                return false;
            }

            var activeForCue = activeHandles.Count(handle =>
                handle != null &&
                !handle.IsTerminal &&
                handle.CueId == policy.CueId);
            return activeForCue >= policy.MaxConcurrentInstances;
        }

        private GameplayVfxPooledInstance Lease(GameObject prefab, int prefabInstanceId)
        {
            if (availableByPrefabId.TryGetValue(prefabInstanceId, out var available))
            {
                while (available.Count > 0)
                {
                    var instance = available.Pop();
                    if (instance?.GameObject != null)
                    {
                        return instance;
                    }
                }
            }

            var gameObject = UnityEngine.Object.Instantiate(prefab, root.PoolRoot, worldPositionStays: false);
            gameObject.name = $"{prefab.name}_PooledVfx";
            var created = new GameplayVfxPooledInstance(gameObject, root.TailRoot);
            if (!allByPrefabId.TryGetValue(prefabInstanceId, out var all))
            {
                all = new List<GameplayVfxPooledInstance>();
                allByPrefabId.Add(prefabInstanceId, all);
            }

            all.Add(created);
            return created;
        }

        private void AdvanceHandle(GameplayVfxPlaybackHandle handle, float now)
        {
            if (TryAdvanceParameterizedMotion(handle, now))
            {
                return;
            }

            switch (handle.State)
            {
                case VfxLifetimeState.Active:
                case VfxLifetimeState.Spawned:
                    AdvanceActiveHandle(handle, now);
                    break;
                case VfxLifetimeState.StopEmitting:
                case VfxLifetimeState.Detached:
                    handle.MarkTailPlaying(now);
                    ReleaseIfTailComplete(handle, now);
                    break;
                case VfxLifetimeState.TailPlaying:
                    ReleaseIfTailComplete(handle, now);
                    break;
            }
        }

        private void AdvanceActiveHandle(GameplayVfxPlaybackHandle handle, float now)
        {
            if (handle.IsPersistent)
            {
                return;
            }

            if (handle.IsLifetimeControllerManaged)
            {
                return;
            }

            var lifetimeSeconds = handle.Policy.DefaultLifetimeSeconds;
            if (lifetimeSeconds > 0f && now - handle.StartedAtSeconds < lifetimeSeconds)
            {
                return;
            }

            switch (handle.Policy.StopPolicy)
            {
                case VfxStopPolicy.DetachThenStopEmittingThenRelease:
                    handle.Detach();
                    handle.StopEmitting();
                    handle.MarkTailPlaying(now);
                    break;
                case VfxStopPolicy.NaturalCompletion:
                case VfxStopPolicy.AuthoredDuration:
                case VfxStopPolicy.StopEmittingThenRelease:
                    handle.StopEmitting();
                    handle.MarkTailPlaying(now);
                    break;
                case VfxStopPolicy.ManualStopRequired:
                case VfxStopPolicy.HardCleanupOnly:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(handle.Policy.StopPolicy));
            }
        }

        private void ReleaseInternal(GameplayVfxPlaybackHandle handle, bool forceHardCleanup)
        {
            if (handle == null)
            {
                return;
            }

            var instance = handle.Instance;
            activeHandles.Remove(handle);
            activeParameterizedMotions.Remove(handle);

            if (instance == null || instance.GameObject == null)
            {
                return;
            }

            if (forceHardCleanup || handle.State == VfxLifetimeState.HardCleanup)
            {
                instance.HardCleanup();
                return;
            }

            if (handle.State != VfxLifetimeState.ReleasedToPool)
            {
                handle.ReleaseToPool();
            }

            instance.DeactivateForPool(root.PoolRoot);
            handle.DetachInstance();
            releaseToPoolCountByCue.TryGetValue(handle.CueId, out var releaseCount);
            releaseToPoolCountByCue[handle.CueId] = releaseCount + 1;
            if (!availableByPrefabId.TryGetValue(instance.PrefabInstanceId, out var available))
            {
                available = new Stack<GameplayVfxPooledInstance>();
                availableByPrefabId.Add(instance.PrefabInstanceId, available);
            }

            available.Push(instance);
        }

        private void ReleaseIfTailComplete(GameplayVfxPlaybackHandle handle, float now)
        {
            if (handle.Instance == null || !handle.Instance.IsTailComplete(now))
            {
                return;
            }

            handle.ReleaseToPool();
            ReleaseInternal(handle, forceHardCleanup: false);
        }

        private bool TryAdvanceParameterizedMotion(GameplayVfxPlaybackHandle handle, float now)
        {
            if (handle.State != VfxLifetimeState.Active &&
                handle.State != VfxLifetimeState.Spawned)
            {
                return false;
            }

            if (!activeParameterizedMotions.TryGetValue(handle, out var motionCommand))
            {
                return false;
            }

            var elapsedSeconds = Mathf.Max(0f, now - handle.StartedAtSeconds);
            handle.Instance?.AdvanceParameterizedMotion(motionCommand, elapsedSeconds);
            if (elapsedSeconds < motionCommand.DurationSeconds)
            {
                return true;
            }

            handle.StopEmitting();
            handle.MarkTailPlaying(now);
            activeParameterizedMotions.Remove(handle);
            ReleaseIfTailComplete(handle, now);
            return true;
        }
    }
}
