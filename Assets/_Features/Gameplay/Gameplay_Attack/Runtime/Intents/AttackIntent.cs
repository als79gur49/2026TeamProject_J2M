using Game.Feature.Gameplay.Movement.Intents;

namespace Game.Feature.Gameplay.Attack.Intents
{
    public sealed class AttackIntent : Intent
    {
        public AttackIntent(int sourceId, int priority, int targetId)
            : this(
                sourceId,
                priority,
                targetId,
                AttackInputKind.EntityIntent,
                0,
                null)
        {
        }

        public AttackIntent(
            int sourceId,
            int priority,
            AttackInputKind inputKind = AttackInputKind.EntityIntent,
            int localSequence = 0,
            ImpactReservation? impactReservation = null)
            : this(
                sourceId,
                priority,
                impactReservation?.TargetId ?? 0,
                inputKind,
                localSequence,
                impactReservation)
        {
        }

        private AttackIntent(
            int sourceId,
            int priority,
            int targetId,
            AttackInputKind inputKind,
            int localSequence,
            ImpactReservation? impactReservation)
            : base(sourceId, priority, Loop.TickPhase.Attack)
        {
            TargetId = targetId;
            InputKind = inputKind;
            LocalSequence = localSequence;
            ImpactReservation = impactReservation;
        }

        public int TargetId { get; }

        public AttackInputKind InputKind { get; }

        public int LocalSequence { get; }

        public ImpactReservation? ImpactReservation { get; }

        public bool IsSynthetic => InputKind != AttackInputKind.EntityIntent;

        public static AttackIntent FromImpactReservation(ImpactReservation reservation)
        {
            return new AttackIntent(
                reservation.SourceId,
                0,
                reservation.TargetId,
                AttackInputKind.ImpactReservation,
                reservation.ReservationSequence,
                reservation);
        }
    }
}
