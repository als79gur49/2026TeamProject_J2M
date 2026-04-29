using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Stages
{
    public readonly struct StageAuthoringNormalizedSpawn
    {
        public StageAuthoringNormalizedSpawn(
            string stableGuid,
            int entityId,
            StageSpawnKind kind,
            SurfaceCell cell,
            Direction facing,
            int hp,
            string unitStackGroup,
            BoxCapabilities boxCapabilities,
            EnemyAiMode enemyAiMode,
            int enemyAiStateTimer,
            EnemyAiProfile enemyAiProfile)
        {
            StableGuid = stableGuid ?? string.Empty;
            EntityId = entityId;
            Kind = kind;
            Cell = cell;
            Facing = facing;
            Hp = hp;
            UnitStackGroup = unitStackGroup ?? string.Empty;
            BoxCapabilities = boxCapabilities;
            EnemyAiMode = enemyAiMode;
            EnemyAiStateTimer = enemyAiStateTimer;
            EnemyAiProfile = enemyAiProfile;
        }

        public string StableGuid { get; }

        public int EntityId { get; }

        public StageSpawnKind Kind { get; }

        public SurfaceCell Cell { get; }

        public Direction Facing { get; }

        public int Hp { get; }

        public string UnitStackGroup { get; }

        public BoxCapabilities BoxCapabilities { get; }

        public EnemyAiMode EnemyAiMode { get; }

        public int EnemyAiStateTimer { get; }

        public EnemyAiProfile EnemyAiProfile { get; }
    }
}
