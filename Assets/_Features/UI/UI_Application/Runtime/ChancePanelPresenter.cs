using System;
using System.Collections.Generic;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class ChancePanelPresenter
    {
        private bool _hasPrevious;
        private UIChanceSlice _previous;
        private int _sequenceId;

        public ChancePanelViewModel ViewModel { get; } = new();

        public void Apply(UIChanceSlice chance)
        {
            var isInitialBind = !_hasPrevious;
            var hint = isInitialBind
                ? ChanceChangeAnimationHint.None
                : BuildAnimationHint(_previous, chance);

            ViewModel.SetState(
                chance.HasChances,
                chance.RemainingChances,
                chance.MaxChances,
                isInitialBind,
                hint);

            _previous = chance;
            _hasPrevious = true;
        }

        private ChanceChangeAnimationHint BuildAnimationHint(
            UIChanceSlice previous,
            UIChanceSlice next)
        {
            if (previous.Equals(next) ||
                !previous.HasChances ||
                !next.HasChances ||
                previous.MaxChances <= 0 ||
                next.MaxChances <= 0)
            {
                return ChanceChangeAnimationHint.None;
            }

            if (next.RemainingChances < previous.RemainingChances)
            {
                var changed = BuildIndexRange(next.RemainingChances, previous.RemainingChances, next.MaxChances);
                var kind = next.RemainingChances == 1 && next.MaxChances > 1
                    ? ChanceChangeKind.LastChanceEntered
                    : ChanceChangeKind.Lost;
                return new ChanceChangeAnimationHint(
                    kind,
                    changed.Count > 0 ? changed[0] : -1,
                    changed,
                    NextSequenceId());
            }

            if (next.RemainingChances > previous.RemainingChances)
            {
                var changed = BuildIndexRange(previous.RemainingChances, next.RemainingChances, next.MaxChances);
                return new ChanceChangeAnimationHint(
                    ChanceChangeKind.Gained,
                    changed.Count > 0 ? changed[0] : -1,
                    changed,
                    NextSequenceId());
            }

            return ChanceChangeAnimationHint.None;
        }

        private int NextSequenceId()
        {
            _sequenceId++;
            return _sequenceId;
        }

        private static IReadOnlyList<int> BuildIndexRange(
            int startInclusive,
            int endExclusive,
            int maxChances)
        {
            var indices = new List<int>();
            var start = Math.Max(0, startInclusive);
            var end = Math.Min(Math.Max(start, endExclusive), Math.Max(0, maxChances));
            for (var i = start; i < end; i++)
            {
                indices.Add(i);
            }

            return indices;
        }
    }
}
