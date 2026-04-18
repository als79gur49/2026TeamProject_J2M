namespace Game.Feature.Gameplay.BoardState
{
    internal enum LegalityBlockerKind
    {
        BoardEdge = 0,
        Terrain = 1,
        Solid = 2,
        Unit = 3,
        Reservation = 4,
    }
}
