using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

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
                !projector.TryProjectSurfaceCell(cell, topology, out var projectedPose))
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }

            var localPosition = projectedPose.LocalPosition;
            var localRotation = projectedPose.LocalRotation;
            if (slot == VfxAnchorSlot.CellFloor)
            {
                localPosition -= projectedPose.Normal * projector.SurfaceTileThickness;
                localRotation *= Quaternion.Euler(180f, 0f, 0f);
            }

            resolvedAnchor = VfxResolvedAnchor.ForCell(
                cell,
                topology,
                slot,
                localPosition,
                localRotation);
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
