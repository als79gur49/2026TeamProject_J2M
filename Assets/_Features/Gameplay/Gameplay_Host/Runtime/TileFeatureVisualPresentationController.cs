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
                if (request.RequestKind != TilePresentationRequestKind.ButtonActivated)
                {
                    continue;
                }

                if (!_registry.TryGetTileVisual(request.TileId, out var target) ||
                    target == null)
                {
                    _diagnosticSink?.Invoke(
                        $"{nameof(TileFeatureVisualPresentationController)} missing ButtonActivated visual target for tile {request.TileId}.");
                    continue;
                }

                target.PlayButtonActivated();
            }
        }
    }
}
