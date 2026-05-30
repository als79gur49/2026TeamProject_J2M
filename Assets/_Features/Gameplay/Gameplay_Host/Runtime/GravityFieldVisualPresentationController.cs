using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GravityFieldVisualPresentationController
    {
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly HashSet<int> _currentContinuousEntityIds = new();
        private readonly HashSet<int> _previousContinuousEntityIds = new();
        private readonly HashSet<LockedTargetPair> _currentLockedTargetPairs = new();
        private readonly HashSet<LockedTargetPair> _previousLockedTargetPairs = new();
        private readonly HashSet<LockedTargetPair> _currentEnemyAuraLockedTargetPairs = new();
        private readonly HashSet<LockedTargetPair> _previousEnemyAuraLockedTargetPairs = new();
        private readonly HashSet<IGravityFieldLockedTargetRevealVisualTarget> _lockedTargetRevealTargets = new();
        private readonly List<int> _previousContinuousEntityIdBuffer = new();
        private readonly List<LockedTargetPair> _previousLockedTargetPairBuffer = new();
        private readonly List<LockedTargetPair> _previousEnemyAuraLockedTargetPairBuffer = new();
        private readonly List<IGravityFieldLockedTargetRevealVisualTarget> _lockedTargetRevealTargetBuffer = new();
        private const int EnemyAuraLockedTargetSourceIdPrefix = 0x40000000;
        private Action<string> _diagnosticSink;
        private GameplayEntityViewRegistry _targetViewRegistry;

        public GravityFieldVisualPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public void SetDiagnosticSink(Action<string> diagnosticSink)
        {
            _diagnosticSink = diagnosticSink;
        }

        public void AttachTargetViewRegistry(GameplayEntityViewRegistry targetViewRegistry)
        {
            _targetViewRegistry = targetViewRegistry;
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

            _previousLockedTargetPairBuffer.Clear();
            foreach (var pair in _previousLockedTargetPairs)
            {
                _previousLockedTargetPairBuffer.Add(pair);
            }

            _currentContinuousEntityIds.Clear();
            _currentLockedTargetPairs.Clear();
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
                AddLockedTargetPairs(state);
            }

            foreach (var pair in _currentLockedTargetPairs)
            {
                ApplyLockedTarget(pair);
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

            for (var i = 0; i < _previousLockedTargetPairBuffer.Count; i++)
            {
                var pair = _previousLockedTargetPairBuffer[i];
                if (_currentLockedTargetPairs.Contains(pair))
                {
                    continue;
                }

                ClearLockedTarget(pair);
            }

            _previousContinuousEntityIds.Clear();
            foreach (var entityId in _currentContinuousEntityIds)
            {
                _previousContinuousEntityIds.Add(entityId);
            }

            _previousLockedTargetPairs.Clear();
            foreach (var pair in _currentLockedTargetPairs)
            {
                _previousLockedTargetPairs.Add(pair);
            }
        }

        public void RefreshEnemyGravityFieldAuraLockedTargets(IReadOnlyList<TickEnemyGravityFieldAuraVisualState> states)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            _previousEnemyAuraLockedTargetPairBuffer.Clear();
            foreach (var pair in _previousEnemyAuraLockedTargetPairs)
            {
                _previousEnemyAuraLockedTargetPairBuffer.Add(pair);
            }

            _currentEnemyAuraLockedTargetPairs.Clear();
            for (var i = 0; i < states.Count; i++)
            {
                AddEnemyAuraLockedTargetPairs(states[i]);
            }

            foreach (var pair in _currentEnemyAuraLockedTargetPairs)
            {
                ApplyLockedTarget(pair);
            }

            for (var i = 0; i < _previousEnemyAuraLockedTargetPairBuffer.Count; i++)
            {
                var pair = _previousEnemyAuraLockedTargetPairBuffer[i];
                if (_currentEnemyAuraLockedTargetPairs.Contains(pair))
                {
                    continue;
                }

                ClearLockedTarget(pair);
            }

            _previousEnemyAuraLockedTargetPairs.Clear();
            foreach (var pair in _currentEnemyAuraLockedTargetPairs)
            {
                _previousEnemyAuraLockedTargetPairs.Add(pair);
            }
        }

        public void ClearTrackedContinuousStates()
        {
            RefreshContinuousStates(Array.Empty<GravityFieldVisualState>());
            RefreshEnemyGravityFieldAuraLockedTargets(Array.Empty<TickEnemyGravityFieldAuraVisualState>());
            ResetTrackedLockedTargetReveals();
        }

        public void UpdatePresentation(float deltaTime)
        {
            if (_lockedTargetRevealTargets.Count == 0)
            {
                return;
            }

            _lockedTargetRevealTargetBuffer.Clear();
            foreach (var target in _lockedTargetRevealTargets)
            {
                _lockedTargetRevealTargetBuffer.Add(target);
            }

            for (var i = 0; i < _lockedTargetRevealTargetBuffer.Count; i++)
            {
                var target = _lockedTargetRevealTargetBuffer[i];
                if (IsMissingRevealTarget(target))
                {
                    _lockedTargetRevealTargets.Remove(target);
                    continue;
                }

                if (!target.UpdateGravityFieldLockedTargetReveal(deltaTime))
                {
                    _lockedTargetRevealTargets.Remove(target);
                }
            }
        }

        private void PlayRequest(GravityFieldPresentationRequest request)
        {
            switch (request.RequestKind)
            {
                case GravityFieldPresentationRequestKind.Activated:
                    if (!TryGetActiveView(request.EmitterEntityId, request.RequestKind.ToString(), out var activatedView))
                    {
                        return;
                    }

                    var activatedTarget = activatedView.GetComponent<IGravityFieldActivatedVisualTarget>();
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
                    if (!TryGetActiveView(request.EmitterEntityId, request.RequestKind.ToString(), out var expiredView))
                    {
                        return;
                    }

                    var expiredTarget = expiredView.GetComponent<IGravityFieldExpiredVisualTarget>();
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
                case GravityFieldPresentationRequestKind.LockedBox:
                    PlayLockedBox(request);
                    return;
            }
        }

        private void PlayLockedBox(GravityFieldPresentationRequest request)
        {
            var payload = request.LockedBoxPayload;
            if (!payload.IsValid)
            {
                _diagnosticSink?.Invoke(
                    $"{nameof(GravityFieldVisualPresentationController)} invalid LockedBox visual payload for emitter {request.EmitterEntityId} target {request.TargetEntityId}.");
                return;
            }

            var pair = new LockedTargetPair(payload.EmitterEntityId, payload.TargetEntityId);
            if (!TryGetActiveTargetView(pair, "locked box one-shot", out var view))
            {
                return;
            }

            var target = view.GetComponent<IGravityFieldLockedBoxOneShotVisualTarget>();
            if (target != null)
            {
                target.PlayGravityFieldLockedBox(payload);
                return;
            }

            _diagnosticSink?.Invoke(
                $"{nameof(GravityFieldVisualPresentationController)} unsupported locked box one-shot visual target for target entity {payload.TargetEntityId} from emitter {payload.EmitterEntityId}.");
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

        private void AddLockedTargetPairs(GravityFieldVisualState state)
        {
            var lockedTargetEntityIds = state.LockedTargetEntityIds;
            for (var i = 0; i < lockedTargetEntityIds.Count; i++)
            {
                var targetEntityId = lockedTargetEntityIds[i];
                if (targetEntityId <= 0)
                {
                    continue;
                }

                _currentLockedTargetPairs.Add(new LockedTargetPair(state.EmitterEntityId, targetEntityId));
            }
        }

        private void AddEnemyAuraLockedTargetPairs(TickEnemyGravityFieldAuraVisualState state)
        {
            if (state.EntityId <= 0 ||
                state.Phase != EnemyUtilityEffectPhase.Active)
            {
                return;
            }

            var sourceId = ResolveEnemyAuraLockedTargetSourceId(state);
            var lockedTargetEntityIds = state.LockedTargetEntityIds;
            for (var i = 0; i < lockedTargetEntityIds.Count; i++)
            {
                var targetEntityId = lockedTargetEntityIds[i];
                if (targetEntityId <= 0)
                {
                    continue;
                }

                _currentEnemyAuraLockedTargetPairs.Add(new LockedTargetPair(sourceId, targetEntityId));
            }
        }

        private static int ResolveEnemyAuraLockedTargetSourceId(in TickEnemyGravityFieldAuraVisualState state)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 397) ^ state.EntityId;
                hash = (hash * 397) ^ state.EffectIndex;
                hash = (hash * 397) ^ state.ActivationSequence;
                return EnemyAuraLockedTargetSourceIdPrefix | (hash & 0x3FFFFFFF);
            }
        }

        private void ApplyLockedTarget(LockedTargetPair pair)
        {
            if (!TryGetActiveTargetView(pair, "locked target", out var view))
            {
                return;
            }

            var target = view.GetComponent<IGravityFieldLockedTargetVisualTarget>();
            if (target != null)
            {
                target.ApplyGravityFieldLockedTarget(pair.EmitterEntityId);
                TrackLockedTargetRevealTarget(view);
                return;
            }

            _diagnosticSink?.Invoke(
                $"{nameof(GravityFieldVisualPresentationController)} unsupported locked target visual target for target entity {pair.TargetEntityId} from emitter {pair.EmitterEntityId}.");
        }

        private void ClearLockedTarget(LockedTargetPair pair)
        {
            if (!TryGetActiveTargetView(pair, "locked target clear", out var view))
            {
                return;
            }

            var target = view.GetComponent<IGravityFieldLockedTargetVisualTarget>();
            if (target != null)
            {
                target.ClearGravityFieldLockedTarget(pair.EmitterEntityId);
                TrackLockedTargetRevealTarget(view);
                return;
            }

            _diagnosticSink?.Invoke(
                $"{nameof(GravityFieldVisualPresentationController)} unsupported locked target clear visual target for target entity {pair.TargetEntityId} from emitter {pair.EmitterEntityId}.");
        }

        private bool TryGetActiveTargetView(LockedTargetPair pair, string purpose, out GameplayEntityView view)
        {
            view = null;
            if (_targetViewRegistry == null ||
                !_targetViewRegistry.TryGetView(pair.TargetEntityId, out view) ||
                view == null ||
                !view.gameObject.activeInHierarchy)
            {
                _diagnosticSink?.Invoke(
                    $"{nameof(GravityFieldVisualPresentationController)} missing {purpose} visual target for target entity {pair.TargetEntityId} from emitter {pair.EmitterEntityId}.");
                return false;
            }

            return true;
        }

        private void TrackLockedTargetRevealTarget(GameplayEntityView view)
        {
            var target = view.GetComponent<IGravityFieldLockedTargetRevealVisualTarget>();
            if (target != null)
            {
                _lockedTargetRevealTargets.Add(target);
            }
        }

        private void ResetTrackedLockedTargetReveals()
        {
            if (_lockedTargetRevealTargets.Count == 0)
            {
                return;
            }

            _lockedTargetRevealTargetBuffer.Clear();
            foreach (var target in _lockedTargetRevealTargets)
            {
                _lockedTargetRevealTargetBuffer.Add(target);
            }

            for (var i = 0; i < _lockedTargetRevealTargetBuffer.Count; i++)
            {
                var target = _lockedTargetRevealTargetBuffer[i];
                if (!IsMissingRevealTarget(target))
                {
                    target.ResetGravityFieldLockedTargetReveal();
                }
            }

            _lockedTargetRevealTargets.Clear();
        }

        private static bool IsMissingRevealTarget(IGravityFieldLockedTargetRevealVisualTarget target)
        {
            if (target == null)
            {
                return true;
            }

            return target is UnityEngine.Object unityObject && unityObject == null;
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

        private readonly struct LockedTargetPair : IEquatable<LockedTargetPair>
        {
            public LockedTargetPair(int emitterEntityId, int targetEntityId)
            {
                EmitterEntityId = emitterEntityId;
                TargetEntityId = targetEntityId;
            }

            public int EmitterEntityId { get; }

            public int TargetEntityId { get; }

            public bool Equals(LockedTargetPair other)
            {
                return EmitterEntityId == other.EmitterEntityId &&
                       TargetEntityId == other.TargetEntityId;
            }

            public override bool Equals(object obj)
            {
                return obj is LockedTargetPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EmitterEntityId * 397) ^ TargetEntityId;
                }
            }
        }
    }
}
