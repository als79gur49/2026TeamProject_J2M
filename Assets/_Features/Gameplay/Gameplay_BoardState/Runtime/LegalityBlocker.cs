namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct LegalityBlocker
    {
        public LegalityBlocker(
            LegalityBlockerKind kind,
            int entityId = 0,
            EntityType? entityType = null,
            SolidKind? solidKind = null,
            int tileId = 0,
            TileFeatureKind? tileFeatureKind = null)
        {
            Kind = kind;
            EntityId = entityId;
            EntityType = entityType;
            SolidKind = solidKind;
            TileId = tileId;
            TileFeatureKind = tileFeatureKind;
        }

        public LegalityBlockerKind Kind { get; }

        public int EntityId { get; }

        public EntityType? EntityType { get; }

        public SolidKind? SolidKind { get; }

        public int TileId { get; }

        public TileFeatureKind? TileFeatureKind { get; }
    }
}
