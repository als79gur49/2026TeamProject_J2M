using System;
using System.Collections.Generic;

namespace Game.Feature.UI.HUD
{
    public enum ObjectiveHudRowKind
    {
        Single = 0,
        ButtonGroupGeneric = 1,
        ButtonGroupMoon = 2,
    }

    public sealed class ObjectiveConditionHudViewModel
    {
        public ObjectiveConditionHudViewModel(
            string stableId,
            string text,
            bool isSatisfied,
            bool justSatisfied,
            bool isGrouped = false,
            int completedCount = 0,
            int requiredCount = 1,
            ObjectiveHudRowKind rowKind = ObjectiveHudRowKind.Single,
            string groupKey = "")
        {
            StableId = stableId ?? string.Empty;
            Text = text ?? string.Empty;
            IsSatisfied = isSatisfied;
            JustSatisfied = justSatisfied;
            IsGrouped = isGrouped;
            CompletedCount = Math.Max(0, completedCount);
            RequiredCount = Math.Max(0, requiredCount);
            RowKind = rowKind;
            GroupKey = groupKey ?? string.Empty;
        }

        public string StableId { get; }

        public string Text { get; }

        public bool IsSatisfied { get; }

        public bool JustSatisfied { get; }

        public bool IsGrouped { get; }

        public int CompletedCount { get; }

        public int RequiredCount { get; }

        public ObjectiveHudRowKind RowKind { get; }

        public string GroupKey { get; }
    }

    public sealed class ObjectiveHudViewModel
    {
        private static readonly ObjectiveConditionHudViewModel[] EmptyRows =
            Array.Empty<ObjectiveConditionHudViewModel>();

        public event Action Changed;

        public bool IsVisible { get; private set; }

        public bool HasObjective => IsVisible;

        public string ObjectiveStableId { get; private set; } = string.Empty;

        public IReadOnlyList<ObjectiveConditionHudViewModel> Rows { get; private set; } = EmptyRows;

        public void SetState(
            bool isVisible,
            IReadOnlyList<ObjectiveConditionHudViewModel> rows)
        {
            SetState(isVisible, string.Empty, rows);
        }

        public void SetState(
            bool isVisible,
            string objectiveStableId,
            IReadOnlyList<ObjectiveConditionHudViewModel> rows)
        {
            var nextRows = CopyRows(rows);
            var nextObjectiveStableId = objectiveStableId ?? string.Empty;
            if (IsVisible == isVisible &&
                string.Equals(ObjectiveStableId, nextObjectiveStableId, StringComparison.Ordinal) &&
                RowsEqual(Rows, nextRows))
            {
                return;
            }

            IsVisible = isVisible;
            ObjectiveStableId = nextObjectiveStableId;
            Rows = nextRows;
            Changed?.Invoke();
        }

        public void Reset()
        {
            SetState(false, string.Empty, EmptyRows);
        }

        private static ObjectiveConditionHudViewModel[] CopyRows(
            IReadOnlyList<ObjectiveConditionHudViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return EmptyRows;
            }

            var copy = new ObjectiveConditionHudViewModel[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                copy[i] = rows[i];
            }

            return copy;
        }

        private static bool RowsEqual(
            IReadOnlyList<ObjectiveConditionHudViewModel> left,
            IReadOnlyList<ObjectiveConditionHudViewModel> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i].StableId, right[i].StableId, StringComparison.Ordinal) ||
                    !string.Equals(left[i].Text, right[i].Text, StringComparison.Ordinal) ||
                    left[i].IsSatisfied != right[i].IsSatisfied ||
                    left[i].JustSatisfied != right[i].JustSatisfied ||
                    left[i].IsGrouped != right[i].IsGrouped ||
                    left[i].CompletedCount != right[i].CompletedCount ||
                    left[i].RequiredCount != right[i].RequiredCount ||
                    left[i].RowKind != right[i].RowKind ||
                    !string.Equals(left[i].GroupKey, right[i].GroupKey, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
