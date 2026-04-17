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

            result = left.SourceActionPlanId.CompareTo(right.SourceActionPlanId);
            if (result != 0)
            {
                return result;
            }

            result = left.LocalActionIndex.CompareTo(right.LocalActionIndex);
            if (result != 0)
            {
                return result;
            }

            result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0)
            {
                return result;
            }

            result = ((int)left.ImpactCell.face).CompareTo((int)right.ImpactCell.face);
            if (result != 0)
            {
                return result;
            }

            result = left.ImpactCell.x.CompareTo(right.ImpactCell.x);
            if (result != 0)
            {
                return result;
            }

            result = left.ImpactCell.y.CompareTo(right.ImpactCell.y);
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
