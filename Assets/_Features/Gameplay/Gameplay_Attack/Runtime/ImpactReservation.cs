using System;
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

        [Obsolete("IR metadata only. Prefer semantic fields such as SourceId, TargetId, Position, Damage, and TickGenerated.")]
        public int SourceActionGroupId { get; }

        [Obsolete("IR metadata only. Prefer semantic fields such as SourceId, TargetId, Position, Damage, and TickGenerated.")]
        public int ReservationSequence { get; }
    }
}
