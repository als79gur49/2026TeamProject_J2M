using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct VfxResolvedAnchor
    {
        private VfxResolvedAnchor(
            bool isResolved,
            VfxAnchorKind kind,
            VfxAnchorSlot slot,
            int entityId,
            SurfaceCell cell,
            CubeTopologyState topology,
            bool hasLocalPose,
            Vector3 localPosition,
            Quaternion localRotation,
            bool usedFallback,
            VfxMissingAnchorPolicy missingPolicy)
        {
            IsResolved = isResolved;
            Kind = kind;
            Slot = slot;
            EntityId = entityId;
            Cell = cell;
            Topology = topology;
            HasLocalPose = hasLocalPose;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            UsedFallback = usedFallback;
            MissingPolicy = missingPolicy;
        }

        public bool IsResolved { get; }

        public VfxAnchorKind Kind { get; }

        public VfxAnchorSlot Slot { get; }

        public int EntityId { get; }

        public bool HasEntity => IsResolved && EntityId > 0;

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public bool HasCell => IsResolved && Kind == VfxAnchorKind.Cell;

        public bool HasLocalPose { get; }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public bool UsedFallback { get; }

        public VfxMissingAnchorPolicy MissingPolicy { get; }

        public static VfxResolvedAnchor Unresolved(VfxMissingAnchorPolicy policy)
        {
            return new VfxResolvedAnchor(
                false,
                VfxAnchorKind.None,
                VfxAnchorSlot.None,
                0,
                default,
                default,
                false,
                default,
                Quaternion.identity,
                false,
                policy);
        }

        public static VfxResolvedAnchor ForCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            VfxAnchorSlot slot,
            bool usedFallback = false)
        {
            return new VfxResolvedAnchor(
                true,
                VfxAnchorKind.Cell,
                slot,
                0,
                cell,
                topology,
                false,
                default,
                Quaternion.identity,
                usedFallback,
                VfxMissingAnchorPolicy.SkipOptional);
        }

        public static VfxResolvedAnchor ForCell(
            SurfaceCell cell,
            CubeTopologyState topology,
            VfxAnchorSlot slot,
            Vector3 localPosition,
            Quaternion localRotation,
            bool usedFallback = false)
        {
            return new VfxResolvedAnchor(
                true,
                VfxAnchorKind.Cell,
                slot,
                0,
                cell,
                topology,
                true,
                localPosition,
                localRotation,
                usedFallback,
                VfxMissingAnchorPolicy.SkipOptional);
        }

        public static VfxResolvedAnchor ForEntity(
            int entityId,
            VfxAnchorSlot slot,
            SurfaceCell fallbackCell,
            CubeTopologyState fallbackTopology,
            bool usedFallback = false)
        {
            return new VfxResolvedAnchor(
                true,
                VfxAnchorKind.Entity,
                slot,
                entityId,
                fallbackCell,
                fallbackTopology,
                false,
                default,
                Quaternion.identity,
                usedFallback,
                VfxMissingAnchorPolicy.SkipOptional);
        }
    }
}
