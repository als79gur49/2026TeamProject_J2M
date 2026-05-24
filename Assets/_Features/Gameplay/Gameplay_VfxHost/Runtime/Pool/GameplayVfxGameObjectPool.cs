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
        private readonly Dictionary<int, GameplayVfxFamily> familyByPrefabId = new();
        private readonly Dictionary<GameplayVfxPlaybackHandle, ParameterizedMotionVfxCommand> activeParameterizedMotions = new();
        private readonly HashSet<GameplayVfxPlaybackHandle> activeHandles = new();
        private readonly Dictionary<int, int> entranceParticleDumpMasks = new();
        private readonly IGameplayVfxCloneSourceProvider cloneSourceProvider;
        private readonly GameplayVfxRuntimeRoot root;
        private readonly IGameplayVfxTimeProvider timeProvider;
        private readonly Dictionary<GameplayVfxCueId, int> releaseToPoolCountByCue = new();
        private IVfxPrefabProvider prefabProvider;
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

        public void ConfigurePrefabProvider(IVfxPrefabProvider provider)
        {
            prefabProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

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
                if (GameplayVfxLifetimeTrace.IsEntranceSpawn(command.CueId))
                {
                    GameplayVfxLifetimeTrace.Log(
                        nameof(PlayParameterizedMotion),
                        "DroppedByMaxConcurrent",
                        $"{GameplayVfxLifetimeTrace.DescribeRequest(command.Request)} {GameplayVfxLifetimeTrace.DescribePolicy(command.Policy)} activeEntrance={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)}",
                        includeStackTrace: true);
                }

                return null;
            }

            var prefabInstanceId = prefab.GetInstanceID();
            familyByPrefabId[prefabInstanceId] = command.CueId.Family;
            var instance = Lease(prefab, prefabInstanceId);
            var now = timeProvider.TimeSeconds;
            var handle = new GameplayVfxPlaybackHandle(++nextHandleId, command, instance, now, timeProvider);
            instance.ActivateParameterizedMotion(prefabInstanceId, handle, root.OneShotRoot, motionCommand, cloneSourceProvider);
            handle.MarkSpawned();
            handle.MarkActive();
            activeHandles.Add(handle);
            activeParameterizedMotions[handle] = motionCommand;
            TraceEntranceHandle(nameof(PlayParameterizedMotion), "HandleActivated", handle, now, includeStackTrace: false);
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

        public void HardClearActiveForTopologyTransition()
        {
            GameplayVfxLifetimeTrace.Log(
                nameof(HardClearActiveForTopologyTransition),
                "Entry",
                $"activeTotalBefore={ActiveCount} activeEntranceBefore={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)}",
                includeStackTrace: true);
            foreach (var handle in activeHandles.ToArray())
            {
                if (handle == null ||
                    handle.IsTerminal ||
                    GameplayVfxTopologyHelperExemptionPolicy.AllowsStopExemption(
                        handle.CueId,
                        handle.TopologyStopMode))
                {
                    if (handle != null &&
                        !handle.IsTerminal &&
                        GameplayVfxTopologyHelperExemptionPolicy.AllowsPresentationSuspendPreserve(
                            handle.CueId,
                            handle.TopologyStopMode))
                    {
                        handle.SuspendPresentation();
                    }

                    continue;
                }

                handle.Stop(GameplayVfxStopMode.TopologyTransitionHardClear);
                TraceEntranceHandle(nameof(HardClearActiveForTopologyTransition), "TopologyTransitionHardClear", handle, timeProvider.TimeSeconds, includeStackTrace: true);
                ReleaseInternal(handle, forceHardCleanup: false);
            }
            GameplayVfxLifetimeTrace.Log(
                nameof(HardClearActiveForTopologyTransition),
                "Exit",
                $"activeTotalAfter={ActiveCount} activeEntranceAfter={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)}");
        }

        public void HardCleanupAll()
        {
            GameplayVfxLifetimeTrace.Log(
                nameof(HardCleanupAll),
                "PoolHardCleanupAllEntry",
                $"activeTotalBefore={ActiveCount} activeEntranceBefore={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)} pooledCount={PooledCount}",
                includeStackTrace: true);
            foreach (var handle in activeHandles.ToArray())
            {
                TraceEntranceHandle(nameof(HardCleanupAll), "HandleHardCleanup", handle, timeProvider.TimeSeconds, includeStackTrace: true);
                handle?.HardCleanup();
            }

            activeHandles.Clear();
            activeParameterizedMotions.Clear();
            releaseToPoolCountByCue.Clear();
            entranceParticleDumpMasks.Clear();

            foreach (var instance in allByPrefabId.Values.SelectMany(list => list).ToArray())
            {
                if (instance?.GameObject != null)
                {
                    instance.HardCleanup();
                }
            }

            availableByPrefabId.Clear();
            allByPrefabId.Clear();
            familyByPrefabId.Clear();
            GameplayVfxLifetimeTrace.Log(
                nameof(HardCleanupAll),
                "PoolHardCleanupAllExit",
                $"activeTotalAfter={ActiveCount} activeEntranceAfter={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)} pooledCount={PooledCount}");
        }

        public void HardCleanupFamily(GameplayVfxFamily family)
        {
            if (family == GameplayVfxFamily.None)
            {
                return;
            }

            GameplayVfxLifetimeTrace.Log(
                nameof(HardCleanupFamily),
                "PoolHardCleanupFamilyEntry",
                $"family={family} activeTotalBefore={ActiveCount} activeEntranceBefore={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)} pooledCount={PooledCount}",
                includeStackTrace: true);
            foreach (var handle in activeHandles.ToArray())
            {
                if (handle == null || handle.CueId.Family != family)
                {
                    continue;
                }

                TraceEntranceHandle(nameof(HardCleanupFamily), "HandleHardCleanupFamily", handle, timeProvider.TimeSeconds, includeStackTrace: true);
                handle.HardCleanup();
                activeHandles.Remove(handle);
                activeParameterizedMotions.Remove(handle);
                entranceParticleDumpMasks.Remove(handle.HandleId);
            }

            foreach (var cue in releaseToPoolCountByCue.Keys.ToArray())
            {
                if (cue.Family == family)
                {
                    releaseToPoolCountByCue.Remove(cue);
                }
            }

            CleanupStoredInstancesForFamily(family);
            GameplayVfxLifetimeTrace.Log(
                nameof(HardCleanupFamily),
                "PoolHardCleanupFamilyExit",
                $"family={family} activeTotalAfter={ActiveCount} activeEntranceAfter={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)} pooledCount={PooledCount}");
        }

        public void Advance(float deltaSeconds)
        {
            var now = timeProvider.TimeSeconds;
            foreach (var handle in activeHandles.ToArray())
            {
            if (handle == null || handle.IsTerminal)
            {
                if (handle != null)
                {
                    entranceParticleDumpMasks.Remove(handle.HandleId);
                }

                activeHandles.Remove(handle);
                continue;
            }

            TraceEntranceParticleDumpThresholds(handle, now);
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
                if (GameplayVfxLifetimeTrace.IsEntranceSpawn(command.CueId))
                {
                    GameplayVfxLifetimeTrace.Log(
                        nameof(Play),
                        "DroppedByMaxConcurrent",
                        $"{GameplayVfxLifetimeTrace.DescribeRequest(command.Request)} {GameplayVfxLifetimeTrace.DescribePolicy(command.Policy)} activeEntrance={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)}",
                        includeStackTrace: true);
                }

                return null;
            }

            var prefabInstanceId = prefab.GetInstanceID();
            familyByPrefabId[prefabInstanceId] = command.CueId.Family;
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
            TraceEntranceHandle(nameof(Play), "HandleActivated", handle, now, includeStackTrace: false);
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

        private void CleanupStoredInstancesForFamily(GameplayVfxFamily family)
        {
            foreach (var pair in familyByPrefabId.ToArray())
            {
                if (pair.Value != family)
                {
                    continue;
                }

                var prefabInstanceId = pair.Key;
                if (allByPrefabId.TryGetValue(prefabInstanceId, out var all))
                {
                    foreach (var instance in all.ToArray())
                    {
                        if (instance?.GameObject != null)
                        {
                            instance.HardCleanup();
                        }
                    }
                }

                allByPrefabId.Remove(prefabInstanceId);
                availableByPrefabId.Remove(prefabInstanceId);
                familyByPrefabId.Remove(prefabInstanceId);
            }
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
            TraceEntranceHandle(nameof(AdvanceActiveHandle), "LifetimeCheck", handle, now, includeStackTrace: false);
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

            TraceEntranceHandle(nameof(AdvanceActiveHandle), "LifetimeBoundaryReached", handle, now, includeStackTrace: true);
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
            TraceEntranceHandle(
                nameof(ReleaseInternal),
                forceHardCleanup ? "ForceHardCleanupRelease" : "Release",
                handle,
                timeProvider.TimeSeconds,
                includeStackTrace: true);
            activeHandles.Remove(handle);
            activeParameterizedMotions.Remove(handle);
            entranceParticleDumpMasks.Remove(handle.HandleId);

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
            GameplayVfxLifetimeTrace.Log(
                nameof(ReleaseInternal),
                "ReleaseCompleted",
                $"cueFamily={handle.CueId.Family} cueCode={handle.CueId.Code} cueName={GameplayVfxLifetimeTrace.DescribeCueName(handle.CueId)} activeTotalAfter={ActiveCount} activeEntranceAfter={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)} releaseToPoolCount={GetReleaseToPoolCount(handle.CueId)}");
        }

        private void ReleaseIfTailComplete(GameplayVfxPlaybackHandle handle, float now)
        {
            if (handle.Instance == null || !handle.Instance.IsTailComplete(now))
            {
                return;
            }

            TraceEntranceHandle(nameof(ReleaseIfTailComplete), "TailComplete", handle, now, includeStackTrace: true);
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

        private void TraceEntranceHandle(
            string method,
            string reason,
            GameplayVfxPlaybackHandle handle,
            float now,
            bool includeStackTrace)
        {
            if (handle == null || !GameplayVfxLifetimeTrace.IsEntranceSpawn(handle.CueId))
            {
                return;
            }

            GameplayVfxLifetimeTrace.Log(
                method,
                reason,
                $"{DescribeHandle(handle, now)} activeTotal={ActiveCount} activeEntrance={GetActiveCount(GameplayVfxLifetimeTrace.EntranceSpawnCue)}",
                handle.Instance?.GameObject,
                includeStackTrace);
        }

        private void TraceEntranceParticleDumpThresholds(GameplayVfxPlaybackHandle handle, float now)
        {
            if (handle == null || !GameplayVfxLifetimeTrace.IsEntranceSpawn(handle.CueId))
            {
                return;
            }

            var age = now - handle.StartedAtSeconds;
            var mask = entranceParticleDumpMasks.TryGetValue(handle.HandleId, out var currentMask)
                ? currentMask
                : 0;
            var nextMask = mask;
            if (age >= 1f && (mask & 1) == 0)
            {
                nextMask |= 1;
                TraceEntranceHandle(nameof(Advance), "ParticleStateDump_1s", handle, now, includeStackTrace: false);
                GameplayVfxLifetimeTrace.Log(
                    nameof(Advance),
                    "ParticleStateDump_1s",
                    GameplayVfxLifetimeTrace.DescribeParticles(handle.Instance?.GameObject),
                    handle.Instance?.GameObject);
            }

            if (age >= 2f && (mask & 2) == 0)
            {
                nextMask |= 2;
                TraceEntranceHandle(nameof(Advance), "ParticleStateDump_2s", handle, now, includeStackTrace: false);
                GameplayVfxLifetimeTrace.Log(
                    nameof(Advance),
                    "ParticleStateDump_2s",
                    GameplayVfxLifetimeTrace.DescribeParticles(handle.Instance?.GameObject),
                    handle.Instance?.GameObject);
            }

            if (age >= 5f && (mask & 4) == 0)
            {
                nextMask |= 4;
                TraceEntranceHandle(nameof(Advance), "ParticleStateDump_5s", handle, now, includeStackTrace: false);
                GameplayVfxLifetimeTrace.Log(
                    nameof(Advance),
                    "ParticleStateDump_5s",
                    GameplayVfxLifetimeTrace.DescribeParticles(handle.Instance?.GameObject),
                    handle.Instance?.GameObject);
            }

            if (nextMask != mask)
            {
                entranceParticleDumpMasks[handle.HandleId] = nextMask;
            }
        }

        private static string DescribeHandle(GameplayVfxPlaybackHandle handle, float now)
        {
            if (handle == null)
            {
                return "handle=<null>";
            }

            var age = Mathf.Max(0f, now - handle.StartedAtSeconds);
            return $"handleId={handle.HandleId} state={handle.State} cueFamily={handle.CueId.Family} cueCode={handle.CueId.Code} cueName={GameplayVfxLifetimeTrace.DescribeCueName(handle.CueId)} isPersistent={handle.IsPersistent} createdAt={handle.StartedAtSeconds:F3} age={age:F3} stopPolicy={handle.Policy.StopPolicy} defaultLifetimeSeconds={handle.Policy.DefaultLifetimeSeconds:F3} tailSeconds={handle.Policy.TailSeconds:F3} effectiveLifetimeSeconds={(handle.Policy.DefaultLifetimeSeconds + handle.Policy.TailSeconds):F3} {GameplayVfxLifetimeTrace.DescribeGameObject(handle.Instance?.GameObject)}";
        }
    }
}
