using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;

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
            : base(sourceId, priority, TickPhase.Attack)
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

        protected internal override int GetTypeSortKey()
        {
            return 0;
        }

        protected internal override int CompareSameType(Intent other)
        {
            var otherAttack = (AttackIntent)other;

            var result = ((int)InputKind).CompareTo((int)otherAttack.InputKind);
            if (result != 0)
            {
                return result;
            }

            result = LocalSequence.CompareTo(otherAttack.LocalSequence);
            if (result != 0)
            {
                return result;
            }

            result = TargetId.CompareTo(otherAttack.TargetId);
            if (result != 0)
            {
                return result;
            }

            if (ImpactReservation.HasValue && otherAttack.ImpactReservation.HasValue)
            {
                return ImpactReservationComparer.Instance.Compare(
                    ImpactReservation.Value,
                    otherAttack.ImpactReservation.Value);
            }

            return 0;
        }

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
