using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class GameplayVfxPresentationController
    {
        private readonly List<ScheduledGameplayVfxRequest> delayedRequests = new();
        private readonly IVfxPool pool;
        private readonly IVfxAnchorResolver anchorResolver;
        private readonly VfxPersistentHandleRegistry persistentRegistry;
        private readonly VfxLifetimeRunner lifetimeRunner;
        private IVfxBindingResolver bindingResolver;
        private GameplayVfxVisibilityContext visibilityContext;
        private bool topologyTransitionStartsSuppressed;
        private bool stageTerminalSuppressed;
        private int topologyTransitionSuppressEpoch;
        private VfxPresentationSuspendReason stickySuspendReasons;

        public GameplayVfxPresentationController(
            IVfxPool pool,
            IVfxAnchorResolver anchorResolver,
            IVfxBindingResolver bindingResolver,
            VfxPersistentHandleRegistry persistentRegistry,
            VfxLifetimeRunner lifetimeRunner)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            this.anchorResolver = anchorResolver ?? throw new ArgumentNullException(nameof(anchorResolver));
            this.bindingResolver = bindingResolver ?? throw new ArgumentNullException(nameof(bindingResolver));
            this.persistentRegistry = persistentRegistry ?? throw new ArgumentNullException(nameof(persistentRegistry));
            this.lifetimeRunner = lifetimeRunner ?? throw new ArgumentNullException(nameof(lifetimeRunner));
        }

        public int MissingBindingCount { get; private set; }

        public int MissingAnchorCount { get; private set; }

        public int CompatibilityFailureCount { get; private set; }

        public int VisibilityBlockedCount { get; private set; }

        public GameplayVfxVisibilityBlockReason LastVisibilityBlockReason { get; private set; }

        public bool IsStageTerminalSuppressed => stageTerminalSuppressed;

        public int PendingDelayedRequestCount => delayedRequests.Count;

        public void ConfigureBindingResolver(IVfxBindingResolver resolver)
        {
            bindingResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public void SetVisibilityContext(GameplayVfxVisibilityContext context)
        {
            visibilityContext = context;
        }

        public void Refresh(GameplayVfxRequestPlan plan)
        {
            Refresh(plan, default);
        }

        public void Refresh(GameplayVfxRequestPlan plan, GameplayVfxRefreshOptions options)
        {
            persistentRegistry.ReleaseCompleted();
            if (stageTerminalSuppressed)
            {
                delayedRequests.Clear();
                return;
            }

            persistentRegistry.BeginReconcile();
            var persistentDesiredKeys = CollectPersistentDesiredKeys(plan);

            if (plan != null)
            {
                foreach (var request in plan.Requests)
                {
                    Process(request, options);
                }
            }

            RemoveStaleDelayedPersistentRequests(persistentDesiredKeys);
            persistentRegistry.EndReconcile(
                lifetimeRunner,
                preserveTopologyHelperExempt: options.PreserveTopologyHelperExempt);
        }

        public void SetTopologyTransitionStartSuppression(bool suppressed, int epoch)
        {
            topologyTransitionStartsSuppressed = suppressed;
            topologyTransitionSuppressEpoch = suppressed ? epoch : 0;
        }

        public void ClearForTopologyTransitionStart(int epoch)
        {
            delayedRequests.Clear();
            topologyTransitionStartsSuppressed = true;
            topologyTransitionSuppressEpoch = epoch;
            persistentRegistry.ClearForTopologyTransitionStart(pool);
        }

        public void BeginStageTerminalVfxSuppression()
        {
            delayedRequests.Clear();
            stageTerminalSuppressed = true;
            persistentRegistry.StopAllForStageTerminal(pool);
        }

        public void ClearStageTerminalVfxSuppression()
        {
            stageTerminalSuppressed = false;
        }

        public void ValidatePendingTopologyTransitionVisibility(GameplayVfxRequestPlan plan)
        {
            persistentRegistry.ReleaseCompleted();
            if (plan == null || plan.Requests.Count == 0)
            {
                return;
            }

            var requests = plan.Requests;
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (!request.IsPersistent ||
                    request.PersistentKey.IsNone ||
                    !persistentRegistry.IsPendingTopologyTransitionVisibilityValidation(request.PersistentKey))
                {
                    continue;
                }

                ValidatePendingTopologyTransitionVisibility(request);
            }
        }

        public void SuspendPresentation(VfxPresentationSuspendReason reason)
        {
            if (reason == VfxPresentationSuspendReason.None)
            {
                return;
            }

            stickySuspendReasons |= reason;
            persistentRegistry.SuspendAll(reason);
        }

        public void ResumePresentation(VfxPresentationSuspendReason reason)
        {
            if (reason == VfxPresentationSuspendReason.None)
            {
                return;
            }

            stickySuspendReasons &= ~reason;
            persistentRegistry.ResumeAll(reason);
        }

        public void HardCleanupAll()
        {
            delayedRequests.Clear();
            topologyTransitionStartsSuppressed = false;
            stageTerminalSuppressed = false;
            topologyTransitionSuppressEpoch = 0;
            stickySuspendReasons = VfxPresentationSuspendReason.None;
            persistentRegistry.HardCleanupAll(pool);
            pool.HardCleanupAll();
        }

        public void HardCleanupFamily(GameplayVfxFamily family, GameplayVfxCleanupReason reason)
        {
            if (family == GameplayVfxFamily.None)
            {
                return;
            }

            RemoveDelayedRequestsForFamily(family);
            persistentRegistry.HardCleanupFamily(pool, family);
            pool.HardCleanupFamily(family);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (stageTerminalSuppressed)
            {
                delayedRequests.Clear();
                return;
            }

            for (var i = delayedRequests.Count - 1; i >= 0; i--)
            {
                var scheduled = delayedRequests[i].Advance(deltaTime);
                if (scheduled.RemainingSeconds > 0f)
                {
                    delayedRequests[i] = scheduled;
                    continue;
                }

                delayedRequests.RemoveAt(i);
                ProcessNow(scheduled.Request);
            }
        }

        private void Process(in GameplayVfxRequest request, in GameplayVfxRefreshOptions options)
        {
            if (stageTerminalSuppressed)
            {
                return;
            }

            if (IsSuppressedByTopologyTransition(request, options))
            {
                return;
            }

            if (request.DelaySeconds > 0f)
            {
                if (request.IsPersistent && !request.PersistentKey.IsNone)
                {
                    if (options.PreserveLiveDelayedPersistent &&
                        persistentRegistry.TryGet(request.PersistentKey, out var existingHandle) &&
                        IsLivePersistentHandle(existingHandle))
                    {
                        persistentRegistry.MarkDesired(request.PersistentKey);
                        return;
                    }

                    persistentRegistry.StopIfActive(
                        request.PersistentKey,
                        VfxStopPolicy.StopEmittingThenRelease,
                        lifetimeRunner);
                    UpsertDelayedRequest(request);
                    return;
                }

                delayedRequests.Add(new ScheduledGameplayVfxRequest(
                    request,
                    request.DelaySeconds,
                    topologyTransitionSuppressEpoch));
                return;
            }

            if (request.IsPersistent &&
                !request.PersistentKey.IsNone &&
                HasPendingDelayedPersistentRequest(request))
            {
                persistentRegistry.StopIfActive(
                    request.PersistentKey,
                    VfxStopPolicy.StopEmittingThenRelease,
                    lifetimeRunner);
                return;
            }

            ProcessNow(request, options);
        }

        private void ProcessNow(in GameplayVfxRequest request)
        {
            ProcessNow(request, default);
        }

        private void ProcessNow(in GameplayVfxRequest request, in GameplayVfxRefreshOptions options)
        {
            if (stageTerminalSuppressed)
            {
                return;
            }

            if (IsSuppressedByTopologyTransition(request, options))
            {
                return;
            }

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                MissingBindingCount++;
                return;
            }

            policy.ValidateOrThrow();
            ValidateCompatibility(request, policy);

            var preAnchorVisibility = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                policy,
                visibilityContext);
            if (!preAnchorVisibility.IsVisible)
            {
                HandleVisibilityBlocked(request, policy, preAnchorVisibility.BlockReason);
                return;
            }

            if (!anchorResolver.TryResolve(request, policy, out var anchor) || !anchor.IsResolved)
            {
                if (!TryHandleMissingAnchor(request, policy, out anchor))
                {
                    return;
                }
            }

            var postAnchorVisibility = GameplayVfxVisibilityPolicy.EvaluateAfterAnchor(
                request,
                policy,
                anchor,
                visibilityContext);
            if (!postAnchorVisibility.IsVisible)
            {
                HandleVisibilityBlocked(request, policy, postAnchorVisibility.BlockReason);
                return;
            }

            var command = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            if (request.IsPersistent)
            {
                var handle = persistentRegistry.GetOrStart(
                        command,
                        pool,
                        lifetimeRunner,
                        topologyTransitionStartsSuppressed || options.DeferNewTopologyTransitionStarts);
                if (handle != null)
                {
                    ApplyStickySuspendReasons(handle);
                    persistentRegistry.MarkDesired(request.PersistentKey);
                }

                return;
            }

            ApplyStickySuspendReasons(pool.PlayTransient(command));
        }

        private void ValidatePendingTopologyTransitionVisibility(in GameplayVfxRequest request)
        {
            if (!persistentRegistry.TryGet(request.PersistentKey, out var existingHandle) ||
                !IsLivePersistentHandle(existingHandle))
            {
                persistentRegistry.CompleteTopologyTransitionVisibilityValidation(request.PersistentKey);
                return;
            }

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                MissingBindingCount++;
                return;
            }

            policy.ValidateOrThrow();
            ValidateCompatibility(request, policy);

            var preAnchorVisibility = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                policy,
                visibilityContext);
            if (!preAnchorVisibility.IsVisible)
            {
                HandlePendingTopologyTransitionVisibilityBlocked(
                    request,
                    policy,
                    preAnchorVisibility.BlockReason);
                return;
            }

            if (!anchorResolver.TryResolve(request, policy, out var anchor) || !anchor.IsResolved)
            {
                if (!TryHandleMissingAnchor(request, policy, out anchor))
                {
                    return;
                }
            }

            var postAnchorVisibility = GameplayVfxVisibilityPolicy.EvaluateAfterAnchor(
                request,
                policy,
                anchor,
                visibilityContext);
            if (!postAnchorVisibility.IsVisible)
            {
                HandlePendingTopologyTransitionVisibilityBlocked(
                    request,
                    policy,
                    postAnchorVisibility.BlockReason);
                return;
            }

            persistentRegistry.ResumeIfActive(
                request.PersistentKey,
                VfxPresentationSuspendReason.Visibility);
            persistentRegistry.ResumeIfActive(
                request.PersistentKey,
                VfxPresentationSuspendReason.TopologyTransition);
            persistentRegistry.CompleteTopologyTransitionVisibilityValidation(request.PersistentKey);
        }

        private void ApplyStickySuspendReasons(IVfxPlaybackHandle handle)
        {
            if (handle == null || stickySuspendReasons == VfxPresentationSuspendReason.None)
            {
                return;
            }

            ApplyStickySuspendReason(handle, VfxPresentationSuspendReason.Visibility);
            ApplyStickySuspendReason(handle, VfxPresentationSuspendReason.TopologyTransition);
            ApplyStickySuspendReason(handle, VfxPresentationSuspendReason.GameplayPause);
        }

        private void ApplyStickySuspendReason(IVfxPlaybackHandle handle, VfxPresentationSuspendReason reason)
        {
            if ((stickySuspendReasons & reason) == reason)
            {
                handle.SuspendPresentation(reason);
            }
        }

        private bool IsSuppressedByTopologyTransition(
            in GameplayVfxRequest request,
            in GameplayVfxRefreshOptions options)
        {
            return (topologyTransitionStartsSuppressed || options.DeferNewTopologyTransitionStarts) &&
                   !GameplayVfxTopologyHelperExemptionPolicy.AllowsSpawnExemption(
                       request.CueId,
                       request.TopologySpawnMode);
        }

        private bool HasPendingDelayedPersistentRequest(in GameplayVfxRequest request)
        {
            if (!request.IsPersistent || request.PersistentKey.IsNone)
            {
                return false;
            }

            for (var i = 0; i < delayedRequests.Count; i++)
            {
                var pending = delayedRequests[i].Request;
                if (pending.IsPersistent &&
                    pending.PersistentKey.Equals(request.PersistentKey))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLivePersistentHandle(IVfxPlaybackHandle handle)
        {
            return handle != null &&
                   (handle.State == VfxLifetimeState.Spawned ||
                    handle.State == VfxLifetimeState.Active ||
                    handle.State == VfxLifetimeState.PresentationSuspended);
        }

        private void UpsertDelayedRequest(in GameplayVfxRequest request)
        {
            for (var i = 0; i < delayedRequests.Count; i++)
            {
                var pending = delayedRequests[i].Request;
                if (!pending.IsPersistent ||
                    !pending.PersistentKey.Equals(request.PersistentKey))
                {
                    continue;
                }

                if (!pending.Equals(request))
                {
                    delayedRequests[i] = new ScheduledGameplayVfxRequest(
                        request,
                        request.DelaySeconds,
                        topologyTransitionSuppressEpoch);
                }

                return;
            }

            delayedRequests.Add(new ScheduledGameplayVfxRequest(
                request,
                request.DelaySeconds,
                topologyTransitionSuppressEpoch));
        }

        private void RemoveStaleDelayedPersistentRequests(HashSet<VfxPersistentKey> desiredPersistentKeys)
        {
            if (delayedRequests.Count == 0)
            {
                return;
            }

            for (var i = delayedRequests.Count - 1; i >= 0; i--)
            {
                var request = delayedRequests[i].Request;
                if (!request.IsPersistent ||
                    request.PersistentKey.IsNone ||
                    desiredPersistentKeys.Contains(request.PersistentKey))
                {
                    continue;
                }

                delayedRequests.RemoveAt(i);
            }
        }

        private void RemoveDelayedRequestsForFamily(GameplayVfxFamily family)
        {
            for (var i = delayedRequests.Count - 1; i >= 0; i--)
            {
                if (delayedRequests[i].Request.CueId.Family == family)
                {
                    delayedRequests.RemoveAt(i);
                }
            }
        }

        private static HashSet<VfxPersistentKey> CollectPersistentDesiredKeys(GameplayVfxRequestPlan plan)
        {
            var keys = new HashSet<VfxPersistentKey>();
            if (plan == null)
            {
                return keys;
            }

            var requests = plan.Requests;
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.IsPersistent && !request.PersistentKey.IsNone)
                {
                    keys.Add(request.PersistentKey);
                }
            }

            return keys;
        }

        private void HandleVisibilityBlocked(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            GameplayVfxVisibilityBlockReason reason)
        {
            VisibilityBlockedCount++;
            LastVisibilityBlockReason = reason;
            if (request.IsPersistent && !request.PersistentKey.IsNone)
            {
                if (ShouldSuspendWhenVisibilityBlocked(request, reason))
                {
                    EnsurePersistentHandleForSuspendedVisibility(request, policy);
                    persistentRegistry.SuspendIfActive(request.PersistentKey);
                    persistentRegistry.MarkDesired(request.PersistentKey);
                    return;
                }

                persistentRegistry.StopIfActive(
                    request.PersistentKey,
                    policy.StopPolicy,
                    lifetimeRunner);
            }
        }

        private void HandlePendingTopologyTransitionVisibilityBlocked(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            GameplayVfxVisibilityBlockReason reason)
        {
            VisibilityBlockedCount++;
            LastVisibilityBlockReason = reason;
            if (!request.IsPersistent || request.PersistentKey.IsNone)
            {
                return;
            }

            if (ShouldSuspendWhenVisibilityBlocked(request, reason))
            {
                persistentRegistry.SuspendIfActive(
                    request.PersistentKey,
                    VfxPresentationSuspendReason.Visibility);
                persistentRegistry.ResumeIfActive(
                    request.PersistentKey,
                    VfxPresentationSuspendReason.TopologyTransition);
                persistentRegistry.CompleteTopologyTransitionVisibilityValidation(request.PersistentKey);
                return;
            }

            persistentRegistry.StopIfActive(
                request.PersistentKey,
                policy.StopPolicy,
                lifetimeRunner);
        }

        private void EnsurePersistentHandleForSuspendedVisibility(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy)
        {
            if (!request.IsPersistent ||
                request.PersistentKey.IsNone ||
                (persistentRegistry.TryGet(request.PersistentKey, out var existingHandle) &&
                 IsLivePersistentHandle(existingHandle)))
            {
                return;
            }

            if (!anchorResolver.TryResolve(request, policy, out var anchor) || !anchor.IsResolved)
            {
                if (!TryHandleMissingAnchor(request, policy, out anchor))
                {
                    return;
                }
            }

            var command = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            persistentRegistry.GetOrStart(
                command,
                pool,
                lifetimeRunner,
                suppressTopologyTransitionStarts: false);
        }

        private static bool ShouldSuspendWhenVisibilityBlocked(
            in GameplayVfxRequest request,
            GameplayVfxVisibilityBlockReason reason)
        {
            return request.CueId.Equals(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)) &&
                   (reason == GameplayVfxVisibilityBlockReason.JumpTopologySuspended ||
                    reason == GameplayVfxVisibilityBlockReason.JumpWindupSourceTopologyMismatch ||
                    reason == GameplayVfxVisibilityBlockReason.InactiveFace ||
                    reason == GameplayVfxVisibilityBlockReason.FrontFaceInactive ||
                    reason == GameplayVfxVisibilityBlockReason.EntityViewInactive);
        }

        private void ValidateCompatibility(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy)
        {
            if (policy.CueId != request.CueId)
            {
                CompatibilityFailureCount++;
                throw new InvalidOperationException("Gameplay VFX binding cue does not match request cue.");
            }

            if (policy.StyleKey != request.StyleKey)
            {
                CompatibilityFailureCount++;
                throw new InvalidOperationException("Gameplay VFX binding style does not match request style.");
            }

            if (request.IsPersistent)
            {
                if (request.PersistentKey.IsNone)
                {
                    CompatibilityFailureCount++;
                    throw new InvalidOperationException("Persistent Gameplay VFX request requires a persistent key.");
                }

                if (policy.PlaybackMode == VfxPlaybackMode.OneShot)
                {
                    CompatibilityFailureCount++;
                    throw new InvalidOperationException("Persistent Gameplay VFX request cannot use one-shot playback.");
                }

                return;
            }

            if (policy.PlaybackMode == VfxPlaybackMode.Loop
                || policy.PlaybackMode == VfxPlaybackMode.Follow
                || policy.PlaybackMode == VfxPlaybackMode.MotionTrack)
            {
                CompatibilityFailureCount++;
                throw new InvalidOperationException("Transient Gameplay VFX request cannot use persistent playback mode.");
            }

            if (policy.StopPolicy == VfxStopPolicy.ManualStopRequired)
            {
                CompatibilityFailureCount++;
                throw new InvalidOperationException("Manual-stop Gameplay VFX binding requires a persistent request.");
            }
        }

        private bool TryHandleMissingAnchor(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            out VfxResolvedAnchor anchor)
        {
            MissingAnchorCount++;
            switch (policy.MissingAnchorPolicy)
            {
                case VfxMissingAnchorPolicy.SkipOptional:
                case VfxMissingAnchorPolicy.ReportDiagnostic:
                    anchor = VfxResolvedAnchor.Unresolved(policy.MissingAnchorPolicy);
                    return false;
                case VfxMissingAnchorPolicy.UseFallbackCell:
                    if (request.Anchor.HasFallbackCell)
                    {
                        anchor = VfxResolvedAnchor.ForCell(
                            request.Anchor.FallbackCell,
                            request.Anchor.FallbackTopology,
                            request.Anchor.Slot == VfxAnchorSlot.None
                                ? VfxAnchorSlot.CellCenter
                                : request.Anchor.Slot,
                            usedFallback: true);
                        return true;
                    }

                    anchor = VfxResolvedAnchor.Unresolved(policy.MissingAnchorPolicy);
                    return false;
                case VfxMissingAnchorPolicy.FailFast:
                    throw new InvalidOperationException("Gameplay VFX anchor resolution failed.");
                default:
                    anchor = VfxResolvedAnchor.Unresolved(policy.MissingAnchorPolicy);
                    return false;
            }
        }

        private readonly struct ScheduledGameplayVfxRequest
        {
            public ScheduledGameplayVfxRequest(
                GameplayVfxRequest request,
                float remainingSeconds,
                int topologyTransitionEpoch)
            {
                Request = request;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
                TopologyTransitionEpoch = topologyTransitionEpoch;
            }

            public GameplayVfxRequest Request { get; }

            public float RemainingSeconds { get; }

            public int TopologyTransitionEpoch { get; }

            public ScheduledGameplayVfxRequest Advance(float deltaTime)
            {
                return new ScheduledGameplayVfxRequest(
                    Request,
                    RemainingSeconds - Math.Max(0f, deltaTime),
                    TopologyTransitionEpoch);
            }
        }
    }

    public readonly struct GameplayVfxRefreshOptions
    {
        public GameplayVfxRefreshOptions(
            bool deferNewTopologyTransitionStarts,
            bool preserveTopologyHelperExempt = false,
            bool preserveLiveDelayedPersistent = false)
        {
            DeferNewTopologyTransitionStarts = deferNewTopologyTransitionStarts;
            PreserveTopologyHelperExempt = preserveTopologyHelperExempt;
            PreserveLiveDelayedPersistent = preserveLiveDelayedPersistent;
        }

        public bool DeferNewTopologyTransitionStarts { get; }

        public bool PreserveTopologyHelperExempt { get; }

        public bool PreserveLiveDelayedPersistent { get; }

        public static GameplayVfxRefreshOptions TopologyTransitionStart()
        {
            return new GameplayVfxRefreshOptions(deferNewTopologyTransitionStarts: true);
        }

        public static GameplayVfxRefreshOptions TopologyTransitionCompletion()
        {
            return new GameplayVfxRefreshOptions(
                deferNewTopologyTransitionStarts: false,
                preserveTopologyHelperExempt: true,
                preserveLiveDelayedPersistent: true);
        }
    }
}
