using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class TileFeatureVisualPresentationController
    {
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

        public void PlayButtonActivatedRequests(IReadOnlyList<TilePresentationRequest> requests)
        {
            PlayRequests(requests);
        }

        public void PlayRequests(IReadOnlyList<TilePresentationRequest> requests)
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

                if (!_registry.TryGetTileVisual(request.TileId, out var target) ||
                    target == null)
                {
                    _diagnosticSink?.Invoke(
                        $"{nameof(TileFeatureVisualPresentationController)} missing {request.RequestKind} visual target for tile {request.TileId}.");
                    continue;
                }

                PlayRequest(request, target);
            }
        }

        private void PlayRequest(TilePresentationRequest request, ITileFeatureVisualTarget target)
        {
            switch (request.RequestKind)
            {
                case TilePresentationRequestKind.ButtonActivated:
                    target.PlayButtonActivated();
                    return;
                case TilePresentationRequestKind.DestroyTileTriggered:
                    if (target is IDestroyTileVisualTarget destroyTileTarget)
                    {
                        destroyTileTarget.PlayDestroyTileTriggered();
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported DestroyTileTriggered visual target for tile {request.TileId}.");
                    }

                    return;
                case TilePresentationRequestKind.SlideTileRedirected:
                    if (target is ISlideTileVisualTarget slideTileTarget)
                    {
                        slideTileTarget.PlaySlideTileRedirected(request.Direction, request.TargetEntityId);
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported SlideTileRedirected visual target for tile {request.TileId}.");
                    }

                    return;
                case TilePresentationRequestKind.BarricadeBlocked:
                    if (target is IBarricadeBlockedVisualTarget barricadeBlockedTarget)
                    {
                        barricadeBlockedTarget.PlayBarricadeBlocked(request.Direction, request.TargetEntityId);
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported BarricadeBlocked visual target for tile {request.TileId}.");
                    }

                    return;
                case TilePresentationRequestKind.BarricadeCrushed:
                    if (target is IBarricadeCrushedVisualTarget barricadeCrushedTarget)
                    {
                        barricadeCrushedTarget.PlayBarricadeCrushed(request.TargetEntityId);
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(TileFeatureVisualPresentationController)} unsupported BarricadeCrushed visual target for tile {request.TileId}.");
                    }

                    return;
            }
        }

        private static bool IsSupportedVisualRequest(TilePresentationRequestKind requestKind)
        {
            return requestKind == TilePresentationRequestKind.ButtonActivated ||
                   requestKind == TilePresentationRequestKind.DestroyTileTriggered ||
                   requestKind == TilePresentationRequestKind.SlideTileRedirected ||
                   requestKind == TilePresentationRequestKind.BarricadeBlocked ||
                   requestKind == TilePresentationRequestKind.BarricadeCrushed;
        }
    }
}
