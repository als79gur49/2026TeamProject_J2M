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
        private string _previousObjectiveStableId = string.Empty;

        public ObjectiveHudViewModel ViewModel { get; } = new();

        public void Apply(UIObjectiveSlice objective)
        {
            if (!objective.HasObjective)
            {
                ViewModel.Reset();
                _previousSatisfiedByStableId.Clear();
                _hasPrevious = false;
                _previousObjectiveStableId = string.Empty;
                return;
            }

            var objectiveStableId = objective.ObjectiveStableId ?? string.Empty;
            if (!string.Equals(_previousObjectiveStableId, objectiveStableId, StringComparison.Ordinal))
            {
                _previousSatisfiedByStableId.Clear();
                _hasPrevious = false;
            }

            var rows = BuildRows(objective);
            ViewModel.SetState(true, objectiveStableId, rows);

            _previousSatisfiedByStableId.Clear();
            for (var i = 0; i < rows.Count; i++)
            {
                _previousSatisfiedByStableId[rows[i].StableId] = rows[i].IsSatisfied;
            }

            _hasPrevious = true;
            _previousObjectiveStableId = objectiveStableId;
        }

        private IReadOnlyList<ObjectiveConditionHudViewModel> BuildRows(UIObjectiveSlice objective)
        {
            var conditions = new List<UIObjectiveConditionSlice>(objective.Conditions);
            conditions.Sort(CompareConditions);
            var rowCandidates = new List<ObjectiveHudRowCandidate>(conditions.Count);
            var groupsByKey = new Dictionary<string, ObjectiveHudGroupAccumulator>(StringComparer.Ordinal);

            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (string.IsNullOrWhiteSpace(condition.TitleText))
                {
                    continue;
                }

                var stableId = ResolveStableId(condition, i);
                var rowKind = InferRowKind(condition, stableId);
                if (IsGroupable(rowKind))
                {
                    var groupKey = BuildGroupKey(objective.ObjectiveStableId, condition, rowKind);
                    if (!groupsByKey.TryGetValue(groupKey, out var group))
                    {
                        group = new ObjectiveHudGroupAccumulator(
                            groupKey,
                            condition.TitleText,
                            rowKind,
                            condition.SortOrder);
                        groupsByKey.Add(groupKey, group);
                        rowCandidates.Add(ObjectiveHudRowCandidate.ForGroup(group));
                    }

                    group.Add(condition);
                    continue;
                }

                rowCandidates.Add(ObjectiveHudRowCandidate.ForRow(
                    condition.SortOrder,
                    condition.TitleText,
                    BuildSingleRow(stableId, condition)));
            }

            rowCandidates.Sort(CompareRowCandidates);

            var rows = new List<ObjectiveConditionHudViewModel>(rowCandidates.Count);
            for (var i = 0; i < rowCandidates.Count; i++)
            {
                rows.Add(rowCandidates[i].BuildRow(this, objective.ObjectiveStableId));
            }

            return rows;
        }

        private ObjectiveConditionHudViewModel BuildSingleRow(
            string stableId,
            UIObjectiveConditionSlice condition)
        {
            var justSatisfied = IsJustSatisfied(stableId, condition.IsSatisfied);
            return new ObjectiveConditionHudViewModel(
                stableId,
                condition.TitleText,
                condition.IsSatisfied,
                justSatisfied,
                completedCount: condition.IsSatisfied ? 1 : 0,
                requiredCount: 1);
        }

        private ObjectiveConditionHudViewModel BuildGroupedRow(
            ObjectiveHudGroupAccumulator group,
            string objectiveStableId)
        {
            var completedCount = group.CompletedCount;
            var requiredCount = group.RequiredCount;
            var isSatisfied = requiredCount > 0 && completedCount >= requiredCount;
            var stableId = ComputeGroupStableId(objectiveStableId, group);
            var justSatisfied = IsJustSatisfied(stableId, isSatisfied);

            return new ObjectiveConditionHudViewModel(
                stableId,
                $"{group.DisplayText} ({completedCount}/{requiredCount})",
                isSatisfied,
                justSatisfied,
                isGrouped: true,
                completedCount: completedCount,
                requiredCount: requiredCount,
                rowKind: group.RowKind,
                groupKey: group.GroupKey);
        }

        private bool IsJustSatisfied(string stableId, bool isSatisfied)
        {
            return _hasPrevious &&
                   isSatisfied &&
                   _previousSatisfiedByStableId.TryGetValue(stableId, out var wasSatisfied) &&
                   !wasSatisfied;
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

        private static int CompareRowCandidates(
            ObjectiveHudRowCandidate left,
            ObjectiveHudRowCandidate right)
        {
            var sortComparison = left.SortOrder.CompareTo(right.SortOrder);
            if (sortComparison != 0)
            {
                return sortComparison;
            }

            return string.Compare(left.TitleText, right.TitleText, StringComparison.Ordinal);
        }

        private static bool IsGroupable(ObjectiveHudRowKind rowKind)
        {
            return rowKind == ObjectiveHudRowKind.ButtonGroupGeneric ||
                   rowKind == ObjectiveHudRowKind.ButtonGroupMoon;
        }

        private static ObjectiveHudRowKind InferRowKind(
            UIObjectiveConditionSlice condition,
            string stableId)
        {
            if (!IsButtonStableId(stableId))
            {
                return ObjectiveHudRowKind.Single;
            }

            if (string.Equals(
                    condition.TitleText,
                    "Place the MoonBlock on the button",
                    StringComparison.Ordinal))
            {
                return ObjectiveHudRowKind.ButtonGroupMoon;
            }

            return ObjectiveHudRowKind.ButtonGroupGeneric;
        }

        private static bool IsButtonStableId(string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId) &&
                   stableId.StartsWith("button-", StringComparison.Ordinal);
        }

        private static string BuildGroupKey(
            string objectiveStableId,
            UIObjectiveConditionSlice condition,
            ObjectiveHudRowKind rowKind)
        {
            return string.Concat(
                objectiveStableId ?? string.Empty,
                "|",
                condition.TitleText ?? string.Empty,
                "|",
                condition.Role.ToString(),
                "|",
                rowKind.ToString());
        }

        private static string ComputeGroupStableId(
            string objectiveStableId,
            ObjectiveHudGroupAccumulator group)
        {
            var objectivePart = NormalizeStableIdPart(objectiveStableId);
            switch (group.RowKind)
            {
                case ObjectiveHudRowKind.ButtonGroupMoon:
                    return string.Equals(
                            group.DisplayText,
                            "Place the MoonBlock on the button",
                            StringComparison.Ordinal)
                        ? $"objective-{objectivePart}-button-group-moon"
                        : $"objective-{objectivePart}-button-group-moon-{NormalizeStableIdPart(group.GroupKey)}";

                case ObjectiveHudRowKind.ButtonGroupGeneric:
                    return string.Equals(
                            group.DisplayText,
                            "Place a push box on the button",
                            StringComparison.Ordinal)
                        ? $"objective-{objectivePart}-button-group-push"
                        : $"objective-{objectivePart}-button-group-push-{NormalizeStableIdPart(group.GroupKey)}";

                default:
                    return $"objective-{objectivePart}-group-{NormalizeStableIdPart(group.GroupKey)}";
            }
        }

        private static string NormalizeStableIdPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) &&
                    chars[i] != '-' &&
                    chars[i] != '_')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
        }

        private sealed class ObjectiveHudGroupAccumulator
        {
            public ObjectiveHudGroupAccumulator(
                string groupKey,
                string displayText,
                ObjectiveHudRowKind rowKind,
                int sortOrder)
            {
                GroupKey = groupKey ?? string.Empty;
                DisplayText = displayText ?? string.Empty;
                RowKind = rowKind;
                SortOrder = sortOrder;
            }

            public string GroupKey { get; }

            public string DisplayText { get; }

            public ObjectiveHudRowKind RowKind { get; }

            public int SortOrder { get; private set; }

            public int CompletedCount { get; private set; }

            public int RequiredCount { get; private set; }

            public void Add(UIObjectiveConditionSlice condition)
            {
                RequiredCount++;
                if (condition.IsSatisfied)
                {
                    CompletedCount++;
                }

                if (condition.SortOrder < SortOrder)
                {
                    SortOrder = condition.SortOrder;
                }
            }
        }

        private readonly struct ObjectiveHudRowCandidate
        {
            private readonly ObjectiveConditionHudViewModel _row;
            private readonly ObjectiveHudGroupAccumulator _group;

            private ObjectiveHudRowCandidate(
                int sortOrder,
                string titleText,
                ObjectiveConditionHudViewModel row,
                ObjectiveHudGroupAccumulator group)
            {
                SortOrder = sortOrder;
                TitleText = titleText ?? string.Empty;
                _row = row;
                _group = group;
            }

            public int SortOrder { get; }

            public string TitleText { get; }

            public static ObjectiveHudRowCandidate ForRow(
                int sortOrder,
                string titleText,
                ObjectiveConditionHudViewModel row)
            {
                return new ObjectiveHudRowCandidate(sortOrder, titleText, row, null);
            }

            public static ObjectiveHudRowCandidate ForGroup(ObjectiveHudGroupAccumulator group)
            {
                return new ObjectiveHudRowCandidate(group.SortOrder, group.DisplayText, null, group);
            }

            public ObjectiveConditionHudViewModel BuildRow(
                ObjectiveHudPresenter presenter,
                string objectiveStableId)
            {
                return _group != null
                    ? presenter.BuildGroupedRow(_group, objectiveStableId)
                    : _row;
            }
        }
    }
}
