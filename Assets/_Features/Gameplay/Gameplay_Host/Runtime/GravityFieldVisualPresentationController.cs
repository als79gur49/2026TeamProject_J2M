using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GravityFieldVisualPresentationController
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private Action<string> _diagnosticSink;

        public GravityFieldVisualPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public void SetDiagnosticSink(Action<string> diagnosticSink)
        {
            _diagnosticSink = diagnosticSink;
        }

        public void PlayRequests(IReadOnlyList<GravityFieldPresentationRequest> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (requests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < requests.Count; i++)
            {
                PlayRequest(requests[i]);
            }
        }

        private void PlayRequest(GravityFieldPresentationRequest request)
        {
            if (!_stateStore.ViewsByEntityId.TryGetValue(request.EmitterEntityId, out var view) ||
                view == null ||
                !view.gameObject.activeInHierarchy)
            {
                _diagnosticSink?.Invoke(
                    $"{nameof(GravityFieldVisualPresentationController)} missing {request.RequestKind} visual target for entity {request.EmitterEntityId}.");
                return;
            }

            switch (request.RequestKind)
            {
                case GravityFieldPresentationRequestKind.Activated:
                    var activatedTarget = view.GetComponent<IGravityFieldActivatedVisualTarget>();
                    if (activatedTarget != null)
                    {
                        activatedTarget.PlayGravityFieldActivated();
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(GravityFieldVisualPresentationController)} unsupported Activated visual target for entity {request.EmitterEntityId}.");
                    }

                    return;
                case GravityFieldPresentationRequestKind.Expired:
                    var expiredTarget = view.GetComponent<IGravityFieldExpiredVisualTarget>();
                    if (expiredTarget != null)
                    {
                        expiredTarget.PlayGravityFieldExpired();
                    }
                    else
                    {
                        _diagnosticSink?.Invoke(
                            $"{nameof(GravityFieldVisualPresentationController)} unsupported Expired visual target for entity {request.EmitterEntityId}.");
                    }

                    return;
            }
        }
    }
}
