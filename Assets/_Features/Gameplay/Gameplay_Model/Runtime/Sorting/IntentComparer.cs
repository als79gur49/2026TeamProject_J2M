using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Model.Intents;

namespace Game.Feature.Gameplay.Model.Sorting
{
    public sealed class IntentComparer : IComparer<Intent>
    {
        public static readonly IntentComparer Instance = new();

        public int Compare(Intent left, Intent right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

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

            result = ((int)left.Phase).CompareTo((int)right.Phase);
            if (result != 0)
            {
                return result;
            }

            result = left.GetTypeSortKey().CompareTo(right.GetTypeSortKey());
            if (result != 0)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(left.GetType().FullName, right.GetType().FullName);
            if (result != 0)
            {
                return result;
            }

            if (left.GetType() != right.GetType())
            {
                throw new InvalidOperationException(
                    "CompareSameType requires identical runtime intent types.");
            }

            result = left.CompareSameType(right);
            if (result != 0)
            {
                return result;
            }

            return left.IntentId.CompareTo(right.IntentId);
        }
    }
}
