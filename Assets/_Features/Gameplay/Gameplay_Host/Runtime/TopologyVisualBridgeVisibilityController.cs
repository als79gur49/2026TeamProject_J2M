using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    /// <summary>
    /// Visual-only bridge binding for authored seam helpers between logical faces.
    /// This data does not participate in gameplay collision, wall runtime data, or authoritative board state.
    /// </summary>
    [Serializable]
    public sealed class TopologyVisualBridgeBinding
    {
        [Tooltip("Visual-only scene object controlled by this bridge binding. Do not assign gameplay-authoritative objects or collider-driven blockers.")]
        public GameObject TargetObject;

        [Tooltip("First logical FaceId endpoint used by the current v1 bridge visibility rule.")]
        public FaceId FirstFace = FaceId.Floor;

        [Tooltip("Second logical FaceId endpoint used by the current v1 bridge visibility rule.")]
        public FaceId SecondFace = FaceId.Front;
    }

    /// <summary>
    /// Presentation-only controller that toggles authored bridge visuals from presenter topology state.
    /// Bridge bindings remain scene-side helpers and do not write authoritative gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TopologyVisualBridgeVisibilityController : MonoBehaviour
    {
        private const string LogPrefix = "[TopologyVisualBridgeVisibilityController]";

        [SerializeField]
        [Tooltip("GameplaySceneHost used as the presenter-derived truth source for visual-only bridge visibility.")]
        private GameplaySceneHost sceneHost;

        [SerializeField]
        [Tooltip("Visual-only bridge bindings. v1 uses logical FaceId pairs; future slot-pair evaluation can replace only the visibility helper.")]
        private TopologyVisualBridgeBinding[] bindings = Array.Empty<TopologyVisualBridgeBinding>();

        private readonly Dictionary<GameObject, bool> _appliedVisibilityByTarget = new();
        private readonly List<RuntimeBinding> _runtimeBindings = new();
        private readonly List<GameObject> _staleTargets = new();
        private bool _isDirty = true;
        private bool _sceneHostResolutionDisableRequested;
        private GameplayTickViewPresenter _subscribedPresenter;
        private int _setActiveApplyCount;

        public GameplaySceneHost SceneHost => sceneHost;

        public IReadOnlyList<TopologyVisualBridgeBinding> Bindings => bindings ?? Array.Empty<TopologyVisualBridgeBinding>();

        internal int DebugSetActiveApplyCount => _setActiveApplyCount;

        internal int DebugValidatedBindingCount => _runtimeBindings.Count;

        private void Awake()
        {
            if (!TryEnsureSceneHostReference(logWarnings: true, disableOnFailure: true))
            {
                return;
            }

            RefreshAuthoringState(logWarnings: false);
        }

        private void OnEnable()
        {
            if (!TryEnsureSceneHostReference(logWarnings: true, disableOnFailure: true))
            {
                return;
            }

            RefreshAuthoringState(logWarnings: true);
            TryEnsurePresenterSubscription();
        }

        private void OnDisable()
        {
            DetachPresenter();
        }

        private void OnValidate()
        {
            TryAutoAssignSceneHost(logWarnings: false, disableOnFailure: false);
            RefreshAuthoringState(logWarnings: false);
        }

        private void LateUpdate()
        {
            if (_sceneHostResolutionDisableRequested &&
                sceneHost == null)
            {
                enabled = false;
                return;
            }

            if (!TryEnsureSceneHostReference(logWarnings: true, disableOnFailure: true))
            {
                return;
            }

            TryEnsurePresenterSubscription();

            if (!_isDirty)
            {
                return;
            }

            var presenter = sceneHost != null ? sceneHost.Presenter : null;
            if (presenter == null)
            {
                return;
            }

            ApplyDesiredVisibility(
                presenter.CurrentTopology,
                presenter.CurrentTopologyTransitionVisualState);
            _isDirty = false;
        }

        private void HandlePresentationStateChanged()
        {
            _isDirty = true;
        }

        private void RefreshAuthoringState(bool logWarnings)
        {
            bindings ??= Array.Empty<TopologyVisualBridgeBinding>();
            _runtimeBindings.Clear();

            var validTargets = new HashSet<GameObject>();

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    LogWarning($"Binding at index {i} is null and will be skipped.", logWarnings);
                    continue;
                }

                var target = binding.TargetObject;
                if (target == null)
                {
                    LogWarning($"Binding at index {i} has no target object and will be skipped.", logWarnings);
                    continue;
                }

                if (binding.FirstFace == binding.SecondFace)
                {
                    LogWarning(
                        $"Binding '{target.name}' uses the same logical face '{binding.FirstFace}' twice and will be skipped.",
                        logWarnings);
                    continue;
                }

                if (transform.IsChildOf(target.transform))
                {
                    LogWarning(
                        $"Binding '{target.name}' points at the controller object or one of its ancestors. The binding will be skipped to avoid disabling the controller root.",
                        logWarnings);
                    continue;
                }

                if (!validTargets.Add(target))
                {
                    LogWarning(
                        $"Binding '{target.name}' is registered more than once. v1 uses first-win ordering and will skip later duplicates.",
                        logWarnings);
                    continue;
                }

                WarnHierarchyOverlap(target, logWarnings);
                _runtimeBindings.Add(new RuntimeBinding(binding));
            }

            PruneAppliedVisibilityCache(validTargets);
            _isDirty = true;
        }

        private void WarnHierarchyOverlap(GameObject target, bool logWarnings)
        {
            var targetTransform = target.transform;

            for (var i = 0; i < _runtimeBindings.Count; i++)
            {
                var existingTarget = _runtimeBindings[i].Binding.TargetObject;
                if (existingTarget == null)
                {
                    continue;
                }

                var existingTransform = existingTarget.transform;
                if (targetTransform.IsChildOf(existingTransform) ||
                    existingTransform.IsChildOf(targetTransform))
                {
                    LogWarning(
                        $"Binding '{target.name}' overlaps hierarchy with '{existingTarget.name}'. Parent/child bridge targets are allowed in v1 but can cause redundant active-root control.",
                        logWarnings);
                    return;
                }
            }
        }

        private void ApplyDesiredVisibility(
            CubeTopologyState topology,
            TopologyTransitionVisualState transitionState)
        {
            for (var i = 0; i < _runtimeBindings.Count; i++)
            {
                var binding = _runtimeBindings[i].Binding;
                var target = binding.TargetObject;
                if (target == null)
                {
                    continue;
                }

                var desiredVisibility = EvaluateBindingVisibility(binding, topology, transitionState);
                var isAlreadyApplied =
                    _appliedVisibilityByTarget.TryGetValue(target, out var lastAppliedVisibility) &&
                    lastAppliedVisibility == desiredVisibility &&
                    target.activeSelf == desiredVisibility;

                if (isAlreadyApplied)
                {
                    continue;
                }

                if (target.activeSelf != desiredVisibility)
                {
                    target.SetActive(desiredVisibility);
                    _setActiveApplyCount++;
                }

                _appliedVisibilityByTarget[target] = desiredVisibility;
            }
        }

        private void PruneAppliedVisibilityCache(HashSet<GameObject> validTargets)
        {
            _staleTargets.Clear();

            foreach (var target in _appliedVisibilityByTarget.Keys)
            {
                if (!validTargets.Contains(target))
                {
                    _staleTargets.Add(target);
                }
            }

            for (var i = 0; i < _staleTargets.Count; i++)
            {
                _appliedVisibilityByTarget.Remove(_staleTargets[i]);
            }
        }

        private bool TryEnsureSceneHostReference(bool logWarnings, bool disableOnFailure)
        {
            if (sceneHost != null)
            {
                return true;
            }

            return TryAutoAssignSceneHost(logWarnings, disableOnFailure);
        }

        private bool TryAutoAssignSceneHost(bool logWarnings, bool disableOnFailure)
        {
            var candidates = FindObjectsByType<GameplaySceneHost>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (candidates.Length == 1)
            {
                sceneHost = candidates[0];
                _sceneHostResolutionDisableRequested = false;
                return true;
            }

            if (logWarnings)
            {
                if (candidates.Length == 0)
                {
                    UnityEngine.Debug.LogWarning(
                        $"{LogPrefix} '{name}' could not find a {nameof(GameplaySceneHost)} in the scene. The controller will disable itself.",
                        this);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        $"{LogPrefix} '{name}' found {candidates.Length} {nameof(GameplaySceneHost)} candidates. Expected exactly one; the controller will disable itself.",
                        this);
                }
            }

            if (disableOnFailure)
            {
                _sceneHostResolutionDisableRequested = true;
                enabled = false;
            }

            return false;
        }

        private void TryEnsurePresenterSubscription()
        {
            var presenter = sceneHost != null ? sceneHost.Presenter : null;
            if (ReferenceEquals(_subscribedPresenter, presenter))
            {
                return;
            }

            DetachPresenter();
            if (presenter == null)
            {
                return;
            }

            _subscribedPresenter = presenter;
            _subscribedPresenter.PresentationStateChanged += HandlePresentationStateChanged;
            _isDirty = true;
        }

        private void DetachPresenter()
        {
            if (_subscribedPresenter == null)
            {
                return;
            }

            _subscribedPresenter.PresentationStateChanged -= HandlePresentationStateChanged;
            _subscribedPresenter = null;
        }

        private void LogWarning(string message, bool logWarnings)
        {
            if (!logWarnings)
            {
                return;
            }

            UnityEngine.Debug.LogWarning($"{LogPrefix} {message}", this);
        }

        /// <summary>
        /// Evaluates bridge visibility for one binding.
        /// v1 intentionally uses logical FaceId pair visibility.
        /// If visual expectation later proves closer to projector slot relations, only this rule seam should change.
        /// </summary>
        internal static bool EvaluateBindingVisibility(
            TopologyVisualBridgeBinding binding,
            CubeTopologyState topology,
            TopologyTransitionVisualState transitionState)
        {
            if (binding == null)
            {
                return false;
            }

            // v1 uses logical FaceId pairs. Future slot-pair evaluation can replace this helper without
            // changing authored binding storage or the controller event/apply flow.
            if (!transitionState.IsActive)
            {
                return topology.IsFaceActive(binding.FirstFace) &&
                       topology.IsFaceActive(binding.SecondFace);
            }

            return transitionState.DestinationTopology.IsFaceActive(binding.FirstFace) &&
                   transitionState.DestinationTopology.IsFaceActive(binding.SecondFace);
        }

        private readonly struct RuntimeBinding
        {
            public RuntimeBinding(TopologyVisualBridgeBinding binding)
            {
                Binding = binding;
            }

            public TopologyVisualBridgeBinding Binding { get; }
        }
    }
}
