using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public sealed class GameplayVfxHostCellAnchorProjector : IGameplayVfxCellAnchorProjector
    {
        private readonly Game.Feature.Gameplay.Host.GameplayCubeProjector projector;

        public GameplayVfxHostCellAnchorProjector(Game.Feature.Gameplay.Host.GameplayCubeProjector projector)
        {
            this.projector = projector ?? throw new ArgumentNullException(nameof(projector));
        }

        public bool TryResolveCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            VfxAnchorSlot slot,
            out VfxResolvedAnchor resolvedAnchor)
        {
            if (!IsSupportedSlot(slot) ||
                !projector.TryProjectSurfaceCell(cell, topology, out _))
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }

            resolvedAnchor = VfxResolvedAnchor.ForCell(cell, topology, slot);
            return true;
        }

        private static bool IsSupportedSlot(VfxAnchorSlot slot)
        {
            return slot == VfxAnchorSlot.CellFloor ||
                   slot == VfxAnchorSlot.CellCenter ||
                   slot == VfxAnchorSlot.CellAboveOccupant;
        }
    }
}
