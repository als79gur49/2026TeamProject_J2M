using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public static class UnitKinematicFootprintResolver
    {
        public static void EnumerateTouchedCells(in UnitKinematicPose pose, List<SurfaceCell> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            AddUnique(buffer, pose.AnchorCell);
        }

        internal static void EnumerateSweptCells(
            in UnitKinematicPose pose,
            SimulationVelocity2 delta,
            List<SurfaceCell> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            AddUnique(buffer, pose.AnchorCell);
            if (TryResolveAnchorDelta(pose.LocalOffset, delta, out var anchorDelta))
            {
                AddUnique(buffer, pose.AnchorCell + anchorDelta);
            }
        }

        internal static bool TryResolveAnchorDelta(
            SimulationOffset2 localOffset,
            SimulationVelocity2 delta,
            out Vector2Int anchorDelta)
        {
            var targetX = checked(localOffset.X.RawValue + delta.X.RawValue);
            var targetY = checked(localOffset.Y.RawValue + delta.Y.RawValue);
            var stepX = ResolveAxisAnchorDelta(targetX);
            var stepY = ResolveAxisAnchorDelta(targetY);

            anchorDelta = new Vector2Int(stepX, stepY);
            return stepX != 0 || stepY != 0;
        }

        private static int ResolveAxisAnchorDelta(int rawTargetOffset)
        {
            if (rawTargetOffset >= KinematicFixed.HalfCellUnits)
            {
                return 1;
            }

            if (rawTargetOffset < -KinematicFixed.HalfCellUnits)
            {
                return -1;
            }

            return 0;
        }

        private static void AddUnique(List<SurfaceCell> buffer, SurfaceCell cell)
        {
            for (var i = 0; i < buffer.Count; i++)
            {
                if (buffer[i] == cell)
                {
                    return;
                }
            }

            buffer.Add(cell);
        }
    }
}
