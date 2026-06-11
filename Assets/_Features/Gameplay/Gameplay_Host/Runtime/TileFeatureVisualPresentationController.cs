using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class TileFeatureVisualPresentationController
    {
        private readonly List<PendingTileFeatureVisualRequest> _pendingRequests = new();
        private readonly HashSet<int> _exitOpenImmediateSyncDeferredTileIds = new();
        private ITileFeatureVisualRegistry _registry;
        private Action<string> _diagnosticSink;

        public void AttachRegistry(ITileFeatureVisualRegistry registry)
        {
            _registry = registry;
        }

        public void SetDiagnosticSink(Action<string> diagnosticSink)
        {
            _diagnosticSink = diagnosticSink;
        }

        public void ResetSession()
        {
            _pendingRequests.Clear();
            _exitOpenImmediateSyncDeferredTileIds.Clear();
        }

        public void PlayButtonActivatedRequests(IReadOnlyList<TilePresentationRequest> requests)
        {
            PlayRequests(requests);
        }

        public void PlayRequests(
            IReadOnlyList<TilePresentationRequest> requests,
            GameplayTimingProfile timingProfile = null)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (_registry == null ||
                requests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (!TileFeatureVisualRequestPlanner.TryCreate(request, out _))
                {
                    continue;
                }

                var delaySeconds = PresentationTimingResolver.ResolveDelaySeconds(
                    request.TimingAnchor,
                    timingProfile);
                if (delaySeconds > 0f)
                {
                    _pendingRequests.Add(new PendingTileFeatureVisualRequest(request, delaySeconds));
                    continue;
                }

                if (!_registry.TryGetTileVisual(request.TileId, out var target) ||
                    target == null)
                {
                    _diagnosticSink?.Invoke(
                        $"{nameof(TileFeatureVisualPresentationController)} missing {request.RequestKind} visual target for tile {request.TileId}.");
                    continue;
                }

                PlayRequestAndTrackImmediateSync(request, target);
            }
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_pendingRequests.Count == 0)
            {
                return;
            }

            for (var i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                var pending = _pendingRequests[i].Advance(deltaTime);
                if (!pending.IsReady)
                {
                    _pendingRequests[i] = pending;
                    continue;
                }

                _pendingRequests.RemoveAt(i);
                PlayReadyRequest(pending.Request);
            }
        }

        public void RefreshContinuousStates(IReadOnlyList<TileFeatureVisualState> visualStates)
        {
            if (visualStates == null)
            {
                throw new ArgumentNullException(nameof(visualStates));
            }

            if (_registry == null ||
                visualStates.Count == 0)
            {
                return;
            }

            for (var i = 0; i < visualStates.Count; i++)
            {
                var visualState = visualStates[i];
                if (!_registry.TryGetTileVisual(visualState.TileId, out var target) ||
                    target == null)
                {
                    _diagnosticSink?.Invoke(
                        $"{nameof(TileFeatureVisualPresentationController)} missing {visualState.TileFeatureKind} visual state target for tile {visualState.TileId}.");
                    continue;
                }

                RefreshContinuousState(visualState, target);
            }
        }

        private void RefreshContinuousState(TileFeatureVisualState visualState, ITileFeatureVisualTarget target)
        {
            var sink = ResolveCueSink(target);
            if (sink == null)
            {
                _diagnosticSink?.Invoke(
                    $"{nameof(TileFeatureVisualPresentationController)} missing visual cue sink for tile {visualState.TileId}.");
                return;
            }

            switch (visualState.TileFeatureKind)
            {
                case TileFeatureKind.Destroy:
                case TileFeatureKind.Slide:
                    TryHandleVisualState(sink, visualState);
                    return;
                case TileFeatureKind.Barricade:
                    TryHandleVisualState(sink, visualState);
                    return;
                case TileFeatureKind.Exit:
                    if (!visualState.IsActive)
                    {
                        _exitOpenImmediateSyncDeferredTileIds.Remove(visualState.TileId);
                    }

                    if (visualState.IsActive &&
                        (visualState.VisibilityGate.HasGate ||
                         _exitOpenImmediateSyncDeferredTileIds.Contains(visualState.TileId)))
                    {
                        if (visualState.VisibilityGate.HasGate)
                        {
                            _exitOpenImmediateSyncDeferredTileIds.Add(visualState.TileId);
                        }

                        return;
                    }

                    _exitOpenImmediateSyncDeferredTileIds.Remove(visualState.TileId);
                    TryHandleVisualState(sink, visualState);
                    return;
            }
        }

        private bool PlayRequest(TilePresentationRequest request, ITileFeatureVisualTarget target)
        {
            var sink = ResolveCueSink(target);
            if (sink == null)
            {
                _diagnosticSink?.Invoke(
                    $"{nameof(TileFeatureVisualPresentationController)} missing visual cue sink for tile {request.TileId}.");
                return false;
            }

            if (!TileFeatureVisualRequestPlanner.TryCreate(request, out var visualRequest))
            {
                return false;
            }

            if (sink.TryHandle(visualRequest))
            {
                return true;
            }

            _diagnosticSink?.Invoke(
                $"{nameof(TileFeatureVisualPresentationController)} unsupported {request.RequestKind} visual target for tile {request.TileId}.");
            return false;
        }

        private void TryHandleVisualState(
            ITileFeatureVisualCueSink sink,
            in TileFeatureVisualState visualState)
        {
            if (!TileFeatureVisualRequestPlanner.TryCreate(visualState, out var visualRequest))
            {
                return;
            }

            if (sink.TryHandle(visualRequest))
            {
                return;
            }

            _diagnosticSink?.Invoke(
                $"{nameof(TileFeatureVisualPresentationController)} unsupported {visualState.TileFeatureKind} visual state target for tile {visualState.TileId}.");
        }

        private static ITileFeatureVisualCueSink ResolveCueSink(ITileFeatureVisualTarget target)
        {
            if (target is ITileFeatureVisualCueSink sink)
            {
                return sink;
            }

            if (target is UnityEngine.Component component)
            {
                var componentSink = component.GetComponent<ITileFeatureVisualCueSink>();
                if (componentSink != null)
                {
                    return componentSink;
                }

                if (target is TileFeatureVisualTargetView targetView)
                {
#pragma warning disable CS0618
                    var adapter = component.gameObject.AddComponent<LegacyTileFeatureVisualCueAdapter>();
#pragma warning restore CS0618
                    adapter.ConfigureTarget(targetView);
                    return adapter;
                }
            }

            return LegacyInterfaceCueSink.CanWrap(target)
                ? new LegacyInterfaceCueSink(target)
                : null;
        }

        private sealed class LegacyInterfaceCueSink : ITileFeatureVisualCueSink
        {
            private readonly ITileFeatureVisualTarget target;

            public LegacyInterfaceCueSink(ITileFeatureVisualTarget target)
            {
                this.target = target;
            }

            public static bool CanWrap(ITileFeatureVisualTarget target)
            {
                return HasLegacyButtonMethod(target) ||
                       target is IDestroyTileVisualTarget ||
                       target is IDestroyTileActivatedVisualTarget ||
                       target is IDestroyTileDeactivatedVisualTarget ||
                       target is ITileFeatureActiveStateVisualTarget ||
                       target is ISlideTileVisualTarget ||
                       target is IBarricadeBlockedVisualTarget ||
                       target is IBarricadeCrushedVisualTarget ||
                       target is IBarricadeActivatedVisualTarget ||
                       target is IBarricadeDeactivatedVisualTarget ||
                       target is IBarricadeActiveStateVisualTarget ||
                       target is IExitOpenedVisualTarget ||
                       target is IExitEnteredVisualTarget ||
                       target is IExitOpenStateVisualTarget ||
                       target is IMoonBlockGeneratedVisualTarget ||
                       target is IMoonBlockGeneratorBlockedVisualTarget;
            }

            public bool TryHandle(in TileFeatureVisualRequest request)
            {
                switch (request.CueId)
                {
                    case TileFeatureVisualCueId.ButtonActivated:
                        if (TryInvokeLegacyButton())
                        {
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.DestroyTileTriggered:
                        if (target is IDestroyTileVisualTarget destroyTarget)
                        {
                            destroyTarget.PlayDestroyTileTriggered();
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.DestroyTileActivated:
                        if (target is IDestroyTileActivatedVisualTarget destroyActivatedTarget)
                        {
                            destroyActivatedTarget.PlayDestroyTileActivated();
                            return true;
                        }

                        if (target is ITileFeatureActiveStateVisualTarget activeStateTarget)
                        {
                            activeStateTarget.SetTileFeatureActiveImmediate(TileFeatureKind.Destroy, true);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.DestroyTileDeactivated:
                        if (target is IDestroyTileDeactivatedVisualTarget destroyDeactivatedTarget)
                        {
                            destroyDeactivatedTarget.PlayDestroyTileDeactivated();
                            return true;
                        }

                        if (target is ITileFeatureActiveStateVisualTarget inactiveStateTarget)
                        {
                            inactiveStateTarget.SetTileFeatureActiveImmediate(TileFeatureKind.Destroy, false);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.DestroyTileActiveState:
                    case TileFeatureVisualCueId.SlideTileActiveState:
                        if (target is ITileFeatureActiveStateVisualTarget tileFeatureActiveTarget)
                        {
                            tileFeatureActiveTarget.SetTileFeatureActiveImmediate(request.FeatureKind, request.Active);
                            return true;
                        }

                        if (request.CueId == TileFeatureVisualCueId.DestroyTileActiveState &&
                            target is IDestroyTileActiveStateVisualTarget destroyActiveTarget)
                        {
                            destroyActiveTarget.SetDestroyTileActiveImmediate(request.Active);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.SlideTileRedirected:
                        if (target is ISlideTileVisualTarget slideTarget)
                        {
                            slideTarget.PlaySlideTileRedirected(request.Direction, request.TargetEntityId);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.BarricadeBlocked:
                        if (target is IBarricadeBlockedVisualTarget barricadeBlockedTarget)
                        {
                            barricadeBlockedTarget.PlayBarricadeBlocked(request.Direction, request.TargetEntityId);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.BarricadeCrushed:
                        if (target is IBarricadeCrushedVisualTarget barricadeCrushedTarget)
                        {
                            barricadeCrushedTarget.PlayBarricadeCrushed(request.TargetEntityId);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.BarricadeActivated:
                        if (target is IBarricadeActivatedVisualTarget barricadeActivatedTarget)
                        {
                            barricadeActivatedTarget.PlayBarricadeActivated();
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.BarricadeDeactivated:
                        if (target is IBarricadeDeactivatedVisualTarget barricadeDeactivatedTarget)
                        {
                            barricadeDeactivatedTarget.PlayBarricadeDeactivated();
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.BarricadeActiveState:
                        if (target is IBarricadeActiveStateVisualTarget barricadeActiveTarget)
                        {
                            barricadeActiveTarget.SetBarricadeActiveImmediate(request.Active);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.ExitOpened:
                        if (target is IExitOpenedVisualTarget exitOpenedTarget)
                        {
                            exitOpenedTarget.PlayExitOpened();
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.ExitEntered:
                        if (target is IExitEnteredVisualTarget exitEnteredTarget)
                        {
                            exitEnteredTarget.PlayExitEntered(request.TargetEntityId);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.ExitOpenState:
                        if (target is IExitOpenStateVisualTarget exitOpenTarget)
                        {
                            exitOpenTarget.SetExitOpenImmediate(request.Active);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.MoonBlockGenerated:
                        if (target is IMoonBlockGeneratedVisualTarget moonBlockGeneratedTarget)
                        {
                            moonBlockGeneratedTarget.PlayMoonBlockGenerated(request.TargetEntityId);
                            return true;
                        }

                        return false;
                    case TileFeatureVisualCueId.MoonBlockGeneratorBlocked:
                        if (target is IMoonBlockGeneratorBlockedVisualTarget moonBlockGeneratorBlockedTarget)
                        {
                            moonBlockGeneratorBlockedTarget.PlayMoonBlockGeneratorBlocked(
                                request.MoonBlockGeneratorBlockedPayload);
                            return true;
                        }

                        return false;
                    default:
                        return false;
                }
            }

            private static bool HasLegacyButtonMethod(ITileFeatureVisualTarget target)
            {
                return target?.GetType().GetMethod(
                    "PlayButtonActivated",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    Type.EmptyTypes,
                    modifiers: null) != null;
            }

            private bool TryInvokeLegacyButton()
            {
                var method = target.GetType().GetMethod(
                    "PlayButtonActivated",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    Type.EmptyTypes,
                    modifiers: null);
                if (method == null)
                {
                    return false;
                }

                method.Invoke(target, Array.Empty<object>());
                return true;
            }
        }

        private void PlayRequestAndTrackImmediateSync(TilePresentationRequest request, ITileFeatureVisualTarget target)
        {
            if (PlayRequest(request, target) &&
                request.RequestKind == TilePresentationRequestKind.ExitOpened)
            {
                _exitOpenImmediateSyncDeferredTileIds.Add(request.TileId);
            }
        }

        private void PlayReadyRequest(TilePresentationRequest request)
        {
            if (_registry == null)
            {
                return;
            }

            if (!_registry.TryGetTileVisual(request.TileId, out var target) ||
                target == null)
            {
                _diagnosticSink?.Invoke(
                    $"{nameof(TileFeatureVisualPresentationController)} missing {request.RequestKind} visual target for tile {request.TileId}.");
                return;
            }

            PlayRequestAndTrackImmediateSync(request, target);
        }

        private readonly struct PendingTileFeatureVisualRequest
        {
            public PendingTileFeatureVisualRequest(TilePresentationRequest request, float remainingSeconds)
            {
                Request = request;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
            }

            public TilePresentationRequest Request { get; }

            public float RemainingSeconds { get; }

            public bool IsReady => RemainingSeconds <= 0.00001f;

            public PendingTileFeatureVisualRequest Advance(float deltaTime)
            {
                return new PendingTileFeatureVisualRequest(Request, RemainingSeconds - deltaTime);
            }
        }
    }
}
