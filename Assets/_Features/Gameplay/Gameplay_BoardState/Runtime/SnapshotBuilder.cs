using System;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class SnapshotBuilder
    {
        public static WorldSnapshot Create(WorldState worldState)
        {
            if (worldState == null)
            {
                throw new ArgumentNullException(nameof(worldState));
            }

            return worldState.CreateSnapshot();
        }
    }
}
