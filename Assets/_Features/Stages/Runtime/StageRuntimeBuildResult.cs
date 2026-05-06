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
            TileFeatureState[] initialTileFeatures,
            TileFeatureRuntimeDefinition[] tileFeatureDefinitions,
            MoonBlockRespawnDefinition[] moonBlockRespawnDefinitions,
            int playerEntityId,
            StageObjectiveRuntimeDefinition objectiveRuntimeDefinition,
            EnemyAiProfileOverride[] enemyAiProfileOverrides)
        {
            BoardBounds = boardBounds;
            InitialTopology = initialTopology;
            InitialEntities = initialEntities ?? Array.Empty<EntityState>();
            InitialTerrain = initialTerrain ?? TerrainData.Empty;
            InitialTileFeatures = initialTileFeatures ?? Array.Empty<TileFeatureState>();
            TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
            MoonBlockRespawnDefinitions = moonBlockRespawnDefinitions ?? Array.Empty<MoonBlockRespawnDefinition>();
            PlayerEntityId = playerEntityId;
            ObjectiveRuntimeDefinition = objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
            EnemyAiProfileOverrides = enemyAiProfileOverrides ?? Array.Empty<EnemyAiProfileOverride>();
        }

        public BoardBounds BoardBounds { get; }

        public CubeTopologyState InitialTopology { get; }

        public EntityState[] InitialEntities { get; }

        public TerrainData InitialTerrain { get; }

        public TileFeatureState[] InitialTileFeatures { get; }

        public TileFeatureRuntimeDefinition[] TileFeatureDefinitions { get; }

        public MoonBlockRespawnDefinition[] MoonBlockRespawnDefinitions { get; }

        public int PlayerEntityId { get; }

        public StageObjectiveRuntimeDefinition ObjectiveRuntimeDefinition { get; }

        public EnemyAiProfileOverride[] EnemyAiProfileOverrides { get; }
    }
}
