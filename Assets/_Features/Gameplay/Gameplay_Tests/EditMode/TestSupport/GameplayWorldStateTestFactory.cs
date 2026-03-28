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
            return CreateBounded(initialEntities, DefaultBoardBounds, GameplayTerrainData.Empty);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            GameplayTerrainData terrainData)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, terrainData);
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
                new CubeTopologyState(FaceId.Floor));
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                boardBounds,
                terrainData ?? GameplayTerrainData.Empty,
                topology);
        }
    }
}
