using Game.Feature.Gameplay.Movement.Intents;

namespace Game.Feature.Gameplay.Attack.Intents
{
    public sealed class AttackIntent : Intent
    {
        public AttackIntent(
            int sourceId,
            int priority,
            AttackInputKind inputKind = AttackInputKind.EntityIntent,
            int localSequence = 0,
            ImpactReservation? impactReservation = null)
            : base(sourceId, priority, Loop.TickPhase.Attack)
        {
            InputKind = inputKind;
            LocalSequence = localSequence;
            ImpactReservation = impactReservation;
        }

        public AttackInputKind InputKind { get; }

        public int LocalSequence { get; }

        public ImpactReservation? ImpactReservation { get; }

        public bool IsSynthetic => InputKind != AttackInputKind.EntityIntent;

        public static AttackIntent FromImpactReservation(ImpactReservation reservation)
        {
            return new AttackIntent(
                reservation.SourceId,
                0,
                AttackInputKind.ImpactReservation,
                reservation.ReservationSequence,
                reservation);
        }
    }
}
