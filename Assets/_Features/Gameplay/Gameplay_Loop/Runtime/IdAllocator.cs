using System;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class IdAllocator
    {
        private int _currentTickIndex = int.MinValue;
        private int _nextGroupId;
        private int _nextIntentId;
        private int _nextSpawnId;

        internal int CurrentTickIndex => _currentTickIndex;

        internal void ResetForTick(int tickIndex)
        {
            _currentTickIndex = tickIndex;
            _nextIntentId = 1;
            _nextGroupId = 1;
            _nextSpawnId = 1;
        }

        internal int AllocateIntentId()
        {
            return Allocate(ref _nextIntentId);
        }

        internal int AllocateGroupId()
        {
            return Allocate(ref _nextGroupId);
        }

        internal int AllocateSpawnId()
        {
            return Allocate(ref _nextSpawnId);
        }

        private int Allocate(ref int nextValue)
        {
            if (_currentTickIndex == int.MinValue)
            {
                throw new InvalidOperationException("IdAllocator must be reset for the current tick before allocating IDs.");
            }

            return nextValue++;
        }
    }
}
