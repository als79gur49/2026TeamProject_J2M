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
            var sink = ResolveCueSink(target, visualState.TileFeatureKind);
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
            var sink = ResolveCueSink(target, request.TileFeatureKind);
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

        private static ITileFeatureVisualCueSink ResolveCueSink(
            ITileFeatureVisualTarget target,
            TileFeatureKind featureKind)
        {
            return TileFeatureVisualCueSinkResolver.Resolve(target, featureKind);
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
