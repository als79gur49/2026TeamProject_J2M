namespace Game.Feature.Gameplay
{
    public readonly struct TileFeatureVfxStyleBinding
    {
        public TileFeatureVfxStyleBinding(int tileId, VfxStyleKey styleKey)
        {
            TileId = tileId;
            StyleKey = styleKey;
        }

        public int TileId { get; }

        public VfxStyleKey StyleKey { get; }
    }
}
