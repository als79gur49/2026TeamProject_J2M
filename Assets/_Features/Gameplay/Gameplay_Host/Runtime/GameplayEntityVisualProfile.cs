using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayEntityVisualProfile
    {
        private const float DefaultSurfaceOffsetMultiplier = 0.08f;
        private const float ProjectileSurfaceOffsetMultiplier = 0.18f;

        public GameplayEntityVisualProfile(
            Vector3 modelLocalScale,
            Vector3 modelLocalPosition,
            Quaternion modelLocalRotation)
        {
            ModelLocalScale = modelLocalScale;
            ModelLocalPosition = modelLocalPosition;
            ModelLocalRotation = modelLocalRotation;
        }

        public Vector3 ModelLocalScale { get; }

        public Vector3 ModelLocalPosition { get; }

        public Quaternion ModelLocalRotation { get; }

        public static GameplayEntityVisualProfile Create(EntityType entityType, float cellSize)
        {
            if (cellSize <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            return entityType switch
            {
                EntityType.Box => CreateExtrudedCube(cellSize, 0.78f, 0.78f, 0.78f, DefaultSurfaceOffsetMultiplier),
                EntityType.Projectile => CreateExtrudedCube(cellSize, 0.18f, 0.48f, 0.18f, ProjectileSurfaceOffsetMultiplier),
                EntityType.None => CreateExtrudedCube(cellSize, 0.9f, 0.9f, 1.0f, DefaultSurfaceOffsetMultiplier),
                _ => CreateExtrudedCube(cellSize, 0.6f, 0.72f, 0.72f, DefaultSurfaceOffsetMultiplier),
            };
        }

        private static GameplayEntityVisualProfile CreateExtrudedCube(
            float cellSize,
            float widthMultiplier,
            float lengthMultiplier,
            float heightMultiplier,
            float projectorSurfaceOffsetMultiplier)
        {
            var modelLocalScale = new Vector3(
                cellSize * widthMultiplier,
                cellSize * lengthMultiplier,
                cellSize * heightMultiplier);
            var projectorSurfaceOffset = cellSize * projectorSurfaceOffsetMultiplier;
            var outwardLift = Mathf.Max(0f, (modelLocalScale.z * 0.5f) - projectorSurfaceOffset);

            return new GameplayEntityVisualProfile(
                modelLocalScale,
                Vector3.forward * outwardLift,
                Quaternion.identity);
        }
    }
}
