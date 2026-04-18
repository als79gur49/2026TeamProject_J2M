namespace Game.Feature.Gameplay.BoardState
{
    // Keep the top-level legality blocker vocabulary stable.
    // New kinds are allowed only when a truly new world-source exists.
    // Existing sources should grow through central factory/query sub-facets instead.
    internal enum LegalityBlockerKind
    {
        BoardEdge = 0,
        Terrain = 1,
        Solid = 2,
        Unit = 3,
        Reservation = 4,
    }
}
