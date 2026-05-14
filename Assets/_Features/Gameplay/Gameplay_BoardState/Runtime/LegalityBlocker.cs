namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct LegalityBlocker
    {
        public LegalityBlocker(
            LegalityBlockerKind kind,
            int entityId = 0,
            EntityType? entityType = null,
            SolidKind? solidKind = null,
            TerrainFlags terrainFlags = TerrainFlags.None,
            int tileId = 0,
            TileFeatureKind? tileFeatureKind = null)
        {
            Kind = kind;
            EntityId = entityId;
            EntityType = entityType;
            SolidKind = solidKind;
            TerrainFlags = terrainFlags;
            TileId = tileId;
            TileFeatureKind = tileFeatureKind;
        }

        public LegalityBlockerKind Kind { get; }

        public int EntityId { get; }

        public EntityType? EntityType { get; }

        public SolidKind? SolidKind { get; }

        public TerrainFlags TerrainFlags { get; }

        public int TileId { get; }

        public TileFeatureKind? TileFeatureKind { get; }
    }
}
