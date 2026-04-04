using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public sealed class StageRuntimeBuildResult
    {
        public StageRuntimeBuildResult(
            BoardBounds boardBounds,
            CubeTopologyState initialTopology,
            EntityState[] initialEntities,
            TerrainData initialTerrain,
            int playerEntityId,
            EnemyAiProfileOverride[] enemyAiProfileOverrides)
        {
            BoardBounds = boardBounds;
            InitialTopology = initialTopology;
            InitialEntities = initialEntities ?? Array.Empty<EntityState>();
            InitialTerrain = initialTerrain ?? TerrainData.Empty;
            PlayerEntityId = playerEntityId;
            EnemyAiProfileOverrides = enemyAiProfileOverrides ?? Array.Empty<EnemyAiProfileOverride>();
        }

        public BoardBounds BoardBounds { get; }

        public CubeTopologyState InitialTopology { get; }

        public EntityState[] InitialEntities { get; }

        public TerrainData InitialTerrain { get; }

        public int PlayerEntityId { get; }

        public EnemyAiProfileOverride[] EnemyAiProfileOverrides { get; }
    }
}
