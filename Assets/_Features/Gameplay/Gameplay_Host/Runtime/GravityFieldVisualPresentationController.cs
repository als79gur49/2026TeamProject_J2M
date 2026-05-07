using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GravityFieldVisualPresentationController
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly HashSet<int> _currentContinuousEntityIds = new();
        private readonly HashSet<int> _previousContinuousEntityIds = new();
        private readonly List<int> _previousContinuousEntityIdBuffer = new();
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

        public void RefreshContinuousStates(IReadOnlyList<GravityFieldVisualState> states)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            _previousContinuousEntityIdBuffer.Clear();
            foreach (var entityId in _previousContinuousEntityIds)
            {
                _previousContinuousEntityIdBuffer.Add(entityId);
            }

            _currentContinuousEntityIds.Clear();
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!_currentContinuousEntityIds.Add(state.EmitterEntityId))
                {
                    _diagnosticSink?.Invoke(
                        $"{nameof(GravityFieldVisualPresentationController)} duplicate continuous visual state for entity {state.EmitterEntityId}; first state retained.");
                    continue;
                }

                ApplyContinuousState(state);
            }

            for (var i = 0; i < _previousContinuousEntityIdBuffer.Count; i++)
            {
                var entityId = _previousContinuousEntityIdBuffer[i];
                if (_currentContinuousEntityIds.Contains(entityId))
                {
                    continue;
                }

                ClearContinuousState(entityId);
            }

            _previousContinuousEntityIds.Clear();
            foreach (var entityId in _currentContinuousEntityIds)
            {
                _previousContinuousEntityIds.Add(entityId);
            }
        }

        public void ClearTrackedContinuousStates()
        {
            RefreshContinuousStates(Array.Empty<GravityFieldVisualState>());
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

        private void ApplyContinuousState(GravityFieldVisualState state)
        {
            if (!TryGetActiveView(state.EmitterEntityId, "continuous", out var view))
            {
                return;
            }

            var target = view.GetComponent<IGravityFieldContinuousVisualTarget>();
            if (target != null)
            {
                target.ApplyGravityFieldVisualState(state);
                return;
            }

            _diagnosticSink?.Invoke(
                $"{nameof(GravityFieldVisualPresentationController)} unsupported continuous visual target for entity {state.EmitterEntityId}.");
        }

        private void ClearContinuousState(int entityId)
        {
            if (!TryGetActiveView(entityId, "continuous clear", out var view))
            {
                return;
            }

            var target = view.GetComponent<IGravityFieldContinuousVisualTarget>();
            if (target != null)
            {
                target.ClearGravityFieldVisualState();
                return;
            }

            _diagnosticSink?.Invoke(
                $"{nameof(GravityFieldVisualPresentationController)} unsupported continuous clear visual target for entity {entityId}.");
        }

        private bool TryGetActiveView(int entityId, string purpose, out GameplayEntityView view)
        {
            if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out view) ||
                view == null ||
                !view.gameObject.activeInHierarchy)
            {
                _diagnosticSink?.Invoke(
                    $"{nameof(GravityFieldVisualPresentationController)} missing {purpose} visual target for entity {entityId}.");
                return false;
            }

            return true;
        }
    }
}
