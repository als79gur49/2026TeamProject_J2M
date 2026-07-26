using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.HUD;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveHudPresenter : IDisposable
    {
        private readonly Dictionary<string, bool> _previousSatisfiedByStableId =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private UIObjectiveSlice _lastObjective = UIObjectiveSlice.Empty;
        private bool _hasPrevious;
        private bool _isDisposed;
        private string _previousObjectiveStableId = string.Empty;

        public ObjectiveHudPresenter(ILocalizedTextResolver localizedTextResolver = null)
        {
            _localizedTextResolver = localizedTextResolver ?? ObjectiveHudInvariantTextResolver.Instance;
            _localizedTextResolver.LocaleChanged += HandleLocaleChanged;
        }

        public ObjectiveHudViewModel ViewModel { get; } = new();

        public void Apply(UIObjectiveSlice objective)
        {
            if (_isDisposed)
            {
                return;
            }

            _lastObjective = objective;
            Render(objective);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _localizedTextResolver.LocaleChanged -= HandleLocaleChanged;
            _isDisposed = true;
        }

        private void HandleLocaleChanged()
        {
            if (!_isDisposed)
            {
                Render(_lastObjective);
            }
        }

        private void Render(UIObjectiveSlice objective)
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
            ViewModel.SetState(
                true,
                objectiveStableId,
                _localizedTextResolver.Resolve(ObjectiveHudLocalization.HeaderDescriptor),
                rows);

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
            var candidates = new List<ObjectiveHudRowCandidate>(conditions.Count);
            var groupsByKey = new Dictionary<string, ObjectiveHudGroupAccumulator>(StringComparer.Ordinal);

            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                var stableId = ResolveStableId(condition, i);
                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }

                var rowKind = MapRowKind(condition.PresentationKind);
                if (IsGroupable(rowKind) && !string.IsNullOrWhiteSpace(condition.StableGroupKey))
                {
                    var groupIdentity = BuildGroupIdentity(condition);
                    if (!groupsByKey.TryGetValue(groupIdentity, out var group))
                    {
                        group = new ObjectiveHudGroupAccumulator(
                            condition.StableGroupKey,
                            condition.PresentationKind,
                            rowKind,
                            condition.SortOrder);
                        groupsByKey.Add(groupIdentity, group);
                        candidates.Add(ObjectiveHudRowCandidate.ForGroup(group));
                    }

                    group.Add(condition);
                    continue;
                }

                candidates.Add(ObjectiveHudRowCandidate.ForRow(
                    condition.SortOrder,
                    stableId,
                    BuildSingleRow(stableId, condition)));
            }

            candidates.Sort(CompareRowCandidates);
            var rows = new List<ObjectiveConditionHudViewModel>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                rows.Add(candidates[i].BuildRow(this, objective.ObjectiveStableId));
            }

            return rows;
        }

        private ObjectiveConditionHudViewModel BuildSingleRow(
            string stableId,
            UIObjectiveConditionSlice condition)
        {
            return new ObjectiveConditionHudViewModel(
                stableId,
                _localizedTextResolver.Resolve(condition.TextDescriptor),
                condition.IsSatisfied,
                IsJustSatisfied(stableId, condition.IsSatisfied),
                completedCount: condition.CompletedCount,
                requiredCount: condition.RequiredCount,
                rowKind: MapRowKind(condition.PresentationKind),
                groupKey: condition.StableGroupKey);
        }

        private ObjectiveConditionHudViewModel BuildGroupedRow(
            ObjectiveHudGroupAccumulator group,
            string objectiveStableId)
        {
            var isSatisfied =
                group.RequiredCount > 0 &&
                group.CompletedCount >= group.RequiredCount;
            var stableId = string.Concat(
                "objective-",
                NormalizeStableIdPart(objectiveStableId),
                "-group-",
                NormalizeStableIdPart(group.GroupKey));
            if (!ObjectiveHudLocalization.TryCreateConditionDescriptor(
                    group.PresentationKind,
                    group.CompletedCount,
                    group.RequiredCount,
                    out var descriptor))
            {
                throw new InvalidOperationException(
                    $"Objective HUD group '{group.GroupKey}' has no localization descriptor.");
            }

            return new ObjectiveConditionHudViewModel(
                stableId,
                _localizedTextResolver.Resolve(descriptor),
                isSatisfied,
                IsJustSatisfied(stableId, isSatisfied),
                isGrouped: true,
                completedCount: group.CompletedCount,
                requiredCount: group.RequiredCount,
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

        private static string ResolveStableId(UIObjectiveConditionSlice condition, int index)
        {
            if (!string.IsNullOrWhiteSpace(condition.StableId))
            {
                return condition.StableId;
            }

            return condition.PresentationKind == GameplayObjectivePresentationKind.None
                ? string.Empty
                : string.Concat(
                    "row-",
                    index.ToString(CultureInfo.InvariantCulture),
                    "-kind-",
                    ((int)condition.PresentationKind).ToString(CultureInfo.InvariantCulture));
        }

        private static string BuildGroupIdentity(UIObjectiveConditionSlice condition)
        {
            return string.Concat(
                condition.StableGroupKey,
                "|kind-",
                ((int)condition.PresentationKind).ToString(CultureInfo.InvariantCulture));
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

            var groupComparison = string.Compare(
                left.StableGroupKey,
                right.StableGroupKey,
                StringComparison.Ordinal);
            return groupComparison != 0
                ? groupComparison
                : string.Compare(left.StableId, right.StableId, StringComparison.Ordinal);
        }

        private static int CompareRowCandidates(
            ObjectiveHudRowCandidate left,
            ObjectiveHudRowCandidate right)
        {
            var sortComparison = left.SortOrder.CompareTo(right.SortOrder);
            return sortComparison != 0
                ? sortComparison
                : string.Compare(left.StableSortKey, right.StableSortKey, StringComparison.Ordinal);
        }

        private static ObjectiveHudRowKind MapRowKind(
            GameplayObjectivePresentationKind presentationKind)
        {
            switch (presentationKind)
            {
                case GameplayObjectivePresentationKind.ActivateButton:
                    return ObjectiveHudRowKind.ButtonGroupGeneric;

                case GameplayObjectivePresentationKind.ActivateMoonButton:
                    return ObjectiveHudRowKind.ButtonGroupMoon;

                default:
                    return ObjectiveHudRowKind.Single;
            }
        }

        private static bool IsGroupable(ObjectiveHudRowKind rowKind)
        {
            return rowKind == ObjectiveHudRowKind.ButtonGroupGeneric ||
                   rowKind == ObjectiveHudRowKind.ButtonGroupMoon;
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
                GameplayObjectivePresentationKind presentationKind,
                ObjectiveHudRowKind rowKind,
                int sortOrder)
            {
                GroupKey = groupKey ?? string.Empty;
                PresentationKind = presentationKind;
                RowKind = rowKind;
                SortOrder = sortOrder;
            }

            public string GroupKey { get; }

            public GameplayObjectivePresentationKind PresentationKind { get; }

            public ObjectiveHudRowKind RowKind { get; }

            public int SortOrder { get; private set; }

            public int CompletedCount { get; private set; }

            public int RequiredCount { get; private set; }

            public void Add(UIObjectiveConditionSlice condition)
            {
                CompletedCount += condition.CompletedCount;
                RequiredCount += condition.RequiredCount;
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
                string stableSortKey,
                ObjectiveConditionHudViewModel row,
                ObjectiveHudGroupAccumulator group)
            {
                SortOrder = sortOrder;
                StableSortKey = stableSortKey ?? string.Empty;
                _row = row;
                _group = group;
            }

            public int SortOrder { get; }

            public string StableSortKey { get; }

            public static ObjectiveHudRowCandidate ForRow(
                int sortOrder,
                string stableSortKey,
                ObjectiveConditionHudViewModel row)
            {
                return new ObjectiveHudRowCandidate(sortOrder, stableSortKey, row, null);
            }

            public static ObjectiveHudRowCandidate ForGroup(ObjectiveHudGroupAccumulator group)
            {
                return new ObjectiveHudRowCandidate(
                    group.SortOrder,
                    group.GroupKey,
                    null,
                    group);
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

        private sealed class ObjectiveHudInvariantTextResolver : ILocalizedTextResolver
        {
            public static readonly ObjectiveHudInvariantTextResolver Instance = new();

            public string CurrentLocaleCode => "en-US";

            public event Action LocaleChanged
            {
                add { }
                remove { }
            }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                switch (descriptor.Key)
                {
                    case ObjectiveHudLocalization.Keys.Header:
                        return "Objectives";

                    case ObjectiveHudLocalization.Keys.ReachExit:
                        return Format("Reach the Exit Zone ({0}/{1})", descriptor.Arguments);

                    case ObjectiveHudLocalization.Keys.ActivateButton:
                        return Format("Place a push box on the button ({0}/{1})", descriptor.Arguments);

                    case ObjectiveHudLocalization.Keys.ActivateMoonButton:
                        return Format("Place the MoonBlock on the button ({0}/{1})", descriptor.Arguments);

                    default:
                        return string.Empty;
                }
            }

            private static string Format(string format, IReadOnlyList<object> arguments)
            {
                var values = new object[arguments.Count];
                for (var i = 0; i < arguments.Count; i++)
                {
                    values[i] = arguments[i];
                }

                return string.Format(CultureInfo.InvariantCulture, format, values);
            }
        }
    }
}
