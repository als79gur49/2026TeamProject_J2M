using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public sealed class GameplayVfxHostAnchorResolver : IVfxAnchorResolver
    {
        private readonly IGameplayVfxCellAnchorProjector cellProjector;
        private readonly IGameplayVfxEntityAnchorProjector entityProjector;

        public GameplayVfxHostAnchorResolver(
            IGameplayVfxCellAnchorProjector cellProjector,
            IGameplayVfxEntityAnchorProjector entityProjector,
            IGameplayVfxMotionAnchorProjector motionProjector = null)
        {
            this.cellProjector = cellProjector ?? throw new ArgumentNullException(nameof(cellProjector));
            this.entityProjector = entityProjector ?? throw new ArgumentNullException(nameof(entityProjector));
        }

        public bool TryResolve(in GameplayVfxRequest request, out VfxResolvedAnchor resolvedAnchor)
        {
            var anchor = request.Anchor;
            switch (anchor.Kind)
            {
                case VfxAnchorKind.Cell:
                    return TryResolveCell(anchor.Cell, anchor.Topology, ResolveCellSlot(anchor.Slot), false, out resolvedAnchor);
                case VfxAnchorKind.Entity:
                    return TryResolveEntity(anchor.EntityId, ResolveEntitySlot(anchor.Slot), anchor, out resolvedAnchor);
                case VfxAnchorKind.EntitySlot:
                    return TryResolveEntitySlot(anchor.EntityId, anchor.Slot, anchor, out resolvedAnchor);
                case VfxAnchorKind.CellToEntity:
                    return TryResolveCellToEntity(anchor, out resolvedAnchor);
                case VfxAnchorKind.EntityToCell:
                    return TryResolveEntityToCell(anchor, out resolvedAnchor);
                default:
                    resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                    return false;
            }
        }

        private bool TryResolveCellToEntity(VfxAnchor anchor, out VfxResolvedAnchor resolvedAnchor)
        {
            if (!anchor.HasCell ||
                !cellProjector.TryResolveCell(
                    anchor.Cell,
                    anchor.Topology,
                    VfxAnchorSlot.CellCenter,
                    out var sourceCellAnchor))
            {
                return TryResolveFallbackCell(anchor, out resolvedAnchor);
            }

            if (entityProjector.TryResolveEntity(
                    anchor.TargetEntityId,
                    VfxAnchorSlot.EntityCenter,
                    out _))
            {
                resolvedAnchor = sourceCellAnchor;
                return true;
            }

            return TryResolveFallbackCell(anchor, out resolvedAnchor);
        }

        private bool TryResolveEntityToCell(VfxAnchor anchor, out VfxResolvedAnchor resolvedAnchor)
        {
            if (entityProjector.TryResolveEntity(
                    anchor.SourceEntityId,
                    VfxAnchorSlot.EntityCenter,
                    out resolvedAnchor))
            {
                return true;
            }

            return TryResolveFallbackCell(anchor, out resolvedAnchor);
        }

        private bool TryResolveEntity(
            int entityId,
            VfxAnchorSlot slot,
            VfxAnchor anchor,
            out VfxResolvedAnchor resolvedAnchor)
        {
            if (entityProjector.TryResolveEntity(entityId, slot, out resolvedAnchor))
            {
                return true;
            }

            return TryResolveFallbackCell(anchor, out resolvedAnchor);
        }

        private bool TryResolveEntitySlot(
            int entityId,
            VfxAnchorSlot slot,
            VfxAnchor anchor,
            out VfxResolvedAnchor resolvedAnchor)
        {
            if (slot == VfxAnchorSlot.EntityCenter &&
                entityProjector.TryResolveEntity(entityId, slot, out resolvedAnchor))
            {
                return true;
            }

            return TryResolveFallbackCell(anchor, out resolvedAnchor);
        }

        private bool TryResolveFallbackCell(VfxAnchor anchor, out VfxResolvedAnchor resolvedAnchor)
        {
            if (!anchor.HasFallbackCell)
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }

            return TryResolveCell(
                anchor.FallbackCell,
                anchor.FallbackTopology,
                ResolveCellSlot(anchor.Slot),
                true,
                out resolvedAnchor);
        }

        private bool TryResolveCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            VfxAnchorSlot slot,
            bool usedFallback,
            out VfxResolvedAnchor resolvedAnchor)
        {
            if (!IsSupportedCellSlot(slot) ||
                !cellProjector.TryResolveCell(cell, topology, slot, out resolvedAnchor))
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }

            if (usedFallback)
            {
                resolvedAnchor = VfxResolvedAnchor.ForCell(cell, topology, slot, true);
            }

            return true;
        }

        private static VfxAnchorSlot ResolveCellSlot(VfxAnchorSlot slot)
        {
            return IsSupportedCellSlot(slot)
                ? slot
                : VfxAnchorSlot.CellCenter;
        }

        private static VfxAnchorSlot ResolveEntitySlot(VfxAnchorSlot slot)
        {
            return slot == VfxAnchorSlot.None
                ? VfxAnchorSlot.EntityCenter
                : slot;
        }

        private static bool IsSupportedCellSlot(VfxAnchorSlot slot)
        {
            return slot == VfxAnchorSlot.CellFloor ||
                   slot == VfxAnchorSlot.CellCenter ||
                   slot == VfxAnchorSlot.CellAboveOccupant;
        }
    }
}
