using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class PresentationMotionFollowingVfxController
    {
        private readonly Dictionary<PresentationMotionInstanceKey, IVfxPlaybackHandle> activeMotionHandlesByKey = new();
        private readonly Dictionary<AttachedVfxFollowerKey, IVfxPlaybackHandle> activeAttachedHandlesByKey = new();
        private readonly Dictionary<PresentationMotionInstanceKey, VfxBindingRuntimePolicy> activeMotionPoliciesByKey = new();
        private readonly Dictionary<AttachedVfxFollowerKey, VfxBindingRuntimePolicy> activeAttachedPoliciesByKey = new();
        private readonly Dictionary<AttachedVfxFollowerKey, AttachedVfxFollowerRetentionPolicy> activeAttachedRetentionPoliciesByKey = new();
        private readonly HashSet<PresentationMotionInstanceKey> desiredMotionKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> desiredAttachedKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> explicitAttachedStopKeys = new();
        private readonly HashSet<PresentationMotionInstanceKey> missingMotionBindingKeys = new();
        private readonly HashSet<PresentationMotionInstanceKey> missingMotionOwnerViewKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> missingAttachedBindingKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> missingAttachedOwnerViewKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> missingExplicitAttachPointKeys = new();
        private readonly List<PresentationMotionInstanceKey> motionStopBuffer = new();
        private readonly List<AttachedVfxFollowerKey> attachedStopBuffer = new();

        public int ActiveHandleCount => activeMotionHandlesByKey.Count + activeAttachedHandlesByKey.Count;

        public int ActiveMotionHandleCount => activeMotionHandlesByKey.Count;

        public int ActiveAttachedHandleCount => activeAttachedHandlesByKey.Count;

        public int MissingBindingCount { get; private set; }

        public int MissingOwnerViewCount { get; private set; }

        public int MotionMissingBindingCount { get; private set; }

        public int MotionMissingOwnerViewCount { get; private set; }

        public int AttachedMissingBindingCount { get; private set; }

        public int AttachedMissingOwnerViewCount { get; private set; }

        public int PlannedAttachCount { get; private set; }

        public int VisibilityBlockedCount { get; private set; }

        public GameplayVfxResolvedVisibilityPolicy LastResolvedVisibilityPolicy { get; private set; }

        public GameplayVfxPresentationOnlyUsageDiagnostic LastPresentationOnlyUsageDiagnostic { get; private set; }

        public int VisibilityBindingResolvedCount { get; private set; }

        public int VisibilityFallbackDefaultCount { get; private set; }

        public int PresentationOnlyAllowedTopologyHelperCount { get; private set; }

        public int PresentationOnlyMisuseCandidateCount { get; private set; }

        public void Refresh(
            int tickIndex,
            GameplayPresentationTrackState trackState,
            GameplayPresentationStateStore stateStore,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            bool enabled,
            GameplayVfxVisibilityContext visibilityContext = default,
            IReadOnlyList<AttachedVfxFollowerDesiredState> attachedDesiredStates = null,
            IReadOnlyList<AttachedVfxFollowerKey> explicitAttachedStopStates = null,
            bool attachedFollowersEnabled = false)
        {
            PlannedAttachCount = 0;
            ResetVisibilityResolveDiagnostics();

            if (trackState == null || stateStore == null || pool == null || bindingResolver == null)
            {
                StopAll(tail: true);
                ClearMissingKeyState();
                return;
            }

            if (!enabled)
            {
                StopAllMotion(tail: true);
                desiredMotionKeys.Clear();
            }
            else
            {
                RefreshMotionFollowers(
                    tickIndex,
                    trackState,
                    stateStore,
                    pool,
                    bindingResolver,
                    visibilityContext);
            }

            if (!attachedFollowersEnabled)
            {
                StopAllAttached(tail: true);
                desiredAttachedKeys.Clear();
            }
            else
            {
                RefreshAttachedFollowers(
                    tickIndex,
                    attachedDesiredStates,
                    explicitAttachedStopStates,
                    stateStore,
                    pool,
                    bindingResolver,
                    visibilityContext);
            }

            PruneMissingKeyState();
        }

        public void ResetSession()
        {
            StopAll(tail: false);
            MissingBindingCount = 0;
            MissingOwnerViewCount = 0;
            MotionMissingBindingCount = 0;
            MotionMissingOwnerViewCount = 0;
            AttachedMissingBindingCount = 0;
            AttachedMissingOwnerViewCount = 0;
            PlannedAttachCount = 0;
            VisibilityBlockedCount = 0;
            ResetVisibilityResolveDiagnostics();
            ClearMissingKeyState();
        }

        public void HardCleanup()
        {
            foreach (var pair in activeMotionHandlesByKey)
            {
                pair.Value?.HardCleanup();
            }

            foreach (var pair in activeAttachedHandlesByKey)
            {
                pair.Value?.HardCleanup();
            }

            activeMotionHandlesByKey.Clear();
            activeAttachedHandlesByKey.Clear();
            activeMotionPoliciesByKey.Clear();
            activeAttachedPoliciesByKey.Clear();
            activeAttachedRetentionPoliciesByKey.Clear();
            ClearMissingKeyState();
            PlannedAttachCount = 0;
        }

        public void HardCleanupFamily(GameplayVfxFamily family)
        {
            if (family == GameplayVfxFamily.None)
            {
                return;
            }

            motionStopBuffer.Clear();
            foreach (var pair in activeMotionHandlesByKey)
            {
                if (activeMotionPoliciesByKey.TryGetValue(pair.Key, out var policy) &&
                    policy.CueId.Family == family)
                {
                    motionStopBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < motionStopBuffer.Count; i++)
            {
                var key = motionStopBuffer[i];
                if (activeMotionHandlesByKey.TryGetValue(key, out var handle))
                {
                    handle?.HardCleanup();
                }

                activeMotionHandlesByKey.Remove(key);
                activeMotionPoliciesByKey.Remove(key);
            }

            attachedStopBuffer.Clear();
            foreach (var pair in activeAttachedHandlesByKey)
            {
                if (pair.Key.CueId.Family == family)
                {
                    attachedStopBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < attachedStopBuffer.Count; i++)
            {
                var key = attachedStopBuffer[i];
                if (activeAttachedHandlesByKey.TryGetValue(key, out var handle))
                {
                    handle?.HardCleanup();
                }

                activeAttachedHandlesByKey.Remove(key);
                activeAttachedPoliciesByKey.Remove(key);
                activeAttachedRetentionPoliciesByKey.Remove(key);
            }
        }

        public void ClearForTopologyTransitionStart(GameplayVfxGameObjectPool pool)
        {
            foreach (var pair in activeMotionHandlesByKey)
            {
                StopHandleForTopologyTransition(pair.Value, pool);
            }

            foreach (var pair in activeAttachedHandlesByKey)
            {
                StopHandleForTopologyTransition(pair.Value, pool);
            }

            activeMotionHandlesByKey.Clear();
            activeAttachedHandlesByKey.Clear();
            activeMotionPoliciesByKey.Clear();
            activeAttachedPoliciesByKey.Clear();
            activeAttachedRetentionPoliciesByKey.Clear();
            desiredMotionKeys.Clear();
            desiredAttachedKeys.Clear();
            explicitAttachedStopKeys.Clear();
            PlannedAttachCount = 0;
        }

        public void StopAttachedFollowersForCue(GameplayVfxCueId cueId, bool tail)
        {
            attachedStopBuffer.Clear();
            foreach (var pair in activeAttachedHandlesByKey)
            {
                if (pair.Key.CueId.Equals(cueId))
                {
                    attachedStopBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < attachedStopBuffer.Count; i++)
            {
                StopAttached(attachedStopBuffer[i], tail);
            }
        }

        private void RefreshMotionFollowers(
            int tickIndex,
            GameplayPresentationTrackState trackState,
            GameplayPresentationStateStore stateStore,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxVisibilityContext visibilityContext)
        {
            desiredMotionKeys.Clear();
            foreach (var pair in trackState.OriginalViewMotionTracks)
            {
                var track = pair.Value;
                if (track == null ||
                    track.IsComplete ||
                    track.Kind != PresentationMotionKind.FlipImpactStay)
                {
                    continue;
                }

                var key = track.InstanceKey;
                if (activeMotionHandlesByKey.TryGetValue(key, out var existingHandle) &&
                    IsHandleLive(existingHandle))
                {
                    if (!IsMotionFollowerVisible(track, visibilityContext))
                    {
                        VisibilityBlockedCount++;
                        StopMotion(key, tail: true);
                        continue;
                    }

                    desiredMotionKeys.Add(key);
                    if (!TryResolveAttachParent(stateStore, track.EntityId, out _))
                    {
                        CountMissingMotionOwnerOnce(key);
                        StopMotion(key, tail: true);
                    }

                    continue;
                }

                desiredMotionKeys.Add(key);
                activeMotionHandlesByKey.Remove(key);
                activeMotionPoliciesByKey.Remove(key);
                if (!TryResolveAttachParent(stateStore, track.EntityId, out var parent))
                {
                    CountMissingMotionOwnerOnce(key);
                    continue;
                }

                if (TryStartAttached(
                        tickIndex,
                        track,
                        parent,
                        pool,
                        bindingResolver,
                        visibilityContext,
                        out var policy,
                        out var handle))
                {
                    activeMotionHandlesByKey[key] = handle;
                    activeMotionPoliciesByKey[key] = policy;
                    missingMotionBindingKeys.Remove(key);
                    missingMotionOwnerViewKeys.Remove(key);
                    PlannedAttachCount++;
                }
            }

            StopStaleMotionHandles();
        }

        private bool TryStartAttached(
            int tickIndex,
            PresentationMotionTrack track,
            Transform parent,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxVisibilityContext visibilityContext,
            out VfxBindingRuntimePolicy policy,
            out IVfxPlaybackHandle handle)
        {
            handle = null;
            policy = default;
            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail);
            var sequenceId = track.InstanceKey.CorrelationId != 0
                ? track.InstanceKey.CorrelationId
                : track.EntityId;
            var request = new GameplayVfxRequest(
                tickIndex,
                sequenceId,
                track.InstanceKey.CorrelationId,
                sourceEntityId: track.EntityId,
                cueId,
                VfxAnchor.ForEntity(track.EntityId, VfxAnchorSlot.EntityCenter),
                VfxTimingKind.DuringMotion,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out policy))
            {
                CountMissingMotionBindingOnce(track.InstanceKey);
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match FlipImpactStayTrail request cue.");
            }

            var decision = EvaluateVisibility(
                request,
                policy,
                visibilityContext);
            if (!decision.IsVisible)
            {
                VisibilityBlockedCount++;
                return false;
            }

            var anchor = VfxResolvedAnchor.ForEntity(
                track.EntityId,
                VfxAnchorSlot.EntityCenter,
                Vector3.zero,
                Quaternion.identity,
                fallbackCell: default,
                fallbackTopology: default);
            var command = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            handle = pool.PlayAttachedTransient(
                command,
                parent,
                controllerManagedLifetime: true);
            return handle != null;
        }

        private static bool TryResolveAttachParent(
            GameplayPresentationStateStore stateStore,
            int entityId,
            out Transform parent)
        {
            parent = null;
            if (stateStore == null ||
                !stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) ||
                view == null ||
                !view.isActiveAndEnabled ||
                !view.gameObject.activeInHierarchy)
            {
                return false;
            }

            parent = view.ModelRoot != null ? view.ModelRoot : view.transform;
            return parent != null;
        }

        private void RefreshAttachedFollowers(
            int tickIndex,
            IReadOnlyList<AttachedVfxFollowerDesiredState> attachedDesiredStates,
            IReadOnlyList<AttachedVfxFollowerKey> explicitStopStates,
            GameplayPresentationStateStore stateStore,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxVisibilityContext visibilityContext)
        {
            desiredAttachedKeys.Clear();
            explicitAttachedStopKeys.Clear();
            if (explicitStopStates != null)
            {
                for (var i = 0; i < explicitStopStates.Count; i++)
                {
                    var key = explicitStopStates[i];
                    explicitAttachedStopKeys.Add(key);
                    StopAttached(key, tail: true);
                }
            }

            if (attachedDesiredStates != null)
            {
                for (var i = 0; i < attachedDesiredStates.Count; i++)
                {
                    var desiredState = attachedDesiredStates[i];
                    var key = desiredState.Key;
                    if (explicitAttachedStopKeys.Contains(key))
                    {
                        continue;
                    }

                    if (activeAttachedHandlesByKey.TryGetValue(key, out var existingHandle) &&
                        IsHandleLive(existingHandle))
                    {
                        if (!IsAttachedFollowerVisible(key, visibilityContext))
                        {
                            VisibilityBlockedCount++;
                            StopAttached(key, tail: true);
                            continue;
                        }

                        desiredAttachedKeys.Add(key);
                        activeAttachedRetentionPoliciesByKey[key] = desiredState.RetentionPolicy;
                        if (!TryResolveAttachParent(
                                stateStore,
                                desiredState.SourceEntityId,
                                desiredState.AttachPointId,
                                key,
                                out _))
                        {
                            CountMissingAttachedOwnerOnce(key);
                            StopAttached(key, tail: true);
                        }

                        continue;
                    }

                    desiredAttachedKeys.Add(key);
                    activeAttachedHandlesByKey.Remove(key);
                    activeAttachedPoliciesByKey.Remove(key);
                    activeAttachedRetentionPoliciesByKey.Remove(key);
                    if (!TryResolveAttachParent(
                            stateStore,
                            desiredState.SourceEntityId,
                            desiredState.AttachPointId,
                            key,
                            out var parent))
                    {
                        CountMissingAttachedOwnerOnce(key);
                        continue;
                    }

                    if (TryStartAttachedFollower(
                            tickIndex,
                            desiredState,
                            parent,
                            pool,
                            bindingResolver,
                            visibilityContext,
                            out var policy,
                            out var handle))
                    {
                        activeAttachedHandlesByKey[key] = handle;
                        activeAttachedPoliciesByKey[key] = policy;
                        activeAttachedRetentionPoliciesByKey[key] = desiredState.RetentionPolicy;
                        missingAttachedBindingKeys.Remove(key);
                        missingAttachedOwnerViewKeys.Remove(key);
                        PlannedAttachCount++;
                    }
                }
            }

            StopStaleAttachedHandles(stateStore, visibilityContext);
        }

        private bool TryStartAttachedFollower(
            int tickIndex,
            in AttachedVfxFollowerDesiredState desiredState,
            Transform parent,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxVisibilityContext visibilityContext,
            out VfxBindingRuntimePolicy policy,
            out IVfxPlaybackHandle handle)
        {
            handle = null;
            policy = default;
            var sequenceId = desiredState.SequenceId != 0
                ? desiredState.SequenceId
                : desiredState.SourceEntityId;
            var request = new GameplayVfxRequest(
                tickIndex,
                sequenceId,
                desiredState.SequenceId,
                sourceEntityId: desiredState.SourceEntityId,
                desiredState.CueId,
                VfxAnchor.ForEntity(desiredState.SourceEntityId, VfxAnchorSlot.EntityCenter),
                VfxTimingKind.DuringMotion,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            var key = desiredState.Key;
            if (!bindingResolver.TryResolve(request, out policy))
            {
                CountMissingAttachedBindingOnce(key);
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match attached follower request cue.");
            }

            var decision = EvaluateVisibility(
                request,
                policy,
                visibilityContext);
            if (!decision.IsVisible)
            {
                VisibilityBlockedCount++;
                return false;
            }

            var anchor = VfxResolvedAnchor.ForEntity(
                desiredState.SourceEntityId,
                VfxAnchorSlot.EntityCenter,
                desiredState.LocalPosition,
                desiredState.LocalRotation,
                fallbackCell: default,
                fallbackTopology: default);
            var command = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            handle = pool.PlayAttachedTransient(
                command,
                parent,
                controllerManagedLifetime: true);
            return handle != null;
        }

        private void StopStaleMotionHandles()
        {
            motionStopBuffer.Clear();
            foreach (var pair in activeMotionHandlesByKey)
            {
                if (!desiredMotionKeys.Contains(pair.Key) ||
                    !IsHandleLive(pair.Value))
                {
                    motionStopBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < motionStopBuffer.Count; i++)
            {
                StopMotion(motionStopBuffer[i], tail: true);
            }
        }

        private void StopStaleAttachedHandles(
            GameplayPresentationStateStore stateStore,
            GameplayVfxVisibilityContext visibilityContext)
        {
            attachedStopBuffer.Clear();
            foreach (var pair in activeAttachedHandlesByKey)
            {
                var key = pair.Key;
                if (!IsHandleLive(pair.Value))
                {
                    attachedStopBuffer.Add(key);
                    continue;
                }

                if (desiredAttachedKeys.Contains(key))
                {
                    continue;
                }

                activeAttachedRetentionPoliciesByKey.TryGetValue(
                    key,
                    out var retentionPolicy);
                if (retentionPolicy == AttachedVfxFollowerRetentionPolicy.RetainUntilExplicitStop)
                {
                    if (!IsAttachedFollowerVisible(key, visibilityContext))
                    {
                        VisibilityBlockedCount++;
                        attachedStopBuffer.Add(key);
                        continue;
                    }

                    if (!TryResolveAttachParent(
                            stateStore,
                            key.SourceEntityId,
                            key.AttachPointId,
                            key,
                            out _))
                    {
                        CountMissingAttachedOwnerOnce(key);
                        attachedStopBuffer.Add(key);
                    }

                    continue;
                }

                attachedStopBuffer.Add(key);
            }

            for (var i = 0; i < attachedStopBuffer.Count; i++)
            {
                StopAttached(attachedStopBuffer[i], tail: true);
            }
        }

        private void StopAll(bool tail)
        {
            StopAllMotion(tail);
            StopAllAttached(tail);
        }

        private void StopAllMotion(bool tail)
        {
            motionStopBuffer.Clear();
            foreach (var pair in activeMotionHandlesByKey)
            {
                motionStopBuffer.Add(pair.Key);
            }

            for (var i = 0; i < motionStopBuffer.Count; i++)
            {
                StopMotion(motionStopBuffer[i], tail);
            }
        }

        private void StopAllAttached(bool tail)
        {
            attachedStopBuffer.Clear();
            foreach (var pair in activeAttachedHandlesByKey)
            {
                attachedStopBuffer.Add(pair.Key);
            }

            for (var i = 0; i < attachedStopBuffer.Count; i++)
            {
                StopAttached(attachedStopBuffer[i], tail);
            }
        }

        private void StopMotion(PresentationMotionInstanceKey key, bool tail)
        {
            if (!activeMotionHandlesByKey.TryGetValue(key, out var handle))
            {
                return;
            }

            activeMotionHandlesByKey.Remove(key);
            activeMotionPoliciesByKey.Remove(key);
            StopHandle(handle, tail);
        }

        private void StopAttached(AttachedVfxFollowerKey key, bool tail)
        {
            if (!activeAttachedHandlesByKey.TryGetValue(key, out var handle))
            {
                activeAttachedPoliciesByKey.Remove(key);
                activeAttachedRetentionPoliciesByKey.Remove(key);
                return;
            }

            activeAttachedHandlesByKey.Remove(key);
            activeAttachedPoliciesByKey.Remove(key);
            activeAttachedRetentionPoliciesByKey.Remove(key);
            StopHandle(handle, tail);
        }

        private static void StopHandle(IVfxPlaybackHandle handle, bool tail)
        {
            if (handle == null)
            {
                return;
            }

            if (tail)
            {
                handle.Detach();
                handle.StopEmitting();
                handle.MarkTailPlaying();
                return;
            }

            handle.HardCleanup();
        }

        private static void StopHandleForTopologyTransition(
            IVfxPlaybackHandle handle,
            GameplayVfxGameObjectPool pool)
        {
            if (handle == null)
            {
                return;
            }

            handle.Stop(GameplayVfxStopMode.TopologyTransitionHardClear);
            pool?.Release(handle);
        }

        private void CountMissingMotionBindingOnce(PresentationMotionInstanceKey key)
        {
            if (missingMotionBindingKeys.Add(key))
            {
                MotionMissingBindingCount++;
                MissingBindingCount++;
            }
        }

        private void CountMissingMotionOwnerOnce(PresentationMotionInstanceKey key)
        {
            if (missingMotionOwnerViewKeys.Add(key))
            {
                MotionMissingOwnerViewCount++;
                MissingOwnerViewCount++;
            }
        }

        private void CountMissingAttachedBindingOnce(AttachedVfxFollowerKey key)
        {
            if (missingAttachedBindingKeys.Add(key))
            {
                AttachedMissingBindingCount++;
                MissingBindingCount++;
            }
        }

        private void CountMissingAttachedOwnerOnce(AttachedVfxFollowerKey key)
        {
            if (missingAttachedOwnerViewKeys.Add(key))
            {
                AttachedMissingOwnerViewCount++;
                MissingOwnerViewCount++;
            }
        }

        private bool IsMotionFollowerVisible(
            PresentationMotionTrack track,
            GameplayVfxVisibilityContext visibilityContext)
        {
            if (track == null)
            {
                return false;
            }

            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail);
            var sequenceId = track.InstanceKey.CorrelationId != 0
                ? track.InstanceKey.CorrelationId
                : track.EntityId;
            var request = new GameplayVfxRequest(
                tickIndex: 0,
                sequenceId: sequenceId,
                presentationSeed: track.InstanceKey.CorrelationId,
                sourceEntityId: track.EntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForEntity(track.EntityId, VfxAnchorSlot.EntityCenter),
                timing: VfxTimingKind.DuringMotion,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);
            var hasPolicy = activeMotionPoliciesByKey.TryGetValue(track.InstanceKey, out var policy);
            return EvaluateVisibility(
                request,
                hasPolicy,
                policy,
                visibilityContext).IsVisible;
        }

        private bool IsAttachedFollowerVisible(
            AttachedVfxFollowerKey key,
            GameplayVfxVisibilityContext visibilityContext)
        {
            var request = new GameplayVfxRequest(
                tickIndex: 0,
                sequenceId: key.SequenceId,
                presentationSeed: key.SequenceId,
                sourceEntityId: key.SourceEntityId,
                cueId: key.CueId,
                anchor: VfxAnchor.ForEntity(key.SourceEntityId, VfxAnchorSlot.EntityCenter),
                timing: VfxTimingKind.DuringMotion,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);
            var hasPolicy = activeAttachedPoliciesByKey.TryGetValue(key, out var policy);
            return EvaluateVisibility(
                request,
                hasPolicy,
                policy,
                visibilityContext).IsVisible;
        }

        private GameplayVfxVisibilityDecision EvaluateVisibility(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            in GameplayVfxVisibilityContext visibilityContext)
        {
            var resolvedPolicy = GameplayVfxVisibilityPolicy.ResolveBindingRuntimePolicy(request, policy);
            RecordVisibilityResolve(request, resolvedPolicy);
            return GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                resolvedPolicy,
                visibilityContext);
        }

        private GameplayVfxVisibilityDecision EvaluateVisibility(
            in GameplayVfxRequest request,
            bool hasBindingPolicy,
            VfxBindingRuntimePolicy policy,
            in GameplayVfxVisibilityContext visibilityContext)
        {
            var resolvedPolicy = GameplayVfxVisibilityPolicy.ResolveFinalPolicy(
                request,
                hasBindingPolicy,
                policy);
            RecordVisibilityResolve(request, resolvedPolicy);
            return GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                resolvedPolicy,
                visibilityContext);
        }

        private void ResetVisibilityResolveDiagnostics()
        {
            LastResolvedVisibilityPolicy = default;
            LastPresentationOnlyUsageDiagnostic = default;
            VisibilityBindingResolvedCount = 0;
            VisibilityFallbackDefaultCount = 0;
            PresentationOnlyAllowedTopologyHelperCount = 0;
            PresentationOnlyMisuseCandidateCount = 0;
        }

        private void RecordVisibilityResolve(
            in GameplayVfxRequest request,
            GameplayVfxResolvedVisibilityPolicy resolvedPolicy)
        {
            LastResolvedVisibilityPolicy = resolvedPolicy;
            if (resolvedPolicy.Source == GameplayVfxVisibilityPolicySource.BindingRuntimePolicy)
            {
                VisibilityBindingResolvedCount++;
            }
            else if (resolvedPolicy.Source == GameplayVfxVisibilityPolicySource.FallbackDefaultGameplay)
            {
                VisibilityFallbackDefaultCount++;
            }

            var presentationOnlyUsage = GameplayVfxVisibilityPolicy.ClassifyPresentationOnlyUsage(
                request,
                resolvedPolicy);
            if (!presentationOnlyUsage.IsPresentationOnly)
            {
                return;
            }

            LastPresentationOnlyUsageDiagnostic = presentationOnlyUsage;
            if (presentationOnlyUsage.Kind == GameplayVfxPresentationOnlyUsageKind.AllowedTopologyHelper)
            {
                PresentationOnlyAllowedTopologyHelperCount++;
            }
            else if (presentationOnlyUsage.Kind == GameplayVfxPresentationOnlyUsageKind.MisuseCandidate)
            {
                PresentationOnlyMisuseCandidateCount++;
            }
        }

        private bool TryResolveAttachParent(
            GameplayPresentationStateStore stateStore,
            int entityId,
            string attachPointId,
            AttachedVfxFollowerKey key,
            out Transform parent)
        {
            parent = null;
            if (stateStore == null ||
                !stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) ||
                view == null ||
                !view.isActiveAndEnabled ||
                !view.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(attachPointId))
            {
                if (view.TryGetVfxAttachPoint(attachPointId, out var attachPoint) &&
                    attachPoint != null)
                {
                    parent = attachPoint;
                    return true;
                }

                CountMissingExplicitAttachPointOnce(key, view, attachPointId);
                return false;
            }

            parent = view.ModelRoot != null ? view.ModelRoot : view.transform;
            return parent != null;
        }

        private void CountMissingExplicitAttachPointOnce(
            AttachedVfxFollowerKey key,
            GameplayEntityView view,
            string attachPointId)
        {
            if (!missingExplicitAttachPointKeys.Add(key))
            {
                return;
            }

            UnityEngine.Debug.LogWarning(
                $"VFX attach point '{attachPointId.Trim()}' not found on entity view '{view.name}'. Cue='{FormatCue(key.CueId)}'. The follower will not be spawned.",
                view);
        }

        private static string FormatCue(GameplayVfxCueId cueId)
        {
            if (cueId.Family == GameplayVfxFamily.Enemy &&
                Enum.IsDefined(typeof(EnemyVfxCue), cueId.Code))
            {
                return ((EnemyVfxCue)cueId.Code).ToString();
            }

            return cueId.ToString();
        }

        private void PruneMissingKeyState()
        {
            missingMotionBindingKeys.RemoveWhere(key => !desiredMotionKeys.Contains(key));
            missingMotionOwnerViewKeys.RemoveWhere(key => !desiredMotionKeys.Contains(key));
            missingAttachedBindingKeys.RemoveWhere(key => !desiredAttachedKeys.Contains(key));
            missingAttachedOwnerViewKeys.RemoveWhere(key => !desiredAttachedKeys.Contains(key));
            missingExplicitAttachPointKeys.RemoveWhere(key => !desiredAttachedKeys.Contains(key));
        }

        private void ClearMissingKeyState()
        {
            desiredMotionKeys.Clear();
            desiredAttachedKeys.Clear();
            missingMotionBindingKeys.Clear();
            missingMotionOwnerViewKeys.Clear();
            missingAttachedBindingKeys.Clear();
            missingAttachedOwnerViewKeys.Clear();
            missingExplicitAttachPointKeys.Clear();
            motionStopBuffer.Clear();
            attachedStopBuffer.Clear();
            explicitAttachedStopKeys.Clear();
        }

        private static bool IsHandleLive(IVfxPlaybackHandle handle)
        {
            return handle != null &&
                   (handle.State == VfxLifetimeState.Spawned ||
                    handle.State == VfxLifetimeState.Active);
        }
    }
}
