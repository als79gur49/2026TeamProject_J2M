using System.Collections.Generic;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;

namespace Game.Feature.Gameplay.Movement.Sorting
{
    internal sealed class RawMovementIntentComparer : IComparer<RawMovementIntent>
    {
        internal static readonly RawMovementIntentComparer Instance = new();

        public int Compare(RawMovementIntent left, RawMovementIntent right)
        {
            var result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = GetCommandSortKey(left.CommandKind).CompareTo(GetCommandSortKey(right.CommandKind));
            if (result != 0)
            {
                return result;
            }

            result = left.Destination.x.CompareTo(right.Destination.x);
            if (result != 0)
            {
                return result;
            }

            result = left.Destination.y.CompareTo(right.Destination.y);
            if (result != 0)
            {
                return result;
            }

            result = left.LocalSequence.CompareTo(right.LocalSequence);
            if (result != 0)
            {
                return result;
            }

            return 0;
        }

        private static int GetCommandSortKey(MovementCommandKind commandKind)
        {
            switch (commandKind)
            {
                case MovementCommandKind.Push:
                    return 0;

                case MovementCommandKind.Flip:
                    return 1;

                case MovementCommandKind.Move:
                    return 2;

                default:
                    return 99;
            }
        }
    }
}
