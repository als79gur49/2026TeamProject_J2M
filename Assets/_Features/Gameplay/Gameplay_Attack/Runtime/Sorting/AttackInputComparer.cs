using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Intents;

namespace Game.Feature.Gameplay.Attack.Sorting
{
    internal sealed class AttackInputComparer : IComparer<AttackIntent>
    {
        internal static readonly AttackInputComparer Instance = new();

        public int Compare(AttackIntent left, AttackIntent right)
        {
            var result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.InputKind).CompareTo((int)right.InputKind);
            if (result != 0)
            {
                return result;
            }

            result = left.LocalSequence.CompareTo(right.LocalSequence);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.CommandKind).CompareTo((int)right.CommandKind);
            if (result != 0)
            {
                return result;
            }

            result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0)
            {
                return result;
            }

            if (left.ImpactReservation.HasValue && right.ImpactReservation.HasValue)
            {
                result = ImpactReservationComparer.Instance.Compare(
                    left.ImpactReservation.Value,
                    right.ImpactReservation.Value);
                if (result != 0)
                {
                    return result;
                }
            }

            return left.IntentId.CompareTo(right.IntentId);
        }
    }
}
