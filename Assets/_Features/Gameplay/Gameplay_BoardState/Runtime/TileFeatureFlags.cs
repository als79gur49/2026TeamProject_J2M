using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Flags]
    public enum TileFeatureFlags
    {
        None = 0,
        Activated = 1 << 0,
    }
}
