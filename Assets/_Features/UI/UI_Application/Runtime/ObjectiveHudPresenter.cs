using System;
using System.Collections.Generic;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveHudPresenter
    {
        private readonly Dictionary<string, bool> _previousSatisfiedByStableId =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private bool _hasPrevious;
        private bool _previousComplete;
        private int _sequenceId;

        public ObjectiveHudViewModel ViewModel { get; } = new();

        public void Apply(UIObjectiveSlice objective)
        {
            if (!objective.HasObjective)
            {
                ViewModel.SetDropdownState(
                    false,
                    string.Empty,
                    string.Empty,
                    Array.Empty<ObjectiveConditionHudViewModel>(),
                    0,
                    0,
                    false,
                    ObjectiveExpansionMode.Collapsed,
                    ObjectiveDropdownAnimationHint.None);
                _previousSatisfiedByStableId.Clear();
                _hasPrevious = false;
                _previousComplete = false;
                return;
            }

            var completedRequired = 0;
            var totalRequired = 0;
            var rows = BuildRows(objective, ref completedRequired, ref totalRequired, out var hasChangedRows);
            var isComplete = objective.IsCleared || objective.AllConditionsSatisfied;
            var pulseComplete = _hasPrevious && !_previousComplete && isComplete;
            var hint = pulseComplete || hasChangedRows
                ? new ObjectiveDropdownAnimationHint(pulseComplete, hasChangedRows, NextSequenceId())
                : ObjectiveDropdownAnimationHint.None;
            var expansionMode = ResolveExpansionMode(hasChangedRows, pulseComplete);

            ViewModel.SetDropdownState(
                true,
                ResolveMainGoalText(objective),
                objective.Summary,
                rows,
                completedRequired,
                totalRequired,
                isComplete,
                expansionMode,
                hint);

            _previousSatisfiedByStableId.Clear();
            for (var i = 0; i < rows.Count; i++)
            {
                _previousSatisfiedByStableId[rows[i].StableId] = rows[i].IsSatisfied;
            }

            _previousComplete = isComplete;
            _hasPrevious = true;
        }

        public void ToggleExpanded()
        {
            ViewModel.ToggleExpanded();
        }

        private ObjectiveExpansionMode ResolveExpansionMode(
            bool hasChangedRows,
            bool pulseComplete)
        {
            if (ViewModel.ExpansionMode == ObjectiveExpansionMode.ManualExpanded)
            {
                return ObjectiveExpansionMode.ManualExpanded;
            }

            if (!_hasPrevious || hasChangedRows || pulseComplete)
            {
                return ObjectiveExpansionMode.AutoExpanded;
            }

            return ViewModel.ExpansionMode;
        }

        private IReadOnlyList<ObjectiveConditionHudViewModel> BuildRows(
            UIObjectiveSlice objective,
            ref int completedRequired,
            ref int totalRequired,
            out bool hasChangedRows)
        {
            hasChangedRows = false;
            var conditions = new List<UIObjectiveConditionSlice>(objective.Conditions);
            conditions.Sort(CompareConditions);
            var rows = new List<ObjectiveConditionHudViewModel>(conditions.Count);

            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition.Required)
                {
                    totalRequired++;
                    if (condition.IsSatisfied)
                    {
                        completedRequired++;
                    }
                }

                var stableId = ResolveStableId(condition, i);
                var justSatisfied = _hasPrevious &&
                    condition.IsSatisfied &&
                    (!_previousSatisfiedByStableId.TryGetValue(stableId, out var wasSatisfied) || !wasSatisfied);
                hasChangedRows |= justSatisfied;
                rows.Add(new ObjectiveConditionHudViewModel(
                    stableId,
                    condition.TitleText,
                    condition.IsSatisfied,
                    condition.Required,
                    MapHudRole(condition.Role),
                    condition.ProgressText,
                    justSatisfied,
                    condition.SortOrder));
            }

            return rows;
        }

        private static string ResolveMainGoalText(UIObjectiveSlice objective)
        {
            if (!string.IsNullOrWhiteSpace(objective.Title))
            {
                return objective.Title;
            }

            var conditions = objective.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i].Role == UIObjectiveConditionRole.PrimaryGoal &&
                    !string.IsNullOrWhiteSpace(conditions[i].TitleText))
                {
                    return conditions[i].TitleText;
                }
            }

            if (!string.IsNullOrWhiteSpace(objective.Summary))
            {
                return objective.Summary;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i].Required &&
                    !string.IsNullOrWhiteSpace(conditions[i].TitleText))
                {
                    return conditions[i].TitleText;
                }
            }

            return string.Empty;
        }

        private int NextSequenceId()
        {
            _sequenceId++;
            return _sequenceId;
        }

        private static ObjectiveConditionHudRole MapHudRole(UIObjectiveConditionRole role)
        {
            switch (role)
            {
                case UIObjectiveConditionRole.PrimaryGoal:
                    return ObjectiveConditionHudRole.PrimaryGoal;
                case UIObjectiveConditionRole.SecondaryGoal:
                    return ObjectiveConditionHudRole.SecondaryGoal;
                case UIObjectiveConditionRole.Challenge:
                    return ObjectiveConditionHudRole.Challenge;
                case UIObjectiveConditionRole.None:
                default:
                    return ObjectiveConditionHudRole.None;
            }
        }

        private static string ResolveStableId(
            UIObjectiveConditionSlice condition,
            int index)
        {
            if (!string.IsNullOrWhiteSpace(condition.StableId))
            {
                return condition.StableId;
            }

            if (!string.IsNullOrWhiteSpace(condition.TitleText))
            {
                return condition.TitleText;
            }

            return $"row-{index}";
        }

        private static int CompareConditions(
            UIObjectiveConditionSlice left,
            UIObjectiveConditionSlice right)
        {
            var sortComparison = left.SortOrder.CompareTo(right.SortOrder);
            if (sortComparison != 0)
            {
                return sortComparison;
            }

            return string.Compare(left.TitleText, right.TitleText, StringComparison.Ordinal);
        }
    }
}
