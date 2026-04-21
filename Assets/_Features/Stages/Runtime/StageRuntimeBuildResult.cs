using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;

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
            StageObjectiveRuntimeDefinition objectiveRuntimeDefinition,
            EnemyAiProfileOverride[] enemyAiProfileOverrides)
        {
            BoardBounds = boardBounds;
            InitialTopology = initialTopology;
            InitialEntities = initialEntities ?? Array.Empty<EntityState>();
            InitialTerrain = initialTerrain ?? TerrainData.Empty;
            PlayerEntityId = playerEntityId;
            ObjectiveRuntimeDefinition = objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
            EnemyAiProfileOverrides = enemyAiProfileOverrides ?? Array.Empty<EnemyAiProfileOverride>();
        }

        public BoardBounds BoardBounds { get; }

        public CubeTopologyState InitialTopology { get; }

        public EntityState[] InitialEntities { get; }

        public TerrainData InitialTerrain { get; }

        public int PlayerEntityId { get; }

        public StageObjectiveRuntimeDefinition ObjectiveRuntimeDefinition { get; }

        public EnemyAiProfileOverride[] EnemyAiProfileOverrides { get; }
    }
}
