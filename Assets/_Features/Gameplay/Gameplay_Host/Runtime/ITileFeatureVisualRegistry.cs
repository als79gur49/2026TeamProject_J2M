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

    public interface IDestroyTileVisualTarget
    {
        void PlayDestroyTileTriggered();
    }

    public interface ISlideTileVisualTarget
    {
        void PlaySlideTileRedirected(Direction direction, int targetEntityId);
    }

    public interface IBarricadeBlockedVisualTarget
    {
        void PlayBarricadeBlocked(Direction direction, int targetEntityId);
    }

    public interface IBarricadeCrushedVisualTarget
    {
        void PlayBarricadeCrushed(int targetEntityId);
    }

    public interface ITileFeatureVisualTargetConfigurator
    {
        void ConfigureTileFeature(int tileId, SurfaceCell cell);
    }
}
