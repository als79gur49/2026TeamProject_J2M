using UnityEngine;

namespace Game.Feature.Gameplay.Attack
{
    public readonly struct ImpactReservation
    {
        public ImpactReservation(
            int sourceId,
            int targetId,
            Vector2Int position,
            int damage,
            int tickGenerated,
            int sourceActionGroupId,
            int reservationSequence)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Position = position;
            Damage = damage;
            TickGenerated = tickGenerated;
            SourceActionGroupId = sourceActionGroupId;
            ReservationSequence = reservationSequence;
        }

        public int SourceId { get; }

        public int TargetId { get; }

        public Vector2Int Position { get; }

        public int Damage { get; }

        public int TickGenerated { get; }

        public int SourceActionGroupId { get; }

        public int ReservationSequence { get; }
    }
}
