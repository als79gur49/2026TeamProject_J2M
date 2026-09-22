using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayEntityVisualProfile
    {
        private const float DefaultVisibleRevealMultiplier = 0.12f;
        private const float BoxVisibleRevealMultiplier = 0.18f;
        private const float ProjectileVisibleRevealMultiplier = 0.04f;
        private const float ProjectileSurfaceOffsetMultiplier = 0.18f;

        public GameplayEntityVisualProfile(
            Vector3 modelLocalScale,
            Vector3 modelLocalPosition,
            Quaternion modelLocalRotation,
            float surfaceOffsetFromFacePlane)
        {
            ModelLocalScale = modelLocalScale;
            ModelLocalPosition = modelLocalPosition;
            ModelLocalRotation = modelLocalRotation;
            SurfaceOffsetFromFacePlane = surfaceOffsetFromFacePlane;
        }

        public Vector3 ModelLocalScale { get; }

        public Vector3 ModelLocalPosition { get; }

        public Quaternion ModelLocalRotation { get; }

        public float SurfaceOffsetFromFacePlane { get; }

        public static GameplayEntityVisualProfile Create(EntityType entityType, float cellSize)
        {
            if (cellSize <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            return entityType switch
            {
                EntityType.Box => CreateInteriorMountedCube(
                    cellSize,
                    1f,
                    1f,
                    0.5f,
                    BoxVisibleRevealMultiplier,
                    ResolveSurfaceOffsetMultiplier(entityType)),
                EntityType.None or EntityType.Wall => CreateInteriorMountedCube(
                    cellSize,
                    1f,
                    1f,
                    0.5f,
                    DefaultVisibleRevealMultiplier,
                    ResolveSurfaceOffsetMultiplier(entityType)),
                _ => CreateInteriorMountedCube(
                    cellSize,
                    0.6f,
                    0.72f,
                    0.72f,
                    DefaultVisibleRevealMultiplier,
                    ResolveSurfaceOffsetMultiplier(entityType)),
            };
        }

        public static float ResolveSurfaceOffsetFromFacePlane(EntityType entityType, float cellSize)
        {
            if (cellSize <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            return cellSize * ResolveSurfaceOffsetMultiplier(entityType);
        }

        private static GameplayEntityVisualProfile CreateInteriorMountedCube(
            float cellSize,
            float widthMultiplier,
            float lengthMultiplier,
            float heightMultiplier,
            float visibleRevealMultiplier,
            float surfaceOffsetMultiplier)
        {
            var modelLocalScale = new Vector3(
                cellSize * widthMultiplier,
                cellSize * lengthMultiplier,
                cellSize * heightMultiplier);
            var interiorMountOffset = Mathf.Max(
                0f,
                (modelLocalScale.z * 0.5f) - (cellSize * visibleRevealMultiplier));

            return new GameplayEntityVisualProfile(
                modelLocalScale,
                Vector3.back * interiorMountOffset,
                Quaternion.identity,
                cellSize * surfaceOffsetMultiplier);
        }

        private static float ResolveSurfaceOffsetMultiplier(EntityType entityType)
        {
            return entityType switch
            {
                EntityType.Box => GameplayPresentationGeometry.TileThicknessMultiplier + BoxVisibleRevealMultiplier,
                EntityType.None or EntityType.Wall => GameplayPresentationGeometry.TileThicknessMultiplier + DefaultVisibleRevealMultiplier,
                _ => GameplayPresentationGeometry.TileThicknessMultiplier + DefaultVisibleRevealMultiplier,
            };
        }
    }
}
