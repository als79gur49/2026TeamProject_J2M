using System;
using System.Collections.Generic;

namespace Game.Feature.UI.HUD
{
    public enum ObjectiveConditionHudRole
    {
        None = 0,
        PrimaryGoal = 1,
        SecondaryGoal = 2,
        Challenge = 3,
    }

    public enum ObjectiveExpansionMode
    {
        Collapsed = 0,
        ManualExpanded = 1,
        AutoExpanded = 2,
    }

    public readonly struct ObjectiveDropdownAnimationHint : IEquatable<ObjectiveDropdownAnimationHint>
    {
        public static readonly ObjectiveDropdownAnimationHint None = new(false, false, 0);

        public ObjectiveDropdownAnimationHint(
            bool pulseComplete,
            bool highlightChangedRows,
            int sequenceId)
        {
            PulseComplete = pulseComplete;
            HighlightChangedRows = highlightChangedRows;
            SequenceId = sequenceId;
        }

        public bool PulseComplete { get; }

        public bool HighlightChangedRows { get; }

        public int SequenceId { get; }

        public bool Equals(ObjectiveDropdownAnimationHint other)
        {
            return PulseComplete == other.PulseComplete &&
                   HighlightChangedRows == other.HighlightChangedRows &&
                   SequenceId == other.SequenceId;
        }

        public override bool Equals(object obj)
        {
            return obj is ObjectiveDropdownAnimationHint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PulseComplete, HighlightChangedRows, SequenceId);
        }
    }

    public sealed class ObjectiveConditionHudViewModel
    {
        public ObjectiveConditionHudViewModel(
            string stableId,
            string title,
            bool isSatisfied,
            bool required,
            ObjectiveConditionHudRole role,
            string progressText,
            bool justSatisfied,
            int sortOrder)
        {
            StableId = stableId ?? string.Empty;
            Title = title ?? string.Empty;
            IsSatisfied = isSatisfied;
            Required = required;
            Role = role;
            ProgressText = progressText ?? string.Empty;
            JustSatisfied = justSatisfied;
            SortOrder = sortOrder;
        }

        public string StableId { get; }

        public string Title { get; }

        public bool IsSatisfied { get; }

        public bool Required { get; }

        public ObjectiveConditionHudRole Role { get; }

        public string ProgressText { get; }

        public bool JustSatisfied { get; }

        public int SortOrder { get; }
    }

    public sealed class ObjectiveHudViewModel
    {
        private static readonly string[] EmptySubGoalTexts = Array.Empty<string>();
        private static readonly ObjectiveConditionHudViewModel[] EmptyRows =
            Array.Empty<ObjectiveConditionHudViewModel>();

        public event Action Changed;

        public bool IsVisible { get; private set; }

        public bool HasObjective => IsVisible;

        public bool IsComplete { get; private set; }

        public bool ShowCompleteBadge { get; private set; }

        public string ObjectiveText { get; private set; } = string.Empty;

        public string MainGoalText { get; private set; } = string.Empty;

        public string Title { get; private set; } = string.Empty;

        public string Summary { get; private set; } = string.Empty;

        public string ProgressText { get; private set; } = string.Empty;

        public int CompletedRequiredCount { get; private set; }

        public int TotalRequiredCount { get; private set; }

        public float Progress01 { get; private set; }

        public IReadOnlyList<string> SubGoalTexts { get; private set; } = EmptySubGoalTexts;

        public IReadOnlyList<ObjectiveConditionHudViewModel> Rows { get; private set; } = EmptyRows;

        public int HiddenSubGoalCount { get; private set; }

        public bool IsExpanded => ExpansionMode != ObjectiveExpansionMode.Collapsed;

        public ObjectiveExpansionMode ExpansionMode { get; private set; }

        public bool CanExpand => Rows.Count > 0 || HiddenSubGoalCount > 0;

        public ObjectiveDropdownAnimationHint AnimationHint { get; private set; } =
            ObjectiveDropdownAnimationHint.None;

        public void SetDropdownState(
            bool isVisible,
            string title,
            string summary,
            IReadOnlyList<ObjectiveConditionHudViewModel> rows,
            int completedRequiredCount,
            int totalRequiredCount,
            bool isComplete,
            ObjectiveExpansionMode expansionMode,
            ObjectiveDropdownAnimationHint animationHint)
        {
            var nextTitle = title ?? string.Empty;
            var nextSummary = summary ?? string.Empty;
            var nextRows = CopyRows(rows);
            var nextTotalRequired = Math.Max(0, totalRequiredCount);
            var nextCompletedRequired = Math.Max(0, Math.Min(completedRequiredCount, nextTotalRequired));
            var nextProgressText = nextTotalRequired > 0
                ? $"{nextCompletedRequired}/{nextTotalRequired}"
                : string.Empty;
            var nextProgress01 = nextTotalRequired > 0
                ? (float)nextCompletedRequired / nextTotalRequired
                : (isComplete ? 1.0f : 0.0f);
            var nextExpansionMode = isVisible && (nextRows.Length > 0 || HiddenSubGoalCount > 0)
                ? expansionMode
                : ObjectiveExpansionMode.Collapsed;
            var nextSubGoalTexts = BuildLegacySubGoalTexts(nextRows);
            var nextObjectiveText = BuildObjectiveText(
                nextTitle,
                nextProgressText,
                nextSubGoalTexts,
                0,
                nextExpansionMode != ObjectiveExpansionMode.Collapsed);

            if (IsVisible == isVisible &&
                IsComplete == isComplete &&
                ShowCompleteBadge == isComplete &&
                CompletedRequiredCount == nextCompletedRequired &&
                TotalRequiredCount == nextTotalRequired &&
                Progress01.Equals(nextProgress01) &&
                ExpansionMode == nextExpansionMode &&
                HiddenSubGoalCount == 0 &&
                string.Equals(Title, nextTitle, StringComparison.Ordinal) &&
                string.Equals(Summary, nextSummary, StringComparison.Ordinal) &&
                string.Equals(ProgressText, nextProgressText, StringComparison.Ordinal) &&
                string.Equals(ObjectiveText, nextObjectiveText, StringComparison.Ordinal) &&
                AnimationHint.Equals(animationHint) &&
                RowsEqual(Rows, nextRows))
            {
                return;
            }

            IsVisible = isVisible;
            IsComplete = isComplete;
            ShowCompleteBadge = isComplete;
            Title = nextTitle;
            MainGoalText = nextTitle;
            Summary = nextSummary;
            ProgressText = nextProgressText;
            CompletedRequiredCount = nextCompletedRequired;
            TotalRequiredCount = nextTotalRequired;
            Progress01 = nextProgress01;
            Rows = nextRows;
            SubGoalTexts = nextSubGoalTexts;
            HiddenSubGoalCount = 0;
            ExpansionMode = nextExpansionMode;
            AnimationHint = animationHint;
            ObjectiveText = nextObjectiveText;
            Changed?.Invoke();
        }

        public void SetState(
            bool isVisible,
            string mainGoalText,
            string progressText,
            IReadOnlyList<string> subGoalTexts,
            int hiddenSubGoalCount,
            bool isComplete)
        {
            var nextMainGoalText = mainGoalText ?? string.Empty;
            var nextProgressText = progressText ?? string.Empty;
            var nextSubGoalTexts = CopySubGoalTexts(subGoalTexts);
            var nextHiddenSubGoalCount = Math.Max(0, hiddenSubGoalCount);
            var nextExpansionMode = isVisible && (nextSubGoalTexts.Length > 0 || nextHiddenSubGoalCount > 0) && IsExpanded
                ? ExpansionMode == ObjectiveExpansionMode.Collapsed
                    ? ObjectiveExpansionMode.ManualExpanded
                    : ExpansionMode
                : ObjectiveExpansionMode.Collapsed;
            var nextObjectiveText = BuildObjectiveText(
                nextMainGoalText,
                nextProgressText,
                nextSubGoalTexts,
                nextHiddenSubGoalCount,
                nextExpansionMode != ObjectiveExpansionMode.Collapsed);

            IsVisible = isVisible;
            MainGoalText = nextMainGoalText;
            Title = nextMainGoalText;
            Summary = string.Empty;
            ProgressText = nextProgressText;
            SubGoalTexts = nextSubGoalTexts;
            HiddenSubGoalCount = nextHiddenSubGoalCount;
            ExpansionMode = nextExpansionMode;
            ObjectiveText = nextObjectiveText;
            IsComplete = isComplete;
            ShowCompleteBadge = isComplete;
            Rows = EmptyRows;
            Changed?.Invoke();
        }

        public void ToggleExpanded()
        {
            if (!IsVisible || !CanExpand)
            {
                return;
            }

            ExpansionMode = IsExpanded
                ? ObjectiveExpansionMode.Collapsed
                : ObjectiveExpansionMode.ManualExpanded;
            ObjectiveText = BuildObjectiveText(
                MainGoalText,
                ProgressText,
                SubGoalTexts,
                HiddenSubGoalCount,
                IsExpanded);
            Changed?.Invoke();
        }

        public void SetExpansionMode(ObjectiveExpansionMode expansionMode)
        {
            if (!IsVisible || !CanExpand)
            {
                expansionMode = ObjectiveExpansionMode.Collapsed;
            }

            if (ExpansionMode == expansionMode)
            {
                return;
            }

            ExpansionMode = expansionMode;
            ObjectiveText = BuildObjectiveText(
                MainGoalText,
                ProgressText,
                SubGoalTexts,
                HiddenSubGoalCount,
                IsExpanded);
            Changed?.Invoke();
        }

        private static string BuildObjectiveText(
            string mainGoalText,
            string progressText,
            IReadOnlyList<string> subGoalTexts,
            int hiddenSubGoalCount,
            bool isExpanded)
        {
            var mainLine = string.IsNullOrWhiteSpace(progressText)
                ? mainGoalText
                : $"{mainGoalText} {progressText}";
            var canExpand = (subGoalTexts?.Count ?? 0) > 0 || hiddenSubGoalCount > 0;
            if (!canExpand)
            {
                return mainLine;
            }

            if (!isExpanded)
            {
                return $"{mainLine} [>]";
            }

            var lines = new List<string> { $"{mainLine} [v]" };
            if (subGoalTexts != null)
            {
                for (var i = 0; i < subGoalTexts.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(subGoalTexts[i]))
                    {
                        lines.Add(subGoalTexts[i]);
                    }
                }
            }

            if (hiddenSubGoalCount > 0)
            {
                lines.Add($"+{hiddenSubGoalCount} more");
            }

            return string.Join("\n", lines);
        }

        private static string[] BuildLegacySubGoalTexts(IReadOnlyList<ObjectiveConditionHudViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return EmptySubGoalTexts;
            }

            var texts = new List<string>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(rows[i].Title))
                {
                    continue;
                }

                texts.Add($"{(rows[i].IsSatisfied ? "[x]" : "[ ]")} {rows[i].Title}");
            }

            return texts.Count == 0 ? EmptySubGoalTexts : texts.ToArray();
        }

        private static string[] CopySubGoalTexts(IReadOnlyList<string> subGoalTexts)
        {
            if (subGoalTexts == null || subGoalTexts.Count == 0)
            {
                return EmptySubGoalTexts;
            }

            var copy = new List<string>(subGoalTexts.Count);
            for (var i = 0; i < subGoalTexts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(subGoalTexts[i]))
                {
                    copy.Add(subGoalTexts[i]);
                }
            }

            return copy.Count == 0 ? EmptySubGoalTexts : copy.ToArray();
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
                    !string.Equals(left[i].Title, right[i].Title, StringComparison.Ordinal) ||
                    !string.Equals(left[i].ProgressText, right[i].ProgressText, StringComparison.Ordinal) ||
                    left[i].IsSatisfied != right[i].IsSatisfied ||
                    left[i].Required != right[i].Required ||
                    left[i].Role != right[i].Role ||
                    left[i].JustSatisfied != right[i].JustSatisfied ||
                    left[i].SortOrder != right[i].SortOrder)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
