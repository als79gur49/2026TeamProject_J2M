namespace Game.Feature.Gameplay.BoardState
{
    internal static class ReservationQuery
    {
        // Reservation remains a single top-level blocker kind this phase.
        // Future cell/edge/entity/payload sub-facets must hang off this query/factory seam
        // rather than introducing file-local blocker enums.
        public static bool BlocksSettlement(ReservationStatus reservationStatus)
        {
            return reservationStatus == ReservationStatus.Conflicted;
        }
    }
}
