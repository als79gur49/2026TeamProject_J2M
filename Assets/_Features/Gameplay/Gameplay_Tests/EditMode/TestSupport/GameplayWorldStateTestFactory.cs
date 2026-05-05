using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    internal static class GameplayWorldStateTestFactory
    {
        private static readonly BoardBounds DefaultBoardBounds = new(
            new Vector2Int(-32, -32),
            new Vector2Int(32, 32));

        public static WorldState CreateBounded(IEnumerable<EntityState> initialEntities)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, GameplayTerrainData.Empty, GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            GameplayTerrainData terrainData)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, terrainData, GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, GameplayTerrainData.Empty, timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                topology,
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                topology,
                timingProfile,
                initialTileFeatures: null);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(
                initialEntities,
                timingProfile ?? GameplayTimingProfile.CreateDefault());
            var resolvedTerrain = terrainData ?? GameplayTerrainData.Empty;
            DebugSpawnValidityPolicy.EnsureRepresentable(
                boardBounds,
                resolvedTerrain,
                normalizedInitialEntities);

            return GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                boardBounds,
                resolvedTerrain,
                topology,
                initialTileFeatures);
        }
    }
}
