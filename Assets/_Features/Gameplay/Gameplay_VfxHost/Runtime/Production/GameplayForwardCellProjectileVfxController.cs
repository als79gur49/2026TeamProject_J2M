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
        private const float DefaultArcHeight = 0.8f;
        private const float FlightEaseInBlend = 0.25f;

        private static readonly string[] SourceSocketNames =
        {
            "Muzzle",
            "ProjectileSocket",
            "Eye",
        };

        private readonly Dictionary<int, IVfxPlaybackHandle> markerHandlesByKey = new();
        private readonly Dictionary<int, ActiveFlight> activeFlightsByKey = new();
        private readonly HashSet<string> seenImpactCommandKeys = new();

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
            seenImpactCommandKeys.Clear();
        }

        public void HardCleanup(GameplayVfxGameObjectPool pool = null)
        {
            Cleanup(pool, hard: true);
            PlayedThisTickCount = 0;
        }

        public void ClearForTopologyTransitionStart(GameplayVfxGameObjectPool pool = null)
        {
            foreach (var pair in markerHandlesByKey)
            {
                StopForTopologyTransition(pair.Value, pool);
            }

            foreach (var pair in activeFlightsByKey)
            {
                StopForTopologyTransition(pair.Value.Handle, pool);
                StopForTopologyTransition(pair.Value.FollowHandle, pool);
            }

            markerHandlesByKey.Clear();
            activeFlightsByKey.Clear();
            PlayedThisTickCount = 0;
        }

        public void Present(
            in GameplayTickPresentationExtensionContext context,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxVisibilityContext visibilityContext)
        {
            PlayedThisTickCount = 0;
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return;
            }

            var cellProjector = new GameplayVfxHostCellAnchorProjector(context.Projector);
            ForwardCellProjectileDebugLog.Log(
                "VFX_CONTROLLER_INPUT",
                $"Tick={context.Result.TickIndex} ArrivalSignals={presentationData.ForwardCellProjectileArrivalSignals.Count} " +
                $"HitSignals={presentationData.ForwardCellImpactSignals.Count} " +
                $"ReleaseSignals={presentationData.ForwardCellProjectileReleaseSignals.Count}");

            var windupSignals = presentationData.ForwardCellProjectileWindupSignals;
            for (var i = 0; i < windupSignals.Count; i++)
            {
                ReleaseMarker(windupSignals[i].PresentationKey, pool);
            }

            var releaseSignals = presentationData.ForwardCellProjectileReleaseSignals;
            for (var i = 0; i < releaseSignals.Count; i++)
            {
                PresentRelease(
                    context,
                    releaseSignals[i],
                    pool,
                    bindingResolver,
                    cellProjector,
                    visibilityContext);
            }

            var arrivalSignals = presentationData.ForwardCellProjectileArrivalSignals;
            for (var i = 0; i < arrivalSignals.Count; i++)
            {
                var shotKey = ForwardCellProjectileDebugLog.BuildShotKey(
                    arrivalSignals[i].SourceEnemyId,
                    arrivalSignals[i].TargetCell,
                    arrivalSignals[i].ImpactTick,
                    arrivalSignals[i].ImpactId,
                    arrivalSignals[i].PresentationKey);
                ForwardCellProjectileDebugLog.Log(
                    "VFX_CONTROLLER_INPUT",
                    $"Tick={context.Result.TickIndex} Shot={shotKey} " +
                    $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(arrivalSignals[i].TargetCell)}) " +
                    $"ResolutionKind={arrivalSignals[i].ResolutionKind} " +
                    $"ImpactId={arrivalSignals[i].ImpactId} PresentationKey={arrivalSignals[i].PresentationKey}");
                ForwardCellProjectileDebugLog.MarkController(
                    shotKey,
                    arrivalSignalCount: arrivalSignals.Count,
                    hitSignalCount: presentationData.ForwardCellImpactSignals.Count,
                    releaseSignalCount: releaseSignals.Count);

                PresentImpact(
                    context.Result.TickIndex,
                    context.Topology,
                    arrivalSignals[i],
                    pool,
                    bindingResolver,
                    cellProjector,
                    visibilityContext);
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
            GameplayVfxHostCellAnchorProjector cellProjector,
            GameplayVfxVisibilityContext visibilityContext)
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

            if (!TryResolveCommand(request, bindingResolver, cellProjector, visibilityContext, out var command))
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
            GameplayVfxHostCellAnchorProjector cellProjector,
            GameplayVfxVisibilityContext visibilityContext)
        {
            ReleaseMarker(signal.PresentationKey, pool);
            ReleaseFlight(signal.PresentationKey, pool);

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
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: false, flightHandleCreated: false, "MissingBinding");
                return;
            }

            policy.ValidateOrThrow();
            var sourceDecision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                policy,
                visibilityContext);
            if (!sourceDecision.IsVisible)
            {
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: false, flightHandleCreated: false, $"SourceVisibility:{sourceDecision.BlockReason}");
                return;
            }

            var targetRequest = new GameplayVfxRequest(
                context.Result.TickIndex,
                signal.PresentationKey,
                signal.PresentationKey,
                signal.SourceEnemyId,
                cueId,
                VfxAnchor.ForCell(signal.TargetCell, context.Topology, VfxAnchorSlot.CellFloor),
                VfxTimingKind.ImmediateOnTickPresentation);
            var targetDecision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                targetRequest,
                policy,
                visibilityContext);
            if (!targetDecision.IsVisible)
            {
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: false, flightHandleCreated: false, $"TargetVisibility:{targetDecision.BlockReason}");
                return;
            }

            if (!TryResolveCellLocalPosition(
                    cellProjector,
                    signal.TargetCell,
                    context.Topology,
                    VfxAnchorSlot.CellFloor,
                    policy.VisibilityMode,
                    out var targetLocalPosition,
                    out _))
            {
                MissingAnchorCount++;
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: false, flightHandleCreated: false, "TargetFloorAnchorMissing");
                return;
            }

            if (!TryResolveCellLocalPosition(
                    cellProjector,
                    signal.TargetCell,
                    context.Topology,
                    VfxAnchorSlot.CellCenter,
                    policy.VisibilityMode,
                    out var targetCenterLocalPosition,
                    out var targetCenterLocalRotation))
            {
                MissingAnchorCount++;
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: false, flightHandleCreated: false, "TargetCenterAnchorMissing");
                return;
            }

            var sourceLocalPosition = ResolveSourceLocalPosition(
                context.StateStore,
                context.Projector,
                context.Topology,
                signal,
                targetCenterLocalPosition,
                policy.VisibilityMode);
            PlayActiveOneShot(
                context,
                signal,
                pool,
                bindingResolver,
                sourceLocalPosition,
                targetCenterLocalRotation,
                visibilityContext);

            var sourceAnchor = VfxResolvedAnchor.ForCell(
                signal.SourceCell,
                context.Topology,
                VfxAnchorSlot.CellCenter,
                sourceLocalPosition,
                targetCenterLocalRotation);
            var postDecision = GameplayVfxVisibilityPolicy.EvaluateAfterAnchor(
                request,
                policy,
                sourceAnchor,
                visibilityContext);
            if (!postDecision.IsVisible)
            {
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: false, flightHandleCreated: false, $"PostVisibility:{postDecision.BlockReason}");
                return;
            }

            var command = new ResolvedVfxPlaybackCommand(request, policy, sourceAnchor);
            var handle = pool.PlayAttachedTransient(command, null, controllerManagedLifetime: true);
            if (handle is GameplayVfxPlaybackHandle playbackHandle &&
                playbackHandle.InstanceTransform != null)
            {
                var followHandle = PlayFlightFollow(
                    context,
                    signal,
                    pool,
                    bindingResolver,
                    playbackHandle.InstanceTransform,
                    sourceLocalPosition,
                    targetCenterLocalRotation,
                    visibilityContext);
                activeFlightsByKey[signal.PresentationKey] = new ActiveFlight(
                    playbackHandle,
                    followHandle,
                    sourceLocalPosition,
                    targetLocalPosition,
                    ResolveArcLiftAxis(context.Projector, signal.SourceCell, signal.TargetCell, context.Topology),
                    ResolveFlightDurationSeconds(signal, context.TimingProfile),
                    DefaultArcHeight);
                PlayedThisTickCount++;
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: true, flightHandleCreated: true, string.Empty);
            }
            else
            {
                LogReleaseFlightVfx(context.Result.TickIndex, signal, flightCommandCreated: true, flightHandleCreated: false, "PoolReturnedNull");
            }
        }

        private void PresentImpact(
            int tickIndex,
            CubeTopologyState topology,
            in TickForwardCellProjectileArrivalPresentationSignal signal,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            GameplayVfxHostCellAnchorProjector cellProjector,
            GameplayVfxVisibilityContext visibilityContext)
        {
            if (!signal.ResolutionKind.IsValidArrival())
            {
                return;
            }

            ReleaseMarker(signal.PresentationKey, pool);
            ReleaseFlight(signal.PresentationKey, pool);

            var cueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact);
            var request = new GameplayVfxRequest(
                tickIndex,
                signal.PresentationKey,
                signal.PresentationKey,
                signal.SourceEnemyId,
                cueId,
                VfxAnchor.ForCell(signal.TargetCell, topology, VfxAnchorSlot.CellFloor),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);
            var shotKey = ForwardCellProjectileDebugLog.BuildShotKey(
                signal.SourceEnemyId,
                signal.TargetCell,
                signal.ImpactTick,
                signal.ImpactId,
                signal.PresentationKey);
            var commandKey = signal.PresentationKey;
            var dedupeKey = $"ForwardCellImpact|Src{signal.SourceEnemyId}|{ForwardCellProjectileDebugLog.FormatCell(signal.TargetCell)}|ImpactTick{signal.ImpactTick}|ImpactId{signal.ImpactId}|PresentationKey{signal.PresentationKey}";
            var sameKeySeenPreviously = seenImpactCommandKeys.Contains(dedupeKey);
            seenImpactCommandKeys.Add(dedupeKey);
            ForwardCellProjectileDebugLog.MarkCommand(shotKey, commandCreated: true);
            ForwardCellProjectileDebugLog.MarkDedupe(shotKey, skipped: false);
            ForwardCellProjectileDebugLog.Log(
                "VFX_IMPACT_COMMAND",
                $"Tick={tickIndex} Shot={shotKey} CommandCreated=true Cue=ForwardCellImpact " +
                $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(signal.TargetCell)}) Anchor=CellFloor " +
                $"CommandKey={commandKey} SkipReason=");
            ForwardCellProjectileDebugLog.Log(
                "VFX_DEDUPE",
                $"Tick={tickIndex} Shot={shotKey} Key={dedupeKey} " +
                $"SameKeySeenPreviously={sameKeySeenPreviously} Skipped=false PreviousShotKey=");

            if (!TryResolveCommand(request, bindingResolver, cellProjector, visibilityContext, out var command))
            {
                ForwardCellProjectileDebugLog.LogSummary(shotKey);
                return;
            }

            if (pool.PlayTransient(command) != null)
            {
                PlayedThisTickCount++;
            }
            else
            {
                ForwardCellProjectileDebugLog.MarkPool(shotKey, poolPlayCalled: true, handleCreated: false);
                ForwardCellProjectileDebugLog.LogSummary(shotKey);
            }
        }

        private bool TryResolveCommand(
            in GameplayVfxRequest request,
            IVfxBindingResolver bindingResolver,
            GameplayVfxHostCellAnchorProjector cellProjector,
            GameplayVfxVisibilityContext visibilityContext,
            out ResolvedVfxPlaybackCommand command)
        {
            var isForwardCellImpact = request.CueId == GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact);
            var shotKey = isForwardCellImpact
                ? ForwardCellProjectileDebugLog.BuildShotKey(
                    request.SourceEntityId,
                    request.Anchor.Cell,
                    request.TickIndex,
                    request.SequenceId,
                    request.SequenceId)
                : string.Empty;
            if (!bindingResolver.TryResolve(request, out var policy))
            {
                MissingBindingCount++;
                if (isForwardCellImpact)
                {
                    ForwardCellProjectileDebugLog.MarkResolve(shotKey, bindingResolved: false, anchorResolved: false);
                    ForwardCellProjectileDebugLog.Log(
                        "VFX_RESOLVE",
                        $"Tick={request.TickIndex} Shot={shotKey} Cue=ForwardCellImpact " +
                        "BindingResolved=false Binding=None AnchorResolved=false " +
                        $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(request.Anchor.Cell)}) " +
                        "AnchorWorldPosition=(0,0,0) TargetFaceActive=false VisibilitySkipped=false " +
                        "VisibilitySkipReason=MissingBinding SuppressedByPause=false SuppressedByTopology=false SuppressedByVisibility=false");
                }
                command = default;
                return false;
            }

            policy.ValidateOrThrow();
            var preDecision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                policy,
                visibilityContext);
            if (!preDecision.IsVisible)
            {
                if (isForwardCellImpact)
                {
                    ForwardCellProjectileDebugLog.MarkResolve(shotKey, bindingResolved: true, anchorResolved: false);
                    ForwardCellProjectileDebugLog.Log(
                        "VFX_RESOLVE",
                        $"Tick={request.TickIndex} Shot={shotKey} Cue=ForwardCellImpact " +
                        $"BindingResolved=true Binding={policy.CueId}:{policy.StyleKey} AnchorResolved=false " +
                        $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(request.Anchor.Cell)}) " +
                        "AnchorWorldPosition=(0,0,0) TargetFaceActive=false VisibilitySkipped=true " +
                        $"VisibilitySkipReason=PreAnchor:{preDecision.BlockReason} SuppressedByPause=false " +
                        "SuppressedByTopology=false SuppressedByVisibility=true");
                }
                command = default;
                return false;
            }

            if (!cellProjector.TryResolveCell(
                    request.Anchor.Cell,
                    request.Anchor.Topology,
                    request.Anchor.Slot,
                    policy.VisibilityMode,
                    out var anchor) ||
                !anchor.IsResolved)
            {
                MissingAnchorCount++;
                if (isForwardCellImpact)
                {
                    ForwardCellProjectileDebugLog.MarkResolve(shotKey, bindingResolved: true, anchorResolved: false);
                    ForwardCellProjectileDebugLog.Log(
                        "VFX_RESOLVE",
                        $"Tick={request.TickIndex} Shot={shotKey} Cue=ForwardCellImpact " +
                        $"BindingResolved=true Binding={policy.CueId}:{policy.StyleKey} AnchorResolved=false " +
                        $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(request.Anchor.Cell)}) " +
                        $"AnchorWorldPosition=(0,0,0) TargetFaceActive={request.Anchor.Topology.IsFaceActive(request.Anchor.Cell.face)} " +
                        "VisibilitySkipped=false VisibilitySkipReason=MissingAnchor SuppressedByPause=false " +
                        "SuppressedByTopology=false SuppressedByVisibility=false");
                }
                command = default;
                return false;
            }

            var postDecision = GameplayVfxVisibilityPolicy.EvaluateAfterAnchor(
                request,
                policy,
                anchor,
                visibilityContext);
            if (!postDecision.IsVisible)
            {
                if (isForwardCellImpact)
                {
                    ForwardCellProjectileDebugLog.MarkResolve(shotKey, bindingResolved: true, anchorResolved: true);
                    ForwardCellProjectileDebugLog.Log(
                        "VFX_RESOLVE",
                        $"Tick={request.TickIndex} Shot={shotKey} Cue=ForwardCellImpact " +
                        $"BindingResolved=true Binding={policy.CueId}:{policy.StyleKey} AnchorResolved=true " +
                        $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(request.Anchor.Cell)}) " +
                        $"AnchorWorldPosition={anchor.LocalPosition} TargetFaceActive={request.Anchor.Topology.IsFaceActive(request.Anchor.Cell.face)} " +
                        $"VisibilitySkipped=true VisibilitySkipReason=PostAnchor:{postDecision.BlockReason} " +
                        "SuppressedByPause=false SuppressedByTopology=false SuppressedByVisibility=true");
                }
                command = default;
                return false;
            }

            command = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            if (isForwardCellImpact)
            {
                ForwardCellProjectileDebugLog.MarkResolve(shotKey, bindingResolved: true, anchorResolved: true);
                ForwardCellProjectileDebugLog.Log(
                    "VFX_RESOLVE",
                    $"Tick={request.TickIndex} Shot={shotKey} Cue=ForwardCellImpact " +
                    $"BindingResolved=true Binding={policy.CueId}:{policy.StyleKey} AnchorResolved=true " +
                    $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(request.Anchor.Cell)}) " +
                    $"AnchorWorldPosition={anchor.LocalPosition} TargetFaceActive={request.Anchor.Topology.IsFaceActive(request.Anchor.Cell.face)} " +
                    "VisibilitySkipped=false VisibilitySkipReason= SuppressedByPause=false " +
                    "SuppressedByTopology=false SuppressedByVisibility=false");
            }
            return true;
        }

        private static void LogReleaseFlightVfx(
            int tickIndex,
            in TickForwardCellProjectileReleasePresentationSignal signal,
            bool flightCommandCreated,
            bool flightHandleCreated,
            string skipReason)
        {
            var shotKey = ForwardCellProjectileDebugLog.BuildShotKey(
                signal.SourceEnemyId,
                signal.TargetCell,
                signal.ImpactTick,
                signal.PresentationKey,
                signal.PresentationKey);
            ForwardCellProjectileDebugLog.Log(
                "RELEASE_FLIGHT_VFX",
                $"Tick={tickIndex} Shot={shotKey} Source={signal.SourceEnemyId} " +
                $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(signal.TargetCell)}) " +
                $"FlightSignalCreated=true FlightCommandCreated={flightCommandCreated} " +
                $"Cue=ForwardCellProjectileFlight FlightHandleCreated={flightHandleCreated} SkipReason={skipReason}");
        }

        private void PlayActiveOneShot(
            in GameplayTickPresentationExtensionContext context,
            in TickForwardCellProjectileReleasePresentationSignal signal,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            Vector3 sourceLocalPosition,
            Quaternion sourceLocalRotation,
            GameplayVfxVisibilityContext visibilityContext)
        {
            var cueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileActive);
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

            if (!TryResolveOptionalCommand(
                    request,
                    bindingResolver,
                    signal.SourceCell,
                    context.Topology,
                    VfxAnchorSlot.CellCenter,
                    sourceLocalPosition,
                    sourceLocalRotation,
                    visibilityContext,
                    out var command))
            {
                return;
            }

            if (pool.PlayTransient(command) != null)
            {
                PlayedThisTickCount++;
            }
        }

        private IVfxPlaybackHandle PlayFlightFollow(
            in GameplayTickPresentationExtensionContext context,
            in TickForwardCellProjectileReleasePresentationSignal signal,
            GameplayVfxGameObjectPool pool,
            IVfxBindingResolver bindingResolver,
            Transform parent,
            Vector3 sourceLocalPosition,
            Quaternion sourceLocalRotation,
            GameplayVfxVisibilityContext visibilityContext)
        {
            if (parent == null)
            {
                return null;
            }

            var cueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileFlightFollow);
            var request = new GameplayVfxRequest(
                context.Result.TickIndex,
                signal.PresentationKey,
                signal.PresentationKey,
                signal.SourceEnemyId,
                cueId,
                VfxAnchor.ForCell(signal.SourceCell, context.Topology, VfxAnchorSlot.CellCenter),
                VfxTimingKind.DuringMotion,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!TryResolveOptionalCommand(
                    request,
                    bindingResolver,
                    signal.SourceCell,
                    context.Topology,
                    VfxAnchorSlot.CellCenter,
                    sourceLocalPosition,
                    sourceLocalRotation,
                    visibilityContext,
                    out var command))
            {
                return null;
            }

            var handle = pool.PlayAttachedTransient(
                command,
                parent,
                controllerManagedLifetime: true);
            if (handle != null)
            {
                PlayedThisTickCount++;
            }

            return handle;
        }

        private static bool TryResolveOptionalCommand(
            in GameplayVfxRequest request,
            IVfxBindingResolver bindingResolver,
            SurfaceCell fallbackCell,
            CubeTopologyState topology,
            VfxAnchorSlot slot,
            Vector3 localPosition,
            Quaternion localRotation,
            GameplayVfxVisibilityContext visibilityContext,
            out ResolvedVfxPlaybackCommand command)
        {
            command = default;
            if (!bindingResolver.TryResolve(request, out var policy))
            {
                return false;
            }

            policy.ValidateOrThrow();
            var preDecision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                policy,
                visibilityContext);
            if (!preDecision.IsVisible)
            {
                return false;
            }

            var anchor = VfxResolvedAnchor.ForCell(
                fallbackCell,
                topology,
                slot,
                localPosition,
                localRotation);
            var postDecision = GameplayVfxVisibilityPolicy.EvaluateAfterAnchor(
                request,
                policy,
                anchor,
                visibilityContext);
            if (!postDecision.IsVisible)
            {
                return false;
            }

            command = new ResolvedVfxPlaybackCommand(
                request,
                policy,
                anchor);
            return true;
        }

        private Vector3 ResolveSourceLocalPosition(
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            CubeTopologyState topology,
            in TickForwardCellProjectileReleasePresentationSignal signal,
            Vector3 targetFallback,
            GameplayVfxVisibilityMode visibilityMode)
        {
            if (stateStore != null &&
                stateStore.ViewsByEntityId.TryGetValue(signal.SourceEnemyId, out var view) &&
                view != null)
            {
                if (TryResolveProjectileMuzzleAttachPoint(view, out var attachPoint))
                {
                    return ToPresentationLocal(view.transform, attachPoint.position);
                }

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
                    VfxAnchorSlot.CellCenter,
                    visibilityMode,
                    out var sourceLocalPosition,
                    out _))
            {
                MissingSourceFallbackCount++;
                return sourceLocalPosition;
            }

            MissingSourceFallbackCount++;
            return targetFallback;
        }

        private bool TryResolveProjectileMuzzleAttachPoint(
            GameplayEntityView view,
            out Transform attachPoint)
        {
            attachPoint = null;
            if (view == null ||
                !view.TryGetComponent(out EnemyForwardCellProjectileVfxAuthoring authoring) ||
                authoring == null)
            {
                return false;
            }

            var attachPointId = authoring.ProjectileMuzzleAttachPointId;
            if (string.IsNullOrWhiteSpace(attachPointId))
            {
                return false;
            }

            if (view.TryGetVfxAttachPoint(attachPointId, out attachPoint) &&
                attachPoint != null)
            {
                return true;
            }

            MissingSourceFallbackCount++;
            return false;
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
            VfxAnchorSlot slot,
            GameplayVfxVisibilityMode visibilityMode,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            if (cellProjector.TryResolveCell(
                    cell,
                    topology,
                    slot,
                    visibilityMode,
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

        private static Vector3 ResolveArcLiftAxis(
            GameplayCubeProjector projector,
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            CubeTopologyState topology)
        {
            var hasTargetNormal = TryResolveSurfaceNormal(projector, targetCell, topology, out var targetNormal);
            var hasSourceNormal = TryResolveSurfaceNormal(projector, sourceCell, topology, out var sourceNormal);

            if (hasTargetNormal && hasSourceNormal)
            {
                var combinedNormal = targetNormal + sourceNormal;
                if (combinedNormal.sqrMagnitude > 0.000001f)
                {
                    return -combinedNormal.normalized;
                }
            }

            if (hasTargetNormal)
            {
                return -targetNormal.normalized;
            }

            if (hasSourceNormal)
            {
                return -sourceNormal.normalized;
            }

            return Vector3.up;
        }

        private static bool TryResolveSurfaceNormal(
            GameplayCubeProjector projector,
            SurfaceCell cell,
            CubeTopologyState topology,
            out Vector3 normal)
        {
            if (projector != null &&
                projector.TryProjectSurfaceCell(cell, topology, out var projectedPose))
            {
                normal = projectedPose.Normal;
                return true;
            }

            normal = default;
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
            pool?.Release(flight.FollowHandle);
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

        private static void StopForTopologyTransition(
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

        private readonly struct ActiveFlight
        {
            public ActiveFlight(
                GameplayVfxPlaybackHandle handle,
                IVfxPlaybackHandle followHandle,
                Vector3 source,
                Vector3 target,
                Vector3 arcLiftAxis,
                float duration,
                float arcHeight)
            {
                Handle = handle;
                FollowHandle = followHandle;
                Source = source;
                Target = target;
                ArcLiftAxis = ResolveValidArcLiftAxis(arcLiftAxis);
                Duration = Mathf.Max(DefaultMinFlightDurationSeconds, duration);
                ArcHeight = arcHeight;
                Elapsed = 0f;
            }

            public GameplayVfxPlaybackHandle Handle { get; }

            public IVfxPlaybackHandle FollowHandle { get; }

            private Vector3 Source { get; }

            private Vector3 Target { get; }

            private Vector3 ArcLiftAxis { get; }

            private float Duration { get; }

            private float ArcHeight { get; }

            private float Elapsed { get; }

            public bool IsComplete => Elapsed >= Duration;

            public ActiveFlight Advance(float deltaSeconds)
            {
                return new ActiveFlight(Handle, FollowHandle, Source, Target, ArcLiftAxis, Duration, ArcHeight, Mathf.Min(Duration, Elapsed + deltaSeconds));
            }

            private ActiveFlight(
                GameplayVfxPlaybackHandle handle,
                IVfxPlaybackHandle followHandle,
                Vector3 source,
                Vector3 target,
                Vector3 arcLiftAxis,
                float duration,
                float arcHeight,
                float elapsed)
            {
                Handle = handle;
                FollowHandle = followHandle;
                Source = source;
                Target = target;
                ArcLiftAxis = ResolveValidArcLiftAxis(arcLiftAxis);
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
                var easedT = ResolveFlightProgress(t);
                var control = (Source + Target) * 0.5f + ArcLiftAxis * ArcHeight;
                var a = Vector3.Lerp(Source, control, easedT);
                var b = Vector3.Lerp(control, Target, easedT);
                var position = Vector3.Lerp(a, b, easedT);
                var tangent = (b - a).sqrMagnitude > 0.0001f ? (b - a).normalized : Vector3.forward;
                transform.localPosition = position;
                transform.localRotation = ResolveFlightRotation(tangent, ArcLiftAxis);
                return true;
            }

            private static Vector3 ResolveValidArcLiftAxis(Vector3 arcLiftAxis)
            {
                return arcLiftAxis.sqrMagnitude > 0.000001f
                    ? arcLiftAxis.normalized
                    : Vector3.up;
            }

            private static Quaternion ResolveFlightRotation(Vector3 tangent, Vector3 arcLiftAxis)
            {
                var forward = tangent.sqrMagnitude > 0.000001f
                    ? tangent.normalized
                    : Vector3.forward;
                var up = ResolveValidArcLiftAxis(arcLiftAxis);
                if (Vector3.Cross(forward, up).sqrMagnitude <= 0.000001f)
                {
                    up = Vector3.up;
                }

                if (Vector3.Cross(forward, up).sqrMagnitude <= 0.000001f)
                {
                    up = Vector3.right;
                }

                return Quaternion.LookRotation(forward, up);
            }
        }

        private static float ResolveFlightProgress(float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            return Mathf.Lerp(t, t * t, FlightEaseInBlend);
        }
    }
}
