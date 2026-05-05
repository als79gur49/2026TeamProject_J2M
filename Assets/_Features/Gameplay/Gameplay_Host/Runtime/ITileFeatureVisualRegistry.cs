using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Host
{
    public interface ITileFeatureVisualRegistry
    {
        bool TryGetTileVisual(int tileId, out ITileFeatureVisualTarget target);
    }

    public interface ITileFeatureVisualTarget
    {
        int TileId { get; }

        SurfaceCell Cell { get; }

        void PlayButtonActivated();
    }
}
