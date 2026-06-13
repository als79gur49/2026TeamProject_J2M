namespace Game.Feature.Gameplay.BoardState
{
    // Keep the top-level legality blocker vocabulary stable.
    // New kinds are allowed only when a truly new world-source exists.
    // Existing sources should grow through central factory/query sub-facets instead.
    public enum LegalityBlockerKind
    {
        BoardEdge = 0,
        Solid = 1,
        Unit = 2,
        Reservation = 3,
        TileFeature = 4,
    }
}
