using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
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
    }
}
