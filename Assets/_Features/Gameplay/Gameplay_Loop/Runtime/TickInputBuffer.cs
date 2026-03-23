using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickInputBuffer
    {
        private readonly Dictionary<int, TickInput> _inputsByTick = new();

        public void Record(in TickInput input)
        {
            if (input.TickIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "TickInputBuffer only accepts positive tick indices.");
            }

            if (_inputsByTick.ContainsKey(input.TickIndex))
            {
                throw new InvalidOperationException("TickInputBuffer already contains input for the requested tick index.");
            }

            _inputsByTick.Add(input.TickIndex, input);
        }

        public bool TryConsume(int tickIndex, out TickInput input)
        {
            if (_inputsByTick.TryGetValue(tickIndex, out input))
            {
                _inputsByTick.Remove(tickIndex);
                return true;
            }

            input = default;
            return false;
        }

        public TickInput ConsumeOrDefault(int tickIndex)
        {
            if (tickIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickIndex), "TickInputBuffer only accepts positive tick indices.");
            }

            return TryConsume(tickIndex, out var input)
                ? input
                : new TickInput(tickIndex);
        }

        public bool HasBufferedInput(int tickIndex)
        {
            return _inputsByTick.ContainsKey(tickIndex);
        }

        public void ClearBefore(int tickIndex)
        {
            if (_inputsByTick.Count == 0)
            {
                return;
            }

            var ticksToClear = new List<int>();
            foreach (var pendingTickIndex in _inputsByTick.Keys)
            {
                if (pendingTickIndex < tickIndex)
                {
                    ticksToClear.Add(pendingTickIndex);
                }
            }

            for (var i = 0; i < ticksToClear.Count; i++)
            {
                _inputsByTick.Remove(ticksToClear[i]);
            }
        }
    }
}
