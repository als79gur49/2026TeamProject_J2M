using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class PresentationMotionFollowingVfxController
    {
        private readonly Dictionary<PresentationMotionInstanceKey, IVfxPlaybackHandle> activeHandlesByKey = new();
        private readonly HashSet<PresentationMotionInstanceKey> desiredKeys = new();
        private readonly HashSet<PresentationMotionInstanceKey> missingBindingKeys = new();
        private readonly HashSet<PresentationMotionInstanceKey> missingOwnerViewKeys = new();
        private readonly List<PresentationMotionInstanceKey> stopBuffer = new();

        public int ActiveHandleCount => activeHandlesByKey.Count;

        public int MissingBindingCount { get; private set; }

        public int MissingOwnerViewCount { get; private set; }

        public int PlannedAttachCount { get; private set; }

        public void Refresh(
            int tickIndex,
            GameplayPresentationTrackState trackState,
            GameplayPresentationStateStore stateStore,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            bool enabled)
        {
            PlannedAttachCount = 0;
            if (!enabled)
            {
                StopAll(tail: true);
                ClearMissingKeyState();
                return;
            }

            if (trackState == null || stateStore == null || pool == null || bindingResolver == null)
            {
                StopAll(tail: true);
                ClearMissingKeyState();
                return;
            }

            desiredKeys.Clear();
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
                desiredKeys.Add(key);
                if (activeHandlesByKey.TryGetValue(key, out var existingHandle) &&
                    IsHandleLive(existingHandle))
                {
                    if (!TryResolveAttachParent(stateStore, track.EntityId, out _))
                    {
                        CountMissingOwnerOnce(key);
                        Stop(key, tail: true);
                    }

                    continue;
                }

                activeHandlesByKey.Remove(key);
                if (!TryResolveAttachParent(stateStore, track.EntityId, out var parent))
                {
                    CountMissingOwnerOnce(key);
                    continue;
                }

                if (TryStartAttached(tickIndex, track, parent, pool, bindingResolver, out var handle))
                {
                    activeHandlesByKey[key] = handle;
                    missingBindingKeys.Remove(key);
                    missingOwnerViewKeys.Remove(key);
                    PlannedAttachCount++;
                }
            }

            StopStaleHandles();
            PruneMissingKeyState();
        }

        public void ResetSession()
        {
            StopAll(tail: false);
            MissingBindingCount = 0;
            MissingOwnerViewCount = 0;
            PlannedAttachCount = 0;
            ClearMissingKeyState();
        }

        public void HardCleanup()
        {
            foreach (var pair in activeHandlesByKey)
            {
                pair.Value?.HardCleanup();
            }

            activeHandlesByKey.Clear();
            ClearMissingKeyState();
            PlannedAttachCount = 0;
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
                CountMissingBindingOnce(track.InstanceKey);
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
            handle = pool.PlayAttachedTransient(command, parent);
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

        private void StopStaleHandles()
        {
            stopBuffer.Clear();
            foreach (var pair in activeHandlesByKey)
            {
                if (!desiredKeys.Contains(pair.Key) ||
                    !IsHandleLive(pair.Value))
                {
                    stopBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < stopBuffer.Count; i++)
            {
                Stop(stopBuffer[i], tail: true);
            }
        }

        private void StopAll(bool tail)
        {
            stopBuffer.Clear();
            foreach (var pair in activeHandlesByKey)
            {
                stopBuffer.Add(pair.Key);
            }

            for (var i = 0; i < stopBuffer.Count; i++)
            {
                Stop(stopBuffer[i], tail);
            }
        }

        private void Stop(PresentationMotionInstanceKey key, bool tail)
        {
            if (!activeHandlesByKey.TryGetValue(key, out var handle))
            {
                return;
            }

            activeHandlesByKey.Remove(key);
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

        private void CountMissingBindingOnce(PresentationMotionInstanceKey key)
        {
            if (missingBindingKeys.Add(key))
            {
                MissingBindingCount++;
            }
        }

        private void CountMissingOwnerOnce(PresentationMotionInstanceKey key)
        {
            if (missingOwnerViewKeys.Add(key))
            {
                MissingOwnerViewCount++;
            }
        }

        private void PruneMissingKeyState()
        {
            missingBindingKeys.RemoveWhere(key => !desiredKeys.Contains(key));
            missingOwnerViewKeys.RemoveWhere(key => !desiredKeys.Contains(key));
        }

        private void ClearMissingKeyState()
        {
            desiredKeys.Clear();
            missingBindingKeys.Clear();
            missingOwnerViewKeys.Clear();
            stopBuffer.Clear();
        }

        private static bool IsHandleLive(IVfxPlaybackHandle handle)
        {
            return handle != null &&
                   handle.State != VfxLifetimeState.ReleasedToPool &&
                   handle.State != VfxLifetimeState.HardCleanup;
        }
    }
}
