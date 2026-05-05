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
        private readonly Dictionary<GameplayVfxPlaybackHandle, FlipDestroySelfMotionVfxCommand> activeFlipDestroySelfMotions = new();
        private readonly HashSet<GameplayVfxPlaybackHandle> activeHandles = new();
        private readonly GameplayVfxRuntimeRoot root;
        private readonly IVfxPrefabProvider prefabProvider;
        private readonly IGameplayVfxTimeProvider timeProvider;
        private int nextHandleId;

        public GameplayVfxGameObjectPool(
            GameplayVfxRuntimeRoot root,
            IVfxPrefabProvider prefabProvider,
            IGameplayVfxTimeProvider timeProvider = null)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            root.InitializeRuntime();
            this.prefabProvider = prefabProvider ?? throw new ArgumentNullException(nameof(prefabProvider));
            this.timeProvider = timeProvider ?? new UnityGameplayVfxTimeProvider();
        }

        public int DroppedByLimitCount { get; private set; }

        public int MissingPrefabCount { get; private set; }

        public int ActiveCount => activeHandles.Count(handle => handle != null && !handle.IsTerminal);

        public int PooledCount => availableByPrefabId.Values.Sum(stack => stack.Count);

        public IVfxPlaybackHandle PlayTransient(in ResolvedVfxPlaybackCommand command)
        {
            return Play(command, root.OneShotRoot);
        }

        public IVfxPlaybackHandle PlayFlipDestroySelfMotion(
            in ResolvedVfxPlaybackCommand command,
            in FlipDestroySelfMotionVfxCommand motionCommand)
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
            var handle = new GameplayVfxPlaybackHandle(++nextHandleId, command, instance, now, timeProvider);
            instance.ActivateFlipDestroySelfMotion(prefabInstanceId, handle, root.OneShotRoot, motionCommand);
            handle.MarkSpawned();
            handle.MarkActive();
            activeHandles.Add(handle);
            activeFlipDestroySelfMotions[handle] = motionCommand;
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
            activeFlipDestroySelfMotions.Clear();

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

        private GameplayVfxPlaybackHandle Play(in ResolvedVfxPlaybackCommand command, Transform parent)
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
            var handle = new GameplayVfxPlaybackHandle(++nextHandleId, command, instance, now, timeProvider);
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
            if (TryAdvanceFlipDestroySelfMotion(handle, now))
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
            activeFlipDestroySelfMotions.Remove(handle);

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

        private bool TryAdvanceFlipDestroySelfMotion(GameplayVfxPlaybackHandle handle, float now)
        {
            if (handle.State != VfxLifetimeState.Active &&
                handle.State != VfxLifetimeState.Spawned)
            {
                return false;
            }

            if (!activeFlipDestroySelfMotions.TryGetValue(handle, out var motionCommand))
            {
                return false;
            }

            var elapsedSeconds = Mathf.Max(0f, now - handle.StartedAtSeconds);
            handle.Instance?.AdvanceFlipDestroySelfMotion(motionCommand, elapsedSeconds);
            if (elapsedSeconds < motionCommand.FlightDurationSeconds)
            {
                return true;
            }

            handle.StopEmitting();
            handle.MarkTailPlaying(now);
            activeFlipDestroySelfMotions.Remove(handle);
            ReleaseIfTailComplete(handle, now);
            return true;
        }
    }
}
