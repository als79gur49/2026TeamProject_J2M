namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct LegalityBlocker
    {
        public LegalityBlocker(
            LegalityBlockerKind kind,
            int entityId = 0,
            EntityType? entityType = null,
            SolidKind? solidKind = null,
            TerrainFlags terrainFlags = TerrainFlags.None)
        {
            Kind = kind;
            EntityId = entityId;
            EntityType = entityType;
            SolidKind = solidKind;
            TerrainFlags = terrainFlags;
        }

        public LegalityBlockerKind Kind { get; }

        public int EntityId { get; }

        public EntityType? EntityType { get; }

        public SolidKind? SolidKind { get; }

        public TerrainFlags TerrainFlags { get; }
    }
}
