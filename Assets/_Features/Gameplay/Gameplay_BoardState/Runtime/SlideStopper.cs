using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public enum SlideStopperKind
    {
        None = 0,
        BoardEdge = 1,
        Terrain = 2,
        Entity = 3,
    }

    public readonly struct SlideStopper
    {
        private SlideStopper(
            SlideStopperKind kind,
            Vector2Int cell,
            int entityId,
            EntityType entityType)
        {
            Kind = kind;
            Cell = cell;
            EntityId = entityId;
            EntityType = entityType;
        }

        public SlideStopperKind Kind { get; }

        public Vector2Int Cell { get; }

        public int EntityId { get; }

        public EntityType EntityType { get; }

        public static SlideStopper CreateBoardEdge(Vector2Int cell)
        {
            return new SlideStopper(SlideStopperKind.BoardEdge, cell, entityId: 0, EntityType.None);
        }

        public static SlideStopper CreateTerrain(Vector2Int cell)
        {
            return new SlideStopper(SlideStopperKind.Terrain, cell, entityId: 0, EntityType.None);
        }

        public static SlideStopper CreateEntity(EntityState entity)
        {
            return new SlideStopper(SlideStopperKind.Entity, entity.position, entity.entityId, entity.type);
        }
    }
}
