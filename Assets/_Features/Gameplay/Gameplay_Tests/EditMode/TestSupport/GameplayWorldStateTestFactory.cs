using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
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
            return CreateBounded(initialEntities, DefaultBoardBounds, GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                topology,
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                topology,
                timingProfile,
                initialTileFeatures: null);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(
                initialEntities,
                timingProfile ?? GameplayTimingProfile.CreateDefault());
            DebugSpawnValidityPolicy.EnsureRepresentable(
                boardBounds,
                normalizedInitialEntities);

            return GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                boardBounds,
                topology,
                initialTileFeatures);
        }
    }
}
