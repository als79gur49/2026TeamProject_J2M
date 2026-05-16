using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    internal enum ObjectiveHudCollectionTransitionKind
    {
        None,
        Enter,
        Exit,
    }

    internal enum ObjectiveHudExitReason
    {
        CompletedDismiss,
        RemovedFromTarget,
    }

    internal readonly struct ObjectiveHudExitIntent
    {
        public ObjectiveHudExitIntent(string stableId, ObjectiveHudExitReason reason)
        {
            StableId = stableId ?? string.Empty;
            Reason = reason;
        }

        public string StableId { get; }

        public ObjectiveHudExitReason Reason { get; }
    }

    public sealed class ObjectiveHudView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _objectiveListRoot;
        [SerializeField] private RectTransform _objectiveItemTemplate;

        private readonly Dictionary<string, ObjectiveHudRowView> _activeRowsByStableId =
            new Dictionary<string, ObjectiveHudRowView>(StringComparer.Ordinal);
        private readonly Stack<ObjectiveHudRowView> _pool = new Stack<ObjectiveHudRowView>();
        private readonly HashSet<string> _dismissedCompletedStableIds =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, ObjectiveConditionHudViewModel> _targetRowsByStableId =
            new Dictionary<string, ObjectiveConditionHudViewModel>(StringComparer.Ordinal);
        private readonly List<string> _targetOrder = new List<string>();

        private readonly Queue<string> _pendingEnterStableIds = new Queue<string>();
        private readonly HashSet<string> _pendingEnterSet =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly Queue<ObjectiveHudExitIntent> _pendingExitIntents =
            new Queue<ObjectiveHudExitIntent>();
        private readonly HashSet<string> _pendingExitSet =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, ObjectiveHudExitReason> _activeExitReasonsByStableId =
            new Dictionary<string, ObjectiveHudExitReason>(StringComparer.Ordinal);
        private readonly HashSet<string> _completedDismissRequestedStableIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _previousTargetSatisfiedByStableId =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _lastVisibleCompletedCountsByStableId =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly List<string> _scratchStableIds = new List<string>();
        private ObjectiveHudViewModel _viewModel;
        private string _transitioningStableId = string.Empty;
        private ObjectiveHudCollectionTransitionKind _transitioningKind =
            ObjectiveHudCollectionTransitionKind.None;
        private string _currentObjectiveStableId;
        private int _createdRowCount;
        private bool _transitionAdvanceRequested;
        private float _nextTransitionAllowedAt;
        private bool _isProcessingTransitionAdvance;
        private bool _isForceClearing;
        private bool _isDestroyedOrDisabled;

        [SerializeField] private float collectionTransitionGapSeconds = 0.05f;

        public ObjectiveHudViewModel ViewModel => _viewModel;

        private void Awake()
        {
            HideAuthoredListChildren();
        }

        public void Bind(ObjectiveHudViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_objectiveListRoot, nameof(_objectiveListRoot));
            RequireReference(_objectiveItemTemplate, nameof(_objectiveItemTemplate));

            if (_objectiveItemTemplate.transform.parent != _objectiveListRoot)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template must be a direct child of Objective_List.");
            }

            var layoutGroup = _objectiveListRoot.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} Objective_List must use a VerticalLayoutGroup.");
            }

            if (!layoutGroup.childControlHeight)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} Objective_List must control child height for row collapse transitions.");
            }

            if (layoutGroup.childForceExpandHeight)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} Objective_List must not force expand child height.");
            }

            if (_objectiveItemTemplate.GetComponent<ObjectiveHudRowView>() == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template is missing an ObjectiveHudRowView.");
            }

            if (_objectiveItemTemplate.GetComponent<LayoutElement>() == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template is missing a LayoutElement.");
            }

            if (_objectiveItemTemplate.GetComponent<Animator>() == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template is missing an Animator.");
            }

            if (!HasObjectiveLabel(_objectiveItemTemplate.gameObject))
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template is missing Label_Objective.");
            }
        }

        private void OnEnable()
        {
            _isDestroyedOrDisabled = false;
            RefreshView();
        }

        private void OnDisable()
        {
            _isDestroyedOrDisabled = true;
            ForceClearAllRows(clearDismissed: true);
            _currentObjectiveStableId = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_objectiveListRoot, nameof(_objectiveListRoot));
            ValidateSerializedReference(_objectiveItemTemplate, nameof(_objectiveItemTemplate));
        }
#endif

        private void OnDestroy()
        {
            _isDestroyedOrDisabled = true;
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            ForceClearAllRows(clearDismissed: true);
            _currentObjectiveStableId = null;
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void Update()
        {
            ProcessTransitionAdvance(Time.unscaledTime);
        }

        private void RefreshView()
        {
            ValidateAuthoredStructureOrThrow();
            HideAuthoredListChildren();

            var isVisible = _viewModel != null && _viewModel.IsVisible;
            _root.SetActive(isVisible);
            if (!isVisible)
            {
                ForceClearAllRows(clearDismissed: true);
                _currentObjectiveStableId = null;
                return;
            }

            var objectiveStableId = _viewModel.ObjectiveStableId ?? string.Empty;
            if (!string.Equals(_currentObjectiveStableId, objectiveStableId, StringComparison.Ordinal))
            {
                ForceClearAllRows(clearDismissed: true);
                _currentObjectiveStableId = objectiveStableId;
            }

            ReconcileRows(_viewModel.Rows);
        }

        private void ReconcileRows(IReadOnlyList<ObjectiveConditionHudViewModel> rows)
        {
            UpdateTargetRows(rows);
            RefreshIdleActiveRows();
            RebuildPendingQueues();
            if (_transitionAdvanceRequested && !_isProcessingTransitionAdvance)
            {
                return;
            }

            TryStartNextTransition();
        }

        private void RequestTransitionAdvance(float delaySeconds)
        {
            _transitionAdvanceRequested = true;
            _nextTransitionAllowedAt = Time.unscaledTime + Mathf.Max(0.0f, delaySeconds);
        }

        private void CancelTransitionAdvanceRequest()
        {
            _transitionAdvanceRequested = false;
            _nextTransitionAllowedAt = 0.0f;
        }

        private void ProcessTransitionAdvance(float now)
        {
            if (!_transitionAdvanceRequested ||
                now < _nextTransitionAllowedAt)
            {
                return;
            }

            _transitionAdvanceRequested = false;
            if (_isDestroyedOrDisabled ||
                _isForceClearing ||
                _viewModel == null ||
                !_viewModel.IsVisible)
            {
                return;
            }

            _isProcessingTransitionAdvance = true;
            try
            {
                ReconcileRows(_viewModel.Rows);
            }
            finally
            {
                _isProcessingTransitionAdvance = false;
            }
        }

        private void UpdateTargetRows(IReadOnlyList<ObjectiveConditionHudViewModel> rows)
        {
            _targetRowsByStableId.Clear();
            _targetOrder.Clear();

            if (rows != null)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row == null)
                    {
                        continue;
                    }

                    var stableId = row.StableId ?? string.Empty;
                    if (!row.IsSatisfied)
                    {
                        _dismissedCompletedStableIds.Remove(stableId);
                        _completedDismissRequestedStableIds.Remove(stableId);
                    }
                    else if (!_dismissedCompletedStableIds.Contains(stableId) &&
                             (row.JustSatisfied || WasPreviouslyUnsatisfied(stableId)))
                    {
                        _completedDismissRequestedStableIds.Add(stableId);
                    }
                    else if (!_activeRowsByStableId.ContainsKey(stableId))
                    {
                        _dismissedCompletedStableIds.Add(stableId);
                        _completedDismissRequestedStableIds.Remove(stableId);
                    }

                    if (_dismissedCompletedStableIds.Contains(stableId))
                    {
                        continue;
                    }

                    if (!_targetRowsByStableId.ContainsKey(stableId))
                    {
                        _targetRowsByStableId.Add(stableId, row);
                        _targetOrder.Add(stableId);
                    }
                }
            }

            UpdatePreviousSatisfiedCache(rows);
        }

        private bool WasPreviouslyUnsatisfied(string stableId)
        {
            return _previousTargetSatisfiedByStableId.TryGetValue(stableId, out var wasSatisfied) &&
                   !wasSatisfied;
        }

        private void UpdatePreviousSatisfiedCache(IReadOnlyList<ObjectiveConditionHudViewModel> rows)
        {
            _previousTargetSatisfiedByStableId.Clear();
            if (rows == null)
            {
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }

                var stableId = row.StableId ?? string.Empty;
                if (!_previousTargetSatisfiedByStableId.ContainsKey(stableId))
                {
                    _previousTargetSatisfiedByStableId.Add(stableId, row.IsSatisfied);
                }
            }
        }

        private void RefreshIdleActiveRows()
        {
            _scratchStableIds.Clear();
            foreach (var pair in _activeRowsByStableId)
            {
                _scratchStableIds.Add(pair.Key);
            }

            for (var i = 0; i < _scratchStableIds.Count; i++)
            {
                var stableId = _scratchStableIds[i];
                if (IsTransitioningStableId(stableId) ||
                    _activeExitReasonsByStableId.ContainsKey(stableId) ||
                    !_targetRowsByStableId.TryGetValue(stableId, out var target) ||
                    !_activeRowsByStableId.TryGetValue(stableId, out var rowView) ||
                    rowView == null)
                {
                    continue;
                }

                rowView.Refresh(target);
                UpdateProgressFeedback(stableId, rowView, target);
            }
        }

        private void UpdateProgressFeedback(
            string stableId,
            ObjectiveHudRowView rowView,
            ObjectiveConditionHudViewModel target)
        {
            var currentCompletedCount = Mathf.Max(0, target.CompletedCount);
            if (!_lastVisibleCompletedCountsByStableId.TryGetValue(stableId, out var previousCompletedCount))
            {
                _lastVisibleCompletedCountsByStableId[stableId] = currentCompletedCount;
                return;
            }

            if (ShouldPlayProgressPulse(rowView, target, previousCompletedCount, currentCompletedCount))
            {
                rowView.PlayProgressPulse();
            }

            _lastVisibleCompletedCountsByStableId[stableId] = currentCompletedCount;
        }

        private static bool ShouldPlayProgressPulse(
            ObjectiveHudRowView rowView,
            ObjectiveConditionHudViewModel target,
            int previousCompletedCount,
            int currentCompletedCount)
        {
            return rowView != null &&
                   rowView.VisualState == ObjectiveRowVisualState.Idle &&
                   target != null &&
                   target.IsGrouped &&
                   !target.IsSatisfied &&
                   target.RequiredCount > 1 &&
                   currentCompletedCount > previousCompletedCount &&
                   currentCompletedCount < target.RequiredCount;
        }

        private void RebuildPendingQueues()
        {
            _pendingEnterStableIds.Clear();
            _pendingEnterSet.Clear();
            _pendingExitIntents.Clear();
            _pendingExitSet.Clear();

            _scratchStableIds.Clear();
            foreach (var pair in _activeRowsByStableId)
            {
                if (pair.Value != null)
                {
                    _scratchStableIds.Add(pair.Key);
                }
            }

            _scratchStableIds.Sort(CompareActiveRowsBySiblingIndex);
            for (var i = 0; i < _scratchStableIds.Count; i++)
            {
                var stableId = _scratchStableIds[i];
                if (IsTransitioningStableId(stableId))
                {
                    continue;
                }

                if (!_targetRowsByStableId.TryGetValue(stableId, out var target))
                {
                    EnqueueExit(new ObjectiveHudExitIntent(
                        stableId,
                        ObjectiveHudExitReason.RemovedFromTarget));
                    continue;
                }

                if (target.IsSatisfied &&
                    _completedDismissRequestedStableIds.Contains(stableId) &&
                    !_dismissedCompletedStableIds.Contains(stableId))
                {
                    EnqueueExit(new ObjectiveHudExitIntent(
                        stableId,
                        ObjectiveHudExitReason.CompletedDismiss));
                }
            }

            for (var i = 0; i < _targetOrder.Count; i++)
            {
                var stableId = _targetOrder[i];
                if (IsEnterCandidate(stableId))
                {
                    _pendingEnterStableIds.Enqueue(stableId);
                    _pendingEnterSet.Add(stableId);
                }
            }
        }

        private int CompareActiveRowsBySiblingIndex(string left, string right)
        {
            var leftIndex = _activeRowsByStableId.TryGetValue(left, out var leftRow) && leftRow != null
                ? leftRow.transform.GetSiblingIndex()
                : int.MaxValue;
            var rightIndex = _activeRowsByStableId.TryGetValue(right, out var rightRow) && rightRow != null
                ? rightRow.transform.GetSiblingIndex()
                : int.MaxValue;

            var siblingComparison = leftIndex.CompareTo(rightIndex);
            return siblingComparison != 0
                ? siblingComparison
                : string.Compare(left, right, StringComparison.Ordinal);
        }

        private void EnqueueExit(ObjectiveHudExitIntent intent)
        {
            if (_pendingExitSet.Add(intent.StableId))
            {
                _pendingExitIntents.Enqueue(intent);
            }
        }

        private bool IsEnterCandidate(string stableId)
        {
            return _targetRowsByStableId.ContainsKey(stableId) &&
                   !_dismissedCompletedStableIds.Contains(stableId) &&
                   !_activeRowsByStableId.ContainsKey(stableId) &&
                   !_pendingEnterSet.Contains(stableId) &&
                   !_pendingExitSet.Contains(stableId) &&
                   !IsTransitioningStableId(stableId);
        }

        private void TryStartNextTransition()
        {
            if (_transitioningKind != ObjectiveHudCollectionTransitionKind.None)
            {
                return;
            }

            while (_pendingExitIntents.Count > 0)
            {
                var intent = _pendingExitIntents.Dequeue();
                _pendingExitSet.Remove(intent.StableId);
                if (!IsValidExitIntent(intent))
                {
                    continue;
                }

                StartExit(intent);
                return;
            }

            while (_pendingEnterStableIds.Count > 0)
            {
                var stableId = _pendingEnterStableIds.Dequeue();
                _pendingEnterSet.Remove(stableId);
                if (!IsValidEnter(stableId))
                {
                    continue;
                }

                StartEnter(stableId);
                return;
            }

            ApplyFinalSiblingOrderIfSafe();
        }

        private bool IsValidExitIntent(ObjectiveHudExitIntent intent)
        {
            if (!_activeRowsByStableId.ContainsKey(intent.StableId) ||
                IsTransitioningStableId(intent.StableId))
            {
                return false;
            }

            if (intent.Reason == ObjectiveHudExitReason.RemovedFromTarget)
            {
                return !_targetRowsByStableId.ContainsKey(intent.StableId);
            }

            return _targetRowsByStableId.TryGetValue(intent.StableId, out var target) &&
                   target.IsSatisfied &&
                   _completedDismissRequestedStableIds.Contains(intent.StableId) &&
                   !_dismissedCompletedStableIds.Contains(intent.StableId);
        }

        private bool IsValidEnter(string stableId)
        {
            return _targetRowsByStableId.TryGetValue(stableId, out var target) &&
                   !_dismissedCompletedStableIds.Contains(stableId) &&
                   !target.IsSatisfied &&
                   !_activeRowsByStableId.ContainsKey(stableId) &&
                   !_pendingExitSet.Contains(stableId) &&
                   !IsTransitioningStableId(stableId);
        }

        private void StartExit(ObjectiveHudExitIntent intent)
        {
            if (!_activeRowsByStableId.TryGetValue(intent.StableId, out var rowView) ||
                rowView == null)
            {
                return;
            }

            _activeExitReasonsByStableId[intent.StableId] = intent.Reason;
            _transitioningStableId = intent.StableId;
            _transitioningKind = ObjectiveHudCollectionTransitionKind.Exit;

            if (intent.Reason == ObjectiveHudExitReason.CompletedDismiss)
            {
                rowView.CompleteAndDismiss();
                return;
            }

            rowView.ExitAndDismiss();
        }

        private void StartEnter(string stableId)
        {
            if (!_targetRowsByStableId.TryGetValue(stableId, out var target))
            {
                return;
            }

            var rowView = GetRowFromPool();
            rowView.TransitionFinished -= HandleRowTransitionFinished;
            rowView.TransitionFinished += HandleRowTransitionFinished;

            var siblingIndex = CalculateTargetInsertSiblingIndex(stableId);
            _activeRowsByStableId[stableId] = rowView;
            rowView.transform.SetSiblingIndex(siblingIndex);
            _lastVisibleCompletedCountsByStableId[stableId] = Mathf.Max(0, target.CompletedCount);

            _transitioningStableId = stableId;
            _transitioningKind = ObjectiveHudCollectionTransitionKind.Enter;
            rowView.PlayEnter(target);
        }

        private int CalculateTargetInsertSiblingIndex(string stableId)
        {
            var targetIndex = _targetOrder.IndexOf(stableId);
            if (targetIndex < 0)
            {
                return _objectiveListRoot != null ? _objectiveListRoot.childCount : 0;
            }

            for (var i = targetIndex - 1; i >= 0; i--)
            {
                if (_activeRowsByStableId.TryGetValue(_targetOrder[i], out var previousRow) &&
                    previousRow != null)
                {
                    return previousRow.transform.GetSiblingIndex() + 1;
                }
            }

            for (var i = targetIndex + 1; i < _targetOrder.Count; i++)
            {
                if (_activeRowsByStableId.TryGetValue(_targetOrder[i], out var nextRow) &&
                    nextRow != null)
                {
                    return nextRow.transform.GetSiblingIndex();
                }
            }

            return 0;
        }

        private void HandleRowTransitionFinished(
            ObjectiveHudRowView rowView,
            ObjectiveRowTransitionKind transitionKind)
        {
            if (rowView == null)
            {
                return;
            }

            if (transitionKind == ObjectiveRowTransitionKind.Enter)
            {
                HandleRowEnterFinished(rowView);
                return;
            }

            HandleRowDismissFinished(rowView);
        }

        private void HandleRowEnterFinished(ObjectiveHudRowView rowView)
        {
            if (!IsCurrentTransition(rowView, ObjectiveHudCollectionTransitionKind.Enter))
            {
                return;
            }

            var stableId = rowView.StableId ?? string.Empty;
            ClearTransition();
            if (_targetRowsByStableId.TryGetValue(stableId, out var target) &&
                _activeRowsByStableId.TryGetValue(stableId, out var activeRow) &&
                ReferenceEquals(activeRow, rowView))
            {
                rowView.Refresh(target);
                _lastVisibleCompletedCountsByStableId[stableId] = Mathf.Max(0, target.CompletedCount);
            }

            RequestTransitionAdvance(collectionTransitionGapSeconds);
        }

        private void HandleRowDismissFinished(ObjectiveHudRowView rowView)
        {
            if (!IsCurrentTransition(rowView, ObjectiveHudCollectionTransitionKind.Exit))
            {
                return;
            }

            var stableId = rowView.StableId ?? string.Empty;
            var hasExitReason = _activeExitReasonsByStableId.TryGetValue(stableId, out var exitReason);
            _activeExitReasonsByStableId.Remove(stableId);

            if (_activeRowsByStableId.TryGetValue(stableId, out var activeRow) &&
                ReferenceEquals(activeRow, rowView))
            {
                _activeRowsByStableId.Remove(stableId);
            }

            if (hasExitReason &&
                exitReason == ObjectiveHudExitReason.CompletedDismiss &&
                _targetRowsByStableId.TryGetValue(stableId, out var target) &&
                target.IsSatisfied)
            {
                _dismissedCompletedStableIds.Add(stableId);
            }
            else if (!hasExitReason ||
                     exitReason == ObjectiveHudExitReason.RemovedFromTarget ||
                     (_targetRowsByStableId.TryGetValue(stableId, out target) && !target.IsSatisfied))
            {
                _dismissedCompletedStableIds.Remove(stableId);
            }

            _completedDismissRequestedStableIds.Remove(stableId);
            _lastVisibleCompletedCountsByStableId.Remove(stableId);

            ClearTransition();

            ReturnRowToPool(rowView);
            RequestTransitionAdvance(collectionTransitionGapSeconds);
        }

        private bool IsCurrentTransition(
            ObjectiveHudRowView rowView,
            ObjectiveHudCollectionTransitionKind expectedKind)
        {
            if (_isDestroyedOrDisabled ||
                _isForceClearing ||
                rowView == null ||
                expectedKind == ObjectiveHudCollectionTransitionKind.None)
            {
                return false;
            }

            var stableId = rowView.StableId ?? string.Empty;
            if (string.IsNullOrEmpty(stableId) ||
                !string.Equals(_transitioningStableId, stableId, StringComparison.Ordinal) ||
                _transitioningKind != expectedKind)
            {
                return false;
            }

            return _activeRowsByStableId.TryGetValue(stableId, out var activeRow) &&
                   ReferenceEquals(activeRow, rowView);
        }

        private void ApplyFinalSiblingOrderIfSafe()
        {
            if (_transitioningKind != ObjectiveHudCollectionTransitionKind.None ||
                _pendingExitIntents.Count > 0 ||
                _activeExitReasonsByStableId.Count > 0)
            {
                return;
            }

            var siblingIndex = 0;
            for (var i = 0; i < _targetOrder.Count; i++)
            {
                if (_activeRowsByStableId.TryGetValue(_targetOrder[i], out var rowView) &&
                    rowView != null)
                {
                    rowView.transform.SetSiblingIndex(siblingIndex);
                    siblingIndex++;
                }
            }
        }

        private bool IsTransitioningStableId(string stableId)
        {
            return _transitioningKind != ObjectiveHudCollectionTransitionKind.None &&
                   string.Equals(_transitioningStableId, stableId, StringComparison.Ordinal);
        }

        private void ClearTransition()
        {
            _transitioningStableId = string.Empty;
            _transitioningKind = ObjectiveHudCollectionTransitionKind.None;
        }

        private ObjectiveHudRowView GetRowFromPool()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            return CreateRow();
        }

        private ObjectiveHudRowView CreateRow()
        {
            var itemTransform = Instantiate(_objectiveItemTemplate, _objectiveListRoot);
            itemTransform.name = $"Objective_Item_Runtime_{_createdRowCount:00}";
            _createdRowCount++;
            itemTransform.gameObject.SetActive(false);

            var rowView = itemTransform.GetComponent<ObjectiveHudRowView>();
            if (rowView == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} runtime item is missing an ObjectiveHudRowView.");
            }

            rowView.Initialize();
            rowView.ForceResetForPool();
            return rowView;
        }

        private void ReturnRowToPool(ObjectiveHudRowView rowView)
        {
            rowView.TransitionFinished -= HandleRowTransitionFinished;
            rowView.ForceResetForPool();
            rowView.gameObject.SetActive(false);
            _pool.Push(rowView);
        }

        private void ForceClearAllRows(bool clearDismissed)
        {
            _isForceClearing = true;
            CancelTransitionAdvanceRequest();
            _scratchStableIds.Clear();
            foreach (var pair in _activeRowsByStableId)
            {
                _scratchStableIds.Add(pair.Key);
            }

            for (var i = 0; i < _scratchStableIds.Count; i++)
            {
                var stableId = _scratchStableIds[i];
                if (!_activeRowsByStableId.TryGetValue(stableId, out var rowView) || rowView == null)
                {
                    continue;
                }

                rowView.TransitionFinished -= HandleRowTransitionFinished;
                rowView.ForceResetForPool();
                rowView.gameObject.SetActive(false);
                _pool.Push(rowView);
            }

            _activeRowsByStableId.Clear();
            _targetRowsByStableId.Clear();
            _targetOrder.Clear();
            _pendingEnterStableIds.Clear();
            _pendingEnterSet.Clear();
            _pendingExitIntents.Clear();
            _pendingExitSet.Clear();
            _activeExitReasonsByStableId.Clear();
            _completedDismissRequestedStableIds.Clear();
            _previousTargetSatisfiedByStableId.Clear();
            _lastVisibleCompletedCountsByStableId.Clear();
            ClearTransition();

            if (clearDismissed)
            {
                _dismissedCompletedStableIds.Clear();
            }

            _isForceClearing = false;
        }

        private void HideAuthoredListChildren()
        {
            if (_objectiveListRoot == null)
            {
                return;
            }

            for (var i = 0; i < _objectiveListRoot.childCount; i++)
            {
                var child = _objectiveListRoot.GetChild(i).gameObject;
                if (IsOwnedRuntimeRow(child))
                {
                    continue;
                }

                child.SetActive(false);
            }
        }

        private bool IsOwnedRuntimeRow(GameObject child)
        {
            foreach (var pair in _activeRowsByStableId)
            {
                if (pair.Value != null && ReferenceEquals(pair.Value.gameObject, child))
                {
                    return true;
                }
            }

            foreach (var pooled in _pool)
            {
                if (pooled != null && ReferenceEquals(pooled.gameObject, child))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasObjectiveLabel(GameObject root)
        {
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i].name == "Label_Objective")
                {
                    return true;
                }
            }

            return false;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} is missing authored reference '{fieldName}'.");
            }
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(ObjectiveHudView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
