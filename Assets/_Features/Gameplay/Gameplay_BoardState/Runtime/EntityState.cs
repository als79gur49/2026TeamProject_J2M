using System;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    [Flags]
    public enum BoxCapabilities
    {
        None = 0,
        Push = 1 << 0,
        Flip = 1 << 1,
        Item = 1 << 2,
        Destroy = 1 << 3,
    }

    public struct EntityState
    {
        public int entityId;
        public SurfaceCell position;
        public int hp;
        public int maxHp;
        public int teamId;
        public EntityType type;
        public EntityPhaseState state;
        public int stateTimer;
        public Direction facing;
        public EntityBoardPresence boardPresence;
        public bool markedForDeath;
        public int spawnTick;
        public BoxCapabilities boxCapabilities;
        public EnemyAiMode aiMode;
        public int aiStateTimer;
    }
}
