using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class GameplayForwardCellProjectileVfxController
    {
        private const float DefaultMinFlightDurationSeconds = 0.05f;
        private const float DefaultArcHeight = 0.35f;

        private static readonly string[] SourceSocketNames =
        {
            "Muzzle",
            "ProjectileSocket",
            "Eye",
        };

        private readonly Dictionary<int, IVfxPlaybackHandle> markerHandlesByKey = new();
        private readonly Dictionary<int, ActiveFlight> activeFlightsByKey = new();

        public int MissingBindingCount { get; private set; }

        public int MissingAnchorCount { get; private set; }

        public int MissingSourceFallbackCount { get; private set; }

        public int PlayedThisTickCount { get; private set; }

        internal int ActiveMarkerCount => markerHandlesByKey.Count;

        internal int ActiveFlightCount => activeFlightsByKey.Count;

        internal int[] ActiveMarkerKeys
        {
            get
            {
                var keys = new int[markerHandlesByKey.Count];
                markerHandlesByKey.Keys.CopyTo(keys, 0);
                return keys;
            }
        }

        internal int[] ActiveFlightKeys
        {
            get
            {
                var keys = new int[activeFlightsByKey.Count];
                activeFlightsByKey.Keys.CopyTo(keys, 0);
                return keys;
            }
        }

        public void ResetSession(GameplayVfxGameObjectPool pool = null)
        {
            Cleanup(pool, hard: false);
            MissingBindingCount = 0;
            MissingAnchorCount = 0;
            MissingSourceFallbackCount = 0;
            PlayedThisTickCount = 0;
        }

        public void HardCleanup(GameplayVfxGameObjectPool pool = null)
        {
            Cleanup(pool, hard: true);
            PlayedThisTickCount = 0;
        }

        public void Present(
            in GameplayTickPresentationExtensionContext context,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver)
        {
            PlayedThisTickCount = 0;
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return;
            }

            var cellProjector = new GameplayVfxHostCellAnchorProjector(context.Projector);

            var windupSignals = presentationData.ForwardCellProjectileWindupSignals;
            for (var i = 0; i < windupSignals.Count; i++)
            {
                PresentWindup(
                    context.Result.TickIndex,
                    context.Topology,
                    windupSignals[i],
                    pool,
                    bindingResolver,
                    cellProjector);
            }

            var releaseSignals = presentationData.ForwardCellProjectileReleaseSignals;
            for (var i = 0; i < releaseSignals.Count; i++)
            {
                PresentRelease(
                    context,
                    releaseSignals[i],
                    pool,
                    bindingResolver,
                    cellProjector);
            }

            var impactSignals = presentationData.ForwardCellImpactSignals;
            for (var i = 0; i < impactSignals.Count; i++)
            {
                PresentImpact(
                    context.Result.TickIndex,
                    context.Topology,
                    impactSignals[i],
                    pool,
                    bindingResolver,
                    cellProjector);
            }

            var clearSignals = presentationData.ForwardCellProjectileClearSignals;
            for (var i = 0; i < clearSignals.Count; i++)
            {
                ReleaseMarker(clearSignals[i].PresentationKey, pool);
                ReleaseFlight(clearSignals[i].PresentationKey, pool);
            }
        }

        public void Update(float deltaSeconds, GameplayVfxGameObjectPool pool)
        {
            if (activeFlightsByKey.Count == 0)
            {
                return;
            }

            var advance = Mathf.Max(0f, deltaSeconds);
            var completedKeys = new List<int>();
            var updatedFlights = new List<KeyValuePair<int, ActiveFlight>>();
            foreach (var pair in activeFlightsByKey)
            {
                var flight = pair.Value.Advance(advance);
                flight.TryApplyPose();

                if (flight.IsComplete)
                {
                    completedKeys.Add(pair.Key);
                }
                else
                {
                    updatedFlights.Add(new KeyValuePair<int, ActiveFlight>(pair.Key, flight));
                }
            }

            for (var i = 0; i < updatedFlights.Count; i++)
            {
                activeFlightsByKey[updatedFlights[i].Key] = updatedFlights[i].Value;
            }

            for (var i = 0; i < completedKeys.Count; i++)
            {
                ReleaseFlight(completedKeys[i], pool);
            }
        }

        private void PresentWindup(
            int tickIndex,
            CubeTopologyState topology,
            in TickForwardCellProjectileWindupPresentationSignal signal,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxHostCellAnchorProjector cellProjector)
        {
            if (markerHandlesByKey.ContainsKey(signal.PresentationKey))
            {
                return;
            }

            var cueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellDangerMarker);
            var request = new GameplayVfxRequest(
                tickIndex,
                signal.PresentationKey,
                signal.PresentationKey,
                signal.SourceEnemyId,
                cueId,
                VfxAnchor.ForCell(signal.TargetCell, topology, VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    entityId: signal.SourceEnemyId,
                    cell: signal.TargetCell,
                    hasCell: true,
                    activationSequence: signal.PresentationKey));

            if (!TryResolveCommand(request, bindingResolver, cellProjector, out var command))
            {
                return;
            }

            var handle = pool.StartPersistent(command);
            if (handle != null)
            {
                markerHandlesByKey[signal.PresentationKey] = handle;
                PlayedThisTickCount++;
            }
        }

        private void PresentRelease(
            in GameplayTickPresentationExtensionContext context,
            in TickForwardCellProjectileReleasePresentationSignal signal,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxHostCellAnchorProjector cellProjector)
        {
            if (markerHandlesByKey.TryGetValue(signal.PresentationKey, out var marker) &&
                marker is GameplayVfxPlaybackHandle markerHandle &&
                markerHandle.InstanceTransform != null)
            {
                markerHandle.InstanceTransform.localScale = Vector3.one * 1.15f;
            }

            ReleaseFlight(signal.PresentationKey, pool);

            if (!TryResolveCellLocalPosition(
                    cellProjector,
                    signal.TargetCell,
                    context.Topology,
                    out var targetLocalPosition,
                    out var targetLocalRotation))
            {
                MissingAnchorCount++;
                return;
            }

            var sourceLocalPosition = ResolveSourceLocalPosition(
                context.StateStore,
                context.Projector,
                context.Topology,
                signal,
                targetLocalPosition);
            var cueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileFlight);
            var request = new GameplayVfxRequest(
                context.Result.TickIndex,
                signal.PresentationKey,
                signal.PresentationKey,
                signal.SourceEnemyId,
                cueId,
                VfxAnchor.ForCell(signal.SourceCell, context.Topology, VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                MissingBindingCount++;
                return;
            }

            policy.ValidateOrThrow();
            var sourceAnchor = VfxResolvedAnchor.ForCell(
                signal.SourceCell,
                context.Topology,
                VfxAnchorSlot.CellCenter,
                sourceLocalPosition,
                targetLocalRotation);
            var command = new ResolvedVfxPlaybackCommand(request, policy, sourceAnchor);
            var handle = pool.PlayAttachedTransient(command, null, controllerManagedLifetime: true);
            if (handle is GameplayVfxPlaybackHandle playbackHandle &&
                playbackHandle.InstanceTransform != null)
            {
                activeFlightsByKey[signal.PresentationKey] = new ActiveFlight(
                    playbackHandle,
                    sourceLocalPosition,
                    targetLocalPosition,
                    ResolveFlightDurationSeconds(signal, context.TimingProfile),
                    DefaultArcHeight);
                PlayedThisTickCount++;
            }
        }

        private void PresentImpact(
            int tickIndex,
            CubeTopologyState topology,
            in TickForwardCellImpactPresentationSignal signal,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxHostCellAnchorProjector cellProjector)
        {
            ReleaseMarker(signal.PresentationKey, pool);
            ReleaseFlight(signal.PresentationKey, pool);

            var cueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact);
            var request = new GameplayVfxRequest(
                tickIndex,
                signal.PresentationKey,
                signal.PresentationKey,
                signal.SourceEnemyId,
                cueId,
                VfxAnchor.ForCell(signal.TargetCell, topology, VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!TryResolveCommand(request, bindingResolver, cellProjector, out var command))
            {
                return;
            }

            if (pool.PlayTransient(command) != null)
            {
                PlayedThisTickCount++;
            }
        }

        private bool TryResolveCommand(
            in GameplayVfxRequest request,
            IVfxBindingResolver bindingResolver,
            GameplayVfxHostCellAnchorProjector cellProjector,
            out ResolvedVfxPlaybackCommand command)
        {
            if (!bindingResolver.TryResolve(request, out var policy))
            {
                MissingBindingCount++;
                command = default;
                return false;
            }

            policy.ValidateOrThrow();
            if (!cellProjector.TryResolveCell(
                    request.Anchor.Cell,
                    request.Anchor.Topology,
                    request.Anchor.Slot,
                    out var anchor) ||
                !anchor.IsResolved)
            {
                MissingAnchorCount++;
                command = default;
                return false;
            }

            command = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return true;
        }

        private Vector3 ResolveSourceLocalPosition(
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            CubeTopologyState topology,
            in TickForwardCellProjectileReleasePresentationSignal signal,
            Vector3 targetFallback)
        {
            if (stateStore != null &&
                stateStore.ViewsByEntityId.TryGetValue(signal.SourceEnemyId, out var view) &&
                view != null)
            {
                if (TryFindNamedSocket(view.transform, out var socket))
                {
                    return ToPresentationLocal(view.transform, socket.position);
                }

                MissingSourceFallbackCount++;
                var sourceTransform = view.ModelRoot != null ? view.ModelRoot : view.transform;
                return ToPresentationLocal(view.transform, sourceTransform.position);
            }

            var cellProjector = new GameplayVfxHostCellAnchorProjector(projector);
            if (TryResolveCellLocalPosition(
                    cellProjector,
                    signal.SourceCell,
                    topology,
                    out var sourceLocalPosition,
                    out _))
            {
                MissingSourceFallbackCount++;
                return sourceLocalPosition;
            }

            MissingSourceFallbackCount++;
            return targetFallback;
        }

        private static Vector3 ToPresentationLocal(Transform viewTransform, Vector3 worldPosition)
        {
            var parent = viewTransform != null ? viewTransform.parent : null;
            return parent != null
                ? parent.InverseTransformPoint(worldPosition)
                : worldPosition;
        }

        private static bool TryFindNamedSocket(Transform root, out Transform socket)
        {
            for (var i = 0; i < SourceSocketNames.Length; i++)
            {
                socket = FindChildRecursive(root, SourceSocketNames[i]);
                if (socket != null)
                {
                    return true;
                }
            }

            socket = null;
            return false;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool TryResolveCellLocalPosition(
            GameplayVfxHostCellAnchorProjector cellProjector,
            SurfaceCell cell,
            CubeTopologyState topology,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            if (cellProjector.TryResolveCell(
                    cell,
                    topology,
                    VfxAnchorSlot.CellCenter,
                    out var anchor) &&
                anchor.IsResolved &&
                anchor.HasLocalPose)
            {
                localPosition = anchor.LocalPosition;
                localRotation = anchor.LocalRotation;
                return true;
            }

            localPosition = default;
            localRotation = Quaternion.identity;
            return false;
        }

        private static float ResolveFlightDurationSeconds(
            in TickForwardCellProjectileReleasePresentationSignal signal,
            GameplayTimingProfile timingProfile)
        {
            var ticksPerSecond = timingProfile != null && timingProfile.SimulationTicksPerSecond > 0
                ? timingProfile.SimulationTicksPerSecond
                : GameplayTimingProfile.DefaultSimulationTicksPerSecond;
            var tickDuration = signal.ImpactDelayTicks / (float)ticksPerSecond;
            return Mathf.Max(DefaultMinFlightDurationSeconds, tickDuration);
        }

        private void ReleaseMarker(int presentationKey, GameplayVfxGameObjectPool pool)
        {
            if (!markerHandlesByKey.Remove(presentationKey, out var handle))
            {
                return;
            }

            pool?.Release(handle);
        }

        private void ReleaseFlight(int presentationKey, GameplayVfxGameObjectPool pool)
        {
            if (!activeFlightsByKey.Remove(presentationKey, out var flight))
            {
                return;
            }

            pool?.Release(flight.Handle);
        }

        private void Cleanup(GameplayVfxGameObjectPool pool, bool hard)
        {
            foreach (var pair in markerHandlesByKey)
            {
                if (hard)
                {
                    pair.Value?.HardCleanup();
                }
                else
                {
                    pool?.Release(pair.Value);
                }
            }

            foreach (var pair in activeFlightsByKey)
            {
                if (hard)
                {
                    pair.Value.Handle?.HardCleanup();
                }
                else
                {
                    pool?.Release(pair.Value.Handle);
                }
            }

            markerHandlesByKey.Clear();
            activeFlightsByKey.Clear();
        }

        private readonly struct ActiveFlight
        {
            public ActiveFlight(
                GameplayVfxPlaybackHandle handle,
                Vector3 source,
                Vector3 target,
                float duration,
                float arcHeight)
            {
                Handle = handle;
                Source = source;
                Target = target;
                Duration = Mathf.Max(DefaultMinFlightDurationSeconds, duration);
                ArcHeight = arcHeight;
                Elapsed = 0f;
            }

            public GameplayVfxPlaybackHandle Handle { get; }

            private Vector3 Source { get; }

            private Vector3 Target { get; }

            private float Duration { get; }

            private float ArcHeight { get; }

            private float Elapsed { get; }

            public bool IsComplete => Elapsed >= Duration;

            public ActiveFlight Advance(float deltaSeconds)
            {
                return new ActiveFlight(Handle, Source, Target, Duration, ArcHeight, Mathf.Min(Duration, Elapsed + deltaSeconds));
            }

            private ActiveFlight(
                GameplayVfxPlaybackHandle handle,
                Vector3 source,
                Vector3 target,
                float duration,
                float arcHeight,
                float elapsed)
            {
                Handle = handle;
                Source = source;
                Target = target;
                Duration = duration;
                ArcHeight = arcHeight;
                Elapsed = elapsed;
            }

            public bool TryApplyPose()
            {
                var transform = Handle != null ? Handle.InstanceTransform : null;
                if (transform == null)
                {
                    return false;
                }

                var t = Mathf.Clamp01(Duration <= 0f ? 1f : Elapsed / Duration);
                var control = (Source + Target) * 0.5f + Vector3.up * ArcHeight;
                var a = Vector3.Lerp(Source, control, t);
                var b = Vector3.Lerp(control, Target, t);
                var position = Vector3.Lerp(a, b, t);
                var tangent = (b - a).sqrMagnitude > 0.0001f ? (b - a).normalized : Vector3.forward;
                transform.localPosition = position;
                transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
                return true;
            }
        }
    }
}
