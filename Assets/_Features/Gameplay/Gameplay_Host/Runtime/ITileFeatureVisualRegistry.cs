using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

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
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IDestroyTileVisualTarget
    {
        void PlayDestroyTileTriggered();
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IDestroyTileActivatedVisualTarget
    {
        void PlayDestroyTileActivated();
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IDestroyTileDeactivatedVisualTarget
    {
        void PlayDestroyTileDeactivated();
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IDestroyTileActiveStateVisualTarget
    {
        void SetDestroyTileActiveImmediate(bool active);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface ITileFeatureActiveStateVisualTarget
    {
        void SetTileFeatureActiveImmediate(TileFeatureKind kind, bool active);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface ISlideTileVisualTarget
    {
        void PlaySlideTileRedirected(Direction direction, int targetEntityId);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IBarricadeBlockedVisualTarget
    {
        void PlayBarricadeBlocked(Direction direction, int targetEntityId);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IBarricadeCrushedVisualTarget
    {
        void PlayBarricadeCrushed(int targetEntityId);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IBarricadeActivatedVisualTarget
    {
        void PlayBarricadeActivated();
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IBarricadeDeactivatedVisualTarget
    {
        void PlayBarricadeDeactivated();
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IBarricadeActiveStateVisualTarget
    {
        void SetBarricadeActiveImmediate(bool active);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IExitOpenedVisualTarget
    {
        void PlayExitOpened();
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IExitEnteredVisualTarget
    {
        void PlayExitEntered(int playerEntityId);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IExitOpenStateVisualTarget
    {
        void SetExitOpenImmediate(bool open);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IMoonBlockGeneratedVisualTarget
    {
        void PlayMoonBlockGenerated(int moonBlockEntityId);
    }

    [System.Obsolete("Use ITileFeatureVisualCueSink with TileFeatureVisualProfileProvider and TileFeatureVisualProfileCueSink.")]
    public interface IMoonBlockGeneratorBlockedVisualTarget
    {
        void PlayMoonBlockGeneratorBlocked(MoonBlockGeneratorBlockedPayload payload);
    }

    public interface ITileFeatureVisualTargetConfigurator
    {
        void ConfigureTileFeature(int tileId, SurfaceCell cell);
    }
}
