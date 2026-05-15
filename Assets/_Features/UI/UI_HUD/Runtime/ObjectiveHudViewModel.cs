using System;
using System.Collections.Generic;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveConditionHudViewModel
    {
        public ObjectiveConditionHudViewModel(
            string stableId,
            string text,
            bool isSatisfied,
            bool justSatisfied)
        {
            StableId = stableId ?? string.Empty;
            Text = text ?? string.Empty;
            IsSatisfied = isSatisfied;
            JustSatisfied = justSatisfied;
        }

        public string StableId { get; }

        public string Text { get; }

        public bool IsSatisfied { get; }

        public bool JustSatisfied { get; }
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
                    left[i].JustSatisfied != right[i].JustSatisfied)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
