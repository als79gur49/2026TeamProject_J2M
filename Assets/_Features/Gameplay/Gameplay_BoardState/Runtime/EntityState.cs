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
        JumpCrushable = 1 << 4,
    }

    public enum BoxArchetype
    {
        Normal = 0,
        Moon = 1,
        GravityField = 2,
    }

    public enum GravityFieldPhase
    {
        None = 0,
        Charging = 1,
        Active = 2,
    }

    public struct EntityState
    {
        public int entityId;
        public SurfaceCell position;
        public int hp;
        public int maxHp;
        public int teamId;
        public EntityType type;
        public UnitRole unitRole;
        public EntityPhaseState state;
        public int stateTimer;
        public Direction facing;
        public EntityBoardPresence boardPresence;
        public bool markedForDeath;
        public int spawnTick;
        public BoxCapabilities boxCapabilities;
        public BoxArchetype boxArchetype;
        public GravityFieldPhase gravityFieldPhase;
        public int gravityFieldTimerTicks;
        public int kineticInstigatorEntityId;
        public int kineticInstigatorTeamId;
        public EnemyAiMode aiMode;
        // Temporary generic recover countdown only. Charge progress is authoritative in
        // EnemyChargeRuntimeState, and future richer recover semantics should move to
        // a dedicated EnemyRecoverRuntimeState instead of expanding this field again.
        public int aiStateTimer;
        public int enemyLocomotionCooldownTicks;
    }
}
