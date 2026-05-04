using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public interface IGameplayVfxCellAnchorProjector
    {
        bool TryResolveCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            VfxAnchorSlot slot,
            out VfxResolvedAnchor resolvedAnchor);
    }
}
