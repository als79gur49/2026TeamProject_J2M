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

    public interface IBarricadeActivatedVisualTarget
    {
        void PlayBarricadeActivated();
    }

    public interface IBarricadeDeactivatedVisualTarget
    {
        void PlayBarricadeDeactivated();
    }

    public interface IBarricadeActiveStateVisualTarget
    {
        void SetBarricadeActiveImmediate(bool active);
    }

    public interface IExitOpenedVisualTarget
    {
        void PlayExitOpened();
    }

    public interface IExitEnteredVisualTarget
    {
        void PlayExitEntered(int playerEntityId);
    }

    public interface IMoonBlockGeneratedVisualTarget
    {
        void PlayMoonBlockGenerated(int moonBlockEntityId);
    }

    public interface ITileFeatureVisualTargetConfigurator
    {
        void ConfigureTileFeature(int tileId, SurfaceCell cell);
    }
}
