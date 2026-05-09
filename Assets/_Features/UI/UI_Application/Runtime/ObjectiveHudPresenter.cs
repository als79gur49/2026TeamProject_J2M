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

        public ObjectiveHudViewModel ViewModel { get; } = new();

        public void Apply(UIObjectiveSlice objective)
        {
            if (!objective.HasObjective)
            {
                ViewModel.Reset();
                _previousSatisfiedByStableId.Clear();
                _hasPrevious = false;
                return;
            }

            var rows = BuildRows(objective);
            ViewModel.SetState(true, rows);

            _previousSatisfiedByStableId.Clear();
            for (var i = 0; i < rows.Count; i++)
            {
                _previousSatisfiedByStableId[rows[i].StableId] = rows[i].IsSatisfied;
            }

            _hasPrevious = true;
        }

        private IReadOnlyList<ObjectiveConditionHudViewModel> BuildRows(UIObjectiveSlice objective)
        {
            var conditions = new List<UIObjectiveConditionSlice>(objective.Conditions);
            conditions.Sort(CompareConditions);
            var rows = new List<ObjectiveConditionHudViewModel>(conditions.Count);

            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (string.IsNullOrWhiteSpace(condition.TitleText))
                {
                    continue;
                }

                var stableId = ResolveStableId(condition, i);
                var justSatisfied = _hasPrevious &&
                    condition.IsSatisfied &&
                    _previousSatisfiedByStableId.TryGetValue(stableId, out var wasSatisfied) &&
                    !wasSatisfied;
                rows.Add(new ObjectiveConditionHudViewModel(
                    stableId,
                    condition.TitleText,
                    condition.IsSatisfied,
                    justSatisfied));
            }

            return rows;
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
