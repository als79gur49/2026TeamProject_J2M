using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class VfxPersistentHandleRegistry
    {
        private readonly Dictionary<VfxPersistentKey, IVfxPlaybackHandle> activeHandles = new();
        private readonly Dictionary<VfxPersistentKey, VfxBindingRuntimePolicy> activePolicies = new();
        private readonly Dictionary<VfxPersistentKey, VfxStopPolicy> stopPolicies = new();
        private readonly HashSet<VfxPersistentKey> desiredKeys = new();

        public int ActiveCount => activeHandles.Count;

        public bool TryGet(VfxPersistentKey key, out IVfxPlaybackHandle handle)
        {
            return activeHandles.TryGetValue(key, out handle);
        }

        public IVfxPlaybackHandle GetOrStart(
            in ResolvedVfxPlaybackCommand command,
            IVfxPool pool,
            VfxLifetimeRunner lifetimeRunner = null)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (activeHandles.TryGetValue(command.PersistentKey, out var existing))
            {
                if (!IsSameBinding(command.PersistentKey, command.Policy))
                {
                    if (!CanStop(existing))
                    {
                        stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
                        return existing;
                    }

                    var oldStopPolicy = stopPolicies.TryGetValue(command.PersistentKey, out var storedPolicy)
                        ? storedPolicy
                        : VfxStopPolicy.StopEmittingThenRelease;
                    if (lifetimeRunner == null)
                    {
                        throw new ArgumentNullException(nameof(lifetimeRunner));
                    }

                    lifetimeRunner.Stop(existing, oldStopPolicy);
                    activeHandles.Remove(command.PersistentKey);
                    activePolicies.Remove(command.PersistentKey);
                    stopPolicies.Remove(command.PersistentKey);
                }
                else
                {
                    stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
                    return existing;
                }
            }

            if (activeHandles.TryGetValue(command.PersistentKey, out existing))
            {
                stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
                return existing;
            }

            var handle = pool.StartPersistent(command);
            if (handle == null)
            {
                return null;
            }

            activeHandles.Add(command.PersistentKey, handle);
            activePolicies.Add(command.PersistentKey, command.Policy);
            stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
            return handle;
        }

        public void MarkDesired(VfxPersistentKey key)
        {
            desiredKeys.Add(key);
        }

        public bool StopIfActive(
            VfxPersistentKey key,
            VfxStopPolicy fallbackStopPolicy,
            VfxLifetimeRunner lifetimeRunner)
        {
            if (lifetimeRunner == null)
            {
                throw new ArgumentNullException(nameof(lifetimeRunner));
            }

            if (!activeHandles.TryGetValue(key, out var handle) ||
                !CanStop(handle))
            {
                return false;
            }

            var stopPolicy = stopPolicies.TryGetValue(key, out var storedPolicy)
                ? storedPolicy
                : fallbackStopPolicy;
            lifetimeRunner.Stop(handle, stopPolicy);
            return true;
        }

        public void BeginReconcile()
        {
            desiredKeys.Clear();
        }

        public void EndReconcile(VfxLifetimeRunner lifetimeRunner)
        {
            if (lifetimeRunner == null)
            {
                throw new ArgumentNullException(nameof(lifetimeRunner));
            }

            foreach (var pair in activeHandles.ToArray())
            {
                if (desiredKeys.Contains(pair.Key) || !CanStop(pair.Value))
                {
                    continue;
                }

                var stopPolicy = stopPolicies.TryGetValue(pair.Key, out var storedPolicy)
                    ? storedPolicy
                    : VfxStopPolicy.StopEmittingThenRelease;
                lifetimeRunner.Stop(pair.Value, stopPolicy);
            }
        }

        public void ReleaseCompleted()
        {
            foreach (var pair in activeHandles.ToArray())
            {
                if (pair.Value.State == VfxLifetimeState.ReleasedToPool
                    || pair.Value.State == VfxLifetimeState.HardCleanup)
                {
                    activeHandles.Remove(pair.Key);
                    activePolicies.Remove(pair.Key);
                    stopPolicies.Remove(pair.Key);
                }
            }
        }

        public void HardCleanupAll(IVfxPool pool)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            foreach (var handle in activeHandles.Values.ToArray())
            {
                if (handle.State != VfxLifetimeState.HardCleanup)
                {
                    handle.HardCleanup();
                    pool.Release(handle);
                }
            }

            activeHandles.Clear();
            activePolicies.Clear();
            stopPolicies.Clear();
            desiredKeys.Clear();
        }

        private bool IsSameBinding(VfxPersistentKey key, VfxBindingRuntimePolicy policy)
        {
            return activePolicies.TryGetValue(key, out var existingPolicy) &&
                   existingPolicy == policy;
        }

        private static bool CanStop(IVfxPlaybackHandle handle)
        {
            return handle.State == VfxLifetimeState.Spawned
                || handle.State == VfxLifetimeState.Active
                || handle.State == VfxLifetimeState.None;
        }
    }
}
