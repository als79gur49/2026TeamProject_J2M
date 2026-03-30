using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayEntityVisualProfile
    {
        private const float DefaultVisibleRevealMultiplier = 0.12f;
        private const float BoxVisibleRevealMultiplier = 0.18f;
        private const float ProjectileVisibleRevealMultiplier = 0.04f;

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
                EntityType.Box => CreateInteriorMountedCube(
                    cellSize,
                    0.78f,
                    0.78f,
                    0.78f,
                    BoxVisibleRevealMultiplier),
                EntityType.Projectile => CreateInteriorMountedCube(
                    cellSize,
                    0.18f,
                    0.48f,
                    0.18f,
                    ProjectileVisibleRevealMultiplier),
                EntityType.None => CreateInteriorMountedCube(
                    cellSize,
                    0.9f,
                    0.9f,
                    1.0f,
                    DefaultVisibleRevealMultiplier),
                _ => CreateInteriorMountedCube(
                    cellSize,
                    0.6f,
                    0.72f,
                    0.72f,
                    DefaultVisibleRevealMultiplier),
            };
        }

        private static GameplayEntityVisualProfile CreateInteriorMountedCube(
            float cellSize,
            float widthMultiplier,
            float lengthMultiplier,
            float heightMultiplier,
            float visibleRevealMultiplier)
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
                Quaternion.identity);
        }
    }
}
