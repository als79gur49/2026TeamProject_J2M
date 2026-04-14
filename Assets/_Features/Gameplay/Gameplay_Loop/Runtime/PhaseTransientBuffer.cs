using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Sorting;

namespace Game.Feature.Gameplay.Loop
{
    internal class ImpactReservationBuffer
    {
        private readonly List<ImpactReservation> _impactReservations = new();

        public void AddImpact(ImpactReservation reservation)
        {
            _impactReservations.Add(reservation);
        }

        public List<ImpactReservation> DrainImpacts()
        {
            if (_impactReservations.Count == 0)
            {
                return new List<ImpactReservation>();
            }

            _impactReservations.Sort(ImpactReservationComparer.Instance);

            var drainedReservations = new List<ImpactReservation>(_impactReservations);
            _impactReservations.Clear();
            return drainedReservations;
        }

        public void Reset()
        {
            _impactReservations.Clear();
        }
    }
}
