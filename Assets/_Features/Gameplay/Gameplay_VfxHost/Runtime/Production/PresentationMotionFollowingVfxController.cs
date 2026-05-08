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
        private readonly HashSet<PresentationMotionInstanceKey> desiredMotionKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> desiredAttachedKeys = new();
        private readonly HashSet<PresentationMotionInstanceKey> missingMotionBindingKeys = new();
        private readonly HashSet<PresentationMotionInstanceKey> missingMotionOwnerViewKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> missingAttachedBindingKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> missingAttachedOwnerViewKeys = new();
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

        public void Refresh(
            int tickIndex,
            GameplayPresentationTrackState trackState,
            GameplayPresentationStateStore stateStore,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            bool enabled,
            IReadOnlyList<AttachedVfxFollowerDesiredState> attachedDesiredStates = null,
            bool attachedFollowersEnabled = false)
        {
            PlannedAttachCount = 0;

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
                    bindingResolver);
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
                    stateStore,
                    pool,
                    bindingResolver);
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
            ClearMissingKeyState();
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
            IVfxBindingResolver bindingResolver)
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
                desiredMotionKeys.Add(key);
                if (activeMotionHandlesByKey.TryGetValue(key, out var existingHandle) &&
                    IsHandleLive(existingHandle))
                {
                    if (!TryResolveAttachParent(stateStore, track.EntityId, out _))
                    {
                        CountMissingMotionOwnerOnce(key);
                        StopMotion(key, tail: true);
                    }

                    continue;
                }

                activeMotionHandlesByKey.Remove(key);
                if (!TryResolveAttachParent(stateStore, track.EntityId, out var parent))
                {
                    CountMissingMotionOwnerOnce(key);
                    continue;
                }

                if (TryStartAttached(tickIndex, track, parent, pool, bindingResolver, out var handle))
                {
                    activeMotionHandlesByKey[key] = handle;
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
            out IVfxPlaybackHandle handle)
        {
            handle = null;
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

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                CountMissingMotionBindingOnce(track.InstanceKey);
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match FlipImpactStayTrail request cue.");
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
                view == null)
            {
                return false;
            }

            parent = view.ModelRoot != null ? view.ModelRoot : view.transform;
            return parent != null;
        }

        private void RefreshAttachedFollowers(
            int tickIndex,
            IReadOnlyList<AttachedVfxFollowerDesiredState> attachedDesiredStates,
            GameplayPresentationStateStore stateStore,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver)
        {
            desiredAttachedKeys.Clear();
            if (attachedDesiredStates != null)
            {
                for (var i = 0; i < attachedDesiredStates.Count; i++)
                {
                    var desiredState = attachedDesiredStates[i];
                    var key = desiredState.Key;
                    desiredAttachedKeys.Add(key);
                    if (activeAttachedHandlesByKey.TryGetValue(key, out var existingHandle) &&
                        IsHandleLive(existingHandle))
                    {
                        if (!TryResolveAttachParent(stateStore, desiredState.SourceEntityId, out _))
                        {
                            CountMissingAttachedOwnerOnce(key);
                            StopAttached(key, tail: true);
                        }

                        continue;
                    }

                    activeAttachedHandlesByKey.Remove(key);
                    if (!TryResolveAttachParent(stateStore, desiredState.SourceEntityId, out var parent))
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
                            out var handle))
                    {
                        activeAttachedHandlesByKey[key] = handle;
                        missingAttachedBindingKeys.Remove(key);
                        missingAttachedOwnerViewKeys.Remove(key);
                        PlannedAttachCount++;
                    }
                }
            }

            StopStaleAttachedHandles();
        }

        private bool TryStartAttachedFollower(
            int tickIndex,
            in AttachedVfxFollowerDesiredState desiredState,
            Transform parent,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            out IVfxPlaybackHandle handle)
        {
            handle = null;
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
            if (!bindingResolver.TryResolve(request, out var policy))
            {
                CountMissingAttachedBindingOnce(key);
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match attached follower request cue.");
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

        private void StopStaleAttachedHandles()
        {
            attachedStopBuffer.Clear();
            foreach (var pair in activeAttachedHandlesByKey)
            {
                if (!desiredAttachedKeys.Contains(pair.Key) ||
                    !IsHandleLive(pair.Value))
                {
                    attachedStopBuffer.Add(pair.Key);
                }
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
            StopHandle(handle, tail);
        }

        private void StopAttached(AttachedVfxFollowerKey key, bool tail)
        {
            if (!activeAttachedHandlesByKey.TryGetValue(key, out var handle))
            {
                return;
            }

            activeAttachedHandlesByKey.Remove(key);
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

        private void PruneMissingKeyState()
        {
            missingMotionBindingKeys.RemoveWhere(key => !desiredMotionKeys.Contains(key));
            missingMotionOwnerViewKeys.RemoveWhere(key => !desiredMotionKeys.Contains(key));
            missingAttachedBindingKeys.RemoveWhere(key => !desiredAttachedKeys.Contains(key));
            missingAttachedOwnerViewKeys.RemoveWhere(key => !desiredAttachedKeys.Contains(key));
        }

        private void ClearMissingKeyState()
        {
            desiredMotionKeys.Clear();
            desiredAttachedKeys.Clear();
            missingMotionBindingKeys.Clear();
            missingMotionOwnerViewKeys.Clear();
            missingAttachedBindingKeys.Clear();
            missingAttachedOwnerViewKeys.Clear();
            motionStopBuffer.Clear();
            attachedStopBuffer.Clear();
        }

        private static bool IsHandleLive(IVfxPlaybackHandle handle)
        {
            return handle != null &&
                   handle.State != VfxLifetimeState.ReleasedToPool &&
                   handle.State != VfxLifetimeState.HardCleanup;
        }
    }
}
