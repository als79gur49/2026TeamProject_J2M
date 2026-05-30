using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Loop;
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
        private readonly IGameplayVfxCloneSourceProvider cloneSourceProvider;
        private readonly GameplayVfxRuntimeRoot root;
        private readonly IGameplayVfxTimeProvider timeProvider;
        private readonly Dictionary<GameplayVfxCueId, int> releaseToPoolCountByCue = new();
        private IVfxPrefabProvider prefabProvider;
        private int nextHandleId;
        private VfxPresentationSuspendReason stickyRuntimeSuspendReasons;

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

        public int MissingSourceViewCount { get; private set; }

        public int CommonHostUnavailableCount { get; private set; }

        public int InvalidPlaybackModePolicyCount { get; private set; }

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
            return PlayParameterizedMotion(
                command,
                motionCommand,
                VfxRendererInactiveVisualSnapshotSet.Empty);
        }

        internal IVfxPlaybackHandle PlayParameterizedMotion(
            in ResolvedVfxPlaybackCommand command,
            in ParameterizedMotionVfxCommand motionCommand,
            in VfxRendererInactiveVisualSnapshotSet sourceVisualSnapshot)
        {
            command.Policy.ValidateOrThrow();
            if (command.CueId != motionCommand.CueId)
            {
                throw new InvalidOperationException("Parameterized VFX command cue does not match resolved playback command cue.");
            }

            if (!ValidateParameterizedMotionPolicy(command.Policy, motionCommand))
            {
                InvalidPlaybackModePolicyCount++;
                LogPoolDiagnostic(nameof(PlayParameterizedMotion), "InvalidPlaybackModePolicy", command);
                return null;
            }

            var canResolveSourceClone = CanResolveSourceClone(motionCommand);
            if (command.Policy.VisualSourceMode == VfxVisualSourceMode.SourceCloneMotion &&
                !canResolveSourceClone)
            {
                MissingSourceViewCount++;
                LogPoolDiagnostic(nameof(PlayParameterizedMotion), "MissingSourceView", command);
                return null;
            }

            if (command.Policy.VisualSourceMode == VfxVisualSourceMode.PrefabWithSourceClone &&
                !canResolveSourceClone)
            {
                MissingSourceViewCount++;
                LogPoolDiagnostic(nameof(PlayParameterizedMotion), "MissingSourceView", command);
            }

            if (!prefabProvider.TryResolvePrefab(command, out var prefab) || prefab == null)
            {
                if (command.Policy.VisualSourceMode == VfxVisualSourceMode.SourceCloneMotion &&
                    command.Policy.HostRequirement == GameplayVfxHostRequirement.CommonHostAllowed)
                {
                    CommonHostUnavailableCount++;
                    LogPoolDiagnostic(nameof(PlayParameterizedMotion), "CommonHostUnavailable", command);
                }
                else
                {
                    MissingPrefabCount++;
                    LogPoolDiagnostic(nameof(PlayParameterizedMotion), "MissingPrefab", command);
                }

                return null;
            }

            if (IsOverConcurrentLimit(command.Policy))
            {
                DroppedByLimitCount++;
                return null;
            }

            var prefabInstanceId = prefab.GetInstanceID();
            familyByPrefabId[prefabInstanceId] = command.CueId.Family;
            var instance = Lease(prefab, prefabInstanceId);
            var now = timeProvider.TimeSeconds;
            var handle = new GameplayVfxPlaybackHandle(++nextHandleId, command, instance, now, timeProvider);
            instance.ActivateParameterizedMotion(
                prefabInstanceId,
                handle,
                root.OneShotRoot,
                motionCommand,
                cloneSourceProvider,
                sourceVisualSnapshot);
            handle.MarkSpawned();
            handle.MarkActive();
            ApplyStickySuspendReasons(handle);
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

        public void HardClearActiveForTopologyTransition()
        {
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
                        handle.SuspendPresentation(VfxPresentationSuspendReason.TopologyTransition);
                    }

                    continue;
                }

                handle.Stop(GameplayVfxStopMode.TopologyTransitionHardClear);
                ReleaseInternal(handle, forceHardCleanup: false);
            }
        }

        public void HardCleanupAll()
        {
            stickyRuntimeSuspendReasons = VfxPresentationSuspendReason.None;
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
            familyByPrefabId.Clear();
        }

        public void HardCleanupFamily(GameplayVfxFamily family)
        {
            if (family == GameplayVfxFamily.None)
            {
                return;
            }

            foreach (var handle in activeHandles.ToArray())
            {
                if (handle == null || handle.CueId.Family != family)
                {
                    continue;
                }

                handle.HardCleanup();
                activeHandles.Remove(handle);
                activeParameterizedMotions.Remove(handle);
            }

            foreach (var cue in releaseToPoolCountByCue.Keys.ToArray())
            {
                if (cue.Family == family)
                {
                    releaseToPoolCountByCue.Remove(cue);
                }
            }

            CleanupStoredInstancesForFamily(family);
        }

        public void SuspendActivePresentation(VfxPresentationSuspendReason reason)
        {
            if (reason == VfxPresentationSuspendReason.None)
            {
                return;
            }

            stickyRuntimeSuspendReasons |= reason;

            foreach (var handle in activeHandles.ToArray())
            {
                if (handle == null || handle.IsTerminal)
                {
                    activeHandles.Remove(handle);
                    continue;
                }

                handle.SuspendPresentation(reason);
            }
        }

        public void ResumeActivePresentation(VfxPresentationSuspendReason reason)
        {
            if (reason == VfxPresentationSuspendReason.None)
            {
                return;
            }

            stickyRuntimeSuspendReasons &= ~reason;

            foreach (var handle in activeHandles.ToArray())
            {
                if (handle == null || handle.IsTerminal)
                {
                    activeHandles.Remove(handle);
                    continue;
                }

                if (reason == VfxPresentationSuspendReason.GameplayPause)
                {
                    handle.ShiftPresentationClock(handle.ConsumeGameplayPauseSuspendDurationSeconds());
                }

                handle.ResumePresentation(reason);
            }
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
                LogPoolDiagnostic(nameof(Play), "MissingPrefab", command);
                LogForwardCellImpactPoolPlay(command, null, null, null, poolPlayCalled: false, handleCreated: false, "MissingPrefab");
                return null;
            }

            if (IsOverConcurrentLimit(command.Policy))
            {
                DroppedByLimitCount++;
                LogForwardCellImpactPoolPlay(command, prefab, null, null, poolPlayCalled: false, handleCreated: false, "ConcurrentLimit");
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
            ApplyStickySuspendReasons(handle);
            activeHandles.Add(handle);
            LogForwardCellImpactPoolPlay(command, prefab, handle, instance, poolPlayCalled: true, handleCreated: true, string.Empty);
            return handle;
        }

        private static void LogForwardCellImpactPoolPlay(
            in ResolvedVfxPlaybackCommand command,
            GameObject prefab,
            GameplayVfxPlaybackHandle handle,
            GameplayVfxPooledInstance instance,
            bool poolPlayCalled,
            bool handleCreated,
            string skipReason)
        {
            if (command.CueId != GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact))
            {
                return;
            }

            var shotKey = ForwardCellProjectileDebugLog.BuildShotKey(
                command.Request.SourceEntityId,
                command.Request.Anchor.Cell,
                command.Request.TickIndex,
                command.Request.SequenceId,
                command.Request.SequenceId);
            ForwardCellProjectileDebugLog.MarkPool(shotKey, poolPlayCalled, handleCreated);
            ForwardCellProjectileDebugLog.Log(
                "VFX_POOL_PLAY",
                $"Tick={command.Request.TickIndex} Shot={shotKey} Cue=ForwardCellImpact " +
                $"PoolPlayCalled={poolPlayCalled} HandleCreated={handleCreated} " +
                $"HandleId={(handle != null ? handle.HandleId : 0)} " +
                $"PooledInstanceId={(instance?.GameObject != null ? instance.GameObject.GetInstanceID() : 0)} " +
                $"PrefabName={(prefab != null ? prefab.name : "None")} " +
                $"InstanceName={(instance?.GameObject != null ? instance.GameObject.name : "None")} " +
                $"ParentPath={BuildTransformPath(instance?.Transform?.parent)} " +
                $"ActiveSelf={(instance?.GameObject != null && instance.GameObject.activeSelf)} " +
                $"ActiveInHierarchy={(instance?.GameObject != null && instance.GameObject.activeInHierarchy)} " +
                $"SkipReason={skipReason}");
            ForwardCellProjectileDebugLog.LogSummary(shotKey);
        }

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "None";
            }

            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private void ApplyStickySuspendReasons(GameplayVfxPlaybackHandle handle)
        {
            if (handle == null || stickyRuntimeSuspendReasons == VfxPresentationSuspendReason.None)
            {
                return;
            }

            ApplyStickySuspendReason(handle, VfxPresentationSuspendReason.Visibility);
            ApplyStickySuspendReason(handle, VfxPresentationSuspendReason.TopologyTransition);
            ApplyStickySuspendReason(handle, VfxPresentationSuspendReason.GameplayPause);
        }

        private void ApplyStickySuspendReason(
            GameplayVfxPlaybackHandle handle,
            VfxPresentationSuspendReason reason)
        {
            if ((stickyRuntimeSuspendReasons & reason) == reason)
            {
                handle.SuspendPresentation(reason);
            }
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

        private bool ValidateParameterizedMotionPolicy(
            VfxBindingRuntimePolicy policy,
            in ParameterizedMotionVfxCommand motionCommand)
        {
            if (policy.VisualSourceMode == VfxVisualSourceMode.SourceCloneMotion)
            {
                return policy.HostRequirement == GameplayVfxHostRequirement.CommonHostAllowed &&
                       motionCommand.CloneMode == ParameterizedMotionVfxCloneMode.SourceCloneMotion;
            }

            if (policy.VisualSourceMode == VfxVisualSourceMode.PrefabWithSourceClone)
            {
                return policy.HostRequirement == GameplayVfxHostRequirement.ExplicitPrefabRequired &&
                       motionCommand.CloneMode == ParameterizedMotionVfxCloneMode.PrefabWithSourceClone;
            }

            if (motionCommand.CloneMode == ParameterizedMotionVfxCloneMode.SourceCloneMotion ||
                motionCommand.CloneMode == ParameterizedMotionVfxCloneMode.PrefabWithSourceClone)
            {
                return false;
            }

            return true;
        }

        private bool CanResolveSourceClone(in ParameterizedMotionVfxCommand motionCommand)
        {
            if (cloneSourceProvider == null ||
                !cloneSourceProvider.TryResolveCloneSource(motionCommand.SourceEntityId, out var source) ||
                source.ModelRoot == null)
            {
                return false;
            }

            return source.ModelRoot.GetComponentsInChildren<Renderer>(includeInactive: true).Length > 0;
        }

        private static void LogPoolDiagnostic(
            string method,
            string reason,
            in ResolvedVfxPlaybackCommand command)
        {
            UnityEngine.Debug.LogWarning(
                $"{nameof(GameplayVfxGameObjectPool)}.{method} skipped VFX playback. " +
                $"reason={reason} cueFamily={command.CueId.Family} cueCode={command.CueId.Code} " +
                $"visualSourceMode={command.Policy.VisualSourceMode} hostRequirement={command.Policy.HostRequirement}");
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
