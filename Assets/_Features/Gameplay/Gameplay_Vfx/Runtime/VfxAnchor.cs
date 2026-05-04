using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct VfxAnchor : IEquatable<VfxAnchor>, IComparable<VfxAnchor>
    {
        private VfxAnchor(
            VfxAnchorKind kind,
            VfxAnchorSlot slot,
            int entityId,
            int sourceEntityId,
            int targetEntityId,
            SurfaceCell cell,
            CubeTopologyState topology,
            bool hasCell,
            int motionTrackId,
            SurfaceCell fallbackCell,
            CubeTopologyState fallbackTopology,
            bool hasFallbackCell)
        {
            Kind = kind;
            Slot = slot;
            EntityId = entityId;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            Cell = cell;
            Topology = topology;
            HasCell = hasCell;
            MotionTrackId = motionTrackId;
            FallbackCell = fallbackCell;
            FallbackTopology = fallbackTopology;
            HasFallbackCell = hasFallbackCell;
        }

        public VfxAnchorKind Kind { get; }

        public VfxAnchorSlot Slot { get; }

        public int EntityId { get; }

        public int SourceEntityId { get; }

        public int TargetEntityId { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public int MotionTrackId { get; }

        public SurfaceCell FallbackCell { get; }

        public CubeTopologyState FallbackTopology { get; }

        public bool HasEntity => EntityId > 0 || SourceEntityId > 0 || TargetEntityId > 0;

        public bool HasCell { get; }

        public bool HasFallbackCell { get; }

        public static VfxAnchor ForCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            VfxAnchorSlot slot = VfxAnchorSlot.CellCenter)
        {
            return new VfxAnchor(
                VfxAnchorKind.Cell,
                slot,
                0,
                0,
                0,
                cell,
                topology,
                true,
                0,
                default,
                default,
                false);
        }

        public static VfxAnchor ForEntity(
            int entityId,
            VfxAnchorSlot slot = VfxAnchorSlot.EntityCenter,
            SurfaceCell fallbackCell = default,
            CubeTopologyState fallbackTopology = default,
            bool hasFallbackCell = false)
        {
            return new VfxAnchor(
                VfxAnchorKind.Entity,
                slot,
                entityId,
                0,
                0,
                default,
                default,
                false,
                0,
                fallbackCell,
                fallbackTopology,
                hasFallbackCell);
        }

        public static VfxAnchor ForEntitySlot(
            int entityId,
            VfxAnchorSlot slot,
            SurfaceCell fallbackCell = default,
            CubeTopologyState fallbackTopology = default,
            bool hasFallbackCell = false)
        {
            return new VfxAnchor(
                VfxAnchorKind.EntitySlot,
                slot,
                entityId,
                0,
                0,
                default,
                default,
                false,
                0,
                fallbackCell,
                fallbackTopology,
                hasFallbackCell);
        }

        public static VfxAnchor ForMotionTrack(
            int motionTrackId,
            VfxAnchorSlot slot = VfxAnchorSlot.MotionPath)
        {
            return new VfxAnchor(
                VfxAnchorKind.MotionTrack,
                slot,
                0,
                0,
                0,
                default,
                default,
                false,
                motionTrackId,
                default,
                default,
                false);
        }

        public static VfxAnchor FromCellToEntity(
            SurfaceCell cell,
            CubeTopologyState topology,
            int targetEntityId)
        {
            return new VfxAnchor(
                VfxAnchorKind.CellToEntity,
                VfxAnchorSlot.MotionPath,
                0,
                0,
                targetEntityId,
                cell,
                topology,
                true,
                0,
                default,
                default,
                false);
        }

        public static VfxAnchor FromEntityToCell(
            int sourceEntityId,
            SurfaceCell cell,
            CubeTopologyState topology)
        {
            return new VfxAnchor(
                VfxAnchorKind.EntityToCell,
                VfxAnchorSlot.MotionPath,
                0,
                sourceEntityId,
                0,
                cell,
                topology,
                true,
                0,
                cell,
                topology,
                true);
        }

        public int CompareTo(VfxAnchor other)
        {
            var kindCompare = Kind.CompareTo(other.Kind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            var slotCompare = Slot.CompareTo(other.Slot);
            if (slotCompare != 0)
            {
                return slotCompare;
            }

            var entityCompare = EntityId.CompareTo(other.EntityId);
            if (entityCompare != 0)
            {
                return entityCompare;
            }

            var sourceCompare = SourceEntityId.CompareTo(other.SourceEntityId);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            var targetCompare = TargetEntityId.CompareTo(other.TargetEntityId);
            if (targetCompare != 0)
            {
                return targetCompare;
            }

            var hasCellCompare = HasCell.CompareTo(other.HasCell);
            if (hasCellCompare != 0)
            {
                return hasCellCompare;
            }

            if (HasCell)
            {
                var cellCompare = VfxOrdering.CompareCell(Cell, other.Cell);
                if (cellCompare != 0)
                {
                    return cellCompare;
                }

                var topologyCompare = VfxOrdering.CompareTopology(Topology, other.Topology);
                if (topologyCompare != 0)
                {
                    return topologyCompare;
                }
            }

            var trackCompare = MotionTrackId.CompareTo(other.MotionTrackId);
            if (trackCompare != 0)
            {
                return trackCompare;
            }

            var fallbackCompare = HasFallbackCell.CompareTo(other.HasFallbackCell);
            if (fallbackCompare != 0)
            {
                return fallbackCompare;
            }

            if (!HasFallbackCell)
            {
                return 0;
            }

            var fallbackCellCompare = VfxOrdering.CompareCell(FallbackCell, other.FallbackCell);
            return fallbackCellCompare != 0
                ? fallbackCellCompare
                : VfxOrdering.CompareTopology(FallbackTopology, other.FallbackTopology);
        }

        public bool Equals(VfxAnchor other)
        {
            return Kind == other.Kind
                && Slot == other.Slot
                && EntityId == other.EntityId
                && SourceEntityId == other.SourceEntityId
                && TargetEntityId == other.TargetEntityId
                && HasCell == other.HasCell
                && (!HasCell || (Cell.Equals(other.Cell) && Topology.Equals(other.Topology)))
                && MotionTrackId == other.MotionTrackId
                && HasFallbackCell == other.HasFallbackCell
                && (!HasFallbackCell || (FallbackCell.Equals(other.FallbackCell) && FallbackTopology.Equals(other.FallbackTopology)));
        }

        public override bool Equals(object obj)
        {
            return obj is VfxAnchor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ (int)Slot;
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ HasCell.GetHashCode();
                if (HasCell)
                {
                    hash = (hash * 397) ^ Cell.GetHashCode();
                    hash = (hash * 397) ^ Topology.GetHashCode();
                }

                hash = (hash * 397) ^ MotionTrackId;
                hash = (hash * 397) ^ HasFallbackCell.GetHashCode();
                if (HasFallbackCell)
                {
                    hash = (hash * 397) ^ FallbackCell.GetHashCode();
                    hash = (hash * 397) ^ FallbackTopology.GetHashCode();
                }

                return hash;
            }
        }

        public static bool operator ==(VfxAnchor left, VfxAnchor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VfxAnchor left, VfxAnchor right)
        {
            return !left.Equals(right);
        }
    }
}
