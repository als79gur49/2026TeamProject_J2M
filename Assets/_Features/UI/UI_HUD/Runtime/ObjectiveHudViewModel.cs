using System;
using System.Collections.Generic;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveHudViewModel
    {
        private static readonly string[] EmptySubGoalTexts = Array.Empty<string>();

        public event Action Changed;

        public bool IsVisible { get; private set; }

        public bool IsComplete { get; private set; }

        public string ObjectiveText { get; private set; } = string.Empty;

        public string MainGoalText { get; private set; } = string.Empty;

        public string ProgressText { get; private set; } = string.Empty;

        public IReadOnlyList<string> SubGoalTexts { get; private set; } = EmptySubGoalTexts;

        public int HiddenSubGoalCount { get; private set; }

        public bool IsExpanded { get; private set; }

        public bool CanExpand => SubGoalTexts.Count > 0 || HiddenSubGoalCount > 0;

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
            var nextIsExpanded = isVisible && (nextSubGoalTexts.Length > 0 || nextHiddenSubGoalCount > 0) && IsExpanded;
            var nextObjectiveText = BuildObjectiveText(
                nextMainGoalText,
                nextProgressText,
                nextSubGoalTexts,
                nextHiddenSubGoalCount,
                nextIsExpanded);

            if (IsVisible == isVisible &&
                IsComplete == isComplete &&
                IsExpanded == nextIsExpanded &&
                HiddenSubGoalCount == nextHiddenSubGoalCount &&
                string.Equals(MainGoalText, nextMainGoalText, StringComparison.Ordinal) &&
                string.Equals(ProgressText, nextProgressText, StringComparison.Ordinal) &&
                string.Equals(ObjectiveText, nextObjectiveText, StringComparison.Ordinal) &&
                AreEqual(SubGoalTexts, nextSubGoalTexts))
            {
                return;
            }

            IsVisible = isVisible;
            MainGoalText = nextMainGoalText;
            ProgressText = nextProgressText;
            SubGoalTexts = nextSubGoalTexts;
            HiddenSubGoalCount = nextHiddenSubGoalCount;
            IsExpanded = nextIsExpanded;
            ObjectiveText = nextObjectiveText;
            IsComplete = isComplete;
            Changed?.Invoke();
        }

        public void ToggleExpanded()
        {
            if (!IsVisible || !CanExpand)
            {
                return;
            }

            IsExpanded = !IsExpanded;
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

        private static bool AreEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
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
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
