using System.Collections.Generic;

namespace Game.Feature.Gameplay.Attack.Sorting
{
    internal sealed class ImpactReservationComparer : IComparer<ImpactReservation>
    {
        internal static readonly ImpactReservationComparer Instance = new();

        public int Compare(ImpactReservation left, ImpactReservation right)
        {
            var result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = left.SourceActionGroupId.CompareTo(right.SourceActionGroupId);
            if (result != 0)
            {
                return result;
            }

            result = left.ReservationSequence.CompareTo(right.ReservationSequence);
            if (result != 0)
            {
                return result;
            }

            result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0)
            {
                return result;
            }

            result = left.Position.x.CompareTo(right.Position.x);
            if (result != 0)
            {
                return result;
            }

            result = left.Position.y.CompareTo(right.Position.y);
            if (result != 0)
            {
                return result;
            }

            result = left.Damage.CompareTo(right.Damage);
            if (result != 0)
            {
                return result;
            }

            return left.TickGenerated.CompareTo(right.TickGenerated);
        }
    }
}
