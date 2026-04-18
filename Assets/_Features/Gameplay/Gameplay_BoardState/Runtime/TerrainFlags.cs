using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Flags]
    public enum TerrainFlags
    {
        None = 0,
        BlocksGroundTraversal = 1 << 0,
    }
}
