using System;
using System.Collections.Generic;
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
                if (!IsSupportedVisualRequest(request.RequestKind))
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
            switch (visualState.TileFeatureKind)
            {
                case TileFeatureKind.Destroy:
                    if (target is IDestroyTileActiveStateVisualTarget destroyTileTarget)
                    {
                        destroyTileTarget.SetDestroyTileActiveImmediate(visualState.IsActive);
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported DestroyTile visual state target for tile {visualState.TileId}.");
                    }

                    return;
                case TileFeatureKind.Barricade:
                    if (target is IBarricadeActiveStateVisualTarget barricadeTarget)
                    {
                        barricadeTarget.SetBarricadeActiveImmediate(visualState.IsActive);
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported Barricade visual state target for tile {visualState.TileId}.");
                    }

                    return;
                case TileFeatureKind.Exit:
                    if (!visualState.IsActive)
                    {
                        _exitOpenImmediateSyncDeferredTileIds.Remove(visualState.TileId);
                    }

                    if (target is IExitOpenStateVisualTarget exitTarget)
                    {
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
                        exitTarget.SetExitOpenImmediate(visualState.IsActive);
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported Exit visual state target for tile {visualState.TileId}.");
                    }

                    return;
            }
        }

        private bool PlayRequest(TilePresentationRequest request, ITileFeatureVisualTarget target)
        {
            switch (request.RequestKind)
            {
                case TilePresentationRequestKind.ButtonActivated:
                    target.PlayButtonActivated();
                    return true;
                case TilePresentationRequestKind.DestroyTileTriggered:
                    if (target is IDestroyTileVisualTarget destroyTileTarget)
                    {
                        destroyTileTarget.PlayDestroyTileTriggered();
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported DestroyTileTriggered visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.DestroyTileActivated:
                    if (target is IDestroyTileActivatedVisualTarget destroyTileActivatedTarget)
                    {
                        destroyTileActivatedTarget.PlayDestroyTileActivated();
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported DestroyTileActivated visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.DestroyTileDeactivated:
                    if (target is IDestroyTileDeactivatedVisualTarget destroyTileDeactivatedTarget)
                    {
                        destroyTileDeactivatedTarget.PlayDestroyTileDeactivated();
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported DestroyTileDeactivated visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.SlideTileRedirected:
                    if (target is ISlideTileVisualTarget slideTileTarget)
                    {
                        slideTileTarget.PlaySlideTileRedirected(request.Direction, request.TargetEntityId);
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported SlideTileRedirected visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.BarricadeBlocked:
                    if (target is IBarricadeBlockedVisualTarget barricadeBlockedTarget)
                    {
                        barricadeBlockedTarget.PlayBarricadeBlocked(request.Direction, request.TargetEntityId);
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported BarricadeBlocked visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.BarricadeCrushed:
                    if (target is IBarricadeCrushedVisualTarget barricadeCrushedTarget)
                    {
                        barricadeCrushedTarget.PlayBarricadeCrushed(request.TargetEntityId);
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported BarricadeCrushed visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.BarricadeActivated:
                    if (target is IBarricadeActivatedVisualTarget barricadeActivatedTarget)
                    {
                        barricadeActivatedTarget.PlayBarricadeActivated();
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported BarricadeActivated visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.BarricadeDeactivated:
                    if (target is IBarricadeDeactivatedVisualTarget barricadeDeactivatedTarget)
                    {
                        barricadeDeactivatedTarget.PlayBarricadeDeactivated();
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported BarricadeDeactivated visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.ExitOpened:
                    if (target is IExitOpenedVisualTarget exitOpenedTarget)
                    {
                        exitOpenedTarget.PlayExitOpened();
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported ExitOpened visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.ExitEntered:
                    if (target is IExitEnteredVisualTarget exitEnteredTarget)
                    {
                        exitEnteredTarget.PlayExitEntered(request.TargetEntityId);
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported ExitEntered visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.MoonBlockGenerated:
                    if (target is IMoonBlockGeneratedVisualTarget moonBlockGeneratedTarget)
                    {
                        moonBlockGeneratedTarget.PlayMoonBlockGenerated(request.TargetEntityId);
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported MoonBlockGenerated visual target for tile {request.TileId}.");
                    }

                    return false;
                case TilePresentationRequestKind.MoonBlockGeneratorBlocked:
                    if (target is IMoonBlockGeneratorBlockedVisualTarget moonBlockGeneratorBlockedTarget)
                    {
                        moonBlockGeneratorBlockedTarget.PlayMoonBlockGeneratorBlocked(
                            request.MoonBlockGeneratorBlockedPayload);
                        return true;
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported MoonBlockGeneratorBlocked visual target for tile {request.TileId}.");
                    }

                    return false;
            }

            return false;
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

        private static bool IsSupportedVisualRequest(TilePresentationRequestKind requestKind)
        {
            return requestKind == TilePresentationRequestKind.ButtonActivated ||
                   requestKind == TilePresentationRequestKind.DestroyTileTriggered ||
                   requestKind == TilePresentationRequestKind.DestroyTileActivated ||
                   requestKind == TilePresentationRequestKind.DestroyTileDeactivated ||
                   requestKind == TilePresentationRequestKind.SlideTileRedirected ||
                   requestKind == TilePresentationRequestKind.BarricadeBlocked ||
                   requestKind == TilePresentationRequestKind.BarricadeCrushed ||
                   requestKind == TilePresentationRequestKind.BarricadeActivated ||
                   requestKind == TilePresentationRequestKind.BarricadeDeactivated ||
                   requestKind == TilePresentationRequestKind.ExitOpened ||
                   requestKind == TilePresentationRequestKind.ExitEntered ||
                   requestKind == TilePresentationRequestKind.MoonBlockGenerated ||
                   requestKind == TilePresentationRequestKind.MoonBlockGeneratorBlocked;
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

            public bool IsReady => RemainingSeconds <= 0f;

            public PendingTileFeatureVisualRequest Advance(float deltaTime)
            {
                return new PendingTileFeatureVisualRequest(Request, RemainingSeconds - deltaTime);
            }
        }
    }
}
