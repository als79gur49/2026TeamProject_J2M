using System;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public enum UnitProbeRejectionReason
    {
        None = 0,
        MissingPose = 1,
        NotSettledAtAnchor = 2,
        InactiveFace = 3,
    }

    public readonly struct UnitProbeCellResult
    {
        public UnitProbeCellResult(SurfaceCell anchorCell, SurfaceCell probeCell, UnitProbeRejectionReason rejectedBy)
        {
            AnchorCell = anchorCell;
            ProbeCell = probeCell;
            RejectedBy = rejectedBy;
        }

        public SurfaceCell AnchorCell { get; }

        public SurfaceCell ProbeCell { get; }

        public UnitProbeRejectionReason RejectedBy { get; }

        public bool Accepted => RejectedBy == UnitProbeRejectionReason.None;
    }

    public static class UnitSpatialQuery
    {
        public static bool IsSettledAtAnchor(WorldSnapshot snapshot, int entityId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return snapshot.TryGetUnitKinematicPose(entityId, out var pose) &&
                   pose.IsSettledAtAnchor &&
                   snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var continuousPose) &&
                   continuousPose.IsSettledAtAnchor;
        }

        public static bool TryResolveSettledProbeCell(
            WorldSnapshot snapshot,
            int entityId,
            Direction facing,
            out UnitProbeCellResult result)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.TryGetUnitKinematicPose(entityId, out var pose))
            {
                result = new UnitProbeCellResult(default, default, UnitProbeRejectionReason.MissingPose);
                return false;
            }

            if (!pose.IsSettledAtAnchor ||
                !snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var continuousPose) ||
                !continuousPose.IsSettledAtAnchor)
            {
                result = new UnitProbeCellResult(pose.AnchorCell, default, UnitProbeRejectionReason.NotSettledAtAnchor);
                return false;
            }

            if (!snapshot.Topology.IsFaceActive(pose.AnchorCell.face))
            {
                result = new UnitProbeCellResult(pose.AnchorCell, default, UnitProbeRejectionReason.InactiveFace);
                return false;
            }

            result = new UnitProbeCellResult(
                pose.AnchorCell,
                pose.AnchorCell + DirectionToDelta(facing),
                UnitProbeRejectionReason.None);
            return true;
        }

        private static Vector2Int DirectionToDelta(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2Int.up,
                Direction.Right => Vector2Int.right,
                Direction.Down => Vector2Int.down,
                Direction.Left => Vector2Int.left,
                _ => throw new InvalidOperationException("Unit probe queries require a cardinal direction."),
            };
        }
    }
}
