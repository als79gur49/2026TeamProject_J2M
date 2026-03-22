using System.Collections.Generic;
using Game.Feature.Gameplay.Movement.Collection;

namespace Game.Feature.Gameplay.Movement.Sorting
{
    internal sealed class RawMovementIntentComparer : IComparer<RawMovementIntent>
    {
        internal static readonly RawMovementIntentComparer Instance = new();

        public int Compare(RawMovementIntent left, RawMovementIntent right)
        {
            var result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            return right.Priority.CompareTo(left.Priority);
        }
    }
}
