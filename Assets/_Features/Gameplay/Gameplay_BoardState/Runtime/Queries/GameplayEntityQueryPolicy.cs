namespace Game.Feature.Gameplay.BoardState
{
    internal static class GameplayEntityQueryPolicy
    {
        public static bool IsEntityOnActiveFace(in ResolvedSpatialState spatialState)
        {
            return spatialState.IsGameplayVisible;
        }

        public static bool IsEntityOccupyingBoard(in ResolvedSpatialState spatialState)
        {
            return SpatialStateSemantics.ClaimsAuthoritativeOccupancy(spatialState);
        }

        public static bool ShouldParticipateInGameplayQueries(in ResolvedSpatialState spatialState)
        {
            return SpatialStateSemantics.ParticipatesInGameplayQueries(spatialState);
        }

        public static bool IsBlockingPlacementEntity(
            in ResolvedSpatialState spatialState,
            bool requireGameplayVisibility)
        {
            return requireGameplayVisibility
                ? ShouldParticipateInGameplayQueries(spatialState)
                : IsEntityOccupyingBoard(spatialState);
        }
    }
}
