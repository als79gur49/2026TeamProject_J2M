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
        private readonly Dictionary<VfxPersistentKey, GameplayVfxTopologyStopMode> topologyStopModes = new();
        private readonly HashSet<VfxPersistentKey> desiredKeys = new();
        private readonly HashSet<VfxPersistentKey> pendingTopologyTransitionVisibilityValidationKeys = new();

        public int ActiveCount => activeHandles.Count;

        public string LastStopReason { get; private set; }

        public string LastRestartReason { get; private set; }

        public bool TryGet(VfxPersistentKey key, out IVfxPlaybackHandle handle)
        {
            return activeHandles.TryGetValue(key, out handle);
        }

        public bool IsPendingTopologyTransitionVisibilityValidation(VfxPersistentKey key)
        {
            return pendingTopologyTransitionVisibilityValidationKeys.Contains(key);
        }

        public void CompleteTopologyTransitionVisibilityValidation(VfxPersistentKey key)
        {
            pendingTopologyTransitionVisibilityValidationKeys.Remove(key);
        }

        public IVfxPlaybackHandle GetOrStart(
            in ResolvedVfxPlaybackCommand command,
            IVfxPool pool,
            VfxLifetimeRunner lifetimeRunner = null,
            bool suppressTopologyTransitionStarts = false)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (activeHandles.TryGetValue(command.PersistentKey, out var existing))
            {
                if (!IsReusable(existing))
                {
                    RemoveActiveEntry(command.PersistentKey);
                    LastRestartReason = $"ReplaceNonReusableState:{existing?.State.ToString() ?? "Null"}";
                }
                else if (IsSameBinding(command.PersistentKey, command.Policy))
                {
                    if (existing.State == VfxLifetimeState.PresentationSuspended)
                    {
                        existing.ResumePresentation(VfxPresentationSuspendReason.Visibility);
                        existing.ResumePresentation(VfxPresentationSuspendReason.TopologyTransition);
                    }

                    stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
                    topologyStopModes[command.PersistentKey] = command.Request.TopologyStopMode;
                    return existing;
                }

                if (activeHandles.TryGetValue(command.PersistentKey, out existing) &&
                    IsReusable(existing) &&
                    !IsSameBinding(command.PersistentKey, command.Policy))
                {
                    if (lifetimeRunner == null)
                    {
                        throw new ArgumentNullException(nameof(lifetimeRunner));
                    }

                    var oldStopPolicy = stopPolicies.TryGetValue(command.PersistentKey, out var storedPolicy)
                        ? storedPolicy
                        : VfxStopPolicy.StopEmittingThenRelease;
                    lifetimeRunner.Stop(existing, oldStopPolicy);
                    LastStopReason = "ReplaceBinding";
                    LastRestartReason = "ReplaceBinding";
                    RemoveActiveEntry(command.PersistentKey);
                }
            }

            if (activeHandles.TryGetValue(command.PersistentKey, out existing))
            {
                if (!IsSameBinding(command.PersistentKey, command.Policy))
                {
                    stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
                    topologyStopModes[command.PersistentKey] = command.Request.TopologyStopMode;
                    return existing;
                }

                stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
                topologyStopModes[command.PersistentKey] = command.Request.TopologyStopMode;
                return existing;
            }

            if (suppressTopologyTransitionStarts &&
                !GameplayVfxTopologyHelperExemptionPolicy.AllowsSpawnExemption(
                    command.CueId,
                    command.Request.TopologySpawnMode))
            {
                return null;
            }

            var handle = pool.StartPersistent(command);
            if (handle == null)
            {
                return null;
            }

            activeHandles.Add(command.PersistentKey, handle);
            activePolicies.Add(command.PersistentKey, command.Policy);
            stopPolicies[command.PersistentKey] = command.Policy.StopPolicy;
            topologyStopModes[command.PersistentKey] = command.Request.TopologyStopMode;
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
            LastStopReason = "ExplicitStop";
            RemoveActiveEntry(key);
            return true;
        }

        public bool SuspendIfActive(VfxPersistentKey key)
        {
            return SuspendIfActive(key, VfxPresentationSuspendReason.Visibility);
        }

        public bool SuspendIfActive(VfxPersistentKey key, VfxPresentationSuspendReason reason)
        {
            if (!activeHandles.TryGetValue(key, out var handle) ||
                !CanSuspend(handle))
            {
                return false;
            }

            handle.SuspendPresentation(reason);
            LastStopReason = "PresentationSuspend";
            return true;
        }

        public void BeginReconcile()
        {
            desiredKeys.Clear();
        }

        public void EndReconcile(
            VfxLifetimeRunner lifetimeRunner,
            bool preserveTopologyHelperExempt = false)
        {
            if (lifetimeRunner == null)
            {
                throw new ArgumentNullException(nameof(lifetimeRunner));
            }

            foreach (var pair in activeHandles.ToArray())
            {
                if (desiredKeys.Contains(pair.Key))
                {
                    continue;
                }

                if (preserveTopologyHelperExempt &&
                    topologyStopModes.TryGetValue(pair.Key, out var topologyStopMode) &&
                    GameplayVfxTopologyHelperExemptionPolicy.AllowsStopExemption(
                        pair.Value.CueId,
                        topologyStopMode))
                {
                    continue;
                }

                if (!CanStop(pair.Value))
                {
                    RemoveActiveEntry(pair.Key);
                    continue;
                }

                var stopPolicy = stopPolicies.TryGetValue(pair.Key, out var storedPolicy)
                    ? storedPolicy
                    : VfxStopPolicy.StopEmittingThenRelease;
                lifetimeRunner.Stop(pair.Value, stopPolicy);
                LastStopReason = "EndReconcileMissingDesired";
                RemoveActiveEntry(pair.Key);
            }
        }

        public void StopAllForStageTerminal(IVfxPool pool)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            foreach (var pair in activeHandles.ToArray())
            {
                var key = pair.Key;
                var handle = pair.Value;
                if (handle == null)
                {
                    RemoveActiveEntry(key);
                    continue;
                }

                if (handle.State != VfxLifetimeState.HardCleanup &&
                    handle.State != VfxLifetimeState.ReleasedToPool)
                {
                    handle.Stop(GameplayVfxStopMode.StopEmittingAndClear);
                    pool.Release(handle);
                    LastStopReason = "StageTerminal";
                }

                RemoveActiveEntry(key);
            }

            desiredKeys.Clear();
            pendingTopologyTransitionVisibilityValidationKeys.Clear();
        }

        public void ClearForTopologyTransitionStart(IVfxPool pool)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            foreach (var pair in activeHandles.ToArray())
            {
                var key = pair.Key;
                var handle = pair.Value;
                if (handle == null)
                {
                    RemoveActiveEntry(key);
                    continue;
                }

                var stopMode = topologyStopModes.TryGetValue(key, out var storedStopMode)
                    ? storedStopMode
                    : handle.TopologyStopMode;
                if (GameplayVfxTopologyHelperExemptionPolicy.AllowsPresentationSuspendPreserve(
                        handle.CueId,
                        stopMode))
                {
                    handle.SuspendPresentation(VfxPresentationSuspendReason.TopologyTransition);
                    pendingTopologyTransitionVisibilityValidationKeys.Add(key);
                    LastStopReason = "TopologyTransitionPresentationSuspend";
                    continue;
                }

                if (GameplayVfxTopologyHelperExemptionPolicy.AllowsStopExemption(handle.CueId, stopMode))
                {
                    continue;
                }

                handle.Stop(GameplayVfxStopMode.TopologyTransitionHardClear);
                pool.Release(handle);
                LastStopReason = "TopologyTransitionHardClear";
                RemoveActiveEntry(key);
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
                    topologyStopModes.Remove(pair.Key);
                    pendingTopologyTransitionVisibilityValidationKeys.Remove(pair.Key);
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
            topologyStopModes.Clear();
            desiredKeys.Clear();
            pendingTopologyTransitionVisibilityValidationKeys.Clear();
        }

        public void HardCleanupFamily(IVfxPool pool, GameplayVfxFamily family)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (family == GameplayVfxFamily.None)
            {
                return;
            }

            foreach (var pair in activeHandles.ToArray())
            {
                var handle = pair.Value;
                if (handle == null || handle.CueId.Family != family)
                {
                    continue;
                }

                if (handle.State != VfxLifetimeState.HardCleanup)
                {
                    handle.HardCleanup();
                    pool.Release(handle);
                }

                RemoveActiveEntry(pair.Key);
            }
        }

        public void SuspendAll(VfxPresentationSuspendReason reason)
        {
            foreach (var handle in activeHandles.Values.ToArray())
            {
                if (CanSuspend(handle))
                {
                    handle.SuspendPresentation(reason);
                }
            }
        }

        public void ResumeAll(VfxPresentationSuspendReason reason)
        {
            foreach (var handle in activeHandles.Values.ToArray())
            {
                if (handle != null && handle.State == VfxLifetimeState.PresentationSuspended)
                {
                    handle.ResumePresentation(reason);
                }
            }
        }

        public bool ResumeIfActive(VfxPersistentKey key, VfxPresentationSuspendReason reason)
        {
            if (!activeHandles.TryGetValue(key, out var handle) ||
                handle == null ||
                handle.State != VfxLifetimeState.PresentationSuspended)
            {
                return false;
            }

            handle.ResumePresentation(reason);
            return true;
        }

        private bool IsSameBinding(VfxPersistentKey key, VfxBindingRuntimePolicy policy)
        {
            return activePolicies.TryGetValue(key, out var existingPolicy) &&
                   existingPolicy == policy;
        }

        private void RemoveActiveEntry(VfxPersistentKey key)
        {
            activeHandles.Remove(key);
            activePolicies.Remove(key);
            stopPolicies.Remove(key);
            topologyStopModes.Remove(key);
            desiredKeys.Remove(key);
            pendingTopologyTransitionVisibilityValidationKeys.Remove(key);
        }

        private static bool IsReusable(IVfxPlaybackHandle handle)
        {
            return handle != null &&
                   (handle.State == VfxLifetimeState.Spawned ||
                    handle.State == VfxLifetimeState.Active ||
                    handle.State == VfxLifetimeState.PresentationSuspended);
        }

        private static bool CanStop(IVfxPlaybackHandle handle)
        {
            return IsReusable(handle);
        }

        private static bool CanSuspend(IVfxPlaybackHandle handle)
        {
            return handle != null &&
                   (handle.State == VfxLifetimeState.Spawned ||
                    handle.State == VfxLifetimeState.Active ||
                    handle.State == VfxLifetimeState.PresentationSuspended);
        }
    }
}
