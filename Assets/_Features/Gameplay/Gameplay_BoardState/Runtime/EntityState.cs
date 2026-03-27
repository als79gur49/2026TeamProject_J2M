using System;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    [Flags]
    public enum BoxCapabilities
    {
        None = 0,
        Pushable = 1 << 0,
        Throwable = 1 << 1,
        LootOnInteractDestroy = 1 << 2,
    }

    public struct EntityState
    {
        public int entityId;
        public Vector2Int position;
        public int hp;
        public int maxHp;
        public int teamId;
        public EntityType type;
        public EntityPhaseState state;
        public int stateTimer;
        public Direction facing;
        public bool markedForDeath;
        public int spawnTick;
        public BoxCapabilities boxCapabilities;
    }
}
