namespace Game.Feature.Gameplay.BoardState
{
    internal static class GameplayEntityQueryPolicy
    {
        public static bool IsEntityOnActiveFace(in ResolvedSpatialState spatialState)
        {
            return StateQuery.IsGameplayVisible(spatialState);
        }

        public static bool IsEntityOccupyingBoard(in ResolvedSpatialState spatialState)
        {
            return StateQuery.ClaimsAuthoritativeOccupancy(spatialState);
        }

        public static bool ShouldParticipateInGameplayQueries(in ResolvedSpatialState spatialState)
        {
            return ModifierQuery.ShouldParticipateInGameplayQueries(spatialState);
        }

        public static bool ShouldParticipateInTargetSelection(in ResolvedSpatialState spatialState)
        {
            return ModifierQuery.ShouldParticipateInTargetSelection(spatialState);
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
